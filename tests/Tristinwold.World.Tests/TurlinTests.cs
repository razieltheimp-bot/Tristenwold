using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class TurlinTests
{
    [Fact]
    public void A_clue_taken_to_a_druid_turns_the_trail_round()
    {
        var t = new TurlinBook();
        Assert.True(t.Hire());
        Assert.False(t.Report()); // nothing found yet
        Assert.True(t.Find());
        Assert.True(t.Report());
        Assert.True(t.Doubt());
        Assert.False(t.AtTheCamp);
        Assert.True(t.Won());
        Assert.Equal((1, TurlinStage.None), (t.Case, t.Stage));
        Assert.True(t.AtTheCamp);
    }

    [Fact]
    public void A_guardian_killed_on_his_word_closes_the_case_and_he_pays()
    {
        var t = new TurlinBook();
        t.Hire();
        t.Find();
        Assert.False(t.Slew()); // not sold on it yet: a guardian killed for its own sake isn't his
        t.Report();
        Assert.True(t.Slew());
        Assert.Equal(TurlinBook.BlamePay, t.Settle());
        Assert.Equal(1, t.Case);
        Assert.Equal(0, t.Settle());
    }

    [Fact]
    public void He_kills_a_hero_once_and_learns_what_the_ring_does_when_they_come_back()
    {
        var t = new TurlinBook();
        t.HeWon("Ana", 1, 0);
        Assert.True(t.Waiting && !t.Knows);
        Assert.True(t.Returned());
        Assert.True(t.Knows && !t.Waiting);
        t.HeWon("Ana", 2, 0);
        Assert.False(t.Returned()); // he knows now: there's no surprise left in it
        t.Hire(); t.Find(); t.Report(); t.Slew();
        Assert.Equal(TurlinBook.ImmortalPay, t.Settle());
    }

    [Fact]
    public void Each_time_hes_killed_he_comes_back_more_beast_and_still_named_turlin()
    {
        var t = new TurlinBook();
        Assert.Equal(("turlin", 0f), (t.Form.Kind, t.Form.Demon));
        var last = -1f;
        for (var i = 0; i < 9; i++)
        {
            Assert.True(t.Form.Demon >= last);
            last = t.Form.Demon;
            t.HeLost("Ana", DamageKind.Fire, i, 0);
            Assert.Equal("Turlin", t.Self.Name);
            Assert.True(t.Self.Alive);
        }
        Assert.Equal(("giant_ape", 1f), (t.Form.Kind, t.Form.Demon));
        Assert.Equal(9, t.Deaths);
        Assert.Single(t.Self.Traits); // he learns to stand fire once
        Assert.Equal(9, t.Self.Scars.Count);
    }

    [Fact]
    public void Getting_away_costs_him_nothing_of_himself_but_still_makes_the_hero_stronger()
    {
        var t = new TurlinBook();
        t.HeEscaped("Ana", 1, 100);
        Assert.Equal((0, 1), (t.Deaths, t.Edge));
        Assert.Equal(1 + TurlinBook.EdgeGain, t.EdgeBonus, 6);
        Assert.Equal(100 + TurlinBook.Between, t.NextAt);
        for (var i = 0; i < 50; i++) t.HeEscaped("Ana", 1, 100);
        Assert.Equal(1 + TurlinBook.EdgeGain * TurlinBook.MostEdge, t.EdgeBonus, 6);
    }

    [Fact]
    public void He_stays_close_to_the_heros_strength_and_his_wins_push_him_up()
    {
        var t = new TurlinBook();
        Assert.Equal(2, t.LevelAgainst(1)); // a fresh hero isn't facing a monster
        Assert.Equal(2, t.LevelAgainst(3));
        for (var i = 0; i < 10; i++) t.HeWon("Ana", i, 0);
        Assert.Equal(5, t.LevelAgainst(3)); // however often he's won, never far ahead
        Assert.Equal(49, new TurlinBook().LevelAgainst(50)); // and never left behind
    }

    [Fact]
    public void He_leaves_the_fruit_alone_at_first()
    {
        var t = new TurlinBook();
        Assert.False(t.CanEat);
        for (var i = 0; i < TurlinBook.FruitAfter; i++) t.HeEscaped("Ana", 1, 0);
        Assert.True(t.CanEat);
    }

    [Fact]
    public void He_survives_a_save()
    {
        var t = new CompanySave { Forest = new ForestBook() };
        t.Forest.Turlin.HeLost("Ana", DamageKind.Frost, 3, 0);
        t.Forest.Turlin.HeWon("Ana", 4, 0);
        var back = CompanySave.FromJson(t.ToJson()).Forest!.Turlin;
        Assert.Equal((1, 1, 1, t.Forest.Turlin.Self.Id), (back.Deaths, back.Self.Wins, back.Self.Traits.Count, back.Self.Id));
        Assert.True(back.Waiting);
        Assert.Contains("Turlin", back.Sheet());
    }

    [Fact]
    public void His_cases_run_out_and_each_hangs_on_a_true_guardian()
    {
        var trueOnes = Northwood.Guardians.Where(g => !g.Corrupted).Select(g => g.Id).ToList();
        Assert.All(TurlinBook.Cases, c => Assert.Contains(c.Guardian, trueOnes));
        Assert.Equal(TurlinBook.Cases.Count, TurlinBook.Cases.Select(c => c.Guardian).Distinct().Count());
        var first = TurlinBook.Cases[0];
        Assert.Equal(first.Guardian, TurlinBook.Blame(first, new HashSet<string>(), trueOnes));
        Assert.NotEqual(first.Guardian, TurlinBook.Blame(first, new HashSet<string> { first.Guardian }, trueOnes));
        Assert.Null(TurlinBook.Blame(first, trueOnes.ToHashSet(), trueOnes));
        var t = new TurlinBook { Case = TurlinBook.Cases.Count };
        Assert.True(t.Over);
        Assert.False(t.Hire());
    }

    [Fact]
    public void His_clues_and_his_den_are_on_ground_in_the_wood()
    {
        Assert.True(Northwood.Inside(Northwood.DenX, Northwood.DenZ) && !Northwood.OnRoad(Northwood.DenX, Northwood.DenZ));
        foreach (var c in TurlinBook.Cases)
        {
            var (x, z) = Northwood.ClueFor(c.Guardian);
            Assert.True(Northwood.Inside(x, z), c.Guardian);
        }
    }

    [Fact]
    public void Soren_is_the_worst_name_on_the_water_and_keeps_to_the_far_seas()
    {
        Assert.Equal("soren", Seas.Flagships[0].Id);
        Assert.Equal("wukong", Seas.Flagships[1].Id);
        Assert.True(Seas.Flagships[0].Bounty > Seas.Flagships[1].Bounty);
        for (var i = 0; i < 300; i++) Assert.Null(Seas.Flies(1, new Rng((ulong)i)));
        for (var i = 0; i < 300; i++) Assert.NotEqual("soren", Seas.Flies(2, new Rng((ulong)i))?.Id);
        Assert.Contains(Enumerable.Range(0, 300), i => Seas.Flies(4, new Rng((ulong)i))?.Id == "soren");
        Assert.All(Seas.Flagships, f => Assert.True(Bestiary.Get(f.Kind) is not null && Items.Get(f.Loot) is not null));
    }

    [Fact]
    public void A_named_captain_pays_in_full_only_the_first_time()
    {
        var sea = new SeaBook();
        var f = Seas.Flagships[1];
        Assert.Equal(f.Bounty, sea.Humble(f));
        Assert.Equal(f.Bounty / 5, sea.Humble(f));
    }
}
