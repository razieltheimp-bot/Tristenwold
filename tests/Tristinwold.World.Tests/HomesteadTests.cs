using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>A home that runs itself: companions left there work its stations, from and into the chest.</summary>
public class HomesteadTests
{
    static House Home(params string[] stations)
    {
        var h = new House { Owned = true, Built = Housing.Stages.Count, IsHome = true };
        foreach (var s in stations) h.Furniture.Add(s);
        return h;
    }

    [Fact]
    public void A_companion_at_the_forge_works_the_chest_a_job_at_a_time()
    {
        var h = Home("home_forge");
        h.Chest["iron_ore"] = 5;
        var made = Homestead.Tick(h, 1, Homestead.JobSeconds * 2);
        Assert.Equal(2, made.Count);
        Assert.Equal(2, h.Chest["iron_ingot"]);
        Assert.Equal(1, h.Chest["iron_ore"]);
    }

    [Fact]
    public void Nobody_at_a_station_and_nothing_to_work_means_nothing_happens()
    {
        var h = Home("home_forge");
        h.Chest["iron_ore"] = 4;
        Assert.Empty(Homestead.Tick(h, 0, 1000));
        Assert.Equal(4, h.Chest["iron_ore"]);
        h.Chest.Clear();
        Assert.Empty(Homestead.Tick(h, 1, 1000));
    }

    [Fact]
    public void A_farm_plot_grows_food_for_nothing_and_each_worker_takes_one_station()
    {
        var h = Home("farm_plot", "alchemy_bench");
        var made = Homestead.Tick(h, 1, Homestead.JobSeconds);
        Assert.Single(made);
        Assert.NotEmpty(h.Chest);
    }

    [Fact]
    public void A_house_that_isnt_home_does_nothing_and_only_those_left_behind_work()
    {
        var h = Home("farm_plot");
        h.IsHome = false;
        Assert.Empty(Homestead.Tick(h, 3, 1000));
        Assert.False(Homestead.Free(new Bond { Following = true }));
        Assert.False(Homestead.Free(new Bond { Following = false, Hitched = true }));
        Assert.True(Homestead.Free(new Bond { Following = false }));
    }

    [Fact]
    public void A_counter_with_nothing_on_it_is_just_a_counter_until_the_hero_says_otherwise()
    {
        var h = Home("shop_counter");
        h.Chest["iron_ingot"] = 3;
        Assert.Empty(Homestead.Tick(h, 1, 1000));
        Assert.Equal(3, h.Chest["iron_ingot"]);
        Assert.Equal(0, h.Till);
    }

    [Fact]
    public void A_companion_sells_what_the_hero_listed_and_buys_in_what_they_asked_for_from_the_till()
    {
        var h = Home("shop_counter");
        h.Chest["iron_ingot"] = 2;
        h.ForSale.Add("iron_ingot");
        h.Buying.Add("coal");
        Homestead.Tick(h, 1, Homestead.JobSeconds);
        Assert.Equal(1, h.Chest["iron_ingot"]);
        Assert.Equal(1, h.Chest["coal"]);
        Assert.Equal(Homestead.SellsFor("iron_ingot") - Homestead.BuysFor("coal"), h.Till);
        Assert.Equal(0, Homestead.Tick(h, 0, 1000).Count); // nobody home, nobody served
    }
}
