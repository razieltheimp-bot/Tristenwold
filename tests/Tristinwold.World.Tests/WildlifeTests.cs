using Tristinwold.Deviant;
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
        Assert.All(home, h => Assert.Contains(h.KindId, new[] { "deer", "fox", "wild_horse", "giant_rat", "mud_crab" }));
        var far = Wildlife.Near(World, 0, -6000, 1500).ToList();
        Assert.Contains(far, h => !Wildlife.Timid.Contains(h.KindId));
        Assert.Contains(Wildlife.Near(World, 0, -2000, 2000), h => Wildlife.Timid.Contains(h.KindId));
    }

    [Fact]
    public void Farms_keep_livestock_towns_keep_dogs_and_cats_and_killing_them_costs_you()
    {
        var farm = World.SettlementsNear(0, 0, 6000).First(s => s.Kind == SettlementKind.Farmstead);
        var animals = Livestock.Of(farm, new Rng(1));
        Assert.Contains(animals, a => a.Kind == "cow" && a.Count >= 2);
        Assert.Contains(animals, a => a.Kind == "sheep");
        var town = World.SettlementsNear(0, 0, 6000).First(s => s.Kind == SettlementKind.Town);
        Assert.Contains(Livestock.Of(town, new Rng(1)), a => a.Kind is "dog" or "cat");
        Assert.All(new[] { "cow", "sheep", "pig", "hen", "horse", "dog", "cat", "wild_horse" }, k => Assert.Contains(k, Wildlife.Timid));
        Assert.True(Livestock.Fine("cow") > Livestock.Fine("hen"));
        // What they're good for.
        Assert.Contains(Loot.Roll("sheep", new Rng(3)).Items, i => i.Id == "wool");
        Assert.Contains(Loot.Roll("cow", new Rng(3)).Items, i => i.Id == "raw_meat");
        // Wild horses run the open land, and a foal can be raised.
        Assert.Contains(Wildlife.Near(World, 0, -2500, 2500), h => h.KindId == "wild_horse");
        Assert.True(Taming.Tameable("wild_horse"));
    }
}
