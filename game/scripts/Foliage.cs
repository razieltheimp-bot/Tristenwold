using Godot;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>
/// Trees built in code from photographs, the way games draw forests: a bark-textured trunk and
/// limbs, and foliage made of flat cards cut from Poly Haven's fir and leaf scans. Poly Haven's
/// own tree models run to millions of triangles; these are a few hundred to a thousand each, so
/// a phone can draw a forest. The cards' normals point out from the crown's middle, so a tree is
/// lit as one soft mass rather than as a pile of flat cards.
/// </summary>
public static class Foliage
{
    const string Root = "res://assets/polyhaven/foliage/";

    public enum Kind { Fir, Autumn, Green, Bare }

    public const int Variants = 3;

    /// <summary>How the wind shader measures these trees' height, so the tops sway about half a metre.</summary>
    public const float HeightScale = 0.13f;

    public static bool Available => ResourceLoader.Exists(Root + "fir_branch.png");

    static readonly Dictionary<(Kind, int, bool), Mesh> Cache = [];

    public static Mesh Tree(Kind kind, int variant, bool fancy)
    {
        if (Cache.TryGetValue((kind, variant, fancy), out var mesh)) return mesh;
        var rng = new Rng(0x7EE5 + (ulong)kind * 977 + (ulong)variant * 131);
        var b = new Builder();
        switch (kind)
        {
            case Kind.Fir: Fir(b, rng); break;
            case Kind.Bare: Broadleaf(b, rng, leaves: false); break;
            default: Broadleaf(b, rng, leaves: true); break;
        }
        var cards = kind switch { Kind.Fir => "fir_branch.png", Kind.Autumn => "leaves_autumn.png", _ => "leaves_green.png" };
        return Cache[(kind, variant, fancy)] = b.Finish(Bark(fancy), kind == Kind.Bare ? null : Cards(cards, fancy));
    }

    // ---- materials -------------------------------------------------------------------------------

    static readonly Dictionary<(string, bool), Material> Materials = [];

    static Material Bark(bool fancy)
    {
        if (Materials.TryGetValue(("bark", fancy), out var m)) return m;
        var diff = GD.Load<Texture2D>(Root + "bark_diff.jpg");
        var normal = GD.Load<Texture2D>(Root + "bark_nor.jpg");
        m = fancy
            ? Graphics.Swaying(1, diff, HeightScale, normal: normal)
            : new StandardMaterial3D { AlbedoTexture = diff, NormalEnabled = true, NormalTexture = normal, VertexColorUseAsAlbedo = true, Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        return Materials[("bark", fancy)] = m;
    }

    static Material Cards(string file, bool fancy)
    {
        if (Materials.TryGetValue((file, fancy), out var m)) return m;
        var tex = GD.Load<Texture2D>(Root + file);
        m = fancy
            ? Graphics.Swaying(1, tex, HeightScale, cutout: true)
            : new StandardMaterial3D
            {
                AlbedoTexture = tex, VertexColorUseAsAlbedo = true, Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.45f,
            };
        return Materials[(file, fancy)] = m;
    }

    // ---- shapes ----------------------------------------------------------------------------------

    /// <summary>A fir: a tapering trunk with whorls of drooping branches, longest at the bottom, and a spire on top.</summary>
    static void Fir(Builder b, Rng rng)
    {
        var height = 13 + (float)rng.NextDouble() * 7;
        b.Tube(new Vector3(0, -0.4f, 0), new Vector3(0, height, 0), 0.022f * height, 0.04f, 7);
        var whorls = 17 + rng.Next(5);
        for (var i = 0; i < whorls; i++)
        {
            var f = 0.14f + 0.82f * i / whorls + (float)(rng.NextDouble() - 0.5) * 0.02f;
            var length = 0.7f + MathF.Pow(1 - f, 1.1f) * height * 0.3f;
            var count = 5 + rng.Next(3);
            var phase = (float)rng.NextDouble() * Mathf.Tau;
            for (var j = 0; j < count; j++)
            {
                var a = phase + j * Mathf.Tau / count + (float)(rng.NextDouble() - 0.5) * 0.5f;
                var droop = 0.18f + 0.35f * (1 - f) + (float)rng.NextDouble() * 0.1f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var basePoint = new Vector3(0, f * height, 0) + dir * 0.1f;
                var tip = basePoint + dir * length * Mathf.Cos(droop) + Vector3.Down * length * Mathf.Sin(droop);
                b.Branch(basePoint, tip, length * 0.5f, dir);
            }
        }
        // The leader at the top: two crossed upright cards.
        var top = new Vector3(0, height + 0.4f, 0);
        for (var k = 0; k < 2; k++)
        {
            var side = k == 0 ? Vector3.Right : Vector3.Back;
            b.Upright(new Vector3(0, height - 1.8f, 0), top, side, 0.7f);
        }
    }

    /// <summary>A broad-leaved tree: a trunk forking into limbs, with a round crown of leaf clusters (or bare, for dead trees).</summary>
    static void Broadleaf(Builder b, Rng rng, bool leaves)
    {
        var height = leaves ? 10 + (float)rng.NextDouble() * 5 : 8 + (float)rng.NextDouble() * 4;
        var fork = height * (0.38f + (float)rng.NextDouble() * 0.1f);
        b.Tube(new Vector3(0, -0.4f, 0), new Vector3(0, fork, 0), 0.03f * height, 0.022f * height, 8);
        var crown = new Vector3(0, height * 0.66f, 0);
        var limbs = 5 + rng.Next(3);
        var phase = (float)rng.NextDouble() * Mathf.Tau;
        for (var i = 0; i < limbs; i++)
        {
            var a = phase + i * Mathf.Tau / limbs + (float)(rng.NextDouble() - 0.5) * 0.6f;
            var start = new Vector3(0, fork - (float)rng.NextDouble() * 1.2f, 0);
            var dir = new Vector3(Mathf.Cos(a) * 0.75f, 1, Mathf.Sin(a) * 0.75f).Normalized();
            var end = start + dir * height * (0.32f + (float)rng.NextDouble() * 0.12f);
            b.Tube(start, end, 0.016f * height, 0.006f * height, 6);
            // Two smaller branches off each limb.
            for (var k = 0; k < 2; k++)
            {
                var from = start.Lerp(end, 0.45f + k * 0.3f);
                var twist = a + (k == 0 ? 0.9f : -0.9f);
                var sub = new Vector3(Mathf.Cos(twist), 0.8f, Mathf.Sin(twist)).Normalized();
                b.Tube(from, from + sub * height * 0.18f, 0.007f * height, 0.003f * height, 5);
            }
        }
        if (!leaves) return;
        var radii = new Vector3(height * 0.3f, height * 0.25f, height * 0.3f);
        for (var i = 0; i < 95; i++)
        {
            var u = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 1.6f - 0.6f, (float)rng.NextDouble() * 2 - 1);
            if (u.LengthSquared() < 0.01f) continue;
            u = u.Normalized();
            var at = crown + u * radii * (0.5f + 0.5f * MathF.Sqrt((float)rng.NextDouble()));
            var size = 2.2f + (float)rng.NextDouble() * 1.1f;
            b.Card(at, size, u, crown, rng);
        }
    }

    /// <summary>Gathers a tree's bark and foliage into one mesh with two surfaces.</summary>
    sealed class Builder
    {
        readonly SurfaceTool _bark = Start();
        readonly SurfaceTool _cards = Start();
        bool _hasCards;

        static SurfaceTool Start()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            return st;
        }

        static void Vertex(SurfaceTool st, Vector3 p, Vector3 n, Vector2 uv, float shade)
        {
            st.SetColor(new Color(shade, shade, shade));
            st.SetNormal(n);
            st.SetUV(uv);
            st.AddVertex(p);
        }

        /// <summary>A tapered tube of bark from a to b. Bark is tiled every 1.2 m.</summary>
        public void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides)
        {
            var axis = (b - a).Normalized();
            var side = Mathf.Abs(axis.Y) > 0.9f ? Vector3.Right : Vector3.Up.Cross(axis).Normalized();
            var other = axis.Cross(side).Normalized();
            var length = a.DistanceTo(b);
            for (var i = 0; i < sides; i++)
            {
                var (a0, a1) = (i * Mathf.Tau / sides, (i + 1) * Mathf.Tau / sides);
                var (d0, d1) = (side * Mathf.Cos(a0) + other * Mathf.Sin(a0), side * Mathf.Cos(a1) + other * Mathf.Sin(a1));
                var (u0, u1) = (i * Mathf.Tau * ra / sides / 1.2f, (i + 1) * Mathf.Tau * ra / sides / 1.2f);
                var v = length / 1.2f;
                Vertex(_bark, a + d0 * ra, d0, new Vector2(u0, 0), 0.8f);
                Vertex(_bark, b + d0 * rb, d0, new Vector2(u0, v), 1);
                Vertex(_bark, b + d1 * rb, d1, new Vector2(u1, v), 1);
                Vertex(_bark, a + d0 * ra, d0, new Vector2(u0, 0), 0.8f);
                Vertex(_bark, b + d1 * rb, d1, new Vector2(u1, v), 1);
                Vertex(_bark, a + d1 * ra, d1, new Vector2(u1, 0), 0.8f);
            }
        }

        /// <summary>
        /// A fir branch: the card folded along its stem into a shallow V, so it still shows from
        /// the side. The card's left edge meets the trunk; its stem runs along the middle.
        /// </summary>
        public void Branch(Vector3 from, Vector3 to, float width, Vector3 outward)
        {
            _hasCards = true;
            var along = (to - from).Normalized();
            var side = along.Cross(Vector3.Up).Normalized();
            var (n0, n1) = ((outward * 0.5f + Vector3.Up * 0.85f).Normalized(), (outward + Vector3.Up * 0.45f).Normalized());
            foreach (var sign in new[] { -1f, 1f })
            {
                var wing = (side * sign * 0.94f + Vector3.Up * 0.34f) * width / 2;
                var v = sign < 0 ? 0f : 1f;
                Vertex(_cards, from, n0, new Vector2(0, 0.5f), 0.45f);
                Vertex(_cards, to, n1, new Vector2(1, 0.5f), 1);
                Vertex(_cards, to + wing, n1, new Vector2(1, v), 1);
                Vertex(_cards, from, n0, new Vector2(0, 0.5f), 0.45f);
                Vertex(_cards, to + wing, n1, new Vector2(1, v), 1);
                Vertex(_cards, from + wing, n0, new Vector2(0, v), 0.5f);
            }
        }

        /// <summary>A branch card standing upright, from bottom to top (the tree's leader).</summary>
        public void Upright(Vector3 bottom, Vector3 top, Vector3 side, float halfWidth)
        {
            _hasCards = true;
            var s = side * halfWidth;
            var n = Vector3.Up;
            Vertex(_cards, bottom - s, n, new Vector2(0, 0), 0.7f);
            Vertex(_cards, top - s, n, new Vector2(1, 0), 1);
            Vertex(_cards, top + s, n, new Vector2(1, 1), 1);
            Vertex(_cards, bottom - s, n, new Vector2(0, 0), 0.7f);
            Vertex(_cards, top + s, n, new Vector2(1, 1), 1);
            Vertex(_cards, bottom + s, n, new Vector2(0, 1), 0.7f);
        }

        /// <summary>A cluster of leaves on a card at a random angle, lit as part of the crown around it.</summary>
        public void Card(Vector3 at, float size, Vector3 outward, Vector3 crown, Rng rng)
        {
            _hasCards = true;
            var t1 = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
            if (t1.LengthSquared() < 0.001f) t1 = Vector3.Right;
            t1 = t1.Normalized();
            var t2 = outward.Cross(t1);
            if (t2.LengthSquared() < 0.001f) t2 = Vector3.Up.Cross(t1);
            t2 = t2.Normalized();
            t1 = t2.Cross(outward).Normalized(); // the card faces mostly outward
            var (h1, h2) = (t1 * size / 2, t2 * size / 2);
            var shade = 0.55f + 0.45f * Mathf.Clamp(outward.Y * 0.5f + 0.5f, 0, 1);
            Vector3[] corners = [at - h1 - h2, at + h1 - h2, at + h1 + h2, at - h1 + h2];
            Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
                Vertex(_cards, corners[i], (corners[i] - crown).Normalized(), uvs[i], shade);
        }

        public Mesh Finish(Material bark, Material? cards)
        {
            var mesh = new ArrayMesh();
            _bark.GenerateTangents();
            _bark.Commit(mesh);
            mesh.SurfaceSetMaterial(0, bark);
            if (_hasCards && cards is not null)
            {
                _cards.Commit(mesh);
                mesh.SurfaceSetMaterial(1, cards);
            }
            return mesh;
        }
    }
}
