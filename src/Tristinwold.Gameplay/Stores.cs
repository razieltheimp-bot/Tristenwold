using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tristinwold.Gameplay;

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

    /// <summary>What's changed at home while the hero was out: the work, the till, the chest and the shop's lists, as the shared world has them.</summary>
    public void Mirror(House from)
    {
        (Work, Harvests, ForSale, Buying, Till, Chest) = (from.Work, from.Harvests, from.ForSale, from.Buying, from.Till, from.Chest);
    }
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

    /// <summary>What a customer pays, and what the shop pays a seller: a player's shop beats any local shop by a crown each way.</summary>
    public static int SellsFor(string id) => Math.Max(1, (Items.Get(id)?.Value ?? 1) - 1);
    public static int BuysFor(string id) => Math.Min(SellsFor(id), Math.Max(1, (Items.Get(id)?.Value ?? 1) / 2) + 1);

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

    /// <summary>Whether a companion's free to work at home: not at the hero's heels, not in a wagon's shafts, and not keeping shop somewhere else.</summary>
    public static bool Free(Bond b) => !b.Following && !b.Hitched && b.Keeps is null or Keeping.Home;

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
/// Gear the hero gives their company. An apprentice or squire carries a weapon and an off-hand and wears a helm and
/// armour, like the hero; a beast, a mount or a machine wears armour, head and body. What it's given adds to theirs:
/// a share more damage, armour, and (forged) a share more health. Given from the hero's pack, and back into it.
/// </summary>
public static class Kit
{
    public static IReadOnlyList<Slot> Slots(Bond b) => PartyRules.IsApprentice(b) ? [Slot.Weapon, Slot.Offhand, Slot.Head, Slot.Body] : [Slot.Head, Slot.Body];

    /// <summary>Whether a piece can be put on this one: something worn, in a slot they have.</summary>
    public static bool Fits(Bond b, Item item) => item.Wearable && Slots(b).Contains(item.Slot) && !item.Bound;

    /// <summary>Puts a piece on them out of the hero's pack; what was in that slot goes back to the pack. A two-handed weapon takes the off-hand's place too.</summary>
    public static bool Give(Bond b, Pack from, string id)
    {
        if (Items.Get(id) is not { } item || !Fits(b, item) || !from.Take(id)) return false;
        TakeBack(b, from, item.Slot);
        if (item.Slot == Slot.Weapon && item.Grip is Grip.TwoHand or Grip.Ranged) TakeBack(b, from, Slot.Offhand);
        if (item.Slot == Slot.Offhand && b.Gear.TryGetValue(Slot.Weapon, out var held) && Items.Get(held) is { Grip: Grip.TwoHand or Grip.Ranged }) TakeBack(b, from, Slot.Weapon);
        b.Gear[item.Slot] = id;
        return true;
    }

    public static void TakeBack(Bond b, Pack to, Slot slot)
    {
        if (b.Gear.Remove(slot, out var id)) to.Add(id);
    }

    /// <summary>What they're wearing adds up to: a share more damage, armour, and a share more health.</summary>
    public static (double Damage, double Armor, double Health) Adds(Bond b) =>
        (b.Gear.Values.Sum(id => Items.Get(id)?.Damage ?? 0), b.Gear.Values.Sum(id => Items.Get(id)?.Armor ?? 0), b.Gear.Values.Sum(id => Items.Get(id)?.Health ?? 0));
}

/// <summary>
/// One of the company keeping shop for the hero: an apprentice or squire sent to mind the counter at home, at the
/// wagon, or aboard the ship. They serve one customer at a time, as a home's company does (<see cref="Homestead"/>).
/// </summary>
public static class Keeping
{
    public const string Home = "home", Wagon = "wagon", Ship = "ship";
    /// <summary>How a keeper is told from other folk to talk to.</summary>
    public const string NpcMark = "keeper:";

    /// <summary>Who can keep shop: the hero's people, not their beasts or machines.</summary>
    public static bool CanKeep(Bond b) => b.Role is BondRole.Squire or BondRole.Apprentice;
}

/// <summary>
/// A hero's home as the shared world keeps it, so it goes on working whether they're there or not: the house, how many
/// of their people are there, and where other players find its shop.
/// </summary>
public sealed class PlayerStore
{
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public int Workers { get; set; }
    /// <summary>A steam wagon opened up as a shop, not a shopfront on the row, and which way it stands.</summary>
    public bool Wagon { get; set; }
    public double Yaw { get; set; }
    public House House { get; set; } = new();

    /// <summary>How a company's wagon shop is told from its home, in the book of stores.</summary>
    public const string WagonMark = "#wagon";
    /// <summary>And its ship's: a shop aboard, wherever she's tied up.</summary>
    public const string ShipMark = "#ship";
    /// <summary>A shop aboard a ship, not a shopfront on the row.</summary>
    public bool Ship { get; set; }

    /// <summary>Whether anyone's minding the counter, and there's a counter to mind.</summary>
    [JsonIgnore] public bool Open => Workers > 0 && House.Furniture.Contains("shop_counter") && House.ForSale.Count + House.Buying.Count > 0;

    /// <summary>A copy of a house, so the city's and the hero's don't share a chest.</summary>
    public static House Copy(House h) => JsonSerializer.Deserialize<House>(JsonSerializer.Serialize(h))!;
}

/// <summary>What a steam machine is built from and what it burns. Anything a fire takes will do, and the better the maker, the further it goes.</summary>
public static class SteamWorks
{
    public static readonly IReadOnlyList<(string Id, int N)> Needs = [("iron_ingot", 8), ("iron_nails", 16), ("scrap_iron", 6)];
    /// <summary>How much steam the firebox holds, in seconds.</summary>
    public const double MostFuel = 1800;

    /// <summary>How long one of something keeps a boiler hot, or 0 if it won't burn.</summary>
    public static double Burns(string id, int makerLevel)
    {
        var burn = id == "coal" ? 120.0 : Recipes.Fuels.Contains(id) ? 60 : 0;
        return burn * (1 + 0.1 * Math.Max(0, makerLevel - 1));
    }
}

public sealed partial class CityEconomy
{
    /// <summary>The heroes' homes and shops, by company. They run on a clock of their own, for as long as the server's up, whoever's online.</summary>
    public Dictionary<string, PlayerStore> Stores { get; set; } = [];

    public PlayerStore? StoreOf(string owner) => Stores.GetValueOrDefault(owner);

    /// <summary>What a player's shop asks and pays today: a crown better than any local shop, each way.</summary>
    public int StorePrice(Item item) => Math.Max(1, Price(item) - 1);
    public int StoreOffer(Item item) => Math.Min(StorePrice(item), Offer(item) + 1);

    /// <summary>Time passes at everyone's homes: their people work, and customers come.</summary>
    public void TendStores(double seconds)
    {
        foreach (var s in Stores.Values) Homestead.Tick(s.House, s.Workers, seconds);
    }

    /// <summary>A visitor buys one thing from a shop: out of its chest, the crowns into its till.</summary>
    bool StoreBuy(string owner, string id, int crowns)
    {
        if (StoreOf(owner) is not { Open: true, House: var h } || !h.ForSale.Contains(id) || h.Chest.GetValueOrDefault(id) <= 0) return false;
        if (--h.Chest[id] <= 0) h.Chest.Remove(id);
        h.Till += crowns;
        return true;
    }

    /// <summary>A visitor sells one thing to a shop: into its chest, out of its till.</summary>
    bool StoreSell(string owner, string id, int crowns)
    {
        if (StoreOf(owner) is not { Open: true, House: var h } || !h.Buying.Contains(id) || h.Till < crowns) return false;
        h.Till -= crowns;
        h.Chest[id] = h.Chest.GetValueOrDefault(id) + 1;
        return true;
    }
}
