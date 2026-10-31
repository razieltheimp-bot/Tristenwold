using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// A pirate crew, the sea's answer to a barbarian clan: its ship, its captain and the cove it runs home to. The small ones
/// work one stretch of water and then another, a sea-lane at a time, worked out from the crew and the time alone, so every
/// company hears of them in the same place. They can't be joined by asking: they take on whoever is still on their feet
/// when they've won a ship. A crew beaten off goes quiet for an hour.
/// </summary>
public sealed record PirateCrew(string Id, string Name, string Ship, string Captain, string Haven, int Hands, int Nearest, int Furthest, ulong Seed, bool Good = false)
{
    /// <summary>How many small crews there are, and how long one works a lane before it moves on.</summary>
    public const int Count = 14;
    public const double Round = 1200, Quiet = 300, BrokenSeconds = 3600;
    /// <summary>What a beaten crew's purse is worth, by how many hands it has.</summary>
    public int Bounty => 40 + 15 * Hands;
    const ulong Salt = 0x5EA_C4EB;

    /// <summary>Black Ysolde's crew, out of the Teeth, who answer to Soren.</summary>
    public static readonly PirateCrew WidowsTooth = new("widows_tooth", "the Widow's Tooth", "the Widow's Tooth", "Black Ysolde", "the Teeth", 6, 1, 8, 0);
    /// <summary>Lu Wukong's crew: they rob nobody but pirates, and they stand between Soren and the towns.</summary>
    public static readonly PirateCrew DrunkenMonkey = new("drunken_monkey", "the Drunken Monkey's crew", "the Drunken Monkey", "Lu Wukong", "Monkey's Rest", 6, 1, 8, 0, Good: true);

    static readonly string[] Words = ["Salt", "Bilge", "Gull", "Rust", "Tar", "Brine", "Barnacle", "Kelp", "Long", "Crooked", "Wet", "Blind"];
    static readonly string[] Things = ["Rats", "Dogs", "Hooks", "Knives", "Sisters", "Crabs", "Lanterns", "Eels", "Cousins", "Widows"];
    static readonly string[] Ships = ["Sour Herring", "Bilge Queen", "Second Helping", "Mother's Regret", "Lucky Tooth", "Wet Sixpence", "Crab's Daughter", "Fair Warning", "Patient Gull", "Empty Promise", "Last Candle", "Sorry Margaret", "Good Intentions", "Tuesday's Child"];
    static readonly string[] Firsts = ["Maggs", "Dunstan", "Tamsin", "Corbin", "Hesper", "Rook", "Oona", "Jeb", "Sal", "Wick", "Bryony", "Pike"];
    static readonly string[] Bynames = ["One-Ear", "the Tide", "Sixfingers", "the Reef", "No-Boots", "the Lamb", "Blackgum", "the Hook"];
    static readonly string[] Havens = ["Gullet Cove", "Wrecker's Creek", "the Sump", "Hangman's Inlet", "Dead Man's Bight", "the Rotten Quay", "Tarpot Bay", "Cold Comfort Cove"];

    /// <summary>The small crews on the water.</summary>
    public static readonly IReadOnlyList<PirateCrew> Small = [.. Enumerable.Range(0, Count).Select(Make)];

    static PirateCrew Make(int i)
    {
        var rng = Noise.Dice(Salt, i, 0);
        var word = Words[i % Words.Length];
        var thing = Things[(i * 7 + 3) % Things.Length];
        var nearest = 1 + rng.Next(3);
        return new PirateCrew($"crew:{i}", $"the {word} {thing}", $"the {Ships[i % Ships.Length]}", $"{Firsts[(i * 5 + 1) % Firsts.Length]} {rng.Pick(Bynames)}",
            rng.Pick(Havens), 3 + rng.Next(3), nearest, nearest + 1 + rng.Next(3), rng.NextULong());
    }

    public static PirateCrew? Get(string? id) => id switch
    {
        null => null,
        "widows_tooth" => WidowsTooth,
        "drunken_monkey" => DrunkenMonkey,
        _ => Small.FirstOrDefault(c => c.Id == id),
    };

    /// <summary>The sea-lanes it works: the runs to the ports between its nearest and furthest water.</summary>
    public IReadOnlyList<SeaPort> Lanes => [.. Seas.Ports.Concat(Seas.Uncharted).Where(p => p.Leagues >= Nearest && p.Leagues <= Furthest).OrderBy(p => p.Id, StringComparer.Ordinal)];

    /// <summary>The lane it's on at a moment of play, and which round that is. Between lanes it's at sea with nothing to show (no lane).</summary>
    public (int Round, SeaPort? Lane) Where(double playSeconds)
    {
        var round = (int)Math.Floor(playSeconds / Round);
        var lanes = Lanes;
        if (lanes.Count == 0 || playSeconds - round * Round < Quiet) return (round, null);
        return (round, lanes[(int)(Noise.Hash(Seed, round) % (ulong)lanes.Count)]);
    }
}
