using Fahrenheit.Events;
using Tristinwold.Mods.Nemesis;
using Tristinwold.Nemesis;
using Tristinwold.Shared;

namespace Tristinwold.Mods.Multiplayer;

public sealed class MultiplayerSettings : FhSettingProvider<MultiplayerModule> {
    protected override IEnumerable<FhSetting> get() => [ auto_connect, host, port, password ];

    public readonly FhSettingToggle auto_connect = new("auto_connect", false);
    public readonly FhSettingText   host         = new("host", "127.0.0.1");
    public readonly FhSettingText   port         = new("port", Wire.DefaultPort.ToString());
    public readonly FhSettingText   password     = new("password", "", ImGuiInputTextFlags.Password);
}

/// <summary>
///     Joins this game to a shared Tristinwold. You keep playing your own party of three in
///     your own FFX; the server shares the nemeses, the contract board, dead drops and news
///     of the other companies.
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed partial class MultiplayerModule : FhModule {
    private readonly MultiplayerSettings _settings = new();
    private readonly Lock                _lock     = new();

    private NemesisModule? _nemesis;
    private GameClient?    _client;

    // Last values sent, so presence goes out only on change. Game thread only.
    private string        _sent_area  = "";
    private string        _sent_party = "";
    private List<string>  _party      = [];

    // Shared state shown in the F10 window. Guarded by _lock.
    private List<PublicContract> _board     = [];
    private List<GroupView>      _groups    = [];
    private List<DeadDropView>   _drops     = [];
    private string               _drops_at  = "";
    private readonly List<string> _log      = [];
    private string?              _keystone_note;
    private string               _campaign  = "";

    /// <summary>
    ///     How an area is named on the server. Other groups only "meet" you when this matches,
    ///     so the story module can replace it with real place names.
    /// </summary>
    public Func<ushort, string> area_name { get; set; } = room => $"Area {room}";

    public string? current_area { get; private set; }

    public override bool init(FhModContext mod_context, FileStream global_state_file) {
        return FhApi.Events.Common.GameLoop.PreUpdate        .subscribe(pre_update)
            && FhApi.Events.Common.GameLoop.PostReturnToTitle.subscribe(_ => { _sent_area = ""; _sent_party = ""; });
    }

    public bool connected => _client?.Status == ClientStatus.Connected;

    public void connect() {
        disconnect();
        if (!link_nemesis()) {
            _logger.Error("The nemesis module isn't loaded; can't join a shared world.");
            return;
        }
        if (!int.TryParse(_settings.port.get(), out int port)) port = Wire.DefaultPort;
        string player = _nemesis!.player_name.Trim();
        string password = _settings.password.get();

        _client = new GameClient(_settings.host.get().Trim(), port,
            () => new Hello(player, password, Wire.ProtocolVersion, party_snapshot()));
        _nemesis.remote_report = report => _client?.Send(new ReportBattle(report));
        _nemesis.remote_rested = () => _client?.Send(new Rested());
        _sent_area = _sent_party = "";
        _client.Start();
        note($"Connecting to {_settings.host.get()}:{port} as {player}...");
    }

    public void disconnect() {
        if (_nemesis is not null) {
            _nemesis.remote_report = null;
            _nemesis.remote_rested = null;
        }
        _client?.Dispose();
        _client = null;
    }

    private bool link_nemesis() {
        if (_nemesis is not null) return true;
        if (new FhModuleHandle<NemesisModule>(this).try_get_module(out NemesisModule? nemesis)) _nemesis = nemesis;
        return _nemesis is not null;
    }

    public void send(Message message) => _client?.Send(message);

    // ---- Game thread ------------------------------------------------------------------------

    private void pre_update(UpdateLoopEventArgs args) {
        if (_client is null && _settings.auto_connect.get() && Globals.save_data != null && *Globals.event_id != 0x17) connect();
        if (_client is null) return;

        // handle() may disconnect (a rejection), so re-check the client every message.
        while (_client is { } client && client.TryReceive(out Message message)) {
            try { handle(message); }
            catch (Exception ex) { _logger.Error($"Shared world message {message.GetType().Name} failed: {ex.Message}"); }
        }
        if (_client?.Status == ClientStatus.Connected) send_presence();
    }

    private void send_presence() {
        if (Globals.save_data == null || *Globals.event_id == 0x17) return; // title screen
        string area = area_name(Globals.save_data->current_room_id);
        _party = read_party();
        string party = string.Join("|", _party);
        current_area = area;
        if (area == _sent_area && party == _sent_party) return;
        _sent_area  = area;
        _sent_party = party;
        _client!.Send(new Presence(area, _party));
    }

    private List<string> party_snapshot() {
        lock (_lock) return [.. _party];
    }

    private List<string> read_party() {
        List<string> names = [];
        for (int i = 0; i < 3; i++) {
            byte chr_id = Globals.save_data->party_frontline[i];
            if (chr_id == 0xFF) continue;
            byte* name = FhXCall.TOGetSaveChrName.fnptr!(chr_id);
            names.Add(name == null ? $"#{chr_id}" : decode(name));
        }
        lock (_lock) _party = names;
        return names;
    }

    private void handle(Message message) {
        switch (message) {
            case Welcome w:
                lock (_lock) _campaign = w.Campaign;
                note($"Joined the shared world: {w.Campaign}.");
                break;
            case Rejected r:
                note($"The server turned us away: {r.Reason}");
                disconnect();
                break;
            case NemesisSnapshot s:
                _nemesis?.replace_world(s.WorldJson);
                break;
            case NemesisEvents e:
                _nemesis?.show_remote_events(e.Events);
                break;
            case Board b:
                lock (_lock) _board = [.. b.Contracts];
                break;
            case Roster r:
                lock (_lock) _groups = [.. r.Groups];
                break;
            case Drops d:
                lock (_lock) {
                    if (_drops_at != d.Area) _drops.Clear();
                    _drops_at = d.Area;
                    _drops.AddRange(d.Notes);
                }
                foreach (DeadDropView n in d.Notes) note($"A hidden note from {n.From}: \"{n.Text}\"", toast: true);
                break;
            case Notice n:
                note(n.From is null ? n.Text : $"{n.From}: {n.Text}", toast: n.Kind is not NoticeKind.Chat);
                break;
            case Reward r:
                FhXCall.MsPayGIL.fnptr!(-r.Gil); // a negative payment is income
                note($"Contract paid: {r.Gil} gil.", toast: true);
                break;
            case KeystoneRevealed k:
                bool mine = _nemesis is not null && k.Player.Equals(_nemesis.player_name, StringComparison.OrdinalIgnoreCase);
                string text = mine
                    ? $"Under Magi light, an hourglass shows in {k.Character}'s eye. {k.Character} is your Keystone."
                    : $"{k.Player}'s company has a Keystone: {k.Character}.";
                lock (_lock) if (mine) _keystone_note = text;
                note(text, toast: true);
                break;
        }
    }

    private void note(string text, bool toast = false) {
        _logger.Info(text);
        lock (_lock) {
            _log.Add($"[{DateTime.Now:HH:mm}] {text}");
            if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
        }
        if (toast) _nemesis?.toast(text);
    }

    private static string decode(byte* game_text) {
        int len = 0;
        while (len < 64 && game_text[len] != 0) len++;
        ReadOnlySpan<byte> src = new(game_text, len);
        Span<byte> utf8 = stackalloc byte[FhEncoding.compute_decode_buffer_size(src)];
        int written = FhEncoding.decode(src, utf8);
        return Encoding.UTF8.GetString(utf8[..written]);
    }
}
