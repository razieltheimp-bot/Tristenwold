using Fahrenheit.Events;
using Tristinwold.Nemesis;

using NemesisEntry = Tristinwold.Nemesis.Nemesis;

namespace Tristinwold.Mods.Nemesis;

public sealed class NemesisSettings : FhSettingProvider<NemesisModule> {
    protected override IEnumerable<FhSetting> get() => [ enable, player_name, show_toasts ];

    public readonly FhSettingToggle enable      = new("enable", true);
    public readonly FhSettingText   player_name = new("player_name", "Player");
    public readonly FhSettingToggle show_toasts = new("show_toasts", true);
}

/// <summary>
///     Puts the nemesis simulation into real FFX battles: picks which monsters become
///     nemeses when a battle starts, buffs and renames them, and reports how the battle
///     ended. The world is saved with each save file.
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed partial class NemesisModule : FhModule {
    private const int MONSTER_SLOTS     = 8;
    private const int MONSTER_CHR_BASE  = 20;   // battle chr ids 20-27 are the monster slots
    private const int NAME_BUFFER_BYTES = 40;   // ChrRam.name

    // Battle state values observed in the Archipelago client (ap-client hooks.cs / deathlink.cs).
    private const byte BTL_STATE_STARTED  = 13;
    private const byte BTL_STATE_GAMEOVER = 0x17;
    private const byte BTL_STATE_ENDING   = 0x21;

    private readonly NemesisSettings _settings = new();

    /// <summary>
    ///     Everything below is touched by game hooks (game thread) and by the roster window
    ///     (render thread); hold this lock for any access.
    /// </summary>
    private readonly Lock _lock = new();
    private NemesisWorld _world = new_world();
    private BattleSession? _battle;

    /// <summary>
    ///     Which player controls a party character (battle chr id 0-17). The multiplayer
    ///     module replaces this; on its own every character belongs to the local player.
    /// </summary>
    public Func<int, string?> controller_of { get; set; }

    public NemesisModule() {
        controller_of = _ => _settings.player_name.get();
    }

    private sealed class BattleSession {
        public required string                        area;
        public required List<FormationSlot>           formation;
        public          Dictionary<int, NemesisSpawn> spawns  = [];
        public          Dictionary<int, int>          killers = [];   // monster slot -> party chr id
        public          bool                          reported;
    }

    private static NemesisWorld new_world() => new((ulong)DateTime.UtcNow.Ticks);

    public override bool init(FhModContext mod_context, FileStream global_state_file) {
        return FhXCall.MsBtlReadManage     .hook(this, h_MsBtlReadManage)
            && FhXCall.MsDamageCheckDeath  .hook(this, h_MsDamageCheckDeath)
            && FhXCall.MsGetBattleEndStatus.hook(this, h_MsGetBattleEndStatus)
            && FhApi.Events.Common.GameLoop.PreUpdate        .subscribe(pre_update)
            && FhApi.Events.Common.GameLoop.PostReturnToTitle.subscribe(_ => reset_world());
    }

    // ---- Public surface for other Tristinwold modules (multiplayer, UI) --------------------

    /// <summary>Runs <paramref name="fn"/> with the world under the module's lock.</summary>
    public T with_world<T>(Func<NemesisWorld, T> fn) {
        lock (_lock) return fn(_world);
    }

    /// <summary>Replaces the whole world, e.g. with the host's copy in multiplayer.</summary>
    public void replace_world(string json) {
        lock (_lock) _world = NemesisWorld.FromJson(json);
    }

    public event Action<IReadOnlyList<WorldEvent>>? world_events;

    /// <summary>
    ///     Set by the multiplayer module: battle results go to the shared server instead of
    ///     the local world, and resting tells the server to advance the day.
    /// </summary>
    public Action<BattleReport>? remote_report { get; set; }
    public Action?               remote_rested { get; set; }

    public string player_name => _settings.player_name.get();

    /// <summary>Shows events that happened elsewhere (another table's game) as toasts.</summary>
    public void show_remote_events(IReadOnlyList<WorldEvent> events) => publish(events);

    public void toast(string text) => _toasts.add(text);

    private void reset_world() {
        lock (_lock) {
            _world  = new_world();
            _battle = null;
        }
    }

    // ---- Save integration ---------------------------------------------------------------

    public override void save_local_state(FileStream local_state_file) {
        string json;
        IReadOnlyList<WorldEvent> events;
        Action? rested = remote_rested;
        lock (_lock) {
            // Saving is resting: the nemeses scheme while the party sleeps. In a shared world
            // the server owns the calendar, so ask it instead.
            events = rested is null ? _world.AdvanceDay() : [];
            json   = _world.ToJson();
        }
        rested?.Invoke();
        publish(events);
        local_state_file.Write(Encoding.UTF8.GetBytes(json));
    }

    public override void load_local_state(FileStream local_state_file, FhLocalStateInfo local_state_info) {
        using StreamReader reader = new(local_state_file, Encoding.UTF8, leaveOpen: true);
        string json = reader.ReadToEnd();
        try {
            lock (_lock) {
                _world  = NemesisWorld.FromJson(json);
                _battle = null;
            }
        }
        catch (Exception ex) {
            _logger.Error($"Could not read nemesis save data, starting a fresh world: {ex.Message}");
            reset_world();
        }
    }

    // ---- Battle start: plan and apply nemeses --------------------------------------------

    [UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
    private void h_MsBtlReadManage() {
        byte old_state = Globals.Battle.btl->battle_state;
        FhXCall.MsBtlReadManage.chain_from(h_MsBtlReadManage).fnptr!();

        Btl* btl = Globals.Battle.btl;
        if (btl->battle_state != BTL_STATE_STARTED || old_state == BTL_STATE_STARTED) return;
        if (!_settings.enable.get() || Globals.Battle.monster_characters == null) return;

        try {
            start_battle(btl);
        }
        catch (Exception ex) {
            _logger.Error($"Nemesis battle setup failed: {ex}");
        }
    }

    private void start_battle(Btl* btl) {
        string area = read_ascii((byte*)&btl->field_name, 14);
        List<FormationSlot> formation = [];
        for (int slot = 0; slot < MONSTER_SLOTS; slot++) {
            Chr* chr = Globals.Battle.monster_characters + slot;
            if (!chr->stat_exist_flag) continue;
            formation.Add(new FormationSlot(slot, monster_id(chr), read_game_text(chr->ram.name)));
        }
        if (formation.Count == 0) return;

        List<string> players = current_players();
        lock (_lock) {
            BattleSession session = new() { area = area, formation = formation };
            foreach (NemesisSpawn spawn in _world.PlanEncounter(area, formation, players)) {
                apply(Globals.Battle.monster_characters + spawn.Slot, spawn.Modifiers);
                session.spawns[spawn.Slot] = spawn;
                _toasts.add(spawn.Taunt is null ? spawn.Modifiers.DisplayName : $"{spawn.Modifiers.DisplayName}: \"{spawn.Taunt}\"");
                _logger.Info($"Nemesis {spawn.Modifiers.DisplayName} takes monster slot {spawn.Slot} in {area}.");
            }
            _battle = session;
        }
    }

    private static int monster_id(Chr* chr) =>
        chr->ptr_base_stats != null ? chr->ptr_base_stats->monster_idx : chr->chr_id;

    private static void apply(Chr* chr, BattleModifiers mods) {
        ref ChrRam ram = ref chr->ram;

        ram.max_hp      = mods.Apply(Stat.Hp, ram.max_hp);
        ram.hp          = ram.max_hp;
        ram.current_hp  = ram.max_hp;
        ram.max_mp      = mods.Apply(Stat.Mp, ram.max_mp);
        ram.mp          = ram.max_mp;
        ram.current_mp  = ram.max_mp;
        ram.overkill_threshold = mods.Apply(Stat.Hp, ram.overkill_threshold);

        ram.strength      = (byte)mods.Apply(Stat.Strength,     ram.strength);
        ram.defense       = (byte)mods.Apply(Stat.Defense,      ram.defense);
        ram.magic         = (byte)mods.Apply(Stat.Magic,        ram.magic);
        ram.magic_defense = (byte)mods.Apply(Stat.MagicDefense, ram.magic_defense);
        ram.agility       = (byte)mods.Apply(Stat.Agility,      ram.agility);
        ram.luck          = (byte)mods.Apply(Stat.Luck,         ram.luck);
        ram.evasion       = (byte)mods.Apply(Stat.Evasion,      ram.evasion);
        ram.accuracy      = (byte)mods.Apply(Stat.Accuracy,     ram.accuracy);

        foreach ((DamageKind kind, Affinity affinity) in mods.Affinities) {
            ElementFlags flag = element_flag(kind);
            if (flag == ElementFlags.NONE) continue;
            ram.elem_absorb &= ~flag;
            ram.elem_ignore &= ~flag;
            ram.elem_resist &= ~flag;
            ram.elem_weak   &= ~flag;
            switch (affinity) {
                case Affinity.Absorb: ram.elem_absorb |= flag; break;
                case Affinity.Immune: ram.elem_ignore |= flag; break;
                case Affinity.Resist: ram.elem_resist |= flag; break;
                case Affinity.Weak:   ram.elem_weak   |= flag; break;
            }
        }

        foreach (AutoStatus status in mods.AutoStatuses)
            ram.status_auto_innate_temporal |= status_flag(status);

        write_game_text(ref ram.name, mods.DisplayName);
    }

    private static ElementFlags element_flag(DamageKind kind) => kind switch {
        DamageKind.Fire      => ElementFlags.FIRE,
        DamageKind.Ice       => ElementFlags.ICE,
        DamageKind.Lightning => ElementFlags.THUNDER,
        DamageKind.Water     => ElementFlags.WATER,
        DamageKind.Holy      => ElementFlags.HOLY,
        _                    => ElementFlags.NONE,
    };

    private static StatusTemporalFlags status_flag(AutoStatus status) => status switch {
        AutoStatus.Haste    => StatusTemporalFlags.HASTE,
        AutoStatus.Protect  => StatusTemporalFlags.PROTECT,
        AutoStatus.Shell    => StatusTemporalFlags.SHELL,
        AutoStatus.Regen    => StatusTemporalFlags.REGEN,
        AutoStatus.Reflect  => StatusTemporalFlags.REFLECT,
        AutoStatus.NulTide  => StatusTemporalFlags.NUL_WATER,
        AutoStatus.NulBlaze => StatusTemporalFlags.NUL_FIRE,
        AutoStatus.NulShock => StatusTemporalFlags.NUL_THUNDER,
        AutoStatus.NulFrost => StatusTemporalFlags.NUL_ICE,
        _                   => StatusTemporalFlags.NONE,
    };

    // ---- During battle: who landed killing blows ----------------------------------------------

    [UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
    private int h_MsDamageCheckDeath(int attacker_id, int target_id, int arg3, int targeting_self) {
        int result = FhXCall.MsDamageCheckDeath.chain_from(h_MsDamageCheckDeath).fnptr!(attacker_id, target_id, arg3, targeting_self);
        int slot   = target_id - MONSTER_CHR_BASE;
        if (result != 0 && slot is >= 0 and < MONSTER_SLOTS) {
            lock (_lock) {
                if (_battle is not null) _battle.killers[slot] = attacker_id;
            }
        }
        return result;
    }

    // ---- Battle end: report the outcome -------------------------------------------------------

    [UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
    private uint h_MsGetBattleEndStatus() {
        uint status = FhXCall.MsGetBattleEndStatus.chain_from(h_MsGetBattleEndStatus).fnptr!();
        if (status == (uint)BattleResult.Defeat && Globals.Battle.btl->battle_state == BTL_STATE_GAMEOVER)
            finish_battle(BattleResult.Defeat);
        return status;
    }

    private void pre_update(UpdateLoopEventArgs args) {
        Btl* btl = Globals.Battle.btl;
        if (btl->battle_state == 0) {
            lock (_lock) _battle = null;
            return;
        }
        // battle_end_type follows BtlResultId: 2 win, 3 party escaped, 4 monsters escaped.
        if (btl->battle_state == BTL_STATE_ENDING && btl->battle_end_type >= 2)
            finish_battle((BattleResult)btl->battle_end_type);
    }

    private void finish_battle(BattleResult result) {
        IReadOnlyList<WorldEvent> events;
        BattleReport report;
        Action<BattleReport>? remote = remote_report;
        lock (_lock) {
            if (_battle is null || _battle.reported) return;
            _battle.reported = true;

            List<EnemyReport> enemies = [];
            foreach (FormationSlot slot in _battle.formation) {
                Chr* chr = Globals.Battle.monster_characters + slot.Slot;
                _battle.spawns.TryGetValue(slot.Slot, out NemesisSpawn? spawn);
                string? killer = _battle.killers.TryGetValue(slot.Slot, out int attacker) ? controller_of(attacker) : null;
                enemies.Add(new EnemyReport(
                    Slot:        slot.Slot,
                    MonsterId:   slot.MonsterId,
                    Species:     slot.Species,
                    MaxHp:       chr->ram.max_hp,
                    Killed:      chr->stat_death != 0 || chr->ram.current_hp <= 0,
                    Escaped:     chr->stat_escape_flag,
                    NemesisId:   spawn?.NemesisId,
                    KillingBlow: null,   // TODO: read the element of the killing command
                    KilledBy:    killer));
            }
            report = new(_battle.area, result, enemies, current_players(), party_level());
            events = remote is null ? _world.RecordBattle(report) : [];
        }
        // The server's answer comes back as a fresh world snapshot plus events.
        remote?.Invoke(report);
        publish(events);
    }

    private List<string> current_players() {
        List<string> players = [];
        for (int i = 0; i < 3; i++) {
            byte chr_id = Globals.save_data->party_frontline[i];
            if (chr_id == 0xFF) continue;
            string? player = controller_of(chr_id);
            if (player is not null && !players.Contains(player)) players.Add(player);
        }
        if (players.Count == 0) players.Add(_settings.player_name.get());
        return players;
    }

    /// <summary>A rough party strength for scaling newborn nemeses: average front-line max HP / 100.</summary>
    private static int party_level() {
        Chr* party = Globals.Battle.player_characters;
        if (party == null) return 1;
        int total = 0, count = 0;
        for (int i = 0; i < 3; i++) {
            byte chr_id = Globals.save_data->party_frontline[i];
            if (chr_id == 0xFF || chr_id > 7) continue;
            total += party[chr_id].ram.max_hp;
            count++;
        }
        return count == 0 ? 1 : Math.Max(1, total / count / 100);
    }

    private void publish(IReadOnlyList<WorldEvent> events) {
        if (events.Count == 0) return;
        foreach (WorldEvent e in events) {
            _logger.Info(e.Text);
            if (_settings.show_toasts.get()) _toasts.add(e.Text);
        }
        world_events?.Invoke(events);
    }

    // ---- Game text helpers ----------------------------------------------------------------

    private static string read_ascii(byte* ptr, int max) {
        int len = 0;
        while (len < max && ptr[len] != 0) len++;
        return Encoding.ASCII.GetString(ptr, len);
    }

    private static string read_game_text(in ChrRam.ChrName name) {
        ReadOnlySpan<byte> src = name;
        int end = src.IndexOf((byte)0);
        if (end >= 0) src = src[..end];
        Span<byte> utf8 = stackalloc byte[FhEncoding.compute_decode_buffer_size(src)];
        int written = FhEncoding.decode(src, utf8);
        return Encoding.UTF8.GetString(utf8[..written]);
    }

    private static void write_game_text(ref ChrRam.ChrName name, string text) {
        Span<byte> dest = name;
        ReadOnlySpan<byte> utf8 = Encoding.UTF8.GetBytes(text);
        // Shorten until the encoded name and its terminator fit the buffer.
        while (utf8.Length > 0 && FhEncoding.compute_encode_buffer_size(utf8) >= NAME_BUFFER_BYTES)
            utf8 = utf8[..^1];
        dest.Clear();
        FhEncoding.encode(utf8, dest[..(NAME_BUFFER_BYTES - 1)]);
    }
}
