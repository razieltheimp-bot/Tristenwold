using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit;

namespace Tristinwold.World.Tests;

public class BrasswakeTests
{
    [Fact]
    public void The_island_never_stops_and_never_comes_near_the_land()
    {
        var (last, far) = (Brasswake.Where(0), 0.0);
        for (var t = 60.0; t < 400000; t += 60)
        {
            var now = Brasswake.Where(t);
            var step = Math.Sqrt(Math.Pow(now.X - last.X, 2) + Math.Pow(now.Z - last.Z, 2));
            Assert.InRange(step, 1, 200); // it's always under way, and a ship can catch it
            far = Math.Max(far, Math.Sqrt(now.X * now.X + now.Z * now.Z));
            last = now;
        }
        Assert.True(far > 4000);
    }

    [Fact]
    public void How_far_out_it_is_changes_with_the_day()
    {
        var days = Enumerable.Range(0, 400).Select(i => Brasswake.Leagues(i * 300.0)).Distinct().ToList();
        Assert.True(days.Count > 1);
        Assert.All(days, d => Assert.InRange(d, 1, Brasswake.MostLeagues));
    }

    [Fact]
    public void Anyone_can_ship_out_to_it_and_the_company_pays_its_own_for_the_run()
    {
        var passenger = new SeaBook();
        var v = passenger.SeekIsle(10)!;
        Assert.Equal(Brasswake.Port, v.Port);
        Assert.Equal(0, v.Pay);
        Assert.Null(passenger.SeekIsle(10)); // already at sea
        Assert.True(new SeaBook { Rank = SeaRank.Bosun }.SeekIsle(10)!.Pay > 0);
    }

    [Fact]
    public void It_is_a_trade_route_with_a_market_of_its_own()
    {
        var sea = new SeaBook();
        var port = Seas.Get(Brasswake.Market.Id)!;
        Assert.Equal(Brasswake.Market, port);
        Assert.Contains(Good.Timber, sea.Market(port, 1).Wants);
        Assert.Contains(Good.Iron, sea.Market(port, 1).Makes);
    }

    [Fact]
    public void Reaching_it_charts_nothing()
    {
        var sea = new SeaBook();
        sea.SeekIsle(0);
        Assert.True(sea.Arrive());
        Assert.Empty(sea.Charted);
    }

    [Fact]
    public void A_ship_at_the_dock_can_cast_off_for_home_and_only_then()
    {
        var sea = new SeaBook();
        Assert.Null(sea.SailFromIsle(0));
        sea.Isle.Berthed = true;
        var v = sea.SailFromIsle(0)!;
        Assert.True(v.Homeward && !sea.Isle.Berthed);
        Assert.Equal((0, (SeaRank?)null), sea.Home(1));
    }

    [Fact]
    public void Aboard_the_work_pays_in_marks_and_makes_a_citizen_of_you()
    {
        var isle = new IsleBook();
        isle.Board();
        Assert.Equal(IsleRank.Guest, isle.Rank);
        var ranks = new List<IsleRank>();
        for (var i = 0; i < 100; i++)
            if (isle.Vent(i % IsleBook.Valves == IsleBook.Valves - 1).Made is { } r) ranks.Add(r);
        Assert.Equal([IsleRank.Hand, IsleRank.Citizen], ranks);
        Assert.True(isle.MaySummon);
        Assert.True(isle.Shifts > 0);
    }

    [Fact]
    public void Marks_buy_their_wares_and_a_pilots_licence_and_crowns_dont_come_into_it()
    {
        var isle = new IsleBook { Marks = 20 };
        Assert.False(isle.Buy("iron_ore"));
        Assert.False(isle.Buy("aether_gauntlet"));
        Assert.True(isle.Buy("steamweave_coat"));
        Assert.Equal(2, isle.Marks);
        Assert.False(isle.LearnToFly());
        isle.Marks = 5;
        Assert.True(isle.LearnToFly());
        Assert.False(isle.LearnToFly());
        Assert.Equal(0, isle.Marks);
    }

    [Fact]
    public void Everything_they_make_exists_is_bound_and_is_known_for_theirs()
    {
        foreach (var (id, marks) in Brasswake.Wares)
        {
            Assert.True(marks > 0);
            Assert.True(Items.Get(id) is { Bound: true }, id);
            Assert.True(Brasswake.Made(id));
        }
        Assert.False(Brasswake.Made("steel_greatsword"));
    }

    [Fact]
    public void Nothing_they_make_leaves_the_island()
    {
        var (sea, pack) = (new SeaBook(), new Pack());
        pack.Add("aether_gauntlet");
        pack.Add("steamweave_coat");
        pack.Add("steel_greatsword");
        pack.Equip("aether_gauntlet");
        sea.Isle.Aboard = true;
        var left = sea.LeaveIsle(pack);
        Assert.Equal(2, left);
        Assert.False(sea.Isle.Aboard);
        Assert.Equal(0, pack.Count("aether_gauntlet"));
        Assert.Equal(0, pack.Count("steamweave_coat"));
        Assert.Equal(1, pack.Count("steel_greatsword"));
        Assert.Null(pack.WornIn(Slot.Weapon));
        Assert.Equal(2, sea.Isle.Locker.Count);

        sea.BackOnIsle(pack);
        Assert.Equal(1, pack.Count("aether_gauntlet"));
        Assert.Equal(1, pack.Count("steamweave_coat"));
        Assert.Empty(sea.Isle.Locker);
    }

    [Fact]
    public void The_island_survives_a_save()
    {
        var sea = new SeaBook { Isle = { Rank = IsleRank.Hand, Marks = 7, Aboard = true, Pilot = true, Locker = ["orrery_ring"], Berthed = true } };
        var copy = System.Text.Json.JsonSerializer.Deserialize<SeaBook>(System.Text.Json.JsonSerializer.Serialize(sea))!;
        Assert.Equal((IsleRank.Hand, 7, true, true), (copy.Isle.Rank, copy.Isle.Marks, copy.Isle.Aboard, copy.Isle.Pilot));
        Assert.Equal(["orrery_ring"], copy.Isle.Locker);
        Assert.True(copy.Isle.Berthed);
    }

    [Fact]
    public void Anyone_let_aboard_can_sign_on_with_the_air_crews_once()
    {
        var isle = new IsleBook();
        Assert.False(isle.Enlist());
        isle.Board();
        Assert.True(isle.Enlist());
        Assert.False(isle.Enlist());
        Assert.Equal(AirRank.Hand, isle.Air);
        Assert.False(isle.TakesTheWheel);
    }

    [Fact]
    public void Patrols_and_kills_make_a_hand_into_a_skipper_and_only_a_won_patrol_counts()
    {
        var isle = new IsleBook { Air = AirRank.Hand, Rank = IsleRank.Guest };
        var raid = Brasswake.Raids[0];
        Assert.Null(isle.Sortie(raid, won: false).Air);
        Assert.Equal(0, isle.Sorties);
        var ranks = new List<AirRank>();
        for (var i = 0; i < 12; i++)
        {
            isle.AirKills += 2;
            if (isle.Sortie(raid, won: true).Air is { } r) ranks.Add(r);
        }
        Assert.Equal([AirRank.Gunner, AirRank.Helmsman, AirRank.Skipper], ranks);
        Assert.True(isle.TakesTheWheel);
        Assert.True(isle.GunPower > 1);
    }

    [Fact]
    public void No_rank_comes_without_the_kills_to_back_it()
    {
        var isle = new IsleBook { Air = AirRank.Gunner, Rank = IsleRank.Guest, Sorties = 20 };
        Assert.Null(isle.Sortie(Brasswake.Raids[0], won: true).Air);
        Assert.Equal(AirRank.Gunner, isle.Air);
    }

    [Fact]
    public void A_patrol_pays_better_won_than_lost_and_better_for_a_skipper_and_it_all_counts_toward_citizen()
    {
        var raid = Brasswake.Raids[1];
        var (hand, skipper) = (new IsleBook { Air = AirRank.Hand }, new IsleBook { Air = AirRank.Skipper });
        var won = hand.Sortie(raid, true).Pay;
        Assert.True(won > new IsleBook { Air = AirRank.Hand }.Sortie(raid, false).Pay);
        Assert.True(skipper.Sortie(raid, true).Pay > won);
        Assert.Equal(won, hand.Earned);
        var guest = new IsleBook { Rank = IsleRank.Guest, Air = AirRank.Hand, Earned = IsleBook.ToHand - 1 };
        Assert.Equal(IsleRank.Hand, guest.Sortie(raid, true).Isle);
    }

    [Fact]
    public void Nothing_worse_comes_at_a_crew_than_it_has_been_trusted_with()
    {
        for (var i = 0; i < 300; i++)
        {
            var rng = new Rng((ulong)i + 1);
            Assert.True(Brasswake.RaidFor(AirRank.None, rng).From <= AirRank.Hand);
            Assert.True(Brasswake.RaidFor(AirRank.Hand, rng).From <= AirRank.Hand);
            Assert.True(Brasswake.RaidFor(AirRank.Gunner, rng).From <= AirRank.Gunner);
        }
        Assert.Contains(Enumerable.Range(0, 300), i => Brasswake.RaidFor(AirRank.Skipper, new Rng((ulong)i + 1)).Id == "rift");
    }

    [Fact]
    public void Everything_in_a_raid_is_a_real_creature_and_dragons_that_mind_their_own_business_stay_out_of_it()
    {
        foreach (var raid in Brasswake.Raids)
            foreach (var (kind, n, _) in raid.Foes)
            {
                Assert.True(n > 0);
                Assert.Equal(kind, Bestiary.Get(kind).Id);
                Assert.True(raid.Riders || !Bestiary.Get(kind).Wary, kind);
            }
    }
}
