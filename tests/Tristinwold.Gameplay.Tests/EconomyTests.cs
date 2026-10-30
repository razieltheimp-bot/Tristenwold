using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

/// <summary>The town's trades (fishing, mining, woodcutting, cooking) and the city's economy.</summary>
public class EconomyTests
{
    [Fact]
    public void Every_catch_vein_and_tree_gives_real_things_and_every_traded_item_is_real()
    {
        foreach (var w in Enum.GetValues<Water>()) Assert.All(Fishing.Catches(w), c => Assert.NotNull(Items.Get(c.Id)));
        Assert.All(Gathering.Veins, v => Assert.NotNull(Items.Get(v.Ore)));
        Assert.All(Gathering.Trees, t => Assert.NotNull(Items.Get(t.Log)));
        // Every item the economy names by id is a real item, and every fish is fish.
        foreach (var w in Enum.GetValues<Water>())
            Assert.All(Fishing.Catches(w).Where(c => c.Id is not ("old_boot" or "pearl")), c => Assert.Equal(Good.Fish, CityEconomy.GoodOf(c.Id)));
        Assert.Equal(Good.Precious, CityEconomy.GoodOf("pearl"));
        Assert.Null(CityEconomy.GoodOf("old_boot"));
        Assert.Null(CityEconomy.GoodOf("iron_sword")); // gear keeps its price
        Assert.Equal(Good.Herbs, CityEconomy.GoodOf("emberleaf"));
        Assert.Equal(Good.Potions, CityEconomy.GoodOf("healing_draught"));
        Assert.Equal(Enum.GetValues<Good>().Length, CityEconomy.Goods.Count);
        Assert.All(CityEconomy.Goods, g => Assert.Equal(g, CityEconomy.Info(g.Good)));
        Assert.Contains(Recipes.For(Job.Cooking), r => r.Makes == "grilled_fish" && r.Needs[0].Id == "salmon");
        Assert.Contains(Recipes.For(Job.Smithing), r => r.Makes == "iron_ingot" && r.Needs[0].Id == "iron_ore");
    }

    [Fact]
    public void Better_anglers_wait_less_strike_easier_and_land_rarer_fish()
    {
        var rng = new Rng(5);
        Assert.True(Fishing.Window(10) > Fishing.Window(1) * 2);
        var waitNew = Enumerable.Range(0, 200).Average(_ => Fishing.BiteDelay(1, rng));
        var waitOld = Enumerable.Range(0, 200).Average(_ => Fishing.BiteDelay(10, rng));
        Assert.InRange(waitNew, 6, 11);
        Assert.InRange(waitOld, 1.5, 5);
        var beginner = Enumerable.Range(0, 500).Select(_ => Fishing.Catch(Water.Sea, 1, rng)).ToList();
        Assert.DoesNotContain("lobster", beginner); // too good for a beginner
        Assert.Contains("herring", beginner);
        var master = Enumerable.Range(0, 2000).Select(_ => Fishing.Catch(Water.Sea, 10, rng)).ToList();
        Assert.Contains("lobster", master);
        Assert.Contains("pearl", master);
        Assert.True(master.Count(f => f == "old_boot") < beginner.Count(f => f == "old_boot") * 4 / 2 + 5); // fewer boots for the skilled
        Assert.Contains("arctic_char", Enumerable.Range(0, 50).Select(_ => Fishing.Catch(Water.Cold, 1, rng)));
        Assert.Contains("perch", Enumerable.Range(0, 50).Select(_ => Fishing.Catch(Water.Fresh, 1, rng)));
    }

    [Fact]
    public void Miners_and_woodcutters_take_fewer_strokes_and_get_more_as_they_learn()
    {
        Assert.Equal(4, Gathering.Strokes(1));
        Assert.Equal(1, Gathering.Strokes(10));
        var rng = new Rng(9);
        var iron = Gathering.Vein("iron");
        var novice = Enumerable.Range(0, 300).Sum(_ => Gathering.Mine(iron, 1, rng).Where(x => x.Id == "iron_ore").Sum(x => x.N));
        var expert = Enumerable.Range(0, 300).Sum(_ => Gathering.Mine(iron, 9, rng).Where(x => x.Id == "iron_ore").Sum(x => x.N));
        Assert.True(expert > novice * 1.5, $"{novice} then {expert}");
        Assert.Contains(Enumerable.Range(0, 400).SelectMany(_ => Gathering.Mine(Gathering.Vein("silver"), 8, rng)), x => x.Id.StartsWith("rough_"));
        Assert.InRange(Enumerable.Range(0, 100).Average(_ => Gathering.Logs(1, rng)), 2, 2.4);
    }

    [Fact]
    public void Prices_follow_the_stock_and_a_hero_selling_floods_the_market()
    {
        var city = CityEconomy.Start(7);
        Assert.InRange(city.Factor(Good.Fish), 0.99, 1.01);
        var herring = Items.Get("herring")!;
        var before = city.Offer(herring);
        for (var i = 0; i < 120; i++) city.Sold("herring", city.Offer(herring));
        Assert.True(city.Factor(Good.Fish) < 0.75, $"{city.Factor(Good.Fish)}");
        Assert.True(city.Price(herring) <= herring.Value, "a glut is cheap");
        Assert.True(city.Taxes > 0);
        city.Stock[Good.Iron] = 8;
        Assert.True(city.Factor(Good.Iron) > 2, "scarce is dear");
        Assert.Equal((int)Math.Round(Items.Get("iron_sword")!.Value * 1.0), city.Price(Items.Get("iron_sword")!)); // gear isn't a good: it's priced by how many the shops hold
        city.Stock[Good.Fish] = 0;
        Assert.False(city.InStock(herring));
        Assert.True(city.InStock(Items.Get("iron_sword")!));
        Assert.True(before >= 1);
    }

    [Fact]
    public void Days_pass_events_happen_and_the_market_finds_its_level_again()
    {
        var city = CityEconomy.Start(42);
        city.Stock[Good.Fish] = 400; // a glut
        city.Stock[Good.Ore] = 0;    // and a shortage
        var news = city.Advance(CityEconomy.DaySeconds * 30 + 1);
        Assert.Equal(31, city.Day);
        Assert.NotEmpty(news);
        Assert.InRange(city.Factor(Good.Fish), 0.75, 1.35);
        Assert.InRange(city.Factor(Good.Ore), 0.6, 1.6);
        Assert.All(CityEconomy.Goods, g => Assert.True(city.Stock[g.Good] >= 0));
        Assert.All(city.Running, r => Assert.Contains(CityEconomy.Events, e => e.Id == r.Id));
        Assert.True(city.News.Count <= 12);
        // It's the same city for the same seed.
        var twin = CityEconomy.Start(42);
        twin.Stock[Good.Fish] = 400;
        twin.Stock[Good.Ore] = 0;
        twin.Advance(CityEconomy.DaySeconds * 30 + 1);
        Assert.Equal(city.Stock[Good.Iron], twin.Stock[Good.Iron], 6);
    }

    [Fact]
    public void A_storm_makes_fish_dear_and_a_caravan_brings_iron()
    {
        var city = CityEconomy.Start(1);
        city.Running.Add(new RunningEvent { Id = "storm", DaysLeft = 2 });
        for (var i = 0; i < 2; i++) city.NextDay();
        Assert.True(city.Factor(Good.Fish) > 1.1, $"{city.Factor(Good.Fish)}");
        var iron = city.Stock[Good.Iron];
        city.Stock[Good.Iron] += CityEconomy.Info(Good.Iron).Target * CityEconomy.Event("caravan").Arrives!.First(a => a.Good == Good.Iron).Share;
        Assert.True(city.Stock[Good.Iron] > iron);
    }

    [Fact]
    public void Contracts_are_posted_for_what_is_short_and_pay_over_the_odds()
    {
        var city = CityEconomy.Start(3);
        city.Stock[Good.Timber] = 10;
        city.Contracts.Clear();
        city.NextDay();
        var timber = city.Contracts.FirstOrDefault(c => c.Good == Good.Timber);
        Assert.NotNull(timber);
        Assert.True(timber.PricePer > Items.Get("pine_log")!.Value, "over the odds");
        Assert.True(city.Contracts.Count <= 4);
        var crowns = city.Deliver(timber, Enumerable.Repeat("pine_log", timber.Wanted).Append("herring"));
        Assert.Equal(timber.Wanted * timber.PricePer + timber.Bonus, crowns);
        Assert.DoesNotContain(timber, city.Contracts); // done
        Assert.True(city.Renown > 0);
        Assert.Equal(0, city.Deliver(new Contract { Good = Good.Iron, Wanted = 5 }, ["herring"])); // the wrong goods
    }

    [Fact]
    public void Works_are_built_from_donations_and_make_a_trade_better()
    {
        var city = CityEconomy.Start(5);
        var work = city.Work!;
        Assert.Equal("harbour_wall", work.Id);
        var fishBefore = city.Stock[Good.Fish];
        var taken = city.Donate(Enumerable.Repeat("stone_block", 100).Concat(Enumerable.Repeat("oak_log", 100)), out var done);
        Assert.Equal(90, taken); // 60 stone and 30 timber, no more
        Assert.Null(done);       // still the coin to find
        Assert.Equal(200, city.DonateCrowns(1000, out done));
        Assert.NotNull(done);
        Assert.Contains("harbour_wall", city.WorksDone);
        Assert.Equal("shore_mine", city.Work!.Id);
        Assert.Equal(4, city.DockWage);
        Assert.True(city.RenownTier >= 2, city.RenownTitle);
        Assert.True(city.Discount > 0);
        _ = fishBefore;
    }

    [Fact]
    public void The_city_saves_and_loads_with_the_company()
    {
        var city = CityEconomy.Start(8);
        city.Advance(CityEconomy.DaySeconds * 3 + 1);
        city.Renown = 33;
        var json = System.Text.Json.JsonSerializer.Serialize(city);
        var back = System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(json)!;
        Assert.Equal(city.Day, back.Day);
        Assert.Equal(city.Stock[Good.Fish], back.Stock[Good.Fish]);
        Assert.Equal(city.Contracts.Count, back.Contracts.Count);
        Assert.Equal(33, back.Renown);
        Assert.Equal(city.NextContract, back.NextContract);
        Assert.Equal(city.Report(), back.Report());
    }

    [Fact]
    public void A_hundred_of_anything_sold_makes_it_cheap_for_a_couple_of_days_then_the_shops_sell_them_down()
    {
        var city = CityEconomy.Start(9);
        var sword = Items.Get("iron_sword")!;
        var full = city.Price(sword);
        for (var i = 0; i < 100; i++) city.Sold("iron_sword", city.Offer(sword));
        Assert.True(city.Price(sword) <= full * 0.5, $"{full} then {city.Price(sword)}");
        Assert.True(city.Offer(sword) < full / 2);
        var prices = new List<int>();
        for (var day = 1; day <= 5; day++) { city.Advance(CityEconomy.DaySeconds * day + 1); prices.Add(city.Price(sword)); }
        Assert.True(prices[0] < full * 0.7, "still cheap the next day");
        Assert.True(prices[1] < full * 0.9, "and the day after");
        Assert.True(prices[4] >= full * 0.95, $"back to normal in a few days: {string.Join(",", prices)} of {full}");
        // Buying the shops out of something makes it dear, and that eases too.
        for (var i = 0; i < 30; i++) city.Bought("iron_sword", city.Price(sword));
        Assert.True(city.Price(sword) > full * 1.5, $"{city.Price(sword)}");
        city.Advance(CityEconomy.DaySeconds * 9 + 1);
        Assert.InRange(city.Price(sword), full - 1, full + 1);
        // A cartload good floods the same way, and the merchants clear it in a few days.
        var fishFull = city.Price(Items.Get("herring")!);
        for (var i = 0; i < 100; i++) city.Sold("herring", 1);
        Assert.True(city.Factor(Good.Fish) < 0.7);
        city.Advance(CityEconomy.DaySeconds * 14 + 1);
        Assert.InRange(city.Factor(Good.Fish), 0.85, 1.15);
        Assert.True(fishFull >= 1);
    }

    static TownMarket Town(string id, double x, double z, params Good[] makes) => new()
    {
        Id = id, Name = id, X = x, Z = z, Size = 0.35, Makes = [.. makes],
        Wants = [.. Enum.GetValues<Good>().Where(g => !makes.Contains(g)).Take(3)],
    };

    [Fact]
    public void The_citys_caravans_run_by_the_prices_and_a_busy_road_with_a_clan_about_loses_wagons_to_a_camp_the_yard_pays_to_clear()
    {
        var city = CityEconomy.Start(5);
        var roads = city.Roads;
        var mill = Town("mill", 900, 0, Good.Timber, Good.Bread);
        foreach (var g in Enum.GetValues<Good>()) mill.Stock[g] = mill.Usual(g);
        city.Apply(CityAction.Know(mill));
        Assert.Single(roads.Known);
        Assert.False(city.Apply(CityAction.Know(mill)), "knowing a place twice is nothing");

        // The mill has timber to spare and the city is short of it: wagons go, the more the bigger the gap.
        city.Stock[Good.Timber] = 10;
        var timber = city.Stock[Good.Timber];
        var news = roads.TradeDay(city, 2, new Rng(1));
        Assert.True(roads.Wagons >= 1, "a wagon went");
        Assert.True(city.Stock[Good.Timber] > timber || news.Any(n => n.Contains("sacked")), string.Join(" | ", news));
        Assert.Equal(1.0, roads.PayMult("mill", 2));
        Assert.Null(roads.KnownDanger("mill", 2));

        // A clan in the mill's country: the road's known trouble, the pay goes up, and sooner or later a caravan is lost.
        city.Apply(CityAction.Raiders("mill", "the Bone Fang"));
        Assert.True(roads.PayMult("mill", city.Day) > 1.2);
        Assert.Contains("the Bone Fang about", roads.KnownDanger("mill", city.Day));
        var sacked = false;
        for (var day = 3; day < 40 && !sacked; day++)
        {
            city.Apply(CityAction.Raiders("mill", "the Bone Fang"));
            city.Stock[Good.Timber] = 10;
            sacked = roads.TradeDay(city, day, new Rng((ulong)day)).Any(n => n.Contains("sacked on the road by the Bone Fang"));
        }
        Assert.True(sacked, "a busy road with raiders about loses a wagon");
        var t = Assert.Single(roads.Troubles);
        Assert.True(t.Clan && t.By == "the Bone Fang" && t.Bounty > 40 && t.Bandits >= 4);
        Assert.InRange(Math.Sqrt(t.X * t.X + t.Z * t.Z), 250, 700); // camped a way out along the road
        Assert.Contains("wagon lost on it this week", roads.KnownDanger("mill", t.Day));
        Assert.True(roads.PayMult("mill", t.Day) >= 1.9, $"{roads.PayMult("mill", t.Day)}");
        Assert.Equal(1, roads.ExtraBandits("mill"));
        Assert.Contains("Trouble: the Bone Fang on the road to mill", roads.Report(t.Day));

        // Cleared: paid for in news, the road's clear, the pay settles.
        var before = city.News.Count;
        city.Apply(CityAction.Cleared(t.Id, "Joe"));
        Assert.Empty(roads.Troubles);
        Assert.Contains("Joe's company cleared the Bone Fang off the road to mill", city.News[before]);
        Assert.DoesNotContain("the the", city.News[before]);
        Assert.DoesNotContain("wagon lost", roads.KnownDanger("mill", t.Day) ?? "");
        // Left alone, a camp moves on after a while.
        roads.Troubles.Add(new RoadTrouble { Id = "x", Town = "mill", TownName = "mill", Day = 1 });
        var later = roads.TradeDay(city, 1 + Roads.TroubleDays + 1, new Rng(4));
        Assert.Contains(later, n => n.Contains("have moved on"));
        Assert.Empty(roads.Troubles);
    }

    [Fact]
    public void Goods_moved_on_the_roads_by_a_hero_are_done_to_the_shared_city()
    {
        var city = CityEconomy.Start(6);
        var farm = Town("farm", 400, 300, Good.Bread, Good.Meat);
        foreach (var g in Enum.GetValues<Good>()) farm.Stock[g] = farm.Usual(g);
        city.Apply(CityAction.Know(farm));
        var bread = city.Roads.Towns["farm"].Stock[Good.Bread];
        city.Apply(CityAction.TownStock("farm", Good.Bread, -5));
        Assert.Equal(bread - 5, city.Roads.Towns["farm"].Stock[Good.Bread]);
        city.Apply(CityAction.TownStock("farm", Good.Bread, 500)); // clamped: a wagon at most
        Assert.Equal(bread - 5 + 60, city.Roads.Towns["farm"].Stock[Good.Bread]);
        var iron = city.Stock[Good.Iron];
        city.Apply(CityAction.CityStock(Good.Iron, -12));
        Assert.Equal(iron - 12, city.Stock[Good.Iron]);
        city.Apply(CityAction.Plundered("farm"));
        Assert.True(city.Roads.Towns["farm"].Raided == 1 && city.Roads.Towns["farm"].Stock[Good.Bread] < bread);
        Assert.True(city.Apply(CityAction.Steadied("farm")));
        var news = city.News.Count;
        city.Apply(CityAction.Escorted("farm"));
        Assert.Contains("The caravan you guarded to farm", city.News[news]);
    }
}
