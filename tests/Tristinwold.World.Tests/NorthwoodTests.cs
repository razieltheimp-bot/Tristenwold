using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>The great wood: a real maze you can get through, roads that stay open, and the land round it.</summary>
public class NorthwoodTests
{
    [Fact]
    public void The_maze_is_huge_and_every_part_of_it_can_be_reached()
    {
        Assert.True(Northwood.MazeCells > 200, $"{Northwood.MazeCells} cells");
        Assert.Equal(Northwood.MazeCells - 1, Northwood.MazePaths); // a tree of paths: all joined, with one way between any two places
    }

    [Fact]
    public void The_roads_run_clear_from_the_gate_to_the_village_and_out_of_the_north()
    {
        foreach (var line in new[] { Northwood.NorthRoad, Northwood.WestRoad })
            for (var i = 1; i < line.Length; i++)
                for (var t = 0.0; t <= 1; t += 0.02)
                {
                    var (x, z) = (line[i - 1].X + (line[i].X - line[i - 1].X) * t, line[i - 1].Z + (line[i].Z - line[i - 1].Z) * t);
                    Assert.True(Northwood.OnRoad(x, z));
                    Assert.True(Northwood.OnPath(x, z));
                    Assert.Empty(Northwood.GiantsIn(x - 3, z - 3, 6));
                }
    }

    [Fact]
    public void Giant_trunks_wall_the_maze_but_never_stand_on_a_path()
    {
        var giants = Northwood.GiantsIn(-700, -1300, 1400).ToList();
        Assert.True(giants.Count > 1000, $"{giants.Count} giants");
        Assert.All(giants, g => Assert.False(Northwood.OnPath(g.X, g.Z)));
        Assert.Empty(Northwood.GiantsIn(0, 0, 64));
    }

    [Fact]
    public void Its_danger_climbs_from_level_three_to_level_eight()
    {
        Assert.Equal(3, Northwood.Level(Northwood.VillageX, -300));
        Assert.Equal(3, Northwood.Level(Northwood.VillageX, Northwood.VillageZ));
        Assert.Equal(8, Northwood.Level(Northwood.ExitX, Northwood.ExitZ), 3);
        Assert.True(Northwood.Level(0, -1000) is > 3 and < 8);
        Assert.True(WorldGen.Danger(Northwood.ExitX, Northwood.ExitZ + 20) > WorldGen.Danger(Northwood.VillageX, Northwood.VillageZ));
        Assert.Equal(Northwood.DangerFor(3), WorldGen.Danger(-160, -300), 3);
    }

    [Fact]
    public void The_turned_guardian_opens_the_lair_and_only_a_true_one_breaks_the_barrier()
    {
        var city = CityEconomy.Start(1);
        var (rotten, truth) = (Northwood.Guardians.First(g => g.Corrupted), Northwood.Guardians.First(g => !g.Corrupted));
        city.Apply(CityAction.Guardian(rotten.Id, corrupted: true));
        Assert.True(city.LairOpen);
        Assert.False(city.DragonFree);
        for (var i = 0; i < 5; i++) city.Apply(CityAction.Guardian(rotten.Id, corrupted: true)); // repeat kills change nothing
        Assert.False(city.DragonFree);
        Assert.Empty(city.GuardiansSlain);
        city.Apply(CityAction.Guardian(truth.Id));
        Assert.True(city.DragonFree);
        Assert.Contains(truth.Id, city.GuardiansSlain);
        city.Apply(CityAction.Guardian("dragon"));
        Assert.True(city.DragonSlain);
        Assert.False(city.DragonFree);
    }

    [Fact]
    public void A_true_guardian_killed_first_frees_nothing_until_the_lair_is_open()
    {
        var city = CityEconomy.Start(1);
        city.Apply(CityAction.Guardian(Northwood.Guardians.First(g => !g.Corrupted).Id));
        Assert.False(city.LairOpen);
        Assert.False(city.DragonFree);
        city.Apply(CityAction.Guardian(Northwood.Guardians.First(g => g.Corrupted).Id, corrupted: true));
        Assert.True(city.DragonFree); // that one kill was the second
    }

    [Fact]
    public void Every_guardian_is_real_and_worth_hunting()
    {
        Assert.Equal(4, Northwood.Guardians.Count);
        Assert.Single(Northwood.Guardians, g => g.Corrupted);
        foreach (var g in Northwood.Guardians)
        {
            Assert.True(Northwood.Inside(g.X, g.Z), g.Name);
            Assert.False(Northwood.OnRoad(g.X, g.Z), g.Name);
            Assert.True(Bestiary.Get(g.Kind).Wary);
            Assert.True(Bestiary.Get(g.Kind).Health > 3000);
            Assert.NotNull(Items.Get(g.Loot));
        }
        Assert.All(Northwood.Guardians.Where(g => !g.Corrupted), g => Assert.True(Items.Get(g.Loot)!.Value >= 1000));
        Assert.True(Northwood.Corrupted(318, -782));
        Assert.False(Northwood.Corrupted(Northwood.VillageX, Northwood.VillageZ));
        Assert.NotNull(Items.Get("spoiled_herbs"));
        Assert.Contains("rotwood", Recipes.Fuels);
    }

    [Fact]
    public void Its_bands_are_real_and_stay_away_from_the_village()
    {
        var bands = Northwood.Near(Northwood.CentreX, Northwood.CentreZ, 700).ToList();
        Assert.True(bands.Count > 20);
        // Near the village only what grazes: deer and foxes, never a band.
        Assert.All(bands, h => { Assert.NotNull(Bestiary.Get(h.KindId)); Assert.True(!Northwood.NearVillage(h.X, h.Z) || Wildlife.Timid.Contains(h.KindId), $"{h.KindId} at {h.X:0},{h.Z:0} is too near the village"); });
        Assert.Contains(bands, h => Northwood.NearVillage(h.X, h.Z) && h.KindId == "deer" && h.Young > 0);
        Assert.Equal(bands.Count, bands.Select(h => h.Id).Distinct().Count());
    }

    [Fact]
    public void The_mountains_rise_behind_the_wood_and_the_strand_is_water_under_them()
    {
        var world = WorldGen.Default;
        Assert.True(world.Height(-1100, -1430) > 90, $"{world.Height(-1100, -1430):0}");
        Assert.True(world.Height(-2620, 0) > 90, $"{world.Height(-2620, 0):0}");
        Assert.True(world.Height(-1500, -250) < WorldGen.WaterLevel);
        Assert.True(world.Height(-960, -260) > WorldGen.WaterLevel, $"{world.Height(-960, -260):0}"); // the road ends on dry land
        Assert.True(Math.Abs(world.Height(-100, -50)) < 3); // and the city's ground is left alone
    }
}
