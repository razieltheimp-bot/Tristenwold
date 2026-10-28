using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// A barbarian warband: a clan of raiders out on the frontier, the real terror of the small places (hamlets,
/// farmsteads, hunting camps, inns, mines) that have a couple of guards at most and no Watch to call. Each roams its
/// own stretch of country, going from one small place to the next: on the road for a while, then down on a place,
/// raiding it, and on again. Worked out from the clan and the time alone, so every company sees it in the same
/// place. Only the far side of Tristinwold's fields: nothing raids within a kilometre of the walls.
/// </summary>
public sealed record Warband(string Id, string Name, string Chief, double X, double Z, int Raiders, ulong Seed)
{
    /// <summary>One clan, at most, to each square of this many metres; none nearer Tristinwold than <see cref="Beyond"/>.</summary>
    public const double Cell = 1800, Beyond = 1000, Range = 2200;
    /// <summary>A round of the clan's: on the road to the next place, then the raid itself, this long.</summary>
    public const double Round = 1200, RaidSeconds = 300;
    const ulong Salt = 0xBA2_BA21;

    static readonly string[] Words = ["Red", "Black", "Iron", "Bone", "Ash", "Wolf", "Blood", "Storm", "Grey", "Thorn", "Skull", "Hollow"];
    static readonly string[] Things = ["Axe", "Hand", "Fang", "Crow", "Shield", "Horn", "Spear", "Brand", "Hound", "Tooth"];
    static readonly string[] Chiefs = ["Hrodgar", "Ulfa", "Brannoch", "Skarra", "Torvald", "Gudrun", "Vorga", "Eirik", "Hala", "Kjeld", "Rana", "Odd"];
    static readonly string[] Bynames = ["the Burner", "Two-Axes", "the Red", "Skullcleaver", "the Unwashed", "Oathbreaker", "the Hungry", "Ironjaw"];

    /// <summary>The clan that roams a square of the frontier, if one does.</summary>
    public static Warband? In(long cx, long cz)
    {
        var rng = Noise.Dice(Salt, cx, cz);
        if (!rng.Chance(0.55)) return null;
        var (x, z) = ((cx + 0.2 + 0.6 * rng.NextDouble()) * Cell, (cz + 0.2 + 0.6 * rng.NextDouble()) * Cell);
        if (Math.Sqrt(x * x + z * z) < Beyond + 400) return null;
        var name = $"the {rng.Pick(Words)} {rng.Pick(Things)} clan";
        return new Warband($"warband:{cx}:{cz}", name, $"{rng.Pick(Chiefs)} {rng.Pick(Bynames)}", x, z, 4 + rng.Next(4), rng.NextULong());
    }

    /// <summary>The clans whose country reaches within <paramref name="radius"/> metres of a place.</summary>
    public static IEnumerable<Warband> Near(double x, double z, double radius)
    {
        var reach = radius + Range;
        for (var cx = (long)Math.Floor((x - reach) / Cell); cx <= (long)Math.Floor((x + reach) / Cell); cx++)
            for (var cz = (long)Math.Floor((z - reach) / Cell); cz <= (long)Math.Floor((z + reach) / Cell); cz++)
                if (In(cx, cz) is { } w) yield return w;
    }

    /// <summary>The small places in its country it raids: hamlets, farmsteads, hunting camps, inns and mines.</summary>
    public IReadOnlyList<Settlement> Targets(WorldGen world) =>
        [.. world.SettlementsNear(X, Z, Range).Where(Raidable).Where(s => Math.Sqrt(s.X * s.X + s.Z * s.Z) > Beyond).OrderBy(s => s.Id)];

    /// <summary>The places a clan goes for: small, and with nobody much to defend them.</summary>
    public static bool Raidable(Settlement s) =>
        s.Kind is SettlementKind.Hamlet or SettlementKind.Farmstead or SettlementKind.HuntingCamp or SettlementKind.Inn or SettlementKind.Mine;

    /// <summary>
    /// Where the clan is at a moment of play: which round it's in, where it is, and the place it's raiding, if it's
    /// down on one. Each round it rides from the last place it raided to the next, then raids it.
    /// </summary>
    public (int Round, double X, double Z, Settlement? Raiding) Where(WorldGen world, double playSeconds)
    {
        var targets = Targets(world);
        if (targets.Count == 0) return (0, X, Z, null);
        var round = (int)Math.Floor(playSeconds / Round);
        var into = playSeconds - round * Round;
        Settlement Pick(int r) => targets[(int)(Noise.Hash(Seed, r) % (ulong)targets.Count)];
        var (from, to) = (Pick(round - 1), Pick(round));
        var travel = Round - RaidSeconds;
        if (into >= travel) return (round, to.X, to.Z, to);
        var f = into / travel;
        // Camped out of sight on the way: they come down on a place, they don't stroll into it.
        return (round, from.X + (to.X - from.X) * f, from.Z + (to.Z - from.Z) * f, null);
    }

    /// <summary>What a place's guards are: how many, by how big a place it is (Tristinwold has its Watch).</summary>
    public static int Guards(Settlement s) => s.Kind switch
    {
        SettlementKind.City => 4,
        SettlementKind.Town => 3,
        SettlementKind.Hamlet or SettlementKind.Mine => 2,
        SettlementKind.Farmstead or SettlementKind.Inn or SettlementKind.HuntingCamp => 1,
        _ => 0,
    };

    /// <summary>What a place pays whoever breaks a raid on it: more for a bigger clan.</summary>
    public int Reward => 30 + 12 * Raiders;
}

/// <summary>Raids already reckoned with (plundered, or broken), and clans the hero has broken, saved with the company.</summary>
public sealed class RaidRecord
{
    /// <summary>"warband:3:-2#41": a clan's round that has been reckoned with.</summary>
    public HashSet<string> Done { get; set; } = [];
    /// <summary>Clans broken by the hero, and the play time until which they're licking their wounds.</summary>
    public Dictionary<string, double> Broken { get; set; } = [];
    public int Defended { get; set; }

    public const double BrokenSeconds = 3600;

    public static string Key(Warband w, int round) => $"{w.Id}#{round}";

    public bool IsBroken(Warband w, double playSeconds) => Broken.GetValueOrDefault(w.Id) > playSeconds;

    /// <summary>A raid to reckon with: the first time it's seen. Keeps only the latest few hundred.</summary>
    public bool Reckon(Warband w, int round)
    {
        if (!Done.Add(Key(w, round))) return false;
        if (Done.Count > 400) Done = [.. Done.Skip(200)];
        return true;
    }

    /// <summary>The hero broke the clan while it raided a place: it pulls back for an hour, and the place is spared.</summary>
    public void Break(Warband w, int round, double playSeconds)
    {
        Broken[w.Id] = playSeconds + BrokenSeconds;
        Done.Add(Key(w, round));
        Defended++;
    }
}
