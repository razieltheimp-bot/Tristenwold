using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>A seed in the ground: what, and when it went in.</summary>
public sealed class Planting
{
    public string Seed { get; set; } = "";
    public double PlantedAt { get; set; }
}

/// <summary>A snare set out in the wilds: where, on what land, and when it will have caught something.</summary>
public sealed class Snare
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public string Biome { get; set; } = "";
    public double ReadyAt { get; set; }
}

/// <summary>One clue of a treasure hunt: the landmark it names, and where to dig.</summary>
public sealed record Clue(string Landmark, double LandmarkX, double LandmarkZ, double X, double Z, string Text);

/// <summary>A treasure hunt under way: its clues, and how far along it is.</summary>
public sealed class TreasureHunt
{
    public List<Clue> Clues { get; set; } = [];
    public int Step { get; set; }
    public Clue? Current => Step < Clues.Count ? Clues[Step] : null;
    public bool Last => Step == Clues.Count - 1;
}

/// <summary>A hero's house: whether they own the plot, how far it's built, what's in it.</summary>
public sealed class House
{
    public bool Owned { get; set; }
    /// <summary>How many of <see cref="Housing.Stages"/> are done.</summary>
    public int Built { get; set; }
    public HashSet<string> Furniture { get; set; } = [];
    public Dictionary<string, int> Chest { get; set; } = [];
    public List<string> Trophies { get; set; } = [];
    public bool Finished => Built >= Housing.Stages.Count;
    public BuildStep? NextStage => Built < Housing.Stages.Count ? Housing.Stages[Built] : null;
}

/// <summary>
/// Everything a hero does besides fight and trade: their plots (the allotments' and their own garden's), their
/// house, the snares they've set, the treasure hunt they're on, the marks who caught them at it, how rested they
/// are, and the feats they've done (with the counts behind them). Saved with the company.
/// </summary>
public sealed class PursuitBook
{
    public Dictionary<string, Planting> Plots { get; set; } = [];
    public House House { get; set; } = new();
    public List<Snare> Snares { get; set; } = [];
    public TreasureHunt? Hunt { get; set; }
    public Dictionary<string, double> WaryUntil { get; set; } = [];
    public double RestedUntil { get; set; }
    public double CharmReadyAt { get; set; }
    public HashSet<string> Feats { get; set; } = [];
    public Dictionary<string, int> Counters { get; set; } = [];
    public int NextSnare { get; set; }

    /// <summary>Counts something done, and returns any feats it earned.</summary>
    public List<Feat> Count(string counter, int by = 1) => Set(counter, Counters.GetValueOrDefault(counter) + by);

    /// <summary>Sets a count outright (the best trade level, say), and returns any feats it earned.</summary>
    public List<Feat> Set(string counter, int value)
    {
        Counters[counter] = Math.Max(Counters.GetValueOrDefault(counter), value);
        var earned = Gameplay.Feats.Earned(counter, Counters[counter], Feats).ToList();
        foreach (var f in earned) Feats.Add(f.Id);
        return earned;
    }

    public int Points => Gameplay.Feats.Points(Feats);
}

/// <summary>
/// Treasure hunts, from a torn map: two to four clues, each naming a landmark near the last (a village, a
/// farmstead, a town, the gates of a city) and how far from it to dig, and which way. Bandit camps and lairs are
/// never the landmark; the spot is always dry land.
/// </summary>
public static class Treasure
{
    static readonly (string Name, double Dx, double Dz)[] Ways =
    [
        ("north", 0, -1), ("north-east", 0.7071, -0.7071), ("east", 1, 0), ("south-east", 0.7071, 0.7071),
        ("south", 0, 1), ("south-west", -0.7071, 0.7071), ("west", -1, 0), ("north-west", -0.7071, -0.7071),
    ];

    public static TreasureHunt NewHunt(WorldGen world, Rng rng, double fromX, double fromZ)
    {
        var hunt = new TreasureHunt();
        var (x, z) = (fromX, fromZ);
        var used = new HashSet<string>();
        var steps = 2 + rng.Next(3);
        for (var i = 0; i < steps; i++)
        {
            var near = world.SettlementsNear(x, z, 1200).Where(s => !s.Hostile && !used.Contains(s.Id) && s.DistanceTo(x, z) > 60)
                .OrderBy(s => s.DistanceTo(x, z)).Take(5).ToList();
            if (near.Count == 0) break;
            var mark = near[rng.Next(near.Count)];
            used.Add(mark.Id);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var way = Ways[rng.Next(Ways.Length)];
                var paces = (int)Math.Round(mark.Radius + 12 + rng.NextDouble() * 20);
                var (dx, dz) = (mark.X + way.Dx * paces, mark.Z + way.Dz * paces);
                if (world.Height(dx, dz) < WorldGen.WaterLevel + 0.6) continue;
                var name = mark.IsCity ? $"the gates of {mark.Name}" : mark.Name;
                hunt.Clues.Add(new Clue(name, mark.X, mark.Z, dx, dz,
                    $"{(i == 0 ? "The map shows" : "The next scrap shows")} {name}, {mark.What}, with a mark {paces} paces {way.Name} of it."));
                (x, z) = (dx, dz);
                break;
            }
        }
        return hunt;
    }

    /// <summary>How close a dig must be to the spot (metres).</summary>
    public const double Reach = 6;

    /// <summary>A hint from how far off a dig was.</summary>
    public static string Warmth(double metres) => metres switch
    {
        < 15 => "You're very close.",
        < 40 => "Close, but not here.",
        < 120 => "Not here. The landmark is near, though.",
        _ => "Nowhere near.",
    };

    /// <summary>What's in the chest at the end: coin and something good.</summary>
    public static (int Crowns, List<(string Id, int N)> Items) Reward(Rng rng, int clues)
    {
        var crowns = 40 * clues + rng.Next(60);
        string[] good = ["pearl", "rough_sapphire", "rough_garnet", "gold_ingot", "silver_ingot", "elixir_of_might", "greater_healing", "garnet_ring", "pearl_necklace"];
        var items = new List<(string, int)> { ("ancient_coin", 1 + rng.Next(4)), (good[rng.Next(good.Length)], 1) };
        if (clues >= 4 || rng.Chance(0.2)) items.Add((good[rng.Next(good.Length)], 1));
        return (crowns, items);
    }
}
