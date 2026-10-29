using Tristinwold.Deviant;

namespace Tristinwold.World.Tests;

/// <summary>The wrong side of every honest job: the Tongs' thug, the poacher, the smuggler and the highwayman, with ranks.</summary>
public class ShadowTests
{
    [Fact]
    public void A_thug_collects_a_round_of_shops_and_rises_by_what_is_collected()
    {
        var sh = new ShadowBook();
        Assert.Equal(-1, sh.Rank(ShadowLine.Thug));
        Assert.True(sh.Join(ShadowLine.Thug));
        Assert.False(sh.Join(ShadowLine.Thug));
        Assert.Equal("a Tong knuckle", sh.TitleOf(ShadowLine.Thug));
        var rng = new Rng(5);
        var round = sh.StartRound(["a", "b", "c", "d", "e"], rng);
        Assert.Equal(ShadowBook.RoundShops, round.Count);
        Assert.Equal(round.Count, round.Distinct().Count());
        Assert.Null(sh.Collect("zzz", rng)); // not on the round
        var paid = 0;
        foreach (var shop in round) paid += sh.Collect(shop, rng, sure: true) ?? 0;
        Assert.True(paid >= 24 && sh.RoundDone);
        Assert.Equal(3, sh.For(ShadowLine.Thug).Done);
        Assert.Null(sh.Collect(round[0], rng, sure: true)); // collected already
        Assert.Equal("a Tong enforcer", sh.TitleOf(ShadowLine.Thug));
        for (var i = 0; i < 12; i++) sh.Did(ShadowLine.Thug, 10);
        Assert.Equal("a Tong captain", sh.TitleOf(ShadowLine.Thug));
        Assert.Contains("gallows", sh.NextStep(ShadowLine.Thug));
    }

    [Fact]
    public void A_shopkeeper_refuses_now_and_then()
    {
        var sh = new ShadowBook();
        sh.Join(ShadowLine.Thug);
        int refused = 0, tries = 200;
        for (var i = 0; i < tries; i++)
        {
            sh.StartRound(["a", "b", "c"], new Rng((ulong)i));
            if (sh.Collect("a", new Rng((ulong)i + 1000)) is null) refused++;
        }
        Assert.InRange(refused, tries / 10, tries / 2);
    }

    [Fact]
    public void A_poacher_sells_pelts_a_smuggler_delivers_crates_and_each_pays_better_as_they_rise()
    {
        var sh = new ShadowBook();
        Assert.Equal(0, sh.SellPelts());
        sh.Join(ShadowLine.Poacher);
        sh.Pelts = 3;
        Assert.Equal(3 * ShadowBook.PeltPrice, sh.SellPelts());
        Assert.Equal(0, sh.Pelts);
        sh.For(ShadowLine.Poacher).Done = 8;
        sh.Pelts = 2;
        Assert.Equal((int)Math.Round(2 * ShadowBook.PeltPrice * 1.5), sh.SellPelts());
        sh.Join(ShadowLine.Smuggler);
        Assert.Null(sh.Deliver("port:saltmere"));
        sh.CrateFor = "port:saltmere";
        Assert.Null(sh.Deliver("port:vosk")); // the wrong port
        Assert.Equal(ShadowBook.CratePay, sh.Deliver("port:saltmere"));
        Assert.Null(sh.CrateFor);
        Assert.Equal("a runner", sh.TitleOf(ShadowLine.Smuggler));
    }

    [Fact]
    public void The_watch_wont_have_a_tong_and_it_is_all_saved()
    {
        var s = new Standing();
        Assert.Null(s.WhyNotWatch);
        s.Shadows.Join(ShadowLine.Thug);
        Assert.Equal("you run with the Tongs", s.WhyNotWatch);
        s.Shadows.CrateFor = "port:vosk";
        s.Tracker = new WagonTracker { TownId = "t", TownName = "Larkmere", X = 500, Z = 0, FromX = 0, FromZ = 0, PlantedAt = 10 };
        var json = CompanySave.Capture(1, 5, new ExploredMap(), new QuestLog(), standing: s).ToJson();
        var back = CompanySave.FromJson(json).Standing!;
        Assert.True(back.Shadows.In(ShadowLine.Thug));
        Assert.Equal("port:vosk", back.Shadows.CrateFor);
        Assert.Equal("Larkmere", back.Tracker!.TownName);
        Assert.Equal(0.5, back.Tracker.Along(10 + 250 / WagonTracker.Pace), 3);
        Assert.False(back.Tracker.Lost(10 + 250 / WagonTracker.Pace));
        Assert.True(back.Tracker.Lost(10 + 500 / WagonTracker.Pace + WagonTracker.AtMarketSeconds + 1));
    }
}
