using Godot;

namespace Tristinwold.Game;

/// <summary>
/// A pair of great bat-like wings for a dragon, built in code: an arm of bone from the shoulder to the wrist,
/// three long fingers fanning back from it, and a membrane in the dragon's colour stretched between them with a
/// scalloped trailing edge. On the ground they fold back along the body; in the air they beat, slower for bigger
/// dragons. They ride on the model's own (small) wing bones, which are shrunk out of sight so only these show.
/// </summary>
public partial class DragonWings : Node3D
{
    readonly Skeleton3D _skeleton;
    readonly List<(Node3D Pivot, int Bone, float Side)> _wings = [];
    float _phase, _fold = 1, _flying;

    /// <summary>How fast the wings beat (beats a second); the body sets it from its size.</summary>
    public float BeatRate { get; set; } = 1.3f;

    DragonWings(Skeleton3D skeleton) => _skeleton = skeleton;

    /// <param name="model">The dragon model the wings attach to (its skeleton has wing_lower.L/R bones).</param>
    /// <param name="halfSpan">How far each wing reaches, in the model's own units.</param>
    /// <param name="glow">A light shining through the membrane (the Rift's in a corrupted dragon's wings).</param>
    public static DragonWings? Grow(Node3D model, float halfSpan, Color membrane, Color boneColour, bool metallic, Color? glow = null)
    {
        if (model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is not { } skeleton) return null;
        var bones = new[] { ("wing_lower.L", 1f), ("wing_lower.R", -1f) }.Select(b => (skeleton.FindBone(b.Item1), b.Item2)).ToArray();
        if (bones.Any(b => b.Item1 < 0)) return null;

        var skin = new StandardMaterial3D
        {
            AlbedoColor = membrane, Roughness = metallic ? 0.35f : 0.75f, Metallic = metallic ? 0.7f : 0,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = true, Backlight = membrane.Lightened(0.3f) * 0.9f,
            VertexColorUseAsAlbedo = true,
        };
        if (glow is { } g)
        {
            skin.Backlight = g * 0.35f;
            skin.EmissionEnabled = true;
            skin.Emission = g;
            skin.EmissionEnergyMultiplier = 0.08f;
        }
        var bone = new StandardMaterial3D { AlbedoColor = boneColour, Roughness = metallic ? 0.3f : 0.6f, Metallic = metallic ? 0.8f : 0 };
        var built = new DragonWings(skeleton);
        foreach (var (index, side) in bones)
        {
            var pivot = new Node3D { Name = side > 0 ? "WingLeft" : "WingRight" };
            pivot.AddChild(new MeshInstance3D { Mesh = Wing(side, halfSpan, skin, bone), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
            built.AddChild(pivot);
            built._wings.Add((pivot, index, side));
        }
        model.AddChild(built);
        skeleton.AddChild(new HideBones(bones.Select(b => b.Item1).ToArray()));
        return built;
    }

    /// <summary>One wing, reaching out along +X for the left (side 1) or -X for the right, its front edge forward (+Z).</summary>
    static ArrayMesh Wing(float side, float l, Material skin, Material boneMaterial)
    {
        Vector3 P(float x, float y, float z) => new(x * side * l, y * l, z * l);
        var shoulder = P(0, 0, 0);
        var elbow = P(0.36f, 0.2f, 0.06f);
        var wrist = P(0.62f, 0.26f, 0.02f);
        Vector3[] tips = [P(1.0f, 0.12f, -0.22f), P(0.9f, 0.0f, -0.55f), P(0.62f, -0.08f, -0.82f)];
        var root = P(0.12f, -0.05f, -0.62f); // where the membrane meets the flank

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Tri(Vector3 a, Vector3 b, Vector3 c, float shadeA = 1, float shadeB = 1, float shadeC = 1)
        {
            var n = (b - a).Cross(c - a).Normalized();
            foreach (var (p, k) in new[] { (a, shadeA), (b, shadeB), (c, shadeC) })
            {
                st.SetColor(new Color(k, k, k));
                st.SetNormal(n);
                st.AddVertex(p);
            }
        }
        // Membrane: darker by the body, lighter towards the edges; the trailing edge dips between the fingers.
        Tri(shoulder, elbow, wrist, 0.55f, 0.75f, 0.9f);
        Tri(shoulder, wrist, root, 0.55f, 0.9f, 0.6f);
        var edge = new List<Vector3> { tips[0] };
        for (var i = 0; i < tips.Length - 1; i++)
        {
            var mid = tips[i].Lerp(tips[i + 1], 0.5f);
            edge.Add(mid.Lerp(wrist, 0.18f));
            edge.Add(tips[i + 1]);
        }
        var lastMid = tips[^1].Lerp(root, 0.5f).Lerp(wrist, 0.15f);
        edge.Add(lastMid);
        edge.Add(root);
        for (var i = 0; i < edge.Count - 1; i++) Tri(wrist, edge[i], edge[i + 1], 1.1f, i % 2 == 0 ? 0.8f : 0.95f, i % 2 == 0 ? 0.95f : 0.8f);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, skin);

        // Bones: the arm and the three fingers, tapering, with a claw at the wrist.
        var bones = new SurfaceTool();
        bones.Begin(Mesh.PrimitiveType.Triangles);
        void Spar(Vector3 a, Vector3 b, float ra, float rb)
        {
            var axis = (b - a).Normalized();
            var u = axis.Cross(Vector3.Up).Normalized();
            if (u.LengthSquared() < 0.01f) u = Vector3.Right;
            var v = axis.Cross(u).Normalized();
            for (var i = 0; i < 5; i++)
            {
                var (a0, a1) = (i * Mathf.Tau / 5, (i + 1) * Mathf.Tau / 5);
                var (d0, d1) = (u * Mathf.Cos(a0) + v * Mathf.Sin(a0), u * Mathf.Cos(a1) + v * Mathf.Sin(a1));
                foreach (var (p, n) in new[] { (a + d0 * ra, d0), (b + d0 * rb, d0), (b + d1 * rb, d1), (a + d0 * ra, d0), (b + d1 * rb, d1), (a + d1 * ra, d1) })
                {
                    bones.SetNormal(n);
                    bones.AddVertex(p);
                }
            }
        }
        var thick = l * 0.035f;
        Spar(shoulder, elbow, thick * 1.4f, thick * 1.1f);
        Spar(elbow, wrist, thick * 1.1f, thick);
        foreach (var tip in tips) Spar(wrist, tip, thick * 0.8f, thick * 0.2f);
        Spar(wrist, wrist + P(0.05f, 0.14f, 0.1f), thick * 0.7f, 0.001f); // the thumb claw
        bones.Commit(mesh);
        mesh.SurfaceSetMaterial(1, boneMaterial);
        return mesh;
    }

    /// <summary>Called each frame by the body: whether it's in the air, and how fast it's going.</summary>
    public void Update(bool flying, float speed, double delta)
    {
        var d = (float)delta;
        _flying = Mathf.MoveToward(_flying, flying ? 1 : 0, d * 1.5f);
        _fold = 1 - _flying;
        _phase += d * Mathf.Tau * BeatRate * (0.6f + 0.4f * _flying);
        foreach (var (pivot, bone, side) in _wings)
        {
            // Sit on the shoulder (the model's own wing bone), wherever the animation has put it.
            pivot.Position = ToLocal(_skeleton.ToGlobal(_skeleton.GetBoneGlobalPose(bone).Origin));
            var beat = Mathf.Sin(_phase) * 0.75f * _flying;               // up and down in flight
            // Folded on the ground: swept back along the body, the membrane turned to hang flat against the flank.
            var lift = (-0.1f + beat) * _flying + 0.05f * _fold;
            var sweep = 1.55f * _fold + 0.15f * _flying * Mathf.Cos(_phase);
            var roll = 0.5f * _fold;
            pivot.Basis = new Basis(Vector3.Up, -sweep * side) * new Basis(Vector3.Back, lift * side) * new Basis(Vector3.Right, roll);
            pivot.Scale = new Vector3(1 - 0.62f * _fold, 1 - 0.5f * _fold, 1 - 0.6f * _fold);
        }
    }

    /// <summary>Shrinks the model's own small wings away (after its animations run), so only the great wings show.</summary>
    sealed partial class HideBones(int[] bones) : SkeletonModifier3D
    {
        public override void _ProcessModificationWithDelta(double delta)
        {
            if (GetSkeleton() is not { } s) return;
            foreach (var b in bones) s.SetBonePoseScale(b, Vector3.One * 0.01f);
        }
    }
}
