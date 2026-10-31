using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit;

namespace Tristinwold.World.Tests;

public class PirateCrewTests
{
    static SeaPort Port(string id) => Seas.Get(id)!;
    static HashSet<string> Everywhere() => [.. Seas.Ports.Select(p => p.Id)];

    [Fact]
    public void The_small_crews_are_small_and_all_different()
    {
        Assert.Equal(PirateCrew.Count, PirateCrew.Small.Count);
        Assert.Equal(PirateCrew.Count, PirateCrew.Small.Select(c => c.Id).Distinct().Count());
        Assert.Equal(PirateCrew.Count, PirateCrew.Small.Select(c => c.Name).Distinct().Count());
        Assert.Equal(PirateCrew.Count, PirateCrew.Small.Select(c => c.Ship).Distinct().Count());
        Assert.All(PirateCrew.Small, c => Assert.True(c.Hands is >= 3 and <= 5 && !c.Good && c.Lanes.Count > 0, c.Name));
        Assert.All(PirateCrew.Small, c => Assert.Equal(c, PirateCrew.Get(c.Id)));
    }

    [Fact]
    public void A_crew_roams_from_lane_to_lane_and_every_company_sees_it_in_the_same_place()
    {
        foreach (var c in PirateCrew.Small)
        {
            var lanes = new HashSet<string>();
            for (var round = 0; round < 60; round++)
            {
                var t = round * PirateCrew.Round;
                Assert.Null(c.Where(t + 10).Lane); // on the move
                var (r, lane) = c.Where(t + PirateCrew.Quiet + 10);
                Assert.Equal(round, r);
                Assert.Equal(lane, c.Where(t + PirateCrew.Quiet + 10).Lane);
                Assert.True(lane!.Leagues >= c.Nearest && lane.Leagues <= c.Furthest);
                lanes.Add(lane.Id);
            }
            if (c.Lanes.Count > 1) Assert.True(lanes.Count > 1, c.Name);
        }
    }

    [Fact]
    public void Who_works_a_lane_is_who_boards_there_and_a_beaten_crew_goes_quiet_for_an_hour()
    {
        var sea = new SeaBook { Charted = Everywhere(), Rank = SeaRank.Captain };
        var (port, t) = Enumerable.Range(0, 400).Select(i => (Port: Port("port:greywater"), T: i * PirateCrew.Round + PirateCrew.Quiet + 1)).First(x => sea.Hunters(x.Port, x.T) is not null);
        var crew = sea.Hunters(port, t)!;
        var v = sea.Sail(port, new Rng(3), crew: crew)!;
        Assert.Equal(crew.Id, v.Crew);
        Assert.Equal(crew.Hands + 1, v.Pirates); // a captain's ship has more to board
        sea.Break(crew, t);
        Assert.NotEqual(crew, sea.Hunters(port, t));
        Assert.True(sea.IsBroken(crew, t + PirateCrew.BrokenSeconds - 1));
        Assert.False(sea.IsBroken(crew, t + PirateCrew.BrokenSeconds + 1));
    }

    [Fact]
    public void Word_of_a_crew_comes_once_a_round()
    {
        var sea = new SeaBook();
        Assert.True(sea.Hear(PirateCrew.Small[0], 3));
        Assert.False(sea.Hear(PirateCrew.Small[0], 3));
        Assert.True(sea.Hear(PirateCrew.Small[0], 4));
    }

    [Fact]
    public void A_place_in_a_crew_strikes_you_off_the_company_and_any_other_crew_at_the_bottom()
    {
        var sea = new SeaBook { Rank = SeaRank.Bosun };
        var (a, b) = (PirateCrew.Small[0], PirateCrew.DrunkenMonkey);
        Assert.True(sea.JoinCrew(a));
        Assert.False(sea.JoinCrew(a));
        Assert.Equal((a, PirateRank.Swab, SeaRank.Landsman), (sea.Sworn, sea.Pirate, sea.Rank));
        sea.Prizes = 6;
        sea.Pirate = PirateRank.Quartermaster;
        Assert.True(sea.JoinCrew(b));
        Assert.Equal((b, PirateRank.Swab, 0), (sea.Sworn, sea.Pirate, sea.Prizes));
        Assert.True(sea.Pardon());
        Assert.Null(sea.Crew);
    }

    [Fact]
    public void A_save_from_before_the_crews_is_still_on_the_widows_tooth()
    {
        var sea = new SeaBook { Pirate = PirateRank.Cutthroat };
        Assert.Equal(PirateCrew.WidowsTooth, sea.Sworn);
        Assert.False(sea.JoinPirates());
    }

    [Fact]
    public void A_good_crew_only_hunts_pirates_and_a_wicked_one_gets_marines_or_Lu()
    {
        var good = new SeaBook();
        good.JoinCrew(PirateCrew.DrunkenMonkey);
        for (var i = 0; i < 100; i++)
        {
            var v = good.Prowl(new Rng((ulong)i + 1))!;
            Assert.True(PirateCrew.Get(v.Crew) is { Good: false });
            Assert.Null(v.Flag);
            Assert.False(v.Marines);
            good.Voyage = null;
        }
        var bad = new SeaBook();
        bad.JoinPirates();
        bad.Pirate = PirateRank.Cutthroat;
        var lu = 0;
        for (var i = 0; i < 300; i++)
        {
            var v = bad.Prowl(new Rng((ulong)i + 1))!;
            Assert.Null(v.Crew);
            Assert.True(v.Marines); // on the way out, a merchantman's marines
            if (v.Flag is { } flag)
            {
                lu++;
                Assert.Equal("wukong", flag);
                Assert.NotEmpty(v.Home);
                v.Homeward = true;
                Assert.False(v.Marines);
            }
            bad.Voyage = null;
        }
        Assert.InRange(lu, 30, 110);
    }

    [Fact]
    public void A_good_crew_leaves_a_broken_crew_alone()
    {
        var sea = new SeaBook();
        sea.JoinCrew(PirateCrew.DrunkenMonkey);
        foreach (var c in PirateCrew.Small.Skip(1)) sea.Break(c, 0);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(PirateCrew.Small[0].Id, sea.Prowl(new Rng((ulong)i + 1), 10)!.Crew);
            sea.Voyage = null;
        }
    }

    [Fact]
    public void Lu_no_longer_robs_the_company_but_Soren_still_does()
    {
        for (var i = 0; i < 400; i++) Assert.NotEqual("wukong", Seas.Flies(4, new Rng((ulong)i))?.Id);
        Assert.Contains(Enumerable.Range(0, 400), i => Seas.Flies(4, new Rng((ulong)i))?.Id == "soren");
    }

    [Fact]
    public void Lu_gets_bigger_and_less_himself_every_time_and_stops_getting_worse_at_the_end()
    {
        Assert.Equal(6, Legend.WukongForms.Count);
        for (var i = 1; i < Legend.WukongForms.Count; i++)
        {
            Assert.True(Legend.WukongForms[i].Size > Legend.WukongForms[i - 1].Size);
            Assert.True(Legend.WukongForms[i].Beast > Legend.WukongForms[i - 1].Beast);
        }
        Assert.Equal(Legend.WukongOf(5), Legend.WukongOf(50));
        Assert.True(Legend.WukongStrength(1) > Legend.WukongStrength(0));
        Assert.Equal(Legend.SorenMost, Legend.WukongStrength(500));
    }

    static CityEconomy Besieged(bool lu) => new() { Siege = new PirateSiege { Town = "city:druitt", Ends = 3, Lu = lu } };

    [Fact]
    public void Lu_comes_to_one_siege_in_three()
    {
        var (sieges, lu) = (0, 0);
        for (ulong seed = 1; seed < 80; seed++)
        {
            var city = CityEconomy.Start(seed);
            for (var day = 0; day < 600; day++)
            {
                var had = city.Siege;
                city.NextDay();
                if (city.Siege is { } s && s != had) { sieges++; if (s.Lu) lu++; }
            }
        }
        Assert.True(sieges > 400, $"{sieges}");
        Assert.InRange(lu / (double)sieges, 0.27, 0.40);
    }

    [Fact]
    public void While_Lu_holds_nothing_burns_and_the_town_isnt_sacked()
    {
        for (var seed = 1; seed < 40; seed++)
        {
            var city = CityEconomy.Start((ulong)seed);
            city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 2, Lu = true };
            var (burnt, purse) = (city.FabricOf("city:druitt").Burnt, city.Treasury);
            city.NextDay();
            city.NextDay();
            Assert.Null(city.Siege);
            Assert.Equal(burnt, city.FabricOf("city:druitt").Burnt);
            Assert.True(city.Treasury >= purse - CityEconomy.SackTreasury / 2);
        }
    }

    [Fact]
    public void Holding_a_town_alone_sometimes_costs_him_his_life()
    {
        var fell = 0;
        for (var seed = 1; seed < 400; seed++)
        {
            var city = CityEconomy.Start((ulong)seed);
            city.Siege = new PirateSiege { Town = "city:greywater", Ends = city.Day + 1, Lu = true };
            city.NextDay();
            fell += city.WukongDeaths;
        }
        Assert.InRange(fell, 60, 140);
    }

    [Fact]
    public void Killed_at_a_siege_he_stops_holding_it()
    {
        var city = Besieged(lu: true);
        Assert.True(city.Apply(CityAction.WukongKilled("The Dread Pirate Soren")));
        Assert.Equal(1, city.WukongDeaths);
        Assert.False(city.Siege!.Lu);
        Assert.Contains(city.News, n => n.Contains("killed Lu Wukong"));
    }
}
