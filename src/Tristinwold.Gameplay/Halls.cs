using System.Text.Json.Serialization;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What a hero can found in the world, by their class: their own little guild, and the people under them.</summary>
public enum HallKind { ThievesGuild, HuntingLodge, FightClub, Monastery, Chapel, Spire, Playhouse, Colony }

/// <summary>
/// One kind of hall: who may found one, what it costs, what it's built of, what its people are called, and what it
/// offers any hero who walks in.
/// </summary>
public sealed record HallPlan(HallKind Kind, string Word, string Member, string Members, IReadOnlyList<string> Classes, int FoundCost, int RecruitCost,
    IReadOnlyList<string> Buildings, string Service, int ServicePrice, string Look, string Steward);

/// <summary>
/// A hall a hero founded, as the shared world keeps it: where it stands, how many are under them, how good they've got,
/// and what's in its till and its stores. It runs on the server's clock, whether its founder's on or not.
/// </summary>
public sealed class Hall
{
    public string Owner { get; set; } = "";
    public string Hero { get; set; } = "";
    public HallKind Kind { get; set; }
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public int Members { get; set; }
    /// <summary>How good its people have got, 0 to 10: a fight club's champion, a monastery's teaching.</summary>
    public double Prowess { get; set; }
    /// <summary>How hot the Watch is on a thieves' guild. Too hot, and thieves get taken.</summary>
    public int Heat { get; set; }
    /// <summary>The day a thieves' guild lying low may go back to work.</summary>
    public int LowUntil { get; set; }
    public int Till { get; set; }
    public Dictionary<string, int> Chest { get; set; } = [];
    public double Work { get; set; }
    /// <summary>The last day each hero was served here, for the services a hero can only have once a day.</summary>
    public Dictionary<string, int> Served { get; set; } = [];
    public int Founded { get; set; }
    /// <summary>Which of its founder's colonies this is: 0 for the first, 2 for the second, and so on. Other halls are one to a founder.</summary>
    public int Number { get; set; }
    /// <summary>A colony's town guards, hired once it has its barracks. Their wages come out of the till.</summary>
    public int Guards { get; set; }

    [JsonIgnore] public string Key => Number > 1 ? $"{Halls.KeyOf(Owner, Kind)}{Number}" : Halls.KeyOf(Owner, Kind);
    /// <summary>A colony as a place with a council: the town it's kept under.</summary>
    [JsonIgnore] public string Town => Halls.ColonyTown(Owner, Number);
    [JsonIgnore] public HallPlan Plan => Halls.PlanOf(Kind);
}

public static class Halls
{
    public static readonly IReadOnlyList<HallPlan> Plans =
    [
        new(HallKind.ThievesGuild, "Thieves' Guild", "thief", "thieves", ["Thief", "Assassin"], 400, 60, ["house", "barn", "cottage", "house"],
            "Hire them for a job", 150, "velvet", "fence"),
        new(HallKind.HuntingLodge, "Hunting Lodge", "hunter", "hunters", ["Ranger"], 300, 40, ["longhall", "cottage", "barn"],
            "Buy from the lodge's stores", 0, "hunter", "lodge-keeper"),
        new(HallKind.FightClub, "Fight Club", "fighter", "fighters", ["Barbarian"], 300, 40, ["longhall", "barn", "house"],
            "Step into the pit", 20, "warmaster", "pit boss"),
        new(HallKind.Monastery, "Monastery", "disciple", "disciples", ["MartialArtist"], 400, 30, ["church", "longhall", "cottage", "house", "cottage"],
            "Train with the disciples", 40, "priest", "elder brother"),
        new(HallKind.Chapel, "Chapel", "acolyte", "acolytes", ["Paladin"], 350, 40, ["church", "house", "cottage"],
            "Ask for a blessing", 25, "priest", "deacon"),
        new(HallKind.Spire, "Spire", "apprentice", "apprentices", ["Wizard", "Warlock", "Sorcerer"], 450, 50, ["tower", "tower", "house", "cottage"],
            "Buy from the spire's shelves", 0, "scholar", "magister"),
        new(HallKind.Playhouse, "Playhouse", "player", "players", ["Poet", "Scribe", "Musician"], 300, 35, ["tavern", "longhall", "house"],
            "See tonight's show", 15, "drinker", "stage-manager"),
        // Any hero, of any class, may settle an island nobody lives on.
        new(HallKind.Colony, "Colony", "settler", "settlers", [], 800, 50, ["cottage", "house", "barn", "cottage", "house"],
            "Trade with the colony", 0, "worker", "reeve"),
    ];

    public static HallPlan PlanOf(HallKind kind) => Plans.First(p => p.Kind == kind);

    /// <summary>What a hero of this class may found, if anything.</summary>
    public static HallPlan? For(string classId) => Plans.FirstOrDefault(p => p.Classes.Contains(classId));

    public static string KeyOf(string owner, HallKind kind) => $"{owner}#{kind}";

    /// <summary>A colony as a place with a council: the town it's kept under.</summary>
    public static string ColonyTown(string owner, int number = 0) => number > 1 ? $"colony:{owner}#{number}" : $"colony:{owner}";

    /// <summary>What a founder's n-th colony is called: their Colony, then their Second Colony, Third Colony...</summary>
    public static string ColonyName(string founder, int number) => number <= 1 ? $"{founder}'s Colony"
        : $"{founder}'s {(number switch { 2 => "Second", 3 => "Third", 4 => "Fourth", 5 => "Fifth", 6 => "Sixth", 7 => "Seventh", 8 => "Eighth", 9 => "Ninth", 10 => "Tenth", _ => $"{number}th" })} Colony";

    /// <summary>How many people a founder of this level can keep under them.</summary>
    public static int MostMembers(int level) => Math.Clamp(2 + level / 3, 2, 12);

    /// <summary>How long the people of a hall take over one round of work.</summary>
    public const double JobSeconds = 120;
    public const int MostInChest = 60, MostInTill = 5000, CaughtAt = 12;
    /// <summary>How far apart two halls have to stand.</summary>
    public const double Apart = 60;

    static readonly string[] Game = ["venison", "venison", "wolf_pelt", "deer_hide", "rabbit_pelt", "game_bird"];
    static readonly string[] Shelves = ["mana_tonic", "healing_draught", "stamina_brew"];
    static readonly string[] Settled = ["herring", "mackerel", "pine_log", "stone_block", "venison"];

    /// <summary>What a fight club's champion has in them: how much they can take, and how hard they hit, against a hero of this level.</summary>
    public static (double Health, double Damage) Champion(Hall h, int heroLevel)
    {
        var level = Math.Max(1, heroLevel - 1 + h.Prowess * 0.4);
        return (140 + 22 * level, 8 + 1.6 * level);
    }

    /// <summary>What a fight in the pit pays its winner: more the better the club's champion.</summary>
    public static int PitPurse(Hall h) => 40 + (int)(h.Prowess * 12);

    /// <summary>How much a monastery's training teaches, by how good its teaching has got.</summary>
    public static int Lesson(Hall h, int heroLevel) => (int)Math.Round((30 + 12 * heroLevel) * (1 + h.Prowess * 0.1));

    /// <summary>A round of work at a hall, for every member: what it earns, what it brings in, and what goes wrong. The news it makes, if any.</summary>
    public static List<string> Tick(Hall h, double seconds, int day, Rng rng)
    {
        var news = new List<string>();
        h.Work += seconds;
        while (h.Work >= JobSeconds)
        {
            h.Work -= JobSeconds;
            if (h.Members <= 0) { h.Work = 0; break; }
            var n = h.Members;
            switch (h.Kind)
            {
                case HallKind.ThievesGuild when day < h.LowUntil:
                    h.Heat = Math.Max(0, h.Heat - 2); // lying low: no takings, and the Watch forgets
                    break;
                case HallKind.ThievesGuild:
                    h.Till += 6 * n;
                    h.Heat += n;
                    if (h.Heat > CaughtAt && rng.Chance(Math.Min(0.5, (h.Heat - CaughtAt) * 0.04)))
                    {
                        h.Members--;
                        h.Heat = Math.Max(0, h.Heat - 6);
                        news.Add($"The Watch took one of {h.Name}'s thieves with a hand in a purse. {h.Members} left.");
                    }
                    break;
                case HallKind.HuntingLodge:
                    for (var i = 0; i < n; i++) Put(h, Game[rng.Next(Game.Length)]);
                    h.Till += 2 * n;
                    break;
                case HallKind.FightClub:
                    h.Till += 4 * n; // the gate on bout night
                    h.Prowess = Math.Min(10, h.Prowess + 0.05 * n);
                    break;
                case HallKind.Monastery:
                    h.Till += 2 * n; // alms, and the lessons the village pays for
                    h.Prowess = Math.Min(10, h.Prowess + 0.08 * n);
                    break;
                case HallKind.Chapel:
                    h.Till += 3 * n; // the tithes
                    break;
                case HallKind.Colony:
                    for (var i = 0; i < n; i++) Put(h, Settled[rng.Next(Settled.Length)]);
                    h.Till = Math.Max(0, h.Till + 3 * n - ColonyDefence.Wage * h.Guards); // the guards are paid first
                    break;
                case HallKind.Spire:
                    for (var i = 0; i < (n + 1) / 2; i++) Put(h, Shelves[rng.Next(Shelves.Length)]);
                    h.Till += 2 * n;
                    break;
                default:
                    h.Till += 5 * n; // the house, most nights
                    break;
            }
            h.Till = Math.Min(MostInTill, h.Till);
        }
        return news;
    }

    static void Put(Hall h, string id)
    {
        if (h.Chest.Values.Sum() >= MostInChest) return;
        h.Chest[id] = h.Chest.GetValueOrDefault(id) + 1;
    }
}

/// <summary>
/// Who keeps a colony safe. Until it has a barracks, nobody but its own settlers: they put down their tools and go at
/// anything that comes in, with whatever's to hand, and more settlers are more of a crowd. With a barracks it can hire
/// town guards, paid out of its till, and then the settlers leave it to them (and only take up arms again if every guard
/// is down). The guards are what man the wall that comes after.
/// </summary>
public static class ColonyDefence
{
    /// <summary>What a town guard costs to take on, and what each one takes from the till every round of work.</summary>
    public const int GuardCost = 150, Wage = 2;
    /// <summary>How far past its fence a colony's people go after something, and how close a settler closes to strike.</summary>
    public const double Range = 14, Reach = 3.5;
    /// <summary>Seconds between one settler's blows; how long the barracks takes to train up a fallen guard's replacement.</summary>
    public const double Every = 2.4, Replace = 150;

    /// <summary>How many guards a colony can keep: more the more people there are to protect.</summary>
    public static int MostGuards(Hall h) => Math.Clamp(1 + h.Members / 2, 1, 6);

    /// <summary>What a settler's blow takes off a foe (a share of its health): a crowd of them can see off a wolf or a bandit, not a deviant.</summary>
    public static double Blow(bool close, bool deviant) => deviant ? (close ? 0.02 : 0.008) : close ? 0.06 : 0.025;

    /// <summary>Whether the settlers fight: always before the barracks, and after it only if no guard's standing.</summary>
    public static bool SettlersFight(bool barracks, int guardsStanding) => !barracks || guardsStanding == 0;
}

public sealed partial class CityEconomy
{
    /// <summary>The halls heroes have founded, by owner and kind. They run on the store clock, whoever's online.</summary>
    public Dictionary<string, Hall> Halls { get; set; } = [];

    public Hall? HallOf(string owner, HallKind kind) => Halls.GetValueOrDefault(Gameplay.Halls.KeyOf(owner, kind));

    /// <summary>Every colony a hero has founded, the first first.</summary>
    public IEnumerable<Hall> ColoniesOf(string owner) => Halls.Values.Where(h => h.Kind == HallKind.Colony && h.Owner == owner).OrderBy(h => Math.Max(1, h.Number));

    /// <summary>The number the hero's next colony takes: 0 for their first, then one past the highest they have.</summary>
    public int NextColony(string owner) => ColoniesOf(owner).Select(h => Math.Max(1, h.Number)).DefaultIfEmpty(0).Max() is var n && n == 0 ? 0 : n + 1;

    /// <summary>Time passes at every hall: its people work, and now and then it's news.</summary>
    public void TendHalls(double seconds)
    {
        var rng = new Rng(Seed ^ (ulong)Day * 31 ^ (ulong)Halls.Count);
        foreach (var h in Halls.Values) News.AddRange(Gameplay.Halls.Tick(h, seconds, Day, rng));
    }

    /// <summary>Whether a new hall may stand here: clear of the others.</summary>
    public bool HallRoom(double x, double z) => Halls.Values.All(h => Math.Sqrt((h.X - x) * (h.X - x) + (h.Z - z) * (h.Z - z)) >= Gameplay.Halls.Apart);

    bool HallAct(CityAction a)
    {
        if (a.Contract is not { } key || (a.Kind != "hall_found" && !Halls.ContainsKey(key))) return false;
        var crowns = Math.Clamp(a.Crowns, 0, 5000);
        switch (a.Kind)
        {
            case "hall_found" when a.Hall is { Owner.Length: > 0 and <= 60, Name.Length: > 0 and <= 80 } h && h.Key == key && !Halls.ContainsKey(key) && HallRoom(h.X, h.Z):
                Halls[key] = new Hall { Owner = h.Owner, Hero = h.Hero.Length <= 60 ? h.Hero : "", Kind = h.Kind, Name = h.Name, X = h.X, Z = h.Z, Members = 1, Founded = Day, Number = h.Kind == HallKind.Colony ? Math.Clamp(h.Number, 0, 99) : 0 };
                if (h.Kind == HallKind.Colony) // its founder sits on its council from the first day
                {
                    var council = CouncilOf(Halls[key].Town);
                    council.Name = h.Name;
                    if (!council.Heroes.Contains(h.Owner)) council.Heroes.Add(h.Owner);
                }
                News.Add($"{h.Hero} has founded {h.Name}.");
                return true;
            case "hall_recruit" when Halls[key] is var r && r.Members < Gameplay.Halls.MostMembers(Math.Clamp(a.N, 1, 60)): r.Members++; return true;
            case "hall_till" when Halls[key] is var t && t.Till >= crowns: t.Till -= crowns; return true;
            case "hall_pay": Halls[key].Till = Math.Min(Gameplay.Halls.MostInTill, Halls[key].Till + crowns); return true;
            case "hall_take" when a.Item is { } id && Halls[key].Chest.GetValueOrDefault(id) > 0:
                if (--Halls[key].Chest[id] <= 0) Halls[key].Chest.Remove(id);
                Halls[key].Till = Math.Min(Gameplay.Halls.MostInTill, Halls[key].Till + crowns);
                return true;
            case "hall_low": Halls[key].LowUntil = Day + 1; return true;
            case "hall_guard" when Halls[key] is { Kind: HallKind.Colony } g && GrownOf(g.Town) >= Settling.Barracks && g.Guards < ColonyDefence.MostGuards(g):
                g.Guards++;
                return true;
            case "hall_served" when a.Item is { Length: > 0 and <= 60 } who: Halls[key].Served[who] = Day; return true;
            case "hall_close":
                if (Halls[key].Kind == HallKind.Colony) Councils.Remove(Halls[key].Town);
                return Halls.Remove(key);
        }
        return false;
    }
}
