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
        new("dread_soren", "The Dread Pirate Soren", 1500, 44, 5.0, 240, 2.8, 1.3, DamageKind.Physical, "#8a1a1a", 2.4),
        new("lu_wukong", "Lu Wukong", 1000, 30, 7.0, 130, 3.0, 0.9, DamageKind.Physical, "#4a4038", 2.0),
        new("monkey_hand", "Hand of the Drunken Monkey", 130, 13, 4.8, 60, 1.8, 1.2, DamageKind.Physical, "#b08a3a", 1.8),
        new("sky_hull", "Sky Junk", 900, 0, 0.1, 0, 6, 1.4, DamageKind.Physical, "#c8703a", 2),
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
        // More of the wilds: bears black and white, the great cats, crocodiles in the marsh, wild cattle and hounds.
        new("black_bear", "Black Bear", 200, 16, 5.2, 50, 2.0, 1.6, DamageKind.Physical, "#2a2420", 1.4),
        new("white_bear", "White Bear", 380, 26, 5.0, 80, 2.4, 1.8, DamageKind.Physical, "#e8e4da", 1.9),
        new("tiger", "Snow Tiger", 300, 26, 7.2, 40, 2.2, 1.0, DamageKind.Physical, "#d8a050", 1.15),
        new("crag_lion", "Crag Lion", 240, 22, 7.0, 30, 2.0, 1.0, DamageKind.Physical, "#c8a060", 1.1),
        new("crocodile", "Marsh Crocodile", 240, 22, 4.0, 140, 2.2, 1.6, DamageKind.Physical, "#4a5a34", 0.55),
        new("aurochs", "Aurochs", 300, 20, 6.0, 80, 2.2, 1.6, DamageKind.Physical, "#2e2620", 1.75, Wary: true),
        new("feral_hound", "Feral Hound", 60, 9, 7.0, 10, 1.5, 0.9, DamageKind.Physical, "#6a5a48", 0.85),
        // The strange crossings of the wilds: no one knows how they came to be, and they don't say.
        new("griffon", "Griffon", 420, 28, 7.0, 70, 2.4, 1.2, DamageKind.Physical, "#b08a50", 1.5),
        // Serpents: in the open sea, rolling through the waves, and on land, snaking over the high ground with fire on their backs.
        new("sea_serpent", "Sea Serpent", 520, 28, 7.5, 90, 4.0, 1.4, DamageKind.Physical, "#2f8ea6", 2.4),
        new("leviathan", "Leviathan", 2400, 52, 6.0, 220, 7.0, 2.0, DamageKind.Physical, "#3a4250", 4.8),
        new("ember_wyrm", "Ember Wyrm", 460, 26, 6.8, 100, 3.2, 1.2, DamageKind.Fire, "#c25420", 1.5),
        // Quarry: they bolt when they see you.
        new("deer", "Deer", 50, 4, 8.5, 0, 1.4, 1.2, DamageKind.Physical, "#8a6a48", 1.3, Timid: true),
        new("fox", "Red Fox", 25, 3, 7.5, 0, 1.0, 1.0, DamageKind.Physical, "#c06a30", 0.5, Timid: true),
        new("arctic_fox", "Arctic Fox", 28, 3, 7.5, 0, 1.0, 1.0, DamageKind.Physical, "#eef0f2", 0.5, Timid: true),
        new("mountain_goat", "Mountain Goat", 60, 6, 7.5, 20, 1.4, 1.2, DamageKind.Physical, "#e8e2d4", 1.0, Timid: true),
        new("hare", "Hare", 12, 1, 9.0, 0, 0.6, 1.0, DamageKind.Physical, "#a08a6a", 0.35, Timid: true),
        new("sky_fox", "Sky Fox", 30, 4, 8.0, 0, 1.0, 1.0, DamageKind.Physical, "#c86a30", 0.55, Timid: true),
        new("jackalope", "Jackalope", 20, 3, 9.0, 0, 0.7, 1.0, DamageKind.Physical, "#9a7a5a", 0.4, Timid: true),
        new("pegasus", "Winged Horse", 180, 8, 10.5, 20, 1.8, 1.4, DamageKind.Physical, "#f0eee8", 1.8, Timid: true),
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
        new("pupil", "Pupil", 105, 12, 5.8, 35, 1.7, 0.8, DamageKind.Physical, "#d8a860", 1.55),
        new("tinker", "Tinker", 95, 12, 5.0, 35, 10, 1.5, DamageKind.Shock, "#c8a050", 1.55),
        new("acolyte", "Acolyte", 95, 12, 5.0, 30, 11, 1.6, DamageKind.Poison, "#6a9a5a", 1.55),
        new("familiar", "Familiar", 110, 14, 5.5, 40, 1.8, 1.1, DamageKind.Poison, "#8a5ad0", 1.4),
        new("clockwork_mite", "Clockwork Mite", 70, 7, 5.5, 150, 1.4, 1.4, DamageKind.Physical, "#8a6a3a", 0.5), // a saboteur's little machine, knee-high, forged by hand
        new("golem", "Brass Golem", 240, 16, 4.4, 150, 2.0, 1.9, DamageKind.Physical, "#c8a048", 1.15), // an artificer's machine, at their side
        // The Ashen Crypt: the burned warden and the thralls he raises (their numbers are set by the raid).
        new("cinder_warden", "Cinder Warden", 600, 18, 4.2, 120, 2.6, 1.6, DamageKind.Fire, "#2e241c", 2.6),
        new("cinder_thrall", "Cinder Thrall", 45, 6, 4.0, 40, 1.7, 1.3, DamageKind.Fire, "#3a2e26", 1.5),
        // The lairs of the wilds: each boss, and its own that come first (their numbers too are set by the raid).
        new("goblin_king", "Goblin King", 700, 20, 4.4, 80, 2.4, 1.5, DamageKind.Fire, "#5a7a3a", 2.4),
        new("rock_bear", "Rock Bear", 800, 24, 5.2, 220, 2.8, 1.8, DamageKind.Physical, "#5a4a3a", 2.6),
        new("briar_mother", "Briar Mother", 700, 20, 2.8, 60, 3.2, 1.8, DamageKind.Poison, "#3a5a2a", 3.6),
        new("ghost_captain", "Hollow Captain", 700, 22, 4.6, 160, 2.4, 1.4, DamageKind.Frost, "#a8e0f0", 2.3),
        // The Northwood's guardians: older than the city, and they leave you be unless you've given them reason.
        new("turlin", "Turlin", 150, 11, 5.2, 40, 2.0, 1.2, DamageKind.Physical, "#3a3440", 2.0, Wary: true),
        new("giant_ape", "Giant Ape", 3200, 30, 5.6, 110, 3.2, 1.7, DamageKind.Physical, "#35303a", 7.2, Wary: true),
        // The drifters: incomplete immortals out in the world (see Drifters), and what Irohe calls up out of his fire.
        new("drifter_broly", "Broly", 640, 26, 5.6, 120, 2.4, 1.1, DamageKind.Physical, "#3a4a2a", 2.3, Wary: true),
        new("drifter_gornok", "Gornok Thornek", 520, 20, 5.4, 100, 2.2, 0.8, DamageKind.Physical, "#6a4a3a", 2.2, Wary: true),
        new("drifter_ren", "Ren", 420, 18, 6.2, 60, 2.0, 0.8, DamageKind.Fire, "#3a5ac0", 1.8, Wary: true),
        new("drifter_irohe", "Irohe", 560, 22, 5.0, 90, 3.0, 1.0, DamageKind.Fire, "#b8382c", 1.9, Wary: true),
        new("drifter_faye", "Faye", 400, 24, 5.2, 50, 6.0, 1.3, DamageKind.Shock, "#e8e8e4", 1.8, Wary: true),
        new("drifter_yokin", "Yokin Hearthbeat", 620, 25, 4.8, 130, 2.8, 1.4, DamageKind.Physical, "#8a8a98", 2.3, Wary: true),
        new("flame_serpent", "Flame Serpent", 260, 16, 6.5, 40, 3.0, 1.0, DamageKind.Fire, "#ff6a20", 1.6),
        // Korrin, Subject One, in his two forms, and Ronacse, Pride, in his (see Undying).
        new("korrin", "Korrin", 900, 28, 6.0, 150, 2.4, 0.9, DamageKind.Shock, "#3a2a5a", 2.2),
        new("korrin_final", "Korrin Unbound", 900, 36, 6.6, 180, 2.8, 0.8, DamageKind.Shock, "#5a3a8a", 2.6),
        new("ronacse", "Ronacse", 850, 30, 5.2, 220, 2.8, 1.1, DamageKind.Fire, "#d8b040", 2.2),
        new("ronacse_pride", "Ronacse, Pride Unbound", 950, 40, 5.8, 200, 3.0, 1.0, DamageKind.Fire, "#ffd8a0", 2.6),
        // The Dark Depths: the demons who raid up out of the Rift, their lord, and heaven's answer to them.
        new("demon_grunt", "Demon Brute", 420, 26, 4.8, 120, 2.8, 1.4, DamageKind.Fire, "#8a2020", 2.4),
        new("duragon", "Duragon", 2600, 48, 5.4, 240, 3.2, 1.1, DamageKind.Poison, "#d8d8d0", 2.8),
        new("angel", "Host of Heaven", 700, 34, 6.8, 160, 6.0, 1.0, DamageKind.Sun, "#ffe8a0", 2.2),
        new("guardian_moss", "Forest Guardian", 5200, 46, 3.6, 120, 4.5, 2.4, DamageKind.Physical, "#5a8a3a", 8, Wary: true),
        new("guardian_bark", "Forest Guardian", 4400, 56, 4.4, 90, 4.5, 2.0, DamageKind.Physical, "#8a6a4a", 7.5, Wary: true),
        new("guardian_elder", "Forest Guardian", 6000, 42, 3.2, 140, 5, 2.6, DamageKind.Frost, "#3a6a5a", 8.5, Wary: true),
        new("guardian_blight", "Corrupted Guardian", 6200, 60, 4.2, 100, 4.5, 2.1, DamageKind.Poison, "#2e4a2e", 8, Wary: true),
        // The great ones of the Summoner's road, one of each in all the world (see Summons): Draxon is a dragon of the Rift, grown giant.
        new("orrogh", "Orrogh, the Stone-Heart", 1600, 26, 3.8, 220, 3.6, 2.0, DamageKind.Physical, "#8a8478", 6),
        new("lumira", "Lumira, the Dawnmoth", 1900, 24, 6.0, 90, 4.0, 1.4, DamageKind.Sun, "#ffe8a0", 5),
        new("quvirn", "Quvirn, the Storm-Beast", 2800, 38, 6.4, 130, 3.8, 1.3, DamageKind.Shock, "#4a4a8a", 5.5),
        new("aegrath", "Aegrath, the Brass Colossus", 3000, 36, 3.6, 300, 4.2, 2.0, DamageKind.Physical, "#c09a44", 7.5),
        new("blaze", "Blaze, the Living Pyre", 3400, 44, 4.6, 150, 4.0, 1.7, DamageKind.Fire, "#ff6a20", 7.5),
        new("shicene", "Shicene, the Winter Wing", 3600, 40, 5.6, 110, 4.4, 1.6, DamageKind.Frost, "#c8ecff", 7.5),
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
