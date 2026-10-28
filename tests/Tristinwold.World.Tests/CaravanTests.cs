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
        Assert.Null(book.Plan(w, 1, rng)); // no place known yet: no route to drive
        book.Market(Village(w), 1); // found one
        for (var run = 1; run <= TradeBook.RunsToOwn; run++)
        {
            book.Run = book.Plan(w, run, rng);
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

    [Fact]
    public void Only_the_places_you_have_found_are_trade_routes_and_each_one_found_adds_one()
    {
        var w = WorldGen.Default;
        var book = new TradeBook();
        Assert.Empty(book.Routes);
        var places = w.SettlementsNear(0, 0, 2500).Where(TownMarket.Trades).OrderBy(s => s.DistanceTo(0, 0)).Take(5).ToList();
        foreach (var (s, i) in places.Select((s, i) => (s, i)))
        {
            book.Market(s, 1);
            Assert.Equal(i + 1, book.Routes.Count());
        }
        Assert.Equal(places.Select(s => s.Id).Order(), book.Routes.Select(m => m.Id).Order());
        // Day by day, every route gets its turn.
        var city = CityEconomy.Start(1);
        var rng = new Rng(2);
        for (var day = 2; day < 6; day++) book.TradeDay(city, day, rng);
        Assert.All(book.Routes, m => Assert.True(m.LastTrade > 1, $"{m.Name} never traded"));
    }

    [Fact]
    public void Guarding_a_caravan_pays_more_the_further_and_the_more_seasoned_the_guard_and_draws_more_bandits()
    {
        var w = WorldGen.Default;
        var book = new TradeBook();
        Assert.Empty(book.Escorts(5)); // nowhere known, no caravans to guard
        foreach (var s in w.SettlementsNear(0, 0, 3500).Where(TownMarket.Trades).Take(8)) book.Market(s, 1);
        var low = book.Escorts(3).ToList();
        var high = book.Escorts(12).ToList();
        Assert.NotEmpty(low);
        Assert.All(low.Zip(high), x => Assert.True(x.Second.Pay > x.First.Pay && x.Second.Bandits >= x.First.Bandits));
        var near = low.First();
        var far = low.Last();
        Assert.True(far.Pay > near.Pay && far.Ambushes.Count >= near.Ambushes.Count);
        Assert.All(low, e => Assert.All(e.Ambushes, a => Assert.InRange(a, 0.1, 0.9)));

        // On the road: the ambushes come as the wagon gets far enough along.
        var e = book.Guarding = low.First();
        Assert.False(e.AmbushDue(0.1));
        Assert.True(e.AmbushDue(e.Ambushes[0] + 0.01));
        e.Sprung++;
        Assert.False(e.AmbushDue(e.Ambushes[0] + 0.01));

        // Brought through: a big load each way.
        var city = CityEconomy.Start(1);
        var town = book.Towns[e.To];
        var before = town.Makes.Sum(g => city.Stock[g]);
        Assert.Contains(e.ToName, book.Delivered(city, 2, new Rng(1)));
        Assert.True(town.Makes.Sum(g => city.Stock[g]) > before);
        Assert.Equal(1, book.Guarded);
        Assert.Null(book.Guarding);

        book.Guarding = low.Last();
        book.Overrun();
        Assert.Equal(1, book.Lost);
        Assert.Null(book.Guarding);
    }

    [Fact]
    public void A_glut_opens_work_carting_it_where_it_is_wanted()
    {
        var w = WorldGen.Default;
        var book = new TradeBook();
        var city = CityEconomy.Start(1);
        var places = w.SettlementsNear(0, 0, 3000).Where(TownMarket.Trades).OrderBy(s => s.DistanceTo(0, 0)).Take(4).ToList();
        foreach (var s in places) book.Market(s, 1);
        Assert.Empty(book.Gluts(city));

        // Somebody sells the first village a hundred fish.
        var town = book.Towns[places[0].Id];
        var before = town.Asks(Good.Fish);
        town.Sell(Good.Fish, 100);
        Assert.True(town.Glut(Good.Fish) && town.Asks(Good.Fish) < before);
        var job = Assert.Single(book.Gluts(city), j => j.From == town.Id && j.Good == Good.Fish);
        Assert.NotEqual(town.Id, job.To);
        Assert.InRange(job.N, 1, 12);
        Assert.True(job.Pay > 15);

        // Carted away: out of the glut, into the place that wanted it, and paid.
        var run = book.Take(job);
        Assert.True(run.Glut && !run.Loaded);
        Assert.Equal(0, book.DeliverGlut(city, 2)); // not loaded yet
        town.Catch(2);
        var glut = town.Stock[Good.Fish];
        book.LoadGlut(city, 2);
        Assert.Equal(glut - job.N, town.Stock[Good.Fish], 3);
        var to = job.To == TradeBook.CityId ? city.Stock[Good.Fish] : book.Towns[job.To].Stock[Good.Fish];
        Assert.Equal(job.Pay, book.DeliverGlut(city, 2));
        var after = job.To == TradeBook.CityId ? city.Stock[Good.Fish] : book.Towns[job.To].Stock[Good.Fish];
        Assert.True(after > to);
        Assert.Null(book.Run);
        Assert.Equal(1, book.GlutsMoved);

        // Tristinwold itself can drown in something too.
        city.Stock[Good.Timber] = CityEconomy.Info(Good.Timber).Target * 3;
        Assert.Contains(book.Gluts(city), j => j.From == TradeBook.CityId && j.Good == Good.Timber);
    }
}
