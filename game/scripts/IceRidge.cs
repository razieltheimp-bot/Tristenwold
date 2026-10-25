using Godot;

namespace Tristinwold.Game;

/// <summary>
/// The ice that has grown on a frost dragon: a ridge of great crystal spikes down its back, tallest over the
/// shoulders and hips, smaller up the neck and down to the tail tip, with clusters splaying from its flanks and
/// icicles hanging from its jaw. Each crystal is a faceted, pointed shard of pale translucent ice, deeper blue
/// at the root. They ride on the spine bones, so they move with the dragon, and they are sized to the dragon,
/// so a small frost dragon has small spikes and an ancient one has spikes taller than a man.
/// </summary>
public static class IceRidge
{
    // Along the spine from the head to the tail tip: the bone, how far along it, and how long the spike is
    // (in the model's own units; the dragon stands about 0.73 tall in them).
    static readonly (string Bone, float At, float Length)[] Ridge =
    [
        ("neck3", 0.35f, 0.07f), ("neck3", 0.8f, 0.085f),
        ("neck2", 0.15f, 0.095f), ("neck2", 0.5f, 0.11f), ("neck2", 0.85f, 0.125f),
        ("neck1", 0.35f, 0.14f), ("neck1", 0.8f, 0.16f),
        ("back1", 0.15f, 0.22f), ("back1", 0.45f, 0.28f), ("back1", 0.75f, 0.31f),
        ("back2", 0.05f, 0.31f), ("back2", 0.38f, 0.27f), ("back2", 0.7f, 0.22f),
        ("tail1", 0.0f, 0.17f), ("tail1", 0.5f, 0.14f),
        ("tail2", 0.0f, 0.115f), ("tail2", 0.5f, 0.095f),
        ("tail3", 0.0f, 0.075f), ("tail3", 0.4f, 0.06f), ("tail3", 0.8f, 0.045f),
    ];

    // Clusters on the flanks, both sides, leaning outwards.
    static readonly (string Bone, float At, float Length)[] Flanks =
    [
        ("back1", 0.3f, 0.12f), ("back1", 0.8f, 0.14f), ("back2", 0.3f, 0.12f), ("tail1", 0.2f, 0.08f),
    ];

    public static void Grow(Node3D model)
    {
        if (model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is not { } skeleton) return;
        var hide = Hide(model, skeleton);
        if (hide.Length == 0) return;
        var rng = new RandomNumberGenerator();
        var ice = Material();

        var tail = skeleton.FindBone("tail1") is var t1 and >= 0 && skeleton.FindBone("neck2") is var n2 and >= 0
            ? (skeleton.GetBoneGlobalRest(t1).Origin - skeleton.GetBoneGlobalRest(n2).Origin) with { Y = 0 }
            : Vector3.Back;
        tail = tail.Normalized();

        var built = new Dictionary<int, SurfaceTool>();
        SurfaceTool On(int bone)
        {
            if (built.TryGetValue(bone, out var st)) return st;
            st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            return built[bone] = st;
        }

        // A spot on the hide above a bone, and which way is out of the back there.
        (Vector3 Root, Vector3 Out, Vector3 Along)? Spot(string name, float at, float side)
        {
            var bone = skeleton.FindBone(name);
            if (bone < 0) return null;
            var rest = skeleton.GetBoneGlobalRest(bone);
            var end = End(skeleton, name);
            var along = (end - rest.Origin).Normalized();
            var from = rest.Origin.Lerp(end, at);
            var up = Vector3.Up + tail * 0.5f;
            var dorsal = (up - along * up.Dot(along)).Normalized();
            var right = along.Cross(dorsal).Normalized();
            var dir = side == 0 ? dorsal : (dorsal * 0.75f + right * side).Normalized();
            return (Surface(hide, from, dir), dir, along);
        }

        foreach (var (name, at, length) in Ridge)
            if (Spot(name, at, 0) is var (root, dir, _))
            {
                var st = On(skeleton.FindBone(name));
                // The great spike leaning back, and smaller shards splaying from its foot.
                Shard(st, root, (dir + tail * 0.35f).Normalized(), length, rng);
                for (var i = 0; i < 2; i++)
                {
                    var splay = (dir + tail * (rng.RandfRange(-0.2f, 0.6f)) + dir.Cross(tail).Normalized() * (i == 0 ? 0.7f : -0.7f) * rng.RandfRange(0.6f, 1.1f)).Normalized();
                    Shard(st, root + tail * rng.RandfRange(-0.02f, 0.02f), splay, length * rng.RandfRange(0.35f, 0.55f), rng);
                }
            }
        foreach (var (name, at, length) in Flanks)
            foreach (var side in new[] { 1f, -1f })
                if (Spot(name, at, side) is var (root, dir, _))
                {
                    var st = On(skeleton.FindBone(name));
                    Shard(st, root, (dir + tail * 0.3f).Normalized(), length, rng);
                    Shard(st, root + tail * 0.03f, (dir + tail * 0.6f + Vector3.Up * 0.3f).Normalized(), length * 0.6f, rng);
                }

        // Icicles hanging from the jaw.
        if (skeleton.FindBone("jaw_lower") is var jaw and >= 0)
        {
            var rest = skeleton.GetBoneGlobalRest(jaw);
            var end = End(skeleton, "jaw_lower");
            var st = On(jaw);
            foreach (var (at, length) in new[] { (0.2f, 0.05f), (0.45f, 0.065f), (0.7f, 0.045f) })
            {
                var from = rest.Origin.Lerp(end, at);
                Shard(st, Surface(hide, from, Vector3.Down, 0.05f), (Vector3.Down + tail * 0.15f).Normalized(), length, rng, 0.11f);
            }
        }

        foreach (var (bone, st) in built)
        {
            var mesh = st.Commit();
            mesh.SurfaceSetMaterial(0, ice);
            var attach = new BoneAttachment3D { BoneName = skeleton.GetBoneName(bone), Name = $"Ice_{skeleton.GetBoneName(bone)}" };
            // The shards were placed in the skeleton's space at rest; hang them off the bone from there.
            attach.AddChild(new MeshInstance3D { Mesh = mesh, Transform = skeleton.GetBoneGlobalRest(bone).AffineInverse(), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
            skeleton.AddChild(attach);
        }
    }

    static readonly Dictionary<string, string> Next = new()
    {
        ["neck1"] = "neck2", ["neck2"] = "neck3", ["neck3"] = "jaw_upper",
        ["back1"] = "back2", ["back2"] = "tail1", ["tail1"] = "tail2", ["tail2"] = "tail3",
    };

    /// <summary>Where a bone ends: where the next one down the spine starts, or a short way along it for the last.</summary>
    static Vector3 End(Skeleton3D skeleton, string name)
    {
        var rest = skeleton.GetBoneGlobalRest(skeleton.FindBone(name));
        if (Next.TryGetValue(name, out var next) && skeleton.FindBone(next) is var b and >= 0) return skeleton.GetBoneGlobalRest(b).Origin;
        return rest.Origin + rest.Basis.Y.Normalized() * 0.1f;
    }

    /// <summary>Pale, glassy, see-through ice that glints at its edges.</summary>
    static StandardMaterial3D Material() => new()
    {
        VertexColorUseAsAlbedo = true, AlbedoColor = new Color(0.84f, 0.94f, 1f, 0.88f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
        Roughness = 0.06f, MetallicSpecular = 1f,
        RimEnabled = true, Rim = 0.7f, RimTint = 0.3f,
        EmissionEnabled = true, Emission = new Color("#3f92d8"), EmissionEnergyMultiplier = 0.45f,
    };

    /// <summary>
    /// One crystal: a five- or six-sided shaft from <paramref name="root"/> along <paramref name="axis"/>, narrowing a
    /// little, then a long point. Its foot is sunk into the hide so it grows out of it. Flat facets, so it glints.
    /// </summary>
    static void Shard(SurfaceTool st, Vector3 root, Vector3 axis, float length, RandomNumberGenerator rng, float thickness = 0.16f)
    {
        length *= rng.RandfRange(0.85f, 1.15f);
        var sides = rng.Randf() < 0.5f ? 5 : 6;
        var radius = length * thickness * rng.RandfRange(0.85f, 1.15f);
        var u = axis.Cross(Mathf.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
        var v = axis.Cross(u).Normalized();
        var twist = rng.Randf() * Mathf.Tau;
        var foot = root - axis * length * 0.18f;
        var shoulder = root + axis * length * rng.RandfRange(0.45f, 0.62f);
        var tip = root + axis * length + (u * rng.RandfRange(-1, 1) + v * rng.RandfRange(-1, 1)) * radius * 0.25f;
        Vector3 Ring(Vector3 centre, float r, int i)
        {
            var a = twist + i * Mathf.Tau / sides;
            return centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
        }
        Color deep = new(0.3f, 0.58f, 0.9f), mid = new(0.6f, 0.83f, 1f), bright = new(0.93f, 0.98f, 1f);
        void Tri(Vector3 a, Color ca, Vector3 b, Color cb, Vector3 c, Color cc)
        {
            var n = (b - a).Cross(c - a).Normalized();
            if ((a - root).Dot(n) < 0) n = -n; // outwards
            foreach (var (p, col) in new[] { (a, ca), (b, cb), (c, cc) })
            {
                st.SetColor(col);
                st.SetNormal(n);
                st.AddVertex(p);
            }
        }
        for (var i = 0; i < sides; i++)
        {
            var (f0, f1) = (Ring(foot, radius, i), Ring(foot, radius, i + 1));
            var (s0, s1) = (Ring(shoulder, radius * 0.82f, i), Ring(shoulder, radius * 0.82f, i + 1));
            Tri(f0, deep, s0, mid, s1, mid);
            Tri(f0, deep, s1, mid, f1, deep);
            Tri(s0, mid, tip, bright, s1, mid);
        }
    }

    /// <summary>The dragon's hide at rest (its mesh's points, in the skeleton's space), to find where the back is.</summary>
    static Vector3[] Hide(Node3D model, Skeleton3D skeleton)
    {
        var toSkeleton = Space(skeleton, model).AffineInverse();
        var points = new List<Vector3>();
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (m.Mesh is not { } mesh || m.Name.ToString().StartsWith("Ice_")) continue;
            var xf = toSkeleton * Space(m, model);
            for (var s = 0; s < mesh.GetSurfaceCount(); s++)
                if (mesh is ArrayMesh am && am.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array() is { } verts)
                    points.AddRange(verts.Select(p => xf * p));
        }
        return [.. points];
    }

    static Transform3D Space(Node3D node, Node3D model)
    {
        var t = Transform3D.Identity;
        for (Node? n = node; n is not null && n != model; n = n.GetParent())
            if (n is Node3D n3) t = n3.Transform * t;
        return t;
    }

    /// <summary>Where a line out from inside the body (<paramref name="from"/>, along <paramref name="dir"/>) leaves the hide.</summary>
    static Vector3 Surface(Vector3[] hide, Vector3 from, Vector3 dir, float reach = 0.3f)
    {
        var best = 0f;
        foreach (var p in hide)
        {
            var d = p - from;
            var along = d.Dot(dir);
            if (along <= 0 || along > reach) continue;
            if ((d - dir * along).Length() < 0.035f && along > best) best = along;
        }
        return from + dir * best;
    }
}
