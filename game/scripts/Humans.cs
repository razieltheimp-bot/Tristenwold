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
/// on the legs, boots on the feet, hair over the crown, skin for the rest. The skin itself is one of MakeHuman's
/// (CC0) skin textures under <c>assets/humans/tex/</c>, an age, a face and a sex each, tinted for the people; each
/// body comes with its eyes, brows, lashes and a head of hair fitted to its face, dressed from the same folder and
/// tinted with their hair colour. Where the files aren't (the phone build), or where the setting is off, the KayKit
/// figures stand in as before; where the textures aren't, the skin is a plain tone.
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

    /// <summary>How far the upper arms are brought down toward the body, in every clip (degrees).</summary>
    public const float ArmsDown = 40;

    /// <summary>
    /// KayKit's rig hangs the upper arms straight off the chest; the bodies have shoulder bones (collarbones) in
    /// between, which the clips never move. An upper arm's turn is kept relative to its parent, so on a body it would
    /// land relative to the shoulder's rest instead of the chest's: every upper-arm key is turned back by the
    /// shoulder's rest once, for all the bodies (their shoulders rest the same way, on the one profile). And KayKit's
    /// stocky figures hold their short arms well out from the body (a quarter turn below level, even standing
    /// still), which real arms turn into a scarecrow: the upper arms are brought down toward the body as well.
    /// </summary>
    static void Fit(AnimationPlayer player, Skeleton3D body)
    {
        var fixes = new Dictionary<string, Quaternion>();
        foreach (var side in new[] { "Left", "Right" })
        {
            var shoulder = body.FindBone($"{side}Shoulder");
            var down = new Quaternion(Vector3.Back, Mathf.DegToRad(side == "Left" ? -ArmsDown : ArmsDown)); // in the chest's frame, in the frontal plane
            if (shoulder >= 0) fixes[$"{side}UpperArm"] = body.GetBoneRest(shoulder).Basis.GetRotationQuaternion().Inverse() * down;
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

    const string TexFolder = Folder + "tex/";
    static readonly Dictionary<string, Texture2D?> Textures = [];

    /// <summary>A texture under <c>assets/humans/tex/</c> by file name, or null where it isn't.</summary>
    public static Texture2D? Texture(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        if (!Textures.TryGetValue(file, out var t))
            Textures[file] = t = ResourceLoader.Exists(TexFolder + file) ? GD.Load<Texture2D>(TexFolder + file) : null;
        return t;
    }

    /// <summary>The faces MakeHuman's skins come in, lightest first.</summary>
    static readonly string[] Faces = ["caucasian", "asian", "african"];

    /// <summary>
    /// What someone's made of: a skin texture (null where there's none), its tint, and how far the skin is recoloured
    /// to the tint outright (an orc's green: a dark skin can't be tinted green, only painted it, keeping the
    /// shading); a plain tone instead; their hair.
    /// </summary>
    public sealed record Skin(string? File, Color Tint, float Recolour, Color Plain, Color Hair, string Eyes);

    /// <summary>The eyes a people come in (see <c>tools/humans/finish_textures.py</c> for the colours).</summary>
    static string[] EyesOf(string? race) => race switch
    {
        "elf" => ["blue", "green", "grey", "blue"],
        "orc" => ["amber", "red", "brown"],
        "goblin" => ["amber", "red"],
        Races.Dragonkind => ["amber", "green", "grey"],
        _ => ["brown", "brown", "hazel", "blue", "green", "grey"],
    };

    /// <summary>
    /// A people's skin, hair and eyes, with the sameness broken by who they are: one of MakeHuman's skins (an age, a
    /// face and a sex) tinted the people's way, the plain tone for where the textures aren't, and the hair.
    /// </summary>
    public static Skin SkinFor(string? race, Sex sex, LifeStage stage, ulong seed)
    {
        var rng = new Rng(seed ^ 0x5C1A);
        var (faces, tint, recolour) = race switch
        {
            "elf" => (new[] { 0, 0, 1 }, new Color(1.0f, 0.98f, 0.96f), 0f),
            "dwarf" => (new[] { 0, 0, 1 }, new Color(1.0f, 0.9f, 0.84f), 0f),
            "orc" => (new[] { 1, 0 }, new Color(0.36f, 0.5f, 0.28f), 0.8f),
            "goblin" => (new[] { 0, 1 }, new Color(0.5f, 0.62f, 0.3f), 0.8f),
            "half_giant" => (new[] { 1, 2, 0 }, new Color(0.98f, 0.94f, 0.9f), 0f),
            Races.Dragonkind => (new[] { 2, 1 }, new Color(0.58f, 0.46f, 0.62f), 0.5f),
            _ => (new[] { 0, 1, 2, 0, 1, 2 }, Colors.White, 0f),
        };
        var face = Faces[rng.Pick(faces)];
        var age = stage switch { LifeStage.Elder => "old", LifeStage.Seasoned => "middleage", _ => "young" };
        var file = $"body-default__{age}_{face}_{(sex == Sex.Female ? "female" : "male")}.jpg";
        var (plain, hair) = ColoursFor(race, stage, seed);
        var eyes = rng.Pick(EyesOf(race));
        return new Skin(Texture(file) is null ? null : file, tint, recolour, plain, hair, Texture($"eyes__{eyes}.jpg") is null ? "" : $"eyes__{eyes}.jpg");
    }

    /// <summary>A people's plain skin tone and hair colour, with the sameness broken by who they are.</summary>
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
        Dress(mesh, skeleton, Humans.OutfitFor(look), Humans.SkinFor(race, sex, stage, seed), skin);
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
    /// The body is rebuilt once with vertex colours (clothes where the alpha is 1, skin where it's 0) and a skin
    /// material that reads them over the skin texture; the eyes, brows, lashes and hair that came with the body are
    /// dressed from their tags ("Hair-Bob01|hair-bob01__bob01.png") and tinted with the hair colour.
    /// </summary>
    static void Dress(MeshInstance3D mi, Skeleton3D skeleton, Humans.Outfit outfit, Humans.Skin who, Color? plain)
    {
        if (mi.Mesh is not ArrayMesh source || source.GetSurfaceCount() == 0) return;
        if (Debug) GD.Print($"dressing {mi.GetParent()?.GetParent()?.Name}: {source.GetSurfaceCount()} surfaces, skin {who.File ?? "plain"}, materials {string.Join(", ", Enumerable.Range(0, source.GetSurfaceCount()).Select(i => source.SurfaceGetMaterial(i)?.ResourceName ?? "-"))}");
        var skin = plain ?? who.Plain;
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
        var bare = skin with { A = 0 };
        var names = new Dictionary<int, string>();
        Color Covers(string name, Vector3 v) => name switch
        {
            _ when name.Contains("Toes") || name.Contains("Foot") => outfit.Boots,
            _ when name.Contains("Leg") || name == "Hips" => outfit.Trousers,
            _ when name.Contains("Hand") || name.Contains("Thumb") || name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little") => bare,
            _ when name.Contains("LowerArm") => outfit.Sleeves ? outfit.Tunic : bare,
            _ when name.Contains("UpperArm") || name.Contains("Shoulder") || name.Contains("Spine") || name.Contains("Chest") => outfit.Tunic,
            "Head" when v.Y > headY + (top - headY) * 0.45f && (v.Z < -0.01f || v.Y > headY + (top - headY) * 0.8f) => outfit.Cap ?? who.Hair,
            _ => bare,
        };
        var colours = new Color[verts.Length];
        for (var i = 0; i < verts.Length; i++)
        {
            // Every bone with a say over this vertex, by its weight: the collar and the cuffs fade in, rather than tear.
            var sum = new Color(0, 0, 0, 0);
            var total = 0f;
            for (var k = 0; k < per; k++)
            {
                var w = weights[i * per + k];
                if (w <= 0) continue;
                var bone = bones[i * per + k];
                if (!names.TryGetValue(bone, out var name)) names[bone] = name = BoneName(bone);
                sum += Covers(name, verts[i]) * w;
                total += w;
            }
            colours[i] = total > 0 ? sum / total : bare;
        }
        arrays[(int)Mesh.ArrayType.Color] = colours;
        var painted = new ArrayMesh();
        painted.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: Weights(arrays));
        painted.SurfaceSetMaterial(0, plain is null && who.File is { } file && Humans.Texture(file) is { } texture ? SkinMaterial(texture, who.Tint, who.Recolour) : new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.62f, MetallicSpecular = 0.3f,
            SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.35f, SubsurfScatterSkinMode = true,
            RimEnabled = true, Rim = 0.2f, RimTint = 0.7f,
        });
        // The parts that came with the body: eyes, brows, lashes, hair.
        for (var s = 1; s < source.GetSurfaceCount(); s++)
        {
            var tag = source.SurfaceGetMaterial(s)?.ResourceName ?? "";
            var bar = tag.IndexOf('|');
            var (equip, part) = bar < 0 ? (tag, "") : (tag[..bar], tag[(bar + 1)..]);
            if (part.Length > 4 && part[^4] == '_') part = part[..^4] + "." + part[^3..]; // the glTF export made "eyeball.png" into "eyeball_png"
            if (equip.StartsWith("Hair") && outfit.Cap is not null) continue; // under a hood or a cap
            if (equip.Contains("Eye-") && who.Eyes.Length > 0) part = who.Eyes; // their own colour of eyes
            var part_arrays = source.SurfaceGetArrays(s);
            painted.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, part_arrays, flags: Weights(part_arrays));
            painted.SurfaceSetMaterial(painted.GetSurfaceCount() - 1, PartMaterial(equip, part, who));
            if (Debug) GD.Print($"  part {s}: {equip} <- {part} ({(Humans.Texture(part) is { } t ? $"{t.GetWidth()}x{t.GetHeight()}" : "no texture")}), {source.SurfaceGetArrayLen(s)} vertices");
        }
        mi.Mesh = painted;
    }

    /// <summary>The bodies weight each vertex to eight bones: a rebuilt surface has to say so, or it won't follow the skeleton.</summary>
    static Mesh.ArrayFormat Weights(Godot.Collections.Array arrays)
    {
        var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
        var bones = arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil ? 0 : arrays[(int)Mesh.ArrayType.Bones].AsInt32Array().Length;
        return verts > 0 && bones == verts * 8 ? Mesh.ArrayFormat.FlagUse8BoneWeights : 0;
    }

    /// <summary>Eyes as they are; hair, brows and lashes (grey in the files) tinted with the hair colour, cut out by their alpha.</summary>
    static Material PartMaterial(string equip, string file, Humans.Skin who)
    {
        var hair = equip.StartsWith("Hair");
        var hairy = hair || equip.Contains("Eyebrow") || equip.Contains("Eyelash");
        var texture = Humans.Texture(file);
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = hairy ? who.Hair : texture is null ? new Color("#4a3a2a") : Colors.White,
            Transparency = hairy && texture is not null ? BaseMaterial3D.TransparencyEnum.AlphaScissor : BaseMaterial3D.TransparencyEnum.Disabled,
            AlphaScissorThreshold = hair ? 0.3f : 0.12f, // brows and lashes are faint strokes
            CullMode = hairy ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
            Roughness = hairy ? 0.75f : 0.45f,
            MetallicSpecular = hairy ? 0.25f : 0.35f,
        };
    }

    /// <summary>Says what each body is dressed in (--humans-debug).</summary>
    public static bool Debug { get; set; } = OS.GetCmdlineUserArgs().Contains("--humans-debug");

    static Shader? _skinShader;

    /// <summary>The skin texture, tinted for the people, with the painted clothes over it where the vertex alpha says so.</summary>
    static ShaderMaterial SkinMaterial(Texture2D skin, Color tint, float recolour)
    {
        _skinShader ??= new Shader { Code = SkinCode };
        var m = new ShaderMaterial { Shader = _skinShader };
        m.SetShaderParameter("skin", skin);
        m.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        m.SetShaderParameter("recolour", recolour);
        return m;
    }

    const string SkinCode = """
        shader_type spatial;
        uniform sampler2D skin : source_color, filter_linear_mipmap_anisotropic;
        uniform vec3 tint = vec3(1.0);
        uniform float recolour = 0.0;
        void fragment() {
            vec4 t = texture(skin, UV);
            float cloth = COLOR.a;
            vec3 paint = pow(COLOR.rgb, vec3(2.2));
            float lum = dot(t.rgb, vec3(0.3, 0.59, 0.11));
            vec3 flesh = mix(t.rgb * tint, vec3(lum) * 2.2 * tint, recolour); // recoloured skin keeps the texture's shading
            ALBEDO = mix(flesh, paint, cloth);
            ROUGHNESS = mix(0.5, 0.85, cloth);
            SPECULAR = mix(0.4, 0.2, cloth);
            SSS_STRENGTH = 0.35 * (1.0 - cloth);
            RIM = 0.15;
            RIM_TINT = 0.7;
        }
        """;
}
