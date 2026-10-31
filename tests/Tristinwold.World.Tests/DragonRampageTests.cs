using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

public class DragonRampageTests
{
    static CityEconomy Freed()
    {
        var c = CityEconomy.Start(5);
        c.DragonFree = true;
        return c;
    }

    static void Days(CityEconomy c, int n) { for (var i = 0; i < n; i++) c.NextDay(); }

    [Fact]
    public void Nothing_burns_while_the_dragon_is_sealed()
    {
        var c = CityEconomy.Start(5);
        Days(c, CityEconomy.SiegeEvery - 2); // short of Soren's first landing
        Assert.All(c.Fabric.Values, f => Assert.Equal(0, f.Burnt));
        Assert.Equal(0, c.Slump);
    }

    [Fact]
    public void It_goes_for_the_timber_port_first_and_what_burns_stays_burned()
    {
        var c = Freed();
        Assert.Equal("city:druitt", c.DragonTarget());
        Days(c, CityEconomy.RampageEvery);
        Assert.Equal("city:druitt", c.DragonAt);
        Assert.Equal(CityEconomy.BurnedAtOnce, c.FabricOf("city:druitt").Burnt);
        Assert.True(c.Slump > 0.2);
        var back = System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(System.Text.Json.JsonSerializer.Serialize(c))!;
        Assert.Equal(CityEconomy.BurnedAtOnce, back.FabricOf("city:druitt").Burnt);
    }

    [Fact]
    public void Prices_fall_when_people_run()
    {
        var c = CityEconomy.Start(5);
        var was = c.Factor(Good.Bread);
        c.Slump = 0.3;
        Assert.True(c.Factor(Good.Bread) < was);
    }

    [Fact]
    public void It_never_touches_the_village_that_never_cut_a_tree()
    {
        var c = Freed();
        Days(c, 90);
        Assert.Equal(0, c.FabricOf("city:hollowheart").Burnt);
    }

    [Fact]
    public void Once_its_dead_the_carpenters_put_the_towns_back_up_a_home_at_a_time()
    {
        var c = CityEconomy.Start(5);
        c.FabricOf("city:druitt").Burnt = 3;
        c.DragonSlain = true;
        for (var i = 0; i < 60 && c.FabricOf("city:druitt").Burnt > 0; i++)
        {
            c.Stock[Good.Timber] = 100;
            c.NextDay();
        }
        Assert.Equal(0, c.FabricOf("city:druitt").Burnt);
    }

    [Fact]
    public void A_town_with_no_timber_stays_burned()
    {
        var c = CityEconomy.Start(5);
        c.FabricOf("city:druitt").Burnt = 3;
        c.Stock[Good.Timber] = 0;
        Assert.Null(c.Carpenters(new Tristinwold.Deviant.Rng(1)));
        Assert.Equal(3, c.FabricOf("city:druitt").Burnt);
    }

    [Fact]
    public void A_prosperous_road_grows_and_stops_growing_at_a_size()
    {
        var c = CityEconomy.Start(5);
        c.Roads.Towns["city:greywater"] = new TownMarket { Id = "city:greywater", Name = "Greywater" };
        var rng = new Tristinwold.Deviant.Rng(1);
        for (var i = 0; i < 60; i++)
        {
            (c.Treasury, c.Stock[Good.Timber]) = (2000, 100);
            c.Carpenters(rng);
        }
        Assert.Equal(CityEconomy.GrowthCap, c.FabricOf("city:greywater").Grown);
    }
}
