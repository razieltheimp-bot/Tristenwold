using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>A named deviant who might sit at a town's table: who, of what kind, how strong, and the heroes it has a score to settle with.</summary>
public sealed record Contender(string Id, string Name, string Kind, int Level, Rank Rank, IReadOnlyList<string> Grudges);

/// <summary>
/// A deviant at a council's table: knifing its way to the head of it, then ruling the place, and sending its raiders
/// out. One that came for a hero (<see cref="Against"/>) took a town near one they rule, and every raid it sends is at them.
/// </summary>
public sealed class Warlord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "bandit";
    public int Level { get; set; } = 1;
    /// <summary>The hero it came to bring down, if it came for one.</summary>
    public string? Against { get; set; }
    public int Since { get; set; }
    /// <summary>The rest of the council is dead, and it rules.</summary>
    public bool Rules { get; set; }
    /// <summary>The heroes it threw off the councils of the towns it took, by town: their seats are theirs again if it falls.</summary>
    public Dictionary<string, string> Ousted { get; set; } = [];
}

/// <summary>
/// Which deviants a council will let in at the door, and how fast they get to the head of the table. Only the people
/// kinds sit: thugs, cutthroats, bandits, pirates, raiders, goblins. Turlin and Soren have ends of their own, and
/// never do.
/// </summary>
public static class Warlords
{
    public static readonly HashSet<string> Humanoid =
    [
        "dock_thug", "velvet_assassin", "lantern_boy", "drowned_hand", "bandit", "bandit_chief", "pirate", "pirate_captain",
        "monkey_hand", "raider", "raider_chief", "goblin", "goblin_tinker", "goblin_king",
    ];

    public static bool MaySit(string kind) => Humanoid.Contains(kind);

    /// <summary>The chance each day that a hero who rules a town draws a rival into a town near it.</summary>
    public const double RivalComes = 0.35;
    /// <summary>The chance each day that some ambitious deviant takes a seat somewhere, and how many of those there can be at once.</summary>
    public const double Ambition = 0.05;
    public const int MostAmbitious = 2;
    /// <summary>The chance each day that one of the council dies of it: a rival is in a hurry, the rest take their time.</summary>
    public const double RivalKnife = 0.6, SlowKnife = 0.25;
    /// <summary>The chance each raiding week that an ambitious one sends its raiders out on its own account.</summary>
    public const double WarlordRaids = 0.5;

    /// <summary>A deviant on the shared world's roster, as a contender: every hero it has fought, won or lost, is a grudge.</summary>
    public static Contender From(Tristinwold.Deviant.Deviant d) =>
        new(d.Id.ToString(), d.FullName, d.Kind, d.Level, d.Rank, [.. d.Players.Where(p => Grudge(p.Value)).Select(p => p.Key).Order(StringComparer.Ordinal)]);

    static bool Grudge(PlayerRecord r) =>
        r.TimesKilledThem > 0 || r.TimesTheyBeatMe > 0 || r.TimesTheyFled > 0
        || (r.Memory & (Memory.KilledPlayer | Memory.PlayerFled | Memory.FledFromPlayer | Memory.Returned | Memory.Vengeful)) != 0;
}

public sealed partial class CityEconomy
{
    /// <summary>The last day the deviants at the councils were seen to.</summary>
    public int WarlordDay { get; set; } = -1;
    /// <summary>The deviants no council will have again: struck down at the head of a table.</summary>
    public HashSet<string> Unseated { get; set; } = [];

    /// <summary>Every town with a deviant at its table.</summary>
    public IEnumerable<(string Town, Warlord Lord)> WarlordTowns =>
        Councils.Where(c => c.Value.Warlord is not null).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => (c.Key, c.Value.Warlord!));

    /// <summary>The town a hero's rival sits in, come there for them.</summary>
    public (string Town, Warlord Lord)? RivalOf(string hero) => WarlordTowns.Where(w => w.Lord.Against == hero).Select(w => ((string, Warlord)?)w).FirstOrDefault();

    /// <summary>Somewhere a deviant can get a seat: a town, not Tristinwold or one of Korrin's, with no hero and no other deviant at its table.</summary>
    bool OpenTable(string town) =>
        town != Capital && Roads.Towns.ContainsKey(town) && !KorrinTowns.Contains(town)
        && CouncilOf(town) is { Heroes.Count: 0, Warlord: null } && Councils.Values.All(c => c.Ruler is null || !c.Holds.Contains(town));

    void Sit(string town, Contender who, string? against)
    {
        CouncilOf(town).Warlord = new Warlord { Id = who.Id, Name = who.Name, Kind = who.Kind, Level = who.Level, Against = against, Since = Day };
        News.Add(against is null
            ? $"{who.Name} has talked, bribed and threatened its way onto the council of {TownName(town)}."
            : $"{who.Name} has taken a seat on the council of {TownName(town)}, a day's ride from where {against} rules, and says so to anyone who'll listen.");
    }

    /// <summary>
    /// Once a day: deviants that are gone from the roster leave their tables; a hero who rules a town may draw a rival
    /// with a grudge into a town near them; now and then an ambitious one takes a seat on its own account; and each one
    /// at a table gets a councillor killed, or, at the head of it, sends its raiders out. True if anything changed.
    /// </summary>
    public bool TendWarlords(IEnumerable<Contender>? contenders)
    {
        if (contenders is null || WarlordDay == Day) return false;
        WarlordDay = Day;
        var rng = new Rng(Seed ^ (ulong)(Day + 1) * 104729);
        var living = contenders.Where(c => Warlords.MaySit(c.Kind) && !Unseated.Contains(c.Id)).GroupBy(c => c.Id).Select(g => g.First()).ToDictionary(c => c.Id);
        var changed = false;

        foreach (var (town, lord) in WarlordTowns.ToList())
        {
            if (living.TryGetValue(lord.Id, out var now)) { (lord.Name, lord.Level) = (now.Name, Math.Max(lord.Level, now.Level)); continue; }
            changed |= WarlordFell(town, null);
        }

        bool Seated(string id) => WarlordTowns.Any(w => w.Lord.Id == id);
        var rulers = Councils.Where(c => c.Value.Ruler is not null).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => (Home: c.Key, Hero: c.Value.Ruler!)).ToList();
        foreach (var (home, hero) in rulers)
        {
            if (RivalOf(hero) is not null || !rng.Chance(Warlords.RivalComes)) continue;
            // One with a grudge already at a table near them turns its raiders their way; otherwise one comes and takes a seat.
            if (WarlordTowns.FirstOrDefault(w => w.Lord.Against is null && living.TryGetValue(w.Lord.Id, out var c) && c.Grudges.Contains(hero) && Neighbours(w.Town).Take(6).Contains(home)) is { Lord: { } near })
            {
                near.Against = hero;
                News.Add($"{near.Name} has heard who rules {TownName(home)}, and has a score to settle with {hero}.");
                changed = true;
                continue;
            }
            if (living.Values.Where(c => c.Grudges.Contains(hero) && !Seated(c.Id)).OrderByDescending(c => c.Level).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() is not { } rival) continue;
            if (Neighbours(home).Take(6).FirstOrDefault(OpenTable) is not { } town) continue;
            Sit(town, rival, hero);
            changed = true;
        }

        // The ambitious: none with a score to settle with a hero who rules somewhere, who are keeping themselves for that.
        if (WarlordTowns.Count(w => w.Lord.Against is null) < Warlords.MostAmbitious && rng.Chance(Warlords.Ambition)
            && living.Values.Where(c => c.Rank >= Rank.Ringleader && !Seated(c.Id) && !rulers.Any(r => c.Grudges.Contains(r.Hero))).OrderBy(c => c.Id, StringComparer.Ordinal).ToList() is { Count: > 0 } climbers
            && Roads.Towns.Keys.Where(OpenTable).Order(StringComparer.Ordinal).ToList() is { Count: > 0 } open)
        {
            Sit(rng.Pick(open), rng.Pick(climbers), null);
            changed = true;
        }

        foreach (var (town, lord) in WarlordTowns.ToList())
            changed |= lord.Rules ? WarlordRaids(town, lord, rng) : Schemes(town, lord, rng);
        return changed;
    }

    /// <summary>Not at the head of the table yet: now and then another of the council turns up dead, and when the last of them does, it rules.</summary>
    bool Schemes(string town, Warlord lord, Rng rng)
    {
        var c = CouncilOf(town);
        var changed = false;
        if (rng.Chance(lord.Against is null ? Warlords.SlowKnife : Warlords.RivalKnife)
            && c.Members.Keys.Where(k => !c.Fallen.Contains(k)).Order(StringComparer.Ordinal).ToList() is { Count: > 0 } left)
            changed = MemberFalls(town, rng.Pick(left));
        if (c.Members.Keys.Any(k => !c.Fallen.Contains(k))) return changed;
        lord.Rules = true;
        News.Add(lord.Against is { } hero
            ? $"{lord.Name} is the last of the council of {TownName(town)} left alive, and rules it now. Its raiders are for {hero}."
            : $"{lord.Name} is the last of the council of {TownName(town)} left alive, and rules it now.");
        return true;
    }

    /// <summary>
    /// At the head of the table: a rival sends its raiders at the towns its hero rules, to take them; an ambitious one
    /// now and then at a neighbour. A hero on the council of a town it takes is thrown off it.
    /// </summary>
    bool WarlordRaids(string town, Warlord lord, Rng rng)
    {
        if (!MayRaid(town)) return false;
        string? on;
        RaidAim aim;
        if (lord.Against is { } hero)
        {
            var theirs = Councils.Where(c => c.Value.Ruler == hero).Select(c => c.Key).Order(StringComparer.Ordinal).ToList();
            on = Neighbours(town).FirstOrDefault(theirs.Contains);
            aim = RaidAim.Land;
            if (on is null) return false;
        }
        else
        {
            if (!rng.Chance(Warlords.WarlordRaids / CarpentersEvery)) return false;
            on = Neighbours(town).Where(t => t != Capital).Take(3).ToList() is { Count: > 0 } near ? rng.Pick(near) : null;
            aim = rng.Pick(Enum.GetValues<RaidAim>());
            if (on is null) return false;
        }
        if (Raid(town, on, aim, rng, lord.Name) is not { } told) return false;
        News.Add(told);
        if (aim == RaidAim.Land && CouncilOf(town).Holds.Contains(on))
            foreach (var seated in CouncilOf(on).Heroes.ToList())
            {
                CouncilOf(on).Heroes.Remove(seated);
                lord.Ousted[on] = seated;
                News.Add($"{lord.Name} has thrown {seated} off the council of {TownName(on)}.");
            }
        return true;
    }

    /// <summary>
    /// A deviant at a council's table is dead, by a hero's hand (<paramref name="by"/>) or some other way. Its town is
    /// free, and so is every town it took; whoever it threw off a council has their seat back; and the hero who killed it
    /// has a seat at its table, the head of it if the rest of the council is dead.
    /// </summary>
    bool WarlordFell(string town, string? by)
    {
        var c = CouncilOf(town);
        if (c.Warlord is not { } lord) return false;
        Unseated.Add(lord.Id);
        c.Warlord = null;
        if (lord.Rules) c.Emptied = true;
        var freed = c.Holds.Select(TownName).ToList();
        c.Holds.Clear();
        c.Tribute.Clear();
        foreach (var (t, hero) in lord.Ousted)
            if (!CouncilOf(t).Heroes.Contains(hero)) CouncilOf(t).Heroes.Add(hero);
        if (by is { Length: > 0 and <= 60 } && !c.Heroes.Contains(by)) c.Heroes.Add(by);
        var news = by is { Length: > 0 and <= 60 } ? $"{by} has struck down {lord.Name}, who sat at the head of the council of {TownName(town)}." : $"{lord.Name} is dead, and the council of {TownName(town)} is rid of it.";
        if (freed.Count > 0) news += $" {string.Join(", ", freed)} {(freed.Count == 1 ? "is" : "are")} free of it.";
        if (lord.Ousted.Count > 0) news += $" {string.Join(", ", lord.Ousted.Values.Distinct())} {(lord.Ousted.Values.Distinct().Count() == 1 ? "has a seat" : "have their seats")} back.";
        if (by is not null && c.Ruler == by) news += $" With the rest of the council dead, {by} rules {TownName(town)} now.";
        News.Add(news);
        return true;
    }
}
