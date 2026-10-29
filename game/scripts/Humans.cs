using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Realistic people. The bodies under <c>assets/humans/</c> are baked from MakeHuman's base mesh (CC0) with the
/// Humanizer tool (kept outside the game: see <c>tools/humans/</c>): one for each people, sex and stage of life, on
/// Godot's humanoid skeleton. They're moved by KayKit's animations, retargeted onto that same skeleton at import
/// (<c>assets/humans/moves.glb</c>), so every walk, swing, spell and fall the cartoon figures have, these have too.
/// Clothes are painted on the body by which bone each part of it follows: a tunic over the trunk and arms, trousers
/// on the legs, boots on the feet, hair over the crown, skin for the rest. Where the files aren't (the phone
/// build), or where the setting is off, the KayKit figures stand in as before.
/// </summary>
public static class Humans
{
    const string Folder = "res://assets/humans/";

    /// <summary>Whether the bodies are here and wanted (the graphics setting; never on a phone).</summary>
    public static bool Enabled { get; set; }
    public static bool Available => ResourceLoader.Exists(Folder + "moves.glb") && ResourceLoader.Exists(Folder + "human_male_adult.glb");

    static PackedScene? _moves;
    static bool _fitted;

    /// <summary>A copy of the retargeted move set for one body (the clips themselves are shared).</summary>
    public static AnimationPlayer? Moves(Skeleton3D body)
    {
        _moves ??= ResourceLoader.Exists(Folder + "moves.glb") ? GD.Load<PackedScene>(Folder + "moves.glb") : null;
        if (_moves?.Instantiate() is not Node3D source) return null;
        var player = source.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (player is null) { source.Free(); return null; }
        source.RemoveChild(player);
        source.Free();
        if (!_fitted) { Fit(player, body); _fitted = true; }
        return player;
    }

    /// <summary>
    /// KayKit's rig hangs the upper arms straight off the chest; the bodies have shoulder bones (collarbones) in
    /// between, which the clips never move. An upper arm's turn is kept relative to its parent, so on a body it would
    /// land relative to the shoulder's rest instead of the chest's: every upper-arm key is turned back by the
    /// shoulder's rest once, for all the bodies (their shoulders rest the same way, on the one profile).
    /// </summary>
    static void Fit(AnimationPlayer player, Skeleton3D body)
    {
        var fixes = new Dictionary<string, Quaternion>();
        foreach (var side in new[] { "Left", "Right" })
        {
            var shoulder = body.FindBone($"{side}Shoulder");
            if (shoulder >= 0) fixes[$"{side}UpperArm"] = body.GetBoneRest(shoulder).Basis.GetRotationQuaternion().Inverse();
        }
        if (fixes.Count == 0) return;
        foreach (var library in player.GetAnimationLibraryList())
            foreach (var name in player.GetAnimationLibrary(library).GetAnimationList())
            {
                var anim = player.GetAnimationLibrary(library).GetAnimation(name);
                for (var t = 0; t < anim.GetTrackCount(); t++)
                {
                    if (anim.TrackGetType(t) != Animation.TrackType.Rotation3D) continue;
                    var bone = anim.TrackGetPath(t).GetSubName(0).ToString();
                    if (!fixes.TryGetValue(bone, out var q)) continue;
                    for (var k = 0; k < anim.TrackGetKeyCount(t); k++)
                        anim.TrackSetKeyValue(t, k, q * anim.TrackGetKeyValue(t, k).AsQuaternion());
                }
            }
    }

    /// <summary>The body file for someone: their people, sex and stage of life, falling back to a human's.</summary>
    public static string? BodyFor(string? race, Sex sex, LifeStage stage)
    {
        var who = sex == Sex.Female ? "female" : "male";
        var when = stage switch { LifeStage.Young => "young", LifeStage.Adolescent => "adolescent", LifeStage.Elder => "elder", _ => "adult" };
        foreach (var people in new[] { race ?? "human", "human" })
        {
            var path = $"{Folder}{people}_{who}_{when}.glb";
            if (ResourceLoader.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>What someone wears, by their trade: tunic, trousers, boots, and whether a hood or cap covers the hair.</summary>
    public sealed record Outfit(Color Tunic, Color Trousers, Color Boots, bool Sleeves = true, Color? Cap = null);

    static readonly Dictionary<string, Outfit> Outfits = new()
    {
        ["guard"] = new(new("#6a6e74"), new("#3a3a44"), new("#2a2220"), Cap: new("#7a7e84")),
        ["marine"] = new(new("#8a3a2a"), new("#3a3a44"), new("#2a2220"), Cap: new("#6a6e74")),
        ["scholar"] = new(new("#4a5a8a"), new("#3a3a4a"), new("#3a2a20")),
        ["priest"] = new(new("#e8e0d0"), new("#d8d0c0"), new("#5a4a3a")),
        ["worker"] = new(new("#8a6a48"), new("#5a4a3a"), new("#3a2a20"), Sleeves: false),
        ["drinker"] = new(new("#7a5a40"), new("#4a3a30"), new("#3a2a20")),
        ["hunter"] = new(new("#5a6a40"), new("#4a4a30"), new("#3a2a20"), Cap: new("#4a5a30")),
        ["townsfolk"] = new(new("#8a7a5a"), new("#5a4a3a"), new("#3a2a20")),
        ["traveller"] = new(new("#6a5a4a"), new("#4a3a30"), new("#3a2a20"), Cap: new("#5a4a3a")),
        ["sailor"] = new(new("#c8d0e0"), new("#3a4a6a"), new("#3a2a20")),
        ["pirate"] = new(new("#a83a3a"), new("#2a2a3a"), new("#2a2220"), Cap: new("#3a2a2a")),
        ["pirate_captain"] = new(new("#5a1a2a"), new("#2a2a3a"), new("#2a2220"), Cap: new("#2a1a1a")),
        ["bandit"] = new(new("#6a5a3a"), new("#4a3a2a"), new("#2a2220"), Cap: new("#4a3a2a")),
        ["bandit_chief"] = new(new("#8a2a1a"), new("#4a3a2a"), new("#2a2220")),
        ["raider"] = new(new("#7a4a2a"), new("#5a3a2a"), new("#3a2a20"), Sleeves: false),
        ["raider_chief"] = new(new("#9a3a1a"), new("#5a3a2a"), new("#3a2a20"), Sleeves: false),
        ["child"] = new(new("#9a8a6a"), new("#6a5a4a"), new("#4a3a30")),
        ["youth"] = new(new("#8a7a5a"), new("#5a4a3a"), new("#3a2a20")),
        ["elder"] = new(new("#7a7a7a"), new("#4a4a4a"), new("#3a2a20")),
        ["captive"] = new(new("#d8d0c8"), new("#c8c0b8"), new("#8a7a6a")),
        ["ashborn"] = new(new("#9a6a5a"), new("#5a4a3a"), new("#3a2a20"), Cap: new("#7a5a4a")),
        ["velvet"] = new(new("#5a2a4a"), new("#2a1a2a"), new("#2a2220"), Cap: new("#4a1a3a")),
        ["warmaster"] = new(new("#6a4a3a"), new("#3a3a44"), new("#2a2220")),
        ["aldric"] = new(new("#c8b080"), new("#3a3a44"), new("#2a2220")),
        ["dragon_knight"] = new(new("#b8a870"), new("#3a3a44"), new("#2a2220")),
    };

    public static Outfit OutfitFor(string look) => Outfits.GetValueOrDefault(look) ?? Outfits["townsfolk"];

    /// <summary>A people's skin, hair and eyes, with the sameness broken by who they are.</summary>
    public static (Color Skin, Color Hair) ColoursFor(string? race, LifeStage stage, ulong seed)
    {
        var rng = new Rng(seed ^ 0x5C1A);
        Color[] skins = race switch
        {
            "elf" => [new("#f0dcc8"), new("#e8d0b8"), new("#d8c0a8")],
            "dwarf" => [new("#d8a888"), new("#c89878"), new("#e0b898")],
            "orc" => [new("#6a8a5a"), new("#5a7a4a"), new("#7a8a60")],
            "goblin" => [new("#8aa060"), new("#7a9050"), new("#9aa870")],
            "half_giant" => [new("#c8a080"), new("#b89070"), new("#d0a888")],
            Races.Dragonkind => [new("#b88868"), new("#a87858"), new("#c09878")],
            _ => [new("#f0d0b0"), new("#e0b890"), new("#c89870"), new("#a87050"), new("#8a5838"), new("#6a4028")],
        };
        Color[] hairs = race switch
        {
            "elf" => [new("#f0e0b0"), new("#c8a060"), new("#3a2a20"), new("#d8d0c0")],
            "dwarf" => [new("#a84a20"), new("#3a2a20"), new("#2a1a10"), new("#c89060")],
            "orc" or "goblin" => [new("#1a1a1a"), new("#2a2a2a")],
            _ => [new("#2a1a10"), new("#4a3020"), new("#8a5a30"), new("#c89050"), new("#e0d0a0"), new("#1a1a1a")],
        };
        var hair = stage == LifeStage.Elder ? new Color("#c8c4bc") : rng.Pick(hairs);
        return (rng.Pick(skins), hair);
    }
}

public partial class Figure
{
    /// <summary>
    /// A realistic person (see <see cref="Humans"/>): the baked body for their people, sex and age, painted with their
    /// trade's clothes, moved by KayKit's retargeted move set, and carrying what their look carries.
    /// </summary>
    public static Figure? Human(string look, string? race, Sex sex, LifeStage stage, ulong seed, Color? skin = null)
    {
        if (Humans.BodyFor(race, sex, stage) is not { } file || GD.Load<PackedScene>(file)?.Instantiate() is not Node3D model) return null;
        var l = Looks.GetValueOrDefault(look) ?? Looks["townsfolk"];
        var f = new Figure { _look = l, _human = true };
        model.RotationDegrees = new Vector3(0, 180, 0); // the bodies face +Z, like KayKit's
        f.AddChild(model);
        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        var mesh = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (skeleton is null || mesh is null) { f.Free(); return null; }
        var moves = Humans.Moves(skeleton);
        if (moves is null) { f.Free(); return null; }
        skeleton.Name = "GeneralSkeleton";
        skeleton.Owner = model;
        skeleton.UniqueNameInOwner = true;
        var (skinColour, hair) = Humans.ColoursFor(race, stage, seed);
        Dress(mesh, skeleton, Humans.OutfitFor(look), skin ?? skinColour, hair);
        // The move set, told where its skeleton is.
        model.AddChild(moves);
        moves.RootNode = moves.GetPathTo(model);
        f._anim = moves;
        foreach (var name in moves.GetAnimationList())
            if (Looping.Contains(name)) moves.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        // What they carry, in real hands (KayKit's weapons are sized for its small figures).
        foreach (var (bone, item) in new[] { ("RightHand", l.RightHand ?? l.Show.FirstOrDefault(Weapons.Contains)), ("LeftHand", l.LeftHand ?? l.Show.FirstOrDefault(s => s.Contains("Shield") || s == "Knife_Offhand" || s == "Spellbook_open")) })
            HoldHuman(skeleton, bone, item);
        return f;
    }

    static readonly string[] Weapons = ["1H_Sword", "2H_Sword", "1H_Axe", "2H_Axe", "Knife", "1H_Wand", "2H_Staff", "1H_Crossbow", "2H_Crossbow", "Mug", "Spellbook"];
    bool _human;

    static void HoldHuman(Skeleton3D skeleton, string bone, string? item)
    {
        if (item is null || skeleton.FindBone(bone) < 0) return;
        var slot = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(slot);
        if (item.StartsWith('@')) { _ = Craft(skeleton, bone, item[1..]); return; }
        if (Models.Scene($"weapons/{item}.gltf")?.Instantiate() is not Node3D weapon) return;
        var grip = new Node3D { Scale = Vector3.One * 1.35f, Position = new Vector3(0, -0.02f, 0.06f), RotationDegrees = bone.StartsWith("Left") ? new Vector3(-90, 0, 90) : new Vector3(-90, 0, -90) };
        slot.AddChild(grip);
        grip.AddChild(weapon);
    }

    /// <summary>
    /// Paints the clothes on: each vertex goes with the bone that moves it most, and the bone says what covers it.
    /// The mesh is rebuilt once with vertex colours, and a skin material that reads them.
    /// </summary>
    static void Dress(MeshInstance3D mi, Skeleton3D skeleton, Humans.Outfit outfit, Color skin, Color hair)
    {
        if (mi.Mesh is not ArrayMesh source || source.GetSurfaceCount() == 0) return;
        var arrays = source.SurfaceGetArrays(0);
        var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
        var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
        var per = verts.Length > 0 ? bones.Length / verts.Length : 4;
        // The mesh's bone indices count through the skin's binds, which needn't be the skeleton's order.
        var binds = mi.Skin;
        string BoneName(int bind) => binds is not null && bind < binds.GetBindCount()
            ? (binds.GetBindBone(bind) is var b && b >= 0 ? skeleton.GetBoneName(b) : binds.GetBindName(bind).ToString())
            : skeleton.GetBoneName(bind);
        var head = skeleton.FindBone("Head");
        var headY = head >= 0 ? skeleton.GetBoneGlobalRest(head).Origin.Y : 1.5f;
        var top = verts.Length > 0 ? verts.Max(v => v.Y) : headY + 0.2f;
        var colours = new Color[verts.Length];
        for (var i = 0; i < verts.Length; i++)
        {
            // The bone with most say over this vertex.
            var best = -1;
            var bestW = 0f;
            for (var k = 0; k < per; k++)
                if (weights[i * per + k] > bestW) { bestW = weights[i * per + k]; best = bones[i * per + k]; }
            var name = best >= 0 ? BoneName(best) : "";
            var v = verts[i];
            colours[i] = name switch
            {
                _ when name.Contains("Toes") || name.Contains("Foot") => outfit.Boots,
                _ when name.Contains("Leg") || name == "Hips" => outfit.Trousers,
                _ when name.Contains("Hand") || name.Contains("Thumb") || name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little") => skin,
                _ when name.Contains("LowerArm") => outfit.Sleeves ? outfit.Tunic : skin,
                _ when name.Contains("UpperArm") || name.Contains("Shoulder") || name.Contains("Spine") || name.Contains("Chest") => outfit.Tunic,
                "Head" when v.Y > headY + (top - headY) * 0.45f && (v.Z < -0.01f || v.Y > headY + (top - headY) * 0.8f) => outfit.Cap ?? hair,
                "Neck" => skin,
                _ => skin,
            };
        }
        arrays[(int)Mesh.ArrayType.Color] = colours;
        var painted = new ArrayMesh();
        painted.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        painted.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.62f, MetallicSpecular = 0.3f,
            SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.35f, SubsurfScatterSkinMode = true,
            RimEnabled = true, Rim = 0.2f, RimTint = 0.7f,
        });
        mi.Mesh = painted;
    }
}
