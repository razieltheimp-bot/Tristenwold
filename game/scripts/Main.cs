using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Starts Tristinwold: builds the city, streams the endless world around the player, and links
/// the game to the shared world. Command line (after "--"):
///   --server address     the server: host:port over TCP (192.168.1.20:7777), or an https:// address such
///                        as a Cloudflare tunnel's; "off" to play alone. Without it, a start screen asks.
///   --company Name       your company's name
///   --touch              on-screen touch controls (on by itself on touch screens)
///   --password secret
///   --save path          where the company's map and quests are kept (default user://saves/Name.json)
///   --selftest           play scripted fights and quests headless and exit 0 or 1
///   --remote [port]      remote play: a phone's web browser shows and controls this game (port 8090)
///   --tunnel             with --remote: a Cloudflare tunnel, so the phone can play from anywhere
///   --remote-lan         with --remote: also reachable on the home network without a tunnel
/// </summary>
public partial class Main : Node3D
{
    readonly Dictionary<string, string> _args = [];
    readonly WorldGen _world = WorldGen.Default;
    readonly ExploredMap _explored = new();
    readonly QuestLog _journal = new();
    Player _player = null!;
    Hud _hud = null!;
    Terrain _terrain = null!;
    Places _places = null!;
    Wilds _wilds = null!;
    EncounterTracker _tracker = null!;
    IGameLink? _client;
    string _area = "", _sentArea = "", _savePath = "";
    string? _nemesisJson;
    double _clock, _lastRespawnCheck, _lastReveal, _lastPlaces, _lastSave;
    readonly Dictionary<Lair, List<Enemy>> _lairs = [];
    readonly List<Person> _townsfolk = [];

    public override void _Ready()
    {
        ParseArgs();
        if (_args.TryGetValue("remote", out var remote))
        {
            // Remote play: a phone's browser shows and controls this game (with --tunnel, from anywhere).
            var port = int.TryParse(remote, out var p) ? p : 8090;
            AddChild(new RemotePlay(this, port, _args.ContainsKey("tunnel"), _args.GetValueOrDefault("cloudflared", "cloudflared"), _args.ContainsKey("remote-lan")));
        }
        if (Mobile) DisplayServer.ScreenSetOrientation(DisplayServer.ScreenOrientation.SensorLandscape); // landscape from the first frame
        if (_args.TryGetValue("portraits", out var folder)) { _ = Portraits(folder); return; }
        if (_args.TryGetValue("showroom", out var room)) { _ = Showroom(room); return; }
        if (_args.TryGetValue("lineup", out var line)) { Proportions.Enabled = !_args.ContainsKey("plain"); _ = Lineup(line); return; }
        if (_args.ContainsKey("account") && _args.ContainsKey("server")) { _ = LogInFromCommandLine(); return; }
        var scripted = _args.ContainsKey("selftest") || _args.ContainsKey("shots");
        if (scripted || _args.ContainsKey("server")) { Begin(ChoiceFromArgs()); return; }
        var start = new StartScreen();
        start.Play += Begin;
        AddChild(start);
        if (_args.TryGetValue("snap", out var snap)) _ = Snap(start, snap);
    }

    /// <summary>The character and server the command line asks for.</summary>
    StartChoice ChoiceFromArgs(string? account = null, string? token = null, string? save = null) => new(
        new CharacterSheet(_args.GetValueOrDefault("company", "Adventurer"), _args.GetValueOrDefault("look", "Knight"), _args.GetValueOrDefault("element", "Fire"), DateTime.UtcNow),
        _args.GetValueOrDefault("server", "127.0.0.1:7777"), _args.GetValueOrDefault("password", ""), account, token, save,
        Enum.TryParse<Graphics.Quality>(_args.GetValueOrDefault("graphics", ""), true, out var q) ? q : Graphics.Default);

    /// <summary>
    /// --account Joe --secret pw --company Bram: logs in (making the account and the character if
    /// they're new) and plays Bram, with Bram's progress from the server.
    /// </summary>
    async Task LogInFromCommandLine()
    {
        var (server, password, account, secret) = (_args["server"], _args.GetValueOrDefault("password", ""), _args["account"], _args.GetValueOrDefault("secret", ""));
        var reply = await AccountClient.AskAsync(server, new AccountRequest("login", account, secret, ServerPassword: password));
        if (!reply.Ok && reply.Error?.StartsWith("No account") == true)
            reply = await AccountClient.AskAsync(server, new AccountRequest("register", account, secret, ServerPassword: password));
        var choice = ChoiceFromArgs(account, reply.Token);
        if (reply.Ok && !reply.Characters.Any(c => c.Name.Equals(choice.Character.Name, StringComparison.OrdinalIgnoreCase)))
            reply = await AccountClient.AskAsync(server, new AccountRequest("create", account, Token: choice.Token, ServerPassword: password, Character: choice.Character));
        var sheet = reply.Characters.FirstOrDefault(c => c.Name.Equals(choice.Character.Name, StringComparison.OrdinalIgnoreCase));
        if (!reply.Ok || sheet is null)
        {
            GD.PrintErr($"Couldn't log in as {account}: {reply.Error}");
            GetTree().Quit(1);
            return;
        }
        var loaded = await AccountClient.AskAsync(server, new AccountRequest("load", account, Token: choice.Token, ServerPassword: password, Character: sheet));
        Begin(choice with { Character = sheet, Save = loaded.Save });
    }

    /// <summary>Renders a portrait of each class for the character page: --portraits game/assets/kaykit/portraits</summary>
    async Task Portraits(string folder)
    {
        GetViewport().TransparentBg = true;
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#2a2620"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#b0a898"), AmbientLightEnergy = 0.6f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-30, 35, 0), LightEnergy = 1.3f, LightColor = new Color("#fff0d6") });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10, 200, 0), LightEnergy = 0.5f, LightColor = new Color("#a0b8ff") });
        var camera = new Camera3D { Position = new Vector3(0.35f, 1.25f, 5.2f), Fov = 30 };
        AddChild(camera);
        camera.LookAt(new Vector3(0, 1.0f, 0), Vector3.Up);
        foreach (var look in Classes.All.Select(d => d.Look))
        {
            var figure = Figure.Create(look);
            figure.RotationDegrees = new Vector3(0, 155, 0); // three-quarter view
            AddChild(figure);
            for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var image = GetViewport().GetTexture().GetImage();
            var side = Math.Min(image.GetWidth(), image.GetHeight());
            image = image.GetRegion(new Rect2I((image.GetWidth() - side) / 2, 0, side, side));
            image.Resize(256, 256, Image.Interpolation.Lanczos);
            image.SavePng($"{folder}/{look.ToLowerInvariant()}.png");
            figure.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().Quit();
    }

    /// <summary>The characters standing in a row, idle and walking, photographed: --lineup /some/folder (--creatures for the animals)</summary>
    async Task Lineup(string folder)
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#8fa6bd"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#b0a898"), AmbientLightEnergy = 0.7f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, 30, 0), LightEnergy = 1.3f, LightColor = new Color("#fff0d6"), ShadowEnabled = true });
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(300, 300) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#6b6248") } });
        string[] dragons = ["dragon_red", "dragon_blue", "dragon_green", "dragon_black", "dragon_white", "dragon_gold", "dragon_silver", "dragon_bronze", "dragon_copper", "dragon_brass"];
        if (_args.ContainsKey("hero")) dragons = ["dragon_red", "dragon_white", "dragon_gold"];
        if (_args.ContainsKey("frost")) dragons = ["dragon_white", "dragon_white", "dragon_white"]; // normal, large and giant
        if (_args.ContainsKey("corrupt")) dragons = ["dragon_red_corrupt", "dragon_gold_corrupt", "dragon_green_corrupt", "dragon_white_corrupt"];
        if (_args.ContainsKey("riders")) dragons = ["rider:dragon_gold", "rider:dragon_silver", "dragon_red_corrupt", "rider:dragon_bronze"];
        string[] looks = _args.ContainsKey("dragons") ? dragons : _args.ContainsKey("creatures")
            ? ["Paladin", "skeever", "goblin", "spriggan", "frost_troll", "dragon", "wolf", "bear"]
            : _args.ContainsKey("young") ? ["Paladin", "wolf", "bear", "deer", "fox", "boar", "dragon_gold", "squire", "apprentice", "youth"]
            : _args.ContainsKey("crypt") ? ["cinder_thrall", "cinder_warden", "Paladin"]
            : [.. Classes.All.Select(d => d.Look)];
        var figures = new List<Body>();
        for (var i = 0; i < looks.Length; i++)
        {
            var figure = Body.For(looks[i]);
            // Younglings and the young who follow a hero, beside a grown hero for scale.
            if (_args.ContainsKey("young") && i > 0 && i < 7) figure.Scale = Vector3.One * (looks[i].StartsWith("dragon") ? 0.22f : 0.45f);
            var spacing = _args.ContainsKey("hero") ? 11f : _args.ContainsKey("dragons") ? 9f : _args.ContainsKey("young") ? 1.15f : _args.ContainsKey("crypt") ? 1.9f : 1.3f;
            figure.Position = new Vector3((i - (looks.Length - 1) / 2f) * spacing, 0, _args.ContainsKey("dragons") ? -(i % 2) * 7 : 0);
            figure.RotationDegrees = new Vector3(0, 160, 0);
            if (_args.ContainsKey("frost"))
            {
                var size = (DragonSize)i;
                figure.Scale = Vector3.One * (Dragons.Scale(size).Height / Dragons.Scale(DragonSize.Normal).Height);
                figure.Position = new Vector3(new[] { -14f, -4f, 12f }[i], 0, 0);
                figure.RotationDegrees = new Vector3(0, 110, 0); // side on, to see the ridge
            }
            AddChild(figure);
            figures.Add(figure);
        }
        var wide = _args.ContainsKey("dragons");
        var camera = new Camera3D { Position = _args.ContainsKey("frost") ? new Vector3(0, 5, 34) : _args.ContainsKey("hero") ? new Vector3(0, 9, 30) : wide ? new Vector3(0, 14, 62) : new Vector3(0, 1.3f, 9f), Fov = 38, Far = 500 };
        AddChild(camera);
        camera.LookAt(wide ? new Vector3(0, 4, 0) : new Vector3(0, 0.9f, 0), Vector3.Up);
        async Task Shoot(string name)
        {
            for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png");
        }
        await Shoot("idle");
        foreach (var f in figures) f.Move(3.5f, true, false);
        await Shoot("walking");
        foreach (var f in figures) f.Attack();
        for (var i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng($"{folder}/attacking.png");
        foreach (var f in figures) { f.RotationDegrees = new Vector3(0, -20, 0); f.Move(0, true, false); }
        await Shoot("from-behind");
        if (wide)
        {
            // In the air: wings beating (the white frost dragon has none, and stays down).
            foreach (var f in figures) { f.RotationDegrees = new Vector3(0, 150, 0); f.Position += new Vector3(0, f is Creature { Winged: true } ? 8 : 0, 0); f.Move(6, false, false); }
            await Shoot("flying");
            for (var i = 0; i < 9; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shoot("flying2");
        }
        GetTree().Quit();
    }

    /// <summary>Every kind of generated building in a row, photographed: --showroom /some/folder</summary>
    async Task Showroom(string folder)
    {
        City.Build(this);
        Graphics.Apply(Graphics.Quality.High, GetViewport(), City.Sky, City.Sun, new Terrain(_world));
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(400, 400) }, Position = new Vector3(0, -0.02f, 600), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#6b6a4a") } });
        string[] kinds = ["house", "house", "cottage", "tavern", "longhall", "barn", "smithy", "market", "tower", "church", "keep", "windmill", "well", "gate", "lighthouse", "ruin"];
        var x = -100f;
        var spots = new List<(string, float)>();
        for (var i = 0; i < kinds.Length; i++)
        {
            var roof = i % 3 == 0 ? Surface.ClayTile : i % 3 == 1 ? Surface.Slate : Surface.Shingle;
            Buildings.Place(this, kinds[i], new Vector3(x, 0, 600), 0, i, roof);
            spots.Add((kinds[i] + i, x));
            x += kinds[i] is "keep" or "church" ? 30 : 18;
        }
        var cam = new Camera3D { Fov = 55, Far = 2000 };
        AddChild(cam);
        cam.MakeCurrent();
        async Task Shoot(string name, Vector3 from, Vector3 at)
        {
            cam.Position = from;
            cam.LookAt(at, Vector3.Up);
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png");
        }
        foreach (var (name, bx) in spots)
            await Shoot(name, new Vector3(bx + 9, 5, 616), new Vector3(bx, 4, 600));
        await Shoot("street", new Vector3(-118, 3, 610), new Vector3(-40, 5, 600));
        GetTree().Quit();
    }

    /// <summary>Saves a picture of the start screen and quits, for checking its layout.</summary>
    async Task Snap(StartScreen start, string path)
    {
        start.Fill("https://seasonal-deck-organisms-sf.trycloudflare.com", "Joe", "secret", "table");
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path);
        if (_args.TryGetValue("snap-characters", out var second))
        {
            // The characters page, playing alone so no server is needed.
            await start.PlayAloneForSnapshot();
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(second);
        }
        GetTree().Quit();
    }

    public bool Mobile => OS.HasFeature("mobile");
    public bool TouchMode { get; private set; }
    public StartChoice Choice { get; private set; } = null!;

    void Begin(StartChoice choice)
    {
        Choice = choice;
        (_args["company"], _args["server"], _args["password"]) = (choice.Character.Name, choice.Server, choice.ServerPassword);
        TouchMode = _args.ContainsKey("touch") || _remoteTouch || DisplayServer.IsTouchscreenAvailable();
        DefineInputs(mouseButtons: !TouchMode);
        City.Build(this);
        _terrain = new Terrain(_world);
        Graphics.Apply(choice.Quality, GetViewport(), City.Sky, City.Sun, _terrain);
        AddChild(_terrain);
        _terrain.EnsureAround(City.PlayerStart, 2);

        _hud = new Hud();
        AddChild(_hud);

        _player = new Player
        {
            Company = choice.Character.Name, Outfit = choice.Character.Look, Position = City.PlayerStart,
            Hero = new Hero { Discipline = Classes.For(choice.Character.Look).Id },
            Element = Enum.TryParse<DamageKind>(choice.Character.Element, out var element) ? element : DamageKind.Fire,
        };
        AddChild(_player);
        _player.Said += text => _hud.Toast(text);
        _player.WentDown += () => { _tracker.Downed(); _hud.Center("You have fallen."); };
        _player.Struck += (enemy, hit, kind) =>
        {
            if (enemy.Sparring || enemy.Yielded) return; // a duel in the yard isn't a battle
            _tracker.Engaged(enemy.Id, enemy.Kind.Id, enemy.Kind.Name, enemy.Stats.MaxHealth, ToNumerics(enemy.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, enemy.NemesisId);
            _tracker.Hit(enemy.Id, kind, _player.Company, _clock);
            if (hit.Killed) _tracker.Died(enemy.Id, _player.Company, _clock);
        };
        _hud.Company = _player.Company;

        Load();
        AddPeople();
        foreach (var lair in City.Lairs) _lairs[lair] = SpawnLair(lair);
        _places = new Places(this, _world);
        AddChild(_places);
        _wilds = new Wilds(this, _world, _terrain);
        AddChild(_wilds);
        _places.Gone = id => _bonds.Any(b => b.Id == id);
        AddGuildMasters();
        AddGuilds();
        AddMarket();
        _herbPatch = new HerbPatch(this);
        AddChild(_herbPatch);
        foreach (var raid in Raids.All)
        {
            var lair = new RaidLair(this, raid);
            _raidLairs.Add(lair);
            AddChild(lair);
        }
        _roamers = new Roamers(this);
        AddChild(_roamers);
        SetUpCompany();
        _hud.AttachMap(_world, _explored, () => _player.GlobalPosition, () => -_player.GlobalTransform.Basis.Z, QuestGoals);

        _tracker = new EncounterTracker(
            (area, enemies) => _client?.Send(new EncounterStarted(area, enemies)),
            report =>
            {
                _client?.Send(new ReportBattle(report));
                _hud.Log($"Fight over: {report.Result}.");
                FightEnded?.Invoke(report);
            },
            _player.Company);

        if (TouchMode) EnableTouch();
        Connect();
        if (_args.ContainsKey("selftest")) AddChild(new SelfTest(this));
        else if (_args.TryGetValue("shots", out var shots)) AddChild(new Shots(this, shots, quick: _args.ContainsKey("quick"), dragon: _args.GetValueOrDefault("dragon"), skirmish: _args.ContainsKey("skirmish"), raid: _args.ContainsKey("raid"), lairs: _args.TryGetValue("lairs", out var lairs) ? lairs : null, wild: _args.ContainsKey("wild")));
        else _hud.Toast($"Tristinwold. You are a {_player.Class.Name}. M: map   J: journal   E: talk   K: skills   P: party   F10: the shared world");
    }

    // ---- the shared world ------------------------------------------------------------------------

    void Connect()
    {
        var server = _args.GetValueOrDefault("server", "127.0.0.1:7777");
        if (server == "off") { _hud.Toast("Playing alone: no shared world."); return; }
        var password = _args.GetValueOrDefault("password", "");
        _client = IGameLink.Open(server, () => new Hello(_player.Company, password, Wire.ProtocolVersion, [_player.Company], Choice.Account, Choice.Token));
        _hud.Send = m => _client?.Send(m);
        _client.Start();
    }

    bool _remoteTouch;

    /// <summary>A phone joined remote play: on-screen controls now, or as soon as the game starts.</summary>
    public void EnableTouchSoon()
    {
        if (_hud is null) { _remoteTouch = true; return; }
        if (!_hud.TouchEnabled) EnableTouch();
    }

    /// <summary>The remote-play link, once the tunnel has one: shown on this computer's screen too.</summary>
    public void ShowRemoteLink(string link)
    {
        if (_hud is not null) _hud.Toast($"Play from your phone: {link}", 30);
        AddChild(new CanvasLayer { Layer = 100, Name = "RemoteLink" });
        GetNode<CanvasLayer>("RemoteLink").AddChild(new Label
        {
            Text = $"Phone link: {link}", Position = new Vector2(12, 4), Modulate = new Color(1, 1, 1, 0.75f),
            LabelSettings = new LabelSettings { FontSize = 14, OutlineSize = 4, OutlineColor = Colors.Black },
        });
    }

    /// <summary>Turns on the on-screen controls (and stops a tap anywhere from counting as a sword swing).</summary>
    public void EnableTouch()
    {
        TouchMode = true;
        foreach (var action in new[] { "attack", "spell" })
            foreach (var e in InputMap.ActionGetEvents(action).OfType<InputEventMouseButton>()) InputMap.ActionEraseEvent(action, e);
        _hud.TouchEnabled = true;
        _hud.Touch.Look = by => _player.Look(by);
        _hud.Touch.LabelFor = action => action switch
        {
            "cycle_element" => _player.Class.Elemental ? _player.Element.ToString() : _player.Class.Utility.Name,
            "spell" => _player.Class.Signature.Name,
            "attack" => _player.Mount is not null ? "Breath" : _player.Class.Primary.Name,
            _ => null,
        };
        _hud.Touch.Lit = action => action switch
        {
            "limit" => _player.Stats.LimitReady,
            "interact" => Nearest() is not null,
            _ => false,
        };
    }

    public IGameLink? Client => _client;
    public Player PlayerNode => _player;
    public EncounterTracker Tracker => _tracker;
    public event Action<Message>? Received;
    public event Action<BattleReport>? FightEnded;
    public Hud HudNode => _hud;
    public WorldGen World => _world;
    public ExploredMap Explored => _explored;
    public QuestLog Journal => _journal;
    public Places PlacesNode => _places;
    public Wilds WildsNode => _wilds;
    public Terrain TerrainNode => _terrain;

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
                    if (Raids.All.Any(r => r.BossKind == e.Kind.Id) && !e.Named) continue; // a lair's boss is a nobody until it beats someone
                    e.MakeNemesis(spawn.NemesisId, spawn.Modifiers, spawn.Taunt);
                    _tracker.MarkNemesis(spawn.Slot, spawn.NemesisId);
                    _hud.Toast(spawn.Taunt is null ? $"{e.Stats.Name} is here." : $"{e.Stats.Name}: \"{spawn.Taunt}\"");
                }
                break;
            case NemesisHunts h:
            {
                // Out of sight behind the player, then it comes for them.
                var behind = _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 25;
                var hunter = Enemy.Create(Bestiary.Get(h.BaseForm), behind with { Y = City.FloorAt(behind) + 0.5f }, WorldGen.Danger(behind.X, behind.Z));
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
            case NemesisSnapshot s: _hud.ShowNemeses(s.WorldJson); _nemesisJson = s.WorldJson; break;
            case NemesisEvents e:
                foreach (var ev in e.Events)
                {
                    _hud.Log(ev.Text);
                    if (ev.Kind == WorldEventKind.Born && _nemesisJson is { } json && NemesisWorld.FromJson(json).Find(ev.NemesisId) is { } born) _roamers.Named(born);
                    if (ev.Kind == WorldEventKind.CheatedDeath) _roamers.CheatedDeath(ev.NemesisId);
                }
                break;
            case Drops d: foreach (var note in d.Notes) _hud.Toast($"A hidden note from {note.From}: \"{note.Text}\""); break;
            case KeystoneRevealed k: _hud.Toast($"{k.Character} of {k.Player}'s company is a Keystone."); break;
        }
    }

    // ---- every frame --------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_player is null) return; // still on the start screen
        _clock += delta;
        while (_client is { } c && c.TryReceive(out var m)) Handle(m);
        var pos = _player.GlobalPosition;

        _terrain.Update(pos, 2);
        if (_clock - _lastPlaces > 0.5) { _lastPlaces = _clock; _places.Update(pos); _wilds.Update(pos, _clock); }
        if (_clock - _lastReveal > 0.2) { _lastReveal = _clock; Explore(pos); }

        _area = City.AreaAt(pos);
        if (_client?.Status == ClientStatus.Connected && _area != _sentArea)
        {
            _sentArea = _area;
            _client.Send(new Presence(_area, [_player.Company]));
        }

        foreach (var e in Enemies().Where(e => e.FightingCompany && !e.Sparring && !e.Yielded))
            _tracker.Engaged(e.Id, e.Kind.Id, e.Kind.Name, e.Stats.MaxHealth, ToNumerics(e.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, e.NemesisId);
        _tracker.Tick(_clock, ToNumerics(_player.GlobalPosition),
            id => FindEnemy(id) is { FightingCompany: true } e && e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 40,
            id => FindEnemy(id) is { } e ? ToNumerics(e.GlobalPosition) : null);
        _player.InFight = _tracker.InFight || _player.Attackers().Any();
        _player.Frozen = _hud.Talking || _hud.MapOpen;
        TendCompany(delta);
        TendGuilds(delta);
        _player.GuildTitle = $"{_guilds.Rank(_player.Class.Calling)} of {Guilds.Of(_player.Class.Calling).Name}";

        if (_clock - _lastRespawnCheck > 30) { _lastRespawnCheck = _clock; RespawnEmptyLairs(); }
        if (_clock - _lastSave > 20) { _lastSave = _clock; Save(); }

        var near = Nearest();
        if (!_player.Downed) _hud.Center(near is null || _hud.Talking ? "" : $"E: {near.Value.Verb}");
        if (Input.IsActionJustPressed("interact"))
        {
            if (_hud.Talking) _hud.EndTalk();
            else near?.Act();
        }
        if (Input.IsActionJustPressed("menu")) _hud.ToggleWindow();
        if (Input.IsActionJustPressed("map")) _hud.ToggleMap();
        if (Input.IsActionJustPressed("journal")) { _hud.ShowJournal(_journal); _hud.OpenJournal(); }
        if (Input.IsActionJustPressed("party") && !_hud.Talking) ShowParty();
        if (Input.IsActionJustPressed("skills") && !_hud.Talking) ShowSkills();
        if (Input.IsActionJustPressed("pack") && !_hud.Talking) ShowPack();
        if (Input.IsActionJustPressed("drink")) QuickDrink();
        if (Input.IsActionJustPressed("ride")) ToggleRide();
        if (Input.IsActionJustPressed("rest") && !_tracker.InFight) Rest();
        var status = _client is null ? "alone" : $"{_player.Company} | {_client.Status}{(_client.Status != ClientStatus.Connected && _client.LastError is { } err ? $" ({err})" : "")}";
        _hud.Update(_player, _area, TouchMode ? status : status + $"    M: map   J: journal   E: talk   K: skills   I: pack   H: heal   P: party   T: rest   Q: {_player.Class.Utility.Name.ToLowerInvariant()}   R: limit{(Dragon is { Rideable: true } ? "   V: ride" : "")}");
        var saga = _saga.Objective(Dragon) is { } next ? [$"The Dragon Knight: {next}{(_saga.Goal(Dragon) is { } g ? $" ({Direction(pos, g)})" : "")}"] : Array.Empty<string>();
        // The nearest lair that suits the hero, and the nearest nemesis out roaming.
        if (Raids.For(_player.Hero.Level, pos.X, pos.Z).FirstOrDefault() is { } raid && LairOf(raid).Tracker(pos, Direction) is { } raidLine) saga = [.. saga, raidLine];
        if (_roamers.Tracker(pos, Direction) is { } roamerLine) saga = [.. saga, roamerLine];
        _hud.Track(saga.Concat(_journal.Open.Take(4).Select(e => $"{e.Quest.Title}: {e.Objective} ({Direction(pos, e.Goal)})")));
        _hud.Party(PartyLines());
    }

    // ---- exploring ----------------------------------------------------------------------------

    void Explore(Vector3 pos)
    {
        _hud.MapRevealed(_explored.Reveal(pos.X, pos.Z, 90));
        foreach (var s in _world.SettlementsNear(pos.X, pos.Z, 200))
        {
            if (s.DistanceTo(pos.X, pos.Z) > s.Radius + (s.IsCity ? 150 : 60) || !_explored.Discovered.Add(s.Id)) continue;
            _explored.Rumored.Remove(s.Id);
            _hud.Toast($"Discovered {s.Name}, {(s.IsCity ? s.Epithet : s.What)}.");
            _hud.Log(s.Story);
        }
    }

    IEnumerable<(double X, double Z)> QuestGoals() => _journal.Open.Select(e => e.Goal).Concat(_saga.Goal(Dragon) is { } g ? [g] : [])
        .Concat(Raids.For(_player.Hero.Level, 0, 0).Where(r => _raids.Ready(r, _guilds.PlaySeconds)).Select(r => (r.X, r.Z)))
        .Concat(_roamers.Spots);

    /// <summary>Resting: the world moves on, the wounded mend, and an egg kept warm by the fire hatches.</summary>
    void Rest()
    {
        _client?.Send(new Rested());
        foreach (var c in CompanionBodies) { c.Revive(); c.Stats.Heal(c.Stats.MaxHealth); c.RefreshPlate(); }
        _hud.Toast("You rest. The world moves on without you.");
        if (_saga.Warm(60, resting: true)) Hatch();
        else if (_saga.Stage == SagaStage.CarryEgg) _hud.Toast($"The egg is warmer ({_saga.Warmth:P0}).");
    }

    static string Direction(Vector3 from, (double X, double Z) to)
    {
        var (dx, dz) = (to.X - from.X, to.Z - from.Z);
        var d = Math.Sqrt(dx * dx + dz * dz);
        if (d < 30) return "here";
        string[] names = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];
        var bearing = (Math.Atan2(dx, -dz) * 180 / Math.PI + 360 + 22.5) % 360;
        return $"{(d < 1000 ? $"{d:0} m" : $"{d / 1000:0.0} km")} {names[(int)(bearing / 45)]}";
    }

    // ---- talking and quests -------------------------------------------------------------------

    (string Verb, Action Act)? Nearest()
    {
        var pos = _player.GlobalPosition;
        var person = _places.People.Concat(_townsfolk).Where(p => p.GlobalPosition.DistanceTo(pos) < 3.5f).MinBy(p => p.GlobalPosition.DistanceTo(pos));
        if (person is not null) return ($"talk to {person.Npc.Name}", () => Talk(person));
        var item = _places.Pickups.Append(_nest).Concat(_raidLairs.Select(l => l.Hoard)).OfType<Pickup>().Where(p => IsInstanceValid(p) && p.GlobalPosition.DistanceTo(pos) < 3).FirstOrDefault();
        if (item is not null) return ($"take {item.Item}", () => PickUp(item));
        if (_herbPatch.Within(pos) is { } plant) return ($"pick {Items.Get(plant.HerbId)?.Name ?? plant.HerbId}", () => Pick(plant));
        if (YounglingNear(pos) is { } young) return ($"tame the {young.Stats.Name}{(_player.Meat > 0 ? " (offer meat)" : "")}", () => Tame(young));
        return null;
    }

    /// <summary>Starts a conversation: news for any quest first, then whatever you want to ask.</summary>
    public void Talk(Person person)
    {
        _player.Face(person.GlobalPosition);
        person.TalkTo(_player.GlobalPosition);
        var news = _journal.TalkedTo(person.Npc.Id).ToList();
        foreach (var u in news) Apply(u);
        if (person.Captive)
        {
            _hud.Talk(person.Npc.Name, news.FirstOrDefault()?.Text ?? person.Npc.Greeting, ("Go. Run.", _hud.EndTalk));
            person.QueueFree();
            return;
        }
        Say(person, news.Count > 0 ? news[^1].Text : Greeting(person));
    }

    void Say(Person person, string text)
    {
        var npc = person.Npc;
        var quest = _world.QuestOf(npc);
        var entry = quest is null ? null : _journal.Get(quest.Id);
        (string, Action) work = (quest, entry) switch
        {
            (null, _) => ("Any work for me?", () => Say(person, "Nothing I'd trust to a stranger.")),
            (_, null) => ("Any work for me?", () => _hud.Talk(Who(npc), quest.Pitch,
                ("I'll do it.", () => { Accept(quest); Say(person, "Good. The gods keep you."); }),
                ("Not now.", () => Say(person, "Suit yourself.")))),
            (_, { Stage: QuestStage.Done }) => ("Any work for me?", () => Say(person, "You've done enough for me already. Thank you.")),
            _ => ("About the job...", () => Say(person, $"Still waiting. {entry.Objective}.")),
        };
        List<(string, Action)> options = [("Who are you?", () => Say(person, npc.Backstory)), ("What do you care about?", () => Say(person, npc.InterestsSpeech))];
        if (!People.IsYouth(npc)) options.Add(work);
        options.AddRange(MoreTalk(person));
        options.Add(("Heard any news?", () => Say(person, Rumour(npc))));
        options.Add(("Farewell.", _hud.EndTalk));
        _hud.Talk(Who(npc), text, [.. options]);
    }

    static string Who(Npc npc) => $"{npc.Name}, {npc.Occupation}{(npc.Age > 0 ? $" ({npc.Age})" : "")}";

    /// <summary>People know what's near them. Asking puts a place on your map with a "?".</summary>
    string Rumour(Npc npc)
    {
        var place = _world.SettlementsNear(npc.X, npc.Z, 3000)
            .Where(s => !_explored.Discovered.Contains(s.Id) && !_explored.Rumored.Contains(s.Id))
            .OrderBy(s => s.DistanceTo(npc.X, npc.Z) * (s.IsCity ? 0.5 : 1)).FirstOrDefault();
        if (place is null) return "Nothing you won't have heard already." + DragonTalk(npc);
        _explored.Rumored.Add(place.Id);
        _hud.Toast($"{place.Name} is marked on your map.");
        var story = place.Story.Split(". ")[0].TrimEnd('.');
        return $"There's {place.What} called {place.Name}, {Direction(new Vector3((float)npc.X, 0, (float)npc.Z), (place.X, place.Z))} of here. {story}." + DragonTalk(npc);
    }

    /// <summary>What people say about the dragons: a war in the sky if there is one, or the ancient whose land is nearest.</summary>
    string DragonTalk(Npc npc)
    {
        var here = new Vector3((float)npc.X, 0, (float)npc.Z);
        if (Dragons.WarsNear(_world, _wilds.Now(), npc.X, npc.Z, 8000).FirstOrDefault() is { } war)
            return $" And keep your head down: {war.Story} The fighting is {Direction(here, (war.FrontX, war.FrontZ))} of here.";
        if (Dragons.Near(_world, npc.X, npc.Z, 9000).MinBy(a => a.DistanceTo(npc.X, npc.Z)) is { } a)
            return $" They say {a.FullName}, {a.What}, rules the land {Direction(here, (a.X, a.Z))} of here.";
        return "";
    }

    public void Accept(Quest quest)
    {
        _journal.Accept(quest);
        if (!_explored.Discovered.Contains(quest.TargetId) && _world.Find(quest.TargetId) is not null) _explored.Rumored.Add(quest.TargetId);
        if (quest.Kind is QuestKind.Slay or QuestKind.Fetch) _places.Restock(quest.TargetId);
        _hud.Toast($"New quest: {quest.Title}.");
        _hud.ShowJournal(_journal);
        Save();
    }

    void Apply(QuestUpdate u)
    {
        _hud.Toast(u.Text);
        if (u.Reward > 0) _player.Septims += _player.Class.Id == "Thief" ? (int)Math.Round(u.Reward * (1 + _player.Class.Passive.Power * _player.Hero.Power("light_fingers"))) : u.Reward;
        if (u.Stage == QuestStage.Done) GainXp(40 + u.Quest.Reward / 2);
        if (u.Stage == QuestStage.Done && ClassQuests.IsClassQuest(u.Quest.Id, out var discipline, out var step)) ClassTrialDone(discipline, step);
        if (u.Stage == QuestStage.Done) _hud.Toast($"Quest complete: {u.Quest.Title}.");
        _hud.ShowJournal(_journal);
        Save();
    }

    public void PickUp(Pickup item)
    {
        if (_raidLairs.FirstOrDefault(l => l.Hoard == item) is { } lair)
        {
            var raid = lair.Raid;
            var (xp, septims, first) = lair.Loot();
            _player.Septims += septims;
            var spoils = Loot.Hoard(raid.Id, _lootRng, first);
            Take(spoils);
            _hud.Log($"From {raid.Hoard}: {Loot.Describe(spoils)}");
            _hud.Toast($"{char.ToUpperInvariant(raid.Hoard[0])}{raid.Hoard[1..]}: {septims} septims, {raid.HoardHolds}. (+{xp} experience{(first ? ", double for the first time" : "")}) In your pack: {Loot.Describe(spoils)}. {char.ToUpperInvariant(raid.Name[0])}{raid.Name[1..]} will wake again in {raid.CooldownSeconds / 60:0} minutes.", 12);
            GainXp(xp);
            if (first && !_guilds.Titles.Contains(raid.Title)) { _guilds.Titles.Add(raid.Title); _hud.Toast($"You are {raid.Title}.", 8); }
            Save();
            return;
        }
        if (item.QuestId == "saga:egg")
        {
            if (Enemies().Any(e => Dragons.IsCorrupted(e.Kind.Id) && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(item.GlobalPosition) < 40))
            {
                _hud.Toast("Not with the corrupted drakes still at the nest.");
                return;
            }
            _saga.TakeEgg();
            _hud.Toast("You lift a dragon egg from the nest. It's heavy, and warm, and something inside it moves. Keep it warm until it hatches: rest (T) when you can.", 12);
            Save();
        }
        else if (_journal.PickedUp(item.QuestId) is { } u) Apply(u);
        item.QueueFree();
        if (item == _nest) _nest = null;
    }

    // ---- saving -------------------------------------------------------------------------------

    void Load()
    {
        var company = string.Concat(_player.Company.Where(char.IsLetterOrDigit));
        _savePath = _args.GetValueOrDefault("save", $"user://saves/{(company.Length > 0 ? company : "company")}.json");
        var scripted = _args.ContainsKey("selftest") || _args.ContainsKey("shots");
        if (scripted && !_args.ContainsKey("save")) _savePath = "user://saves/selftest.json";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_savePath).GetBaseDir());
        if (scripted && Godot.FileAccess.FileExists(_savePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_savePath));
        if (Choice.Account is not null) _savePath = $"user://saves/{string.Concat(Choice.Account.Where(char.IsLetterOrDigit))}-{company}.json";
        var stored = Choice.Save ?? (Choice.Account is null && Godot.FileAccess.FileExists(_savePath) ? Godot.FileAccess.GetFileAsString(_savePath) : null);
        if (stored is not null)
        {
            var save = CompanySave.FromJson(stored);
            if (save.Seed == _world.Seed)
            {
                save.Restore(_explored, _journal);
                _player.Septims = save.Septims;
                _player.Meat = save.Meat;
                _player.Pack = save.Pack ?? new Pack();
                _herbs = save.Herbs ?? new HerbBook();
                if (save.Hero is { } hero) _player.Become(hero);
                _bonds.AddRange(save.Companions);
                _saga = save.Saga ?? new DragonSaga();
                _guilds = save.Guilds ?? new GuildBook();
                _raids = save.Raids ?? new RaidBook();
                return;
            }
        }
        foreach (var c in _world.AuthoredCities.Where(c => c.StartsKnown)) _explored.Rumored.Add(c.Id); // Tristinwold and its neighbours
    }

    public string SavePath => _savePath;

    string? _lastSent;

    /// <summary>Saves on this device and, for a logged-in character, on the server too.</summary>
    public void Save()
    {
        var json = CompanySave.Capture(_world.Seed, _player.Septims, _explored, _journal, _player.Hero, _bonds, _saga, _player.Meat, _guilds, _raids, _player.Pack, _herbs).ToJson();
        using (var file = Godot.FileAccess.Open(_savePath, Godot.FileAccess.ModeFlags.Write)) file?.StoreString(json);
        if (Choice.Account is not null && _client?.Status == ClientStatus.Connected && json != _lastSent)
        {
            _client.Send(new SaveProgress(json, _player.Septims, _area));
            _lastSent = json;
        }
    }

    // ---- people and enemies ------------------------------------------------------------------

    void AddPeople()
    {
        // The Gallows Perch's keeper and the Magi archivist, written by hand; everyone else is generated.
        Townsfolk(new Npc("tristinwold/corva", "Mother Corva", Sex.Female, 0, "keeper of the Gallows Perch", "city:tristinwold", "Tristinwold",
            "Nobody remembers the Gallows Perch without Mother Corva behind the bar. Every contract on the board passes under her eye, and most of the ones that never reach it.",
            ["the Tongs", "gossip"], "dry", "Sit. Drink. The board's on the wall, and the Tongs are under the floor.",
            City.GallowsPerch.X - 2.5, City.GallowsPerch.Z + 11)); // by the door
        Townsfolk(new Npc("tristinwold/ilse", "Sister Ilse Varrow", Sex.Female, 0, "archivist of the Magi", "city:tristinwold", "Tristinwold",
            "Sister Ilse keeps the Magi Archive and its locked shelves. She answers questions with questions, and remembers every answer you give.",
            ["the Magi", "maps"], "wary", "The Archive is not a library. What are you looking for?", -34, 6));
    }

    void Townsfolk(Npc npc, string? look = null)
    {
        var at = new Vector3((float)npc.X, 0, (float)npc.Z);
        var person = new Person { Npc = npc, Look = look, Position = at with { Y = City.FloorAt(at) } };
        AddChild(person);
        _townsfolk.Add(person);
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

    static void DefineInputs(bool mouseButtons)
    {
        // A small deadzone, so a touch joystick only part way out still walks.
        void Key(string action, Key key) { if (!InputMap.HasAction(action)) InputMap.AddAction(action, 0.2f); InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key }); }
        void Mouse(string action, MouseButton b) { if (!InputMap.HasAction(action)) InputMap.AddAction(action, 0.2f); if (mouseButtons) InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = b }); }
        Key("move_forward", Godot.Key.W); Key("move_back", Godot.Key.S); Key("move_left", Godot.Key.A); Key("move_right", Godot.Key.D);
        Key("jump", Godot.Key.Space); Key("sprint", Godot.Key.Shift);
        Mouse("attack", MouseButton.Left); Mouse("spell", MouseButton.Right);
        Key("limit", Godot.Key.R); Key("cycle_element", Godot.Key.Q); Key("menu", Godot.Key.F10); Key("rest", Godot.Key.T);
        Key("interact", Godot.Key.E); Key("map", Godot.Key.M); Key("journal", Godot.Key.J);
        Key("party", Godot.Key.P); Key("skills", Godot.Key.K); Key("ride", Godot.Key.V); Key("descend", Godot.Key.C);
        Key("pack", Godot.Key.I); Key("drink", Godot.Key.H);
    }

    public override void _ExitTree()
    {
        if (_player is null) return; // still on the start screen
        Save();
        TearDownCompany();
        _client?.Dispose();
    }
}
