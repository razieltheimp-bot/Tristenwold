using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class HandiworkTests
{
    [Fact]
    public void A_busy_yard_fills_the_bay_and_it_settles_back_when_nobody_keeps_at_it()
    {
        var city = CityEconomy.Start(3);
        Assert.Equal(2, city.BayShips); // two at rest
        for (var i = 0; i < 12; i++) Assert.True(city.Apply(CityAction.Worked(CityEconomy.Capital, HandiKind.Ships)));
        Assert.Equal(5, city.BayShips);
        for (var i = 0; i < 400; i++) city.Apply(CityAction.Worked(CityEconomy.Capital, HandiKind.Ships));
        Assert.Equal(9, city.BayShips); // as many as the bay holds
        var busy = city.BayShips;
        for (var d = 0; d < 120; d++) city.Advance((city.Day + 1) * CityEconomy.DaySeconds + 1);
        Assert.True(city.BayShips < busy, "the work fades when nobody keeps at it");
    }

    [Fact]
    public void Only_the_people_of_the_company_keep_shop_and_one_away_at_the_wagon_or_ship_isnt_working_at_home()
    {
        Assert.True(Keeping.CanKeep(new Bond { Role = BondRole.Apprentice }));
        Assert.True(Keeping.CanKeep(new Bond { Role = BondRole.Squire }));
        Assert.False(Keeping.CanKeep(new Bond { Role = BondRole.Pet }));
        Assert.True(Homestead.Free(new Bond { Following = false, Keeps = Keeping.Home }));
        Assert.False(Homestead.Free(new Bond { Following = false, Keeps = Keeping.Ship }));
        Assert.False(Homestead.Free(new Bond { Following = false, Keeps = Keeping.Wagon }));
        Assert.True(Homestead.Free(new Bond { Following = false }));
        var kept = System.Text.Json.JsonSerializer.Deserialize<Bond>(System.Text.Json.JsonSerializer.Serialize(new Bond { Keeps = Keeping.Ship }))!;
        Assert.Equal(Keeping.Ship, kept.Keeps);
    }

    [Fact]
    public void A_shop_kept_aboard_trades_slowly_selling_what_is_put_out_and_buying_in_out_of_the_till()
    {
        var shop = new House { Owned = true, Built = Housing.Stages.Count, IsHome = true, Furniture = ["shop_counter"] };
        shop.Chest["iron_ingot"] = 3;
        shop.ForSale.Add("iron_ingot");
        shop.Buying.Add("leather");
        Homestead.Tick(shop, 1, Homestead.JobSeconds - 1);
        Assert.Equal(0, shop.Till); // not a customer yet
        Homestead.Tick(shop, 1, Homestead.JobSeconds * 3);
        Assert.Equal(0, shop.Chest.GetValueOrDefault("iron_ingot"));
        Assert.True(shop.Chest.GetValueOrDefault("leather") > 0, "the till buys leather in");
        Assert.Equal(3 * Homestead.SellsFor("iron_ingot") - shop.Chest["leather"] * Homestead.BuysFor("leather"), shop.Till);
    }

    [Fact]
    public void A_towns_forges_arm_its_guards_and_its_harvests_raise_its_children()
    {
        var city = CityEconomy.Start(3);
        const string town = "city:greywater";
        Assert.Equal((0, 0), (city.GuardTier(town), city.FedYouths(town)));
        for (var i = 0; i < 15; i++) city.Apply(CityAction.Worked(town, HandiKind.Smithing));
        Assert.Equal(1, city.GuardTier(town));
        for (var i = 0; i < 30; i++) city.Apply(CityAction.Worked(town, HandiKind.Smithing));
        Assert.Equal(2, city.GuardTier(town));
        Assert.Equal(0, city.GuardTier("city:druitt")); // only where the work's done
        for (var i = 0; i < 24; i++) city.Apply(CityAction.Worked(town, HandiKind.Farming));
        Assert.Equal(3, city.FedYouths(town));
        Assert.False(city.Apply(CityAction.Worked(town, HandiKind.Farming, 0)));
        // A piece of work counts for no more than a little, however it's sent.
        city.Apply(CityAction.Worked("city:druitt", HandiKind.Smithing, 1000));
        Assert.Equal(0, city.GuardTier("city:druitt"));
    }
}
