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
        holder.SetMeta("prop", path); // for the prop audit (--audit)
        holder.Position = at;
        holder.Rotation = new Vector3(0, yaw, 0);
        parent.AddChild(holder);
        if (Scene(path)?.Instantiate() is not Node3D model) return holder;
        model.Scale = Vector3.One * scale;
        holder.AddChild(model);
        // Some models are built around their middle, not their foot: stand those on the ground, not half in it.
        // Rocks and crags are meant to be sunk in, and banners, wall torches, arches and pillars are meant to
        // meet a wall or a roof, so those stay exactly where they're put.
        var placedByHand = FixedInPlace.Any(path.Contains);
        if (!placedByHand && Bounds(model) is { Size.Y: > 0.05f } own && own.Position.Y < -own.Size.Y * 0.05f)
            model.Position += Vector3.Up * -own.Position.Y;
        if (placedByHand) { }
        else if (holder.IsInsideTree()) Settle(holder); // placed into a live world: it was ready before it had a model
        else holder.Ready += () => Settle(holder);
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

    static readonly string[] FixedInPlace = ["nature/", "rock", "mountain", "banner", "mounted", "arch", "pillar"];

    /// <summary>
    /// Once a prop is in the world (and the world's walls are solid), makes sure it isn't standing in one: if it's
    /// inside a building or wall, it steps out the way the wall faces, a little at a time; then it drops onto whatever is
    /// under it, so nothing hangs in the air.
    /// </summary>
    static async void Settle(Node3D prop)
    {
        var tree = prop.GetTree();
        await prop.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        await prop.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        if (!GodotObject.IsInstanceValid(prop) || !prop.IsInsideTree()) return;
        var space = prop.GetWorld3D().DirectSpaceState;
        var mine = prop is CollisionObject3D body ? new Godot.Collections.Array<Rid> { body.GetRid() } : [];
        for (var step = 0; step < 16; step++)
        {
            if (WorldBox(prop) is not { } box) return;
            var probe = new PhysicsShapeQueryParameters3D
            {
                Shape = new BoxShape3D { Size = box.Size * 0.5f + Vector3.One * 0.01f },
                Transform = new Transform3D(Basis.Identity, box.GetCenter() + Vector3.Up * box.Size.Y * 0.1f), Exclude = mine,
            };
            var wall = space.IntersectShape(probe, 8).Select(h => h["collider"].AsGodotObject()).OfType<Node3D>()
                .FirstOrDefault(o => o is StaticBody3D && !o.HasMeta("prop") && !o.HasMeta("ground"));
            if (wall is null) break;
            var rest = space.GetRestInfo(probe);
            var away = rest.Count > 0 ? (Vector3)rest["normal"] : prop.GlobalPosition - wall.GlobalPosition;
            away.Y = 0;
            if (away.LengthSquared() < 0.01f) away = prop.GlobalPosition - wall.GlobalPosition with { Y = prop.GlobalPosition.Y };
            away.Y = 0;
            if (away.LengthSquared() < 0.01f) away = Vector3.Right;
            prop.GlobalPosition += away.Normalized() * 0.75f;
        }
        // Onto whatever is really under its middle: down if it's hanging, up if the ground there is higher than the
        // map said (a slope, a pier deck). Never more than a few metres: it's a nudge, not a fall.
        if (WorldBox(prop) is not { } placed) return;
        var c = placed.GetCenter();
        var hit = space.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = new Vector3(c.X, placed.Position.Y + Mathf.Min(placed.Size.Y * 0.6f, 2.5f), c.Z), To = new Vector3(c.X, placed.Position.Y - 3, c.Z), Exclude = mine,
        });
        if (hit.Count > 0 && ((Vector3)hit["position"]).Y - placed.Position.Y is var shift and (> 0.08f or < -0.08f))
            prop.GlobalPosition += Vector3.Up * shift;
    }

    static Aabb? WorldBox(Node3D prop)
    {
        Aabb? box = null;
        foreach (var m in prop.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.IsVisibleInTree()))
        {
            var b = m.GlobalTransform * m.GetAabb();
            box = box is { } x ? x.Merge(b) : b;
        }
        return box is { Size.Y: > 0.05f } ? box : null;
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

    /// <summary>Plays one of its moves by name (a kick, a crossbow shot, a spell raised high), if it has it.</summary>
    public virtual void Perform(string clip, double seconds = 0.7, float speed = 1.3f) => Attack();

    /// <summary>Sits down (in a saddle) or gets up.</summary>
    public virtual void Seat(bool seated) { }

    /// <summary>A red glow for a deviant.</summary>
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
    /// (frost) dragon spread great wings and fly; the frost dragon keeps its small wings and stays on the ground,
    /// with a ridge of ice spikes grown down its back.
    /// </summary>
    static Body Dragon(string colour, bool corrupt = false)
    {
        var metallic = colour is "gold" or "silver" or "bronze" or "copper" or "brass";
        var coat = corrupt ? $"dragons/{colour}_corrupt.png" : $"dragons/{colour}.png";
        if (Creature.Create("dragon.glb", 4.5f, coat: coat, metallic: metallic && !corrupt, glow: corrupt ? $"dragons/{colour}_corrupt_glow.png" : null) is not { } dragon) return Beast.Bear();
        if (Wings.TryGetValue(colour, out var wings))
        {
            // Its own wings bled violet, the bones turned to the Rift's gold.
            if (corrupt) dragon.SpreadWings(wings.Membrane.Lerp(new Color("#4a1a7e"), 0.75f), new Color("#d8b060"), false, glow: new Color("#8a3ae0"));
            else dragon.SpreadWings(wings.Membrane, wings.Bone, metallic);
        }
        else dragon.Freeze(corrupt);
        if (corrupt) dragon.Corrupt();
        return dragon;
    }

    static Body Burned(string look, Action<Figure> burn)
    {
        var f = Figure.Create(look);
        burn(f);
        return f;
    }

    /// <summary>A dragon knight on a dragon.</summary>
    static Body Ridden(string dragonLook)
    {
        var body = For(dragonLook);
        if (body is Creature c) c.Mount(Figure.Create("dragon_knight"));
        return body;
    }

    public static Body For(string look) => look switch
    {
        "giant_rat" => (Body?)Creature.Create("giant_rat.glb", 0.6f) ?? Beast.Rat(),
        "spriggan" => (Body?)Creature.Create("forest_monster.glb", 2.3f) ?? Figure.Create(look),
        "frost_troll" => (Body?)Wildfolk.Troll(2.4f, new Color("#9ec0e4")) ?? Figure.Create(look),
        "goblin" => (Body?)Creature.Create("goblin.glb", 1.3f) ?? Figure.Create(look),
        "dragon" => Dragon("white"),
        "cinder_warden" => Burned(look, Cinder.Warden),
        "cinder_thrall" => Burned(look, Cinder.Thrall),
        _ when look.StartsWith("rider:") => Ridden(look[6..]),
        _ when look.StartsWith("dragon_") && look.EndsWith("_corrupt") => Dragon(look[7..^8], corrupt: true),
        _ when look.StartsWith("dragon_") => Dragon(look[7..]),
        "wolf" => (Body?)Creature.Create("wolf.glb", 0.95f) ?? Beast.Wolf(new Color("#5a5650")),
        "ice_wolf" => (Body?)Creature.Create("ice_wolf.glb", 1.0f) ?? Beast.Wolf(new Color("#d8e2ea")),
        "fox" => (Body?)Creature.Create("fox.glb", 0.5f) ?? Beast.Wolf(new Color("#c06a30")),
        "deer" => (Body?)Deer.Create() ?? Beast.Wolf(new Color("#8a6a48")),
        "boar" => (Body?)Creature.Create("boar.glb", 0.95f) ?? Beast.Wolf(new Color("#4a3a2e")),
        "hawk" => (Body?)Creature.Create("hawk.glb", 0.35f) ?? Beast.Rat(),
        "bear" => (Body?)Creature.Create(GD.Randf() < 0.3f ? "bear_black.glb" : "bear.glb", 1.5f) ?? Beast.Bear(),
        "mud_crab" => Beast.Crab(),
        _ when Lairfolk.For(look) is { } lairfolk => lairfolk,
        _ when Wildfolk.For(look) is { } wildfolk => wildfolk,
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
        // Playable classes: three warriors, three rogues, three mages, three bards.
        ["MartialArtist"] = new("Rogue", [], 0.74f, new Color("#f2c27a"), Unarmed: true),
        ["Barbarian"] = new("Barbarian", ["2H_Axe", "Barbarian_Hat"]),
        ["Paladin"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet"], 0.75f, new Color("#fff0d0"), 0.06f),
        ["Ranger"] = new("Rogue_Hooded", ["2H_Crossbow"], 0.74f, new Color("#b8d4a0")),
        ["Thief"] = new("Rogue", ["Knife", "Knife_Offhand"]),
        ["Assassin"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand"], 0.74f, new Color("#8c88aa")),
        ["Wizard"] = new("Mage", ["2H_Staff", "Mage_Hat"]),
        ["Warlock"] = new("Mage", ["1H_Wand", "Spellbook_open"], 0.74f, new Color("#b49ad8")),
        ["Sorcerer"] = new("Mage", [], 0.74f, new Color("#f2a888")),
        ["Poet"] = new("Mage", ["Spellbook_open"], 0.74f, new Color("#f4dca0"), LeftHand: "@laurel"),
        ["Scribe"] = new("Mage", ["Spellbook"], 0.74f, new Color("#c8c0e0"), RightHand: "@quill"),
        ["Musician"] = new("Rogue", [], 0.74f, new Color("#f0c890"), LeftHand: "@lute", Unarmed: true),
        // Characters made before there were nine classes.
        ["Knight"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet"]),
        ["Mage"] = new("Mage", ["2H_Staff", "Mage_Hat"]),
        ["Rogue"] = new("Rogue", ["Knife", "Knife_Offhand"]),
        ["Rogue_Hooded"] = new("Rogue_Hooded", ["Knife", "Knife_Offhand"]),
        // Dragon knights, the guild masters, and the young who follow a hero.
        ["dragon_knight"] = new("Knight", ["2H_Sword", "Knight_Helmet"], 0.74f, new Color("#e8dcb0"), 0.04f),
        ["aldric"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet"], 0.78f, new Color("#ffd890"), 0.12f),
        ["warmaster"] = new("Barbarian", ["2H_Axe"], 0.8f),
        ["velvet"] = new("Rogue_Hooded", ["Knife"], 0.74f, new Color("#c07a98")),
        ["youth"] = new("Rogue", [], 0.6f, Unarmed: true),
        ["squire"] = new("Knight", ["1H_Sword", "Round_Shield"], 0.62f),
        ["apprentice"] = new("Mage", ["1H_Wand"], 0.6f),
        ["protege"] = new("Rogue", ["Knife"], 0.6f),
        ["understudy"] = new("Rogue", [], 0.6f, new Color("#f0d8a0"), LeftHand: "@lute", Unarmed: true),
        ["laureate"] = new("Mage", ["Spellbook_open"], 0.76f, new Color("#ffe4a8"), 0.05f, LeftHand: "@laurel", Unarmed: true),
        ["familiar"] = new("Skeleton_Minion", [], 0.62f, new Color("#b080ff"), 0.7f),
        // The Ashen Crypt.
        ["cinder_warden"] = new("Skeleton_Warrior", [], 1.3f, new Color("#4a3a30")),
        ["cinder_thrall"] = new("Skeleton_Minion", [], 0.7f, new Color("#5a4636")),
        // The wild lairs' bosses and their own that are figures (the rest are creatures: see Lairfolk).
        ["ghost_captain"] = new("Knight", ["1H_Sword", "Badge_Shield", "Knight_Helmet"], 0.95f),
        ["hollow_soldier"] = new("Knight", ["1H_Sword", "Round_Shield", "Knight_Helmet"], 0.74f),
        ["forge_golem"] = new("Barbarian", ["2H_Axe"], 1.45f),
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

    /// <param name="avatar">A hero's own look (head, body, arms and legs from any figure; a hat; colours; height),
    /// worn instead of the class's. The class still decides what they carry.</param>
    public static Figure Create(string look, Tristinwold.Gameplay.Avatar? avatar = null)
    {
        var l = Looks.GetValueOrDefault(look) ?? (look.StartsWith('#') ? Looks["Knight"] : Looks["townsfolk"]);
        if (avatar is not null)
            l = l with { Scale = avatar.Scale, Tint = null, Glow = 0, Show = [.. l.Show.Where(x => !x.Contains("Hat") && !x.Contains("Helmet"))] };
        var f = new Figure { _look = l };
        if (Models.Scene($"characters/{l.Model}.glb")?.Instantiate() is Node3D model)
        {
            model.Scale = Vector3.One * l.Scale;
            if (avatar is not null) model.Scale = new Vector3(l.Scale * avatar.Width, l.Scale, l.Scale * avatar.Width); // a broad dwarf, a slim elf
            model.RotationDegrees = new Vector3(0, 180, 0); // KayKit faces +Z; Godot's forward is -Z
            f.AddChild(model);
            foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                if (Equipment.Any(e => m.Name.ToString().Contains(e))) m.Visible = l.Show.Contains(m.Name.ToString());
            if (l.Tint is { } tint) Models.Tint(model, tint, l.Glow);
            if (avatar is not null && model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is { } frame)
                Wear(frame, avatar);
            Shade(model);
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

    // ---- shading -------------------------------------------------------------------------------

    static readonly Dictionary<Material, Material> Shaded = [];

    /// <summary>
    /// Takes the plastic off: KayKit's colours with a soft rim of light round the edges (so a figure stands out
    /// against what's behind it, as people do in daylight) and a matte, cloth-and-skin surface instead of a sheen.
    /// Each material is done once and shared.
    /// </summary>
    static void Shade(Node3D model)
    {
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (var i = 0; i < m.GetSurfaceOverrideMaterialCount(); i++)
            {
                if (m.GetActiveMaterial(i) is not BaseMaterial3D original) continue;
                if (!Shaded.TryGetValue(original, out var done))
                {
                    var mat = (BaseMaterial3D)original.Duplicate();
                    mat.RimEnabled = true;
                    mat.Rim = 0.3f;
                    mat.RimTint = 0.65f;
                    mat.Roughness = Mathf.Max(mat.Roughness, 0.7f);
                    mat.MetallicSpecular = 0.35f;
                    Shaded[original] = Shaded[mat] = done = mat;
                }
                m.SetSurfaceOverrideMaterial(i, done);
            }
    }

    // ---- a hero's own look ---------------------------------------------------------------------

    /// <summary>The parts of a figure's body, by the end of each mesh's name. Skeletons have eyes and a jaw with
    /// their head (and the mage a skull for one), and some a cloak with their body.</summary>
    static readonly Dictionary<string, string[]> Parts = new()
    {
        ["head"] = ["_Head", "_Head_Hooded", "_Skull", "_Eyes", "_Jaw"],
        ["body"] = ["_Body", "_Cloak"],
        ["arms"] = ["_ArmLeft", "_ArmRight"],
        ["legs"] = ["_LegLeft", "_LegRight"],
    };

    static readonly Dictionary<string, Node3D?> Sources = [];

    static Node3D? Source(string figure)
    {
        if (!Sources.TryGetValue(figure, out var source))
            Sources[figure] = source = Models.Scene($"characters/{figure}.glb")?.Instantiate() as Node3D;
        return source;
    }

    /// <summary>
    /// Puts a hero's own look on a figure: its body parts are swapped for the ones picked (every KayKit figure
    /// shares one skeleton, so a mage's head sits on a knight's body and moves with it), its hat for the one picked,
    /// and the colours painted in.
    /// </summary>
    static void Wear(Skeleton3D skeleton, Tristinwold.Gameplay.Avatar a)
    {
        foreach (var m in skeleton.GetChildren().OfType<MeshInstance3D>().Where(m => m.Skin is not null).ToList())
        {
            skeleton.RemoveChild(m);
            m.QueueFree();
        }
        var outfit = new Color(a.OutfitHex);
        var skin = new Color(a.SkinHex);
        if (a.Blood is { } blood) skin = skin.Lerp(new Color(blood.Hex), a.PureDragon ? 0.8f : 0.45f); // dragon blood shows in the skin as scales
        foreach (var (part, figure) in new[] { ("head", a.Head), ("body", a.Body), ("arms", a.Arms), ("legs", a.Legs) })
        {
            if (Source(figure) is not { } source) continue;
            foreach (var original in source.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                         .Where(m => m.Skin is not null && Parts[part].Any(end => m.Name.ToString().EndsWith(end))))
            {
                var copy = new MeshInstance3D { Name = original.Name, Mesh = original.Mesh, Skin = original.Skin, Transform = original.Transform };
                skeleton.AddChild(copy);
                copy.Skeleton = copy.GetPathTo(skeleton);
                var colour = part == "head" ? skin : outfit;
                if (colour != Colors.White && !original.Name.ToString().EndsWith("_Eyes")) Paint(copy, colour);
            }
        }
        foreach (var m in skeleton.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                     .Where(m => m.Skin is null && (m.Name.ToString().Contains("Hat") || m.Name.ToString().Contains("Helmet") || m.Name.ToString().Contains("_Hood"))))
            m.Visible = false;
        if (a.HatFigure.Length > 0 && Source(a.HatFigure)?.FindChild(a.Hat, true, false) is MeshInstance3D hat)
        {
            var slot = skeleton.GetChildren().OfType<BoneAttachment3D>().FirstOrDefault(b => b.BoneName == "head")
                       ?? new BoneAttachment3D { BoneName = "head" };
            if (slot.GetParent() is null) skeleton.AddChild(slot);
            slot.AddChild(new MeshInstance3D { Name = "Avatar_Hat", Mesh = hat.Mesh, Transform = hat.Transform });
        }
        Adorn(skeleton, a, skin);
    }

    // ---- what their people and their blood add: ears, tusks, beards, horns, a tail ---------------------------

    static StandardMaterial3D Matte(Color c, float rough = 0.8f) => new() { AlbedoColor = c, Roughness = rough, RimEnabled = true, Rim = 0.3f, RimTint = 0.65f };

    /// <summary>A cone along a direction: an ear, a tusk, a horn.</summary>
    static MeshInstance3D Cone(Vector3 at, Vector3 along, float length, float radius, Material m, float flat = 1)
    {
        var d = along.Normalized();
        var basis = new Basis(new Quaternion(Vector3.Up, d)) * Basis.FromScale(new Vector3(1, 1, flat));
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 0.06f, BottomRadius = radius, Height = length, RadialSegments = 10, Rings = 1 },
            Transform = new Transform3D(basis, at + d * length / 2), MaterialOverride = m,
        };
    }

    /// <summary>
    /// What a hero's people and blood add to their figure, sized from their own head and body: an elf's long ears,
    /// a goblin's great flaps, an orc's tusks; a beard; horns; and for dragonkind a tail. Everything is placed in the
    /// skeleton's resting space and hung from the head or hips, so it nods and sways with them.
    /// </summary>
    static void Adorn(Skeleton3D skeleton, Tristinwold.Gameplay.Avatar a, Color skin)
    {
        Aabb? Box(string part) => skeleton.GetChildren().OfType<MeshInstance3D>()
            .Where(m => m.Skin is not null && Parts[part].Any(end => m.Name.ToString().EndsWith(end)) && !m.Name.ToString().EndsWith("_Eyes"))
            .Select(m => m.Transform * m.GetAabb()).Aggregate((Aabb?)null, (x, b) => x is { } y ? y.Merge(b) : b);
        Node3D? Hang(string bone)
        {
            var i = skeleton.FindBone(bone);
            if (i < 0) return null;
            var slot = skeleton.GetChildren().OfType<BoneAttachment3D>().FirstOrDefault(b => b.BoneName == bone) ?? new BoneAttachment3D { BoneName = bone };
            if (slot.GetParent() is null) skeleton.AddChild(slot);
            var space = new Node3D { Name = $"Adorn_{bone}", Transform = skeleton.GetBoneGlobalRest(i).AffineInverse() };
            slot.AddChild(space);
            return space;
        }
        var race = a.People;
        var blood = a.Blood;
        var flesh = Matte(new Color("#eab890") * skin);
        if (Box("head") is { } head && Hang("head") is { } h)
        {
            var (c, sz) = (head.GetCenter(), head.Size);
            var front = c.Z + sz.Z * 0.5f;
            // Ears, out from the sides of the head.
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = new Vector3(c.X + side * sz.X * 0.44f, c.Y + sz.Y * 0.02f, c.Z - sz.Z * 0.04f);
                switch (race.Ears)
                {
                    case "elf": h.AddChild(Cone(ear, new Vector3(side, 0.75f, -0.35f), sz.X * 0.36f, sz.X * 0.07f, flesh, 0.45f)); break;
                    case "goblin": h.AddChild(Cone(ear, new Vector3(side, 0.3f, -0.15f), sz.X * 0.5f, sz.X * 0.15f, flesh, 0.35f)); break;
                    case "orc": h.AddChild(Cone(ear, new Vector3(side, 0.55f, -0.3f), sz.X * 0.2f, sz.X * 0.07f, flesh, 0.5f)); break;
                }
                if (race.Tusks)
                    h.AddChild(Cone(new Vector3(c.X + side * sz.X * 0.13f, c.Y - sz.Y * 0.2f, front - sz.Z * 0.06f), new Vector3(side * 0.15f, 1, 0.25f),
                        sz.Y * 0.16f, sz.X * 0.035f, Matte(new Color("#f2ead2"), 0.4f)));
            }
            // A beard, from the chin down.
            var hair = Matte(new Color(a.HairHex), 0.95f);
            var chin = new Vector3(c.X, c.Y - sz.Y * 0.28f, front - sz.Z * 0.1f);
            switch (a.Beard)
            {
                case "Stubble":
                    h.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = sz.X * 0.3f, Height = sz.Y * 0.3f }, MaterialOverride = Matte(new Color(a.HairHex) with { A = 0.55f }), Position = chin + new Vector3(0, sz.Y * 0.05f, -sz.Z * 0.06f), Scale = new Vector3(1, 0.7f, 0.55f) });
                    break;
                case "Short":
                    h.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = sz.X * 0.2f, Height = sz.Y * 0.42f }, MaterialOverride = hair, Position = chin, Scale = new Vector3(1.25f, 1, 0.6f) });
                    break;
                case "Long":
                    h.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = sz.X * 0.21f, Height = sz.Y * 0.85f }, MaterialOverride = hair, Position = chin - new Vector3(0, sz.Y * 0.2f, 0), Scale = new Vector3(1.2f, 1, 0.55f) });
                    break;
                case "Forked":
                    h.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = sz.X * 0.2f, Height = sz.Y * 0.4f }, MaterialOverride = hair, Position = chin, Scale = new Vector3(1.2f, 1, 0.55f) });
                    foreach (var side in new[] { -1f, 1f })
                        h.AddChild(Cone(chin + new Vector3(side * sz.X * 0.08f, -sz.Y * 0.1f, 0), new Vector3(side * 0.25f, -1, 0.1f), sz.Y * 0.45f, sz.X * 0.09f, hair));
                    break;
                case "Braided":
                    h.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = sz.X * 0.2f, Height = sz.Y * 0.4f }, MaterialOverride = hair, Position = chin, Scale = new Vector3(1.2f, 1, 0.55f) });
                    for (var i = 0; i < 4; i++)
                        h.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = sz.X * (0.1f - i * 0.012f), Height = sz.X * (0.2f - i * 0.024f) }, MaterialOverride = hair, Position = chin - new Vector3(0, sz.Y * (0.24f + i * 0.13f), -sz.Z * 0.02f) });
                    break;
            }
            // Horns, from the top of the head: always on dragonkind (swept, if none were picked).
            var horns = a.Horns == "none" && blood is not null ? "Swept" : a.Horns;
            var horn = Matte(blood is { } b ? new Color(b.Hex).Darkened(0.45f) : new Color("#d8ccb0"), 0.5f);
            foreach (var side in new[] { -1f, 1f })
            {
                var root = new Vector3(c.X + side * sz.X * 0.24f, c.Y + sz.Y * 0.36f, c.Z + sz.Z * 0.08f);
                switch (horns)
                {
                    case "Nubs": h.AddChild(Cone(root, new Vector3(side * 0.3f, 1, 0.1f), sz.Y * 0.14f, sz.X * 0.06f, horn)); break;
                    case "Swept": h.AddChild(Cone(root, new Vector3(side * 0.3f, 0.55f, -1), sz.Y * 0.55f, sz.X * 0.07f, horn)); break;
                    case "Long": h.AddChild(Cone(root, new Vector3(side * 0.25f, 1, -0.35f), sz.Y * 0.65f, sz.X * 0.07f, horn)); break;
                    case "Ram":
                        // Three pieces curling back, down and forward round the ear.
                        var at = root + new Vector3(side * sz.X * 0.04f, 0, 0);
                        var dirs = new[] { new Vector3(side * 0.6f, 0.5f, -0.6f), new Vector3(side * 0.5f, -0.6f, -0.5f), new Vector3(side * 0.2f, -0.6f, 0.7f) };
                        for (var i = 0; i < dirs.Length; i++)
                        {
                            var len = sz.Y * (0.24f - i * 0.04f);
                            var piece = new MeshInstance3D
                            {
                                Mesh = new CylinderMesh { TopRadius = sz.X * (0.065f - i * 0.018f), BottomRadius = sz.X * (0.08f - i * 0.018f), Height = len, RadialSegments = 10 },
                                Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, dirs[i].Normalized())), at + dirs[i].Normalized() * len / 2), MaterialOverride = horn,
                            };
                            h.AddChild(piece);
                            at += dirs[i].Normalized() * len * 0.92f;
                        }
                        h.AddChild(Cone(at, dirs[^1], sz.Y * 0.12f, sz.X * 0.03f, horn));
                        break;
                }
            }
        }
        // A dragonkind's tail, from the small of the back, down and out behind, tapering.
        if (blood is not null && Box("body") is { } body && skeleton.FindBone("hips") is var hips and >= 0 && Hang("hips") is { } low)
        {
            var scales = Matte(new Color(blood.Hex), 0.55f);
            var at = skeleton.GetBoneGlobalRest(hips).Origin + new Vector3(0, -body.Size.Y * 0.05f, -body.Size.Z * 0.42f);
            var dir = new Vector3(0, -0.55f, -1).Normalized();
            for (var i = 0; i < 6; i++)
            {
                var len = body.Size.Y * 0.22f;
                var r0 = body.Size.X * (0.16f - i * 0.024f);
                low.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = Mathf.Max(0.005f, r0 - body.Size.X * 0.024f), BottomRadius = r0, Height = len, RadialSegments = 10 },
                    Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, dir)), at + dir * len / 2), MaterialOverride = scales,
                });
                at += dir * len * 0.95f;
                dir = (dir + new Vector3(0, 0.22f, 0)).Normalized(); // curling up towards the tip
            }
        }
    }

    static void Paint(MeshInstance3D m, Color tint)
    {
        for (var s = 0; s < m.Mesh.GetSurfaceCount(); s++)
            if (m.Mesh.SurfaceGetMaterial(s)?.Duplicate() is BaseMaterial3D mat)
            {
                mat.AlbedoColor = tint;
                m.SetSurfaceOverrideMaterial(s, mat);
            }
    }

    static void Hold(Skeleton3D skeleton, string bone, string? item)
    {
        if (item is not null && item.StartsWith('@')) { _ = Craft(skeleton, bone, item[1..]); return; }
        if (item is null || skeleton.FindBone(bone) < 0 || Models.Scene($"weapons/{item}.gltf")?.Instantiate() is not Node3D weapon) return;
        var slot = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(slot);
        slot.AddChild(weapon);
    }

    /// <summary>
    /// What KayKit doesn't make, built from simple shapes: a musician's lute (held by the neck), a scribe's quill, and a
    /// poet's laurel (worn round the head, whatever <paramref name="bone"/> says). Sizes are in the hand slot's units,
    /// where a KayKit blade is about one and a half long.
    /// </summary>
    static Node3D? Craft(Skeleton3D skeleton, string bone, string item)
    {
        var thing = new Node3D { Name = $"Craft_{item}" };
        StandardMaterial3D Paint(string hex, float rough = 0.65f) => new() { AlbedoColor = new Color(hex), Roughness = rough, RimEnabled = true, Rim = 0.25f };
        void Part(Mesh mesh, Material paint, Vector3 at, Vector3 turn = default, Vector3? scale = null) =>
            thing.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = paint, Position = at, RotationDegrees = turn, Scale = scale ?? Vector3.One });
        switch (item)
        {
            case "lute":
            {
                var wood = Paint("#b0703a");
                var dark = Paint("#4a2c16");
                Part(new SphereMesh { Radius = 0.3f, Height = 0.6f }, wood, new Vector3(0, -0.5f, 0), scale: new Vector3(1, 1.2f, 0.55f)); // the bowl
                Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.02f }, dark, new Vector3(0, -0.42f, 0.16f), new Vector3(90, 0, 0)); // the rose
                Part(new BoxMesh { Size = new Vector3(0.09f, 0.62f, 0.06f) }, dark, new Vector3(0, 0, 0.06f)); // the neck
                Part(new BoxMesh { Size = new Vector3(0.11f, 0.18f, 0.05f) }, wood, new Vector3(0, 0.36f, 0.02f), new Vector3(-35, 0, 0)); // the pegbox
                for (var i = -1; i <= 1; i++) // strings, catching the light
                    Part(new BoxMesh { Size = new Vector3(0.006f, 0.95f, 0.006f) }, Paint("#f4ecd8", 0.3f), new Vector3(i * 0.025f, -0.2f, 0.1f));
                break;
            }
            case "rod":
                // Held near the butt, the rod reaching up and out ahead; a reel by the hand.
                Part(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.03f, Height = 2.4f }, Paint("#8a6a3e"), new Vector3(0, 1.0f, 0.35f), new Vector3(16, 0, 0));
                Part(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.05f }, Paint("#4a3a2a"), new Vector3(0.06f, 0.05f, 0.05f), new Vector3(0, 0, 90));
                break;
            case "pick":
                Part(new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 1.1f }, Paint("#8a6a3e"), new Vector3(0, 0.35f, 0));
                Part(new PrismMesh { Size = new Vector3(0.9f, 0.12f, 0.08f) }, Paint("#7a7e86", 0.4f), new Vector3(0, 0.9f, 0), new Vector3(0, 0, 0), new Vector3(1, 1, 1));
                break;
            case "axe":
                Part(new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 1.1f }, Paint("#8a6a3e"), new Vector3(0, 0.35f, 0));
                Part(new BoxMesh { Size = new Vector3(0.36f, 0.26f, 0.04f) }, Paint("#8a8e96", 0.4f), new Vector3(0.18f, 0.82f, 0));
                break;
            case "quill":
                Part(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.006f, Height = 0.5f }, Paint("#e8e0d0"), new Vector3(0, 0.15f, 0));
                Part(new SphereMesh { Radius = 0.08f, Height = 0.4f }, Paint("#f6f2ea", 0.9f), new Vector3(0.03f, 0.28f, 0), new Vector3(0, 0, -10), new Vector3(0.6f, 1, 0.12f)); // the vane
                Part(new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.001f, Height = 0.06f }, Paint("#1a1a2a", 0.3f), new Vector3(0, -0.13f, 0)); // the inked nib
                break;
            case "laurel":
            {
                // Round the head, from the head's own size, like the beards and horns.
                bone = "head";
                var head = skeleton.GetChildren().OfType<MeshInstance3D>().Where(m => m.Skin is not null && Parts["head"].Any(end => m.Name.ToString().EndsWith(end)) && !m.Name.ToString().EndsWith("_Eyes"))
                    .Select(m => m.Transform * m.GetAabb()).Aggregate((Aabb?)null, (x, b) => x is { } y ? y.Merge(b) : b);
                var i = skeleton.FindBone(bone);
                if (head is not { } box || i < 0) return null;
                thing.Transform = skeleton.GetBoneGlobalRest(i).AffineInverse();
                var (c, sz) = (box.GetCenter(), box.Size);
                var leaf = Paint("#6f9a34", 0.8f);
                var ring = c + new Vector3(0, sz.Y * 0.12f, 0);
                var r = sz.X * 0.52f;
                for (var k = 0; k < 18; k++)
                {
                    var t = k / 18f * Mathf.Tau;
                    var at = ring + new Vector3(Mathf.Cos(t) * r, 0, Mathf.Sin(t) * r * (sz.Z / sz.X));
                    Part(new SphereMesh { Radius = sz.X * 0.07f, Height = sz.X * 0.14f }, leaf, at, new Vector3(0, -Mathf.RadToDeg(t), 35), new Vector3(1.6f, 0.5f, 0.7f));
                }
                break;
            }
            default: return null;
        }
        if (skeleton.FindBone(bone) < 0) return null;
        var slot = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(slot);
        slot.AddChild(thing);
        return slot;
    }

    /// <summary>Puts something made from shapes (a fishing rod) in the right hand for a while; free the node to put it away.</summary>
    public Node3D? Carry(string item) =>
        FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is { } skeleton ? Craft(skeleton, "handslot.r", item) : null;

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

    bool _seated;

    public override void Seat(bool seated)
    {
        _seated = seated;
        // In the saddle the legs are tucked out of sight, and the rider sits a little down into the dragon's back.
        foreach (var m in FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (m.Name.ToString().Contains("_Leg")) m.Visible = !seated;
        _busy = 0;
        _loop = "";
        Play(seated ? "Sit_Chair_Idle" : "Idle", 0.2f);
        if (_anim is not null) _anim.SpeedScale = 1;
    }

    public override void Perform(string clip, double seconds = 0.7, float speed = 1.3f)
    {
        if (_anim is not null) _anim.SpeedScale = 1;
        if (_anim?.HasAnimation(clip) != true) { Attack(); return; }
        Once(clip, seconds, speed);
    }

    double _airborneSince = -1;

    public override void Move(float speed, bool grounded, bool swimming)
    {
        // How long it's been off the ground: a step down a stair isn't a jump; a real fall lands with a bend of the knees.
        var now = Time.GetTicksMsec() / 1000.0;
        if (!grounded && !swimming) { if (_airborneSince < 0) _airborneSince = now; }
        else if (_airborneSince >= 0)
        {
            var flew = now - _airborneSince;
            _airborneSince = -1;
            if (flew > 0.45 && !_dead && !_seated && _anim?.HasAnimation("Jump_Land") == true) { Once("Jump_Land", 0.28, 1.3f); return; }
        }
        if (_dead || _busy > 0 || _seated) return;
        var idle = _look.Unarmed ? "Unarmed_Idle" : "Idle";
        var inAir = !grounded && _airborneSince >= 0 && now - _airborneSince > 0.18;
        var (name, rate) = swimming ? ("Walking_C", 0.8f)
            : inAir ? ("Jump_Idle", 1f)
            : speed > 6.5f ? ("Running_A", speed / 7.5f)
            : speed > 0.4f ? ("Walking_A", Mathf.Max(0.6f, speed / 3.8f))
            : (idle, 1f);
        if (name == _loop) { if (_anim is not null) _anim.SpeedScale = rate; return; }
        _loop = name;
        Play(name, 0.28f); // a soft blend from one gait into the next
        if (_anim is not null) _anim.SpeedScale = rate;
    }

    /// <summary>How the weapon it's been given is held (see <see cref="Wardrobe"/>); null for its own.</summary>
    public Tristinwold.Gameplay.Grip? Held { get; set; }

    public override void Attack(bool heavy = false)
    {
        if (_anim is not null) _anim.SpeedScale = 1;
        var twoHanded = Held is { } held ? held == Tristinwold.Gameplay.Grip.TwoHand
            : _look.Model.StartsWith("Skeleton") || _look.Show.Contains("2H_Axe") || _look.Show.Contains("2H_Sword");
        string[] swings = twoHanded
            ? ["2H_Melee_Attack_Chop", "2H_Melee_Attack_Slice"]
            : _look.Unarmed && Held is null ? ["Unarmed_Melee_Attack_Punch_A", "Unarmed_Melee_Attack_Punch_B"]
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
        if (!_seated) Once("Lie_StandUp", 1.2);
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
    int[] _arms = [];
    const float ArmDrop = 0.75f;
    float _lift;

    void Measure(Skeleton3D s)
    {
        _scale = new Vector3[s.GetBoneCount()];
        for (var i = 0; i < _scale.Length; i++) _scale[i] = Stretch.GetValueOrDefault(s.GetBoneName(i), Vector3.One);
        _hips = s.FindBone("hips");
        _arms = [.. new[] { s.FindBone("upperarm.l"), s.FindBone("upperarm.r") }.Where(b => b >= 0)];
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
        // KayKit holds its arms out wide, to clear the chunky body; a slim body lets them hang closer. Only the
        // sideways part is taken in, so a sword held out in front stays where it is.
        foreach (var arm in _arms)
        {
            var parent = s.GetBoneParent(arm);
            if (parent < 0) continue;
            var global = s.GetBoneGlobalPose(arm);
            var along = global.Basis.Y.Normalized();
            var sideways = Mathf.Abs(along.X);
            var axis = along.Cross(Vector3.Down);
            if (sideways < 0.2f || axis.LengthSquared() < 1e-4f) continue;
            var turned = new Basis(axis.Normalized(), ArmDrop * (sideways - 0.2f) / 0.8f) * global.Basis;
            s.SetBonePose(arm, new Transform3D(s.GetBoneGlobalPose(parent).Basis.Inverse() * turned, s.GetBonePose(arm).Origin));
        }
    }
}

/// <summary>A four-legged animal built from shapes, with a trotting gait: giant rats, wolves, bears, mud crabs.</summary>
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
        var box = Models.Bounds(model);
        var tall = modelHeight ?? box.Size.Y;
        if (tall > 0) model.Scale = Vector3.One * (height / tall);
        model.RotationDegrees = new Vector3(0, yaw, 0);
        // Stand on the ground: some models (the dragon) have their origin mid-body, not at the feet.
        if (modelHeight is null && tall > 0) model.Position = model.Position with { Y = -box.Position.Y * model.Scale.Y };
        _meshes.AddRange(model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>());
        return true;
    }

    /// <summary>Paints the model with another texture (a dragon's colour), shiny if metallic.</summary>
    protected void Coat(string file, bool metallic, string? glow = null)
    {
        var texture = GD.Load<Texture2D>($"res://assets/creatures/{file}");
        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture, Roughness = metallic ? 0.38f : 0.8f, Metallic = metallic ? 0.75f : 0,
            MetallicSpecular = metallic ? 0.7f : 0.4f,
        };
        Material coat = material;
        if (glow is not null)
        {
            // A glowing coat: veins of light that pulse slowly (a corrupted dragon's).
            var veins = new ShaderMaterial { Shader = GlowCoat };
            veins.SetShaderParameter("albedo_tex", texture);
            veins.SetShaderParameter("glow_tex", GD.Load<Texture2D>($"res://assets/creatures/{glow}"));
            coat = veins;
        }
        foreach (var m in _meshes) m.MaterialOverride = coat;
    }

    static readonly Shader GlowCoat = new()
    {
        Code = """
            shader_type spatial;
            uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
            uniform sampler2D glow_tex : source_color, filter_linear_mipmap;
            uniform float glow = 2.4;
            void fragment() {
                ALBEDO = texture(albedo_tex, UV).rgb;
                ROUGHNESS = 0.7;
                SPECULAR = 0.45;
                float pulse = 0.7 + 0.3 * sin(TIME * 1.7 + UV.x * 9.0 + UV.y * 5.0);
                EMISSION = texture(glow_tex, UV).rgb * glow * pulse;
            }
            """,
    };

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
            if (m.MaterialOverride is StandardMaterial3D { EmissionTexture: null } mat) { mat.EmissionEnabled = true; mat.Emission = new Color("#ff4020"); mat.EmissionEnergyMultiplier = 0.5f; }
        GetTree().CreateTimer(0.15).Timeout += () =>
        {
            foreach (var m in _meshes) if (IsInstanceValid(m) && m.MaterialOverride is StandardMaterial3D { EmissionTexture: null } mat) mat.EmissionEnabled = false;
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
    /// <param name="glow">An emission map to go with the coat (the veins of a corrupted dragon).</param>
    public static Creature? Create(string file, float height, Color? tint = null, string? coat = null, bool metallic = false, string? glow = null)
    {
        var c = new Creature();
        if (!c.Load(file, height)) return null;
        if (tint is { } t) Models.Tint(c.Model, t);
        if (coat is not null) c.Coat(coat, metallic, glow);
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
        if (_rider is not null && Scale.X > 0) _rider.Scale = Vector3.One / Scale.X; // a knight stays a knight's size on a giant
    }

    DragonWings? _wings;
    bool _flying;
    float _speed;

    /// <summary>Gives a dragon its great wings (it can fly from now on).</summary>
    public void SpreadWings(Color membrane, Color bone, bool metallic, Color? glow = null)
    {
        _wings = DragonWings.Grow(Model, 4.5f * 1.55f / Model.Scale.X, membrane, bone, metallic, glow);
    }

    public bool Winged => _wings is not null;

    /// <summary>Grows ice spikes on a frost dragon's back (violet-black ice on a corrupted one).</summary>
    public void Freeze(bool corrupt = false) => IceRidge.Grow(Model, corrupt);

    /// <summary>Grows amethyst down its back instead of ice (the Crystal Basilisk).</summary>
    public void Crystallise() => IceRidge.Grow(Model, amethyst: true);

    /// <summary>The Rift's mark: a dark haze rising off it and a violet light about it.</summary>
    public void Corrupt()
    {
        var c = new Color(0.5f, 0.22f, 0.85f, 0.3f);
        var fade = new Gradient { Offsets = [0, 0.3f, 1], Colors = [new Color(c, 0), c, new Color(0.05f, 0, 0.08f, 0)] };
        AddChild(new CpuParticles3D
        {
            Mesh = new QuadMesh
            {
                Size = Vector2.One * 0.9f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = Colors.White, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = Effects.Dot,
                },
            },
            Amount = 40, Lifetime = 2.2, Position = new Vector3(0, 1.8f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1.0f, 0.9f, 2.4f), Direction = Vector3.Up, Spread = 25, Gravity = new Vector3(0, 0.6f, 0),
            InitialVelocityMin = 0.3f, InitialVelocityMax = 0.9f, ScaleAmountMin = 0.8f, ScaleAmountMax = 2.2f, ColorRamp = fade,
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 2.2f, 0), LightColor = new Color("#a050ff"), LightEnergy = 1.6f, OmniRange = 8 });
    }

    /// <summary>Where a rider sits, in this body's own space: on the back, behind the wings.</summary>
    public Vector3 Saddle { get; private set; }

    Figure? _rider;

    /// <summary>Puts a dragon knight in the saddle.</summary>
    public void Mount(Figure rider)
    {
        _rider = rider;
        AddChild(rider);
        rider.Position = FindSaddle();
        rider.Seat(true);
    }

    /// <summary>The saddle: part way along the first back bone, a little above the hide.</summary>
    public Vector3 FindSaddle()
    {
        if (Saddle != Vector3.Zero) return Saddle;
        var s = Model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        var (b1, b2) = (s?.FindBone("back1") ?? -1, s?.FindBone("back2") ?? -1);
        if (s is null || b1 < 0 || b2 < 0) return Saddle = new Vector3(0, 2.2f, 0);
        var space = Transform3D.Identity;
        for (Node? n = s; n is not null && n != this; n = n.GetParent())
            if (n is Node3D n3) space = n3.Transform * space;
        var spot = space * s.GetBoneGlobalRest(b1).Origin.Lerp(s.GetBoneGlobalRest(b2).Origin, 0.45f);
        return Saddle = spot - new Vector3(0, 0.3f, 0); // the rider's hips sink a little into the back (the legs are hidden)
    }

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
