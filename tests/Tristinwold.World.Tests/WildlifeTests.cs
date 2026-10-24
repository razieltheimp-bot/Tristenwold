using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>The animals and beasts between the settlements.</summary>
public class WildlifeTests
{
    static readonly WorldGen World = WorldGen.Default;

    [Fact]
    public void The_same_seed_gives_the_same_herds()
    {
        var a = Wildlife.Near(World, -150, -1500, 1200).ToList();
        var b = Wildlife.Near(World, -150, -1500, 1200).ToList();
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Every_herd_is_a_real_creature_on_dry_land_outside_settlements()
    {
        foreach (var h in Wildlife.Near(World, 0, -3000, 3000))
        {
            Assert.Equal(h.KindId, Bestiary.Get(h.KindId).Id);
            Assert.True(World.Height(h.X, h.Z) > WorldGen.WaterLevel, $"{h.Id} is in the water");
            Assert.DoesNotContain(World.SettlementsNear(h.X, h.Z, 0), s => s.DistanceTo(h.X, h.Z) < s.Radius);
            Assert.InRange(h.Count, 1, 8);
        }
    }

    [Fact]
    public void Home_is_safe_and_the_far_wilds_are_not()
    {
        var home = Wildlife.Near(World, 0, 0, Wildlife.SafeRadius - 20).ToList();
        Assert.All(home, h => Assert.Contains(h.KindId, new[] { "deer", "fox", "skeever", "mudcrab" }));
        var far = Wildlife.Near(World, 0, -6000, 1500).ToList();
        Assert.Contains(far, h => !Wildlife.Timid.Contains(h.KindId));
        Assert.Contains(Wildlife.Near(World, 0, -2000, 2000), h => Wildlife.Timid.Contains(h.KindId));
    }
}
