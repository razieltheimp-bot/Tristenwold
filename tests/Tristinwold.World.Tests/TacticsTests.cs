using System.Text.Json;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>Deviants that hunt the hero, learn how they fight, and come back ready for it.</summary>
public class TacticsTests
{
    static Lessons Fought(int hits, DamageKind kind, AttackKind attack, double metres, double each = 20)
    {
        var l = new Lessons();
        for (var i = 0; i < hits; i++) l.Struck(each, kind, attack, metres);
        return l;
    }

    [Fact]
    public void A_few_blows_teach_it_nothing()
    {
        var counter = Tactics.Read(Fought(Tactics.HitsToLearn - 1, DamageKind.Fire, AttackKind.Spell, 20));
        Assert.Null(counter.Resists);
        Assert.False(counter.Lunges);
        Assert.Empty(counter.Lines);
    }

    [Fact]
    public void Burn_it_from_afar_and_it_learns_to_take_fire_and_close_the_gap()
    {
        var counter = Tactics.Read(Fought(12, DamageKind.Fire, AttackKind.Spell, 20));
        Assert.Equal(DamageKind.Fire, counter.Resists);
        Assert.True(counter.Lunges);
        Assert.True(counter.Speed > 1);
        Assert.Equal(1.5, counter.Ward); // spells, every one
        Assert.Equal(1, counter.Armor);
        Assert.Contains(counter.Lines, l => l.Said.Contains("Fire again"));
    }

    [Fact]
    public void Fight_it_blade_to_blade_and_its_hide_hardens()
    {
        var l = Fought(8, DamageKind.Physical, AttackKind.Weapon, 2);
        l.Struck(30, DamageKind.Frost, AttackKind.Spell, 3);
        var counter = Tactics.Read(l);
        Assert.Equal(DamageKind.Physical, counter.Resists);
        Assert.Equal(1.4, counter.Armor);
        Assert.False(counter.Lunges);
        Assert.Equal(1, counter.Ward);
    }

    [Fact]
    public void A_mix_of_everything_leaves_it_nothing_to_resist()
    {
        var l = new Lessons();
        foreach (var kind in new[] { DamageKind.Fire, DamageKind.Frost, DamageKind.Shock, DamageKind.Physical })
            for (var i = 0; i < 3; i++) l.Struck(20, kind, kind == DamageKind.Physical ? AttackKind.Weapon : AttackKind.Spell, i == 0 ? 2 : 15);
        Assert.Null(Tactics.Read(l).Resists);
    }

    [Fact]
    public void Running_makes_it_quicker_and_drinking_to_stay_up_makes_it_go_for_the_kill()
    {
        var l = new Lessons { Escapes = 2, Potions = 3 };
        var counter = Tactics.Read(l);
        Assert.Equal(1.2, counter.Speed, 6);
        Assert.Equal(1.3, counter.Finisher);
        // What's new since it last fought the hero is what it tells them.
        var before = Tactics.Read(new Lessons { Escapes = 2 });
        Assert.Equal(["finisher"], counter.Since(before).Select(x => x.Id));
        Assert.Equal(1.3, Tactics.Read(new Lessons { Escapes = 9 }).Speed, 6); // it doesn't get endlessly faster
    }

    [Fact]
    public void It_hunts_until_the_hero_reaches_a_town_or_gets_well_clear()
    {
        Assert.True(Tactics.StillHunting(100, heroInTown: false));
        Assert.False(Tactics.StillHunting(100, heroInTown: true));
        Assert.False(Tactics.StillHunting(Tactics.LoseRange + 1, heroInTown: false));
    }

    [Fact]
    public void It_follows_the_hero_into_town_only_when_it_thinks_it_can_kill_them_there()
    {
        Assert.False(Tactics.Dares(wins: 1, heroHealth: 1, learned: 0, guardsNear: 0)); // a fresh hero, beaten once: it waits outside
        Assert.True(Tactics.Dares(1, 0.4, 0, 0)); // the hero's hurt: it comes in after them
        Assert.False(Tactics.Dares(1, 0.4, 0, 2)); // unless the Watch is standing by
        Assert.True(Tactics.Dares(3, 1, 2, 0)); // one that has beaten them three times and learned their ways fears no walls
        Assert.False(Tactics.Dares(3, 1, 2, 3));
        Assert.True(Tactics.StillHunting(50, heroInTown: true, dares: true));
        Assert.False(Tactics.StillHunting(50, heroInTown: true, dares: false));
    }

    static Npc Townsman(string occupation, int age = 35) => new("t", "T", Sex.Male, age, occupation, "city:tristinwold", "Tristinwold", "", [], "warm", "", 0, 0);

    [Fact]
    public void Townsfolk_fight_throw_heal_or_take_cover_by_who_they_are()
    {
        Assert.Equal(Defender.Fights, TownDefence.Role(Townsman("blacksmith")));
        Assert.Equal(Defender.Fights, TownDefence.Role(Townsman("dockhand")));
        Assert.Equal(Defender.Fights, TownDefence.Role(Townsman("sergeant of the Watch")));
        Assert.Equal(Defender.Heals, TownDefence.Role(Townsman("priest of the Tide")));
        Assert.Equal(Defender.Throws, TownDefence.Role(Townsman("weaver")));
        Assert.Equal(Defender.Cowers, TownDefence.Role(Townsman("weaver", age: 12)));
        Assert.Equal(Defender.Cowers, TownDefence.Role(Townsman("blacksmith", age: 78)));
        Assert.Equal(Defender.Cowers, TownDefence.Role(Townsman("blacksmith"), captive: true));
        Assert.True(TownDefence.Blow(Defender.Fights, close: true) > TownDefence.Blow(Defender.Throws, close: false));
        Assert.Equal(0, TownDefence.Blow(Defender.Cowers, close: true));
    }

    [Fact]
    public void Something_that_beats_you_in_the_wilds_takes_a_name_and_roams_from_where_it_stood()
    {
        var book = new RaidBook();
        var r = book.Took("frost_troll", 600, -900, 1000, 77);
        Assert.Null(r.Lair);
        Assert.Equal("frost_troll", r.KindId);
        Assert.EndsWith(", Frost Troll", r.Title);
        Assert.Equal((600, -900), r.Where(WorldGen.Default, 1000)); // standing over the hero, at first
        Assert.NotEqual((600.0, -900.0), r.Where(WorldGen.Default, 1000 + 3600));

        // A good deal stronger than its kind, and worth the hunting.
        var (health, damage) = Roaming.Numbers(r, 5);
        var kind = Bestiary.Get("frost_troll");
        Assert.True(health > kind.Health * 1.8 && damage > kind.Damage * 1.25);
        var (xp, crowns) = Roaming.Bounty(r, 5);
        Assert.True(xp > 0 && crowns > 0);
        r.Wins++;
        Assert.True(Roaming.Numbers(r, 5).Health > health);
    }

    [Fact]
    public void Only_so_many_roam_the_wilds_and_the_least_of_them_moves_on()
    {
        var book = new RaidBook();
        var first = book.Took("wolf", 500, 500, 0, 1);
        first.Wins = 5;
        for (var i = 0; i < RaidBook.MostWild + 2; i++) book.Took("bandit", 500, 500, 10 + i, (ulong)(100 + i));
        Assert.Equal(RaidBook.MostWild, book.Roamers.Count);
        Assert.Contains(first, book.Roamers); // the one that has beaten you most stays
    }

    [Fact]
    public void What_it_learned_and_that_it_is_hunting_are_kept()
    {
        var book = new RaidBook();
        var r = book.Took("bandit", 400, 400, 0, 9);
        r.Hunting = true;
        r.Lessons.Struck(25, DamageKind.Shock, AttackKind.Spell, 18);
        r.Lessons.Escapes = 1;
        var copy = JsonSerializer.Deserialize<RaidBook>(JsonSerializer.Serialize(book))!.Roamers.Single();
        Assert.True(copy.Hunting);
        Assert.Equal("bandit", copy.KindId);
        Assert.Equal(25, copy.Lessons.Damage[DamageKind.Shock]);
        Assert.Equal(1, copy.Lessons.Escapes);
        Assert.Equal(r.Where(WorldGen.Default, 5000), copy.Where(WorldGen.Default, 5000));
    }

    sealed class Foe;

    [Fact]
    public void Only_a_foe_that_strikes_twice_running_counts()
    {
        var (a, b) = (new Foe(), new Foe());
        var s = new Strikes<Foe>();
        s.Struck(a);
        Assert.False(s.Counts(a)); // once is not enough
        s.Struck(a);
        Assert.True(s.Counts(a));
        Assert.Same(a, s.Holder);
        // Falling back, the hero is clipped by the other one in the hall: the first one's count is gone,
        // and the other has only the one.
        s.Struck(b);
        Assert.False(s.Counts(a));
        Assert.False(s.Counts(b));
        Assert.Null(s.Holder);
        // Back and forth between them never counts for either.
        s.Struck(a); s.Struck(b); s.Struck(a);
        Assert.Null(s.Holder);
        s.Struck(a);
        Assert.True(s.Counts(a));
        s.Clear();
        Assert.Null(s.Holder);
    }
}
