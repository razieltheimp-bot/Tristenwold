using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>A hero's name in the world, the Watch career, a clan's offer, and the good and evil in every guild story.</summary>
public class StandingTests
{
    static Warband Clan() => Warband.Near(0, 0, 4000).First();

    [Fact]
    public void A_recruit_walks_beats_collars_trouble_and_rises_to_captain()
    {
        var w = new WatchBook();
        var rng = new Rng(7);
        Assert.Null(w.StartPatrol(rng)); // not on the rolls
        Assert.True(w.Join());
        Assert.False(w.Join());
        var beat = w.StartPatrol(rng)!;
        Assert.Equal(4, beat.Posts.Count);
        Assert.Equal(beat.Posts.Count, beat.Posts.Distinct().Count());
        Assert.All(beat.Posts, p => Assert.Contains(p, WatchBook.Beat));
        Assert.Null(w.StartPatrol(rng)); // one beat at a time
        Assert.Equal((0, null), w.Report(1)); // not walked yet
        Assert.False(w.Reached(beat.Posts[1])); // out of order
        foreach (var p in beat.Posts) Assert.True(w.Reached(p));
        Assert.True(beat.Walked);
        w.Arrest();
        var (pay, promoted) = w.Report(1);
        Assert.Equal(WatchBook.BeatPay + WatchBook.ArrestPay, pay);
        Assert.Null(promoted);
        Assert.Null(w.Patrol);

        for (var i = 1; i < WatchBook.ToWatchman; i++) { Walk(w, rng); }
        Assert.Equal(WatchRank.Watchman, w.Rank);
        Assert.Equal(5, w.StartPatrol(rng)!.Posts.Count);
        w.Patrol = null;
        while (w.Patrols < WatchBook.ToSergeant) Walk(w, rng);
        Assert.Equal(WatchRank.Watchman, w.Rank); // beats alone don't make a sergeant
        w.Arrests = WatchBook.SergeantArrests;
        Walk(w, rng);
        Assert.Equal(WatchRank.Sergeant, w.Rank);
        w.Arrests = WatchBook.CaptainArrests;
        while (w.Patrols < WatchBook.ToCaptain + 1) Walk(w, rng, level: WatchBook.CaptainLevel - 1);
        Assert.Equal(WatchRank.Sergeant, w.Rank); // too green for the captaincy
        Walk(w, rng, level: WatchBook.CaptainLevel);
        Assert.Equal(WatchRank.Captain, w.Rank);
        Assert.Equal("Captain of the Tristinwold Watch", WatchBook.Title(w.Rank));
        Assert.True(w.Earned > 0);
        w.Dismiss();
        Assert.Equal(WatchRank.None, w.Rank);
        Assert.Equal(1, w.Dismissals);
    }

    static void Walk(WatchBook w, Rng rng, int level = 1)
    {
        var beat = w.StartPatrol(rng)!;
        foreach (var p in beat.Posts) w.Reached(p);
        w.Report(level);
    }

    [Fact]
    public void Trouble_waits_at_a_post_on_most_beats()
    {
        var w = new WatchBook();
        w.Join();
        var rng = new Rng(3);
        int trouble = 0, beats = 40;
        for (var i = 0; i < beats; i++)
        {
            var beat = w.StartPatrol(rng)!;
            if (beat.TroubleAt >= 0) { trouble++; Assert.InRange(beat.TroubleAt, 0, beat.Posts.Count - 1); }
            w.Patrol = null;
        }
        Assert.InRange(trouble, beats / 2, beats - 1);
    }

    [Fact]
    public void A_clansman_rides_raids_takes_a_share_and_rises_to_warchief()
    {
        var c = new ClanBook();
        var clan = Clan();
        Assert.Equal((0, null), c.Rode(clan, 1)); // not sworn
        Assert.True(c.Join(clan));
        Assert.False(c.Join(clan));
        Assert.Equal(ClanRank.Spearman, c.Rank);
        Assert.Equal(clan.Name, c.ClanName);
        var other = Warband.Near(0, 0, 6000).First(w => w.Id != clan.Id);
        Assert.Equal((0, null), c.Rode(other, 1)); // not your clan
        var (share, promoted) = c.Rode(clan, 1);
        Assert.Equal(c.Share(clan), share);
        Assert.True(share > 20);
        Assert.Null(promoted);
        Assert.Equal(ClanRank.Raider, c.Rode(clan, 1).Promoted);
        Assert.True(c.Share(clan) > share, "a raider's share is bigger than a spear's");
        while (c.Raids < ClanBook.ToChampion - 1) c.Rode(clan, 1);
        Assert.Equal(ClanRank.Champion, c.Rode(clan, 1).Promoted);
        while (c.Raids < ClanBook.ToWarchief + 1) c.Rode(clan, ClanBook.WarchiefLevel - 1);
        Assert.Equal(ClanRank.Champion, c.Rank); // too green
        Assert.Equal(ClanRank.Warchief, c.Rode(clan, ClanBook.WarchiefLevel).Promoted);
        Assert.StartsWith("warchief of ", c.Title);
        Assert.True(c.Plunder > 0);
        c.Leave();
        Assert.False(c.In);
    }

    [Fact]
    public void Honour_and_infamy_make_a_name_and_the_world_reads_it()
    {
        var s = new Standing();
        Assert.Equal(Repute.Unknown, s.Repute);
        Assert.Equal(1, s.PriceFactor);
        Assert.Null(s.Aside(new Rng(1)));
        s.Good(6, "broke the raid on Larkmere");
        s.Good(6, "walked a beat for the Watch");
        s.Good(6, "put down a cutthroat");
        Assert.Equal(Repute.Decent, s.Repute);
        s.Honour = 130;
        Assert.Equal(Repute.Hero, s.Repute);
        Assert.Equal("a hero of Tristinwold", s.Name);
        Assert.True(s.PriceFactor < 1);
        Assert.Contains("put down a cutthroat", string.Join(" ", Enumerable.Range(0, 20).Select(i => s.Aside(new Rng((ulong)i)))));
        s.Infamy = 200;
        Assert.Equal(Repute.Villain, s.Repute);
        Assert.True(s.PriceFactor > 1);
        Assert.NotNull(s.Aside(new Rng(1)));
        for (var i = 0; i < Standing.KeepDeeds + 5; i++) s.Evil(1, $"deed {i}");
        Assert.Equal(Standing.KeepDeeds, s.Deeds.Count);
        Assert.Equal($"deed {Standing.KeepDeeds + 4}", s.LastDeed);
        Assert.Contains("villain", s.Summary());
    }

    [Fact]
    public void A_price_on_the_head_makes_an_outlaw_strikes_a_watchman_off_and_is_paid_off_dear_through_a_smuggler()
    {
        var s = new Standing();
        s.Watch.Join();
        s.Wanted(Standing.RaidBounty);
        Assert.True(s.Outlaw);
        Assert.False(s.Watch.In); // struck off
        Assert.Equal("there's a price on your head", s.WhyNotWatch);
        Assert.StartsWith("an outlaw", s.Name);
        Assert.Equal(1.3, s.PriceFactor);
        var purse = 10;
        Assert.Null(s.PayBounty(ref purse)); // can't afford it
        purse = 100;
        Assert.Equal((int)Math.Ceiling(Standing.RaidBounty * 1.2), s.PayBounty(ref purse, throughSmuggler: true));
        Assert.False(s.Outlaw);
        Assert.Equal(100 - 48, purse);
        Assert.Null(s.WhyNotWatch);
        Assert.True(s.Watch.Join());
    }

    [Fact]
    public void Swearing_to_a_clan_ends_a_watchmans_service_and_a_villain_is_turned_away()
    {
        var s = new Standing();
        s.Watch.Join();
        Assert.True(s.JoinClan(Clan()));
        Assert.False(s.Watch.In);
        Assert.Equal("you ride with a barbarian clan", s.WhyNotWatch);
        Assert.True(s.Infamy > 0);
        Assert.StartsWith("swore to the ", s.LastDeed);
        s.Clan.Leave();
        s.Infamy = 100;
        Assert.Equal("the whole city knows what you are", s.WhyNotWatch);
    }

    [Fact]
    public void Every_guild_story_offers_a_good_choice_and_a_wicked_one_and_the_world_hears_which_you_took()
    {
        foreach (var story in Guilds.Drama)
        {
            Assert.Contains(story.Choices, c => c.Good);
            Assert.Contains(story.Choices, c => c.Evil);
            Assert.All(story.Choices.Where(c => c.Honour + c.Infamy > 0), c => Assert.False(string.IsNullOrEmpty(c.Deed)));
        }
        var book = new GuildBook();
        var s = new Standing();
        var purse = Guilds.Drama.First(d => d.Id == "tong_purse");
        Guilds.Choose(book, purse, purse.Choices.First(c => c.Flag == "tong_money"), standing: s);
        Assert.Equal(4, s.Infamy);
        Assert.Equal("took the Tongs' money for the Iron Circle", s.LastDeed);
        // A duel lost settles nothing with the world.
        var duel = Guilds.Drama.First(d => d.Id == "first_blade");
        Guilds.Choose(book, duel, duel.Choices.First(c => c.Duel), wonDuel: false, standing: s);
        Assert.Equal(0, s.Honour);
        Guilds.Choose(book, duel, duel.Choices.First(c => c.Duel), wonDuel: true, standing: s);
        Assert.Equal(2, s.Honour);
    }

    [Fact]
    public void The_standing_is_saved_with_the_company()
    {
        var s = new Standing { Honour = 12, Infamy = 3, Bounty = 40 };
        s.Watch.Join();
        s.Watch.StartPatrol(new Rng(2));
        s.Clan.Join(Clan());
        var map = new ExploredMap();
        var json = CompanySave.Capture(1, 5, map, new QuestLog(), standing: s).ToJson();
        var back = CompanySave.FromJson(json).Standing!;
        Assert.Equal(12, back.Honour);
        Assert.Equal(40, back.Bounty);
        Assert.Equal(WatchRank.Recruit, back.Watch.Rank);
        Assert.Equal(s.Watch.Patrol!.Posts, back.Watch.Patrol!.Posts);
        Assert.Equal(s.Clan.ClanId, back.Clan.ClanId);
        Assert.Null(CompanySave.FromJson("{}").Standing); // an old save: the game starts a fresh one
    }
}
