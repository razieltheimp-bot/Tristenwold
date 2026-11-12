using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>Walking through Hollowheart: round its great trees, never into them.</summary>
public class HeartTreeWalkTests
{
    static (double X, double Z) Walk(double x, double z, double gx, double gz, out int steps, out bool intoRoots)
    {
        (steps, intoRoots) = (0, false);
        var side = 0;
        while (Math.Sqrt((gx - x) * (gx - x) + (gz - z) * (gz - z)) > 0.6 && steps++ < 3000)
        {
            var (wx, wz) = Northwood.Around(x, z, gx, gz, ref side);
            var (dx, dz) = (wx - x, wz - z);
            var d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6) break;
            var step = Math.Min(0.5, d);
            (x, z) = (x + dx / d * step, z + dz / d * step);
            if (Northwood.InRoots(x, z, 0.6)) intoRoots = true;
        }
        return (x, z);
    }

    [Fact]
    public void Walking_about_Hollowheart_goes_round_the_great_trees_never_into_them()
    {
        var rng = new Random(5);
        var (made, tried) = (0, 0);
        while (tried < 200)
        {
            var (x, z) = (Northwood.VillageX + rng.NextDouble() * 160 - 80, Northwood.VillageZ + rng.NextDouble() * 160 - 80);
            var (gx, gz) = (Northwood.VillageX + rng.NextDouble() * 160 - 80, Northwood.VillageZ + rng.NextDouble() * 160 - 80);
            if (Northwood.InRoots(x, z) || Northwood.InRoots(gx, gz)) continue;
            tried++;
            var (ex, ez) = Walk(x, z, gx, gz, out var steps, out var into);
            Assert.False(into, $"walked into the roots from {x:0},{z:0} toward {gx:0},{gz:0}");
            if (steps < 3000) made++;
        }
        Assert.True(made >= tried * 0.93, $"{made} of {tried} walks got where they were going by steering alone (the rest are put on their way by the walker's own unsticking)");
    }

    [Fact]
    public void Nothing_in_the_way_means_straight_there_and_any_spot_in_the_roots_is_pushed_clear_of_them_all()
    {
        var t = Northwood.Hearts[0];
        var c = Northwood.Clearance(t);
        var (x, z) = (t.X, t.Z + c * 3.5);
        Assert.Equal((x + 3, z + 1), Northwood.Around(x, z, x + 3, z + 1));
        var rng = new Random(9);
        for (var i = 0; i < 300; i++)
        {
            var (ox, oz) = Northwood.OutOfTrunks(Northwood.VillageX + rng.NextDouble() * 120 - 60, Northwood.VillageZ + rng.NextDouble() * 120 - 60);
            Assert.False(Northwood.InRoots(ox, oz, 0.01), $"{ox:0.0},{oz:0.0} is still in the roots");
        }
    }
}
