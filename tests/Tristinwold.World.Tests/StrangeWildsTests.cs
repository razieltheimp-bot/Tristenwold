using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit;

namespace Tristinwold.World.Tests;

public class StrangeWildsTests
{
    static readonly string[] Strange = ["kipine", "deathcrest_ape", "dire_imp", "imp", "winged_ape"];

    [Fact]
    public void The_stranger_creatures_live_out_in_the_wilds_never_near_home()
    {
        var w = WorldGen.Default;
        var herds = Wildlife.Near(w, 0, 0, 3000).Where(h => Strange.Contains(h.KindId)).ToList();
        foreach (var kind in Strange) Assert.Contains(herds, h => h.KindId == kind);
        Assert.All(herds, h => Assert.True(h.DistanceTo(0, 0) > Wildlife.SafeRadius, $"{h.KindId} at {h.X:0},{h.Z:0}"));
        foreach (var kind in Strange)
        {
            Assert.Equal(kind, Bestiary.Get(kind).Id);
            Assert.NotEmpty(Wildlife.TricksOf(kind));
        }
    }

    [Fact]
    public void Packs_and_bands()
    {
        var herds = Wildlife.Near(WorldGen.Default, 0, 0, 3000).ToList();
        Assert.All(herds.Where(h => h.KindId == "deathcrest_ape"), h => Assert.True(h.Count >= 3, "apes hunt in packs"));
        Assert.All(herds.Where(h => h.KindId == "kipine"), h => Assert.Equal(1, h.Count));
        Assert.All(herds.Where(h => h.KindId == "dire_imp"), h => Assert.True(h.Band == "imp" && h.BandCount >= 2, "a dire imp has its band of imps"));
        Assert.All(herds.Where(h => h.KindId is "imp" or "dire_imp"), h => Assert.Equal(0, h.Young));
        Assert.True(Wildlife.HuntsUndead("deathcrest_ape") && !Wildlife.HuntsUndead("wolf"));
    }
}
