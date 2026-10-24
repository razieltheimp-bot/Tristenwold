using Godot;

namespace Tristinwold.Game;

/// <summary>
/// The 3D models: Kay Lousberg's KayKit packs (CC0), from game/assets/kaykit, and Poly Haven's
/// scanned rocks and props (CC0), from game/assets/polyhaven/models. Each pack is drawn
/// at its own scale, so every model is placed through here at a size that fits a person 1.85 m tall.
/// </summary>
public static class Models
{
    const string Root = "res://assets/kaykit/";
    const string PolyHaven = "res://assets/polyhaven/models/"; // "ph:boulder.glb": a Poly Haven scan, at its real size
    static readonly Dictionary<string, PackedScene?> Scenes = [];
    static readonly Dictionary<string, Mesh?> Meshes = [];

    /// <summary>Building colours: each town picks one.</summary>
    public static readonly string[] Colours = ["red", "blue", "green"];

    public static PackedScene? Scene(string path)
    {
        if (Scenes.TryGetValue(path, out var scene)) return scene;
        var full = path.StartsWith("ph:") ? PolyHaven + path[3..] : Root + path;
        return Scenes[path] = ResourceLoader.Exists(full) ? GD.Load<PackedScene>(full) : null;
    }

    /// <summary>A model placed in the world: scaled, turned, and (if solid) given a box to bump into.</summary>
    public static Node3D Place(Node3D parent, string path, Vector3 at, float scale, float yaw = 0, bool solid = true, float shrink = 0.8f)
    {
        var holder = solid ? (Node3D)new StaticBody3D() : new Node3D();
        holder.Position = at;
        holder.Rotation = new Vector3(0, yaw, 0);
        parent.AddChild(holder);
        if (Scene(path)?.Instantiate() is not Node3D model) return holder;
        model.Scale = Vector3.One * scale;
        holder.AddChild(model);
        if (solid)
        {
            var box = Bounds(model);
            if (box.Size != Vector3.Zero)
                holder.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = new Vector3(box.Size.X * shrink, box.Size.Y, box.Size.Z * shrink) },
                    Position = box.GetCenter(),
                });
        }
        return holder;
    }

    /// <summary>The first mesh in a model, for drawing many copies at once (trees, rocks, grass).</summary>
    public static Mesh? MeshOf(string path)
    {
        if (Meshes.TryGetValue(path, out var mesh)) return mesh;
        Mesh? found = null;
        if (Scene(path)?.Instantiate() is Node3D model)
        {
            found = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh;
            model.Free();
        }
        return Meshes[path] = found;
    }

    /// <summary>The bounding box of everything visible in a model, in the model's parent's space.</summary>
    public static Aabb Bounds(Node3D model)
    {
        Aabb? box = null;
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (!m.Visible) continue;
            var t = Transform3D.Identity;
            for (Node? n = m; n is not null && n != model.GetParent(); n = n.GetParent())
                if (n is Node3D n3) t = n3.Transform * t;
            var b = t * m.GetAabb();
            box = box is { } x ? x.Merge(b) : b;
        }
        return box ?? new Aabb();
    }

    /// <summary>Recolours a model by multiplying a tint into its materials (a frost troll, an Ashborn, a wraith).</summary>
    public static void Tint(Node model, Color tint, float glow = 0)
    {
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (var s = 0; s < m.GetSurfaceOverrideMaterialCount(); s++)
            {
                if ((m.GetActiveMaterial(s)?.Duplicate() ?? new StandardMaterial3D()) is not BaseMaterial3D mat) continue;
                mat.AlbedoColor = tint;
                if (glow > 0) { mat.EmissionEnabled = true; mat.Emission = tint; mat.EmissionEnergyMultiplier = glow; }
                m.SetSurfaceOverrideMaterial(s, mat);
            }
    }

    // ---- what goes where -----------------------------------------------------------------------

    public static string Building(string kind, string colour) => $"medieval/{colour}/building_{kind}_{colour}.gltf";

    /// <summary>Which building suits a named place: "The Drunken Stag" is a tavern, "The Temple of the Loom" a church.</summary>
    public static string BuildingFor(string place)
    {
        var p = place.ToLowerInvariant();
        if (p.Contains("temple") || p.Contains("shrine") || p.Contains("chapel") || p.Contains("choir")) return "church";
        if (p.Contains("hall") || p.Contains("keep") || p.Contains("court") || p.Contains("crown") || p.Contains("eyrie") || p.Contains("longhouse")) return "castle";
        if (p.Contains("tower") || p.Contains("archive") || p.Contains("library") || p.Contains("loom") || p.Contains("bell")) return "tower_B";
        if (p.Contains("market") || p.Contains("exchange") || p.Contains("arcade") || p.Contains("stall") || p.Contains("counting")) return "market";
        if (p.Contains("forge") || p.Contains("smith")) return "blacksmith";
        if (p.Contains("watch") || p.Contains("barracks") || p.Contains("fort")) return "barracks";
        if (p.Contains("mine") || p.Contains("pit") || p.Contains("undervault")) return "mine";
        if (p.Contains("mill")) return "windmill";
        if (p.StartsWith("the ") && p.Split(' ').Length == 3) return "tavern"; // "The Hanged Lark", "The Last Toast"
        return "home_B";
    }

    public static float BuildingScale(string kind) => kind switch
    {
        "castle" => 7f,
        "tower_A" or "tower_B" => 6.5f,
        "church" => 7.5f,
        _ => 7.5f,
    };
}

/// <summary>
/// What a fighter or a townsperson looks like and how it moves. A KayKit character with its
/// weapons and its animations, or a beast built from shapes.
/// </summary>
public abstract partial class Body : Node3D
{
    /// <summary>Walking or running at this speed (m/s), on the ground or not, in the water or not.</summary>
    public abstract void Move(float speed, bool grounded, bool swimming);
    public abstract void Attack(bool heavy = false);
    public abstract void Cast();
    public abstract void Hurt();
    public abstract void Die();
    public abstract void Rise();
    public virtual void Talk() { }

    /// <summary>A red glow for a nemesis.</summary>
    public abstract void Mark(Color colour);

    public static Body For(string look) => look switch
    {
        "skeever" => Beast.Rat(),
        "wolf" => Beast.Wolf(new Color("#5a5650")),
        "ice_wolf" => Beast.Wolf(new Color("#d8e2ea")),
        "bear" => Beast.Bear(),
        "mudcrab" => Beast.Crab(),
        _ => Figure.Create(look),
    };
}

/// <summary>A KayKit adventurer or skeleton, dressed for the part and animated.</summary>
public partial class Figure : Body
{
    /// <summary>Character presets: model, what they carry and wear, size and tint.</summary>
    public sealed record Look(string Model, string[] Show, float Scale = 0.74f, Color? Tint = null, float Glow = 0, string? RightHand = null, string? LeftHand = null, bool Unarmed = false);

    public static readonly Dictionary<string, Look> Looks = new()
    {
        // Playable classes.
        ["Knight"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet", "Knight_Cape"]),
        ["Barbarian"] = new("Barbarian", ["1H_Axe", "Barbarian_Round_Shield", "Barbarian_Hat", "Barbarian_Cape"]),
        ["Mage"] = new("Mage", ["2H_Staff", "Mage_Hat", "Mage_Cape"]),
        ["Rogue"] = new("Rogue", ["Knife", "Knife_Offhand", "Rogue_Cape"]),
        ["Rogue_Hooded"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand", "Rogue_Cape"]),
        // Enemies.
        ["dock_thug"] = new("Barbarian", ["1H_Axe", "Barbarian_Cape"]),
        ["lantern_boy"] = new("Rogue", ["Knife"], 0.66f, new Color("#ffcf9a")),
        ["drowned_hand"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand", "Rogue_Cape"], 0.76f, new Color("#9ac8e8")),
        ["bandit"] = new("Rogue_Hooded", ["Knife", "1H_Crossbow", "Rogue_Cape"], 0.74f, new Color("#c8b08a")),
        ["bandit_chief"] = new("Barbarian", ["2H_Axe", "Barbarian_Hat", "Barbarian_Cape"], 0.84f, new Color("#e0a090")),
        ["draugr"] = new("Skeleton_Warrior", [], 0.7f, RightHand: "Skeleton_Axe", LeftHand: "Skeleton_Shield_Small_A"),
        ["draugr_overlord"] = new("Skeleton_Warrior", [], 0.92f, new Color("#bfe4ff"), 0.15f, RightHand: "Skeleton_Blade", LeftHand: "Skeleton_Shield_Large_A"),
        ["sea_wraith"] = new("Skeleton_Mage", [], 0.74f, new Color("#7fe0d8"), 0.6f, RightHand: "Skeleton_Staff"),
        ["spriggan"] = new("Skeleton_Rogue", [], 0.78f, new Color("#a8c070"), 0.3f, RightHand: "Skeleton_Blade"),
        ["frost_troll"] = new("Barbarian", ["2H_Axe"], 1.35f, new Color("#c8d4e4")),
        // People.
        ["guard"] = new("Knight", ["1H_Sword", "Round_Shield", "Knight_Helmet", "Knight_Cape"]),
        ["scholar"] = new("Mage", ["Spellbook", "Mage_Cape"], Unarmed: true),
        ["priest"] = new("Mage", ["Spellbook_open", "Mage_Hat", "Mage_Cape"], Unarmed: true),
        ["worker"] = new("Barbarian", ["Barbarian_Cape"], Unarmed: true),
        ["drinker"] = new("Barbarian", ["Mug", "Barbarian_Hat"], Unarmed: true),
        ["hunter"] = new("Rogue_Hooded", ["1H_Crossbow", "Rogue_Cape"]),
        ["townsfolk"] = new("Rogue", ["Rogue_Cape"], Unarmed: true),
        ["traveller"] = new("Rogue_Hooded", ["Rogue_Cape"], Unarmed: true),
        ["ashborn"] = new("Rogue_Hooded", ["Rogue_Cape"], 0.7f, new Color("#e08070"), Unarmed: true),
        ["captive"] = new("Rogue", [], 0.72f, new Color("#d8d0c8"), Unarmed: true),
    };

    static readonly string[] Equipment = ["Sword", "Shield", "Axe", "Mug", "Spellbook", "Wand", "Staff", "Crossbow", "Knife", "Throwable", "Hat", "Helmet", "Cape"];

    static readonly HashSet<string> Looping =
    [
        "Idle", "Unarmed_Idle", "2H_Melee_Idle", "Walking_A", "Walking_B", "Walking_C", "Walking_Backwards", "Running_A", "Running_B",
        "Blocking", "Spellcasting", "Jump_Idle", "Sit_Floor_Idle", "Sit_Chair_Idle", "Lie_Idle", "Cheer", "Death_A_Pose", "Death_B_Pose",
    ];

    AnimationPlayer? _anim;
    Look _look = Looks["Knight"];
    double _busy;
    bool _dead;
    int _swing;
    string _loop = "";

    public static Figure Create(string look)
    {
        var l = Looks.GetValueOrDefault(look) ?? (look.StartsWith('#') ? Looks["Knight"] : Looks["townsfolk"]);
        var f = new Figure { _look = l };
        if (Models.Scene($"characters/{l.Model}.glb")?.Instantiate() is Node3D model)
        {
            model.Scale = Vector3.One * l.Scale;
            model.RotationDegrees = new Vector3(0, 180, 0); // KayKit faces +Z; Godot's forward is -Z
            f.AddChild(model);
            foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                if (Equipment.Any(e => m.Name.ToString().Contains(e))) m.Visible = l.Show.Contains(m.Name.ToString());
            if (l.Tint is { } tint) Models.Tint(model, tint, l.Glow);
            if (model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is { } skeleton)
            {
                Hold(skeleton, "handslot.r", l.RightHand);
                Hold(skeleton, "handslot.l", l.LeftHand);
            }
            if (model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is { } body)
                body.AddChild(new Proportions());
            f._anim = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (f._anim is not null)
                foreach (var name in f._anim.GetAnimationList())
                    if (Looping.Contains(name)) f._anim.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }
        return f;
    }

    static void Hold(Skeleton3D skeleton, string bone, string? item)
    {
        if (item is null || skeleton.FindBone(bone) < 0 || Models.Scene($"weapons/{item}.gltf")?.Instantiate() is not Node3D weapon) return;
        var slot = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(slot);
        slot.AddChild(weapon);
    }

    public override void _Ready() => Play(_look.Unarmed ? "Unarmed_Idle" : "Idle");

    public override void _Process(double delta) => _busy -= delta;

    void Play(string name, float blend = 0.18f, float speed = 1)
    {
        if (_anim is null || !_anim.HasAnimation(name)) return;
        _anim.Play(name, blend, speed);
    }

    void Once(string name, double seconds, float speed = 1)
    {
        if (_dead) return;
        Play(name, 0.08f, speed);
        _anim?.Seek(0, true);
        _busy = seconds / speed;
        _loop = "";
    }

    public override void Move(float speed, bool grounded, bool swimming)
    {
        if (_dead || _busy > 0) return;
        var idle = _look.Unarmed ? "Unarmed_Idle" : "Idle";
        var (name, rate) = swimming ? ("Walking_C", 0.8f)
            : !grounded ? ("Jump_Idle", 1f)
            : speed > 6.5f ? ("Running_A", speed / 7.5f)
            : speed > 0.4f ? ("Walking_A", Mathf.Max(0.6f, speed / 3.8f))
            : (idle, 1f);
        if (name == _loop) { if (_anim is not null) _anim.SpeedScale = rate; return; }
        _loop = name;
        Play(name);
        if (_anim is not null) _anim.SpeedScale = rate;
    }

    public override void Attack(bool heavy = false)
    {
        if (_anim is not null) _anim.SpeedScale = 1;
        string[] swings = _look.Model.StartsWith("Skeleton") || _look.Show.Contains("2H_Axe")
            ? ["2H_Melee_Attack_Chop", "2H_Melee_Attack_Slice"]
            : _look.Unarmed ? ["Unarmed_Melee_Attack_Punch_A", "Unarmed_Melee_Attack_Punch_B"]
            : ["1H_Melee_Attack_Slice_Diagonal", "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Horizontal"];
        Once(heavy ? "2H_Melee_Attack_Spin" : swings[_swing++ % swings.Length], heavy ? 1.1 : 0.7, heavy ? 1 : 1.4f);
    }

    public override void Cast()
    {
        if (_anim is not null) _anim.SpeedScale = 1;
        Once("Spellcast_Shoot", 0.8, 1.3f);
    }

    public override void Hurt()
    {
        if (_busy > 0.2) return; // don't cut off a swing for every scratch
        if (_anim is not null) _anim.SpeedScale = 1;
        Once("Hit_A", 0.4, 1.3f);
    }

    public override void Die()
    {
        if (_anim is not null) _anim.SpeedScale = 1;
        Play(GD.Randf() < 0.5f ? "Death_A" : "Death_B", 0.1f);
        _dead = true;
    }

    public override void Rise()
    {
        _dead = false;
        _busy = 0;
        _loop = "";
        Once("Lie_StandUp", 1.2);
    }

    public override void Talk() => Once("Interact", 1.2);

    public override void Mark(Color colour) => Models.Tint(this, colour.Lerp(Colors.White, 0.35f), 0.35f);
}

/// <summary>
/// Gives the KayKit characters human proportions, keeping their blocky body as the centre of mass:
/// a smaller head, a taller trunk, and arms and legs half as long again, so a figure reads as a
/// person rather than a big head on a box. It runs after the animations each frame, stretching
/// each limb bone along its length; a stretched bone's children are un-stretched so a hand or
/// foot keeps its shape, and the hips are raised by however much the legs grew.
/// </summary>
public partial class Proportions : SkeletonModifier3D
{
    const float Leg = 2.3f, Arm = 1.75f;

    /// <summary>Off draws the characters as KayKit made them (for comparing).</summary>
    public static bool Enabled { get; set; } = true;

    static readonly Dictionary<string, Vector3> Stretch = new()
    {
        ["head"] = Vector3.One * 0.5f,
        ["spine"] = new(0.92f, 1.3f, 0.92f),
        ["chest"] = new(1f, 1.15f, 0.95f),
        ["upperarm.l"] = new(1, Arm, 1), ["lowerarm.l"] = new(1, Arm, 1),
        ["upperarm.r"] = new(1, Arm, 1), ["lowerarm.r"] = new(1, Arm, 1),
        ["upperleg.l"] = new(1, Leg, 1), ["lowerleg.l"] = new(1, Leg, 1),
        ["upperleg.r"] = new(1, Leg, 1), ["lowerleg.r"] = new(1, Leg, 1),
    };

    Vector3[] _scale = [];
    int _hips = -1;
    float _lift;

    void Measure(Skeleton3D s)
    {
        _scale = new Vector3[s.GetBoneCount()];
        for (var i = 0; i < _scale.Length; i++) _scale[i] = Stretch.GetValueOrDefault(s.GetBoneName(i), Vector3.One);
        _hips = s.FindBone("hips");
        int lower = s.FindBone("lowerleg.l"), foot = s.FindBone("foot.l");
        if (lower >= 0 && foot >= 0) _lift = (s.GetBoneRest(lower).Origin.Length() + s.GetBoneRest(foot).Origin.Length()) * (Leg - 1);
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        if (!Enabled || GetSkeleton() is not { } s) return;
        if (_scale.Length != s.GetBoneCount()) Measure(s);
        for (var i = 0; i < _scale.Length; i++)
        {
            var parent = s.GetBoneParent(i);
            var undo = parent >= 0 ? _scale[parent] : Vector3.One;
            if (undo == Vector3.One && _scale[i] == Vector3.One && i != _hips) continue;
            var pose = s.GetBonePose(i);
            var basis = Basis.FromScale(Vector3.One / undo) * pose.Basis * Basis.FromScale(_scale[i]);
            var origin = i == _hips ? pose.Origin + Vector3.Up * _lift : pose.Origin;
            s.SetBonePose(i, new Transform3D(basis, origin));
        }
    }
}

/// <summary>A four-legged animal built from shapes, with a trotting gait: skeevers, wolves, bears, mudcrabs.</summary>
public partial class Beast : Body
{
    readonly List<Node3D> _legs = [];
    Node3D _torso = new(), _head = new();
    float _phase, _speed, _lunge, _stride = 0.5f, _bob = 0.05f, _headZ;
    bool _dead;
    double _flash;
    readonly List<StandardMaterial3D> _materials = [];

    static StandardMaterial3D Mat(Color c) => new() { AlbedoColor = c, Roughness = 0.85f };

    MeshInstance3D Part(Node3D parent, Mesh mesh, Color colour, Vector3 at, Vector3? rotation = null)
    {
        var mat = Mat(colour);
        _materials.Add(mat);
        var m = new MeshInstance3D { Mesh = mesh, Position = at, RotationDegrees = rotation ?? Vector3.Zero, MaterialOverride = mat };
        parent.AddChild(m);
        return m;
    }

    /// <summary>A body (length, girth), a head, four legs and maybe a tail.</summary>
    static Beast Build(Color fur, Color dark, float length, float girth, float legLength, float headSize, float tail, bool ears)
    {
        var b = new Beast { _stride = 0.6f, _bob = girth * 0.08f };
        var hip = legLength + girth * 0.4f;
        b._torso = new Node3D { Position = new Vector3(0, hip, 0) };
        b.AddChild(b._torso);
        b.Part(b._torso, new CapsuleMesh { Radius = girth / 2, Height = length }, fur, Vector3.Zero, new Vector3(90, 0, 0));
        b._headZ = -length / 2 - headSize * 0.3f;
        b._head = new Node3D { Position = new Vector3(0, girth * 0.35f, b._headZ) };
        b._torso.AddChild(b._head);
        b.Part(b._head, new SphereMesh { Radius = headSize / 2, Height = headSize }, fur, Vector3.Zero);
        b.Part(b._head, new CylinderMesh { TopRadius = headSize * 0.16f, BottomRadius = headSize * 0.28f, Height = headSize * 0.8f }, dark, new Vector3(0, -headSize * 0.08f, -headSize * 0.55f), new Vector3(-90, 0, 0));
        b.Part(b._head, new SphereMesh { Radius = headSize * 0.07f, Height = headSize * 0.14f }, new Color("#101010"), new Vector3(headSize * 0.2f, headSize * 0.15f, -headSize * 0.38f));
        b.Part(b._head, new SphereMesh { Radius = headSize * 0.07f, Height = headSize * 0.14f }, new Color("#101010"), new Vector3(-headSize * 0.2f, headSize * 0.15f, -headSize * 0.38f));
        if (ears)
            foreach (var side in new[] { -1f, 1f })
                b.Part(b._head, new CylinderMesh { TopRadius = 0, BottomRadius = headSize * 0.16f, Height = headSize * 0.4f }, dark, new Vector3(side * headSize * 0.25f, headSize * 0.45f, 0.02f), new Vector3(0, 0, side * -15));
        if (tail > 0)
            b.Part(b._torso, new CylinderMesh { TopRadius = 0.01f, BottomRadius = girth * 0.12f, Height = tail }, dark, new Vector3(0, girth * 0.15f, length / 2 + tail * 0.4f), new Vector3(-70, 0, 0));
        foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var leg = new Node3D { Position = new Vector3(x * girth * 0.32f, hip - girth * 0.1f, z * length * 0.32f) };
            b.AddChild(leg);
            b.Part(leg, new CapsuleMesh { Radius = girth * 0.13f, Height = legLength + girth * 0.2f }, dark, new Vector3(0, -legLength / 2, 0));
            b._legs.Add(leg);
        }
        return b;
    }

    public static Beast Rat() => Build(new Color("#6b5a4a"), new Color("#4a3c30"), 0.75f, 0.38f, 0.16f, 0.3f, 0.6f, ears: true);
    public static Beast Wolf(Color fur) => Build(fur, fur.Darkened(0.3f), 1.15f, 0.48f, 0.5f, 0.38f, 0.5f, ears: true);
    public static Beast Bear() => Build(new Color("#4a3428"), new Color("#2e2018"), 1.6f, 1.0f, 0.55f, 0.6f, 0.1f, ears: true);

    public static Beast Crab()
    {
        var b = Build(new Color("#7a6a4a"), new Color("#5a4a30"), 0.5f, 0.7f, 0.22f, 0.25f, 0, ears: false);
        b._torso.Scale = new Vector3(1.6f, 0.6f, 1.2f);
        return b;
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        if (_flash > 0 && (_flash -= delta) <= 0) foreach (var m in _materials) m.EmissionEnabled = false;
        if (_dead) return;
        _phase += d * (2f + _speed * 2.2f);
        var swing = Mathf.Clamp(_speed / 6f, 0, 1) * _stride;
        for (var i = 0; i < _legs.Count; i++)
            _legs[i].Rotation = new Vector3(Mathf.Sin(_phase + (i is 0 or 3 ? 0 : Mathf.Pi)) * swing, 0, 0);
        _torso.Position = _torso.Position with { Y = _legs[0].Position.Y + _bob * Mathf.Abs(Mathf.Sin(_phase)) * Mathf.Clamp(_speed / 3f, 0, 1) + 0.05f };
        _lunge = Mathf.MoveToward(_lunge, 0, d * 3);
        _head.Position = _head.Position with { Z = _headZ - _lunge * 0.35f };
    }

    public override void Move(float speed, bool grounded, bool swimming) => _speed = speed;
    public override void Attack(bool heavy = false) => _lunge = 1;
    public override void Cast() => _lunge = 1;

    public override void Hurt()
    {
        foreach (var m in _materials) { m.EmissionEnabled = true; m.Emission = new Color("#ff4020"); m.EmissionEnergyMultiplier = 0.6f; }
        _flash = 0.15;
    }

    public override void Die()
    {
        _dead = true;
        var t = CreateTween();
        t.TweenProperty(this, "rotation:z", Mathf.Pi / 2, 0.4);
    }

    public override void Rise()
    {
        _dead = false;
        Rotation = Vector3.Zero;
    }

    public override void Mark(Color colour)
    {
        foreach (var m in _materials) m.AlbedoColor = m.AlbedoColor.Lerp(colour, 0.35f);
    }
}
