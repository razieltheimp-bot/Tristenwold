using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Tails, all in one smooth piece: a tube that tapers from the root to the tip and curls up along its length, rather
/// than a stack of cones. A dragon's has a ridge of spines down its back and a spade at the tip; a lizard's a row of
/// low scales; an ape's is long, thin and plain. Built in whatever space the root is given in (a body's, a bone's).
/// </summary>
public static class Tails
{
    public enum Kind { Dragon, Lizard, Ape }

    /// <param name="root">Where it grows from.</param>
    /// <param name="way">Which way it sets off.</param>
    /// <param name="length">How long it is, along its curve.</param>
    /// <param name="thick">Its radius at the root.</param>
    /// <param name="curl">How far it bends up over its length (the sum of what's added to its heading, upward).</param>
    public static Node3D Make(Vector3 root, Vector3 way, float length, float thick, float curl, Material skin, Kind kind, Material? ridge = null)
    {
        var tail = new Node3D { Name = "Tail" };
        const int n = 16, sides = 12;
        var path = new Vector3[n + 1];
        var heading = way.Normalized();
        path[0] = root;
        for (var i = 1; i <= n; i++)
        {
            path[i] = path[i - 1] + heading * length / n;
            heading = (heading + Vector3.Up * curl / n).Normalized();
        }
        var tip = kind == Kind.Ape ? 0.35f : 0.12f;
        float Radius(int i)
        {
            var t = (float)i / n;
            return thick * (tip + (1 - tip) * Mathf.Pow(1 - t, kind == Kind.Ape ? 0.5f : 0.9f));
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rings = new Vector3[n + 1, sides + 1];
        var norms = new Vector3[n + 1, sides + 1];
        var side = Vector3.Zero;
        var ups = new Vector3[n + 1];
        for (var i = 0; i <= n; i++)
        {
            var t = (path[Math.Min(i + 1, n)] - path[Math.Max(i - 1, 0)]).Normalized();
            side = i == 0 ? t.Cross(Mathf.Abs(t.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized() : (side - t * side.Dot(t)).Normalized(); // carried along, so it doesn't twist
            var up = side.Cross(t).Normalized();
            if (up.Y < 0) up = -up;
            ups[i] = up;
            for (var k = 0; k <= sides; k++)
            {
                var a = Mathf.Tau * k / sides;
                norms[i, k] = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                // A little flatter top to bottom, as a real tail is.
                rings[i, k] = path[i] + side * Mathf.Cos(a) * Radius(i) + up * Mathf.Sin(a) * Radius(i) * 0.85f;
            }
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            if ((c - a).Cross(b - a).Dot(na + nb + nc) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
            st.SetNormal(na); st.AddVertex(a);
            st.SetNormal(nb); st.AddVertex(b);
            st.SetNormal(nc); st.AddVertex(c);
        }
        for (var i = 0; i < n; i++)
            for (var k = 0; k < sides; k++)
            {
                Tri(rings[i, k], rings[i + 1, k], rings[i, k + 1], norms[i, k], norms[i + 1, k], norms[i, k + 1]);
                Tri(rings[i + 1, k], rings[i + 1, k + 1], rings[i, k + 1], norms[i + 1, k], norms[i + 1, k + 1], norms[i, k + 1]);
            }
        // Rounded off at the tip.
        var end = (path[n] - path[n - 1]).Normalized();
        var cap = path[n] + end * Radius(n);
        for (var k = 0; k < sides; k++) Tri(rings[n, k], rings[n, k + 1], cap, norms[n, k], norms[n, k + 1], end);

        tail.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = skin, Name = "TailBody" });

        MeshInstance3D Spine(Vector3 at, Vector3 point, float height, float width, Material m) => new()
        {
            Mesh = new CylinderMesh { TopRadius = 0.001f, BottomRadius = width, Height = height, RadialSegments = 6, Rings = 1 },
            Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, point.Normalized())) * Basis.FromScale(new Vector3(1, 1, 0.45f)), at + point.Normalized() * height * 0.4f),
            MaterialOverride = m,
        };
        switch (kind)
        {
            case Kind.Dragon:
            {
                var spines = ridge ?? skin;
                for (var i = 1; i < n - 3; i += 2)
                {
                    var back = (path[i + 1] - path[i]).Normalized();
                    tail.AddChild(Spine(path[i] + ups[i] * Radius(i) * 0.8f, ups[i] - back * 0.6f, Radius(i) * 1.1f, Radius(i) * 0.35f, spines));
                }
                // The spade: a flat, pointed blade of hide at the tip.
                var basis = Basis.LookingAt(-end, ups[n]);
                tail.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 12, Rings = 6 }, MaterialOverride = spines,
                    Transform = new Transform3D(basis * Basis.FromScale(new Vector3(thick * 0.55f, thick * 0.12f, thick * 0.8f)), path[n] + end * thick * 0.45f),
                });
                break;
            }
            case Kind.Lizard:
            {
                var scales = ridge ?? skin;
                for (var i = 1; i < n - 2; i++)
                {
                    var back = (path[i + 1] - path[i]).Normalized();
                    tail.AddChild(Spine(path[i] + ups[i] * Radius(i) * 0.78f, ups[i] - back * 0.9f, Radius(i) * 0.45f, Radius(i) * 0.3f, scales));
                }
                break;
            }
        }
        return tail;
    }
}
