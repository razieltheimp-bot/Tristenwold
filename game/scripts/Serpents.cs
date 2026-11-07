using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Serpents: one long body that ripples as it goes, built whole from shapes and drawn by a shader that bends it, so it
/// costs nothing to move.
/// <list type="bullet">
/// <item>The sea serpent (after the user's teal-and-rose picture): a long sea-blue body, pale beneath, a rose fin
/// webbed between spines all down its back, fans of fin at its neck and behind its head, long trailing whiskers, swept
/// horns and a fan at the tail. It rolls through the waves in humps.</item>
/// <item>The leviathan (after the spined monster on the cliffs): huge and slate-dark, a double row of bone spikes down
/// its back, a crown of spikes, great webbed frills at its neck, jaws open on rows of teeth.</item>
/// <item>The ember wyrm, the land serpent (after the fire dragon): long and scaled red-gold with pale belly plates, four
/// clawed legs, a mane of flame down its spine and at its elbows, antlers, a dark beard, long whiskers and a tufted
/// tail. It snakes along the ground. Irohe's flame serpent is one of these made all of fire.</item>
/// </list>
/// The body lies along +Z from the head (the front is -Z, as for every body); the shader sways it side to side and
/// heaves it up and down in a wave that runs down it as it moves, steps the legs, and throws the head forward to bite.
/// </summary>
public partial class Serpent : Body
{
    public enum Look { Sea, Leviathan, Land, Flame }

    MeshInstance3D _mesh = null!;
    float _clock, _speed, _lunge, _hurt, _rate, _calm = 1;
    bool _dead;
    Look _look;

    /// <summary>Its length, head to tail, in metres.</summary>
    public float Length { get; private set; }

    public static Serpent Create(Look look)
    {
        var s = new Serpent { _look = look };
        var (length, r) = look switch { Look.Sea => (14f, 0.45f), Look.Leviathan => (26f, 1.05f), Look.Flame => (8f, 0.32f), _ => (9f, 0.4f) };
        s.Length = length;
        var b = new Builder();
        var sea = look is Look.Sea or Look.Leviathan;
        // The sea's swim with the middle of them at the waterline (a swimmer floats a little under it); the land's lie on the ground.
        var yc = sea ? Fighter.SwimDepth - r * 0.25f : r * 1.05f;
        var (back, belly, fin, spine, eye) = look switch
        {
            Look.Sea => (new Color("#2f8ea6"), new Color("#cfe9e4"), new Color("#d2427e"), new Color("#6a1c48"), new Color("#ffd23a")),
            Look.Leviathan => (new Color("#3a4250"), new Color("#8c8678"), new Color("#7a6c70"), new Color("#ddd4c0"), new Color("#7cf0a0")),
            Look.Flame => (new Color("#ff7a20"), new Color("#ffd070"), new Color("#ffc040"), new Color("#ff5a10"), new Color("#fff0a0")),
            _ => (new Color("#c25420"), new Color("#e8c890"), new Color("#ffb02c"), new Color("#3a2014"), new Color("#ffe040")),
        };
        var glowAll = look == Look.Flame ? 1.1f : 0;
        float Radius(float t) => r * (t < 0.12f ? 0.82f + 0.18f * t / 0.12f : Mathf.Lerp(1, 0.1f, Mathf.Pow((t - 0.12f) / 0.88f, 1.15f)));

        // The body: rings down its length, sea-blue or red-gold above and pale beneath, banded faintly like scales.
        const int rings = 90, sides = 16;
        var path = new List<Vector3>();
        for (var i = 0; i <= rings; i++) path.Add(new Vector3(0, yc, length * i / rings));
        b.Tube(path, i => Radius(i / (float)rings), sides, (i, up) =>
        {
            var c = up < -0.3f ? belly : up < 0.05f ? back.Lerp(belly, 0.35f) : back;
            return (c * (i % 3 == 0 ? 0.88f : 1f)) with { A = 1 };
        }, glowAll);

        // ---- the head ----
        var rh = r * (look == Look.Leviathan ? 1.25f : 1.15f);
        var skull = new Vector3(0, yc + rh * 0.12f, -rh * 0.75f);
        b.Prim(Sphere(), Xf(skull, new Vector3(0.95f, 0.8f, 1.35f) * rh), back, glowAll);
        var snout = skull + new Vector3(0, -rh * 0.08f, -rh * 1.25f);
        b.Prim(Sphere(), Xf(snout, new Vector3(0.62f, 0.5f, 1.15f) * rh), back, glowAll);
        // The lower jaw, open: wider on the leviathan and the wyrm.
        var gape = look switch { Look.Leviathan => 0.42f, Look.Sea => 0.18f, _ => 0.3f };
        var jaw = snout + new Vector3(0, -rh * 0.42f, rh * 0.15f);
        b.Prim(Sphere(), Xf(jaw, new Vector3(0.5f, 0.22f, 1.15f) * rh, new Vector3(-gape, 0, 0)), belly, glowAll);
        b.Prim(Sphere(), Xf(snout + new Vector3(0, -rh * 0.3f, rh * 0.1f), new Vector3(0.46f, 0.12f, 1f) * rh), new Color("#5a1418"), glowAll * 0.5f); // the mouth
        // Teeth, top and bottom, down both sides of the mouth.
        var teeth = look == Look.Leviathan ? 7 : 5;
        foreach (var side in new[] { -1f, 1f })
            for (var k = 0; k < teeth; k++)
            {
                var along = -rh * (0.4f + 1.25f * k / teeth);
                var size = rh * (k == teeth - 1 ? 0.22f : 0.12f) * (look == Look.Leviathan ? 1.4f : 1);
                b.Prim(Cone(), Xf(snout + new Vector3(side * rh * 0.4f, -rh * 0.22f, along + rh * 1.2f), new Vector3(0.35f, 1, 0.35f) * size, new Vector3(Mathf.Pi, 0, 0)), new Color("#f2ead8"));
                b.Prim(Cone(), Xf(jaw + new Vector3(side * rh * 0.36f, rh * 0.15f, along + rh * 1.1f), new Vector3(0.35f, 1, 0.35f) * size * 0.85f), new Color("#f2ead8"));
            }
        // Eyes, burning, under a hard brow.
        foreach (var side in new[] { -1f, 1f })
        {
            var at = skull + new Vector3(side * rh * 0.72f, rh * 0.18f, -rh * 0.55f);
            b.Prim(Sphere(), Xf(at, new Vector3(0.2f, 0.18f, 0.26f) * rh), eye, 1.4f);
            b.Prim(Sphere(), Xf(at + new Vector3(0, rh * 0.14f, -rh * 0.02f), new Vector3(0.28f, 0.1f, 0.4f) * rh), back, glowAll);
        }

        switch (look)
        {
            case Look.Sea:
                SeaSerpent(b, r, rh, yc, length, Radius, skull, snout, fin, spine, belly);
                break;
            case Look.Leviathan:
                Leviathan(b, r, rh, yc, length, Radius, skull, snout, fin, spine);
                break;
            default:
                Wyrm(b, r, rh, yc, length, Radius, skull, snout, jaw, fin, spine, belly, look == Look.Flame);
                break;
        }

        var shader = new ShaderMaterial { Shader = Shader };
        shader.SetShaderParameter("length", length);
        shader.SetShaderParameter("sway", look switch { Look.Sea => r * 0.6f, Look.Leviathan => r * 0.5f, _ => r * 1.7f });
        shader.SetShaderParameter("heave", look switch { Look.Sea => r * 1.7f, Look.Leviathan => r * 1.35f, _ => r * 0.25f });
        shader.SetShaderParameter("waves", look switch { Look.Leviathan => 1.3f, Look.Sea => 1.8f, _ => 1.5f });
        shader.SetShaderParameter("reach", r * 2.2f);
        shader.SetShaderParameter("leg_top", yc);
        s._rate = look is Look.Sea or Look.Leviathan ? 1.1f : 0.6f;
        s._mesh = new MeshInstance3D { Mesh = b.Build(), MaterialOverride = shader, ExtraCullMargin = r * 4, Name = "SerpentBody" };
        s.AddChild(s._mesh);
        return s;
    }

    /// <summary>A rose fin webbed between spines down the back, fans of fin at the neck and behind the head, whiskers, horns and a tail fan.</summary>
    static void SeaSerpent(Builder b, float r, float rh, float yc, float length, Func<float, float> radius, Vector3 skull, Vector3 snout, Color fin, Color spine, Color belly)
    {
        // The dorsal fin: spines leaning back, the web between them, from the neck nearly to the tail.
        var bases = new List<Vector3>();
        var tips = new List<Vector3>();
        for (var t = 0.05f; t <= 0.9f; t += 0.022f)
        {
            var z = t * length;
            var top = new Vector3(0, yc + radius(t) * 0.92f, z);
            var h = r * (1.15f - 0.75f * t) * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(t * 37)));
            bases.Add(top);
            tips.Add(top + new Vector3(0, h, h * 0.55f));
        }
        for (var i = 0; i < bases.Count - 1; i++)
        {
            b.Sheet(bases[i], tips[i], bases[i + 1], fin);
            b.Sheet(tips[i], tips[i + 1], bases[i + 1], fin.Lightened(0.15f));
            b.Spike(bases[i], tips[i] + (tips[i] - bases[i]) * 0.2f, r * 0.04f, spine);
        }
        // Fans: at the neck (pectoral), behind the head (frills), and at the tail.
        foreach (var side in new[] { -1f, 1f })
        {
            b.Fan(new Vector3(side * r * 0.85f, yc - r * 0.2f, length * 0.09f), [new Vector3(side * 1, -0.1f, 0.3f), new Vector3(side * 0.9f, -0.35f, 0.8f), new Vector3(side * 0.6f, -0.5f, 1.1f), new Vector3(side * 0.3f, -0.55f, 1.2f)],
                [r * 2.0f, r * 2.3f, r * 2.1f, r * 1.6f], fin, spine, r * 0.035f);
            b.Fan(skull + new Vector3(side * rh * 0.65f, rh * 0.4f, rh * 0.5f), [new Vector3(side * 0.8f, 0.9f, 0.5f), new Vector3(side * 1f, 0.4f, 0.9f), new Vector3(side * 0.9f, 0f, 1.1f)],
                [rh * 1.8f, rh * 2.1f, rh * 1.7f], fin, spine, rh * 0.035f);
            // Long whiskers from the snout, trailing back and down; swept horns from the brow.
            b.Tube(Curve(snout + new Vector3(side * rh * 0.45f, -rh * 0.05f, -rh * 0.6f), new Vector3(side * 0.6f, 0.05f, 0.5f), new Vector3(side * 0.2f, -0.6f, 1.2f), length * 0.24f, 18),
                i => rh * 0.07f * (1 - i / 20f), 6, (_, _) => fin.Lightened(0.2f));
            b.Tube(Curve(skull + new Vector3(side * rh * 0.35f, rh * 0.6f, -rh * 0.1f), new Vector3(side * 0.25f, 0.8f, 0.6f), new Vector3(0, -0.3f, 1f), rh * 1.6f, 10),
                i => rh * 0.13f * (1 - i / 11f), 8, (_, _) => belly);
        }
        b.Fan(new Vector3(0, yc, length * 0.99f), [new Vector3(0, 1, 0.7f), new Vector3(0, 0.35f, 1), new Vector3(0, -0.35f, 1), new Vector3(0, -1, 0.7f)],
            [r * 1.9f, r * 2.4f, r * 2.4f, r * 1.9f], fin, spine, r * 0.04f);
    }

    /// <summary>A double row of bone spikes, a crown of them, great webbed neck frills, long whiskers.</summary>
    static void Leviathan(Builder b, float r, float rh, float yc, float length, Func<float, float> radius, Vector3 skull, Vector3 snout, Color fin, Color spine)
    {
        for (var t = 0.03f; t <= 0.82f; t += 0.03f)
            foreach (var side in new[] { -0.35f, 0.35f })
            {
                var rr = radius(t);
                var root = new Vector3(side * rr, yc + rr * 0.85f, t * length);
                var tall = r * (1.5f * (1 - t) + 0.35f) * (0.8f + 0.4f * Mathf.Abs(Mathf.Sin(t * 41 + side)));
                b.Spike(root, root + new Vector3(side * 0.6f * tall, tall, tall * 0.55f), rr * 0.16f, spine);
            }
        // The crown: spikes from the back of the skull, fanning back.
        for (var k = 0; k < 6; k++)
        {
            var a = -0.9f + 1.8f * k / 5;
            var root = skull + new Vector3(Mathf.Sin(a) * rh * 0.7f, rh * 0.55f, rh * 0.4f);
            b.Spike(root, root + new Vector3(Mathf.Sin(a) * rh * 1.2f, rh * 1.3f, rh * 1.6f), rh * 0.14f, spine);
        }
        b.Spike(snout + new Vector3(0, rh * 0.35f, -rh * 0.4f), snout + new Vector3(0, rh * 0.9f, -rh * 0.2f), rh * 0.12f, spine); // a horn on the nose
        foreach (var side in new[] { -1f, 1f })
        {
            b.Fan(new Vector3(side * r * 0.9f, yc + r * 0.2f, length * 0.06f), [new Vector3(side * 0.4f, 1, 0.3f), new Vector3(side * 0.9f, 0.8f, 0.6f), new Vector3(side * 1, 0.3f, 0.9f), new Vector3(side * 0.8f, -0.2f, 1)],
                [r * 2.6f, r * 3.0f, r * 2.7f, r * 2.0f], fin, spine, r * 0.06f);
            b.Tube(Curve(snout + new Vector3(side * rh * 0.5f, -rh * 0.1f, -rh * 0.5f), new Vector3(side * 0.7f, -0.2f, 0.4f), new Vector3(side * 0.4f, -0.4f, 1), length * 0.18f, 16),
                i => rh * 0.09f * (1 - i / 18f), 6, (_, _) => fin.Darkened(0.2f));
        }
        // A ridge of low spikes to the tail.
        for (var t = 0.84f; t <= 0.98f; t += 0.025f)
        {
            var root = new Vector3(0, yc + radius(t) * 0.9f, t * length);
            b.Spike(root, root + new Vector3(0, r * 0.4f, r * 0.3f), radius(t) * 0.25f, spine);
        }
    }

    /// <summary>
    /// The ember wyrm: four clawed legs, a mane of flame down its spine and at its elbows, antlers, a dark beard, long
    /// whiskers and a tufted tail. Made of fire (Irohe's), it's all flame.
    /// </summary>
    static void Wyrm(Builder b, float r, float rh, float yc, float length, Func<float, float> radius, Vector3 skull, Vector3 snout, Vector3 jaw, Color flame, Color dark, Color belly, bool fire)
    {
        var glow = fire ? 1.1f : 0;
        var scales = fire ? flame : new Color("#a84418");
        // The mane: flames along the spine, licking up and back, flickering.
        for (var t = 0.0f; t <= 0.97f; t += 0.025f)
        {
            var rr = radius(t);
            var root = new Vector3(0, yc + rr * 0.8f, t * length);
            var tall = r * (0.9f - 0.45f * t) * (0.7f + 0.6f * Mathf.Abs(Mathf.Sin(t * 53)));
            var lean = Mathf.Sin(t * 29) * 0.3f;
            b.Spike(root, root + new Vector3(lean * tall, tall, tall * 0.9f), rr * 0.32f, (t * 40 % 2 < 1 ? flame : new Color("#ff6a18")), 1.6f);
        }
        // A tuft of dark hair and flame at the tail.
        var end = new Vector3(0, yc, length);
        for (var k = 0; k < 7; k++)
        {
            var a = (k - 3) * 0.32f;
            b.Spike(end, end + new Vector3(Mathf.Sin(a) * r * 1.0f, Mathf.Cos(a * 1.3f) * r * 0.8f - r * 0.2f, r * 1.8f), r * 0.12f, k % 2 == 0 ? dark : flame, k % 2 == 0 ? glow : 1.6f);
        }
        // Legs: thigh out and down, shin to the ground, three claws forward, flames at the elbow.
        foreach (var (t, front) in new[] { (0.17f, true), (0.52f, false) })
            foreach (var side in new[] { -1f, 1f })
            {
                var z = t * length;
                var hip = new Vector3(side * radius(t) * 0.85f, yc, z);
                var knee = hip + new Vector3(side * r * 0.75f, -r * 0.35f, front ? -r * 0.25f : r * 0.3f);
                var foot = new Vector3(knee.X + side * r * 0.1f, 0.06f, knee.Z - r * 0.15f);
                var phase = (side > 0) == front ? 1f : 1 + Mathf.Pi; // the opposite corners step together
                b.Tube([hip, hip.Lerp(knee, 0.5f), knee], _ => r * 0.32f, 10, (_, _) => scales, glow, phase);
                b.Tube([knee, knee.Lerp(foot, 0.5f), foot], i => r * (0.24f - i * 0.03f), 10, (_, _) => scales, glow, phase);
                for (var c = -1; c <= 1; c++)
                    b.Spike(foot + new Vector3(c * r * 0.12f, 0, -r * 0.05f), foot + new Vector3(c * r * 0.2f, -0.04f, -r * 0.42f), r * 0.06f, dark, glow, phase);
                for (var k = 0; k < 3; k++) // flames from the elbow, streaming back
                    b.Spike(knee, knee + new Vector3(side * r * (0.2f + 0.15f * k), r * (0.25f + 0.2f * k), r * (0.7f + 0.2f * k)), r * 0.09f, flame, 1.6f, phase);
            }
        foreach (var side in new[] { -1f, 1f })
        {
            // Antlers: a beam swept back, a tine forward off it.
            var root = skull + new Vector3(side * rh * 0.4f, rh * 0.6f, rh * 0.1f);
            var beam = Curve(root, new Vector3(side * 0.35f, 0.9f, 0.6f), new Vector3(side * 0.2f, -0.5f, 0.9f), rh * 1.9f, 10);
            var bone = fire ? flame : new Color("#a8988a");
            b.Tube(beam, i => rh * 0.13f * (1 - i / 11.5f), 8, (_, _) => bone, glow);
            b.Tube(Curve(beam[4], new Vector3(side * 0.2f, 1, -0.2f), new Vector3(0, 0.1f, 0.6f), rh * 0.7f, 6), i => rh * 0.08f * (1 - i / 7f), 6, (_, _) => bone, glow);
            // Long whiskers from the nose, curling.
            b.Tube(Curve(snout + new Vector3(side * rh * 0.4f, rh * 0.1f, -rh * 0.55f), new Vector3(side * 0.9f, 0.3f, -0.1f), new Vector3(side * -0.4f, 0.6f, 2.2f), length * 0.22f, 18),
                i => rh * 0.06f * (1 - i / 20f), 6, (_, _) => fire ? flame : new Color("#8a6a44"), glow);
            // Fringes of flame-coloured hair at the cheeks.
            for (var k = 0; k < 3; k++)
                b.Spike(skull + new Vector3(side * rh * 0.8f, -rh * 0.1f, rh * (0.1f + 0.2f * k)), skull + new Vector3(side * rh * 1.5f, -rh * (0.1f + 0.15f * k), rh * (0.7f + 0.3f * k)), rh * 0.12f, flame, 1.3f);
        }
        // A dark beard under the jaw.
        for (var k = 0; k < 5; k++)
            b.Spike(jaw + new Vector3((k - 2) * rh * 0.12f, -rh * 0.1f, rh * 0.2f), jaw + new Vector3((k - 2) * rh * 0.18f, -rh * 0.9f, rh * 0.7f), rh * 0.08f, fire ? flame : new Color("#5a1810"), glow);
    }

    // ---- shapes ----

    static SphereMesh Sphere() => new() { Radius = 1, Height = 2, RadialSegments = 20, Rings = 10 };
    static CylinderMesh Cone() => new() { TopRadius = 0, BottomRadius = 1, Height = 1, RadialSegments = 8, Rings = 1 };

    static Transform3D Xf(Vector3 at, Vector3 scale, Vector3? turn = null) =>
        new(Basis.FromEuler(turn ?? Vector3.Zero) * Basis.FromScale(scale), at);

    /// <summary>A line of points from a root along a heading that bends by <paramref name="bend"/> over its length.</summary>
    static List<Vector3> Curve(Vector3 root, Vector3 way, Vector3 bend, float length, int n)
    {
        var path = new List<Vector3> { root };
        var d = way.Normalized();
        for (var i = 1; i <= n; i++)
        {
            path.Add(path[^1] + d * length / n);
            d = (d + bend / n).Normalized();
        }
        return path;
    }

    /// <summary>Everything the serpent is made of, in one mesh: positions, normals, colours, and per vertex how much it glows and which leg (if any) it steps with.</summary>
    sealed class Builder
    {
        readonly List<Vector3> _v = [], _n = [];
        readonly List<Color> _c = [];
        readonly List<Vector2> _u = [];

        void Add(Vector3 v, Vector3 n, Color c, float glow, float leg)
        {
            _v.Add(v); _n.Add(n); _c.Add(c.SrgbToLinear()); _u.Add(new Vector2(glow, leg));
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color col, float glow = 0, float leg = 0)
        {
            if ((c - a).Cross(b - a).Dot(na + nb + nc) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
            Add(a, na, col, glow, leg); Add(b, nb, col, glow, leg); Add(c, nc, col, glow, leg);
        }

        /// <summary>A flat web of fin between three points (seen from both sides).</summary>
        public void Sheet(Vector3 a, Vector3 b, Vector3 c, Color col, float glow = 0)
        {
            var n = (b - a).Cross(c - a).Normalized();
            Tri(a, b, c, n, n, n, col, glow);
        }

        /// <summary>One of Godot's shapes, placed, all of one colour.</summary>
        public void Prim(PrimitiveMesh m, Transform3D xf, Color col, float glow = 0, float leg = 0)
        {
            var arrays = m.GetMeshArrays();
            var vs = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var ns = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            int[] ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var normalBasis = xf.Basis.Inverse().Transposed();
            var count = ix.Length > 0 ? ix.Length : vs.Length;
            for (var k = 0; k + 2 < count; k += 3)
            {
                var (i0, i1, i2) = ix.Length > 0 ? (ix[k], ix[k + 1], ix[k + 2]) : (k, k + 1, k + 2);
                Tri(xf * vs[i0], xf * vs[i1], xf * vs[i2], (normalBasis * ns[i0]).Normalized(), (normalBasis * ns[i1]).Normalized(), (normalBasis * ns[i2]).Normalized(), col, glow, leg);
            }
        }

        /// <summary>A pointed spike (a horn, a spine, a claw, a lick of flame) from a root to a tip.</summary>
        public void Spike(Vector3 root, Vector3 tip, float width, Color col, float glow = 0, float leg = 0)
        {
            var along = tip - root;
            var up = along.Normalized();
            var side = up.Cross(Mathf.Abs(up.Y) < 0.95f ? Vector3.Up : Vector3.Right).Normalized();
            var basis = new Basis(side * width, along, side.Cross(up) * width);
            Prim(Cone(), new Transform3D(basis, root + along / 2), col, glow, leg);
        }

        /// <summary>A fan of fin: spines out from one root, the web between each pair, each spine its own length.</summary>
        public void Fan(Vector3 root, Vector3[] ways, float[] lengths, Color web, Color spine, float width)
        {
            var tips = ways.Select((w, i) => root + w.Normalized() * lengths[i]).ToArray();
            for (var i = 0; i < tips.Length - 1; i++)
            {
                // A little curve to the web: a middle point between the two tips, set back toward the root.
                var mid = root.Lerp((tips[i] + tips[i + 1]) / 2, 0.85f);
                Sheet(root, tips[i], mid, web);
                Sheet(root, mid, tips[i + 1], web.Lightened(0.12f));
                Sheet(tips[i], tips[i + 1], mid, web.Lightened(0.06f));
            }
            foreach (var tip in tips) Spike(root, tip + (tip - root) * 0.08f, width, spine);
        }

        /// <summary>A smooth tube along a line of points, its radius and colour at each ring (colour also by how far up round the ring).</summary>
        public void Tube(IReadOnlyList<Vector3> path, Func<int, float> radius, int sides, Func<int, float, Color> colour, float glow = 0, float leg = 0)
        {
            var n = path.Count - 1;
            var rings = new Vector3[n + 1, sides + 1];
            var norms = new Vector3[n + 1, sides + 1];
            var side = Vector3.Zero;
            for (var i = 0; i <= n; i++)
            {
                var t = (path[Math.Min(i + 1, n)] - path[Math.Max(i - 1, 0)]).Normalized();
                side = i == 0 ? t.Cross(Mathf.Abs(t.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized() : (side - t * side.Dot(t)).Normalized();
                var up = side.Cross(t).Normalized();
                if (up.Y < 0) up = -up;
                for (var k = 0; k <= sides; k++)
                {
                    var a = Mathf.Tau * k / sides;
                    norms[i, k] = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    rings[i, k] = path[i] + norms[i, k] * radius(i);
                }
            }
            for (var i = 0; i < n; i++)
                for (var k = 0; k < sides; k++)
                {
                    var c = colour(i, norms[i, k].Y);
                    Tri(rings[i, k], rings[i + 1, k], rings[i, k + 1], norms[i, k], norms[i + 1, k], norms[i, k + 1], c, glow, leg);
                    Tri(rings[i + 1, k], rings[i + 1, k + 1], rings[i, k + 1], norms[i + 1, k], norms[i + 1, k + 1], norms[i, k + 1], c, glow, leg);
                }
            // Closed at both ends.
            foreach (var (i, outward) in new[] { (0, (path[0] - path[Math.Min(1, n)]).Normalized()), (n, (path[n] - path[Math.Max(n - 1, 0)]).Normalized()) })
            {
                var cap = path[i] + outward * radius(i) * 0.6f;
                for (var k = 0; k < sides; k++) Tri(rings[i, k], rings[i, k + 1], cap, norms[i, k], norms[i, k + 1], outward, colour(i, norms[i, k].Y), glow, leg);
            }
        }

        public ArrayMesh Build()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Godot.Mesh.ArrayType.Max);
            arrays[(int)Godot.Mesh.ArrayType.Vertex] = _v.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Normal] = _n.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Color] = _c.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.TexUV2] = _u.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }

    static readonly Shader Shader = new()
    {
        Code = """
            shader_type spatial;
            render_mode cull_disabled;
            uniform float length = 10.0;
            uniform float sway = 0.6;
            uniform float heave = 0.0;
            uniform float waves = 1.5;
            uniform float reach = 0.6;
            uniform float leg_top = 0.4;
            instance uniform float clock = 0.0;
            instance uniform float lunge = 0.0;
            instance uniform float hurt = 0.0;
            instance uniform float calm = 1.0;
            instance uniform vec3 mark = vec3(0.0);
            varying float glow;
            varying float along;

            void vertex() {
                float s = clamp(VERTEX.z / length, 0.0, 1.0);
                // The wave runs down the body as it moves; the head stays steadier than the rest.
                float env = smoothstep(0.0, 0.3, s) * calm;
                float ph = 6.2831 * waves * s - clock;
                VERTEX.x += sway * env * sin(ph);
                VERTEX.y += heave * env * sin(ph + 1.2);
                // Legs step, the opposite corners together: the further down the leg, the more it swings.
                if (UV2.y != 0.0) VERTEX.z += sin(clock * 2.2 + UV2.y) * max(0.0, leg_top - VERTEX.y) * 0.55 * calm;
                // The head strikes forward.
                VERTEX.z -= lunge * reach * (1.0 - smoothstep(0.0, 0.14, s));
                glow = UV2.x;
                along = VERTEX.z;
            }

            void fragment() {
                if (!FRONT_FACING) NORMAL = -NORMAL;
                vec3 c = mix(COLOR.rgb, vec3(0.9, 0.12, 0.08), hurt * 0.6) + mark * 0.25;
                ALBEDO = c;
                ROUGHNESS = 0.42;
                SPECULAR = 0.55;
                RIM = 0.35;
                RIM_TINT = 0.5;
                EMISSION = c * glow * (0.75 + 0.25 * sin(TIME * 7.0 + along * 2.3));
            }
            """,
    };

    // ---- moving ----

    public override void _Process(double delta)
    {
        var d = (float)delta;
        if (!_dead) _clock += d * (_rate + _speed * 0.75f);
        _lunge = Mathf.MoveToward(_lunge, 0, d * 2.5f);
        _hurt = Mathf.MoveToward(_hurt, 0, d * 5);
        _mesh.SetInstanceShaderParameter("clock", _clock);
        _mesh.SetInstanceShaderParameter("lunge", _lunge);
        _mesh.SetInstanceShaderParameter("hurt", _hurt);
        _mesh.SetInstanceShaderParameter("calm", _calm);
    }

    public override void Move(float speed, bool grounded, bool swimming) => _speed = speed;
    public override void Attack(bool heavy = false) => _lunge = 1;
    public override void Cast() => _lunge = 1;
    public override void Hurt() => _hurt = 1;

    public override void Die()
    {
        _dead = true;
        var t = CreateTween();
        t.TweenMethod(Callable.From<float>(v => _calm = v), 1f, 0f, 1.2);
        // The sea's sink; the land's settle where they lie.
        if (_look is Look.Sea or Look.Leviathan) t.Parallel().TweenProperty(this, "position:y", Position.Y - 4, 4);
    }

    public override void Rise()
    {
        _dead = false;
        _calm = 1;
        Position = Position with { Y = 0 };
    }

    public override void Mark(Color colour) => _mesh.SetInstanceShaderParameter("mark", new Vector3(colour.R, colour.G, colour.B));
}
