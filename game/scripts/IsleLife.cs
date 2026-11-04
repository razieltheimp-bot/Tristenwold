using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Brasswake, the moving island (see <see cref="Brasswake"/>), and the trade route to it. Marit Hale will tell you how far
/// out it is today, and any ship may tie up at its Strangers' Dock and trade from the hold. Aboard, a hero's crowns are worth
/// nothing: the work on the valves and in the air crews pays in the island's own marks, marks buy what it makes, and
/// that stays behind when the hero goes. A hero who's earned enough is made a citizen, and a citizen can light the
/// brass beacon and have a skiff sent for them.
/// </summary>
public partial class Main
{
    const string IsleWarden = "isle/warden", Dockmaster = "isle/dockmaster", BrassFactor = "tristinwold/brassfactor", Archwright = "isle/archwright", Engineer = "isle/engineer", Stores = "isle/stores", Skymaster = "isle/skymaster";
    TheIsle? _isle;

    public TheIsle? IsleNode => _isle;
    IsleBook Isle => _sea.Isle;
    bool Tinker => _player.Class.Calling == Calling.Artificer;
    int IsleAway => Brasswake.Leagues(_guilds.PlaySeconds);

    // ---- the trade route -----------------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> IsleRoute(Person person)
    {
        var id = person.Npc.Id;
        if (id is not (HarbourMaster or Smuggler or BrassFactor)) yield break;
        var away = $"{IsleAway} day{(IsleAway == 1 ? "" : "s")} out";
        yield return ("Where's Brasswake today?", () => Say(person, id == BrassFactor
            ? $"{away}, and moving. It's always moving: the island's a ship, and the ship doesn't stop. Tie up at the Strangers' Dock and they'll trade with you. Timber, ale and herbs is what they're short of, iron and silver is what they've got, and anything cleverer than that stays where it is."
            : $"{away}, by the last word from the Brass Factor's board. Artificers who walked out on the mainland two hundred years back, took everything they'd invented, bolted a hull to a rock and went to sea. They'll take any ship that comes alongside. Not a dragon, though, and not anyone on one."));
        if (_sea.Voyage is null)
        {
            if (id == HarbourMaster && _sea.Rank != SeaRank.Landsman && !_sea.Outlaw)
                yield return ($"Ship me out to Brasswake. ({away}{(_sea.PayFor(Brasswake.Market) > 0 ? $", {_sea.PayFor(Brasswake.Market)} crowns" : "")})", () => SeekIsle(person));
            else if (id != HarbourMaster || _sea.Rank == SeaRank.Landsman)
                yield return ($"Get me passage to Brasswake. ({away}, {IsleBook.PassagePrice} crowns)", () =>
                {
                    if (_player.Crowns < IsleBook.PassagePrice) { Say(person, $"{IsleBook.PassagePrice} crowns. The sea isn't free."); return; }
                    _player.Crowns -= IsleBook.PassagePrice;
                    SeekIsle(person);
                });
        }
        if (Isle.MaySummon && _sea.Voyage is null)
            yield return ("Light the brass beacon.", () =>
            {
                _hud.EndTalk();
                _hud.Toast("You turn the little brass beacon in your hand, and it starts to tick. Before the hour's out there's a skiff over the pier with no water under it.", 9);
                GoToIsle();
            });
    }

    void SeekIsle(Person person)
    {
        if (_sea.SeekIsle(_guilds.PlaySeconds) is not { } v) { Say(person, "Not with that ship, you're not."); return; }
        _hud.EndTalk();
        _hud.Toast($"Bound for Brasswake, wherever it's got to: {v.Leagues} day{(v.Leagues == 1 ? "" : "s")} out, today.", 8);
        GoToSea(v);
        Save();
    }

    /// <summary>Under the stern of the bank of steam, and in to the Strangers' Dock: her lines go ashore, and she waits there.</summary>
    public void ReachIsle()
    {
        _sea.Voyage = null;
        Isle.Berthed = true;
        _hud.Toast("The fog ahead isn't fog. It's hot. Then there's a noise like every clock in the world, and out of the white comes a mountain with a hull on it, under way. A junk drops down alongside, looks you over, and waves you in to the little dock on her east side.", 11);
        GoToIsle();
    }

    // ---- aboard ----------------------------------------------------------------------------------------------------

    void GoToIsle()
    {
        var first = Isle.Rank == IsleRank.Stranger;
        _atSea?.QueueFree();
        _atSea = null;
        _sea.BackOnIsle(_player.Pack);
        Isle.Board();
        KeepDragonsAway();
        AddChild(_isle = new TheIsle(this));
        _player.GlobalPosition = TheIsle.Gangway;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        _sea.Market(Brasswake.Market, _city.Day);
        if (first) _isle.Greet(Locals.First(x => x.Npc.Id == IsleWarden));
        else _hud.Toast($"Brasswake. {IsleBook.Title(Isle.Rank)}, back aboard: {Isle.Marks} marks, and whatever you left at the gangway.", 7);
        Save();
    }

    /// <summary>Nothing with wings comes aboard: a hero's dragon goes home, and the hero comes down off it.</summary>
    void KeepDragonsAway()
    {
        _player.Dismount();
        foreach (var bond in _bonds.Where(b => b.Following && b.Role == BondRole.Dragon).ToList())
        {
            bond.Following = false;
            if (BodyOf(bond) is { } body) { _companions.Remove(body); body.QueueFree(); }
            _hud.Toast($"Every gun on the island is looking at {bond.Name}. {bond.Name} turns for home; you come aboard alone.", 8);
        }
    }

    /// <summary>Off the island by skiff: what it made stays at the gangway.</summary>
    public void LeaveIsle()
    {
        var left = _sea.LeaveIsle(_player.Pack);
        Isle.Berthed = false;
        _player.Fit();
        _player.Dress();
        _isle?.QueueFree();
        _isle = null;
        Ashore(Smugglers + new Vector3(1, 0, -2));
        _hud.Toast($"It's a long flight, and then you're on the south pier, and the skiff's a long way up.{Left(left)}", 10);
        Save();
    }

    static string Left(int n) => n > 0 ? $" {n} thing{(n == 1 ? "" : "s")} they made for you stayed at the gangway." : "";

    /// <summary>Off the island the way they came: down to the Strangers' Dock, and home on their own ship.</summary>
    void CastOffIsle(Person person)
    {
        var left = _sea.LeaveIsle(_player.Pack);
        _player.Fit();
        _player.Dress();
        if (_sea.SailFromIsle(_guilds.PlaySeconds) is not { } v) { Say(person, "There's no ship of yours at this dock."); return; }
        _hud.EndTalk();
        _isle?.QueueFree();
        _isle = null;
        _hud.Toast($"Lines in. The island draws away behind you, still under way, and doesn't look back.{Left(left)}", 9);
        GoToSea(v);
        Save();
    }

    /// <summary>A hero who's in the Wake isn't aboard any more.</summary>
    void IsleWoke()
    {
        if (!Isle.Aboard) return;
        _player.StandUp(_player.GlobalPosition);
        _sea.LeaveIsle(_player.Pack);
        _player.Fit();
        _player.Dress();
        _isle?.QueueFree();
        _isle = null;
    }

    void TendIsle()
    {
        if (Isle.Aboard && _isle is null && !_player.Downed) GoToIsle(); // a game saved aboard
    }

    (string Verb, Action Act)? IsleAction(Vector3 pos) => _isle?.Action(pos);

    IEnumerable<string> IsleLines(Vector3 pos)
    {
        if (_isle?.Line(pos) is { } here) yield return here;
    }

    IEnumerable<(string, Action)> IsleTalk(Person person)
    {
        foreach (var o in IsleRoute(person)) yield return o;
        var id = person.Npc.Id;
        if (!id.StartsWith("isle/")) yield break;
        if (id == IsleWarden)
        {
            yield return ("What are the rules?", () => Say(person, "Any ship may tie up. Past this gangway, crowns are no good: you earn marks on the valves or in the air, and marks are no good anywhere else. Whatever we make for you, you may use while you're aboard, and you hand it back here when you go. Bring a dragon within sight of this island, or come on one, and the guns will settle the matter."));
            yield return ("Why so hard on dragons?", () => Say(person, "A ship can only come at you one way, and it has to ask. A dragon goes where it likes, and a rider goes where the Order likes, and the Order has wanted a look in our foundries for a hundred years. There's nothing on a dragon we need, and nothing here it should see."));
            yield return ("Can I sell you anything?", () => Say(person, "Talk to the dockmaster, on the Strangers' Dock. Timber, ale and herbs, the few things a rock at sea can't make. The fish come from the pens, the bread and the greens from the glasshouses, and the iron from the rock under your feet."));
            yield return ("Fly me back to the mainland.", () => { _hud.EndTalk(); _isle?.FlyHome(); });
        }
        else if (id == Dockmaster)
        {
            var market = _sea.Market(Brasswake.Market, _city.Day);
            if (_sea.Capacity > 0 && Isle.Berthed) yield return ("Trade from the hold.", () => TradeAtPort(person, market));
            yield return ("What do you buy?", () => Say(person, $"{market.Name} pays well for {market.WantsText}, and sells {market.MakesText}. That's the whole of the trade. Designs aren't for sale. The few you've seen in Tristinwold, somebody decided you could have."));
            if (Isle.Berthed) yield return ("Cast off for home.", () => CastOffIsle(person));
        }
        else if (id == Archwright)
        {
            yield return ("Why did you leave?", () => Say(person, "The Magi wanted what we'd made, the Crown wanted it pointed at someone, and the Order wanted it on a dragon. We looked at what they'd do with it, and we looked at the sea. There's little on the mainland we need, and a good deal here it shouldn't have. So we trade at arm's length, and we keep moving. A thing that never stops can't be surrounded."));
            yield return ("How do I get on here?", () => Say(person, $"You're {IsleBook.Title(Isle.Rank)}. {Isle.NextStep}"));
            if (Tinker) yield return ("I'm of the Foundry. Doesn't that count?", () => Say(person, "The Foundry's what we left behind. It's why you read the signs better than most, and it's why you'll understand what you're looking at. It doesn't get you a key."));
        }
        else if (id == Engineer)
        {
            if (_isle?.OnShift == true) yield return ("How's the pressure?", () => Say(person, "Go and see to the one that's lit, and stop talking to me."));
            else yield return ($"Put me on the valves. ({IsleBook.VentPay} marks a valve, {IsleBook.ShiftPay} for the shift)", () =>
            {
                _hud.EndTalk();
                _isle?.StartShift();
            });
            yield return ("What's driving all this?", () => Say(person, "Four paddles and a boiler the size of Tristinwold's market, burning what we dig out of the rock. The rock's the island. Every year there's a bit less island and a bit more hull. Nobody up top likes to talk about that."));
        }
        else if (id == Stores)
        {
            foreach (var (item, marks) in Brasswake.Wares.Where(w => _player.Pack.Count(w.Id) == 0))
                yield return ($"{Items.Get(item)!.Name} ({marks} marks; you have {Isle.Marks})", () =>
                {
                    if (!Isle.Buy(item)) { Say(person, $"{marks} marks. The valves are at the stern."); return; }
                    _player.Pack.Add(item);
                    Sound.Ui("coin", -2);
                    Say(person, $"{Items.Get(item)!.Text} Sign here. You give it back at the gangway.");
                    Save();
                });
            yield return ("I've got crowns.", () => Say(person, "Have you. They'd make good washers."));
        }
        else if (id == Skymaster)
        {
            if (Isle.Air == AirRank.None)
                yield return ("Sign me on with the air crews.", () =>
                {
                    Isle.Enlist();
                    Say(person, "You'd better not be scared of heights, or dragons, or me. There's a junk going up on patrol whenever the horns go, and there's always a gun on her wants a hand. Hold down the trigger and try to hit what's trying to eat us. Come back alive and you get paid.");
                    Save();
                });
            else
            {
                if (_isle?.RaidOn != true) yield return ($"Take me up on patrol. ({IsleBook.Title(Isle.Air)})", () => { _hud.EndTalk(); _isle?.Patrol(); });
                yield return ("How do I get on in the crews?", () => Say(person, $"You're {Article(IsleBook.Title(Isle.Air))}, with {Isle.Sorties} patrol{(Isle.Sorties == 1 ? "" : "s")} home and {Isle.AirKills} brought down. {Isle.NextAirStep}"));
            }
            if (Isle.Pilot) yield return ("Give me a skiff.", () => { _hud.EndTalk(); _isle?.TakeSkiff(); });
            else yield return ($"Teach me to fly. ({IsleBook.Licence} marks)", () =>
            {
                if (!Isle.LearnToFly()) { Say(person, $"{IsleBook.Licence} marks, for the fuel you're about to waste."); return; }
                Say(person, "Push her nose up and she climbs, push it down and she dives. Left and right do what you'd hope. She'll fly straight into anything you point her at, so don't. Press E and she'll bring you home to the dock on her own.");
                Save();
            });
            yield return ("Fly me back to the mainland.", () => { _hud.EndTalk(); _isle?.FlyHome(); });
        }
    }

    // ---- what's come ashore in Tristinwold ------------------------------------------------------------------------------

    /// <summary>A little of the island on the harbour road: a steam crane at the piers, brass lamps, the great clock, and the Brass Factor's post.</summary>
    void AddSteamworks()
    {
        Townsfolk(new Npc(BrassFactor, "Wren Tollard", Sex.Other, 46, "Brass Factor of the Brasswake trade", "city:tristinwold", "Tristinwold",
            "Keeps the board on the harbour road that says how far out the island is today. Wears a very good watch, and won't tell you how it works.",
            ["the sea", "craft"], "brisk", "Three days out this morning. Or was. Ask me again.", 87.5, -3.5), "scholar"); // out in front of the warehouse, by the clock
        var (iron, brass) = (Armoury.Metal(new Color("#3a3a40"), 0.5f), Armoury.Brass);
        var globe = new StandardMaterial3D { AlbedoColor = new Color("#ffd9a0"), EmissionEnabled = true, Emission = new Color("#ffc070"), EmissionEnergyMultiplier = 2.5f };
        for (var z = -48f; z <= 48; z += 16)
        {
            var at = new Vector3(95, 0, z + 8);
            at.Y = City.FloorAt(at);
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.16f, Height = 4.6f, RadialSegments = 8 }, MaterialOverride = brass, Position = at + Vector3.Up * 2.3f });
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 10, Rings = 6 }, MaterialOverride = globe, Position = at + Vector3.Up * 4.8f });
        }
        // The steam crane between the middle piers, its jib going slowly round over the cargo.
        var crane = new Vector3(122, 0, 7.5f); // out past the harbour wall, between the piers, on its own footing in the water
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 1.3f, Height = 18, RadialSegments = 12 }, MaterialOverride = iron, Position = crane + Vector3.Down * 0 });
        AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 1, OuterRadius = 1.35f, Rings = 24, RingSegments = 8 }, MaterialOverride = brass, Position = crane + Vector3.Up * 2 });
        var jib = new Node3D { Position = crane + Vector3.Up * 9 };
        jib.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(13, 0.5f, 0.6f) }, MaterialOverride = brass, Position = new Vector3(4, 0.4f, 0) });
        jib.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 5, RadialSegments = 6 }, MaterialOverride = iron, Position = new Vector3(9.5f, -2.4f, 0) });
        jib.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One * 1.4f }, MaterialOverride = BuildingMaterials.For(Surface.Planks), Position = new Vector3(9.5f, -5, 0) });
        AddChild(jib);
        var swing = jib.CreateTween().SetLoops();
        swing.TweenProperty(jib, "rotation:y", 1.1f, 14).SetTrans(Tween.TransitionType.Sine);
        swing.TweenProperty(jib, "rotation:y", -0.7f, 14).SetTrans(Tween.TransitionType.Sine);
        // The great clock in front of the warehouse: brass wheels going round in the open, and the Factor's board beneath.
        var clock = new Vector3(84.5f, 0, -6.5f);
        clock.Y = City.FloorAt(clock);
        var tower = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.4f, 8, 2.4f) }, MaterialOverride = BuildingMaterials.For(Surface.DarkStone), Position = clock + Vector3.Up * 4 };
        Buildings.Structure(tower, "the Brass Factor's clock");
        AddChild(tower);
        foreach (var (y, r, speed) in new[] { (6.4f, 1.1f, 12f), (4.6f, 0.7f, -7f) })
        {
            var wheel = new Node3D { Position = clock + new Vector3(1.3f, y, 0), Rotation = new Vector3(0, 0, Mathf.Pi / 2) };
            wheel.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.14f, RadialSegments = 20 }, MaterialOverride = brass });
            for (var i = 0; i < 12; i++)
                wheel.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.14f, 0.22f) }, MaterialOverride = brass, Position = new Vector3(Mathf.Cos(i * Mathf.Tau / 12) * (r + 0.08f), 0, Mathf.Sin(i * Mathf.Tau / 12) * (r + 0.08f)) });
            AddChild(wheel);
            var turn = wheel.CreateTween().SetLoops();
            turn.TweenMethod(Callable.From<float>(a => wheel.Rotation = new Vector3(a, 0, Mathf.Pi / 2)), 0f, Mathf.Tau * Math.Sign(speed), Math.Abs(speed));
        }
        Sign(clock + Vector3.Up * 9.5f, "The Brass Factor");
    }

    public Person PersonAt(Npc npc, string look, float floor) => MeetAt(npc, look, floor);
    public (string Verb, Action Act)? NearestForTest() => Nearest();

    /// <summary>For the self-test and the pictures: straight aboard, and off again.</summary>
    public void BoardForTest() => GoToIsle();
    public void LeaveIsleForTest() => LeaveIsle();
    public static Vector3 BrassFactorAt => new(87.5f, 0, -3.5f);
    public void RaidForTest(string id) => _isle?.Raid(Brasswake.Raids.First(r => r.Id == id));
}

/// <summary>
/// The island itself: a hull with a mountain on it, in tiers like a wedding cake, its streets and houses stacked up the
/// rock with waterfalls running off it and a great wheel turning in them; stacks and paddles at the stern; flying ships
/// going round it; and its own bank of steam all about it. Played out on the same stage as a voyage, far above the land,
/// and made whole each time, since it only exists while someone's on it.
/// </summary>
public partial class TheIsle(Main main) : Node3D
{
    static readonly Vector3 Stage = AtSea.Stage;
    /// <summary>One step of the mountain: how wide and long, how far aft, and its floor above the water. The first is the hull.</summary>
    readonly record struct Tier(float Wide, float Long, float Aft, float Floor);
    static readonly Tier[] Tiers = [new(80, 132, 0, 9), new(58, 92, 6, 30), new(40, 62, 0, 54), new(24, 38, -6, 80), new(11, 16, -8, 108)];
    const int DockTier = 3;
    const float HullTaper = 0.6f;

    public static Vector3 Gangway => Stage + new Vector3(Tiers[0].Wide + 12, Tiers[0].Floor + 1, 16);
    static Vector3 DockAt => Stage + new Vector3(0, Tiers[DockTier].Floor + 1, Tiers[DockTier].Aft + Tiers[DockTier].Long - 8);
    static readonly Vector3 GalleryAt = new(0, Tiers[0].Floor, -112);

    readonly List<(Node3D Node, Vector3 Axis, float Speed)> _spinning = [];
    readonly List<Flyer> _flyers = [];
    readonly List<Enemy> _foes = [];
    readonly List<Enemy> _fallen = [];
    readonly List<(Vector3 From, Vector3 To)> _lifts = [];
    readonly List<(Node3D Body, Vector3[] Beat, int Next, float Speed)> _walkers = [];
    readonly List<Person> _people = [];
    readonly List<(Vector3 At, MeshInstance3D Glow)> _valves = [];
    Node3D? _foam;
    double _clock;
    float _fogWas;
    bool _hazeWas;

    /// <summary>A ship of the fleet: its round of the island, a name, and the hidden hull that takes the blows for it.</summary>
    sealed class Flyer(Node3D ship, string name, float radius, float height, float speed, float phase)
    {
        public Node3D Ship => ship;
        public string Name => name;
        public float Radius => radius;
        public float Height => height;
        public float Speed => speed;
        public float Phase => phase;
        public Enemy? Hull;
        public double Reload, Lost;
        /// <summary>Under the hero's hand: the fleet doesn't steer her.</summary>
        public bool Manual;
        public bool Aboard;
    }

    /// <summary>Where a spot's floor is, on the rock: the highest tier it's over.</summary>
    static float? Floor(float x, float z)
    {
        for (var i = Tiers.Length - 1; i >= 0; i--)
            if (Inside(Tiers[i], x, z, i == 0)) return Tiers[i].Floor;
        return null;
    }

    static bool Inside(Tier t, float x, float z, bool hull)
    {
        var (u, v) = (x / (t.Wide * Taper(z - t.Aft, t, hull)), (z - t.Aft) / t.Long);
        return u * u + v * v < 1;
    }

    /// <summary>The hull draws in to a bow, forward.</summary>
    static float Taper(float along, Tier t, bool hull) => hull ? 1 - HullTaper * Mathf.Pow(Math.Max(0, along / t.Long), 2) : 1;

    static Vector3 Edge(Tier t, float angle, bool hull, float grow = 1)
    {
        var along = Mathf.Cos(angle) * t.Long * grow;
        return new Vector3(Mathf.Sin(angle) * t.Wide * grow * Taper(along, t, hull), 0, t.Aft + along);
    }

    public override void _Ready()
    {
        Enemy.Slain += OnSlain;
        City.Afloat = p => new Vector2(p.X - Stage.X, p.Z - Stage.Z).Length() < 1600 && p.Y > Stage.Y - 200 ? Stage.Y + (Floor(p.X - Stage.X, p.Z - Stage.Z) ?? 0) : null;
        Position = Stage;
        // Inside the bank of steam the air's clear: the haze that hides her from the sea isn't on her.
        (_fogWas, _hazeWas) = (City.Sky.FogDensity, City.Sky.VolumetricFogEnabled);
        (City.Sky.FogDensity, City.Sky.VolumetricFogEnabled) = (0.0002f, false);
        Sea();
        for (var i = 0; i < Tiers.Length; i++) Rock(i);
        Streets();
        Water();
        Works();
        Engines();
        Lifts();
        Valves();
        Docks();
        Fleet();
        Folk();
        DeckGuns();
        var isle = main.Sea.Isle;
        isle.NextRaidAt = Math.Max(isle.NextRaidAt, main.GuildBook.PlaySeconds + Brasswake.FirstRaid);
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        foreach (var p in _people.Where(IsInstanceValid)) main.Dismiss(p);
        foreach (var e in _foes.Concat(_fallen).Concat(_flyers.Select(f => f.Hull).OfType<Enemy>()).Where(IsInstanceValid)) e.QueueFree();
        City.Afloat = null;
        (City.Sky.FogDensity, City.Sky.VolumetricFogEnabled) = (_fogWas, _hazeWas);
    }

    // ---- the sea going by, and the bank of steam it hides in ----------------------------------------------------

    void Sea()
    {
        const float reach = 6000;
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(reach, reach), SubdivideWidth = 200, SubdivideDepth = 200 },
            MaterialOverride = main.TerrainNode.Fancy ? Graphics.Water(main.TerrainNode.MirrorSky) : new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.25f, 0.33f), Roughness = 0.1f, Metallic = 0.3f } });
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(reach, reach) }, Position = Vector3.Down * 6, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#0b2230"), Roughness = 1, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        // Foam going by, soft and streaked: she stays on the stage, and the sea runs past her.
        var soft = new GradientTexture2D { Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 48, Height = 48, Gradient = new Gradient { Offsets = [0, 0.4f, 1], Colors = [new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0)] } };
        var foam = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new PlaneMesh { Size = Vector2.One }, InstanceCount = 700 };
        var rng = new Tristinwold.Deviant.Rng(4417);
        for (var i = 0; i < foam.InstanceCount; i++)
        {
            var side = i % 2 == 0 ? 1f : -1f;
            var back = 150 - (float)rng.NextDouble() * 900; // the wake, spreading away behind her, thick at the paddles
            var wide = i < 260 ? 82 + Math.Abs(back - 150) * 0.16f + (float)rng.NextDouble() * 16
                : i < 460 ? 30 + (float)rng.NextDouble() * 50 + Math.Abs(back + 130) * 0.03f
                : 100 + (float)rng.NextDouble() * 500;
            if (i >= 260 && i < 460) back = -135 - (float)rng.NextDouble() * 620;
            var big = i >= 460 ? 0.5f : 1;
            foam.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3((3 + (float)rng.NextDouble() * 9) * big, 1, (14 + (float)rng.NextDouble() * 34) * big)), new Vector3(side * wide, 0.3f, back)));
        }
        _foam = new MultiMeshInstance3D { Multimesh = foam, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = new StandardMaterial3D { AlbedoTexture = soft, AlbedoColor = new Color(1, 1, 1, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(_foam);
        var puff = new GradientTexture2D { Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 64, Height = 64, Gradient = new Gradient { Offsets = [0, 1], Colors = [new Color(1, 1, 1, 0.75f), new Color(1, 1, 1, 0)] } };
        var bank = new StandardMaterial3D { AlbedoTexture = puff, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, AlbedoColor = new Color("#e8eef2"), NoDepthTest = false };
        var wall = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new QuadMesh { Size = new Vector2(340, 190) }, InstanceCount = 90 };
        for (var i = 0; i < wall.InstanceCount; i++)
        {
            var a = i * Mathf.Tau / wall.InstanceCount + (float)rng.NextDouble() * 0.1f;
            var r = 760 + (float)rng.NextDouble() * 170;
            wall.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(Mathf.Sin(a) * r, 60 + (float)rng.NextDouble() * 70, Mathf.Cos(a) * r)));
        }
        AddChild(new MultiMeshInstance3D { Multimesh = wall, MaterialOverride = bank, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ---- the rock ---------------------------------------------------------------------------------------------------

    static StandardMaterial3D Clad(Surface surface, Color tint, float scale = 0.12f)
    {
        var m = (BuildingMaterials.For(surface).Duplicate() as StandardMaterial3D) ?? new StandardMaterial3D();
        (m.Uv1Triplanar, m.Uv1WorldTriplanar, m.Uv1Scale, m.AlbedoColor, m.CullMode) = (true, true, Vector3.One * scale, tint, BaseMaterial3D.CullModeEnum.Disabled);
        return m;
    }

    static StandardMaterial3D Crag(Color tint)
    {
        var m = new StandardMaterial3D { AlbedoColor = tint, Roughness = 0.95f, Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = Vector3.One * 0.035f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        if (ResourceLoader.Exists("res://assets/polyhaven/models/cliff_namaqualand_cliff_01_diff_1k.jpg")) m.AlbedoTexture = GD.Load<Texture2D>("res://assets/polyhaven/models/cliff_namaqualand_cliff_01_diff_1k.jpg");
        return m;
    }

    /// <summary>How far the rock juts or falls back at a spot on a tier's face: the same every time.</summary>
    static float Ragged(int tier, float angle, float up) =>
        (Mathf.Sin(angle * 7 + tier * 2.1f + up * 2) + 0.6f * Mathf.Sin(angle * 17 + tier + up * 5) + 0.3f * Mathf.Sin(angle * 41 - up * 7)) * (up < 0.05f ? 0.3f : 1);

    /// <summary>A tier: a steep ragged face up from the one below, and a floor. Its own shape, made solid.</summary>
    void Rock(int i)
    {
        var (t, hull) = (Tiers[i], i == 0);
        var bottom = hull ? -14f : Tiers[i - 1].Floor - 1;
        const int round = 96, rows = 6;
        var sides = new SurfaceTool();
        sides.Begin(Mesh.PrimitiveType.Triangles);
        var cap = new SurfaceTool();
        cap.Begin(Mesh.PrimitiveType.Triangles);
        var centre = new Vector3(0, t.Floor, t.Aft);
        Vector3 Face(float a, int row)
        {
            var up = row / (float)rows; // 0 at the foot, 1 at the top
            var e = Edge(t, a, hull);
            var y = Mathf.Lerp(bottom, t.Floor, up);
            if (hull) return new Vector3(e.X * Mathf.Lerp(0.8f, 1, up), y, t.Aft + (e.Z - t.Aft) * Mathf.Lerp(0.92f, 1, up));
            var flare = 1 + 0.1f * (1 - up) * (1 - up);
            var push = 1 + (row == rows ? 0 : Ragged(i, a, up) * 2.2f / t.Wide);
            return new Vector3(e.X * flare * push, y, t.Aft + (e.Z - t.Aft) * flare * push);
        }
        for (var n = 0; n < round; n++)
        {
            var (a, b) = (n * Mathf.Tau / round, (n + 1) * Mathf.Tau / round);
            for (var r = 0; r < rows; r++)
                foreach (var v in new[] { Face(a, r), Face(a, r + 1), Face(b, r + 1), Face(a, r), Face(b, r + 1), Face(b, r) }) sides.AddVertex(v);
            foreach (var v in new[] { centre, Face(b, rows), Face(a, rows) }) cap.AddVertex(v);
        }
        sides.GenerateNormals();
        cap.GenerateNormals();
        var side = sides.Commit();
        var top = cap.Commit();
        var body = new StaticBody3D();
        body.AddChild(new MeshInstance3D { Mesh = side, MaterialOverride = hull ? Clad(Surface.DarkStone, new Color("#4a443e"), 0.08f) : Crag(new Color("#9a9286")) });
        body.AddChild(new MeshInstance3D { Mesh = top, MaterialOverride = hull ? Clad(Surface.Planks, new Color("#a08a6a"), 0.18f) : Clad(Surface.Stone, new Color("#a89e8e"), 0.2f) });
        body.AddChild(new CollisionShape3D { Shape = side.CreateTrimeshShape() });
        body.AddChild(new CollisionShape3D { Shape = top.CreateTrimeshShape() });
        AddChild(body);
        if (!hull) return;
        // The hull's riveted strakes, and a brass ram at the bow.
        var brass = Armoury.Brass;
        foreach (var y in new[] { -2f, 3f, 7.6f })
        {
            var shape = new PolygonLine(round + 1);
            for (var n = 0; n <= round; n++)
            {
                var e = Edge(t, n * Mathf.Tau / round, true);
                var up = (y + 14) / (t.Floor + 14);
                shape.Add(new Vector3(e.X * Mathf.Lerp(0.8f, 1, up) * 1.004f, y, t.Aft + (e.Z - t.Aft) * Mathf.Lerp(0.92f, 1, up) * 1.004f));
            }
            AddChild(new MeshInstance3D { Mesh = shape.Tube(0.4f), MaterialOverride = brass });
        }
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 7, Height = 40, RadialSegments = 12 }, MaterialOverride = brass, Position = new Vector3(0, 4, t.Long + 8), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        Bulwark(t);
    }

    /// <summary>A bulwark round the deck, breast-high and solid, with a gap at the gangway.</summary>
    void Bulwark(Tier t)
    {
        const int round = 120;
        const float high = 1.3f, in_ = 0.985f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var n = 0; n < round; n++)
        {
            var (a, b) = (n * Mathf.Tau / round, (n + 1) * Mathf.Tau / round);
            var deg = Mathf.RadToDeg(a);
            if (deg is > 76 and < 86) continue; // the gangway, at the Strangers' Dock
            var (p, q) = (Edge(t, a, true, in_) with { Y = t.Floor }, Edge(t, b, true, in_) with { Y = t.Floor });
            foreach (var v in new[] { p, p + Vector3.Up * high, q + Vector3.Up * high, p, q + Vector3.Up * high, q }) st.AddVertex(v);
        }
        st.GenerateNormals();
        var mesh = st.Commit();
        var wall = new StaticBody3D();
        wall.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = Armoury.Metal(new Color("#6a5238"), 0.5f) });
        wall.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        AddChild(wall);
    }

    /// <summary>A line of points, drawn as a thin square tube: a strake, a pipe.</summary>
    sealed class PolygonLine(int count)
    {
        readonly List<Vector3> _points = new(count);
        public void Add(Vector3 p) => _points.Add(p);

        public ArrayMesh Tube(float radius)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (var i = 0; i + 1 < _points.Count; i++)
            {
                var (a, b) = (_points[i], _points[i + 1]);
                var out_ = (b - a).Cross(Vector3.Up).Normalized() * radius;
                var up = Vector3.Up * radius;
                foreach (var (x, y) in new[] { (out_ + up, out_ - up), (out_ - up, -out_ - up), (-out_ - up, -out_ + up), (-out_ + up, out_ + up) })
                    foreach (var v in new[] { a + x, b + x, b + y, a + x, b + y, a + y }) st.AddVertex(v);
            }
            st.GenerateNormals();
            return st.Commit();
        }
    }

    // ---- the streets --------------------------------------------------------------------------------------------------

    /// <summary>Houses stacked up the rock, row over row, all facing out to the water, the upper ones hung on struts.</summary>
    void Streets()
    {
        var kinds = new[] { "house", "house", "tavern", "smithy", "house", "tower", "longhall", "house" };
        var strut = Clad(Surface.Timber, new Color("#4a3424"), 0.3f);
        var seed = 11;
        for (var i = 1; i < DockTier + 1; i++)
        {
            var (t, below) = (Tiers[i], Tiers[i - 1]);
            var high = t.Floor - below.Floor;
            var count = i == 1 ? 26 : i == 2 ? 19 : 12;
            for (var row = 0; row < 3; row++)
            {
                var y = below.Floor + row * high * 0.31f;
                var size = 1.25f - row * 0.1f;
                for (var n = 0; n < count; n++)
                {
                    var a = (n + 0.5f + row * 0.37f) * Mathf.Tau / count;
                    var deg = Mathf.PosMod(Mathf.RadToDeg(a), 360);
                    if (deg is > 78 and < 102) continue; // the lifts
                    if (i == 1 && row == 0 && deg is > 55 and < 125) continue; // the market, on the deck
                    if (deg is > 240 and < 300) continue; // the falls, and the great wheel
                    if (row > 0 && (n * 7 + row * 3) % 5 == 0) continue; // bare rock here and there
                    var edge = Edge(t, a, false);
                    var outward = new Vector3(edge.X, 0, edge.Z - t.Aft).Normalized();
                    var at = edge + outward * (row == 0 ? 6.5f * size + 1 : 4 * size) + Vector3.Up * y;
                    Buildings.Place(this, row > 0 && kinds[seed % kinds.Length] == "tower" ? "house" : kinds[seed % kinds.Length], at, Mathf.Atan2(outward.X, outward.Z), seed++, seed % 3 == 0 ? Surface.ClayTile : Surface.Slate, size, door: false, solid: row == 0);
                    if (row == 0) continue;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var across = outward.Cross(Vector3.Up);
                        var foot = at + across * side * 3.5f - outward * 2 + Vector3.Down * 5.5f;
                        var knee = at + across * side * 3.5f + outward * 4 + Vector3.Down * 0.2f;
                        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.45f, (knee - foot).Length(), 0.45f) }, MaterialOverride = strut, Position = (foot + knee) / 2, Basis = Aimed((knee - foot).Normalized()) });
                    }
                }
            }
        }
        // The market on the deck, in the gap between the houses, and what's piled about it.
        var deck = Tiers[0].Floor;
        foreach (var (x, z, yaw) in new[] { (60f, 44f, 1.4f), (64f, -22f, 1.7f) })
            Buildings.Place(this, "market", new Vector3(x, deck, z), yaw, x > 62 ? 3 : 5, Surface.ClayTile, 1.2f, door: false);
        foreach (var (x, z, file, yaw) in new[] { (67f, 30f, "stall_red.glb", 1.5f), (69f, 22f, "stall_green.glb", 1.5f), (70f, 2f, "stall_red.glb", 1.6f), (68f, -8f, "stall_green.glb", 1.6f), (58f, 26f, "stall_green.glb", -1.5f), (58f, 6f, "stall_red.glb", -1.5f), (59f, -8f, "stall_red.glb", -1.6f) })
            Models.Place(this, "res://assets/props/caravan/" + file, new Vector3(x, deck, z), 2.6f, yaw, solid: false);
        // The main pipe, running round the deck at head height on brass trestles.
        var pipe = new PolygonLine(61);
        for (var n = 0; n <= 60; n++)
        {
            var e = Edge(Tiers[1], n * Mathf.Tau / 60, false, 1.03f);
            pipe.Add(e with { Y = deck + 3.6f });
            if (n % 5 == 0) AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.22f, Height = 3.6f, RadialSegments = 8 }, MaterialOverride = Armoury.Metal(new Color("#3a3a40"), 0.5f), Position = e with { Y = deck + 1.8f } });
        }
        AddChild(new MeshInstance3D { Mesh = pipe.Tube(0.42f), MaterialOverride = Armoury.Bronze });
        var rng = new Tristinwold.Deviant.Rng(90);
        for (var i = 0; i < 44; i++)
        {
            var a = (float)rng.NextDouble() * Mathf.Tau;
            var at = Edge(Tiers[0], a, true, 0.86f + 0.1f * (float)rng.NextDouble());
            if (Floor(at.X, at.Z) != Tiers[0].Floor) continue;
            Models.Place(this, i % 3 == 0 ? "ph:barrel.glb" : "ph:crate.glb", at with { Y = deck }, i % 3 == 0 ? 1.1f : 1.3f, (float)rng.NextDouble() * 3, solid: false);
        }
        // Lamps along the deck: a brass post and a warm globe, lit against the shadow of the rock.
        var globe = new StandardMaterial3D { AlbedoColor = new Color("#ffd9a0"), EmissionEnabled = true, Emission = new Color("#ffc070"), EmissionEnergyMultiplier = 2.5f };
        for (var i = 0; i < 18; i++)
        {
            var at = Edge(Tiers[0], (i + 0.5f) * Mathf.Tau / 18, true, 0.92f) with { Y = deck };
            if (Floor(at.X, at.Z) != deck) continue;
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.16f, Height = 5, RadialSegments = 8 }, MaterialOverride = Armoury.Brass, Position = at + Vector3.Up * 2.5f });
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.34f, Height = 0.68f, RadialSegments = 10, Rings = 6 }, MaterialOverride = globe, Position = at + Vector3.Up * 5.2f });
            if (i % 3 == 0) AddChild(new OmniLight3D { Position = at + Vector3.Up * 5, LightColor = new Color("#ffc890"), LightEnergy = 1.2f, OmniRange = 22 });
        }
        Sign(new Vector3(62, deck + 7, -10), "The Cog Market");
        Sign(new Vector3(0, Tiers[1].Floor + 6, Tiers[1].Aft + Tiers[1].Long - 6), "Foundry Row");
        Sign(new Vector3(0, Tiers[2].Floor + 6, Tiers[2].Aft + Tiers[2].Long - 6), "The Gearworks");
        // The glasshouses: bread and greens, grown here, and never a sack brought aboard.
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.92f, 0.9f, 0.32f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Metallic = 0.5f, Roughness = 0.1f };
        var green = new StandardMaterial3D { AlbedoColor = new Color("#4f8a3c"), Roughness = 0.95f };
        foreach (var (x, z) in new[] { (0f, 112f), (-13f, 104f), (13f, 104f) })
        {
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 7, Height = 7, IsHemisphere = true, RadialSegments = 24, Rings = 8 }, MaterialOverride = glass, Position = new Vector3(x, deck, z) });
            AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 6.8f, OuterRadius = 7.3f, Rings = 28, RingSegments = 8 }, MaterialOverride = Armoury.Brass, Position = new Vector3(x, deck + 0.2f, z) });
            for (var i = 0; i < 14; i++)
            {
                var (a, r) = (i * 2.4f, 1.2f + i * 0.34f);
                AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.8f, Height = 1.5f, RadialSegments = 8, Rings = 4 }, MaterialOverride = green, Position = new Vector3(x + Mathf.Cos(a) * r, deck + 0.7f, z + Mathf.Sin(a) * r) });
            }
        }
        Sign(new Vector3(0, deck + 10, 106), "The Glasshouses");
    }

    /// <summary>A basis with its Y axis along a direction.</summary>
    static Basis Aimed(Vector3 way)
    {
        var across = way.Cross(Mathf.Abs(way.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up).Normalized();
        return new Basis(across, way, across.Cross(way));
    }

    void Sign(Vector3 at, string text) => AddChild(new Label3D { Text = text, Position = at, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, OutlineSize = 12, PixelSize = 0.03f, Modulate = new Color("#ffd27a") });

    // ---- water off the rock, and the great wheel in it --------------------------------------------------------------

    const string FallShader = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;
float streak(vec2 uv, float speed, float width) {
    float x = floor(uv.x * width);
    float line = fract(sin(x * 12.9898) * 43758.5453);
    return smoothstep(0.35, 1.0, sin((uv.y * 3.0 + line * 6.0 - TIME * speed * (0.7 + line)) * 6.283) * 0.5 + 0.5);
}
void fragment() {
    float s = 0.55 * streak(UV, 0.9, 24.0) + 0.45 * streak(UV + 0.37, 1.3, 61.0);
    ALBEDO = mix(vec3(0.70, 0.83, 0.90), vec3(1.0), s);
    float edge = smoothstep(0.0, 0.28, UV.x) * smoothstep(1.0, 0.72, UV.x);
    ALPHA = (0.42 + 0.5 * s) * edge * smoothstep(0.0, 0.03, UV.y);
}";

    void Fall(Vector3 top, float wide, float drop, Vector3 outward)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(wide, drop), Orientation = PlaneMesh.OrientationEnum.Z }, Position = top + outward * 3.6f + Vector3.Down * drop / 2, Rotation = new Vector3(0, Mathf.Atan2(outward.X, outward.Z), 0),
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = FallShader } }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        Steam(top + outward * 5 + Vector3.Down * (drop - 1), wide * 0.5f, 3, new Color(1, 1, 1, 0.3f), 1.2f);
    }

    void Water()
    {
        // The west face comes down in steps, past the great wheel, and over the bulwark into the sea.
        foreach (var (angle, width) in new[] { (Mathf.Pi * 1.5f, 1f), (Mathf.Pi * 1.36f, 0.6f), (Mathf.Pi * 1.62f, 0.5f) })
            for (var i = Tiers.Length - 2; i >= 1; i--)
            {
                var (t, below) = (Tiers[i], Tiers[i - 1]);
                var edge = Edge(t, angle, false);
                var outward = new Vector3(edge.X, 0, edge.Z - t.Aft).Normalized();
                Fall(edge with { Y = t.Floor }, (10 + i * 3) * width, t.Floor - below.Floor, outward);
            }
        var low = Edge(Tiers[0], Mathf.Pi * 1.5f, true);
        Fall(low with { Y = Tiers[0].Floor + 1 }, 22, Tiers[0].Floor + 2, Vector3.Left);
        var wheelAt = Edge(Tiers[1], Mathf.Pi * 1.5f, false) + new Vector3(-9, (Tiers[0].Floor + Tiers[1].Floor) / 2 + 3, 0);
        Wheel(wheelAt, 12, 7, Vector3.Right, -0.3f, 16);
    }

    /// <summary>A wheel of timber and iron: rims, spokes and paddles, turning about an axle.</summary>
    void Wheel(Vector3 at, float radius, float wide, Vector3 axis, float speed, int blades)
    {
        var (timber, iron) = (Clad(Surface.Timber, new Color("#6a4a30"), 0.3f), Armoury.Metal(new Color("#3a3a40"), 0.5f));
        var hold = new Node3D { Position = at };
        hold.Basis = axis == Vector3.Right ? new Basis(Vector3.Forward, Mathf.Pi / 2) : Basis.Identity;
        foreach (var side in new[] { -wide / 2, wide / 2 })
            hold.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius - 1.2f, OuterRadius = radius, Rings = 48, RingSegments = 8 }, MaterialOverride = iron, Position = Vector3.Up * side });
        hold.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = wide + 2, RadialSegments = 16 }, MaterialOverride = iron });
        for (var i = 0; i < blades; i++)
        {
            var a = i * Mathf.Tau / blades;
            var way = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            hold.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(radius * 0.26f, wide, 0.5f) }, MaterialOverride = timber, Position = way * radius * 0.9f, Rotation = new Vector3(0, -a, 0) });
            foreach (var side in new[] { -wide / 2, wide / 2 })
                hold.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(radius * 1.9f, 0.5f, 0.5f) }, MaterialOverride = iron, Position = Vector3.Up * side, Rotation = new Vector3(0, -a, 0) });
        }
        AddChild(hold);
        _spinning.Add((hold, Vector3.Up, speed));
    }

    void Steam(Vector3 at, float size, float life, Color tint, float rise = 2.5f)
    {
        var puff = new GradientTexture2D { Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 48, Height = 48, Gradient = new Gradient { Offsets = [0, 0.5f, 1], Colors = [new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0.2f), new Color(1, 1, 1, 0)] } };
        AddChild(new CpuParticles3D
        {
            Position = at, Amount = 34, Lifetime = life, Preprocess = life, Mesh = new QuadMesh { Size = Vector2.One * size },
            MaterialOverride = new StandardMaterial3D { AlbedoTexture = puff, AlbedoColor = tint, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, VertexColorUseAsAlbedo = true },
            Direction = Vector3.Up, Spread = 16, Gravity = new Vector3(0, 0.4f, -1.6f), InitialVelocityMin = rise * 0.7f, InitialVelocityMax = rise * 1.3f, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.5f,
        });
    }

    // ---- the works: gears on the rock, pipes, stacks and paddles ---------------------------------------------------

    void Works()
    {
        var gears = new[] { (Mathf.Pi * 0.12f, 2, 6f, 0.6f), (Mathf.Pi * 0.2f, 2, 3.6f, -1), (Mathf.Pi * 0.8f, 2, 7f, 0.5f), (Mathf.Pi * 0.3f, 3, 5f, -0.7f), (Mathf.Pi * 1.8f, 3, 6f, 0.5f), (Mathf.Pi * 0.75f, 3, 4f, -0.8f) };
        foreach (var (a, tier, radius, speed) in gears)
        {
            var t = Tiers[tier];
            var at = Edge(t, a, false, 1.03f) with { Y = t.Floor - 7 };
            var outward = (at with { Y = 0 } - new Vector3(0, 0, t.Aft)).Normalized();
            GearOn(at, radius, outward, speed);
        }
        // Pipes up the rock, and the stacks at the top of the works.
        foreach (var a in new[] { 0.4f, 0.75f, 2.1f, 2.6f, 3.9f, 5.5f })
        {
            for (var i = 1; i < Tiers.Length; i++)
            {
                var t = Tiers[i];
                var at = Edge(t, a, false, 1.01f);
                AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.7f, Height = t.Floor - Tiers[i - 1].Floor + 2, RadialSegments = 10 }, MaterialOverride = Armoury.Brass, Position = at with { Y = (t.Floor + Tiers[i - 1].Floor) / 2 } });
            }
        }
        var iron = Armoury.Metal(new Color("#2e2e34"), 0.5f);
        foreach (var (x, z, tall) in new[] { (-12f, -46f, 34f), (12f, -46f, 34f), (-22f, -32f, 26f), (22f, -32f, 26f), (0f, -54f, 42f) })
        {
            var at = new Vector3(x, Tiers[2].Floor, z);
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 3, BottomRadius = 4, Height = tall, RadialSegments = 16 }, MaterialOverride = iron, Position = at + Vector3.Up * tall / 2 });
            foreach (var band in new[] { 0.45f, 0.97f })
                AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 3, OuterRadius = 3.7f, Rings = 24, RingSegments = 8 }, MaterialOverride = Armoury.Brass, Position = at + Vector3.Up * tall * band });
            Steam(at + Vector3.Up * (tall + 1), 16, 11, new Color(0.9f, 0.9f, 0.92f, 0.8f), 6);
        }
    }

    void GearOn(Vector3 at, float radius, Vector3 outward, float speed)
    {
        var hold = new Node3D { Position = at };
        var across = Vector3.Up.Cross(outward).Normalized();
        hold.Basis = new Basis(across, outward, across.Cross(outward)); // its axle points out of the rock
        var spin = new Node3D();
        hold.AddChild(spin);
        var iron = Armoury.Metal(new Color("#6a5a3a"), 0.45f);
        spin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.7f, RadialSegments = 32 }, MaterialOverride = Armoury.Brass });
        spin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius * 0.3f, BottomRadius = radius * 0.3f, Height = 1.3f, RadialSegments = 16 }, MaterialOverride = iron });
        var teeth = (int)(radius * 4);
        for (var i = 0; i < teeth; i++)
        {
            var a = i * Mathf.Tau / teeth;
            spin.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.6f, 0.9f) }, MaterialOverride = Armoury.Brass, Position = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius + 0.35f), Rotation = new Vector3(0, -a, 0) });
        }
        AddChild(hold);
        _spinning.Add((spin, Vector3.Up, speed));
    }

    /// <summary>What drives her: four great paddles aft, thrashing the sea white.</summary>
    void Engines()
    {
        foreach (var side in new[] { -1f, 1f })
        {
            Wheel(new Vector3(side * 76, 4, -60), 14, 10, Vector3.Right, -0.9f, 14);
            Wheel(new Vector3(side * 56, 4, -106), 13, 10, Vector3.Right, -0.9f, 14);
            Steam(new Vector3(side * 80, 0.5f, -74), 13, 3, new Color(1, 1, 1, 0.6f), 2.4f);
            Steam(new Vector3(side * 60, 0.5f, -120), 13, 3, new Color(1, 1, 1, 0.6f), 2.4f);
            // The iron housings they turn in, against the hull.
            foreach (var (x, z) in new[] { (side * 68, -60f), (side * 48, -106f) })
                AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(8, 15, 22) }, MaterialOverride = Armoury.Metal(new Color("#3a3a40"), 0.5f), Position = new Vector3(x, 6, z) });
        }
    }

    // ---- the lifts -----------------------------------------------------------------------------------------------------

    /// <summary>Steam lifts up the east face, tier by tier: cages on iron frames.</summary>
    void Lifts()
    {
        var iron = Armoury.Metal(new Color("#3a3a40"), 0.5f);
        for (var i = 1; i < Tiers.Length; i++)
        {
            var (t, below) = (Tiers[i], Tiers[i - 1]);
            var on = new Vector3(t.Wide - 3, t.Floor, t.Aft);
            var foot = new Vector3(t.Wide + 4, below.Floor, t.Aft);
            _lifts.Add((foot, on));
            foreach (var (dx, dz) in new[] { (-2.2f, -2.2f), (2.2f, -2.2f), (-2.2f, 2.2f), (2.2f, 2.2f) })
                AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, t.Floor - below.Floor + 5, 0.35f) }, MaterialOverride = iron, Position = foot + new Vector3(dx, (t.Floor - below.Floor + 5) / 2, dz) });
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(5, 0.3f, 5) }, MaterialOverride = Armoury.Brass, Position = foot + Vector3.Up * 0.15f });
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(5, 0.3f, 5) }, MaterialOverride = iron, Position = foot + Vector3.Up * (t.Floor - below.Floor + 5) });
            AddChild(new Label3D { Text = "Lift", Position = foot + Vector3.Up * 4, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
            AddChild(new Label3D { Text = "Lift", Position = on + Vector3.Up * 3, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
        }
    }

    bool _riding;

    void Ride(Vector3 from, Vector3 to)
    {
        var (hero, cage) = (main.PlayerNode, new Node3D { Position = from + Vector3.Up * 2 });
        AddChild(cage);
        _riding = true;
        hero.SitAt(() => cage.GlobalPosition);
        var over = new Vector3(from.X, to.Y + 2, from.Z);
        var t = cage.CreateTween();
        if (Mathf.Abs(from.Y - to.Y) > 1 && from.Y < to.Y)
        {
            t.TweenProperty(cage, "position", over, 4).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(cage, "position", to + Vector3.Up * 2, 1);
        }
        else
        {
            t.TweenProperty(cage, "position", new Vector3(to.X, from.Y + 2, to.Z), 1);
            t.TweenProperty(cage, "position", to + Vector3.Up * 2, 4).SetTrans(Tween.TransitionType.Sine);
        }
        t.TweenCallback(Callable.From(() =>
        {
            _riding = false;
            hero.StandUp(ToGlobal(to + Vector3.Up * 0.6f));
            cage.QueueFree();
        }));
        Sound.Ui("quest", -8);
    }

    public (string Verb, Action Act)? Action(Vector3 pos)
    {
        if (_riding) return null;
        var here = ToLocal(pos);
        foreach (var gun in _guns)
            if (here.DistanceTo(gun + Vector3.Up) < 3.6f) return ("man the deck gun", () => ManGun(gun));
        foreach (var (from, to) in _lifts)
        {
            if (here.DistanceTo(from + Vector3.Up) < 3.4f) return ("ride the steam lift up", () => Ride(from, to));
            if (here.DistanceTo(to + Vector3.Up) < 3.4f) return ("ride the steam lift down", () => Ride(to, from));
        }
        if (OnShift && _valves[_lit].At.DistanceTo(here) < 3.6f) return ("ease the valve", Ease);
        return null;
    }

    public (string Verb, Action Act)? SeatAction() =>
        _patrol is not null ? ("go down the wire to the Sky Dock", () => EndPatrol(false))
        : _manned is not null ? ("leave the gun", LeaveGun)
        : _flying ? ("bring her in to the Sky Dock", Land) : null;

    /// <summary>The way to wherever the hero's wanted, for the tracker.</summary>
    public string? Line(Vector3 pos)
    {
        var isle = main.Sea.Isle;
        if (_patrol is { Hull: { } hull } ship) return $"On patrol aboard {ship.Name}: {_foes.Count} left in the sky, hull {hull.Stats.Health / hull.Stats.MaxHealth:P0}. {(ship.Manual ? "Steer with the stick. " : "")}Hold attack to fire at what's in front of you";
        if (_manned is not null) return $"On a deck gun: {(_foes.Count > 0 ? $"{_foes.Count} left in the sky. " : "")}Hold attack to fire at what's in front of you";
        if (RaidOn) return $"The horns are going: {_foes.Count} in the sky over Brasswake. The deck guns want hands";
        if (_flying) return $"Flying a sky-skiff: {_altitude:0} m up. Up and down climb and dive, left and right steer, E brings her home";
        if (OnShift) return $"On the valves: ease the lit valve at the stern ({_shiftLeft} to go, {Math.Max(0, _valveLeft):0}s before it blows)";
        return $"Brasswake: {IsleBook.Title(isle.Rank)}, {isle.Marks} marks";
    }

    // ---- the valves -----------------------------------------------------------------------------------------------------

    int _lit, _shiftLeft;
    double _valveLeft;
    public bool OnShift => _shiftLeft > 0;
    /// <summary>The valve that wants easing (for the self-test).</summary>
    public Vector3 LitValve => ToGlobal(_valves[_lit].At);
    const double ValveSeconds = 25;

    void Valves()
    {
        for (var i = 0; i < IsleBook.Valves; i++)
        {
            var a = -1.1f + i * 0.44f;
            var at = GalleryAt + new Vector3(Mathf.Sin(a) * 30, 0, -Mathf.Cos(a) * 6);
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.7f, Height = 2, RadialSegments = 12 }, MaterialOverride = Armoury.Metal(new Color("#3a3a40"), 0.5f), Position = at + Vector3.Up });
            AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.72f, Rings = 18, RingSegments = 8 }, MaterialOverride = Armoury.Brass, Position = at + Vector3.Up * 2.1f });
            var glow = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.38f, Height = 0.76f, RadialSegments = 12, Rings = 6 }, Position = at + Vector3.Up * 2.5f, Visible = false,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ff5a20"), EmissionEnabled = true, Emission = new Color("#ff5a20"), EmissionEnergyMultiplier = 2 } };
            AddChild(glow);
            _valves.Add((at, glow));
        }
        Sign(GalleryAt + Vector3.Up * 9, "The Steam Gallery");
    }

    public void StartShift()
    {
        _shiftLeft = IsleBook.Valves;
        LightValve();
        main.HudNode.Toast("Dorrin Pike points at a board of gauges. \"One lit at a time. Get to it before it blows. Don't stop to admire it.\"", 7);
    }

    void LightValve()
    {
        foreach (var (_, g) in _valves) g.Visible = false;
        _lit = main.LootRng.Next(_valves.Count);
        _valves[_lit].Glow.Visible = true;
        _valveLeft = ValveSeconds;
    }

    void Ease()
    {
        var last = _shiftLeft == 1;
        var (pay, made) = main.Sea.Isle.Vent(last);
        NextValve();
        main.HudNode.Toast($"The gauge drops out of the red. +{pay} marks ({main.Sea.Isle.Marks}).{(last ? " That's the shift." : "")}", 3);
        if (made is { } r) main.HudNode.Toast(r == IsleRank.Citizen ? "The Archwright sends for you, and there's a small brass beacon on the desk, and a line in a very old book with your name in it. You're a citizen of Brasswake. Light that beacon, anywhere in the world, and they'll send a skiff." : "Dorrin Pike looks at your burnt hands and nods. You're a hand of the engines now.", 12);
        main.Save();
    }

    void NextValve()
    {
        if (--_shiftLeft > 0) LightValve();
        else foreach (var (_, g) in _valves) g.Visible = false;
    }

    // ---- the docks, the skiffs and the fleet --------------------------------------------------------------------------

    void Docks()
    {
        // The Strangers' Dock on the east side: a little timber pier, always out, for any ship that comes alongside.
        var deck = Tiers[0].Floor;
        var dockZ = 16f;
        var dock = new StaticBody3D { Position = new Vector3(Tiers[0].Wide + 7, deck - 0.3f, dockZ) };
        var size = new Vector3(22, 0.6f, 9);
        dock.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Clad(Surface.Planks, new Color("#8a7458"), 0.2f) });
        dock.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(dock);
        var post = Clad(Surface.Timber, new Color("#4a3424"), 0.3f);
        for (var x = -9f; x <= 9; x += 6)
            foreach (var z in new[] { -4f, 4f })
                dock.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.36f, Height = deck + 2, RadialSegments = 8 }, MaterialOverride = post, Position = new Vector3(x, -(deck - 1) / 2, z) });
        foreach (var x in new[] { 10.5f })
            dock.AddChild(new OmniLight3D { Position = new Vector3(x, 4, 0), LightColor = new Color("#ffc890"), LightEnergy = 1.4f, OmniRange = 20 });
        Sign(new Vector3(Tiers[0].Wide + 8, deck + 7, dockZ), "The Strangers' Dock");
        if (main.Sea.Isle.Berthed && Models.Scene("ph:ship.glb")?.Instantiate() is Node3D ship)
        {
            ship.Position = new Vector3(Tiers[0].Wide + 18, 0, dockZ + 14);
            AddChild(ship);
        }
        // The Sky Dock, right at the top: a stone pier out over everything, with a mast and a skiff on it.
        var top = Tiers[DockTier];
        var pier = new StaticBody3D { Position = new Vector3(0, top.Floor - 0.3f, top.Aft + top.Long + 12) };
        var long_ = new Vector3(14, 0.6f, 40);
        pier.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = long_ }, MaterialOverride = Clad(Surface.Stone, new Color("#a89e8e"), 0.2f) });
        pier.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = long_ } });
        AddChild(pier);
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.5f, Height = 26, RadialSegments = 10 }, MaterialOverride = Armoury.Metal(new Color("#3a3a40"), 0.5f), Position = new Vector3(0, top.Floor + 13, top.Aft + top.Long - 4) });
        Sign(new Vector3(0, top.Floor + 9, top.Aft + top.Long + 8), "The Sky Dock");
        Orrery();
        _skiff = Junk(0.42f, "#c8703a");
        _skiff.Position = SkiffBerth;
        AddChild(_skiff);
    }

    /// <summary>At the very top of her, turning in the wind: the great orrery, that keeps the island's time.</summary>
    void Orrery()
    {
        var at = new Vector3(0, Tiers[4].Floor + 14, Tiers[4].Aft);
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 1.6f, Height = 14, RadialSegments = 12 }, MaterialOverride = Armoury.Metal(new Color("#3a3a40"), 0.5f), Position = at + Vector3.Down * 7 });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 2.6f, Height = 5.2f, RadialSegments = 24, Rings = 12 }, Position = at, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffd27a"), EmissionEnabled = true, Emission = new Color("#ffb040"), EmissionEnergyMultiplier = 1.6f } });
        foreach (var (radius, tilt, speed) in new[] { (6f, 0.3f, 0.5f), (9f, 1.1f, -0.35f), (12.5f, 0.7f, 0.22f) })
        {
            var hold = new Node3D { Position = at, Rotation = new Vector3(tilt, 0, tilt * 0.5f) };
            var ring = new Node3D();
            hold.AddChild(ring);
            ring.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius - 0.3f, OuterRadius = radius, Rings = 48, RingSegments = 8 }, MaterialOverride = Armoury.Brass });
            ring.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.9f, Height = 1.8f, RadialSegments = 12, Rings = 8 }, MaterialOverride = Armoury.Bronze, Position = new Vector3(radius, 0, 0) });
            AddChild(hold);
            _spinning.Add((ring, Vector3.Up, speed));
        }
    }

    static Vector3 SkiffBerth => new(10, Tiers[DockTier].Floor + 5, Tiers[DockTier].Aft + Tiers[DockTier].Long + 16);

    Node3D _skiff = null!;
    Node3D? _craft;
    bool _flying, _homeward;
    float _heading, _altitude;

    /// <summary>The hero takes a skiff up alone: up and down on the stick climb and dive, left and right steer. She doesn't stop.</summary>
    public void TakeSkiff()
    {
        if (_flying) return;
        _flying = true;
        (_heading, _altitude, _craft) = (0, SkiffBerth.Y, _skiff);
        main.PlayerNode.SitAt(() => _skiff.GlobalPosition + Vector3.Up * 2.4f);
        main.HudNode.Toast("The deck comes up under you and the dock drops away. Up and down climb and dive, left and right steer. E brings her home.", 8);
    }

    void Land()
    {
        _flying = false;
        _skiff.Position = SkiffBerth;
        _skiff.Rotation = Vector3.Zero;
        main.PlayerNode.StandUp(DockAt + Vector3.Up * 0.5f);
    }

    void Fly(double delta)
    {
        var (turn, climb) = (Input.GetAxis("move_right", "move_left"), Input.GetAxis("move_back", "move_forward"));
        if (main.HudNode.Talking) (turn, climb) = (0, 0);
        _heading += turn * 0.8f * (float)delta;
        _altitude = Mathf.Clamp(_altitude + climb * 14 * (float)delta, 12, 320);
        var way = new Vector3(Mathf.Sin(_heading), 0, Mathf.Cos(_heading));
        var at = _craft!.Position + way * 26 * (float)delta;
        if (new Vector2(at.X, at.Z).Length() > 1400) at -= way * 2; // past the bank of steam she won't go: there's a reason nobody sees her
        if (Floor(at.X, at.Z) is { } rock) _altitude = Math.Max(_altitude, rock + 16);
        _craft.Position = at with { Y = _altitude };
        _craft.Rotation = new Vector3(-climb * 0.2f, _heading, -turn * 0.28f);
        main.PlayerNode.Face(_craft.GlobalPosition + way * 40);
    }

    // ---- the air war --------------------------------------------------------------------------------------------------

    const string Sky = "sky", Raiders = "sky_raid";
    /// <summary>How far a shell carries for the hero, and for the fleet and the deck guns, who hold their fire until it'll tell.</summary>
    const float ShellSpeed = 300, GunReach = 520, FleetReach = 170;
    SkyRaid? _raid;
    Flyer? _patrol;
    Vector3? _manned;
    double _gunReload, _callIn, _flak;
    readonly List<Vector3> _guns = [];

    public bool RaidOn => _foes.Count > 0;
    public IReadOnlyList<Enemy> Foes => _foes;
    public bool OnPatrol => _patrol is not null;
    public bool Manning => _manned is not null;
    public int FlyersUp => _flyers.Count(f => f.Lost <= 0);
    public double FleetHullLost => _flyers.Sum(f => f.Hull is { } h && IsInstanceValid(h) ? h.Stats.MaxHealth - h.Stats.Health : 0);

    double Danger => 1 + 0.08 * main.PlayerNode.Hero.Level;

    /// <summary>Each ship's hull: an unseen fighter that rides with her, so a dragon has something to go for and a shot has something to hit.</summary>
    void Launch(Flyer f)
    {
        var hull = Enemy.Create(Bestiary.Get("sky_hull"), f.Ship.GlobalPosition, Danger, look: "townsfolk");
        (hull.Allied, hull.Faction, hull.Foe, hull.Carried) = (true, Sky, Raiders, true);
        hull.Stats.Name = f.Name;
        main.AddEnemy(hull);
        hull.Unseen(true);
        hull.HidePlate();
        hull.HoldAloft();
        f.Hull = hull;
        f.Ship.Visible = true;
    }

    /// <summary>What's coming over the bank of steam, in a ring on the far side of the hero, going for the nearest ship.</summary>
    public void Raid(SkyRaid raid)
    {
        if (RaidOn) return;
        _raid = raid;
        var bearing = main.LootRng.NextDouble() * Math.Tau;
        var n = 0;
        foreach (var (kind, count, size) in raid.Foes)
            for (var i = 0; i < count; i++)
            {
                var a = (float)bearing + n++ * 0.22f;
                var at = Stage + new Vector3(Mathf.Sin(a) * 360, 90, Mathf.Cos(a) * 360);
                var foe = Enemy.Create(Bestiary.Get(kind), at, Danger, size: size, look: raid.Riders ? $"rider:{kind}" : null);
                (foe.Faction, foe.Foe, foe.Roam) = (Raiders, Sky, 120);
                if (raid.Riders) foe.Stats.Name = "Rider of the Eyrie";
                main.AddEnemy(foe);
                foe.TakeOff(90, 9999);
                _foes.Add(foe);
                if (NearestShip(foe.GlobalPosition) is { Hull: { } hull }) foe.Distract(hull);
            }
        Sound.Ui("error");
        main.HudNode.Toast(raid.Hail + (_patrol is null ? ". There are deck guns on every tier: man one." : "."), 10);
        main.HudNode.Log($"Brasswake: {raid.Hail}.");
    }

    Flyer? NearestShip(Vector3 from) => _flyers.Where(f => f.Lost <= 0 && f.Hull is not null).MinBy(f => f.Ship.GlobalPosition.DistanceTo(from));

    Enemy? NearestFoe(Vector3 from) => _foes.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).MinBy(e => e.GlobalPosition.DistanceTo(from));

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (!_foes.Remove(victim)) return;
        _fallen.Add(victim);
        GetTree().CreateTimer(9).Timeout += () =>
        {
            _fallen.Remove(victim);
            if (IsInstanceValid(victim)) victim.QueueFree();
        };
        if (byCompany)
        {
            var isle = main.Sea.Isle;
            var pay = isle.Downed();
            isle.Marks += pay;
            isle.Earned += pay;
            main.HudNode.Toast($"{victim.Stats.Name} comes down burning. +{pay} marks.", 4);
        }
        if (_foes.Count > 0) return;
        Sound.Ui("quest", -6);
        if (_patrol is not null) EndPatrol(true);
        else main.HudNode.Toast("The horns stop. The sky's clear, and the fleet goes back to its rounds.", 7);
        main.Save();
    }

    /// <summary>A fleet ship's turn: on her round if the sky's clear, and if it isn't, round the nearest thing in it with her guns going.</summary>
    void TendFleet(double delta)
    {
        // The guns on the tiers go off on their own: nothing with wings is getting aboard.
        if (RaidOn && (_flak -= delta) <= 0)
        {
            _flak = 0.8;
            var gun = ToGlobal(_guns[main.LootRng.Next(_guns.Count)] + Vector3.Up * 3);
            if (NearestFoe(gun) is { } target && target.GlobalPosition.DistanceTo(gun) < FleetReach) Shell(gun, target, (18 + 2.0 * main.PlayerNode.Hero.Level) * Danger, false);
        }
        foreach (var f in _flyers)
        {
            if (f.Lost > 0)
            {
                if ((f.Lost -= delta) <= 0) Launch(f);
                continue;
            }
            if (f.Hull is null) Launch(f);
            var hull = f.Hull!;
            if (!IsInstanceValid(hull) || hull.State == EnemyState.Dead) { ShotDown(f); continue; }
            hull.GlobalPosition = f.Ship.GlobalPosition + Vector3.Up * 3;
            hull.HoldAloft();
            f.Reload -= delta;
            var foe = NearestFoe(f.Ship.GlobalPosition);
            if (foe is not null && f.Reload <= 0 && foe.GlobalPosition.DistanceTo(f.Ship.GlobalPosition) < FleetReach)
            {
                f.Reload = 1.6 + main.LootRng.NextDouble() * 1.4;
                Shell(f.Ship.GlobalPosition + Vector3.Up * 4, foe, (30 + 3.0 * main.PlayerNode.Hero.Level) * Danger, false);
            }
            if (f.Manual) continue;
            var a = f.Phase + (float)_clock * f.Speed;
            var wanted = new Vector3(Mathf.Sin(a) * f.Radius, f.Height + Mathf.Sin(a * 3) * 4, Mathf.Cos(a) * f.Radius - 20);
            if (foe is not null)
            {
                var b = f.Phase + (float)_clock * 0.3f * Mathf.Sign(f.Speed);
                wanted = ToLocal(foe.GlobalPosition) + new Vector3(Mathf.Sin(b) * 13, 14, Mathf.Cos(b) * 13); // close enough to be bitten
                wanted.Y = Math.Max(wanted.Y, 40);
            }
            var step = wanted - f.Ship.Position;
            var along = Mathf.Min(step.Length(), (foe is null ? 30 : 15) * (float)delta);
            if (step.Length() < 0.01f) continue;
            f.Ship.Position += step.Normalized() * along;
            var flat = new Vector2(step.X, step.Z);
            if (flat.Length() > 0.5f)
            {
                var yaw = Mathf.LerpAngle(f.Ship.Rotation.Y, Mathf.Atan2(step.X, step.Z), Math.Min(1, (float)delta * 0.9f));
                f.Ship.Rotation = new Vector3(0, yaw, Mathf.Clamp(Mathf.AngleDifference(yaw, Mathf.Atan2(step.X, step.Z)) * -0.5f, -0.22f, 0.22f));
            }
        }
    }

    /// <summary>She's lost: smoke, a long fall into the sea, and the yard has another up in a while.</summary>
    void ShotDown(Flyer f)
    {
        f.Lost = 75;
        f.Hull = null;
        var at = f.Ship.Position;
        if (f == _patrol) EndPatrol(false);
        main.HudNode.Toast($"{f.Name} is going down! You can see the crew going over the side on their wires.", 6);
        Effects.Burst(this, f.Ship.GlobalPosition, DamageKind.Fire);
        var t = f.Ship.CreateTween();
        t.TweenProperty(f.Ship, "position", at with { Y = -12 }, 7).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        t.Parallel().TweenProperty(f.Ship, "rotation", f.Ship.Rotation + new Vector3(0.5f, 0, 0.9f), 7);
        t.TweenCallback(Callable.From(() => { f.Ship.Visible = false; f.Ship.Rotation = Vector3.Zero; }));
        Steam(at with { Y = 2 }, 22, 6, new Color(0.3f, 0.3f, 0.32f, 0.8f), 5);
    }

    /// <summary>A shell, seen going: it lands if the thing's still there to be hit.</summary>
    void Shell(Vector3 from, Enemy foe, double damage, bool byHero)
    {
        var shot = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 8, Rings = 4 }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffd27a"), EmissionEnabled = true, Emission = new Color("#ffb040"), EmissionEnergyMultiplier = 3 } };
        AddChild(shot);
        shot.GlobalPosition = from;
        var at = foe.GlobalPosition + Vector3.Up * 1.5f;
        var t = shot.CreateTween();
        t.TweenProperty(shot, "global_position", at, Math.Max(0.1, from.DistanceTo(at) / ShellSpeed));
        t.TweenCallback(Callable.From(() =>
        {
            shot.QueueFree();
            if (!IsInstanceValid(foe) || foe.State == EnemyState.Dead) return;
            Effects.Burst(this, foe.GlobalPosition + Vector3.Up * 1.5f, DamageKind.Fire);
            foe.Shelled(damage, byHero);
        }));
    }

    /// <summary>The hero's own gun: what's nearest to where they're looking, if it's anywhere near; otherwise a shell into the empty sky.</summary>
    void Fire()
    {
        var eye = main.PlayerNode.Camera.GlobalTransform;
        var ahead = -eye.Basis.Z.Normalized();
        var target = _foes.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(eye.Origin) < GunReach && (e.GlobalPosition - eye.Origin).Normalized().Dot(ahead) > 0.88f)
            .OrderByDescending(e => (e.GlobalPosition - eye.Origin).Normalized().Dot(ahead)).FirstOrDefault();
        _gunReload = 0.7;
        Sound.At("creature/growl_small", eye.Origin, -6, 0.5f, 40);
        var muzzle = eye.Origin + ahead * 3 + Vector3.Down;
        if (target is not null)
        {
            Shell(muzzle, target, (45 + 8.0 * main.PlayerNode.Hero.Level) * main.Sea.Isle.GunPower * Danger, true);
            return;
        }
        var miss = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 8, Rings = 4 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffd27a"), EmissionEnabled = true, Emission = new Color("#ffb040"), EmissionEnergyMultiplier = 3 } };
        AddChild(miss);
        miss.GlobalPosition = muzzle;
        var t = miss.CreateTween();
        t.TweenProperty(miss, "global_position", muzzle + ahead * GunReach, GunReach / ShellSpeed);
        t.TweenCallback(Callable.From(miss.QueueFree));
    }

    /// <summary>Deck guns on every tier, facing out: anyone may man one.</summary>
    void DeckGuns()
    {
        foreach (var (tier, angle) in new[] { (0, 0.5f), (0, 5.6f), (0, 2.4f), (1, 0.8f), (1, 2.9f), (1, 4f), (2, 0.5f), (2, 3.6f) })
        {
            var t = Tiers[tier];
            var at = Edge(t, angle, tier == 0, 0.9f) with { Y = t.Floor };
            Models.Place(this, "ph:cannon.glb", at, 2.2f, angle, solid: false);
            _guns.Add(at);
        }
    }

    void ManGun(Vector3 gun)
    {
        _manned = gun;
        main.PlayerNode.SitAt(() => ToGlobal(gun + Vector3.Up * 3));
        main.HudNode.Toast("You swing the gun round. Hold attack to fire: it finds whatever you're looking at, near enough. E to leave it.", 6);
    }

    void LeaveGun()
    {
        if (_manned is not { } gun) return;
        _manned = null;
        main.PlayerNode.StandUp(ToGlobal(gun + new Vector3(1.5f, 1, 1.5f)));
    }

    /// <summary>Up with the patrol: on a gun aboard a junk, or at her wheel for a helmsman, until the sky's clear or she's lost.</summary>
    public void Patrol()
    {
        if (_patrol is not null || _flyers.FirstOrDefault(f => f.Lost <= 0 && f.Hull is not null && f.Ship.Name != "dirigible") is not { } ship) return;
        var isle = main.Sea.Isle;
        _patrol = ship;
        _callIn = 6;
        main.PlayerNode.SitAt(() => ship.Ship.GlobalPosition + Vector3.Up * 7 + ship.Ship.GlobalBasis.Z * 3);
        if (isle.TakesTheWheel)
        {
            ship.Manual = true;
            (_flying, _craft, _heading, _altitude) = (true, ship.Ship, ship.Ship.Rotation.Y, ship.Ship.Position.Y);
        }
        main.HudNode.Toast(isle.TakesTheWheel ? $"She's yours: {ship.Name}. Up and down climb and dive, left and right steer, and hold attack to fire. They've just sighted something." : $"A wire takes you up to {ship.Name}, and somebody hands you a gun. Hold attack to fire at what you're looking at. The lookout's shouting.", 8);
    }

    /// <summary>Back on the Sky Dock: paid if the sky was cleared, and a little for the trouble if it wasn't.</summary>
    void EndPatrol(bool won)
    {
        if (_patrol is not { } ship) return;
        _patrol = null;
        ship.Manual = false;
        _flying = false;
        main.PlayerNode.StandUp(DockAt + Vector3.Up * 0.5f);
        var raid = _raid ?? Brasswake.Raids[0];
        var isle = main.Sea.Isle;
        var (pay, air, made) = isle.Sortie(raid, won);
        main.HudNode.Toast(won ? $"{ship.Name} comes in with her paint burnt off and everybody cheering. +{pay} marks." : $"You come down the wire to the Sky Dock with your hands burning. +{pay} marks, for the trouble.", 8);
        if (air is { } r) main.HudNode.Toast($"Aune Rook looks you over, then pins something brass on you. You're a {IsleBook.Title(r)} now.{(r == AirRank.Helmsman ? " Next patrol, you take the wheel." : "")}", 10);
        if (made is { } rank) main.HudNode.Toast($"You're {IsleBook.Title(rank)} now.", 8);
        main.Save();
    }

    /// <summary>Off the top of the island and away: a little of the flight, before the hood goes on.</summary>
    public void FlyHome()
    {
        if (_homeward) return;
        _homeward = true;
        main.PlayerNode.SitAt(() => _skiff.GlobalPosition + Vector3.Up * 2.4f);
        var t = _skiff.CreateTween();
        t.TweenProperty(_skiff, "position", SkiffBerth + new Vector3(60, 60, 200), 7).SetTrans(Tween.TransitionType.Sine);
        t.TweenCallback(Callable.From(() =>
        {
            main.PlayerNode.StandUp(Gangway);
            main.LeaveIsle();
        }));
        main.HudNode.Toast("The skiff lifts off the Sky Dock, and for a moment you can see all of it: the tiers, the wheel, the sea going white behind the paddles.", 8);
    }

    /// <summary>A flying junk: a hull with no water under it, ribbed orange sails, brass round her bow and engines at her stern. Her bow is +Z.</summary>
    Node3D Junk(float size, string sail)
    {
        var ship = new Node3D();
        var inner = new Node3D { Scale = Vector3.One * size };
        ship.AddChild(inner);
        if (Models.Scene("ph:pinnace.glb")?.Instantiate() is Node3D hull)
        {
            inner.AddChild(hull);
            foreach (var m in hull.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Name.ToString().Contains("rigging") || m.Name.ToString().Contains("sails"))) m.Visible = false;
        }
        else inner.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(7, 4, 30) }, MaterialOverride = Clad(Surface.Planks, new Color("#6a4a30"), 0.3f), Position = Vector3.Up * 2 });
        var cloth = new StandardMaterial3D { AlbedoColor = new Color(sail), Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var rib = Armoury.Leather("#3a2418");
        foreach (var (z, tall, wide) in new[] { (9f, 18f, 11f), (-2f, 26f, 15f), (-12f, 19f, 11f) })
        {
            inner.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.26f, Height = tall + 4, RadialSegments = 8 }, MaterialOverride = rib, Position = new Vector3(0, (tall + 4) / 2 + 3, z) });
            inner.AddChild(new MeshInstance3D { Mesh = Sail(wide, tall), MaterialOverride = cloth, Position = new Vector3(0.4f, 6, z + 1) });
            for (var i = 1; i < 6; i++)
            {
                var f = i / 6f;
                inner.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.14f, wide * (1.1f - f * 0.72f)) }, MaterialOverride = rib, Position = new Vector3(0.5f, 6 + tall * f, z + 1 - wide * f * 0.28f) });
            }
        }
        // The gilded bow, and three engines at the stern.
        inner.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 2.4f, OuterRadius = 3.1f, Rings = 28, RingSegments = 8 }, MaterialOverride = Armoury.Brass, Position = new Vector3(0, 5, 15.5f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        var burn = new StandardMaterial3D { AlbedoColor = new Color("#bfe8ff"), EmissionEnabled = true, Emission = new Color("#9fdcff"), EmissionEnergyMultiplier = 3 };
        foreach (var (x, y) in new[] { (-2.6f, 3f), (2.6f, 3f), (0f, 5.4f) })
        {
            inner.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1.3f, Height = 5, RadialSegments = 14 }, MaterialOverride = Armoury.Brass, Position = new Vector3(x, y, -14), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
            inner.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.8f, Height = 0.2f, RadialSegments = 14 }, MaterialOverride = burn, Position = new Vector3(x, y, -16.6f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        }
        return ship;
    }

    /// <summary>A junk's sail: a fan, wide at the foot and swept back at the head, seen from the side.</summary>
    static ArrayMesh Sail(float wide, float tall)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var edge = new[] { new Vector3(0, 0, wide * 0.55f), new Vector3(0, tall * 0.45f, wide * 0.42f), new Vector3(0, tall * 0.8f, wide * 0.1f), new Vector3(0, tall, -wide * 0.26f), new Vector3(0, tall * 0.62f, -wide * 0.5f), new Vector3(0, tall * 0.22f, -wide * 0.56f), new Vector3(0, 0, -wide * 0.45f) };
        var hub = new Vector3(0, tall * 0.3f, 0);
        for (var i = 0; i < edge.Length; i++)
            foreach (var v in new[] { hub, edge[i], edge[(i + 1) % edge.Length] }) st.AddVertex(v);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>A dirigible: a long cream envelope, ribbed, with a small hull slung under it. Her bow is +Z.</summary>
    Node3D Dirigible(float size)
    {
        var ship = new Node3D { Scale = Vector3.One * size, Name = "dirigible" };
        var cream = new StandardMaterial3D { AlbedoColor = new Color("#d8d0bc"), Roughness = 0.7f };
        ship.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 32, Rings = 16 }, MaterialOverride = cream, Position = Vector3.Up * 16, Scale = new Vector3(9, 9, 30) });
        var strap = Armoury.Leather("#3a2418");
        foreach (var z in new[] { -20f, -10f, 0f, 10f, 20f })
        {
            var r = 9 * Mathf.Sqrt(1 - z * z / 900);
            ship.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = r - 0.1f, OuterRadius = r + 0.2f, Rings = 36, RingSegments = 6 }, MaterialOverride = strap, Position = new Vector3(0, 16, z), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        }
        if (Models.Scene("ph:pinnace.glb")?.Instantiate() is Node3D hull)
        {
            ship.AddChild(hull);
            foreach (var m in hull.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Name.ToString().Contains("rigging") || m.Name.ToString().Contains("sails"))) m.Visible = false;
        }
        foreach (var (x, z) in new[] { (-3f, 10f), (3f, 10f), (-3f, -10f), (3f, -10f) })
            ship.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 12, 0.2f) }, MaterialOverride = strap, Position = new Vector3(x, 8, z) });
        foreach (var side in new[] { -1f, 1f })
        {
            var prop = new Node3D { Position = new Vector3(side * 8, 5, -14) };
            prop.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 7, 0.2f) }, MaterialOverride = Armoury.Brass });
            prop.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(7, 0.5f, 0.2f) }, MaterialOverride = Armoury.Brass });
            ship.AddChild(prop);
            _spinning.Add((prop, Vector3.Back, 9));
        }
        return ship;
    }

    /// <summary>What's flying round her: the junks that keep the sea clear, and the great gas-ship.</summary>
    void Fleet()
    {
        var ships = new (Node3D Ship, string Name, float Radius, float Height, float Speed, float Phase)[]
        {
            (Junk(1, "#c8703a"), "the Long Kettle", 190, 62, 0.13f, 0.4f),
            (Junk(0.8f, "#b85a30"), "the Third Attempt", 260, 96, -0.09f, 2.2f),
            (Junk(1.2f, "#d08040"), "the Great Aunt", 165, 118, 0.15f, 3.9f),
            (Dirigible(1.1f), "the Slow Argument", 300, 150, 0.06f, 1.2f),
            (Dirigible(0.8f), "the Prudence", 230, 176, -0.05f, 4.7f),
        };
        foreach (var (ship, name, radius, height, speed, phase) in ships)
        {
            ship.Position = new Vector3(Mathf.Sin(phase) * radius, height, Mathf.Cos(phase) * radius);
            AddChild(ship);
            _flyers.Add(new Flyer(ship, name, radius, height, speed, phase));
        }
    }

    // ---- the people -----------------------------------------------------------------------------------------------------

    void Folk()
    {
        Person Meet(string id, string name, Sex sex, int age, string job, string about, string line, string look, Vector3 at)
        {
            var p = main.PersonAt(new Npc(id, name, sex, age, job, Brasswake.Port, Brasswake.Name, about, ["the sea", "craft"], "brisk", line, Stage.X + at.X, Stage.Z + at.Z), look, Stage.Y + at.Y);
            _people.Add(p);
            return p;
        }
        Meet("isle/warden", "Warden Ilsabet Cray", Sex.Female, 52, "warden of the gangway", "She's turned back eleven ships in thirty years, and she remembers all of them. Everything aboard has been written down by someone; she's the someone.", "Name, and what you've brought.", "guard", new Vector3(Tiers[0].Wide - 8, Tiers[0].Floor, 12));
        Meet("isle/dockmaster", "Hesk Ardo", Sex.Male, 58, "dockmaster of the Strangers' Dock", "Has tied up every ship that's come alongside for thirty years, and never once let one stay past its welcome.", "Lines ashore, and mind the gap. She doesn't stop for anyone.", "worker", new Vector3(Tiers[0].Wide + 15, Tiers[0].Floor, 19));
        Meet("isle/archwright", "Archwright Sefa Vell", Sex.Female, 71, "Archwright of Brasswake", "The ninth to hold the office, and the first to say out loud that the island's getting smaller. She has oil on her cuffs, and doesn't care.", "You found us. Sit down before you touch anything.", "scholar", new Vector3(4, Tiers[2].Floor, Tiers[2].Aft + Tiers[2].Long - 10));
        Meet("isle/engineer", "Dorrin Pike", Sex.Male, 44, "chief of the Steam Gallery", "Burn-scarred to the elbows and proud of it. He hears the engines change in his sleep.", "Mind the hot bits. They're all hot bits.", "worker", GalleryAt + new Vector3(0, 0, 8));
        Meet("isle/stores", "Tobble Marsh", Sex.Other, 38, "keeper of the Cog Market", "Keeps the book on every spring and rivet on the island, and can tell you what any of it cost to make in hours.", "Marks only, and it all comes back.", "worker", new Vector3(62, Tiers[0].Floor, 33));
        Meet("isle/skymaster", "Aune Rook", Sex.Female, 29, "skymaster of the Sky Dock", "Took a skiff through the Brass Gate at sixteen, for a bet, and they made her skymaster to keep an eye on her.", "Don't look down. It's fine, I'm only saying that.", "hunter", new Vector3(5, Tiers[DockTier].Floor, Tiers[DockTier].Aft + Tiers[DockTier].Long - 8));
        // The automatons who walk the streets, sweeping and carrying.
        var beats = new[] { (0, 0.1f), (0, 2f), (1, 0.4f), (1, 3.4f), (2, 0.4f), (2, 3.4f) };
        foreach (var (tier, start) in beats)
        {
            var body = Clockwork.For("Scrap");
            AddChild(body);
            var t = Tiers[tier + 1];
            var round = new Vector3[8];
            for (var i = 0; i < round.Length; i++) round[i] = Edge(Tiers[tier], start + i * 0.1f, tier == 0, 0.93f) with { Y = Tiers[tier].Floor + 0.1f };
            body.Position = round[0];
            _walkers.Add((body, round, 1, 1.4f));
        }
    }

    /// <summary>The hero's first time aboard: the warden, and the terms.</summary>
    public void Greet(Person warden) => main.HudNode.Talk($"{warden.Npc.Name}, {warden.Npc.Occupation}",
        $"There's a ship with no water under her over your head, and a brass cannon looking you over, and it goes on looking until your lines are ashore. \"Any ship may tie up,\" says the woman at the gangway. \"That's the oldest law we've got. This is {Brasswake.Name}. You may look. You may work. You may trade at the dock. What's made here stays here, and if you ever come back on a dragon, you'll find out what the guns are for. Do you understand me?\"",
        ("I understand.", () =>
        {
            main.HudNode.EndTalk();
            main.HudNode.Toast("Marks at the valves, at the stern. The dockmaster trades at the Strangers' Dock. The lifts on the east face go up through the tiers, and the Sky Dock's at the very top.", 9);
        }));

    public override void _Process(double delta)
    {
        _clock += delta;
        foreach (var (node, axis, speed) in _spinning) node.RotateObjectLocal(axis, speed * (float)delta);
        TendFleet(delta);
        if (_foam is not null) _foam.Position = _foam.Position with { Z = -Mathf.PosMod((float)_clock * 7, 450) };
        for (var i = 0; i < _walkers.Count; i++)
        {
            var (body, beat, next, speed) = _walkers[i];
            var to = beat[next];
            var step = to - body.Position;
            if (step.Length() < 0.3f) { _walkers[i] = (body, beat, (next + 1) % beat.Length, speed); continue; }
            body.Position += step.Normalized() * speed * (float)delta;
            body.Rotation = new Vector3(0, Mathf.Atan2(step.X, step.Z), 0);
        }
        if (_flying) Fly(delta);
        if ((_manned is not null || _patrol is not null) && !main.HudNode.Talking && (_gunReload -= delta) <= 0 && Input.IsActionPressed("attack")) Fire();
        if (_patrol is not null && !RaidOn && (_callIn -= delta) <= 0) Raid(Brasswake.RaidFor(main.Sea.Isle.Air, main.LootRng));
        var isle = main.Sea.Isle;
        if (!RaidOn && _patrol is null && main.GuildBook.PlaySeconds >= isle.NextRaidAt && !main.HudNode.Talking)
        {
            isle.NextRaidAt = main.GuildBook.PlaySeconds + Brasswake.RaidEvery;
            Raid(Brasswake.RaidFor(isle.Air, main.LootRng));
        }
        if (OnShift && (_valveLeft -= delta) <= 0)
        {
            main.HudNode.Toast("The valve lets go with a bang, and the whole gallery fills with steam. No marks for that one.", 5);
            NextValve();
        }
        // Over the edge: the deck crew's automatons have a net for that.
        if (!_flying && !_homeward && main.PlayerNode.Seat is null && main.PlayerNode.GlobalPosition.Y < Stage.Y + 4)
        {
            main.PlayerNode.GlobalPosition = Gangway;
            main.PlayerNode.Velocity = Vector3.Zero;
            main.PlayerNode.ResetPhysicsInterpolation();
            main.HudNode.Toast("A brass arm comes out of the hull and hooks you out of the sea by the belt. It sets you down at the gangway without comment.", 6);
        }
    }

    /// <summary>For the pictures and the self-test.</summary>
    public IReadOnlyList<Vector3> LiftFoots => _lifts.Select(l => ToGlobal(l.From)).ToList();
    public IReadOnlyList<Vector3> LiftTops => _lifts.Select(l => ToGlobal(l.To)).ToList();
    public int Flyers => _flyers.Count;
    public bool Flying => _flying;
    public Vector3 SkiffAt => _skiff.GlobalPosition;
    public IReadOnlyList<Vector3> DeckGunsAt => _guns.Select(ToGlobal).ToList();
    public Vector3? ShipAt(int i) => i < _flyers.Count ? _flyers[i].Ship.GlobalPosition : null;
    public void ShootForTest() => Fire();
    public float TierFloor(int i) => Stage.Y + Tiers[i].Floor;
}
