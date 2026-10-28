using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Everything to do besides fighting and trading (see <see cref="PursuitBook"/>): the allotments south of the walls
/// and a garden at home; fires anywhere outside town; the Tumblers' Run, an obstacle course west of the walls;
/// lifting purses; snares in the wilds; treasure hunts from torn maps; a house of your own on the plot north of the
/// walls, built stage by stage and furnished; and feats for all of it.
/// </summary>
public partial class Main
{
    PursuitBook _pursuits = new();
    Node3D _pursuitRoot = null!;
    readonly Dictionary<string, (Vector3 At, Node3D Body, string Shown)> _beds = [];
    readonly List<(Node3D Body, Vector3 At, double Until)> _fires = [];
    readonly Dictionary<string, Node3D> _snareBodies = [];
    Node3D? _houseBody;
    string _houseShown = "";
    double _pursuitTick;
    int _lapNext;

    public PursuitBook Pursuits => _pursuits;

    public static readonly Vector3 AllotmentsAt = new(25, 0, 130), HousePlotAt = new(-35, 0, -135), CourseAt = new(-150, 0, 28);

    public bool WellRested => _pursuits.RestedUntil > _guilds.PlaySeconds;

    // ---- setting up ----------------------------------------------------------------------------------------

    void AddPursuits()
    {
        _pursuitRoot = new Node3D { Name = "Pursuits" };
        AddChild(_pursuitRoot);
        var keepers = new (Shop Shop, Vector3 At, string Look, Sex Sex, int Age, string Who, string Story, string Temper)[]
        {
            (Shops.Allotments, AllotmentsAt + new Vector3(10, 0, -4), "worker", Sex.Female, 67, "keeper of the allotments", "Gilly Thatch has kept the Commons Allotments for forty years, and grown the prize marrow at every harvest fair for thirty of them. She'll rent you a bed for nothing, if you'll keep the slugs off hers.", "warm"),
            (Shops.Jeweller, FactorAt + new Vector3(5, 0, 4), "scholar", Sex.Female, 41, "jeweller", "Ilka Brightwater cut stones for the Crown until she fell out with the Crown's taste. Now she cuts them for anyone, and lends her bench to anyone who'll learn.", "dry"),
            (Shops.Course, CourseAt + new Vector3(4, 0, 4), "traveller", Sex.Male, 29, "keeper of the Tumblers' Run", "Cole Sparrow ran rooftops for the Lantern Boys until he fell off one. He built the Run so nobody else would learn the way he did.", "cheerful"),
        };
        foreach (var (shop, at, look, sex, age, who, story, temper) in keepers)
            Townsfolk(new Npc(shop.Id, shop.Keeper, sex, age, who, "city:tristinwold", "Tristinwold", story, ["trade", "the road"], temper, shop.Greeting, at.X, at.Z), look);
        City.Zones.Add(new Zone("The Commons Allotments", AllotmentsAt, 16, 9));
        City.Zones.Add(new Zone("The Tumblers' Run", CourseAt, 20, 9));
        City.Zones.Add(new Zone("Hearthside", HousePlotAt, 14, 9));
        Sign(AllotmentsAt + new Vector3(0, 4, -6), "The Commons Allotments");
        Sign(CourseAt + new Vector3(0, 6, 0), "The Tumblers' Run");
        Sign(HousePlotAt + new Vector3(0, 7, 0), "Hearthside");

        // The allotments: six beds in two rows, a barrow and some sacks.
        for (var i = 0; i < 6; i++)
            _beds[$"allotment_{i}"] = (AllotmentsAt + new Vector3(-6 + i % 3 * 6, 0, -2.5f + i / 3 * 5), Bed(AllotmentsAt + new Vector3(-6 + i % 3 * 6, 0, -2.5f + i / 3 * 5)), "");
        foreach (var (path, off) in new[] { ("medieval/props/wheelbarrow.gltf", new Vector3(10, 0, 3)), ("medieval/props/sack.gltf", new Vector3(11, 0, -1)), ("medieval/props/bucket_water.gltf", new Vector3(-10, 0, 0)) })
            Models.Place(_pursuitRoot, path, AllotmentsAt + off, 5, off.X);

        BuildCourse();
        ShowHouse();
    }

    /// <summary>A bed of dug earth, edged with planks.</summary>
    Node3D Bed(Vector3 at)
    {
        var bed = new Node3D { Position = at with { Y = City.FloorAt(at) } };
        _pursuitRoot.AddChild(bed);
        bed.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4.4f, 0.22f, 2.6f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#4a3424"), Roughness = 1 }, Position = Vector3.Up * 0.08f });
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#7a5a3a"), Roughness = 0.9f };
        foreach (var (x, z, w, d) in new[] { (0f, -1.35f, 4.6f, 0.12f), (0f, 1.35f, 4.6f, 0.12f), (-2.25f, 0f, 0.12f, 2.6f), (2.25f, 0f, 0.12f, 2.6f) })
            bed.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, 0.3f, d) }, MaterialOverride = wood, Position = new Vector3(x, 0.12f, z) });
        return bed;
    }

    // ---- every frame ---------------------------------------------------------------------------------------

    void TendPursuits(double delta)
    {
        var now = _guilds.PlaySeconds;
        var pos = _player.GlobalPosition;
        // Warm by a fire, out of a fight: the hurt heal.
        _fires.RemoveAll(f =>
        {
            if (now < f.Until && IsInstanceValid(f.Body)) return false;
            if (IsInstanceValid(f.Body)) f.Body.QueueFree();
            return true;
        });
        if (!_tracker.InFight && _fires.Any(f => f.At.DistanceTo(pos) < 5)) _player.Stats.Heal(_player.Stats.MaxHealth * Firemaking.WarmHeal * delta);
        if ((_pursuitTick -= delta) > 0) return;
        _pursuitTick = 1;
        // The beds show what's growing in them.
        foreach (var id in _beds.Keys.ToList())
        {
            var (at, body, shown) = _beds[id];
            var now_ = PlotLook(id, now);
            if (now_ == shown) continue;
            foreach (var c in body.GetChildren().Where(c => c.HasMeta("plant"))) c.QueueFree();
            if (_pursuits.Plots.TryGetValue(id, out var p) && Farming.FromSeed(p.Seed) is { } crop) Grow(body, crop, Farming.Growth(crop, p.PlantedAt, now));
            _beds[id] = (at, body, now_);
        }
        // Snares in sight.
        foreach (var s in _pursuits.Snares.Where(s => !_snareBodies.ContainsKey(s.Id) && new Vector2((float)s.X - pos.X, (float)s.Z - pos.Z).Length() < 120))
            _snareBodies[s.Id] = SnareBody(s);
        foreach (var id in _snareBodies.Keys.Where(id => _pursuits.Snares.All(s => s.Id != id)).ToList())
        {
            _snareBodies[id].QueueFree();
            _snareBodies.Remove(id);
        }
        if (_houseShown != HouseKey) ShowHouse();
    }

    /// <summary>What a bed looks like now, as a key: empty, or a crop at a stage.</summary>
    string PlotLook(string id, double now) =>
        _pursuits.Plots.TryGetValue(id, out var p) && Farming.FromSeed(p.Seed) is { } c ? $"{c.Seed}:{(int)(Farming.Growth(c, p.PlantedAt, now) * 4)}" : "";

    /// <summary>A crop in its bed: rows of leaves growing up, and the crop's colour showing when it's ripe.</summary>
    static void Grow(Node3D bed, Crop crop, double growth)
    {
        var leaf = new StandardMaterial3D { AlbedoColor = new Color("#4a8a3a"), Roughness = 0.9f };
        var ripe = new StandardMaterial3D { AlbedoColor = new Color(crop.Colour), Roughness = 0.6f };
        var size = 0.15f + (float)growth * 0.45f;
        for (var i = 0; i < 8; i++)
        {
            var at = new Vector3(-1.6f + i % 4 * 1.07f, 0.2f + size * 0.4f, i < 4 ? -0.6f : 0.6f);
            var plant = new MeshInstance3D { Mesh = new SphereMesh { Radius = size * 0.5f, Height = size * 0.8f }, MaterialOverride = leaf, Position = at, Scale = new Vector3(1, 0.7f, 1) };
            plant.SetMeta("plant", true);
            bed.AddChild(plant);
            if (growth < 1) continue;
            var fruit = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f }, MaterialOverride = ripe, Position = at + new Vector3(0.12f, size * 0.3f, 0.05f) };
            fruit.SetMeta("plant", true);
            bed.AddChild(fruit);
        }
    }

    /// <summary>What there is to do here with E: a bed, a fire, an obstacle, a snare, a dig, the house.</summary>
    (string Verb, Action Act)? PursuitAction(Vector3 pos)
    {
        var now = _guilds.PlaySeconds;
        foreach (var (id, (at, _, _)) in _beds)
        {
            if (at.DistanceTo(pos with { Y = at.Y }) > 2.8f) continue;
            if (!_pursuits.Plots.TryGetValue(id, out var p) || Farming.FromSeed(p.Seed) is not { } crop) return ("plant something", () => PlantMenu(id));
            var g = Farming.Growth(crop, p.PlantedAt, now);
            return g >= 1 ? ($"harvest the {crop.Name.ToLowerInvariant()}", () => Harvest(id)) : ($"look at the {crop.Name.ToLowerInvariant()} ({g:P0} grown)", () => _hud.Toast($"{crop.Name}: {g:P0} grown. About {Math.Ceiling((1 - g) * crop.GrowSeconds / 60)} more minutes.", 3));
        }
        if (_fires.FirstOrDefault(f => f.At.DistanceTo(pos) < 3) is { Body: not null } fire) return ("cook at the fire", () => ShowCraft(Job.Cooking, _hud.EndTalk));
        if (CourseObstacleAt(pos) is { } o) return (Agility.Course[o].Verb, () => _ = Run(o));
        if (_pursuits.Snares.FirstOrDefault(s => new Vector2((float)s.X - pos.X, (float)s.Z - pos.Z).Length() < 2.5f) is { } snare)
            return ("check the snare", () => CheckSnare(snare));
        if (_pursuits.Hunt?.Current is { } clue && _player.Pack.Count("spade") > 0 && new Vector2((float)clue.X - pos.X, (float)clue.Z - pos.Z).Length() < 25)
            return ("dig for the treasure", Dig);
        if (pos.DistanceTo(HousePlotAt with { Y = pos.Y }) < 8)
            return (_pursuits.House.Owned ? "see to your house" : "look at the plot", HouseMenu);
        return null;
    }

    /// <summary>Lines for the tracker: a treasure clue, snares out, well rested.</summary>
    IEnumerable<string> PursuitLines(Vector3 pos)
    {
        if (_pursuits.Hunt?.Current is { } clue)
            yield return $"Treasure: {clue.Text} ({Direction(pos, (clue.LandmarkX, clue.LandmarkZ))} to {clue.Landmark})";
        if (_pursuits.Snares.Count > 0)
        {
            var ready = _pursuits.Snares.Count(s => s.ReadyAt <= _guilds.PlaySeconds);
            var nearest = _pursuits.Snares.MinBy(s => new Vector2((float)s.X - pos.X, (float)s.Z - pos.Z).Length())!;
            yield return $"Snares: {_pursuits.Snares.Count} set, {ready} sprung (nearest {Direction(pos, (nearest.X, nearest.Z))})";
        }
        if (WellRested) yield return $"Well rested: a tenth more experience for {(int)((_pursuits.RestedUntil - _guilds.PlaySeconds) / 60) + 1} more minutes";
    }

    /// <summary>What the pack can start: a fire, a snare, a treasure hunt, a dig, the way home.</summary>
    IEnumerable<(string, Action)> PackPursuits()
    {
        var pack = _player.Pack;
        var pos = _player.GlobalPosition;
        var inTown = new Vector2(pos.X, pos.Z).Length() < City.OuterRadius + 30;
        if (pack.Count("tinderbox") > 0 && Firemaking.Best(pack) is { } log)
            yield return (inTown ? "Light a fire (not in town)" : $"Light a fire ({Items.Get(log.Log)!.Name.ToLowerInvariant()})", () => { _hud.EndTalk(); LightFire(); });
        if (pack.Count("snare") > 0) yield return (inTown ? "Set a snare (out in the wilds, not in town)" : "Set a snare here", () => { _hud.EndTalk(); SetSnare(); });
        if (pack.Count("torn_map") > 0 && _pursuits.Hunt is null) yield return ("Read the torn map", ReadMap);
        if (_pursuits.Hunt is not null && pack.Count("spade") > 0) yield return ("Dig here (spade)", () => { _hud.EndTalk(); Dig(); });
        if (pack.Count("home_charm") > 0) yield return ("Use the home charm", () => { _hud.EndTalk(); GoHome(); });
    }

    // ---- feats ---------------------------------------------------------------------------------------------

    /// <summary>Counts something done; any feat it earns is announced.</summary>
    public void Did(string counter, int by = 1) => Announce(_pursuits.Count(counter, by));

    void Announce(IEnumerable<Feat> feats)
    {
        foreach (var f in feats)
        {
            Sound.Ui("levelup", -2);
            _hud.Toast($"Feat: {f.Name}! {f.Text} (+{f.Points} points; {_pursuits.Points} in all)", 6);
        }
    }

    /// <summary>The trade feats: the best level reached, and how many trades are at five or more.</summary>
    void TradeFeats()
    {
        var levels = Enum.GetValues<Job>().Select(j => _player.Pack.Level(j)).ToList();
        Announce(_pursuits.Set("best_trade", levels.Max()));
        Announce(_pursuits.Set("trades_at_5", levels.Count(l => l >= 5)));
    }

    /// <summary>Something made at a bench, a forge, a fire or a kitchen: counted for the feats.</summary>
    void Made(Recipe r)
    {
        if (r.Job == Job.Cooking) Did("meals");
        if (r.Item.Slot is Slot.Ring or Slot.Neck) Did("jewellery");
    }

    public void ShowFeats()
    {
        var earned = Feats.All.Where(f => _pursuits.Feats.Contains(f.Feat.Id)).ToList();
        var text = $"{earned.Count} of {Feats.All.Count} feats, {_pursuits.Points} points.\n" +
                   string.Join("\n", Feats.All.Select(f => $"{(_pursuits.Feats.Contains(f.Feat.Id) ? "[x]" : "[ ]")} {f.Feat.Name} ({f.Feat.Points}): {f.Feat.Text}" +
                       (_pursuits.Feats.Contains(f.Feat.Id) || f.Needs <= 1 ? "" : $" {Math.Min(f.Needs, _pursuits.Counters.GetValueOrDefault(f.Counter))}/{f.Needs}")));
        _hud.Talk("Feats", text, ("Back to the menu", GameMenu), ("Back to the game", _hud.EndTalk));
    }

    // ---- the allotments ------------------------------------------------------------------------------------

    void PlantMenu(string plot)
    {
        var pack = _player.Pack;
        var level = pack.Level(Job.Farming);
        var options = Farming.Crops.Where(c => pack.Count(c.Seed) > 0).Select(c => (c.Level > level
            ? $"({c.Name}: needs farming {c.Level})"
            : $"Plant {c.Name.ToLowerInvariant()} ({pack.Count(c.Seed)} seed{(pack.Count(c.Seed) == 1 ? "" : "s")}; ready in {c.GrowSeconds / 60:0} minutes)",
            (Action)(() =>
            {
                if (c.Level > level) { Sound.Ui("error"); _hud.Toast($"{c.Name} need farming {c.Level}; you're {level}.", 3); return; }
                pack.Take(c.Seed);
                _pursuits.Plots[plot] = new Planting { Seed = c.Seed, PlantedAt = _guilds.PlaySeconds };
                var up = pack.Practise(Job.Farming, Farming.PlantXp(c));
                Sound.Ui("herb");
                _hud.EndTalk();
                _hud.Toast($"You plant {c.Name.ToLowerInvariant()}. Come back in {c.GrowSeconds / 60:0} minutes.{(up is { } l ? $" Farming rises to {l}!" : "")}", 4);
                Save();
            }))).ToList();
        options.Add(("Leave it", _hud.EndTalk));
        _hud.Talk("An empty bed", options.Count == 1 ? "You've no seeds. Gilly Thatch sells them, by the allotments." : "What will you plant?", [.. options]);
    }

    void Harvest(string plot)
    {
        if (!_pursuits.Plots.Remove(plot, out var p) || Farming.FromSeed(p.Seed) is not { } crop) return;
        var level = _player.Pack.Level(Job.Farming);
        var n = Farming.Yield(crop, level, _lootRng);
        _player.Pack.Add(crop.Gives, n);
        var up = _player.Pack.Practise(Job.Farming, Farming.HarvestXp(crop));
        Hands()?.Perform("Use_Item", 0.6, 1.2f);
        Sound.Ui("herb");
        _hud.Toast($"You harvest {Items.Count(crop.Gives, n).ToLowerInvariant()}.{(up is { } l ? $" Farming rises to {l}!" : "")}", 4);
        Did("harvests");
        Save();
    }

    // ---- fires ---------------------------------------------------------------------------------------------

    void LightFire()
    {
        var pos = _player.GlobalPosition;
        if (new Vector2(pos.X, pos.Z).Length() < City.OuterRadius + 30) { Sound.Ui("error"); _hud.Toast("Not in town: the Watch would have words. Go out past the walls.", 4); return; }
        if (_player.Swimming || City.OnPier(pos)) { Sound.Ui("error"); _hud.Toast("Not here.", 3); return; }
        if (Firemaking.Best(_player.Pack) is not { } log) return;
        var level = _player.Pack.Level(Job.Firemaking);
        Hands()?.Perform("Use_Item", 0.8, 1);
        if (!_lootRng.Chance(Firemaking.Catches(level))) { _hud.Toast("The tinder smokes and dies. Try again.", 3); return; }
        _player.Pack.Take(log.Log);
        var at = pos - _player.GlobalTransform.Basis.Z * 1.6f;
        at.Y = City.FloorAt(at);
        _fires.Add((Fire(at), at, _guilds.PlaySeconds + log.Seconds));
        var up = _player.Pack.Practise(Job.Firemaking, log.Xp);
        _hud.Toast($"The fire catches. Cook at it (E), or rest by it to heal.{(up is { } l ? $" Firemaking rises to {l}!" : "")}", 4);
        Did("fires");
        Save();
    }

    /// <summary>A campfire: a ring of stones, flames and sparks, a warm light and its crackle.</summary>
    Node3D Fire(Vector3 at)
    {
        var n = new Node3D { Position = at };
        _pursuitRoot.AddChild(n);
        Models.Place(n, "ph:fire_pit.glb", Vector3.Zero, 0.9f, 0, solid: false);
        var flame = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("#ffa040"), AlbedoTexture = Effects.Dot,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        };
        var rise = new Curve();
        rise.AddPoint(new Vector2(0, 1));
        rise.AddPoint(new Vector2(1, 0));
        n.AddChild(new CpuParticles3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * 0.5f, Material = flame }, Amount = 40, Lifetime = 0.8, Position = Vector3.Up * 0.25f,
            Direction = Vector3.Up, Spread = 12, InitialVelocityMin = 0.8f, InitialVelocityMax = 1.6f, Gravity = new Vector3(0, 0.6f, 0),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.25f, ScaleAmountCurve = rise,
        });
        n.AddChild(new OmniLight3D { LightColor = new Color("#ff9a40"), LightEnergy = 2.2f, OmniRange = 8, Position = Vector3.Up * 0.8f, ShadowEnabled = false });
        if (ResourceLoader.Exists("res://assets/audio/amb/fire.ogg"))
            n.AddChild(new AudioStreamPlayer3D { Stream = GD.Load<AudioStream>("res://assets/audio/amb/fire.ogg"), Bus = Sound.Effects, Autoplay = true, UnitSize = 3, MaxDistance = 20, VolumeDb = -6 });
        return n;
    }

    // ---- the Tumblers' Run ---------------------------------------------------------------------------------

    /// <summary>Each obstacle's way across, in order: where it starts, the path over it, and where it lands.</summary>
    static readonly Vector3[][] CoursePaths =
    [
        [new(-9, 0, -10), new(-4, 0.85f, -10), new(1, 0.85f, -10), new(4, 0, -10)],      // the balance log
        [new(7, 0, -10), new(7, 1.5f, -12), new(7, 3.1f, -13.5f), new(7, 3.1f, -16)],     // the climbing net, up onto the platform
        [new(7, 3.1f, -17), new(7, 2, -20), new(7, 1.8f, -23), new(7, 0, -26)],           // the rope swing, down over the pit
        [new(3, 0, -28), new(0.5f, 0.3f, -28), new(-2.5f, 0.3f, -27.5f), new(-5.5f, 0.3f, -28), new(-8.5f, 0.3f, -27.5f), new(-11, 0, -28)], // the stepping stones
        [new(-13, 0, -25), new(-13, 2.6f, -23), new(-13, 2.6f, -21.5f), new(-13, 0, -19.5f)],  // the wall
        [new(-13, 0, -16), new(-13, 2.8f, -15), new(-13, 1.4f, -12.5f), new(-12, 0, -11)], // up the ladder, down the slide
    ];

    void BuildCourse()
    {
        var root = new Node3D { Position = CourseAt with { Y = City.FloorAt(CourseAt) } };
        _pursuitRoot.AddChild(root);
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#7a5a3a"), Roughness = 0.9f };
        var rope = new StandardMaterial3D { AlbedoColor = new Color("#c8b48a"), Roughness = 1 };
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#8a8a84"), Roughness = 1 };
        var water = new StandardMaterial3D { AlbedoColor = new Color("#3a6a8a"), Roughness = 0.1f, Metallic = 0.2f };
        void Box(Vector3 at, Vector3 size, Material m, bool solid = false)
        {
            Node3D n = solid ? new StaticBody3D { Position = at } : new Node3D { Position = at };
            n.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m });
            if (solid) n.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            root.AddChild(n);
        }
        void Post(Vector3 from, Vector3 to, float r, Material m)
        {
            var mid = (from + to) / 2;
            var n = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = from.DistanceTo(to) }, MaterialOverride = m, Position = mid };
            root.AddChild(n);
            var dir = (to - from).Normalized();
            if (Mathf.Abs(dir.Dot(Vector3.Up)) < 0.999f) n.LookAt(n.GlobalPosition + dir, Vector3.Up, useModelFront: false);
            n.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);
        }
        // 1. The balance log, on two trestles over a ditch.
        Box(new Vector3(-2.5f, 0.02f, -10), new Vector3(8, 0.04f, 1.6f), new StandardMaterial3D { AlbedoColor = new Color("#3a2c20"), Roughness = 1 });
        Post(new Vector3(-6.5f, 0.75f, -10), new Vector3(2, 0.75f, -10), 0.18f, wood);
        foreach (var x in new[] { -6f, 1.5f }) { Post(new Vector3(x, 0, -10.5f), new Vector3(x, 0.8f, -10), 0.07f, wood); Post(new Vector3(x, 0, -9.5f), new Vector3(x, 0.8f, -10), 0.07f, wood); }
        // 2. The climbing net up to the platform, and 3. the rope swing from it, over a pit of water.
        Box(new Vector3(7, 3.0f, -15.5f), new Vector3(4, 0.2f, 4), wood, solid: true);
        foreach (var (x, z) in new[] { (5.2f, -13.7f), (8.8f, -13.7f), (5.2f, -17.3f), (8.8f, -17.3f) }) Post(new Vector3(x, 0, z), new Vector3(x, 3, z), 0.1f, wood);
        for (var i = 0; i < 6; i++) Post(new Vector3(5.4f + i * 0.65f, 0.1f, -10.6f), new Vector3(5.4f + i * 0.65f, 3, -13.6f), 0.03f, rope);
        for (var i = 0; i < 5; i++) Post(new Vector3(5.2f, 0.4f + i * 0.6f, -11 - i * 0.6f), new Vector3(8.8f, 0.4f + i * 0.6f, -11 - i * 0.6f), 0.03f, rope);
        Box(new Vector3(7, 0.03f, -21.5f), new Vector3(5, 0.06f, 7), water);
        Post(new Vector3(4.5f, 0, -21.5f), new Vector3(4.5f, 6, -21.5f), 0.12f, wood);
        Post(new Vector3(9.5f, 0, -21.5f), new Vector3(9.5f, 6, -21.5f), 0.12f, wood);
        Post(new Vector3(4.5f, 6, -21.5f), new Vector3(9.5f, 6, -21.5f), 0.12f, wood);
        Post(new Vector3(7, 6, -21.5f), new Vector3(7, 2.2f, -19.5f), 0.04f, rope);
        // 4. Stepping stones across a pond.
        Box(new Vector3(-4, 0.03f, -28), new Vector3(15, 0.06f, 4), water);
        foreach (var (x, z) in new[] { (0.5f, -28f), (-2.5f, -27.5f), (-5.5f, -28f), (-8.5f, -27.5f) })
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.7f, Height = 0.3f }, MaterialOverride = stone, Position = new Vector3(x, 0.15f, z) });
        // 5. The wall.
        Box(new Vector3(-13, 1.3f, -22.3f), new Vector3(4, 2.6f, 0.6f), stone, solid: true);
        // 6. A ladder up a little tower, and the slide down.
        Box(new Vector3(-13, 1.4f, -15.4f), new Vector3(2, 2.8f, 1.2f), wood, solid: true);
        Post(new Vector3(-13, 2.8f, -14.8f), new Vector3(-12.2f, 0.1f, -11), 0.35f, wood);
        // Numbered posts at each start, so the order's plain.
        for (var i = 0; i < CoursePaths.Length; i++)
        {
            var at = CoursePaths[i][0];
            root.AddChild(new Label3D { Text = $"{i + 1}. {Agility.Course[i].Name}", Position = at + Vector3.Up * 2.2f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, OutlineSize = 8, PixelSize = 0.01f, Modulate = new Color("#ffd27a"), VisibilityRangeEnd = 25 });
            Post(at + new Vector3(0.8f, 0, 0), at + new Vector3(0.8f, 1.6f, 0), 0.06f, wood);
        }
    }

    Vector3 CourseWorld(Vector3 local) => CourseAt with { Y = City.FloorAt(CourseAt) } + local;

    int? CourseObstacleAt(Vector3 pos)
    {
        for (var i = 0; i < CoursePaths.Length; i++)
        {
            var start = CourseWorld(CoursePaths[i][0]);
            if (new Vector2(start.X - pos.X, start.Z - pos.Z).Length() < 2.2f && Math.Abs(start.Y - pos.Y) < 1.5f) return i;
        }
        return null;
    }

    /// <summary>Takes an obstacle: the hero follows its path (or falls partway), and the lap goes on, or starts over.</summary>
    async Task Run(int i)
    {
        var o = Agility.Course[i];
        var level = _player.Pack.Level(Job.Agility); // anyone may try anything: the harder obstacles just throw the clumsy more often
        if (_working) return;
        _working = true;
        _player.Scripted = true;
        var path = CoursePaths[i].Select(CourseWorld).ToList();
        var falls = _lootRng.Chance(Agility.FallChance(o, level));
        var steps = falls ? path.Count / 2 + 1 : path.Count;
        _player.Face(path[^1]);
        Hands()?.Perform(i is 1 or 4 ? "Jump_Idle" : i == 3 ? "Jump_Start" : "Walking_A", 0.3, 1);
        for (var k = 1; k < steps; k++)
        {
            var tween = _player.CreateTween();
            tween.TweenProperty(_player, "global_position", path[k], 0.55).SetTrans(Tween.TransitionType.Sine);
            await ToSignal(tween, Tween.SignalName.Finished);
            if (!IsInstanceValid(this)) return;
            if (i == 3) Sound.At("move/land", _player.GlobalPosition, -10, 1.3f);
        }
        if (falls)
        {
            var ground = _player.GlobalPosition with { Y = City.FloorAt(_player.GlobalPosition) };
            var drop = _player.CreateTween();
            drop.TweenProperty(_player, "global_position", ground, 0.35).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await ToSignal(drop, Tween.SignalName.Finished);
            _player.Stats.TakeHit(_player.Stats.MaxHealth * 0.06, DamageKind.Physical, AttackKind.Weapon);
            Hands()?.Hurt();
            Sound.At("creature/hurt_hero", _player.GlobalPosition, -4);
            _player.Pack.Practise(Job.Agility, o.Xp / 3);
            _hud.Toast($"You fall off {o.Name}. The lap starts over.", 3);
            _lapNext = 0;
        }
        else
        {
            var up = _player.Pack.Practise(Job.Agility, o.Xp);
            var lap = i == _lapNext;
            _lapNext = lap ? i + 1 : 0;
            if (_lapNext == Agility.Course.Count)
            {
                _lapNext = 0;
                var lapUp = _player.Pack.Practise(Job.Agility, Agility.LapBonus);
                Sound.Ui("quest", -3);
                _hud.Toast($"A whole lap of the Tumblers' Run! (+{Agility.LapBonus} agility){((lapUp ?? up) is { } l ? $" Agility rises to {l}!" : "")}", 5);
                Did("laps");
                _player.Fit();
            }
            else if (up is { } l) { _hud.Toast($"Agility rises to {l}: you're quicker on your feet.", 4); _player.Fit(); }
        }
        _player.Scripted = false;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        _working = false;
        Save();
    }

    // ---- lifting purses ------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> PursuitTalk(Person person)
    {
        var npc = person.Npc;
        if (npc.Id == "tristinwold/factor" && !_pursuits.House.Owned)
            yield return ($"I'd like to buy land. (The plot at Hearthside, north of the walls: {Housing.PlotPrice} crowns)", () => BuyPlot(person));
        if (!People.IsYouth(npc) && !person.Captive && Guilds.Member(npc.Id) is not { Role: GuildRole.Leader })
            yield return ("(Pick their pocket)", () => PickPocket(person));
    }

    void PickPocket(Person person)
    {
        var npc = person.Npc;
        _hud.EndTalk();
        if (_pursuits.WaryUntil.GetValueOrDefault(npc.Id) > _guilds.PlaySeconds) { _hud.Toast($"{npc.Name} keeps a hand on their purse and an eye on you.", 3); return; }
        var mark = Thieving.MarkFor(npc.Occupation);
        var level = _player.Pack.Level(Job.Thieving);
        if (_lootRng.Chance(Thieving.Chance(mark, level, _player.Class.Calling, _player.Class.Id)))
        {
            var things = Thieving.Lift(mark, _lootRng, out var crowns);
            _player.Crowns += crowns;
            foreach (var (id, n) in things) _player.Pack.Add(id, n);
            var up = _player.Pack.Practise(Job.Thieving, Thieving.Xp(mark));
            Sound.Ui("coin", -6);
            _hud.Toast($"You lift {crowns} crowns{(things.Count > 0 ? $" and {Loot.Describe(things).ToLowerInvariant()}" : "")} from {npc.Name}.{(up is { } l ? $" Thieving rises to {l}!" : "")}", 4);
            Did("pockets");
        }
        else
        {
            var fine = Math.Min(_player.Crowns, Thieving.Fine(mark));
            _player.Crowns -= fine;
            _city.Renown = Math.Max(0, _city.Renown - 3);
            _pursuits.WaryUntil[npc.Id] = _guilds.PlaySeconds + Thieving.WarySeconds;
            _player.Pack.Practise(Job.Thieving, Thieving.Xp(mark) / 4);
            Sound.Ui("error");
            person.TalkTo(_player.GlobalPosition);
            _hud.Toast($"\"Thief! THIEF!\" {npc.Name} grabs your wrist. The Watch fines you {fine} crowns, and the Exchange hears of it.", 6);
            Did("caught");
        }
        Save();
    }

    // ---- snares --------------------------------------------------------------------------------------------

    void SetSnare()
    {
        var pos = _player.GlobalPosition;
        if (new Vector2(pos.X, pos.Z).Length() < 140) { Sound.Ui("error"); _hud.Toast("Out in the wilds: nothing worth catching runs in town.", 4); return; }
        var level = _player.Pack.Level(Job.Trapping);
        if (_pursuits.Snares.Count >= Trapping.MaxSnares(level)) { Sound.Ui("error"); _hud.Toast($"You can watch {Trapping.MaxSnares(level)} snares at once (more as you get better).", 4); return; }
        if (_player.Swimming) return;
        _player.Pack.Take("snare");
        var at = pos - _player.GlobalTransform.Basis.Z * 1.2f;
        _pursuits.Snares.Add(new Snare
        {
            Id = $"snare_{++_pursuits.NextSnare}", X = at.X, Z = at.Z, Biome = _world.BiomeAt(at.X, at.Z).ToString(),
            ReadyAt = _guilds.PlaySeconds + Trapping.Wait(level, _lootRng),
        });
        Hands()?.Perform("Use_Item", 0.6, 1.2f);
        _hud.Toast("You peg the snare down and cover it. Come back in a few minutes.", 4);
        Save();
    }

    Node3D SnareBody(Snare s)
    {
        var at = new Vector3((float)s.X, 0, (float)s.Z);
        var n = new Node3D { Position = at with { Y = City.FloorAt(at) } };
        _pursuitRoot.AddChild(n);
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#6a4a2a"), Roughness = 1 };
        n.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.5f }, MaterialOverride = wood, Position = Vector3.Up * 0.25f });
        n.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.19f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#a0a4a8"), Metallic = 0.6f }, Position = Vector3.Up * 0.05f });
        n.AddChild(new Label3D { Text = "Snare", Position = Vector3.Up * 0.9f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 6, PixelSize = 0.01f, VisibilityRangeEnd = 20 });
        return n;
    }

    void CheckSnare(Snare s)
    {
        if (s.ReadyAt > _guilds.PlaySeconds) { _hud.Toast($"Nothing yet. Give it about {Math.Ceiling((s.ReadyAt - _guilds.PlaySeconds) / 60)} more minutes.", 3); return; }
        _pursuits.Snares.Remove(s);
        var got = Trapping.Catch(s.Biome, _lootRng);
        foreach (var (id, n) in got) _player.Pack.Add(id, n);
        var kept = _lootRng.Chance(0.9);
        if (kept) _player.Pack.Add("snare");
        var up = _player.Pack.Practise(Job.Trapping, Trapping.Xp(got[0].Id));
        Sound.Ui("pickup");
        _hud.Toast($"In the snare: {Loot.Describe(got).ToLowerInvariant()}.{(kept ? "" : " The wire's snapped.")}{(up is { } l ? $" Trapping rises to {l}!" : "")}", 4);
        Did("snared");
        Save();
    }

    // ---- treasure ------------------------------------------------------------------------------------------

    void ReadMap()
    {
        if (!_player.Pack.Take("torn_map")) return;
        var pos = _player.GlobalPosition;
        _pursuits.Hunt = Treasure.NewHunt(_world, _lootRng, pos.X, pos.Z);
        if (_pursuits.Hunt.Current is not { } clue) { _pursuits.Hunt = null; _hud.Toast("The map's too torn to read.", 3); return; }
        _hud.Talk("A torn map", $"{clue.Text} You'll need a spade.{(_player.Pack.Count("spade") == 0 ? " (Hollis sells them.)" : "")}", ("Put it away", _hud.EndTalk));
        Save();
    }

    void Dig()
    {
        if (_pursuits.Hunt is not { Current: { } clue } hunt) return;
        if (_player.Pack.Count("spade") == 0) { Sound.Ui("error"); _hud.Toast("You need a spade.", 3); return; }
        var pos = _player.GlobalPosition;
        var off = new Vector2((float)clue.X - pos.X, (float)clue.Z - pos.Z).Length();
        Hands()?.Perform("2H_Melee_Attack_Chop", 0.7, 1.1f);
        Effects.Burst(this, pos - _player.GlobalTransform.Basis.Z + Vector3.Up * 0.2f, DamageKind.Physical, 0.9f);
        if (off > Treasure.Reach) { _hud.Toast(Treasure.Warmth(off), 3); return; }
        if (!hunt.Last)
        {
            hunt.Step++;
            _hud.Talk("Another scrap", $"Wrapped in oilcloth: another piece of the map. {hunt.Current!.Text}", ("Put it away", _hud.EndTalk));
            Sound.Ui("pickup");
            Save();
            return;
        }
        var (crowns, items) = Treasure.Reward(_lootRng, hunt.Clues.Count);
        _player.Crowns += crowns;
        foreach (var (id, n) in items) _player.Pack.Add(id, n);
        _pursuits.Hunt = null;
        Models.Place(this, "ph:chest.glb", pos - _player.GlobalTransform.Basis.Z * 1.2f, 0.6f, _player.Rotation.Y, solid: false);
        Sound.Ui("quest");
        _hud.Toast($"A chest! {crowns} crowns, and {Loot.Describe(items).ToLowerInvariant()}.", 7);
        Did("treasures");
        Save();
    }

    // ---- the house -----------------------------------------------------------------------------------------

    void BuyPlot(Person factor)
    {
        if (_player.Crowns < Housing.PlotPrice) { Sound.Ui("error"); Say(factor, $"{Housing.PlotPrice} crowns, and not a copper less. The Crown sets the price of land."); return; }
        _player.Crowns -= Housing.PlotPrice;
        _pursuits.House.Owned = true;
        _city.Treasury += Housing.PlotPrice;
        Share(CityAction.Treasury(Housing.PlotPrice));
        Sound.Ui("coin");
        Did("plots");
        Save();
        Say(factor, "Signed and sealed: the plot at Hearthside, north of the walls, is yours. You'll want stone for the foundation, planks and nails for the rest. Saw your planks on the plot; Hulda makes nails.");
    }

    void HouseMenu()
    {
        var h = _pursuits.House;
        if (!h.Owned)
        {
            _hud.Talk("Hearthside", $"A level plot north of the walls, staked out and roped off. A sign says it's for sale at the Exchange: {Housing.PlotPrice} crowns, to Factor Ottilie Marsh.", ("Leave it", _hud.EndTalk));
            return;
        }
        var pack = _player.Pack;
        var level = pack.Level(Job.Building);
        var options = new List<(string, Action)> { ("Saw planks, carve things (the sawhorse)", () => ShowCraft(Job.Building, HouseMenu)) };
        string Needs(BuildStep s) => string.Join(", ", s.Needs.Select(n => $"{Items.Count(n.Id, n.N)} ({pack.Count(n.Id)})"));
        if (h.NextStage is { } stage)
            options.Insert(0, ($"{stage.Name}: {Needs(stage)}{(stage.Level > level ? $" [building {stage.Level}]" : "")}", () => Build(stage)));
        else
        {
            foreach (var f in Housing.Furniture.Where(f => !h.Furniture.Contains(f.Id)))
                options.Add(($"Make {f.Name.ToLowerInvariant()}: {Needs(f)}{(f.Level > level ? $" [building {f.Level}]" : "")}. {f.Text}", () => Build(f)));
            if (h.Furniture.Contains("bed")) options.Add(("Sleep in your own bed", Sleep));
            if (h.Furniture.Contains("chest")) options.Add(($"Open the chest ({h.Chest.Values.Sum()} things in it)", Chest));
            if (h.Furniture.Contains("hearth")) options.Add(("Cook at the hearth", () => ShowCraft(Job.Cooking, HouseMenu)));
            if (h.Furniture.Contains("trophy_wall")) options.Add(($"The trophy wall ({h.Trophies.Count} hung)", TrophyWall));
        }
        options.Add(("Leave it", _hud.EndTalk));
        var built = h.Built == 0 ? "A staked-out plot, yours." : h.Finished ? $"Your house, finished.{(h.Furniture.Count > 0 ? $" In it: {string.Join(", ", h.Furniture.Select(f => Housing.Step(f).Name.ToLowerInvariant()))}." : " Bare as yet.")}"
            : $"Building: {string.Join(", ", Housing.Stages.Take(h.Built).Select(s => s.Name.ToLowerInvariant()))}, done.";
        _hud.Talk("Hearthside", $"{built} Building {level}.", [.. options]);
    }

    void Build(BuildStep s)
    {
        var pack = _player.Pack;
        if (pack.Level(Job.Building) < s.Level) { Sound.Ui("error"); _hud.Toast($"That takes building {s.Level}.", 3); return; }
        if (!pack.Has(s.Needs)) { Sound.Ui("error"); _hud.Toast($"You need {string.Join(", ", s.Needs.Select(n => Items.Count(n.Id, n.N)))}.", 4); return; }
        foreach (var (id, n) in s.Needs) pack.Take(id, n);
        var stage = Housing.Stages.Contains(s);
        if (stage) _pursuits.House.Built++;
        else _pursuits.House.Furniture.Add(s.Id);
        ShowHouse(); // it goes up as you build it
        var up = pack.Practise(Job.Building, s.Xp);
        Hands()?.Perform("2H_Melee_Attack_Chop", 0.7, 1.1f);
        Sound.Ui("anvil", -2);
        _hud.Toast($"{s.Name}: done. {s.Text}{(up is { } l ? $" Building rises to {l}!" : "")}", 5);
        if (stage && _pursuits.House.Finished) { Did("house"); _hud.Toast("Your house is finished. Furnish it as you like: a bed, a chest, a garden, a hearth, a trophy wall.", 8); }
        Save();
        HouseMenu();
    }

    void Sleep()
    {
        _pursuits.RestedUntil = _guilds.PlaySeconds + Housing.RestedSeconds;
        _player.Stats.Heal(1e9);
        _player.Stats.Restore(1e9, 1e9);
        Rest();
        _hud.EndTalk();
        _hud.Toast("You sleep in your own bed, and wake well rested: a tenth more experience for half an hour.", 6);
        Did("slept");
        Save();
    }

    void Chest()
    {
        var h = _pursuits.House;
        var options = new List<(string, Action)>();
        foreach (var (id, n) in h.Chest.OrderBy(kv => kv.Key).ToList())
            options.Add(($"Take out {Items.Count(id, n)}", () => { _player.Pack.Add(id, n); h.Chest.Remove(id); Save(); Chest(); }));
        foreach (var (item, n) in _player.Pack.Contents.Where(c => c.Item.Kind is not ItemKind.Tool).ToList())
            options.Add(($"Put in {Items.Count(item.Id, n)}", () => { _player.Pack.Take(item.Id, n); h.Chest[item.Id] = h.Chest.GetValueOrDefault(item.Id) + n; Save(); Chest(); }));
        options.Add(("Close the chest", HouseMenu));
        _hud.Talk("Your chest", h.Chest.Count == 0 ? "Empty." : string.Join(", ", h.Chest.Select(kv => Items.Count(kv.Key, kv.Value))), [.. options]);
    }

    void TrophyWall()
    {
        var h = _pursuits.House;
        var options = _player.Pack.Contents.Where(c => c.Item.Kind == ItemKind.Trophy && !h.Trophies.Contains(c.Item.Id))
            .Select(c => ($"Hang the {c.Item.Name}", (Action)(() =>
            {
                _player.Pack.Take(c.Item.Id);
                h.Trophies.Add(c.Item.Id);
                ApplyTrophies();
                Sound.Ui("equip");
                Save();
                TrophyWall();
            }))).ToList();
        options.Add(("Back", HouseMenu));
        _hud.Talk("The trophy wall", h.Trophies.Count == 0 ? "Bare. Hang the trophies of the lairs here; each makes your arm a little surer."
            : $"Hung: {string.Join(", ", h.Trophies.Select(t => Items.Get(t)?.Name ?? t))}. Your blows land {(Housing.TrophyBonus(h.Trophies.Count) - 1):P0} harder for them.", [.. options]);
    }

    void ApplyTrophies()
    {
        _player.Trophies = _pursuits.House.Furniture.Contains("trophy_wall") ? Housing.TrophyBonus(_pursuits.House.Trophies.Count) : 1;
        _player.Fit();
    }

    void GoHome()
    {
        if (!_pursuits.House.Owned) { _hud.Toast("You've no home to go to. Buy the plot at Hearthside from the Exchange.", 4); return; }
        if (_pursuits.CharmReadyAt > _guilds.PlaySeconds) { _hud.Toast($"The charm's still cold. {Math.Ceiling((_pursuits.CharmReadyAt - _guilds.PlaySeconds) / 60)} more minutes.", 3); return; }
        if (_tracker.InFight) { _hud.Toast("Not in the middle of a fight.", 3); return; }
        _pursuits.CharmReadyAt = _guilds.PlaySeconds + Housing.CharmCooldown;
        Effects.Rise(_player, DamageKind.Sun);
        var home = HousePlotAt + new Vector3(0, 0, 9);
        _terrain.EnsureAround(home, 2);
        _player.Dismount();
        _player.GlobalPosition = home with { Y = City.FloorAt(home) + 0.3f };
        _player.ResetPhysicsInterpolation();
        _hud.Toast("Home.", 3);
        Save();
    }

    string HouseKey => $"{_pursuits.House.Owned}:{_pursuits.House.Built}:{string.Join(",", _pursuits.House.Furniture.Order())}";

    /// <summary>The house as it stands: stakes on an empty plot, then the foundation, the frame, the walls, the roof, and the house.</summary>
    void ShowHouse()
    {
        _houseShown = HouseKey;
        _houseBody?.QueueFree();
        var h = _pursuits.House;
        var root = new Node3D { Position = HousePlotAt with { Y = City.FloorAt(HousePlotAt) } };
        _pursuitRoot.AddChild(root);
        _houseBody = root;
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#7a5a3a"), Roughness = 0.9f };
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#8f8a80"), Roughness = 1 };
        void Box(Vector3 at, Vector3 size, Material m) =>
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at });
        // The corner stakes and rope are there until the walls are.
        if (h.Built < 3)
        {
            foreach (var (x, z) in new[] { (-4f, -3.5f), (4f, -3.5f), (-4f, 3.5f), (4f, 3.5f) }) Box(new Vector3(x, 0.5f, z), new Vector3(0.1f, 1, 0.1f), wood);
            var rope = new StandardMaterial3D { AlbedoColor = new Color("#c8b48a") };
            Box(new Vector3(0, 0.9f, -3.5f), new Vector3(8, 0.03f, 0.03f), rope);
            Box(new Vector3(0, 0.9f, 3.5f), new Vector3(8, 0.03f, 0.03f), rope);
            Box(new Vector3(-4, 0.9f, 0), new Vector3(0.03f, 0.03f, 7), rope);
            Box(new Vector3(4, 0.9f, 0), new Vector3(0.03f, 0.03f, 7), rope);
        }
        if (h.Built >= 1 && h.Built < 4) Box(new Vector3(0, 0.2f, 0), new Vector3(7.6f, 0.4f, 6.6f), stone);
        if (h.Built >= 2 && h.Built < 4)
        {
            foreach (var (x, z) in new[] { (-3.6f, -3.1f), (3.6f, -3.1f), (-3.6f, 3.1f), (3.6f, 3.1f), (0f, -3.1f), (0f, 3.1f) }) Box(new Vector3(x, 2.4f, z), new Vector3(0.25f, 4, 0.25f), wood);
            Box(new Vector3(0, 4.3f, -3.1f), new Vector3(7.4f, 0.25f, 0.25f), wood);
            Box(new Vector3(0, 4.3f, 3.1f), new Vector3(7.4f, 0.25f, 0.25f), wood);
        }
        if (h.Built == 3)
        {
            Box(new Vector3(0, 2.2f, -3.1f), new Vector3(7.4f, 3.6f, 0.3f), stone);
            Box(new Vector3(-3.6f, 2.2f, 0), new Vector3(0.3f, 3.6f, 6.2f), stone);
            Box(new Vector3(3.6f, 2.2f, 0), new Vector3(0.3f, 3.6f, 6.2f), stone);
            Box(new Vector3(-2.2f, 2.2f, 3.1f), new Vector3(3, 3.6f, 0.3f), stone);
            Box(new Vector3(2.2f, 2.2f, 3.1f), new Vector3(3, 3.6f, 0.3f), stone);
        }
        if (h.Built >= 4) Buildings.Place(root, h.Finished ? "house" : "cottage", Vector3.Zero, 0, 11);
        // The yard, as it's furnished.
        if (h.Furniture.Contains("chest")) Models.Place(root, "ph:chest.glb", new Vector3(-3, 0, 6), 0.7f, 0.3f);
        if (h.Furniture.Contains("hearth")) Models.Place(root, "ph:fire_pit.glb", new Vector3(3.5f, 0, 6.5f), 1, 0);
        if (h.Furniture.Contains("trophy_wall")) Models.Place(root, "medieval/props/weaponrack.gltf", new Vector3(-5.5f, 0, 4), 5, 1.57f);
        if (h.Furniture.Contains("garden"))
            for (var i = 0; i < 2; i++)
            {
                var id = $"home_{i}";
                var at = HousePlotAt + new Vector3(6.5f, 0, -2 + i * 3.4f);
                if (!_beds.ContainsKey(id)) _beds[id] = (at, Bed(at), "");
            }
        ApplyTrophies();
    }

    // ---- for the self-test ---------------------------------------------------------------------------------

    public (string Verb, Action Act)? PursuitActionForTest(Vector3 pos) => PursuitAction(pos);
    public IEnumerable<string> PackPursuitsForTest() => PackPursuits().Select(o => o.Item1);
    public Task RunForTest(int i) => Run(i);
    public Vector3 CourseStart(int i) => CourseWorld(CoursePaths[i][0]);
    public Vector3 CourseEnd(int i) => CourseWorld(CoursePaths[i][^1]);
    public int LapNext => _lapNext;
    public void LightFireForTest() => LightFire();
    public int FiresLit => _fires.Count;
    public void PickPocketForTest(Person p) => PickPocket(p);
    public void SetSnareForTest() => SetSnare();
    public void CheckSnareForTest(Snare s) => CheckSnare(s);
    public void ReadMapForTest() => ReadMap();
    public void DigForTest() => Dig();
    public Vector3 BedAt(string id) => _beds[id].At;
    public int BedPlants(string id) => _beds[id].Body.GetChildren().Count(c => c.HasMeta("plant") && !c.IsQueuedForDeletion());
    public string HouseShown => _houseShown;
}
