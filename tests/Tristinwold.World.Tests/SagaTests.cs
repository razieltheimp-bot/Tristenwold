using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>Pact dragons and the dragon knights, the corrupted, the young of the wilds, and the road to Dragon Knight.</summary>
public class SagaTests
{
    static readonly WorldGen World = WorldGen.Default;
    static readonly List<Ancient> Many = Dragons.Near(World, 0, 0, 40000).ToList();

    [Fact]
    public void Some_ancients_are_corrupted_and_some_metallics_keep_a_pact_with_a_town()
    {
        Assert.Contains(Many, a => a.Corrupted);
        Assert.Contains(Many, a => a.Allied);
        Assert.All(Many.Where(a => a.Allied), a =>
        {
            Assert.True(a.Metallic && !a.Corrupted);
            Assert.True(a.Ward!.DistanceTo(a.X, a.Z) <= a.Territory);
            var watch = Dragons.Watch(a).ToList();
            Assert.All(watch, h => Assert.True(h.Riders && h.Allied && h.Faction == a.Id));
        });
        Assert.All(Many.Where(a => a.Corrupted), a => Assert.True(Dragons.IsCorrupted(a.KindId)));
        Assert.Equal(DragonColour.Red, Dragons.ColourOf("dragon_red_corrupt"));
    }

    [Fact]
    public void Corrupted_dragons_fight_everyone_and_the_alliance_fends_off_chromatic_and_corrupted()
    {
        Assert.True(Allegiance.Hostile(Side.Corrupted, Side.Metallic));
        Assert.True(Allegiance.Hostile(Side.Corrupted, Side.Chromatic));
        Assert.True(Allegiance.Hostile(Side.Alliance, Side.Chromatic));
        Assert.True(Allegiance.Hostile(Side.Alliance, Side.Corrupted));
        Assert.True(Allegiance.Hostile(Side.Chromatic, Side.Alliance));
        Assert.False(Allegiance.Hostile(Side.Alliance, Side.Metallic));
        Assert.False(Allegiance.Hostile(Side.Metallic, Side.Chromatic));
        Assert.False(Allegiance.Hostile(Side.Wild, Side.Company));
        Assert.False(Allegiance.Hostile(Side.Corrupted, Side.Corrupted));
    }

    [Fact]
    public void Pact_towns_are_raided_some_seasons()
    {
        var pact = Many.First(a => a.Allied);
        var start = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        var raids = Enumerable.Range(0, 30).SelectMany(i => Dragons.RaidsNear(World, start.AddHours(i * Dragons.SeasonHours), pact.Ward!.X, pact.Ward.Z, 500)).ToList();
        Assert.NotEmpty(raids);
        Assert.All(raids, r =>
        {
            var c = Bestiary.Get(r.KindId);
            Assert.True(Dragons.IsCorrupted(r.KindId) || !Dragons.IsMetallic(Dragons.ColourOf(r.KindId)!.Value), $"{c.Name} raiding");
        });
    }

    [Fact]
    public void Herds_have_young_with_them_and_towns_have_youths()
    {
        var herds = Wildlife.Near(World, 3000, 3000, 2500).ToList();
        Assert.Contains(herds, h => h.Young > 0);
        Assert.All(herds.Where(h => h.Young > 0), h => Assert.True(Taming.Tameable(h.KindId) || Dragons.IsCorrupted(h.KindId), h.KindId));
        var town = World.SettlementsNear(0, 0, 6000).First(s => s.Kind == SettlementKind.Town);
        var youths = World.YouthsOf(town);
        Assert.NotEmpty(youths);
        Assert.All(youths, y => Assert.True(People.IsYouth(y)));
        Assert.Null(Quests.For(World, youths[0])); // they don't hand out work
    }

    [Fact]
    public void The_road_to_dragon_knight()
    {
        var saga = new DragonSaga();
        Assert.True(World.Height(DragonSaga.RookeryX, DragonSaga.RookeryZ) > WorldGen.WaterLevel + 1, "the rookery is under water");
        Assert.True(saga.Begin(World));
        var matriarch = Many.Concat(Dragons.Near(World, 0, 0, 30000)).First(a => a.Id == saga.MatriarchId);
        Assert.True(matriarch.Allied);
        // It's earned: no egg, and no word of where one is, until Ser Aldric is satisfied.
        Assert.Equal(SagaStage.Proving, saga.Stage);
        Assert.False(saga.TakeEgg());
        Assert.Contains("level 5", saga.Objective(null, 2));
        Assert.Equal(6, saga.Feed(6));
        Assert.False(saga.Bless(9)); // the dragons are still hungry
        Assert.Equal(4, saga.Feed(30)); // no more than they need
        Assert.False(saga.Bless(DragonSaga.MinLevel - 1)); // and the hero isn't ready
        Assert.True(saga.Bless(DragonSaga.MinLevel));
        Assert.Equal(SagaStage.SeekEgg, saga.Stage);
        // He says roughly which way, no more; the tracker shows no spot until the hero has found it.
        Assert.Matches("to the (north|south|east|west)", saga.Hint);
        Assert.Null(saga.Goal(null));
        Assert.Contains("for yourself", saga.Objective(null));
        saga.Found();
        Assert.Equal((saga.NestX, saga.NestZ), saga.Goal(null));
        Assert.True(saga.TakeEgg());
        Assert.False(saga.Warm(DragonSaga.HatchSeconds / 2, resting: false));
        Assert.True(saga.Warm(DragonSaga.HatchSeconds / 12, resting: true));
        Assert.Equal(SagaStage.Raising, saga.Stage);
        var dragon = saga.Hatch(new Rng(5));
        Assert.Equal(Dragons.KindFor(matriarch.Colour), dragon.KindId);
        Assert.False(saga.Knighted(dragon));
        while (dragon.Level < 10) dragon.Gain(Bond.XpToNext(dragon.Level));
        Assert.Equal((DragonSaga.RookeryX, DragonSaga.RookeryZ), saga.Goal(dragon));
        Assert.True(saga.Knighted(dragon));

        // A hatchling lost sends you back for another egg.
        var again = new DragonSaga();
        again.Begin(World);
        again.Feed(10);
        again.Bless(10);
        again.TakeEgg();
        again.Warm(DragonSaga.HatchSeconds, false);
        again.Lose();
        Assert.Equal(SagaStage.SeekEgg, again.Stage);
        Assert.Equal(1, again.Lost);

        // And a knight's grown dragon lost: another egg, raised from the start, and kneel again.
        saga.Lose();
        Assert.Equal(SagaStage.SeekEgg, saga.Stage);
        Assert.Equal(1, saga.Lost);
    }

    [Fact]
    public void Heroes_and_companions_are_saved()
    {
        var hero = new Hero { Discipline = "Ranger", Level = 7, Points = 2, Ranks = { ["volley"] = 3 } };
        var pet = new Bond { KindId = "bear", Name = "Bramble", Level = 4, Fed = 50 };
        var save = CompanySave.Capture(1, 10, new ExploredMap(), new QuestLog(), hero, [pet], new DragonSaga { Stage = SagaStage.CarryEgg, Warmth = 0.4 }, meat: 3);
        var back = CompanySave.FromJson(save.ToJson());
        Assert.Equal("Ranger", back.Hero!.Discipline);
        Assert.Equal(3, back.Hero.Rank("volley"));
        Assert.Equal("Bramble", back.Companions.Single().Name);
        Assert.Equal(50, back.Companions[0].Fed);
        Assert.Equal(0.4, back.Saga!.Warmth);
        Assert.Equal(3, back.Meat);
    }

    [Fact]
    public void A_dragon_won_over_in_the_wilds_and_raised_can_be_knighted_too()
    {
        var saga = new DragonSaga(); // never asked Ser Aldric for anything
        var wyrmling = new Bond { KindId = "dragon_red", Role = BondRole.Dragon, Name = "Ash" };
        Assert.False(saga.ReadyToKnight(wyrmling));
        while (wyrmling.Level < 10) wyrmling.Gain(Bond.XpToNext(wyrmling.Level));
        Assert.True(saga.ReadyToKnight(wyrmling));
        Assert.True(saga.Knighted(wyrmling));
        Assert.Equal(SagaStage.Knighted, saga.Stage);
        Assert.False(saga.Knighted(wyrmling)); // once
    }
}
