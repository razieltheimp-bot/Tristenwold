using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>The kinds of goods Tristinwold trades in. Every item that's one of these is priced by how much of it the city has.</summary>
public enum Good { Fish, Meat, Bread, Ale, Timber, Stone, Ore, Coal, Iron, Leather, Herbs, Potions, Precious }

/// <summary>What's become of a town's buildings, for everyone on the server: burned stays burned until carpenters rebuild it.</summary>
public sealed class TownFabric
{
    /// <summary>Homes the dragon has burned, and not yet rebuilt.</summary>
    public int Burnt { get; set; }
    /// <summary>New buildings its carpenters have raised over the years.</summary>
    public int Grown { get; set; }
    /// <summary>Homes left empty when times turned hard: mouldering, boarded up, falling apart, until the good years mend them.</summary>
    public int Derelict { get; set; }
}

/// <summary>The Dread Pirate Soren at a town's gates: which, the day he'll have finished with it if nobody stops him, and whether Lu Wukong has come to stand against him.</summary>
public sealed class PirateSiege
{
    public string Town { get; set; } = "";
    public int Ends { get; set; }
    public bool Lu { get; set; }
}

/// <summary>
/// One good in the city: how much it likes to keep (<paramref name="Target"/>), how much its own people make and use
/// in a day, who makes it, who wants it, and what a unit of it is usually worth.
/// </summary>
public sealed record GoodInfo(Good Good, string Name, int Target, double Made, double Used, string Makers, string Buyer, int Worth);

/// <summary>Something that happens to the city for a few days: a storm, a festival, a caravan, a fire.</summary>
public sealed record CityEvent(string Id, string Title, string News, int Days,
    (Good Good, double By)[]? Made = null, (Good Good, double By)[]? Used = null, (Good Good, double Share)[]? Arrives = null);

/// <summary>Works the city builds when it has the goods and the coin: each makes some trade better for good.</summary>
public sealed record CityWork(string Id, string Title, string Done, (Good Good, int N)[] Needs, int Crowns, (Good Good, double By)[] Better, int DockWage = 0, double Tax = 0);

/// <summary>A business wanting a good the city's short of, and paying over the odds for it, delivered at the Exchange.</summary>
public sealed class Contract
{
    public string Id { get; set; } = "";
    public Good Good { get; set; }
    public string Buyer { get; set; } = "";
    public int Wanted { get; set; }
    public int Delivered { get; set; }
    public int PricePer { get; set; }
    public int Bonus { get; set; }
    public int Expires { get; set; }
    public int Left => Wanted - Delivered;
}

/// <summary>
/// Something a hero did to the city: sold or bought something, delivered to a contract, gave to the works, landed
/// cargo on the docks, paid the Crown for land. The game does it to its own copy of the city at once; on a server,
/// it's sent on and done to the one city everybody shares (see <see cref="CityEconomy.Apply"/>).
/// </summary>
public sealed record CityAction(string Kind, string? Item = null, int Crowns = 0, string? Contract = null, IReadOnlyList<string>? Items = null, Good? Good = null, int N = 0,
    string? Town = null, TownMarket? Market = null, PlayerStore? Store = null, Hall? Hall = null, double X = 0, double Z = 0)
{
    public static CityAction Sold(string item, int crowns) => new("sold", item, crowns);
    public static CityAction Bought(string item, int crowns) => new("bought", item, crowns);
    public static CityAction Deliver(string contract, IReadOnlyList<string> items) => new("deliver", Contract: contract, Items: items);
    public static CityAction Donate(IReadOnlyList<string> items) => new("donate", Items: items);
    public static CityAction DonateCrowns(int crowns) => new("donate_crowns", Crowns: crowns);
    public static CityAction Cargo(Good good, int n) => new("cargo", Good: good, N: n);
    public static CityAction Treasury(int crowns) => new("treasury", Crowns: crowns);
    // The roads (see Roads): a place come to know, goods put into or taken from its stores, and what befalls it.
    public static CityAction Know(TownMarket market) => new("know", Town: market.Id, Market: market);
    public static CityAction TownStock(string town, Good good, int delta) => new("town_stock", Town: town, Good: good, N: delta);
    public static CityAction CityStock(Good good, int delta) => new("city_stock", Good: good, N: delta);
    public static CityAction Plundered(string town) => new("plundered", Town: town);
    public static CityAction Steadied(string town) => new("steady", Town: town);
    public static CityAction Escorted(string town) => new("escorted", Town: town);
    public static CityAction Raiders(string town, string clan) => new("raiders", Item: clan, Town: town);
    public static CityAction Cleared(string trouble, string by) => new("cleared", Item: by, Contract: trouble);
    /// <summary>A guardian of the Northwood slain, or the dragon: everybody's business.</summary>
    public static CityAction Guardian(string id, bool corrupted = false) => new("guardian", Item: id, N: corrupted ? 1 : 0);
    /// <summary>One of the two great blades claimed, for the whole server.</summary>
    public static CityAction Blade(string blade, string by) => new("blade", Item: blade, Contract: by);
    /// <summary>Soren driven off (he got to his ship) or killed (he'll be back, and less).</summary>
    public static CityAction SiegeBroken(string by, bool killed) => new("siege_broken", Item: by, N: killed ? 1 : 0);
    /// <summary>Soren killed out in the wilds, hunting whoever took his blade.</summary>
    public static CityAction SorenKilled(string by) => new("soren_killed", Item: by);
    /// <summary>Soren has his blade back from whoever held it.</summary>
    public static CityAction WebLost(string holder) => new("web_lost", Item: holder);
    /// <summary>Lu Wukong killed (he'll be back, and less), and who did it.</summary>
    public static CityAction WukongKilled(string by) => new("wukong_killed", Item: by);
    // The towns' councils (see TownCouncil): a place's council met, a hero seated, a member dead, a raid sent, a purse emptied by whoever rules there.
    public static CityAction CouncilMet(string town, string name, IReadOnlyList<string> members) => new("council_met", Contract: name, Items: members, Town: town);
    public static CityAction CouncilSeat(string town, string hero) => new("council_seat", Item: hero, Town: town);
    public static CityAction CouncilFall(string town, string member) => new("council_fall", Item: member, Town: town);
    public static CityAction TownRaid(string from, string on, RaidAim aim, string? by) => new("town_raid", Item: from, Contract: by, N: (int)aim, Town: on);
    public static CityAction CouncilPurse(string town, string ruler) => new("council_purse", Item: ruler, Town: town);
    // Heroes' homes and shops (see PlayerStore): one set up or changed by its owner, shut, and a visitor trading at the counter.
    public static CityAction StoreSet(PlayerStore store) => new("store_set", Store: store);
    // What a guild can be hired to do to a town: mend an empty home, or a verse against its name.
    public static CityAction Mended(string town) => new("mended", Town: town);
    public static CityAction TownName(string town, double by) => new("town_name", N: (int)Math.Round(by * 10), Town: town);
    public static CityAction StoreShut(string owner) => new("store_shut", Contract: owner);
    public static CityAction StoreBuy(string owner, string item, int crowns) => new("store_buy", item, crowns, owner);
    public static CityAction StoreSell(string owner, string item, int crowns) => new("store_sell", item, crowns, owner);
    // The halls heroes found (see Hall): founded, a recruit taken on, the till emptied or paid into, a thing bought from its stores, lying low, a hero served, shut.
    public static CityAction HallFound(Hall hall) => new("hall_found", Contract: hall.Key, Hall: hall);
    public static CityAction HallRecruit(string key, int level) => new("hall_recruit", Contract: key, N: level);
    public static CityAction HallTill(string key, int crowns) => new("hall_till", Crowns: crowns, Contract: key);
    public static CityAction HallPay(string key, int crowns) => new("hall_pay", Crowns: crowns, Contract: key);
    public static CityAction HallTake(string key, string item, int crowns) => new("hall_take", item, crowns, key);
    public static CityAction HallLow(string key) => new("hall_low", Contract: key);
    public static CityAction HallServed(string key, string who) => new("hall_served", who, Contract: key);
    public static CityAction HallClose(string key) => new("hall_close", Contract: key);
    /// <summary>A druid's tree, set in the ground for everyone.</summary>
    public static CityAction Plant(string owner, double x, double z) => new("plant", Contract: owner, X: x, Z: z);
}

/// <summary>An event running now, and the days it has left.</summary>
public sealed class RunningEvent
{
    public string Id { get; set; } = "";
    public int DaysLeft { get; set; }
}

/// <summary>
/// Tristinwold's economy. Each good has a stock: the city's boats, farms, mine, woodlot and workshops add to it every
/// day, its people use it up, merchants bring more when it's short and ship it out when there's a glut, and the
/// smelters and the apothecary turn ore and coal into iron, and herbs into potions. A good's price follows its stock:
/// scarce is dear, plentiful is cheap. So does what a hero does: sell forty herring and herring gets cheap; take the
/// mine's ore and iron gets cheaper. Things happen (storms keep the boats in, a caravan comes, a festival drinks the
/// taverns dry), businesses post contracts for what's short, and the city saves its taxes and its stock for public
/// works that make a trade better for good. A hero who helps is known for it, and shopkeepers treat them better.
/// A day is ten minutes of play.
/// </summary>
public sealed partial class CityEconomy
{
    public const double DaySeconds = 10 * 60;

    public static readonly IReadOnlyList<GoodInfo> Goods =
    [
        new(Good.Fish, "fish", 120, 56, 56, "the fishing boats", "the Salt Barrel's kitchen", 5),
        new(Good.Meat, "meat", 80, 32, 32, "the farms and hunters", "the butchers of the Warrens", 5),
        new(Good.Bread, "bread", 150, 77, 77, "the farms and bakers", "the Gallows Perch", 3),
        new(Good.Ale, "ale", 100, 42, 42, "the breweries", "the Salt Barrel", 4),
        new(Good.Timber, "timber", 90, 28, 28, "the woodlot", "the shipwrights", 6),
        new(Good.Stone, "stone", 120, 28, 28, "the Greyhollow quarry", "the masons' guild", 2),
        new(Good.Ore, "ore", 80, 32, 0, "the Greyhollow mine", "the smelters", 5),
        new(Good.Coal, "coal", 80, 26, 10, "the Greyhollow mine", "the smelters", 2),
        new(Good.Iron, "iron", 60, 0, 16, "the smelters", "the Anvil Gate", 12),
        new(Good.Leather, "leather and hides", 50, 11, 11, "the tanners", "the saddlers", 9),
        new(Good.Herbs, "herbs", 60, 18, 2, "the herb-gatherers", "Saffron's Remedies", 5),
        new(Good.Potions, "potions", 40, 0, 9, "the apothecaries", "the Watch's surgeon", 28),
        new(Good.Precious, "silver, gold and gems", 30, 3, 3, "the deep veins and the divers", "the Crown's jewellers", 30),
    ];

    public static GoodInfo Info(Good g) => Goods[(int)g];

    public static readonly IReadOnlyList<CityEvent> Events =
    [
        new("storm", "A storm in the harbour", "A storm's keeping the fishing boats in. Fish is dear.", 2, Made: [(Good.Fish, 0.25)]),
        new("shoal", "The herring are running", "The herring are running: the boats come in low in the water. Fish is cheap.", 2, Made: [(Good.Fish, 2.2)]),
        new("flood", "Water in the mine", "Water's broken into the Greyhollow mine. Ore and coal are short, and iron will follow.", 3, Made: [(Good.Ore, 0.2), (Good.Coal, 0.3)]),
        new("caravan", "A caravan from the south", "A caravan's in from the south with iron, hides, herbs and silver.", 1,
            Arrives: [(Good.Iron, 0.5), (Good.Leather, 0.4), (Good.Herbs, 0.4), (Good.Precious, 0.3)]),
        new("festival", "The harvest festival", "It's the harvest festival: the taverns are drinking the city dry.", 2, Used: [(Good.Ale, 2.2), (Good.Meat, 1.8), (Good.Bread, 1.4)]),
        new("bandits", "Bandits on the south road", "Bandits on the south road: the farm carts aren't getting through.", 3,
            Made: [(Good.Meat, 0.5), (Good.Bread, 0.7), (Good.Leather, 0.5)]),
        new("fever", "Marsh fever in the Warrens", "Marsh fever in the Warrens. The apothecaries can't keep up.", 3, Used: [(Good.Potions, 2.5), (Good.Herbs, 1.5)]),
        new("fire", "Fire at the lumber yard", "Fire at the lumber yard! Half the timber's gone, and the rebuilding will need more.", 2,
            Used: [(Good.Timber, 1.6)], Arrives: [(Good.Timber, -0.5)]),
        new("shipyard", "A ship on the slips", "The shipwrights have a new hull on the slips: timber and iron are wanted.", 3, Used: [(Good.Timber, 2), (Good.Iron, 1.5)]),
        new("dragon", "A dragon over the pastures", "A dragon's been taking cattle from the pastures. Meat is dear.", 2, Made: [(Good.Meat, 0.4)]),
        new("good_harvest", "A good harvest", "A good harvest's in. Bread and ale are cheap.", 2, Made: [(Good.Bread, 1.6), (Good.Ale, 1.4)]),
    ];

    public static CityEvent Event(string id) => Events.First(e => e.Id == id);

    public static readonly IReadOnlyList<CityWork> Works =
    [
        new("harbour_wall", "Mend the harbour wall", "The harbour wall's mended: the boats go out in rougher weather, and the docks pay a little more.",
            [(Good.Stone, 60), (Good.Timber, 30)], 200, [(Good.Fish, 1.25)], DockWage: 1),
        new("shore_mine", "Shore up the Greyhollow mine", "The mine's new props are in: the miners dig deeper, and safer.",
            [(Good.Timber, 50), (Good.Iron, 15)], 250, [(Good.Ore, 1.3), (Good.Coal, 1.3)]),
        new("ovens", "New ovens for the bakers", "The bakers' new ovens are lit: more bread, and cheaper.",
            [(Good.Stone, 40), (Good.Iron, 20), (Good.Coal, 30)], 250, [(Good.Bread, 1.3)]),
        new("warrens_lamps", "Lamps and drains for the Warrens", "The Warrens have lamps and drains: fewer fevers, and the Lantern Boys like it less.",
            [(Good.Iron, 25), (Good.Stone, 40), (Good.Coal, 20)], 200, [(Good.Herbs, 1.2)]),
        new("exchange_wing", "A new wing for the Exchange", "The Exchange's new wing is open: more merchants, and more taxes for the city.",
            [(Good.Stone, 80), (Good.Timber, 40), (Good.Precious, 10)], 500, [(Good.Leather, 1.2), (Good.Precious, 1.3)], Tax: 0.3),
        new("shipyard", "A shipyard on the strand", "The shipyard's launched its first hull: ships bring timber and iron, and the docks are busier than ever.",
            [(Good.Timber, 120), (Good.Iron, 40)], 600, [(Good.Timber, 1.3), (Good.Iron, 1.2)], DockWage: 1),
    ];

    public static readonly int[] RenownSteps = [0, 25, 75, 150, 300];
    public static readonly string[] RenownTitles = ["a stranger to the Exchange", "a known trader", "a trusted supplier", "a pillar of Tristinwold", "the Exchange's favourite"];

    // ---- the state (saved with the company) --------------------------------------------------------------------

    public ulong Seed { get; set; }
    public int Day { get; set; } = 1;
    /// <summary>The Northwood, for everyone: the first time the turned guardian falls the dragon's lair opens; a true guardian killed after that breaks the barrier.</summary>
    public bool LairOpen { get; set; }
    public bool DragonFree { get; set; }
    public bool DragonSlain { get; set; }
    public HashSet<string> GuardiansSlain { get; set; } = [];

    /// <summary>The towns' buildings, by place.</summary>
    public Dictionary<string, TownFabric> Fabric { get; set; } = [];
    /// <summary>How far the market's fallen with people fleeing the dragon: a share off every price, wearing away.</summary>
    public double Slump { get; set; }
    /// <summary>Where the dragon went last.</summary>
    public string? DragonAt { get; set; }

    /// <summary>Soren's siege of a town, if there is one; and who holds the two great blades, for everyone.</summary>
    public PirateSiege? Siege { get; set; }
    public string? BladeHolder { get; set; }
    public string? WebHolder { get; set; }
    /// <summary>How many times Soren's been killed, by anyone. He came back from every one.</summary>
    public int SorenDeaths { get; set; }
    /// <summary>How many times Lu Wukong's been killed, by anyone. He came back from every one.</summary>
    public int WukongDeaths { get; set; }
    public const int SiegeEvery = 24, SiegeDays = 3, SiegeBurns = 1, SackBurns = 2, SackTreasury = 200;
    /// <summary>The towns his ships can reach.</summary>
    public static readonly IReadOnlyList<string> SiegeTowns = ["city:druitt", "city:greywater", "city:tristinwold"];

    /// <summary>Soren lands. Every day he's there, a town burns a little more, and its trade stops.</summary>
    string BeginSiege(Rng rng)
    {
        Siege = new PirateSiege { Town = rng.Pick(SiegeTowns), Ends = Day + SiegeDays, Lu = rng.Chance(Legend.LuComes) };
        FabricOf(Siege.Town).Burnt += SiegeBurns;
        Slump = Math.Min(MostSlump, Slump + SlumpBlow / 2);
        return $"The Dread Pirate Soren has landed at {PlaceName(Siege.Town)} and laid siege to it. He'll be {SiegeDays} days at it, if nobody stops him."
            + (Siege.Lu ? " But there's a patched sail in the bay and somebody singing: Lu Wukong has come to stand against him." : "");
    }

    /// <summary>A day of the siege. While Lu holds, nothing more burns; if he's still holding at the end, Soren sails with nothing, and it may have cost Lu his life.</summary>
    string? TendSiege(Rng rng)
    {
        if (Siege is not { } siege) return null;
        if (siege.Lu)
        {
            if (Day < siege.Ends) return $"Lu Wukong's crew are holding the streets of {PlaceName(siege.Town)} against Soren.";
            Siege = null;
            if (!rng.Chance(Legend.LuFalls)) return $"Lu Wukong held {PlaceName(siege.Town)} to the end, and Soren has sailed with nothing.";
            WukongDeaths++;
            return $"Soren has sailed from {PlaceName(siege.Town)} with nothing, but Lu Wukong fell holding it. Nobody who knows the sea believes that's the end of him.";
        }
        FabricOf(siege.Town).Burnt += SiegeBurns;
        foreach (var g in Goods) Stock[g.Good] = Stock.GetValueOrDefault(g.Good) * 0.95;
        if (Day < siege.Ends) return $"{PlaceName(siege.Town)} burns under Soren's guns.";
        FabricOf(siege.Town).Burnt += SackBurns;
        Treasury = Math.Max(0, Treasury - SackTreasury);
        Siege = null;
        return $"Soren has sacked {PlaceName(siege.Town)} and sailed. The carpenters have their work cut out.";
    }

    public const int RampageEvery = 3, CarpentersEvery = 5, BurnedAtOnce = 3, GrowthCap = 24, DerelictCap = 10, RebuildTimber = 12;
    /// <summary>How well a town has to be doing for carpenters to come, and how badly for people to start leaving.</summary>
    public const double Thrives = 0.3, Fails = -0.3;
    public const double SlumpBlow = 0.3, MostSlump = 0.6;
    /// <summary>How much each town lives by wood, which is what the dragon hunts. Hollowheart isn't here: it's never cut a tree.</summary>
    public static readonly IReadOnlyDictionary<string, int> WoodHunger = new Dictionary<string, int> { ["city:druitt"] = 12, ["city:tristinwold"] = 7, ["city:greywater"] = 5 };

    public TownFabric FabricOf(string town) => Fabric.TryGetValue(town, out var f) ? f : Fabric[town] = new TownFabric();

    /// <summary>The town the dragon's hungriest for: whoever's burning the most wood, less what it's burned there already.</summary>
    public string DragonTarget() => WoodHunger.OrderByDescending(t => t.Value + (t.Key == "city:tristinwold" ? Stock.GetValueOrDefault(Good.Timber) / 30 : 0) - FabricOf(t.Key).Burnt).First().Key;

    /// <summary>The dragon comes down on a town. What burns stays burned, and everyone who can run does.</summary>
    string Rampage()
    {
        var town = DragonAt = DragonTarget();
        FabricOf(town).Burnt += BurnedAtOnce;
        Slump = Math.Min(MostSlump, Slump + SlumpBlow);
        foreach (var g in Goods) Stock[g.Good] = Stock.GetValueOrDefault(g.Good) * 0.85;
        Stock[Good.Timber] = Stock.GetValueOrDefault(Good.Timber) * 0.5;
        Treasury = Math.Max(0, Treasury - 100);
        return $"The dragon has come down on {PlaceName(town)}: {BurnedAtOnce} more homes are ash, and the market's falling as people run for the coast.";
    }

    static string PlaceName(string town) => town.Replace("city:", "") is var n && n.Length > 0 ? char.ToUpperInvariant(n[0]) + n[1..] : town;

    /// <summary>
    /// How a place's trade stands, from -1 (dying) to 1 (booming). The capital goes by its stores, its treasury and the
    /// market's nerve; anywhere else by its stores, whether wagons still come, and what's on its road. Burned homes and
    /// a siege count against anywhere.
    /// </summary>
    public double Prosperity(string town)
    {
        var fabric = Fabric.GetValueOrDefault(town);
        var harm = (fabric?.Burnt ?? 0) * 0.06 + Slump / 2 + (Siege?.Town == town ? 0.4 : 0);
        if (town == Capital)
            return Math.Clamp(Goods.Average(g => Math.Clamp(Stock.GetValueOrDefault(g.Good) / g.Target, 0, 2) - 1) * 0.6 + Math.Clamp((Treasury - 400) / 1000.0, -0.4, 0.4) - harm, -1, 1);
        if (!Roads.Towns.TryGetValue(town, out var m)) return Math.Clamp(-harm, -1, 1);
        var stores = m.Stock.Count == 0 ? 0 : m.Stock.Average(s => Math.Clamp(s.Value / Math.Max(0.5, m.Usual(s.Key)), 0, 2) - 1);
        var since = Day - Math.Max(m.LastTrade, Roads.Road(town).LastTrade);
        var wagons = since <= 10 ? 0.4 : since <= 30 ? 0.4 * (30 - since) / 20 : 0;
        return Math.Clamp(stores * 0.8 + wagons - Roads.Hazard(town, Day) - harm, -1, 1);
    }

    /// <summary>The carpenters' week: the burned go back up first, if there's timber; then a town that's doing well mends what's been left to rot, and grows out past its walls.</summary>
    public string? Carpenters(Rng rng)
    {
        var burned = Fabric.Where(f => f.Value.Burnt > 0).Select(f => f.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (burned.Count > 0)
        {
            if (Stock.GetValueOrDefault(Good.Timber) < RebuildTimber) return null;
            var town = rng.Pick(burned);
            FabricOf(town).Burnt--;
            Stock[Good.Timber] -= RebuildTimber;
            return $"Carpenters have raised a home again in {PlaceName(town)}, on the ash of the last one.";
        }
        if (DragonFree || Stock.GetValueOrDefault(Good.Timber) < 3 * RebuildTimber) return null;
        var towns = Roads.Towns.Keys.Append(Capital).Where(k => Prosperity(k) >= Thrives && (FabricOf(k).Derelict > 0 || FabricOf(k).Grown < GrowthCap)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (towns.Count == 0) return null;
        var grown = rng.Pick(towns);
        if (FabricOf(grown).Derelict > 0)
        {
            FabricOf(grown).Derelict--;
            Stock[Good.Timber] -= RebuildTimber;
            return $"Trade's back in {TownName(grown)}: carpenters have put an empty home right, and someone's moved in.";
        }
        FabricOf(grown).Grown++;
        Stock[Good.Timber] -= 2 * RebuildTimber;
        return $"{TownName(grown)} is growing: carpenters have come, and there's a new house out past the wall.";
    }

    void Slain(string guardian, bool corrupted)
    {
        if (guardian == "dragon") { DragonSlain = true; DragonFree = false; return; }
        if (corrupted) LairOpen = true; // however often it's killed, this happens once
        else GuardiansSlain.Add(guardian);
        if (LairOpen && GuardiansSlain.Count > 0 && !DragonSlain) DragonFree = true;
    }
    public double NextDayAt { get; set; } = DaySeconds;
    public Dictionary<Good, double> Stock { get; set; } = [];
    /// <summary>Each good's price yesterday (as a share of its worth), for the market's arrows.</summary>
    public Dictionary<Good, double> Yesterday { get; set; } = [];
    public int Treasury { get; set; } = 400;
    public double Taxes { get; set; }
    public List<RunningEvent> Running { get; set; } = [];
    public int WorkIndex { get; set; }
    public Dictionary<Good, int> WorkGoods { get; set; } = [];
    public int WorkCrowns { get; set; }
    public List<string> WorksDone { get; set; } = [];
    public List<Contract> Contracts { get; set; } = [];
    /// <summary>The hero's own name at the Exchange. On a server, where the rest of the city is shared, each hero keeps their own.</summary>
    public int Renown { get; set; }
    public List<string> News { get; set; } = [];
    public int NextContract { get; set; }
    /// <summary>
    /// Things the city doesn't trade by the cartload (swords, cloaks, trophies), by how many more or fewer of each the
    /// shops hold than they'd like: sell them a hundred swords and swords are cheap for a couple of days; buy them out
    /// and they're dear. Mostly gone in a couple of days.
    /// </summary>
    public Dictionary<string, double> Flood { get; set; } = [];
    /// <summary>The roads out of the city: the places it trades with, its caravans, and what befalls them.</summary>
    public Roads Roads { get; set; } = new();

    public static CityEconomy Start(ulong seed)
    {
        var e = new CityEconomy { Seed = seed };
        foreach (var g in Goods) e.Stock[g.Good] = g.Target;
        foreach (var g in Goods) e.Yesterday[g.Good] = 1;
        e.News.Add("Trade's steady in Tristinwold. The boats are out and the forges are lit.");
        e.PostContracts(new Rng(seed ^ 0xC0A7));
        return e;
    }

    // ---- prices -----------------------------------------------------------------------------------------------

    /// <summary>A good's price now, as a share of its usual worth: scarce is dear (up to two and a half times), a glut cheap (down to under half).</summary>
    public double Factor(Good g)
    {
        var stock = Math.Max(1, Stock.GetValueOrDefault(g, Info(g).Target));
        return Math.Clamp(Math.Pow(Info(g).Target / stock, 0.7) * (1 - Slump), 0.45, 2.6);
    }

    /// <summary>How many of a thing the shops take before its price is well down (or give up before it's well up).</summary>
    public const double FloodScale = 25;

    /// <summary>The price of something not traded by the cartload, as a share of its worth: a hundred sold and it's under half; the shops bought out and it's dear.</summary>
    public double ItemFactor(string itemId)
    {
        var f = Flood.GetValueOrDefault(itemId);
        return f >= 0 ? Math.Max(0.45, Math.Pow(FloodScale / (FloodScale + f), 0.6)) : Math.Min(2.5, Math.Pow(FloodScale / Math.Max(FloodScale * 0.15, FloodScale + f), 0.6));
    }

    /// <summary>The good an item is, if it's one the city trades in by the cartload (gear, trophies and meals aren't).</summary>
    public static Good? GoodOf(string itemId) => itemId switch
    {
        "herring" or "mackerel" or "cod" or "sea_bass" or "eel" or "lobster" or "salmon" or "perch" or "trout" or "carp" or "pike" or "golden_carp" or "arctic_char" or "ice_pike" => Good.Fish,
        "venison" or "meat" or "rabbit" or "game_bird" or "raw_meat" => Good.Meat,
        "bread" or "travel_rations" or "potato" or "onion" or "cabbage" or "raspberries" or "bilberries" or "honeycomb" or "penny_bun" or "chanterelle" or "wild_garlic" => Good.Bread,
        "ale" => Good.Ale,
        "pine_log" or "oak_log" or "yew_log" or "plank" => Good.Timber,
        "stone_block" => Good.Stone,
        "iron_ore" or "silver_ore" or "gold_ore" or "scrap_iron" => Good.Ore,
        "coal" => Good.Coal,
        "iron_ingot" or "steel_ingot" or "iron_nails" => Good.Iron,
        "leather" or "wool" or "cow_hide" or "horse_hide" or "scruffy_pelt" or "wolf_pelt" or "frost_pelt" or "bear_pelt" or "boar_hide" or "deer_hide" or "fox_pelt" or "troll_hide" or "ape_hide" or "rabbit_pelt" or "marten_pelt" or "snow_hare_pelt" => Good.Leather,
        "silver_ingot" or "gold_ingot" or "rough_garnet" or "rough_sapphire" or "pearl" or "garnet" or "sapphire" => Good.Precious,
        _ => Items.Get(itemId)?.Kind switch { ItemKind.Herb => Good.Herbs, ItemKind.Potion => Good.Potions, _ => null },
    };

    /// <summary>What a shop asks for an item today (before the buyer's own markup).</summary>
    public int Price(Item item) => Math.Max(1, (int)Math.Round(item.Value * (GoodOf(item.Id) is { } g ? Factor(g) : ItemFactor(item.Id))));

    /// <summary>What a shop pays for an item today: half its price, less for a hero the Exchange doesn't know.</summary>
    public int Offer(Item item) => Math.Max(1, (int)Math.Round(Price(item) * (0.5 + 0.02 * RenownTier)));

    /// <summary>Whether the shops have any of a good left to sell (a storm can empty the fishmongers).</summary>
    public bool InStock(Item item) => GoodOf(item.Id) is not { } g || Stock.GetValueOrDefault(g) >= 1;

    /// <summary>The share off shop prices the city's regard is worth: none for a stranger, eight percent for its favourite.</summary>
    public double Discount => RenownTier * 0.02;

    public int RenownTier => Enumerable.Range(0, RenownSteps.Length).Last(i => Renown >= RenownSteps[i]);
    public string RenownTitle => RenownTitles[RenownTier];

    /// <summary>A hero sold the city something: its stock grows, and the city takes its tax.</summary>
    public void Sold(string itemId, int crowns)
    {
        if (GoodOf(itemId) is { } g) Stock[g] = Stock.GetValueOrDefault(g) + 1;
        else Flood[itemId] = Flood.GetValueOrDefault(itemId) + 1;
        Taxes += crowns * 0.05;
    }

    /// <summary>A hero bought something from a shop: its stock shrinks, and the city takes its tax.</summary>
    public void Bought(string itemId, int crowns)
    {
        if (GoodOf(itemId) is { } g) Stock[g] = Math.Max(0, Stock.GetValueOrDefault(g) - 1);
        else Flood[itemId] = Math.Max(-FloodScale * 0.85, Flood.GetValueOrDefault(itemId) - 1);
        Taxes += crowns * 0.05;
    }

    // ---- days -------------------------------------------------------------------------------------------------

    /// <summary>Runs the days that have passed by <paramref name="playSeconds"/>. Returns the news of each.</summary>
    public List<string> Advance(double playSeconds)
    {
        var told = new List<string>();
        for (var guard = 0; playSeconds >= NextDayAt && guard < 30; guard++)
        {
            NextDayAt += DaySeconds;
            told.AddRange(NextDay());
        }
        if (playSeconds >= NextDayAt) NextDayAt = playSeconds + DaySeconds; // a long gap: skip ahead
        return told;
    }

    double Made(Good g) => Info(g).Made * Product(Running.Select(r => Event(r.Id).Made), g) * Product(DoneWorks.Select(w => w.Better), g);
    double Used(Good g) => Info(g).Used * Product(Running.Select(r => Event(r.Id).Used), g);

    static double Product(IEnumerable<(Good Good, double By)[]?> lists, Good g) =>
        lists.SelectMany(l => l ?? []).Where(x => x.Good == g).Aggregate(1.0, (a, x) => a * x.By);

    IEnumerable<CityWork> DoneWorks => Works.Where(w => WorksDone.Contains(w.Id));
    public CityWork? Work => WorkIndex < Works.Count ? Works[WorkIndex] : null;

    /// <summary>A day in the city. Returns what happened worth telling.</summary>
    public List<string> NextDay()
    {
        var rng = new Rng(Seed ^ (ulong)Day * 0x9E3779B97F4A7C15UL);
        var told = new List<string>();
        foreach (var g in Goods) Yesterday[g.Good] = Factor(g.Good);
        Day++;

        // What the city makes and uses, a little more or less each day.
        foreach (var g in Goods)
        {
            var noise = 0.85 + rng.NextDouble() * 0.3;
            Stock[g.Good] = Math.Max(0, Stock.GetValueOrDefault(g.Good) + Made(g.Good) * noise - Used(g.Good));
        }
        // The smelters turn ore and coal into iron; the apothecaries, herbs into potions.
        var smelted = Math.Min(16, Math.Min(Stock[Good.Ore] / 2, Stock[Good.Coal]));
        (Stock[Good.Ore], Stock[Good.Coal], Stock[Good.Iron]) = (Stock[Good.Ore] - 2 * smelted, Stock[Good.Coal] - smelted, Stock[Good.Iron] + smelted);
        var brewed = Math.Min(9, Stock[Good.Herbs] / 2);
        (Stock[Good.Herbs], Stock[Good.Potions]) = (Stock[Good.Herbs] - 2 * brewed, Stock[Good.Potions] + brewed);
        // Merchants bring what's short and ship out a glut, a fair bit each day: a glut is a couple of days' bargain, not a season's.
        foreach (var g in Goods) Stock[g.Good] += (g.Target - Stock[g.Good]) * 0.3;
        // The shops sell down a pile of swords, or restock a run on them, by half each day.
        foreach (var (id, f) in Flood.ToList())
            if (Math.Abs(f) < 0.5) Flood.Remove(id); else Flood[id] = f * 0.35;

        // Events run out, and new ones begin.
        foreach (var r in Running) r.DaysLeft--;
        foreach (var r in Running.Where(r => r.DaysLeft <= 0).ToList())
        {
            Running.Remove(r);
            told.Add($"{Event(r.Id).Title}: that's over now.");
        }
        if (Running.Count < 2 && rng.Chance(0.35))
        {
            var e = rng.Pick(Events.Where(x => Running.All(r => r.Id != x.Id)).ToList());
            Running.Add(new RunningEvent { Id = e.Id, DaysLeft = e.Days });
            foreach (var (g, share) in e.Arrives ?? []) Stock[g] = Math.Max(0, Stock[g] + Info(g).Target * share);
            told.Add(e.News);
        }

        // Taxes and upkeep: the Watch, the lamps, the walls.
        var income = (int)Math.Round((45 + Taxes) * (1 + DoneWorks.Sum(w => w.Tax)));
        Taxes = 0;
        Treasury = Math.Max(0, Treasury + income - 30);

        // The city puts its spare stock and coin into the works.
        if (Work is { } work)
        {
            foreach (var (g, n) in work.Needs)
            {
                var spare = Math.Max(0, Stock[g] - Info(g).Target * 0.9);
                var give = (int)Math.Min(Math.Min(spare, Info(g).Target * 0.12), n - WorkGoods.GetValueOrDefault(g));
                if (give <= 0) continue;
                Stock[g] -= give;
                WorkGoods[g] = WorkGoods.GetValueOrDefault(g) + give;
            }
            var coin = Math.Min(Math.Max(0, (Treasury - 150) / 4), work.Crowns - WorkCrowns);
            Treasury -= coin;
            WorkCrowns += coin;
            if (Finish() is { } done) told.Add(done);
        }

        // Contracts run out, and the businesses that are short post new ones.
        Contracts.RemoveAll(c => c.Expires < Day || c.Left <= 0);
        PostContracts(rng);

        // The city's caravans, out along every road it knows.
        told.AddRange(Roads.TradeDay(this, Day, rng));

        News.AddRange(told);
        if (News.Count > 12) News.RemoveRange(0, News.Count - 12);
        Slump = Math.Max(0, Slump - 0.02);
        if (DragonFree && !DragonSlain && Day % RampageEvery == 0) told.Add(Rampage());
        if (TendSiege(rng) is { } siege) told.Add(siege);
        else if (Siege is null && Day % SiegeEvery == 0) told.Add(BeginSiege(rng));
        if (Day % CarpentersEvery == 0)
        {
            if (Carpenters(rng) is { } built) told.Add(built);
            var week = TownsTurn(new Rng(Seed ^ (ulong)Day * 7919)).ToList();
            News.AddRange(week);
            told.AddRange(week);
        }
        return told;
    }

    void PostContracts(Rng rng)
    {
        var short_ = Goods.Select(g => (g.Good, F: Factor(g.Good))).Where(x => Contracts.All(c => c.Good != x.Good)).OrderByDescending(x => x.F).ToList();
        if (short_.Count == 0) return;
        foreach (var (g, f) in short_.Where(x => x.F >= 1.25).Take(3).DefaultIfEmpty(short_.FirstOrDefault()))
        {
            if (Contracts.Count >= 4) break;
            var info = Info(g);
            var need = Math.Clamp(f, 1, 1.8); // a desperate city pays well, but not a fortune
            var wanted = Math.Max(4, (int)Math.Round(info.Target * (0.08 + 0.06 * rng.NextDouble()) * need / Math.Max(1, info.Worth / 6.0)));
            var per = Math.Max(1, (int)Math.Round(info.Worth * need * 1.25));
            Contracts.Add(new Contract
            {
                Id = $"c{++NextContract}", Good = g, Buyer = info.Buyer, Wanted = wanted, PricePer = per,
                Bonus = Math.Max(10, per * wanted / 5), Expires = Day + 3,
            });
        }
    }

    // ---- what a hero does for the city ------------------------------------------------------------------------

    /// <summary>
    /// Delivers some of a contract's good (the items' ids). Pays for each, and the bonus on finishing; the goods go
    /// into the city's stock. Returns the crowns earned.
    /// </summary>
    public int Deliver(Contract c, IEnumerable<string> itemIds)
    {
        var n = Math.Min(c.Left, itemIds.Count(id => GoodOf(id) == c.Good));
        if (n <= 0) return 0;
        c.Delivered += n;
        Stock[c.Good] = Stock.GetValueOrDefault(c.Good) + n;
        var paid = n * c.PricePer;
        Renown += n;
        if (c.Left == 0) { paid += c.Bonus; Renown += 5; Contracts.Remove(c); }
        Taxes += paid * 0.05;
        return paid;
    }

    /// <summary>Gives goods to the city's works (the items' ids): returns how many it took. The works may finish.</summary>
    public int Donate(IEnumerable<string> itemIds, out string? finished)
    {
        finished = null;
        if (Work is not { } work) return 0;
        var taken = 0;
        foreach (var id in itemIds)
            if (GoodOf(id) is { } g && work.Needs.FirstOrDefault(x => x.Good == g) is { N: > 0 } need && WorkGoods.GetValueOrDefault(g) < need.N)
            {
                WorkGoods[g] = WorkGoods.GetValueOrDefault(g) + 1;
                taken++;
            }
        Renown += taken;
        finished = Finish();
        return taken;
    }

    /// <summary>Gives crowns to the works. Returns how many it took.</summary>
    public int DonateCrowns(int crowns, out string? finished)
    {
        finished = null;
        if (Work is not { } work) return 0;
        var take = Math.Clamp(crowns, 0, work.Crowns - WorkCrowns);
        WorkCrowns += take;
        Renown += take / 10;
        finished = Finish();
        return take;
    }

    /// <summary>
    /// Does what a hero did (see <see cref="CityAction"/>). A server does this for every player's game, so nothing a
    /// game sends can do more than a hero could: a cartload of goods at most, a few thousand crowns.
    /// </summary>
    public bool Apply(CityAction a)
    {
        var items = (a.Items ?? []).Take(200).ToList();
        var crowns = Math.Clamp(a.Crowns, 0, 5000);
        switch (a.Kind)
        {
            case "sold" when a.Item is not null: Sold(a.Item, crowns); return true;
            case "bought" when a.Item is not null: Bought(a.Item, crowns); return true;
            case "deliver" when Contracts.FirstOrDefault(c => c.Id == a.Contract) is { } c: Deliver(c, items); return true;
            case "donate": Donate(items, out _); return true;
            case "donate_crowns": DonateCrowns(crowns, out _); return true;
            case "cargo" when a.Good is { } g && Enum.IsDefined(g): Stock[g] = Stock.GetValueOrDefault(g) + Math.Clamp(a.N, 0, 10); return true;
            case "treasury": Treasury += crowns; return true;
            case "guardian" when a.Item is { } guardian: Slain(guardian, a.N == 1); return true;
            case "blade" when a.Item == Legend.Rubilacxe && BladeHolder is null && a.Contract is { Length: > 0 and <= 60 } holder: BladeHolder = holder; News.Add($"{holder} has drawn the sword from the stone."); return true;
            case "blade" when a.Item == Legend.Arachnicalibur && WebHolder is null && a.Contract is { Length: > 0 and <= 60 } taker: WebHolder = taker; News.Add($"{taker} has taken Arachnicalibur from the Dread Pirate Soren."); return true;
            case "soren_killed" when a.Item is { Length: > 0 and <= 60 } slayer:
                SorenDeaths++;
                News.Add($"{slayer} has killed the Dread Pirate Soren. Nobody who knows the sea believes that's the end of it.");
                return true;
            case "wukong_killed" when a.Item is { Length: > 0 and <= 60 } killer:
                WukongDeaths++;
                if (Siege is { Lu: true } held) held.Lu = false; // there's nobody holding the town now
                News.Add($"{killer} has killed Lu Wukong. Nobody who knows the sea believes that's the end of him.");
                return true;
            case "web_lost" when a.Item is not null && WebHolder == a.Item:
                WebHolder = null;
                News.Add($"The Dread Pirate Soren has taken Arachnicalibur back from {a.Item}.");
                return true;
            case "siege_broken" when Siege is { } broken:
                News.Add($"{(a.Item is { Length: > 0 and <= 60 } who2 ? who2 : "Someone")} has broken Soren's siege of {PlaceName(broken.Town)}{(a.N == 1 ? ", and killed him. Nobody who knows the sea believes that's the end of it" : "")}.");
                if (a.N == 1) SorenDeaths++;
                Siege = null;
                return true;
            // The roads.
            case "know" when a.Market is { } m && m.Id.Length is > 0 and <= 80: return Roads.Know(m);
            case "town_stock" when a.Town is not null && a.Good is { } tg && Enum.IsDefined(tg): Roads.Adjust(a.Town, tg, Math.Clamp(a.N, -60, 60)); return true;
            case "city_stock" when a.Good is { } cg && Enum.IsDefined(cg): Stock[cg] = Math.Max(0, Stock.GetValueOrDefault(cg) + Math.Clamp(a.N, -60, 60)); return true;
            case "mended" when a.Town is not null && FabricOf(a.Town).Derelict > 0: FabricOf(a.Town).Derelict--; return true;
            case "town_name" when a.Town is not null: CouncilOf(a.Town).Standing = Math.Clamp(CouncilOf(a.Town).Standing + a.N / 10.0, -5, 5); return true;
            case "store_set" when a.Store is { Owner.Length: > 0 and <= 60 } shop: Stores[shop.Owner] = shop; return true;
            case { } hall when hall.StartsWith("hall_", StringComparison.Ordinal): return HallAct(a);
            case "plant" when a.Contract is not null: return Plant(a.Contract, a.X, a.Z);
            case "store_shut" when a.Contract is not null: return Stores.Remove(a.Contract);
            case "store_buy" when a.Contract is not null && a.Item is not null: return StoreBuy(a.Contract, a.Item, crowns);
            case "store_sell" when a.Contract is not null && a.Item is not null: return StoreSell(a.Contract, a.Item, crowns);
            case "council_met" when a.Town is not null: return Meet(a.Town, a.Contract, items);
            case "council_seat" when a.Town is not null && a.Item is { Length: > 0 and <= 60 } seated: return SeatHero(a.Town, seated);
            case "council_fall" when a.Town is not null && a.Item is { Length: > 0 } member: return MemberFalls(a.Town, member);
            case "council_purse" when a.Town is not null && CouncilOf(a.Town).Ruler == a.Item: CouncilOf(a.Town).Purse = 0; return true;
            case "town_raid" when a.Town is not null && a.Item is { Length: > 0 } raiders && Enum.IsDefined((RaidAim)a.N):
                if (Raid(raiders, a.Town, (RaidAim)a.N, new Rng(Seed ^ (ulong)Day ^ (ulong)Roads.Wagons * 7919), a.Contract is { Length: <= 60 } ? a.Contract : null) is not { } raided) return false;
                News.Add(raided);
                return true;
            case "plundered" when a.Town is not null: return Roads.Plundered(a.Town, Day) > 0;
            case "steady" when a.Town is not null: return Roads.Steady(a.Town, Day);
            case "escorted" when a.Town is not null:
                if (Roads.Escorted(this, a.Town, Day, new Rng(Seed ^ (ulong)Day ^ (ulong)Roads.Wagons * 7919)) is { } said) News.Add(said);
                return true;
            case "raiders" when a.Town is not null && a.Item is not null: Roads.RaidersAbout(a.Town, Day, a.Item.Length > 40 ? a.Item[..40] : a.Item); return true;
            case "cleared" when a.Contract is not null && Roads.Cleared(a.Contract) is { } t:
                News.Add($"{(a.Item is { Length: > 0 and <= 40 } who ? who + "'s company" : "Someone")} cleared {(t.Clan ? t.By : "the bandits")} off the road to {t.TownName}. The wagons roll again.");
                return true;
            default: return false;
        }
    }

    /// <summary>What the works still need (goods, then crowns).</summary>
    public IEnumerable<(Good Good, int N)> WorkNeeds => Work is { } w ? w.Needs.Select(x => (x.Good, x.N - WorkGoods.GetValueOrDefault(x.Good))).Where(x => x.Item2 > 0) : [];

    string? Finish()
    {
        if (Work is not { } work || WorkNeeds.Any() || WorkCrowns < work.Crowns) return null;
        WorksDone.Add(work.Id);
        WorkIndex++;
        WorkGoods.Clear();
        WorkCrowns = 0;
        var told = $"{work.Title}: done! {work.Done}";
        News.Add(told);
        return told;
    }

    /// <summary>What dock work pays a crate: more once the harbour wall and the shipyard are built, and when there's cargo to shift.</summary>
    public int DockWage => 3 + DoneWorks.Sum(w => w.DockWage) + (Running.Any(r => r.Id is "caravan" or "shipyard" or "shoal") ? 1 : 0);

    /// <summary>How the city is doing: by how many of its goods are short.</summary>
    public string Mood
    {
        get
        {
            var short_ = Goods.Count(g => Factor(g.Good) > 1.5);
            return short_ switch { 0 => "thriving", 1 => "getting by", <= 3 => "struggling", _ => "in hard times" };
        }
    }

    /// <summary>The market board: each good's price and which way it's going, what's happening, and the works.</summary>
    public string Report()
    {
        var lines = Goods.Select(g =>
        {
            var f = Factor(g.Good);
            var was = Yesterday.GetValueOrDefault(g.Good, 1);
            var arrow = f > was * 1.04 ? "up" : f < was * 0.96 ? "down" : "steady";
            var word = f >= 1.6 ? "very dear" : f >= 1.2 ? "dear" : f <= 0.65 ? "very cheap" : f <= 0.85 ? "cheap" : "fair";
            return $"{Cap(g.Name)}: {word}, {f:0.00}x ({arrow})";
        });
        var happening = Running.Count == 0 ? "Nothing out of the ordinary." : string.Join(" ", Running.Select(r => $"{Event(r.Id).Title} ({r.DaysLeft} day{(r.DaysLeft == 1 ? "" : "s")} more)."));
        return $"Day {Day}. The city is {Mood}; the treasury holds {Treasury} crowns.\n{string.Join("\n", lines)}\nNews: {happening}\n{Roads.Report(Day)}";
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
