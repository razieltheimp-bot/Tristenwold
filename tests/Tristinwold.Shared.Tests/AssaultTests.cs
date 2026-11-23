using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

public class AssaultTests
{
    static readonly ServerOptions Quiet = new() { RewindChance = 0, InterceptChance = 0, VannEnabled = false, KeystoneMeetChance = 0 };

    static Hello Hi(string player) => new(player, "", Wire.ProtocolVersion, [$"{player}-A"]);

    const string Capital = "city:tristinwold";

    [Fact]
    public void Ronacse_hunts_only_heroes_near_his_own_strength()
    {
        Assert.False(Assaults.PrideHunts(4, 10));
        Assert.True(Assaults.PrideHunts(5, 10));
        Assert.False(Assaults.PrideHunts(10, 326)); // Vann-Magus, and the Ronacse that killed him
        Assert.True(Assaults.PrideHunts(316, 326));
    }

    [Fact]
    public void A_deviant_far_above_the_hero_stays_out_of_their_fights()
    {
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 1);
        server.Connect(Hi("Joe"));
        server.Handle("Joe", new ReportBattle(new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, "Draugr", "Draugr Wight", 900, Killed: false, Escaped: false)], ["Joe"], KillerSlot: 0)));
        var deviant = Assert.Single(server.World.Living);
        deviant.Level = 20;
        FormationSlot[] fight = [new(0, "Draugr", "Draugr Wight")];

        var weak = Enumerable.Range(0, 40).SelectMany(_ => server.Handle("Joe", new EncounterStarted("The Docks", fight, Level: 3))).ToList();
        Assert.DoesNotContain(weak, o => o.Message is DeviantAppears);
        Assert.Equal(3, server.State.Groups["Joe"].Level);

        var strong = Enumerable.Range(0, 40).SelectMany(_ => server.Handle("Joe", new EncounterStarted("The Docks", fight, Level: 30))).ToList();
        Assert.Contains(strong, o => o.Message is DeviantAppears);

        // An older game doesn't say: no limit, as before.
        var old = Enumerable.Range(0, 40).SelectMany(_ => server.Handle("Joe", new EncounterStarted("The Docks", fight))).ToList();
        Assert.Contains(old, o => o.Message is DeviantAppears);
    }

    [Fact]
    public void With_no_hero_worth_his_time_Ronacse_lays_siege_to_a_town_that_everyone_can_wear_down()
    {
        var city = CityEconomy.Start(1);
        city.PrideWins = 50; // level 110
        city.TendAssaults(0, []);
        Assert.Empty(city.Assaults); // nobody playing
        city.TendAssaults(105, []);
        Assert.Empty(city.Assaults); // a hero worth hunting: he hunts them instead

        city.TendAssaults(8, []);
        var siege = Assert.Single(city.Assaults);
        Assert.True(siege.IsPride);
        Assert.Equal(Capital, siege.Town); // the only town this city knows yet
        Assert.True(city.TradeShut(Capital));
        Assert.Contains(city.News, n => n.Contains("Ronacse") && n.Contains("siege"));
        Assert.Contains(city.Rumours, r => r.Did.Contains("Ronacse") && r.Bad);

        // Every hero's blows add up, no one report too much at once, never past his second form.
        Assert.True(city.Apply(CityAction.AssaultHurt(Capital, 0.9)));
        Assert.Equal(Assaults.MostBlow, siege.Hurt, 6);
        for (var i = 0; i < 5; i++) city.Apply(CityAction.AssaultHurt(Capital, 0.5));
        Assert.Equal(2, siege.Hurt, 6);
        Assert.False(city.Apply(CityAction.AssaultHurt(Capital, double.NaN)));

        Assert.True(city.Apply(CityAction.AssaultBroken(Capital, "Joe")));
        Assert.Empty(city.Assaults);
        Assert.Equal(1, city.PrideDeaths);
        Assert.True(city.PrideDown);
        Assert.False(city.TradeShut(Capital));
        Assert.Contains(city.News, n => n.Contains("Joe has broken"));
    }

    [Fact]
    public void A_siege_nobody_breaks_sacks_the_town_and_breaks_a_seal()
    {
        var city = CityEconomy.Start(2);
        city.PrideWins = 50;
        city.TendAssaults(8, []);
        var burnt = city.FabricOf(Capital).Burnt;
        var treasury = city.Treasury;
        city.Day += Assaults.Days;
        city.TendAssaults(8, []);
        Assert.Empty(city.Assaults);
        Assert.Equal(1, city.SealsBroken);
        Assert.Equal(burnt + CityEconomy.SiegeBurns + CityEconomy.SackBurns, city.FabricOf(Capital).Burnt);
        Assert.True(city.Treasury < treasury || treasury == 0);
        Assert.Contains(city.Rumours, r => r.Key.EndsWith("|sacked"));
        city.TendAssaults(8, []);
        Assert.Empty(city.Assaults); // not straight back: he rests between
    }

    [Fact]
    public void A_deviant_past_every_hero_takes_its_band_to_a_town_and_leaves_when_it_dies()
    {
        var city = CityEconomy.Start(3);
        AssaultCandidate stray = new("a", "Mog the Small", "Draugr", 20, 0, 20), chief = new("b", "Grimgor Bonehand", "Draugr", 20, 1, 23);
        city.TendAssaults(30, [stray, chief]);
        Assert.Empty(city.Assaults); // a hero playing who's a match for it
        city.TendAssaults(5, [stray]);
        Assert.Empty(city.Assaults); // a stray doesn't lead a band on a town

        city.TendAssaults(5, [stray, chief]);
        var held = Assert.Single(city.Assaults);
        Assert.Equal("b", held.By);
        Assert.Equal("Draugr", held.Kind);
        Assert.True(city.TradeShut(held.Town));

        city.TendAssaults(5, [stray]); // killed, by the roster's word
        Assert.Empty(city.Assaults);
        Assert.False(city.TradeShut(held.Town));

        var again = CityEconomy.Start(4);
        again.TendAssaults(5, [chief]);
        again.Day += Assaults.Days;
        var sacked = again.TendAssaults(5, [chief]);
        Assert.Equal("b", Assert.Single(sacked).By);
    }

    [Fact]
    public void A_hunter_too_strong_for_its_quarry_takes_their_town_instead()
    {
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 6);
        server.Connect(Hi("Joe"));
        server.Handle("Joe", new ReportBattle(new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, "Draugr", "Draugr Wight", 900, Killed: false, Escaped: false)], ["Joe"], KillerSlot: 0)));
        var deviant = Assert.Single(server.World.Living);
        deviant.Level = 20;
        CityTrade Begin(string by, string name) => new(CityAction.AssaultBegun(new Assault { Town = Capital, By = by, Name = name, Kind = "Draugr", Level = 999, Rank = 3 }, "Joe"));

        // Joe's a match for it (within 15 levels): it hunts him as before, and no town's taken.
        server.Handle("Joe", new Presence("The Docks", ["Joe-A"], Level: 10));
        server.Handle("Joe", Begin(deviant.Id.ToString(), "Anything"));
        Assert.Empty(server.City.Assaults);

        // Far beneath it: it takes his town, under its own name and strength whatever his game said.
        server.Handle("Joe", new Presence("The Wilds", ["Joe-A"], Level: 3));
        server.Handle("Joe", Begin(Guid.NewGuid().ToString(), "Nobody")); // no such deviant
        Assert.Empty(server.City.Assaults);
        server.Handle("Joe", Begin(deviant.Id.ToString(), "Anything"));
        var held = Assert.Single(server.City.Assaults);
        Assert.Equal((deviant.FullName, 20, Capital), (held.Name, held.Level, held.Town));
        Assert.True(server.City.TradeShut(Capital));
        Assert.Contains(server.City.News, n => n.Contains("Joe's company") && n.Contains("instead"));
        server.Handle("Joe", Begin(deviant.Id.ToString(), "Anything"));
        Assert.Single(server.City.Assaults); // one town to a deviant
    }

    [Fact]
    public void On_the_server_the_siege_starts_on_its_own_clock_and_everyone_hears()
    {
        var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 5, clock: () => now);
        server.Connect(Hi("Vann-Magus"));
        server.Handle("Vann-Magus", new Presence("The Docks", ["Vann-Magus"], Level: 10));
        server.City.PrideWins = 158; // level 326
        server.Tick();
        now += TimeSpan.FromSeconds(31);
        var told = server.Tick();
        Assert.Single(server.City.Assaults);
        Assert.Contains(told, o => o.Message is Notice n && n.Text.Contains("Ronacse"));
        Assert.Contains(told, o => o.Message is CitySnapshot);
    }
}
