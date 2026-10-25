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
        var full = path.StartsWith("ph:") ? PolyHaven + path[3..] : path.StartsWith("creature:") ? "res://assets/creatures/" + path[9..] : Root + path;
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

    /// <summary>Each dragon's wings: the membrane, and the bones that hold it (a green dragon's are amber on green).</summary>
    static readonly Dictionary<string, (Color Membrane, Color Bone)> Wings = new()
    {
        ["red"] = (new("#b0281a"), new("#3a1410")), ["blue"] = (new("#4c7ad0"), new("#1c2c60")),
        ["green"] = (new("#d97a2a"), new("#3e7a34")), ["black"] = (new("#3a2c44"), new("#141216")),
        ["gold"] = (new("#e8b44a"), new("#9a6a1c")), ["silver"] = (new("#d4dae4"), new("#8a909c")),
        ["bronze"] = (new("#3c8a7a"), new("#7a4c22")), ["copper"] = (new("#d07a44"), new("#5a8a7a")),
        ["brass"] = (new("#d8b860"), new("#8a6a2a")),
    };

    /// <summary>
    /// A dragon in one of the ten colours, normal-sized (4.5 m); the metallic ones shine. All but the white
    /// (frost) dragon spread great wings and fly; the frost dragon keeps its small wings and stays on the ground.
    /// </summary>
    static Body Dragon(string colour)
    {
        var metallic = colour is "gold" or "silver" or "bronze" or "copper" or "brass";
        if (Creature.Create("dragon.glb", 4.5f, coat: $"dragons/{colour}.png", metallic: metallic) is not { } dragon) return Beast.Bear();
        if (Wings.TryGetValue(colour, out var wings)) dragon.SpreadWings(wings.Membrane, wings.Bone, metallic);
        return dragon;
    }

    public static Body For(string look) => look switch
    {
        "skeever" => (Body?)Creature.Create("giant_rat.glb", 0.6f) ?? Beast.Rat(),
        "spriggan" => (Body?)Creature.Create("forest_monster.glb", 2.3f) ?? Figure.Create(look),
        "frost_troll" => (Body?)Creature.Create("troll.glb", 2.4f, new Color("#9ec0e4")) ?? Figure.Create(look),
        "goblin" => (Body?)Creature.Create("goblin.glb", 1.3f) ?? Figure.Create(look),
        "dragon" => Dragon("white"),
        _ when look.StartsWith("dragon_") => Dragon(look[7..]),
        "wolf" => (Body?)Creature.Create("wolf.glb", 0.95f) ?? Beast.Wolf(new Color("#5a5650")),
        "ice_wolf" => (Body?)Creature.Create("ice_wolf.glb", 1.0f) ?? Beast.Wolf(new Color("#d8e2ea")),
        "fox" => (Body?)Creature.Create("fox.glb", 0.5f) ?? Beast.Wolf(new Color("#c06a30")),
        "deer" => (Body?)Deer.Create() ?? Beast.Wolf(new Color("#8a6a48")),
        "boar" => (Body?)Creature.Create("boar.glb", 0.95f) ?? Beast.Wolf(new Color("#4a3a2e")),
        "hawk" => (Body?)Creature.Create("hawk.glb", 0.35f) ?? Beast.Rat(),
        "bear" => (Body?)Creature.Create(GD.Randf() < 0.3f ? "bear_black.glb" : "bear.glb", 1.5f) ?? Beast.Bear(),
        "mudcrab" => Beast.Crab(),
        _ => Figure.Create(look),
    };
}

/// <summary>A KayKit adventurer or skeleton, dressed for the part and animated.</summary>
public partial class Figure : Body
{
    /// <summary>
    /// Character presets: model, what they carry and wear, size and tint. KayKit's capes are left
    /// off: they hang as one stiff slab that hides the body from behind.
    /// </summary>
    public sealed record Look(string Model, string[] Show, float Scale = 0.74f, Color? Tint = null, float Glow = 0, string? RightHand = null, string? LeftHand = null, bool Unarmed = false);

    public static readonly Dictionary<string, Look> Looks = new()
    {
        // Playable classes.
        ["Knight"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet"]),
        ["Barbarian"] = new("Barbarian", ["1H_Axe", "Barbarian_Round_Shield", "Barbarian_Hat"]),
        ["Mage"] = new("Mage", ["2H_Staff", "Mage_Hat"]),
        ["Rogue"] = new("Rogue", ["Knife", "Knife_Offhand"]),
        ["Rogue_Hooded"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand"]),
        // Enemies.
        ["dock_thug"] = new("Barbarian", ["1H_Axe"]),
        ["lantern_boy"] = new("Rogue", ["Knife"], 0.66f, new Color("#ffcf9a")),
        ["drowned_hand"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand"], 0.76f, new Color("#9ac8e8")),
        ["bandit"] = new("Rogue_Hooded", ["Knife", "1H_Crossbow"], 0.74f, new Color("#c8b08a")),
        ["bandit_chief"] = new("Barbarian", ["2H_Axe", "Barbarian_Hat"], 0.84f, new Color("#e0a090")),
        ["draugr"] = new("Skeleton_Warrior", [], 0.7f, RightHand: "Skeleton_Axe", LeftHand: "Skeleton_Shield_Small_A"),
        ["draugr_overlord"] = new("Skeleton_Warrior", [], 0.92f, new Color("#bfe4ff"), 0.15f, RightHand: "Skeleton_Blade", LeftHand: "Skeleton_Shield_Large_A"),
        ["sea_wraith"] = new("Skeleton_Mage", [], 0.74f, new Color("#7fe0d8"), 0.6f, RightHand: "Skeleton_Staff"),
        ["spriggan"] = new("Skeleton_Rogue", [], 0.78f, new Color("#a8c070"), 0.3f, RightHand: "Skeleton_Blade"),
        ["frost_troll"] = new("Barbarian", ["2H_Axe"], 1.35f, new Color("#c8d4e4")),
        // People.
        ["guard"] = new("Knight", ["1H_Sword", "Round_Shield", "Knight_Helmet"]),
        ["scholar"] = new("Mage", ["Spellbook"], Unarmed: true),
        ["priest"] = new("Mage", ["Spellbook_open", "Mage_Hat"], Unarmed: true),
        ["worker"] = new("Barbarian", [], Unarmed: true),
        ["drinker"] = new("Barbarian", ["Mug", "Barbarian_Hat"], Unarmed: true),
        ["hunter"] = new("Rogue_Hooded", ["1H_Crossbow"]),
        ["townsfolk"] = new("Rogue", [], Unarmed: true),
        ["traveller"] = new("Rogue_Hooded", [], Unarmed: true),
        ["ashborn"] = new("Rogue_Hooded", [], 0.7f, new Color("#e08070"), Unarmed: true),
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
/// Gives the KayKit characters human proportions: a smaller head, a slimmer and taller trunk, and
/// longer, leaner arms and legs, so a figure reads as a person rather than a big head on a box.
/// It runs after the animations each frame, stretching each limb bone along its length; a
/// stretched bone's children are un-stretched so a hand or foot keeps its shape, and the hips
/// are raised by however much the legs grew. Hands (and what they hold) shrink to suit.
/// </summary>
public partial class Proportions : SkeletonModifier3D
{
    const float Leg = 2.3f, Arm = 1.75f;

    /// <summary>Off draws the characters as KayKit made them (for comparing).</summary>
    public static bool Enabled { get; set; } = true;

    static readonly Dictionary<string, Vector3> Stretch = new()
    {
        ["head"] = Vector3.One * 0.5f,
        ["hips"] = new(0.8f, 1, 0.8f),
        ["spine"] = new(0.78f, 1.3f, 0.78f),
        ["chest"] = new(0.8f, 1.15f, 0.8f),
        ["upperarm.l"] = new(0.8f, Arm, 0.8f), ["lowerarm.l"] = new(0.8f, Arm, 0.8f),
        ["upperarm.r"] = new(0.8f, Arm, 0.8f), ["lowerarm.r"] = new(0.8f, Arm, 0.8f),
        ["upperleg.l"] = new(0.82f, Leg, 0.82f), ["lowerleg.l"] = new(0.82f, Leg, 0.82f),
        ["upperleg.r"] = new(0.82f, Leg, 0.82f), ["lowerleg.r"] = new(0.82f, Leg, 0.82f),
        ["hand.l"] = Vector3.One * 0.75f, ["hand.r"] = Vector3.One * 0.75f,
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

/// <summary>An animal from a model: shared flashing when hurt, keeling over when killed, and a lunge to bite.</summary>
public abstract partial class Animal : Body
{
    protected Node3D Model = null!;
    protected bool Dead;
    protected float Lunge;
    readonly List<MeshInstance3D> _meshes = [];

    /// <summary>Loads a creature's model, scaled so it stands <paramref name="height"/> metres tall and faces forward.</summary>
    /// <param name="modelHeight">The model's own height, for models whose skinning makes it unmeasurable.</param>
    /// <param name="yaw">Degrees to turn the model so its head points along Godot's forward (-Z).</param>
    protected bool Load(string file, float height, float? modelHeight = null, float yaw = 180)
    {
        if (Models.Scene($"creature:{file}")?.Instantiate() is not Node3D model) return false;
        Model = model;
        AddChild(model);
        var tall = modelHeight ?? Models.Bounds(model).Size.Y;
        if (tall > 0) model.Scale = Vector3.One * (height / tall);
        model.RotationDegrees = new Vector3(0, yaw, 0);
        _meshes.AddRange(model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>());
        return true;
    }

    /// <summary>Paints the model with another texture (a dragon's colour), shiny if metallic.</summary>
    protected void Coat(string file, bool metallic)
    {
        var texture = GD.Load<Texture2D>($"res://assets/creatures/{file}");
        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture, Roughness = metallic ? 0.38f : 0.8f, Metallic = metallic ? 0.75f : 0,
            MetallicSpecular = metallic ? 0.7f : 0.4f,
        };
        foreach (var m in _meshes) m.MaterialOverride = material;
    }

    public override void _Process(double delta)
    {
        Lunge = Mathf.MoveToward(Lunge, 0, (float)delta * 3);
        if (!Dead) Model.Position = Model.Position with { Z = -Lunge * 0.4f };
    }

    public override void Attack(bool heavy = false) => Lunge = 1;
    public override void Cast() => Lunge = 1;

    public override void Hurt()
    {
        foreach (var m in _meshes)
            if (m.MaterialOverride is StandardMaterial3D mat) { mat.EmissionEnabled = true; mat.Emission = new Color("#ff4020"); mat.EmissionEnergyMultiplier = 0.5f; }
        GetTree().CreateTimer(0.15).Timeout += () =>
        {
            foreach (var m in _meshes) if (IsInstanceValid(m) && m.MaterialOverride is StandardMaterial3D mat) mat.EmissionEnabled = false;
        };
    }

    public override void Die()
    {
        Dead = true;
        CreateTween().TweenProperty(this, "rotation:z", Mathf.Pi / 2, 0.4);
    }

    public override void Rise()
    {
        Dead = false;
        Rotation = Vector3.Zero;
    }

    public override void Mark(Color colour)
    {
        foreach (var m in _meshes)
            if (m.MaterialOverride is StandardMaterial3D mat) mat.AlbedoColor = mat.AlbedoColor.Lerp(colour, 0.35f);
            else Models.Tint(m, colour.Lerp(Colors.White, 0.35f), 0.2f);
    }
}

/// <summary>
/// A deer (CC0, Polygonal Mind). The model comes rigged but without animations, so its legs are
/// swung here, in diagonal pairs like a trot; standing still, it lowers its head to graze now and then.
/// </summary>
public partial class Deer : Animal
{
    Skeleton3D? _skeleton;
    readonly List<(int Bone, Quaternion Pose, float Phase)> _legs = [];
    int _neck = -1;
    Quaternion _neckRest;
    float _phase, _speed, _graze;
    double _grazeClock;

    public static Deer? Create()
    {
        var d = new Deer();
        if (!d.Load("deer_pm.glb", 1.55f, modelHeight: 2.73f)) return null; // its armature is scaled by 0.01, which skinning undoes
        d._skeleton = d.Model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (d._skeleton is { } s)
        {
            // Front left with back right, front right with back left.
            foreach (var (name, phase) in new[] { ("bn_l_arm_01", 0f), ("bn_r_leg_01", 0f), ("bn_r_arm_01", Mathf.Pi), ("bn_l_leg_01", Mathf.Pi) })
                if (s.FindBone(name) is var b and >= 0) d._legs.Add((b, s.GetBoneRest(b).Basis.GetRotationQuaternion(), phase));
            d._neck = s.FindBone("bn_neck");
            if (d._neck >= 0) d._neckRest = s.GetBoneRest(d._neck).Basis.GetRotationQuaternion();
        }
        return d;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_skeleton is null || Dead) return;
        var d = (float)delta;
        _phase += d * (1.5f + _speed * 1.6f);
        var swing = Mathf.Clamp(_speed / 7f, 0, 1) * 0.6f;
        foreach (var (bone, rest, phase) in _legs)
            _skeleton.SetBonePoseRotation(bone, rest * new Quaternion(Vector3.Right, Mathf.Sin(_phase + phase) * swing));
        // Grazing: when standing, now and then the head goes down for a few seconds.
        _grazeClock -= delta;
        if (_grazeClock <= 0) _grazeClock = 4 + GD.Randf() * 6;
        var wantGraze = _speed < 0.3f && _grazeClock < 3 ? 1f : 0f;
        _graze = Mathf.MoveToward(_graze, wantGraze, d * 1.5f);
        if (_neck >= 0) _skeleton.SetBonePoseRotation(_neck, _neckRest * new Quaternion(Vector3.Right, _graze * 0.9f));
        Model.Position = Model.Position with { Y = Mathf.Abs(Mathf.Sin(_phase)) * swing * 0.12f };
    }

    public override void Move(float speed, bool grounded, bool swimming) => _speed = speed;
}

/// <summary>
/// A creature from a converted model, each with its own idle, walk, run, attack and death: wolves,
/// ice wolves, foxes, bears, boars and hawks from 0 A.D. (tools/creatures/zero_ad.py), and giant
/// rats, goblins, forest monsters, trolls and a dragon from OpenGameArt (tools/creatures/oga.py).
/// </summary>
public partial class Creature : Animal
{
    AnimationPlayer? _anim;
    string _playing = "";
    double _busy;

    /// <param name="coat">A texture (under assets/creatures) to paint it with instead of its own.</param>
    /// <param name="metallic">Make the coat shine like metal.</param>
    public static Creature? Create(string file, float height, Color? tint = null, string? coat = null, bool metallic = false)
    {
        var c = new Creature();
        if (!c.Load(file, height)) return null;
        if (tint is { } t) Models.Tint(c.Model, t);
        if (coat is not null) c.Coat(coat, metallic);
        c._anim = c.Model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (c._anim is not null)
            foreach (var name in c._anim.GetAnimationList())
                c._anim.GetAnimation(name).LoopMode = name.EndsWith("Death") || name.EndsWith("Attack") ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        return c;
    }

    bool Has(string clip) => _anim?.HasAnimation(clip) == true;

    void Play(string clip, float rate = 1)
    {
        if (_anim is null || !Has(clip)) return;
        if (clip != _playing) { _anim.Play(clip, 0.2); _playing = clip; }
        _anim.SpeedScale = rate;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _busy -= delta;
        if (_wings is not null)
        {
            _wings.BeatRate = 1.4f / MathF.Sqrt(Math.Max(1, Scale.X)); // great dragons beat slowly
            _wings.Update(_flying, _speed, delta);
        }
    }

    DragonWings? _wings;
    bool _flying;
    float _speed;

    /// <summary>Gives a dragon its great wings (it can fly from now on).</summary>
    public void SpreadWings(Color membrane, Color bone, bool metallic)
    {
        _wings = DragonWings.Grow(Model, 4.5f * 1.55f / Model.Scale.X, membrane, bone, metallic);
    }

    public bool Winged => _wings is not null;

    public override void Move(float speed, bool grounded, bool swimming)
    {
        _flying = _wings is not null && !grounded && !Dead;
        _speed = speed;
        if (Dead || _busy > 0) return;
        if (_flying) { Play("Idle", 0.5f); return; } // legs tucked, the wings do the work
        if (speed > 4.5f && Has("Run")) Play("Run", Mathf.Clamp(speed / 7f, 0.6f, 1.6f));
        else if (speed > 0.3f) Play("Walk", Mathf.Clamp(speed / 2f, 0.6f, 2f));
        else Play("Idle");
    }

    public override void Attack(bool heavy = false)
    {
        if (Dead) return;
        if (!Has("Attack")) { base.Attack(heavy); return; }
        _playing = "";
        Play("Attack", 1.3f);
        _anim!.Seek(0, true);
        _busy = _anim.GetAnimation("Attack").Length / 1.3;
    }

    public override void Die()
    {
        if (!Has("Death")) { base.Die(); return; }
        Dead = true;
        _playing = "";
        Play("Death");
    }

    public override void Rise()
    {
        Dead = false;
        _playing = "";
        Rotation = Vector3.Zero;
    }
}
