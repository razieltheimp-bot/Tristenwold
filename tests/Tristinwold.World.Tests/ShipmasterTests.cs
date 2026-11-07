using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>An apprentice in command of a fleet ship: sailing on the hero's orders, finding new land, and working a trade route.</summary>
public class ShipmasterTests
{
    sealed class Waters
    {
        public readonly SeaBook Sea = new() { OwnShip = true, ShipName = "the Wren" };
        public readonly CityEconomy City = CityEconomy.Start(5);
        public readonly Bond Lad = new() { Name = "Tobin", Role = BondRole.Squire, Level = 8 };
        public readonly FleetShip Ship;
        public double Now = 1000;

        public Waters()
        {
            var crowns = 99999;
            Ship = Sea.BuyForFleet(ShipSize.Medium, "the Gannet", ref crowns)!;
            Ship.Hands = Ship.Crew;
        }

        public IDock? Dock(string id) => id == Seas.Tristinwold ? new HomeDock(City) : Seas.Get(id) is { } p ? new PortDock(Sea.Market(p, (int)(Now / CityEconomy.DaySeconds))) : null;

        public List<ShipNews> Tend(Rng rng) => Sea.TendFleet(Now, Dock, id => id == Lad.Id ? Lad : null, rng);

        /// <summary>Time passes till she's done whatever she's doing now.</summary>
        public List<ShipNews> Sail(Rng rng, double extra = 1)
        {
            var news = Tend(rng);
            Now = Math.Max(Now, Ship.Sailing ? Ship.Arrives : Ship.NextSail) + extra;
            news.AddRange(Tend(rng));
            return news;
        }
    }

    [Fact]
    public void The_days_between_ports_are_reckoned_across_the_water()
    {
        foreach (var p in Seas.Ports.Concat(Seas.Uncharted))
        {
            Assert.Equal(p.Leagues, Seas.Between(Seas.Tristinwold, p.Id));
            foreach (var q in Seas.Ports.Concat(Seas.Uncharted).Where(q => q != p))
            {
                Assert.Equal(Seas.Between(p.Id, q.Id), Seas.Between(q.Id, p.Id));
                Assert.InRange(Seas.Between(p.Id, q.Id), 1, p.Leagues + q.Leagues);
            }
        }
        Assert.True(Seas.Between("port:druitt", "port:greywater") < 3, "the two home ports either side of Tristinwold");
    }

    [Fact]
    public void An_apprentice_takes_command_and_is_away_from_the_company_till_brought_ashore()
    {
        var w = new Waters();
        var pet = new Bond { Name = "Rufus", Role = BondRole.Pet };
        Assert.False(SeaBook.TakeCommand(w.Ship, pet), "a dog can't captain a ship");
        Assert.True(SeaBook.TakeCommand(w.Ship, w.Lad));
        Assert.Equal(w.Ship.Name, w.Lad.Sails);
        Assert.False(w.Lad.Following);
        Assert.False(PartyRules.MayFollow([w.Lad], w.Lad, false, true), "they can't be called while they're at sea");
        Assert.False(Homestead.Free(w.Lad), "nor set to work at home");
        Assert.True(SeaBook.StakeHer(w.Ship, 400));
        Assert.Equal(400, SeaBook.StandDown(w.Ship, w.Lad, w.Dock(Seas.Tristinwold)!));
        Assert.Null(w.Lad.Sails);
        Assert.False(w.Ship.Mastered);
    }

    [Fact]
    public void Ordered_to_a_port_she_sails_there_and_trades_only_at_that_port()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        SeaBook.StakeHer(w.Ship, 1500);
        Assert.False(w.Sea.Order(w.Ship, ["port:ambermouth"], false, w.Now), "not a port her charts know");
        Assert.True(w.Sea.Order(w.Ship, ["port:greywater"], false, w.Now));
        var city = w.City.Stock.ToDictionary();
        w.Tend(new Rng(3));
        Assert.True(w.Ship.Sailing && w.Ship.Away, "she casts off on the tide");
        Assert.Equal("port:greywater", w.Ship.Bound);
        Assert.Equal(w.Now + CityEconomy.DaySeconds / w.Ship.Speed, w.Ship.Arrives, 3);
        Assert.True(w.Ship.Loaded > 0, $"she loads at the Exchange what Greywater pays well for ({string.Join(", ", w.Ship.Cargo)})");
        Assert.True(w.Ship.Cargo.Keys.All(g => w.City.Stock[g] < city[g]), "out of Tristinwold's own stores");
        var bought = w.Ship.Cargo.ToDictionary();

        var druitt = w.Sea.Market(Seas.Get("port:druitt")!, 0).Stock.ToDictionary();
        w.Now = w.Ship.Arrives + 1;
        w.Tend(new Rng(3));
        Assert.Equal("port:greywater", w.Ship.At);
        Assert.False(w.Ship.Sailing);
        var greywater = w.Sea.Markets["port:greywater"];
        Assert.True(bought.Keys.All(g => greywater.Stock[g] > greywater.Usual(g)), "what she brought is in Greywater's market now");
        Assert.Equal(druitt, w.Sea.Markets["port:druitt"].Stock.ToDictionary()); // and nowhere else's
        Assert.Empty(w.Ship.Plan);
        Assert.Contains(w.Ship.Log, l => l.Contains("Waiting at Greywater"));

        // She waits there till she's told otherwise, and doesn't trade again.
        var stock = greywater.Stock.ToDictionary();
        w.Now += 600;
        w.Tend(new Rng(3));
        Assert.Equal("port:greywater", w.Ship.At);
        Assert.Equal(stock, greywater.Stock.ToDictionary());
    }

    [Fact]
    public void Home_again_she_hands_over_everything_over_her_stake()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        SeaBook.StakeHer(w.Ship, 1500);
        w.Sea.Order(w.Ship, ["port:druitt", Seas.Tristinwold], false, w.Now);
        var news = new List<ShipNews>();
        for (var i = 0; i < 6 && (w.Ship.Away || w.Ship.Plan.Count > 0); i++) news.AddRange(w.Sail(new Rng(11)));
        Assert.False(w.Ship.Away);
        Assert.Empty(w.Ship.Plan);
        var home = news.Sum(n => n.Crowns);
        Assert.True(w.Ship.Purse <= w.Ship.Stake, $"whatever was over her stake went to the hero ({home} home)");
        Assert.True(w.Lad.Xp > 0 || w.Lad.Level > 8, "her master learns from every voyage");
    }

    [Fact]
    public void Sent_past_the_charts_her_master_finds_new_land_for_good()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        var edge = w.Sea.Unfound!;
        Assert.True(w.Sea.Order(w.Ship, [Seas.Beyond], false, w.Now));
        w.Tend(new Rng(5));
        Assert.True(w.Ship.Charting);
        Assert.Equal(0, w.Ship.Loaded); // nothing to sell where nobody knows what they want
        var news = w.Sail(new Rng(5));
        Assert.Contains(edge.Id, w.Sea.Charted);
        Assert.Equal(edge.Id, w.Ship.At);
        Assert.Contains(news, n => n.Text.Contains(edge.Name));
        Assert.Contains(w.Sea.Known, p => p.Id == edge.Id);
        Assert.NotEqual(edge, w.Sea.Unfound);
        Assert.True(w.Sea.Order(w.Ship, [edge.Id, Seas.Tristinwold], true, w.Now), "and it can go on a route now");
    }

    [Fact]
    public void A_trade_route_goes_round_and_round_and_makes_money()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        SeaBook.StakeHer(w.Ship, 2000);
        Assert.False(w.Sea.Order(w.Ship, ["port:druitt"], true, w.Now), "a route wants two stops");
        Assert.True(w.Sea.Order(w.Ship, [Seas.Tristinwold, "port:druitt", "port:greywater"], true, w.Now));
        var news = new List<ShipNews>();
        var stops = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            news.AddRange(w.Sail(new Rng((ulong)(40 + i))));
            if (!w.Ship.Sailing) stops.Add(w.Ship.Where);
        }
        Assert.True(w.Ship.OnRoute, "she keeps at it");
        Assert.Contains("port:druitt", stops);
        Assert.Contains("port:greywater", stops);
        Assert.Contains(Seas.Tristinwold, stops);
        Assert.True(w.Ship.Made > 0, string.Join(" | ", w.Ship.Log) + $"a route pays ({news.Sum(n => n.Crowns)} home, {w.Ship.Purse} in her purse)");
        Assert.True(w.Ship.Log.Count > 3);
    }

    [Fact]
    public void Orders_under_sail_wait_for_the_next_port()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        w.Sea.Order(w.Ship, ["port:druitt"], false, w.Now);
        w.Tend(new Rng(2));
        var due = w.Ship.Arrives;
        w.Sea.Order(w.Ship, ["port:greywater"], false, w.Now + 10);
        Assert.Equal("port:druitt", w.Ship.Bound);
        Assert.Equal(due, w.Ship.Arrives);
        w.Now = due + 1;
        w.Tend(new Rng(2));
        Assert.Equal("port:druitt", w.Ship.At);
        Assert.Single(w.Ship.Plan); // still bound for Greywater once she's done here
        w.Sail(new Rng(2));
        w.Sail(new Rng(2));
        Assert.Equal("port:greywater", w.Ship.At);
    }

    [Fact]
    public void A_colony_by_the_sea_is_a_harbour_and_she_hands_over_her_takings_there()
    {
        var w = new Waters();
        var colony = Seas.Harbour("harbour:test-colony", "Gullhaven", 2200, 300, [Good.Fish, Good.Timber], [Good.Bread, Good.Iron]);
        try
        {
            Assert.Equal(2, Seas.Between(Seas.Tristinwold, colony.Id)); // as far out as it really is
            Assert.Contains(w.Sea.Known, p => p.Id == colony.Id);
            w.Sea.PaysAt = [colony.Id];
            SeaBook.TakeCommand(w.Ship, w.Lad);
            SeaBook.StakeHer(w.Ship, 1500);
            Assert.True(w.Sea.Order(w.Ship, ["port:greywater", colony.Id], true, w.Now));
            var news = new List<ShipNews>();
            for (var i = 0; i < 10; i++) news.AddRange(w.Sail(new Rng((ulong)(70 + i))));
            Assert.Contains(news, n => n.Crowns > 0 && n.Text.Contains("Gullhaven"));
            Assert.True(w.Sea.Markets.ContainsKey(colony.Id), "she trades at the colony's own market");
            Assert.False(w.Sea.Pays("port:greywater"));
            Assert.True(w.Sea.Pays(Seas.Tristinwold));
        }
        finally { Seas.Harbours.Remove(colony.Id); }
    }

    [Fact]
    public void A_landing_far_from_its_colony_has_a_wagon_road_raiders_can_rob()
    {
        Assert.Equal(0, Seas.WagonRisk(60));
        Assert.True(Seas.WagonRisk(400) > 0 && Seas.WagonRisk(2000) > Seas.WagonRisk(400) && Seas.WagonRisk(1e5) <= 0.35);
        var (robbed, paid) = (0, 0);
        for (var seed = 1UL; seed <= 40; seed++)
        {
            var w = new Waters();
            var landing = Seas.Harbour("harbour:far-colony", "Farstead Landing", 1800, 0, [Good.Fish], [Good.Bread], inland: 2400);
            w.Sea.PaysAt = [landing.Id];
            SeaBook.TakeCommand(w.Ship, w.Lad);
            SeaBook.StakeHer(w.Ship, 100);
            w.Ship.Purse += 1000; // takings aboard
            w.Sea.Order(w.Ship, [landing.Id], false, w.Now);
            var news = new List<ShipNews>();
            for (var i = 0; i < 3; i++) news.AddRange(w.Sail(new Rng(seed)));
            Seas.Harbours.Remove(landing.Id);
            paid += news.Sum(n => n.Crowns);
            if (news.Any(n => n.Text.Contains("Raiders fell on the wagons"))) robbed++;
        }
        Assert.InRange(robbed, 2, 30);
        Assert.True(paid > 0);
    }

    [Fact]
    public void Masterless_she_comes_home()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        w.Sea.Order(w.Ship, ["port:druitt"], false, w.Now);
        w.Sail(new Rng(1));
        Assert.Equal("port:druitt", w.Ship.At);
        var news = w.Sea.TendFleet(w.Now, w.Dock, _ => null, new Rng(1));
        Assert.Contains(news, n => n.Text.Contains("lost her master"));
        Assert.False(w.Ship.Mastered);
        w.Now = w.Ship.Sailing ? w.Ship.Arrives + 1 : w.Now + 1;
        w.Sea.TendFleet(w.Now, w.Dock, _ => null, new Rng(1));
        w.Now = w.Ship.Arrives + 1;
        w.Sea.TendFleet(w.Now, w.Dock, _ => null, new Rng(1));
        Assert.False(w.Ship.Away);
    }

    [Fact]
    public void Her_orders_and_log_survive_a_save()
    {
        var w = new Waters();
        SeaBook.TakeCommand(w.Ship, w.Lad);
        SeaBook.StakeHer(w.Ship, 800);
        w.Sea.Order(w.Ship, [Seas.Tristinwold, "port:druitt"], true, w.Now);
        w.Tend(new Rng(9));
        var back = System.Text.Json.JsonSerializer.Deserialize<SeaBook>(System.Text.Json.JsonSerializer.Serialize(w.Sea))!;
        var f = back.Fleet[0];
        Assert.Equal(w.Ship.Master, f.Master);
        Assert.Equal(w.Ship.Plan, f.Plan);
        Assert.True(f.Loop);
        Assert.Equal(w.Ship.Bound, f.Bound);
        Assert.Equal(w.Ship.Cargo, f.Cargo);
        Assert.Equal(w.Ship.Log, f.Log);
    }
}
