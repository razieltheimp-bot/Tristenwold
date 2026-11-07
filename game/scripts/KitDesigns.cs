using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The game's own weapons and shields for the cartoon figures, each started from one of KayKit's (CC0, the Adventurers
/// pack, in assets/kaykit/weapons) and made over: the mesh itself reshaped (a blade drawn out, narrowed, curved or
/// waved, an axe given a beard or a crescent, a shield cut to a kite) and things of our own built onto it (wrapped
/// grips, fullers, quillons, spines, gems, runes, rivets, bosses, crystals, chains). Every piece is in KayKit's own
/// frame: its length up +Y from the grip (a crossbow's along +Z), a shield's face toward +Z.
/// </summary>
public static class KitDesigns
{
    /// <summary>One design: the KayKit piece it starts from (none for something built whole), how its mesh is moved, and what's put on it.</summary>
    sealed record Design(string? Piece, Func<Vector3, Vector3>? Shape, Action<Node3D>? Adorn);

    static readonly Dictionary<string, string> Files = new()
    {
        ["1H_Sword"] = "sword_1handed", ["2H_Sword"] = "sword_2handed", ["1H_Axe"] = "axe_1handed", ["2H_Axe"] = "axe_2handed",
        ["Knife"] = "dagger", ["Knife_Offhand"] = "dagger", ["2H_Staff"] = "staff", ["Staff"] = "staff", ["1H_Wand"] = "wand",
        ["1H_Crossbow"] = "crossbow_1handed", ["2H_Crossbow"] = "crossbow_2handed",
        ["Round_Shield"] = "shield_round", ["Barbarian_Round_Shield"] = "shield_round_barbarian", ["Badge_Shield"] = "shield_badge",
        ["Spike_Shield"] = "shield_spikes", ["Rectangle_Shield"] = "shield_square",
    };

    static readonly Dictionary<string, Mesh?> Meshes = [];

    /// <summary>KayKit's own mesh for a piece, as it comes.</summary>
    public static Mesh? Base(string piece)
    {
        if (!Files.TryGetValue(piece, out var file)) return null;
        if (Meshes.TryGetValue(file, out var known)) return known;
        var scene = Models.Scene($"weapons/{file}.gltf")?.Instantiate() as Node3D;
        var mesh = scene?.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh;
        scene?.Free();
        return Meshes[file] = mesh;
    }

    /// <summary>Whether an item has a design of its own.</summary>
    public static bool Has(string id) => Designs.ContainsKey(id);

    /// <summary>The names of the items with designs, in order (for pictures).</summary>
    public static IEnumerable<string> All => Designs.Keys;

    /// <summary>
    /// An item's own piece, built and painted, placed as KayKit's would be (<paramref name="placed"/>), or null if it
    /// has no design (then KayKit's own is worn, as before).
    /// </summary>
    public static Node3D? Build(Item item, Transform3D placed)
    {
        if (item.Forged || !Designs.TryGetValue(item.Id, out var d)) return null;
        var root = new MeshInstance3D { Name = $"Worn_{item.Id}", Transform = placed };
        if (d.Piece is { } piece)
        {
            if (Base(piece) is not { } mesh) return null;
            root.Mesh = d.Shape is null ? mesh : Reshape(mesh, d.Shape);
            Styled.Apply(root);
            // The colour of what it's made of, over KayKit's; any glow is in the parts put on it, not the whole piece.
            if (item.Tint is { } tint)
            {
                var c = new Color(tint);
                if (!item.Metal && c.Luminance > 0.75f) c *= 1.7f; // bone and the like: pale through, not a grey with a cream cast
                Wardrobe.Paint(root, c, item.Metal, null);
            }
        }
        d.Adorn?.Invoke(root);
        return root;
    }

    /// <summary>A mesh with every point moved, and its normals worked out again for the new shape.</summary>
    static ArrayMesh Reshape(Mesh mesh, Func<Vector3, Vector3> move)
    {
        var result = new ArrayMesh();
        for (var s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            var arrays = mesh.SurfaceGetArrays(s);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for (var i = 0; i < points.Length; i++) points[i] = move(points[i]);
            arrays[(int)Mesh.ArrayType.Vertex] = points;
            arrays[(int)Mesh.ArrayType.Normal] = new Variant();
            arrays[(int)Mesh.ArrayType.Tangent] = new Variant();
            var st = new SurfaceTool();
            st.CreateFromArrays(arrays);
            st.GenerateNormals();
            var made = st.Commit();
            result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, made.SurfaceGetArrays(0));
            result.SurfaceSetMaterial(s, mesh.SurfaceGetMaterial(s));
        }
        return result;
    }

    // ---- what the additions are made of, in the cartoon figures' own style -----------------------------------------

    static readonly Dictionary<string, Material> Mats = [];

    static Material M(string hex, bool metal = false, string? glow = null, float energy = 1.6f)
    {
        var key = $"{hex}|{metal}|{glow}|{energy}";
        if (Mats.TryGetValue(key, out var m)) return m;
        return Mats[key] = Styled.Painted(new StandardMaterial3D { AlbedoColor = new Color(hex) }, Colors.White, glow, glow is null ? 0 : energy, metal);
    }

    static Material Leather => M("#4a3020");
    static Material DarkLeather => M("#2e1e14");
    static Material Iron => M("#5a5e66", true);
    static Material Steel => M("#c8d0da", true);
    static Material Gold => Shiny("#d8a030");
    static Material Brass => Shiny("#b8863a");

    /// <summary>Gold and brass in a plain metal material: the cartoon shader reads a warm colour as skin, or greys it if it's forced to metal.</summary>
    static Material Shiny(string hex)
    {
        if (Mats.TryGetValue(hex, out var m)) return m;
        return Mats[hex] = new StandardMaterial3D { AlbedoColor = new Color(hex), Metallic = 0.85f, Roughness = 0.32f, RimEnabled = true, Rim = 0.2f };
    }
    static Material Bone => M("#efe4c8");
    static Material Wood => M("#6a4a2c");

    static void Part(Node3D on, Mesh mesh, Material m, Vector3 at, Vector3? degrees = null, Vector3? scale = null) =>
        on.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = m, Position = at, RotationDegrees = degrees ?? Vector3.Zero, Scale = scale ?? Vector3.One });

    static TorusMesh Ring(float inner, float outer) => new() { InnerRadius = inner, OuterRadius = outer, Rings = 18, RingSegments = 8 };
    static SphereMesh Ball(float r) => new() { Radius = r, Height = r * 2, RadialSegments = 14, Rings = 7 };
    static CylinderMesh Rod(float r, float h, int sides = 10) => new() { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = sides };
    static CylinderMesh Spike(float r, float h, int sides = 8) => new() { TopRadius = 0, BottomRadius = r, Height = h, RadialSegments = sides };
    static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };

    /// <summary>Leather wound round a grip, a band at each height.</summary>
    static void Wrap(Node3D on, float r, Material m, params float[] ys)
    {
        foreach (var y in ys) Part(on, Ring(r * 0.75f, r * 1.12f), m, new Vector3(0, y, 0));
    }

    // ---- the designs ----------------------------------------------------------------------------------------------

    // The one-handed sword (KayKit's): pommel -0.37 to -0.14, grip to 0.08, guard to 0.30 (0.25 either side), blade
    // to its point at 1.41, 0.16 either side at the guard. The greatsword: grip -0.11 to 0.04, crossbar at 0.34-0.49
    // (0.42 either side), blade 0.63 to 1.96. The dagger: guard 0.08-0.30, blade to 0.98. The one-handed axe's head
    // is on -X from 0.27 to 0.97, its edge at -0.59; the greataxe's two heads from 0.43 to 1.29, edges at 0.62.
    // The staff's knob is 0.72 to 1.25. Shields face +Z, a round one 0.44 across, the tower 0.44 by 0.6.

    /// <summary>The staff with its knob taken off: the top shrunk down onto the shaft, to build something else there.</summary>
    static Vector3 Knobless(Vector3 p) => p.Y > 0.72f ? new Vector3(p.X * 0.35f, 0.72f + (p.Y - 0.72f) * 0.3f, p.Z * 0.35f) : p;

    static readonly Dictionary<string, Design> Designs = new()
    {
        // ---- swords ----
        // The arming sword: a slimmer blade with a dark fuller down it, the grip bound in leather.
        ["iron_sword"] = new("1H_Sword", p => p.Y > 0.3f ? p with { X = p.X * 0.86f } : p, s =>
        {
            Wrap(s, 0.06f, Leather, -0.1f, -0.04f, 0.02f);
            foreach (var z in new[] { -0.05f, 0.05f }) Part(s, Box(0.036f, 0.78f, 0.012f), M("#2a2e36", true), new Vector3(0, 0.78f, z));
        }),
        // The longsword: a longer, narrower blade, a broad guard with knobbed quillons, a brass collar, a blue stone in the pommel.
        ["steel_sword"] = new("1H_Sword", p => p.Y switch
        {
            > 0.3f => new Vector3(p.X * 0.8f, 0.3f + (p.Y - 0.3f) * 1.2f, p.Z),
            > 0.08f => p with { X = p.X * 1.25f },
            _ => p,
        }, s =>
        {
            foreach (var x in new[] { -0.33f, 0.33f }) Part(s, Ball(0.045f), Steel, new Vector3(x, 0.19f, 0));
            Part(s, Box(0.24f, 0.05f, 0.1f), Brass, new Vector3(0, 0.33f, 0));
            foreach (var z in new[] { -0.05f, 0.05f }) Part(s, Box(0.034f, 1.0f, 0.012f), M("#2a2e36", true), new Vector3(0, 0.95f, z));
            Part(s, Ball(0.05f), M("#3a7ad8", glow: "#5a9aff", energy: 0.8f), new Vector3(0, -0.3f, 0.07f));
            Wrap(s, 0.06f, DarkLeather, -0.1f, -0.04f, 0.02f);
        }),
        // Wyrmfang: a curving blade of pale bone, its back cut in teeth, horns off the guard and a dragon's amber eye in it.
        ["dragonbone_sword"] = new("1H_Sword", p =>
        {
            if (p.Y <= 0.3f) return p;
            var t = (p.Y - 0.3f) / 1.11f;
            return p with { X = p.X * 1.1f + 0.14f * t * t };
        }, s =>
        {
            for (var k = 0; k < 6; k++)
            {
                var y = 0.42f + k * 0.15f;
                var t = (y - 0.3f) / 1.11f;
                var back = -0.176f * (1 - t) + 0.14f * t * t;
                Part(s, new PrismMesh { Size = new Vector3(0.08f, 0.09f, 0.03f) }, Bone, new Vector3(back - 0.025f, y, 0), new Vector3(0, 0, 90));
            }
            foreach (var side in new[] { -1f, 1f }) Part(s, Spike(0.04f, 0.24f), Bone, new Vector3(side * 0.27f, 0.32f, 0), new Vector3(0, 0, -side * 30));
            foreach (var z in new[] { -0.075f, 0.075f }) Part(s, Ball(0.045f), M("#e8a020", glow: "#ffb030", energy: 1.2f), new Vector3(0, 0.19f, z), scale: new Vector3(1, 0.7f, 0.5f));
            Wrap(s, 0.06f, M("#5a2a1e"), -0.1f, -0.03f, 0.04f);
        }),
        // The zweihander: parrying hooks off the blade, its ricasso and grip bound.
        ["iron_greatsword"] = new("2H_Sword", null, s =>
        {
            foreach (var side in new[] { -1f, 1f }) Part(s, new PrismMesh { Size = new Vector3(0.12f, 0.1f, 0.035f) }, Iron, new Vector3(side * 0.21f, 0.8f, 0), new Vector3(0, 0, side * -90));
            Part(s, Box(0.3f, 0.05f, 0.1f), Leather, new Vector3(0, 0.7f, 0));
            Part(s, Box(0.3f, 0.05f, 0.1f), Leather, new Vector3(0, 0.76f, 0));
            Wrap(s, 0.065f, Leather, -0.08f, -0.02f, 0.03f);
        }),
        // The flamberge: a waved blade, gold on the crossbar and the pommel.
        ["steel_greatsword"] = new("2H_Sword", p =>
        {
            if (p.Y <= 0.7f) return p;
            var t = (p.Y - 0.7f) / 1.26f;
            return p with { X = p.X + 0.085f * Mathf.Sin((p.Y - 0.7f) * 13) * Mathf.Min(1, (p.Y - 0.7f) / 0.12f) * (1 - 0.6f * t) };
        }, s =>
        {
            Part(s, Ball(0.12f), Gold, new Vector3(0, -0.36f, 0));
            foreach (var x in new[] { -0.45f, 0.45f }) Part(s, Ball(0.07f), Gold, new Vector3(x, 0.42f, 0));
            Wrap(s, 0.065f, DarkLeather, -0.08f, -0.02f, 0.03f);
        }),
        // Spinecleaver: a blade broadening to a cleaver's belly, spines down its edge, a red stone in the crossbar.
        ["dragonbone_greatsword"] = new("2H_Sword", p =>
        {
            if (p.Y <= 0.63f) return p;
            var t = (p.Y - 0.63f) / 1.33f;
            return p with { X = p.X * (1 + 1.1f * t * (1 - t)) };
        }, s =>
        {
            for (var k = 0; k < 5; k++)
            {
                var y = 0.78f + k * 0.22f;
                var t = (y - 0.63f) / 1.33f;
                Part(s, Spike(0.035f, 0.16f), Bone, new Vector3(0.15f * (1 - t * 0.85f) * (1 + 1.1f * t * (1 - t)) + 0.05f, y, 0), new Vector3(0, 0, -100));
            }
            foreach (var z in new[] { -0.12f, 0.12f }) Part(s, Ball(0.07f), M("#c02010", glow: "#ff4020", energy: 1.4f), new Vector3(0, 0.42f, z), scale: new Vector3(1, 1, 0.5f));
            Wrap(s, 0.065f, M("#5a2a1e"), -0.08f, -0.02f, 0.03f);
        }),
        // Soren's boarding cutlass: a broad, sweeping blade under a knuckle-bow, black, with an ember in the pommel.
        ["sorens_reaver"] = new("2H_Sword", p =>
        {
            if (p.Y > 0.63f)
            {
                var t = (p.Y - 0.63f) / 1.33f;
                return new Vector3(p.X * 1.35f + 0.38f * t * t, 0.63f + (p.Y - 0.63f) * 0.82f, p.Z);
            }
            return p.Y > 0.3f ? p with { X = p.X * 0.55f } : p;
        }, s =>
        {
            Part(s, Ring(0.17f, 0.21f), M("#1c1a1a", true), new Vector3(0.14f, 0.12f, 0), new Vector3(90, 0, 0), new Vector3(0.75f, 1.35f, 1));
            Part(s, Ball(0.09f), M("#601010", glow: "#ff5020", energy: 1.6f), new Vector3(0, -0.36f, 0));
            Wrap(s, 0.065f, M("#3a1010"), -0.08f, -0.02f, 0.03f);
        }),
        // The Aether Gauntlet: a brass fist with the steam behind it, made whole rather than from a blade.
        ["aether_gauntlet"] = new(null, null, root =>
        {
            var s = new Node3D { Scale = Vector3.One * 1.7f };
            root.AddChild(s);
            Part(s, Rod(0.15f, 0.26f, 12), Brass, new Vector3(0, -0.12f, 0));
            Part(s, Box(0.3f, 0.3f, 0.22f), Brass, new Vector3(0, 0.16f, 0));
            for (var i = 0; i < 4; i++) Part(s, Box(0.06f, 0.14f, 0.08f), Brass, new Vector3(-0.105f + i * 0.07f, 0.37f, 0.03f), new Vector3(-25, 0, 0));
            Part(s, Box(0.08f, 0.16f, 0.08f), Brass, new Vector3(0.18f, 0.18f, 0.06f), new Vector3(0, 0, -35));
            Part(s, Rod(0.025f, 0.34f), M("#3a8aa0", glow: "#7fd8ff", energy: 1.8f), new Vector3(-0.17f, 0.05f, -0.06f));
            Part(s, Ball(0.06f), M("#e8e0c0", true), new Vector3(0, 0.12f, 0.12f), scale: new Vector3(1, 1, 0.4f));
        }),

        // ---- axes and the maul ----
        // The bearded axe: the head drawn down into a beard below the haft, the grip bound.
        ["iron_axe"] = new("1H_Axe", p =>
        {
            if (p.X >= -0.12f || p.Y >= 0.62f) return p;
            var t = Mathf.Clamp((-p.X - 0.12f) / 0.47f, 0, 1);
            return p with { Y = p.Y - (0.62f - p.Y) * 0.75f * t };
        }, s =>
        {
            Wrap(s, 0.075f, Leather, -0.16f, -0.09f, -0.02f);
            Part(s, Ring(0.06f, 0.085f), Iron, new Vector3(0, 0.2f, 0));
        }),
        // The raider's axe: a broader head with a spike behind it and a point on top.
        ["steel_axe"] = new("1H_Axe", p => p.X < -0.12f ? p with { X = -0.12f + (p.X + 0.12f) * 1.12f } : p, s =>
        {
            Part(s, Spike(0.06f, 0.3f), Steel, new Vector3(0.27f, 0.66f, 0), new Vector3(0, 0, -90));
            Part(s, Spike(0.05f, 0.22f), Steel, new Vector3(0, 1.07f, 0));
            Wrap(s, 0.075f, DarkLeather, -0.16f, -0.09f, -0.02f);
        }),
        // The broad axe: both heads wider, iron bands under them.
        ["iron_greataxe"] = new("2H_Axe", p => p.Y > 0.43f && Mathf.Abs(p.X) > 0.1f ? p with { X = p.X * 1.15f } : p, s =>
        {
            foreach (var y in new[] { 0.26f, 0.36f }) Part(s, Ring(0.08f, 0.11f), Iron, new Vector3(0, y, 0));
            Wrap(s, 0.085f, Leather, -0.3f, -0.22f, -0.14f);
        }),
        // The crescent axe: each head swept into a moon, a spike on top.
        ["steel_greataxe"] = new("2H_Axe", p =>
        {
            if (p.Y <= 0.43f || Mathf.Abs(p.X) <= 0.1f) return p;
            var t = (Mathf.Abs(p.X) - 0.1f) / 0.52f;
            return new Vector3(p.X * 1.05f, 0.86f + (p.Y - 0.86f) * (1 + 0.65f * t), p.Z);
        }, s =>
        {
            Part(s, Spike(0.06f, 0.32f), Steel, new Vector3(0, 1.44f, 0));
            Wrap(s, 0.085f, DarkLeather, -0.3f, -0.22f, -0.14f);
        }),
        // The Warden's cinder axe: crescent heads, cracked through with fire, embers in its bands.
        ["cinder_greataxe"] = new("2H_Axe", p =>
        {
            if (p.Y <= 0.43f || Mathf.Abs(p.X) <= 0.1f) return p;
            var t = (Mathf.Abs(p.X) - 0.1f) / 0.52f;
            return new Vector3(p.X * 1.08f, 0.86f + (p.Y - 0.86f) * (1 + 0.4f * t), p.Z);
        }, s =>
        {
            var fire = M("#ff6a20", glow: "#ff6a20", energy: 2.4f);
            foreach (var side in new[] { -1f, 1f })
                foreach (var z in new[] { -0.105f, 0.105f })
                {
                    Part(s, Box(0.3f, 0.025f, 0.012f), fire, new Vector3(side * 0.35f, 0.8f, z), new Vector3(0, 0, side * 25));
                    Part(s, Box(0.18f, 0.02f, 0.012f), fire, new Vector3(side * 0.42f, 0.98f, z), new Vector3(0, 0, side * -35));
                }
            foreach (var y in new[] { 0.26f, 0.36f }) Part(s, Ring(0.08f, 0.11f), M("#2a2018", true), new Vector3(0, y, 0));
            for (var i = 0; i < 4; i++) Part(s, Ball(0.025f), fire, new Vector3(Mathf.Cos(i * 1.57f) * 0.1f, 0.31f, Mathf.Sin(i * 1.57f) * 0.1f));
        }),
        // Korrun's heartwood maul: the axe heads gone, a great block of living wood on the haft, root-bound, with violet knots.
        ["barkbreaker_maul"] = new("2H_Axe", p => p.Y > 0.4f && Mathf.Abs(p.X) > 0.08f ? p with { X = p.X * 0.12f } : p, s =>
        {
            Part(s, Box(0.62f, 0.42f, 0.42f), M("#6a4a2c"), new Vector3(0, 0.92f, 0));
            foreach (var x in new[] { -0.22f, 0.22f }) Part(s, Ring(0.21f, 0.26f), M("#4a3220"), new Vector3(x, 0.92f, 0), new Vector3(0, 0, 90));
            foreach (var (x, z) in new[] { (0.12f, 0.21f), (-0.18f, -0.21f), (0.31f, 0.05f) })
                Part(s, Ball(0.055f), M("#a040e0", glow: "#d070ff", energy: 1.8f), new Vector3(x, 0.95f, z));
            for (var i = 0; i < 4; i++) Part(s, Ball(0.07f), M("#5a8a3a"), new Vector3(-0.25f + i * 0.17f, 1.14f, (i % 2 - 0.5f) * 0.2f), scale: new Vector3(1, 0.5f, 1));
        }),

        // ---- daggers ----
        ["iron_dagger"] = new("Knife", null, s => Wrap(s, 0.06f, Leather, -0.02f, 0.04f)),
        // The stiletto: a needle of a blade, longer, with a ring for a pommel.
        ["steel_dagger"] = new("Knife", p => p.Y > 0.3f ? new Vector3(p.X * 0.55f, 0.3f + (p.Y - 0.3f) * 1.2f, p.Z) : p, s =>
        {
            Part(s, Ring(0.05f, 0.08f), Steel, new Vector3(0, -0.3f, 0), new Vector3(90, 0, 0));
            Wrap(s, 0.06f, DarkLeather, -0.02f, 0.04f);
        }),
        // Duskfang: a waved, curving blade with violet runes down it.
        ["shadow_dagger"] = new("Knife", p =>
        {
            if (p.Y <= 0.3f) return p;
            var t = (p.Y - 0.3f) / 0.68f;
            return p with { X = p.X + 0.035f * Mathf.Sin(t * Mathf.Pi * 3) + 0.08f * t * t };
        }, s =>
        {
            foreach (var (y, k) in new[] { (0.44f, 0.14f), (0.6f, 0.3f), (0.76f, 0.5f) })
                foreach (var z in new[] { -0.04f, 0.04f })
                    Part(s, Ball(0.022f), M("#8040ff", glow: "#a050ff", energy: 2f), new Vector3(0.035f * Mathf.Sin(k * Mathf.Pi * 3) + 0.08f * k * k, y, z));
        }),
        // Blightthorn: thorns down both edges, a green vine wound round the grip, buds that glow.
        ["blightthorn"] = new("Knife", p => p.Y > 0.3f ? p with { X = p.X * 0.9f } : p, s =>
        {
            var thorn = M("#2a3a22");
            for (var k = 0; k < 6; k++)
            {
                var y = 0.38f + k * 0.1f;
                var side = k % 2 == 0 ? -1f : 1f;
                Part(s, Spike(0.025f, 0.1f), thorn, new Vector3(side * 0.11f * (1 - (y - 0.3f) / 0.75f), y, 0), new Vector3(0, 0, side * 70));
            }
            Wrap(s, 0.06f, M("#3a6a2a"), -0.05f, 0.01f, 0.07f);
            Part(s, Ball(0.03f), M("#60c030", glow: "#80ff40", energy: 2f), new Vector3(0, 0.2f, 0.08f));
        }),
        // The parrying dagger: two prongs off the guard to catch a blade.
        ["parrying_dagger"] = new("Knife_Offhand", null, s =>
        {
            foreach (var side in new[] { -1f, 1f }) Part(s, Rod(0.022f, 0.3f), Steel, new Vector3(side * 0.17f, 0.38f, 0), new Vector3(0, 0, -side * 18));
            Wrap(s, 0.06f, Leather, -0.02f, 0.04f);
        }),

        // ---- staffs and the wand ----
        // The hedge staff: charms hung under the knob, and a cloth wrap where the hand goes.
        ["apprentice_staff"] = new("2H_Staff", null, s =>
        {
            foreach (var (x, z, hex) in new[] { (0.16f, 0.06f, "#c03a2a"), (-0.13f, -0.07f, "#3a6ac0"), (0.02f, 0.17f, "#e0b040") })
            {
                Part(s, Rod(0.008f, 0.24f, 5), M("#e8e0c8"), new Vector3(x, 0.6f, z));
                Part(s, Ball(0.055f), M(hex), new Vector3(x, 0.46f, z));
            }
            Wrap(s, 0.08f, M("#8a3a2a"), 0.18f, 0.26f, 0.34f);
        }),
        // The amethyst staff: the knob gone, and a great cluster of glowing crystal grown out of the top instead.
        ["amethyst_staff"] = new("2H_Staff", Knobless, s =>
        {
            var crystal = M("#9a6ad8", glow: "#9a50ff", energy: 1.5f);
            Part(s, Spike(0.13f, 0.62f, 6), crystal, new Vector3(0, 1.2f, 0));
            foreach (var (x, z, lean) in new[] { (0.12f, 0.04f, -32f), (-0.12f, 0.05f, 34f), (0.02f, -0.13f, 0f), (-0.05f, 0.12f, 18f) })
                Part(s, Spike(0.08f, 0.4f, 6), crystal, new Vector3(x, 1.06f, z), new Vector3(z * 220, 0, lean));
            Part(s, Ring(0.1f, 0.14f), Gold, new Vector3(0, 0.9f, 0));
        }),
        // The Stormcaller's staff: the knob gone, forked tines holding a ball of lightning in a ring.
        ["storm_staff"] = new("2H_Staff", Knobless, s =>
        {
            foreach (var side in new[] { -1f, 1f }) Part(s, Rod(0.035f, 0.56f), Steel, new Vector3(side * 0.15f, 1.12f, 0), new Vector3(0, 0, -side * 20));
            Part(s, Ring(0.08f, 0.12f), Steel, new Vector3(0, 0.88f, 0));
            Part(s, Ball(0.15f), M("#8ad0ff", glow: "#8ad0ff", energy: 2.6f), new Vector3(0, 1.36f, 0));
            Part(s, Ring(0.2f, 0.23f), M("#e0f0ff", true), new Vector3(0, 1.36f, 0), new Vector3(70, 0, 20));
        }),
        // Ulmoth's living staff: broad leaves out of the knob, vines down the shaft, and buds that glow.
        ["mosswarden_staff"] = new("2H_Staff", null, s =>
        {
            for (var i = 0; i < 7; i++)
            {
                var a = i * Mathf.Tau / 7;
                Part(s, Ball(0.15f), M("#4a8a3a"), new Vector3(Mathf.Cos(a) * 0.3f, 1.0f + (i % 2) * 0.12f, Mathf.Sin(a) * 0.3f), new Vector3(0, -Mathf.RadToDeg(a), 35), new Vector3(1, 0.22f, 0.5f));
            }
            foreach (var y in new[] { -0.4f, 0f, 0.35f }) Part(s, Ring(0.07f, 0.1f), M("#3a6a2a"), new Vector3(0, y, 0), new Vector3(12, 0, 0));
            foreach (var y in new[] { -0.2f, 0.2f, 0.55f }) Part(s, Ball(0.045f), M("#80e040", glow: "#a0ff60", energy: 2f), new Vector3(0.08f, y, 0.06f));
        }),
        // Lu Wukong's chained staff: a plain rod, gold-capped at both ends, chains and red ribbons at the top.
        ["wukongs_staff"] = new("Staff", p => p.Y > 0.7f ? new Vector3(p.X * 0.35f, p.Y, p.Z * 0.35f) : p, s =>
        {
            foreach (var y in new[] { -0.84f, 1.2f }) Part(s, Rod(0.12f, 0.16f, 12), Gold, new Vector3(0, y, 0));
            for (var i = 0; i < 4; i++) Part(s, Ring(0.025f, 0.04f), Iron, new Vector3(0.14f, 1.08f - i * 0.06f, 0), new Vector3(i % 2 * 90, 0, 0));
            foreach (var side in new[] { -1f, 1f }) Part(s, Box(0.05f, 0.3f, 0.01f), M("#c02020"), new Vector3(side * 0.13f, 0.98f, 0), new Vector3(0, 0, side * 12));
        }),
        // The oak wand: carved rings up it and a green stone at the tip.
        ["oak_wand"] = new("1H_Wand", null, s =>
        {
            foreach (var y in new[] { 0.22f, 0.36f, 0.5f }) Part(s, Ring(0.035f, 0.05f), M("#8a5a30"), new Vector3(0, y, 0));
            Part(s, Ball(0.045f), M("#3aa060", glow: "#60e090", energy: 1.4f), new Vector3(0, 0.74f, 0));
        }),

        // ---- crossbows ----
        // The hunting crossbow: a fur-trimmed stock and a sling.
        ["hunting_crossbow"] = new("1H_Crossbow", null, s =>
        {
            Part(s, Box(0.14f, 0.05f, 0.22f), M("#7a5a3a"), new Vector3(0, -0.12f, -0.08f));
            Part(s, Box(0.02f, 0.02f, 0.7f), Leather, new Vector3(-0.1f, -0.16f, 0.25f));
        }),
        // The heavy crossbow: a windlass to wind it, its prod plated in steel.
        ["heavy_crossbow"] = new("2H_Crossbow", null, s =>
        {
            Part(s, Rod(0.06f, 0.32f), Iron, new Vector3(0, 0.02f, -0.22f), new Vector3(0, 0, 90));
            foreach (var side in new[] { -1f, 1f })
            {
                Part(s, Box(0.02f, 0.16f, 0.03f), Iron, new Vector3(side * 0.17f, 0.07f, -0.22f));
                Part(s, Box(0.24f, 0.05f, 0.08f), Steel, new Vector3(side * 0.42f, 0.12f, 0.5f), new Vector3(0, side * 8, 0));
            }
        }),

        // ---- shields ----
        // The wooden shield: planks showing, an iron rim of rivets.
        ["wooden_shield"] = new("Barbarian_Round_Shield", null, s =>
        {
            foreach (var x in new[] { -0.22f, 0.22f }) Part(s, Box(0.014f, 0.62f, 0.02f), DarkLeather, new Vector3(x, 0, 0.1f));
            for (var i = 0; i < 12; i++) Part(s, Ball(0.022f), Iron, new Vector3(Mathf.Cos(i * Mathf.Tau / 12) * 0.38f, Mathf.Sin(i * Mathf.Tau / 12) * 0.38f, 0.09f));
        }),
        // The iron shield: a great domed boss, iron bands crossed over the face, a ring of rivets.
        ["iron_shield"] = new("Round_Shield", null, s =>
        {
            foreach (var turn in new[] { 45f, -45f }) Part(s, Box(0.06f, 0.8f, 0.02f), Iron, new Vector3(0, 0, 0.11f), new Vector3(0, 0, turn));
            Part(s, Ball(0.13f), Iron, new Vector3(0, 0, 0.16f), scale: new Vector3(1, 1, 0.6f));
            for (var i = 0; i < 8; i++) Part(s, Ball(0.022f), Steel, new Vector3(Mathf.Cos(i * Mathf.Tau / 8 + 0.4f) * 0.32f, Mathf.Sin(i * Mathf.Tau / 8 + 0.4f) * 0.32f, 0.11f));
        }),
        // The steel tower shield, cut to a kite: a ridge down the middle, gold at the top.
        ["steel_shield"] = new("Rectangle_Shield", p => p.Y < 0 ? p with { X = p.X * (1 - 0.6f * Mathf.Min(1, -p.Y / 0.6f)) } : p, s =>
        {
            Part(s, Box(0.06f, 1.0f, 0.04f), Steel, new Vector3(0, 0.02f, 0.13f));
            Part(s, Box(0.7f, 0.05f, 0.04f), Gold, new Vector3(0, 0.44f, 0.12f));
            Part(s, Ball(0.06f), Gold, new Vector3(0, 0.44f, 0.15f));
        }),
        // The spiked shield: its spikes drawn out, a ring of rivets.
        ["spiked_shield"] = new("Spike_Shield", p => p.Z > 0.15f ? p with { Z = 0.15f + (p.Z - 0.15f) * 1.8f } : p, s =>
        {
            for (var i = 0; i < 10; i++) Part(s, Ball(0.02f), Iron, new Vector3(Mathf.Cos(i * Mathf.Tau / 10) * 0.42f, Mathf.Sin(i * Mathf.Tau / 10) * 0.42f, 0.09f));
        }),
        // The Hollow Legion's shield: a ring and a star of moonlight on its face.
        ["hollow_shield"] = new("Badge_Shield", null, s =>
        {
            var moon = M("#a8e0f0", glow: "#6ac8ff", energy: 2f);
            Part(s, Ring(0.13f, 0.17f), moon, new Vector3(0, 0.02f, 0.1f), new Vector3(90, 0, 0));
            Part(s, Ball(0.06f), moon, new Vector3(0, 0.02f, 0.11f));
            foreach (var turn in new[] { 0f, 90f }) Part(s, Box(0.03f, 0.5f, 0.015f), moon, new Vector3(0, 0.02f, 0.1f), new Vector3(0, 0, turn));
        }),
        // The spring buckler: a brass gear on its face, and the coil it springs on.
        ["spring_buckler"] = new("Round_Shield", null, s =>
        {
            Part(s, Ring(0.16f, 0.22f), Brass, new Vector3(0, 0, 0.12f), new Vector3(90, 0, 0));
            for (var i = 0; i < 10; i++)
            {
                var a = i * Mathf.Tau / 10;
                Part(s, Box(0.05f, 0.05f, 0.04f), Brass, new Vector3(Mathf.Cos(a) * 0.24f, Mathf.Sin(a) * 0.24f, 0.12f), new Vector3(0, 0, Mathf.RadToDeg(a)));
            }
            for (var i = 0; i < 4; i++) Part(s, Ring(0.05f, 0.065f), M("#b0b4b8", true), new Vector3(0, 0, 0.17f + i * 0.03f), new Vector3(90, 0, 0));
        }),
    };
}
