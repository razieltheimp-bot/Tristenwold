using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

public class TradeTests
{
    [Fact]
    public void Every_class_grows_three_more_moves_that_are_all_different_and_a_level_makes_them_stronger()
    {
        foreach (var c in Classes.All)
        {
            var moves = Advances.Of(c.Id);
            Assert.Equal([Advances.First, Advances.Second, Advances.Third], moves.Select(a => a.Level));
            Assert.Empty(Advances.Known(c.Id, 5));
            Assert.Equal(3, Advances.Known(c.Id, 18).Count());
        }
        var ids = Advances.Everything.Select(a => a.Move.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(ids, id => Classes.All.SelectMany(c => c.Skills).Any(s => s.Id == id));
        Assert.Equal("Ether Lance", Classes.Move("ether_lance")!.Name);
        Assert.Equal(MoveStyle.Passive, Advances.Of("EtherAeronaut")[0].Style);
        Assert.Equal(6, Advances.FlightLevel);
        var (young, old) = (new Hero { Level = 1 }, new Hero { Level = 20 });
        Assert.Equal(1, young.LevelPower(true));
        Assert.True(old.LevelPower(true) > old.LevelPower(false));
        Assert.True(old.LevelPower(false) > 1);
    }

    [Fact]
    public void Everything_that_can_be_killed_leaves_something_every_time()
    {
        foreach (var kind in Bestiary.All.Select(k => k.Id).Where(k => k != "sky_hull")) // a flying ship's hull is the ship
            for (var seed = 1UL; seed < 200; seed++)
            {
                var drop = Loot.Roll(kind, new Rng(seed));
                Assert.True(drop.Items.Count > 0 || drop.Crowns > 0, $"{kind} left nothing");
                Assert.NotEmpty(Loot.Roll(kind, new Rng(seed), young: true).Items);
            }
    }

    [Fact]
    public void A_horse_leaves_its_hide_or_its_meat_and_the_hide_makes_leather()
    {
        var drops = Enumerable.Range(1, 40).SelectMany(i => Loot.Roll("wild_horse", new Rng((ulong)i)).Items).ToList();
        Assert.Contains(drops, i => i.Id == "horse_hide");
        Assert.Contains(drops, i => i.Id == "raw_meat");
        Assert.Contains(Recipes.All, r => r.Makes == "leather" && r.Needs.Any(n => n.Id == "horse_hide"));
    }

    [Fact]
    public void Every_recipe_shop_and_drop_names_real_items()
    {
        foreach (var r in Recipes.All)
        {
            Assert.NotNull(Items.Get(r.Makes));
            Assert.All(r.Needs, n => Assert.NotNull(Items.Get(n.Id)));
        }
        foreach (var s in Shops.All) Assert.All(s.Sells, id => Assert.True(id == "meat" || Items.Get(id) is not null, $"{s.Name} sells {id}"));
        foreach (var kind in Bestiary.All.Select(k => k.Id))
            Assert.All(Loot.Roll(kind, new Rng(3)).Items, i => Assert.NotNull(Items.Get(i.Id)));
        Assert.Equal(Items.All.Count, Items.All.Select(i => i.Id).Distinct().Count());
        Assert.All(Items.All.Where(i => i.Wearable), i => Assert.True(i.Look is not null || i.Slot is Slot.Body or Slot.Ring or Slot.Neck, i.Id));
    }

    [Fact]
    public void Beasts_leave_what_they_would_and_dragons_leave_a_fortune()
    {
        var rng = new Rng(11);
        var wolf = Enumerable.Range(0, 20).SelectMany(_ => Loot.Roll("wolf", rng).Items).ToList();
        Assert.Contains(wolf, i => i.Id == "wolf_pelt");
        Assert.DoesNotContain(wolf, i => i.Id.StartsWith("dragon"));
        Assert.Equal(("wolf_pelt", 1), Assert.Single(Loot.Roll("wolf", rng, young: true).Items));
        var dragon = Loot.Roll("dragon_red", rng);
        Assert.Contains(dragon.Items, i => i.Id == "dragon_scale_red" && i.N >= 4);
        Assert.Contains(dragon.Items, i => i.Id == "dragon_bone");
        Assert.True(dragon.Crowns >= 150);
        Assert.Contains(Loot.Roll("dragon_black_corrupt", rng).Items, i => i.Id == "rift_crystal");
        // A lair's hoard: its trophy, and the first time its own piece of gear.
        var hoard = Loot.Hoard("raid:goblin_warren", rng, first: true);
        Assert.Contains(hoard, i => i.Id == "goblin_crown");
        Assert.True(hoard.Sum(i => Items.Get(i.Id)!.Value * i.N) > dragon.Items.Count);
    }

    [Fact]
    public void Gear_is_worn_and_makes_the_hero_stronger()
    {
        var pack = new Pack();
        pack.Add("iron_greatsword");
        pack.Add("iron_shield");
        pack.Add("iron_mail");
        Assert.True(pack.Equip("iron_shield") && pack.Equip("iron_mail"));
        Assert.Equal(18 + 26, pack.Armor);
        Assert.True(pack.Equip("iron_greatsword"));
        Assert.Null(pack.WornIn(Slot.Offhand)); // two hands for a greatsword
        Assert.Equal(1, pack.Count("iron_shield"));
        Assert.True(pack.Damage > 0.15);
        pack.Unequip(Slot.Weapon);
        Assert.Equal(1, pack.Count("iron_greatsword"));
        Assert.False(pack.Equip("bread"));
    }

    [Fact]
    public void Trades_make_things_from_their_makings_and_get_better_with_practice()
    {
        var pack = new Pack();
        var sword = Recipes.All.First(r => r.Makes == "iron_sword");
        Assert.False(pack.Make(sword));
        pack.Add("iron_ingot", 2);
        pack.Add("leather");
        Assert.True(pack.Make(sword));
        Assert.Equal(1, pack.Count("iron_sword"));
        Assert.Equal(0, pack.Count("iron_ingot"));
        Assert.True(pack.JobXp[Job.Smithing] > 0);
        var plate = Recipes.All.First(r => r.Makes == "steel_plate");
        pack.Add("steel_ingot", 5);
        pack.Add("leather", 2);
        Assert.False(pack.Make(plate)); // not skilled enough yet
        pack.Practise(Job.Smithing, Pack.XpFor(plate.Level));
        Assert.True(pack.Make(plate));
        Assert.Equal(Pack.MaxJobLevel, new Pack { JobXp = { [Job.Herbalism] = 1_000_000 } }.Level(Job.Herbalism));
    }

    [Fact]
    public void Shops_sell_what_they_stock_and_buy_anything_for_half()
    {
        var pack = new Pack();
        var (crowns, meat) = (100, 0);
        Assert.True(Shops.Buy(Shops.Smithy, "iron_sword", pack, ref crowns, ref meat));
        Assert.Equal(60, crowns);
        Assert.False(Shops.Buy(Shops.Smithy, "dragon_heart", pack, ref crowns, ref meat));
        Assert.True(Shops.Buy(Shops.Provisions, "meat", pack, ref crowns, ref meat));
        Assert.Equal(1, meat);
        Assert.True(Shops.Sell("iron_sword", pack, ref crowns));
        Assert.Equal(60 - Shops.MeatPrice + 20, crowns);
        Assert.False(Shops.Sell("iron_sword", pack, ref crowns));
        Assert.Equal("3 Wolf Pelts", Items.Count("wolf_pelt", 3));
        Assert.Equal("2 Travel Rations", Items.Count("travel_rations", 2));
    }
}
