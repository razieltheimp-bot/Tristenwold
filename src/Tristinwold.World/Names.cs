using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// Names for everything the generator makes. Places follow the reworked cities (Galeholt,
/// Cinderhelm, Mirewatch): two old words run together. People have Nordic-ish given names
/// and family names; Ashborn take single names.
/// </summary>
public static class Names
{
    static readonly string[] Heads =
    [
        "Ash", "Black", "Bram", "Briar", "Cairn", "Cinder", "Cold", "Crow", "Dun", "Dusk", "Elder", "Ember",
        "Fen", "Frost", "Gloam", "Grey", "Hare", "Hawk", "Hollow", "Iron", "Kettle", "Lark", "Marrow", "Moss",
        "North", "Oak", "Pale", "Raven", "Rime", "Rook", "Salt", "Shale", "Silver", "Sleet", "Stag", "Stone",
        "Tallow", "Thorn", "Umber", "Vesper", "Wether", "Whit", "Winter", "Wolf", "Wren", "Yew", "Bitter", "Gale",
        "Mire", "Dread", "Hearth", "Sorrow", "Barrow", "Copper", "Fell", "Heron", "Lantern", "Bell",
    ];

    static readonly string[] Tails =
    [
        "holt", "gate", "helm", "deep", "moor", "watch", "wake", "reach", "ford", "stead", "fell", "mere",
        "wick", "crag", "hollow", "thorpe", "cairn", "brook", "hearth", "mark", "rest", "by", "ton", "den",
        "well", "ridge", "vale", "haven", "march", "barrow", "shaw", "combe",
    ];

    /// <summary>A place name like "Rimeford" or "Crowhollow".</summary>
    public static string Place(Rng rng)
    {
        while (true)
        {
            var head = rng.Pick(Heads);
            var tail = rng.Pick(Tails);
            if (head.EndsWith(tail, StringComparison.OrdinalIgnoreCase) || head.ToLowerInvariant() == tail) continue;
            if (char.ToLowerInvariant(head[^1]) == tail[0] && tail.Length < 4) continue; // no "Wolffford"
            return head + tail;
        }
    }

    static readonly string[] Women =
    [
        "Asta", "Brynja", "Dagny", "Eira", "Freydis", "Gudrun", "Hilde", "Idun", "Inga", "Jorunn", "Kari", "Liv",
        "Maren", "Nanna", "Oda", "Ragna", "Runa", "Sigrid", "Solveig", "Thora", "Tove", "Unn", "Vigdis", "Yrsa",
        "Alva", "Berit", "Elin", "Hella", "Ilse", "Kelda", "Mira", "Senna", "Tamsin", "Wenna", "Corva", "Edda",
    ];

    static readonly string[] Men =
    [
        "Arn", "Bjorn", "Dag", "Egil", "Finn", "Gorm", "Halvard", "Ivar", "Jarl", "Ketil", "Leif", "Magnus",
        "Njal", "Orm", "Ragnar", "Sten", "Torvald", "Ulf", "Vidar", "Yngve", "Aldric", "Bram", "Cael", "Davor",
        "Eamon", "Garrick", "Hesk", "Jory", "Kesten", "Lorne", "Mathis", "Oskar", "Piet", "Roderic", "Tobin", "Wil",
    ];

    static readonly string[] Either = ["Ash", "Briar", "Ren", "Sorrel", "Wynn", "Ellis", "Rook", "Sable", "Teague", "Vale"];

    static readonly string[] Ashborn =
    [
        "Kesh", "Ysra", "Vael", "Oriax", "Thessa", "Morn", "Ziva", "Caul", "Ember", "Ashk", "Nyx", "Sere",
        "Ilith", "Vorr", "Kaela", "Drass",
    ];

    static readonly string[] FamilyHeads =
    [
        "Ash", "Black", "Bright", "Cold", "Crow", "Dark", "Ember", "Fair", "Frost", "Grey", "Hammer", "Iron",
        "Long", "Oak", "Quick", "Raven", "Red", "Salt", "Shield", "Silver", "Snow", "Stone", "Storm", "Thorn",
        "War", "White", "Wolf", "Wind", "Kettle", "Mead",
    ];

    static readonly string[] FamilyTails =
    [
        "mane", "brand", "hand", "shield", "hammer", "water", "brook", "wood", "born", "sworn", "heart", "field",
        "song", "wright", "hollow", "barrow", "ward", "helm", "beard", "cloak",
    ];

    public static string Given(Rng rng, Sex sex) => sex switch
    {
        Sex.Female => rng.Pick(Women),
        Sex.Male => rng.Pick(Men),
        _ => rng.Chance(0.5) ? rng.Pick(Either) : rng.Pick(rng.Chance(0.5) ? Women : Men),
    };

    public static string Family(Rng rng) => rng.Pick(FamilyHeads) + rng.Pick(FamilyTails);

    public static string Person(Rng rng, Sex sex, bool ashborn = false) =>
        ashborn ? rng.Pick(Ashborn) + (rng.Chance(0.4) ? " of the " + rng.Pick(["Rift", "Ash", "Seventh Gate", "Long Burn"]) : "")
                : $"{Given(rng, sex)} {Family(rng)}";

    static readonly string[] InnAdjectives = ["Drunken", "Sleeping", "Frozen", "Bent", "Laughing", "Silver", "Black", "Lost", "Crooked", "Lonely", "Golden", "Weeping"];
    static readonly string[] InnNouns = ["Stag", "Gull", "Kettle", "Hound", "Lantern", "Crow", "Barrel", "Bell", "Mare", "Boar", "Anchor", "Fiddle"];

    public static string Inn(Rng rng) => $"The {rng.Pick(InnAdjectives)} {rng.Pick(InnNouns)}";

    /// <summary>The faiths of the reworked cities, plus a few of the wilds.</summary>
    public static readonly string[] Divines =
    [
        "the Four Winds", "the Seven Lights", "the Watchful Sun", "the Veiled Lady", "the Door", "the Loom",
        "the Drowned Mother", "the Antlered King", "the Last Ember",
    ];

    public static string Divine(Rng rng) => rng.Pick(Divines);
}

public enum Sex { Female, Male, Other }
