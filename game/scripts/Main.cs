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
    EncounterTracker _tracker = null!;
    IGameLink? _client;
    string _area = "", _sentArea = "", _savePath = "";
    double _clock, _lastRespawnCheck, _lastReveal, _lastPlaces, _lastSave;
    readonly Dictionary<Lair, List<Enemy>> _lairs = [];
    readonly List<Person> _townsfolk = [];

    public override void _Ready()
    {
        ParseArgs();
        var scripted = _args.ContainsKey("selftest") || _args.ContainsKey("shots");
        if (scripted || _args.ContainsKey("server")) { Begin(); return; }
        var start = new StartScreen();
        start.Play += (company, server, password) =>
        {
            (_args["company"], _args["server"], _args["password"]) = (company, server, password);
            Begin();
        };
        AddChild(start);
        if (_args.TryGetValue("snap", out var snap)) _ = Snap(start, snap);
    }

    /// <summary>Saves a picture of the start screen and carries on, for checking its layout.</summary>
    async Task Snap(StartScreen start, string path)
    {
        start.Fill("Joe", "https://seasonal-deck-organisms-sf.trycloudflare.com", "secret");
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    public bool Mobile => OS.HasFeature("mobile");
    public bool TouchMode { get; private set; }

    void Begin()
    {
        TouchMode = _args.ContainsKey("touch") || DisplayServer.IsTouchscreenAvailable();
        DefineInputs(mouseButtons: !TouchMode);
        City.Build(this, fog: Mobile ? 0.006f : 0.0035f);
        _terrain = new Terrain(_world) { ViewChunks = Mobile ? 4 : 7 };
        AddChild(_terrain);
        _terrain.EnsureAround(City.PlayerStart, 2);

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
            if (!hit.Killed) return;
            _tracker.Died(enemy.Id, _player.Company, _clock);
            if (enemy.SiteId is { } site)
            {
                foreach (var u in _journal.Killed(site).ToList()) Apply(u);
                _places.KilledAt(site);
            }
        };
        _hud.Company = _player.Company;

        Load();
        AddPeople();
        foreach (var lair in City.Lairs) _lairs[lair] = SpawnLair(lair);
        _places = new Places(this, _world);
        AddChild(_places);
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
        else if (_args.TryGetValue("shots", out var shots)) AddChild(new Shots(this, shots));
        else _hud.Toast("Tristinwold. M: map   J: journal   E: talk   F10: the shared world");
    }

    // ---- the shared world ------------------------------------------------------------------------

    void Connect()
    {
        var server = _args.GetValueOrDefault("server", "127.0.0.1:7777");
        if (server == "off") { _hud.Toast("Playing alone: no shared world."); return; }
        var password = _args.GetValueOrDefault("password", "");
        _client = IGameLink.Open(server, () => new Hello(_player.Company, password, Wire.ProtocolVersion, [_player.Company]));
        _hud.Send = m => _client?.Send(m);
        _client.Start();
    }

    /// <summary>Turns on the on-screen controls (and stops a tap anywhere from counting as a sword swing).</summary>
    public void EnableTouch()
    {
        TouchMode = true;
        foreach (var action in new[] { "attack", "spell" })
            foreach (var e in InputMap.ActionGetEvents(action).OfType<InputEventMouseButton>()) InputMap.ActionEraseEvent(action, e);
        _hud.TouchEnabled = true;
        _hud.Touch.Look = by => _player.Look(by);
        _hud.Touch.LabelFor = action => action == "cycle_element" ? _player.Element.ToString() : null;
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
            case NemesisSnapshot s: _hud.ShowNemeses(s.WorldJson); break;
            case NemesisEvents e: foreach (var ev in e.Events) _hud.Log(ev.Text); break;
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
        if (_clock - _lastPlaces > 0.5) { _lastPlaces = _clock; _places.Update(pos); }
        if (_clock - _lastReveal > 0.2) { _lastReveal = _clock; Explore(pos); }

        _area = City.AreaAt(pos);
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
        _player.Frozen = _hud.Talking || _hud.MapOpen;

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
        if (Input.IsActionJustPressed("rest") && !_tracker.InFight)
        {
            _client?.Send(new Rested());
            _hud.Toast("You rest. The world moves on without you.");
        }
        var status = _client is null ? "alone" : $"{_player.Company} | {_client.Status}{(_client.Status != ClientStatus.Connected && _client.LastError is { } err ? $" ({err})" : "")}";
        _hud.Update(_player, _area, TouchMode ? status : status + "    M: map   J: journal   E: talk   F10: shared world   T: rest   Q: element   R: limit");
        _hud.Track(_journal.Open.Take(4).Select(e => $"{e.Quest.Title}: {e.Objective} ({Direction(pos, e.Goal)})"));
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

    IEnumerable<(double X, double Z)> QuestGoals() => _journal.Open.Select(e => e.Goal);

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
        var item = _places.Pickups.Where(p => p.GlobalPosition.DistanceTo(pos) < 3).FirstOrDefault();
        if (item is not null) return ($"take {item.Item}", () => PickUp(item));
        return null;
    }

    /// <summary>Starts a conversation: news for any quest first, then whatever you want to ask.</summary>
    public void Talk(Person person)
    {
        _player.Face(person.GlobalPosition);
        var news = _journal.TalkedTo(person.Npc.Id).ToList();
        foreach (var u in news) Apply(u);
        if (person.Captive)
        {
            _hud.Talk(person.Npc.Name, news.FirstOrDefault()?.Text ?? person.Npc.Greeting, ("Go. Run.", _hud.EndTalk));
            person.QueueFree();
            return;
        }
        Say(person, news.Count > 0 ? news[^1].Text : person.Npc.Greeting);
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
        _hud.Talk(Who(npc), text,
            ("Who are you?", () => Say(person, npc.Backstory)),
            ("What do you care about?", () => Say(person, npc.InterestsSpeech)),
            work,
            ("Heard any news?", () => Say(person, Rumour(npc))),
            ("Farewell.", _hud.EndTalk));
    }

    static string Who(Npc npc) => $"{npc.Name}, {npc.Occupation}{(npc.Age > 0 ? $" ({npc.Age})" : "")}";

    /// <summary>People know what's near them. Asking puts a place on your map with a "?".</summary>
    string Rumour(Npc npc)
    {
        var place = _world.SettlementsNear(npc.X, npc.Z, 3000)
            .Where(s => !_explored.Discovered.Contains(s.Id) && !_explored.Rumored.Contains(s.Id))
            .OrderBy(s => s.DistanceTo(npc.X, npc.Z) * (s.IsCity ? 0.5 : 1)).FirstOrDefault();
        if (place is null) return "Nothing you won't have heard already.";
        _explored.Rumored.Add(place.Id);
        _hud.Toast($"{place.Name} is marked on your map.");
        var story = place.Story.Split(". ")[0].TrimEnd('.');
        return $"There's {place.What} called {place.Name}, {Direction(new Vector3((float)npc.X, 0, (float)npc.Z), (place.X, place.Z))} of here. {story}.";
    }

    public void Accept(Quest quest)
    {
        _journal.Accept(quest);
        if (!_explored.Discovered.Contains(quest.TargetId)) _explored.Rumored.Add(quest.TargetId);
        _hud.Toast($"New quest: {quest.Title}.");
        _hud.ShowJournal(_journal);
        Save();
    }

    void Apply(QuestUpdate u)
    {
        _hud.Toast(u.Text);
        if (u.Reward > 0) _player.Septims += u.Reward;
        if (u.Stage == QuestStage.Done) _hud.Toast($"Quest complete: {u.Quest.Title}.");
        _hud.ShowJournal(_journal);
        Save();
    }

    public void PickUp(Pickup item)
    {
        if (_journal.PickedUp(item.QuestId) is { } u) Apply(u);
        item.QueueFree();
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
        if (Godot.FileAccess.FileExists(_savePath))
        {
            var save = CompanySave.FromJson(Godot.FileAccess.GetFileAsString(_savePath));
            if (save.Seed == _world.Seed)
            {
                save.Restore(_explored, _journal);
                _player.Septims = save.Septims;
                return;
            }
        }
        foreach (var c in _world.AuthoredCities.Where(c => c.StartsKnown)) _explored.Rumored.Add(c.Id); // Tristinwold and its neighbours
    }

    public string SavePath => _savePath;

    public void Save()
    {
        using var file = Godot.FileAccess.Open(_savePath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(CompanySave.Capture(_world.Seed, _player.Septims, _explored, _journal).ToJson());
    }

    // ---- people and enemies ------------------------------------------------------------------

    void AddPeople()
    {
        // The Gallows Perch's keeper and the Magi archivist, written by hand; everyone else is generated.
        Townsfolk(new Npc("tristinwold/corva", "Mother Corva", Sex.Female, 0, "keeper of the Gallows Perch", "city:tristinwold", "Tristinwold",
            "Nobody remembers the Gallows Perch without Mother Corva behind the bar. Every contract on the board passes under her eye, and most of the ones that never reach it.",
            ["the Tongs", "gossip"], "dry", "Sit. Drink. The board's on the wall, and the Tongs are under the floor.",
            City.GallowsPerch.X - 7, City.GallowsPerch.Z + 6));
        Townsfolk(new Npc("tristinwold/ilse", "Sister Ilse Varrow", Sex.Female, 0, "archivist of the Magi", "city:tristinwold", "Tristinwold",
            "Sister Ilse keeps the Magi Archive and its locked shelves. She answers questions with questions, and remembers every answer you give.",
            ["the Magi", "maps"], "wary", "The Archive is not a library. What are you looking for?", -34, 6));
    }

    void Townsfolk(Npc npc)
    {
        var at = new Vector3((float)npc.X, 0, (float)npc.Z);
        var person = new Person { Npc = npc, Position = at with { Y = City.FloorAt(at) } };
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
    }

    public override void _ExitTree()
    {
        if (_player is null) return; // still on the start screen
        Save();
        _client?.Dispose();
    }
}
