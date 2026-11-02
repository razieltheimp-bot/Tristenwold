using Godot;
using Tristinwold.Deviant;
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

    public static readonly Vector3 GallowsPerch = new(82, 0, -23);
    public static readonly Vector3 PlayerStart = new(78, 0, -6);
    public static readonly Vector3 Needle = new(210, 0, 70);

    // The outer ring's second smith and general store, and the inner ring's own, with its two taverns.
    public static readonly Vector3 WestForge = new(-82, 0, 30), Sundries = new(-20, 0, 84);
    public static readonly Vector3 GiltAnvil = new(-8, InnerHeight, -46), HarrowGoods = new(-36, InnerHeight, 28);
    public static readonly Vector3 GildedFlagon = new(38, InnerHeight, 26), ScholarsRest = new(-14, InnerHeight, 44);

    /// <summary>Where a shopkeeper stands: out front towards the city's centre, and to one side of the door.</summary>
    public static Vector3 Frontage(Vector3 at)
    {
        var inward = new Vector3(-at.X, 0, -at.Z).Normalized();
        return at + inward * 7 + new Vector3(-inward.Z, 0, inward.X) * 3.5f;
    }

    public static List<Zone> Zones { get; } =
    [
        new("The Crown", Vector3.Zero, CrownRadius, 3),
        new("The Exchange", Vector3.Zero, InnerRadius, 2),
        new("Tristinwold", Vector3.Zero, OuterRadius + 2, 1),
        new("The Gallows Perch", GallowsPerch, 9, 10),
        new("The Salt Barrel", new(40, 0, 78), 8, 10),
        new("The Lantern Boys' Den", new(-50, 0, 72), 9, 10),
        new("St. Aubrel's Church", new(-80, 0, -40), 11, 10),
        new("The Drowned Chapel", new(118, 0, -182), 30, 10),
        new("The Watch House", new(70, 0, 55), 8, 10),
        new("The Warrens", new(-20, 0, -82), 15, 9),
        new("The Magi Archive", new(-40, InnerHeight, 0), 8, 10),
        new("The West Gate Forge", WestForge, 8, 10),
        new("Pell's Sundries", Sundries, 7, 10),
        new("The Gilt Anvil", GiltAnvil, 8, 10),
        new("Harrow & Daughters", HarrowGoods, 7, 10),
        new("The Gilded Flagon", GildedFlagon, 8, 10),
        new("The Scholar's Rest", ScholarsRest, 8, 10),
        new("The Docks", new(120, 0, 0), 22, 5),
        new("The Harbor", new(200, 0, 0), 120, 4),
        new("The Needle", Needle, 16, 10),
        new("The Tong Sea-Caves", new(150, 0, -110), 14, 10),
    ];

    public static List<Lair> Lairs { get; } =
    [
        new("giant_rat", 3, new(-20, 0, -82), 6),
        new("dock_thug", 2, new(130, 0, 30), 2), // the far end of the south pier, away from the honest work on the middle one
        new("lantern_boy", 2, new(-50, 0, 72), 4),
        new("drowned_hand", 2, new(150, 0, -110), 5),
        new("dock_thug", 2, new(-205, 0, 55), 6),
    ];

    public static string AreaAt(Vector3 p)
    {
        if (BelowName?.Invoke(p) is { } chamber) return chamber;
        Zone? best = null;
        foreach (var z in Zones)
        {
            var flat = new Vector2(p.X - z.Center.X, p.Z - z.Center.Z).Length();
            if (flat <= z.Radius && (best is null || z.Priority > best.Priority)) best = z;
        }
        return best?.Name ?? (Northwood.Inside(p.X, p.Z) ? "The Northwood" : WorldGen.Default.AreaAt(p.X, p.Z));
    }

    /// <summary>Ground height at a point: the city's ring platforms, or the land outside them.</summary>
    /// <summary>Where the piers run out into the harbour (their centre lines), and how far.</summary>
    public static readonly float[] Piers = [-30, -15, 0, 15, 30];
    /// <summary>They start well up the shore, where the land's level with their boards, so there's no step on or off them.</summary>
    public const float PierFrom = 94, PierTo = 142;

    /// <summary>The boardwalk across the piers' landward ends, so you can walk from the east gate's pier to every other.</summary>
    public const float BoardwalkX = 110, BoardwalkReach = 33;

    public static bool OnPier(Vector3 p) => p.X is > PierFrom and < PierTo && Piers.Any(z => Math.Abs(p.Z - z) < 2.5f)
        || Math.Abs(p.X - BoardwalkX) < 2.6f && Math.Abs(p.Z) < BoardwalkReach;

    /// <summary>Out at sea (see <see cref="AtSea"/>): the deck's height, where there's a ship under the hero instead of land.</summary>
    public static Func<Vector3, float?>? Afloat { get; set; }

    /// <summary>Down the mine (see <see cref="Cave"/>): the rock floor, and the name of the chamber, where there's a cave instead of land.</summary>
    public static Func<Vector3, float?>? Below { get; set; }
    /// <summary>Whether the land under a point has been built yet (the game says): until it has, nothing is let fall through where it will be.</summary>
    public static Func<Vector3, bool>? GroundBuilt { get; set; }
    public static Func<Vector3, string?>? BelowName { get; set; }

    static Action? _letIn;

    /// <summary>The sky's shut out just now: the hero's under a roof or down a mine.</summary>
    public static bool Shut => _letIn is not null;

    /// <summary>Under a roof or a mountain: no sky, no sun, a little light from nowhere and whatever lamps there are.</summary>
    public static void ShutOutSky(Color ambient, float energy, float fog)
    {
        LetInSky();
        var env = Sky;
        var (background, colour, source, light, power, reflected, haze, density, sun) = (env.BackgroundMode, env.BackgroundColor, env.AmbientLightSource, env.AmbientLightColor, env.AmbientLightEnergy, env.ReflectedLightSource, env.FogLightColor, env.FogDensity, Sun.Visible);
        _letIn = () =>
        {
            (env.BackgroundMode, env.BackgroundColor, env.AmbientLightSource, env.AmbientLightColor, env.AmbientLightEnergy, env.ReflectedLightSource, env.FogLightColor, env.FogDensity) =
                (background, colour, source, light, power, reflected, haze, density);
            Sun.Visible = sun;
        };
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = new Color("#050404");
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = ambient;
        env.AmbientLightEnergy = energy;
        env.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
        env.FogLightColor = new Color("#0c0a08");
        env.FogDensity = fog;
        Sun.Visible = false;
    }

    public static void LetInSky()
    {
        _letIn?.Invoke();
        _letIn = null;
    }

    public static float FloorAt(Vector3 p)
    {
        if (Below?.Invoke(p) is { } rock) return rock;
        if (Afloat?.Invoke(p) is { } deck) return deck;
        if (OnPier(p)) return Mathf.Max(0, (float)WorldGen.Default.Height(p.X, p.Z)); // the boards
        var r = new Vector2(p.X, p.Z).Length();
        return r < CrownRadius ? CrownHeight : r < InnerRadius ? InnerHeight : (float)WorldGen.Default.Height(p.X, p.Z);
    }

    /// <summary>The sky, fog and light, and the sun: the graphics settings adjust them.</summary>
    public static Godot.Environment Sky { get; private set; } = null!;
    public static DirectionalLight3D Sun { get; private set; } = null!;

    const string SkyPhoto = "res://assets/polyhaven/sky/sky.hdr";

    /// <summary>The sky is the photograph (real clouds), rather than painted.</summary>
    public static bool PhotoSky { get; private set; }

    public static void Build(Node3D root, float fog = 0.0035f)
    {
        var rng = new Rng(1337);

        // A photographed sky (Poly Haven, CC0): real clouds, and the light they give. The land, the water and every
        // shiny thing are lit and reflect from it, and the sun shines from where it stands in the photograph (it was
        // shot at 48 degrees up, as high as our sun; the panorama is turned to put it in the same place).
        // Haze hides where the streamed land ends.
        var photo = ResourceLoader.Exists(SkyPhoto) ? GD.Load<Texture2D>(SkyPhoto) : null;
        PhotoSky = photo is not null;
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = photo is not null
                    ? new PanoramaSkyMaterial { Panorama = photo, EnergyMultiplier = 1.0f }
                    : new ProceduralSkyMaterial
                    {
                        SkyTopColor = new Color("#2f63a8"), SkyHorizonColor = new Color("#b4cde2"), SkyCurve = 0.12f,
                        GroundBottomColor = new Color("#4a5a3e"), GroundHorizonColor = new Color("#b4cde2"), SunAngleMax = 25,
                    },
                RadianceSize = Godot.Sky.RadianceSizeEnum.Size256,
            },
            SkyRotation = new Vector3(0, Mathf.Pi, 0),
            AmbientLightSource = photo is not null ? Godot.Environment.AmbientSource.Sky : Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#9aa4b4"),
            AmbientLightEnergy = photo is not null ? 0.75f : 0.55f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            FogEnabled = true,
            FogDensity = fog,
            FogLightColor = new Color("#a9c2da"),
            FogSkyAffect = 0.1f,
            FogAerialPerspective = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(Sun = new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), ShadowEnabled = true, LightEnergy = photo is not null ? 1.6f : 1.25f, LightColor = new Color("#fff2dc") });
        Sky = env;

        // The land and the harbor come from the world generator (see Terrain).

        // The rings: raised stone platforms, each higher than the last.
        Disc(root, InnerRadius, InnerHeight, "#9a9284");
        Disc(root, CrownRadius, CrownHeight, "#b0a898");

        // Castle walls with gates. Gates face east (to the docks) and west (to the land).
        Walls(root, OuterRadius, 0, "wall", 1, gates: [0, 180], gatehouses: true);
        Walls(root, InnerRadius, InnerHeight, "wall_low", 1, gates: [0, 200]);
        Walls(root, CrownRadius, CrownHeight, "wall_low", 0.8f, gates: [0]);
        Ramp(root, InnerRadius, 0, InnerHeight, 0);
        Ramp(root, InnerRadius, 0, InnerHeight, 200);
        Ramp(root, CrownRadius, InnerHeight, CrownHeight, 0);


        // Landmarks.
        Landmark(root, "tavern", "red", GallowsPerch, 9.5f, Mathf.Atan2(PlayerStart.X - GallowsPerch.X, PlayerStart.Z - GallowsPerch.Z), "The Gallows Perch"); // its door faces where you start
        Landmark(root, "tavern", "blue", new Vector3(40, 0, 78), 7.5f, 2.6f, "The Salt Barrel");
        Landmark(root, "barracks", "red", new Vector3(-50, 0, 72), 6.5f, 2.2f, "The Lantern Boys' Den");
        Landmark(root, "barracks", "blue", new Vector3(70, 0, 55), 7, 2.4f, "The Watch House");
        Landmark(root, "church", "green", new Vector3(-80, 0, -40), 9, Mathf.DegToRad(20), "St. Aubrel's Church"); // the drowned one is out on the north shore
        Landmark(root, "tower_B", "blue", new Vector3(-40, InnerHeight, 0), 7, 1.6f, "The Magi Archive");
        Landmark(root, "market", "red", new Vector3(30, InnerHeight, -30), 9, 2.2f, "The Exchange");
        Landmark(root, "castle", "red", new Vector3(0, CrownHeight, 0), 8, 1.57f, "The Crown");
        Landmark(root, "smithy", "red", WestForge, 8, Facing(WestForge), "The West Gate Forge");
        Landmark(root, "store", "green", Sundries, 8, Facing(Sundries), "Pell's Sundries");
        Landmark(root, "smithy", "blue", GiltAnvil, 8, Facing(GiltAnvil), "The Gilt Anvil");
        Landmark(root, "store", "blue", HarrowGoods, 8, Facing(HarrowGoods), "Harrow & Daughters");
        Landmark(root, "tavern", "red", GildedFlagon, 8, Facing(GildedFlagon), "The Gilded Flagon");
        Landmark(root, "tavern", "blue", ScholarsRest, 8, Facing(ScholarsRest), "The Scholar's Rest");
        // Streets (after the landmarks, so they keep clear of them) of houses in each ring, crowded at the bottom, grander at the top.
        Houses(root, rng, 68, 93, 0, 52, ["house", "house", "house", "cottage"]);
        Houses(root, rng, 38, 54, InnerHeight, 18, ["house", "house", "tower"]);

        for (var i = 0; i < 6; i++) // the Warrens: a crush of ruins and hovels
            Buildings.Place(root, i % 2 == 0 ? "ruin" : "cottage", new Vector3(-34 + i * 6.5f, 0, -84 - (i % 3) * 5), (float)rng.NextDouble() * Mathf.Tau, i, Surface.Thatch, 0.85f);
        Label(root, new Vector3(-20, 8, -82), "The Warrens");

        // The Docks: piers out into the harbor, stacked with cargo.
        foreach (var z in Piers)
        {
            Buildings.Place(root, "pier", new Vector3((PierFrom + PierTo) / 2, 0, z), 0, 0);
            for (var k = 0; k < 3; k++)
                Models.Place(root, rng.Chance(0.5) ? "ph:barrel.glb" : "ph:crate.glb",
                    new Vector3(116 + rng.Next(22), 0, z + (float)rng.NextDouble() * 3 - 1.5f), 1, (float)rng.NextDouble() * Mathf.Tau);
        }
        Buildings.Place(root, "boardwalk", new Vector3(BoardwalkX, 0, 0), 0, 0);
        Label(root, new Vector3(120, 5, 0), "The Docks");

        // The Needle: a lighthouse on a rocky island.
        Cylinder(root, Needle + new Vector3(0, -1, 0), 14, 2.4f, "#7a7868");
        Landmark(root, "lighthouse", "blue", Needle + new Vector3(0, 0.2f, 0), 9, 0, "The Needle", labelHeight: 30);
        root.AddChild(new OmniLight3D { Position = Needle + new Vector3(0, 22, 0), LightColor = new Color("#ffd27a"), LightEnergy = 4, OmniRange = 60 });

        // The Tong sea-caves: crags on the north shore.
        Models.Place(root, "medieval/nature/mountain_B.gltf", new Vector3(150, -4.2f, -120), 14, 0.7f);
        Models.Place(root, "medieval/nature/mountain_C.gltf", new Vector3(170, -4.2f, -108), 9, 2.1f);
        Label(root, new Vector3(150, 16, -108), "The Tong Sea-Caves");

        // Lanterns along the gate roads.
        foreach (var angle in new[] { 0f, 180f })
            for (var r = 66; r <= 96; r += 15)
            {
                var a = Mathf.DegToRad(angle);
                foreach (var side in new[] { -6f, 6f })
                    Buildings.Place(root, "lantern", new Vector3(Mathf.Cos(a) * r - Mathf.Sin(a) * side, 0, Mathf.Sin(a) * r + Mathf.Cos(a) * side),
                        a + (side > 0 ? Mathf.Pi : 0), 0); // arms reach over the road
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

    /// <summary>A raised, paved stone platform, with a round body to stand on.</summary>
    static void Disc(Node3D parent, float radius, float top, string colour)
    {
        var platform = new StaticBody3D { Position = new Vector3(0, 0, 0) };
        platform.SetMeta("ground", true);
        platform.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = top }, Position = new Vector3(0, top / 2, 0) });
        parent.AddChild(platform);
        var look = Buildings.Place(parent, FormattableString.Invariant($"disc:{radius}:{top}"), Vector3.Zero, 0, 0);
        foreach (var c in look.GetChildren().OfType<CollisionShape3D>()) c.QueueFree(); // the cylinder above does the colliding
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
        // Low at its outer end on the lower ring, rising to meet the gate (a positive tilt would lift the outer end
        // instead, and leave it standing up like a half-raised drawbridge).
        ramp.Rotation = new Vector3(0, -rad, -Mathf.Atan2(rise, length));
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
            if (Placed.Any(p => new Vector2(p.X - pos.X, p.Z - pos.Z).Length() < 10.5f)) continue; // no two houses in the same spot
            var roof = rng.Chance(0.6) ? Surface.ClayTile : rng.Chance(0.5) ? Surface.Slate : Surface.Shingle;
            Buildings.Place(parent, rng.Pick(kinds), pos, Mathf.Atan2(-pos.X, -pos.Z), placed + (int)(r * 7), roof); // front door to the centre
            Placed.Add(pos);
            placed++;
        }
    }

    static readonly List<Vector3> Placed = [];

    /// <summary>The turn that puts a building's front door towards the city's centre.</summary>
    static float Facing(Vector3 at) => Mathf.Atan2(-at.X, -at.Z);

    static Node3D Landmark(Node3D parent, string kind, string colour, Vector3 at, float scale, float yaw, string name, float labelHeight = 0)
    {
        var b = Buildings.Place(parent, kind, at, yaw, (int)(at.X * 13 + at.Z), Buildings.RoofFor(colour), Mathf.Clamp(scale / 8, 0.95f, 1.2f));
        Placed.Add(at);
        var height = labelHeight > 0 ? labelHeight : Buildings.HeightOf(b) + 3;
        Label(parent, at + new Vector3(0, height, 0), name);
        return b;
    }

    /// <summary>A ring of stone wall, with a gap (or a gatehouse) at each gate.</summary>
    static void Walls(Node3D parent, float radius, float baseY, string kind, float scale, float[] gates, bool gatehouses = false)
    {
        var length = (kind == "wall" ? 10 : 8) * scale * 0.96f; // pieces overlap a little round the curve
        var count = (int)Math.Ceiling(Mathf.Tau * radius / length);
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            if (gates.Any(g => Math.Abs(Mathf.Wrap(Mathf.RadToDeg(a) - g, -180, 180)) < 6)) continue;
            Buildings.Place(parent, kind, new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius), -a - Mathf.Pi / 2, 0, scale: scale);
        }
        if (!gatehouses) return;
        foreach (var g in gates)
        {
            var a = Mathf.DegToRad(g);
            Buildings.Place(parent, "gate", new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius), Mathf.Atan2(Mathf.Cos(a), Mathf.Sin(a)), 0);
        }
    }

    static void Label(Node3D parent, Vector3 pos, string text) => parent.AddChild(new Label3D
    {
        Text = text, Position = pos, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12,
        Modulate = new Color("#e8dcc4"), PixelSize = 0.02f,
    });
}
