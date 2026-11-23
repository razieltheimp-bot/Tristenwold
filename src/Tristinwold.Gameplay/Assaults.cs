using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// The strong go for the strong. When no hero alive is worth Ronacse's time, or a deviant has grown past every hero
/// playing, they don't come for anyone on the road: they come down on a town, and stay till somebody breaks them or
/// the town is sacked. Its market shuts and it burns a little more each day. Everyone fighting one wears down the
/// same wounds (<see cref="Assault.Hurt"/>), so heroes who couldn't beat it alone can beat it between them.
/// </summary>
public sealed class Assault
{
    /// <summary>The town ("city:tristinwold", or a known place's id).</summary>
    public string Town { get; set; } = "";
    /// <summary>Who: <see cref="Assaults.Pride"/>, or the deviant's id.</summary>
    public string By { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>What it is, for the game to raise it (a bestiary kind).</summary>
    public string Kind { get; set; } = "";
    public int Level { get; set; }
    public int Rank { get; set; }
    public int Since { get; set; }
    public int Ends { get; set; }
    /// <summary>The last day it burned something.</summary>
    public int Burned { get; set; }
    /// <summary>
    /// The wounds every hero's given it, as a share of its health: 0 to 1, and for Ronacse on to 2 (1 to 2 is his
    /// second form, Pride Unbound).
    /// </summary>
    public double Hurt { get; set; }

    public bool IsPride => By == Assaults.Pride;
    public double MostHurt => IsPride ? 2 : 1;
}

/// <summary>A deviant on the roster, as far as the city needs to know it to send it after a town.</summary>
public sealed record AssaultCandidate(string Id, string Name, string Kind, int Level, int Rank, int Might);

public static class Assaults
{
    public const string Pride = "pride";
    /// <summary>Ronacse only hunts a hero within this many levels of his own; and never one under level 5.</summary>
    public const int PrideReach = 10, PrideLeast = 5;
    /// <summary>How long one holds a town before it's sacked, and the days between one and the next of the same kind.</summary>
    public const int Days = 2, PrideEvery = 3, DeviantEvery = 2;
    /// <summary>Only a deviant of a rank (a ringleader or above) leads its band on a town.</summary>
    public const int DeviantLeastRank = 1;
    /// <summary>The most wounds one hero can claim at once (a bad report can't end it).</summary>
    public const double MostBlow = 0.5;

    /// <summary>Whether Ronacse thinks a hero of this level worth hunting.</summary>
    public static bool PrideHunts(int heroLevel, int prideLevel) => heroLevel >= Math.Max(PrideLeast, prideLevel - PrideReach);
}

public sealed partial class CityEconomy
{
    /// <summary>The towns held by something too strong for any one hero (see <see cref="Assault"/>).</summary>
    public List<Assault> Assaults { get; set; } = [];
    public int PrideAssaultNext { get; set; }
    public int DeviantAssaultNext { get; set; }

    static readonly int OutclassBy = new DeviantRules().OutclassBy;

    public Assault? AssaultOn(string? town) => town is null ? null : Assaults.FirstOrDefault(a => a.Town == town);
    public Assault? PrideAssault => Assaults.FirstOrDefault(a => a.IsPride);

    /// <summary>
    /// The day's turn for the towns: who's sacked one, and who goes for one now. <paramref name="strongest"/> is the
    /// strongest hero playing (0 if nobody is, or no game has said); <paramref name="deviants"/> the roster, on a
    /// server (none in a game on its own). Returns the deviants that sacked a town, so the roster can grow them.
    /// </summary>
    public List<Assault> TendAssaults(int strongest, IReadOnlyList<AssaultCandidate> deviants)
    {
        var sacked = new List<Assault>();
        var living = deviants.Select(d => d.Id).ToHashSet();
        foreach (var a in Assaults.ToList())
        {
            // Killed by someone (the roster's word on it), or Ronacse down: the town's free.
            if (!a.IsPride && !living.Contains(a.By) || a.IsPride && PrideDown) { Assaults.Remove(a); continue; }
            if (Day > a.Burned)
            {
                a.Burned = Day;
                FabricOf(a.Town).Burnt += SiegeBurns;
            }
            if (Day < a.Ends) continue;
            Assaults.Remove(a);
            FabricOf(a.Town).Burnt += SackBurns;
            if (a.Town == Capital) Treasury = Math.Max(0, Treasury - SackTreasury);
            var place = AssaultPlace(a.Town);
            if (a.IsPride)
            {
                PrideAssaultNext = Day + Gameplay.Assaults.PrideEvery;
                var seal = SealsBroken < Undying.Seals;
                if (seal) SealsBroken++;
                News.Add($"Nobody broke Ronacse's siege of {place.Name}. He's humbled the whole town, and left it burning{(seal ? $"; and a seal on the Rift has broken with it, {SealsBroken} of {Undying.Seals}" : "")}.");
            }
            else
            {
                DeviantAssaultNext = Day + Gameplay.Assaults.DeviantEvery;
                News.Add($"{a.Name}'s band has sacked {place.Name} and gone, and nobody could stop it. It's the stronger for it.");
                sacked.Add(a);
            }
            Told(a, "sacked", $"{a.Name} sacked the town, and nobody could stop {(a.IsPride ? "him" : "it")}", place);
        }
        if (strongest <= 0) return sacked; // nobody to hear of it, or no game's said how strong it is

        if (!PrideDown && PrideAssault is null && Day >= PrideAssaultNext && !Gameplay.Assaults.PrideHunts(strongest, PrideLevel)
            && TownToAssault(1) is { } prideTown)
        {
            var a = Begin(prideTown, Gameplay.Assaults.Pride, "Ronacse, the Sin of Pride", "ronacse", PrideLevel, 0);
            PrideAssaultNext = a.Ends;
            News.Add($"No hero alive is worth Ronacse's time, he says, so he's come for a whole town: he's laid siege to {AssaultPlace(prideTown).Name}, and meteors are falling in its streets. Its market is shut. He'll be {Gameplay.Assaults.Days} days at it, if nobody breaks him.");
            Told(a, "begun", "Ronacse came down on the town in gold, saying no hero alive was worth his time, and the sky's been falling on it since", AssaultPlace(prideTown));
        }
        if (Day >= DeviantAssaultNext && !Assaults.Any(a => !a.IsPride)
            && deviants.Where(d => d.Rank >= Gameplay.Assaults.DeviantLeastRank && d.Might > strongest + OutclassBy).MaxBy(d => d.Might) is { } dev
            && TownToAssault(2) is { } devTown)
        {
            var a = Begin(devTown, dev.Id, dev.Name, dev.Kind, dev.Level, dev.Rank);
            DeviantAssaultNext = a.Ends;
            var place = AssaultPlace(devTown);
            News.Add($"{dev.Name} has grown past any hero to fight, and taken its band to {place.Name} instead. They hold the streets, and the market's shut. It'll be sacked in {Gameplay.Assaults.Days} days if nobody drives them out.");
            Told(a, "begun", $"{dev.Name} has brought its whole band down on the town. No hero's strong enough to face it alone", place);
        }
        return sacked;
    }

    Assault Begin(string town, string by, string name, string kind, int level, int rank)
    {
        var a = new Assault { Town = town, By = by, Name = name, Kind = kind, Level = level, Rank = rank, Since = Day, Ends = Day + Gameplay.Assaults.Days, Burned = Day };
        Assaults.Add(a);
        return a;
    }

    /// <summary>A town to come down on: the capital or a known place, not one somebody else already holds.</summary>
    string? TownToAssault(ulong salt)
    {
        var towns = Roads.Towns.Keys.Append(Capital).Distinct()
            .Where(t => !TradeShut(t) && Siege?.Town != t && AssaultOn(t) is null)
            .Order(StringComparer.Ordinal).ToList();
        return towns.Count == 0 ? null : new Rng(Seed ^ (ulong)Day * 0x632BE59BD9B4E019UL ^ salt).Pick(towns);
    }

    (string Name, double X, double Z) AssaultPlace(string town) =>
        town == Capital ? ("Tristinwold", 0, 0) : Roads.Towns.TryGetValue(town, out var m) ? (m.Name, m.X, m.Z) : (PlaceName(town), double.NaN, double.NaN);

    void Told(Assault a, string what, string did, (string Name, double X, double Z) place) => Heard(new Rumour
    {
        Key = $"assault|{a.By}|{a.Town}|{a.Since}|{what}", Did = did, Town = place.Name, Bad = true,
        X = double.IsNaN(place.X) ? null : place.X, Z = double.IsNaN(place.Z) ? null : place.Z,
    });

    /// <summary>
    /// A deviant sent after a hero who's far beneath it: it goes for their town instead. One town to a deviant, and
    /// not one already held; it starts whole, and has the usual days before it's sacked.
    /// </summary>
    bool AssaultBegun(Assault? a, string? against)
    {
        if (a is null || a.IsPride || a.By is not { Length: > 0 and <= 60 } || a.Name is not { Length: > 0 and <= 80 } || a.Kind is not { Length: > 0 and <= 60 }
            || a.Town is not { Length: > 0 and <= 80 } || AssaultOn(a.Town) is not null || Assaults.Any(x => x.By == a.By) || Assaults.Count >= 6) return false;
        var held = Begin(a.Town, a.By, a.Name, a.Kind, Math.Clamp(a.Level, 1, 1000), Math.Clamp(a.Rank, 0, 3));
        var place = AssaultPlace(held.Town);
        var who = against is { Length: > 0 and <= 60 } ? $"{against}'s company" : "a hero";
        News.Add($"{held.Name} came hunting {who}, found them beneath its notice, and fell on {place.Name} instead. Its band holds the streets and the market's shut. It'll be sacked in {Gameplay.Assaults.Days} days if nobody drives it out.");
        Told(held, "begun", $"{held.Name} came looking for someone, and when it found them, it took the whole town instead", place);
        return true;
    }

    /// <summary>A hero's blows, added to everyone's.</summary>
    bool AssaultHurt(string? town, double share)
    {
        if (AssaultOn(town) is not { } a || !double.IsFinite(share) || share <= 0) return false;
        a.Hurt = Math.Min(a.MostHurt, a.Hurt + Math.Min(share, Gameplay.Assaults.MostBlow));
        return true;
    }

    /// <summary>Broken: the one holding the town brought down. For Ronacse, that's his death; a deviant's comes through the roster.</summary>
    bool AssaultBroken(string? town, string? by)
    {
        if (AssaultOn(town) is not { } a || by is not { Length: > 0 and <= 60 }) return false;
        Assaults.Remove(a);
        var place = AssaultPlace(a.Town);
        if (a.IsPride)
        {
            PrideAssaultNext = Day + Gameplay.Assaults.PrideEvery;
            PrideFell(by);
        }
        else DeviantAssaultNext = Day + Gameplay.Assaults.DeviantEvery;
        News.Add($"{by} has broken {a.Name}'s hold on {place.Name}. The town's free, and its market's open again.");
        Told(a, "broken", $"{a.Name} was driven out of the town at last, by a hero and everyone who fought before them", place);
        return true;
    }
}
