using System.Text.Json;

namespace Tristinwold.Deviant;

/// <summary>
/// Names, epithets and taunts. Ships with a default set; load your own JSON to
/// give the deviants your setting's flavour (Tong crews, Sin-touched, and so on).
/// </summary>
public sealed class NamePack
{
    public List<string> FirstNames { get; init; } = [];
    public List<string> NameSuffixes { get; init; } = [];
    public List<string> Epithets { get; init; } = [];
    /// <summary>Epithets earned by surviving a particular kind of death.</summary>
    public Dictionary<DamageKind, List<string>> ScarEpithets { get; init; } = [];
    public Dictionary<DamageKind, List<string>> ScarDescriptions { get; init; } = [];
    /// <summary>Taunt lines keyed by situation. {player} and {name} are filled in.</summary>
    public Dictionary<string, List<string>> Taunts { get; init; } = [];

    public static NamePack Load(string path) =>
        JsonSerializer.Deserialize<NamePack>(File.ReadAllText(path), DeviantJson.Options)
        ?? throw new InvalidDataException($"Empty name pack: {path}");

    public static NamePack Default { get; } = new()
    {
        FirstNames =
        [
            "Grask", "Vorn", "Mok", "Ulka", "Thessa", "Brakk", "Ruun", "Skarra", "Dorv", "Ixa",
            "Holt", "Marrow", "Kesh", "Vael", "Gorrim", "Snik", "Yorra", "Tusk", "Corwen", "Ashk",
            "Silt", "Brine", "Hask", "Juro", "Nim", "Oskar", "Pell", "Quill", "Rhok", "Zeth",
        ],
        NameSuffixes = ["", "", "", "ak", "os", "a", "ul", "ik"],
        Epithets =
        [
            "the Hungry", "the Cruel", "Bone-Biter", "the Patient", "Gutter-Fang", "the Grinning",
            "Skull-Cracker", "the Mad", "the Collector", "Blood-Drinker", "the Quiet", "Ring-Breaker",
            "the Tide-Cursed", "Lantern-Eater", "the Unbowed", "of the Deep Docks",
        ],
        ScarEpithets = new()
        {
            [DamageKind.Fire] = ["the Unburnt", "Ash-Skin", "the Charred"],
            [DamageKind.Frost] = ["Frost-Scarred", "the Thawed"],
            [DamageKind.Shock] = ["Storm-Touched", "the Twitching"],
            [DamageKind.Sun] = ["the Unhallowed", "Light-Spurner"],
            [DamageKind.Physical] = ["the Stitched", "Many-Scars", "the Unkillable"],
            [DamageKind.Poison] = ["the Purged"],
        },
        ScarDescriptions = new()
        {
            [DamageKind.Fire] = ["Burn scars cover half its face."],
            [DamageKind.Frost] = ["Frostbitten patches never healed."],
            [DamageKind.Shock] = ["Branching lightning scars run down its back."],
            [DamageKind.Sun] = ["A pale brand smoulders on its chest."],
            [DamageKind.Physical] = ["Crude stitches hold a deep wound closed."],
            [DamageKind.Poison] = ["Its veins show dark under the skin."],
        },
        Taunts = new()
        {
            ["first"] = ["Fresh meat for {name}!", "You'll do."],
            ["killed"] = ["Back again, {player}? I remember how you fell.", "I killed you once. I'll enjoy the second time."],
            ["fled"] = ["Running didn't save you last time, {player}.", "Look who stopped running."],
            ["escaped"] = ["You let me go once. Mistake.", "Missed me, {player}?"],
            ["returned"] = ["You thought I was dead? Look what you did to me!", "These scars are yours, {player}. I'm returning them."],
            ["friend"] = ["I killed {player}. You're next.", "{player} begged at the end. Will you?"],
            ["vengeful"] = ["You killed my pact-sworn, {player}. Now you die.", "For my sworn kin!"],
            ["generic"] = ["{name} will be remembered!", "Another hero for my collection."],
        },
    };
}

/// <summary>
/// The default strengths and weaknesses, as multipliers on what a fighter has, elemental
/// resistances (Weak = -50%, Resist = +50%, Immune = 100%, Absorb heals) and Final
/// Fantasy-style statuses the game layer applies as magic effects.
/// </summary>
public static class TraitCatalog
{
    static Trait StatTrait(string id, TraitKind kind, string label, Stat stat, double mult, string? epithet = null) =>
        new(id, kind, label, epithet, StatMultipliers: new() { [stat] = mult });

    static Trait Element(string id, TraitKind kind, string label, DamageKind element, Affinity affinity, string? epithet = null) =>
        new(id, kind, label, epithet, Affinities: new() { [element] = affinity });

    static Trait Auto(string id, string label, AutoStatus status, string? epithet = null) =>
        new(id, TraitKind.Strength, label, epithet, AutoStatuses: [status]);

    public static IReadOnlyList<Trait> Strengths { get; } =
    [
        StatTrait("brutal", TraitKind.Strength, "Brutal: hits much harder", Stat.Damage, 1.35, "the Brutal"),
        StatTrait("ironhide", TraitKind.Strength, "Ironhide: shrugs off blows", Stat.Armor, 1.5, "Iron-Hide"),
        StatTrait("arcane", TraitKind.Strength, "Arcane: stronger spells", Stat.SpellPower, 1.4, "the Witch-Eyed"),
        StatTrait("warded", TraitKind.Strength, "Warded: resists magic", Stat.MagicResist, 1.5),
        StatTrait("swift", TraitKind.Strength, "Swift: fast on its feet", Stat.Speed, 1.25, "the Swift"),
        StatTrait("tireless", TraitKind.Strength, "Tireless: endless stamina for power attacks", Stat.Stamina, 1.8),
        StatTrait("deadeye", TraitKind.Strength, "Deadeye: lands critical hits", Stat.CritChance, 2.0),
        StatTrait("colossal", TraitKind.Strength, "Colossal: huge health pool", Stat.Health, 1.5, "the Mountain"),
        Element("fire_eater", TraitKind.Strength, "Fire-Eater: fire heals it", DamageKind.Fire, Affinity.Absorb, "Fire-Eater"),
        Element("frostblood", TraitKind.Strength, "Frostblood: immune to frost", DamageKind.Frost, Affinity.Immune),
        Element("stormborn", TraitKind.Strength, "Stormborn: shock heals it", DamageKind.Shock, Affinity.Absorb, "Stormborn"),
        Element("venomblood", TraitKind.Strength, "Venomblood: immune to poison", DamageKind.Poison, Affinity.Immune),
        Auto("mirrored", "Mirrored: spells bounce off (Reflect)", AutoStatus.Reflect),
        Auto("bulwark", "Bulwark: Protect, halves weapon damage", AutoStatus.Protect),
        Auto("spellward", "Spellward: Shell, halves spell damage", AutoStatus.Shell),
        Auto("regenerating", "Regenerating: Regen, heals over time", AutoStatus.Regen, "the Undying"),
        Auto("quickened", "Quickened: Haste, attacks faster", AutoStatus.Haste),
    ];

    public static IReadOnlyList<Trait> Weaknesses { get; } =
    [
        Element("flammable", TraitKind.Weakness, "Flammable: weak to fire", DamageKind.Fire, Affinity.Weak),
        Element("brittle", TraitKind.Weakness, "Brittle: weak to frost", DamageKind.Frost, Affinity.Weak),
        Element("conductive", TraitKind.Weakness, "Conductive: weak to shock", DamageKind.Shock, Affinity.Weak),
        Element("sun_cursed", TraitKind.Weakness, "Sun-Cursed: weak to sun magic", DamageKind.Sun, Affinity.Weak),
        Element("frail_blood", TraitKind.Weakness, "Frail Blood: weak to poison", DamageKind.Poison, Affinity.Weak),
        StatTrait("glass_jaw", TraitKind.Weakness, "Glass Jaw: poor armor", Stat.Armor, 0.6),
        StatTrait("spell_bare", TraitKind.Weakness, "Spell-Bare: no magic resistance", Stat.MagicResist, 0.3),
        StatTrait("sluggish", TraitKind.Weakness, "Sluggish: slow on its feet", Stat.Speed, 0.85),
        StatTrait("winded", TraitKind.Weakness, "Winded: tires quickly", Stat.Stamina, 0.6),
    ];

    /// <summary>The resistance a deviant learns from surviving a kind of death.</summary>
    public static Trait ScarResistance(DamageKind cause, Affinity affinity) => cause == DamageKind.Physical
        ? new($"scar_{cause}".ToLowerInvariant(), TraitKind.Strength, "Scar Tissue: tougher hide",
            StatMultipliers: new() { [Stat.Armor] = affinity == Affinity.Immune ? 1.6 : 1.3 })
        : new($"scar_{cause}".ToLowerInvariant(), TraitKind.Strength,
            $"Scarred by {cause}: {(affinity == Affinity.Immune ? "immune to" : "resists")} {cause.ToString().ToLowerInvariant()}",
            Affinities: new() { [cause] = affinity });
}
