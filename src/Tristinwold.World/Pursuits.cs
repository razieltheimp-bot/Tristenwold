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
    /// <summary>How many wings have been added to the finished house: as many as the hero cares to build.</summary>
    public int Rooms { get; set; }
    /// <summary>The hero has listed this house as their home: the ring brings them here, and companions not with them wait and work here.</summary>
    public bool IsHome { get; set; }
    /// <summary>How far along each station's next job is, in seconds, and how many crops the farm plot has brought in.</summary>
    public Dictionary<string, double> Work { get; set; } = [];
    public int Harvests { get; set; }
    /// <summary>The house as a shop, if the hero set one up: what's for sale from the chest, what they've told their company to buy for it, and the crowns in the till.</summary>
    public HashSet<string> ForSale { get; set; } = [];
    public HashSet<string> Buying { get; set; } = [];
    public int Till { get; set; }
    public HashSet<string> Furniture { get; set; } = [];
    public Dictionary<string, int> Chest { get; set; } = [];
    public List<string> Trophies { get; set; } = [];
    public bool Finished => Built >= Housing.Stages.Count;
    public BuildStep? NextStage => Built < Housing.Stages.Count ? Housing.Stages[Built] : null;
}

/// <summary>
/// A home that runs itself. Companions not travelling with the hero stay at the house, and each one works a station:
/// the farm plot grows food, and the forge, the alchemy bench and the hearth turn what's in the chest into what's made
/// from it, a job at a time, back into the chest. A station with nobody at it, or with nothing to work, stands idle.
/// </summary>
public static class Homestead
{
    /// <summary>How long a companion takes over one job.</summary>
    public const double JobSeconds = 90;

    /// <summary>The stations, by the furniture that makes them, and what's worked at each (none: the farm plot, which grows).</summary>
    public static readonly IReadOnlyList<(string Station, Job? Job)> Stations =
        [("shop_counter", null), ("farm_plot", null), ("home_forge", Job.Smithing), ("alchemy_bench", Job.Herbalism), ("hearth", Job.Cooking)];

    /// <summary>What a customer pays, and what the shop pays a seller: half, as any shop does.</summary>
    public static int SellsFor(string id) => Math.Max(1, Items.Get(id)?.Value ?? 1);
    public static int BuysFor(string id) => Math.Max(1, SellsFor(id) / 2);

    /// <summary>A customer at the counter: one thing sold off the list from the chest into the till, and one thing bought for the chest out of it. False if the hero's told their company to do neither, or neither can be done.</summary>
    static bool Serve(House h)
    {
        var did = false;
        if (h.ForSale.Where(h.Chest.ContainsKey).Order(StringComparer.Ordinal).FirstOrDefault() is { } sell)
        {
            if (--h.Chest[sell] <= 0) h.Chest.Remove(sell);
            h.Till += SellsFor(sell);
            did = true;
        }
        if (h.Buying.Where(id => h.Till >= BuysFor(id)).Order(StringComparer.Ordinal).FirstOrDefault() is { } buy)
        {
            h.Till -= BuysFor(buy);
            h.Chest[buy] = h.Chest.GetValueOrDefault(buy) + 1;
            did = true;
        }
        return did;
    }

    /// <summary>Whether a companion's free to work at home: not at the hero's heels, and not in a wagon's shafts.</summary>
    public static bool Free(Bond b) => !b.Following && !b.Hitched;

    /// <summary>What each worked station does in this much time: what came of it, in the chest now.</summary>
    public static List<(string Id, int N)> Tick(House h, int workers, double seconds)
    {
        var made = new List<(string, int)>();
        if (!h.Finished || !h.IsHome) return made;
        foreach (var (station, job) in Stations.Where(s => h.Furniture.Contains(s.Station)).Take(workers))
        {
            h.Work[station] = h.Work.GetValueOrDefault(station) + seconds;
            while (h.Work[station] >= JobSeconds)
            {
                h.Work[station] -= JobSeconds;
                if (station == "shop_counter")
                {
                    if (!Serve(h)) { h.Work[station] = 0; break; } // a counter and nothing on it: nobody comes
                }
                else if (job is { } j)
                {
                    var r = Recipes.For(j).Where(r => !Forging.Unique(r) && r.Needs.All(n => h.Chest.GetValueOrDefault(n.Id) >= n.N)).OrderByDescending(r => r.Level).ThenBy(r => r.Makes, StringComparer.Ordinal).FirstOrDefault();
                    if (r is null) { h.Work[station] = 0; break; } // nothing to work: they stand about till there is
                    foreach (var (id, n) in r.Needs)
                        if ((h.Chest[id] -= n) <= 0) h.Chest.Remove(id);
                    h.Chest[r.Makes] = h.Chest.GetValueOrDefault(r.Makes) + r.Count;
                    made.Add((r.Makes, r.Count));
                }
                else
                {
                    var crops = Farming.Crops.Where(c => c.Level <= 3).ToList();
                    var crop = crops[h.Harvests++ % crops.Count];
                    h.Chest[crop.Gives] = h.Chest.GetValueOrDefault(crop.Gives) + crop.Min;
                    made.Add((crop.Gives, crop.Min));
                }
            }
        }
        return made;
    }
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
