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
        Assert.Equal((int)Math.Round(Items.Get("iron_sword")!.Value * 1.0), city.Price(Items.Get("iron_sword")!)); // gear isn't a good
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
}
