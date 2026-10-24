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

        // A clear sky, warm sun, and haze that hides where the streamed land ends.
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color("#2f63a8"), SkyHorizonColor = new Color("#b4cde2"), SkyCurve = 0.12f,
                    GroundBottomColor = new Color("#4a5a3e"), GroundHorizonColor = new Color("#b4cde2"), SunAngleMax = 25,
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#9aa4b4"),
            AmbientLightEnergy = 0.55f,
            FogEnabled = true,
            FogDensity = fog,
            FogLightColor = new Color("#a9c2da"),
            FogSkyAffect = 0.1f,
            FogAerialPerspective = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(Sun = new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), ShadowEnabled = true, LightEnergy = 1.25f, LightColor = new Color("#fff0d6") });
        Sky = env;

        // The land and the harbor come from the world generator (see Terrain).

        // The rings: raised stone platforms, each higher than the last.
        Disc(root, InnerRadius, InnerHeight, "#9a9284");
        Disc(root, CrownRadius, CrownHeight, "#b0a898");

        // Castle walls with gates. Gates face east (to the docks) and west (to the land).
        Walls(root, OuterRadius, 0, 6, gates: [0, 180]);
        Walls(root, InnerRadius, InnerHeight, 5, gates: [0, 200]);
        Walls(root, CrownRadius, CrownHeight, 3.5f, gates: [0]);
        Ramp(root, InnerRadius, 0, InnerHeight, 0);
        Ramp(root, InnerRadius, 0, InnerHeight, 200);
        Ramp(root, CrownRadius, InnerHeight, CrownHeight, 0);

        // Streets of houses in each ring, crowded at the bottom, grander at the top.
        Houses(root, rng, 68, 93, 0, 46, ["home_A", "home_A", "home_B", "blacksmith", "lumbermill"]);
        Houses(root, rng, 38, 54, InnerHeight, 16, ["home_B", "home_B", "market", "tower_A"]);

        // Landmarks.
        Landmark(root, "tavern", "red", GallowsPerch, 9.5f, 0.5f, "The Gallows Perch");
        Landmark(root, "tavern", "blue", new Vector3(40, 0, 78), 7.5f, 2.6f, "The Salt Barrel");
        Landmark(root, "barracks", "red", new Vector3(-50, 0, 72), 6.5f, 2.2f, "The Lantern Boys' Den");
        Landmark(root, "barracks", "blue", new Vector3(70, 0, 55), 7, 2.4f, "The Watch House");
        var chapel = Landmark(root, "church", "green", new Vector3(-80, -1.6f, -40), 9, 0.4f, "The Drowned Chapel");
        chapel.RotationDegrees = new Vector3(5, 20, -4); // half sunk, leaning
        Landmark(root, "tower_B", "blue", new Vector3(-40, InnerHeight, 0), 7, 1.6f, "The Magi Archive");
        Landmark(root, "market", "red", new Vector3(30, InnerHeight, -30), 9, 2.2f, "The Exchange");
        Landmark(root, "castle", "red", new Vector3(0, CrownHeight, 0), 8, 1.57f, "The Crown");
        for (var i = 0; i < 7; i++) // the Warrens: a crush of ruins and lean-tos
            Models.Place(root, i % 2 == 0 ? "medieval/neutral/building_destroyed.gltf" : Models.Building("home_A", "green"),
                new Vector3(-30 + rng.Next(22), 0, -92 + rng.Next(16)), i % 2 == 0 ? 6 : 4.5f, (float)rng.NextDouble() * Mathf.Tau);
        Label(root, new Vector3(-20, 8, -82), "The Warrens");

        // The Docks: piers out into the harbor, stacked with cargo.
        for (var z = -30; z <= 30; z += 15)
        {
            Box(root, new Vector3(122, -0.25f, z), new Vector3(40, 0.5f, 5), "#7a5a38", solid: true);
            for (var k = 0; k < 3; k++)
                Models.Place(root, rng.Chance(0.5) ? "dungeon/barrel_large.glb" : "dungeon/crates_stacked.glb",
                    new Vector3(108 + rng.Next(28), 0, z + (float)rng.NextDouble() * 3 - 1.5f), rng.Chance(0.5) ? 0.5f : 0.6f, (float)rng.NextDouble() * Mathf.Tau);
        }
        Label(root, new Vector3(120, 5, 0), "The Docks");

        // The Needle: a lighthouse on a rocky island.
        Cylinder(root, Needle + new Vector3(0, -1, 0), 14, 2.4f, "#7a7868");
        Landmark(root, "tower_A", "blue", Needle + new Vector3(0, 0.2f, 0), 9, 0, "The Needle", labelHeight: 30);
        root.AddChild(new OmniLight3D { Position = Needle + new Vector3(0, 22, 0), LightColor = new Color("#ffd27a"), LightEnergy = 4, OmniRange = 60 });

        // The Tong sea-caves: crags on the north shore.
        Models.Place(root, "medieval/nature/mountain_B.gltf", new Vector3(150, -3, -120), 14, 0.7f);
        Models.Place(root, "medieval/nature/mountain_C.gltf", new Vector3(170, -3, -108), 9, 2.1f);
        Label(root, new Vector3(150, 16, -108), "The Tong Sea-Caves");

        // Lanterns along the gate roads.
        foreach (var angle in new[] { 0f, 180f })
            for (var r = 64; r <= 96; r += 16)
            {
                var a = Mathf.DegToRad(angle);
                foreach (var side in new[] { -6f, 6f })
                    Models.Place(root, "halloween/lantern_standing.gltf",
                        new Vector3(Mathf.Cos(a) * r - Mathf.Sin(a) * side, 0, Mathf.Sin(a) * r + Mathf.Cos(a) * side), 2.2f, 0, solid: false);
            }
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

    static void Houses(Node3D parent, Rng rng, float rMin, float rMax, float y, int count, string[] kinds)
    {
        for (var placed = 0; placed < count;)
        {
            var angle = (float)(rng.NextDouble() * Mathf.Tau);
            if (Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle), -180, 180)) < 14 || Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle) - 180, -180, 180)) < 14 ||
                Math.Abs(Mathf.Wrap(Mathf.RadToDeg(angle) - 200, -180, 180)) < 14) continue; // keep gate roads clear
            var r = rMin + (float)rng.NextDouble() * (rMax - rMin);
            var pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            if (Zones.Any(z => z.Priority >= 9 && new Vector2(pos.X - z.Center.X, pos.Z - z.Center.Z).Length() < z.Radius + 5)) continue;
            var kind = rng.Pick(kinds);
            var colour = rng.Chance(0.6) ? "red" : rng.Chance(0.5) ? "blue" : "green";
            Models.Place(parent, Models.Building(kind, colour), pos, 6.5f + (float)rng.NextDouble() * 1.5f, -angle + Mathf.Pi / 2);
            placed++;
        }
    }

    static Node3D Landmark(Node3D parent, string kind, string colour, Vector3 at, float scale, float yaw, string name, float labelHeight = 0)
    {
        var b = Models.Place(parent, Models.Building(kind, colour), at, scale, yaw);
        var height = labelHeight > 0 ? labelHeight : Models.Bounds((Node3D)b.GetChild(0)).End.Y + 3;
        Label(parent, at + new Vector3(0, height, 0), name);
        return b;
    }

    /// <summary>A ring of castle wall, with a gap at each gate.</summary>
    static void Walls(Node3D parent, float radius, float baseY, float scale, float[] gates)
    {
        var length = 2 * scale; // each wall piece is 2 units long
        var count = (int)Math.Ceiling(Mathf.Tau * radius / length);
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            if (gates.Any(g => Math.Abs(Mathf.Wrap(Mathf.RadToDeg(a) - g, -180, 180)) < 6)) continue;
            Models.Place(parent, "medieval/neutral/wall_straight.gltf", new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius),
                scale * 1.02f, -a - Mathf.Pi / 2, shrink: 1);
        }
    }

    static void Label(Node3D parent, Vector3 pos, string text) => parent.AddChild(new Label3D
    {
        Text = text, Position = pos, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12,
        Modulate = new Color("#e8dcc4"), PixelSize = 0.02f,
    });
}
