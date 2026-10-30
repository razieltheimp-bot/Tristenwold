using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
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
    string? _deviantJson;
    bool _limitWasReady;
    double _clock, _lastScore, _fightMusicUntil, _lastRespawnCheck, _lastReveal, _lastPlaces, _lastSave;
    readonly Dictionary<Lair, List<Enemy>> _lairs = [];
    readonly List<Person> _townsfolk = [];

    public override void _Ready()
    {
        ParseArgs();
        // Closing the window (or Android's back button) saves first: see _Notification.
        GetTree().AutoAcceptQuit = false;
        GetTree().QuitOnGoBack = false;
        if (RemotePlay.Current is { } playing)
        {
            // Back from logging out: the phone is still connected.
            playing.Main = this;
            _remoteTouch = playing.Viewers > 0;
            if (playing.Link is { } link) Callable.From(() => ShowRemoteLink(link)).CallDeferred();
        }
        else if (_args.TryGetValue("remote", out var remote))
        {
            // Remote play: a phone's browser shows and controls this game (with --tunnel, from anywhere).
            var port = int.TryParse(remote, out var p) ? p : 8090;
            RemotePlay.Current = new RemotePlay(this, port, _args.ContainsKey("tunnel"), _args.GetValueOrDefault("cloudflared", "cloudflared"), _args.ContainsKey("remote-lan"));
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, RemotePlay.Current);
        }
        if (Sound.Current is null) GetTree().Root.CallDeferred(Node.MethodName.AddChild, new Sound()); // kept across logging out, like remote play
        if (Mobile) DisplayServer.ScreenSetOrientation(DisplayServer.ScreenOrientation.SensorLandscape); // landscape from the first frame
        if (_args.TryGetValue("races", out var racesShot)) { _ = RaceLineup(racesShot); return; }
        if (_args.TryGetValue("portraits", out var folder)) { _ = Portraits(folder); return; }
        if (_args.ContainsKey("probe-hands")) { ProbeHands(); return; }
        if (_args.TryGetValue("showroom", out var room)) { _ = Showroom(room); return; }
        if (_args.TryGetValue("lineup", out var line)) { Proportions.Enabled = !_args.ContainsKey("plain"); _ = Lineup(line); return; }
        var scripted = _args.ContainsKey("selftest") || _args.ContainsKey("shots");
        if (!LeftToStart)
        {
            if (_args.ContainsKey("account") && _args.ContainsKey("server")) { _ = LogInFromCommandLine(); return; }
            if (scripted || _args.ContainsKey("server")) { Begin(ChoiceFromArgs()); return; }
        }
        var start = new StartScreen { DefaultServer = _args.GetValueOrDefault("login-server") };
        start.Play += Begin;
        AddChild(start);
        Callable.From(() => { Sound.Current?.SetMood("title"); Sound.Current?.SetPlace(""); }).CallDeferred();
        if (LeftToStart && _args.ContainsKey("selftest")) ReportBackAtStart();
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

    /// <summary>Every people side by side, and dragonkind pure and half, for checking how they look: --races shot.png</summary>
    async Task RaceLineup(string path)
    {
        var looks = new (string Class, string Look)[]
        {
            ("Paladin", "race=human;beard=Short;hair=Brown"),
            ("Barbarian", "race=orc;head=Barbarian;body=Barbarian;arms=Barbarian;legs=Barbarian;hat=none"),
            ("Thief", "race=goblin"),
            ("Ranger", "race=elf;head=Rogue;hat=none;hair=Blond"),
            ("Paladin", "race=dwarf;hat=none;beard=Braided;hair=Copper;build=Broad"),
            ("Barbarian", "race=half_giant;hat=none;beard=Long;hair=Black"),
            ("Sorcerer", "race=dragonkind;dragon=Red;half=none"),
            ("Paladin", "race=dragonkind;dragon=Gold;half=dwarf;beard=Forked;hair=Auburn;hat=none"),
            ("Wizard", "race=dragonkind;dragon=Silver;half=elf;horns=Long;hat=none"),
            ("Barbarian", "race=dragonkind;dragon=Green;half=orc;horns=Ram;hat=none"),
        };
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#2a241c"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#e8dcc8"), AmbientLightEnergy = 0.6f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, 25, 0), LightEnergy = 1.3f });
        for (var i = 0; i < looks.Length; i++)
        {
            var f = Figure.Create(looks[i].Class, Avatar.Parse(looks[i].Look, looks[i].Class));
            f.Position = new Vector3((i - (looks.Length - 1) / 2f) * 1.55f, 0, 0);
            f.RotationDegrees = new Vector3(0, 180 + (i % 2 == 0 ? 25 : -25), 0); // facing the camera
            AddChild(f);
        }
        var camera = new Camera3D { Fov = 40 };
        AddChild(camera);
        camera.Position = new Vector3(0, 1.6f, 12.5f);
        camera.LookAt(new Vector3(0, 1.1f, 0), Vector3.Up);
        camera.MakeCurrent();
        for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path);
        // And a close look at three of them from the side, where horns and tails show.
        camera.Position = new Vector3(-3.5f, 1.5f, 3.6f);
        camera.LookAt(new Vector3(-3.5f, 1.1f, 0), Vector3.Up);
        for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-side.png"));
        GetTree().Quit();
    }

    /// <summary>Prints the rest frames of a real body's hands and head (headless; to orient what they hold): --probe-hands</summary>
    void ProbeHands()
    {
        foreach (var file in new[] { "res://assets/humans/human_male_adult.glb", "res://assets/humans/dwarf_female_adult.glb" })
        {
            if (GD.Load<PackedScene>(file)?.Instantiate() is not Node3D model) { GD.Print($"no {file}"); continue; }
            var sk = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
            GD.Print($"== {file}: {sk.GetBoneCount()} bones; root {model.Name}");
            foreach (var bone in new[] { "Hips", "Spine", "Chest", "Neck", "Head", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand", "RightThumbProximal", "RightIndexProximal", "RightMiddleProximal", "RightMiddleDistal", "RightLittleProximal", "LeftHand", "LeftMiddleProximal", "LeftThumbProximal", "LeftIndexProximal" })
            {
                var i = sk.FindBone(bone);
                if (i < 0) { GD.Print($"  {bone}: none"); continue; }
                var t = sk.GetBoneGlobalRest(i);
                GD.Print($"  {bone}: origin {t.Origin.X:0.000},{t.Origin.Y:0.000},{t.Origin.Z:0.000}  X {t.Basis.X.X:0.00},{t.Basis.X.Y:0.00},{t.Basis.X.Z:0.00}  Y {t.Basis.Y.X:0.00},{t.Basis.Y.Y:0.00},{t.Basis.Y.Z:0.00}  Z {t.Basis.Z.X:0.00},{t.Basis.Z.Y:0.00},{t.Basis.Z.Z:0.00}");
            }
            var mesh = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First();
            var verts = (mesh.Mesh as ArrayMesh)!.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            GD.Print($"  body: {verts.Length} verts, top {verts.Max(v => v.Y):0.000}, front (max z) {verts.Max(v => v.Z):0.000}, back {verts.Min(v => v.Z):0.000}; surfaces {string.Join(", ", Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Select(k => mesh.Mesh.SurfaceGetMaterial(k)?.ResourceName ?? "-"))}");
            var headTop = verts.Max(v => v.Y);
            foreach (var band in new[] { 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.3f })
            {
                var inBand = verts.Where(v => v.Y > headTop - band).ToList();
                GD.Print($"    within {band:0.00} of the top: {inBand.Count} verts, x {inBand.Min(v => v.X):0.000}..{inBand.Max(v => v.X):0.000}, z {inBand.Min(v => v.Z):0.000}..{inBand.Max(v => v.Z):0.000}");
            }
            model.Free();
        }
        // KayKit's knight: where its hand slot sits and how its sword lies in it, to put real gear in a real hand the same way.
        if (Models.Scene("characters/Knight.glb")?.Instantiate() is Node3D knight)
        {
            var sk = knight.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
            GD.Print($"== KayKit Knight: {sk.GetBoneCount()} bones: {string.Join(" ", Enumerable.Range(0, sk.GetBoneCount()).Select(sk.GetBoneName))}");
            foreach (var bone in new[] { "hand.r", "handslot.r", "hand.l", "handslot.l", "head", "chest", "arm.r", "forearm.r" })
            {
                var i = sk.FindBone(bone);
                if (i < 0) { GD.Print($"  {bone}: none"); continue; }
                var t = sk.GetBoneGlobalRest(i);
                GD.Print($"  {bone}: origin {t.Origin.X:0.000},{t.Origin.Y:0.000},{t.Origin.Z:0.000}  X {t.Basis.X.X:0.00},{t.Basis.X.Y:0.00},{t.Basis.X.Z:0.00}  Y {t.Basis.Y.X:0.00},{t.Basis.Y.Y:0.00},{t.Basis.Y.Z:0.00}  Z {t.Basis.Z.X:0.00},{t.Basis.Z.Y:0.00},{t.Basis.Z.Z:0.00}");
            }
            foreach (var m in knight.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Name.ToString().Contains("Sword") || m.Name.ToString().Contains("Shield")))
            {
                var box = m.GetAabb();
                GD.Print($"  mesh {m.Name} under {m.GetParent()?.Name} ({m.GetParent()?.GetType().Name}{(m.GetParent() is BoneAttachment3D ba ? " bone " + ba.BoneName : "")}): pos {m.Position.X:0.000},{m.Position.Y:0.000},{m.Position.Z:0.000} rot {m.RotationDegrees.X:0},{m.RotationDegrees.Y:0},{m.RotationDegrees.Z:0} aabb from {box.Position.X:0.00},{box.Position.Y:0.00},{box.Position.Z:0.00} size {box.Size.X:0.00},{box.Size.Y:0.00},{box.Size.Z:0.00} skin {(m.Skin is null ? "no" : "yes")}");
                if (m.GetParent() is Node3D pn && pn is not Skeleton3D) GD.Print($"    parent {pn.Name} pos {pn.Position.X:0.000},{pn.Position.Y:0.000},{pn.Position.Z:0.000} rot {pn.RotationDegrees.X:0},{pn.RotationDegrees.Y:0},{pn.RotationDegrees.Z:0}");
            }
            knight.Free();
        }
        GetTree().Quit();
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
            ? ["Paladin", "giant_rat", "goblin", "spriggan", "frost_troll", "dragon", "wolf", "bear"]
            : _args.ContainsKey("young") ? ["Paladin", "wolf", "bear", "deer", "fox", "boar", "dragon_gold", "squire", "apprentice", "youth"]
            : _args.ContainsKey("crypt") ? ["cinder_thrall", "cinder_warden", "Paladin"]
            : _args.TryGetValue("only", out var only) ? only.Split(',')
            : _args.ContainsKey("animals") ? ["Paladin", "cow", "horse", "sheep", "pig", "dog", "cat", "hen", "eagle", "songbird", "fish", "whale"]
            : [.. Classes.All.Select(d => d.Look)];
        var figures = new List<Body>();
        for (var i = 0; i < looks.Length; i++)
        {
            var figure = Body.For(looks[i]);
            // Younglings and the young who follow a hero, beside a grown hero for scale.
            if (_args.ContainsKey("young") && i > 0 && i < 7) figure.Scale = Vector3.One * (looks[i].StartsWith("dragon") ? 0.22f : 0.45f);
            var spacing = _args.ContainsKey("hero") ? 11f : _args.ContainsKey("dragons") ? 9f : _args.ContainsKey("young") ? 1.15f : _args.ContainsKey("crypt") ? 1.9f : _args.ContainsKey("animals") ? 2.4f : 1.3f;
            figure.Position = new Vector3((i - (looks.Length - 1) / 2f) * spacing, 0, _args.ContainsKey("dragons") ? -(i % 2) * 7 : 0);
            figure.RotationDegrees = new Vector3(0, _args.ContainsKey("top") ? 0 : 160, 0);
            if (_args.ContainsKey("frost"))
            {
                var size = (DragonSize)i;
                figure.Scale = Vector3.One * (Dragons.Scale(size).Height / Dragons.Scale(DragonSize.Normal).Height);
                figure.Position = new Vector3(new[] { -14f, -4f, 12f }[i], 0, 0);
                figure.RotationDegrees = new Vector3(0, 110, 0); // side on, to see the ridge
            }
            if (_args.TryGetValue("scale", out var big)) figure.Scale *= float.Parse(big, System.Globalization.CultureInfo.InvariantCulture);
            AddChild(figure);
            figures.Add(figure);
        }
        var wide = _args.ContainsKey("dragons");
        var camera = new Camera3D { Position = _args.ContainsKey("top") ? new Vector3(0, _args.ContainsKey("only") ? 9 : 28, 0.01f) : _args.ContainsKey("animals") ? new Vector3(0, 3.5f, 30) : _args.ContainsKey("frost") ? new Vector3(0, 5, 34) : _args.ContainsKey("hero") ? new Vector3(0, 9, 30) : wide ? new Vector3(0, 14, 62) : new Vector3(0, 1.3f, 9f), Fov = 38, Far = 500 };
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
            // The characters page, playing alone so no server is needed (--ultra: at Ultra, for the real bodies' page).
            if (_args.ContainsKey("ultra")) start.SelectQuality(Graphics.Quality.Ultra);
            await start.PlayAloneForSnapshot();
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(second);
        }
        if (_args.TryGetValue("snap-look", out var third))
        {
            // The look editor, with a hero put together from four figures.
            if (!_args.ContainsKey("snap-characters")) await start.PlayAloneForSnapshot();
            start.LookForSnapshot(Avatar.Parse(_args.GetValueOrDefault("avatar", "head=Mage;body=Knight;arms=Barbarian;legs=Skeleton_Rogue;hat=Barbarian_Hat;outfit=Crimson;skin=Tan;height=Tall"))!);
            for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            start.ScrollToLook();
            for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(third);
        }
        GetTree().Quit();
    }

    public bool Mobile => OS.HasFeature("mobile");
    public bool TouchMode { get; private set; }
    public StartChoice Choice { get; private set; } = null!;

    void Begin(StartChoice choice)
    {
        if (_args.ContainsKey("selftest")) GD.PrintErr("[selftest] Begin");
        Choice = choice;
        (_args["company"], _args["server"], _args["password"]) = (choice.Character.Name, choice.Server, choice.ServerPassword);
        TouchMode = _args.ContainsKey("touch") || _remoteTouch || DisplayServer.IsTouchscreenAvailable();
        DefineInputs(mouseButtons: !TouchMode);
        City.Build(this);
        _terrain = new Terrain(_world);
        Graphics.Apply(choice.Quality, GetViewport(), City.Sky, City.Sun, _terrain);
        AddChild(_terrain);
        _terrain.EnsureAround(City.PlayerStart, 2);

        _hud = new Hud { PhonePad = _phonePad };
        AddChild(_hud);

        _player = new Player
        {
            Company = choice.Character.Name, Outfit = choice.Character.Look, Avatar = choice.Character.Avatar ?? _args.GetValueOrDefault("avatar"), Position = City.PlayerStart,
            Hero = new Hero { Discipline = Classes.For(choice.Character.Look).Id },
            Element = Enum.TryParse<DamageKind>(choice.Character.Element, out var element) ? element : DamageKind.Fire,
        };
        AddChild(_player);
        Poise.Hero = _player; // the creatures' eyes follow the hero
        _player.Said += text => _hud.Toast(text);
        _player.LimitUsed += (a, c) => _hud.LimitBanner(a.Name, a.Text, c);
        _player.WentDown += () => { _tracker.Downed(); _hud.Center("You have fallen."); };
        _player.Struck += (enemy, hit, kind) =>
        {
            if (enemy.Sparring || enemy.Yielded) return; // a duel in the yard isn't a battle
            _tracker.Engaged(enemy.Id, enemy.Kind.Id, enemy.Kind.Name, enemy.Stats.MaxHealth, ToNumerics(enemy.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, enemy.DeviantId);
            _tracker.Hit(enemy.Id, kind, _player.Company, _clock);
            if (hit.Killed) _tracker.Died(enemy.Id, _player.Company, _clock);
        };
        _hud.Company = _player.Company;

        Load();
        BeginStory();
        BeginTrade();
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
        AddTownWork();
        AddCaravanYard();
        AddShipyard();
        AddWatchHouse();
        AddShadows();
        AddPursuits();
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
        _watch = new TownWatch(this);
        AddChild(_watch);
        _sky = new Skylife(this);
        AddChild(_sky);
        _riders = new RiderFlights(this);
        AddChild(_riders);
        _raiders = new Raiders(this) { Record = _raidRecord };
        AddChild(_raiders);
        _life = new TownLife(this);
        AddChild(_life);
        SetUpCompany();
        _hud.AttachMap(_world, _explored, () => _player.GlobalPosition, () => -_player.GlobalTransform.Basis.Z, QuestGoals,
            () => _raids.Seen.Select(Raids.Get).OfType<Raid>().Select(r => (r.X, r.Z, r.Name)));

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
        if (_args.ContainsKey("selftest")) { GD.PrintErr("[selftest] world begun; adding the self-test"); AddChild(new SelfTest(this)); }
        else if (_args.TryGetValue("shots", out var shots)) AddChild(new Shots(this, shots, quick: _args.ContainsKey("quick"), dragon: _args.GetValueOrDefault("dragon"), skirmish: _args.ContainsKey("skirmish"), raid: _args.ContainsKey("raid"), lairs: _args.TryGetValue("lairs", out var lairs) ? lairs : null, wild: _args.ContainsKey("wild"), limit: _args.ContainsKey("limit"), market: _args.ContainsKey("market"), audit: _args.ContainsKey("audit"), spells: _args.ContainsKey("spells"), town: _args.ContainsKey("town")));
        else _hud.Toast($"Tristinwold. You are a {_player.Class.Name}. Esc: menu (save, log out)   M: map   J: journal   E: talk   K: skills   P: party   F10: the shared world");
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
        RemoteLabel("RemoteLink", 4).Text = $"Phone link: {link}";
        ShowRemoteQr(link);
    }

    /// <summary>
    /// The phone link as a QR code in the corner of this computer's screen: point the iPhone's camera at it and tap
    /// the link that pops up, no Telegram needed. It steps aside while a phone is playing.
    /// </summary>
    void ShowRemoteQr(string link)
    {
        var layer = GetNodeOrNull<CanvasLayer>("RemoteLayer");
        if (layer is null) return;
        layer.GetNodeOrNull("RemoteQr")?.Free();
        using var generator = new QRCoder.QRCodeGenerator();
        var modules = generator.CreateQrCode(link, QRCoder.QRCodeGenerator.ECCLevel.M).ModuleMatrix;
        var n = modules.Count;
        var image = Image.CreateEmpty(n, n, false, Image.Format.Rgb8);
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                image.SetPixel(x, y, modules[y][x] ? Colors.Black : Colors.White);
        var box = new VBoxContainer { Name = "RemoteQr" };
        box.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        (box.OffsetLeft, box.OffsetTop, box.OffsetRight) = (-250, 236, -24);
        box.AddChild(new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image), CustomMinimumSize = new Vector2(226, 226),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        });
        box.AddChild(new Label
        {
            Text = "Scan with the iPhone's camera", HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 14, OutlineSize = 4, OutlineColor = Colors.Black },
        });
        layer.AddChild(box);
        box.Visible = RemotePlay.Current is not { Viewers: > 0 };
    }

    /// <summary>The QR code steps aside while a phone is playing, and comes back when none is.</summary>
    public void RemoteQrVisible(bool visible)
    {
        if (GetNodeOrNull<CanvasLayer>("RemoteLayer")?.GetNodeOrNull<Control>("RemoteQr") is { } qr) qr.Visible = visible;
    }

    readonly List<string> _remoteNotes = [];

    /// <summary>What remote play has to say (the tunnel, Telegram), in the corner of this computer's screen,
    /// since the game has no console window to print it in.</summary>
    public void RemoteNote(string text)
    {
        _remoteNotes.Add(text);
        if (_remoteNotes.Count > 3) _remoteNotes.RemoveAt(0);
        RemoteLabel("RemoteNotes", 24).Text = string.Join("\n", _remoteNotes);
    }

    /// <summary>A line of remote play's at the bottom of the screen, clear of the bars, the map and the buttons.</summary>
    Label RemoteLabel(string name, float y)
    {
        var layer = GetNodeOrNull<CanvasLayer>("RemoteLayer");
        if (layer is null) AddChild(layer = new CanvasLayer { Layer = 100, Name = "RemoteLayer" });
        var box = layer.GetNodeOrNull<VBoxContainer>("RemoteLines");
        if (box is null)
        {
            // Tucked into the bottom-right corner, small, so it never sits across the start screen's buttons.
            box = new VBoxContainer { Name = "RemoteLines", GrowVertical = Control.GrowDirection.Begin, GrowHorizontal = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            box.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            (box.OffsetTop, box.OffsetBottom, box.OffsetLeft, box.OffsetRight) = (-6, -6, -12, -12);
            box.AddThemeConstantOverride("separation", 0);
            layer.AddChild(box);
        }
        if (box.GetNodeOrNull<Label>(name) is { } label) return label;
        label = new Label
        {
            Name = name, Modulate = new Color(1, 1, 1, 0.7f), HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            LabelSettings = new LabelSettings { FontSize = 11, OutlineSize = 3, OutlineColor = Colors.Black },
        };
        box.AddChild(label);
        if (name == "RemoteLink") box.MoveChild(label, -1); // the link last, at the very bottom
        return label;
    }

    /// <summary>An upright phone plays with the controller under its picture, so the game's own on-screen
    /// controls step aside while it does (they come back when the phone is turned sideways).</summary>
    public void PhonePad(bool on)
    {
        _phonePad = on;
        if (_hud is not null) _hud.PhonePad = on;
    }

    bool _phonePad;

    /// <summary>A drag on an upright phone's picture turns the view.</summary>
    public void LookFromPhone(Vector2 by)
    {
        if (_player is not null && IsInstanceValid(_player)) _player.Look(by);
    }

    /// <summary>What an upright phone's buttons should say (the class's moves) and which should glow.</summary>
    public Dictionary<string, object>? PadState()
    {
        if (_player is null || _hud is null || !IsInstanceValid(_player)) return null;
        string[] moves = ["attack", "spell", "cycle_element", "limit", "interact", "ride", "descend", "jump"];
        return new()
        {
            ["labels"] = moves.Where(a => _hud.Touch.LabelFor?.Invoke(a) is not null).ToDictionary(a => a, a => _hud.Touch.LabelFor!(a)!),
            ["lit"] = moves.Where(a => _hud.Touch.Lit?.Invoke(a) == true).ToDictionary(a => a, _ => true),
        };
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
            case DeviantAppears a:
                foreach (var spawn in a.Spawns)
                {
                    if (_tracker.ActorForSlot(spawn.Slot) is not { } id || FindEnemy(id) is not { } e) continue;
                    if (Raids.All.Any(r => r.BossKind == e.Kind.Id) && !e.Named) continue; // a lair's boss is a nobody until it beats someone
                    e.MakeDeviant(spawn.DeviantId, spawn.Modifiers, spawn.Taunt);
                    _tracker.MarkDeviant(spawn.Slot, spawn.DeviantId);
                    _hud.Toast(spawn.Taunt is null ? $"{e.Stats.Name} is here." : $"{e.Stats.Name}: \"{spawn.Taunt}\"");
                }
                break;
            case DeviantHunts h:
            {
                // Out of sight behind the player, then it comes for them.
                var behind = _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 25;
                var hunter = Enemy.Create(Bestiary.Get(h.BaseForm), behind with { Y = City.FloorAt(behind) + 0.5f }, WorldGen.Danger(behind.X, behind.Z));
                AddEnemy(hunter);
                hunter.MakeDeviant(h.DeviantId, h.Modifiers, h.Taunt);
                hunter.Provoke(_player);
                _tracker.Engaged(hunter.Id, hunter.Kind.Id, hunter.Kind.Name, hunter.Stats.MaxHealth, ToNumerics(hunter.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, h.DeviantId);
                _hud.Toast($"{hunter.Stats.Name} has found you.");
                break;
            }
            case Reward r: _player.Crowns += r.Coppers / 100; _hud.Toast($"Contract paid: {r.Coppers / 100} crowns."); break;
            case Board b: _hud.ShowBoard(b.Contracts); break;
            case CitySnapshot c: TakeCity(c); break;
            case Roster r: _hud.ShowCompanies(r.Groups); break;
            case DeviantSnapshot s: _hud.ShowDeviants(s.WorldJson); _deviantJson = s.WorldJson; break;
            case DeviantEvents e:
                foreach (var ev in e.Events)
                {
                    _hud.Log(ev.Text);
                    if (ev.Kind == WorldEventKind.Born && _deviantJson is { } json && DeviantWorld.FromJson(json).Find(ev.DeviantId) is { } born) _roamers.Named(born);
                    if (ev.Kind == WorldEventKind.Returned) _roamers.Returned(ev.DeviantId);
                }
                break;
            case Drops d: foreach (var note in d.Notes) _hud.Toast($"A hidden note from {note.From}: \"{note.Text}\""); break;
            case KeystoneRevealed k: _hud.Toast($"{k.Character} of {k.Player}'s company is a Keystone."); break;
        }
    }

    // ---- music and ambience ---------------------------------------------------------------------

    /// <summary>
    /// The music and the sound of the place, for where the hero is and what's happening: a boss (a named foe, a
    /// deviant, a grown dragon, a raid) or a fight, a tavern, the Bards' College, the city, or the open wilds; the
    /// fire in a tavern, the city's bustle, gulls on the docks, a cave's drip, wind up high or in the snow, the
    /// wilds by day or (by this device's clock) by night.
    /// </summary>
    void Score(Vector3 pos)
    {
        if (Sound.Current is not { } sound) return;
        var boss = Enemies().Any(e => e.FightingCompany && (e.Named || e.DeviantId is not null || e.IsDragon && !e.Young) && e.GlobalPosition.DistanceTo(pos) < 60)
                   || _raidLairs.Any(l => l.Fighting);
        if (_player.InFight || boss) _fightMusicUntil = _clock + (boss ? 8 : 5); // let the fight music ring on a moment after
        var college = Guilds.Of(Calling.Bard);
        var atCollege = new Vector2((float)college.X - pos.X, (float)college.Z - pos.Z).Length() < 26;
        var tavern = _area is "The Gallows Perch" or "The Salt Barrel";
        var town = new Vector2(pos.X, pos.Z).Length() < City.OuterRadius + 15 || _area is "The Docks";
        sound.SetMood(boss ? "boss" : _clock < _fightMusicUntil ? "battle" : tavern ? "tavern" : atCollege ? "bard" : town ? "town" : "explore");
        var high = _player.Mount is { Airborne: true } || _world.BiomeAt(pos.X, pos.Z) is Biome.Snowfield or Biome.Tundra or Biome.Crags || pos.Y > 45;
        var hour = DateTime.Now.Hour;
        sound.SetPlace(tavern ? "fire"
            : _area is "The Docks" or "The Harbor" or "The Tong Sea-Caves" && !_area.Contains("Cave") ? "docks"
            : _area.Contains("Cave") || _area.Contains("Crypt") || _area is "The Warrens" ? "cave"
            : town ? "city"
            : high ? "wind"
            : hour >= 20 || hour < 6 ? "wilds_night" : "wilds_day");
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
        if (_clock - _lastScore > 0.5) { _lastScore = _clock; Score(pos); }
        if (_client?.Status == ClientStatus.Connected && _area != _sentArea)
        {
            _sentArea = _area;
            _client.Send(new Presence(_area, [_player.Company]));
        }

        foreach (var e in Enemies().Where(e => e.FightingCompany && !e.Sparring && !e.Yielded))
            _tracker.Engaged(e.Id, e.Kind.Id, e.Kind.Name, e.Stats.MaxHealth, ToNumerics(e.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, e.DeviantId);
        _tracker.Tick(_clock, ToNumerics(_player.GlobalPosition),
            id => FindEnemy(id) is { FightingCompany: true } e && e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 40,
            id => FindEnemy(id) is { } e ? ToNumerics(e.GlobalPosition) : null);
        _player.InFight = _tracker.InFight || _player.Attackers().Any();
        _player.Frozen = _hud.Talking || _hud.MapOpen || _working;
        TendCity(delta);
        TendLine(delta);
        TendCompany(delta);
        TendGuilds(delta);
        TendPursuits(delta);
        TendFarLairs();
        TendStory();
        TendCaravans(delta);
        TendSea(delta);
        TendStanding(delta);
        TendArtificer(delta);
        _player.GuildTitle = $"{_guilds.Rank(_player.Class.Calling)} of {Guilds.Of(_player.Class.Calling).Name}";

        if (_player.Stats.LimitReady && !_limitWasReady)
            _hud.Toast($"Limit break ready: {_player.Class.Limit.Name}. {(TouchMode ? "Tap Limit" : "Press R")}!", 5);
        _limitWasReady = _player.Stats.LimitReady;
        if (_clock - _lastRespawnCheck > 30) { _lastRespawnCheck = _clock; RespawnEmptyLairs(); }
        if (_clock - _lastSave > 20) { _lastSave = _clock; Save(); }

        var near = Nearest();
        if (!_player.Downed) _hud.Center(near is null || _hud.Talking ? "" : $"E: {near.Value.Verb}");
        if (Input.IsActionJustPressed("interact"))
        {
            if (_hud.Talking) _hud.EndTalk();
            else near?.Act();
        }
        if (Input.IsActionJustPressed("save")) _ = SaveNow(announce: true);
        if (Input.IsActionJustPressed("game_menu")) Back();
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
        _hud.Update(_player, _area, TouchMode ? status : status + $"    Esc: menu   F5: save   M: map   J: journal   E: talk   K: skills   I: pack   H: heal   P: party   T: rest   Q: {_player.Class.Utility.Name.ToLowerInvariant()}   R: limit{(_bonds.Any(b => b.Rideable && b.Following) ? "   V: ride" : "")}");
        var saga = _saga.Objective(Dragon, _player.Hero.Level) is { } next ? [$"The Dragon Knight: {next}{(_saga.Goal(Dragon) is { } g ? $" ({Direction(pos, g)})" : "")}"] : Array.Empty<string>();
        if (StoryLine(pos) is { } story) saga = [story, .. saga];
        // The nearest lair that suits the hero, and the nearest deviant out roaming.
        if (_suited is { } raid && LairOf(raid).Tracker(pos, Direction) is { } raidLine) saga = [.. saga, raidLine];
        if (_roamers.Tracker(pos, Direction) is { } roamerLine) saga = [.. saga, roamerLine];
        if (_raiders.Tracker(pos, Direction) is { } clanLine) saga = [.. saga, clanLine];
        saga = [.. saga, .. TownLines(pos), .. CaravanLines(pos), .. SeaLines(pos), .. StandingLines(pos), .. ArtificerLines(pos), .. ShadowLines(pos), .. PursuitLines(pos)];
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
            if (TownMarket.Trades(s)) _trade.Market(s, _city.Day); // known now: its market is remembered, and Tristinwold's caravans find it
            _hud.Log(s.Story);
        }
    }

    IEnumerable<(double X, double Z)> QuestGoals() => _journal.Open.Select(e => e.Goal).Concat(_saga.Goal(Dragon) is { } g ? [g] : []).Concat(_storyGoal is { } s ? [s] : [])
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
        if (FishingAction() is { } fishing) return fishing;
        var person = _places.People.Concat(_townsfolk).Where(p => p.GlobalPosition.DistanceTo(pos) < 3.5f).MinBy(p => p.GlobalPosition.DistanceTo(pos));
        if (person is not null) return ($"talk to {person.Npc.Name}", () => Talk(person));
        var item = _places.Pickups.Append(_nest).Concat(_raidLairs.Select(l => l.Hoard)).OfType<Pickup>().Where(p => IsInstanceValid(p) && p.GlobalPosition.DistanceTo(pos) < 3).FirstOrDefault();
        if (item is not null) return ($"take {item.Item}", () => PickUp(item));
        if (_herbPatch.Within(pos) is { } plant) return ($"pick {Items.Get(plant.HerbId)?.Name ?? plant.HerbId}", () => Pick(plant));
        if (YounglingNear(pos) is { } young) return ($"tame the {young.Stats.Name}{(young.IsDragon ? " (a dragon: it will weigh you)" : _player.Meat > 0 ? " (offer meat)" : "")}", () => Tame(young));
        return SeaAction(pos) ?? TownAction(pos) ?? CaravanAction(pos) ?? ClanAction(pos) ?? ArtificerAction(pos) ?? PursuitAction(pos);
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

    static string Who(Npc npc) => $"{npc.Name}, {npc.Occupation}{(npc.Age > 0 ? $" ({npc.Age}, {Ageing.Word(Ageing.Stage(npc.Race, npc.Age))} {Races.Get(npc.Race).Name.ToLowerInvariant()})" : "")}";

    /// <summary>People know what's near them. Asking puts a place on your map with a "?".</summary>
    string Rumour(Npc npc)
    {
        // Townsfolk talk about the market too: what's dear, and what's happened.
        if (npc.HomeId == "city:tristinwold" && _city.News.Count > 0 && _lootRng.Chance(0.4)) return $"Have you heard? {_city.News[^1]}";
        // And about you.
        if (npc.HomeId == "city:tristinwold" && _standing.Gossip(_lootRng) is { } gossip) return gossip;
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
        Sound.Ui("quest", -3);
        _hud.ShowJournal(_journal);
        Save();
    }

    void Apply(QuestUpdate u)
    {
        _hud.Toast(u.Text);
        if (u.Reward > 0) _player.Crowns += _player.Class.Id == "Thief" ? (int)Math.Round(u.Reward * (1 + _player.Class.Passive.Power * _player.Hero.Power("light_fingers"))) : u.Reward;
        if (u.Stage == QuestStage.Done) GainXp(40 + u.Quest.Reward / 2);
        if (u.Stage == QuestStage.Done) _standing.Good(1, $"saw to {u.Quest.Title.ToLowerInvariant()}");
        if (u.Stage == QuestStage.Done && ClassQuests.IsClassQuest(u.Quest.Id, out var discipline, out var step)) ClassTrialDone(discipline, step);
        if (u.Stage == QuestStage.Done) { _hud.Toast($"Quest complete: {u.Quest.Title}."); Sound.Ui("quest"); }
        _hud.ShowJournal(_journal);
        Save();
    }

    public void PickUp(Pickup item)
    {
        if (_raidLairs.FirstOrDefault(l => l.Hoard == item) is { } lair)
        {
            var raid = lair.Raid;
            var (xp, crowns, first) = lair.Loot();
            _player.Crowns += crowns;
            var spoils = Loot.Hoard(raid.Id, _lootRng, first);
            Take(spoils);
            _hud.Log($"From {raid.Hoard}: {Loot.Describe(spoils)}");
            _hud.Toast($"{char.ToUpperInvariant(raid.Hoard[0])}{raid.Hoard[1..]}: {crowns} crowns, {raid.HoardHolds}. (+{xp} experience{(first ? ", double for the first time" : "")}) In your pack: {Loot.Describe(spoils)}. {char.ToUpperInvariant(raid.Name[0])}{raid.Name[1..]} will wake again in {raid.CooldownSeconds / 60:0} minutes.", 12);
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
        _city = CityEconomy.Start(_world.Seed);
        _pursuits = new PursuitBook();
        var stored = Choice.Save ?? (Choice.Account is null && Godot.FileAccess.FileExists(_savePath) ? Godot.FileAccess.GetFileAsString(_savePath) : null);
        if (stored is not null)
        {
            var save = CompanySave.FromJson(stored);
            if (save.Seed == _world.Seed)
            {
                save.Restore(_explored, _journal);
                _player.Crowns = save.Crowns;
                _player.Meat = save.Meat;
                _player.Pack = save.Pack ?? new Pack();
                _herbs = save.Herbs ?? new HerbBook();
                if (save.Hero is { } hero) _player.Become(hero);
                _bonds.AddRange(save.Companions);
                _saga = save.Saga ?? new DragonSaga();
                _story = save.Story ?? new ImmortalSaga();
                _trade = save.Trade ?? new TradeBook();
                _raidRecord = save.Warbands ?? new RaidRecord();
                _sea = save.Sea ?? new SeaBook();
                _standing = save.Standing ?? new Standing();
                _guilds = save.Guilds ?? new GuildBook();
                _raids = save.Raids ?? new RaidBook();
                _city = save.City ?? CityEconomy.Start(_world.Seed);
                _pursuits = save.Pursuits ?? new PursuitBook();
                if (!scripted && save.Where is { } where) StandAt(where);
                return;
            }
        }
        foreach (var c in _world.AuthoredCities.Where(c => c.StartsKnown)) _explored.Rumored.Add(c.Id); // Tristinwold and its neighbours
    }

    public string SavePath => _savePath;

    string? _lastSent;

    /// <summary>Saves on this device and, for a logged-in character, on the server too.</summary>
    public string Save()
    {
        var json = CompanySave.Capture(_world.Seed, _player.Crowns, _explored, _journal, _player.Hero, _bonds, _saga, _player.Meat, _guilds, _raids, _player.Pack, _herbs, Here(), _city, _pursuits, _story, _trade, _raiders?.Record ?? _raidRecord, _sea, _standing).ToJson();
        using (var file = Godot.FileAccess.Open(_savePath, Godot.FileAccess.ModeFlags.Write)) file?.StoreString(json);
        _savedAt = _clock;
        if (Choice.Account is not null && _client?.Status == ClientStatus.Connected && json != _lastSent)
        {
            _client.Send(new SaveProgress(json, _player.Crowns, _area));
            _lastSent = json;
        }
        return json;
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

    public Rng LootRng => _lootRng;

    /// <summary>Someone met away from home (a port's harbour master come aboard), who can be talked to until they go.</summary>
    public Person MeetAt(Npc npc, string? look = null)
    {
        Townsfolk(npc, look);
        return _townsfolk[^1];
    }

    public void Dismiss(Person person)
    {
        _townsfolk.Remove(person);
        if (IsInstanceValid(person)) person.QueueFree();
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
        Key("save", Godot.Key.F5); Key("game_menu", Godot.Key.Escape);
    }

    public override void _ExitTree()
    {
        if (_player is null) return; // still on the start screen
        Save();
        TearDownCompany();
        _client?.Dispose();
    }
}
