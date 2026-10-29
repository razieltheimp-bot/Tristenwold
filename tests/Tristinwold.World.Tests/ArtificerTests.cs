using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>The fifth calling: its classes, its guild and stories, its golem, and what it does to the markets and the ships.</summary>
public class ArtificerTests
{
    [Fact]
    public void Three_artificers_with_a_golem_each_and_a_guild_to_teach_them()
    {
        var mine = Classes.Of(Calling.Artificer).ToList();
        Assert.Equal(["Alchemist", "EtherAeronaut", "Saboteur"], mine.Select(d => d.Id));
        Assert.Contains(mine, d => d.Utility.Id == "merge");
        Assert.Contains(mine, d => d.Elemental);
        var g = Guilds.Of(Calling.Artificer);
        Assert.Equal("the Brass Foundry", g.Name);
        Assert.All(mine, d => Assert.NotNull(g.TeacherOf(d.Id)));
        Assert.Equal("Jonquil Pike", g.Rival.Name);
        Assert.Equal("the Brass Foundry", Classes.GuildOf(Calling.Artificer));
        foreach (var d in mine) Assert.NotNull(ClassQuests.MasteryTitle(d.Id));
        var golem = new Bond { KindId = "golem", Role = BondRole.Golem, Level = 7 };
        Assert.True(golem.Machine);
        Assert.False(golem.Eats);
        Assert.Equal(1, golem.Growth);
        Assert.False(golem.DiesIfFelled);
        Assert.True(golem.Strength > new Bond { KindId = "golem", Role = BondRole.Golem, Level = 1 }.Strength, "it grows with its maker");
        Assert.NotNull(Bestiary.Get("golem"));
        var hull = new Bond { KindId = "golem", Role = BondRole.Golem, Level = 7, Build = Bond.GolemBuild("EtherAeronaut") };
        Assert.True(hull.Size > golem.Size && golem.Size == 1, "the aeronaut's golem, the one they climb inside, is built bigger than the others'");
        Assert.Equal(1, Bond.GolemBuild("Alchemist"));
    }

    [Fact]
    public void The_foundrys_stories_have_a_good_choice_and_a_wicked_one_each_and_a_duel_with_jonquil()
    {
        var stories = Guilds.Drama.Where(s => s.Guild == Calling.Artificer).ToList();
        Assert.Equal(4, stories.Count);
        Assert.Equal([GuildRank.Initiate, GuildRank.Member, GuildRank.Adept, GuildRank.Veteran], stories.Select(s => s.Rank));
        Assert.All(stories, s => { Assert.Contains(s.Choices, c => c.Good); Assert.Contains(s.Choices, c => c.Evil); });
        Assert.Contains(stories.SelectMany(s => s.Choices), c => c.Duel);
        var book = new GuildBook { PlaySeconds = Guilds.DramaGapSeconds };
        Assert.Equal("leaking_still", Guilds.NextDrama(book, Calling.Artificer)!.Id);
    }

    [Fact]
    public void Every_class_old_and_new_has_a_trade_perk()
    {
        Assert.All(Classes.All, d => Assert.NotEqual("None", Perks.Of(d.Id).Name));
        Assert.True(Perks.Of("Saboteur").Sell > 1);
        Assert.True(Perks.Of("MartialArtist").Shop < 1);
        Assert.True(Perks.Of("Barbarian").Gather > 1 && Perks.Of("Barbarian").Pay > 1);
        Assert.Equal("None", Perks.Of("nobody").Name);
    }

    [Fact]
    public void An_aeronaut_fits_a_ship_with_modules_that_make_it_faster_and_tougher()
    {
        var sea = new SeaBook();
        sea.SignOn();
        var crowns = 1000;
        Assert.Null(sea.Fit("rocket", ref crowns));
        Assert.Equal(260, sea.Fit("light_sails", ref crowns));
        Assert.Null(sea.Fit("light_sails", ref crowns)); // fitted already
        Assert.Equal(740, crowns);
        var plain = new SeaBook().Sail(Seas.Get("port:saltmere")!, new Rng(1));
        new SeaBook { Rank = SeaRank.Deckhand }.ToString();
        var quick = sea.Sail(Seas.Get("port:saltmere")!, new Rng(1))!;
        Assert.Equal(1.33, quick.Pace);
        Assert.True(quick.LegSeconds < CityEconomy.DaySeconds, "a quarter off the leg");
        Assert.Null(plain); // not signed on
        sea.Hull = 100;
        sea.Damage(10);
        Assert.Equal(90, sea.Hull);
        sea.Fit("bilge_pump", ref crowns);
        sea.Damage(10);
        Assert.Equal(85, sea.Hull);
    }

    [Fact]
    public void An_alchemist_steadies_a_market_once_a_day()
    {
        var w = new WorldGen(seed: 7);
        var place = w.SettlementsNear(0, 0, 4000).First(s => s.Kind == SettlementKind.Town);
        var book = new TradeBook();
        var m = book.Market(place, 1);
        var good = m.Makes.First();
        m.Stock[good] = 0.5; // a crash
        var dear = m.Asks(good);
        Assert.True(m.Steady(1));
        Assert.False(m.Steady(1)); // once a day
        Assert.True(m.Asks(good) < dear, "the price eases");
        Assert.True(m.Stock[good] > m.Usual(good) * 0.6, "the stock comes most of the way back");
        Assert.True(m.Steady(2));
    }
}
