using Tristinwold.Nemesis;

namespace Tristinwold.Gameplay;

/// <summary>
/// A kind of enemy (or quarry): its base numbers, how it fights and what it looks like. A timid
/// kind runs from people rather than fighting them.
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
    bool Timid = false)
{
    /// <summary>A fresh fighter. <paramref name="danger"/> grows with distance from Tristinwold: 1 at home.</summary>
    public Combatant NewCombatant(double danger = 1)
    {
        var scale = Math.Pow(Math.Max(1, danger), 0.8);
        return new(Name, Math.Round(Health * scale), magicka: 40, stamina: 100, armor: Armor * Math.Sqrt(scale)) { DamageMult = scale };
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
        new("dragon", "Frost Dragon", 900, 40, 5.5, 200, 3.5, 2.2, DamageKind.Frost, "#8aa0c0", 4.5),
        new("boar", "Wild Boar", 90, 10, 6.0, 30, 1.5, 1.1, DamageKind.Physical, "#4a3a2e", 0.9),
        // Quarry: they bolt when they see you.
        new("deer", "Deer", 50, 4, 8.5, 0, 1.4, 1.2, DamageKind.Physical, "#8a6a48", 1.3, Timid: true),
        new("fox", "Red Fox", 25, 3, 7.5, 0, 1.0, 1.0, DamageKind.Physical, "#c06a30", 0.5, Timid: true),
    ];

    public static EnemyKind Get(string id) => All.FirstOrDefault(k => k.Id == id) ?? All[1];
}
