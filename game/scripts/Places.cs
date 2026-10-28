using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;
using Noise = Tristinwold.World.Noise;

namespace Tristinwold.Game;

/// <summary>Someone you can talk to, dressed for their work.</summary>
public partial class Person : StaticBody3D
{
    public required Npc Npc { get; init; }
    public bool Captive { get; init; }

    /// <summary>How they look, if not as their trade suggests (a guild teacher in their class's kit).</summary>
    public string? Look { get; init; }
    public Body Figure { get; private set; } = null!;
    readonly Label3D _plate = new() { Position = new Vector3(0, 2.35f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 8, PixelSize = 0.006f, Modulate = new Color("#a8d8a0") };

    /// <summary>What someone of this trade wears and carries.</summary>
    public static string LookFor(Npc npc, bool captive = false)
    {
        if (captive) return "captive";
        if (npc.Ashborn) return "ashborn";
        if (People.IsYouth(npc)) return "youth";
        var job = npc.Occupation.ToLowerInvariant();
        bool Any(params string[] words) => words.Any(job.Contains);
        return Any("iron circle") ? "warmaster" : Any("velvet knives") ? "velvet" : Any("order of the eyrie") ? "aldric"
            : Any("guard", "sellsword", "lookout") ? "guard"
            : Any("priest", "shrine", "pilgrim", "midwife", "healer", "archivist") ? "priest"
            : Any("scholar", "alchemist", "scribe", "herbalist", "assayer") ? "scholar"
            : Any("hunter", "trapper", "tanner") ? "hunter"
            : Any("innkeeper", "brewer", "bard", "cook", "keeper of") ? "drinker"
            : Any("smith", "miner", "foreman", "woodcutter", "farm", "miller", "carpenter", "gravedigger", "dockhand", "herd", "labourer", "beekeeper", "fishwife") ? "worker"
            : Any("traveller", "courier", "merchant", "trader", "beggar") ? "traveller"
            : "townsfolk";
    }

    public override void _Ready()
    {
        Figure = Tristinwold.Game.Body.For(Look ?? LookFor(Npc, Captive));
        AddChild(Figure);
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f }, Position = new Vector3(0, 0.88f, 0) });
        AddChild(_plate);
        RotationDegrees = new Vector3(0, (float)(Noise.Unit((ulong)WorldGen.StableHash(Npc.Id)) * 360), 0);
        Mark(false);
    }

    /// <summary>A gold "!" over anyone with work to give.</summary>
    public void Mark(bool hasWork) => _plate.Text = $"{(hasWork ? "! " : "")}{Npc.Name}\n{Npc.Occupation}";

    /// <summary>Turns to face something (a deviant in the street, the hero they're patching up).</summary>
    public void Face(Vector3 at)
    {
        var to = at - GlobalPosition;
        to.Y = 0;
        if (to.LengthSquared() > 0.01f) LookAt(GlobalPosition + to, Vector3.Up);
    }

    /// <summary>Turns to face whoever is talking to them, and gestures.</summary>
    public void TalkTo(Vector3 who)
    {
        var to = who - GlobalPosition;
        to.Y = 0;
        if (to.LengthSquared() > 0.01f) LookAt(GlobalPosition + to, Vector3.Up);
        Figure.Talk();
    }
}

/// <summary>A quest item, in a chest where it was lost.</summary>
public partial class Pickup : Node3D
{
    public required string QuestId { get; init; }
    public required string Item { get; init; }

    /// <summary>A dragon egg of this colour in a nest of branches, rather than a chest.</summary>
    public string? Egg { get; init; }

    public override void _Ready()
    {
        if (Egg is { } shell)
        {
            var sticks = new StandardMaterial3D { AlbedoColor = new Color("#5a4630"), Roughness = 1 };
            for (var i = 0; i < 14; i++)
                AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 1.6f }, MaterialOverride = sticks,
                    Position = new Vector3(Mathf.Cos(i * 0.9f) * 0.75f, 0.2f, Mathf.Sin(i * 0.9f) * 0.75f),
                    RotationDegrees = new Vector3(80, i * 51, 0),
                });
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.32f, Height = 0.9f }, Position = new Vector3(0, 0.45f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(shell), Metallic = 0.6f, Roughness = 0.35f, RimEnabled = true, Rim = 0.5f },
            });
            AddChild(new OmniLight3D { Position = new Vector3(0, 1.2f, 0), LightColor = new Color(shell).Lerp(Colors.White, 0.5f), OmniRange = 5, LightEnergy = 1.2f });
            AddChild(new Label3D { Text = Item, Position = new Vector3(0, 1.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#ffd27a") });
            return;
        }
        if (Models.Scene("dungeon/chest_gold.glb")?.Instantiate() is Node3D chest)
        {
            chest.Scale = Vector3.One * 0.55f;
            AddChild(chest);
        }
        AddChild(new OmniLight3D { Position = new Vector3(0, 1.2f, 0), LightColor = new Color("#ffd27a"), OmniRange = 5, LightEnergy = 1.5f });
        AddChild(new Label3D { Text = Item, Position = new Vector3(0, 1.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#ffd27a") });
    }
}

/// <summary>
/// Builds the settlements near the player out of simple shapes, with their people and any
/// lair, and takes them down again once the player has gone. Tristinwold itself is built
/// once by <see cref="City"/>; here it only gets its people.
/// </summary>
public partial class Places(Main main, WorldGen world) : Node3D
{
    const double BuildRange = 420, DropRange = 650;

    sealed class Built
    {
        public required Settlement Site;
        public required Node3D Root;
        public readonly List<Person> People = [];
        public readonly List<Enemy> Enemies = [];
        public readonly List<Enemy> Animals = [];
        public readonly List<Enemy> Guards = [];
        public readonly Dictionary<string, Node3D> QuestThings = [];
    }

    readonly Dictionary<string, Built> _built = [];
    readonly HashSet<string> _cleared = []; // lairs emptied this session stay empty

    public IEnumerable<Person> People => _built.Values.SelectMany(b => b.People).Where(IsInstanceValid);

    /// <summary>A place's own guards, while it's built.</summary>
    public IEnumerable<Enemy> GuardsOf(string siteId) =>
        _built.TryGetValue(siteId, out var b) ? b.Guards.Where(g => IsInstanceValid(g) && g.State != EnemyState.Dead) : [];

    /// <summary>Whether someone has left their home (a youth who became a squire or apprentice).</summary>
    public Func<string, bool>? Gone { get; set; }
    public IEnumerable<Pickup> Pickups => _built.Values.SelectMany(b => b.QuestThings.Values).OfType<Pickup>().Where(IsInstanceValid);
    public bool IsBuilt(string id) => _built.ContainsKey(id);

    public void Update(Vector3 player)
    {
        foreach (var s in world.SettlementsNear(player.X, player.Z, BuildRange))
            if (!_built.ContainsKey(s.Id)) _built[s.Id] = Build(s);
        foreach (var b in _built.Values.Where(b => b.Site.DistanceTo(player.X, player.Z) > DropRange + b.Site.Radius).ToList())
            Drop(b);
        foreach (var b in _built.Values) Refresh(b);
    }

    /// <summary>A lair emptied this session fills again (a new job sends you back to it).</summary>
    public void Restock(string siteId)
    {
        if (_cleared.Remove(siteId) && _built.TryGetValue(siteId, out var b)) Drop(b);
    }

    public void KilledAt(string siteId)
    {
        if (_built.TryGetValue(siteId, out var b) && b.Enemies.All(e => !IsInstanceValid(e) || e.State == EnemyState.Dead)) _cleared.Add(siteId);
    }

    void Drop(Built b)
    {
        foreach (var e in b.Enemies.Concat(b.Animals).Concat(b.Guards).Where(IsInstanceValid)) e.QueueFree();
        b.Root.QueueFree();
        _built.Remove(b.Site.Id);
    }

    /// <summary>Quest items and captives come and go with the company's quests; "!" marks follow the work.</summary>
    void Refresh(Built b)
    {
        foreach (var p in b.People.Where(IsInstanceValid))
            if (!p.Captive) p.Mark(world.QuestOf(p.Npc) is { } q && main.Journal.Get(q.Id) is null);

        foreach (var entry in main.Journal.Open.Where(e => e.Quest.TargetId == b.Site.Id && e.Stage == QuestStage.Active))
        {
            var q = entry.Quest;
            if (b.QuestThings.ContainsKey(q.Id)) continue;
            var at = Ground(b.Site.X + 1.5, b.Site.Z + 1.5);
            if (q.Kind == QuestKind.Fetch)
                b.QuestThings[q.Id] = Add(b.Root, new Pickup { QuestId = q.Id, Item = q.Item!, Position = at });
            else if (q.Kind == QuestKind.Rescue)
            {
                var captive = new Npc(q.PersonId!, q.PersonName!, Sex.Other, 30, "captive", b.Site.Id, b.Site.Name,
                    $"{q.PersonName} is a long way from home.", ["going home"], "nervous", "Please! Get me out of here!", b.Site.X, b.Site.Z);
                var person = Add(b.Root, new Person { Npc = captive, Captive = true, Position = at });
                b.People.Add(person);
                b.QuestThings[q.Id] = person;
            }
        }
        foreach (var (id, thing) in b.QuestThings.ToList())
            if (main.Journal.Get(id) is not { Stage: QuestStage.Active })
            {
                if (IsInstanceValid(thing)) thing.QueueFree();
                b.QuestThings.Remove(id);
            }
    }

    Built Build(Settlement s)
    {
        _lots.Clear(); // plots are counted per settlement
        var root = new Node3D { Name = s.Name.Replace(" ", "_") + "_" + Math.Abs(WorldGen.StableHash(s.Id) % 10000) };
        AddChild(root);
        var b = new Built { Site = s, Root = root };
        var rng = Noise.Dice(world.Seed, 8, WorldGen.StableHash(s.Id));
        if (s.Id != "city:tristinwold")
        {
            var colour = Models.Colours[Math.Abs(WorldGen.StableHash(s.Id) % Models.Colours.Length)];
            switch (s.Kind)
            {
                case SettlementKind.City or SettlementKind.Town: Town(root, s, rng, colour); break;
                case SettlementKind.Hamlet: Hamlet(root, s, rng, colour); break;
                case SettlementKind.Farmstead: Farm(root, s, rng, colour); break;
                case SettlementKind.HuntingCamp: Camp(root, s, rng, 2, null); Prop(root, s, "medieval/props/weaponrack.gltf", 4, -3, 5); Prop(root, s, "medieval/props/target.gltf", -5, 4, 5); break;
                case SettlementKind.Inn: Inn(root, s, rng, colour); break;
                case SettlementKind.Shrine: Shrine(root, s, rng); break;
                case SettlementKind.Mine: Mine(root, s, rng, colour); break;
                case SettlementKind.AshbornCamp: Camp(root, s, rng, 4, new Color("#b05040")); break;
                case SettlementKind.BanditCamp: Camp(root, s, rng, 3, new Color("#a08060")); Palisade(root, s); Prop(root, s, "medieval/props/flag_red.gltf", 0, -6, 6); break;
                case SettlementKind.Barrow: Barrow(root, s, rng); break;
                case SettlementKind.Den: Den(root, s, rng); break;
            }
            Label(root, Ground(s.X, s.Z) + new Vector3(0, s.IsCity ? 40 : 12, 0), s.Name, s.IsCity ? 0.08f : 0.04f);
        }

        foreach (var npc in world.PeopleOf(s))
            b.People.Add(Add(root, new Person { Npc = npc, Position = Ground(npc.X, npc.Z) }));
        // The young, unless one has already left home to follow the company.
        foreach (var npc in world.YouthsOf(s).Where(y => Gone?.Invoke(y.Id) != true))
            b.People.Add(Add(root, new Person { Npc = npc, Position = Ground(npc.X, npc.Z) }));

        // The animals people keep, near the people who keep them (in the open, where people stand).
        foreach (var (kind, count) in Livestock.Of(s, rng))
            for (var i = 0; i < count; i++)
            {
                var by = b.People.Count > 0 ? b.People[rng.Next(b.People.Count)].Npc : null;
                var (ax, az) = by is null ? (s.X, s.Z) : (by.X, by.Z);
                var a = rng.NextDouble() * Math.Tau;
                var r = 2.5 + rng.NextDouble() * 4;
                var e = Enemy.Create(Bestiary.Get(kind), Ground(ax + Math.Cos(a) * r, az + Math.Sin(a) * r) + new Vector3(0, 0.5f, 0));
                e.Owner = s.Name;
                e.Roam = kind is "hen" or "cat" ? 4 : 6;
                main.AddEnemy(e);
                b.Animals.Add(e);
            }

        // A couple of guards, by how big a place it is: no Watch out here.
        if (s.Id != "city:tristinwold")
            for (var i = 0; i < Warband.Guards(s); i++)
            {
                var a = i * Math.Tau / Math.Max(1, Warband.Guards(s)) + rng.NextDouble();
                var g = Enemy.Create(Bestiary.Get("watchman"), Ground(s.X + Math.Cos(a) * s.Radius * 0.35, s.Z + Math.Sin(a) * s.Radius * 0.35) + new Vector3(0, 0.5f, 0), look: "guard");
                g.Allied = true;
                g.Watch = true;
                g.Roam = 8;
                var keep = s;
                g.Keeps = p => keep.DistanceTo(p.X, p.Z) < keep.Radius + 40;
                g.Stats.Name = $"Guard of {s.Name}";
                main.AddEnemy(g);
                g.RefreshPlate();
                b.Guards.Add(g);
            }

        if (s.Lair is { } lair && !_cleared.Contains(s.Id))
        {
            var danger = WorldGen.Danger(s.X, s.Z);
            for (var i = 0; i < lair.Count; i++)
            {
                var a = i * Mathf.Tau / lair.Count + rng.NextDouble();
                b.Enemies.Add(Spawn(lair.KindId, s.X + Math.Cos(a) * s.Radius * 0.45, s.Z + Math.Sin(a) * s.Radius * 0.45, danger, s.Id));
            }
            if (lair.BossKindId is { } boss) b.Enemies.Add(Spawn(boss, s.X, s.Z, danger, s.Id));
        }
        return b;
    }

    Enemy Spawn(string kind, double x, double z, double danger, string siteId)
    {
        var e = Enemy.Create(Bestiary.Get(kind), Ground(x, z) + new Vector3(0, 0.5f, 0), danger, siteId);
        main.AddEnemy(e);
        return e;
    }

    // ---- building ----------------------------------------------------------------------------

    Vector3 Ground(double x, double z) => new((float)x, (float)world.Height(x, z), (float)z);

    static T Add<T>(Node3D parent, T node) where T : Node3D
    {
        parent.AddChild(node);
        return node;
    }

    static void Label(Node3D parent, Vector3 pos, string text, float pixel = 0.03f) => parent.AddChild(new Label3D
    {
        Text = text, Position = pos, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12,
        Modulate = new Color("#e8dcc4"), PixelSize = pixel,
    });

    /// <summary>A model at an offset (metres) from the settlement's centre, standing on the ground.</summary>
    Node3D Prop(Node3D parent, Settlement s, string path, double dx, double dz, float scale, float yaw = 0, bool solid = true) =>
        Models.Place(parent, path, Ground(s.X + dx, s.Z + dz), scale, yaw, solid);

    readonly List<(Vector2 At, float Room)> _lots = [];

    /// <summary>
    /// A generated building at an offset from the settlement's centre, front door towards the centre.
    /// Returns null (and builds nothing) if another building already stands there.
    /// </summary>
    Node3D? Building(Node3D parent, Settlement s, string kind, string colour, double dx, double dz, float yaw, float scale = 7.5f, string? name = null)
    {
        kind = Buildings.KindFor(kind);
        var at = new Vector2((float)(s.X + dx), (float)(s.Z + dz));
        var room = kind switch { "keep" => 16f, "church" => 12f, "longhall" or "tavern" or "barn" => 8f, "well" => 2.5f, _ => 6f };
        if (_lots.Any(l => l.At.DistanceTo(at) < l.Room + room)) return null;
        _lots.Add((at, room));
        var facing = dx * dx + dz * dz > 1 ? Mathf.Atan2(-(float)dx, -(float)dz) : yaw;
        var size = kind == "keep" ? (s.Kind == SettlementKind.City ? 1f : 0.82f) : 1f;
        var b = Buildings.Place(parent, kind, Ground(s.X + dx, s.Z + dz), facing, (int)(WorldGen.StableHash(s.Id) % 1000) + _lots.Count, Buildings.RoofFor(colour), size);
        if (name is not null) Label(parent, b.Position + new Vector3(0, Buildings.HeightOf(b) + 2.5f, 0), name, 0.02f);
        return b;
    }

    /// <summary>Buildings in a ring around the centre, facing in.</summary>
    void Ring(Node3D parent, Settlement s, Rng rng, string colour, int count, double from, double to, string[] kinds, float scale = 7f)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (i + rng.NextDouble() * 0.4) / count * Mathf.Tau;
            var r = s.Radius * (from + rng.NextDouble() * (to - from));
            Building(parent, s, rng.Pick(kinds), colour, Math.Cos(a) * r, Math.Sin(a) * r, -(float)a + Mathf.Pi / 2, scale * (0.9f + (float)rng.NextDouble() * 0.2f));
        }
    }

    void Clutter(Node3D parent, Settlement s, Rng rng, int count, double spread)
    {
        string[] props = ["ph:barrel.glb", "ph:barrel.glb", "ph:crate.glb", "ph:crate.glb", "medieval/props/sack.gltf", "medieval/props/wheelbarrow.gltf", "dungeon/keg.glb"];
        for (var i = 0; i < count; i++)
        {
            var path = rng.Pick(props);
            var scale = path.StartsWith("ph:") ? 1f : path.StartsWith("dungeon") ? 0.5f : 5f;
            var a = rng.NextDouble() * Mathf.Tau;
            var r = s.Radius * (0.15 + rng.NextDouble() * spread);
            Prop(parent, s, path, Math.Cos(a) * r, Math.Sin(a) * r, scale, (float)rng.NextDouble() * Mathf.Tau);
        }
    }

    void Lanterns(Node3D parent, Settlement s, Rng rng, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (i + 0.5) / count * Mathf.Tau;
            Buildings.Place(parent, "lantern", Ground(s.X + Math.Cos(a) * s.Radius * 0.35, s.Z + Math.Sin(a) * s.Radius * 0.35), (float)a, i);
        }
    }

    void Town(Node3D parent, Settlement s, Rng rng, string colour)
    {
        // A ring of stone wall with a gatehouse on each side.
        var r = (float)s.Radius;
        var wall = s.Kind == SettlementKind.City ? "wall" : "wall_low";
        var length = (wall == "wall" ? 10 : 8) * 0.96f;
        var count = (int)Math.Ceiling(Mathf.Tau * r / length);
        var gap = 7.5f / r; // half the gatehouse's width, as an angle
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            if (Enumerable.Range(0, 4).Any(g => Math.Abs(Mathf.Wrap(a - g * Mathf.Pi / 2, -Mathf.Pi, Mathf.Pi)) < gap)) continue;
            Buildings.Place(parent, wall, Ground(s.X + Math.Cos(a) * r, s.Z + Math.Sin(a) * r), -a - Mathf.Pi / 2, 0);
        }
        for (var g = 0; g < 4; g++)
        {
            var a = g * Mathf.Pi / 2;
            Buildings.Place(parent, "gate", Ground(s.X + Math.Cos(a) * r, s.Z + Math.Sin(a) * r), Mathf.Atan2(Mathf.Cos(a), Mathf.Sin(a)), 0);
        }
        // The seat of power in the middle, the named places around the square, and streets of houses.
        if (s.Places.Count > 0) Building(parent, s, "castle", colour, 0, 0, (float)rng.NextDouble() * Mathf.Tau, s.Kind == SettlementKind.City ? 6.5f : 5f, s.Places[0]);
        for (var i = 1; i < s.Places.Count; i++)
        {
            var a = i * Mathf.Tau / s.Places.Count + 0.4;
            var kind = Models.BuildingFor(s.Places[i]);
            Building(parent, s, kind == "castle" ? "tower_B" : kind, colour, Math.Cos(a) * r * 0.34, Math.Sin(a) * r * 0.34, -(float)a + Mathf.Pi / 2, Models.BuildingScale(kind) * 0.9f, s.Places[i]);
        }
        var houses = (int)(r * 0.32f);
        for (var placed = 0; placed < houses;)
        {
            var a = rng.NextDouble() * Mathf.Tau;
            if (Enumerable.Range(0, 4).Any(g => Math.Abs(Mathf.Wrap((float)a - g * Mathf.Pi / 2, -Mathf.Pi, Mathf.Pi)) < 0.22f)) continue; // keep the gate roads clear
            var d = r * (0.56 + rng.NextDouble() * 0.3);
            Building(parent, s, rng.Pick(["house", "house", "house", "cottage", "smithy", "barn", "tavern"]), colour, Math.Cos(a) * d, Math.Sin(a) * d, -(float)a + Mathf.Pi / 2, 6.5f);
            placed++;
        }
        for (var g = 0; g < 4; g++)
            Prop(parent, s, $"medieval/props/flag_{colour}.gltf", Math.Cos(g * Mathf.Pi / 2 + 0.12) * (r - 4), Math.Sin(g * Mathf.Pi / 2 + 0.12) * (r - 4), 8, solid: false);
        Clutter(parent, s, rng, 10, 0.25);
        Lanterns(parent, s, rng, 6);
    }

    void Hamlet(Node3D parent, Settlement s, Rng rng, string colour)
    {
        Ring(parent, s, rng, colour, 4 + rng.Next(4), 0.6, 0.9, ["cottage", "cottage", "house", "barn"]);
        Building(parent, s, "well", colour, 0, 0, 0, 5);
        Clutter(parent, s, rng, 5, 0.3);
        Lanterns(parent, s, rng, 3);
    }

    void Farm(Node3D parent, Settlement s, Rng rng, string colour)
    {
        Building(parent, s, "cottage", colour, 0, 0, 0.2f, 7);
        Building(parent, s, "windmill", colour, -12, 4, 0.9f, 7);
        Building(parent, s, "barn", colour, 6, -11, 0.4f, 7);
        Prop(parent, s, "medieval/neutral/building_grain.gltf", 11, -7, 7, 0.3f, solid: false);
        for (var i = 0; i < 6; i++)
            Prop(parent, s, "medieval/neutral/fence_wood_straight.gltf", 4 + i * 3.6, 2, 1.8f, 0);
        Clutter(parent, s, rng, 3, 0.4);
    }

    void Inn(Node3D parent, Settlement s, Rng rng, string colour)
    {
        Building(parent, s, "tavern", colour, 0, 0, 0.3f, 9, s.Name);
        Building(parent, s, "barn", colour, 14, 3, 1.2f, 5.5f); // the stable
        Prop(parent, s, "dungeon/table_long_decorated_A.glb", -4, 8, 0.6f, 0.3f);
        Prop(parent, s, "dungeon/keg.glb", -8, 6, 0.6f);
        Clutter(parent, s, rng, 4, 0.5);
        Lanterns(parent, s, rng, 2);
    }

    void Shrine(Node3D parent, Settlement s, Rng rng)
    {
        Prop(parent, s, "halloween/shrine_candles.gltf", 0, 0, 2);
        for (var i = 0; i < 7; i++)
        {
            var a = i * Mathf.Tau / 7;
            Prop(parent, s, "halloween/pillar.gltf", Math.Cos(a) * s.Radius * 0.6, Math.Sin(a) * s.Radius * 0.6, 1.6f, -(float)a);
        }
        Prop(parent, s, "halloween/candle_triple.gltf", 1.6, 1.2, 1.5f, solid: false);
        Prop(parent, s, "halloween/lantern_standing.gltf", -2, -1.5, 1.8f, solid: false);
    }

    void Mine(Node3D parent, Settlement s, Rng rng, string colour)
    {
        Building(parent, s, "mine", colour, 0, -s.Radius * 0.1, 0, 9);
        Building(parent, s, "cottage", colour, s.Radius * 0.6, s.Radius * 0.4, -0.6f, 6.5f);
        Prop(parent, s, "medieval/props/resource_stone.gltf", 4, -6, 6);
        Prop(parent, s, "medieval/props/wheelbarrow.gltf", 2, 5, 5, 0.8f);
        Clutter(parent, s, rng, 3, 0.4);
    }

    void Camp(Node3D parent, Settlement s, Rng rng, int tents, Color? tint)
    {
        for (var i = 0; i < tents; i++)
        {
            var a = i * Mathf.Tau / tents + rng.NextDouble() * 0.5;
            var tent = Prop(parent, s, "medieval/props/tent.gltf", Math.Cos(a) * s.Radius * 0.55, Math.Sin(a) * s.Radius * 0.55, 7, -(float)a - Mathf.Pi / 2);
            if (tint is { } t) Models.Tint(tent, t);
        }
        Fire(parent, s);
        Prop(parent, s, "medieval/props/resource_lumber.gltf", 3, 2.5, 4, 0.5f);
        Clutter(parent, s, rng, 2, 0.4);
    }

    void Fire(Node3D parent, Settlement s)
    {
        var at = Ground(s.X, s.Z);
        Models.Place(parent, "ph:fire_pit.glb", at, 1, solid: false);
        parent.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.5f, Height = 0.25f }, Position = at + new Vector3(0, 0.12f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#3a2a1a"), EmissionEnabled = true, Emission = new Color("#ff6a1a"), EmissionEnergyMultiplier = 2 } });
        parent.AddChild(new OmniLight3D { Position = at + new Vector3(0, 1.5f, 0), LightColor = new Color("#ff9a4a"), LightEnergy = 3f, OmniRange = 16, ShadowEnabled = false });
        foreach (var a in new[] { 0.3f, 2.4f, 4.4f })
            Models.Place(parent, "dungeon/trunk_medium_A.glb", at + new Vector3(Mathf.Cos(a) * 2.2f, 0, Mathf.Sin(a) * 2.2f), 0.6f, a, solid: false);
    }

    void Palisade(Node3D parent, Settlement s)
    {
        var count = (int)(Mathf.Tau * s.Radius / 4.4);
        for (var i = 0; i < count; i++)
        {
            if (i < 2) continue; // the way in
            var a = i * Mathf.Tau / count;
            Prop(parent, s, "medieval/neutral/fence_wood_straight.gltf", Math.Cos(a) * s.Radius, Math.Sin(a) * s.Radius, 2.3f, -(float)a - Mathf.Pi / 2);
        }
        Prop(parent, s, "dungeon/torch_lit.glb", 2, s.Radius - 1, 1.2f, solid: false);
    }

    void Barrow(Node3D parent, Settlement s, Rng rng)
    {
        Prop(parent, s, "halloween/crypt.gltf", 0, -s.Radius * 0.45, 1.4f);
        string[] graves = ["halloween/grave_A.gltf", "halloween/grave_B.gltf", "halloween/gravestone.gltf", "halloween/gravemarker_A.gltf", "halloween/gravemarker_B.gltf", "halloween/grave_A_destroyed.gltf"];
        for (var i = 0; i < 9; i++)
            Prop(parent, s, rng.Pick(graves), (rng.NextDouble() - 0.5) * s.Radius * 1.4, (rng.NextDouble() * 0.6) * s.Radius, 1.2f, (float)(rng.NextDouble() - 0.5) * 0.6f);
        Prop(parent, s, "halloween/tree_dead_large.gltf", -s.Radius * 0.7, -s.Radius * 0.2, 1.3f, (float)rng.NextDouble() * 3);
        Prop(parent, s, "halloween/tree_dead_medium.gltf", s.Radius * 0.7, s.Radius * 0.1, 1.3f, (float)rng.NextDouble() * 3);
        Prop(parent, s, "halloween/post_skull.gltf", 2, s.Radius * 0.75, 2, solid: false);
        Prop(parent, s, "halloween/lantern_standing.gltf", -2, s.Radius * 0.75, 2, solid: false);
        for (var i = 0; i < 4; i++)
            Prop(parent, s, rng.Pick(["halloween/bone_A.gltf", "halloween/bone_B.gltf", "halloween/skull.gltf", "halloween/ribcage.gltf"]), (rng.NextDouble() - 0.5) * 8, (rng.NextDouble() - 0.5) * 8, 1.2f, (float)rng.NextDouble() * 6, solid: false);
    }

    void Den(Node3D parent, Settlement s, Rng rng)
    {
        Prop(parent, s, "medieval/nature/mountain_C.gltf", 0, -s.Radius * 0.55, 9, (float)rng.NextDouble() * 6);
        for (var i = 0; i < 7; i++)
        {
            var a = rng.NextDouble() * Mathf.Tau;
            Prop(parent, s, $"medieval/nature/rock_single_{"ABCDE"[rng.Next(5)]}.gltf", Math.Cos(a) * s.Radius * 0.7, Math.Sin(a) * s.Radius * 0.7, 10 + (float)rng.NextDouble() * 8, (float)rng.NextDouble() * 6);
        }
        for (var i = 0; i < 5; i++)
            Prop(parent, s, rng.Pick(["halloween/bone_A.gltf", "halloween/bone_C.gltf", "halloween/skull.gltf", "halloween/ribcage.gltf"]), (rng.NextDouble() - 0.5) * 6, (rng.NextDouble() - 0.5) * 6, 1.2f, (float)rng.NextDouble() * 6, solid: false);
    }
}
