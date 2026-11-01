using Tristinwold.Deviant;

namespace Tristinwold.Deviant.Tests;

public class BandsTests
{
    static BattleReport Lost(string kind) => new("the wilds", BattleResult.PartyEscaped, [new EnemyReport(0, kind, kind, 100, false, false, BaseForm: kind)], ["Ana"]);

    [Fact]
    public void Each_rank_has_more_soldiers_than_the_last()
    {
        Assert.Equal(0, Bands.Soldiers(Rank.Stray)); // a new deviant is on its own
        Assert.True(Bands.Soldiers(Rank.Ringleader) > 0 && Bands.Soldiers(Rank.Scourge) > Bands.Soldiers(Rank.Ringleader) && Bands.Soldiers(Rank.Tyrant) > Bands.Soldiers(Rank.Scourge));
    }

    [Fact]
    public void There_is_no_limit_on_strays()
    {
        var w = new DeviantWorld(5);
        for (var i = 0; i < 60; i++) w.RecordBattle(Lost("bandit"));
        Assert.True(w.Strays.Count() > 12);
    }

    [Fact]
    public void Soldiers_break_away_and_strays_climb_by_themselves()
    {
        var w = new DeviantWorld(7);
        w.RecordBattle(Lost("bandit"));
        for (var i = 0; i < 120; i++) w.AdvanceDay();
        Assert.True(w.All.Count > 5, $"{w.All.Count} deviants");
        Assert.Contains(w.All, n => n.Rank >= Rank.Ringleader);
    }

    [Fact]
    public void Open_grudges_can_be_walked_in_on()
    {
        var w = new DeviantWorld(7);
        for (var i = 0; i < 6; i++) w.RecordBattle(Lost("bandit"));
        var seen = 0;
        for (var i = 0; i < 80 && seen == 0; i++)
        {
            w.AdvanceDay();
            seen = w.Feuds.Count();
        }
        Assert.True(seen > 0);
        Assert.All(w.Feuds, f => Assert.Contains(f.B.Id, f.A.Rivals));
    }

    [Fact]
    public void A_crowd_of_strays_doesnt_mean_a_deviant_in_every_fight()
    {
        var w = new DeviantWorld(9);
        for (var i = 0; i < 40; i++) w.RecordBattle(Lost("bandit"));
        var spawns = w.PlanEncounter("the wilds", [new FormationSlot(0, "bandit", "bandit"), new FormationSlot(1, "bandit", "bandit"), new FormationSlot(2, "bandit", "bandit")], ["Ana"]);
        Assert.True(spawns.Count <= w.Rules.MaxDeviantsPerBattle);
    }

    [Fact]
    public void A_soldier_who_rises_is_bound_by_blood_to_the_one_it_rose_under()
    {
        var w = new DeviantWorld(7);
        w.RecordBattle(Lost("bandit"));
        var first = w.All[0];
        for (var i = 0; i < 40 && first.Oaths.Count == 0; i++) w.AdvanceDay();
        Assert.NotEmpty(first.Oaths);
        var sworn = w.Find(first.Oaths.First())!;
        Assert.True(DeviantWorld.Sworn(first, sworn) && DeviantWorld.Sworn(sworn, first));
    }

    [Fact]
    public void The_sworn_never_feud_however_long_the_world_runs()
    {
        var w = new DeviantWorld(11);
        for (var i = 0; i < 8; i++) w.RecordBattle(Lost("bandit"));
        for (var i = 0; i < 400; i++)
        {
            w.AdvanceDay();
            Assert.All(w.Feuds, f => Assert.False(DeviantWorld.Sworn(f.A, f.B)));
        }
        Assert.Contains(w.All, n => n.Oaths.Count > 0);
    }

    [Fact]
    public void An_oath_survives_a_save()
    {
        var w = new DeviantWorld(7);
        w.RecordBattle(Lost("bandit"));
        for (var i = 0; i < 40 && w.All[0].Oaths.Count == 0; i++) w.AdvanceDay();
        Assert.Equal(w.All[0].Oaths, DeviantWorld.FromJson(w.ToJson()).All[0].Oaths);
    }
}
