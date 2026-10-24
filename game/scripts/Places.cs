using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;
using Noise = Tristinwold.World.Noise;

namespace Tristinwold.Game;

/// <summary>Someone you can talk to.</summary>
public partial class Person : StaticBody3D
{
    public required Npc Npc { get; init; }
    public bool Captive { get; init; }
    readonly Label3D _plate = new() { Position = new Vector3(0, 2.4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, OutlineSize = 8, PixelSize = 0.006f, Modulate = new Color("#a8d8a0") };

    public override void _Ready()
    {
        var colour = Captive ? new Color("#b8b0a0") : Npc.Ashborn ? new Color("#7a2a2a") : Color.FromHsv((float)Noise.Unit((ulong)WorldGen.StableHash(Npc.Id)) * 0.15f + 0.05f, 0.45f, 0.55f);
        AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.38f, Height = 1.75f }, Position = new Vector3(0, 0.88f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = colour } });
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f }, Position = new Vector3(0, 0.88f, 0) });
        AddChild(_plate);
        Mark(false);
    }

    /// <summary>A gold "!" over anyone with work to give.</summary>
    public void Mark(bool hasWork) => _plate.Text = $"{(hasWork ? "! " : "")}{Npc.Name}\n{Npc.Occupation}";
}

/// <summary>A quest item lying where it was lost.</summary>
public partial class Pickup : Node3D
{
    public required string QuestId { get; init; }
    public required string Item { get; init; }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.4f) }, Position = new Vector3(0, 0.2f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#d8b25a"), EmissionEnabled = true, Emission = new Color("#6a4a10") },
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 1, 0), LightColor = new Color("#ffd27a"), OmniRange = 4 });
        AddChild(new Label3D { Text = Item, Position = new Vector3(0, 1.2f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, OutlineSize = 8, PixelSize = 0.01f, Modulate = new Color("#ffd27a") });
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
        public readonly Dictionary<string, Node3D> QuestThings = [];
    }

    readonly Dictionary<string, Built> _built = [];
    readonly HashSet<string> _cleared = []; // lairs emptied this session stay empty

    public IEnumerable<Person> People => _built.Values.SelectMany(b => b.People).Where(IsInstanceValid);
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

    public void KilledAt(string siteId)
    {
        if (_built.TryGetValue(siteId, out var b) && b.Enemies.All(e => !IsInstanceValid(e) || e.State == EnemyState.Dead)) _cleared.Add(siteId);
    }

    void Drop(Built b)
    {
        foreach (var e in b.Enemies.Where(IsInstanceValid)) e.QueueFree();
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
        var root = new Node3D { Name = s.Name.Replace(" ", "_") + "_" + Math.Abs(WorldGen.StableHash(s.Id) % 10000) };
        AddChild(root);
        var b = new Built { Site = s, Root = root };
        var rng = Noise.Dice(world.Seed, 8, WorldGen.StableHash(s.Id));
        if (s.Id != "city:tristinwold")
        {
            switch (s.Kind)
            {
                case SettlementKind.City or SettlementKind.Town: Town(root, s, rng); break;
                case SettlementKind.Hamlet: Houses(root, s, rng, 4 + rng.Next(4), "#5b4a3a"); Well(root, s); break;
                case SettlementKind.Farmstead: Farm(root, s, rng); break;
                case SettlementKind.HuntingCamp: Tents(root, s, rng, 2, "#7a6a50"); Fire(root, s); break;
                case SettlementKind.Inn: House(root, s.X, s.Z, 14, 7, 10, 0.3f, "#6e5235", s.Name); House(root, s.X + 10, s.Z + 4, 6, 4, 8, 0.3f, "#5a4630"); break;
                case SettlementKind.Shrine: Stones(root, s, rng, 7, 3.5f); Box(root, Ground(s.X, s.Z) + new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1, 1), "#8a8a96"); break;
                case SettlementKind.Mine: Mine(root, s); Houses(root, s, rng, 2, "#4f4438"); break;
                case SettlementKind.AshbornCamp: Tents(root, s, rng, 4, "#5a2020"); Fire(root, s); break;
                case SettlementKind.BanditCamp: Tents(root, s, rng, 3, "#4a3a2a"); Fire(root, s); Palisade(root, s); break;
                case SettlementKind.Barrow: Barrow(root, s, rng); break;
                case SettlementKind.Den: Rocks(root, s, rng); break;
            }
            Label(root, Ground(s.X, s.Z) + new Vector3(0, s.IsCity ? 40 : 12, 0), s.Name, s.IsCity ? 0.08f : 0.04f);
        }

        foreach (var npc in world.PeopleOf(s))
            b.People.Add(Add(root, new Person { Npc = npc, Position = Ground(npc.X, npc.Z) }));

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

    // ---- shapes ------------------------------------------------------------------------------

    Vector3 Ground(double x, double z) => new((float)x, (float)world.Height(x, z), (float)z);

    static T Add<T>(Node3D parent, T node) where T : Node3D
    {
        parent.AddChild(node);
        return node;
    }

    static StandardMaterial3D Mat(string colour) => new() { AlbedoColor = new Color(colour), Roughness = 0.9f };

    static StaticBody3D Box(Node3D parent, Vector3 pos, Vector3 size, string colour, float yaw = 0)
    {
        var body = new StaticBody3D { Position = pos, Rotation = new Vector3(0, yaw, 0) };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Mat(colour) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }

    static void Label(Node3D parent, Vector3 pos, string text, float pixel = 0.03f) => parent.AddChild(new Label3D
    {
        Text = text, Position = pos, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12,
        Modulate = new Color("#e8dcc4"), PixelSize = pixel,
    });

    void House(Node3D parent, double x, double z, float w, float h, float d, float yaw, string colour, string? name = null)
    {
        var at = Ground(x, z);
        var house = Box(parent, at + new Vector3(0, h / 2 - 0.3f, 0), new Vector3(w, h + 0.6f, d), colour, yaw);
        house.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(w + 0.8f, h * 0.45f, d + 0.8f) }, Position = new Vector3(0, (h + 0.6f) / 2 + h * 0.225f, 0),
            MaterialOverride = Mat("#3a2a20"),
        });
        if (name is not null) Label(parent, at + new Vector3(0, h * 1.6f + 1.5f, 0), name, 0.02f);
    }

    void Houses(Node3D parent, Settlement s, Rng rng, int count, string colour)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (i + rng.NextDouble() * 0.5) / count * Mathf.Tau;
            var r = s.Radius * (0.55 + rng.NextDouble() * 0.35);
            var size = 4 + (float)rng.NextDouble() * 3;
            House(parent, s.X + Math.Cos(a) * r, s.Z + Math.Sin(a) * r, size, size * 0.7f, size * 0.9f, -(float)a, colour);
        }
    }

    void Town(Node3D parent, Settlement s, Rng rng)
    {
        // A ring wall with four gates, streets of houses, and a building for each named place.
        var floor = (float)s.Floor;
        var r = (float)s.Radius;
        const int segments = 48;
        for (var i = 0; i < segments; i++)
        {
            var a = i * Mathf.Tau / segments;
            if (Enumerable.Range(0, 4).Any(g => Math.Abs(Mathf.Wrap(a - g * Mathf.Pi / 2, -Mathf.Pi, Mathf.Pi)) < 0.12f)) continue;
            Box(parent, new Vector3((float)s.X + Mathf.Cos(a) * r, floor + 3, (float)s.Z + Mathf.Sin(a) * r), new Vector3(1.6f, 7, Mathf.Tau * r / segments + 0.4f), "#6d6558", -a);
        }
        var placed = 0;
        for (var tries = 0; placed < (int)(r * 0.45f) && tries < 400; tries++)
        {
            var a = rng.NextDouble() * Mathf.Tau;
            if (Enumerable.Range(0, 4).Any(g => Math.Abs(Mathf.Wrap((float)a - g * Mathf.Pi / 2, -Mathf.Pi, Mathf.Pi)) < 0.25f)) continue;
            var d = r * (0.35 + rng.NextDouble() * 0.55);
            var size = 4 + (float)rng.NextDouble() * 4;
            House(parent, s.X + Math.Cos(a) * d, s.Z + Math.Sin(a) * d, size, size * 0.8f, size * 0.9f, -(float)a, "#6a5a48");
            placed++;
        }
        // The named places in a ring around the square; the first is the seat of power, in the middle.
        for (var i = 0; i < s.Places.Count; i++)
        {
            if (i == 0) { House(parent, s.X, s.Z, 14, 14, 14, 0, "#8a8274", s.Places[0]); continue; }
            var a = i * Mathf.Tau / s.Places.Count + 0.4;
            House(parent, s.X + Math.Cos(a) * r * 0.3, s.Z + Math.Sin(a) * r * 0.3, 9, 6, 8, -(float)a, "#7a6650", s.Places[i]);
        }
    }

    void Well(Node3D parent, Settlement s)
    {
        var body = new StaticBody3D { Position = Ground(s.X, s.Z) + new Vector3(0, 0.5f, 0) };
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1 }, MaterialOverride = Mat("#7a7468") });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1, Height = 1 } });
        parent.AddChild(body);
    }

    void Farm(Node3D parent, Settlement s, Rng rng)
    {
        House(parent, s.X, s.Z, 6, 4, 6, 0.2f, "#5b4a3a");
        House(parent, s.X - 9, s.Z + 3, 8, 5, 6, 0.2f, "#7a3a2a");
        var field = Ground(s.X + 10, s.Z - 6);
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(14, 0.2f, 10) }, Position = field, MaterialOverride = Mat(rng.Chance(0.5) ? "#8a7a3a" : "#5a6a2a") });
    }

    void Tents(Node3D parent, Settlement s, Rng rng, int count, string colour)
    {
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count + rng.NextDouble() * 0.5;
            var at = Ground(s.X + Math.Cos(a) * s.Radius * 0.55, s.Z + Math.Sin(a) * s.Radius * 0.55);
            var tent = new StaticBody3D { Position = at + new Vector3(0, 1.1f, 0), Rotation = new Vector3(0, -(float)a, 0) };
            tent.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(3, 2.2f, 4) }, MaterialOverride = Mat(colour) });
            tent.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.4f, 2.2f, 4) } });
            parent.AddChild(tent);
        }
    }

    void Fire(Node3D parent, Settlement s)
    {
        var at = Ground(s.X, s.Z);
        parent.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.8f, Height = 0.4f }, Position = at + new Vector3(0, 0.2f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#3a2a1a"), EmissionEnabled = true, Emission = new Color("#ff6a1a") } });
        parent.AddChild(new OmniLight3D { Position = at + new Vector3(0, 1.5f, 0), LightColor = new Color("#ff9a4a"), LightEnergy = 2.5f, OmniRange = 14 });
    }

    void Stones(Node3D parent, Settlement s, Rng rng, int count, float height)
    {
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            Box(parent, Ground(s.X + Math.Cos(a) * s.Radius * 0.6, s.Z + Math.Sin(a) * s.Radius * 0.6) + new Vector3(0, height / 2 - 0.3f, 0),
                new Vector3(1, height * (0.8f + (float)rng.NextDouble() * 0.4f), 0.6f), "#8a8a80", -a);
        }
    }

    void Mine(Node3D parent, Settlement s)
    {
        var at = Ground(s.X - s.Radius * 0.5, s.Z);
        Box(parent, at + new Vector3(0, 4, 0), new Vector3(12, 9, 10), "#5a554e");
        Box(parent, at + new Vector3(6.2f, 1.6f, 0), new Vector3(0.6f, 3.2f, 3), "#141210"); // the way in
        Label(parent, at + new Vector3(7, 4.5f, 0), "Mine entrance", 0.015f);
    }

    void Palisade(Node3D parent, Settlement s)
    {
        for (var i = 0; i < 20; i++)
        {
            var a = i * Mathf.Tau / 24; // a gap on one side
            Box(parent, Ground(s.X + Math.Cos(a) * s.Radius, s.Z + Math.Sin(a) * s.Radius) + new Vector3(0, 1.5f, 0), new Vector3(0.6f, 3.6f, 5.6f), "#5a4630", -(float)a);
        }
    }

    void Barrow(Node3D parent, Settlement s, Rng rng)
    {
        var at = Ground(s.X, s.Z - s.Radius * 0.4);
        var mound = new StaticBody3D { Position = at, Scale = new Vector3(1, 0.45f, 1) };
        mound.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 9, Height = 18 }, MaterialOverride = Mat("#5a6248") });
        mound.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 9 } });
        parent.AddChild(mound);
        Box(parent, at + new Vector3(0, 1.8f, 8.6f), new Vector3(3, 3.6f, 0.8f), "#5a5a58");
        Stones(parent, s, rng, 5, 4);
    }

    void Rocks(Node3D parent, Settlement s, Rng rng)
    {
        for (var i = 0; i < 6; i++)
        {
            var size = 3 + (float)rng.NextDouble() * 5;
            var at = Ground(s.X + (rng.NextDouble() - 0.5) * s.Radius, s.Z - s.Radius * 0.5 + (rng.NextDouble() - 0.5) * 6);
            var rock = Box(parent, at + new Vector3(0, size * 0.35f, 0), new Vector3(size, size * 0.8f, size), "#5e5a54", (float)rng.NextDouble() * 3);
            rock.RotationDegrees += new Vector3((float)rng.NextDouble() * 20, 0, (float)rng.NextDouble() * 20);
        }
        Box(parent, Ground(s.X, s.Z - s.Radius * 0.5) + new Vector3(0, 1.5f, 3.5f), new Vector3(3, 3, 0.5f), "#121110"); // the cave mouth
    }
}
