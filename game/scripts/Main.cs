using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Starts Tristinwold: builds the city, the player and the enemies, and links the game to the
/// shared world. Command line (after "--"):
///   --server host:port   the Tristinwold server's TCP address (default 127.0.0.1:7777; "off" to play alone)
///   --company Name       your company's name
///   --password secret
///   --selftest           play a scripted fight headless and exit 0 or 1
/// </summary>
public partial class Main : Node3D
{
    readonly Dictionary<string, string> _args = [];
    Player _player = null!;
    Hud _hud = null!;
    EncounterTracker _tracker = null!;
    GameClient? _client;
    string _area = "", _sentArea = "";
    double _clock, _lastRespawnCheck;
    readonly Dictionary<Lair, List<Enemy>> _lairs = [];

    public override void _Ready()
    {
        ParseArgs();
        DefineInputs();
        City.Build(this);

        _hud = new Hud();
        AddChild(_hud);

        _player = new Player { Company = _args.GetValueOrDefault("company", "Adventurer"), Position = City.PlayerStart };
        AddChild(_player);
        _player.Said += text => _hud.Toast(text);
        _player.WentDown += () => { _tracker.Downed(); _hud.Center("You have fallen."); };
        _player.Struck += (enemy, hit, kind) =>
        {
            _tracker.Engaged(enemy.Id, enemy.Kind.Id, enemy.Kind.Name, enemy.Stats.MaxHealth, ToNumerics(enemy.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, enemy.NemesisId);
            _tracker.Hit(enemy.Id, kind, _player.Company, _clock);
            if (hit.Killed) _tracker.Died(enemy.Id, _player.Company, _clock);
        };
        _hud.Company = _player.Company;

        AddPeople();
        foreach (var lair in City.Lairs) _lairs[lair] = SpawnLair(lair);

        _tracker = new EncounterTracker(
            (area, enemies) => _client?.Send(new EncounterStarted(area, enemies)),
            report =>
            {
                _client?.Send(new ReportBattle(report));
                _hud.Log($"Fight over: {report.Result}.");
                FightEnded?.Invoke(report);
            },
            _player.Company);

        Connect();
        if (_args.ContainsKey("selftest")) AddChild(new SelfTest(this));
    }

    // ---- the shared world ------------------------------------------------------------------------

    void Connect()
    {
        var server = _args.GetValueOrDefault("server", "127.0.0.1:7777");
        if (server == "off") { _hud.Toast("Playing alone: no shared world."); return; }
        var (host, port) = (server.Split(':')[0], server.Contains(':') ? int.Parse(server.Split(':')[1]) : Wire.DefaultPort);
        var password = _args.GetValueOrDefault("password", "");
        _client = new GameClient(host, port, () => new Hello(_player.Company, password, Wire.ProtocolVersion, [_player.Company]));
        _hud.Send = m => _client?.Send(m);
        _client.Start();
    }

    public GameClient? Client => _client;
    public Player PlayerNode => _player;
    public EncounterTracker Tracker => _tracker;
    public event Action<Message>? Received;
    public event Action<BattleReport>? FightEnded;
    public Hud HudNode => _hud;

    void Handle(Message m)
    {
        Received?.Invoke(m);
        switch (m)
        {
            case Welcome w: _hud.Toast($"{_player.Company}'s company joins {w.Campaign}."); break;
            case Rejected r: _hud.Toast($"The server turned us away: {r.Reason}"); break;
            case Notice n: _hud.Toast(n.From is null ? n.Text : n.Kind == NoticeKind.Vann ? $"{n.From}: \"{n.Text}\"" : $"{n.From}: {n.Text}"); break;
            case NemesisAppears a:
                foreach (var spawn in a.Spawns)
                {
                    if (_tracker.ActorForSlot(spawn.Slot) is not { } id || FindEnemy(id) is not { } e) continue;
                    e.MakeNemesis(spawn.NemesisId, spawn.Modifiers, spawn.Taunt);
                    _tracker.MarkNemesis(spawn.Slot, spawn.NemesisId);
                    _hud.Toast(spawn.Taunt is null ? $"{e.Stats.Name} is here." : $"{e.Stats.Name}: \"{spawn.Taunt}\"");
                }
                break;
            case NemesisHunts h:
            {
                // Out of sight behind the player, then it comes for them.
                var behind = _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 25;
                var hunter = Enemy.Create(Bestiary.Get(h.BaseForm), behind with { Y = City.FloorAt(behind) + 0.5f });
                AddEnemy(hunter);
                hunter.MakeNemesis(h.NemesisId, h.Modifiers, h.Taunt);
                hunter.Provoke(_player);
                _tracker.Engaged(hunter.Id, hunter.Kind.Id, hunter.Kind.Name, hunter.Stats.MaxHealth, ToNumerics(hunter.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, h.NemesisId);
                _hud.Toast($"{hunter.Stats.Name} has found you.");
                break;
            }
            case Reward r: _player.Septims += r.Gil / 100; _hud.Toast($"Contract paid: {r.Gil / 100} septims."); break;
            case Board b: _hud.ShowBoard(b.Contracts); break;
            case Roster r: _hud.ShowCompanies(r.Groups); break;
            case NemesisSnapshot s: _hud.ShowNemeses(s.WorldJson); break;
            case NemesisEvents e: foreach (var ev in e.Events) _hud.Log(ev.Text); break;
            case Drops d: foreach (var note in d.Notes) _hud.Toast($"A hidden note from {note.From}: \"{note.Text}\""); break;
            case KeystoneRevealed k: _hud.Toast($"{k.Character} of {k.Player}'s company is a Keystone."); break;
        }
    }

    // ---- every frame --------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        _clock += delta;
        while (_client is { } c && c.TryReceive(out var m)) Handle(m);

        _area = City.AreaAt(_player.GlobalPosition);
        if (_client?.Status == ClientStatus.Connected && _area != _sentArea)
        {
            _sentArea = _area;
            _client.Send(new Presence(_area, [_player.Company]));
        }

        foreach (var e in Enemies().Where(e => e.FightingCompany))
            _tracker.Engaged(e.Id, e.Kind.Id, e.Kind.Name, e.Stats.MaxHealth, ToNumerics(e.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, e.NemesisId);
        _tracker.Tick(_clock, ToNumerics(_player.GlobalPosition),
            id => FindEnemy(id) is { FightingCompany: true } e && e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 40,
            id => FindEnemy(id) is { } e ? ToNumerics(e.GlobalPosition) : null);
        _player.InFight = _tracker.InFight;
        if (!_player.Downed) _hud.Center("");

        if (_clock - _lastRespawnCheck > 30) { _lastRespawnCheck = _clock; RespawnEmptyLairs(); }

        if (Input.IsActionJustPressed("menu")) _hud.ToggleWindow();
        if (Input.IsActionJustPressed("rest") && !_tracker.InFight)
        {
            _client?.Send(new Rested());
            _hud.Toast("You rest. The world moves on without you.");
        }
        var status = _client is null ? "alone" : $"{_player.Company} | {_client.Status}{(_client.Status != ClientStatus.Connected && _client.LastError is { } err ? $" ({err})" : "")}";
        _hud.Update(_player, _area, status + "    F10: shared world   T: rest   Q: element   R: limit");
    }

    // ---- people and enemies ------------------------------------------------------------------

    void AddPeople()
    {
        // The Gallows Perch's keeper and the Magi archivist, until real characters arrive.
        Townsfolk("Mother Corva", "#4a6b3a", City.GallowsPerch + new Vector3(-7, 0, 6));
        Townsfolk("Sister Ilse Varrow", "#8a8a96", new Vector3(-34, City.InnerHeight, 6));
    }

    void Townsfolk(string name, string colour, Vector3 at)
    {
        var body = new StaticBody3D { Position = at };
        body.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(colour) } });
        body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        body.AddChild(new Label3D { Text = name, Position = new Vector3(0, 2.3f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color("#a8d8a0") });
        AddChild(body);
    }

    List<Enemy> SpawnLair(Lair lair)
    {
        var list = new List<Enemy>();
        var kind = Bestiary.Get(lair.KindId);
        for (var i = 0; i < lair.Count; i++)
        {
            var offset = new Vector3((float)GD.RandRange(-lair.Spread, lair.Spread), 0, (float)GD.RandRange(-lair.Spread, lair.Spread));
            var at = lair.Center + offset;
            var e = Enemy.Create(kind, at with { Y = City.FloorAt(at) + 0.5f });
            AddEnemy(e);
            list.Add(e);
        }
        return list;
    }

    void RespawnEmptyLairs()
    {
        foreach (var lair in _lairs.Keys.ToList())
        {
            var alive = _lairs[lair].Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).ToList();
            var farAway = _player.GlobalPosition.DistanceTo(lair.Center) > 60;
            _lairs[lair] = alive.Count == 0 && farAway ? SpawnLair(lair) : alive;
        }
    }

    public void AddEnemy(Enemy e)
    {
        AddChild(e);
        e.AddToGroup("enemies");
    }

    public IEnumerable<Enemy> Enemies() => GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(IsInstanceValid);

    public Enemy? FindEnemy(long id) => Enemies().FirstOrDefault(e => e.Id == id);

    public string Area => _area;
    public double Clock => _clock;

    static System.Numerics.Vector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

    // ---- setup -------------------------------------------------------------------------------

    void ParseArgs()
    {
        var a = OS.GetCmdlineUserArgs();
        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--")) continue;
            var key = a[i][2..];
            _args[key] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "true";
        }
    }

    static void DefineInputs()
    {
        void Key(string action, Key key) { if (!InputMap.HasAction(action)) InputMap.AddAction(action); InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key }); }
        void Mouse(string action, MouseButton b) { if (!InputMap.HasAction(action)) InputMap.AddAction(action); InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = b }); }
        Key("move_forward", Godot.Key.W); Key("move_back", Godot.Key.S); Key("move_left", Godot.Key.A); Key("move_right", Godot.Key.D);
        Key("jump", Godot.Key.Space); Key("sprint", Godot.Key.Shift);
        Mouse("attack", MouseButton.Left); Mouse("spell", MouseButton.Right);
        Key("limit", Godot.Key.R); Key("cycle_element", Godot.Key.Q); Key("menu", Godot.Key.F10); Key("rest", Godot.Key.T);
    }

    public override void _ExitTree() => _client?.Dispose();
}
