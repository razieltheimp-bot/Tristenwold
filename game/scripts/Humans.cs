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

    /// <summary>Whether the bodies are in use: the graphics setting decides (<see cref="WantedAt"/>).</summary>
    public static bool Enabled { get; set; } = WantedAt(Graphics.Default);

    /// <summary>
    /// Both looks, by the graphics setting: above High (Ultra, Cinematic) the people are the realistic bodies; High
    /// and below, the cartoon figures. Never on a phone, which hasn't the files; --humans asks for the bodies anyway.
    /// </summary>
    public static bool WantedAt(Graphics.Quality q) => Available && !OS.HasFeature("mobile") && (q > Graphics.Quality.High || OS.GetCmdlineUserArgs().Contains("--humans"));
    public static bool Available => ResourceLoader.Exists(Folder + "moves.glb") && ResourceLoader.Exists(Folder + "human_male_adult.glb");

    /// <summary>The peoples in the order the bodies were baked (tools/humans/bake_humans.gd), which decided each one's hair.</summary>
    public static readonly string[] Peoples = ["human", "elf", "dwarf", "orc", "goblin", "half_giant", "dragonkind"];
    static readonly string[] FemaleHair = ["Bob01", "Long01", "Braid01", "Ponytail01", "Bob02", "Long01", "Braid01"];
    static readonly string[] MaleHair = ["Short01", "Short02", "Short03", "Short04", "Afro01", "Short02", "Short03"];

    /// <summary>The hair style a body was made with (the bake spread the styles about the peoples and sexes).</summary>
    public static string HairOf(string? race, Sex sex, LifeStage stage)
    {
        var i = Math.Max(0, Array.IndexOf(Peoples, race ?? "human"));
        var set = sex == Sex.Female ? FemaleHair : MaleHair;
        return set[(i * 3 + (stage == LifeStage.Elder ? 1 : 0)) % set.Length];
    }

    /// <summary>A body of this sex made with this hair style (the same stage of life where there is one), to borrow the hair from.</summary>
    public static string? BodyWithHair(string style, Sex sex, LifeStage stage)
    {
        foreach (var when in new[] { stage, LifeStage.Adult, LifeStage.Elder, LifeStage.Adolescent, LifeStage.Young }.Distinct())
            foreach (var people in Peoples)
                if (HairOf(people, sex, when) == style && BodyFor(people, sex, when) is { } file && file.Contains($"/{people}_")) return file;
        return null;
    }

    /// <summary>The looks that aren't people: machines, the undead, spirits and trolls keep their own figures.</summary>
    static readonly HashSet<string> NotPeople = ["golem", "familiar", "cinder_warden", "cinder_thrall", "forge_golem", "draugr", "draugr_overlord", "sea_wraith", "spriggan", "frost_troll"];

    /// <summary>Whether a look is somebody: a person, to be given a real body.</summary>
    public static bool IsPerson(string look) => Figure.Looks.ContainsKey(look) && !NotPeople.Contains(look);

    /// <summary>Who an enemy or a follower of this look is: a people, a sex and an age, drawn for each one.</summary>
    public static (string Race, Sex Sex, LifeStage Stage, ulong Seed)? Cast(string look, ulong? seed = null)
    {
        if (!IsPerson(look)) return null;
        var s = seed ?? ((ulong)GD.Randi() << 32 | GD.Randi());
        var rng = new Rng(s ^ 0xCA57);
        string[] peoples = look switch
        {
            "raider" or "raider_chief" or "warmaster" => ["human", "orc", "half_giant", "human"],
            "bandit" or "bandit_chief" or "dock_thug" or "drowned_hand" => ["human", "human", "dwarf", "orc"],
            "pirate" or "pirate_captain" or "dread_soren" or "sailor" or "monkey_hand" => ["human", "human", "elf", "dwarf"],
            "guard" or "marine" or "squire" or "hollow_soldier" or "ghost_captain" => ["human", "human", "dwarf"],
            "dragon_knight" or "aldric" or "laureate" => ["human"],
            _ => ["human", "human", "human", "elf", "dwarf"],
        };
        var stage = look switch
        {
            "child" => LifeStage.Young,
            "youth" or "squire" or "apprentice" or "protege" or "understudy" or "lantern_boy" => LifeStage.Adolescent,
            "elder" or "laureate" => LifeStage.Elder,
            _ => LifeStage.Adult,
        };
        return (rng.Pick(peoples), rng.Chance(0.5) ? Sex.Female : Sex.Male, stage, s);
    }

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
        ["soren"] = new(new("#9a1f1a"), new("#1e2230"), new("#4a2c1c")), // the red coat's sleeves, dark breeches, sea boots
        ["giant_ape"] = new(new("#4a2e22"), new("#4a2e22"), new("#a98470")),
        ["turlin"] = new(new("#17151a"), new("#17151a"), new("#17151a")), // black fur, near enough all over
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
        ["pirate"] = new(new("#a83a3a"), new("#2a2a3a"), new("#2a2220"), Cap: new("#8a2a2a")),
        ["pirate_captain"] = new(new("#5a1a2a"), new("#2a2a3a"), new("#2a2220"), Cap: new("#2a1a2a")),
        ["monkey_hand"] = new(new("#b8903a"), new("#3a3026"), new("#2a2220"), Cap: new("#8a2f24")), // patched gold and a red rag: Lu's colours
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
        // The playable classes: what each wears out of its guild, before the hero's own colours go on.
        ["Paladin"] = new(new("#8a8e96"), new("#3a3a44"), new("#2a2220")),
        ["Knight"] = new(new("#8a8e96"), new("#3a3a44"), new("#2a2220")),
        ["MartialArtist"] = new(new("#c8b090"), new("#5a4a3a"), new("#3a2a20"), Sleeves: false),
        ["Barbarian"] = new(new("#7a4a2a"), new("#5a3a2a"), new("#3a2a20"), Sleeves: false),
        ["Ranger"] = new(new("#5a6a40"), new("#4a4a30"), new("#3a2a20"), Cap: new("#4a5a30")),
        ["Thief"] = new(new("#4a3a30"), new("#2a2a2a"), new("#2a2220")),
        ["Rogue"] = new(new("#4a3a30"), new("#2a2a2a"), new("#2a2220")),
        ["Assassin"] = new(new("#2a2a34"), new("#1e1e24"), new("#1a1a1a"), Cap: new("#22222a")),
        ["Rogue_Hooded"] = new(new("#4a3a30"), new("#2a2a2a"), new("#2a2220"), Cap: new("#3a2a20")),
        ["Wizard"] = new(new("#3a4a8a"), new("#2a2a4a"), new("#3a2a20")),
        ["Mage"] = new(new("#3a4a8a"), new("#2a2a4a"), new("#3a2a20")),
        ["Warlock"] = new(new("#4a2a5a"), new("#2a1a30"), new("#2a2220")),
        ["Sorcerer"] = new(new("#8a2a2a"), new("#4a2020"), new("#3a2a20")),
        ["Poet"] = new(new("#e8dcc0"), new("#6a5a4a"), new("#3a2a20")),
        ["Scribe"] = new(new("#6a6a8a"), new("#3a3a4a"), new("#3a2a20")),
        ["Musician"] = new(new("#c08a50"), new("#5a4a3a"), new("#3a2a20")),
        ["Alchemist"] = new(new("#8a6a3e"), new("#40382c"), new("#3a2a20"), Cap: new("#5a4630")),
        ["EtherAeronaut"] = new(new("#7a4f2c"), new("#3a3028"), new("#2a2220"), Cap: new("#b08a4a")),
        ["Stormdruid"] = new(new("#4a6a4a"), new("#34402c"), new("#3a2a20")),
        ["Shapeshifter"] = new(new("#7a5a3a"), new("#4a3a28"), new("#3a2a20"), Sleeves: false),
        ["Spiritcaller"] = new(new("#8aa0a0"), new("#3a4a4a"), new("#3a2a20")),
        ["Saboteur"] = new(new("#34342e"), new("#26261f"), new("#1e1a16"), Cap: new("#2c2c26")),
        // The young who follow a hero, and the rest of the enemies.
        ["squire"] = new(new("#a0a4ac"), new("#3a3a44"), new("#2a2220")),
        ["apprentice"] = new(new("#4a5a9a"), new("#2a2a4a"), new("#3a2a20")),
        ["protege"] = new(new("#5a4a3a"), new("#2a2a2a"), new("#2a2220")),
        ["understudy"] = new(new("#d0a060"), new("#5a4a3a"), new("#3a2a20")),
        ["laureate"] = new(new("#f0e4c8"), new("#6a5a4a"), new("#3a2a20")),
        ["lantern_boy"] = new(new("#8a5a3a"), new("#4a3a2a"), new("#2a2220")),
        ["drowned_hand"] = new(new("#3a5a6a"), new("#2a2a3a"), new("#2a2220"), Cap: new("#2a4a5a")),
        ["dock_thug"] = new(new("#6a4a3a"), new("#4a3a2a"), new("#2a2220"), Sleeves: false),
        ["hollow_soldier"] = new(new("#8aa0a8"), new("#5a6a70"), new("#3a3a40")),
        ["ghost_captain"] = new(new("#9ab0b8"), new("#5a6a70"), new("#3a3a40"), Cap: new("#8a9aa0")),
    };

    public static Outfit OutfitFor(string look) => look.StartsWith("lu_") ? new(LuFur(look), LuFur(look), LuFur(look)) : Outfits.GetValueOrDefault(look.StartsWith("soren_") ? "soren" : look) ?? Outfits["townsfolk"];

    /// <summary>The colour of Lu's body under his fur, greying as he goes.</summary>
    public static Color LuFur(string look) => new Color("#5a432e").Lerp(new Color("#67666a"), Tristinwold.Gameplay.Legend.WukongOf(int.Parse(look[3..])).Beast);

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
    public static Skin SkinFor(string? race, Sex sex, LifeStage stage, ulong seed, string? face = null, string? eyes = null, Color? hair = null, Color? tint = null, float? recolour = null)
    {
        var rng = new Rng(seed ^ 0x5C1A);
        var (faces, ownTint, ownRecolour) = race switch
        {
            "elf" => (new[] { 0, 0, 1 }, new Color(1.0f, 0.98f, 0.96f), 0f),
            "dwarf" => (new[] { 0, 0, 1 }, new Color(1.0f, 0.9f, 0.84f), 0f),
            "orc" => (new[] { 1, 0 }, new Color(0.36f, 0.5f, 0.28f), 0.8f),
            "goblin" => (new[] { 0, 1 }, new Color(0.5f, 0.62f, 0.3f), 0.8f),
            "half_giant" => (new[] { 1, 2, 0 }, new Color(0.98f, 0.94f, 0.9f), 0f),
            "ape" => (new[] { 2 }, new Color(0.3f, 0.22f, 0.17f), 0.9f),
            "plant" => (new[] { 0 }, new Color(0.42f, 0.5f, 0.24f), 0.9f),
            "shroomie" => (new[] { 0 }, new Color(0.84f, 0.76f, 0.64f), 0.7f),
            "lizard" => (new[] { 0 }, new Color(0.34f, 0.5f, 0.27f), 0.9f),
            Races.Dragonkind => (new[] { 2, 1 }, new Color(0.58f, 0.46f, 0.62f), 0.5f),
            _ => (new[] { 0, 1, 2, 0, 1, 2 }, Colors.White, 0f),
        };
        var chosen = Faces.Contains(face ?? "") ? face! : Faces[rng.Pick(faces)];
        var age = stage switch { LifeStage.Elder => "old", LifeStage.Seasoned => "middleage", _ => "young" };
        var file = $"body-default__{age}_{chosen}_{(sex == Sex.Female ? "female" : "male")}.jpg";
        var (plain, ownHair) = ColoursFor(race, stage, seed);
        var eye = eyes ?? rng.Pick(EyesOf(race));
        return new Skin(Texture(file) is null ? null : file, tint ?? ownTint, recolour ?? ownRecolour, plain, hair ?? ownHair, Texture($"eyes__{eye}.jpg") is null ? "" : $"eyes__{eye}.jpg");
    }

    /// <summary>The creator's names for the faces and eyes, as the files have them.</summary>
    public static string? FaceFile(string face) => face switch { "Fair" => "caucasian", "Olive" => "asian", "Dark" => "african", _ => null };
    public static string? EyeFile(string eyes) => eyes == "Natural" ? null : eyes.ToLowerInvariant();

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
    /// <summary>What a real body is dressed in, kept so it can be dressed again (armour put on, a helm): the clothes,
    /// the skin, a plain tone instead, the hair style ("None" for shaved; null for the body's own), and whether the
    /// clothes are metal.</summary>
    sealed record Garb(Humans.Outfit Outfit, Humans.Skin Who, Color? Plain, string? Hair, bool Metal = false, float Width = 1);

    bool _human;
    ArrayMesh? _source;
    MeshInstance3D? _mesh;
    Skeleton3D? _skeleton;
    Garb? _garb;
    Sex _sex;
    LifeStage _stage;
    readonly Dictionary<string, Node> _held = [];
    List<Node> _garments = [];
    string _garmentLook = "townsfolk";
    string _wear = "Class";
    (string Style, Color Colour)? _cut;
    (string Race, string Form)? _kin;
    float _top = 1.7f;

    /// <summary>A real body (see <see cref="Humans"/>), rather than a KayKit figure.</summary>
    public bool IsHuman => _human;

    /// <summary>
    /// A realistic person (see <see cref="Humans"/>): the baked body for their people, sex and age, painted with their
    /// trade's clothes, moved by KayKit's retargeted move set, and carrying what their look carries.
    /// </summary>
    public static Figure? Human(string look, string? race, Sex sex, LifeStage stage, ulong seed, Color? skin = null)
    {
        var l = Looks.GetValueOrDefault(look) ?? Looks["townsfolk"];
        var size = l.Scale > 0.8f ? l.Scale / 0.74f : 1; // a chief or a captain stands bigger than his men, as his figure did
        return Build(look, Humans.BodyFor(race, sex, stage), sex, stage, new Garb(Humans.OutfitFor(look), Humans.SkinFor(race, sex, stage, seed), skin, null), size, look == "giant_ape" ? size * 1.12f : 1, null); // the ape's broad with it
    }

    /// <summary>
    /// A hero as they made themselves: their people, sex and age pick the body; their face, eyes, hair and its colour,
    /// clothes, skin tone, height and build dress it; the class decides what they carry; dragonkind grow horns.
    /// </summary>
    public static Figure? Human(string classLook, Tristinwold.Gameplay.Avatar a)
    {
        var people = a.IsDragonkind && !a.PureDragon ? a.Half : a.Race;
        var sex = a.Female ? Sex.Female : Sex.Male;
        var stage = a.Age switch { "Elder" => LifeStage.Elder, "Seasoned" => LifeStage.Seasoned, _ => LifeStage.Adult };
        var seed = (ulong)WorldGen.StableHash(a.Encode());
        // Their skin: their people's own, or a tone picked; dragon blood shows in it.
        Color? tint = a.Skin == "Natural" ? null : new Color(a.SkinHex);
        float? recolour = a.Skin is "Ashen" or "Frost" or "Verdant" ? 0.6f : a.Skin == "Natural" ? null : 0f;
        if (a.Blood is { } blood)
        {
            var own = Humans.SkinFor(people, sex, stage, seed).Tint;
            tint = (tint ?? own).Lerp(new Color(blood.Hex), a.PureDragon ? 0.45f : 0.25f);
            recolour = Math.Max(recolour ?? 0, 0.5f);
        }
        if (a.Race == "plant" && a.Form == "Fall" && a.Skin == "Natural") (tint, recolour) = (new Color(0.36f, 0.27f, 0.19f), 0.9f); // bare wood
        var who = Humans.SkinFor(people, sex, stage, seed, Humans.FaceFile(a.Face), Humans.EyeFile(a.Eyes), new Color(a.HairHex), tint, recolour);
        // Their clothes: the class's, in their colour, with a hood or a cap if they want one.
        var outfit = Humans.OutfitFor(classLook);
        if (a.Outfit != "Natural")
        {
            var dye = new Color(a.OutfitHex);
            outfit = outfit with { Tunic = outfit.Tunic * dye, Trousers = outfit.Trousers * dye.Darkened(0.2f), Cap = outfit.Cap is { } c ? c * dye : null };
        }
        if (Tristinwold.Gameplay.Avatar.ClothHex(a.Top) is { } top) outfit = outfit with { Tunic = new Color(top) };
        if (Tristinwold.Gameplay.Avatar.ClothHex(a.Bottom) is { } bottom) outfit = outfit with { Trousers = new Color(bottom) };
        if (a.Sleeves != "Class") outfit = outfit with { Sleeves = a.Sleeves == "Long" };
        outfit = a.Headwear switch
        {
            "Hood" => outfit with { Cap = outfit.Cap ?? outfit.Tunic.Darkened(0.25f) },
            "Cap" => outfit with { Cap = outfit.Trousers.Lightened(0.1f) },
            _ => outfit with { Cap = null },
        };
        var height = Tristinwold.Gameplay.Avatar.Heights.FirstOrDefault(h => h.Name == a.Height).Scale is > 0 and var h ? h / 0.74f : 1;
        var width = Tristinwold.Gameplay.Avatar.Builds.FirstOrDefault(b => b.Name == a.Build).Width is > 0 and var w ? w : 1;
        if (a.Race is "ape" or "plant" or "shroomie" or "lizard") (height, width) = (height * (float)a.People.Size, width * (float)a.People.Width); // they have no bodies of their own to be built to size
        var hair = a.Race is "plant" or "shroomie" or "lizard" || Tristinwold.Gameplay.Avatar.IsCut(a.Style) ? "None" : a.Style == "Natural" ? null : a.Style;
        var f = Build(classLook, Humans.BodyFor(people, sex, stage), sex, stage, new Garb(outfit, who, null, hair, Width: width), height, width, a);
        return f;
    }

    static Figure? Build(string look, string? file, Sex sex, LifeStage stage, Garb garb, float height, float width, Tristinwold.Gameplay.Avatar? a)
    {
        if (file is null || GD.Load<PackedScene>(file)?.Instantiate() is not Node3D model) return null;
        var l = Looks.GetValueOrDefault(look) ?? Looks["townsfolk"];
        var f = new Figure { _look = l, _human = true, _sex = sex, _stage = stage };
        model.RotationDegrees = new Vector3(0, 180, 0); // the bodies face +Z, like KayKit's
        model.Scale = new Vector3(width, height, width);
        f.AddChild(model);
        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        var mesh = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (skeleton is null || mesh is null) { f.Free(); return null; }
        var moves = Humans.Moves(skeleton);
        if (moves is null) { f.Free(); return null; }
        if (look == "giant_ape" || look.StartsWith("lu_")) skeleton.AddChild(new ApeStance());
        skeleton.Name = "GeneralSkeleton";
        skeleton.Owner = model;
        skeleton.UniqueNameInOwner = true;
        f._source = mesh.Mesh as ArrayMesh;
        f._mesh = mesh;
        f._skeleton = skeleton;
        f._garmentLook = look;
        if (a is not null)
        {
            f._wear = a.Wear;
            f._cut = Tristinwold.Gameplay.Avatar.IsCut(a.Style) && a.Race is not ("plant" or "shroomie" or "lizard") ? (a.Style, new Color(a.HairHex)) : null; // leaves, a cap or a crest: never hair
            f._kin = a.Race is "ape" or "plant" or "shroomie" or "lizard" ? (a.Race, a.Form) : null;
        }
        if (f._source is { } src && src.GetSurfaceCount() > 0) { var vs = src.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array(); if (vs.Length > 0) f._top = vs.Max(v => v.Y); }
        // A cap or a hood in the kit covers the hair: it's put away, and the scalp painted its colour, so nothing pokes through the steel.
        // With nothing on the head, the hair is kept and the scalp left alone, whatever the outfit's cap colour was for.
        if (Garments.For(look).Head is "cap" or "hood" && garb.Outfit.Cap is null)
            garb = garb with { Outfit = garb.Outfit with { Cap = Garments.For(look).Head == "cap" ? garb.Outfit.Trousers.Darkened(0.2f) : garb.Outfit.Tunic.Darkened(0.25f) } };
        else if (Garments.For(look).Head is null or "hat" or "bandana" && garb.Outfit.Cap is not null)
            garb = garb with { Outfit = garb.Outfit with { Cap = null } };
        f.Redress(garb);
        f.Regarment(null, null);
        // The move set, told where its skeleton is.
        model.AddChild(moves);
        moves.RootNode = moves.GetPathTo(model);
        f._anim = moves;
        foreach (var name in moves.GetAnimationList())
            if (Looping.Contains(name)) moves.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        // What they carry, in real hands (KayKit's weapons are sized for its small figures).
        foreach (var (bone, item) in new[] { ("RightHand", l.RightHand ?? l.Show.FirstOrDefault(Weapons.Contains)), ("LeftHand", l.LeftHand ?? l.Show.FirstOrDefault(s => s.Contains("Shield") || s == "Knife_Offhand" || s == "Spellbook_open")) })
            if (HoldHuman(skeleton, bone, item, glow: l.Glow > 0.2f && l.Tint is { } g ? g.ToHtml(false) is var hex ? "#" + hex : null : null) is { } held) f._held[bone] = held;
        if (a is not null) f.HornsHuman(a);
        skeleton.AddChild(new HumanGaze()); // the head turns to whoever's near
        return f;
    }

    /// <summary>What a real body holds in a hand (the self-test looks).</summary>
    public Node? HeldIn(string bone) => _held.GetValueOrDefault(bone);

    static readonly string[] Weapons = ["1H_Sword", "2H_Sword", "1H_Axe", "2H_Axe", "Knife", "1H_Wand", "2H_Staff", "1H_Crossbow", "2H_Crossbow", "Mug", "Spellbook"];

    /// <summary>What a look carries, forged to real size (see <see cref="Armoury"/>) and put in a real hand.</summary>
    static Node? HoldHuman(Skeleton3D skeleton, string bone, string? item, Color? tint = null, string? glow = null)
    {
        if (item is null || skeleton.FindBone(bone) < 0) return null;
        if (item.StartsWith('@')) { var crafted = Craft(skeleton, bone, item[1..]); return crafted?.GetParent(); }
        if (Armoury.Make(item, tint, glow: glow) is not { } piece) return null;
        var slot = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(slot);
        slot.AddChild(Armoury.InHand(bone, piece));
        return slot;
    }

    /// <summary>
    /// Dresses a real body in what it wears (a hero putting armour on, or a helm): the hero's class pieces leave the
    /// hands that gear takes, body armour paints the tunic in its colour (metal, if it's metal), and a helm or a hat
    /// paints a cap on the head in its colour, so nothing of another style sits on a real body.
    /// </summary>
    public void WearHuman(Tristinwold.Gameplay.Pack pack, Func<Tristinwold.Gameplay.Item, Node3D?> piece)
    {
        if (_skeleton is null || _garb is null) return;
        foreach (var (slot, bone) in new[] { (Tristinwold.Gameplay.Slot.Weapon, "RightHand"), (Tristinwold.Gameplay.Slot.Offhand, "LeftHand") })
        {
            // The item names its KayKit piece ("Knight/1H_Sword"); the armoury forges the same thing to real size, in the item's metal.
            if (pack.WornIn(slot) is not { } item || item.Look is null) continue;
            if (item.Look.StartsWith('@')) // made from shapes: a hammer, a mace
            {
                if (_held.Remove(bone, out var off)) off.QueueFree();
                if (Craft(_skeleton, bone, item.Look[1..]) is { } crafted) _held[bone] = crafted;
                if (slot == Tristinwold.Gameplay.Slot.Weapon) Held = item.Grip;
                continue;
            }
            var id = item.Look.Contains('/') ? item.Look.Split('/')[1] : item.Look;
            var forged = Armoury.Make(id, item.Tint is { } t ? new Color(t) : null, item.Metal, item.Glow) ?? piece(item);
            if (forged is null) continue;
            if (_held.Remove(bone, out var was)) was.QueueFree();
            var hold = new BoneAttachment3D { BoneName = bone };
            _skeleton.AddChild(hold);
            hold.AddChild(Armoury.InHand(bone, forged));
            _held[bone] = hold;
            if (slot == Tristinwold.Gameplay.Slot.Weapon) Held = item.Grip;
        }
        var garb = _garb;
        if (pack.WornIn(Tristinwold.Gameplay.Slot.Body) is { } armour)
        {
            var tint = new Color(armour.Tint ?? "#8a6a48");
            garb = garb with { Outfit = garb.Outfit with { Tunic = tint, Trousers = tint.Darkened(0.35f) }, Metal = armour.Metal };
        }
        if (pack.WornIn(Tristinwold.Gameplay.Slot.Head) is { } helm)
            garb = garb with { Outfit = garb.Outfit with { Cap = new Color(helm.Tint ?? (helm.Metal ? "#9a9ea6" : "#5a4a3a")) } };
        if (garb != _garb) Redress(garb);
        Regarment(pack.WornIn(Tristinwold.Gameplay.Slot.Body) is { } worn ? (new Color(worn.Tint ?? "#8a6a48"), worn.Metal) : null,
            pack.WornIn(Tristinwold.Gameplay.Slot.Head) is { } hat ? new Color(hat.Tint ?? (hat.Metal ? "#9a9ea6" : "#5a4a3a")) : null);
    }

    /// <summary>The medieval kit over the clothes (see <see cref="Garments"/>), put on fresh: the look's own, or the armour and helm worn.</summary>
    void Regarment((Color Tint, bool Metal)? armour, Color? helm)
    {
        foreach (var n in _garments) if (IsInstanceValid(n)) { n.GetParent()?.RemoveChild(n); n.QueueFree(); }
        _garments = _skeleton is not null && _garb is not null ? Garments.Dress(_skeleton, _garmentLook, _garb.Outfit, _top, armour, helm, _wear, _cut, _kin) : [];
    }

    /// <summary>How many kit pieces hang on this body (the self-test counts).</summary>
    public int GarmentCount => _garments.Count;

    /// <summary>Dresses (or dresses again) the body from its untouched mesh.</summary>
    void Redress(Garb garb)
    {
        if (_source is null || _mesh is null || _skeleton is null) return;
        _garb = garb;
        foreach (var old in _skeleton.GetChildren().OfType<MeshInstance3D>().Where(m => m.Name.ToString().StartsWith("BorrowedHair")).ToList()) { _skeleton.RemoveChild(old); old.QueueFree(); }
        Dress(_mesh, _skeleton, _source, garb);
        if (garb.Hair is { } style && style != "None" && style != Humans.HairOf(PeopleOf(_source), _sex, _stage)) BorrowHair(style);
    }

    /// <summary>Which people a body is, from its file name (kept in the mesh's name by the bake).</summary>
    string? PeopleOf(ArrayMesh mesh) => Humans.Peoples.FirstOrDefault(p => (_skeleton?.GetParent()?.Name.ToString() ?? "").StartsWith(p + "_") || mesh.ResourcePath.Contains($"/{p}_"));

    /// <summary>
    /// Hair of another style, borrowed from a body of the same sex that was made with it: its hair surfaces go on
    /// this skeleton with that body's own binding, so they sit where that body's head is, which is where this one's is.
    /// </summary>
    void BorrowHair(string style)
    {
        if (_skeleton is null || _garb is null || Humans.BodyWithHair(style, _sex, _stage) is not { } file || GD.Load<PackedScene>(file)?.Instantiate() is not Node3D donor) return;
        var mi = donor.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (mi?.Mesh is ArrayMesh source && mi.Skin is { } skin)
        {
            var hair = new ArrayMesh();
            for (var s = 1; s < source.GetSurfaceCount(); s++)
            {
                var tag = source.SurfaceGetMaterial(s)?.ResourceName ?? "";
                if (!tag.StartsWith("Hair")) continue;
                var bar = tag.IndexOf('|');
                var part = bar < 0 ? "" : tag[(bar + 1)..];
                if (part.Length > 4 && part[^4] == '_') part = part[..^4] + "." + part[^3..];
                var arrays = source.SurfaceGetArrays(s);
                hair.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: Weights(arrays));
                hair.SurfaceSetMaterial(hair.GetSurfaceCount() - 1, PartMaterial("Hair", part, _garb.Who));
            }
            if (hair.GetSurfaceCount() > 0)
            {
                var worn = new MeshInstance3D { Name = "BorrowedHair", Mesh = hair, Skin = skin, Transform = mi.Transform };
                _skeleton.AddChild(worn);
                worn.Skeleton = worn.GetPathTo(_skeleton);
            }
        }
        donor.Free();
    }

    /// <summary>Dragonkind's horns, from the top of a real head, hung from the head bone so they nod with it.</summary>
    void HornsHuman(Tristinwold.Gameplay.Avatar a)
    {
        if (a.Blood is not { } blood || _skeleton is null || _source is null || _skeleton.FindBone("Head") is var head && head < 0) return;
        var horns = a.Horns == "none" ? "Swept" : a.Horns;
        var slot = new BoneAttachment3D { BoneName = "Head" };
        _skeleton.AddChild(slot);
        var space = new Node3D { Name = "Adorn_Head", Transform = _skeleton.GetBoneGlobalRest(head).AffineInverse() };
        slot.AddChild(space);
        var rest = _skeleton.GetBoneGlobalRest(head).Origin;
        var verts = _source.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var top = verts.Length > 0 ? verts.Max(v => v.Y) : rest.Y + 0.2f;
        var crown = new Vector3(rest.X, rest.Y + (top - rest.Y) * 0.8f, rest.Z - 0.01f);
        var horn = new StandardMaterial3D { AlbedoColor = new Color(blood.Hex).Darkened(0.45f), Roughness = 0.5f, RimEnabled = true, Rim = 0.3f, RimTint = 0.65f };
        var size = top - rest.Y; // the head's height, roughly
        foreach (var side in new[] { -1f, 1f })
        {
            var root = crown + new Vector3(side * size * 0.42f, 0, 0);
            switch (horns)
            {
                case "Nubs": space.AddChild(Cone(root, new Vector3(side * 0.3f, 1, 0.1f), size * 0.35f, size * 0.12f, horn)); break;
                case "Long": space.AddChild(Cone(root, new Vector3(side * 0.25f, 1, -0.35f), size * 1.3f, size * 0.14f, horn)); break;
                case "Ram":
                    space.AddChild(Cone(root, new Vector3(side * 0.6f, 0.2f, -1), size * 0.7f, size * 0.16f, horn));
                    space.AddChild(Cone(root + new Vector3(side * size * 0.25f, -size * 0.2f, -size * 0.55f), new Vector3(side * 0.3f, -1, -0.1f), size * 0.6f, size * 0.13f, horn));
                    space.AddChild(Cone(root + new Vector3(side * size * 0.3f, -size * 0.7f, -size * 0.45f), new Vector3(side * 0.1f, -0.2f, 1), size * 0.5f, size * 0.1f, horn));
                    break;
                default: space.AddChild(Cone(root, new Vector3(side * 0.3f, 0.55f, -1), size * 1.1f, size * 0.14f, horn)); break;
            }
        }
    }

    /// <summary>
    /// Paints the clothes on: each vertex goes with the bone that moves it most, and the bone says what covers it.
    /// The body is rebuilt from its untouched mesh with vertex colours (clothes where the alpha is 1, skin where it's
    /// 0) and a skin material that reads them over the skin texture; the eyes, brows, lashes and hair that came with
    /// the body are dressed from their tags ("Hair-Bob01|hair-bob01__bob01.png") and tinted with the hair colour.
    /// </summary>
    static void Dress(MeshInstance3D mi, Skeleton3D skeleton, ArrayMesh source, Garb garb)
    {
        var (outfit, who, plain) = (garb.Outfit, garb.Who, garb.Plain);
        if (source.GetSurfaceCount() == 0) return;
        if (Debug) GD.Print($"dressing {mi.GetParent()?.GetParent()?.Name}: {source.GetSurfaceCount()} surfaces, skin {who.File ?? "plain"}, hair {garb.Hair ?? "own"}, materials {string.Join(", ", Enumerable.Range(0, source.GetSurfaceCount()).Select(i => source.SurfaceGetMaterial(i)?.ResourceName ?? "-"))}");
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
            "Head" when v.Y > headY + (top - headY) * 0.45f && (v.Z < -0.01f || v.Y > headY + (top - headY) * 0.8f) => outfit.Cap ?? (garb.Hair == "None" ? bare : who.Hair),
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
        painted.SurfaceSetMaterial(0, plain is null && who.File is { } file && Humans.Texture(file) is { } texture ? SkinMaterial(texture, who.Tint, who.Recolour, garb.Metal) : new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.62f, MetallicSpecular = 0.3f,
            SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.35f, SubsurfScatterSkinMode = true,
            RimEnabled = true, Rim = 0.2f, RimTint = 0.7f,
        });
        // The parts that came with the body: eyes, brows, lashes, hair.
        var ownHair = garb.Hair is null; // another style is borrowed, or none worn
        for (var s = 1; s < source.GetSurfaceCount(); s++)
        {
            var tag = source.SurfaceGetMaterial(s)?.ResourceName ?? "";
            var bar = tag.IndexOf('|');
            var (equip, part) = bar < 0 ? (tag, "") : (tag[..bar], tag[(bar + 1)..]);
            if (part.Length > 4 && part[^4] == '_') part = part[..^4] + "." + part[^3..]; // the glTF export made "eyeball.png" into "eyeball_png"
            if (equip.StartsWith("Hair") && (outfit.Cap is not null || !ownHair)) continue; // under a hood or a cap, or another style
            if (equip.Contains("Eye-") && who.Eyes.Length > 0) part = who.Eyes; // their own colour of eyes
            var part_arrays = source.SurfaceGetArrays(s);
            painted.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, part_arrays, flags: Weights(part_arrays));
            painted.SurfaceSetMaterial(painted.GetSurfaceCount() - 1, ClothMaterial(equip, part, outfit, garb.Metal) ?? PartMaterial(equip, part, who));
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

    /// <summary>
    /// Real clothes, where the body was baked with them (a shirt, trousers and shoes: see tools/humans): each is dyed
    /// in the outfit's colour over its own weave, the shirt in steel where the hero wears mail or plate. Null for a
    /// part that isn't clothing.
    /// </summary>
    static Material? ClothMaterial(string equip, string file, Humans.Outfit outfit, bool metal)
    {
        var colour = equip switch
        {
            _ when equip.StartsWith("Shirt") || equip.StartsWith("Jacket") || equip.StartsWith("Top") => outfit.Tunic,
            _ when equip.StartsWith("Pants") || equip.StartsWith("Trousers") || equip.StartsWith("Skirt") => outfit.Trousers,
            _ when equip.StartsWith("Shoes") || equip.StartsWith("Boots") => outfit.Boots,
            _ when equip.StartsWith("Hat") => outfit.Cap ?? outfit.Trousers,
            _ => (Color?)null,
        };
        if (colour is not { } dye) return null;
        var shirt = equip.StartsWith("Shirt") || equip.StartsWith("Jacket") || equip.StartsWith("Top");
        var shoes = equip.StartsWith("Shoes") || equip.StartsWith("Boots");
        // The clothes came with printed, modern textures (a logo on the shirt, denim); the dye goes over a plain weave instead. Leather keeps its grain.
        var texture = shoes ? Humans.Texture(file) : Weave;
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = shoes && texture is not null ? dye.Lightened(0.15f) : dye,
            Uv1Scale = shoes ? Vector3.One : new Vector3(6, 6, 1),
            Metallic = shirt && metal ? 0.85f : 0,
            Roughness = shirt && metal ? 0.4f : 0.88f,
            MetallicSpecular = shirt && metal ? 0.7f : 0.2f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, // cloth is seen from inside at a cuff or a hem
        };
    }

    static Texture2D? _weave;

    /// <summary>A plain cloth weave, made once: fine noise about a light grey, so a dye over it reads as the dye with a little thread in it.</summary>
    static Texture2D Weave => _weave ??= new NoiseTexture2D
    {
        Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.9f, FractalOctaves = 3 },
        ColorRamp = new Gradient { Offsets = [0f, 1f], Colors = [new Color(0.86f, 0.85f, 0.84f), new Color(0.97f, 0.96f, 0.95f)] },
    };

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
    static ShaderMaterial SkinMaterial(Texture2D skin, Color tint, float recolour, bool metal = false)
    {
        _skinShader ??= new Shader { Code = SkinCode };
        var m = new ShaderMaterial { Shader = _skinShader };
        m.SetShaderParameter("skin", skin);
        m.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        m.SetShaderParameter("recolour", recolour);
        m.SetShaderParameter("metal", metal ? 1f : 0f);
        return m;
    }

    const string SkinCode = """
        shader_type spatial;
        uniform sampler2D skin : source_color, filter_linear_mipmap_anisotropic;
        uniform vec3 tint = vec3(1.0);
        uniform float recolour = 0.0;
        uniform float metal = 0.0;
        void fragment() {
            vec4 t = texture(skin, UV);
            float cloth = COLOR.a;
            vec3 paint = pow(COLOR.rgb, vec3(2.2));
            float lum = dot(t.rgb, vec3(0.3, 0.59, 0.11));
            vec3 flesh = mix(t.rgb * tint, vec3(lum) * 2.2 * tint, recolour); // recoloured skin keeps the texture's shading
            ALBEDO = mix(flesh, paint, cloth);
            ROUGHNESS = mix(0.5, mix(0.85, 0.35, metal), cloth);
            SPECULAR = mix(0.4, mix(0.2, 0.6, metal), cloth);
            METALLIC = metal * cloth;
            SSS_STRENGTH = 0.35 * (1.0 - cloth);
            RIM = 0.15;
            RIM_TINT = 0.7;
        }
        """;
}

/// <summary>
/// A real body's head turns to whoever is near (the hero, mostly): the head bone is swung toward them, no more than a
/// fair turn of the neck, eased in and out, on top of whatever its clip is doing. Runs after the animation, as a
/// skeleton modifier. The head faces +Z in its own frame (the bodies face +Z).
/// </summary>
public partial class HumanGaze : SkeletonModifier3D
{
    /// <summary>Degrees the head is tipped forward from its clip, always: the clips look up on a real neck.</summary>
    public const float Level = 22; // positive tips the face down: the clips hold a real head tipped back about so much
    public Node3D? Watching { get; set; }
    public float Range { get; set; } = 7;
    int _head = -1;
    Quaternion _turn = Quaternion.Identity;
    float _gazing;
    Node3D? _body;

    public override void _Ready()
    {
        if (GetSkeleton() is { } sk) _head = sk.FindBone("Head");
        _body = GetParent()?.GetParent() as Node3D;
    }

    public override void _ProcessModification()
    {
        if (_head < 0 || GetSkeleton() is not { } sk) return;
        var who = Watching ?? Poise.Hero;
        var want = 0f;
        var goal = Quaternion.Identity;
        var pose = sk.GetBoneGlobalPose(_head);
        var toWorld = sk.GlobalTransform;
        if (who is not null && IsInstanceValid(who) && _body is not null && who != _body)
        {
            var eye = toWorld * pose.Origin;
            var to = who.GlobalPosition + Vector3.Up * 1.5f - eye;
            var ahead = toWorld.Basis * (pose.Basis * Vector3.Back); // where the face points now
            if (to.Length() < Range && to.Normalized().Dot(ahead.Normalized()) > -0.1f)
            {
                want = 1;
                var local = (pose.Basis.Inverse() * (toWorld.Basis.Inverse() * to)).Normalized(); // into the head's own frame
                var forward = Vector3.Back;
                var angle = forward.AngleTo(local);
                if (angle > 0.01f) goal = new Quaternion(forward.Cross(local).Normalized(), Mathf.Min(angle, Mathf.DegToRad(55)));
            }
        }
        _gazing = Mathf.MoveToward(_gazing, want, 0.03f);
        _turn = _turn.Normalized().Slerp(goal.Normalized(), 0.1f).Normalized(); // a little drift each frame would otherwise add up to an error
        var turn = Quaternion.Identity.Slerp(_turn, _gazing);
        // KayKit's clips tip a real head back a little (they were made for its big round heads): brought level, then turned.
        var level = new Quaternion(Vector3.Right, Mathf.DegToRad(Level));
        var parent = sk.GetBoneParent(_head);
        var parentBasis = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        var turned = pose.Basis * new Basis(level * turn);
        sk.SetBonePoseRotation(_head, (parentBasis.Inverse() * turned).GetRotationQuaternion());
    }
}
