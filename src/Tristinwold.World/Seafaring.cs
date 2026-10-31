using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>Where a sailor stands aboard: not signed on, then the four ranks from the bottom of the deck to the top.</summary>
public enum SeaRank { Landsman, Deckhand, Bosun, FirstMate, Captain }

/// <summary>Where a pirate stands in their crew: none, then up from the bottom to the captain's chair.</summary>
public enum PirateRank { None, Swab, Cutthroat, Quartermaster, Captain }

/// <summary>Where a shipwright stands in the yard.</summary>
public enum WrightRank { None, Apprentice, Journeyman, Master }

/// <summary>
/// A port over the sea from Tristinwold: how many days' sailing it is (in leagues of a day each), how thick the pirates
/// are on the way, and what its market makes and wants.
/// </summary>
public sealed record SeaPort(string Id, string Name, int Leagues, double Pirates, Good[] Makes, Good[] Wants, string About)
{
    /// <summary>Its market the first time a Tristinwold ship comes in: a busy port's, bigger than a village's.</summary>
    public TownMarket Market(int day)
    {
        var m = new TownMarket { Id = Id, Name = Name, Makes = [.. Makes], Wants = [.. Wants], Day = day, Size = 0.45 };
        foreach (var g in Enum.GetValues<Good>()) m.Stock[g] = m.Usual(g);
        return m;
    }
}

/// <summary>The ports Tristinwold's ships sail to, nearest first.</summary>
public static class Seas
{
    /// <summary>The worst names on the water, worst first.</summary>
    public static readonly IReadOnlyList<Flagship> Flagships =
    [
        new("soren", "the Dread Pirate Soren", "the Red Reckoning", "dread_soren", 3, 0.14, 2500, "sorens_reaver",
            "A black sail, and a red coat at her rail with a sword as tall as you are. The Dread Pirate Soren is coming aboard",
            "Soren laughs round his pipe with your blood on his gauntlet, and steps backward off the rail. A rope takes him up. \"Again sometime,\" he calls.",
            "\"Not one in a hundred stays standing that long. I've Black Ysolde and a dozen like her who answer to me; go and swab her deck till you've earned mine. Or there's the sea.\""),
        new("wukong", "Lu Wukong", "the Drunken Monkey", "lu_wukong", 2, 0.22, 1200, "wukongs_staff",
            "A patched sail and somebody singing. He doesn't rob the honest, and you aren't. A hooded ape swings across on a chain with a gourd at his hip: Lu Wukong has come for your plunder",
            "Lu Wukong vaults the rail on his staff, takes a pull at his gourd and bows from the other deck. \"Tell my brother I said hello!\"",
            "\"You fight like someone who's been told they can't. I like that. Your ship's mine, and your plunder goes back where it came from. But I've a berth on the Drunken Monkey for anyone who'd sooner rob your sort than be one. Or there's the sea, which is also fine.\"",
            HuntsPirates: true),
    ];

    public static Flagship? Flag(string? id) => Flagships.FirstOrDefault(f => f.Id == id);

    /// <summary>Whether a voyage this far out crosses a named captain: the worst of them only sail the far water.</summary>
    public static Flagship? Flies(int leagues, Rng rng) => Flagships.FirstOrDefault(f => !f.HuntsPirates && leagues >= f.MinLeagues && rng.Chance(f.Chance));

    public static readonly IReadOnlyList<SeaPort> Ports =
    [
        new("port:druitt", "Druitt", 1, 0.1, [Good.Timber, Good.Leather], [Good.Bread, Good.Iron, Good.Ale],
            "the timber port under the western mountains, a day round the headland; it lives on what it cuts"),
        new("port:greywater", "Greywater", 1, 0.1, [Good.Bread, Good.Herbs, Good.Ale], [Good.Timber, Good.Iron, Good.Stone],
            "the orchard town on the wood's eastern shore, a day out of the cove and round the point, all fruit trees, berries and cider"),
        new("port:saltmere", "Saltmere", 1, 0.25, [Good.Fish, Good.Ale], [Good.Timber, Good.Bread],
            "a fishing town on the next cape up the coast, all smokehouses and nets; the short run, and the pirates mostly leave it be"),
        new("port:brinehollow", "Brinehollow", 2, 0.4, [Good.Leather, Good.Meat], [Good.Iron, Good.Fish],
            "a tanners' port in a deep inlet, two days down the coast; it stinks, and it pays"),
        new("port:vosk", "Vosk", 2, 0.5, [Good.Iron, Good.Ore, Good.Coal], [Good.Bread, Good.Ale, Good.Herbs],
            "a smelting town under a smoking mountain, across the strait; the corsairs of the Teeth watch that strait"),
        new("port:corrow", "the Isle of Corrow", 3, 0.65, [Good.Herbs, Good.Precious], [Good.Timber, Good.Iron, Good.Potions],
            "an island of terraced herb gardens and pearl divers, three days out over open water, where the pirates hunt"),
        new("port:ambermouth", "Ambermouth", 4, 0.85, [Good.Potions, Good.Precious], [Good.Leather, Good.Stone, Good.Fish],
            "a rich alchemists' city at the mouth of the Amber, four days south through the worst of the pirate waters"),
    ];

    /// <summary>What lies past the known ports, nearest first: nobody's sailed there and come back with a chart. Each one found is a port for good.</summary>
    public static readonly IReadOnlyList<SeaPort> Uncharted =
    [
        new("port:skerrylund", "Skerrylund", 4, 0.45, [Good.Fish, Good.Leather], [Good.Timber, Good.Ale, Good.Bread],
            "a scatter of rocky isles where the seal-hunters winter, off every chart the Brine Company owns"),
        new("port:halcyon", "Port Halcyon", 5, 0.5, [Good.Herbs, Good.Ale], [Good.Iron, Good.Stone, Good.Potions],
            "a white-walled harbour town on a warm coast, who'd never heard of Tristinwold and were glad to"),
        new("port:cinderisles", "the Cinder Isles", 5, 0.7, [Good.Coal, Good.Ore], [Good.Bread, Good.Timber, Good.Meat],
            "black islands that smoke at the peak, mined by people who've learned not to mind"),
        new("port:pearlatoll", "the Pearl Atoll", 6, 0.55, [Good.Precious, Good.Fish], [Good.Timber, Good.Iron, Good.Leather],
            "a ring of reef and palm round a green lagoon, where the divers go deeper than anyone should"),
        new("port:verdane", "Verdane", 7, 0.6, [Good.Timber, Good.Herbs], [Good.Iron, Good.Potions, Good.Precious],
            "the first port on a whole new continent of forest, its back to trees nobody's seen the far side of"),
        new("port:ostmark", "Ostmark", 8, 0.8, [Good.Iron, Good.Precious], [Good.Bread, Good.Ale, Good.Herbs],
            "a walled city of iron and old money on the far continent's cold shore, with a fleet of its own"),
    ];

    /// <summary>The only two ports a Tristinwold ship knows at the start. Everything else has to be found.</summary>
    public static readonly IReadOnlyList<string> Home = ["port:druitt", "port:greywater"];

    /// <summary>What there is to find, nearest first.</summary>
    public static IEnumerable<SeaPort> ToChart => Ports.Where(p => !Home.Contains(p.Id)).Concat(Uncharted);

    public static SeaPort? Get(string id) => Ports.Concat(Uncharted).FirstOrDefault(p => p.Id == id);
}

/// <summary>
/// A name on the water: a pirate captain who comes over the rail in person. The Dread Pirate Soren is the worst of them,
/// and Lu Wukong, brother to the ape king of Hollowheart, the next, though he only comes aboard pirates. A big enough name
/// gets away over the side, so they can be met again.
/// </summary>
public sealed record Flagship(string Id, string Captain, string Ship, string Kind, int MinLeagues, double Chance, int Bounty, string Loot, string Hail, string Escape, string Offer, bool HuntsPirates = false);

/// <summary>A voyage under way: out to a port, a call there, and home again; and the pirates waiting on each leg.</summary>
public sealed class Voyage
{
    public string Port { get; set; } = "";
    public string PortName { get; set; } = "";
    public int Leagues { get; set; } = 1;
    /// <summary>Sailing home (false: still on the way out, or in port).</summary>
    public bool Homeward { get; set; }
    /// <summary>Tied up at the port.</summary>
    public bool InPort { get; set; }
    /// <summary>When on each leg the pirates come (as a share of the way), out and home.</summary>
    public List<double> Out { get; set; } = [];
    public List<double> Home { get; set; } = [];
    /// <summary>How many of this leg's attacks have come.</summary>
    public int Sprung { get; set; }
    /// <summary>How many pirates board in each attack.</summary>
    public int Pirates { get; set; }
    /// <summary>A named captain hunting these waters this voyage (see <see cref="Seas.Flagships"/>): whoever boards first.</summary>
    public string? Flag { get; set; }
    /// <summary>The crew in it (see <see cref="PirateCrew"/>): the one working these waters, or on a cruise of a good crew's, the pirates being hunted.</summary>
    public string? Crew { get; set; }
    public int Pay { get; set; }
    /// <summary>The hero has command (a captain), rather than serving under someone.</summary>
    public bool Command { get; set; }
    /// <summary>A pirate cruise: out after a merchantman (the attack on the way out is the hero's crew boarding her), and home to the Teeth.</summary>
    public bool Raid { get; set; }
    /// <summary>On a cruise, whether the prize was taken.</summary>
    public bool Prize { get; set; }
    /// <summary>The deck work done on this voyage, and how much there's been (more comes all the way: see the game's AtSea).</summary>
    public int Chores { get; set; }
    public int ChoresDue { get; set; }

    /// <summary>How far along this leg (0 to 1), kept with the save: a voyage is long.</summary>
    public double Along { get; set; }
    /// <summary>Out past the charts, to find a port rather than to trade with one.</summary>
    public bool Explore { get; set; }
    /// <summary>How fast she sails against the usual: light sails make it 1.33.</summary>
    public double Pace { get; set; } = 1;
    /// <summary>A leg takes a day of the city's clock for every day out: ten minutes a league.</summary>
    public double LegSeconds => Leagues * CityEconomy.DaySeconds / Math.Max(0.5, Pace);

    public List<double> Leg => Homeward ? Home : Out;
    /// <summary>What's boarding on this cruise is the law, and not other pirates.</summary>
    public bool Marines => Raid && Crew is null && !(Homeward && Flag is not null);
    public bool AttackDue(double along) => !InPort && Sprung < Leg.Count && along >= Leg[Sprung];
    public string Danger => (Out.Count + Home.Count) switch { 0 => "calm water", 1 => "a sail or two on the horizon", 2 => "pirate water", _ => "the worst of the pirate water" };
}

/// <summary>
/// Life at sea and in the shipyard (saved with the company). Sign on with the harbour master and sail as a deckhand
/// on the Brine Company's ships to the ports over the sea; each voyage home counts, and beating off pirates counts
/// more, and the company promotes: bosun, first mate, and at last captain, with a ship of the company's to command or
/// one of your own. The shipyard on the shore takes on anyone who'll carry a plank: an apprentice fits planks to the
/// hulls pirates have holed, a journeyman for better pay, and a master shipwright can lay down a keel and build a ship
/// of their own for the price of the timber.
/// </summary>
public sealed class SeaBook
{
    public SeaRank Rank { get; set; }
    public int Voyages { get; set; }
    public int Repelled { get; set; }
    public int Lost { get; set; }
    public int Earned { get; set; }
    public Voyage? Voyage { get; set; }

    public WrightRank Wright { get; set; }
    public int Repairs { get; set; }
    /// <summary>How far the hero's own ship has come on the slipway (out of <see cref="KeelJobs"/>).</summary>
    public int Keel { get; set; }
    public bool KeelLaid { get; set; }

    public bool OwnShip { get; set; }
    public string ShipName { get; set; } = "";
    /// <summary>The state of the hull the hero sails in, out of 100: pirates hole it, the shipyard mends it (the company keeps its own ships mended).</summary>
    public double Hull { get; set; } = 100;
    /// <summary>What an ether-aeronaut has fitted at the shipyard (see <see cref="Modules"/>): it sails with them on every ship they're aboard.</summary>
    public HashSet<string> Modules { get; set; } = [];
    public Dictionary<Good, int> Hold { get; set; } = [];
    public int Profit { get; set; }
    public Dictionary<string, TownMarket> Markets { get; set; } = [];

    public const int ToBosun = 3, ToMate = 7, MateRepels = 2, ToCaptain = 12, CaptainRepels = 5, CaptainLevel = 8;
    public const int JourneymanAt = 3, MasterAt = 8, PlanksPerJob = 4, KeelJobs = 3, KeelPrice = 500, ShipPrice = 1500;
    public const double PlankMends = 9, Seaworthy = 40;

    /// <summary>The modules an ether-aeronaut can fit, and what they cost: a bilge pump (boarders hole her half as fast) and light sails (a quarter off every leg).</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, int Price, string Text)> ModuleKinds = new Dictionary<string, (string, int, string)>
    {
        ["bilge_pump"] = ("an automated bilge pump", 180, "pumps as fast as boarders can hole her: the hull takes half the harm"),
        ["light_sails"] = ("light sails", 260, "a third more way on her: a quarter off every leg, and pirates with less time to find her"),
    };

    public bool HasModule(string id) => Modules.Contains(id);

    /// <summary>Fits a module, if it's one and it isn't fitted: the price paid, or null.</summary>
    public int? Fit(string id, ref int crowns)
    {
        if (!ModuleKinds.TryGetValue(id, out var kind) || Modules.Contains(id) || crowns < kind.Price) return null;
        crowns -= kind.Price;
        Modules.Add(id);
        return kind.Price;
    }

    /// <summary>How fast a ship sails with what's fitted.</summary>
    public double Pace => HasModule("light_sails") ? 1.33 : 1;
    public const string CompanyShip = "the Gull";

    // The other side of the law: a hero whose ship was taken may join the pirates who took it.
    public PirateRank Pirate { get; set; }
    /// <summary>The crew they're sworn to, by its id; none means the Widow's Tooth (saves from before there were others).</summary>
    public string? Crew { get; set; }
    public PirateCrew Sworn => PirateCrew.Get(Crew) ?? PirateCrew.WidowsTooth;
    /// <summary>Crews the hero has beaten, and the play time until which they're keeping their heads down.</summary>
    public Dictionary<string, double> Broken { get; set; } = [];
    /// <summary>"crew:3#41": a round of a crew's that's already been heard of ashore.</summary>
    public HashSet<string> Heard { get; set; } = [];
    public int Raids { get; set; }
    public int Prizes { get; set; }
    public const int ToCutthroat = 2, ToQuartermaster = 5, ToPirateCaptain = 9, PardonPrice = 400;
    public bool Outlaw => Pirate != PirateRank.None;
    static readonly string[] Prey = ["a Saltmere herring boat", "a Brinehollow tanner", "a Vosk ore-carrier", "a Corrow pearl-trader", "an Ambermouth spice ship"];

    /// <summary>How many loads the hold takes: a captain's private venture on the company's ship, or a whole hold of their own.</summary>
    public int Capacity => OwnShip ? 50 : Rank == SeaRank.Captain ? 20 : 0;
    public int Loaded => Hold.Values.Sum();
    public int Room => Math.Max(0, Capacity - Loaded);
    public string Ship => OwnShip ? ShipName : CompanyShip;

    /// <summary>How far out the company will send someone of the hero's rank.</summary>
    public int Reach => Rank switch { SeaRank.Captain or SeaRank.FirstMate => 4, SeaRank.Bosun => 3, _ => 2 };
    public IEnumerable<SeaPort> Ports => Seas.Ports.Where(p => Seas.Home.Contains(p.Id) || Charted.Contains(p.Id) && p.Leagues <= Reach).Concat(Seas.Uncharted.Where(p => Charted.Contains(p.Id)));

    /// <summary>The ports the hero has found past the old charts; open to them for good.</summary>
    public HashSet<string> Charted { get; set; } = [];
    /// <summary>The named captains this hero has driven off their deck.</summary>
    public HashSet<string> Humbled { get; set; } = [];
    /// <summary>A bosun can be trusted with a chart. The next place past the edge of it, if there's one left.</summary>
    public SeaPort? NextChart => Rank >= SeaRank.Bosun ? Seas.ToChart.FirstOrDefault(p => !Charted.Contains(p.Id)) : null;
    public const double ExplorePay = 1.6;

    public static string Explorer(int charted) => charted switch { >= 6 => "master of the charts", >= 4 => "pathfinder", >= 2 => "navigator", >= 1 => "chart-maker", _ => "" };

    public static string Title(SeaRank r) => r switch
    {
        SeaRank.Deckhand => "deckhand", SeaRank.Bosun => "bosun", SeaRank.FirstMate => "first mate", SeaRank.Captain => "captain", _ => "landsman",
    };

    public static string Title(PirateRank r) => r switch
    {
        PirateRank.Swab => "swab", PirateRank.Cutthroat => "cutthroat", PirateRank.Quartermaster => "quartermaster", PirateRank.Captain => "pirate captain", _ => "",
    };

    public static string Title(WrightRank r) => r switch
    {
        WrightRank.Apprentice => "apprentice shipwright", WrightRank.Journeyman => "journeyman shipwright", WrightRank.Master => "master shipwright", _ => "",
    };

    public bool SignOn()
    {
        if (Rank != SeaRank.Landsman || Outlaw) return false;
        Rank = SeaRank.Deckhand;
        return true;
    }

    /// <summary>A voyage's wage: by rank, and more the further out (a captain's is the company's wage on top of any trading).</summary>
    public int PayFor(SeaPort port) =>
        (int)Math.Round((Rank switch { SeaRank.Bosun => 32, SeaRank.FirstMate => 50, SeaRank.Captain => OwnShip ? 0 : 70, _ => 20 }) * (1 + 0.3 * (port.Leagues - 1)));

    public TownMarket Market(SeaPort port, int day)
    {
        if (!Markets.TryGetValue(port.Id, out var m)) Markets[port.Id] = m = port.Market(day);
        m.Catch(day);
        return m;
    }

    /// <summary>Sets sail for a port: where the pirates lie in wait on each leg, and the deck work there'll be.</summary>
    /// <param name="explore">Out past the charts: it's the port that's the prize, and the company pays more for the risk.</param>
    /// <param name="crew">The small crew working that lane just now, if any: it's them who board, and not the Teeth.</param>
    public Voyage? Sail(SeaPort port, Rng rng, bool explore = false, PirateCrew? crew = null)
    {
        var allowed = explore ? NextChart == port : Ports.Contains(port);
        if (Rank == SeaRank.Landsman || Outlaw || Voyage is not null || !allowed || OwnShip && Hull < Seaworthy) return null;
        if (!OwnShip) Hull = 100; // the company sends a sound ship
        List<double> Leg()
        {
            var attacks = new List<double>();
            for (var i = 0; i < 1 + port.Leagues / 2; i++)
                if (rng.Chance(port.Pirates)) attacks.Add(0.3 + 0.5 * (i + rng.NextDouble()) / (1 + port.Leagues / 2));
            return attacks;
        }
        return Voyage = new Voyage
        {
            Pace = Pace,
            Port = port.Id, PortName = port.Name, Leagues = port.Leagues, Out = Leg(), Home = Leg(),
            Pirates = (crew?.Hands ?? 2 + port.Leagues + rng.Next(2)) + (Rank >= SeaRank.FirstMate ? 1 : 0),
            Pay = (int)Math.Round(PayFor(port) * (explore ? ExplorePay : 1)), Command = Rank == SeaRank.Captain, Explore = explore,
            Flag = Seas.Flies(port.Leagues, rng)?.Id, Crew = crew?.Id,
        };
    }

    /// <summary>The small crew working the run to a port just now, if one's out and not licking its wounds.</summary>
    public PirateCrew? Hunters(SeaPort port, double playSeconds) =>
        PirateCrew.Small.FirstOrDefault(c => !IsBroken(c, playSeconds) && c.Where(playSeconds).Lane?.Id == port.Id);

    public bool IsBroken(PirateCrew c, double playSeconds) => Broken.GetValueOrDefault(c.Id) > playSeconds;

    /// <summary>A crew beaten: it keeps its head down for an hour.</summary>
    public void Break(PirateCrew c, double playSeconds) => Broken[c.Id] = playSeconds + PirateCrew.BrokenSeconds;

    /// <summary>The first this company's heard of a crew's round on a lane. Keeps only the latest few hundred.</summary>
    public bool Hear(PirateCrew c, int round)
    {
        if (!Heard.Add($"{c.Id}#{round}")) return false;
        if (Heard.Count > 400) Heard = [.. Heard.Skip(200)];
        return true;
    }

    /// <summary>A named captain beaten off: the price on them the first time, a purse after that. The flag's gone for this voyage.</summary>
    public int Humble(Flagship f)
    {
        if (Voyage is { } v) v.Flag = null;
        return Humbled.Add(f.Id) ? f.Bounty : f.Bounty / 5;
    }


    /// <summary>The wage for a voyage, by the share of the deck work done: a hand who shirks is paid as one (a quarter, at worst).</summary>
    public static int Wage(Voyage v) => v.ChoresDue == 0 ? v.Pay : (int)Math.Round(v.Pay * Math.Clamp(0.25 + 0.75 * v.Chores / v.ChoresDue, 0.25, 1));

    /// <summary>Ashore at the port.</summary>
    public bool Arrive()
    {
        if (Voyage is not { InPort: false, Homeward: false } v) return false;
        v.InPort = true;
        if (v.Explore) Charted.Add(v.Port);
        return true;
    }

    /// <summary>Cast off for home.</summary>
    public bool CastOff()
    {
        if (Voyage is not { InPort: true } v) return false;
        (v.InPort, v.Homeward, v.Sprung, v.Along) = (false, true, 0, 0);
        return true;
    }

    /// <summary>A boarding beaten off.</summary>
    public void Repel()
    {
        Repelled++;
        if (Voyage is { } v) v.Sprung++;
    }

    /// <summary>Pirates aboard hole the ship while they're on her.</summary>
    public void Damage(double amount) => Hull = Math.Max(0, Hull - amount * (HasModule("bilge_pump") ? 0.5 : 1));

    /// <summary>
    /// Home again, the voyage done: the wage (less if the deck work wasn't done), the count, and any promotion the
    /// company gives for it (the new rank, or null).
    /// </summary>
    public (int Pay, SeaRank? Promoted) Home(int heroLevel)
    {
        if (Voyage is not { Homeward: true } v) return (0, null);
        var pay = Wage(v);
        Voyage = null;
        Voyages++;
        Earned += pay;
        return (pay, Promote(heroLevel));
    }

    /// <summary>What the company thinks the hero's earned: the next rank, if they've the voyages, the fights, and (for command) the years in them.</summary>
    public SeaRank? Promote(int heroLevel)
    {
        SeaRank? next = Rank switch
        {
            SeaRank.Deckhand when Voyages >= ToBosun => SeaRank.Bosun,
            SeaRank.Bosun when Voyages >= ToMate && Repelled >= MateRepels => SeaRank.FirstMate,
            SeaRank.FirstMate when Voyages >= ToCaptain && Repelled >= CaptainRepels && heroLevel >= CaptainLevel => SeaRank.Captain,
            _ => null,
        };
        if (next is { } r) Rank = r;
        return next;
    }

    /// <summary>What's still wanted for the next rank, said plainly (null at the top).</summary>
    public string? NextStep(int heroLevel) => Rank switch
    {
        SeaRank.Landsman => "Sign on with the harbour master.",
        SeaRank.Deckhand => $"{Math.Max(0, ToBosun - Voyages)} more voyage{(ToBosun - Voyages == 1 ? "" : "s")} home, and the company makes you bosun.",
        SeaRank.Bosun => $"First mate wants {ToMate} voyages ({Voyages} so far) and {MateRepels} boardings beaten off ({Repelled} so far).",
        SeaRank.FirstMate => $"Command wants {ToCaptain} voyages ({Voyages}), {CaptainRepels} boardings beaten off ({Repelled}), and some years on you (level {CaptainLevel}; you're {heroLevel}).",
        _ => null,
    };

    /// <summary>
    /// Taken, and offered a place among the pirates who took the ship (better than going over the side): struck off the
    /// company's books, and a swab aboard the Widow's Tooth.
    /// </summary>
    public bool JoinPirates() => JoinCrew(PirateCrew.WidowsTooth);

    /// <summary>A place taken in a crew: off the company's books, or off another crew's, and a swab at the bottom of this one.</summary>
    public bool JoinCrew(PirateCrew crew)
    {
        if (Outlaw && Sworn == crew) return false;
        (Pirate, Rank, Voyage, Crew, Prizes) = (PirateRank.Swab, SeaRank.Landsman, null, crew.Id, 0);
        return true;
    }

    /// <summary>
    /// A cruise. Most crews go after a merchantman: her crew to beat on the way out, maybe a company patrol on the way home,
    /// or Lu Wukong, who hunts their sort. A good crew hunts another pirate crew, and has that crew's friends to fear.
    /// </summary>
    public Voyage? Prowl(Rng rng, double playSeconds = 0)
    {
        if (!Outlaw || Voyage is not null) return null;
        Hull = 100; // she's the crew's ship, and the crew keep her sound
        var port = Seas.Ports[Math.Min(Seas.Ports.Count - 1, (int)Pirate)];
        var hunted = Sworn.Good ? rng.Pick(PirateCrew.Small.Where(c => !IsBroken(c, playSeconds)).DefaultIfEmpty(PirateCrew.Small[0]).ToList()) : null;
        var hunter = Sworn.Good ? null : Seas.Flagships.FirstOrDefault(f => f.HuntsPirates && Pirate >= PirateRank.Cutthroat && rng.Chance(f.Chance));
        return Voyage = new Voyage
        {
            Pace = Pace,
            Port = "raid", PortName = hunted is { } c ? $"{c.Ship} of {c.Name}" : rng.Pick(Prey[..((int)Pirate + 1)]),
            Leagues = port.Leagues, Raid = true, Out = [0.5], Home = hunter is not null || rng.Chance(0.35) ? [0.4 + 0.3 * rng.NextDouble()] : [],
            Pirates = hunted?.Hands ?? 2 + port.Leagues + rng.Next(2), Pay = 40 + 30 * (int)Pirate, Command = Pirate == PirateRank.Captain,
            Flag = hunter?.Id, Crew = hunted?.Id,
        };
    }

    /// <summary>The merchantman's crew beaten: she's a prize, and the cruise turns for home.</summary>
    public bool TakePrize()
    {
        if (Voyage is not { Raid: true, Homeward: false } v) return false;
        (v.Prize, v.Homeward, v.Sprung, v.Along) = (true, true, 0, 0);
        Prizes++;
        return true;
    }

    /// <summary>Back at the Teeth: the share of the plunder (none without a prize), and promotion in the crew.</summary>
    public (int Plunder, PirateRank? Promoted) Plunder(int heroLevel)
    {
        if (Voyage is not { Raid: true, Homeward: true } v) return (0, null);
        Voyage = null;
        Raids++;
        var share = v.Prize ? Wage(v) : 0;
        Earned += share;
        PirateRank? next = Pirate switch
        {
            PirateRank.Swab when Prizes >= ToCutthroat => PirateRank.Cutthroat,
            PirateRank.Cutthroat when Prizes >= ToQuartermaster => PirateRank.Quartermaster,
            PirateRank.Quartermaster when Prizes >= ToPirateCaptain && heroLevel >= CaptainLevel => PirateRank.Captain,
            _ => null,
        };
        if (next is { } r) Pirate = r;
        return (share, next);
    }

    /// <summary>The company's pardon, bought: off the pirates' books and back on the company's, at the bottom.</summary>
    public bool Pardon()
    {
        if (!Outlaw || Voyage is not null) return false;
        (Pirate, Rank, Crew) = (PirateRank.None, SeaRank.Deckhand, null);
        return true;
    }

    /// <summary>Taken: the pirates have the ship. The cargo's gone with them; the crew are put over the side, and the ship's found drifting, gutted.</summary>
    public void Taken()
    {
        Voyage = null;
        Lost++;
        Hold.Clear();
        Hull = 15;
    }

    // ---- the shipyard -----------------------------------------------------------------------------------------------

    public int PlankPay => Wright switch { WrightRank.Journeyman => 10, WrightRank.Master => 15, _ => 6 };

    /// <summary>A plank fitted to a hull on the slipway: the yard's work mends the hero's own ship too, if it's in.</summary>
    public void FitPlank(bool ownShip)
    {
        if (Wright == WrightRank.None) Wright = WrightRank.Apprentice;
        if (ownShip) Hull = Math.Min(100, Hull + PlankMends);
    }

    /// <summary>A job done in the yard (a hull's planks all fitted): the count, and promotion by it (the new rank, or null).</summary>
    public WrightRank? JobDone()
    {
        Repairs++;
        WrightRank? next = Wright switch
        {
            WrightRank.Apprentice when Repairs >= JourneymanAt => WrightRank.Journeyman,
            WrightRank.Journeyman when Repairs >= MasterAt => WrightRank.Master,
            _ => null,
        };
        if (next is { } r) Wright = r;
        return next;
    }

    public bool MayLayKeel => Wright == WrightRank.Master && !OwnShip && !KeelLaid;

    /// <summary>A master lays down a keel (the timber bought): their own ship, built a job at a time.</summary>
    public bool LayKeel()
    {
        if (!MayLayKeel) return false;
        KeelLaid = true;
        Keel = 0;
        return true;
    }

    /// <summary>A day's work on the hero's own ship on the slipway; launched (true) when the last is done.</summary>
    public bool Build(string name)
    {
        if (!KeelLaid || OwnShip) return false;
        if (++Keel < KeelJobs) return false;
        Launch(name);
        return true;
    }

    /// <summary>A captain may buy a ship from the yard outright.</summary>
    public bool MayBuy => Rank == SeaRank.Captain && !OwnShip;

    public void Launch(string name)
    {
        (OwnShip, ShipName, Hull, KeelLaid) = (true, name, 100, false);
    }

    /// <summary>Names for a new ship, one picked for the hero's.</summary>
    public static string NameShip(Rng rng) => "the " + rng.Pick(["Gannet", "Kestrel", "Saltwind", "Brightwater", "Long Reach", "Second Chance", "Wavecutter", "Storm Petrel", "Ringing", "Hollow Moon"]);
}
