namespace Tristinwold.Gameplay;

/// <summary>
/// A young animal the beastkeeper raised by hand: tame already, sold by how dangerous it grows up to be
/// (<see cref="Danger"/>, 1 to 5) and how hard it is to train (<see cref="Training"/>, 1 to 5).
/// </summary>
public sealed record PetOffer(string KindId, int Danger, int Training, string Text)
{
    public string Name => Taming.YoungName(KindId, Bestiary.Get(KindId).Name);

    /// <summary>The asking price: danger counts for most, then the years of patience in it.</summary>
    public int BasePrice => (int)Math.Round(25 * Math.Pow(Danger, 1.7) * (0.5 + 0.5 * Training) / 5) * 5;
}

/// <summary>
/// Wren's Menagerie by the Gallows Perch: every beast that can be tamed, raised from birth and sold young. Never a
/// dragon: no one sells those, and no one could; a dragon has to be found as an egg and hatched.
/// </summary>
public static class Menagerie
{
    public static readonly IReadOnlyList<PetOffer> Stock =
    [
        new("fox", 1, 1, "Quick and clever; it'll find every rabbit for a mile."),
        new("deer", 1, 1, "Gentle, and it'll carry your herbs. It won't fight much."),
        new("giant_rat", 1, 2, "Filthy, loyal, and it bites above its size."),
        new("hawk", 1, 3, "Falconry takes patience. It'll be your eyes in the sky, and its talons sting."),
        new("mud_crab", 2, 1, "A shell like a shield. Feed it and it's happy; it isn't clever."),
        new("wolf", 2, 2, "The road's oldest companion. A wolf grown at your side is worth two swords."),
        new("boar", 2, 3, "Stubborn as a mule and twice as tough. It charges first and thinks never."),
        new("goblin", 2, 4, "Cunning, and it steals. Raised right, it steals for you."),
        new("ice_wolf", 3, 3, "From the far north. Its bite freezes; it needs a firm hand."),
        new("deathcrest_ape", 3, 4, "Its crest is poison. Clever hands, a temper to match."),
        new("winged_ape", 3, 4, "It climbs, it glides, it hits hard. It sulks if it's bored."),
        new("spriggan", 3, 5, "Grown from a seed of the old woods. It heals the ground it walks on, and hates fire."),
        new("bear", 4, 3, "A cave bear cub. In a year it'll be bigger than you, and much stronger."),
        new("kipine", 4, 4, "Part owl, part bear, part stag, with wings. Mind the antlers."),
        new("frost_troll", 5, 5, "A troll whelp. It'll break anything that comes for you, and anything you leave lying about."),
    ];

    /// <summary>What it costs a tamer of this level: 3% off for each level of beast taming past the first.</summary>
    public static int Price(PetOffer offer, int tamingLevel, double markup = 1) =>
        (int)Math.Round(offer.BasePrice * (1 - 0.03 * Math.Clamp(tamingLevel - 1, 0, Pack.MaxJobLevel - 1)) * markup);

    /// <summary>The stars a number of 1 to 5 is shown as.</summary>
    public static string Stars(int n) => new string('*', n) + new string('.', 5 - n);
}
