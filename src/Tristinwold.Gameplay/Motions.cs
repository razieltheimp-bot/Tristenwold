using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What a seated hero can put to their council: a raid, the purse shared out or paid to one of them, a place it holds set free, the purse spent on the town, or a hero voted off.</summary>
public enum MotionKind { Raid, SharePurse, PayPurse, Free, Spend, Unseat }

/// <summary>
/// A motion before a council: who put it, what it's for, how long the vote stays open, and how everyone at the
/// table voted. Every seated hero gets a vote, and so does every councillor still living; the councillors vote the
/// moment it's put, the heroes when they come to the table.
/// </summary>
public sealed class Motion
{
    public int Id { get; set; }
    public MotionKind Kind { get; set; }
    public string By { get; set; } = "";
    /// <summary>The town to raid, the hero to pay or vote off, or the place to set free.</summary>
    public string Target { get; set; } = "";
    public RaidAim Aim { get; set; }
    public double SecondsLeft { get; set; }
    /// <summary>The heroes who've voted, and which way.</summary>
    public Dictionary<string, bool> Votes { get; set; } = [];
    /// <summary>What the town thinks of each hero who voted (from -5 to 5): the one it thinks best of breaks a tie.</summary>
    public Dictionary<string, double> Thought { get; set; } = [];
    public int CouncilYes { get; set; }
    public int CouncilNo { get; set; }
}

public sealed partial class CityEconomy
{
    /// <summary>How long a vote stays open: a day, ten minutes. It closes sooner once every hero at the table has voted.</summary>
    public const double MotionSeconds = DaySeconds;
    /// <summary>The most motions a council has before it at once, and the share of the votes it takes to put a hero off the table.</summary>
    public const int MostMotions = 4;
    public const double UnseatShare = 2.0 / 3;
    /// <summary>What it costs to mend a home or raise a new one when a council spends its purse on the town.</summary>
    public const int BuildCost = 80;

    /// <summary>Whether a hero can put motions to a council: seated there, with no deviant ruling it and nobody ruling it alone.</summary>
    public bool MayPropose(string town, string hero)
    {
        var c = CouncilOf(town);
        return c.Heroes.Contains(hero) && c.Ruler is null && c.Warlord is not { Rules: true };
    }

    /// <summary>A hero puts a motion to their council. The councillors vote on it at once; the heroes have a day.</summary>
    public bool Propose(string town, string by, MotionKind kind, string target, RaidAim aim, double thought)
    {
        var c = CouncilOf(town);
        if (!MayPropose(town, by) || c.Motions.Count >= MostMotions || c.Motions.Any(m => m.By == by && m.Kind == kind && m.Target == target)) return false;
        var fits = kind switch
        {
            MotionKind.Raid => target.Length is > 0 and <= 80 && target != town && TownSize(target) > 0,
            MotionKind.SharePurse or MotionKind.Spend => c.Purse > 0,
            MotionKind.PayPurse => c.Purse > 0 && c.Heroes.Contains(target),
            MotionKind.Free => c.Holds.Contains(target),
            MotionKind.Unseat => c.Heroes.Contains(target) && target != by,
            _ => false,
        };
        if (!fits) return false;
        var m = new Motion { Id = ++c.LastMotion, Kind = kind, By = by, Target = target, Aim = aim, SecondsLeft = MotionSeconds };
        m.Votes[by] = true;
        m.Thought[by] = Math.Clamp(thought, -5, 5);
        // The councillors: never for emptying the town's purse into a hero's pocket, always for spending it on the town,
        // and otherwise as much as they think of whoever put it.
        var rng = new Rng(Seed ^ Hash(town) ^ (ulong)m.Id * 0x9E3779B97F4A7C15UL);
        foreach (var _ in c.Members.Keys.Where(k => !c.Fallen.Contains(k)).Order(StringComparer.Ordinal))
        {
            var yes = kind switch
            {
                MotionKind.SharePurse or MotionKind.PayPurse => false,
                MotionKind.Spend => true,
                _ => rng.Chance(Math.Clamp(m.Thought[by] / 5, 0.15, 0.9)),
            };
            if (yes) m.CouncilYes++; else m.CouncilNo++;
        }
        c.Motions.Add(m);
        News.Add($"{by} has put it to the council of {TownName(town)}: {Said(m)}.");
        if (AllVoted(c, m)) Close(town, c, m);
        return true;
    }

    /// <summary>A hero at the table votes on a motion (and can change their mind while it's open).</summary>
    public bool Vote(string town, string hero, int id, bool yes, double thought)
    {
        var c = CouncilOf(town);
        if (!c.Heroes.Contains(hero) || c.Motions.FirstOrDefault(m => m.Id == id) is not { } m || m.Kind == MotionKind.Unseat && m.Target == hero) return false;
        m.Votes[hero] = yes;
        m.Thought[hero] = Math.Clamp(thought, -5, 5);
        if (AllVoted(c, m)) Close(town, c, m);
        return true;
    }

    /// <summary>A hero takes the crowns the council voted them.</summary>
    public bool Collect(string town, string hero) => CouncilOf(town).Owed.Remove(hero);

    /// <summary>The votes still open, run down by the seconds gone by; those whose time is up are counted.</summary>
    public bool TendMotions(double seconds)
    {
        var changed = false;
        foreach (var (town, c) in Councils.Where(c => c.Value.Motions.Count > 0).OrderBy(c => c.Key, StringComparer.Ordinal).ToList())
            foreach (var m in c.Motions.ToList())
            {
                m.SecondsLeft -= seconds;
                if (m.SecondsLeft > 0) continue;
                Close(town, c, m);
                changed = true;
            }
        return changed;
    }

    static IEnumerable<string> Voters(TownCouncil c, Motion m) => c.Heroes.Where(h => !(m.Kind == MotionKind.Unseat && m.Target == h));

    static bool AllVoted(TownCouncil c, Motion m) => Voters(c, m).All(m.Votes.ContainsKey);

    /// <summary>Whether a motion carries: more for it than against (two in three, to put a hero off the table), and a tie goes the way of the hero the town thinks best of.</summary>
    public static bool Carries(TownCouncil c, Motion m)
    {
        var votes = m.Votes.Where(v => Voters(c, m).Contains(v.Key)).ToList();
        var (yes, no) = (votes.Count(v => v.Value) + m.CouncilYes, votes.Count(v => !v.Value) + m.CouncilNo);
        if (yes == 0) return false;
        if (m.Kind == MotionKind.Unseat) return yes >= UnseatShare * (yes + no) - 1e-9;
        if (yes != no) return yes > no;
        var best = votes.OrderByDescending(v => m.Thought.GetValueOrDefault(v.Key)).ToList();
        // Two thought as well of, on opposite sides: it stays as it is.
        if (best.Count == 0 || best.Count > 1 && m.Thought.GetValueOrDefault(best[1].Key) == m.Thought.GetValueOrDefault(best[0].Key) && best[1].Value != best[0].Value) return false;
        return best[0].Value;
    }

    /// <summary>The vote counted, and what was carried done.</summary>
    void Close(string town, TownCouncil c, Motion m)
    {
        c.Motions.Remove(m);
        var votes = m.Votes.Where(v => Voters(c, m).Contains(v.Key)).ToList();
        var (yes, no) = (votes.Count(v => v.Value) + m.CouncilYes, votes.Count(v => !v.Value) + m.CouncilNo);
        var name = TownName(town);
        if (!Carries(c, m)) { News.Add($"The council of {name} voted down {m.By}'s motion to {Said(m)} ({yes} for, {no} against)."); return; }
        var done = Enact(town, c, m);
        News.Add($"The council of {name} carried {m.By}'s motion to {Said(m)} ({yes} for, {no} against){(done is null ? ", but there was nothing left to do it with" : "")}.");
        if (done is { Length: > 0 }) News.Add(done);
    }

    /// <summary>What a carried motion does. Null if it can't be done now (the purse spent, the raiders still out).</summary>
    string? Enact(string town, TownCouncil c, Motion m)
    {
        switch (m.Kind)
        {
            case MotionKind.Raid:
                return MayRaid(town) ? Raid(town, m.Target, m.Aim, new Rng(Seed ^ Hash(town) ^ (ulong)m.Id * 7919), m.By) : null;
            case MotionKind.SharePurse when c.Purse > 0 && c.Heroes.Count > 0:
                var each = c.Purse / c.Heroes.Count;
                if (each <= 0) return null;
                foreach (var h in c.Heroes) c.Owed[h] = c.Owed.GetValueOrDefault(h) + each;
                c.Purse -= each * c.Heroes.Count;
                return "";
            case MotionKind.PayPurse when c.Purse > 0 && c.Heroes.Contains(m.Target):
                c.Owed[m.Target] = c.Owed.GetValueOrDefault(m.Target) + c.Purse;
                c.Purse = 0;
                return "";
            case MotionKind.Free when c.Holds.Remove(m.Target):
                return $"{TownName(m.Target)} answers to nobody now.";
            case MotionKind.Spend when c.Purse > 0:
                SpendOn(town, c.Purse);
                c.Purse = 0;
                return "";
            case MotionKind.Unseat when c.Heroes.Remove(m.Target):
                c.Motions.RemoveAll(o => o.By == m.Target);
                foreach (var o in c.Motions) { o.Votes.Remove(m.Target); o.Thought.Remove(m.Target); }
                return c.Ruler is { } ruler ? $"{ruler} is the last of the council of {TownName(town)} left at the table, and rules it now." : "";
            default:
                return null;
        }
    }

    /// <summary>A council's purse spent on its own town: burned homes rebuilt, empty ones mended, new ones raised, and what's left filling its stores.</summary>
    void SpendOn(string town, int crowns)
    {
        var f = FabricOf(town);
        for (; crowns >= BuildCost; crowns -= BuildCost)
            if (f.Burnt > 0) f.Burnt--;
            else if (f.Derelict > 0) f.Derelict--;
            else if (f.Grown < GrowthCap) f.Grown++;
            else break;
        if (crowns <= 0) return;
        if (town == Capital) { Treasury += crowns; return; }
        if (!Roads.Towns.TryGetValue(town, out var market)) return;
        var share = Math.Min(1, crowns / 400.0);
        foreach (var g in Enum.GetValues<Good>())
            if (market.Stock.GetValueOrDefault(g) is var now && now < market.Usual(g)) market.Stock[g] = now + (market.Usual(g) - now) * share;
    }

    /// <summary>A motion in a few words, as the news and the table put it.</summary>
    public string Said(Motion m) => m.Kind switch
    {
        MotionKind.Raid => $"raid {TownName(m.Target)} for {(m.Aim switch { RaidAim.Trade => "a tribute", RaidAim.Land => "the place itself", _ => "its stores" })}",
        MotionKind.SharePurse => "share the purse among the heroes at the table",
        MotionKind.PayPurse => $"pay the purse to {m.Target}",
        MotionKind.Free => $"set {TownName(m.Target)} free",
        MotionKind.Spend => "spend the purse on the town",
        _ => $"vote {m.Target} off the council",
    };

    static ulong Hash(string s)
    {
        var h = 14695981039346656037UL;
        foreach (var ch in s) h = (h ^ ch) * 1099511628211UL;
        return h;
    }
}
