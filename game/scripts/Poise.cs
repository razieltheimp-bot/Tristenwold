using Godot;

namespace Tristinwold.Game;

/// <summary>
/// What makes a creature stand in the world rather than on a flat stage: its feet find the ground under each of
/// them (a horse on a hillside stands with its uphill legs bent and its downhill legs straight; a wolf on a rock has
/// a paw on the rock), its head turns to look at whoever comes near, and its tail swings with its own weight. All
/// of it is Godot's skeleton modifiers, told which bones are which for each model: the rigs come from different
/// packs and name nothing alike.
/// </summary>
public static class Poise
{
    /// <summary>Off draws the creatures as their clips alone move them (for comparing).</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Who every creature glances at when they come near: the hero.</summary>
    public static Node3D? Hero { get; set; }

    /// <summary>
    /// A leg: the three bones from the hip or shoulder to the foot. With no End bone, the foot is the far end of the
    /// Mid bone, as far along it as Mark (a loose foot control bone, resting at the hoof) is from Mid's root.
    /// </summary>
    public sealed record Leg(string Root, string Mid, string? End = null, string? Mark = null);

    /// <summary>A model's bones: its legs, the bone that is its head (and which way that bone points), and its tail from root to tip.</summary>
    public sealed record Rig(Leg[] Legs, string? Head = null, Vector3? HeadForward = null, string? TailRoot = null, string? TailEnd = null);

    static Leg[] Quad(string fl, string fr, string bl, string br, Func<string, (string, string, string)> bones) =>
        [.. new[] { fl, fr, bl, br }.Select(l => { var (a, b, c) = bones(l); return new Leg(a, b, c); })];

    static readonly Dictionary<string, Rig> Rigs = new()
    {
        ["wolf.glb"] = new(Quad("Front_L", "Front_R", "Back_L", "Back_R", l => ($"{l[..^2]}Leg1{l[^2..]}", $"{l[..^2]}Leg2{l[^2..]}", $"{l[..^2]}Leg3{l[^2..]}")),
            "Head", Vector3.Up, "Tail1", "Tail4"),
        // Quaternius's farm animals: the hooves are loose control bones, so the foot is the far end of the lower leg.
        ["horse.glb"] = new([.. new[] { "Front.L", "Front.R", "Back.L", "Back.R" }.Select(l => new Leg($"{l[..^2]}UpLeg{l[^2..]}", $"{l[..^2]}LowLeg{l[^2..]}", Mark: $"{l[..^2]}Foot{l[^2..]}"))],
            "Head", Vector3.Up, "Tail1", "Tail4"),
        ["boar.glb"] = new(
            [new("Boar_Front_Leg_Left", "Boar_Forearm_Left", "Boar_Front_Foot_Left"), new("Boar_Front_Leg_Right", "Boar_Forearm_Right", "Boar_Front_Foot_Right"),
             new("Boar_Back_Leg_Left", "Boar_Shin_Left", "Boar_Back_Foot_Left"), new("Boar_Back_Leg_Right", "Boar_Shin_Right", "Boar_Back_Foot_Right")],
            "Boar_Head", Vector3.Up),
        ["dragon.glb"] = new(
            [new("leg_front_upper.L", "leg_front_lower.L", "leg_front_foot.L"), new("leg_front_upper.R", "leg_front_lower.R", "leg_front_foot.R"),
             new("leg_back_upper.L", "leg_back_lower.L", "leg_back_foot.L"), new("leg_back_upper.R", "leg_back_lower.R", "leg_back_foot.R")],
            "neck3", Vector3.Up, "tail1", "tail3"),
        ["deer_pm.glb"] = new(
            [new("bn_l_arm_02", "bn_l_arm_03", "bn_l_arm_04"), new("bn_r_arm_02", "bn_r_arm_03", "bn_r_arm_04"),
             new("bn_l_leg_02", "bn_l_leg_03", "bn_l_leg_04"), new("bn_r_leg_02", "bn_r_leg_03", "bn_r_leg_04")],
            "bn_head", Vector3.Up, "bn_tail", "bn_tail_end"),
        ["goblin.glb"] = new([new("upperleg_L", "lowerleg_L", Mark: "foot_L"), new("upperleg_R", "lowerleg_R", Mark: "foot_R")], "head", Vector3.Up),
        ["troll.glb"] = new([new("thigh.L", "shin.L", "foot.L"), new("thigh.R", "shin.R", "foot.R")]),
    };

    static readonly Dictionary<string, string> Same = new()
    {
        ["ice_wolf.glb"] = "wolf.glb", ["fox.glb"] = "wolf.glb", ["cow.glb"] = "horse.glb", ["sheep.glb"] = "horse.glb", ["pig.glb"] = "horse.glb",
    };

    public static Rig? For(string file) => Rigs.GetValueOrDefault(Same.GetValueOrDefault(file) ?? file);

    /// <summary>Gives a creature its footing, its glance and its tail, if its bones are known.</summary>
    public static Footing? Attach(Body body, Node3D model, string file)
    {
        if (!Enabled || For(file) is not { } rig) return null;
        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (skeleton is null) return null;
        skeleton.ModifierCallbackModeProcess = Skeleton3D.ModifierCallbackModeProcessEnum.Idle; // every frame, not every physics tick
        var footing = new Footing(body, model, skeleton, rig);
        skeleton.AddChild(footing);
        footing.ProcessPriority = 20; // after the clip's player has posed the bones
        footing.Build();
        skeleton.AddChild(footing.Witness);
        footing.Witness.ProcessPriority = 21; // and after the footing: it sees the finished pose
        return footing;
    }
}

/// <summary>
/// The modifier that runs first on a creature's skeleton each frame: reads where the clips put the feet, finds the
/// ground under each, and moves the legs' targets to it; aims the head at whoever's near; and keeps the tail's
/// spring fed. The leg IK, look-at and spring modifiers that follow it do the rest.
/// </summary>
public partial class Footing(Body body, Node3D model, Skeleton3D skeleton, Poise.Rig rig) : Node
{
    float _baseY = model.Position.Y, _drop;

    /// <summary>The model's been stood somewhere new (a body lifted onto its feet): stand it from there.</summary>
    public void Rebase() => _baseY = model.Position.Y;
    /// <summary>How far the body has settled below its stance to let a downhill leg reach the ground.</summary>
    public float Drop => _drop;
    readonly List<(int Root, int Mid, int End, float Past, Node3D Target)> _legs = [];
    readonly List<float> _hold = [];
    int _head = -1;
    Quaternion _turn = Quaternion.Identity;
    float _gazing;
    readonly List<int> _tail = [];
    Vector3 _wasAt, _wasVelocity;
    float _tailSwing, _tailRate, _wasYaw;
    /// <summary>How far the tail is swung aside from its clip, in degrees (for the self-test).</summary>
    public float TailSwing => Mathf.RadToDeg(_tailSwing);

    /// <summary>Whoever the creature looks at (the hero, usually); null and it looks where its clip says.</summary>
    public Node3D? Watching { get; set; }
    public float WatchRange { get; set; } = 14;

    /// <summary>How far each foot was moved to meet the ground last frame (for the self-test).</summary>
    public IReadOnlyList<float> Lifts => _lifts;
    /// <summary>Runs after every other modifier and keeps what they left: where the feet and the head ended up (for the self-test).</summary>
    public Witness Witness { get; } = new();
    /// <summary>How much of a look it's giving (0 none, 1 full), and how far the head is turned from its clip, in degrees.</summary>
    public float Gazing => _gazing;
    public float HeadTurn => Mathf.RadToDeg(_turn.GetAngle() > Mathf.Pi ? Mathf.Tau - _turn.GetAngle() : _turn.GetAngle());
    readonly List<float> _lifts = [];

    public void Build()
    {
        Witness.Skeleton = skeleton;
        Witness.Head = rig.Head is { } h ? skeleton.FindBone(h) : -1;
        foreach (var leg in rig.Legs)
        {
            var (root, mid, end) = (skeleton.FindBone(leg.Root), skeleton.FindBone(leg.Mid), leg.End is null ? -1 : skeleton.FindBone(leg.End));
            if (root < 0 || mid < 0 || (leg.End is not null && end < 0)) continue;
            var target = new Node3D { Name = $"Foot_{leg.End ?? leg.Mid}" };
            body.AddChild(target);
            // No foot bone: the foot is a point past the end of the lower leg, as far as the foot control rests from
            // the knee (or, with no such bone, as far again as the thigh is long).
            var mark = leg.Mark is null ? -1 : skeleton.FindBone(leg.Mark);
            var past = end >= 0 ? 0
                : mark >= 0 ? skeleton.GetBoneGlobalRest(mark).Origin.DistanceTo(skeleton.GetBoneGlobalRest(mid).Origin)
                : skeleton.GetBoneRest(mid).Origin.Length();
            _legs.Add((root, mid, end, past, target));
            _lifts.Add(0);
            _hold.Add(0);
            Witness.Feet.Add((end >= 0 ? end : mid, past, target));
        }
        if (rig.Head is { } head) _head = skeleton.FindBone(head);
        if (rig.TailRoot is { } tailRoot && rig.TailEnd is { } tailEnd && skeleton.FindBone(tailRoot) >= 0 && skeleton.FindBone(tailEnd) >= 0)
        {
            // The tail, root to tip, by walking up from the tip.
            for (var b = skeleton.FindBone(tailEnd); b >= 0; b = skeleton.GetBoneParent(b))
            {
                _tail.Insert(0, b);
                if (b == skeleton.FindBone(tailRoot)) break;
            }
        }
    }

    /// <summary>How many times the pose has been worked on (for the self-test).</summary>
    public int Passes { get; private set; }

    /// <summary>
    /// Done every frame after the clip has set its pose (this runs late in the frame: see <see cref="Poise.Attach"/>),
    /// rather than as a modifier pass: the skeleton runs those only on frames the clip changed something, and a still
    /// idle would leave the feet and the glance behind.
    /// </summary>
    public override void _Process(double delta)
    {
        Passes++;
        var here = body.GlobalPosition;
        var baseGround = City.FloorAt(here);
        var toWorld = skeleton.GlobalTransform;
        // Where the clip has each foot (the foot bone, or the lower leg carried on by its own length), and how much
        // higher or lower the ground is there than under the body.
        var feet = new Vector3[_legs.Count];
        for (var i = 0; i < _legs.Count; i++)
        {
            var (_, mid, end, past, _) = _legs[i];
            feet[i] = end >= 0 ? skeleton.GetBoneGlobalPose(end).Origin : skeleton.GetBoneGlobalPose(mid) * new Vector3(0, past, 0);
            _lifts[i] = Mathf.Clamp(City.FloorAt(toWorld * feet[i]) - baseGround, -0.6f, 0.6f);
        }
        // A leg can bend to meet higher ground but not stretch to lower: the body settles down by the deepest
        // shortfall instead, and the other legs bend the more.
        var deepest = _legs.Count > 0 ? Mathf.Max(0, -_lifts.Min()) : 0;
        _drop = Mathf.Lerp(_drop, deepest, 0.2f);
        model.Position = model.Position with { Y = _baseY - _drop };
        toWorld = skeleton.GlobalTransform;
        for (var i = 0; i < _legs.Count; i++)
        {
            var (root, mid, end, past, target) = _legs[i];
            var rise = _lifts[i] + _drop;
            target.GlobalPosition = toWorld * feet[i] + Vector3.Up * rise;
            _hold[i] = Mathf.Clamp(Mathf.Abs(rise) / 0.03f, 0, 1); // a hair's difference isn't worth a bent knee
            if (_hold[i] > 0.001f) Reach(root, mid, end, past, target.GlobalPosition, _hold[i]);
        }
        if (_head >= 0) Glance(here, toWorld);
        if (_tail.Count > 0) Swing(here, (float)delta);
    }

    /// <summary>
    /// The tail swings with the body's own motion: it's a weight on a spring, flung the other way when the body
    /// turns or sets off, and settling back as the spring's damping lets it. Spread down the tail, root to tip.
    /// </summary>
    void Swing(Vector3 here, float delta)
    {
        if (delta <= 0 || delta > 0.25f) { _wasAt = here; return; }
        var yaw = body.GlobalRotation.Y;
        var turned = Mathf.Wrap(yaw - _wasYaw, -Mathf.Pi, Mathf.Pi);
        var velocity = (here - _wasAt) / delta;
        var sideways = body.GlobalTransform.Basis.X.Dot(velocity - _wasVelocity) / Mathf.Max(delta, 0.001f); // sideways acceleration
        (_wasAt, _wasVelocity, _wasYaw) = (here, velocity, yaw);
        // The spring: pulled toward straight, flung by the turn and the sideways lurch, damped.
        var force = -_tailSwing * 18f - _tailRate * 4.5f + turned * 40f - sideways * 0.08f;
        _tailRate += force * delta;
        _tailSwing = Mathf.Clamp(_tailSwing + _tailRate * delta, -0.9f, 0.9f);
        for (var i = 0; i < _tail.Count; i++)
        {
            var share = _tailSwing * (0.35f + 0.65f * (i + 1) / _tail.Count) / _tail.Count * 1.6f;
            var bone = _tail[i];
            var pose = skeleton.GetBonePoseRotation(bone);
            skeleton.SetBonePoseRotation(bone, pose * new Quaternion(Vector3.Up, share)); // about the bone's own up: aside, not up and down
        }
    }

    /// <summary>
    /// Two-bone IK, by hand (the engine's own does nothing on these rigs): the knee is bent to put the foot the right
    /// distance from the hip, then the hip is swung so the foot lands on its mark. Everything is worked in the
    /// skeleton's space and written back as each bone's rotation relative to its parent.
    /// </summary>
    void Reach(int root, int mid, int end, float past, Vector3 markWorld, float weight)
    {
        var toSkel = skeleton.GlobalTransform.AffineInverse();
        var mark = toSkel * markWorld;
        var hip = skeleton.GetBoneGlobalPose(root);
        var knee = skeleton.GetBoneGlobalPose(mid);
        var foot = end >= 0 ? skeleton.GetBoneGlobalPose(end).Origin : knee * new Vector3(0, past, 0);
        var (r, m, e) = (hip.Origin, knee.Origin, foot);
        var (a, b) = ((m - r).Length(), (e - m).Length());
        if (a < 1e-4f || b < 1e-4f) return;
        var d = Mathf.Clamp((mark - r).Length(), Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        // The bend: the angle at the knee that puts the foot d from the hip.
        var thigh = (r - m).Normalized();
        var shin = (e - m).Normalized();
        var axis = thigh.Cross(shin);
        if (axis.LengthSquared() < 1e-6f) axis = body.GlobalTransform.Basis.X; // a straight leg bends sideways-on to the body
        axis = axis.Normalized();
        var now = Mathf.Acos(Mathf.Clamp(thigh.Dot(shin), -1, 1));
        var want = Mathf.Acos(Mathf.Clamp((a * a + b * b - d * d) / (2 * a * b), -1, 1));
        var bend = new Quaternion(axis, (want - now) * weight);
        var kneeBasis = new Basis(bend) * knee.Basis;
        skeleton.SetBonePoseRotation(mid, (hip.Basis.Inverse() * kneeBasis).GetRotationQuaternion());
        // The swing: the hip turned so the (now bent) leg's foot points at the mark.
        var bentFoot = m + bend * (e - m);
        var from = (bentFoot - r).Normalized();
        var to = (mark - r).Normalized();
        var swingAxis = from.Cross(to);
        if (swingAxis.LengthSquared() > 1e-8f)
        {
            var swing = new Quaternion(swingAxis.Normalized(), from.AngleTo(to) * weight);
            var parent = skeleton.GetBoneParent(root);
            var parentBasis = parent >= 0 ? skeleton.GetBoneGlobalPose(parent).Basis : Basis.Identity;
            skeleton.SetBonePoseRotation(root, (parentBasis.Inverse() * (new Basis(swing) * hip.Basis)).GetRotationQuaternion());
        }
    }

    /// <summary>
    /// The head turns toward whoever's near and in front (the engine's own look-at modifier does nothing on these
    /// rigs, so this is done by hand): the bone's own axis (which runs out along the muzzle) is swung toward them,
    /// no more than a fair turn of the neck, eased in and out, on top of whatever its clip is doing.
    /// </summary>
    void Glance(Vector3 here, Transform3D toWorld)
    {
        var who = Watching ?? Poise.Hero;
        var want = 0f;
        var goal = Quaternion.Identity;
        var pose = skeleton.GetBoneGlobalPose(_head);
        if (who is not null && IsInstanceValid(who))
        {
            var to = who.GlobalPosition - here;
            var ahead = -body.GlobalTransform.Basis.Z;
            if (to.Length() < WatchRange && to.Normalized().Dot(ahead) > -0.2f)
            {
                want = 1;
                var eye = toWorld * pose.Origin;
                var dir = toWorld.Basis.Inverse() * (who.GlobalPosition + Vector3.Up * 1.4f - eye); // into skeleton space
                var local = (pose.Basis.Inverse() * dir).Normalized(); // and into the head's own
                var forward = Vector3.Up; // the bone's axis
                var angle = forward.AngleTo(local);
                if (angle > 0.01f)
                {
                    var axis = forward.Cross(local).Normalized();
                    goal = new Quaternion(axis, Mathf.Min(angle, Mathf.DegToRad(60)));
                }
            }
        }
        _gazing = Mathf.MoveToward(_gazing, want, 0.04f);
        _turn = _turn.Slerp(goal, 0.12f);
        var turn = Quaternion.Identity.Slerp(_turn, _gazing);
        var parent = skeleton.GetBoneParent(_head);
        var parentBasis = parent >= 0 ? skeleton.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        var turned = pose.Basis * new Basis(turn);
        skeleton.SetBonePoseRotation(_head, (parentBasis.Inverse() * turned).GetRotationQuaternion());
    }
}

/// <summary>The last modifier on a creature's skeleton: it changes nothing and records the pose the others left (the self-test reads it; from outside, a bone's pose reads as its clip's).</summary>
public partial class Witness : Node
{
    public Skeleton3D? Skeleton { get; set; }
    public int Head { get; set; } = -1;
    /// <summary>Each foot: its bone (or the lower leg), how far past that bone's origin the foot is (0 for a foot bone), and the target it was given.</summary>
    public List<(int Bone, float Past, Node3D Target)> Feet { get; } = [];
    public List<Vector3> FeetAt { get; } = [];
    /// <summary>How far each foot ended from where the IK was asked to put it.</summary>
    public List<float> Misses { get; } = [];
    public Basis HeadBasis { get; private set; } = Basis.Identity;

    public override void _Process(double delta)
    {
        if (Skeleton is null) return;
        var toWorld = Skeleton.GlobalTransform;
        FeetAt.Clear();
        Misses.Clear();
        foreach (var (bone, past, target) in Feet)
        {
            var at = toWorld * (Skeleton.GetBoneGlobalPose(bone) * new Vector3(0, past, 0));
            FeetAt.Add(at);
            Misses.Add(at.DistanceTo(target.GlobalPosition));
        }
        if (Head >= 0) HeadBasis = toWorld.Basis * Skeleton.GetBoneGlobalPose(Head).Basis;
    }
}
