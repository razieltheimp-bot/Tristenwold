using System.Text.Json;

namespace Tristinwold.Nemesis;

/// <summary>
/// Names, epithets and taunts. Ships with a default set; load your own JSON to
/// give the nemeses your setting's flavour (Tong crews, Sin-touched, and so on).
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
        JsonSerializer.Deserialize<NamePack>(File.ReadAllText(path), NemesisJson.Options)
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
            [DamageKind.Ice] = ["Frost-Scarred", "the Thawed"],
            [DamageKind.Lightning] = ["Storm-Touched", "the Twitching"],
            [DamageKind.Water] = ["the Drowned", "Who-Came-Back-Wet"],
            [DamageKind.Holy] = ["the Unhallowed", "Light-Spurner"],
            [DamageKind.Physical] = ["the Stitched", "Many-Scars", "the Unkillable"],
            [DamageKind.Gravity] = ["the Crushed"],
            [DamageKind.Poison] = ["the Purged"],
        },
        ScarDescriptions = new()
        {
            [DamageKind.Fire] = ["Burn scars cover half its face."],
            [DamageKind.Ice] = ["Frostbitten patches never healed."],
            [DamageKind.Lightning] = ["Branching lightning scars run down its back."],
            [DamageKind.Water] = ["Its lungs rattle with old seawater."],
            [DamageKind.Holy] = ["A pale brand smoulders on its chest."],
            [DamageKind.Physical] = ["Crude stitches hold a deep wound closed."],
            [DamageKind.Gravity] = ["Its bones were set crooked after being crushed."],
            [DamageKind.Poison] = ["Its veins show dark under the skin."],
        },
        Taunts = new()
        {
            ["first"] = ["Fresh meat for {name}!", "You'll do."],
            ["killed"] = ["Back again, {player}? I remember how you fell.", "I killed you once. I'll enjoy the second time."],
            ["fled"] = ["Running didn't save you last time, {player}.", "Look who stopped running."],
            ["escaped"] = ["You let me go once. Mistake.", "Missed me, {player}?"],
            ["cheated"] = ["You thought I was dead? Look what you did to me!", "These scars are yours, {player}. I'm returning them."],
            ["friend"] = ["I killed {player}. You're next.", "{player} begged at the end. Will you?"],
            ["avenger"] = ["You killed my blood-brother, {player}. Now you die.", "For my brother!"],
            ["generic"] = ["{name} will be remembered!", "Another hero for my collection."],
        },
    };
}

/// <summary>The default strengths and weaknesses. Mapped to FFX stats, elements and auto-statuses.</summary>
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
        StatTrait("brutal", TraitKind.Strength, "Brutal: hits much harder", Stat.Strength, 1.3, "the Brutal"),
        StatTrait("ironhide", TraitKind.Strength, "Ironhide: shrugs off blows", Stat.Defense, 1.4, "Iron-Hide"),
        StatTrait("arcane", TraitKind.Strength, "Arcane: stronger spells", Stat.Magic, 1.35, "the Witch-Eyed"),
        StatTrait("warded", TraitKind.Strength, "Warded: resists magic", Stat.MagicDefense, 1.4),
        StatTrait("swift", TraitKind.Strength, "Swift: acts more often", Stat.Agility, 1.25, "the Swift"),
        StatTrait("elusive", TraitKind.Strength, "Elusive: hard to hit", Stat.Evasion, 1.5, "the Shadow"),
        StatTrait("deadeye", TraitKind.Strength, "Deadeye: rarely misses", Stat.Accuracy, 1.5),
        StatTrait("colossal", TraitKind.Strength, "Colossal: huge HP pool", Stat.Hp, 1.5, "the Mountain"),
        Element("fire_eater", TraitKind.Strength, "Fire-Eater: absorbs fire", DamageKind.Fire, Affinity.Absorb, "Fire-Eater"),
        Element("frostblood", TraitKind.Strength, "Frostblood: immune to ice", DamageKind.Ice, Affinity.Immune),
        Element("stormborn", TraitKind.Strength, "Stormborn: absorbs lightning", DamageKind.Lightning, Affinity.Absorb, "Stormborn"),
        Element("tidecaller", TraitKind.Strength, "Tidecaller: absorbs water", DamageKind.Water, Affinity.Absorb),
        Auto("mirrored", "Mirrored: spells bounce off (Auto-Reflect)", AutoStatus.Reflect),
        Auto("bulwark", "Bulwark: Auto-Protect", AutoStatus.Protect),
        Auto("spellward", "Spellward: Auto-Shell", AutoStatus.Shell),
        Auto("regenerating", "Regenerating: Auto-Regen", AutoStatus.Regen, "the Undying"),
        Auto("quickened", "Quickened: Auto-Haste", AutoStatus.Haste),
    ];

    public static IReadOnlyList<Trait> Weaknesses { get; } =
    [
        Element("flammable", TraitKind.Weakness, "Flammable: weak to fire", DamageKind.Fire, Affinity.Weak),
        Element("brittle", TraitKind.Weakness, "Brittle: weak to ice", DamageKind.Ice, Affinity.Weak),
        Element("conductive", TraitKind.Weakness, "Conductive: weak to lightning", DamageKind.Lightning, Affinity.Weak),
        Element("waterlogged", TraitKind.Weakness, "Waterlogged: weak to water", DamageKind.Water, Affinity.Weak),
        Element("unholy", TraitKind.Weakness, "Unholy: weak to holy", DamageKind.Holy, Affinity.Weak),
        StatTrait("glass_jaw", TraitKind.Weakness, "Glass Jaw: low defense", Stat.Defense, 0.7),
        StatTrait("dull_minded", TraitKind.Weakness, "Dull-Minded: low magic defense", Stat.MagicDefense, 0.7),
        StatTrait("sluggish", TraitKind.Weakness, "Sluggish: slow to act", Stat.Agility, 0.8),
        StatTrait("clumsy", TraitKind.Weakness, "Clumsy: misses often", Stat.Accuracy, 0.75),
    ];

    /// <summary>The resistance a nemesis learns from surviving a kind of death.</summary>
    public static Trait ScarResistance(DamageKind cause, Affinity affinity) => cause == DamageKind.Physical
        ? new($"scar_{cause}".ToLowerInvariant(), TraitKind.Strength, "Scar Tissue: tougher hide",
            StatMultipliers: new() { [Stat.Defense] = affinity == Affinity.Immune ? 1.5 : 1.25 })
        : new($"scar_{cause}".ToLowerInvariant(), TraitKind.Strength,
            $"Scarred by {cause}: {(affinity == Affinity.Immune ? "immune" : "resists")} {cause.ToString().ToLowerInvariant()}",
            Affinities: new() { [cause] = affinity });
}
