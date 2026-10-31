namespace Tristinwold.World;

/// <summary>Something out on the open water: an islet to chart, or a reef to stay off.</summary>
public sealed record SeaMark(double X, double Z, double Radius, bool Reef, int Id);

/// <summary>
/// The hero at the wheel on a voyage of exploration. The sea's made as it's sailed, a patch at a time, the same for every
/// sailing of a voyage. A course steered true makes better time than the crew would alone; a bad one loses ground.
/// </summary>
public static class Helm
{
    public const double Patch = 420, TurnRate = 0.32, FullSpeed = 16, ReefSail = 0.45, BestPace = 1.6;
    /// <summary>How far a hull keeps off a mark, what a reef costs her, and how near an islet needs to be to chart it.</summary>
    public const double Clearance = 9, ReefDamage = 12, Charting = 130, LowestHull = 10;
    public const int ChartPay = 15;

    /// <summary>The way to a port from where a voyage starts, as a compass heading in radians: its own for each port.</summary>
    public static double Bearing(string port) => (double)(uint)WorldGen.StableHash(port) / uint.MaxValue * Math.Tau;

    /// <summary>What's in one patch of sea.</summary>
    public static IEnumerable<SeaMark> In(int px, int pz, string port)
    {
        var rng = new Tristinwold.Deviant.Rng((ulong)WorldGen.StableHash($"{port}/{px}/{pz}"));
        if (px == 0 && pz == 0) yield break; // the ship starts in clear water
        double Spot(int patch) => (patch + 0.15 + rng.NextDouble() * 0.7) * Patch;
        if (rng.Chance(0.3)) yield return new SeaMark(Spot(px), Spot(pz), 34 + rng.NextDouble() * 40, false, px * 10007 + pz);
        if (rng.Chance(0.45)) yield return new SeaMark(Spot(px), Spot(pz), 9 + rng.NextDouble() * 9, true, px * 10007 + pz + 1);
    }

    static double Off(double heading, double bearing) => Math.Atan2(Math.Sin(bearing - heading), Math.Cos(bearing - heading));

    /// <summary>How fast she's making way toward port, as a share of the crew's own pace: dead on at full sail is best, and sailing away loses ground.</summary>
    public static double Pace(double heading, double bearing, double sail) => Math.Clamp(Math.Cos(Off(heading, bearing)), -0.5, 1) * sail * BestPace;

    /// <summary>What the compass says.</summary>
    public static string Steer(double heading, double bearing)
    {
        var off = Off(heading, bearing);
        var degrees = (int)Math.Round(Math.Abs(off) * 180 / Math.PI);
        return degrees <= 8 ? "steady on course" : $"come {(off > 0 ? "to port" : "to starboard")} {degrees} degrees";
    }
}
