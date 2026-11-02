using System.Text.Json.Serialization;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What a town raids another for: its stores, a tribute that forces trade its way, or the place itself.</summary>
public enum RaidAim { Supplies, Trade, Land }

/// <summary>
/// The people who run a place, for everyone on the server: its members, who's fallen, the heroes who've won a seat,
/// what the other towns make of it, what's in its purse, and what it holds over its neighbours.
/// </summary>
public sealed class TownCouncil
{
    public string Name { get; set; } = "";
    /// <summary>Its own members, by id: names, for the news.</summary>
    public Dictionary<string, string> Members { get; set; } = [];
    public HashSet<string> Fallen { get; set; } = [];
    /// <summary>The companies that have won a seat.</summary>
    public List<string> Heroes { get; set; } = [];
    /// <summary>What the other towns make of this one, from -5 to 5. Raiding costs it; quiet weeks mend it.</summary>
    public double Standing { get; set; }
    public int Purse { get; set; }
    /// <summary>The places it's taken: they pay it a share of what they earn.</summary>
    public HashSet<string> Holds { get; set; } = [];
    /// <summary>The towns forced to trade its way, and the last day each one pays.</summary>
    public Dictionary<string, int> Tribute { get; set; } = [];
    public int LastRaid { get; set; } = -99;
    public int LastBanded { get; set; } = -99;

    [JsonIgnore] public IEnumerable<string> Living => Members.Where(m => !Fallen.Contains(m.Key)).Select(m => m.Value);

    /// <summary>The hero who rules here: the only one left at the table.</summary>
    [JsonIgnore] public string? Ruler => Heroes.Count == 1 && Members.Count > 0 && Members.Keys.All(Fallen.Contains) ? Heroes[0] : null;
}

public sealed partial class CityEconomy
{
    public const string Capital = "city:tristinwold";
    /// <summary>What a town has to think of a hero to hear them out, and what they have to have done there to be given a seat.</summary>
    public const double SpeakAt = 2, SeatAt = 3;
    /// <summary>The chance, each carpenters' week, that one town raids another on its own: they're mostly busy with raiders from outside.</summary>
    public const double TownRaidChance = 0.1;
    /// <summary>What a raid costs a town's name, how low a name draws the others down on it, and how much a quiet week mends.</summary>
    public const double RaidShame = 1.5, BadName = -3, QuietWeek = 0.1;
    public const double RaidKillsMember = 0.25, BandKillsMember = 0.5;
    public const int RaidEvery = 5, BandEvery = 30, TributeDays = 30, Bandmates = 3, BandBurns = 2;

    public Dictionary<string, TownCouncil> Councils { get; set; } = [];

    public TownCouncil CouncilOf(string town) => Councils.TryGetValue(town, out var c) ? c : Councils[town] = new TownCouncil();

    string TownName(string town) => Councils.TryGetValue(town, out var c) && c.Name.Length > 0 ? c.Name : Roads.Towns.TryGetValue(town, out var m) ? m.Name : PlaceName(town);

    /// <summary>A place's council, the first time anyone comes to it: members as "id|name".</summary>
    bool Meet(string town, string? name, IEnumerable<string> members)
    {
        var c = CouncilOf(town);
        if (c.Members.Count > 0) return false;
        if (name is { Length: > 0 and <= 60 }) c.Name = name;
        foreach (var m in members.Select(m => m.Split('|', 2)).Where(m => m.Length == 2)) c.Members[m[0]] = m[1];
        return c.Members.Count > 0;
    }

    /// <summary>A company takes a seat at a place's table.</summary>
    public bool SeatHero(string town, string hero)
    {
        var c = CouncilOf(town);
        if (c.Heroes.Contains(hero)) return false;
        c.Heroes.Add(hero);
        News.Add($"{hero} has taken a seat on the council of {TownName(town)}.");
        return true;
    }

    /// <summary>A member of a council is dead. If that leaves one hero at the table, they rule.</summary>
    public bool MemberFalls(string town, string id)
    {
        var c = CouncilOf(town);
        if (!c.Members.TryGetValue(id, out var name) || !c.Fallen.Add(id)) return false;
        News.Add($"{name}, of the council of {TownName(town)}, is dead.");
        if (c.Ruler is { } ruler) News.Add($"{ruler} is the last of the council of {TownName(town)} left standing, and rules it now.");
        return true;
    }

    void FallsAtRandom(string town, Rng rng, double chance)
    {
        if (!rng.Chance(chance)) return;
        var living = Councils.GetValueOrDefault(town)?.Members.Keys.Where(k => !CouncilOf(town).Fallen.Contains(k)).Order(StringComparer.Ordinal).ToList();
        if (living is { Count: > 0 }) MemberFalls(town, rng.Pick(living));
    }

    double TownSize(string town) => town == Capital ? 1 : Roads.Towns.TryGetValue(town, out var m) ? m.Size : 0.15;

    /// <summary>How much a town can put in the field: its size, and how well it's doing.</summary>
    double Strength(string town) => TownSize(town) * (1.5 + Prosperity(town));

    /// <summary>Its stores carried off. What's taken goes to the raiders' stores, as much as they can carry.</summary>
    void Sack(string from, string on)
    {
        var loot = new Dictionary<Good, double>();
        if (on == Capital)
            foreach (var g in Stock.Keys.ToList()) { loot[g] = Stock[g] * 0.25; Stock[g] -= loot[g]; }
        else if (Roads.Towns.TryGetValue(on, out var m))
        {
            m.Catch(Day);
            foreach (var g in m.Stock.Keys.ToList()) { loot[g] = m.Stock[g] * 0.45; m.Stock[g] -= loot[g]; }
            m.Raided++;
        }
        foreach (var (g, n) in loot)
            if (from == Capital) Stock[g] = Stock.GetValueOrDefault(g) + n / 2;
            else if (Roads.Towns.TryGetValue(from, out var theirs)) theirs.Stock[g] = theirs.Stock.GetValueOrDefault(g) + n / 2;
    }

    /// <summary>Whether a town can send out raiders yet.</summary>
    public bool MayRaid(string town) => Day - CouncilOf(town).LastRaid >= RaidEvery;

    /// <summary>One town raids another, and what came of it, for the news. Null if it can't go.</summary>
    public string? Raid(string from, string on, RaidAim aim, Rng rng, string? by = null)
    {
        if (from == on || !MayRaid(from) || TownSize(on) <= 0) return null;
        var (raiders, target) = (CouncilOf(from), CouncilOf(on));
        raiders.LastRaid = Day;
        raiders.Standing = Math.Max(-5, raiders.Standing - RaidShame);
        var (us, them) = (TownName(from), TownName(on));
        var led = by is null ? us : $"{by}, of {us},";
        if (Strength(from) * (0.7 + rng.NextDouble() * 0.6) < Strength(on) * (0.7 + rng.NextDouble() * 0.6))
        {
            FallsAtRandom(from, rng, RaidKillsMember / 2);
            return $"{led} sent raiders against {them} and they were beaten back.";
        }
        FallsAtRandom(on, rng, RaidKillsMember);
        switch (aim)
        {
            case RaidAim.Trade:
                raiders.Tribute[on] = Day + TributeDays;
                return $"{led} raided {them} and forced a tribute out of it: {them} trades on {us}'s terms now.";
            case RaidAim.Land:
                foreach (var c in Councils.Values) c.Holds.Remove(on);
                raiders.Holds.Add(on);
                return $"{led} raided {them} and took it: {them} answers to {us} now.";
            default:
                Sack(from, on);
                raiders.Purse += target.Purse / 2;
                target.Purse -= target.Purse / 2;
                return $"{led} raided {them} and carried off its stores.";
        }
    }

    /// <summary>The towns near a place, nearest first.</summary>
    public IEnumerable<string> Neighbours(string town)
    {
        var (x, z) = Roads.Towns.TryGetValue(town, out var m) ? (m.X, m.Z) : (0, 0);
        var others = Roads.Towns.Values.Where(o => o.Id != town).Select(o => (o.Id, D: o.DistanceTo(x, z)));
        if (town != Capital) others = others.Append((Capital, Math.Sqrt(x * x + z * z)));
        return others.OrderBy(o => o.D).ThenBy(o => o.Id, StringComparer.Ordinal).Select(o => o.Id);
    }

    /// <summary>A town with a bad name, and its neighbours have had enough: they band together and come for it.</summary>
    string? Bands(Rng rng)
    {
        foreach (var (town, c) in Councils.Where(c => c.Value.Standing <= BadName && Day - c.Value.LastBanded >= BandEvery).OrderBy(c => c.Key, StringComparer.Ordinal).ToList())
        {
            var band = Neighbours(town).Where(n => CouncilOf(n).Ruler is null).Take(Bandmates).ToList();
            if (band.Count < 2) continue;
            c.LastBanded = Day;
            c.Standing = Math.Min(5, c.Standing + 1);
            Sack(band[0], town);
            FabricOf(town).Burnt += BandBurns;
            FallsAtRandom(town, rng, BandKillsMember);
            return $"{string.Join(", ", band.Select(TownName))} have banded together against {TownName(town)}, and come down on it for what it's done.";
        }
        return null;
    }

    /// <summary>Every town's week: what each earns and owes, a run-down place getting worse, a neighbour with a grudge, or a bad name called in.</summary>
    IEnumerable<string> TownsTurn(Rng rng)
    {
        var told = new List<string>();
        foreach (var town in Roads.Towns.Keys.Append(Capital).Order(StringComparer.Ordinal).ToList())
        {
            var c = CouncilOf(town);
            var earned = (int)(100 * TownSize(town) * Math.Max(0, 1 + Prosperity(town)));
            if (Councils.Values.FirstOrDefault(o => o.Holds.Contains(town)) is { } lord) { lord.Purse += earned / 4; earned -= earned / 4; }
            c.Purse += earned;
            c.Standing += Math.Sign(-c.Standing) * Math.Min(QuietWeek, Math.Abs(c.Standing));
            if (Prosperity(town) <= Fails && FabricOf(town).Derelict < DerelictCap)
            {
                FabricOf(town).Derelict++;
                told.Add($"Times are hard in {TownName(town)}: another home's been left to the mould.");
            }
        }
        foreach (var (over, c) in Councils.OrderBy(c => c.Key, StringComparer.Ordinal))
            foreach (var (under, last) in c.Tribute.OrderBy(t => t.Key, StringComparer.Ordinal).ToList())
            {
                if (Day > last) { c.Tribute.Remove(under); continue; }
                var due = CouncilOf(under).Purse / 5;
                CouncilOf(under).Purse -= due;
                c.Purse += due;
            }
        if (Bands(rng) is { } banded) told.Add(banded);
        if (rng.Chance(TownRaidChance))
        {
            var towns = Roads.Towns.Keys.Where(t => CouncilOf(t).Heroes.Count == 0 && MayRaid(t)).Order(StringComparer.Ordinal).ToList();
            if (towns.Count > 0 && rng.Pick(towns) is var from && Neighbours(from).FirstOrDefault() is { } on
                && Raid(from, on, rng.Pick(Enum.GetValues<RaidAim>()), rng) is { } raid) told.Add(raid);
        }
        return told;
    }
}
