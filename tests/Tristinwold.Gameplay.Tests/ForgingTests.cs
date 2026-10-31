using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class ForgingTests
{
    static string Serial(ulong seed, string baseId = "steel_sword", string forge = "city:tristinwold", string name = "The Anvil Gate", int smith = 5, int hero = 10, string maker = "Ana") =>
        Forging.Serial(baseId, seed, forge, name, smith, hero, maker);

    static Item Piece(ulong seed, string baseId = "steel_sword", string forge = "city:tristinwold") => Items.Get(Serial(seed, baseId, forge))!;

    [Fact]
    public void A_serial_is_the_same_piece_on_every_machine()
    {
        var (a, b) = (Piece(77), Items.Get(Serial(77))!);
        Assert.Equal((a.Name, a.Damage, a.Armor, a.Speed, a.Crit, a.Health, a.Text), (b.Name, b.Damage, b.Armor, b.Speed, b.Crit, b.Health, b.Text));
    }

    [Fact]
    public void Ten_thousand_swords_from_one_forge_are_ten_thousand_different_swords()
    {
        var rng = new Rng(9);
        var pieces = Enumerable.Range(0, 10000).Select(_ => Piece(rng.NextULong())).ToList();
        Assert.Equal(10000, pieces.Select(p => (p.Damage, p.Armor, p.Speed, p.Crit, p.Health, p.Fire, string.Join(",", p.Parts!))).Distinct().Count());
    }

    [Fact]
    public void Armour_comes_out_different_too()
    {
        var rng = new Rng(4);
        var pieces = Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong(), "steel_plate")).ToList();
        Assert.Equal(5000, pieces.Select(p => (p.Armor, p.Speed, p.Crit, p.Health, p.Fire, string.Join(",", p.Parts!))).Distinct().Count());
        Assert.All(pieces, p => Assert.Equal(Slot.Body, p.Slot));
    }

    [Fact]
    public void The_same_seed_at_a_different_forge_comes_out_different()
    {
        var (home, deep) = (Piece(123), Piece(123, forge: "city:cinderhelm"));
        Assert.NotEqual(home.Damage, deep.Damage);
        Assert.EndsWith("+2", home.Name);
        Assert.EndsWith("+3", deep.Name);
    }

    [Fact]
    public void Every_named_forge_leans_its_own_way_and_every_other_place_gets_a_forge_of_its_own()
    {
        Assert.Equal(Forging.Forges.Count, Forging.Forges.Select(f => (f.Damage, f.Armor, f.Speed, f.Crit, f.Health, f.Fire)).Distinct().Count());
        var (a, b) = (Forging.ForgeAt("city:mirewatch", "Mirewatch"), Forging.ForgeAt("site:hamlet-4", "Oakley"));
        Assert.Equal("The Mirewatch Forge", a.Name);
        Assert.Equal(a, Forging.ForgeAt("city:mirewatch", "Mirewatch"));
        Assert.NotEqual((a.Damage, a.Armor, a.Speed, a.Crit, a.Health), (b.Damage, b.Armor, b.Speed, b.Crit, b.Health));
    }

    [Fact]
    public void Nothing_comes_out_past_its_limits()
    {
        var rng = new Rng(11);
        var plain = Items.Get("steel_sword")!;
        foreach (var p in Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong(), forge: "city:cinderhelm")))
        {
            Assert.InRange(p.Damage, plain.Damage * Forging.Lowest - 0.001, plain.Damage * Forging.Highest + 0.001);
            Assert.InRange(p.Speed, -0.08, 0.08);
            Assert.InRange(p.Crit, 0, 8);
            Assert.InRange(p.Health, 0, 0.08);
        }
    }

    [Fact]
    public void A_better_smith_and_a_better_forge_make_better_work_over_many_tries()
    {
        double Mean(int smith, string forge, string name)
        {
            var rng = new Rng(21);
            return Enumerable.Range(0, 3000).Average(_ => Items.Get(Forging.Serial("steel_sword", rng.NextULong(), forge, name, smith, 10, "Ana"))!.Damage);
        }
        Assert.True(Mean(10, "city:tristinwold", "The Anvil Gate") > Mean(1, "city:tristinwold", "The Anvil Gate"));
        Assert.True(Mean(5, "city:cinderhelm", "The Ashen Crucible") > Mean(5, "city:druitt", "The Sawpit Forge"));
    }

    [Fact]
    public void All_the_grades_turn_up_and_the_best_are_rare()
    {
        var rng = new Rng(31);
        var grades = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong()).Grade!.Value).GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(4, grades.Count);
        Assert.True(grades[ForgeGrade.Legendary] < grades[ForgeGrade.Fine]);
    }

    [Fact]
    public void Parts_alone_make_tens_of_thousands_of_one_piece()
    {
        Assert.Equal(32768, Forging.PartCombinations(true));
        Assert.Equal(32768, Forging.PartCombinations(false));
    }

    [Fact]
    public void A_forged_piece_goes_in_the_pack_is_worn_and_counts()
    {
        var pack = new Pack();
        var id = Serial(5);
        pack.Add(id);
        Assert.True(pack.Equip(id));
        var piece = Items.Get(id)!;
        Assert.Equal(piece.Damage, pack.Damage);
        Assert.Equal(piece.Speed, pack.Speed);
        Assert.Equal(piece.Crit, pack.Crit);
        Assert.Equal(piece.Health, pack.Health);
        pack.Unequip(Slot.Weapon);
        Assert.Equal(1, pack.Count(id));
    }

    [Fact]
    public void A_shop_prices_it_on_what_it_is_and_it_doesnt_flood_the_market()
    {
        var city = CityEconomy.Start(1);
        var piece = Piece(6);
        Assert.Equal(piece.Value, city.Price(piece));
        var plain = Items.Get("steel_sword")!;
        var before = city.Price(plain);
        city.Sold(piece.Id, city.Offer(piece));
        Assert.Equal(before, city.Price(plain));
    }

    [Fact]
    public void Odd_characters_in_a_name_cant_break_a_serial()
    {
        var id = Forging.Serial("steel_sword", 8, "city:tristinwold", "The Anvil Gate", 3, 4, "A~nna ~~ Bell");
        Assert.NotNull(Items.Get(id));
        Assert.Contains("Forged by Anna  Bell", Items.Get(id)!.Text);
    }

    [Theory]
    [InlineData("forged1~nonsense")]
    [InlineData("forged1~steel_ingot~0000000000000001~city:tristinwold~The Anvil Gate~1~1~Ana")]
    [InlineData("forged1~steel_sword~nothex~city:tristinwold~The Anvil Gate~1~1~Ana")]
    [InlineData("forged1~no_such_thing~0000000000000001~city:tristinwold~The Anvil Gate~1~1~Ana")]
    public void A_bad_serial_is_nothing_at_all(string id) => Assert.Null(Items.Get(id));

    [Fact]
    public void Only_worn_things_from_a_forge_or_bench_come_out_unique()
    {
        Assert.True(Forging.Unique(Recipes.All.First(r => r.Makes == "steel_sword")));
        Assert.False(Forging.Unique(Recipes.All.First(r => r.Makes == "steel_ingot")));
        Assert.False(Forging.Unique(Recipes.All.First(r => r.Job == Job.Cooking)));
    }

    [Fact]
    public void Weapons_roll_a_plus_to_hit_and_plus_damage_and_armour_doesnt()
    {
        var rng = new Rng(41);
        var swords = Enumerable.Range(0, 3000).Select(_ => Piece(rng.NextULong())).ToList();
        Assert.True(swords.Select(p => p.ToHit).Distinct().Count() >= 4 && swords.Select(p => p.Power).Distinct().Count() >= 4);
        Assert.All(swords, p => { Assert.InRange(p.ToHit, 0, Forging.MostToHit); Assert.True(p.Power >= 1); });
        Assert.Contains("to hit", swords.First(p => p.ToHit > 0).Text);
        var plate = Piece(5, "steel_plate");
        Assert.Equal((0, 0), (plate.ToHit, plate.Power));
    }

    [Fact]
    public void A_plus_to_hit_takes_points_off_the_chance_of_a_glancing_blow()
    {
        Assert.Equal(0.1, Forging.Glances(0), 6);
        Assert.Equal(0.02, Forging.Glances(8), 6);
        Assert.Equal(0, Forging.Glances(20));
    }

    [Fact]
    public void Only_the_weapon_in_hand_gives_its_plus_to_hit_and_damage()
    {
        var (pack, id) = (new Pack(), Serial(1234));
        pack.Add(id);
        Assert.Equal((0, 0), (pack.ToHit, pack.Power));
        pack.Equip(id);
        Assert.Equal((Items.Get(id)!.ToHit, Items.Get(id)!.Power), (pack.ToHit, pack.Power));
    }

    [Fact]
    public void A_weapons_fire_rolls_a_die_and_the_big_dice_are_rare()
    {
        var rng = new Rng(51);
        var burning = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong())).Where(p => p.Fire is not null).ToList();
        Assert.All(burning, p => Assert.Contains(p.FireDie, new[] { 6, 8, 10, 12 }));
        Assert.All(burning, p => Assert.Contains($"plus d{p.FireDie} ", p.Text));
        var dice = burning.GroupBy(p => p.FireDie).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(dice[6] > dice[8] && dice[8] > dice[10] && dice[10] > dice[12] && dice[12] > 0);
    }

    [Fact]
    public void A_better_forge_shifts_the_dice_up()
    {
        Assert.Equal((6, 8, 10, 12), (Forging.FireDie(0.1, 0), Forging.FireDie(0.5, 0), Forging.FireDie(0.8, 0), Forging.FireDie(0.95, 0)));
        Assert.Equal(8, Forging.FireDie(0.4, 2));
        Assert.Equal(12, Forging.FireDie(0.85, 3));
    }

    [Fact]
    public void No_fire_no_die_and_armour_resists_instead()
    {
        var rng = new Rng(61);
        Assert.All(Enumerable.Range(0, 2000).Select(_ => Piece(rng.NextULong())).Where(p => p.Fire is null), p => Assert.Equal(0, p.FireDie));
        Assert.All(Enumerable.Range(0, 2000).Select(_ => Piece(rng.NextULong(), "steel_plate")), p => Assert.Equal(0, p.FireDie));
    }

    [Fact]
    public void Its_base_is_remembered()
    {
        Assert.Equal("steel_sword", Forging.BaseOf(Serial(9)));
        Assert.Equal("steel_sword", Forging.BaseOf("steel_sword"));
    }
}
