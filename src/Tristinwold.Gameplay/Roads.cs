using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

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

    /// <summary>The Tristinwold day an alchemist last steadied this market (once a day).</summary>
    public int Steadied { get; set; } = -1;

    /// <summary>
    /// An alchemist's transmutation: with the reagents to pay for it, every good's stock is brought most of the way
    /// back to what the place usually keeps, so a glut or a crash eases and the prices with it. Once a day; whether
    /// it was done.
    /// </summary>
    public bool Steady(int day)
    {
        if (Steadied == day) return false;
        foreach (var g in Enum.GetValues<Good>())
        {
            var usual = Usual(g);
            var stock = Stock.GetValueOrDefault(g, usual);
            Stock[g] = stock + (usual - stock) * 0.7;
        }
        Steadied = day;
        return true;
    }

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

    /// <summary>Raided: the clan carries off nearly half of everything in its stores. Returns how many loads were taken.</summary>
    public int Plundered(int day)
    {
        Catch(day);
        var taken = 0.0;
        foreach (var g in Stock.Keys.ToList())
        {
            taken += Stock[g] * 0.45;
            Stock[g] *= 0.55;
        }
        Raided++;
        return (int)Math.Round(taken);
    }

    /// <summary>How many times raiders have plundered it.</summary>
    public int Raided { get; set; }

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

    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));

    /// <summary>"timber and bread".</summary>
    public string MakesText => And(Makes);
    public string WantsText => And(Wants);
    static string And(IEnumerable<Good> goods) => string.Join(" and ", goods.Select(g => CityEconomy.Info(g).Name));
}
/// <summary>
/// A caravan sacked on one of Tristinwold's roads, and the camp of whoever did it, off the road between the city
/// and the town: the caravan yard pays whoever clears it. Kept with the city, so on a server every company hears
/// the rumour and any of them can go.
/// </summary>
public sealed class RoadTrouble
{
    public string Id { get; set; } = "";
    /// <summary>The route: the town the caravan was running to or from.</summary>
    public string Town { get; set; } = "";
    public string TownName { get; set; } = "";
    /// <summary>Where they're camped.</summary>
    public double X { get; set; }
    public double Z { get; set; }
    /// <summary>"bandits", or a clan's name when a warband was about.</summary>
    public string By { get; set; } = "bandits";
    public int Bandits { get; set; } = 3;
    public int Bounty { get; set; }
    public int Day { get; set; }
    /// <summary>Caravans lost to them so far.</summary>
    public int Wagons { get; set; } = 1;
    public Good Good { get; set; }
    public int Loads { get; set; }

    public bool Clan => By != "bandits";
}

/// <summary>What's known of one road: the wagons on it today, the ones lost lately, and whether a clan's about.</summary>
public sealed class RoadState
{
    /// <summary>The days a caravan was lost on it, the last few.</summary>
    public List<int> Lost { get; set; } = [];
    /// <summary>The day a warband was last reported in the town's country, and which.</summary>
    public int RaidersDay { get; set; } = -99;
    public string? Clan { get; set; }
    /// <summary>Caravans on it today.</summary>
    public int Wagons { get; set; }
    public int LastTrade { get; set; }
}

/// <summary>
/// The roads out of Tristinwold, kept with the city so everyone on a server shares them: the market of every place
/// anyone has come to know, the city's own caravans, which run by the prices (whatever's cheap out there and dear
/// here, or the other way about), the traffic that draws bandits and clans, the caravans they sack, the rumours
/// that come back, and the camps the caravan yard pays to have cleared. The more wagons on a road, the more worth
/// robbing; every wagon lost makes the road known trouble, and the yard pays its guards accordingly.
/// </summary>
public sealed class Roads
{
    /// <summary>How far out the yard's wagons go.</summary>
    public const double Reach = 4000;
    /// <summary>How many days a camp waits to be cleared before the bandits move on, and how long a lost wagon is remembered.</summary>
    public const int TroubleDays = 6, Remember = 5;
    /// <summary>A price gap worth sending a wagon for.</summary>
    public const double WorthIt = 1.25;

    public Dictionary<string, TownMarket> Towns { get; set; } = [];
    public Dictionary<string, RoadState> State { get; set; } = [];
    public List<RoadTrouble> Troubles { get; set; } = [];
    public int NextTrouble { get; set; }
    /// <summary>Caravans out on all the roads today.</summary>
    public int Wagons { get; set; }
    /// <summary>Caravans lost, all told.</summary>
    public int Sacked { get; set; }

    /// <summary>The places near enough for a wagon, nearest first: Tristinwold's trade routes.</summary>
    public IEnumerable<TownMarket> Known => Towns.Values.Where(m => m.DistanceTo(0, 0) < Reach).OrderBy(m => m.DistanceTo(0, 0));

    public RoadState Road(string town)
    {
        if (!State.TryGetValue(town, out var r)) State[town] = r = new RoadState();
        return r;
    }

    /// <summary>A place come to know: its market is kept from now on. Whether it was new.</summary>
    public bool Know(TownMarket m)
    {
        if (m.Id.Length == 0 || Towns.ContainsKey(m.Id)) return false;
        Towns[m.Id] = m;
        return true;
    }

    /// <summary>Goods put into, or taken out of, a place's stores (a wagon unloaded, or loaded).</summary>
    public void Adjust(string town, Good g, int delta)
    {
        if (!Towns.TryGetValue(town, out var m)) return;
        m.Stock[g] = Math.Max(0, m.Stock.GetValueOrDefault(g, m.Usual(g)) + delta);
    }

    public int Plundered(string town, int day) => Towns.TryGetValue(town, out var m) ? m.Plundered(day) : 0;
    public bool Steady(string town, int day) => Towns.TryGetValue(town, out var m) && m.Steady(day);

    /// <summary>A warband's in a town's country: the road there is the more dangerous for a couple of days.</summary>
    public void RaidersAbout(string town, int day, string clan)
    {
        var r = Road(town);
        r.RaidersDay = day;
        r.Clan = clan;
    }

    bool Raiders(RoadState r, int day) => day - r.RaidersDay <= 2;
    int Open(string town) => Troubles.Count(t => t.Town == town);
    int LostLately(RoadState r, int day) => r.Lost.Count(d => day - d <= Remember);

    /// <summary>How much more the yard pays a guard on a road, for what's known of it: wagons lost lately, camps uncleared, a clan about.</summary>
    public double PayMult(string town, int day)
    {
        var r = Road(town);
        return Math.Min(2.5, 1 + 0.4 * Math.Min(3, LostLately(r, day)) + 0.25 * Open(town) + (Raiders(r, day) ? 0.3 : 0));
    }

    /// <summary>How much what's on a town's road is hurting its trade: wagons lost lately, camps uncleared, a clan about.</summary>
    public double Hazard(string town, int day)
    {
        var r = Road(town);
        return Math.Min(0.6, 0.1 * LostLately(r, day) + 0.2 * Open(town) + (Raiders(r, day) ? 0.2 : 0));
    }

    /// <summary>More bandits at each ambush on a road with camps on it.</summary>
    public int ExtraBandits(string town) => Math.Min(3, Open(town));

    /// <summary>What's known against a road, in words, or null if nothing is.</summary>
    public string? KnownDanger(string town, int day)
    {
        var r = Road(town);
        var parts = new List<string>();
        var lost = LostLately(r, day);
        if (lost > 0) parts.Add(lost == 1 ? "a wagon lost on it this week" : $"{lost} wagons lost on it this week");
        if (Open(town) is var open and > 0) parts.Add(open == 1 ? "a camp on it still uncleared" : $"{open} camps on it uncleared");
        if (Raiders(r, day)) parts.Add($"{r.Clan ?? "a clan"} about");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>The camp's cleared: the trouble's over, the road's that much safer. The trouble, or null if it was gone already.</summary>
    public RoadTrouble? Cleared(string id)
    {
        var t = Troubles.FirstOrDefault(t => t.Id == id);
        if (t is null) return null;
        Troubles.Remove(t);
        Road(t.Town).Lost.Clear();
        return t;
    }

    static int CityPays(CityEconomy city, Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g) * 0.85));
    static int CityAsks(CityEconomy city, Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g)));

    /// <summary>The goods worth a wagon between a place and the city today: what it sells cheaper than the city pays, and what it pays more than the city asks.</summary>
    IEnumerable<(Good Good, bool Bring, double Gap)> Gaps(TownMarket m, CityEconomy city) =>
        Enum.GetValues<Good>().SelectMany(g => new[]
        {
            (g, true, m.ForSale(g) > 0 ? (double)CityPays(city, g) / m.Asks(g) : 0),
            (g, false, city.Stock.GetValueOrDefault(g) > 2 ? (double)m.Pays(g) / CityAsks(city, g) : 0),
        }).Where(x => x.Item3 >= WorthIt).OrderByDescending(x => x.Item3);

    /// <summary>One caravan between Tristinwold and a place: a load of a good each way, whatever each side has to give. Returns what it did, or null if nothing moved.</summary>
    static string? Exchange(TownMarket m, CityEconomy city, int day, Rng rng, int loads, Good? bring = null, Good? take = null)
    {
        m.Catch(day);
        m.LastTrade = day;
        var size = Math.Max(1, (int)Math.Round(4 * m.Size / 0.15)) * loads;
        // What it brings: the good asked for, else whatever the place has to spare, what it makes first.
        var b = bring is { } want && m.ForSale(want) > 0 ? want
            : m.Makes.Concat(Enum.GetValues<Good>()).FirstOrDefault(g => m.ForSale(g) > 0, bring ?? (m.Makes.Count > 0 ? rng.Pick(m.Makes) : Good.Bread));
        var n = Math.Min(size, m.ForSale(b));
        if (n > 0)
        {
            m.Stock[b] -= n;
            city.Stock[b] = city.Stock.GetValueOrDefault(b) + n;
        }
        var t = take ?? (m.Wants.Count > 0 ? rng.Pick(m.Wants) : Good.Iron);
        var back = Math.Min(size, (int)city.Stock.GetValueOrDefault(t) - 1);
        if (back > 0)
        {
            city.Stock[t] -= back;
            m.Stock[t] = m.Stock.GetValueOrDefault(t) + back;
        }
        if (n <= 0 && back <= 0) return null;
        return n > 0 && back > 0 ? $"brings {CityEconomy.Info(b).Name} and takes {CityEconomy.Info(t).Name} home"
            : n > 0 ? $"brings {CityEconomy.Info(b).Name}" : $"takes {CityEconomy.Info(t).Name} out";
    }

    /// <summary>A guarded caravan got through: a big load each way. What the Exchange says, or null.</summary>
    public string? Escorted(CityEconomy city, string town, int day, Rng rng)
    {
        if (!Towns.TryGetValue(town, out var m)) return null;
        var best = Gaps(m, city).FirstOrDefault();
        return Exchange(m, city, day, rng, 3, best.Gap > 0 && best.Bring ? best.Good : null, best.Gap > 0 && !best.Bring ? best.Good : null) is { } said
            ? $"The caravan you guarded to {m.Name} {said}." : $"The caravan you guarded to {m.Name} got through, though neither side had much to trade today.";
    }

    /// <summary>
    /// A day on the roads: the city's caravans go wherever the prices say, one for each good worth the trip (three at
    /// most to a place) and one to each of the three places that have waited longest regardless; every wagon risks
    /// the road, more where the road is busy, where a clan's about, and where a camp's already uncleared; a caravan
    /// lost is a rumour at the Exchange and a camp for the yard to pay someone to clear. Returns the day's news.
    /// </summary>
    public List<string> TradeDay(CityEconomy city, int day, Rng rng)
    {
        var news = new List<string>();
        foreach (var m in Towns.Values) m.Catch(day);
        foreach (var t in Troubles.Where(t => day - t.Day > TroubleDays).ToList())
        {
            Troubles.Remove(t);
            news.Add($"{(t.Clan ? Cap(t.By) : "The bandits")} camped on the road to {t.TownName} have moved on. Nobody went for them.");
        }
        Wagons = 0;
        var routes = Known.ToList();
        var waited = routes.OrderBy(m => Road(m.Id).LastTrade).Take(3).Select(m => m.Id).ToHashSet();
        var told = 0;
        var more = 0;
        foreach (var m in routes)
        {
            var road = Road(m.Id);
            road.Lost.RemoveAll(d => day - d > Remember);
            var runs = Gaps(m, city).Take(3).Select(x => ((Good?)x.Good, x.Bring)).ToList();
            if (runs.Count == 0 && waited.Contains(m.Id)) runs.Add((null, true));
            road.Wagons = runs.Count;
            Wagons += runs.Count;
            if (runs.Count > 0) road.LastTrade = day;
            var sackedHere = false;
            foreach (var (good, bring) in runs)
            {
                var risk = Math.Clamp(0.03 + 0.03 * (runs.Count - 1) + 0.02 * m.DistanceTo(0, 0) / 1000 + (Raiders(road, day) ? 0.2 : 0) + 0.08 * Open(m.Id), 0.02, 0.6);
                if (!sackedHere && rng.Chance(risk))
                {
                    sackedHere = true;
                    news.Add(Sack(m, city, day, rng, good ?? rng.Pick(m.Makes), bring));
                    continue;
                }
                var said = Exchange(m, city, day, rng, 1, bring ? good : null, bring ? null : good);
                if (said is null) continue;
                if (told < 3) { told++; news.Add($"A caravan from {m.Name} {said}."); }
                else more++;
            }
        }
        if (more > 0) news.Add($"{more} more wagon{(more == 1 ? "" : "s")} on the roads today.");
        return news;
    }

    /// <summary>A caravan lost: the goods never arrive, the road's known trouble, and whoever did it is camped by it. The rumour.</summary>
    string Sack(TownMarket m, CityEconomy city, int day, Rng rng, Good good, bool bring)
    {
        var road = Road(m.Id);
        var n = Math.Max(1, (int)Math.Round(4 * m.Size / 0.15));
        if (bring) m.Stock[good] = Math.Max(0, m.Stock.GetValueOrDefault(good, m.Usual(good)) - n);
        else city.Stock[good] = Math.Max(0, city.Stock.GetValueOrDefault(good) - Math.Min(n, (int)city.Stock.GetValueOrDefault(good)));
        road.Lost.Add(day);
        Sacked++;
        var clan = Raiders(road, day) ? road.Clan : null;
        var t = Troubles.FirstOrDefault(t => t.Town == m.Id && (clan is null ? !t.Clan : t.By == clan));
        if (t is null)
        {
            // Camped off the road, a third to two thirds of the way out, a little way to one side.
            var f = 0.35 + rng.NextDouble() * 0.3;
            var side = (rng.Chance(0.5) ? 1 : -1) * (40 + rng.NextDouble() * 50);
            var len = Math.Max(1, m.DistanceTo(0, 0));
            var (px, pz) = (-m.Z / len, m.X / len);
            t = new RoadTrouble
            {
                Id = $"road{++NextTrouble}", Town = m.Id, TownName = m.Name, By = clan ?? "bandits", Day = day, Good = good, Loads = n,
                X = m.X * f + px * side, Z = m.Z * f + pz * side,
                Bandits = clan is null ? 3 + Math.Min(2, LostLately(road, day) - 1) : 4,
            };
            Troubles.Add(t);
        }
        else
        {
            t.Wagons++;
            t.Bandits = Math.Min(7, t.Bandits + 1);
            t.Day = day;
        }
        t.Loads += t.Wagons > 1 ? n : 0;
        t.Bounty = 40 + 15 * t.Bandits + (int)Math.Round(m.DistanceTo(0, 0) / 50) + 10 * (t.Wagons - 1);
        return $"A caravan {(bring ? "from" : "bound for")} {m.Name} was sacked on the road by {(clan is null ? "bandits" : clan)}: {n} loads of {CityEconomy.Info(good).Name} lost" +
               (t.Wagons > 1 ? $", the {Ordinal(t.Wagons)} wagon they've taken" : "") +
               $". They're camped off the road; Garrick Wain at the caravan yard pays {t.Bounty} crowns to whoever clears them.";
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    static string Ordinal(int n) => n switch { 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => $"{n}th" };

    /// <summary>The roads, for the market board.</summary>
    public string Report(int day)
    {
        if (Towns.Count == 0) return "Roads: no place known yet.";
        var trouble = Troubles.Count == 0 ? "" : " Trouble: " + string.Join("; ", Troubles.Select(t => $"{t.By} on the road to {t.TownName} ({t.Bounty} crowns to clear)")) + ".";
        return $"Roads: {Wagons} wagon{(Wagons == 1 ? "" : "s")} out to {Known.Count()} place{(Known.Count() == 1 ? "" : "s")} today; {Sacked} lost all told.{trouble}";
    }
}
