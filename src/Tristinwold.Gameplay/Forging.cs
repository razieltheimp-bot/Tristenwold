using System.Collections.Concurrent;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>One thing that can go in one socket of a forged piece: what it adds, how it looks, and what it calls the piece if it's the best thing about it.</summary>
public sealed record ForgePart(string Name, string Prefix, double Damage = 0, double Armor = 0, double Speed = 0, double Crit = 0, double Health = 0,
    double Value = 0, double Length = 1, double Width = 1, string? Tint = null);

/// <summary>A forge, and what working at it does to the math: its plus, what it leans to, and the fire it favours.</summary>
public sealed record ForgeHouse(string Id, string Name, int Plus, double Damage = 0, double Armor = 0, double Speed = 0, double Crit = 0, double Health = 0, DamageKind? Fire = null);

/// <summary>
/// Something more than numbers that a piece can come out with. <paramref name="Key"/> is what the game adds up; the value
/// is rolled between <paramref name="Low"/> and <paramref name="High"/>, or is a die if <paramref name="Dice"/> is set.
/// </summary>
public sealed record ForgeAbility(string Key, string Title, double Low, double High, string Says, bool Weapon = false, bool LightOnly = false, bool HeavyOnly = false, bool Dice = false);

/// <summary>How good a forged piece came out, against an ordinary one of its kind.</summary>
public enum ForgeGrade { Common, Fine, Masterwork, Legendary }

/// <summary>
/// Every weapon and piece of armour a hero makes is one of a kind. It rolls a part for each of its five sockets, a fire
/// (an element), and a quality; the forge adds its plus and leanings; the smith's skill and the hero's level go in too.
/// The piece's id is its serial: the base item, a random seed and all of the above, so the whole of it can be rebuilt
/// from those few characters on any machine, and packs, saves, shops and the server carry it like any other item.
/// </summary>
public static class Forging
{
    /// <summary>Marks a forged id, and the version of these tables it was rolled on.</summary>
    public const string Mark = "forged1";
    const char Cut = '~';

    /// <summary>How far one roll can swing a piece's main stat, and how far all of it together can take it from the ordinary.</summary>
    public const double Swing = 0.08, Lowest = 0.7, Highest = 1.6, FireResist = 10;
    public const double PerPlus = 0.03, PerSmithLevel = 0.01, PerHeroLevel = 0.004;
    public const double FireChance = 0.3;
    /// <summary>The chance any weapon blow only glances, for half its damage; each point of plus to hit takes a point off it.</summary>
    public const double GlanceChance = 10, GlanceDamage = 0.5;
    public const int MostToHit = 8;

    /// <summary>
    /// The die a weapon's fire rolls on every blow, on top of the weapon's own damage: a d6 most often, a d12 seldom,
    /// and a forge's plus moves the odds up. <paramref name="luck"/> is a roll from 0 to 1.
    /// </summary>
    public static int FireDie(double luck, int plus) => (luck + plus * 0.03) switch { >= 0.95 => 12, >= 0.8 => 10, >= 0.5 => 8, _ => 6 };

    /// <summary>The chance, from 0 to 1, that a blow glances for someone with this much plus to hit.</summary>
    public static double Glances(int toHit) => Math.Max(0, GlanceChance - toHit) / 100;

    public static readonly DamageKind[] Fires = [DamageKind.Fire, DamageKind.Frost, DamageKind.Shock, DamageKind.Poison, DamageKind.Sun];

    public static string FireTint(DamageKind fire) => fire switch
    {
        DamageKind.Fire => "#ff6a20", DamageKind.Frost => "#70d0ff", DamageKind.Shock => "#c0a0ff", DamageKind.Poison => "#70e040", _ => "#ffe080",
    };

    static string FireWord(DamageKind fire) => fire switch
    {
        DamageKind.Fire => "Burning", DamageKind.Frost => "Frostbound", DamageKind.Shock => "Stormstruck", DamageKind.Poison => "Venomed", _ => "Sunlit",
    };

    /// <summary>A weapon's sockets: what it cuts with, what stops a blade reaching the hand, what the hand holds, what balances it, and how it was finished.</summary>
    public static readonly IReadOnlyList<(string Socket, ForgePart[] Parts)> WeaponSockets =
    [
        ("Edge",
        [
            new("Plain edge", "Plain"),
            new("Long edge", "Reaching", Damage: 0.04, Speed: -0.01, Length: 1.14, Width: 0.95),
            new("Broad edge", "Broad", Damage: 0.07, Speed: -0.02, Length: 1.02, Width: 1.3),
            new("Thin edge", "Keen", Damage: 0.02, Crit: 3, Length: 1.06, Width: 0.76, Tint: "#d8dee8"),
            new("Serrated edge", "Toothed", Damage: 0.05, Crit: 1, Width: 1.1, Tint: "#9aa2ac"),
            new("Short edge", "Quick", Damage: -0.02, Speed: 0.04, Length: 0.84),
            new("Heavy edge", "Crushing", Damage: 0.1, Speed: -0.04, Length: 1.05, Width: 1.42, Tint: "#6f747c"),
            new("Blackened edge", "Grim", Damage: 0.05, Crit: 2, Tint: "#3a3a42"),
            new("Curved edge", "Curved", Damage: 0.03, Speed: 0.015, Length: 1.04, Width: 0.9),
            new("Double edge", "Twin-Edged", Damage: 0.045, Crit: 1, Width: 1.08),
            new("Waved edge", "Flamelike", Damage: 0.035, Crit: 1.5, Width: 1.16, Tint: "#b8c0cc"),
            new("Hollow-ground edge", "Razor", Crit: 4, Damage: -0.01, Width: 0.7, Tint: "#e0e4ea"),
        ]),
        ("Guard",
        [
            new("Bare guard", "Bare", Width: 0.5),
            new("Straight guard", "Sturdy", Armor: 3, Width: 1),
            new("Swept guard", "Swept", Speed: 0.015, Width: 1.3, Tint: "#b8a070"),
            new("Basket guard", "Guarded", Armor: 6, Speed: -0.01, Width: 1.1, Tint: "#8a8f98"),
            new("Hooked guard", "Hooked", Crit: 1.5, Width: 1.5, Tint: "#6a6a74"),
            new("Ring guard", "Ringed", Health: 0.01, Width: 0.9, Tint: "#c8a040"),
            new("Spiked guard", "Spiked", Damage: 0.03, Width: 1.6, Tint: "#4a4a52"),
            new("Wide guard", "Wide", Armor: 4, Speed: -0.005, Width: 1.9, Tint: "#7a6048"),
            new("Lugged guard", "Lugged", Damage: 0.015, Armor: 2, Width: 1.2),
            new("Disc guard", "Round-Guarded", Armor: 5, Width: 0.8, Tint: "#9aa0a8"),
            new("Winged guard", "Winged", Speed: 0.02, Crit: 0.5, Width: 1.75, Tint: "#c8ccd4"),
            new("Thumb-ring guard", "Thumbed", Crit: 2, Width: 0.7, Tint: "#8a7a50"),
        ]),
        ("Grip",
        [
            new("Leather grip", "Honest", Tint: "#5a3e28"),
            new("Wire grip", "Wired", Crit: 1, Tint: "#b0b4bc"),
            new("Ray-skin grip", "Sure", Speed: 0.02, Tint: "#d0ccb8"),
            new("Oak grip", "Oaken", Damage: 0.015, Tint: "#7a5a38"),
            new("Long grip", "Levered", Damage: 0.03, Speed: -0.01, Length: 1.03, Tint: "#4a3628"),
            new("Short grip", "Close", Speed: 0.025, Length: 0.97, Tint: "#3a2a20"),
            new("Bone grip", "Boned", Crit: 2, Tint: "#e0d8c0"),
            new("Silk-wrapped grip", "Silken", Speed: 0.03, Damage: -0.01, Tint: "#7a2a3a"),
            new("Cord grip", "Corded", Speed: 0.01, Crit: 0.5, Tint: "#8a7a5a"),
            new("Sharkskin grip", "Rough-Bound", Crit: 1.5, Speed: 0.01, Tint: "#5a6a70"),
            new("Horn grip", "Horned", Damage: 0.02, Crit: 0.5, Tint: "#2a2622"),
            new("Blackthorn grip", "Thorned", Damage: 0.025, Speed: 0.005, Tint: "#1e1a18"),
        ]),
        ("Pommel",
        [
            new("Plain pommel", "Balanced"),
            new("Lead pommel", "Weighted", Damage: 0.025, Speed: -0.01, Tint: "#5a5a60"),
            new("Brass pommel", "Brassbound", Health: 0.01, Tint: "#c09a44"),
            new("Spiked pommel", "Vicious", Crit: 1.5, Tint: "#3a3a42"),
            new("Hollow pommel", "Light", Speed: 0.02, Tint: "#a8acb4"),
            new("Disc pommel", "Steady", Armor: 2, Tint: "#8a8f98"),
            new("Wheel pommel", "Wheeled", Damage: 0.015, Crit: 0.5, Tint: "#6a6f78"),
            new("Stone pommel", "Stone-Set", Health: 0.015, Speed: -0.005, Tint: "#7a3030"),
            new("Fishtail pommel", "Fishtailed", Speed: 0.015, Crit: 0.5, Tint: "#9a9ea6"),
            new("Skull pommel", "Death-Headed", Damage: 0.02, Crit: 1, Tint: "#d8d0b8"),
            new("Silver pommel", "Silver-Set", Value: 0.2, Health: 0.005, Tint: "#c8ccd4"),
            new("Claw pommel", "Clawed", Crit: 2, Speed: -0.005, Tint: "#3a3028"),
        ]),
        ("Finish",
        [
            new("Plain finish", "Workmanlike"),
            new("Oiled finish", "Oiled", Value: 0.05),
            new("Etched finish", "Etched", Crit: 1, Value: 0.1),
            new("Quenched finish", "Hard", Damage: 0.03, Value: 0.05),
            new("Tempered finish", "Tempered", Damage: 0.015, Armor: 2, Value: 0.05),
            new("Inlaid finish", "Inlaid", Value: 0.25, Tint: "#d0b060"),
            new("Blued finish", "Blued", Speed: 0.015, Value: 0.1, Tint: "#3a4a6a"),
            new("Folded finish", "Folded", Damage: 0.04, Speed: -0.005, Value: 0.15),
            new("Acid-etched finish", "Patterned", Crit: 1.5, Value: 0.12),
            new("Twice-tempered finish", "Twice-Tempered", Damage: 0.035, Speed: 0.005, Value: 0.1),
            new("Charred finish", "Charred", Damage: 0.02, Crit: 1, Tint: "#2e2a2a"),
            new("Mirror finish", "Mirrored", Speed: 0.02, Value: 0.2, Tint: "#e8ecf2"),
        ]),
    ];

    /// <summary>Armour's sockets: the shell that takes the blow, what's under it, how it's held on, its edging, and how it was finished.</summary>
    public static readonly IReadOnlyList<(string Socket, ForgePart[] Parts)> ArmorSockets =
    [
        ("Shell",
        [
            new("Plain shell", "Plain"),
            new("Thick shell", "Thick", Armor: 0.08, Speed: -0.015),
            new("Layered shell", "Layered", Armor: 0.1, Speed: -0.02, Tint: "#8f949c"),
            new("Thin shell", "Light", Armor: -0.03, Speed: 0.025, Tint: "#c0c4cc"),
            new("Ribbed shell", "Ribbed", Armor: 0.06, Tint: "#7a7f88"),
            new("Scaled shell", "Scaled", Armor: 0.05, Health: 0.01, Tint: "#6a7a70"),
            new("Studded shell", "Studded", Armor: 0.07, Speed: -0.005, Tint: "#5a4a3a"),
            new("Blackened shell", "Grim", Armor: 0.04, Crit: 1, Tint: "#35353c"),
            new("Banded shell", "Banded", Armor: 0.09, Speed: -0.012, Tint: "#858a92"),
            new("Boiled shell", "Boiled", Armor: 0.03, Speed: 0.01, Tint: "#6a4a30"),
            new("Fluted shell", "Fluted", Armor: 0.05, Speed: 0.005, Tint: "#b0b4bc"),
            new("Horn-plated shell", "Horn-Plated", Armor: 0.07, Health: 0.005, Tint: "#3a3228"),
        ]),
        ("Lining",
        [
            new("Linen lining", "Lined"),
            new("Wool lining", "Warm", Health: 0.01),
            new("Felt lining", "Padded", Armor: 0.03),
            new("Silk lining", "Silken", Speed: 0.015),
            new("Fur lining", "Furred", Health: 0.015, Speed: -0.005),
            new("Mail lining", "Mailed", Armor: 0.06, Speed: -0.01),
            new("Leather lining", "Hide-Backed", Armor: 0.02, Health: 0.005),
            new("Quilted lining", "Quilted", Armor: 0.03, Health: 0.005),
            new("Moss lining", "Mossy", Health: 0.012, Speed: 0.003),
            new("Linked lining", "Linked", Armor: 0.04, Health: 0.005),
            new("Spidersilk lining", "Gossamer", Speed: 0.02, Armor: -0.005),
            new("Sheepskin lining", "Fleeced", Health: 0.018, Speed: -0.003),
        ]),
        ("Strapping",
        [
            new("Leather straps", "Strapped"),
            new("Buckled straps", "Buckled", Armor: 0.015),
            new("Laced straps", "Laced", Speed: 0.01),
            new("Riveted straps", "Riveted", Armor: 0.03, Speed: -0.005),
            new("Sliding straps", "Supple", Speed: 0.02, Armor: -0.01),
            new("Chain straps", "Chained", Armor: 0.04, Speed: -0.01),
            new("Broad straps", "Broad-Belted", Health: 0.01),
            new("Knotted straps", "Knotted", Crit: 0.5),
            new("Hooked straps", "Hooked", Speed: 0.012, Armor: 0.005),
            new("Studded straps", "Stud-Strapped", Armor: 0.025),
            new("Wide straps", "Well-Hung", Armor: 0.01, Health: 0.006),
            new("Quick-release straps", "Quick-Shed", Speed: 0.018, Health: -0.002),
        ]),
        ("Trim",
        [
            new("No trim", "Unadorned"),
            new("Brass trim", "Brass-Trimmed", Value: 0.1, Tint: "#c09a44"),
            new("Silver trim", "Silvered", Value: 0.2, Tint: "#c8ccd4"),
            new("Copper trim", "Coppered", Value: 0.08, Tint: "#b86434"),
            new("Black trim", "Dark", Crit: 0.5, Tint: "#2a2a30"),
            new("Red trim", "Crimson", Health: 0.005, Tint: "#8a2020"),
            new("Blue trim", "Azure", Speed: 0.005, Tint: "#2a4a8a"),
            new("Gilt trim", "Gilded", Value: 0.3, Tint: "#d8b040"),
            new("Fur trim", "Fur-Edged", Health: 0.008, Tint: "#6a5a48"),
            new("Green trim", "Verdant", Value: 0.05, Tint: "#2f5a34"),
            new("Bone trim", "Bone-Edged", Crit: 0.8, Tint: "#d8d0b8"),
            new("Violet trim", "Royal", Value: 0.25, Tint: "#5a3a7a"),
        ]),
        ("Finish",
        [
            new("Plain finish", "Workmanlike"),
            new("Oiled finish", "Oiled", Value: 0.05),
            new("Waxed finish", "Waxed", Armor: 0.015),
            new("Burnished finish", "Burnished", Armor: 0.02, Value: 0.1),
            new("Etched finish", "Etched", Value: 0.15),
            new("Hardened finish", "Hardened", Armor: 0.05, Speed: -0.005),
            new("Lacquered finish", "Lacquered", Speed: 0.01, Value: 0.1),
            new("Runed finish", "Runed", Health: 0.015, Value: 0.2),
            new("Scoured finish", "Scoured", Speed: 0.006, Armor: 0.01),
            new("Blackened finish", "Sooted", Armor: 0.025, Crit: 0.5),
            new("Case-hardened finish", "Case-Hardened", Armor: 0.04, Value: 0.05),
            new("Blessed finish", "Blessed", Health: 0.01, Armor: 0.01, Value: 0.15),
        ]),
    ];

    /// <summary>What armour can come out able to do. The light and the heavy each have their own.</summary>
    public static readonly IReadOnlyList<ForgeAbility> Abilities =
    [
        new("health", "the Stout Heart", 0.02, 0.08, "+{0:P0} health"),
        new("dodge", "the Slip", 2, 8, "{0:0.#}% harder to hit"),
        new("bulwark", "the Bulwark", 3, 12, "+{0:0} armour"),
        new("regen", "Mending", 0.2, 0.8, "heals {0:0.#}% of your health a second"),
        new("thorns", "Thorns", 0.05, 0.15, "turns {0:P0} of each blow back on whoever struck it"),
        new("mana", "the Wellspring", 0.05, 0.2, "+{0:P0} mana"),
        new("stamina", "the Long Road", 0.05, 0.2, "+{0:P0} stamina"),
        new("ward", "Warding", 3, 10, "+{0:0} magic resistance"),
        new("crit", "Fortune", 1, 4, "+{0:0.#}% critical"),
        new("jump", "the Featherstep", 0.2, 1, "jump up to {0:P0} higher", LightOnly: true),
        new("speed", "the Fleet", 0.02, 0.06, "+{0:P0} speed", LightOnly: true),
        new("block", "the Mountain", 0.02, 0.06, "takes {0:P0} less from every blow", HeavyOnly: true),
        new("deflect", "the Turned Blade", 4, 12, "{0:0.#}% of blows glance off you for half", HeavyOnly: true),
        // Rune expulsion: struck, the armour lets go what's cut into it, and everything near is blasted back.
        new("rune_fire", "the Flame Rune", 6, 12, "rune expulsion: blasts foes away with 3d{0:0} flame", Dice: true),
        new("rune_shock", "the Storm Rune", 6, 12, "rune expulsion: blasts foes away with 3d{0:0} lightning", Dice: true),
        new("rune_frost", "the Ice Rune", 6, 12, "rune expulsion: blasts foes away with 3d{0:0} ice", Dice: true),
        new("rune_sun", "the Sun Rune", 6, 12, "rune expulsion: blasts foes away with 3d{0:0} holy light", Dice: true),
        new("rune_poison", "the Blight Rune", 6, 12, "rune expulsion: blasts foes away with 3d{0:0} venom", Dice: true),
        // And what a weapon can do past its own damage: the dice roll on every blow.
        new("drain", "the Leech", 4, 8, "heals you d{0:0} on every blow", Weapon: true, Dice: true),
        new("bleed", "the Red Hand", 4, 10, "plus d{0:0} bleeding damage", Weapon: true, Dice: true),
        new("stun", "the Hammerfall", 0.03, 0.1, "{0:P0} chance to stun", Weapon: true),
        new("weapon_crit", "the Open Vein", 1, 5, "+{0:0.#}% critical", Weapon: true),
        new("weapon_speed", "the Swift Hand", 0.02, 0.06, "+{0:P0} speed", Weapon: true),
    ];

    /// <summary>The runes armour can be cut with, what each lets go, how far it throws, and how long it takes to gather again.</summary>
    public static readonly IReadOnlyList<(string Key, DamageKind Kind)> Runes =
        [("rune_fire", DamageKind.Fire), ("rune_shock", DamageKind.Shock), ("rune_frost", DamageKind.Frost), ("rune_sun", DamageKind.Sun), ("rune_poison", DamageKind.Poison)];
    public const double RuneReach = 5, RuneSeconds = 14;
    public const float RuneThrow = 5;

    /// <summary>The most any of these can add up to across everything worn.</summary>
    public const double MostDeflect = 35, MostDodge = 30, MostJump = 1, MostBlock = 0.2, MostThorns = 0.3, MostRegen = 2;

    /// <summary>How many abilities a piece comes out with: more often none, seldom three.</summary>
    static int HowMany(double luck) => luck switch { >= 0.95 => 3, >= 0.75 => 2, >= 0.4 => 1, _ => 0 };

    /// <summary>The die an ability rolls: the same steps as a fire, up from a d4.</summary>
    static int DieIn(double low, double high, double luck) => (int)(low + 2 * Math.Floor(((high - low) / 2 + 1) * Math.Min(luck, 0.999)));

    /// <summary>How many different pieces one base item can be by its parts alone, before the fire and the rolls.</summary>
    public static long PartCombinations(bool weapon) => (weapon ? WeaponSockets : ArmorSockets).Aggregate(1L, (n, s) => n * s.Parts.Length);

    /// <summary>The forge on Soren's flagship: what comes off it is only ever taken from him.</summary>
    public const string RedReckoning = "ship:red_reckoning";

    /// <summary>The named forges, each with its own leanings. Anywhere else with a smith gets one of its own, made from its name.</summary>
    public static readonly IReadOnlyList<ForgeHouse> Forges =
    [
        new(RedReckoning, "The Red Reckoning", 3, Damage: 0.04, Armor: 0.04, Crit: 2),
        new("city:tristinwold", "The Anvil Gate", 2, Damage: 0.03, Armor: 0.03),
        new("city:druitt", "The Sawpit Forge", 1, Damage: 0.05, Speed: -0.01),
        new("city:greywater", "The Orchard Forge", 1, Speed: 0.02, Health: 0.01, Fire: DamageKind.Poison),
        new("city:hollowheart", "The Heartwood Hearth", 2, Health: 0.02, Crit: 1, Fire: DamageKind.Sun),
        new("city:galeholt", "The Windward Anvil", 2, Speed: 0.03, Fire: DamageKind.Shock),
        new("city:vespergate", "The Dusk Foundry", 2, Crit: 2, Fire: DamageKind.Poison),
        new("city:cinderhelm", "The Ashen Crucible", 3, Damage: 0.05, Fire: DamageKind.Fire),
        new("city:argentdeep", "The Silver Deep", 3, Armor: 0.06, Fire: DamageKind.Frost),
    ];

    /// <summary>The forge at a place: a named one, or one of its own, fixed by the place's name.</summary>
    public static ForgeHouse ForgeAt(string place, string? placeName = null)
    {
        if (Forges.FirstOrDefault(f => f.Id == place) is { } known) return known;
        var rng = new Rng(Hash(place));
        var name = $"The {placeName ?? "Road"} Forge";
        return rng.Next(5) switch
        {
            0 => new(place, name, 1, Damage: 0.02 + rng.NextDouble() * 0.03),
            1 => new(place, name, 1, Armor: 0.02 + rng.NextDouble() * 0.03),
            2 => new(place, name, 1, Speed: 0.01 + rng.NextDouble() * 0.02),
            3 => new(place, name, 1, Crit: 1 + rng.NextDouble() * 1.5),
            _ => new(place, name, 1, Health: 0.01 + rng.NextDouble() * 0.015, Fire: Fires[rng.Next(Fires.Length)]),
        };
    }

    static ulong Hash(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text) hash = (hash ^ c) * 1099511628211UL;
        return hash;
    }

    /// <summary>Whether a recipe's result comes out one of a kind: anything worn, made at a forge or a bench.</summary>
    public static bool Unique(Recipe r) => r.Job is Job.Smithing or Job.Crafting && r.Count == 1 && r.Item.Wearable;

    /// <summary>The serial of a new piece. The seed's what makes it different from every other piece ever made.</summary>
    public static string Serial(string baseId, ulong seed, string forge, string forgeName, int smith, int hero, string maker) =>
        string.Join(Cut, Mark, baseId, seed.ToString("x16"), forge, Clean(forgeName), smith, hero, Clean(maker));

    static string Clean(string text)
    {
        var plain = new string(text.Where(c => c != Cut && !char.IsControl(c)).ToArray()).Trim();
        return plain.Length > 32 ? plain[..32] : plain;
    }

    public static bool IsForged(string id) => id.StartsWith(Mark + Cut);

    /// <summary>The ordinary item a forged piece was made from (or the item itself).</summary>
    public static string BaseOf(string id) => IsForged(id) && id.Split(Cut) is { Length: > 1 } parts ? parts[1] : id;

    static readonly ConcurrentDictionary<string, Item?> Rolled = new();

    /// <summary>The piece a serial stands for, the same on every machine; null if it isn't a good one.</summary>
    public static Item? Resolve(string id) => IsForged(id) ? Rolled.GetOrAdd(id, Roll) : null;

    static Item? Roll(string id)
    {
        var bits = id.Split(Cut);
        if (bits.Length != 8 || !ulong.TryParse(bits[2], System.Globalization.NumberStyles.HexNumber, null, out var seed)
            || !int.TryParse(bits[5], out var smith) || !int.TryParse(bits[6], out var hero)) return null;
        if (Items.Get(bits[1]) is not { Wearable: true } plain || IsForged(bits[1])) return null;
        var forge = ForgeAt(bits[3], bits[4].StartsWith("The ") && bits[4].EndsWith(" Forge") ? bits[4][4..^6] : null);
        var rng = new Rng(seed);
        var weapon = plain.Slot == Slot.Weapon;
        var chosen = (weapon ? WeaponSockets : ArmorSockets).Select(s => (s.Socket, Part: s.Parts[rng.Next(s.Parts.Length)])).ToList();
        var parts = chosen.Select(c => c.Part).ToList();

        // The fire: the forge's own, more often than not, if it has one.
        DamageKind? fire = rng.Chance(forge.Fire is null ? FireChance : FireChance * 1.5)
            ? forge.Fire is { } leaning && rng.Chance(0.65) ? leaning : Fires[rng.Next(Fires.Length)]
            : null;

        // Everything that goes into its main stat: the parts, the forge's plus and leanings, the smith, the hero, and the roll.
        var shared = forge.Plus * PerPlus + Math.Min(smith, 10) * PerSmithLevel + Math.Min(hero, 50) * PerHeroLevel;
        double Scale(double parted, double leaning) => Math.Clamp((1 + parted + leaning + shared) * (1 + (rng.NextDouble() * 2 - 1) * Swing), Lowest, Highest);
        var damageBy = Scale(parts.Sum(p => p.Damage), forge.Damage);
        var armorBy = Scale(weapon ? 0 : parts.Sum(p => p.Armor), forge.Armor);
        var speed = Math.Round(Math.Clamp(parts.Sum(p => p.Speed) + forge.Speed + (rng.NextDouble() - 0.5) * 0.01, -0.08, 0.08), 4);
        var crit = Math.Round(Math.Clamp(parts.Sum(p => p.Crit) + forge.Crit + rng.NextDouble(), 0, 8), 2);
        var health = Math.Round(Math.Clamp(parts.Sum(p => p.Health) + forge.Health + rng.NextDouble() * 0.005, 0, 0.08), 4);
        var flatArmor = weapon ? parts.Sum(p => p.Armor) * (0.8 + rng.NextDouble() * 0.4) : 0;
        // A weapon's plus to hit comes of how it handles; its plus damage of what it's made of, on top of the share above.
        var toHit = weapon ? Math.Clamp(rng.Next(3) + parts.Count(p => p.Speed > 0 || p.Crit > 0) + forge.Plus / 2, 0, MostToHit) : 0;
        var power = weapon ? (int)Math.Round(1 + rng.NextDouble() * 2 + plain.Damage * 10 * (0.5 + rng.NextDouble() * 0.5) + parts.Count(p => p.Damage > 0) * 0.5 + forge.Plus * 0.5) : 0;

        var die = weapon && fire is not null ? FireDie(rng.NextDouble(), forge.Plus) : 0;
        // Anything more than numbers. Light things and heavy things each have what suits them.
        var light = !plain.Metal;
        var pool = Abilities.Where(a => a.Weapon == weapon && (!a.LightOnly || light) && (!a.HeavyOnly || !light)).ToList();
        var powers = new Dictionary<string, double>();
        var saying = new List<string>();
        string? title = null;
        for (var n = HowMany(rng.NextDouble()); n > 0 && pool.Count > 0; n--)
        {
            var ability = pool[rng.Next(pool.Count)];
            pool.Remove(ability);
            var value = ability.Dice ? DieIn(ability.Low, ability.High, rng.NextDouble()) : Math.Round(ability.Low + rng.NextDouble() * (ability.High - ability.Low), 4);
            powers[ability.Key] = value;
            saying.Add(string.Format(ability.Says, value));
            title ??= ability.Title;
        }
        var damage = Math.Round(plain.Damage * damageBy, 4);
        var armor = Math.Round(plain.Armor * armorBy + flatArmor + powers.GetValueOrDefault("bulwark"), 1);

        // Named for the best thing about it, that the plainest parts don't get to be.
        var main = weapon ? damageBy : armorBy;
        var grade = main >= 1.34 ? ForgeGrade.Legendary : main >= 1.2 ? ForgeGrade.Masterwork : main >= 1.08 ? ForgeGrade.Fine : ForgeGrade.Common;
        var best = parts.Skip(1).Prepend(parts[0]).OrderByDescending(Score).First();
        var fireWord = fire is { } f ? FireWord(f) + " " : "";
        var plus = forge.Plus > 0 ? $" +{forge.Plus}" : "";
        var name = $"{best.Prefix} {fireWord}{plain.Name}{plus}{(title is null ? "" : $" of {title}")}";
        var numberBits = bits[2][..4].ToUpperInvariant() + "-" + bits[2][4..8].ToUpperInvariant();

        var stats = new List<string>();
        if (toHit > 0) stats.Add($"+{toHit} to hit");
        if (power > 0) stats.Add($"+{power} damage");
        if (damage > 0) stats.Add($"+{damage:P1} might");
        if (armor > 0) stats.Add($"+{armor:0.#} armour");
        if (Math.Abs(speed) >= 0.0005) stats.Add($"{speed:+0.0%;-0.0%} speed");
        if (crit >= 0.05) stats.Add($"+{crit:0.#}% critical");
        if (health >= 0.0005) stats.Add($"+{health:P1} health");
        if (fire is { } hurt) stats.Add(weapon ? $"plus d{die} {hurt.ToString().ToLowerInvariant()} damage" : $"{FireResist:0}% {hurt.ToString().ToLowerInvariant()} resistance");
        stats.AddRange(saying);
        var text = $"{grade} work. {string.Join(", ", stats)}. {string.Join(", ", parts.Select(p => p.Name.ToLowerInvariant()))}. Forged by {bits[7]} at {forge.Name}. No. {numberBits}.";

        return plain with
        {
            Id = id, Name = name, Text = text, Damage = damage, Armor = armor, Speed = speed, Crit = crit, Health = health, Fire = fire, Forged = true, ToHit = toHit, Power = power, FireDie = die, Powers = powers,
            Value = Math.Max(1, (int)Math.Round(plain.Value * main * (1 + parts.Sum(p => p.Value)))),
            Tint = parts[0].Tint ?? (plain.Slot == Slot.Weapon ? plain.Tint : plain.Tint ?? parts[3].Tint),
            Glow = fire is { } lit ? FireTint(lit) : plain.Glow, Metal = plain.Metal || weapon,
            Length = (float)(parts[0].Length * parts[2].Length), Width = (float)parts[0].Width,
            GuardWidth = weapon ? (float)parts[1].Width : 0, GuardTint = weapon ? parts[1].Tint : parts[3].Tint,
            PommelTint = weapon ? parts[3].Tint : null, GripTint = weapon ? parts[2].Tint : null,
            Grade = grade, Parts = parts.Select(p => p.Name).ToArray(),
        };
    }

    /// <summary>How much a part stands out, so the name goes to what it's really known for.</summary>
    static double Score(ForgePart p) => Math.Abs(p.Damage) * 10 + Math.Abs(p.Armor) * 10 + Math.Abs(p.Speed) * 20 + p.Crit * 0.5 + p.Health * 40 + p.Value * 3;
}
