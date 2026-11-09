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

    // The young get names from a far bigger pool than their elders, so a company of apprentices isn't all Tobins.
    static readonly string[] Girls =
    [
        .. Women,
        "Aelis", "Ailsa", "Annika", "Aris", "Aud", "Bera", "Bryn", "Cerys", "Dalla", "Dorte", "Eydis", "Elske", "Embla", "Esja",
        "Fenna", "Frida", "Gerd", "Greta", "Gyda", "Halla", "Hedda", "Helka", "Isolde", "Jenna", "Juna", "Katla", "Kelsa",
        "Lena", "Lotta", "Lyra", "Malla", "Marit", "Nessa", "Nora", "Orla", "Pella", "Quill", "Rhona", "Rika", "Saga",
        "Selka", "Signy", "Siv", "Svala", "Tilda", "Tyra", "Ulla", "Vala", "Vera", "Wren", "Ylva", "Ada", "Brisa", "Cilla",
        "Dessa", "Elsa", "Fia", "Gala", "Hanne", "Iona", "Jessa", "Kaia", "Lise", "Moira", "Nell", "Olwen", "Petra", "Rosa",
    ];

    static readonly string[] Boys =
    [
        .. Men,
        "Alder", "Ansel", "Ari", "Asger", "Baldur", "Benno", "Birger", "Brand", "Calder", "Colm", "Dain", "Dorian", "Einar",
        "Elric", "Erland", "Fenn", "Frode", "Gisli", "Gunnar", "Hakon", "Hallam", "Harald", "Idris", "Ingolf", "Jonas",
        "Kalle", "Kjell", "Lars", "Loki", "Lukas", "Mads", "Morten", "Nils", "Odd", "Olaf", "Osric", "Pell", "Quinn",
        "Rafe", "Rolf", "Rune", "Sigurd", "Snorri", "Soren", "Stig", "Teodor", "Tor", "Trygve", "Ulric", "Valdis", "Wulf",
        "Yorick", "Alrik", "Bastian", "Corin", "Dunstan", "Emeric", "Falk", "Gideon", "Hob", "Ivo", "Jasper", "Kit", "Linus",
    ];

    static readonly string[] YoungEither = [.. Either, "Arin", "Bay", "Cass", "Dell", "Emry", "Fable", "Glen", "Hollis", "Ivy", "Jun", "Kestrel", "Lark", "Merrow", "Nix", "Pip", "Robin", "Sky", "Tam", "Wynter", "Zephyr"];

    /// <summary>A young person's first name, from the far bigger pool kept for the young.</summary>
    public static string YoungGiven(Rng rng, Sex sex) => sex switch
    {
        Sex.Female => rng.Pick(Girls),
        Sex.Male => rng.Pick(Boys),
        _ => rng.Chance(0.5) ? rng.Pick(YoungEither) : rng.Pick(rng.Chance(0.5) ? Girls : Boys),
    };

    /// <summary>A young person's whole name.</summary>
    public static string Young(Rng rng, Sex sex) => $"{YoungGiven(rng, sex)} {Family(rng)}";

    /// <summary>How many first names the young can have, for each sex (and for the rest).</summary>
    public static (int Girls, int Boys, int Either) YoungPool => (Girls.Distinct().Count(), Boys.Distinct().Count(), YoungEither.Distinct().Count());

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
