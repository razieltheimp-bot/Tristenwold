using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>Where things happen in a built lair: where the boss comes from, where its own gather, where its hoard lies.</summary>
public sealed record LairLayout(Node3D Root, Vector3 BossAt, Vector3 ThrallsAt, float ThrallSpread, Vector3 HoardAt);

/// <summary>
/// The lairs, built where they stand: a burned crypt; a cave in a heap of great scanned boulders, its mouth black
/// and deep (a goblin warren full of loot and campfires, a bear's den of bones, a hollow grown with amethyst); a
/// ruin gone to the wild (a manor swallowed by red-blooming briar, a shrine in a dead grove under violet dusk); a
/// dwarven forge, its furnace still burning and its stair going down into the dark; a broken fort of the dead; and
/// a thunderhawk's nest of whole trees up among stone spires under a storm.
/// </summary>
public static class LairSites
{
    static Material? _stone, _darkStone, _bramble;
    /// <summary>Old stone, from Poly Haven's scans, laid on any shape by its position (so a wall of any size is right).</summary>
    static Material Stone => _stone ??= Photo(Surface.Stone, "#b8b2a8", 0.5f);
    static Material DarkStone => _darkStone ??= Photo(Surface.DarkStone, "#8a8680", 0.4f);

    static Material Photo(Surface s, string tint, float perMetre)
    {
        if (BuildingMaterials.For(s).Duplicate() is not StandardMaterial3D m) return new StandardMaterial3D { AlbedoColor = new Color(tint), Roughness = 0.95f };
        m.AlbedoColor = new Color(tint);
        m.Uv1Triplanar = true;
        m.Uv1WorldTriplanar = true;
        m.Uv1Scale = Vector3.One * perMetre;
        return m;
    }

    static readonly StandardMaterial3D Dark = new() { AlbedoColor = new Color("#030202"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    static readonly StandardMaterial3D Wood = new() { AlbedoColor = new Color("#4a3624"), Roughness = 0.9f };
    static readonly StandardMaterial3D Iron = new() { AlbedoColor = new Color("#2e2e32"), Roughness = 0.5f, Metallic = 0.8f };
    static readonly StandardMaterial3D Molten = new()
    {
        AlbedoColor = new Color("#ffb040"), EmissionEnabled = true, Emission = new Color("#ff7a20"), EmissionEnergyMultiplier = 3,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
    static readonly StandardMaterial3D Amethyst = new()
    {
        AlbedoColor = new Color(0.62f, 0.38f, 0.95f, 0.88f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f,
        MetallicSpecular = 1, EmissionEnabled = true, Emission = new Color("#8a40ff"), EmissionEnergyMultiplier = 1.1f, RimEnabled = true, Rim = 0.8f,
    };
    static readonly StandardMaterial3D Petal = new() { AlbedoColor = new Color("#b81c26"), Roughness = 0.6f };
    /// <summary>Dense briar: leaves all over, dark.</summary>
    static Material Bramble => _bramble ??= ResourceLoader.Exists("res://assets/polyhaven/foliage/leaves_green.png")
        ? new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>("res://assets/polyhaven/foliage/leaves_green.png"), AlbedoColor = new Color("#4a6a34"),
            Uv1Triplanar = true, Uv1Scale = Vector3.One * 1.6f, Roughness = 0.85f,
        }
        : new StandardMaterial3D { AlbedoColor = new Color("#24361a"), Roughness = 0.9f };

    public static LairLayout Build(Raid raid, Vector3 c, Node3D into)
    {
        var root = new Node3D { Name = raid.Id.Replace(':', '_') };
        into.AddChild(root);
        // The lair faces the way it's approached from: from Tristinwold. Its back is away from the city.
        var back = new Vector3(c.X, 0, c.Z).Normalized();
        if (back == Vector3.Zero) back = Vector3.Forward;
        var rng = new RandomNumberGenerator { Seed = (ulong)WorldGen.StableHash(raid.Id) };
        return raid.Site switch
        {
            LairSite.Cave => Cave(raid, c, back, root, rng),
            LairSite.Ruin => Ruin(raid, c, back, root, rng),
            LairSite.Dungeon => Forge(c, back, root, rng),
            LairSite.Fort => Fort(c, back, root, rng),
            LairSite.Eyrie => Eyrie(c, root, rng),
            _ => Crypt(c, root),
        };
    }

    // ---- pieces ---------------------------------------------------------------------------------

    static Vector3 Floor(Vector3 p) => p with { Y = City.FloorAt(p) };

    static Vector3 Side(Vector3 back) => new(-back.Z, 0, back.X);

    /// <summary>A scanned boulder sized in metres, sunk a little into the ground, solid.</summary>
    static void Rock(Node3D root, Vector3 at, Vector3 size, float yaw, string model = "ph:boulder.glb", float sink = 0.25f)
    {
        if (Models.MeshOf(model) is not { } mesh) return;
        var box = mesh.GetAabb();
        var scale = new Vector3(size.X / box.Size.X, size.Y / box.Size.Y, size.Z / box.Size.Z);
        var floor = City.FloorAt(at);
        var body = new StaticBody3D { Position = at with { Y = floor - size.Y * sink }, Rotation = new Vector3(0, yaw, 0) };
        var centre = box.GetCenter();
        body.AddChild(new MeshInstance3D { Mesh = mesh, Scale = scale, Position = new Vector3(-centre.X * scale.X, -box.Position.Y * scale.Y, -centre.Z * scale.Z) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size * new Vector3(0.8f, 1, 0.8f) }, Position = Vector3.Up * size.Y / 2 });
        root.AddChild(body);
    }

    /// <summary>A block of stone: a wall, a step, a slab. Solid.</summary>
    static void Block(Node3D root, Vector3 centre, Vector3 size, float yaw, Material material, bool solid = true)
    {
        Node3D holder = solid ? new StaticBody3D() : new Node3D();
        holder.Position = centre;
        holder.Rotation = new Vector3(0, yaw, 0);
        holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material });
        if (solid) holder.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(holder);
    }

    /// <summary>A wall from a to b standing on the ground, broken to a height at each end.</summary>
    static void Wall(Node3D root, Vector3 a, Vector3 b, float height, float thick, Material material)
    {
        var mid = a.Lerp(b, 0.5f);
        var floor = Mathf.Min(City.FloorAt(a), City.FloorAt(b)) - 0.4f;
        var flat = (b - a) with { Y = 0 };
        Block(root, mid with { Y = floor + (height + 0.4f) / 2 }, new Vector3(thick, height + 0.4f, flat.Length()), Mathf.Atan2(flat.X, flat.Z), material);
    }

    static void Prop(Node3D root, string path, Vector3 at, float scale, float yaw = 0, bool solid = false) =>
        Models.Place(root, path, Floor(at), scale, yaw, solid);

    static void Lamp(Node3D root, Vector3 at, string colour, float energy, float range, float flicker = 0.15f)
    {
        var light = new Flicker { LightColor = new Color(colour), LightEnergy = energy, OmniRange = range, Base = energy, Amount = flicker, ShadowEnabled = false };
        root.AddChild(light);
        light.Position = at;
    }

    static void Fire(Node3D root, Vector3 at, float size)
    {
        var flame = new CpuParticles3D
        {
            Mesh = new QuadMesh
            {
                Size = Vector2.One * size * 2,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                },
            },
            Amount = (int)(20 + size * 40), Lifetime = 0.8, Direction = Vector3.Up, Spread = 15, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = size * 0.6f,
            ColorRamp = new Gradient { Offsets = [0, 0.5f, 1], Colors = [new Color(1, 0.8f, 0.4f, 0.55f), new Color(1, 0.4f, 0.08f, 0.5f), new Color(0.4f, 0.05f, 0, 0)] },
        };
        root.AddChild(flame);
        flame.Position = at;
        Lamp(root, at + Vector3.Up * 0.8f, "#ff8a3a", 1.4f + size, 6 + size * 6, 0.25f);
    }

    static CpuParticles3D Haze(Vector3 at, Color colour, float extent, float size, int amount) => new()
    {
        Position = at,
        Mesh = new QuadMesh
        {
            Size = Vector2.One * size,
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            },
        },
        Amount = amount, Lifetime = 9, Preprocess = 9, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(extent, 0.3f, extent),
        Direction = Vector3.Right, Spread = 180, Gravity = Vector3.Zero, InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f, ScaleAmountMin = 0.8f, ScaleAmountMax = 1.6f,
        ColorRamp = new Gradient { Offsets = [0, 0.3f, 0.7f, 1], Colors = [new Color(colour, 0), colour, colour, new Color(colour, 0)] },
    };

    /// <summary>A skin laid over the ground, following its every rise: scorched, trodden, overgrown, paved.</summary>
    public static MeshInstance3D Ground(Vector3 c, float radius, Color colour, float edge = 1.2f)
    {
        const int n = 28;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int i, int j)
        {
            var (x, z) = ((i / (float)n * 2 - 1) * radius, (j / (float)n * 2 - 1) * radius);
            return new Vector3(x, City.FloorAt(c + new Vector3(x, 0, z)) - c.Y + 0.06f, z);
        }
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                foreach (var (a, b) in new[] { (0, 0), (1, 0), (1, 1), (0, 0), (1, 1), (0, 1) })
                {
                    var p = Point(i + a, j + b);
                    var d = new Vector2(p.X, p.Z).Length() / radius;
                    st.SetColor(new Color(colour, Mathf.Clamp(1.15f - d * d * edge, 0, colour.A)));
                    st.SetNormal(Vector3.Up);
                    st.AddVertex(p);
                }
        return new MeshInstance3D
        {
            Mesh = st.Commit(), Position = c, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1 },
        };
    }

    /// <summary>A cluster of crystal points, splaying from one root.</summary>
    static void Crystals(Node3D root, Vector3 at, float size, RandomNumberGenerator rng)
    {
        var holder = new Node3D { Position = Floor(at) };
        root.AddChild(holder);
        var count = 4 + rng.RandiRange(0, 4);
        for (var i = 0; i < count; i++)
        {
            var dir = new Vector3(rng.RandfRange(-0.6f, 0.6f), 1, rng.RandfRange(-0.6f, 0.6f)).Normalized();
            var length = size * rng.RandfRange(0.4f, 1);
            var radius = length * 0.16f;
            var shard = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = radius, Height = length, RadialSegments = 6, Rings = 1 }, MaterialOverride = Amethyst,
                Basis = Graft.Along(dir), Position = dir * length * 0.4f,
            };
            holder.AddChild(shard);
        }
    }

    // ---- the crypt ------------------------------------------------------------------------------

    static LairLayout Crypt(Vector3 c, Node3D root)
    {
        var scorch = new StandardMaterial3D { AlbedoColor = new Color("#1e1a17"), Roughness = 1 };
        root.AddChild(Ground(c, 22, new Color(0.09f, 0.075f, 0.065f, 0.92f)));
        var dais = new StaticBody3D { Position = c + Vector3.Up * 0.3f };
        dais.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(7, 0.6f, 7) }, MaterialOverride = Stone });
        dais.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(7, 0.6f, 7) } });
        dais.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.9f, 2.8f) }, MaterialOverride = scorch, Position = new Vector3(0, 0.75f, -2.2f) });
        root.AddChild(dais);
        for (var i = 0; i < 10; i++)
        {
            var a = i * Mathf.Tau / 10;
            var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 15;
            var broken = i % 3 == 1;
            var height = broken ? 2.5f + i % 2 : 7;
            var pillar = new StaticBody3D { Position = at with { Y = City.FloorAt(at) + height / 2 } };
            pillar.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.75f, Height = height, RadialSegments = 10 }, MaterialOverride = Stone });
            pillar.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.7f, Height = height } });
            if (!broken) pillar.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.5f, 1.8f) }, MaterialOverride = Stone, Position = Vector3.Up * height / 2 });
            root.AddChild(pillar);
            if (broken) Models.Place(root, "dungeon/rubble_large.glb", at + new Vector3(1.2f, City.FloorAt(at) - at.Y, 0.8f), 0.35f, a, solid: false);
            if (i % 2 == 0)
            {
                var torch = Floor(at.Lerp(c, 0.12f));
                root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.08f, Height = 2.4f }, MaterialOverride = scorch, Position = torch + Vector3.Up * 1.3f });
                Fire(root, torch + Vector3.Up * 2.6f, 0.18f);
            }
        }
        root.AddChild(Haze(c + Vector3.Up * 0.6f, new Color(0.8f, 0.82f, 0.85f, 0.16f), 18, 4.5f, 28));
        return new LairLayout(root, c + new Vector3(0, 0.6f, -1), c, 8, c + new Vector3(0, 0.6f, 1.5f));
    }

    // ---- caves ----------------------------------------------------------------------------------

    /// <summary>A hollow in a heap of great boulders, its cave mouth black and deep at the back.</summary>
    static LairLayout Cave(Raid raid, Vector3 c, Vector3 back, Node3D root, RandomNumberGenerator rng)
    {
        var side = Side(back);
        var floor = raid.BossKind switch
        {
            "crystal_basilisk" => new Color(0.12f, 0.09f, 0.15f, 0.85f),
            "rock_bear" => new Color(0.13f, 0.12f, 0.1f, 0.75f),
            _ => new Color(0.15f, 0.11f, 0.07f, 0.75f),
        };
        root.AddChild(Ground(c, 24, floor));
        var mouth = c + back * 11;

        // The hill: the mouth's two great flanking stones, the lintel across them, and the heap over and behind.
        Rock(root, mouth + side * 4.6f, new Vector3(5, 7.5f, 6), rng.RandfRange(0, 6));
        Rock(root, mouth - side * 4.6f, new Vector3(5, 7, 6), rng.RandfRange(0, 6));
        var lintel = mouth + back * 0.6f;
        Rock(root, lintel + Vector3.Up * 0.0f, new Vector3(10, 3.2f, 5), Mathf.Atan2(side.X, side.Z), "ph:boulder_flat.glb", sink: -1.6f);
        foreach (var (along, out_, size) in new[] { (0f, 5f, 11f), (-6f, 4f, 9f), (6f, 4f, 9f), (-3f, 9f, 12f), (4f, 10f, 12f), (-10f, 2f, 8f), (10f, 2f, 8f), (0f, 13f, 10f) })
            Rock(root, mouth + side * along + back * out_, new Vector3(size, size * 0.85f, size), rng.RandfRange(0, 6), sink: 0.3f);
        // The throat: black, going down out of sight.
        Block(root, Floor(mouth) + back * 3.2f + Vector3.Up * 2.2f, new Vector3(4.4f, 5, 4), Mathf.Atan2(back.X, back.Z), Dark, solid: false);
        // The hollow's walls, boulders round the back half of the clearing.
        for (var i = 0; i < 9; i++)
        {
            var a = Mathf.Atan2(back.Z, back.X) + (i - 4) * 0.36f;
            if (Mathf.Abs(i - 4) < 1) continue;
            var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng.RandfRange(17, 20);
            var s = rng.RandfRange(3.5f, 6);
            Rock(root, at, new Vector3(s, s * rng.RandfRange(0.7f, 1.1f), s), rng.RandfRange(0, 6));
        }
        for (var i = 0; i < 6; i++)
        {
            var at = c + new Vector3(rng.RandfRange(-14, 14), 0, rng.RandfRange(-14, 14));
            if (at.DistanceTo(c) < 6) continue;
            Rock(root, at, Vector3.One * rng.RandfRange(0.6f, 1.4f), rng.RandfRange(0, 6), "ph:boulder_small.glb", 0.2f);
        }

        switch (raid.BossKind)
        {
            case "goblin_king":
                Fire(root, Floor(c + side * 3 - back * 2) + Vector3.Up * 0.3f, 0.3f);
                Prop(root, "ph:fire_pit.glb", c + side * 3 - back * 2, 1.4f);
                foreach (var (x, z, model, s) in new[] { (-7f, 4f, "dungeon/barrel_large.glb", 0.5f), (-8f, 2f, "dungeon/crates_stacked.glb", 0.5f), (7f, 5f, "dungeon/box_stacked.glb", 0.5f), (6f, 7f, "dungeon/barrel_small_stack.glb", 0.5f), (-5f, -6f, "dungeon/keg.glb", 0.5f), (2f, 8f, "dungeon/trunk_medium_A.glb", 0.5f) })
                    Prop(root, model, c + side * x + back * z, s, rng.RandfRange(0, 6), solid: true);
                foreach (var x in new[] { -2.6f, 2.6f })
                {
                    var torch = Floor(mouth - back * 1.5f + side * x);
                    root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.07f, Height = 2.2f }, MaterialOverride = Wood, Position = torch + Vector3.Up * 1.1f });
                    Fire(root, torch + Vector3.Up * 2.3f, 0.16f);
                    Prop(root, "dungeon/banner_patternA_red.glb", mouth - back * 1.2f + side * x * 1.4f, 0.8f, Mathf.Atan2(-back.X, -back.Z));
                }
                foreach (var i in Enumerable.Range(0, 8))
                    Prop(root, $"halloween/bone_{"ABC"[i % 3]}.gltf", c + new Vector3(rng.RandfRange(-10, 10), 0, rng.RandfRange(-10, 10)), 1, rng.RandfRange(0, 6));
                break;
            case "rock_bear":
                foreach (var i in Enumerable.Range(0, 14))
                    Prop(root, i % 5 == 0 ? "halloween/ribcage.gltf" : i % 4 == 0 ? "halloween/skull.gltf" : $"halloween/bone_{"ABC"[i % 3]}.gltf",
                        mouth - back * rng.RandfRange(1, 9) + side * rng.RandfRange(-6, 6), i % 4 == 0 && i % 5 != 0 ? 0.45f : 1.1f, rng.RandfRange(0, 6));
                Prop(root, "ph:dead_trunk.glb", c - side * 7 + back * 3, 1.2f, 0.7f, solid: true);
                Prop(root, "ph:stump.glb", c + side * 8 - back * 2, 1.2f, 0, solid: true);
                root.AddChild(Haze(Floor(mouth) + Vector3.Up * 0.6f, new Color(0.7f, 0.68f, 0.62f, 0.12f), 6, 3, 10));
                break;
            case "crystal_basilisk":
                foreach (var i in Enumerable.Range(0, 16))
                {
                    var a = Mathf.Atan2(back.Z, back.X) + rng.RandfRange(-1.9f, 1.9f);
                    var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng.RandfRange(9, 16);
                    Crystals(root, at, rng.RandfRange(1.2f, 3.2f), rng);
                }
                foreach (var x in new[] { -3.2f, 3.2f }) Crystals(root, mouth - back * 0.8f + side * x, 3.8f, rng);
                foreach (var (x, z) in new[] { (0f, 6f), (-8f, 0f), (8f, 1f) }) Lamp(root, Floor(c + side * x + back * z) + Vector3.Up * 2, "#a060ff", 1.6f, 10);
                root.AddChild(Haze(c + Vector3.Up * 0.5f, new Color(0.6f, 0.45f, 0.9f, 0.1f), 16, 4, 20));
                break;
        }
        return new LairLayout(root, Floor(mouth - back * 0.5f) + Vector3.Up * 0.3f, Floor(c + back * 5), 4, Floor(mouth - back * 2.5f) + Vector3.Up * 0.5f);
    }

    // ---- ruins ----------------------------------------------------------------------------------

    /// <summary>A ruin gone back to the wild: broken walls, an arch still standing, fallen columns.</summary>
    static LairLayout Ruin(Raid raid, Vector3 c, Vector3 back, Node3D root, RandomNumberGenerator rng)
    {
        var side = Side(back);
        var briar = raid.BossKind == "briar_mother";
        var wall = briar ? Stone : DarkStone;
        root.AddChild(Ground(c, 22, briar ? new Color(0.2f, 0.26f, 0.13f, 0.7f) : new Color(0.12f, 0.1f, 0.14f, 0.85f)));
        root.AddChild(Ground(c + back * 2, 10, new Color(0.27f, 0.26f, 0.24f, 0.7f), 2.2f)); // what's left of the flagstones
        // The walls of the old hall: a long rectangle, broken down to stumps in places, with gaps.
        var (w, d) = (11f, 9f);
        var corners = new[] { c - side * w - back * d, c + side * w - back * d, c + side * w + back * d, c - side * w + back * d };
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (corners[i], corners[(i + 1) % 4]);
            var pieces = 4;
            for (var k = 0; k < pieces; k++)
            {
                if (i == 0 && k is 1 or 2) continue; // the way in, facing the city
                if (rng.Randf() < 0.2f) continue;
                var (p, q) = (a.Lerp(b, k / (float)pieces), a.Lerp(b, (k + 0.85f) / pieces));
                Wall(root, p, q, rng.RandfRange(1.2f, i == 2 ? 6.5f : 4.5f), 0.9f, wall);
                if (rng.Randf() < 0.5f) Prop(root, "dungeon/rubble_large.glb", p.Lerp(q, 0.5f) - (p.Lerp(q, 0.5f) - c).Normalized() * 1.4f, 0.3f, rng.RandfRange(0, 6));
            }
        }
        // An arch still standing at the back, and the altar (or hearthstone) under it.
        Prop(root, "halloween/arch.gltf", c + back * 7, 1.5f, Mathf.Atan2(side.X, side.Z), solid: false);
        Block(root, Floor(c + back * 6) + Vector3.Up * 0.45f, new Vector3(3, 0.9f, 1.6f), Mathf.Atan2(side.X, side.Z), wall);
        // Columns, standing and fallen.
        for (var i = 0; i < 6; i++)
        {
            var at = c + side * ((i % 2 == 0 ? -1 : 1) * 6.5f) + back * (-5 + (i / 2) * 5);
            if (rng.Randf() < 0.45f)
            {
                var fallen = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.55f, Height = 5, RadialSegments = 10 }, MaterialOverride = wall };
                var holder = new StaticBody3D { Position = Floor(at) + Vector3.Up * 0.45f, Rotation = new Vector3(Mathf.Pi / 2, rng.RandfRange(0, 6), 0) };
                holder.AddChild(fallen);
                holder.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.5f, Height = 5 } });
                root.AddChild(holder);
            }
            else
            {
                var height = rng.RandfRange(3, 6);
                var col = new StaticBody3D { Position = Floor(at) + Vector3.Up * height / 2 };
                col.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.55f, Height = height, RadialSegments = 10 }, MaterialOverride = wall });
                col.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.5f, Height = height } });
                root.AddChild(col);
            }
        }
        if (briar)
        {
            // The briar that swallowed it: thorny mounds in red bloom, ivy up the walls.
            for (var i = 0; i < 22; i++)
            {
                var at = c + new Vector3(rng.RandfRange(-17, 17), 0, rng.RandfRange(-17, 17));
                if (at.DistanceTo(c) < 5) continue;
                var s = rng.RandfRange(0.8f, 2.2f);
                var mound = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = s, Height = s * 1.2f, RadialSegments = 10, Rings = 6 }, MaterialOverride = Bramble,
                    Position = Floor(at) + Vector3.Up * s * 0.25f, Scale = new Vector3(1, 0.8f, rng.RandfRange(0.8f, 1.4f)),
                };
                root.AddChild(mound);
                for (var b = 0; b < 4; b++)
                {
                    var dir = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(0.2f, 1), rng.RandfRange(-1, 1)).Normalized();
                    root.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = 0.13f, Height = 0.12f, RadialSegments = 8, Rings = 4 }, MaterialOverride = Petal,
                        Position = mound.Position + dir * new Vector3(s, s * 0.5f, s) * 0.95f,
                    });
                }
            }
            root.AddChild(Haze(c + Vector3.Up * 1.2f, new Color(0.9f, 0.85f, 0.35f, 0.08f), 16, 1.2f, 30));
        }
        else
        {
            // A dead grove under a violet dusk, the shrine's candles still lit.
            foreach (var i in Enumerable.Range(0, 6))
            {
                var a = i * Mathf.Tau / 6 + 0.3f;
                Prop(root, i % 2 == 0 ? "halloween/tree_dead_large.gltf" : "halloween/tree_dead_medium.gltf", c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng.RandfRange(16, 20), 2.2f, rng.RandfRange(0, 6), solid: true);
            }
            Prop(root, "halloween/shrine_candles.gltf", c + back * 6.2f, 1.3f, Mathf.Atan2(-back.X, -back.Z));
            foreach (var x in new[] { -4f, 4f }) Prop(root, "halloween/candle_triple.gltf", c + back * 5 + side * x, 1.5f);
            Lamp(root, Floor(c + back * 6) + Vector3.Up * 2.2f, "#ffb070", 1.2f, 7, 0.2f);
            foreach (var (x, z) in new[] { (-9f, 0f), (9f, -2f), (0f, -8f) }) Lamp(root, Floor(c + side * x + back * z) + Vector3.Up * 3, "#9050ff", 1.4f, 12, 0.05f);
            root.AddChild(Haze(c + Vector3.Up * 0.6f, new Color(0.35f, 0.2f, 0.5f, 0.22f), 18, 5, 34));
        }
        return new LairLayout(root, Floor(c + back * 4) + Vector3.Up * 0.3f, Floor(c), 6, Floor(c + back * 6) + Vector3.Up * 1.1f);
    }

    // ---- the sunken forge -----------------------------------------------------------------------

    static LairLayout Forge(Vector3 c, Vector3 back, Node3D root, RandomNumberGenerator rng)
    {
        var side = Side(back);
        var yaw = Mathf.Atan2(side.X, side.Z);
        root.AddChild(Ground(c, 22, new Color(0.12f, 0.11f, 0.1f, 0.9f)));
        // The forge yard: paved, walled, with dwarven pillars.
        var paving = new StandardMaterial3D { AlbedoColor = new Color("#4a4640"), Roughness = 0.9f };
        Block(root, Floor(c) + Vector3.Down * 0.15f, new Vector3(26, 0.5f, 26), yaw, paving);
        var (w, d) = (13f, 13f);
        var corners = new[] { c - side * w - back * d, c + side * w - back * d, c + side * w + back * d, c - side * w + back * d };
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (corners[i], corners[(i + 1) % 4]);
            if (i == 0)
            {
                Wall(root, a, a.Lerp(b, 0.35f), 3.5f, 1.2f, DarkStone);
                Wall(root, a.Lerp(b, 0.65f), b, 3.5f, 1.2f, DarkStone);
            }
            else Wall(root, a, b, i == 2 ? 7 : 4, 1.2f, DarkStone);
            Prop(root, "dungeon/pillar.glb", a, 1.2f, 0, solid: true);
        }
        // The stair going down into the mountain, at the back: a great doorway, black beyond.
        var door = c + back * 12;
        foreach (var x in new[] { -3.4f, 3.4f }) Block(root, Floor(door + side * x) + Vector3.Up * 3.5f, new Vector3(1.4f, 7, 2), yaw, Stone);
        Block(root, Floor(door) + Vector3.Up * 7.4f, new Vector3(8.4f, 1.2f, 2.4f), yaw, Stone);
        Block(root, Floor(door) + Vector3.Up * 3.2f + back * 0.9f, new Vector3(5.6f, 6.4f, 0.3f), yaw, Dark, solid: false);
        for (var s = 0; s < 4; s++)
            Block(root, Floor(door - back * (1.2f + s * 0.7f)) + Vector3.Up * (0.1f - s * 0.05f), new Vector3(5.4f, 0.25f, 0.7f), yaw, Stone, solid: false);
        // The furnace, still burning, and its chimney; a trough of molten metal; anvils.
        var furnace = c - side * 8 + back * 7;
        Block(root, Floor(furnace) + Vector3.Up * 1.8f, new Vector3(4, 3.6f, 3.4f), yaw, DarkStone);
        Block(root, Floor(furnace) + Vector3.Up * 1.2f - back * 1.72f, new Vector3(2, 1.4f, 0.1f), yaw, Molten, solid: false);
        Fire(root, Floor(furnace) + Vector3.Up * 1.2f - back * 1.9f, 0.5f);
        var stack = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.9f, Height = 9, RadialSegments = 12 }, MaterialOverride = DarkStone, Position = Floor(furnace) + Vector3.Up * 7.5f };
        root.AddChild(stack);
        root.AddChild(new CpuParticles3D
        {
            Position = Floor(furnace) + Vector3.Up * 12.2f,
            Mesh = new QuadMesh { Size = Vector2.One * 2.5f, Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles } },
            Amount = 24, Lifetime = 6, Direction = Vector3.Up, Spread = 15, Gravity = new Vector3(0.4f, 0.6f, 0), InitialVelocityMin = 0.5f, InitialVelocityMax = 1,
            ScaleAmountMin = 0.8f, ScaleAmountMax = 2.4f,
            ColorRamp = new Gradient { Offsets = [0, 0.2f, 1], Colors = [new Color(0.1f, 0.09f, 0.08f, 0), new Color(0.1f, 0.09f, 0.08f, 0.5f), new Color(0.3f, 0.3f, 0.3f, 0)] },
        });
        var trough = c + side * 7 + back * 2;
        Block(root, Floor(trough) + Vector3.Up * 0.3f, new Vector3(1.6f, 0.6f, 8), Mathf.Atan2(back.X, back.Z), DarkStone);
        Block(root, Floor(trough) + Vector3.Up * 0.62f, new Vector3(1.1f, 0.05f, 7.4f), Mathf.Atan2(back.X, back.Z), Molten, solid: false);
        Lamp(root, Floor(trough) + Vector3.Up * 1.4f, "#ff7a2a", 2, 8, 0.2f);
        foreach (var (x, z) in new[] { (-3f, 1f), (2f, -2f), (-1f, -6f) })
        {
            var anvil = Floor(c + side * x + back * z);
            Block(root, anvil + Vector3.Up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f), yaw, Iron);
            Block(root, anvil + Vector3.Up * 0.8f, new Vector3(0.5f, 0.2f, 1.2f), yaw, Iron);
        }
        foreach (var (x, z, model) in new[] { (9f, -8f, "dungeon/barrel_large.glb"), (10f, -6f, "dungeon/crates_stacked.glb"), (-10f, -7f, "dungeon/box_large.glb"), (8f, 9f, "dungeon/barrel_small_stack.glb") })
            Prop(root, model, c + side * x + back * z, 0.5f, rng.RandfRange(0, 6), solid: true);
        Prop(root, "dungeon/torch_mounted.glb", door - back * 1.1f + side * 4.4f + Vector3.Up * 3, 1.2f, Mathf.Atan2(-back.X, -back.Z));
        Lamp(root, Floor(door - back * 1.6f) + Vector3.Up * 4, "#ff9a4a", 1.4f, 9, 0.2f);
        return new LairLayout(root, Floor(door - back * 2.5f) + Vector3.Up * 0.4f, Floor(c), 6, Floor(furnace - back * 3 + side * 2) + Vector3.Up * 0.5f);
    }

    // ---- the fort -------------------------------------------------------------------------------

    static LairLayout Fort(Vector3 c, Vector3 back, Node3D root, RandomNumberGenerator rng)
    {
        var side = Side(back);
        root.AddChild(Ground(c, 24, new Color(0.2f, 0.22f, 0.2f, 0.75f)));
        var (w, d) = (14f, 14f);
        var corners = new[] { c - side * w - back * d, c + side * w - back * d, c + side * w + back * d, c - side * w + back * d };
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (corners[i], corners[(i + 1) % 4]);
            for (var k = 0; k < 5; k++)
            {
                if (i == 0 && k == 2) continue; // the gate, facing the road
                var broken = rng.Randf() < 0.25f;
                var (p, q) = (a.Lerp(b, k / 5f), a.Lerp(b, (k + 1) / 5f));
                Wall(root, p, q, broken ? rng.RandfRange(1.5f, 3) : 5.5f, 1.4f, Stone);
                if (!broken)
                    for (var m = 0; m < 3; m++)
                    {
                        var at = p.Lerp(q, (m + 0.5f) / 3);
                        Block(root, at with { Y = Mathf.Min(City.FloorAt(p), City.FloorAt(q)) + 5.6f }, new Vector3(1.4f, 0.8f, 0.8f), Mathf.Atan2((q - p).X, (q - p).Z), Stone, solid: false);
                    }
                else Prop(root, "dungeon/rubble_large.glb", p.Lerp(q, 0.5f) + (p.Lerp(q, 0.5f) - c).Normalized() * 1.5f, 0.35f, rng.RandfRange(0, 6));
            }
            // A round tower on each corner, one fallen half down.
            var height = i == 1 ? 4 : 9;
            var tower = new StaticBody3D { Position = Floor(a) + Vector3.Up * height / 2f };
            tower.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.7f, Height = height, RadialSegments = 14 }, MaterialOverride = Stone });
            tower.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.6f, Height = height } });
            root.AddChild(tower);
        }
        // The gate's two posts, and the fallen garrison's graves inside.
        foreach (var x in new[] { -3.2f, 3.2f }) Block(root, Floor(c - back * d + side * x) + Vector3.Up * 3.5f, new Vector3(1.8f, 7, 1.8f), Mathf.Atan2(side.X, side.Z), Stone);
        for (var i = 0; i < 10; i++)
            Prop(root, i % 3 == 0 ? "halloween/gravestone.gltf" : "halloween/grave_A.gltf", c + side * (-9 + (i % 5) * 4.5f) + back * (i < 5 ? 4 : 8), 1.2f, Mathf.Atan2(-back.X, -back.Z));
        foreach (var x in new[] { -6f, 6f }) Prop(root, "dungeon/banner_thin_blue.glb", c + back * 13 + side * x, 1.4f, Mathf.Atan2(-back.X, -back.Z));
        Prop(root, "dungeon/sword_shield.glb", c + back * 12 + side * 2, 0.8f, Mathf.Atan2(-back.X, -back.Z));
        Prop(root, "dungeon/chest.glb", c + back * 11, 0.6f, Mathf.Atan2(-back.X, -back.Z), solid: true);
        foreach (var (x, z) in new[] { (-8f, -6f), (8f, -6f), (0f, 9f) }) Lamp(root, Floor(c + side * x + back * z) + Vector3.Up * 3, "#8ac8ff", 1.2f, 12, 0.1f);
        root.AddChild(Haze(c + Vector3.Up * 0.5f, new Color(0.75f, 0.85f, 0.95f, 0.18f), 16, 5, 34));
        return new LairLayout(root, Floor(c + back * 8) + Vector3.Up * 0.3f, Floor(c - back * 6), 5, Floor(c + back * 10) + Vector3.Up * 0.6f);
    }

    // ---- the eyrie ------------------------------------------------------------------------------

    static LairLayout Eyrie(Vector3 c, Node3D root, RandomNumberGenerator rng)
    {
        root.AddChild(Ground(c, 24, new Color(0.3f, 0.29f, 0.27f, 0.8f)));
        // Spires of stone round the crag.
        for (var i = 0; i < 9; i++)
        {
            var a = i * Mathf.Tau / 9 + rng.RandfRange(-0.15f, 0.15f);
            if (i == 0) continue; // the way up
            var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng.RandfRange(15, 19);
            var h = rng.RandfRange(7, 14);
            Rock(root, at, new Vector3(rng.RandfRange(3, 4.5f), h, rng.RandfRange(3, 4.5f)), rng.RandfRange(0, 6), sink: 0.1f);
            Rock(root, at + Vector3.Up * h * 0.7f, new Vector3(2.5f, h * 0.45f, 2.5f), rng.RandfRange(0, 6), sink: -h * 0.7f / (h * 0.45f));
        }
        // The nest: whole trees laid round in rings, twigs, bones, and what it's stolen.
        for (var ring = 0; ring < 3; ring++)
            for (var i = 0; i < 9; i++)
            {
                var a = i * Mathf.Tau / 9 + ring * 0.35f;
                var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (6 - ring * 0.4f);
                var log = Models.Place(root, "ph:dead_trunk.glb", Floor(at) + Vector3.Up * (0.2f + ring * 0.7f), 1.1f, -a, solid: ring == 0);
                log.Rotation = new Vector3(rng.RandfRange(-0.15f, 0.15f), -a + Mathf.Pi / 2, rng.RandfRange(-0.1f, 0.1f));
            }
        for (var i = 0; i < 40; i++)
        {
            var a = rng.RandfRange(0, Mathf.Tau);
            var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng.RandfRange(4.6f, 6.8f);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = rng.RandfRange(1.5f, 3), RadialSegments = 4 }, MaterialOverride = Wood,
                Position = Floor(at) + Vector3.Up * rng.RandfRange(0.5f, 2), Rotation = new Vector3(rng.RandfRange(0.8f, 2.3f), rng.RandfRange(0, 6), 0),
            });
        }
        foreach (var i in Enumerable.Range(0, 6))
            Prop(root, i % 3 == 0 ? "halloween/ribcage.gltf" : $"halloween/bone_{"ABC"[i % 3]}.gltf", c + new Vector3(rng.RandfRange(-3, 3), 0, rng.RandfRange(-3, 3)), 1.4f, rng.RandfRange(0, 6));
        Prop(root, "dungeon/coin_stack_medium.glb", c + new Vector3(1.2f, 0, -0.6f), 0.8f);
        foreach (var i in Enumerable.Range(0, 3))
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.45f, Height = 1.1f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#d8dce4"), Roughness = 0.4f },
                Position = Floor(c + new Vector3(-1.5f + i * 0.9f, 0, 1.4f)) + Vector3.Up * 0.5f,
            });
        // The storm overhead: a dark lid of cloud, and the lightning in it.
        root.AddChild(new CpuParticles3D
        {
            Position = c + Vector3.Up * 30,
            Mesh = new QuadMesh { Size = Vector2.One * 22, Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles } },
            Amount = 30, Lifetime = 20, Preprocess = 20, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(30, 3, 30),
            Direction = Vector3.Right, Spread = 180, Gravity = Vector3.Zero, InitialVelocityMin = 0.3f, InitialVelocityMax = 0.8f,
            ColorRamp = new Gradient { Offsets = [0, 0.2f, 0.8f, 1], Colors = [new Color(0.2f, 0.22f, 0.28f, 0), new Color(0.2f, 0.22f, 0.28f, 0.7f), new Color(0.2f, 0.22f, 0.28f, 0.7f), new Color(0.2f, 0.22f, 0.28f, 0)] },
        });
        Lamp(root, c + Vector3.Up * 24, "#b0d8ff", 1.5f, 50, 0.9f);
        return new LairLayout(root, Floor(c) + Vector3.Up * 0.6f, Floor(c), 9, Floor(c + new Vector3(0, 0, -0.8f)) + Vector3.Up * 0.6f);
    }
}
