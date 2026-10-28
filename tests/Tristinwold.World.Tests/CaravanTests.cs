using System.Text.Json;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>Every place out in the world keeps its own little market; the caravan yard sends wagons between them.</summary>
public class CaravanTests
{
    static Settlement Village(WorldGen w) => w.SettlementsNear(0, 0, 2500).Where(TownMarket.Trades).OrderBy(s => s.DistanceTo(0, 0)).First();

    [Fact]
    public void Each_place_makes_a_few_goods_cheaply_and_pays_well_for_what_it_lacks()
    {
        var w = WorldGen.Default;
        var places = w.SettlementsNear(0, 0, 6000).Where(TownMarket.Trades).Take(30).ToList();
        Assert.NotEmpty(places);
        foreach (var s in places)
        {
            var m = TownMarket.For(s);
            Assert.InRange(m.Makes.Count, 1, 4);
            Assert.InRange(m.Wants.Count, 3, 4);
            Assert.Empty(m.Makes.Intersect(m.Wants));
            foreach (var g in m.Makes) Assert.True(m.Asks(g) < CityEconomy.Info(g).Worth, $"{s.Name} makes {g} but asks {m.Asks(g)}");
            foreach (var g in m.Wants) Assert.True(m.Pays(g) > CityEconomy.Info(g).Worth, $"{s.Name} wants {g} but pays {m.Pays(g)}");
            Assert.Equal(m.Makes, TownMarket.For(s).Makes); // the same place is the same market for everyone
        }
        Assert.True(places.Select(s => string.Join(",", TownMarket.For(s).Makes)).Distinct().Count() > 3, "every place trades in the same things");
        Assert.DoesNotContain(w.SettlementsNear(0, 0, 6000).Where(s => s.Kind is SettlementKind.BanditCamp or SettlementKind.Den), TownMarket.Trades);
    }

    [Fact]
    public void Selling_fills_its_stores_and_the_price_falls_then_the_days_bring_it_back()
    {
        var m = TownMarket.For(Village(WorldGen.Default));
        var want = m.Wants[0];
        var first = m.Pays(want);
        var crowns = m.Sell(want, 12);
        Assert.True(crowns > 0 && m.Pays(want) < first, $"{first} then {m.Pays(want)}");
        Assert.True(crowns < first * 12);
        m.Catch(30);
        Assert.InRange(m.Pays(want), first - 2, first + 1);

        var make = m.Makes[0];
        var have = m.ForSale(make);
        var cost = m.Buy(make, 1000);
        Assert.True(cost > 0 && m.ForSale(make) == 0 && have > 0);
        Assert.Equal(0, m.Buy(make, 3)); // it won't sell its last
    }

    [Fact]
    public void A_loader_becomes_a_driver_a_driver_a_trader_with_their_own_wagon()
    {
        var w = WorldGen.Default;
        var book = new TradeBook();
        for (var i = 1; i < TradeBook.LoadsToDrive; i++) Assert.False(book.Loaded1());
        Assert.Equal(CaravanRank.Loader, book.Rank);
        Assert.True(book.Loaded1());
        Assert.Equal(CaravanRank.Driver, book.Rank);
        Assert.False(book.BuyWagon());

        var rng = new Rng(3);
        for (var run = 1; run <= TradeBook.RunsToOwn; run++)
        {
            book.Run = book.Plan(w, [], run, rng);
            var r = Assert.IsType<CaravanRun>(book.Run);
            Assert.True(r.Pay > 20);
            var town = book.Towns[r.To];
            var (outBefore, backBefore) = (town.Stock[r.Out], town.Stock[r.Back]);
            Assert.Equal(r.Pay / 2, book.Arrive(run));
            Assert.True(town.Stock[r.Out] > outBefore && town.Stock[r.Back] < backBefore, "the goods changed hands");
            Assert.Equal(1, town.Visits >= 1 ? 1 : 0);
            Assert.Equal(run == TradeBook.RunsToOwn, book.Home());
        }
        Assert.True(book.MayOwn && book.BuyWagon());
        Assert.Equal(CaravanRank.Entrepreneur, book.Rank);
        Assert.Equal(0, book.Capacity); // no horses yet
        book.Horses = 2;
        Assert.Equal(2 * TradeBook.PerHorse, book.Capacity);
        book.Cargo[Good.Iron] = 5;
        Assert.Equal(2 * TradeBook.PerHorse - 5, book.Room);

        var copy = JsonSerializer.Deserialize<TradeBook>(JsonSerializer.Serialize(book))!;
        Assert.Equal(CaravanRank.Entrepreneur, copy.Rank);
        Assert.Equal(5, copy.Cargo[Good.Iron]);
        Assert.Equal(book.Towns.Keys, copy.Towns.Keys);
    }

    [Fact]
    public void Tristinwold_trades_with_the_places_it_knows()
    {
        var w = WorldGen.Default;
        var city = CityEconomy.Start(1);
        var book = new TradeBook();
        Assert.Empty(book.TradeDay(city, 1, new Rng(1))); // knows nowhere yet
        var s = Village(w);
        var m = book.Market(s, 1);
        var made = m.Makes.ToDictionary(g => g, g => city.Stock[g]);
        var news = book.TradeDay(city, 2, new Rng(1));
        Assert.Contains($"A caravan from {s.Name}", Assert.Single(news));
        Assert.Contains(m.Makes, g => city.Stock[g] > made[g]);
    }
}
