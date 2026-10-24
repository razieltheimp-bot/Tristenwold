using Tristinwold.Nemesis;

namespace Tristinwold.Gameplay;

/// <summary>A kind of enemy: its base numbers, how it fights and what it looks like (until real art).</summary>
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
    double Height)
{
    public Combatant NewCombatant() => new(Name, Health, magicka: 40, stamina: 100, armor: Armor);
}

/// <summary>The enemies of Tristinwold's first act: Tong thugs, harbor horrors and the restless dead.</summary>
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
    ];

    public static EnemyKind Get(string id) => All.FirstOrDefault(k => k.Id == id) ?? All[1];
}
