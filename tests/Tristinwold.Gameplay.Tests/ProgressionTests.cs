using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

/// <summary>The twelve classes, levels and skill ranks, companions growing up, and taming the young.</summary>
public class ProgressionTests
{
    [Fact]
    public void Twelve_classes_three_to_a_calling_each_with_its_own_moves()
    {
        Assert.Equal(12, Classes.All.Count);
        foreach (var calling in Enum.GetValues<Calling>()) Assert.Equal(3, Classes.Of(calling).Count());
        // No two classes share a move (bar the mages' element switch).
        var moves = Classes.All.SelectMany(d => d.Skills).Where(a => a.Id != "attune").Select(a => a.Id).ToList();
        Assert.Equal(moves.Count, moves.Distinct().Count());
        Assert.All(Classes.All, d => Assert.False(string.IsNullOrWhiteSpace(d.Text)));
        // Old characters keep the nearest class.
        Assert.Equal("Paladin", Classes.For("Knight").Id);
        Assert.Equal("Assassin", Classes.For("Rogue_Hooded").Id);
        Assert.Equal("Barbarian", Classes.Get("barbarian").Id);
        // The bards: a poet, a scribe and a musician, taught at the Bards' College.
        Assert.Equal(["Poet", "Scribe", "Musician"], Classes.Of(Calling.Bard).Select(d => d.Id));
        Assert.Equal("the Bards' College", Classes.GuildOf(Calling.Bard));
    }

    [Fact]
    public void Levels_bring_skill_points_that_rank_up_moves()
    {
        var hero = new Hero { Discipline = "Barbarian" };
        Assert.Equal(0, hero.Gain(Hero.XpToNext(1) - 1));
        Assert.Equal(1, hero.Gain(1));
        Assert.Equal(2, hero.Level);
        Assert.Equal(1, hero.Points);
        Assert.True(hero.Raise("cleave"));
        Assert.False(hero.Raise("cleave")); // no points left
        Assert.Equal(1.15, hero.Power("cleave"), 3);
        hero.Gain(100000);
        while (hero.Raise("cleave")) { }
        Assert.Equal(Hero.MaxRank, hero.Rank("cleave"));
        // Joining another class gives the points back.
        var points = hero.Points;
        hero.Join("Wizard");
        Assert.Equal(points + Hero.MaxRank - 1, hero.Points);
        Assert.Equal(1, hero.Rank("cleave"));
    }

    [Fact]
    public void A_companion_grows_up_learns_moves_and_gets_hungry()
    {
        var pup = new Bond { KindId = "wolf", Role = BondRole.Pet };
        Assert.True(pup.Size < 0.5);
        Assert.Empty(pup.Skills);
        pup.Gain(100000);
        Assert.True(pup.Level > 10);
        Assert.Equal(1, pup.Growth);
        Assert.Contains(pup.Skills, s => s.Name == "Savage Bite");
        pup.Tick(Bond.HungerSeconds);
        Assert.True(pup.Starving);
        var hungry = pup.Strength;
        pup.Feed();
        Assert.True(pup.Strength > hungry);

        var squire = new Bond { KindId = "squire", Role = BondRole.Squire };
        squire.Tick(Bond.HungerSeconds * 2);
        Assert.False(squire.Hungry); // people feed themselves
    }

    [Fact]
    public void A_dragon_can_carry_its_knight_from_level_ten_and_is_lost_if_it_falls_younger()
    {
        var dragon = new Bond { KindId = "dragon_gold", Role = BondRole.Dragon };
        Assert.True(dragon.DiesIfFelled);
        Assert.False(dragon.Rideable);
        Assert.Equal("hatchling", dragon.Stage);
        while (dragon.Level < 10) dragon.Gain(Bond.XpToNext(dragon.Level));
        Assert.True(dragon.Rideable);
        Assert.False(dragon.DiesIfFelled);
        Assert.Equal(1, dragon.Size);
        Assert.Contains(dragon.Skills, s => s.Id == "breath");
    }

    [Fact]
    public void Young_beasts_and_monsters_can_be_tamed_but_not_people_or_the_dead()
    {
        Assert.True(Taming.Tameable("wolf"));
        Assert.True(Taming.Tameable("frost_troll"));
        Assert.True(Taming.Tameable("dragon_red"));
        Assert.False(Taming.Tameable("dragon_red_corrupt"));
        Assert.False(Taming.Tameable("bandit"));
        Assert.False(Taming.Tameable("draugr"));
        Assert.Equal("Bear Cub", Taming.YoungName("bear", "Cave Bear"));
        Assert.Equal("Red Wyrmling", Taming.YoungName("dragon_red", "Red Dragon"));
        Assert.Equal(0, Taming.Chance(kinNearby: true, offeringMeat: true, beastmaster: true, 30));
        Assert.Equal(1, Taming.Chance(false, false, beastmaster: true, 1));
        Assert.True(Taming.Chance(false, true, false, 1) > Taming.Chance(false, false, false, 1));
    }

    [Fact]
    public void Corrupted_dragons_are_tougher_and_never_wary()
    {
        var gold = Bestiary.Get("dragon_gold");
        var fallen = Bestiary.Get("dragon_gold_corrupt");
        Assert.Equal("dragon_gold_corrupt", fallen.Id);
        Assert.True(gold.Wary);
        Assert.False(fallen.Wary);
        Assert.True(fallen.Health > gold.Health && fallen.Damage > gold.Damage);
    }
}

/// <summary>The Rift spreading from dragon to dragon.</summary>
public class CorruptionTests
{
    [Fact]
    public void Enough_wounds_from_a_corrupted_dragon_turn_another_and_a_little_ebbs_away()
    {
        var c = 0.0;
        c = Corruption.Infect(c, 150, 600);
        Assert.False(Corruption.Turns(c));
        Assert.Equal(0.4, c, 3);
        Assert.Equal(0.4 - 30 / Corruption.EbbSeconds, Corruption.Ebb(c, 30), 3);
        Assert.Equal(0, Corruption.Ebb(c, 1000));
        for (var i = 0; i < 3; i++) c = Corruption.Infect(c, 80, 600);
        Assert.True(Corruption.Turns(c));
        Assert.Equal(1, c); // never more than full
    }

    [Fact]
    public void A_wyrmling_is_far_harder_to_win_over_than_any_beast()
    {
        // It won't look at a green hero at all, and a ranger's knack doesn't decide it.
        Assert.Equal(0, Taming.Chance(false, true, true, Taming.DragonLevel - 1, 10, dragon: true));
        Assert.True(Taming.Chance(false, false, true, Taming.DragonLevel, dragon: true) < 1);
        // Far worse odds than a wolf pup's, for the same hero.
        Assert.True(Taming.Chance(false, true, false, 10, 3, dragon: true) < Taming.Chance(false, true, false, 10, 3) / 2);
        // Years and a way with beasts count for most; meat for little; never better than even.
        Assert.True(Taming.Chance(false, false, false, 20, 8, dragon: true) > Taming.Chance(false, false, false, 10, 1, dragon: true));
        Assert.True(Taming.Chance(false, true, false, 10, dragon: true) - Taming.Chance(false, false, false, 10, dragon: true) <= 0.05 + 1e-9);
        Assert.Equal(0.5, Taming.Chance(false, true, true, 60, 10, dragon: true));
        Assert.Equal(0, Taming.Chance(true, true, true, 60, 10, dragon: true)); // and never with its kin close
        Assert.True(Taming.Tameable("dragon_red") && Taming.Tameable("dragon_gold") && !Taming.Tameable("dragon_red_corrupt"));
    }
}
