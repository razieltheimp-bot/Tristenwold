namespace Tristinwold.Gameplay;

/// <summary>One step in a place growing into a town: what it builds, what that takes, and what it brings.</summary>
public sealed record GrowthStep(int Stage, string Name, string Builds, IReadOnlyList<(string Id, int N)> Needs, int Crowns, string Gives);

/// <summary>
/// A refuge grows into a town as heroes bring it what it needs, a step at a time: a tavern (somewhere to stay, a bar to
/// make it home at), then a market (traders, a general store), then a council chamber (a council sits, and a hero can
/// win a seat on it), then a barracks (town guards can be hired; till then its own people defend it), and only then a
/// fortified wall and more homes, and it's a town. A colony is founded with its tavern already up, and goes on from there.
/// </summary>
public static class Settling
{
    public const int Tavern = 1, Market = 2, Chamber = 3, Barracks = 4, Walls = 5;
    /// <summary>Where a hero's colony starts: it's founded with its tavern.</summary>
    public const int ColonyStarts = Tavern;

    public static readonly IReadOnlyList<GrowthStep> Steps =
    [
        new(Tavern, "a tavern", "tavern", [("pine_log", 12), ("stone_block", 6)], 120,
            "Travellers can stay the night, and a hero can make it home at the bar."),
        new(Market, "a market", "store", [("pine_log", 10), ("iron_ingot", 4), ("linen", 4)], 200,
            "Traders come, and a general store opens."),
        new(Chamber, "a council chamber", "council", [("stone_block", 14), ("pine_log", 8)], 300,
            "A council sits, and a hero can win a seat on it."),
        new(Barracks, "a barracks", "barracks", [("stone_block", 16), ("pine_log", 10), ("iron_ingot", 6)], 350,
            "Town guards can be hired to keep the place: till now its own people have had to defend it. And with men to man it, a wall can go up."),
        new(Walls, "a fortified wall and more homes", "house", [("stone_block", 30), ("pine_log", 16), ("iron_nails", 20)], 500,
            "A stone wall with a gatehouse, and room for more families: it's a town now."),
    ];

    public static GrowthStep? Next(int stage) => Steps.FirstOrDefault(s => s.Stage == stage + 1);

    /// <summary>How a place's growth is kept: its own id, or a colony's town.</summary>
    public static bool IsColony(string town) => town.StartsWith("colony:");
}

public sealed partial class CityEconomy
{
    /// <summary>How far each refuge or colony has grown (see <see cref="Settling"/>): refuges start at 0, colonies at their tavern.</summary>
    public Dictionary<string, int> Growth { get; set; } = [];

    /// <summary>
    /// How growth was kept before the barracks came in, when the walls were step 4: a world saved then is read into
    /// <see cref="Growth"/>, a place that had its walls keeping them (and getting its barracks for nothing). Never written.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Grown
    {
        get => null;
        set
        {
            if (value is null) return;
            foreach (var (town, n) in value) Growth.TryAdd(town, n >= Settling.Barracks ? Settling.Walls : n);
        }
    }

    public int GrownOf(string town) => Growth.TryGetValue(town, out var n) ? n : Settling.IsColony(town) ? Settling.ColonyStarts : 0;

    /// <summary>The next step built, for everyone: only ever the next one.</summary>
    bool Grow(string town, int to)
    {
        if (town.Length is 0 or > 80 || to != GrownOf(town) + 1 || to > Settling.Steps.Count) return false;
        Growth[town] = to;
        return true;
    }
}
