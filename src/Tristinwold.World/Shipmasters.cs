using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>
/// A market a ship's master can trade with: only ever the one where she's tied up. A port over the sea, or the
/// Exchange in Tristinwold itself.
/// </summary>
public interface IDock
{
    string Id { get; }
    string Name { get; }
    /// <summary>What it pays for a load of a good, and what it asks for one, as things stand.</summary>
    int Pays(Good g);
    int Asks(Good g);
    /// <summary>How many loads of a good it has to spare.</summary>
    int ForSale(Good g);
    /// <summary>Sells it that many loads: the crowns it pays.</summary>
    int Sell(Good g, int n);
    /// <summary>Buys that many loads from it: the crowns they cost.</summary>
    int Buy(Good g, int n);
}

/// <summary>A port's market, as the ship's master finds it on the quay.</summary>
public sealed class PortDock(TownMarket m) : IDock
{
    public string Id => m.Id;
    public string Name => m.Name;
    public int Pays(Good g) => m.Pays(g);
    public int Asks(Good g) => m.Asks(g);
    public int ForSale(Good g) => m.ForSale(g);
    public int Sell(Good g, int n) => m.Sell(g, n);
    public int Buy(Good g, int n) => m.Buy(g, n);
}

/// <summary>Tristinwold's Exchange, which takes what a ship brings home and sells her what the city has plenty of.</summary>
public sealed class HomeDock(CityEconomy city) : IDock
{
    public string Id => Seas.Tristinwold;
    public string Name => "Tristinwold";
    public int Asks(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g)));
    public int Pays(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * city.Factor(g) * 0.85));
    /// <summary>The Exchange keeps a few of everything back for the city's own.</summary>
    public int ForSale(Good g) => Math.Max(0, (int)city.Stock.GetValueOrDefault(g) - 6);

    public int Sell(Good g, int n)
    {
        var crowns = 0;
        for (var i = 0; i < n; i++)
        {
            crowns += Pays(g);
            city.Stock[g] = city.Stock.GetValueOrDefault(g) + 1;
        }
        return crowns;
    }

    public int Buy(Good g, int n)
    {
        var crowns = 0;
        for (var i = 0; i < Math.Min(n, ForSale(g)); i++)
        {
            crowns += Asks(g);
            city.Stock[g] -= 1;
        }
        return crowns;
    }
}

/// <summary>What came of a fleet ship's orders: word for the hero, and any crowns her master sends home with it.</summary>
public sealed record ShipNews(FleetShip Ship, string Text, int Crowns = 0);

public static partial class Seas
{
    /// <summary>Tristinwold harbour, as a stop on a ship's orders.</summary>
    public const string Tristinwold = "port:tristinwold";
    /// <summary>An order to sail on past the last chart and find whatever's there.</summary>
    public const string Beyond = "chart";

    /// <summary>
    /// Which way each port lies from Tristinwold, in degrees round from north: so the days between two of them are
    /// a reckoning across the water, not out and back by way of home.
    /// </summary>
    static readonly Dictionary<string, double> Bearings = new()
    {
        ["port:druitt"] = 250, ["port:greywater"] = 100, ["port:saltmere"] = 20, ["port:brinehollow"] = 200,
        ["port:vosk"] = 135, ["port:corrow"] = 290, ["port:ambermouth"] = 180, ["port:skerrylund"] = 340,
        ["port:halcyon"] = 160, ["port:cinderisles"] = 240, ["port:pearlatoll"] = 210, ["port:verdane"] = 60, ["port:ostmark"] = 30,
    };

    static double Leagues(string id) => id == Tristinwold ? 0 : Harbours.TryGetValue(id, out var h) ? h.Leagues : Get(id)?.Leagues ?? 1;
    static double Bearing(string id) => Harbours.TryGetValue(id, out var h) ? h.Bearing : Bearings.GetValueOrDefault(id, WorldGen.StableHash(id) % 360);

    /// <summary>
    /// A hero's own places on the coast that their ships can put in at: a colony by the sea, or a hometown that's on the
    /// water. The game tells this what there is (they're the hero's, not the world's charts); each is a port with a small
    /// market of what its people make, as far out as it really lies.
    /// </summary>
    public static readonly Dictionary<string, (SeaPort Port, double Leagues, double Bearing)> Harbours = [];

    /// <summary>A metre's worth of a league on the sea charts (a day's sailing).</summary>
    public const double LeagueMetres = 900;

    /// <summary>Makes a place on the coast a harbour for the hero's ships (or updates it): its id, name, where it is, and what it makes and wants.</summary>
    public static SeaPort Harbour(string id, string name, double x, double z, Good[] makes, Good[] wants)
    {
        var leagues = Math.Max(0.5, Math.Sqrt(x * x + z * z) / LeagueMetres);
        var bearing = (Math.Atan2(x, -z) * 180 / Math.PI + 360) % 360;
        var port = new SeaPort(id, name, Math.Max(1, (int)Math.Round(leagues)), 0.15, makes, wants, $"{name}, your own harbour");
        Harbours[id] = (port, leagues, bearing);
        return port;
    }

    /// <summary>Days of sailing between two ports (Tristinwold among them): at least one.</summary>
    public static int Between(string a, string b)
    {
        if (a == b) return 0;
        var (la, lb) = (Leagues(a), Leagues(b));
        var turn = (Bearing(a) - Bearing(b)) * Math.PI / 180;
        return Math.Max(1, (int)Math.Round(Math.Sqrt(Math.Max(0, la * la + lb * lb - 2 * la * lb * Math.Cos(turn)))));
    }

    /// <summary>How thick the pirates are on the water between two ports: the worse end's, a little less out in the middle of it.</summary>
    public static double PiratesBetween(string a, string b) =>
        Math.Max(a == Tristinwold ? 0 : Get(a)?.Pirates ?? 0.3, b == Tristinwold ? 0 : Get(b)?.Pirates ?? 0.3) * 0.8;

    public static string NameOf(string id) => id == Tristinwold ? "Tristinwold" : id == Beyond ? "past the charts" : Get(id)?.Name ?? id;
}

/// <summary>
/// A ship of the fleet with one of the hero's apprentices or squires in command of her: she sails on their orders,
/// sent by word wherever the hero is. Sail to a port, sail on past the charts to find new land, or work a trade route
/// round a string of ports, buying and selling as she goes. Her master only ever trades with the market where she's
/// tied up, out of her own purse: what the hero staked her with, and what she's made since. Whatever she makes over
/// the stake comes home to the hero each time she ties up in Tristinwold.
/// </summary>
public sealed partial class FleetShip
{
    /// <summary>The apprentice or squire in command of her (their bond's id), and their name, for her log.</summary>
    public string? Master { get; set; }
    public string MasterName { get; set; } = "";
    /// <summary>The port she's tied up at over the sea; null when she's home in Tristinwold or at sea.</summary>
    public string? At { get; set; }
    /// <summary>Where she's sailing to, where from, and when she set off and will make it.</summary>
    public string? Bound { get; set; }
    public string? From { get; set; }
    public double Departed { get; set; }
    public double Arrives { get; set; }
    /// <summary>Bound past the charts, for whatever's there.</summary>
    public bool Charting { get; set; }
    /// <summary>Her orders: the ports to make, in order (Tristinwold among them, or <see cref="Seas.Beyond"/>); round again and again if it's a route.</summary>
    public List<string> Plan { get; set; } = [];
    public bool Loop { get; set; }
    public int Stop { get; set; }
    /// <summary>When she'll cast off again, once she's done her trading in port.</summary>
    public double NextSail { get; set; }
    /// <summary>What's in her hold, and what was paid for it, good by good.</summary>
    public Dictionary<Good, int> Cargo { get; set; } = [];
    public Dictionary<Good, int> Paid { get; set; } = [];
    /// <summary>Her purse, what the hero put into it, and what she's made on her trading all told.</summary>
    public int Purse { get; set; }
    public int Stake { get; set; }
    public int Made { get; set; }
    /// <summary>Her log: the latest few things that happened, newest last.</summary>
    public List<string> Log { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore] public bool Sailing => Bound is not null;
    [System.Text.Json.Serialization.JsonIgnore] public string Where => At ?? Seas.Tristinwold;
    [System.Text.Json.Serialization.JsonIgnore] public bool Mastered => Master is not null;
    [System.Text.Json.Serialization.JsonIgnore] public int HoldSize => SeaBook.Hulls[Size].Hold + (Upgrades.Contains("deep_hold") ? 20 : 0);
    [System.Text.Json.Serialization.JsonIgnore] public int Loaded => Cargo.Values.Sum();
    [System.Text.Json.Serialization.JsonIgnore] public double Speed => SeaBook.Hulls[Size].Pace * (Upgrades.Contains("fine_canvas") ? 1.1 : 1);
    [System.Text.Json.Serialization.JsonIgnore] public bool OnRoute => Loop && Plan.Count >= 2;

    /// <summary>Where she is, said plainly.</summary>
    public string Whereabouts(double now) => Bound is { } b
        ? $"under sail for {Seas.NameOf(Charting ? Seas.Beyond : b)}, {Due(now)} out"
        : At is { } a ? $"tied up at {Seas.NameOf(a)}" : "in Tristinwold harbour";

    public string Due(double now)
    {
        var left = Math.Max(0, Arrives - now);
        return left < 90 ? "a minute or so" : $"{Math.Ceiling(left / 60)} minutes";
    }

    public void Note(string line)
    {
        Log.Add(line);
        if (Log.Count > 12) Log.RemoveRange(0, Log.Count - 12);
    }
}

public sealed partial class SeaBook
{
    /// <summary>How long a ship lies in port doing her trading before she sails on (play seconds).</summary>
    public const double LayOver = 60;
    /// <summary>The most stops on a route, and the most loads of one good a master buys for the next port at a time (any more and they'd glut it).</summary>
    public const int MostStops = 6, MostOfOne = 15;

    /// <summary>Every port the hero's ships know the way to: the two at the start, and everything charted since.</summary>
    public IEnumerable<SeaPort> Known =>
        Seas.Ports.Where(p => Seas.Home.Contains(p.Id) || Charted.Contains(p.Id)).Concat(Seas.Uncharted.Where(p => Charted.Contains(p.Id))).Concat(Seas.Harbours.Values.Select(h => h.Port));

    /// <summary>
    /// Where a ship's master hands over what she's made beyond her stake: Tristinwold, and the hero's own places on the
    /// water (their hometown, their colonies by the sea), as the game says.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore] public HashSet<string> PaysAt { get; set; } = [];

    public bool Pays(string port) => port == Seas.Tristinwold || PaysAt.Contains(port);

    /// <summary>The nearest land nobody's charted yet, if there's any left.</summary>
    public SeaPort? Unfound => Seas.ToChart.FirstOrDefault(p => !Charted.Contains(p.Id));

    /// <summary>One of the company goes aboard to take command of her: only while she's home and nobody else has.</summary>
    public static bool TakeCommand(FleetShip f, Bond b)
    {
        if (f.Away || f.Mastered || !Keeping.CanKeep(b) || b.Sails is not null || b.Keeps is not null) return false;
        (f.Master, f.MasterName, b.Sails, b.Following, f.Plan, f.Loop, f.Stop) = (b.Id, b.Name, f.Name, false, [], false, 0);
        return true;
    }

    /// <summary>Her master comes ashore (she must be home): her purse comes with them, and her hold goes to the Exchange. The crowns for the hero.</summary>
    public static int StandDown(FleetShip f, Bond? b, IDock home)
    {
        if (f.Away) return 0;
        var crowns = f.Purse + f.Cargo.Sum(c => home.Sell(c.Key, c.Value));
        (f.Master, f.MasterName, f.Plan, f.Loop, f.Stop, f.Purse, f.Stake) = (null, "", [], false, 0, 0, 0);
        f.Cargo.Clear();
        f.Paid.Clear();
        if (b is not null) b.Sails = null;
        return crowns;
    }

    /// <summary>
    /// Orders for her master: the ports to make, in order, and whether to go round them again and again. Every one must
    /// be a port the hero's ships know (or home, or past the charts); a route wants two stops at least. Under sail she
    /// finishes the leg she's on first.
    /// </summary>
    public bool Order(FleetShip f, IReadOnlyList<string> stops, bool loop, double now)
    {
        if (!f.Mastered || !f.Crewed || stops.Count == 0 || stops.Count > MostStops) return false;
        if (stops.Any(s => s != Seas.Tristinwold && s != Seas.Beyond && Known.All(p => p.Id != s))) return false;
        if (loop && (stops.Distinct().Count() < 2 || stops.Contains(Seas.Beyond))) return false;
        (f.Plan, f.Loop, f.Stop) = ([.. stops], loop, 0);
        if (!f.Sailing) f.NextSail = now; // off on the tide
        f.Note(loop
            ? $"Orders: a trade route, {string.Join(", ", stops.Select(Seas.NameOf))}, and round again."
            : $"Orders: {string.Join(", then ", stops.Select(s => s == Seas.Beyond ? "out past the charts" : Seas.NameOf(s)))}.");
        return true;
    }

    /// <summary>The hero's crowns into her purse, to trade with. Only in Tristinwold, where the hero can hand them over.</summary>
    public static bool StakeHer(FleetShip f, int crowns)
    {
        if (f.Away || crowns <= 0) return false;
        f.Purse += crowns;
        f.Stake += crowns;
        return true;
    }

    /// <summary>
    /// The fleet's masters about their orders while the hero's away: casting off when it's time, making port, trading
    /// there and nowhere else, finding new land, beating off pirates (or not), and bringing home what they've made.
    /// What came of it, for the hero to hear.
    /// </summary>
    public List<ShipNews> TendFleet(double now, Func<string, IDock?> dock, Func<string, Bond?> bond, Rng rng)
    {
        var news = new List<ShipNews>();
        foreach (var f in Fleet.Where(f => f.Mastered))
            for (var step = 0; step < 40; step++) // catch up however many legs have gone by
            {
                if (bond(f.Master!) is null)
                {
                    // Their master's gone (old age comes for everyone): the hands bring her home, and nothing more.
                    var name = f.MasterName;
                    (f.Master, f.MasterName, f.Plan, f.Loop, f.Stop) = (null, "", f.Where == Seas.Tristinwold && !f.Sailing ? [] : [Seas.Tristinwold], false, 0);
                    f.Note($"{name} is gone. The hands bring her home.");
                    if (f.Plan.Count > 0 && !f.Sailing) f.NextSail = now;
                    news.Add(new(f, $"{f.Name} has lost her master, {name}. Her hands are bringing her home."));
                    break;
                }
                if (f.Sailing)
                {
                    if (now < f.Arrives) break;
                    news.AddRange(MakePort(f, dock, bond, rng));
                    continue;
                }
                if (f.Plan.Count == 0 || now < f.NextSail) break;
                if (CastOff(f, dock, rng) is { } said) news.Add(new(f, said));
            }
        foreach (var f in Fleet.Where(f => !f.Mastered && f.Plan.Count > 0)) // a masterless ship the hands are bringing home
            if (f.Sailing ? now >= f.Arrives : now >= f.NextSail)
            {
                if (f.Sailing) news.AddRange(MakePort(f, dock, bond, rng));
                else CastOff(f, dock, rng);
            }
        return news;
    }

    /// <summary>Off to the next port on her orders (or past the charts): word only if there's nowhere left to find.</summary>
    string? CastOff(FleetShip f, Func<string, IDock?> dock, Rng rng)
    {
        var to = f.Plan[f.Stop];
        if (to == Seas.Beyond)
        {
            if (Unfound is not { } edge)
            {
                Advance(f);
                f.Note("Nothing left past the charts to find.");
                return $"{f.MasterName} writes from {f.Name}: every sea's been charted. There's nowhere left to find.";
            }
            (to, f.Charting) = (edge.Id, true);
        }
        if (to == f.Where) { Advance(f); return null; } // there already
        if (f.Hull < Seaworthy)
        {
            f.Hull = Seaworthy; // the hands patch her up enough to swim, which takes a while
            f.NextSail += LayOver * 3;
            f.Note("Patched up to swim.");
            return null;
        }
        // Her master loads what's cheap here and dear where she's bound, before she goes: here, and nowhere else.
        if (!f.Charting && f.Mastered && dock(f.Where) is { } here && dock(to) is { } there && Load(f, here, there) is { } loaded) f.Note(loaded);
        var days = Seas.Between(f.Where, to);
        (f.From, f.Bound, f.At, f.Departed) = (f.Where, to, null, f.NextSail);
        f.Arrives = f.Departed + days * CityEconomy.DaySeconds / f.Speed;
        f.Note($"Cast off from {Seas.NameOf(f.From)} for {(f.Charting ? "the edge of the charts" : Seas.NameOf(to))}, {days} day{(days == 1 ? "" : "s")}.");
        return null;
    }

    static void Advance(FleetShip f)
    {
        if (++f.Stop < f.Plan.Count) return;
        f.Stop = 0;
        if (!f.Loop) f.Plan = [];
    }

    /// <summary>In port at the end of a leg: the pirates on the way, the land found, the trading there, the mending, and the takings home.</summary>
    List<ShipNews> MakePort(FleetShip f, Func<string, IDock?> dock, Func<string, Bond?> bond, Rng rng)
    {
        var news = new List<ShipNews>();
        var (port, from, charting) = (f.Bound!, f.From ?? Seas.Tristinwold, f.Charting);
        var master = f.Master is { } id ? bond(id) : null;
        var days = Seas.Between(from, port);
        var landed = f.Arrives;
        (f.At, f.Bound, f.From, f.Charting) = (port == Seas.Tristinwold ? null : port, null, null, false);
        var xp = 15 * days;

        // Pirates on the way, maybe: a master worth the name beats them off; a green one loses half the hold and some of the purse.
        var pirates = charting ? Math.Max(0.5, Seas.Get(port)?.Pirates ?? 0.5) : Seas.PiratesBetween(from, port);
        if (rng.Chance(Math.Min(0.9, pirates * (0.6 + 0.2 * days))))
        {
            var holds = Math.Clamp(0.25 + 0.035 * (master?.Level ?? 0) + (f.Crewed ? 0.1 : 0), 0.25, 0.85);
            var takes = Hulls[f.Size].Takes * (f.Upgrades.Contains("iron_strakes") ? 0.75 : 1);
            if (master is not null && rng.Chance(holds))
            {
                f.Hull = Math.Max(10, f.Hull - (3 + rng.NextDouble() * 9) * takes);
                xp += 40;
                f.Note($"Pirates on the way to {Seas.NameOf(port)}: beaten off.");
                news.Add(new(f, $"{f.MasterName} beat off pirates on the way to {Seas.NameOf(port)}! {f.Name}'s hull is at {f.Hull:0}%."));
            }
            else
            {
                f.Hull = Math.Max(10, f.Hull - (25 + rng.NextDouble() * 20) * takes);
                var lost = 0;
                foreach (var g in f.Cargo.Keys.ToList())
                {
                    var gone = (f.Cargo[g] + 1) / 2;
                    lost += gone;
                    f.Paid[g] = f.Paid.GetValueOrDefault(g) * (f.Cargo[g] - gone) / Math.Max(1, f.Cargo[g]);
                    if ((f.Cargo[g] -= gone) <= 0) { f.Cargo.Remove(g); f.Paid.Remove(g); }
                }
                var coin = Math.Min(f.Purse / 10, 40 + 60 * days); // what's in the master's strongbox they mostly keep hidden
                f.Purse -= coin;
                f.Note($"Boarded on the way to {Seas.NameOf(port)}: lost {lost} loads and {coin} crowns.");
                news.Add(new(f, $"Pirates boarded {f.Name} on the way to {Seas.NameOf(port)} and took {lost} loads and {coin} crowns. Her hull is at {f.Hull:0}%."));
            }
        }

        if (charting && port != Seas.Tristinwold && Charted.Add(port))
        {
            var found = Seas.Get(port)!;
            f.Note($"Found {found.Name}!");
            news.Add(new(f, $"{f.MasterName} has found land past the charts: {found.Name}, {found.About}. {f.Name} is tied up there now, and it's on your charts for good."));
            xp += 80;
        }
        if (f.Plan.Count > 0 && f.Plan[f.Stop] == (charting ? Seas.Beyond : port)) Advance(f);
        master?.Gain(xp);

        // Trading, at this port's market and nowhere else.
        if (dock(port) is { } here)
        {
            var next = f.Plan.Count > 0 && f.Plan[f.Stop] is var n && n != Seas.Beyond && n != port ? dock(n) : port == Seas.Tristinwold ? null : dock(Seas.Tristinwold);
            var said = Unload(f, here, next);
            if (said is not null) f.Note(said);
            Mend(f);
            if (Pays(port) && f.Purse > f.Stake)
            {
                var home = f.Purse - f.Stake;
                f.Purse = f.Stake;
                news.Add(new(f, $"{f.Name} is in {(port == Seas.Tristinwold ? "Tristinwold harbour" : $"at {Seas.NameOf(port)}")}. {f.MasterName} sends you {home} crowns from her trading{(said is null ? "" : $": {said}")}.", home));
            }
            else if (said is not null && port != Seas.Tristinwold && f.Plan.Count == 0)
                news.Add(new(f, $"{f.Name} has made {here.Name}. {said}"));
        }
        if (f.Plan.Count == 0 && port != Seas.Tristinwold && !charting) f.Note($"Waiting at {Seas.NameOf(port)} for orders.");
        f.NextSail = landed + LayOver;
        return news;
    }

    /// <summary>
    /// Her master sells at the port she's tied up in: whatever this market will take, unless the next port on her orders
    /// would pay better for it and this one wouldn't cover what it cost. What was done, said plainly, or null.
    /// </summary>
    public static string? Unload(FleetShip f, IDock here, IDock? next)
    {
        var lines = new List<string>();
        foreach (var (g, n) in f.Cargo.ToList())
        {
            var paid = f.Paid.GetValueOrDefault(g);
            if (next is not null && next.Id != here.Id && next.Pays(g) > here.Pays(g) * 1.1 && here.Pays(g) * n < paid) continue; // worth more further on
            var crowns = here.Sell(g, n);
            f.Made += crowns - paid;
            f.Purse += crowns;
            f.Cargo.Remove(g);
            f.Paid.Remove(g);
            lines.Add($"sold {n} {CityEconomy.Info(g).Name} for {crowns}");
        }
        return Said(here, lines);
    }

    /// <summary>
    /// And buys at the port she's tied up in, out of her purse, for where she's bound: what's cheap here and dear there,
    /// the best bargains first, a load at a time while it's still worth it. What was done, or null.
    /// </summary>
    public static string? Load(FleetShip f, IDock here, IDock there)
    {
        if (there.Id == here.Id) return null;
        var lines = new List<string>();
        var room = f.HoldSize - f.Loaded;
        int Margin(Good g) => (int)(there.Pays(g) * 0.95) - here.Asks(g) - Math.Max(2, here.Asks(g) / 5);
        foreach (var g in Enum.GetValues<Good>().Where(g => here.ForSale(g) > 0 && Margin(g) > 0).OrderByDescending(g => (double)Margin(g) / here.Asks(g)).ToList())
        {
            var (n, cost) = (0, 0);
            while (room > 0 && n < MostOfOne && here.ForSale(g) > 0 && Margin(g) > 0 && f.Purse >= here.Asks(g))
            {
                var c = here.Buy(g, 1);
                (f.Purse, cost, n, room) = (f.Purse - c, cost + c, n + 1, room - 1);
            }
            if (n == 0) continue;
            f.Cargo[g] = f.Cargo.GetValueOrDefault(g) + n;
            f.Paid[g] = f.Paid.GetValueOrDefault(g) + cost;
            lines.Add($"bought {n} {CityEconomy.Info(g).Name} for {cost}");
        }
        return Said(here, lines);
    }

    static string? Said(IDock here, List<string> lines) => lines.Count == 0 ? null : $"At {here.Name}: {string.Join(", ", lines)}.";

    /// <summary>The yard at the port mends her out of her purse, as much as it'll run to.</summary>
    static void Mend(FleetShip f)
    {
        if (f.Hull >= 100) return;
        var planks = Math.Min((int)Math.Ceiling((100 - f.Hull) / PlankMends), f.Purse / 12);
        if (planks <= 0) return;
        f.Purse -= planks * 12;
        f.Hull = Math.Min(100, f.Hull + planks * PlankMends);
    }
}
