using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

public class PlayerStoreTests
{
    static PlayerStore Shop()
    {
        var h = new House { Owned = true, Built = Housing.Stages.Count, IsHome = true, Till = 100 };
        h.Furniture.Add("shop_counter");
        h.Chest["iron_ingot"] = 3;
        h.ForSale.Add("iron_ingot");
        h.Buying.Add("coal");
        return new PlayerStore { Owner = "Joe", Name = "Joe's Shop", Workers = 1, House = h };
    }

    [Fact]
    public void A_players_shop_beats_any_local_shop_by_a_crown_each_way()
    {
        var c = CityEconomy.Start(5);
        var iron = Items.Get("iron_ingot")!;
        Assert.Equal(c.Price(iron) - 1, c.StorePrice(iron));
        Assert.Equal(c.Offer(iron) + 1, c.StoreOffer(iron));
        Assert.Equal(iron.Value - 1, Homestead.SellsFor("iron_ingot"));
    }

    [Fact]
    public void Nothing_can_be_bought_from_a_shop_and_sold_straight_back_for_more()
    {
        var c = CityEconomy.Start(5);
        foreach (var item in Items.All) Assert.True(c.StoreOffer(item) <= c.StorePrice(item), item.Id);
    }

    [Fact]
    public void A_visitor_trades_at_the_counter_with_the_owner_away()
    {
        var c = CityEconomy.Start(5);
        Assert.True(c.Apply(CityAction.StoreSet(Shop())));
        Assert.True(c.Apply(CityAction.StoreBuy("Joe", "iron_ingot", 20)));
        Assert.Equal(2, c.StoreOf("Joe")!.House.Chest["iron_ingot"]);
        Assert.Equal(120, c.StoreOf("Joe")!.House.Till);
        Assert.True(c.Apply(CityAction.StoreSell("Joe", "coal", 5)));
        Assert.Equal(1, c.StoreOf("Joe")!.House.Chest["coal"]);
        Assert.False(c.Apply(CityAction.StoreSell("Joe", "bread", 5)));
        Assert.False(c.Apply(CityAction.StoreBuy("Joe", "coal", 5)));
    }

    [Fact]
    public void A_boiler_burns_anything_a_fire_takes_and_a_better_maker_gets_further_on_it()
    {
        Assert.True(SteamWorks.Burns("coal", 1) > 0 && SteamWorks.Burns("pine_log", 1) > 0);
        Assert.Equal(0, SteamWorks.Burns("bread", 10));
        Assert.True(SteamWorks.Burns("coal", 20) > SteamWorks.Burns("coal", 1));
        Assert.All(SteamWorks.Needs, n => Assert.NotNull(Items.Get(n.Id)));
    }

    [Fact]
    public void A_steam_wagon_carries_a_full_load_with_no_horse()
    {
        var book = new TradeBook { Steam = true };
        Assert.Equal(TradeBook.PerHorse * TradeBook.MaxHorses, book.Capacity);
    }

    [Fact]
    public void A_shop_with_nobody_minding_it_is_shut()
    {
        var c = CityEconomy.Start(5);
        var s = Shop();
        s.Workers = 0;
        c.Apply(CityAction.StoreSet(s));
        Assert.False(c.Apply(CityAction.StoreBuy("Joe", "iron_ingot", 20)));
    }

    [Fact]
    public void Home_keeps_working_with_the_city_clock_alone_and_outlives_a_save()
    {
        var c = CityEconomy.Start(5);
        c.Apply(CityAction.StoreSet(Shop()));
        c.TendStores(Homestead.JobSeconds * 2);
        Assert.Equal(1, c.StoreOf("Joe")!.House.Chest["iron_ingot"]);
        var copy = System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(System.Text.Json.JsonSerializer.Serialize(c))!;
        Assert.Equal("Joe's Shop", copy.StoreOf("Joe")!.Name);
        Assert.Equal(1, copy.StoreOf("Joe")!.House.Chest["iron_ingot"]);
    }
}
