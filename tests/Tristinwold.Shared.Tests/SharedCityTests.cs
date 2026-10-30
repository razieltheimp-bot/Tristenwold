using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>One Tristinwold for everybody on a server: its stock, prices, days, contracts and works.</summary>
public class SharedCityTests
{
    static Hello Hi(string player) => new(player, "", Wire.ProtocolVersion, [$"{player}-A", $"{player}-B", $"{player}-C"]);

    static IEnumerable<T> To<T>(IEnumerable<Outgoing> outs, string player) where T : Message =>
        outs.Where(o => o.To is null ? !string.Equals(o.Except, player, StringComparison.OrdinalIgnoreCase) : o.To == player)
            .Select(o => o.Message).OfType<T>();

    static (ServerCore Server, Func<DateTime> Clock, Action<double> Wait) Server()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var server = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, RewindChance = 0, KeystoneMeetChance = 0 }, seed: 3, clock: () => now);
        return (server, () => now, seconds => now = now.AddSeconds(seconds));
    }

    [Fact]
    public void Every_company_joins_the_same_city_and_sees_what_the_others_sell()
    {
        var (server, _, wait) = Server();
        var joe = To<CitySnapshot>(server.Connect(Hi("Joe")), "Joe").Single().City;
        var mira = To<CitySnapshot>(server.Connect(Hi("Mira")), "Mira").Single().City;
        Assert.Equal(joe.Day, mira.Day);
        Assert.Equal(joe.Stock[Good.Fish], mira.Stock[Good.Fish]);

        server.Tick();
        var fish = server.City.Factor(Good.Fish);
        for (var i = 0; i < 60; i++) server.Handle("Joe", new CityTrade(CityAction.Sold("herring", 3)));
        wait(1);
        var after = To<CitySnapshot>(server.Tick(), "Mira").Single().City;
        Assert.True(after.Factor(Good.Fish) < fish * 0.85, $"Joe's sixty herring make fish cheaper for Mira too ({fish:0.00} to {after.Factor(Good.Fish):0.00})");

        wait(1);
        Assert.Empty(server.Tick()); // nothing changed: nothing sent
    }

    [Fact]
    public void Contracts_and_the_works_are_filled_together()
    {
        var (server, _, _) = Server();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        var city = server.City;
        city.Stock[Good.Timber] = 5;
        city.Contracts.Clear();
        city.NextDay();
        var contract = city.Contracts.First(c => c.Good == Good.Timber);
        var half = contract.Wanted / 2;
        server.Handle("Joe", new CityTrade(CityAction.Deliver(contract.Id, [.. Enumerable.Repeat("pine_log", half)])));
        server.Handle("Mira", new CityTrade(CityAction.Deliver(contract.Id, [.. Enumerable.Repeat("pine_log", contract.Wanted)])));
        Assert.DoesNotContain(contract, city.Contracts); // Mira finished what Joe started, and no more was taken than wanted
        Assert.Equal(contract.Wanted, contract.Delivered);

        var work = city.Work!;
        var news = server.Handle("Mira", new CityTrade(CityAction.DonateCrowns(work.Crowns)));
        foreach (var (good, n) in work.Needs)
            news = server.Handle("Joe", new CityTrade(CityAction.Donate([.. Enumerable.Repeat(Probe(good), n)])));
        Assert.Contains(work.Id, city.WorksDone);
        Assert.Contains(To<Notice>(news, "Mira"), n => n.Text.Contains(work.Title)); // Joe's gift finished it: Mira hears
        Assert.Empty(To<Notice>(news, "Joe"));
    }

    static string Probe(Good g) => g switch { Good.Stone => "stone_block", Good.Timber => "pine_log", Good.Iron => "iron_ingot", Good.Coal => "coal", Good.Precious => "pearl", _ => "herring" };

    [Fact]
    public void Days_pass_while_anyone_plays_and_stop_when_nobody_does()
    {
        var (server, _, wait) = Server();
        server.Connect(Hi("Joe"));
        server.Tick();
        var day = server.City.Day;
        for (var i = 0; i < 11; i++) { wait(60); server.Tick(); }
        Assert.Equal(day + 1, server.City.Day);

        server.Disconnect("Joe");
        for (var i = 0; i < 30; i++) { wait(60); server.Tick(); }
        Assert.Equal(day + 1, server.City.Day);

        // A server stalled for an hour doesn't run six days at once when it wakes.
        server.Connect(Hi("Joe"));
        server.Tick();
        wait(3600);
        server.Tick();
        Assert.Equal(day + 1, server.City.Day);
    }

    [Fact]
    public void A_game_cannot_do_more_to_the_city_than_a_hero_could()
    {
        var (server, _, _) = Server();
        server.Connect(Hi("Joe"));
        var city = server.City;
        var treasury = city.Treasury;
        var fish = city.Stock[Good.Fish];
        server.Handle("Joe", new CityTrade(CityAction.Treasury(int.MaxValue)));
        server.Handle("Joe", new CityTrade(CityAction.Cargo(Good.Fish, 1_000_000)));
        server.Handle("Joe", new CityTrade(CityAction.Cargo((Good)99, 5)));
        server.Handle("Joe", new CityTrade(CityAction.Deliver("no-such-contract", ["herring"])));
        server.Handle("Joe", new CityTrade(new CityAction("burn_it_down")));
        Assert.Equal(treasury + 5000, city.Treasury);
        Assert.Equal(fish + 10, city.Stock[Good.Fish]);
        Assert.Equal(13, city.Stock.Count);
    }

    [Fact]
    public void The_city_crosses_the_wire_and_is_kept_with_the_campaign()
    {
        var (server, _, _) = Server();
        server.Connect(Hi("Joe"));
        server.Handle("Joe", new CityTrade(CityAction.Sold("herring", 3)));
        var trade = (CityTrade)Wire.Decode(Wire.Encode(new CityTrade(CityAction.Deliver("c1", ["herring", "cod"]))));
        Assert.Equal(["herring", "cod"], trade.Action.Items!);
        var snapshot = (CitySnapshot)Wire.Decode(Wire.Encode(new CitySnapshot(server.City, 42)));
        Assert.Equal(server.City.Stock[Good.Fish], snapshot.City.Stock[Good.Fish]);
        Assert.Equal(42, snapshot.Seconds);
        Assert.Equal(server.City.Contracts.Select(c => c.Id), snapshot.City.Contracts.Select(c => c.Id));

        var path = Path.Combine(Path.GetTempPath(), $"city-{Guid.NewGuid():N}.json");
        try
        {
            server.State.Save(path);
            var loaded = CampaignState.Load(path);
            Assert.Equal(server.City.Stock[Good.Fish], loaded.City!.Stock[Good.Fish]);
            Assert.Equal(server.City.Day, loaded.City.Day);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_roads_are_everyones_a_place_one_company_finds_is_on_every_companys_map_and_its_caravans_run_on_the_server()
    {
        var (server, _, wait) = Server();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        var mill = new TownMarket { Id = "hamlet:mill", Name = "Millford", X = 800, Z = 100, Size = 0.35, Makes = [Good.Timber], Wants = [Good.Iron, Good.Fish, Good.Ale] };
        foreach (var g in Enum.GetValues<Good>()) mill.Stock[g] = mill.Usual(g);
        server.Handle("Joe", new CityTrade(CityAction.Know(mill)));
        // Mira hears of it with the next city.
        wait(1.5);
        var outs = server.Tick();
        var seen = To<CitySnapshot>(outs, "Mira").Last().City;
        Assert.True(seen.Roads.Towns.ContainsKey("hamlet:mill"), "the place is on the shared roads");
        Assert.Equal([Good.Timber], seen.Roads.Towns["hamlet:mill"].Makes);
        // A day on the server: the city's caravans run to it, and everyone hears.
        var day = new List<Outgoing>();
        for (var i = 0; i < 12; i++) { wait(60); day.AddRange(server.Tick()); }
        Assert.Contains(To<Notice>(day, "Mira"), n => n.Text.Contains("A caravan from Millford") || n.Text.Contains("wagon"));
        Assert.True(To<CitySnapshot>(day, "Joe").Last().City.Roads.Wagons >= 1);
        // Goods a hero moves out there, and a camp cleared, are done to everyone's roads.
        server.Handle("Mira", new CityTrade(CityAction.TownStock("hamlet:mill", Good.Iron, 7)));
        var city = server.City;
        Assert.True(city.Roads.Towns["hamlet:mill"].Stock[Good.Iron] >= 7);
        city.Roads.Troubles.Add(new RoadTrouble { Id = "road1", Town = "hamlet:mill", TownName = "Millford", Day = city.Day, Bounty = 60 });
        var cleared = server.Handle("Joe", new CityTrade(CityAction.Cleared("road1", "Joe")));
        Assert.Empty(city.Roads.Troubles);
        Assert.Contains(To<Notice>(cleared, "Mira"), n => n.Text.Contains("Joe's company cleared the bandits off the road to Millford"));
    }
}
