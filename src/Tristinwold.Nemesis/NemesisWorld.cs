namespace Tristinwold.Nemesis;

/// <summary>Tuning knobs. Defaults aim for "a nemesis shows up every few battles".</summary>
public sealed class NemesisRules
{
    public int MaxWarchiefs { get; init; } = 5;
    public int MaxCaptainsPerWarchief { get; init; } = 4;
    public int MaxUnassignedCaptains { get; init; } = 6;
    public int MaxGrunts { get; init; } = 12;

    public int PowerForWarchief { get; init; } = 100;
    public int PowerForOverlord { get; init; } = 250;

    public double AppearanceChance { get; init; } = 0.35;
    public double HomeAreaAppearanceChance { get; init; } = 0.6;
    public int MaxNemesesPerBattle { get; init; } = 2;

    public int MaxLevel { get; init; } = 20;
    public int MaxDisplayNameLength { get; init; } = 40;

    public double FeudChancePerDay { get; init; } = 0.2;
    public double FeudFightChancePerDay { get; init; } = 0.5;
    public double BloodOathChancePerDay { get; init; } = 0.1;
}

/// <summary>
/// The whole nemesis simulation. The game layer feeds it battle reports and asks it
/// which nemeses to put into each new battle; everything else happens in here.
/// Not thread-safe: call it from the game thread (or under one lock).
/// </summary>
public sealed class NemesisWorld
{
    public const int SaveVersion = 1;

    readonly List<Nemesis> _nemeses;
    readonly Rng _rng;

    public NemesisRules Rules { get; }
    public NamePack Names { get; }
    public int Day { get; private set; }
    public IReadOnlyList<Nemesis> All => _nemeses;
    public IEnumerable<Nemesis> Living => _nemeses.Where(n => n.Alive);

    public NemesisWorld(ulong seed, NemesisRules? rules = null, NamePack? names = null)
        : this(new Rng(seed), [], 0, rules, names) { }

    NemesisWorld(Rng rng, List<Nemesis> nemeses, int day, NemesisRules? rules, NamePack? names)
    {
        _rng = rng;
        _nemeses = nemeses;
        Day = day;
        Rules = rules ?? new NemesisRules();
        Names = names ?? NamePack.Default;
    }

    public Nemesis? Find(Guid id) => _nemeses.FirstOrDefault(n => n.Id == id);

    // ---- Hierarchy -------------------------------------------------------------------------

    public Nemesis? Overlord => Living.FirstOrDefault(n => n.Rank == Rank.Overlord);
    public IEnumerable<Nemesis> Warchiefs => Living.Where(n => n.Rank == Rank.Warchief).OrderByDescending(n => n.Power);
    public IEnumerable<Nemesis> CaptainsUnder(Guid warchiefId) =>
        Living.Where(n => n.Rank == Rank.Captain && n.ServesUnder == warchiefId).OrderByDescending(n => n.Power);
    public IEnumerable<Nemesis> UnassignedCaptains =>
        Living.Where(n => n.Rank == Rank.Captain && (n.ServesUnder is null || Find(n.ServesUnder.Value)?.Alive != true));
    public IEnumerable<Nemesis> Grunts => Living.Where(n => n.Rank == Rank.Grunt).OrderByDescending(n => n.Power);

    // ---- Battle setup ----------------------------------------------------------------------

    /// <summary>
    /// Decides which enemies in this fight become nemeses. A nemesis only takes the place of
    /// an enemy of its own kind, so it looks and fights the way players remember it.
    /// </summary>
    public IReadOnlyList<NemesisSpawn> PlanEncounter(string areaId, IReadOnlyList<FormationSlot> formation, IReadOnlyList<string> players)
    {
        var spawns = new List<NemesisSpawn>();
        var freeSlots = formation.ToList();
        var candidates = Living
            .Where(n => freeSlots.Any(s => s.Kind == n.Kind))
            .Select(n => (Nemesis: n, Weight: AppearanceWeight(n, areaId, players)))
            .OrderByDescending(c => c.Weight)
            .ToList();

        foreach (var (nemesis, weight) in candidates)
        {
            if (spawns.Count >= Rules.MaxNemesesPerBattle) break;
            var chance = Math.Min(0.95, (nemesis.HomeArea == areaId ? Rules.HomeAreaAppearanceChance : Rules.AppearanceChance) * weight);
            if (!_rng.Chance(chance)) continue;
            var slot = freeSlots.FirstOrDefault(s => s.Kind == nemesis.Kind);
            if (slot is null) continue;
            freeSlots.Remove(slot);
            spawns.Add(new NemesisSpawn(slot.Slot, nemesis.Id, ModifiersFor(nemesis), Taunt(nemesis, players)));
        }
        return spawns;
    }

    double AppearanceWeight(Nemesis n, string areaId, IReadOnlyList<string> players)
    {
        var weight = 1.0 + 0.25 * (int)n.Rank;
        foreach (var p in players)
        {
            if (!n.Players.TryGetValue(p, out var rec)) continue;
            if (rec.Memory.HasFlag(Memory.KilledPlayer)) weight += 1.0;
            if (rec.Memory.HasFlag(Memory.PlayerFled)) weight += 0.5;
            if (rec.Memory.HasFlag(Memory.Avenger)) weight += 2.0; // hunting this player
        }
        if (n.Memory.HasFlag(Memory.CheatedDeath)) weight += 0.5;
        return weight;
    }

    /// <summary>Rank, level and traits folded into one set of numbers for the game layer.</summary>
    public BattleModifiers ModifiersFor(Nemesis n)
    {
        var rank = (int)n.Rank;
        var level = Math.Min(n.Level, Rules.MaxLevel);
        var stats = Enum.GetValues<Stat>().ToDictionary(s => s, s => s switch
        {
            Stat.Health => (1.0 + 0.6 * rank) * (1.0 + 0.08 * (level - 1)),
            Stat.Magicka or Stat.Stamina => 1.0 + 0.3 * rank,
            _ => (1.0 + 0.1 * rank) * (1.0 + 0.03 * (level - 1)),
        });
        var affinities = new Dictionary<DamageKind, Affinity>();
        var autos = new List<AutoStatus>();
        foreach (var t in n.Traits)
        {
            if (t.StatMultipliers is not null)
                foreach (var (s, m) in t.StatMultipliers) stats[s] *= m;
            if (t.Affinities is not null)
                foreach (var (k, a) in t.Affinities)
                    affinities[k] = affinities.TryGetValue(k, out var existing) ? Stronger(existing, a) : a;
            if (t.AutoStatuses is not null)
                autos.AddRange(t.AutoStatuses.Where(s => !autos.Contains(s)));
        }
        return new BattleModifiers(DisplayName(n), stats, affinities, autos);
    }

    static Affinity Stronger(Affinity a, Affinity b) => (Affinity)Math.Max((int)a, (int)b);

    public string DisplayName(Nemesis n)
    {
        var full = n.FullName;
        return full.Length <= Rules.MaxDisplayNameLength ? full : n.Name[..Math.Min(n.Name.Length, Rules.MaxDisplayNameLength)];
    }

    string? Taunt(Nemesis n, IReadOnlyList<string> players)
    {
        string? target = null;
        string key = n.Encounters == 0 ? "first" : "generic";
        foreach (var p in players)
        {
            if (!n.Players.TryGetValue(p, out var rec)) continue;
            var k = rec.Memory.HasFlag(Memory.Avenger) ? "avenger"
                : n.Memory.HasFlag(Memory.CheatedDeath) && rec.TimesTheyBeatMe > 0 ? "cheated"
                : rec.Memory.HasFlag(Memory.KilledPlayer) ? "killed"
                : rec.Memory.HasFlag(Memory.PlayerFled) ? "fled"
                : n.Memory.HasFlag(Memory.FledFromPlayer) ? "escaped"
                : null;
            if (k is null) continue;
            (key, target) = (k, p);
            if (k == "avenger") break;
        }
        // In a shared world it may have killed one of your friends in their own game.
        if (target is null)
        {
            var victim = n.Players.FirstOrDefault(kv => !players.Contains(kv.Key) && kv.Value.Memory.HasFlag(Memory.KilledPlayer)).Key;
            if (victim is not null) (key, target) = ("friend", victim);
        }
        if (!Names.Taunts.TryGetValue(key, out var lines) || lines.Count == 0) return null;
        return _rng.Pick(lines).Replace("{name}", n.FullName).Replace("{player}", target ?? "hero");
    }

    // ---- Battle results --------------------------------------------------------------------

    public IReadOnlyList<WorldEvent> RecordBattle(BattleReport report)
    {
        var events = new List<WorldEvent>();
        var nemesesInBattle = report.Enemies
            .Where(e => e.NemesisId is not null)
            .Select(e => (Enemy: e, Nemesis: Find(e.NemesisId!.Value)))
            .Where(x => x.Nemesis is { Alive: true })
            .ToList();

        foreach (var (enemy, n) in nemesesInBattle)
        {
            n!.Encounters++;
            n.Memory |= Memory.MetPlayer;
            foreach (var p in report.Players) n.Player(p).Memory |= Memory.MetPlayer;

            if (report.Result == BattleResult.Defeat)
                Triumph(n, report, events);
            else if (report.Result == BattleResult.PartyEscaped)
                PlayerFled(n, report, events);
            else if (enemy.Escaped)
                Escaped(n, report, events);
            else if (enemy.Killed)
                Defeated(n, enemy, report, events);
        }

        // A plain monster that wipes the party, or that the party runs from or that runs
        // away itself, gets a name. This is how the roster fills up.
        if (nemesesInBattle.Count == 0)
        {
            var survivor = report.Enemies.Where(e => !e.Killed).OrderByDescending(e => e.MaxHealth).FirstOrDefault();
            if (survivor is not null)
            {
                var (rank, memory) = report.Result switch
                {
                    BattleResult.Defeat => (Rank.Captain, Memory.KilledPlayer),
                    BattleResult.PartyEscaped => (Rank.Grunt, Memory.PlayerFled),
                    _ when survivor.Escaped => (Rank.Grunt, Memory.FledFromPlayer),
                    _ => (Rank.Grunt, Memory.None),
                };
                if (memory != Memory.None) Birth(survivor, rank, memory, report, events);
            }
        }

        UpdateHierarchy(events);
        return events;
    }

    void Triumph(Nemesis n, BattleReport report, List<WorldEvent> events)
    {
        n.Wins++;
        n.Memory |= Memory.KilledPlayer;
        GainPower(n, 40, levels: 2);
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory |= Memory.KilledPlayer;
            rec.TimesKilledThem++;
        }
        Log(n, $"Wiped out {Party(report)} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Triumph, n.Id, $"{n.FullName} defeated {Party(report)} and grows stronger."));
    }

    void PlayerFled(Nemesis n, BattleReport report, List<WorldEvent> events)
    {
        n.Memory |= Memory.PlayerFled;
        GainPower(n, 15, levels: 1);
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory |= Memory.PlayerFled;
            rec.TimesTheyFled++;
        }
        Log(n, $"{Party(report)} ran from it in {report.AreaId}.");
        events.Add(new(WorldEventKind.Triumph, n.Id, $"{n.FullName} laughs as {Party(report)} flee."));
    }

    void Escaped(Nemesis n, BattleReport report, List<WorldEvent> events)
    {
        n.Memory |= Memory.FledFromPlayer;
        GainPower(n, 10, levels: 0);
        Log(n, $"Escaped from {Party(report)} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Escaped, n.Id, $"{n.FullName} escaped. It will be back."));
    }

    void Defeated(Nemesis n, EnemyReport enemy, BattleReport report, List<WorldEvent> events)
    {
        n.Losses++;
        var killer = enemy.KilledBy ?? report.Players.FirstOrDefault();
        foreach (var p in report.Players) n.Player(p).TimesTheyBeatMe++;

        var cheatChance = n.Rank switch { Rank.Grunt => 0.35, Rank.Captain => 0.55, Rank.Warchief => 0.65, _ => 0.75 }
            - 0.2 * n.Scars.Count;
        if (_rng.Chance(Math.Max(0.05, cheatChance)))
        {
            var cause = enemy.KillingBlow ?? DamageKind.Physical;
            AddScar(n, cause);
            n.Memory |= Memory.CheatedDeath;
            GainPower(n, 10, levels: 1);
            Log(n, $"Was cut down by {killer ?? "the party"} but survived, scarred by {cause}.");
            events.Add(new(WorldEventKind.CheatedDeath, n.Id, $"{n.FullName} was not as dead as it looked."));
            return;
        }

        n.Alive = false;
        Log(n, $"Slain by {killer ?? "the party"} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Slain, n.Id, $"{n.FullName} has been slain by {killer ?? "the party"}!"));

        // Death has consequences: a blood-brother swears revenge on whoever did it.
        if (n.BloodBrother is { } bid && Find(bid) is { Alive: true } brother)
        {
            brother.Memory |= Memory.Avenger;
            if (killer is not null) brother.Player(killer).Memory |= Memory.Avenger;
            GainPower(brother, 15, levels: 1);
            Log(brother, $"Swore revenge on {killer ?? "the party"} for {n.FullName}.");
            events.Add(new(WorldEventKind.Avenger, brother.Id, $"{brother.FullName} swears to avenge {n.FullName}!", n.Id));
        }
    }

    void Birth(EnemyReport enemy, Rank rank, Memory memory, BattleReport report, List<WorldEvent> events)
    {
        if (rank == Rank.Grunt && Grunts.Count() >= Rules.MaxGrunts)
        {
            // Roster full: the newcomer replaces the weakest grunt, who fades into the horde.
            var weakest = Grunts.Last();
            weakest.Alive = false;
            Log(weakest, "Was pushed out of the ranks by a newcomer.");
        }

        var n = new Nemesis
        {
            Id = _rng.NextGuid(),
            Name = MakeName(),
            Kind = enemy.Kind,
            Species = enemy.Species,
            Rank = rank,
            Level = Math.Max(1, report.PartyLevel / 5),
            HomeArea = report.AreaId,
            Memory = memory | Memory.MetPlayer,
            Encounters = 1,
            Wins = memory.HasFlag(Memory.KilledPlayer) ? 1 : 0,
        };
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory = memory | Memory.MetPlayer;
            if (memory.HasFlag(Memory.KilledPlayer)) rec.TimesKilledThem = 1;
            if (memory.HasFlag(Memory.PlayerFled)) rec.TimesTheyFled = 1;
        }
        n.Power = rank == Rank.Captain ? 40 : 10;
        AddTrait(n, TraitKind.Strength);
        AddTrait(n, TraitKind.Weakness);
        if (rank >= Rank.Captain) AddTrait(n, TraitKind.Strength);
        n.Epithet = n.Traits.Select(t => t.Epithet).FirstOrDefault(e => e is not null) ?? _rng.Pick(Names.Epithets);
        _nemeses.Add(n);

        var why = memory.HasFlag(Memory.KilledPlayer) ? $"after defeating {Party(report)}"
            : memory.HasFlag(Memory.PlayerFled) ? $"after {Party(report)} fled from it"
            : $"after escaping {Party(report)}";
        Log(n, $"Rose from the ranks of {n.Species} in {report.AreaId} {why}.");
        events.Add(new(WorldEventKind.Born, n.Id, $"A {n.Species} named {n.FullName} rose to {rank} {why}."));
    }

    // ---- Traits, scars, names ---------------------------------------------------------------

    void AddTrait(Nemesis n, TraitKind kind)
    {
        var pool = (kind == TraitKind.Strength ? TraitCatalog.Strengths : TraitCatalog.Weaknesses)
            .Where(t => n.Traits.All(o => o.Id != t.Id) && !Conflicts(n, t))
            .ToList();
        if (pool.Count > 0) n.Traits.Add(_rng.Pick(pool));
    }

    /// <summary>A nemesis can't both absorb fire and be weak to it.</summary>
    static bool Conflicts(Nemesis n, Trait t) =>
        t.Affinities is not null && n.Traits.Any(o => o.Affinities is not null && o.Affinities.Keys.Intersect(t.Affinities.Keys).Any());

    void AddScar(Nemesis n, DamageKind cause)
    {
        var descriptions = Names.ScarDescriptions.GetValueOrDefault(cause) ?? ["An ugly scar."];
        n.Scars.Add(new Scar(cause, _rng.Pick(descriptions), Day));

        // Surviving a death teaches resistance; surviving it twice, immunity. Any weakness
        // to that element is burned away.
        var id = $"scar_{cause}".ToLowerInvariant();
        var hadScar = n.Traits.Any(t => t.Id == id);
        n.Traits.RemoveAll(t => t.Id == id || (t.Kind == TraitKind.Weakness && t.Affinities?.ContainsKey(cause) == true));
        n.Traits.Add(TraitCatalog.ScarResistance(cause, hadScar ? Affinity.Immune : Affinity.Resist));

        if (Names.ScarEpithets.TryGetValue(cause, out var epithets) && epithets.Count > 0)
            n.Epithet = _rng.Pick(epithets);
    }

    string MakeName()
    {
        for (var i = 0; i < 20; i++)
        {
            var name = _rng.Pick(Names.FirstNames) + _rng.Pick(Names.NameSuffixes.Count > 0 ? Names.NameSuffixes : [""]);
            if (Living.All(n => n.Name != name)) return name;
        }
        return _rng.Pick(Names.FirstNames) + " " + (char)('A' + _rng.Next(26));
    }

    // ---- Power and promotion ----------------------------------------------------------------

    void GainPower(Nemesis n, int power, int levels)
    {
        n.Power += power;
        n.Level = Math.Min(Rules.MaxLevel, n.Level + levels);
    }

    void UpdateHierarchy(List<WorldEvent> events)
    {
        // Grunts with enough renown become captains.
        foreach (var g in Grunts.Where(g => g.Power >= 40).ToList())
        {
            if (UnassignedCaptains.Count() >= Rules.MaxUnassignedCaptains && !HasCaptainVacancy()) break;
            Promote(g, Rank.Captain, events);
        }

        // Captains under nobody look for a warchief with room.
        foreach (var c in UnassignedCaptains.ToList())
        {
            var chief = Warchiefs.Where(w => CaptainsUnder(w.Id).Count() < Rules.MaxCaptainsPerWarchief)
                .OrderBy(w => CaptainsUnder(w.Id).Count()).FirstOrDefault();
            if (chief is not null) c.ServesUnder = chief.Id;
        }

        // The strongest captains rise when there's room at the top.
        while (Warchiefs.Count() < Rules.MaxWarchiefs
               && Living.Where(n => n.Rank == Rank.Captain && n.Power >= Rules.PowerForWarchief)
                   .OrderByDescending(n => n.Power).FirstOrDefault() is { } rising)
        {
            var oldChief = rising.ServesUnder;
            Promote(rising, Rank.Warchief, events);
            // Its old warchief's other captains stay put; the new warchief starts with none.
            if (oldChief is not null) Log(rising, "Left its warchief's service to lead its own band.");
        }

        if (Overlord is null && Warchiefs.FirstOrDefault(w => w.Power >= Rules.PowerForOverlord) is { } heir)
        {
            Promote(heir, Rank.Overlord, events);
            foreach (var c in CaptainsUnder(heir.Id).ToList()) c.ServesUnder = null; // they need a new chief
        }

        // With no vacancy, an ambitious captain challenges the weakest warchief.
        if (Warchiefs.Count() >= Rules.MaxWarchiefs)
        {
            var weakest = Warchiefs.Last();
            var challenger = Living.Where(n => n.Rank == Rank.Captain && n.Power > weakest.Power + 20)
                .OrderByDescending(n => n.Power).FirstOrDefault();
            if (challenger is not null && challenger.Rivals.Add(weakest.Id))
            {
                weakest.Rivals.Add(challenger.Id);
                events.Add(new(WorldEventKind.FeudStarted, challenger.Id,
                    $"{challenger.FullName} challenges Warchief {weakest.FullName} for its title.", weakest.Id));
            }
        }
    }

    bool HasCaptainVacancy() =>
        Warchiefs.Any(w => CaptainsUnder(w.Id).Count() < Rules.MaxCaptainsPerWarchief);

    void Promote(Nemesis n, Rank rank, List<WorldEvent> events)
    {
        var from = n.Rank;
        n.Rank = rank;
        if (rank >= Rank.Warchief) n.ServesUnder = null;
        AddTrait(n, TraitKind.Strength);
        Log(n, $"Promoted from {from} to {rank}.");
        events.Add(new(WorldEventKind.Promoted, n.Id, $"{n.FullName} has been promoted to {rank}!"));
    }

    /// <summary>
    /// Outside meddling (Vann whispering to a nemesis): grants power and levels and lets the
    /// hierarchy react. Returns the resulting events.
    /// </summary>
    public IReadOnlyList<WorldEvent> Empower(Guid id, int power, string why)
    {
        var events = new List<WorldEvent>();
        if (Find(id) is not { Alive: true } n) return events;
        GainPower(n, power, levels: 1);
        Log(n, why);
        UpdateHierarchy(events);
        return events;
    }

    // ---- Time passing ------------------------------------------------------------------------

    /// <summary>
    /// The world moves while the party rests: feuds are fought, oaths sworn, titles change
    /// hands. Call it when the party saves, sleeps at an inn, or every N battles.
    /// </summary>
    public IReadOnlyList<WorldEvent> AdvanceDay()
    {
        Day++;
        var events = new List<WorldEvent>();
        var living = Living.Where(n => n.Rank >= Rank.Captain).ToList();

        if (living.Count >= 2 && _rng.Chance(Rules.FeudChancePerDay))
        {
            var a = _rng.Pick(living);
            var b = _rng.Pick(living.Where(x => x != a && x.BloodBrother != a.Id).ToList() is { Count: > 0 } others ? others : [a]);
            if (a != b && a.Rivals.Add(b.Id))
            {
                b.Rivals.Add(a.Id);
                events.Add(new(WorldEventKind.FeudStarted, a.Id, $"{a.FullName} and {b.FullName} are now bitter rivals.", b.Id));
            }
        }

        if (living.Count >= 2 && _rng.Chance(Rules.BloodOathChancePerDay))
        {
            var a = _rng.Pick(living);
            var b = living.FirstOrDefault(x => x != a && x.BloodBrother is null && a.BloodBrother is null && !a.Rivals.Contains(x.Id));
            if (b is not null)
            {
                a.BloodBrother = b.Id;
                b.BloodBrother = a.Id;
                Log(a, $"Swore a blood oath with {b.FullName}.");
                Log(b, $"Swore a blood oath with {a.FullName}.");
                events.Add(new(WorldEventKind.BloodOath, a.Id, $"{a.FullName} and {b.FullName} swore a blood oath.", b.Id));
            }
        }

        foreach (var a in Living.ToList())
        {
            foreach (var rivalId in a.Rivals.ToList())
            {
                var b = Find(rivalId);
                if (b is null || !b.Alive) { a.Rivals.Remove(rivalId); continue; }
                if (a.Id.CompareTo(b.Id) > 0 || !_rng.Chance(Rules.FeudFightChancePerDay)) continue; // each pair once
                Duel(a, b, events);
            }
        }

        UpdateHierarchy(events);
        return events;
    }

    void Duel(Nemesis a, Nemesis b, List<WorldEvent> events)
    {
        double Score(Nemesis n) => n.Power + n.Level * 5 + (int)n.Rank * 30 + _rng.Next(60);
        var (winner, loser) = Score(a) >= Score(b) ? (a, b) : (b, a);
        winner.Rivals.Remove(loser.Id);
        loser.Rivals.Remove(winner.Id);
        GainPower(winner, 30, levels: 1);

        // Taking a warchief's place: the winner inherits the title.
        if (loser.Rank > winner.Rank)
        {
            (winner.Rank, loser.Rank) = (loser.Rank, winner.Rank);
            winner.ServesUnder = null;
            foreach (var c in Living.Where(c => c.ServesUnder == loser.Id).ToList()) c.ServesUnder = winner.Id;
            loser.ServesUnder = winner.Rank == Rank.Warchief ? winner.Id : null;
            events.Add(new(WorldEventKind.Succession, winner.Id, $"{winner.FullName} took {loser.FullName}'s place as {winner.Rank}!", loser.Id));
        }

        if (_rng.Chance(loser.Rank <= Rank.Captain ? 0.5 : 0.25))
        {
            loser.Alive = false;
            Log(loser, $"Killed in a feud with {winner.FullName}.");
            Log(winner, $"Killed its rival {loser.FullName}.");
            events.Add(new(WorldEventKind.FeudWon, winner.Id, $"{winner.FullName} killed its rival {loser.FullName}.", loser.Id));
        }
        else
        {
            loser.Memory |= Memory.Humiliated;
            loser.Power = Math.Max(0, loser.Power - 20);
            AddScar(loser, DamageKind.Physical);
            Log(loser, $"Beaten and humiliated by {winner.FullName}.");
            Log(winner, $"Humiliated its rival {loser.FullName}.");
            events.Add(new(WorldEventKind.FeudLost, loser.Id, $"{loser.FullName} was humiliated by {winner.FullName}.", winner.Id));
        }
    }

    // ---- Helpers and persistence --------------------------------------------------------------

    void Log(Nemesis n, string text) => n.History.Add(new HistoryEntry(Day, text));

    static string Party(BattleReport r) => r.Players.Count switch
    {
        0 => "the party",
        1 => r.Players[0],
        _ => string.Join(", ", r.Players.Take(r.Players.Count - 1)) + " and " + r.Players[^1],
    };

    public sealed record State(int Version, int Day, ulong RngState, List<Nemesis> Nemeses);

    public State Snapshot() => new(SaveVersion, Day, _rng.State, _nemeses);

    public static NemesisWorld Restore(State state, NemesisRules? rules = null, NamePack? names = null)
    {
        if (state.Version > SaveVersion)
            throw new InvalidDataException($"Nemesis save version {state.Version} is newer than this mod ({SaveVersion}).");
        return new NemesisWorld(new Rng(state.RngState), state.Nemeses, state.Day, rules, names);
    }

    public string ToJson() => NemesisJson.Serialize(Snapshot());

    public static NemesisWorld FromJson(string json, NemesisRules? rules = null, NamePack? names = null) =>
        Restore(NemesisJson.Deserialize<State>(json), rules, names);
}
