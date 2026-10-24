using Godot;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>A named part of the city. The smallest zone the player stands in names where they are.</summary>
public sealed record Zone(string Name, Vector3 Center, float Radius, float Priority);

/// <summary>Where a group of enemies lives.</summary>
public sealed record Lair(string KindId, int Count, Vector3 Center, float Spread);

/// <summary>
/// Builds Tristinwold from simple shapes until real art arrives: three concentric rings, each
/// higher than the last, walled with gates; the Docks and harbor to the east with the Needle on
/// its island; the Gallows Perch by the east gate. Place names match world/world.json.
/// </summary>
public static class City
{
    public const float OuterRadius = 100, InnerRadius = 60, CrownRadius = 30;
    public const float InnerHeight = 1.5f, CrownHeight = 3f;

    public static readonly Vector3 GallowsPerch = new(84, 0, -14);
    public static readonly Vector3 PlayerStart = new(78, 0, -6);
    public static readonly Vector3 Needle = new(210, 0, 70);

    public static List<Zone> Zones { get; } =
    [
        new("The Crown", Vector3.Zero, CrownRadius, 3),
        new("The Exchange", Vector3.Zero, InnerRadius, 2),
        new("Tristinwold", Vector3.Zero, OuterRadius + 2, 1),
        new("The Gallows Perch", GallowsPerch, 9, 10),
        new("The Salt Barrel", new(40, 0, 78), 8, 10),
        new("The Lantern Boys' Den", new(-50, 0, 72), 9, 10),
        new("The Drowned Chapel", new(-80, 0, -40), 11, 10),
        new("The Watch House", new(70, 0, 55), 8, 10),
        new("The Warrens", new(-20, 0, -82), 15, 9),
        new("The Magi Archive", new(-40, InnerHeight, 0), 8, 10),
        new("The Docks", new(120, 0, 0), 22, 5),
        new("The Harbor", new(200, 0, 0), 120, 4),
        new("The Needle", Needle, 16, 10),
        new("The Tong Sea-Caves", new(150, 0, -110), 14, 10),
    ];

    public static List<Lair> Lairs { get; } =
    [
        new("skeever", 3, new(-20, 0, -82), 6),
        new("dock_thug", 2, new(118, 0, 14), 5),
        new("lantern_boy", 2, new(-50, 0, 72), 4),
        new("draugr", 1, new(-80, 0, -40), 3),
        new("sea_wraith", 1, new(-84, 0, -46), 3),
        new("drowned_hand", 2, new(150, 0, -110), 5),
        new("dock_thug", 2, new(-170, 0, 40), 6),
    ];

    public static string AreaAt(Vector3 p)
    {
        Zone? best = null;
        foreach (var z in Zones)
        {
            var flat = new Vector2(p.X - z.Center.X, p.Z - z.Center.Z).Length();
            if (flat <= z.Radius && (best is null || z.Priority > best.Priority)) best = z;
        }
        return best?.Name ?? WorldGen.Default.AreaAt(p.X, p.Z);
    }

    /// <summary>Ground height at a point: the city's ring platforms, or the land outside them.</summary>
    public static float FloorAt(Vector3 p)
    {
        var r = new Vector2(p.X, p.Z).Length();
        return r < CrownRadius ? CrownHeight : r < InnerRadius ? InnerHeight : (float)WorldGen.Default.Height(p.X, p.Z);
    }

    /// <summary>The sky, fog and light, and the sun: the graphics settings adjust them.</summary>
    public static Godot.Environment Sky { get; private set; } = null!;
    public static DirectionalLight3D Sun { get; private set; } = null!;

    public static void Build(Node3D root, float fog = 0.0035f)
    {
        var rng = new Rng(1337);

        // Sky and sun.
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial { SkyTopColor = new Color("#3d5a80"), SkyHorizonColor = new Color("#c9b79c"), GroundBottomColor = new Color("#2b2620") } },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            FogEnabled = true,
            FogDensity = fog, // hides where the streamed land ends
            FogLightColor = new Color("#b7a98f"),
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(Sun = new DirectionalLight3D { RotationDegrees = new Vector3(-50, -30, 0), ShadowEnabled = true, LightEnergy = 1.0f });
        Sky = env;

        // The land and the harbor come from the world generator (see Terrain).

        // The rings: raised platforms, each higher than the last.
        Disc(root, InnerRadius, InnerHeight, "#857a66");
        Disc(root, CrownRadius, CrownHeight, "#a39c8b");

        // Walls with gates. Gates face east (to the docks) and west (to the land).
        Wall(root, OuterRadius, 0, 7, "#6d6558", gates: [0, 180]);
        Wall(root, InnerRadius, InnerHeight, 6, "#7d7466", gates: [0, 200]);
        Wall(root, CrownRadius, CrownHeight, 5, "#b0a890", gates: [0]);
        Ramp(root, InnerRadius, 0, InnerHeight, 0);
        Ramp(root, InnerRadius, 0, InnerHeight, 200);
        Ramp(root, CrownRadius, InnerHeight, CrownHeight, 0);

        // Streets of houses in each ring, crowded at the bottom, grand at the top.
        Houses(root, rng, 66, 95, 0, 70, "#5b4a3a", 4, 7);
        Houses(root, rng, 36, 56, InnerHeight, 28, "#7a6650", 6, 9);

        // Landmarks.
        Building(root, GallowsPerch, new Vector3(12, 11, 9), "#2a2420", "The Gallows Perch", roof: "#121010");
        Building(root, new Vector3(40, 0, 78), new Vector3(9, 6, 8), "#6e5235", "The Salt Barrel");
        Building(root, new Vector3(-50, 0, 72), new Vector3(10, 5, 9), "#8a3a2a", "The Lantern Boys' Den");
        Building(root, new Vector3(70, 0, 55), new Vector3(8, 7, 8), "#4a4a52", "The Watch House");
        var chapel = Building(root, new Vector3(-80, -1.5f, -40), new Vector3(14, 9, 18), "#3f5450", "The Drowned Chapel");
        chapel.RotationDegrees = new Vector3(4, 20, -3); // half sunk, leaning
        Building(root, new Vector3(-40, InnerHeight, 0), new Vector3(12, 8, 10), "#5f6a7a", "The Magi Archive");
        Building(root, new Vector3(30, InnerHeight, -30), new Vector3(16, 10, 12), "#8c6d3e", "The Exchange");
        Building(root, new Vector3(0, CrownHeight, 0), new Vector3(14, 26, 14), "#cfc6ad", "The Crown", roof: "#4a5a7a");
        for (var i = 0; i < 6; i++) // the Warrens: a crush of low shacks
            Box(root, new Vector3(-28 + rng.Next(18), 1.2f, -88 + rng.Next(14)), new Vector3(4, 2.4f, 3), "#3b3128", solid: true);
        Label(root, new Vector3(-20, 5, -82), "The Warrens");

        // The Docks: piers out into the harbor.
        for (var z = -30; z <= 30; z += 15)
            Box(root, new Vector3(122, -0.25f, z), new Vector3(40, 0.5f, 5), "#6a5034", solid: true);
        Label(root, new Vector3(120, 4, 0), "The Docks");

        // The Needle: a lighthouse on an island.
        Cylinder(root, Needle + new Vector3(0, -1, 0), 14, 2, "#6b6a55");
        Cylinder(root, Needle + new Vector3(0, 12, 0), 2.5f, 24, "#e8e0cf");
        root.AddChild(new OmniLight3D { Position = Needle + new Vector3(0, 25, 0), LightColor = new Color("#ffd27a"), LightEnergy = 4, OmniRange = 60 });
        Label(root, Needle + new Vector3(0, 28, 0), "The Needle");

        // The Tong sea-caves on the north shore.
        Box(root, new Vector3(150, 3, -118), new Vector3(30, 10, 10), "#3a3a3a", solid: true);
        Label(root, new Vector3(150, 9, -110), "The Tong Sea-Caves");
    }

    // ---- shapes ------------------------------------------------------------------------------

    static StandardMaterial3D Mat(string colour) => new() { AlbedoColor = new Color(colour), Roughness = 0.9f };

    static Node3D Box(Node3D parent, Vector3 pos, Vector3 size, string colour, bool solid, float offsetX = 0)
    {
        Node3D node = solid ? new StaticBody3D() : new Node3D();
        node.Position = pos + new Vector3(offsetX, 0, 0);
        node.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Mat(colour) });
        if (solid) node.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(node);
        return node;
    }

    static void Cylinder(Node3D parent, Vector3 pos, float radius, float height, string colour)
    {
        var body = new StaticBody3D { Position = pos };
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height }, MaterialOverride = Mat(colour) });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } });
        parent.AddChild(body);
    }

    static void Disc(Node3D parent, float radius, float top, string colour) =>
        Cylinder(parent, new Vector3(0, top / 2, 0), radius, top, colour);

    static void Wall(Node3D parent, float radius, float baseY, float height, string colour, float[] gates)
    {
        const int segments = 64;
        for (var i = 0; i < segments; i++)
        {
            var angle = i * 360f / segments;
            if (gates.Any(g => Math.Abs(Mathf.Wrap(angle - g, -180, 180)) < 6)) continue; // leave a gate
            var rad = Mathf.DegToRad(angle);
            var segment = Box(parent, new Vector3(Mathf.Cos(rad) * radius, baseY + height / 2, Mathf.Sin(rad) * radius),
                new Vector3(1.6f, height, 2 * Mathf.Pi * radius / segments + 0.4f), colour, solid: true);
            segment.Rotation = new Vector3(0, -rad, 0);
        }
    }

    /// <summary>A sloped road up through a gate, from one ring's level to the next.</summary>
    static void Ramp(Node3D parent, float radius, float fromY, float toY, float gateAngle)
    {
        var rad = Mathf.DegToRad(gateAngle);
        var length = 10f;
        var rise = toY - fromY;
        var mid = radius + length / 2 - 1;
        var ramp = Box(parent, new Vector3(Mathf.Cos(rad) * mid, fromY + rise / 2 - 0.25f, Mathf.Sin(rad) * mid),
            new Vector3(length + 2, 0.5f, 8), "#8a8070", solid: true);
        ramp.Rotation = new Vector3(0, -rad, Mathf.Atan2(rise, length));
    }

    static void Houses(Node3D parent, Rng rng, float rMin, float rMax, float y, int count, string colour, float sizeMin, float sizeMax)
    {
        for (var placed = 0; placed < count;)
        {
            var angle = (float)(rng.NextDouble() * Mathf.Tau);
            if (Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle), -180, 180)) < 14 || Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle) - 180, -180, 180)) < 14 ||
                Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle) - 200, -180, 180)) < 14) continue; // keep gate roads clear
            var r = rMin + (float)rng.NextDouble() * (rMax - rMin);
            var size = sizeMin + (float)rng.NextDouble() * (sizeMax - sizeMin);
            var pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            if (Zones.Any(z => z.Priority >= 9 && new Vector2(pos.X - z.Center.X, pos.Z - z.Center.Z).Length() < z.Radius + 4)) continue;
            var house = Box(parent, pos + new Vector3(0, size * 0.4f, 0), new Vector3(size, size * 0.8f, size * 0.9f), colour, solid: true);
            house.Rotation = new Vector3(0, -angle, 0);
            placed++;
        }
    }

    static Node3D Building(Node3D parent, Vector3 pos, Vector3 size, string colour, string name, string roof = "#3a2a20")
    {
        var b = Box(parent, pos + new Vector3(0, size.Y / 2, 0), size, colour, solid: true);
        b.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(size.X + 1, size.Y * 0.35f, size.Z + 1) },
            Position = new Vector3(0, size.Y / 2 + size.Y * 0.175f, 0),
            MaterialOverride = Mat(roof),
        });
        Label(parent, pos + new Vector3(0, size.Y * 1.45f + 1, 0), name);
        return b;
    }

    static void Label(Node3D parent, Vector3 pos, string text) => parent.AddChild(new Label3D
    {
        Text = text, Position = pos, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12,
        Modulate = new Color("#e8dcc4"), PixelSize = 0.02f,
    });
}
