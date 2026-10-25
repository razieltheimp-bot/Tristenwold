using Tristinwold.Nemesis;

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
        return new(Name, Math.Round(Health * scale * toughness), magicka: 40, stamina: 100, armor: Armor * Math.Sqrt(scale)) { DamageMult = scale * might };
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
        new("skeever", "Giant Skeever", 40, 6, 5.5, 0, 1.4, 1.0, DamageKind.Physical, "#6b5a4a", 0.6),
        new("dock_thug", "Dock Thug", 110, 12, 4.2, 60, 1.8, 1.4, DamageKind.Physical, "#7a4e34", 1.8),
        new("lantern_boy", "Lantern Boy Cutpurse", 70, 9, 5.0, 20, 1.6, 1.0, DamageKind.Physical, "#b8402c", 1.6),
        new("drowned_hand", "Drowned Hand Smuggler", 140, 14, 4.0, 120, 1.9, 1.5, DamageKind.Physical, "#3a6a8a", 1.85),
        new("draugr", "Draugr", 160, 15, 3.4, 150, 2.0, 1.7, DamageKind.Frost, "#8aa0a8", 1.9),
        new("sea_wraith", "Sea Wraith", 120, 18, 4.6, 0, 2.2, 2.0, DamageKind.Frost, "#7fc6c0", 2.0),
        new("bandit", "Bandit", 100, 11, 4.4, 50, 1.8, 1.3, DamageKind.Physical, "#6a5a3a", 1.8),
        new("bandit_chief", "Bandit Chief", 220, 18, 4.2, 140, 2.0, 1.5, DamageKind.Physical, "#8a2a1a", 1.95),
        new("draugr_overlord", "Draugr Overlord", 320, 22, 3.6, 220, 2.2, 1.8, DamageKind.Frost, "#b0c8d0", 2.1),
        new("wolf", "Wolf", 55, 8, 6.5, 0, 1.6, 0.9, DamageKind.Physical, "#5a5a5a", 0.9),
        new("ice_wolf", "Ice Wolf", 80, 10, 6.5, 20, 1.6, 0.9, DamageKind.Frost, "#d8e4ea", 0.95),
        new("bear", "Cave Bear", 260, 20, 5.0, 60, 2.2, 1.8, DamageKind.Physical, "#4a3428", 1.6),
        new("frost_troll", "Frost Troll", 340, 22, 4.8, 40, 2.4, 1.6, DamageKind.Physical, "#c8c8d0", 2.4),
        new("mudcrab", "Mudcrab", 45, 7, 3.2, 120, 1.4, 1.2, DamageKind.Physical, "#7a6a4a", 0.6),
        new("spriggan", "Spriggan", 140, 14, 4.5, 30, 2.0, 1.4, DamageKind.Poison, "#8a7a3a", 2.0),
        new("goblin", "Goblin", 75, 9, 5.2, 30, 1.6, 1.0, DamageKind.Physical, "#6a8a4a", 1.3),
        new("boar", "Wild Boar", 90, 10, 6.0, 30, 1.5, 1.1, DamageKind.Physical, "#4a3a2e", 0.9),
        // Quarry: they bolt when they see you.
        new("deer", "Deer", 50, 4, 8.5, 0, 1.4, 1.2, DamageKind.Physical, "#8a6a48", 1.3, Timid: true),
        new("fox", "Red Fox", 25, 3, 7.5, 0, 1.0, 1.0, DamageKind.Physical, "#c06a30", 0.5, Timid: true),
        .. DragonKinds(),
        .. DragonKinds().Select(Corrupted),
        // Companions: the youths who follow a hero, and a warlock's familiar.
        new("squire", "Squire", 120, 11, 5.0, 80, 1.9, 1.2, DamageKind.Physical, "#b0b8c8", 1.6),
        new("apprentice", "Apprentice", 90, 13, 5.0, 30, 12, 1.6, DamageKind.Fire, "#7a5aa8", 1.55),
        new("protege", "Protege", 100, 12, 5.6, 40, 1.8, 0.9, DamageKind.Physical, "#5a4a3a", 1.55),
        new("familiar", "Familiar", 110, 14, 5.5, 40, 1.8, 1.1, DamageKind.Poison, "#8a5ad0", 1.4),
        // The Ashen Crypt: the burned warden and the thralls he raises (their numbers are set by the raid).
        new("cinder_warden", "Cinder Warden", 600, 18, 4.2, 120, 2.6, 1.6, DamageKind.Fire, "#2e241c", 2.6),
        new("cinder_thrall", "Cinder Thrall", 45, 6, 4.0, 40, 1.7, 1.3, DamageKind.Fire, "#3a2e26", 1.5),
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
