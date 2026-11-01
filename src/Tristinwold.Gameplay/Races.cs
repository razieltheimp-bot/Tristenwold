using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// A people a hero can be born to, with what that gives them and what it costs. The numbers multiply the class's own
/// (1 is no change); <see cref="Armor"/> and <see cref="Crit"/> add; resistances add to the hero's own.
/// </summary>
public sealed record Race(
    string Id, string Name, string Text, string Good, string Bad,
    double Health = 1, double Mana = 1, double Stamina = 1, double Damage = 1, double SpellPower = 1, double Speed = 1,
    double Armor = 0, double Crit = 0, double Xp = 1, double Prices = 1,
    (DamageKind Kind, double Amount)[]? Resists = null,
    double Size = 1, double Width = 1, string Skin = "#ffffff", string Ears = "", bool Tusks = false);

/// <summary>The colour of a dragonkind's dragon: their scales, and the breath (and bane) in their blood.</summary>
public sealed record DragonBlood(string Colour, string Hex, DamageKind Breath)
{
    /// <summary>What the blood can't stand: frost for the fire-blooded and the other way about, and so on.</summary>
    public DamageKind Bane => Breath switch
    {
        DamageKind.Fire => DamageKind.Frost,
        DamageKind.Frost => DamageKind.Fire,
        DamageKind.Shock => DamageKind.Poison,
        _ => DamageKind.Shock,
    };

    public string Says => $"breathes {Breath.ToString().ToLowerInvariant()}, can't stand {Bane.ToString().ToLowerInvariant()}";
}

public static class Races
{
    public static readonly IReadOnlyList<Race> All =
    [
        new("human", "Human", "The people of the rings: quick to learn and easy to deal with.",
            "Learns 15% faster; 10% cheaper in shops", "No gift of the blood: no resistances",
            Xp: 1.15, Prices: 0.9),
        new("orc", "Orc", "Tusked and green, from the hill clans. Strong, and proud of it.",
            "20% more health, 12% more harm with weapons, 10% more stamina", "25% less mana, spells 10% weaker; townsfolk charge 15% more",
            Health: 1.2, Damage: 1.12, Stamina: 1.1, Mana: 0.75, SpellPower: 0.9, Prices: 1.15, Size: 1.06, Width: 1.12, Skin: "#a4c88c", Ears: "orc", Tusks: true),
        new("goblin", "Goblin", "Small, quick and sly, with ears for every whisper.",
            "15% faster, 12% more likely to strike true (critical), poison hurts a third less", "20% less health, 10% less harm",
            Speed: 1.15, Crit: 12, Health: 0.8, Damage: 0.9, Resists: [(DamageKind.Poison, 33)], Size: 0.74, Width: 0.95, Skin: "#c4dc84", Ears: "goblin"),
        new("elf", "Elf", "Long-lived, light-footed, at home with magic.",
            "25% more mana, spells 12% stronger, magic hurts 15% less", "12% less health, 5 less armour",
            Mana: 1.25, SpellPower: 1.12, Health: 0.88, Armor: -5, Resists: [(DamageKind.Shock, 15), (DamageKind.Frost, 15), (DamageKind.Fire, 15)],
            Size: 1.04, Width: 0.92, Ears: "elf"),
        new("dwarf", "Dwarf", "Short, broad and stubborn as stone, from the deep forges.",
            "25 more armour, 20% more stamina, fire and poison hurt a quarter less", "10% slower, 10% less mana",
            Armor: 25, Stamina: 1.2, Speed: 0.9, Mana: 0.9, Resists: [(DamageKind.Fire, 25), (DamageKind.Poison, 25)], Size: 0.8, Width: 1.2),
        new("half_giant", "Half-giant", "Giant's blood: head and shoulders over everyone, and hard to stop.",
            "35% more health, 20% more harm, 10 more armour", "12% slower, 30% less mana, 10% less stamina",
            Health: 1.35, Damage: 1.2, Armor: 10, Speed: 0.88, Mana: 0.7, Stamina: 0.9, Size: 1.3, Width: 1.15),
        new("ape", "Apekin", "The folk of the Northwood's treetops: broad, clever and long in the arm, and the hands of every warrior there are harder than most men's swords.",
            "15% more health, 12% more harm, 8% faster", "Townsfolk charge 20% more; spells 10% weaker",
            Health: 1.15, Damage: 1.12, Speed: 1.08, Prices: 1.2, SpellPower: 0.9, Size: 1.02, Width: 1.12, Skin: "#5a4030"),
        new("plant", "Photosynthens", "Grown, not born, at the feet of the Northwood's guardians. In their Spring they're all leaf and root; in their Fall, bare wood and branch.",
            "20% more mana, 10 more armour, poison hurts half as much, frost a quarter less", "Fire hurts 40% more; 5% slower",
            Mana: 1.2, Armor: 10, Speed: 0.95, Resists: [(DamageKind.Poison, 50), (DamageKind.Frost, 25), (DamageKind.Fire, -40)], Size: 1.28, Width: 1.02, Skin: "#7a8a48"),
        new("shroomie", "Shroomie", "The mushroom folk of the deep undergrowth, quiet and patient, with long memories and a cap that keeps the rain off.",
            "Spells 15% stronger, 10% more mana, poison hurts 60% less", "10% slower, 5% less health",
            SpellPower: 1.15, Mana: 1.1, Speed: 0.9, Health: 0.95, Resists: [(DamageKind.Poison, 60)], Size: 0.76, Width: 1.08, Skin: "#d8c8b0"),
        new("lizard", "Scalkin", "Swamp-born and half feral, they keep to the roots of Hollowheart or the wild marshes. Most are born warriors. The rest make the greatest druids alive.",
            "12% more health, 10% more harm, spells 8% stronger, poison hurts a quarter less", "Frost hurts 30% more; townsfolk charge 25% more",
            Health: 1.12, Damage: 1.1, SpellPower: 1.08, Prices: 1.25, Resists: [(DamageKind.Poison, 25), (DamageKind.Frost, -30)], Size: 1.04, Width: 1.02, Skin: "#5f8a4a"),
        new(Dragonkind, "Dragonkind", "Dragon blood made flesh: scaled, horned and tailed, their dragon's breath in their veins. Pure, or half of another people.",
            "10% more health, 12% more harm; their dragon's breath hardly hurts them (three-quarters less)", "Their dragon's bane hurts 40% more; people fear them (20% dearer in shops)",
            Health: 1.1, Damage: 1.12, Prices: 1.2, Size: 1.08, Width: 1.06),
    ];

    public const string Dragonkind = "dragonkind";

    public static Race Get(string? id) => All.FirstOrDefault(r => r.Id == id) ?? All[0];

    /// <summary>The peoples a dragonkind can be half of.</summary>
    public static IEnumerable<Race> Halves => All.Where(r => r.Id != Dragonkind);

    /// <summary>
    /// A half-dragon: half dragonkind, half another people. Everything that people gives and costs comes at half
    /// strength, on top of half the dragon's; they look like that people (an elf's ears, an orc's tusks), with the
    /// dragon's horns, tail and some of its scales.
    /// </summary>
    public static Race Half(Race other)
    {
        var d = Get(Dragonkind);
        double Mid(double a, double b) => (a + b) / 2;
        return new($"{Dragonkind}+{other.Id}", $"Half-dragon {other.Name.ToLowerInvariant()}", $"Half dragonkind, half {other.Name.ToLowerInvariant()}.",
            $"Half of each: {other.Good.ToLowerInvariant()}; their dragon's breath hurts half as much",
            $"Half of each: {other.Bad.ToLowerInvariant()}; their dragon's bane hurts a third more",
            Health: Mid(d.Health, other.Health), Mana: Mid(d.Mana, other.Mana), Stamina: Mid(d.Stamina, other.Stamina),
            Damage: Mid(d.Damage, other.Damage), SpellPower: Mid(d.SpellPower, other.SpellPower), Speed: Mid(d.Speed, other.Speed),
            Armor: other.Armor / 2, Crit: other.Crit / 2, Xp: Mid(1, other.Xp), Prices: Mid(d.Prices, other.Prices),
            Resists: other.Resists?.Select(r => (r.Kind, r.Amount / 2)).ToArray(),
            Size: Mid(d.Size, other.Size), Width: Mid(d.Width, other.Width), Skin: other.Skin, Ears: other.Ears, Tusks: other.Tusks);
    }

    /// <summary>A dragonkind's dragon: one for each colour of dragon there is.</summary>
    public static readonly IReadOnlyList<DragonBlood> Dragons =
    [
        new("Red", "#b8382c", DamageKind.Fire), new("Blue", "#3a5ac0", DamageKind.Shock), new("Green", "#3f8a3a", DamageKind.Poison),
        new("Black", "#38383c", DamageKind.Poison), new("White", "#dfe6ee", DamageKind.Frost), new("Gold", "#e0b040", DamageKind.Fire),
        new("Silver", "#c8ccd4", DamageKind.Frost), new("Bronze", "#9c6a3a", DamageKind.Shock), new("Copper", "#c87444", DamageKind.Poison),
        new("Brass", "#d0aa54", DamageKind.Fire),
    ];

    public static DragonBlood? Dragon(string? colour) => Dragons.FirstOrDefault(d => d.Colour.Equals(colour, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Puts a people (and any dragon blood) on a fighter: pools, harm, spells, speed, armour, criticals and
    /// resistances. Pools are set by the caller; this gives their multipliers back for it to use.
    /// </summary>
    /// <param name="pure">Pure dragonkind (the blood at full strength), not half.</param>
    public static (double Health, double Mana, double Stamina) Apply(Combatant c, Race race, DragonBlood? dragon, bool pure = false)
    {
        c.DamageMult *= race.Damage;
        c.SpellPower *= race.SpellPower;
        c.SpeedMult *= race.Speed;
        c.Armor += race.Armor;
        c.CritChance += race.Crit;
        // The hero's resistances are their blood's alone, set fresh each time (this runs whenever their gear changes).
        c.Resist.Clear();
        foreach (var (kind, amount) in race.Resists ?? []) c.Resist[kind] = c.Resist.GetValueOrDefault(kind) + amount;
        if (dragon is not null)
        {
            c.Resist[dragon.Breath] = c.Resist.GetValueOrDefault(dragon.Breath) + (pure ? 75 : 50);
            c.Resist[dragon.Bane] = c.Resist.GetValueOrDefault(dragon.Bane) - (pure ? 40 : 33);
        }
        return (race.Health, race.Mana, race.Stamina);
    }

    /// <summary>What shops ask of this hero, as a share of the usual price.</summary>
    public static double Prices(Race race) => race.Prices;
}
