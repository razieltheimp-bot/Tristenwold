using Godot;
using Tristinwold.Deviant;

namespace Tristinwold.Game;

/// <summary>
/// Medieval buildings generated from parts, the way they were built: stone footings, timber
/// frames with plaster panels, upper floors jutting over the street, steep roofs with overhangs,
/// chimneys, leaded windows with shutters, plank doors. Every kind comes in variants from a seed,
/// so no two streets look alike. Front doors face +Z.
/// </summary>
public static class Buildings
{
    /// <summary>A built variant: its mesh and the boxes to collide with.</summary>
    sealed record Design(ArrayMesh Mesh, List<(Vector3 Centre, Vector3 Size)> Solids, float Height);

    static readonly Dictionary<string, Design> Cache = [];
    const int Variants = 6;

    /// <summary>The roof a settlement colour suggests.</summary>
    public static Surface RoofFor(string colour) => colour switch { "red" => Surface.ClayTile, "blue" => Surface.Slate, _ => Surface.Shingle };

    /// <summary>Places a building. <paramref name="seed"/> picks the variant; the same seed gives the same building.</summary>
    /// <param name="door">Whether it can be walked into.</param>
    /// <param name="solid">Whether anything can bump into it (not one hung up out of reach).</param>
    public static Node3D Place(Node3D parent, string kind, Vector3 at, float yaw, int seed, Surface roof = Surface.Slate, float scale = 1, bool door = true, bool solid = true)
    {
        var design = Get(kind, Math.Abs(seed) % Variants, roof);
        var body = new StaticBody3D { Position = at, Rotation = new Vector3(0, yaw, 0), Scale = Vector3.One * scale };
        body.AddChild(new MeshInstance3D { Mesh = design.Mesh });
        foreach (var (centre, size) in design.Solids.Where(_ => solid))
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = centre });
        body.SetMeta("height", design.Height * scale);
        body.SetMeta("seed", seed);
        parent.AddChild(body);
        if (door && HasInside(KindFor(kind)))
            Doors.Add((body, KindFor(kind), seed, design.Solids.Max(s => s.Centre.Z + s.Size.Z / 2) * scale));
        return body;
    }

    /// <summary>The buildings you can walk into, and where each one's door is (on the front, +Z, in the middle).</summary>
    static readonly List<(Node3D Body, string Kind, int Seed, float Front)> Doors = [];

    static bool HasInside(string kind) => kind is "house" or "cottage" or "tavern" or "longhall" or "smithy" or "barn" or "church" or "keep" or "tower" or "store";

    /// <summary>A door that isn't on a building made here: its node's +Z points out of it.</summary>
    public static void Door(Node3D at, string kind, int seed) => Doors.Add((at, kind, seed, 0));

    /// <summary>The nearest door within reach, and the spot outside it to come back to.</summary>
    public static (string Kind, int Seed, Vector3 Step)? DoorNear(Vector3 pos)
    {
        Doors.RemoveAll(d => !GodotObject.IsInstanceValid(d.Body));
        (string, int, Vector3)? best = null;
        var nearest = 3.2f;
        foreach (var (body, kind, seed, front) in Doors)
        {
            if (!body.IsInsideTree()) continue;
            var door = body.ToGlobal(new Vector3(0, 0, front));
            var flat = new Vector2(door.X - pos.X, door.Z - pos.Z).Length();
            if (flat >= nearest || Mathf.Abs(door.Y - pos.Y) > 4) continue;
            nearest = flat;
            best = (kind, seed, body.ToGlobal(new Vector3(0, 0, front + 1.6f)));
        }
        return best;
    }

    /// <summary>Boards a building's door up: nobody goes in now.</summary>
    public static void Shut(Node3D building) => Doors.RemoveAll(d => d.Body == building);

    static StandardMaterial3D? _mould;

    /// <summary>Black-green mould creeping over everything, in blotches, as it does on a place nobody airs.</summary>
    static StandardMaterial3D MouldCoat => _mould ??= new StandardMaterial3D
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1, Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = Vector3.One * 0.12f,
        AlbedoTexture = new NoiseTexture2D
        {
            Width = 256, Height = 256, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.03f, FractalOctaves = 4 },
            ColorRamp = new Gradient { Offsets = [0.42f, 0.6f], Colors = [new Color(0.16f, 0.2f, 0.12f, 0), new Color(0.11f, 0.15f, 0.09f, 0.92f)] },
        },
    };

    /// <summary>Lets the mould into a building.</summary>
    public static void Mould(Node3D building)
    {
        foreach (var mesh in building.GetChildren().OfType<MeshInstance3D>()) mesh.MaterialOverlay = MouldCoat;
    }

    public static float HeightOf(Node3D building) => building.HasMeta("height") ? (float)building.GetMeta("height") : 8;

    /// <summary>Which kind of building suits what a game asked for (the old KayKit names still work).</summary>
    public static string KindFor(string asked) => asked switch
    {
        "home_A" or "home" => "house",
        "home_B" => "house",
        "lumbermill" => "barn",
        "blacksmith" => "smithy",
        "barracks" => "longhall",
        "castle" => "keep",
        "tower_A" or "tower_B" => "tower",
        _ => asked,
    };

    static Design Get(string kind, int variant, Surface roof)
    {
        var key = $"{kind}/{variant}/{roof}";
        if (Cache.TryGetValue(key, out var d)) return d;
        var b = new Builder();
        if (kind.StartsWith("disc:"))
        {
            // A raised round platform, paved on top: "disc:radius:height".
            var parts = kind.Split(':');
            var (r, h) = (float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            b.Round(r, -1, h, Surface.Stone, 64);
            b.Solid(new Vector3(0, (h - 1) / 2, 0), new Vector3(r * 1.41f, h + 1, r * 1.41f)); // (the city adds its own round collision)
            return Cache[key] = b.Finish();
        }
        var rng = new Rng((ulong)Math.Abs(Tristinwold.World.WorldGen.StableHash(kind) % 100_000) + (ulong)variant * 7919 + 17);
        switch (KindFor(kind))
        {
            case "house": House(b, rng, roof); break;
            case "cottage": Cottage(b, rng, roof == Surface.Shingle ? Surface.Thatch : roof); break;
            case "tavern": Tavern(b, rng, roof); break;
            case "longhall": Longhall(b, rng, roof); break;
            case "barn": Barn(b, rng); break;
            case "smithy": Smithy(b, rng, roof); break;
            case "market": Market(b, rng, roof); break;
            case "tower": Tower(b, rng, roof); break;
            case "church": Church(b, rng); break;
            case "keep": Keep(b, rng); break;
            case "windmill": Windmill(b, rng); break;
            case "well": Well(b, roof); break;
            case "wall": WallSegment(b, 10, 7, 2.4f); break;
            case "wall_low": WallSegment(b, 8, 5, 2); break;
            case "gate": Gatehouse(b); break;
            case "lighthouse": Lighthouse(b); break;
            case "ruin": Ruin(b, rng); break;
            case "mine": MineEntrance(b); break;
            case "mine_portal": MinePortal(b); break;
            case "store": Store(b, roof); break;
            case "lantern": Lantern(b); break;
            case "pier": Pier(b); break;
            case "boardwalk": Boardwalk(b); break;
            case "steam_wagon": SteamWagon(b, roof); break;
            default: House(b, rng, roof); break;
        }
        return Cache[key] = b.Finish();
    }

    // ---- the kinds of building ------------------------------------------------------------------

    static void House(Builder b, Rng rng, Surface roof)
    {
        var w = 5.6f + (float)rng.NextDouble() * 2.2f;
        var d = 6.2f + (float)rng.NextDouble() * 2.4f;
        var floors = rng.Chance(0.75) ? 2 : 1;
        var stoneGround = rng.Chance(0.55);
        const float fh = 2.9f;
        b.Plinth(w, d);
        b.Floor(w, d, 0.35f, fh, stoneGround ? Surface.Stone : Surface.Plaster, timbered: !stoneGround, door: true, rng);
        var jetty = floors == 2 && rng.Chance(0.7) ? 0.45f : 0;
        if (floors == 2) b.Floor(w + 2 * jetty, d + 2 * jetty, 0.35f + fh, fh, Surface.Plaster, timbered: true, door: false, rng, jettyFrom: jetty);
        var top = 0.35f + fh * floors;
        var acrossFront = rng.Chance(0.5);
        var roofTop = b.Gable(w + 2 * jetty, d + 2 * jetty, top, 0.55f + (float)rng.NextDouble() * 0.15f, roof, Surface.Plaster, acrossFront);
        if (rng.Chance(0.75)) b.Chimney(acrossFront ? new Vector3(w * 0.3f, 0, -d * 0.15f) : new Vector3(-w * 0.15f, 0, d * 0.25f), roofTop + 0.9f);
        b.Solid(new Vector3(0, top / 2, 0), new Vector3(w, top, d));
    }

    static void Cottage(Builder b, Rng rng, Surface roof)
    {
        var w = 5f + (float)rng.NextDouble() * 1.5f;
        var d = 7f + (float)rng.NextDouble() * 2.5f;
        b.Plinth(w, d);
        b.Floor(w, d, 0.3f, 2.7f, rng.Chance(0.6) ? Surface.Stone : Surface.Plaster, timbered: rng.Chance(0.5), door: true, rng);
        var roofTop = b.Gable(w, d, 3f, 0.75f, roof, Surface.Plaster, acrossFront: rng.Chance(0.5), overhang: 0.6f);
        if (rng.Chance(0.6)) b.Chimney(new Vector3(-w * 0.32f, 0, 0), roofTop + 0.7f);
        b.Solid(new Vector3(0, 1.5f, 0), new Vector3(w, 3, d));
    }

    static void Tavern(Builder b, Rng rng, Surface roof)
    {
        const float w = 10.5f, d = 12, fh = 3f;
        b.Plinth(w, d);
        b.Floor(w, d, 0.4f, fh, Surface.Stone, timbered: false, door: true, rng, doorWidth: 1.6f);
        b.Floor(w + 0.9f, d + 0.9f, 0.4f + fh, fh, Surface.Plaster, timbered: true, door: false, rng, jettyFrom: 0.45f);
        b.Floor(w + 0.9f, d + 0.9f, 0.4f + 2 * fh, fh * 0.9f, Surface.Plaster, timbered: true, door: false, rng);
        var top = 0.4f + fh * 2.9f;
        var roofTop = b.Gable(w + 0.9f, d + 0.9f, top, 0.62f, roof, Surface.Plaster, acrossFront: true, overhang: 0.7f);
        b.Chimney(new Vector3(w * 0.36f, 0, -d * 0.2f), roofTop + 1);
        b.Chimney(new Vector3(-w * 0.36f, 0, -d * 0.2f), roofTop + 1);
        // A sign on a bracket over the door.
        b.Beam(new Vector3(0.9f, 3.6f, d / 2 + 0.1f), new Vector3(0.9f, 3.6f, d / 2 + 1.5f), 0.12f, Surface.Timber);
        b.Box(Surface.Planks, new Vector3(0.6f, 2.6f, d / 2 + 1.1f), new Vector3(0.66f, 3.45f, d / 2 + 1.9f));
        b.Solid(new Vector3(0, top / 2, 0), new Vector3(w, top, d));
    }

    static void Longhall(Builder b, Rng rng, Surface roof)
    {
        const float w = 8.5f, d = 17;
        b.Plinth(w, d);
        b.Floor(w, d, 0.4f, 4.2f, Surface.Stone, timbered: false, door: true, rng, doorWidth: 1.8f);
        var roofTop = b.Gable(w, d, 4.6f, 0.8f, roof, Surface.Stone, acrossFront: false, overhang: 0.6f);
        b.Chimney(new Vector3(0, 0, -d * 0.3f), roofTop + 0.8f);
        b.Solid(new Vector3(0, 2.3f, 0), new Vector3(w, 4.6f, d));
    }

    static void Barn(Builder b, Rng rng)
    {
        const float w = 8, d = 12;
        b.Plinth(w, d, 0.25f);
        b.Floor(w, d, 0.25f, 4.2f, Surface.Planks, timbered: true, door: true, rng, doorWidth: 3f, windows: false);
        b.Gable(w, d, 4.45f, 0.78f, Surface.Thatch, Surface.Planks, acrossFront: false, overhang: 0.6f);
        b.Solid(new Vector3(0, 2.2f, 0), new Vector3(w, 4.4f, d));
    }

    static void Smithy(Builder b, Rng rng, Surface roof)
    {
        Cottage(b, rng, roof);
        // An open lean-to forge on posts beside it.
        foreach (var x in new[] { 3.4f, 6.4f })
            foreach (var z in new[] { -2.5f, 2.5f })
                b.Beam(new Vector3(x, 0, z), new Vector3(x, 2.6f, z), 0.18f, Surface.Timber);
        b.Slope(new Vector3(2.6f, 3.2f, -3.2f), new Vector3(7f, 2.5f, -3.2f), 6.4f, roof);
        b.Box(Surface.DarkStone, new Vector3(4.4f, 0, -1), new Vector3(5.6f, 1, 1));
        b.Chimney(new Vector3(5, 0, -0.8f), 4.6f, 0.9f);
    }

    static void Market(Builder b, Rng rng, Surface roof)
    {
        const float w = 9, d = 7, fh = 3.2f;
        // Open stalls on timber posts below, a hall above.
        for (var i = 0; i <= 4; i++)
            foreach (var z in new[] { -d / 2 + 0.2f, d / 2 - 0.2f })
                b.Beam(new Vector3(-w / 2 + 0.2f + i * (w - 0.4f) / 4, 0, z), new Vector3(-w / 2 + 0.2f + i * (w - 0.4f) / 4, fh, z), 0.28f, Surface.Timber);
        b.Box(Surface.Stone, new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0.2f, d / 2));
        b.Box(Surface.Planks, new Vector3(-w / 2 - 0.4f, fh, -d / 2 - 0.4f), new Vector3(w / 2 + 0.4f, fh + 0.25f, d / 2 + 0.4f));
        b.Floor(w + 0.8f, d + 0.8f, fh + 0.25f, 2.8f, Surface.Plaster, timbered: true, door: false, rng);
        b.Gable(w + 0.8f, d + 0.8f, fh + 3.05f, 0.6f, roof, Surface.Plaster, acrossFront: true);
        b.Solid(new Vector3(0, 4.5f, 0), new Vector3(w, 3, d));
    }

    static void Tower(Builder b, Rng rng, Surface roof)
    {
        var s = 5.5f + (float)rng.NextDouble();
        var h = 15f + (float)rng.NextDouble() * 5;
        b.Box(Surface.Stone, new Vector3(-s / 2 - 0.3f, 0, -s / 2 - 0.3f), new Vector3(s / 2 + 0.3f, 1.2f, s / 2 + 0.3f));
        b.Walls(s, s, 1.2f, h - 1.2f, Surface.Stone);
        for (var y = 4f; y < h - 2; y += 4) b.SlitWindows(s, s, y);
        b.Door(0, 1.2f, s / 2, 1.3f, 2.4f);
        if (rng.Chance(0.5)) b.Battlements(s, s, h);
        else b.Pyramid(s + 1, h, h + s * 1.1f, roof);
        b.Solid(new Vector3(0, h / 2, 0), new Vector3(s, h, s));
    }

    static void Church(Builder b, Rng rng)
    {
        const float w = 9, d = 18;
        b.Plinth(w, d, 0.5f);
        b.Walls(w, d, 0.5f, 8.5f, Surface.Stone);
        b.TallWindows(w, d, 2.5f, 4.5f);
        b.Gable(w, d, 9f, 0.95f, Surface.Slate, Surface.Stone, acrossFront: false, overhang: 0.5f);
        // The bell tower at the front, with a spire.
        const float t = 5;
        b.Push(new Vector3(0, 0, d / 2 + t / 2 - 0.5f));
        b.Walls(t, t, 0, 18, Surface.Stone);
        b.SlitWindows(t, t, 13);
        b.Door(0, 0.2f, t / 2, 1.8f, 3.2f);
        b.Pyramid(t + 0.6f, 18, 28, Surface.Slate);
        b.Pop();
        b.Solid(new Vector3(0, 4.5f, 0), new Vector3(w, 9, d));
        b.Solid(new Vector3(0, 9, d / 2 + t / 2 - 0.5f), new Vector3(t, 18, t));
    }

    static void Keep(Builder b, Rng rng)
    {
        const float s = 14, h = 17;
        b.Box(Surface.DarkStone, new Vector3(-s / 2 - 0.6f, 0, -s / 2 - 0.6f), new Vector3(s / 2 + 0.6f, 1.6f, s / 2 + 0.6f));
        b.Walls(s, s, 1.6f, h - 1.6f, Surface.Stone);
        foreach (var y in new[] { 6f, 10.5f, 14f }) b.SlitWindows(s, s, y, 3);
        b.Door(0, 1.6f, s / 2, 2.6f, 4f);
        b.Battlements(s, s, h);
        foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            b.Push(new Vector3(x * s / 2, 0, z * s / 2));
            b.Round(3.2f, 0, h + 4, Surface.Stone, 14);
            b.Cone(3.8f, h + 4, h + 10, Surface.Slate, 14);
            b.Pop();
        }
        b.Solid(new Vector3(0, h / 2, 0), new Vector3(s + 4, h, s + 4));
    }

    static void Windmill(Builder b, Rng rng)
    {
        b.Round(3f, 0, 9, Surface.Stone, 12, topRadius: 2.4f);
        b.Cone(3f, 9, 12.5f, Surface.Thatch, 12);
        b.Door(0, 0, 2.95f, 1.1f, 2.1f);
        // Four sails on a hub, facing the front.
        var hub = new Vector3(0, 9.5f, 3.2f);
        b.Box(Surface.Timber, hub - new Vector3(0.3f, 0.3f, 0.3f), hub + new Vector3(0.3f, 0.3f, 0.4f));
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Pi / 2 + 0.4f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            b.Beam(hub + new Vector3(0, 0, 0.35f), hub + new Vector3(0, 0, 0.35f) + dir * 6.5f, 0.14f, Surface.Timber);
            var side = new Vector3(-dir.Y, dir.X, 0) * 0.9f;
            b.Panel(hub + new Vector3(0, 0, 0.45f) + dir * 1.5f, dir * 5f, side, Surface.Planks);
        }
        b.Solid(new Vector3(0, 4.5f, 0), new Vector3(5.5f, 9, 5.5f));
    }

    static void Well(Builder b, Surface roof)
    {
        b.Round(1.1f, 0, 0.9f, Surface.Stone, 12);
        foreach (var x in new[] { -1f, 1f }) b.Beam(new Vector3(x, 0.9f, 0), new Vector3(x, 2.6f, 0), 0.14f, Surface.Timber);
        b.Beam(new Vector3(-1.1f, 2.4f, 0), new Vector3(1.1f, 2.4f, 0), 0.1f, Surface.Timber);
        b.Gable(2.2f, 1.6f, 2.6f, 0.7f, roof, Surface.Timber, acrossFront: false, overhang: 0.25f, gableWalls: false);
        b.Solid(new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.9f, 2.2f));
    }

    /// <summary>A length of city wall along X, with a walkway and battlements; centred on the origin.</summary>
    static void WallSegment(Builder b, float length, float height, float thickness)
    {
        b.Box(Surface.Stone, new Vector3(-length / 2, -1, -thickness / 2), new Vector3(length / 2, height, thickness / 2));
        for (var x = -length / 2 + 0.4f; x < length / 2 - 0.4f; x += 1.6f)
            b.Box(Surface.Stone, new Vector3(x, height, thickness / 2 - 0.5f), new Vector3(x + 0.8f, height + 1.1f, thickness / 2));
        b.Solid(new Vector3(0, height / 2, 0), new Vector3(length, height + 1, thickness));
    }

    static void Gatehouse(Builder b)
    {
        foreach (var x in new[] { -4.2f, 4.2f })
        {
            b.Push(new Vector3(x, 0, 0));
            b.Walls(3.6f, 4.4f, -1, 11, Surface.Stone);
            b.SlitWindows(3.6f, 4.4f, 6.5f, 1);
            b.Battlements(3.6f, 4.4f, 10);
            b.Pop();
            b.Solid(new Vector3(x, 5, 0), new Vector3(3.6f, 10, 4.4f));
        }
        b.Box(Surface.Stone, new Vector3(-2.4f, 5.2f, -2), new Vector3(2.4f, 9, 2));
        b.Battlements(4.8f, 4, 9);
    }

    static void Lighthouse(Builder b)
    {
        b.Round(3.4f, 0, 2, Surface.DarkStone, 16);
        b.Round(2.6f, 2, 20, Surface.Plaster, 16, topRadius: 2.1f);
        b.Round(2.3f, 20, 20.4f, Surface.DarkStone, 16);
        b.Round(1.8f, 20.4f, 23, Surface.Glass, 12);
        b.Cone(2.3f, 23, 25.5f, Surface.Slate, 12);
        b.Door(0, 2, 2.55f, 1.1f, 2.2f);
        b.Solid(new Vector3(0, 11, 0), new Vector3(5, 22, 5));
    }

    static void Ruin(Builder b, Rng rng)
    {
        var w = 6f + (float)rng.NextDouble() * 2;
        var d = 6f + (float)rng.NextDouble() * 3;
        foreach (var (x0, z0, x1, z1) in new[] { (-w / 2, -d / 2, w / 2, -d / 2 + 0.6f), (-w / 2, -d / 2, -w / 2 + 0.6f, d / 2), (w / 2 - 0.6f, -d / 2, w / 2, d * 0.1f) })
        {
            // Broken walls: uneven heights along their length.
            var steps = 4;
            for (var i = 0; i < steps; i++)
            {
                var t0 = i / (float)steps;
                var t1 = (i + 1) / (float)steps;
                var h = 0.6f + (float)rng.NextDouble() * 3.2f;
                b.Box(Surface.Stone, new Vector3(Mathf.Lerp(x0, x1, t0), 0, Mathf.Lerp(z0, z1, t0)), new Vector3(Mathf.Lerp(x0, x1, t1) + (x1 - x0 < 1 ? 0.6f : 0), h, Mathf.Lerp(z0, z1, t1) + (z1 - z0 < 1 ? 0.6f : 0)));
            }
        }
        b.Solid(new Vector3(0, 1, -d / 2 + 0.3f), new Vector3(w, 2, 0.6f));
    }

    /// <summary>A street lantern: a timber post, an iron arm, and a glazed lamp hanging from it.</summary>
    static void Lantern(Builder b)
    {
        b.Box(Surface.DarkStone, new Vector3(-0.3f, 0, -0.3f), new Vector3(0.3f, 0.35f, 0.3f));
        b.Beam(new Vector3(0, 0.3f, 0), new Vector3(0, 3.1f, 0), 0.18f, Surface.Timber);
        b.Beam(new Vector3(0, 2.9f, 0), new Vector3(0.75f, 2.9f, 0), 0.07f, Surface.DarkStone);
        b.Beam(new Vector3(0, 2.55f, 0), new Vector3(0.55f, 2.88f, 0), 0.05f, Surface.DarkStone);
        b.Box(Surface.Glass, new Vector3(0.58f, 2.3f, -0.15f), new Vector3(0.9f, 2.72f, 0.15f));
        b.Box(Surface.DarkStone, new Vector3(0.54f, 2.72f, -0.19f), new Vector3(0.94f, 2.8f, 0.19f));
        b.Solid(new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3, 0.3f));
    }

    /// <summary>A plank pier on timber piles, 40 m along X and 5 m wide, deck at height 0.</summary>
    static void Pier(Builder b)
    {
        b.Box(Surface.Planks, new Vector3(-24, -0.35f, -2.5f), new Vector3(24, 0, 2.5f));
        for (var x = -23f; x <= 23; x += 4.75f)
            foreach (var z in new[] { -2.3f, 2.3f })
                b.Beam(new Vector3(x, -4.5f, z), new Vector3(x, x < -3 ? 0 : 0.6f, z), 0.32f, Surface.Timber);
        b.Beam(new Vector3(-3, 0.55f, 2.4f), new Vector3(24, 0.55f, 2.4f), 0.14f, Surface.Timber); // and none where the boardwalk crosses
        b.Solid(new Vector3(0, -0.2f, 0), new Vector3(48, 0.4f, 5));
    }

    /// <summary>A timber-and-plaster house on a wagon bed, its wide plank roof overhanging, a brick chimney up through it, a driver's bench out front, and a railed porch at the back with a ladder down.</summary>
    static void SteamWagon(Builder b, Surface roof)
    {
        const float wide = 3.3f, floor = 1.45f, eaves = 4.1f;
        b.Box(Surface.Timber, new Vector3(-1.85f, floor - 0.28f, -3.9f), new Vector3(1.85f, floor, 3.9f)); // the bed
        b.Box(Surface.Plaster, new Vector3(-wide / 2, floor, -2.6f), new Vector3(wide / 2, eaves, 2.6f));
        foreach (var x in new[] { -wide / 2 - 0.02f, wide / 2 + 0.02f })
        {
            foreach (var z in new[] { -2.6f, -0.9f, 0.9f, 2.6f })
                b.Beam(new Vector3(x, floor, z), new Vector3(x, eaves, z), 0.16f, Surface.Timber);
            b.Beam(new Vector3(x, floor + 1.25f, -2.6f), new Vector3(x, floor + 1.25f, 2.6f), 0.14f, Surface.Timber);
            b.Beam(new Vector3(x, eaves - 0.1f, -2.6f), new Vector3(x, eaves - 0.1f, 2.6f), 0.14f, Surface.Timber);
        }
        foreach (var side in new[] { -1f, 1f })
            b.Box(Surface.Glass, new Vector3(side * wide / 2 - 0.05f, floor + 1.4f, -0.5f), new Vector3(side * wide / 2 + 0.05f, floor + 2.1f, 0.5f));
        b.Box(Surface.Planks, new Vector3(-0.55f, floor, 2.6f), new Vector3(0.55f, floor + 2.1f, 2.68f)); // the back door
        b.Gable(wide, 5.2f, eaves, 0.45f, roof == Surface.ClayTile ? Surface.Shingle : Surface.Planks, Surface.Plaster, acrossFront: false, overhang: 0.75f);
        b.Chimney(new Vector3(0.9f, 0, -1.6f), 7.1f, 0.8f);
        b.Box(Surface.Timber, new Vector3(-1.1f, floor + 0.35f, -3.8f), new Vector3(1.1f, floor + 0.5f, -3.1f)); // the bench
        foreach (var x in new[] { -1.8f, 1.8f })
        {
            b.Beam(new Vector3(x, floor, 3.85f), new Vector3(x, floor + 1, 3.85f), 0.1f, Surface.Timber);
            b.Beam(new Vector3(x, floor, 2.7f), new Vector3(x, floor + 1, 2.7f), 0.1f, Surface.Timber);
            b.Beam(new Vector3(x, floor + 1, 2.7f), new Vector3(x, floor + 1, 3.85f), 0.08f, Surface.Timber);
        }
        b.Beam(new Vector3(-1.8f, floor + 1, 3.85f), new Vector3(-0.5f, floor + 1, 3.85f), 0.08f, Surface.Timber);
        b.Beam(new Vector3(0.5f, floor + 1, 3.85f), new Vector3(1.8f, floor + 1, 3.85f), 0.08f, Surface.Timber);
        foreach (var x in new[] { -0.4f, 0.4f }) b.Beam(new Vector3(x, 0, 4.6f), new Vector3(x, floor, 3.9f), 0.07f, Surface.Timber); // the ladder
        for (var y = 0.3f; y < floor; y += 0.35f) b.Beam(new Vector3(-0.4f, y, 4.6f - y * 0.48f), new Vector3(0.4f, y, 4.6f - y * 0.48f), 0.05f, Surface.Timber);
    }

    /// <summary>The walk across the landward ends of the piers: as long as the five of them are wide, on piles.</summary>
    static void Boardwalk(Builder b)
    {
        b.Box(Surface.Planks, new Vector3(-2.6f, -0.35f, -33), new Vector3(2.6f, 0, 33));
        for (var z = -32f; z <= 32; z += 6.4f)
            foreach (var x in new[] { -2.4f, 2.4f })
                b.Beam(new Vector3(x, -4.5f, z), new Vector3(x, 0, z), 0.32f, Surface.Timber);
        b.Solid(new Vector3(0, -0.2f, 0), new Vector3(5.2f, 0.4f, 66));
    }

    static void MineEntrance(Builder b)
    {
        b.Box(Surface.DarkStone, new Vector3(-6, 0, -9), new Vector3(6, 7, -1.2f));
        b.Box(Surface.Glass, new Vector3(-1.4f, 0, -1.25f), new Vector3(1.4f, 2.8f, -1.2f)); // the dark way in
        foreach (var x in new[] { -1.6f, 1.6f }) b.Beam(new Vector3(x, 0, -1), new Vector3(x, 3.1f, -1), 0.3f, Surface.Timber);
        b.Beam(new Vector3(-2f, 3.1f, -1), new Vector3(2f, 3.1f, -1), 0.32f, Surface.Timber);
        b.Solid(new Vector3(0, 3.5f, -5), new Vector3(12, 7, 7.6f));
    }

    /// <summary>A shop with its whole front open to the street: plank floor, three walls, a roof, and room to walk in to the counter.</summary>
    static void Store(Builder b, Surface roof)
    {
        const float w = 6.8f, d = 6.4f, h = 3.2f;
        b.Box(Surface.Planks, new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0.06f, d / 2));
        b.Box(Surface.Plaster, new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, h, -d / 2 + 0.2f));
        b.Solid(new Vector3(0, h / 2, -d / 2 + 0.1f), new Vector3(w, h, 0.2f));
        foreach (var side in new[] { -1f, 1f })
        {
            b.Box(Surface.Plaster, new Vector3(side < 0 ? -w / 2 : w / 2 - 0.2f, 0, -d / 2), new Vector3(side < 0 ? -w / 2 + 0.2f : w / 2, h, d / 2));
            b.Solid(new Vector3(side * (w / 2 - 0.1f), h / 2, 0), new Vector3(0.2f, h, d));
            b.Beam(new Vector3(side * (w / 2 - 0.1f), 0, d / 2), new Vector3(side * (w / 2 - 0.1f), h, d / 2), 0.26f, Surface.Timber);
            b.Beam(new Vector3(side * (w / 2 - 0.1f), 0, -d / 2 + 0.1f), new Vector3(side * (w / 2 - 0.1f), h, -d / 2 + 0.1f), 0.26f, Surface.Timber);
        }
        b.Beam(new Vector3(-w / 2, h, d / 2), new Vector3(w / 2, h, d / 2), 0.28f, Surface.Timber);
        b.Gable(w, d, h, 0.6f, roof, Surface.Plaster, acrossFront: true, overhang: 0.6f);
    }

    /// <summary>A mine's open doorway, to be set into a hillside: timber sets holding back the rock, and the way in between them.</summary>
    static void MinePortal(Builder b)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            b.Box(Surface.DarkStone, new Vector3(side < 0 ? -4.4f : 1.6f, 0, -5.4f), new Vector3(side < 0 ? -1.6f : 4.4f, 7, -0.1f));
            b.Solid(new Vector3(side * 3, 3.5f, -2.75f), new Vector3(2.8f, 7, 5.3f));
            foreach (var z in new[] { -0.2f, -2.2f, -4.2f }) b.Beam(new Vector3(side * 1.55f, 0, z), new Vector3(side * 1.55f, 3.1f, z), 0.3f, Surface.Timber);
        }
        b.Box(Surface.DarkStone, new Vector3(-1.6f, 3.2f, -5.4f), new Vector3(1.6f, 7, -0.1f));
        foreach (var z in new[] { -0.2f, -2.2f, -4.2f }) b.Beam(new Vector3(-2f, 3.1f, z), new Vector3(2f, 3.1f, z), 0.32f, Surface.Timber);
        b.Solid(new Vector3(0, 5.1f, -2.75f), new Vector3(3.2f, 3.8f, 5.3f));
        b.Solid(new Vector3(0, 3.5f, -5.6f), new Vector3(3.2f, 7, 0.4f));
    }

    // ---- the builder ---------------------------------------------------------------------------

    /// <summary>Collects faces per material, in a stack of local frames, into one mesh.</summary>
    sealed class Builder
    {
        readonly Dictionary<Surface, SurfaceTool> _tools = [];
        readonly List<(Vector3, Vector3)> _solids = [];
        readonly Stack<Vector3> _offsets = new();
        Vector3 _offset;
        float _height;

        public void Push(Vector3 by) { _offsets.Push(_offset); _offset += by; }
        public void Pop() => _offset = _offsets.Pop();

        public void Solid(Vector3 centre, Vector3 size) => _solids.Add((centre, size));

        SurfaceTool Tool(Surface s)
        {
            if (_tools.TryGetValue(s, out var t)) return t;
            t = new SurfaceTool();
            t.Begin(Mesh.PrimitiveType.Triangles);
            return _tools[s] = t;
        }

        /// <summary>One flat face from <paramref name="o"/> along <paramref name="u"/> and <paramref name="v"/>, facing <paramref name="outward"/>. UVs in metres.</summary>
        public void Quad(Surface s, Vector3 o, Vector3 u, Vector3 v, Vector3 outward, Vector2 uv0 = default)
        {
            var n = u.Cross(v).Normalized();
            if (n.Dot(outward) < 0) n = -n;
            Vector3[] p = [o, o + u, o + u + v, o + v];
            var (lu, lv) = (u.Length(), v.Length());
            Vector2[] uv = [uv0, uv0 + new Vector2(lu, 0), uv0 + new Vector2(lu, lv), uv0 + new Vector2(0, lv)];
            // Godot's front faces wind clockwise as seen from outside.
            int[] order = u.Cross(v).Dot(n) > 0 ? [0, 2, 1, 0, 3, 2] : [0, 1, 2, 0, 2, 3];
            var t = Tool(s);
            foreach (var i in order)
            {
                t.SetNormal(n);
                t.SetUV(uv[i]);
                t.AddVertex(p[i] + _offset);
                _height = Math.Max(_height, p[i].Y + _offset.Y);
            }
        }

        public void Tri(Surface s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Func<Vector3, Vector2> uv)
        {
            var n = (b - a).Cross(c - a).Normalized();
            if (n.Dot(outward) < 0) n = -n;
            Vector3[] p = (b - a).Cross(c - a).Dot(n) > 0 ? [a, c, b] : [a, b, c];
            var t = Tool(s);
            foreach (var q in p)
            {
                t.SetNormal(n);
                t.SetUV(uv(q));
                t.AddVertex(q + _offset);
                _height = Math.Max(_height, q.Y + _offset.Y);
            }
        }

        /// <summary>An axis-aligned box between two corners.</summary>
        public void Box(Surface s, Vector3 min, Vector3 max)
        {
            var size = max - min;
            Quad(s, new Vector3(min.X, min.Y, max.Z), new Vector3(size.X, 0, 0), new Vector3(0, size.Y, 0), Vector3.Back, new Vector2(min.X, min.Y));
            Quad(s, new Vector3(max.X, min.Y, min.Z), new Vector3(-size.X, 0, 0), new Vector3(0, size.Y, 0), Vector3.Forward, new Vector2(min.X, min.Y));
            Quad(s, new Vector3(max.X, min.Y, max.Z), new Vector3(0, 0, -size.Z), new Vector3(0, size.Y, 0), Vector3.Right, new Vector2(min.Z, min.Y));
            Quad(s, new Vector3(min.X, min.Y, min.Z), new Vector3(0, 0, size.Z), new Vector3(0, size.Y, 0), Vector3.Left, new Vector2(min.Z, min.Y));
            Quad(s, new Vector3(min.X, max.Y, min.Z), new Vector3(size.X, 0, 0), new Vector3(0, 0, size.Z), Vector3.Up, new Vector2(min.X, min.Z));
            Quad(s, new Vector3(min.X, min.Y, min.Z), new Vector3(size.X, 0, 0), new Vector3(0, 0, size.Z), Vector3.Down, new Vector2(min.X, min.Z));
        }

        /// <summary>A square beam from one point to another (posts, braces, rails).</summary>
        public void Beam(Vector3 from, Vector3 to, float thickness, Surface s)
        {
            var along = to - from;
            var len = along.Length();
            if (len < 0.01f) return;
            var dir = along / len;
            var side = Math.Abs(dir.Y) > 0.9f ? Vector3.Right : dir.Cross(Vector3.Up).Normalized();
            var up = side.Cross(dir).Normalized();
            var (a, c) = (side * thickness / 2, up * thickness / 2);
            Vector3[] corners = [-a - c, a - c, a + c, -a + c];
            for (var i = 0; i < 4; i++)
            {
                var p0 = from + corners[i];
                var p1 = from + corners[(i + 1) % 4];
                var mid = (corners[i] + corners[(i + 1) % 4]) / 2;
                Quad(s, p0, p1 - p0, along, mid, new Vector2(0, 0));
            }
            Quad(s, from - a - c, a * 2, c * 2, -dir);
            Quad(s, to - a - c, a * 2, c * 2, dir);
        }

        /// <summary>A flat board: from a corner along two edges, with a little thickness.</summary>
        public void Panel(Vector3 o, Vector3 u, Vector3 v, Surface s)
        {
            var n = u.Cross(v).Normalized() * 0.05f;
            Quad(s, o + n, u, v, n);
            Quad(s, o - n, u, v, -n);
        }

        public void Plinth(float w, float d, float h = 0.35f) =>
            Box(Surface.DarkStone, new Vector3(-w / 2 - 0.12f, -0.8f, -d / 2 - 0.12f), new Vector3(w / 2 + 0.12f, h, d / 2 + 0.12f));

        /// <summary>The four outer walls of a rectangle, from <paramref name="y0"/> up by <paramref name="h"/>.</summary>
        public void Walls(float w, float d, float y0, float h, Surface s)
        {
            Quad(s, new Vector3(-w / 2, y0, d / 2), new Vector3(w, 0, 0), new Vector3(0, h, 0), Vector3.Back, new Vector2(0, y0));
            Quad(s, new Vector3(w / 2, y0, -d / 2), new Vector3(-w, 0, 0), new Vector3(0, h, 0), Vector3.Forward, new Vector2(0, y0));
            Quad(s, new Vector3(w / 2, y0, d / 2), new Vector3(0, 0, -d), new Vector3(0, h, 0), Vector3.Right, new Vector2(0, y0));
            Quad(s, new Vector3(-w / 2, y0, -d / 2), new Vector3(0, 0, d), new Vector3(0, h, 0), Vector3.Left, new Vector2(0, y0));
        }

        /// <summary>
        /// One storey: walls, and on plaster the timber frame (sill, plate, posts, a mid rail, corner
        /// braces), windows with shutters, and maybe the front door. A jettied storey gets beam ends
        /// and a boarded underside where it overhangs the one below.
        /// </summary>
        public void Floor(float w, float d, float y0, float h, Surface wall, bool timbered, bool door, Rng rng,
            float jettyFrom = 0, float doorWidth = 1.15f, bool windows = true)
        {
            Walls(w, d, y0, h, wall);
            if (jettyFrom > 0)
            {
                Quad(Surface.Planks, new Vector3(-w / 2, y0, -d / 2), new Vector3(w, 0, 0), new Vector3(0, 0, d), Vector3.Down);
                for (var x = -w / 2 + 0.3f; x < w / 2; x += 0.7f)
                    Box(Surface.Timber, new Vector3(x - 0.09f, y0 - 0.22f, d / 2 - jettyFrom - 0.1f), new Vector3(x + 0.09f, y0, d / 2 + 0.05f));
            }
            for (var side = 0; side < 4; side++)
            {
                var (len, depth) = side % 2 == 0 ? (w, d) : (d, w);
                var yaw = side * Mathf.Pi / 2;
                var bays = Math.Max(2, (int)Math.Round(len / 1.7f));
                var bay = len / bays;
                var doorBay = door && side == 0 ? bays / 2 : -1;
                for (var i = 0; i < bays; i++)
                {
                    var x0 = -len / 2 + i * bay;
                    var cx = x0 + bay / 2;
                    if (i == doorBay) Door(cx, y0, depth / 2, Math.Min(doorWidth, bay - 0.3f), Math.Min(2.2f, h - 0.4f), yaw);
                    else if (windows && (i % 2 == 1 || bays <= 3) && i != doorBay)
                        Window(cx, y0 + Math.Min(0.95f, h * 0.33f), depth / 2, Math.Min(0.85f, bay - 0.5f), Math.Min(1.15f, h * 0.45f), yaw, rng.Chance(0.6), !timbered);
                    else if (timbered && (i == 0 || i == bays - 1))
                    {
                        // Corner braces in the end bays.
                        var (fromX, toX) = i == 0 ? (x0 + 0.1f, x0 + bay - 0.15f) : (x0 + bay - 0.1f, x0 + 0.15f);
                        Beam(Rot(new Vector3(fromX, y0 + 0.15f, depth / 2 + 0.05f), yaw), Rot(new Vector3(toX, y0 + h - 0.2f, depth / 2 + 0.05f), yaw), 0.15f, Surface.Timber);
                    }
                    if (timbered && i > 0)
                        Beam(Rot(new Vector3(x0, y0, depth / 2 + 0.05f), yaw), Rot(new Vector3(x0, y0 + h, depth / 2 + 0.05f), yaw), 0.17f, Surface.Timber);
                }
                if (timbered)
                {
                    foreach (var y in new[] { y0 + 0.08f, y0 + h - 0.08f })
                        Beam(Rot(new Vector3(-len / 2 - 0.05f, y, depth / 2 + 0.05f), yaw), Rot(new Vector3(len / 2 + 0.05f, y, depth / 2 + 0.05f), yaw), 0.18f, Surface.Timber);
                    Beam(Rot(new Vector3(-len / 2, y0, depth / 2 + 0.05f), yaw), Rot(new Vector3(-len / 2, y0 + h, depth / 2 + 0.05f), yaw), 0.2f, Surface.Timber);
                }
            }
        }

        static Vector3 Rot(Vector3 p, float yaw) => p.Rotated(Vector3.Up, yaw);

        /// <summary>A window centred at <paramref name="cx"/> in the wall at distance <paramref name="z"/>, turned by <paramref name="yaw"/>.</summary>
        void Window(float cx, float sill, float z, float w, float h, float yaw, bool shutters, bool stoneFrame)
        {
            var frame = stoneFrame ? Surface.Stone : Surface.Timber;
            var o = Rot(new Vector3(cx - w / 2, sill, z + 0.03f), yaw);
            Quad(Surface.Glass, o, Rot(new Vector3(w, 0, 0), yaw), new Vector3(0, h, 0), Rot(Vector3.Back, yaw), new Vector2(cx, sill));
            var t = stoneFrame ? 0.16f : 0.11f;
            Beam(Rot(new Vector3(cx - w / 2 - t / 2, sill - t / 2, z + 0.06f), yaw), Rot(new Vector3(cx + w / 2 + t / 2, sill - t / 2, z + 0.06f), yaw), t, frame);
            Beam(Rot(new Vector3(cx - w / 2 - t / 2, sill + h + t / 2, z + 0.06f), yaw), Rot(new Vector3(cx + w / 2 + t / 2, sill + h + t / 2, z + 0.06f), yaw), t, frame);
            Beam(Rot(new Vector3(cx - w / 2 - t / 2, sill, z + 0.06f), yaw), Rot(new Vector3(cx - w / 2 - t / 2, sill + h, z + 0.06f), yaw), t, frame);
            Beam(Rot(new Vector3(cx + w / 2 + t / 2, sill, z + 0.06f), yaw), Rot(new Vector3(cx + w / 2 + t / 2, sill + h, z + 0.06f), yaw), t, frame);
            Beam(Rot(new Vector3(cx, sill, z + 0.05f), yaw), Rot(new Vector3(cx, sill + h, z + 0.05f), yaw), 0.05f, Surface.Timber); // mullion
            if (!shutters) return;
            foreach (var side in new[] { -1f, 1f })
            {
                // Shutters swung half open.
                var hinge = new Vector3(cx + side * (w / 2 + t), sill, z + 0.08f);
                var swing = new Vector3(side * w * 0.42f, 0, w * 0.32f);
                Panel(Rot(hinge, yaw), Rot(swing, yaw), new Vector3(0, h, 0), Surface.Planks);
            }
        }

        public void Door(float cx, float y0, float z, float w, float h, float yaw = 0)
        {
            Quad(Surface.Planks, Rot(new Vector3(cx - w / 2, y0, z + 0.02f), yaw), Rot(new Vector3(w, 0, 0), yaw), new Vector3(0, h, 0), Rot(Vector3.Back, yaw), new Vector2(cx, 0));
            Beam(Rot(new Vector3(cx - w / 2 - 0.1f, y0, z + 0.07f), yaw), Rot(new Vector3(cx - w / 2 - 0.1f, y0 + h + 0.1f, z + 0.07f), yaw), 0.18f, Surface.Timber);
            Beam(Rot(new Vector3(cx + w / 2 + 0.1f, y0, z + 0.07f), yaw), Rot(new Vector3(cx + w / 2 + 0.1f, y0 + h + 0.1f, z + 0.07f), yaw), 0.18f, Surface.Timber);
            Beam(Rot(new Vector3(cx - w / 2 - 0.25f, y0 + h + 0.12f, z + 0.08f), yaw), Rot(new Vector3(cx + w / 2 + 0.25f, y0 + h + 0.12f, z + 0.08f), yaw), 0.22f, Surface.Timber);
            Box(Surface.Stone, Rot(new Vector3(cx - w / 2 - 0.2f, y0 - 0.25f, z), yaw).Min(Rot(new Vector3(cx + w / 2 + 0.2f, y0, z + 0.55f), yaw)),
                Rot(new Vector3(cx - w / 2 - 0.2f, y0 - 0.25f, z), yaw).Max(Rot(new Vector3(cx + w / 2 + 0.2f, y0, z + 0.55f), yaw))); // the step
        }

        /// <summary>Arrow-slit windows on all four sides at one height.</summary>
        public void SlitWindows(float w, float d, float y, int perSide = 1)
        {
            for (var side = 0; side < 4; side++)
            {
                var (len, depth) = side % 2 == 0 ? (w, d) : (d, w);
                var yaw = side * Mathf.Pi / 2;
                for (var i = 0; i < perSide; i++)
                {
                    var cx = -len / 2 + (i + 0.5f) * len / perSide;
                    Quad(Surface.Glass, Rot(new Vector3(cx - 0.2f, y, depth / 2 + 0.02f), yaw), Rot(new Vector3(0.4f, 0, 0), yaw), new Vector3(0, 1.4f, 0), Rot(Vector3.Back, yaw));
                    Beam(Rot(new Vector3(cx - 0.4f, y - 0.1f, depth / 2 + 0.08f), yaw), Rot(new Vector3(cx + 0.4f, y - 0.1f, depth / 2 + 0.08f), yaw), 0.2f, Surface.Stone);
                    Beam(Rot(new Vector3(cx - 0.4f, y + 1.5f, depth / 2 + 0.08f), yaw), Rot(new Vector3(cx + 0.4f, y + 1.5f, depth / 2 + 0.08f), yaw), 0.2f, Surface.Stone);
                }
            }
        }

        /// <summary>Tall church windows along the long sides.</summary>
        public void TallWindows(float w, float d, float y, float h)
        {
            foreach (var side in new[] { 1, 3 })
            {
                var yaw = side * Mathf.Pi / 2;
                for (var z = -d / 2 + 2.5f; z < d / 2 - 1.5f; z += 3.2f)
                {
                    Quad(Surface.Glass, Rot(new Vector3(z - 0.55f, y, w / 2 + 0.02f), yaw), Rot(new Vector3(1.1f, 0, 0), yaw), new Vector3(0, h, 0), Rot(Vector3.Back, yaw), new Vector2(z, y));
                    Beam(Rot(new Vector3(z - 0.75f, y - 0.15f, w / 2 + 0.1f), yaw), Rot(new Vector3(z + 0.75f, y - 0.15f, w / 2 + 0.1f), yaw), 0.25f, Surface.Stone);
                    Beam(Rot(new Vector3(z - 0.75f, y + h + 0.15f, w / 2 + 0.1f), yaw), Rot(new Vector3(z + 0.75f, y + h + 0.15f, w / 2 + 0.1f), yaw), 0.25f, Surface.Stone);
                }
            }
        }

        /// <summary>A pitched roof with overhang, gable ends and a ridge; returns the ridge height.</summary>
        public float Gable(float w, float d, float wallTop, float pitch, Surface roof, Surface gableWall, bool acrossFront, float overhang = 0.5f, bool gableWalls = true)
        {
            // Build with the ridge along X, turned a quarter if the ridge should run front to back.
            var yaw = acrossFront ? 0 : Mathf.Pi / 2;
            var (span, length) = acrossFront ? (d, w) : (w, d);
            var rise = span / 2 * Mathf.Tan(pitch);
            var ridge = wallTop + rise;
            var drop = overhang * Mathf.Tan(pitch);
            var eaveY = wallTop - drop;
            var halfL = length / 2 + overhang;
            foreach (var s in new[] { 1f, -1f })
            {
                var eave = new Vector3(-halfL, eaveY, s * (span / 2 + overhang));
                var up = new Vector3(0, ridge - eaveY, -s * (span / 2 + overhang));
                Quad(roof, Rot(eave, yaw), Rot(new Vector3(2 * halfL, 0, 0), yaw), Rot(up, yaw), Rot(new Vector3(0, 1, s), yaw));
                // The boarded underside and a fascia board along the eave.
                Quad(Surface.Planks, Rot(eave + new Vector3(0, -0.14f, 0), yaw), Rot(new Vector3(2 * halfL, 0, 0), yaw), Rot(up, yaw), Rot(new Vector3(0, -1, -s), yaw));
                Beam(Rot(eave + new Vector3(0, -0.07f, 0), yaw), Rot(eave + new Vector3(2 * halfL, -0.07f, 0), yaw), 0.16f, Surface.Timber);
            }
            Beam(Rot(new Vector3(-halfL, ridge + 0.05f, 0), yaw), Rot(new Vector3(halfL, ridge + 0.05f, 0), yaw), 0.22f, roof == Surface.Thatch ? Surface.Thatch : Surface.Timber);
            foreach (var e in new[] { 1f, -1f })
            {
                // Bargeboards up the gable edges, and the gable wall itself.
                foreach (var s in new[] { 1f, -1f })
                    Beam(Rot(new Vector3(e * halfL, eaveY - 0.07f, s * (span / 2 + overhang)), yaw), Rot(new Vector3(e * halfL, ridge - 0.07f, 0), yaw), 0.16f, Surface.Timber);
                if (!gableWalls) continue;
                var x = e * length / 2;
                Tri(gableWall, Rot(new Vector3(x, wallTop, -span / 2), yaw), Rot(new Vector3(x, wallTop, span / 2), yaw), Rot(new Vector3(x, ridge, 0), yaw),
                    Rot(new Vector3(e, 0, 0), yaw), p => new Vector2(acrossFront ? p.Z : p.X, p.Y));
                if (gableWall == Surface.Plaster)
                    Beam(Rot(new Vector3(x + e * 0.05f, wallTop, 0), yaw), Rot(new Vector3(x + e * 0.05f, ridge, 0), yaw), 0.16f, Surface.Timber); // king post
            }
            return ridge;
        }

        /// <summary>A single sloped roof (a lean-to), from a high edge down to a low one.</summary>
        public void Slope(Vector3 high, Vector3 low, float width, Surface roof)
        {
            var across = new Vector3(0, 0, width);
            Quad(roof, high, low - high, across, Vector3.Up);
            Quad(Surface.Planks, high + Vector3.Down * 0.12f, low - high, across, Vector3.Down);
        }

        public void Chimney(Vector3 at, float top, float size = 0.75f)
        {
            Box(Surface.Stone, new Vector3(at.X - size / 2, 1, at.Z - size / 2), new Vector3(at.X + size / 2, top, at.Z + size / 2));
            Box(Surface.DarkStone, new Vector3(at.X - size / 2 - 0.08f, top - 0.2f, at.Z - size / 2 - 0.08f), new Vector3(at.X + size / 2 + 0.08f, top, at.Z + size / 2 + 0.08f));
        }

        /// <summary>Crenellations around the top of a rectangle.</summary>
        public void Battlements(float w, float d, float top)
        {
            Box(Surface.Stone, new Vector3(-w / 2 - 0.3f, top - 0.4f, -d / 2 - 0.3f), new Vector3(w / 2 + 0.3f, top, d / 2 + 0.3f));
            for (var side = 0; side < 4; side++)
            {
                var (len, depth) = side % 2 == 0 ? (w + 0.6f, d + 0.6f) : (d + 0.6f, w + 0.6f);
                var yaw = side * Mathf.Pi / 2;
                for (var x = -len / 2; x < len / 2 - 0.3f; x += 1.3f)
                {
                    var a = Rot(new Vector3(x, top, depth / 2 - 0.45f), yaw);
                    var b = Rot(new Vector3(x + 0.7f, top + 1f, depth / 2), yaw);
                    Box(Surface.Stone, a.Min(b), a.Max(b));
                }
            }
        }

        public void Pyramid(float s, float baseY, float apex, Surface roof)
        {
            var h = s / 2;
            Vector3[] c = [new(-h, baseY, h), new(h, baseY, h), new(h, baseY, -h), new(-h, baseY, -h)];
            var top = new Vector3(0, apex, 0);
            for (var i = 0; i < 4; i++)
            {
                var a = c[i];
                var b = c[(i + 1) % 4];
                var mid = (a + b) / 2;
                var along = (b - a).Normalized();
                var downSlope = (mid - top).Length();
                Tri(roof, a, b, top, mid with { Y = 0 } + Vector3.Up * 0.3f, p => new Vector2((p - a).Dot(along), (p.Y - baseY) / (apex - baseY) * downSlope));
            }
        }

        /// <summary>A round (many-sided) tower wall, optionally tapering.</summary>
        public void Round(float radius, float y0, float y1, Surface s, int sides, float topRadius = -1)
        {
            if (topRadius < 0) topRadius = radius;
            var circumference = Mathf.Tau * radius;
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.Tau / sides;
                var a1 = (i + 1) * Mathf.Tau / sides;
                var p0 = new Vector3(Mathf.Sin(a0) * radius, y0, Mathf.Cos(a0) * radius);
                var p1 = new Vector3(Mathf.Sin(a1) * radius, y0, Mathf.Cos(a1) * radius);
                var q0 = new Vector3(Mathf.Sin(a0) * topRadius, y1, Mathf.Cos(a0) * topRadius);
                var q1 = new Vector3(Mathf.Sin(a1) * topRadius, y1, Mathf.Cos(a1) * topRadius);
                var outward = new Vector3(Mathf.Sin((a0 + a1) / 2), 0, Mathf.Cos((a0 + a1) / 2));
                var u0 = i * circumference / sides;
                // Each face maps flat, starting where the last one ended, so stone courses run round the tower.
                var width = (p1 - p0).Length();
                Vector2 Uv(Vector3 p) => new(u0 + (p == p1 || p == q1 ? width : 0), p.Y);
                Tri(s, p0, p1, q1, outward, Uv);
                Tri(s, p0, q1, q0, outward, Uv);
            }
            // A cap on top, so a squat drum (a well) is closed.
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.Tau / sides;
                var a1 = (i + 1) * Mathf.Tau / sides;
                Tri(s, new Vector3(0, y1, 0), new Vector3(Mathf.Sin(a0) * topRadius, y1, Mathf.Cos(a0) * topRadius), new Vector3(Mathf.Sin(a1) * topRadius, y1, Mathf.Cos(a1) * topRadius), Vector3.Up, p => new Vector2(p.X, p.Z));
            }
        }

        public void Cone(float radius, float y0, float apex, Surface roof, int sides)
        {
            var slant = new Vector2(radius, apex - y0).Length();
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.Tau / sides;
                var a1 = (i + 1) * Mathf.Tau / sides;
                var p0 = new Vector3(Mathf.Sin(a0) * radius, y0, Mathf.Cos(a0) * radius);
                var p1 = new Vector3(Mathf.Sin(a1) * radius, y0, Mathf.Cos(a1) * radius);
                var outward = new Vector3(Mathf.Sin((a0 + a1) / 2), 0.5f, Mathf.Cos((a0 + a1) / 2));
                Tri(roof, p0, p1, new Vector3(0, apex, 0), outward,
                    p => new Vector2(Mathf.Atan2(p.X, p.Z) * radius, (p.Y - y0) / (apex - y0) * slant));
            }
        }

        public Design Finish()
        {
            var mesh = new ArrayMesh();
            foreach (var (surface, tool) in _tools)
            {
                tool.GenerateTangents();
                tool.Commit(mesh);
                mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, BuildingMaterials.For(surface));
            }
            return new Design(mesh, [.. _solids], _height);
        }
    }
}
