using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The market stalls on the Gallows Perch road: a counter under a striped awning on four posts, goods piled about it,
/// a sign over it; and at the smithy, a forge glowing beside the anvil.
/// </summary>
public static class Market
{
    static readonly StandardMaterial3D Wood = new() { AlbedoColor = new Color("#6a4a30"), Roughness = 0.85f };
    static readonly StandardMaterial3D Iron = new() { AlbedoColor = new Color("#2e2e32"), Roughness = 0.5f, Metallic = 0.8f };

    public static void Stall(Node3D world, Vector3 at, Shop shop)
    {
        var floor = City.FloorAt(at);
        var root = new Node3D { Position = at with { Y = floor }, Name = shop.Id.Replace(':', '_') };
        world.AddChild(root);
        // The keeper stands behind the counter (to the north, off the road), facing the road.
        var back = new Vector3(0, 0, 1.6f);
        var awning = shop.Forge ? "#5a5248" : shop.Bench ? "#4a7a5a" : "#a83a2a";
        Solid(root, back + new Vector3(0, 0.5f, -1.2f), new Vector3(3.4f, 1, 0.8f), Wood);
        foreach (var (x, z) in new[] { (-1.9f, -1.8f), (1.9f, -1.8f), (-1.9f, 1.8f), (1.9f, 1.8f) })
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 2.8f }, MaterialOverride = Wood, Position = back + new Vector3(x, 1.4f, z) });
        // The striped awning.
        for (var i = 0; i < 6; i++)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(4.2f / 6, 0.05f, 4.2f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(i % 2 == 0 ? awning : "#e8dcc4"), Roughness = 0.9f },
                Position = back + new Vector3(-2.1f + 4.2f / 12 + i * 4.2f / 6, 2.85f, 0), RotationDegrees = new Vector3(-8, 0, 0),
            });
        root.AddChild(new Label3D
        {
            Text = shop.Name, Position = back + new Vector3(0, 3.4f, -1.9f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48, OutlineSize = 10, PixelSize = 0.012f, Modulate = new Color("#ffd27a"),
        });
        // Their goods.
        string[] goods = shop.Forge ? ["dungeon/sword_shield.glb", "dungeon/barrel_small_stack.glb"]
            : shop.Bench ? ["halloween/candle_triple.gltf", "dungeon/shelf_small.glb", "dungeon/keg.glb"]
            : ["dungeon/crates_stacked.glb", "dungeon/barrel_large.glb", "dungeon/box_small.glb"];
        for (var i = 0; i < goods.Length; i++)
            Models.Place(root, goods[i], back + new Vector3(-2.6f + i * 2.6f, 0, 1.2f), goods[i].Contains("candle") ? 1.2f : 0.5f, i * 1.3f, solid: false);
        if (shop.Bench)
            for (var i = 0; i < 5; i++)
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.08f, Height = 0.22f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(new[] { "#c03030", "#3060c0", "#30a050", "#c0a030", "#9050c0" }[i], 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f },
                    Position = back + new Vector3(-1.2f + i * 0.6f, 1.12f, -1.2f),
                });
        if (shop.Forge) Forge(root, back + new Vector3(3.6f, 0, -0.4f));
    }

    static void Solid(Node3D root, Vector3 at, Vector3 size, Material m)
    {
        var body = new StaticBody3D { Position = at };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
    }

    /// <summary>A stone forge with its fire going, and an anvil.</summary>
    static void Forge(Node3D root, Vector3 at)
    {
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#5a5650"), Roughness = 0.95f };
        Solid(root, at + new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 1.4f), stone);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.1f, 0.12f, 0.9f) }, Position = at + new Vector3(0, 1.22f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffb040"), EmissionEnabled = true, Emission = new Color("#ff6a10"), EmissionEnergyMultiplier = 2.5f },
        });
        root.AddChild(new CpuParticles3D
        {
            Position = at + new Vector3(0, 1.3f, 0), Amount = 30, Lifetime = 0.8, Direction = Vector3.Up, Spread = 20, Gravity = new Vector3(0, 1.5f, 0),
            InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.45f, 0.05f, 0.35f),
            Mesh = new QuadMesh
            {
                Size = Vector2.One * 0.35f,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                },
            },
            ColorRamp = new Gradient { Offsets = [0, 0.5f, 1], Colors = [new Color(1, 0.8f, 0.4f, 0.6f), new Color(1, 0.4f, 0.08f, 0.5f), new Color(0.3f, 0.05f, 0, 0)] },
        });
        root.AddChild(new OmniLight3D { Position = at + new Vector3(0, 1.8f, 0), LightColor = new Color("#ff8a3a"), LightEnergy = 1.6f, OmniRange = 6 });
        var anvil = at + new Vector3(-1.6f, 0, 0.6f);
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.6f, 0.35f) }, MaterialOverride = Iron, Position = anvil + new Vector3(0, 0.3f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.18f, 0.8f) }, MaterialOverride = Iron, Position = anvil + new Vector3(0, 0.69f, 0) });
    }
}

/// <summary>
/// The herbs growing near the hero (see <see cref="Herbs"/>), each a little plant you can pick (E): leafy emberleaf,
/// mushrooms, reeds, flowers, berry bushes. Picked, it's gone until it grows back.
/// </summary>
public partial class HerbPatch(Main main) : Node3D
{
    readonly Dictionary<string, (Plant Plant, Node3D Body)> _shown = [];
    double _tick;

    public const float Reach = 2.4f;

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var p = main.PlayerNode.GlobalPosition;
        var near = Herbs.Near(main.World, p.X, p.Z, 90).Where(x => main.HerbLog.Ready(x, main.GuildBook.PlaySeconds)).ToDictionary(x => x.Id);
        foreach (var id in _shown.Keys.Where(id => !near.ContainsKey(id)).ToList())
        {
            _shown[id].Body.QueueFree();
            _shown.Remove(id);
        }
        foreach (var (id, plant) in near)
        {
            if (_shown.ContainsKey(id)) continue;
            var at = new Vector3((float)plant.X, 0, (float)plant.Z);
            if (!main.TerrainNode.HasGround(at)) continue;
            var body = Grow(plant.HerbId);
            AddChild(body);
            body.GlobalPosition = at with { Y = City.FloorAt(at) };
            body.RotationDegrees = new Vector3(0, (float)(WorldGen.StableHash(id) % 360), 0);
            _shown[id] = (plant, body);
        }
    }

    /// <summary>The plant within reach of a spot, if any.</summary>
    public Plant? Within(Vector3 at) => _shown.Values.Where(v => v.Body.GlobalPosition.DistanceTo(at) < Reach)
        .OrderBy(v => v.Body.GlobalPosition.DistanceTo(at)).Select(v => v.Plant).FirstOrDefault();

    public int Shown => _shown.Count;

    public void Picked(Plant p)
    {
        if (!_shown.Remove(p.Id, out var v)) return;
        Effects.Burst(main, v.Body.GlobalPosition + Vector3.Up * 0.3f, Tristinwold.Nemesis.DamageKind.Poison, 0.6f);
        v.Body.QueueFree();
    }

    static StandardMaterial3D Mat(string hex, float glow = 0) => new()
    {
        AlbedoColor = new Color(hex), Roughness = 0.8f, EmissionEnabled = glow > 0, Emission = new Color(hex), EmissionEnergyMultiplier = glow,
    };

    /// <summary>A plant, built for its herb: what it looks like in the wild.</summary>
    static Node3D Grow(string herb)
    {
        var n = new Node3D();
        var rng = new RandomNumberGenerator { Seed = (ulong)WorldGen.StableHash(herb) };
        var (leaf, flower, kind) = herb switch
        {
            "emberleaf" => ("#7a3a1a", "#e04a20", "leafy"),
            "ladys_mantle" => ("#4a7a3a", "#c8d860", "leafy"),
            "bloodcap" => ("#d8c8b0", "#b01818", "mushroom"),
            "ghostcap" => ("#d8d8e0", "#b0d8ff", "mushroom"),
            "bluebell" => ("#3a6a3a", "#4a6ad8", "flower"),
            "marsh_reed" => ("#6a7a3a", "#8a6a3a", "reed"),
            "nightshade" => ("#2a4a2a", "#3a1a4a", "berries"),
            "snowberry" => ("#3a5a4a", "#f0f0f4", "berries"),
            "frostbloom" => ("#5a8aa0", "#a0e0ff", "flower"),
            "silverthorn" => ("#8a9aa0", "#c8d0d8", "leafy"),
            _ => ("#4a6a3a", "#a050c0", "flower"), // mountain thistle
        };
        switch (kind)
        {
            case "mushroom":
                for (var i = 0; i < 4; i++)
                {
                    var at = new Vector3(rng.RandfRange(-0.25f, 0.25f), 0, rng.RandfRange(-0.25f, 0.25f));
                    var h = rng.RandfRange(0.12f, 0.25f);
                    n.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.035f, Height = h }, MaterialOverride = Mat(leaf), Position = at + Vector3.Up * h / 2 });
                    n.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.09f, Height = 0.09f, IsHemisphere = true }, MaterialOverride = Mat(flower, herb == "ghostcap" ? 0.8f : 0), Position = at + Vector3.Up * h });
                }
                break;
            case "reed":
                for (var i = 0; i < 9; i++)
                {
                    var h = rng.RandfRange(0.6f, 1.1f);
                    var lean = new Vector3(rng.RandfRange(-0.15f, 0.15f), 1, rng.RandfRange(-0.15f, 0.15f)).Normalized();
                    var at = new Vector3(rng.RandfRange(-0.2f, 0.2f), 0, rng.RandfRange(-0.2f, 0.2f));
                    n.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.015f, Height = h }, MaterialOverride = Mat(leaf), Basis = Graft.Along(lean), Position = at + lean * h / 2 });
                    if (i % 3 == 0) n.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.025f, Height = 0.16f }, MaterialOverride = Mat(flower), Position = at + lean * h });
                }
                break;
            default:
                // Leaves in a rosette, and flowers or berries above them.
                for (var i = 0; i < 7; i++)
                {
                    var a = i * Mathf.Tau / 7;
                    n.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = 0.1f, Height = 0.04f }, MaterialOverride = Mat(leaf),
                        Position = new Vector3(Mathf.Cos(a), 0.8f, Mathf.Sin(a)) * 0.14f + Vector3.Up * 0.04f, Scale = new Vector3(1.6f, 1, 0.8f), Rotation = new Vector3(0, -a, 0.3f),
                    });
                }
                if (kind != "leafy")
                    for (var i = 0; i < (kind == "berries" ? 9 : 4); i++)
                    {
                        var at = new Vector3(rng.RandfRange(-0.15f, 0.15f), rng.RandfRange(0.2f, 0.4f), rng.RandfRange(-0.15f, 0.15f));
                        n.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.008f, Height = at.Y }, MaterialOverride = Mat(leaf), Position = at with { Y = at.Y / 2 } });
                        n.AddChild(new MeshInstance3D
                        {
                            Mesh = new SphereMesh { Radius = kind == "berries" ? 0.03f : 0.05f, Height = kind == "berries" ? 0.06f : 0.06f },
                            MaterialOverride = Mat(flower, herb == "frostbloom" ? 0.5f : 0.15f), Position = at,
                        });
                    }
                else
                    for (var i = 0; i < 5; i++)
                    {
                        var a = i * Mathf.Tau / 5 + 0.3f;
                        n.AddChild(new MeshInstance3D
                        {
                            Mesh = new SphereMesh { Radius = 0.08f, Height = 0.03f }, MaterialOverride = Mat(flower, 0.1f),
                            Position = new Vector3(Mathf.Cos(a) * 0.06f, 0.16f, Mathf.Sin(a) * 0.06f), Scale = new Vector3(1.4f, 1, 0.7f), Rotation = new Vector3(0.6f, -a, 0),
                        });
                    }
                break;
        }
        return n;
    }
}
