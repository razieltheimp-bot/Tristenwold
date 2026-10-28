using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// The little economy of one place out in the world: a hamlet, a farmstead, a mine, a town. It makes a few goods,
/// which are cheap there because there are plenty, and wants a few it doesn't make, which are dear because there are
/// few. Bring it what it wants and it pays well; every load you sell it fills it up, and the price falls. Left alone,
/// its stores drift back to what they usually are, a day at a time (Tristinwold's days). Once a place is known, its
/// market is remembered with the company, and Tristinwold's own caravans start trading with it.
/// </summary>
public sealed class TownMarket
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    /// <summary>How big a market it is, against Tristinwold's (a hamlet's is a sliver of it).</summary>
    public double Size { get; set; } = 0.15;
    public List<Good> Makes { get; set; } = [];
    public List<Good> Wants { get; set; } = [];
    public Dictionary<Good, double> Stock { get; set; } = [];
    /// <summary>The Tristinwold day it was last brought up to date.</summary>
    public int Day { get; set; }
    /// <summary>How many times the hero has brought a wagon here.</summary>
    public int Visits { get; set; }
    /// <summary>The Tristinwold day a caravan last ran between here and the city.</summary>
    public int LastTrade { get; set; }

    /// <summary>What it usually keeps of a good: plenty of what it makes, little of what it wants.</summary>
    public double Usual(Good g) => Baseline(g) * (Makes.Contains(g) ? 2.6 : Wants.Contains(g) ? 0.3 : 1);

    double Baseline(Good g) => Math.Max(2, CityEconomy.Info(g).Target * Size * 0.7);

    /// <summary>A good's price now, as a share of its usual worth anywhere: what it has plenty of is cheap, what it lacks dear.</summary>
    public double Factor(Good g) => Math.Clamp(Math.Pow(Baseline(g) / Math.Max(0.5, Stock.GetValueOrDefault(g, Usual(g))), 0.55), 0.4, 2.8);

    /// <summary>What it asks for one of a good.</summary>
    public int Asks(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * Factor(g)));

    /// <summary>What it pays for one.</summary>
    public int Pays(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * Factor(g) * 0.85));

    /// <summary>Sells it <paramref name="n"/> of a good, one at a time (so the price falls as it fills): the crowns it pays.</summary>
    public int Sell(Good g, int n)
    {
        var crowns = 0;
        for (var i = 0; i < n; i++)
        {
            crowns += Pays(g);
            Stock[g] = Stock.GetValueOrDefault(g, Usual(g)) + 1;
        }
        return crowns;
    }

    /// <summary>What <paramref name="n"/> of a good would cost, bought one at a time; it won't sell its last one.</summary>
    public int Cost(Good g, int n)
    {
        var (crowns, stock) = (0, Stock.GetValueOrDefault(g, Usual(g)));
        for (var i = 0; i < n; i++, stock--)
            crowns += Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * Math.Clamp(Math.Pow(Baseline(g) / Math.Max(0.5, stock), 0.55), 0.4, 2.8)));
        return crowns;
    }

    /// <summary>
    /// Drowning in a good: far more of it than it usually keeps (someone sold it a hundred fish), so cheap that it's worth
    /// carting somewhere else.
    /// </summary>
    public bool Glut(Good g) => Stock.GetValueOrDefault(g, Usual(g)) >= Usual(g) * 2 + 6;

    /// <summary>How much more of a good it has than it usually keeps.</summary>
    public int Surplus(Good g) => Math.Max(0, (int)Math.Floor(Stock.GetValueOrDefault(g, Usual(g)) - Usual(g)));

    /// <summary>How many of a good it has to sell.</summary>
    public int ForSale(Good g) => Math.Max(0, (int)Math.Floor(Stock.GetValueOrDefault(g, Usual(g))) - 1);

    /// <summary>Buys <paramref name="n"/> of a good from it (as many as it has to spare): the crowns it costs.</summary>
    public int Buy(Good g, int n)
    {
        n = Math.Min(n, ForSale(g));
        var crowns = Cost(g, n);
        Stock[g] = Stock.GetValueOrDefault(g, Usual(g)) - n;
        return crowns;
    }

    /// <summary>Days pass: its stores drift back toward what they usually are.</summary>
    public void Catch(int day)
    {
        if (day <= Day) return;
        var keep = Math.Pow(0.8, day - Day);
        foreach (var g in Stock.Keys.ToList()) Stock[g] = Usual(g) + (Stock[g] - Usual(g)) * keep;
        Day = day;
    }

    /// <summary>Whether a place keeps a market at all: anywhere people live and work, not a bandit camp, a barrow or a den.</summary>
    public static bool Trades(Settlement s) =>
        s.Kind is not (SettlementKind.BanditCamp or SettlementKind.Barrow or SettlementKind.Den) && s.Id != "city:tristinwold";

    /// <summary>A place's market as it is the first time anyone brings a wagon: what it makes by its country and its trade, and what it wants.</summary>
    public static TownMarket For(Settlement s, int day = 0)
    {
        var rng = new Rng((ulong)WorldGen.StableHash(s.Id) ^ 0x7EAD);
        Good[] country = s.Biome switch
        {
            Biome.AutumnForest => [Good.Timber, Good.Bread, Good.Herbs, Good.Meat],
            Biome.PineForest => [Good.Timber, Good.Leather, Good.Meat],
            Biome.Marsh => [Good.Herbs, Good.Fish, Good.Leather],
            Biome.Tundra => [Good.Leather, Good.Meat, Good.Fish],
            Biome.Snowfield => [Good.Leather, Good.Precious],
            _ => [Good.Stone, Good.Ore, Good.Coal],
        };
        Good[] trade = s.Kind switch
        {
            SettlementKind.Farmstead => [Good.Bread, Good.Meat],
            SettlementKind.Hamlet => [Good.Bread, Good.Ale],
            SettlementKind.Mine => [Good.Ore, Good.Coal, Good.Precious],
            SettlementKind.HuntingCamp => [Good.Leather, Good.Meat],
            SettlementKind.Inn => [Good.Ale, Good.Bread],
            SettlementKind.Shrine => [Good.Herbs, Good.Potions],
            SettlementKind.Town => [Good.Ale, Good.Iron],
            SettlementKind.City => [Good.Iron, Good.Potions],
            _ => [Good.Herbs],
        };
        var makes = trade.Take(1).Concat(country.Concat(trade.Skip(1)).OrderBy(_ => rng.Next(1000)).Take(s.IsCity ? 3 : 2)).Distinct().ToList();
        var wants = Enum.GetValues<Good>().Where(g => !makes.Contains(g)).OrderBy(_ => rng.Next(1000)).Take(s.IsCity ? 4 : 3).ToList();
        if (s.Kind == SettlementKind.AshbornCamp && !makes.Contains(Good.Bread)) wants[0] = Good.Bread; // refugees: bread above all
        var m = new TownMarket
        {
            Id = s.Id, Name = s.Name, X = s.X, Z = s.Z, Makes = makes, Wants = wants, Day = day,
            Size = s.Kind switch { SettlementKind.City => 0.6, SettlementKind.Town => 0.35, SettlementKind.Hamlet or SettlementKind.Inn or SettlementKind.Mine => 0.15, _ => 0.1 },
        };
        foreach (var g in Enum.GetValues<Good>()) m.Stock[g] = m.Usual(g);
        return m;
    }

    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));

    /// <summary>"timber and bread".</summary>
    public string MakesText => And(Makes);
    public string WantsText => And(Wants);
    static string And(IEnumerable<Good> goods) => string.Join(" and ", goods.Select(g => CityEconomy.Info(g).Name));
}

/// <summary>How far up the caravan yard a hero has come.</summary>
public enum CaravanRank { None, Loader, Driver, Entrepreneur }

/// <summary>A run a driver is making for the yard: out to a town with Tristinwold's goods, and back with the town's.</summary>
public sealed class CaravanRun
{
    public string To { get; set; } = "";
    public string ToName { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public Good Out { get; set; }
    public Good Back { get; set; }
    public int Load { get; set; } = 10;
    /// <summary>Delivered, and on the way home with the town's goods.</summary>
    public bool Returning { get; set; }
    public int Pay { get; set; }
    /// <summary>Moving a glut: picked up where it's piled up (the From place), dropped where it's wanted (To), and done.</summary>
    public bool Glut { get; set; }
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public double FromX { get; set; }
    public double FromZ { get; set; }
    /// <summary>A glut picked up, and on its way to where it's wanted.</summary>
    public bool Loaded { get; set; }
}

/// <summary>A glut somewhere along the routes (or in Tristinwold itself), and where it would fetch a price: work for the yard.</summary>
public sealed record GlutJob(string From, string FromName, double FromX, double FromZ, Good Good, int N, string To, string ToName, double ToX, double ToZ, int Pay)
{
    public string Key => $"{From}:{Good}";
}

/// <summary>
/// Trade beyond Tristinwold's walls, kept with the company: the market of every place the hero has brought a wagon to
/// or come to know, the hero's place at the caravan yard (a loader, a driver, then a trader with their own wagon and
/// horses), the run under way, and what's on their own wagon.
/// </summary>
public sealed class TradeBook
{
    public Dictionary<string, TownMarket> Towns { get; set; } = [];
    public CaravanRank Rank { get; set; } = CaravanRank.None;
    public int Loads { get; set; }
    public int Runs { get; set; }
    public bool OwnWagon { get; set; }
    public int Horses { get; set; }
    public bool WagonOut { get; set; }
    public CaravanRun? Run { get; set; }
    public Dictionary<Good, int> Cargo { get; set; } = [];
    /// <summary>What the hero's own trading has made, all told (crowns taken less crowns paid).</summary>
    public int Profit { get; set; }

    public const int LoadsToDrive = 10, RunsToOwn = 3, WagonPrice = 300, HorsePrice = 140, LoadWage = 4, PerHorse = 12, MaxHorses = 2;
    /// <summary>How far out the yard sends its wagons, and its own caravans trade.</summary>
    public const double Reach = 4000;

    public int Capacity => PerHorse * Math.Clamp(Horses, 0, MaxHorses);
    public int Loaded => Cargo.Values.Sum();
    public int Room => Math.Max(0, Capacity - Loaded);

    /// <summary>A place's market: the one already known, or a new one the first time.</summary>
    public TownMarket Market(Settlement s, int day)
    {
        if (!Towns.TryGetValue(s.Id, out var m)) Towns[s.Id] = m = TownMarket.For(s, day);
        m.Catch(day);
        return m;
    }

    /// <summary>Loaded a sack or crate onto the yard's wagon. Returns true when that makes them a driver.</summary>
    public bool Loaded1()
    {
        if (Rank == CaravanRank.None) Rank = CaravanRank.Loader;
        Loads++;
        if (Rank != CaravanRank.Loader || Loads < LoadsToDrive) return false;
        Rank = CaravanRank.Driver;
        return true;
    }

    /// <summary>
    /// Tristinwold's trade routes: the places the hero has found (and only those), near enough for a wagon. Every place
    /// found adds a route.
    /// </summary>
    public IEnumerable<TownMarket> Routes => Towns.Values.Where(m => m.DistanceTo(0, 0) < Reach).OrderBy(m => m.DistanceTo(0, 0));

    /// <summary>
    /// The routes Tristinwold has always run: its nearest market towns and villages, two of them, known to every hero
    /// from the start. Everything further out is for the hero to find.
    /// </summary>
    public static IEnumerable<Settlement> OldRoutes(WorldGen world) =>
        world.SettlementsNear(0, 0, 2500).Where(s => TownMarket.Trades(s) && s.Kind is SettlementKind.Town or SettlementKind.Hamlet or SettlementKind.Farmstead or SettlementKind.Inn)
            .OrderBy(s => s.DistanceTo(0, 0)).Take(2);

    /// <summary>Opens Tristinwold's old routes, if the book has none yet. Returns the places.</summary>
    public List<Settlement> OpenOldRoutes(WorldGen world, int day)
    {
        if (Towns.Count > 0) return [];
        var old = OldRoutes(world).ToList();
        foreach (var s in old) Market(s, day);
        return old;
    }

    /// <summary>A run for the yard along one of the routes (one of the nearest few). Null if no place is known yet.</summary>
    public CaravanRun? Plan(WorldGen world, int day, Rng rng)
    {
        var to = Routes.Take(3).OrderBy(_ => rng.Next(100)).Select(m => world.Find(m.Id)).OfType<Settlement>().FirstOrDefault();
        if (to is null) return null;
        var m = Market(to, day);
        var d = to.DistanceTo(0, 0);
        return new CaravanRun
        {
            To = to.Id, ToName = to.Name, X = to.X, Z = to.Z, Out = rng.Pick(m.Wants), Back = rng.Pick(m.Makes),
            Pay = 20 + (int)Math.Round(d / 40),
        };
    }

    /// <summary>The driver reached the town: the goods are delivered to its stores and the town's are loaded. Half the pay now.</summary>
    public int Arrive(int day)
    {
        if (Run is not { Returning: false } r || !Towns.TryGetValue(r.To, out var m)) return 0;
        m.Catch(day);
        m.Stock[r.Out] = m.Stock.GetValueOrDefault(r.Out) + r.Load;
        m.Stock[r.Back] = Math.Max(1, m.Stock.GetValueOrDefault(r.Back) - r.Load);
        m.Visits++;
        r.Returning = true;
        return r.Pay / 2;
    }

    /// <summary>Home with the town's goods: the run is done; the rest of the pay. Returns true when that makes them ready to trade on their own.</summary>
    public bool Home()
    {
        if (Run is not { Returning: true }) return false;
        Run = null;
        Runs++;
        return Runs == RunsToOwn;
    }

    /// <summary>Whether they may set up on their own: three runs for the yard behind them.</summary>
    public bool MayOwn => Runs >= RunsToOwn;

    /// <summary>Bought their own wagon: they're a trader now.</summary>
    public bool BuyWagon()
    {
        if (!MayOwn || OwnWagon) return false;
        OwnWagon = true;
        Rank = CaravanRank.Entrepreneur;
        return true;
    }

    /// <summary>
    /// A day of Tristinwold's own trade along its routes: a small caravan runs to each of a few known places (the ones
    /// that have waited longest), bringing some of what the place makes, and taking home some of what it wants.
    /// Returns what's said of it at the Exchange.
    /// </summary>
    public List<string> TradeDay(CityEconomy city, int day, Rng rng)
    {
        var news = new List<string>();
        foreach (var m in Routes.OrderBy(m => m.LastTrade).Take(3).ToList())
            if (Exchange(m, city, day, rng, 1) is { } said) news.Add($"A caravan from {m.Name} {said}.");
        return news;
    }

    /// <summary>One caravan between Tristinwold and a place: goods each way, <paramref name="loads"/> times the usual. Returns what it did.</summary>
    static string? Exchange(TownMarket m, CityEconomy city, int day, Rng rng, int loads)
    {
        m.Catch(day);
        m.LastTrade = day;
        var (brings, takes) = (rng.Pick(m.Makes), rng.Pick(m.Wants));
        var n = Math.Min(Math.Max(1, (int)Math.Round(4 * m.Size / 0.15)) * loads, m.ForSale(brings));
        if (n <= 0) return null;
        m.Stock[brings] -= n;
        city.Stock[brings] = city.Stock.GetValueOrDefault(brings) + n;
        var back = Math.Min(n, (int)city.Stock.GetValueOrDefault(takes) - 1);
        if (back > 0)
        {
            city.Stock[takes] -= back;
            m.Stock[takes] = m.Stock.GetValueOrDefault(takes) + back;
        }
        return $"brings {CityEconomy.Info(brings).Name}{(back > 0 ? $" and takes {CityEconomy.Info(takes).Name} home" : "")}";
    }

    // ---- guarding the city's caravans --------------------------------------------------------------------------

    /// <summary>How seasoned an adventurer must be before the yard will pay them to guard a caravan.</summary>
    public const int GuardLevel = 3;

    /// <summary>The caravan the hero is guarding now, if any.</summary>
    public Escort? Guarding { get; set; }
    /// <summary>How many caravans they've brought through, and lost.</summary>
    public int Guarded { get; set; }
    public int Lost { get; set; }

    /// <summary>Whether the hero has guarded a caravan before (the first is a starter's job, open to anyone).</summary>
    public bool Seasoned => Guarded + Lost > 0;

    /// <summary>
    /// A new hero's first guard job, open whatever their level: the city's caravan along the nearest route, one ambush
    /// of two bandits on the way.
    /// </summary>
    public Escort? FirstRoad() => Routes.Select(m => new Escort
    {
        To = m.Id, ToName = m.Name, X = m.X, Z = m.Z, Bandits = 2, Ambushes = [0.5],
        Pay = 30 + (int)Math.Round(m.DistanceTo(0, 0) / 30),
    }).FirstOrDefault();

    /// <summary>
    /// The guard work the yard has going: a caravan along each route, the further and the more seasoned the guard, the
    /// more bandits on the road and the better it pays.
    /// </summary>
    public IEnumerable<Escort> Escorts(int heroLevel) => Routes.Take(6).Select(m =>
    {
        var d = m.DistanceTo(0, 0);
        var ambushes = d < 900 ? 1 : d < 2200 ? 2 : 3;
        return new Escort
        {
            To = m.Id, ToName = m.Name, X = m.X, Z = m.Z,
            Bandits = Math.Min(6, 2 + heroLevel / 4 + (int)(d / 1500)),
            Ambushes = [.. Enumerable.Range(1, ambushes).Select(i => (double)i / (ambushes + 1))],
            Pay = (int)Math.Round((25 + d / 25) * (1 + 0.12 * heroLevel)),
        };
    });

    /// <summary>The caravan got through: paid, and a big load each way between the place and Tristinwold. Returns what the Exchange says.</summary>
    public string? Delivered(CityEconomy city, int day, Rng rng)
    {
        if (Guarding is not { } e || !Towns.TryGetValue(e.To, out var m)) return null;
        Guarding = null;
        Guarded++;
        return Exchange(m, city, day, rng, 3) is { } said ? $"The caravan you guarded to {m.Name} {said}." : null;
    }

    /// <summary>The bandits took the wagon.</summary>
    public void Overrun()
    {
        if (Guarding is null) return;
        Guarding = null;
        Lost++;
    }

    // ---- gluts --------------------------------------------------------------------------------------------------

    public const string CityId = "city:tristinwold";

    /// <summary>Tristinwold itself drowning in a good: far more than its usual stores.</summary>
    public static bool Glut(CityEconomy city, Good g) => city.Stock.GetValueOrDefault(g) >= CityEconomy.Info(g).Target * 1.8;

    static int CityPays(CityEconomy city, Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g) * 0.85));
    static int CityAsks(CityEconomy city, Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g)));

    /// <summary>
    /// The gluts along the routes, each a job for the yard: take the surplus from where it's piled up (and cheap) to the
    /// known place that'll pay most for it. The bigger the gap between the two prices and the longer the road, the
    /// better it pays.
    /// </summary>
    public IEnumerable<GlutJob> Gluts(CityEconomy city)
    {
        var places = Routes.ToList();
        var sources = places.SelectMany(m => Enum.GetValues<Good>().Where(m.Glut).Select(g => (Id: m.Id, Name: m.Name, m.X, m.Z, Good: g, Surplus: m.Surplus(g), Price: m.Asks(g))))
            .Concat(Enum.GetValues<Good>().Where(g => Glut(city, g))
                .Select(g => (Id: CityId, Name: "Tristinwold", X: 0.0, Z: 0.0, Good: g, Surplus: (int)(city.Stock[g] - CityEconomy.Info(g).Target), Price: CityAsks(city, g))));
        foreach (var (id, name, x, z, g, surplus, price) in sources)
        {
            var best = places.Where(m => m.Id != id).Select(m => (m.Id, m.Name, m.X, m.Z, Pays: m.Pays(g)))
                .Concat(id == CityId ? [] : [(Id: CityId, Name: "Tristinwold", X: 0.0, Z: 0.0, Pays: CityPays(city, g))])
                .MaxBy(t => t.Pays);
            if (best.Id is null || best.Pays < price * 1.3) continue;
            var n = Math.Clamp(surplus, 1, 12);
            var road = Math.Sqrt((best.X - x) * (best.X - x) + (best.Z - z) * (best.Z - z));
            yield return new GlutJob(id, name, x, z, g, n, best.Id, best.Name, best.X, best.Z,
                15 + n * (best.Pays - price) / 2 + (int)Math.Round(road / 40));
        }
    }

    /// <summary>Takes on a glut job: a run from where it's piled up to where it's wanted.</summary>
    public CaravanRun Take(GlutJob j) => Run = new CaravanRun
    {
        Glut = true, From = j.From, FromName = j.FromName, FromX = j.FromX, FromZ = j.FromZ,
        To = j.To, ToName = j.ToName, X = j.ToX, Z = j.ToZ, Out = j.Good, Back = j.Good, Load = j.N, Pay = j.Pay,
    };

    /// <summary>The wagon's at the glut: loaded, out of that place's stores.</summary>
    public void LoadGlut(CityEconomy city, int day)
    {
        if (Run is not { Glut: true, Loaded: false } r) return;
        if (r.From == CityId) city.Stock[r.Out] = Math.Max(1, city.Stock.GetValueOrDefault(r.Out) - r.Load);
        else if (Towns.TryGetValue(r.From, out var m))
        {
            m.Catch(day);
            m.Stock[r.Out] = Math.Max(1, m.Stock.GetValueOrDefault(r.Out) - r.Load);
        }
        r.Loaded = true;
    }

    /// <summary>Delivered where it's wanted: into that place's stores, and the run is done. Returns the pay.</summary>
    public int DeliverGlut(CityEconomy city, int day)
    {
        if (Run is not { Glut: true, Loaded: true } r) return 0;
        if (r.To == CityId) city.Stock[r.Out] = city.Stock.GetValueOrDefault(r.Out) + r.Load;
        else if (Towns.TryGetValue(r.To, out var m))
        {
            m.Catch(day);
            m.Stock[r.Out] = m.Stock.GetValueOrDefault(r.Out) + r.Load;
        }
        Run = null;
        GlutsMoved++;
        return r.Pay;
    }

    /// <summary>How many gluts the hero has carted away.</summary>
    public int GlutsMoved { get; set; }
}

/// <summary>A caravan of Tristinwold's on the road to a known place, with the hero hired to guard it.</summary>
public sealed class Escort
{
    public string To { get; set; } = "";
    public string ToName { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public int Pay { get; set; }
    /// <summary>How many bandits come at each ambush.</summary>
    public int Bandits { get; set; } = 2;
    /// <summary>How far along the road (0 to 1) the ambushes wait; and how many have been sprung.</summary>
    public List<double> Ambushes { get; set; } = [];
    public int Sprung { get; set; }
    /// <summary>What's left of the wagon and its load (it's lost at 0).</summary>
    public double Wagon { get; set; } = 100;

    /// <summary>The next ambush, if the wagon has come far enough along the road for it.</summary>
    public bool AmbushDue(double along) => Sprung < Ambushes.Count && along >= Ambushes[Sprung];

    /// <summary>"dangerous", by how many bandits wait, and how often.</summary>
    public string Danger => (Bandits * Ambushes.Count) switch { <= 2 => "a quiet road", <= 5 => "bandits about", <= 10 => "dangerous", _ => "deadly" };
}
