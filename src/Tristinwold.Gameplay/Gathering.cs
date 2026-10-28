using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What kind of water a line goes into: the harbour and the sea, a lake or river, or icy northern water.</summary>
public enum Water { Sea, Fresh, Cold }

/// <summary>
/// Fishing: cast, wait for a bite, and strike while the float's under. A better angler waits less, has longer to
/// strike, and lands the rarer fish more often; what bites depends on the water. Now and then it's a boot.
/// </summary>
public static class Fishing
{
    /// <summary>(fish, weight, the fishing level it starts biting at).</summary>
    static (string Id, double Weight, int Level)[] Table(Water w) => w switch
    {
        Water.Sea => [("herring", 30, 1), ("mackerel", 22, 1), ("cod", 16, 2), ("sea_bass", 9, 3), ("eel", 7, 3), ("lobster", 4, 5), ("salmon", 4, 4), ("pearl", 1.2, 6), ("old_boot", 4, 1)],
        Water.Fresh => [("perch", 30, 1), ("carp", 22, 1), ("trout", 18, 2), ("pike", 9, 3), ("eel", 5, 3), ("salmon", 3, 5), ("golden_carp", 0.8, 6), ("old_boot", 4, 1)],
        _ => [("arctic_char", 30, 1), ("trout", 20, 1), ("ice_pike", 8, 4), ("salmon", 6, 3), ("old_boot", 3, 1)],
    };

    /// <summary>Everything that can be caught in a water, and from what level.</summary>
    public static IEnumerable<(string Id, int Level)> Catches(Water w) => Table(w).Select(t => (t.Id, t.Level));

    /// <summary>How long until something bites (seconds): five to twelve for a beginner, two to six for a master.</summary>
    public static double BiteDelay(int level, Rng rng) => (5 - 0.33 * (level - 1)) + rng.NextDouble() * (7 - 0.6 * (level - 1));

    /// <summary>How long the float stays under: the time there is to strike.</summary>
    public static double Window(int level) => 0.9 + 0.12 * (level - 1);

    /// <summary>
    /// What's on the hook. Fish above the angler's level don't bite; the rare ones come more often as they improve.
    /// </summary>
    public static string Catch(Water w, int level, Rng rng)
    {
        var table = Table(w).Where(t => t.Level <= level)
            .Select(t => (t.Id, Weight: t.Id == "old_boot" ? t.Weight / (1 + 0.3 * (level - 1)) : t.Weight * (1 + 0.15 * (level - t.Level) * (t.Level > 1 ? 1 : 0)))).ToList();
        return rng.PickWeighted(table);
    }

    /// <summary>Fishing experience for a catch: more for a better fish.</summary>
    public static int Xp(string id) => id == "old_boot" ? 2 : 6 + (Items.Get(id)?.Value ?? 0) / 2;
}

/// <summary>An ore vein's rock: a vein gives what it's made of, and its rarer ones need a better miner.</summary>
public sealed record Vein(string Id, string Name, string Ore, int Level, string Colour);

/// <summary>A tree that can be felled at a woodlot: what it gives, and what it takes.</summary>
public sealed record Timber(string Id, string Name, string Log, int Level);

/// <summary>
/// Mining and woodcutting: a vein or a tree takes a few strokes (fewer as you get better), gives ore or logs (more
/// as you get better), and comes back in a while. Mining turns up stone with the ore, and now and then a gem.
/// </summary>
public static class Gathering
{
    public static readonly IReadOnlyList<Vein> Veins =
    [
        new("stone", "Grey Stone", "stone_block", 1, "#8a8a84"),
        new("iron", "Iron Vein", "iron_ore", 1, "#a0522d"),
        new("coal", "Coal Seam", "coal", 1, "#2a2a2c"),
        new("silver", "Silver Vein", "silver_ore", 3, "#c8ccd4"),
        new("gold", "Gold Vein", "gold_ore", 5, "#e0b040"),
    ];

    public static readonly IReadOnlyList<Timber> Trees =
    [
        new("pine", "Pine", "pine_log", 1),
        new("oak", "Oak", "oak_log", 2),
        new("yew", "Yew", "yew_log", 4),
    ];

    public static Vein Vein(string id) => Veins.First(v => v.Id == id);
    public static Timber Tree(string id) => Trees.First(t => t.Id == id);

    /// <summary>Strokes of the pick or axe it takes: four for a beginner, one for a master.</summary>
    public static int Strokes(int level) => Math.Max(1, 4 - (level - 1) / 3);

    /// <summary>What a vein gives up: its ore (one to three), and with luck, a rough gem.</summary>
    public static List<(string Id, int N)> Mine(Vein v, int level, Rng rng)
    {
        var got = new List<(string, int)> { (v.Ore, 1 + (rng.Chance(0.1 * level) ? 1 : 0) + (level >= 7 && rng.Chance(0.3) ? 1 : 0)) };
        if (v.Id != "stone" && rng.Chance(0.25)) got.Add(("stone_block", 1));
        if (v.Id != "stone" && v.Id != "coal" && rng.Chance(0.015 * level)) got.Add((level >= 5 && rng.Chance(0.4) ? "rough_sapphire" : "rough_garnet", 1));
        return got;
    }

    /// <summary>What a felled tree gives: two to four logs.</summary>
    public static int Logs(int level, Rng rng) => 2 + (rng.Chance(0.1 * level) ? 1 : 0) + (level >= 6 && rng.Chance(0.4) ? 1 : 0);

    public static int MiningXp(Vein v) => 5 + 4 * v.Level;
    public static int WoodXp(Timber t) => 6 + 4 * t.Level;

    /// <summary>How long a mined vein or felled tree takes to come back (seconds of play).</summary>
    public const double RegrowSeconds = 8 * 60;
}
