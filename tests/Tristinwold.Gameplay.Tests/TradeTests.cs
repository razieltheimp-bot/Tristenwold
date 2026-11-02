using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

public class TradeTests
{
    [Fact]
    public void A_mount_jumps_by_its_speed_and_its_growth()
    {
        var (horse, wild) = (Bestiary.Get("horse").Speed, Bestiary.Get("wild_horse").Speed);
        Assert.True(Taming.MountJump(wild, 6) > Taming.MountJump(horse, 6));
        Assert.True(Taming.MountJump(horse, 20) > Taming.MountJump(horse, 6));
        var height = Math.Pow(Taming.MountJump(horse, 6), 2) / (2 * 9.8);
        Assert.InRange(height, 1.3, 2.2);
    }

    static Bond Horse() => new() { KindId = "horse", Role = BondRole.Pet, Level = 6 };
    static Bond Wolf() => new() { KindId = "wolf", Role = BondRole.Pet, Level = 2 };
    static Bond Youth() => new() { KindId = "squire", Role = BondRole.Squire, Level = 2 };

    [Fact]
    public void Apprentices_pets_mounts_and_the_golem_are_counted_apart()
    {
        var party = new List<Bond> { new() { KindId = "golem", Role = BondRole.Golem, Level = 5 } };
        party.AddRange([Wolf(), Wolf(), Wolf(), Wolf()]);
        Assert.False(PartyRules.MayFollow(party, Wolf(), freePets: false)); // four pets, and the golem didn't take a place
        Assert.True(PartyRules.MayFollow(party, Wolf(), freePets: true)); // a Ranger or a Shapeshifter
        var foal = new Bond { KindId = "horse", Role = BondRole.Pet, Level = 1 };
        Assert.True(PartyRules.IsPet(foal) && !PartyRules.IsMount(foal));
        party.Add(Horse());
        Assert.False(PartyRules.MayFollow(party, Horse(), freePets: false)); // one rider, one mount
        party.Add(Youth());
        Assert.Equal(2, PartyRules.Riders(party));
        Assert.True(PartyRules.MayFollow(party, Horse(), freePets: false)); // an apprentice brings a place for a mount
        party.AddRange([Youth(), Youth()]);
        Assert.False(PartyRules.MayFollow(party, Youth(), freePets: false)); // three apprentices at most
        Assert.True(PartyRules.MayFollow(party, Youth(), freePets: false, freeApprentices: true)); // but a Paladin or a Wizard takes on all who come
        Assert.True(PartyRules.FreeApprentices("Paladin") && PartyRules.FreeApprentices("Wizard") && !PartyRules.FreeApprentices("Ranger"));
    }

    [Fact]
    public void Only_artificers_have_constructs_and_the_one_who_climbs_inside_has_one()
    {
        Assert.Equal(0, PartyRules.Constructs(Calling.Warrior, "Paladin", 20));
        Assert.Equal(1, PartyRules.Constructs(Calling.Artificer, "Alchemist", 5));
        Assert.Equal(2, PartyRules.Constructs(Calling.Artificer, "Alchemist", 6));
        Assert.Equal(1, PartyRules.Constructs(Calling.Artificer, "Saboteur", 20)); // a saboteur forges mites instead
        Assert.Equal(1, PartyRules.Constructs(Calling.Artificer, "EtherAeronaut", 20));
    }

    [Fact]
    public void Mites_are_unlimited_named_in_turn_and_break_for_good()
    {
        var mites = Enumerable.Range(0, 40).Select(i => new Bond { KindId = "clockwork_mite", Role = BondRole.Mite, Level = 3, Name = PartyRules.MiteName(i) }).ToList();
        Assert.True(PartyRules.MayFollow(mites, new Bond { Role = BondRole.Mite }, freePets: false));
        Assert.Equal(40, mites.Select(m => m.Name).Distinct().Count());
        Assert.All(mites, m => Assert.True(m.Machine && m.DiesIfFelled && !m.Eats && !PartyRules.IsPet(m)));
        Assert.Equal("clockwork_mite", Bestiary.Get("clockwork_mite").Id);
    }

    [Fact]
    public void A_fall_costs_by_the_metre_and_a_maker_stays_in_the_machine_longer_with_levels()
    {
        Assert.Equal(0, Flight.FallDamage(Flight.SafeFall));
        Assert.Equal(Flight.FallPerMetre * 10, Flight.FallDamage(Flight.SafeFall + 10));
        Assert.True(Flight.SuitSeconds(20) > Flight.SuitSeconds(6));
        Assert.Equal(0, Flight.SuitSeconds(1));
    }

    [Fact]
    public void A_house_can_grow_for_ever_and_every_wing_is_more_work()
    {
        var (first, fiftieth) = (Housing.Room(0), Housing.Room(49));
        Assert.Equal("Add a wing", first.Name);
        Assert.Equal("Add wing 50", fiftieth.Name);
        Assert.True(fiftieth.Needs.Sum(n => n.N) > first.Needs.Sum(n => n.N));
        Assert.Equal(10, fiftieth.Level);
        Assert.All(first.Needs.Concat(fiftieth.Needs), n => Assert.NotNull(Items.Get(n.Id)));
    }

    [Fact]
    public void An_apprentice_can_die_and_a_pet_cannot()
    {
        Assert.True(Youth().DiesIfFelled);
        Assert.True(new Bond { Role = BondRole.Apprentice }.DiesIfFelled);
        Assert.False(Wolf().DiesIfFelled);
        Assert.False(new Bond { Role = BondRole.Golem }.DiesIfFelled);
    }

    [Fact]
    public void An_apprentice_sent_home_leaves_a_mount_with_no_rider()
    {
        var (first, second, youth) = (Horse(), Horse(), Youth());
        var party = new List<Bond> { first, second, youth };
        Assert.Empty(PartyRules.SpareMounts(party));
        youth.Following = false;
        Assert.Equal([second], PartyRules.SpareMounts(party));
        Assert.Equal([first], PartyRules.SpareMounts(party, ridden: second)); // never the one under the hero
    }

    [Fact]
    public void Every_class_has_a_limit_break_of_its_own()
    {
        var ids = Classes.All.Select(c => c.Limit.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var names = Classes.All.Select(c => c.Limit.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

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
        foreach (var s in Shops.All.Concat(Shops.Samples)) Assert.All(s.Sells, id => Assert.True(id == "meat" || Items.Get(id) is not null, $"{s.Name} sells {id}"));
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
