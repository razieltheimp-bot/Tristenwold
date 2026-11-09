using System.Text.Json;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>The Summoner's road with Braz Algún: a ring marked, the great ones of the road beaten one at a time and bound, and Draxon last.</summary>
public class SummonerTests
{
    static Hero At(int level) => new() { Level = level };

    /// <summary>Sent after the next great one and beats it.</summary>
    static Summon? Win(Hero h) => Summons.Send(h) is { } t ? Summons.Bind(h, t.Id) : null;

    [Fact]
    public void Braz_marks_no_ring_before_level_five_and_nothing_binds_before_she_sends_you()
    {
        var h = At(Summons.MinLevel - 1);
        Assert.False(Summons.Begin(h));
        Assert.Null(Summons.Send(h));
        Assert.Null(Summons.Bind(h, Summons.Orrogh));
        h.Level = Summons.MinLevel;
        Assert.True(Summons.Begin(h));
        Assert.False(Summons.Begin(h));
        Assert.Null(Summons.Bind(h, Summons.Orrogh)); // marked, but not sent yet
        Assert.Empty(h.Bound);
    }

    [Fact]
    public void The_great_ones_come_in_order_one_at_a_time_and_the_first_makes_you_a_Summoner_once_Braz_sees_it()
    {
        var h = At(10);
        Summons.Begin(h);
        Assert.Equal(Summons.Trials[0].Step, Summons.Step(h));
        var first = Summons.Send(h)!;
        Assert.Equal(Summons.Orrogh, first.Id);
        Assert.Null(Summons.Send(h)); // one at a time
        Assert.Null(Summons.Bind(h, Summons.Lumira)); // not the one she sent you after
        Assert.Equal(Summons.Orrogh, Summons.Bind(h, Summons.Orrogh)?.Id);
        Assert.Null(Summons.Callable(h));
        Assert.Null(Summons.Send(h)); // show her first
        Assert.True(Summons.Name(h));
        Assert.Equal(Summons.Orrogh, Summons.Callable(h)?.Id);
        Assert.Equal(Summons.Lumira, Win(h)?.Id);
        Assert.Equal(Summons.Orrogh, h.Calls); // the first stays chosen until they choose another
        Assert.True(Summons.Choose(h, Summons.Lumira));
        Assert.False(Summons.Choose(h, Summons.Quvirn));
        Assert.Equal(Summons.Lumira, Summons.Callable(h)?.Id);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], Summons.Trials.Select(t => t.Step));
        Assert.Equal(5, Summons.MinLevel);
        Assert.Equal(10, Summons.Trials[0].Recommended);
        Assert.True(Summons.Trials.Zip(Summons.Trials.Skip(1)).All(p => p.Second.Recommended > p.First.Recommended)); // each harder than the last
    }

    [Fact]
    public void No_dragon_is_ever_bound_but_Draxon_at_the_end_of_the_road()
    {
        var h = At(20);
        Summons.Begin(h);
        Summons.Bind(h, Summons.Orrogh);
        Assert.Null(Summons.Bind(h, Summons.Draxon)); // not sent after him
        Win(h);
        Summons.Name(h);
        for (var i = 0; i < Summons.Trials.Count - 2; i++) Win(h);
        Assert.Equal(Summons.Draxon, Summons.Send(h)?.Id);
        Assert.Equal(Summons.Draxon, Summons.Bind(h, Summons.Draxon)?.Id);
        Assert.Null(Summons.Send(h)); // the road walked
        Assert.Equal(Summons.Trials[^1].Step + 1, Summons.Step(h));
        Assert.Single(Summons.All, s => s.Look.StartsWith("dragon") || s.Id == Summons.Draxon);
    }

    [Fact]
    public void A_called_beast_killed_is_torn_from_the_ring_and_must_be_beaten_again()
    {
        var h = At(20);
        Summons.Begin(h);
        Win(h);
        Summons.Name(h);
        Win(h);
        Win(h);
        Assert.Equal([Summons.Orrogh, Summons.Lumira, Summons.Quvirn], h.Bound);
        Summons.Choose(h, Summons.Lumira);
        Assert.True(Summons.Lose(h, Summons.Lumira));
        Assert.False(Summons.Lose(h, Summons.Lumira));
        Assert.DoesNotContain(Summons.Lumira, h.Bound);
        Assert.Equal(Summons.Orrogh, h.Calls); // the ring answers with another it holds
        Assert.Equal(Summons.Lumira, Summons.Send(h)?.Id); // and Braz sends you after Lumira again before anything new
        Assert.Equal(Summons.Lumira, Summons.Bind(h, Summons.Lumira)?.Id);
        Assert.Equal(Summons.Aegrath, Summons.NextTrial(h)?.Id);
        Assert.Equal(SummonerStep.Summoner, h.Summoner); // still a summoner, even with an empty ring
        foreach (var id in h.Bound.ToList()) Summons.Lose(h, id);
        Assert.Equal(SummonerStep.Summoner, h.Summoner);
        Assert.Null(Summons.Callable(h));
    }

    [Fact]
    public void A_beast_hits_harder_as_the_hero_grows()
    {
        var s = Summons.Get(Summons.Quvirn)!;
        Assert.Equal(s.Might, Summons.Blow(s, 1));
        Assert.True(Summons.Blow(s, 20) > Summons.Blow(s, 10));
    }

    [Fact]
    public void The_road_and_the_bound_beasts_are_kept_in_the_save()
    {
        var h = At(10);
        Summons.Begin(h);
        Win(h);
        Summons.Name(h);
        Summons.Send(h);
        var back = JsonSerializer.Deserialize<Hero>(JsonSerializer.Serialize(h))!;
        Assert.Equal(SummonerStep.Summoner, back.Summoner);
        Assert.Equal([Summons.Orrogh], back.Bound);
        Assert.Equal(Summons.Lumira, back.Seeking);
        Assert.Equal(Summons.Orrogh, Summons.Callable(back)?.Id);
        Assert.Equal(SummonerStep.None, JsonSerializer.Deserialize<Hero>("{\"Level\":3}")!.Summoner); // an old save
    }

    [Fact]
    public void Every_great_one_lairs_on_dry_open_ground_far_out_apart_from_the_rest_the_same_for_everyone()
    {
        var world = WorldGen.Default;
        var lairs = Summons.Trials.Select(t => Summons.LairOf(world, t)).ToList();
        Assert.Equal(lairs, Summons.Trials.Select(t => Summons.LairOf(world, t)));
        foreach (var (x, z) in lairs)
        {
            Assert.InRange(Math.Sqrt(x * x + z * z), 1000, 3800);
            Assert.True(world.Height(x, z) > WorldGen.WaterLevel + 3);
            Assert.False(Northwood.Inside(x, z));
            Assert.Empty(world.SettlementsNear(x, z, 400));
        }
        for (var i = 0; i < lairs.Count; i++)
            for (var j = i + 1; j < lairs.Count; j++)
                Assert.True(Math.Sqrt(Math.Pow(lairs[i].X - lairs[j].X, 2) + Math.Pow(lairs[i].Z - lairs[j].Z, 2)) >= 900);
        Assert.All(Summons.Trials, t => Assert.NotNull(Summons.Get(t.Id)));
    }
}
