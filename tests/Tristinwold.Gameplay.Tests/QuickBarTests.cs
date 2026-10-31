using System.Text.Json;

namespace Tristinwold.Gameplay.Tests;

public class QuickBarTests
{
    static Hero Hero(string way = "Paladin")
    {
        var hero = new Hero { Discipline = way };
        hero.Bar.Arrange(hero);
        return hero;
    }

    [Fact]
    public void A_new_hero_finds_their_four_moves_on_1_to_4_and_5_free()
    {
        var c = Classes.Get("Paladin");
        Assert.Equal(new[] { c.Primary.Id, c.Signature.Id, c.Utility.Id, c.Limit.Id, null }, Hero().Bar.Slots.Take(5));
    }

    [Fact]
    public void The_keys_read_1_to_9_then_0()
    {
        Assert.Equal("1234567890", string.Concat(Enumerable.Range(0, QuickBar.Size).Select(QuickBar.Key)));
    }

    [Fact]
    public void Every_class_lays_out_cleanly_and_no_key_holds_a_passive()
    {
        foreach (var c in Classes.All)
        {
            var hero = Hero(c.Id);
            Assert.Equal(4, hero.Bar.Slots.Count(s => s is not null));
            Assert.DoesNotContain(c.Passive.Id, hero.Bar.Slots);
        }
    }

    [Fact]
    public void A_second_class_takes_the_old_keys_and_an_old_move_goes_on_the_free_one()
    {
        var hero = Hero();
        hero.Join("Wizard");
        hero.Bar.Arrange(hero);
        var (w, p) = (Classes.Get("Wizard"), Classes.Get("Paladin"));
        Assert.Equal(new[] { w.Primary.Id, w.Signature.Id, w.Utility.Id, w.Limit.Id, p.Primary.Id }, hero.Bar.Slots.Take(5));
    }

    [Fact]
    public void You_can_swap_in_what_you_learned_but_not_another_class_s_limit()
    {
        var hero = Hero();
        hero.Join("Wizard");
        var moves = QuickBar.Moves(hero).Select(a => a.Id).ToList();
        Assert.Contains(Classes.Get("Paladin").Signature.Id, moves);
        Assert.Contains(Classes.Get("Wizard").Limit.Id, moves);
        Assert.DoesNotContain(Classes.Get("Paladin").Limit.Id, moves);
    }

    [Fact]
    public void A_move_keeps_the_rank_it_earned_in_its_old_class()
    {
        var hero = new Hero { Points = 3 };
        hero.Raise("smite");
        hero.Raise("smite");
        hero.Join("Wizard");
        Assert.Equal(3, hero.Earned("smite"));
        Assert.Equal(1.3, hero.MovePower("smite"), 6);
        Assert.Equal(1, hero.Earned("bolt"));
    }

    [Fact]
    public void Picking_something_already_on_the_bar_swaps_the_two()
    {
        var bar = Hero().Bar;
        var (first, second) = (bar.Slots[0], bar.Slots[1]);
        bar.Set(0, second);
        Assert.Equal((second, first), (bar.Slots[0], bar.Slots[1]));
    }

    [Fact]
    public void A_move_taken_off_stays_off()
    {
        var hero = Hero();
        var move = hero.Bar.Slots[2];
        hero.Bar.Set(2, null);
        hero.Bar.Arrange(hero);
        Assert.Null(hero.Bar.Slots[2]);
        hero.Bar.Set(4, move);
        Assert.Equal(move, hero.Bar.Slots[4]);
        Assert.DoesNotContain(move!, hero.Bar.Left);
    }

    [Fact]
    public void Potions_find_their_own_keys_from_6_up_and_stay_off_once_taken_off()
    {
        var bar = Hero().Bar;
        bar.Stock(["healing_draught", "mana_draught"]);
        Assert.Equal(new[] { "healing_draught", "mana_draught", null, null, null }, bar.Slots.Skip(5));
        bar.Set(5, null);
        bar.Stock(["healing_draught", "mana_draught"]);
        Assert.Null(bar.Slots[5]);
    }

    [Fact]
    public void Potions_never_crowd_the_moves_when_the_keys_are_full()
    {
        var bar = Hero().Bar;
        bar.Stock(Enumerable.Range(0, 9).Select(i => $"potion{i}"));
        Assert.Equal(Enumerable.Range(0, 5).Select(i => $"potion{i}"), bar.Slots.Skip(5));
        Assert.Null(bar.Slots[4]);
    }

    [Fact]
    public void The_bar_survives_a_save_and_an_old_save_gets_a_fresh_one()
    {
        var hero = Hero();
        hero.Bar.Stock(["healing_draught"]);
        hero.Bar.Set(1, null);
        var back = JsonSerializer.Deserialize<Hero>(JsonSerializer.Serialize(hero))!;
        Assert.Equal(hero.Bar.Slots, back.Bar.Slots);
        Assert.Equal(hero.Bar.Left, back.Bar.Left);
        var old = JsonSerializer.Deserialize<Hero>("""{"Discipline":"Thief"}""")!;
        old.Bar.Arrange(old);
        Assert.Equal(Classes.Get("Thief").Primary.Id, old.Bar.Slots[0]);
    }
}
