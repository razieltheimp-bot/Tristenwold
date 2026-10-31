using System.Text.Json;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>Sailing to the ports over the sea, the pirates on the way, the ranks up to captain, and the shipyard.</summary>
public class SeafaringTests
{
    /// <summary>A sailor whose company has charted every old port: these tests are about the voyages, not the finding.</summary>
    static HashSet<string> Everywhere() => [.. Seas.Ports.Select(p => p.Id)];

    static SeaPort Port(string id) => Seas.Get(id)!;

    [Fact]
    public void The_ports_lie_further_and_further_out_through_worse_and_worse_water()
    {
        Assert.True(Seas.Ports.Count >= 5);
        Assert.Equal(Seas.Ports.OrderBy(p => p.Leagues).Select(p => p.Id), Seas.Ports.Select(p => p.Id));
        Assert.True(Seas.Ports.Zip(Seas.Ports.Skip(1)).All(p => p.First.Pirates <= p.Second.Pirates));
        Assert.All(Seas.Ports, p => Assert.Empty(p.Makes.Intersect(p.Wants)));
        var m = Port("port:vosk").Market(1);
        Assert.True(m.Factor(Good.Iron) < 1 && m.Factor(Good.Bread) > 1, "a smelting town sells iron cheap and pays dear for bread");
    }

    [Fact]
    public void A_deckhand_sails_the_near_ports_and_a_voyage_runs_out_in_port_and_home()
    {
        var sea = new SeaBook { Charted = Everywhere() };
        Assert.Null(sea.Sail(Port("port:saltmere"), new Rng(1))); // sign on first
        Assert.True(sea.SignOn());
        Assert.Equal(SeaRank.Deckhand, sea.Rank);
        Assert.DoesNotContain(sea.Ports, p => p.Id == "port:ambermouth");
        Assert.Null(sea.Sail(Port("port:ambermouth"), new Rng(1)));

        var v = sea.Sail(Port("port:brinehollow"), new Rng(7))!;
        Assert.Null(sea.Sail(Port("port:saltmere"), new Rng(1))); // one voyage at a time
        Assert.Equal(sea.PayFor(Port("port:brinehollow")), v.Pay);
        Assert.Equal(0, v.ChoresDue); // the deck work comes as the voyage goes
        Assert.Equal(2 * CityEconomy.DaySeconds, v.LegSeconds); // two days' sailing: twenty minutes each way
        Assert.Equal((0, (SeaRank?)null), sea.Home(1)); // not home before going
        Assert.False(sea.CastOff());
        Assert.True(sea.Arrive());
        Assert.False(sea.Arrive());
        Assert.True(sea.CastOff());
        Assert.True(v.Homeward && !v.InPort);
        v.Chores = v.ChoresDue;
        var (pay, promoted) = sea.Home(1);
        Assert.Equal(v.Pay, pay);
        Assert.Null(promoted);
        Assert.Equal(1, sea.Voyages);
        Assert.Null(sea.Voyage);
    }

    [Fact]
    public void Pirates_wait_more_often_on_the_far_runs_and_a_skimped_deck_costs_wages()
    {
        int Attacks(string id)
        {
            var total = 0;
            for (var s = 1UL; s <= 200; s++)
            {
                var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.FirstMate };
                var v = sea.Sail(Port(id), new Rng(s))!;
                Assert.All(v.Out.Concat(v.Home), a => Assert.InRange(a, 0.3, 0.8));
                total += v.Out.Count + v.Home.Count;
            }
            return total;
        }
        Assert.True(Attacks("port:ambermouth") > Attacks("port:saltmere") * 3);

        var lazy = new SeaBook { Charted = Everywhere(), Rank = SeaRank.Deckhand };
        var trip = lazy.Sail(Port("port:saltmere"), new Rng(3))!;
        trip.ChoresDue = 12;
        trip.Chores = 3;
        lazy.Arrive();
        lazy.CastOff();
        Assert.True(lazy.Home(1).Pay < trip.Pay);
    }

    [Fact]
    public void The_company_promotes_by_voyages_and_fights_all_the_way_to_captain()
    {
        var sea = new SeaBook { Charted = Everywhere() };
        sea.SignOn();
        var promotions = new List<SeaRank>();
        for (var i = 0; i < 20 && sea.Rank < SeaRank.Captain; i++)
        {
            var v = sea.Sail(Port("port:saltmere"), new Rng((ulong)i + 1))!;
            sea.Arrive();
            sea.CastOff();
            if (i % 2 == 0) sea.Repel();
            v.Chores = v.ChoresDue;
            if (sea.Home(heroLevel: i < 14 ? 5 : 9).Promoted is { } r) promotions.Add(r);
        }
        Assert.Equal([SeaRank.Bosun, SeaRank.FirstMate, SeaRank.Captain], promotions);
        Assert.True(sea.Voyages >= SeaBook.ToCaptain);
        Assert.Null(sea.NextStep(9));
        Assert.Contains(sea.Ports, p => p.Id == "port:ambermouth");
        Assert.Equal(20, sea.Capacity); // a captain's private venture in the company's hold
        Assert.True(sea.Sail(Port("port:ambermouth"), new Rng(1))!.Command);
    }

    [Fact]
    public void Command_waits_for_the_years_on_a_first_mate()
    {
        var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.FirstMate, Voyages = 20, Repelled = 10 };
        Assert.Null(sea.Promote(SeaBook.CaptainLevel - 1));
        Assert.Contains("level", sea.NextStep(SeaBook.CaptainLevel - 1));
        Assert.Equal(SeaRank.Captain, sea.Promote(SeaBook.CaptainLevel));
    }

    [Fact]
    public void Pirates_hole_the_hull_a_holed_ship_stays_in_port_and_a_taken_one_loses_its_hold()
    {
        var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.Captain, OwnShip = true, ShipName = "the Gannet" };
        sea.Hold[Good.Iron] = 12;
        sea.Sail(Port("port:corrow"), new Rng(5));
        sea.Damage(30);
        Assert.Equal(70, sea.Hull);
        sea.Taken();
        Assert.Null(sea.Voyage);
        Assert.Empty(sea.Hold);
        Assert.Equal(1, sea.Lost);
        Assert.True(sea.Hull < SeaBook.Seaworthy);
        Assert.Null(sea.Sail(Port("port:saltmere"), new Rng(1))); // into the yard first
        while (sea.Hull < SeaBook.Seaworthy) sea.FitPlank(ownShip: true);
        Assert.NotNull(sea.Sail(Port("port:saltmere"), new Rng(1)));
    }

    [Fact]
    public void The_yard_promotes_its_shipwrights_and_a_master_builds_a_ship_of_their_own()
    {
        var sea = new SeaBook { Charted = Everywhere() };
        Assert.Equal(WrightRank.None, sea.Wright);
        sea.FitPlank(ownShip: false);
        Assert.Equal(WrightRank.Apprentice, sea.Wright);
        var ranks = new List<WrightRank>();
        for (var i = 0; i < SeaBook.MasterAt; i++)
            if (sea.JobDone() is { } r) ranks.Add(r);
        Assert.Equal([WrightRank.Journeyman, WrightRank.Master], ranks);
        Assert.Equal(15, sea.PlankPay);
        Assert.False(sea.MayBuy); // only a captain buys one; a master builds one
        Assert.True(sea.LayKeel());
        Assert.False(sea.LayKeel());
        Assert.False(sea.Build("the Kestrel"));
        Assert.False(sea.Build("the Kestrel"));
        Assert.True(sea.Build("the Kestrel"));
        Assert.True(sea.OwnShip);
        Assert.Equal("the Kestrel", sea.Ship);
        Assert.Equal(50, sea.Capacity);
        Assert.False(sea.MayLayKeel);
    }

    [Fact]
    public void A_life_at_sea_is_saved()
    {
        var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.Bosun, Voyages = 4, Repelled = 1, Wright = WrightRank.Journeyman, Repairs = 5 };
        sea.Sail(Port("port:vosk"), new Rng(9));
        sea.Damage(38);
        sea.Arrive();
        sea.Market(Port("port:vosk"), 3).Sell(Good.Bread, 4);
        var copy = JsonSerializer.Deserialize<SeaBook>(JsonSerializer.Serialize(sea))!;
        Assert.Equal((SeaRank.Bosun, 4, 1, WrightRank.Journeyman, 5, 62.0), (copy.Rank, copy.Voyages, copy.Repelled, copy.Wright, copy.Repairs, copy.Hull));
        Assert.True(copy.Voyage is { InPort: true, Port: "port:vosk" });
        Assert.Equal(sea.Markets["port:vosk"].Stock[Good.Bread], copy.Markets["port:vosk"].Stock[Good.Bread]);
    }

    [Fact]
    public void A_hero_whose_ship_is_taken_can_turn_pirate_and_rise_to_pirate_captain_or_buy_a_pardon()
    {
        var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.Bosun, Voyages = 5 };
        sea.Sail(Port("port:vosk"), new Rng(2));
        sea.Taken();
        Assert.True(sea.JoinPirates());
        Assert.False(sea.JoinPirates());
        Assert.True(sea.Outlaw);
        Assert.Equal((PirateRank.Swab, SeaRank.Landsman), (sea.Pirate, sea.Rank)); // struck off the company's books
        Assert.Null(sea.Sail(Port("port:saltmere"), new Rng(1)));
        Assert.False(sea.SignOn());

        var ranks = new List<PirateRank>();
        for (var i = 0; i < 12; i++)
        {
            var cruise = sea.Prowl(new Rng((ulong)i + 1))!;
            Assert.True(cruise.Raid && cruise.Out.Count == 1);
            Assert.Equal((0, (PirateRank?)null), (0, sea.Plunder(1).Promoted)); // nothing till she's taken
            Assert.True(sea.TakePrize());
            Assert.False(sea.TakePrize());
            var (share, promoted) = sea.Plunder(heroLevel: i < 10 ? 5 : 9);
            Assert.Equal(cruise.Pay, share);
            if (promoted is { } r) ranks.Add(r);
        }
        Assert.Equal([PirateRank.Cutthroat, PirateRank.Quartermaster, PirateRank.Captain], ranks);

        Assert.True(sea.Pardon());
        Assert.Equal((PirateRank.None, SeaRank.Deckhand), (sea.Pirate, sea.Rank));
        Assert.NotNull(sea.Sail(Port("port:saltmere"), new Rng(1)));
    }
}
