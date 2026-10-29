using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// A kind of enemy (or quarry): its base numbers, how it fights and what it looks like. A timid
/// kind runs from people rather than fighting them; a wary one leaves them be unless attacked.
/// </summary>
public sealed record EnemyKind(
    string Id,
    string Name,
    double Health,
    double Damage,
    double Speed,
    double Armor,
    double Reach,
    double AttackSeconds,
    DamageKind DamageKind,
    string Colour,
    double Height,
    bool Timid = false,
    bool Wary = false)
{
    /// <summary>A fresh fighter. <paramref name="danger"/> grows with distance from Tristinwold: 1 at home.</summary>
    /// <param name="toughness">Extra health (a dragon's size).</param>
    /// <param name="might">Extra damage.</param>
    public Combatant NewCombatant(double danger = 1, double toughness = 1, double might = 1)
    {
        var scale = Math.Pow(Math.Max(1, danger), 0.8);
        return new(Name, Math.Round(Health * scale * toughness), mana: 40, stamina: 100, armor: Armor * Math.Sqrt(scale)) { DamageMult = scale * might };
    }
}

/// <summary>
/// The enemies of Tristinwold's first act (Tong thugs, harbor horrors, the restless dead) and of
/// the wilds beyond it: bandits, beasts, barrow-kings.
/// </summary>
public static class Bestiary
{
    public static IReadOnlyList<EnemyKind> All { get; } =
    [
        new("giant_rat", "Giant Rat", 40, 6, 5.5, 0, 1.4, 1.0, DamageKind.Physical, "#6b5a4a", 0.6),
        new("dock_thug", "Dock Thug", 110, 12, 4.2, 60, 1.8, 1.4, DamageKind.Physical, "#7a4e34", 1.8),
        new("watchman", "Watchman", 280, 16, 5.0, 90, 1.9, 1.3, DamageKind.Physical, "#4a5a7a", 1.8), // the town Watch: on your side
        // The guilds' initiates about the town, and the cutthroats the Velvet Knives send.
        new("young_thief", "Young Thief", 60, 4, 6.0, 10, 1.4, 1.0, DamageKind.Physical, "#3a3a3a", 1.7, Timid: true, Wary: true),
        new("initiate", "Initiate", 90, 5, 5.5, 20, 1.6, 1.2, DamageKind.Physical, "#6a6a6a", 1.75, Wary: true),
        new("velvet_assassin", "Velvet Knife", 220, 18, 6.4, 40, 1.6, 0.9, DamageKind.Physical, "#2a1a2a", 1.8),
        new("lantern_boy", "Lantern Boy Cutpurse", 70, 9, 5.0, 20, 1.6, 1.0, DamageKind.Physical, "#b8402c", 1.6),
        new("drowned_hand", "Drowned Hand Smuggler", 140, 14, 4.0, 120, 1.9, 1.5, DamageKind.Physical, "#3a6a8a", 1.85),
        new("draugr", "Draugr", 160, 15, 3.4, 150, 2.0, 1.7, DamageKind.Frost, "#8aa0a8", 1.9),
        new("sea_wraith", "Sea Wraith", 120, 18, 4.6, 0, 2.2, 2.0, DamageKind.Frost, "#7fc6c0", 2.0),
        new("bandit", "Bandit", 100, 11, 4.4, 50, 1.8, 1.3, DamageKind.Physical, "#6a5a3a", 1.8),
        new("bandit_chief", "Bandit Chief", 220, 18, 4.2, 140, 2.0, 1.5, DamageKind.Physical, "#8a2a1a", 1.95),
        // At sea: the pirates of the Teeth who board merchantmen, and the company's marines who guard them.
        new("pirate", "Pirate Cutthroat", 120, 13, 4.8, 60, 1.8, 1.2, DamageKind.Physical, "#3a4a6a", 1.8),
        new("pirate_captain", "Pirate Captain", 300, 21, 4.6, 180, 2.0, 1.4, DamageKind.Physical, "#6a1a2a", 1.95),
        new("marine", "Company Marine", 150, 14, 4.4, 70, 1.9, 1.3, DamageKind.Physical, "#8a3a2a", 1.85),
        // The barbarian clans that raid the frontier: big, painted, and they hit hard.
        new("raider", "Barbarian Raider", 140, 14, 4.6, 70, 1.9, 1.4, DamageKind.Physical, "#7a4a2a", 1.95),
        new("raider_chief", "Barbarian Warchief", 340, 22, 4.4, 200, 2.1, 1.6, DamageKind.Physical, "#9a3a1a", 2.1),
        new("draugr_overlord", "Draugr Overlord", 320, 22, 3.6, 220, 2.2, 1.8, DamageKind.Frost, "#b0c8d0", 2.1),
        new("wolf", "Wolf", 55, 8, 6.5, 0, 1.6, 0.9, DamageKind.Physical, "#5a5a5a", 0.9),
        new("ice_wolf", "Ice Wolf", 80, 10, 6.5, 20, 1.6, 0.9, DamageKind.Frost, "#d8e4ea", 0.95),
        new("bear", "Cave Bear", 260, 20, 5.0, 60, 2.2, 1.8, DamageKind.Physical, "#4a3428", 1.6),
        new("frost_troll", "Frost Troll", 340, 22, 4.8, 40, 2.4, 1.6, DamageKind.Physical, "#c8c8d0", 2.4),
        new("mud_crab", "Mud Crab", 45, 7, 3.2, 120, 1.4, 1.2, DamageKind.Physical, "#7a6a4a", 0.6),
        new("spriggan", "Spriggan", 140, 14, 4.5, 30, 2.0, 1.4, DamageKind.Poison, "#8a7a3a", 2.0),
        new("goblin", "Goblin", 75, 9, 5.2, 30, 1.6, 1.0, DamageKind.Physical, "#6a8a4a", 1.3),
        new("boar", "Wild Boar", 90, 10, 6.0, 30, 1.5, 1.1, DamageKind.Physical, "#4a3a2e", 0.9),
        // Quarry: they bolt when they see you.
        new("deer", "Deer", 50, 4, 8.5, 0, 1.4, 1.2, DamageKind.Physical, "#8a6a48", 1.3, Timid: true),
        new("fox", "Red Fox", 25, 3, 7.5, 0, 1.0, 1.0, DamageKind.Physical, "#c06a30", 0.5, Timid: true),
        // Farm and town animals, and wild horses on the open land: they run, they don't fight.
        new("cow", "Cow", 120, 4, 4.5, 10, 1.6, 1.4, DamageKind.Physical, "#e8e4dc", 1.45, Timid: true),
        new("sheep", "Sheep", 50, 2, 5.0, 5, 1.0, 1.2, DamageKind.Physical, "#e8e4d8", 0.95, Timid: true),
        new("pig", "Pig", 70, 3, 4.5, 5, 1.0, 1.2, DamageKind.Physical, "#e0a8a0", 0.75, Timid: true),
        new("hen", "Hen", 10, 1, 4.0, 0, 0.5, 1.0, DamageKind.Physical, "#e8dcc8", 0.4, Timid: true),
        new("horse", "Horse", 150, 6, 9.0, 15, 1.8, 1.4, DamageKind.Physical, "#6a4a30", 1.75, Timid: true),
        new("wild_horse", "Wild Horse", 150, 6, 10.0, 15, 1.8, 1.4, DamageKind.Physical, "#8a6a4a", 1.75, Timid: true),
        new("dog", "Dog", 45, 5, 7.0, 5, 1.2, 1.1, DamageKind.Physical, "#8a6a48", 0.75, Timid: true, Wary: true),
        new("cat", "Cat", 15, 2, 6.5, 0, 0.6, 0.9, DamageKind.Physical, "#6e6a66", 0.32, Timid: true, Wary: true),
        // Only ever seen at a tamer's side: Wren's Menagerie raises them from the egg.
        new("hawk", "Hawk", 35, 7, 8.0, 0, 1.4, 0.8, DamageKind.Physical, "#8a6a40", 0.6),
        .. DragonKinds(),
        .. DragonKinds().Select(Corrupted),
        // Companions: the youths who follow a hero, and a warlock's familiar.
        new("squire", "Squire", 120, 11, 5.0, 80, 1.9, 1.2, DamageKind.Physical, "#b0b8c8", 1.6),
        new("apprentice", "Apprentice", 90, 13, 5.0, 30, 12, 1.6, DamageKind.Fire, "#7a5aa8", 1.55),
        new("protege", "Protege", 100, 12, 5.6, 40, 1.8, 0.9, DamageKind.Physical, "#5a4a3a", 1.55),
        new("understudy", "Understudy", 95, 12, 5.2, 35, 10, 1.4, DamageKind.Physical, "#c8a040", 1.55),
        new("familiar", "Familiar", 110, 14, 5.5, 40, 1.8, 1.1, DamageKind.Poison, "#8a5ad0", 1.4),
        // The Ashen Crypt: the burned warden and the thralls he raises (their numbers are set by the raid).
        new("cinder_warden", "Cinder Warden", 600, 18, 4.2, 120, 2.6, 1.6, DamageKind.Fire, "#2e241c", 2.6),
        new("cinder_thrall", "Cinder Thrall", 45, 6, 4.0, 40, 1.7, 1.3, DamageKind.Fire, "#3a2e26", 1.5),
        // The lairs of the wilds: each boss, and its own that come first (their numbers too are set by the raid).
        new("goblin_king", "Goblin King", 700, 20, 4.4, 80, 2.4, 1.5, DamageKind.Fire, "#5a7a3a", 2.4),
        new("rock_bear", "Rock Bear", 800, 24, 5.2, 220, 2.8, 1.8, DamageKind.Physical, "#5a4a3a", 2.6),
        new("briar_mother", "Briar Mother", 700, 20, 2.8, 60, 3.2, 1.8, DamageKind.Poison, "#3a5a2a", 3.6),
        new("ghost_captain", "Hollow Captain", 700, 22, 4.6, 160, 2.4, 1.4, DamageKind.Frost, "#a8e0f0", 2.3),
        new("hollow_soldier", "Hollow Soldier", 60, 8, 4.2, 80, 1.9, 1.3, DamageKind.Frost, "#9ad0e0", 1.8),
        new("crystal_basilisk", "Crystal Basilisk", 800, 24, 4.2, 200, 3.2, 1.7, DamageKind.Shock, "#5a3a7a", 2.4),
        new("crystal_rat", "Crystal Rat", 40, 6, 5.5, 60, 1.4, 1.0, DamageKind.Shock, "#7a5aa8", 0.7),
        new("forge_golem", "Forge Golem", 900, 26, 3.6, 260, 3.0, 2.0, DamageKind.Fire, "#3a3a40", 3.4),
        new("goblin_tinker", "Goblin Tinker", 70, 9, 5.2, 40, 1.6, 1.0, DamageKind.Physical, "#4a5a3a", 1.25),
        new("duskprowler", "Duskprowler", 700, 26, 7.0, 80, 2.6, 1.1, DamageKind.Poison, "#1a1424", 2.0),
        new("shadow_wolf", "Shadow Wolf", 60, 9, 6.8, 20, 1.6, 0.9, DamageKind.Poison, "#201a2a", 1.0),
        new("thunderhawk", "Thunderhawk", 800, 26, 5.5, 100, 3.0, 1.5, DamageKind.Shock, "#4a7ad0", 3.2),
        new("stormwing", "Stormwing", 55, 8, 6.0, 10, 1.5, 1.0, DamageKind.Shock, "#7aa8e8", 1.1),
        // Stranger things of the wilds: the kipine, deathcrest apes, imps and dire imps, winged apes.
        new("kipine", "Kipine", 260, 20, 6.0, 80, 2.4, 1.4, DamageKind.Physical, "#4a3a34", 2.4),
        new("deathcrest_ape", "Deathcrest Ape", 120, 12, 6.8, 70, 1.8, 0.9, DamageKind.Poison, "#b8b0a8", 1.9),
        new("dire_imp", "Dire Imp", 170, 14, 5.8, 90, 1.8, 1.0, DamageKind.Fire, "#8a3a22", 1.8),
        new("imp", "Imp", 45, 6, 6.5, 20, 1.4, 0.9, DamageKind.Fire, "#a02a3a", 0.7),
        new("winged_ape", "Winged Ape", 150, 15, 5.0, 60, 2.0, 1.3, DamageKind.Physical, "#2a2826", 2.0),
    ];

    /// <summary>
    /// A dragon twisted by the Rift: darker, tougher, harder-hitting, and at war with every living thing,
    /// dragons of every colour included. Any colour can fall to it.
    /// </summary>
    static EnemyKind Corrupted(EnemyKind k) => k with
    {
        Id = k.Id + "_corrupt", Name = "Corrupted " + k.Name, Health = k.Health * 1.3, Damage = k.Damage * 1.25,
        Colour = "#3a1a48", Wary = false,
    };

    /// <summary>Dragons of every colour: chromatic ones hunt people; metallic ones are wary and leave them be unless attacked.</summary>
    static IEnumerable<EnemyKind> DragonKinds() =>
        new (string Colour, DamageKind Breath, string Hex, bool Metallic)[]
        {
            ("Red", DamageKind.Fire, "#a8281c", false), ("Blue", DamageKind.Shock, "#2a4ab0", false),
            ("Green", DamageKind.Poison, "#2f7a2a", false), ("Black", DamageKind.Poison, "#18181c", false),
            ("White", DamageKind.Frost, "#dfe6ee", false), ("Gold", DamageKind.Fire, "#d8a030", true),
            ("Silver", DamageKind.Frost, "#b8bcc4", true), ("Bronze", DamageKind.Shock, "#8c5a2a", true),
            ("Copper", DamageKind.Poison, "#b86434", true), ("Brass", DamageKind.Fire, "#c09a44", true),
        }.Select(d => new EnemyKind("dragon_" + d.Colour.ToLowerInvariant(), d.Colour + " Dragon", 600, 30, 5.5, 160, 3.5, 2.0, d.Breath, d.Hex, 4.5, Wary: d.Metallic));

    public static EnemyKind Get(string id) => All.FirstOrDefault(k => k.Id == id) ?? All[1];
}
