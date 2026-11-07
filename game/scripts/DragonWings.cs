using Godot;

namespace Tristinwold.Game;

/// <summary>
/// A pair of great bat-like wings for a dragon, built in code as a real wing is: an arm of bone from the shoulder through
/// the elbow to the wrist, knuckled at the joints, and a hand beyond it, three long fingers fanning back from the wrist
/// to clawed tips. The skin, in the dragon's colour, darker by the body and along the bones, billows between them, its
/// trailing edge scalloped between the fingertips. On the ground the wing folds as a bat's does: the arm swept back along
/// the body, the hand folded back at the wrist and the fingers closed together, the skin hanging against the flank. In
/// the air they open and beat, slower for bigger dragons, the hand following the arm. They ride on the model's own
/// (small) wing bones, which are shrunk out of sight so only these show.
/// </summary>
public partial class DragonWings : Node3D
{
    readonly Skeleton3D _skeleton;
    sealed record Wing(Node3D Pivot, Node3D Skin, Node3D Hand, Node3D Fan, int Bone, float Side, Basis Close, Basis Gather);
    readonly List<Wing> _wings = [];
    float _phase, _fold = 1, _flying;

    /// <summary>Held open as if flying (for a picture).</summary>
    public bool Spread { get; set; }

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
            AlbedoColor = membrane, Roughness = metallic ? 0.35f : 0.7f, Metallic = metallic ? 0.6f : 0,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = true, Backlight = membrane.Lightened(0.3f) * 0.9f,
            VertexColorUseAsAlbedo = true, RimEnabled = true, Rim = 0.25f, RimTint = 0.5f,
        };
        if (glow is { } g)
        {
            skin.Backlight = g * 0.35f;
            skin.EmissionEnabled = true;
            skin.Emission = g;
            skin.EmissionEnergyMultiplier = 0.08f;
        }
        var bone = new StandardMaterial3D { AlbedoColor = boneColour, Roughness = metallic ? 0.3f : 0.55f, Metallic = metallic ? 0.8f : 0, RimEnabled = true, Rim = 0.2f };
        var claw = new StandardMaterial3D { AlbedoColor = boneColour.Darkened(0.55f), Roughness = 0.3f, MetallicSpecular = 0.7f };
        var built = new DragonWings(skeleton);
        foreach (var (index, side) in bones)
        {
            var pivot = new Node3D { Name = side > 0 ? "WingLeft" : "WingRight" };
            built.AddChild(pivot);
            var hand = new Node3D { Name = "Hand", Position = P(side, halfSpan, 0.62f, 0.26f, 0.02f) };
            pivot.AddChild(hand);
            var fan = new Node3D { Name = "Fan" };
            hand.AddChild(fan);
            var innerSkin = new Node3D { Name = "Skin" };
            pivot.AddChild(innerSkin);
            Build(pivot, innerSkin, fan, side, halfSpan, skin, bone, claw);
            // Folding, the fingers close together across the fan, and the skin between the arm and the body gathers in
            // to the arm, as a bat's does.
            built._wings.Add(new Wing(pivot, innerSkin, hand, fan, index, side,
                Squeeze(new Vector3(0.9f * side, 0, 0.42f), 0.25f), Squeeze(Vector3.Up.Cross(P(side, 1, 0.62f, 0.26f, 0.02f)), 0.3f)));
        }
        model.AddChild(built);
        skeleton.AddChild(new HideBones(bones.Select(b => b.Item1).ToArray()));
        return built;
    }

    static Vector3 P(float side, float l, float x, float y, float z) => new(x * side * l, y * l, z * l);

    /// <summary>A squash to <paramref name="k"/> of the size along one direction, nothing else changed.</summary>
    static Basis Squeeze(Vector3 axis, float k)
    {
        var a = axis.Normalized();
        Vector3 Col(Vector3 e) => e + a * (a.Dot(e) * (k - 1));
        return new Basis(Col(Vector3.Right), Col(Vector3.Up), Col(Vector3.Back));
    }

    static Basis Toward(Basis b, float t) =>
        new(Vector3.Right.Lerp(b.Column0, t), Vector3.Up.Lerp(b.Column1, t), Vector3.Back.Lerp(b.Column2, t));

    /// <summary>One wing, reaching out along +X for the left (side 1) or -X for the right, its front edge forward (+Z): the arm on the pivot, the hand's fingers and skin on the fan (from the wrist).</summary>
    static void Build(Node3D arm, Node3D innerSkin, Node3D fan, float side, float l, Material skin, Material boneMaterial, Material clawMaterial)
    {
        Vector3 Q(float x, float y, float z) => P(side, l, x, y, z);
        var shoulder = Q(0, 0, 0);
        var elbow = Q(0.36f, 0.2f, 0.06f);
        var wrist = Q(0.62f, 0.26f, 0.02f);
        Vector3[] tips = [Q(1.0f, 0.12f, -0.22f), Q(0.9f, 0.0f, -0.55f), Q(0.62f, -0.08f, -0.82f)];
        var root = Q(0.12f, -0.05f, -0.62f); // where the membrane meets the flank
        var thick = l * 0.035f;

        // The skin. Inner: shoulder, elbow, wrist and back to the flank. Outer: from the wrist out between the fingers,
        // scalloped between their tips, and back to the flank.
        var inner = new Membrane(l);
        inner.Panel(wrist, [shoulder, elbow], [1, 1], 0, true);
        inner.Panel(wrist, [root, shoulder], [0, 1], 0.6f, false);
        var innerMesh = inner.Build(skin);
        innerSkin.AddChild(new MeshInstance3D { Mesh = innerMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        var edge = new List<Vector3> { tips[0] - wrist };
        var bony = new List<float> { 1 };
        for (var i = 0; i < tips.Length - 1; i++)
        {
            edge.Add(tips[i].Lerp(tips[i + 1], 0.5f).Lerp(wrist, 0.2f) - wrist);
            bony.Add(0);
            edge.Add(tips[i + 1] - wrist);
            bony.Add(1);
        }
        edge.Add(tips[^1].Lerp(root, 0.5f).Lerp(wrist, 0.16f) - wrist);
        bony.Add(0);
        edge.Add(root - wrist);
        bony.Add(0.5f);
        var outer = new Membrane(l);
        outer.Panel(Vector3.Zero, [.. edge], [.. bony], 1, false);
        fan.AddChild(new MeshInstance3D { Mesh = outer.Build(skin), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

        // The bones: a smooth arm, knuckles at the elbow and wrist, three fingers tapering to claws, and a thumb claw.
        void Bone(Node3D into, Vector3 a, Vector3 b, float ra, float tip) =>
            into.AddChild(Tails.Tube(a, b - a, a.DistanceTo(b), ra, Vector3.Zero, tip, 1, boneMaterial, 6, 10).Mesh);
        void Knuckle(Node3D into, Vector3 at, float r) => into.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 14, Rings = 7 }, MaterialOverride = boneMaterial, Position = at,
        });
        void Claw(Node3D into, Vector3 at, Vector3 way, float length, float r) =>
            into.AddChild(Tails.Tube(at, way, length, r, Vector3.Down * 1.2f, 0.04f, 1, clawMaterial, 6, 8).Mesh);
        Bone(arm, shoulder, elbow, thick * 1.5f, 0.75f);
        Bone(arm, elbow, wrist, thick * 1.15f, 0.85f);
        Knuckle(arm, shoulder, thick * 1.6f);
        Knuckle(arm, elbow, thick * 1.35f);
        Knuckle(fan, Vector3.Zero, thick * 1.2f);
        foreach (var tip in tips)
        {
            Bone(fan, Vector3.Zero, tip - wrist, thick * 0.85f, 0.25f);
            Claw(fan, tip - wrist, tip - wrist, l * 0.05f, thick * 0.25f);
        }
        Claw(fan, Vector3.Zero, Q(0.08f, 0.14f, 0.12f), l * 0.1f, thick * 0.6f); // the thumb claw
    }

    /// <summary>
    /// Skin stretched across a fan of points from a hub: subdivided, billowing a little between the bones, darker by the
    /// body and along the bones, lighter towards the edge, smooth-shaded.
    /// </summary>
    sealed class Membrane(float l)
    {
        readonly SurfaceTool _st = Begin();
        static SurfaceTool Begin() { var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles); return st; }

        /// <param name="hub">Where the fan starts (the wrist).</param>
        /// <param name="rim">The points round its edge, in order.</param>
        /// <param name="bony">For each rim point, how far it's on a bone (1) or out in the skin (0): the skin sags away from the bones.</param>
        /// <param name="hubBone">How bony the hub is.</param>
        /// <param name="darkHub">Darker at the hub (by the body) rather than at the rim.</param>
        public void Panel(Vector3 hub, Vector3[] rim, float[] bony, float hubBone, bool darkHub)
        {
            const int rings = 8, across = 6;
            for (var i = 0; i < rim.Length - 1; i++)
            {
                Vector3 At(int r, int c, out Color colour)
                {
                    var (u, v) = (r / (float)rings, c / (float)across);
                    var edge = rim[i].Lerp(rim[i + 1], v);
                    var p = hub.Lerp(edge, u);
                    var bone = Mathf.Lerp(hubBone, Mathf.Lerp(bony[i], bony[i + 1], v), u);
                    // The skin sags down a little where it's furthest from any bone.
                    p += Vector3.Down * l * 0.08f * u * (1 - bone) * Mathf.Sin(Mathf.Pi * Mathf.Clamp(u * 1.15f, 0, 1));
                    var vein = Mathf.Pow(bone, 3); // a dark line down each bone
                    var shade = (darkHub ? 0.55f + 0.45f * u : 0.65f + 0.4f * u) * (1 - 0.35f * vein) * (0.9f + 0.1f * Mathf.Sin(u * 19 + v * 7)); // darker by the body and along the bones, mottled
                    colour = new Color(shade, shade * 0.97f, shade * 0.95f);
                    return p;
                }
                for (var r = 0; r < rings; r++)
                    for (var c = 0; c < across; c++)
                    {
                        var a = At(r, c, out var ca);
                        var b = At(r + 1, c, out var cb);
                        var d = At(r + 1, c + 1, out var cd);
                        var e = At(r, c + 1, out var ce);
                        Tri(a, b, d, ca, cb, cd);
                        if (r > 0) Tri(a, d, e, ca, cd, ce);
                    }
            }
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            if ((b - a).Cross(c - a).LengthSquared() < 1e-12f) return;
            _st.SetColor(ca); _st.AddVertex(a);
            _st.SetColor(cb); _st.AddVertex(b);
            _st.SetColor(cc); _st.AddVertex(c);
        }

        public ArrayMesh Build(Material skin)
        {
            _st.Index();
            _st.GenerateNormals();
            var mesh = _st.Commit();
            mesh.SurfaceSetMaterial(0, skin);
            return mesh;
        }
    }

    /// <summary>Called each frame by the body: whether it's in the air, and how fast it's going.</summary>
    public void Update(bool flying, float speed, double delta)
    {
        var d = (float)delta;
        _flying = Mathf.MoveToward(_flying, flying || Spread ? 1 : 0, d * 1.5f);
        _fold = 1 - _flying;
        _phase += d * Mathf.Tau * BeatRate * (0.6f + 0.4f * _flying);
        foreach (var w in _wings)
        {
            // Sit on the shoulder (the model's own wing bone), wherever the animation has put it.
            w.Pivot.Position = ToLocal(_skeleton.ToGlobal(_skeleton.GetBoneGlobalPose(w.Bone).Origin));
            var beat = Mathf.Sin(_phase) * 0.75f * _flying; // up and down in flight
            // Folded on the ground: the arm swept back and up along the body, the skin turned to hang against the flank.
            var lift = (-0.1f + beat) * _flying + 0.15f * _fold;
            var sweep = 1.3f * _fold + 0.15f * _flying * Mathf.Cos(_phase);
            var roll = 1.05f * _fold;
            w.Pivot.Basis = new Basis(Vector3.Up, sweep * w.Side) * new Basis(Vector3.Back, lift * w.Side) * new Basis(Vector3.Right, roll);
            // The hand: folded back at the wrist on the ground; in the air it follows the arm a beat behind.
            w.Hand.Basis = new Basis(Vector3.Up, 2.3f * _fold * w.Side) * new Basis(Vector3.Back, -Mathf.Sin(_phase - 0.8f) * 0.35f * _flying * w.Side);
            // The fingers close together as it folds.
            w.Fan.Basis = Toward(w.Close, _fold);
            w.Skin.Basis = Toward(w.Gather, _fold);
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
