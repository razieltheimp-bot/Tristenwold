using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

/// <summary>Gardening, fires, pockets, the obstacle course, snares, the house, jewellery, and feats.</summary>
public class PursuitTests
{
    [Fact]
    public void Everything_the_pursuits_name_is_a_real_item()
    {
        Assert.All(Farming.Crops, c => { Assert.NotNull(Items.Get(c.Seed)); Assert.NotNull(Items.Get(c.Gives)); });
        Assert.All(Firemaking.Logs, l => Assert.NotNull(Items.Get(l.Log)));
        foreach (var m in new[] { Thieving.Commoner, Thieving.Merchant, Thieving.Guard, Thieving.Gentry }) Assert.All(m.Things, t => Assert.NotNull(Items.Get(t.Id)));
        foreach (var b in new[] { "PineForest", "Tundra", "Marsh", "AutumnForest" }) Assert.All(Trapping.Catches(b), c => Assert.NotNull(Items.Get(c.Id)));
        Assert.All(Housing.Stages.Concat(Housing.Furniture), s => Assert.All(s.Needs, n => Assert.NotNull(Items.Get(n.Id))));
        Assert.Equal(Feats.All.Count, Feats.All.Select(f => f.Feat.Id).Distinct().Count());
        Assert.Contains(Items.All, i => i.Slot == Slot.Ring);
        Assert.Contains(Items.All, i => i.Slot == Slot.Neck);
    }

    [Fact]
    public void Crops_grow_with_time_and_better_gardeners_grow_more()
    {
        var potato = Farming.FromSeed("potato_seed")!;
        Assert.Equal(0, Farming.Growth(potato, 100, 100));
        Assert.Equal(0.5, Farming.Growth(potato, 100, 100 + potato.GrowSeconds / 2), 3);
        Assert.Equal(1, Farming.Growth(potato, 100, 100 + potato.GrowSeconds * 3));
        var rng = new Rng(3);
        var novice = Enumerable.Range(0, 200).Average(_ => Farming.Yield(potato, 1, rng));
        var master = Enumerable.Range(0, 200).Average(_ => Farming.Yield(potato, 9, rng));
        Assert.True(master > novice, $"{novice} then {master}");
        Assert.True(Farming.Of("frostbloom")!.Level > potato.Level);
    }

    [Fact]
    public void Quick_hands_lift_more_purses_and_harder_marks_carry_more()
    {
        Assert.Same(Thieving.Guard, Thieving.MarkFor("a sergeant of the Watch"));
        Assert.Same(Thieving.Merchant, Thieving.MarkFor("fishmonger"));
        Assert.Same(Thieving.Gentry, Thieving.MarkFor("Laureate of the Bards' College"));
        Assert.Same(Thieving.Commoner, Thieving.MarkFor("a washerwoman"));
        Assert.True(Thieving.Chance(Thieving.Merchant, 1, Calling.Rogue, "Thief") > Thieving.Chance(Thieving.Merchant, 1, Calling.Warrior, "Barbarian"));
        Assert.True(Thieving.Chance(Thieving.Gentry, 10, Calling.Mage, "Wizard") > Thieving.Chance(Thieving.Gentry, 1, Calling.Mage, "Wizard"));
        var rng = new Rng(4);
        var poor = Enumerable.Range(0, 100).Sum(_ => { Thieving.Lift(Thieving.Commoner, rng, out var c); return c; });
        var rich = Enumerable.Range(0, 100).Sum(_ => { Thieving.Lift(Thieving.Gentry, rng, out var c); return c; });
        Assert.True(rich > poor * 3);
        Assert.True(Thieving.Fine(Thieving.Gentry) > Thieving.Fine(Thieving.Commoner));
    }

    [Fact]
    public void Practice_makes_fewer_falls_and_quicker_feet()
    {
        var wall = Agility.Course.First(o => o.Name == "the wall");
        Assert.True(Agility.FallChance(wall, 10) < Agility.FallChance(wall, 1));
        Assert.True(Agility.Speed(10) > Agility.Speed(1));
        Assert.Equal(1, Agility.Speed(1));
        Assert.True(Trapping.MaxSnares(9) > Trapping.MaxSnares(1));
        var rng = new Rng(5);
        Assert.Contains(Enumerable.Range(0, 60).SelectMany(_ => Trapping.Catch("Tundra", rng)), c => c.Id == "snow_hare_pelt");
        Assert.True(Firemaking.Catches(10) > Firemaking.Catches(1));
    }

    [Fact]
    public void A_house_is_built_in_order_and_furnished_after()
    {
        Assert.Equal(["foundation", "frame", "walls", "roof", "door"], Housing.Stages.Select(s => s.Id));
        Assert.Contains(Housing.Furniture, f => f.Id == "bed");
        Assert.Equal(1.1, Housing.TrophyBonus(10));
        Assert.Equal(1.1, Housing.TrophyBonus(25)); // no more than ten count
        // The makings come from the town's trades: planks from logs, nails from iron.
        Assert.Contains(Recipes.All, r => r.Makes == "plank" && r.Job == Job.Building);
        Assert.Contains(Recipes.All, r => r.Makes == "iron_nails" && r.Job == Job.Smithing);
        Assert.Contains(Recipes.For(Job.Crafting), r => r.Makes == "garnet_ring");
    }
}
