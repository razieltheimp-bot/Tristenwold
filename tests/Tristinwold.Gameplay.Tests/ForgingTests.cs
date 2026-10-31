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
        Assert.Equal(248832, Forging.PartCombinations(true));
        Assert.Equal(248832, Forging.PartCombinations(false));
        Assert.All(Forging.WeaponSockets.Concat(Forging.ArmorSockets), s => Assert.Equal(12, s.Parts.Length));
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
        Assert.Equal(8, Forging.FireDie(0.45, 2));
        Assert.Equal(12, Forging.FireDie(0.9, 3));
    }

    [Fact]
    public void No_fire_no_die_and_armour_resists_instead()
    {
        var rng = new Rng(61);
        Assert.All(Enumerable.Range(0, 2000).Select(_ => Piece(rng.NextULong())).Where(p => p.Fire is null), p => Assert.Equal(0, p.FireDie));
        Assert.All(Enumerable.Range(0, 2000).Select(_ => Piece(rng.NextULong(), "steel_plate")), p => Assert.Equal(0, p.FireDie));
    }

    [Fact]
    public void Armour_comes_out_with_abilities_and_only_some_do()
    {
        var rng = new Rng(71);
        var pieces = Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong(), "leather_armor")).ToList();
        Assert.Contains(pieces, p => p.Powers!.Count == 0);
        Assert.Contains(pieces, p => p.Powers!.Count == 3);
        Assert.Contains(pieces, p => p.Powers!.ContainsKey("health"));
        Assert.Contains(pieces, p => p.Powers!.ContainsKey("dodge"));
        Assert.True(pieces.Where(p => p.Powers!.Count > 0).All(p => p.Name.Contains(" of ")));
    }

    [Fact]
    public void Light_armour_leaps_and_heavy_armour_stands()
    {
        var rng = new Rng(72);
        var light = Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong(), "leather_armor")).ToList();
        var heavy = Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong(), "steel_plate")).ToList();
        Assert.Contains(light, p => p.Powers!.ContainsKey("jump"));
        Assert.DoesNotContain(heavy, p => p.Powers!.ContainsKey("jump"));
        Assert.Contains(heavy, p => p.Powers!.ContainsKey("block"));
        Assert.DoesNotContain(light, p => p.Powers!.ContainsKey("block"));
        Assert.Contains(heavy, p => p.Powers!.ContainsKey("deflect"));
        Assert.DoesNotContain(light, p => p.Powers!.ContainsKey("deflect"));
    }

    [Fact]
    public void However_much_featherstep_you_wear_a_jump_is_at_most_double()
    {
        var rng = new Rng(73);
        var leapers = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong(), "leather_armor")).Where(p => p.Powers!.ContainsKey("jump")).Take(1).ToList();
        var capper = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong(), "leather_cap")).First(p => p.Powers!.GetValueOrDefault("jump") > 0.6);
        var cloak = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong(), "traveller_cloak")).First(p => p.Powers!.GetValueOrDefault("jump") > 0.6);
        var pack = new Pack();
        foreach (var id in new[] { capper.Id, cloak.Id }) { pack.Add(id); pack.Equip(id); }
        Assert.Equal(2, pack.JumpHeight);
        Assert.NotEmpty(leapers);
    }

    [Fact]
    public void Weapons_get_their_own_abilities_and_dice()
    {
        var rng = new Rng(74);
        var swords = Enumerable.Range(0, 5000).Select(_ => Piece(rng.NextULong())).ToList();
        Assert.Contains(swords, p => p.Powers!.ContainsKey("bleed"));
        Assert.All(swords.Where(p => p.Powers!.ContainsKey("bleed")), p => Assert.Contains((int)p.Powers!["bleed"], new[] { 4, 6, 8, 10 }));
        Assert.All(swords.Where(p => p.Powers!.ContainsKey("drain")), p => Assert.Contains((int)p.Powers!["drain"], new[] { 4, 6, 8 }));
        Assert.DoesNotContain(swords, p => p.Powers!.ContainsKey("dodge"));
    }

    [Fact]
    public void Dice_abilities_reach_their_biggest_die()
    {
        var rng = new Rng(75);
        var swords = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong())).ToList();
        Assert.Equal(new[] { 4, 6, 8, 10 }, swords.Where(p => p.Powers!.ContainsKey("bleed")).Select(p => (int)p.Powers!["bleed"]).Distinct().OrderBy(d => d));
    }

    [Fact]
    public void Armour_can_be_cut_with_runes_of_every_kind()
    {
        var rng = new Rng(76);
        var pieces = Enumerable.Range(0, 20000).Select(_ => Piece(rng.NextULong(), "steel_plate")).ToList();
        Assert.All(Forging.Runes, r => Assert.Contains(pieces, p => p.Powers!.ContainsKey(r.Key)));
        Assert.Contains(pieces, p => p.Text.Contains("rune expulsion"));
        Assert.DoesNotContain(Enumerable.Range(0, 3000).Select(_ => Piece(rng.NextULong())), p => p.Powers!.Keys.Any(k => k.StartsWith("rune_")));
    }

    [Fact]
    public void The_great_blades_stay_with_who_earned_them()
    {
        Assert.Equal("excalibur", new string(Items.Get(Legend.Rubilacxe)!.Name.Reverse().ToArray()).ToLowerInvariant());
        foreach (var id in new[] { Legend.Rubilacxe, Legend.RubilacxeScabbard, Legend.Arachnicalibur, Legend.ArachnicaliburScabbard }) Assert.True(Items.Get(id)!.Bound);
        Assert.Equal((4, 4), (Items.Get(Legend.Rubilacxe)!.ToHit, Items.Get(Legend.Rubilacxe)!.Power));
        Assert.Equal(30, Items.Get(Legend.ArachnicaliburScabbard)!.Powers!["dodge"]);
    }

    [Fact]
    public void There_is_one_of_each_blade_on_a_server()
    {
        var city = CityEconomy.Start(1);
        Assert.True(city.Apply(CityAction.Blade(Legend.Rubilacxe, "Ana")));
        Assert.False(city.Apply(CityAction.Blade(Legend.Rubilacxe, "Bram")));
        Assert.Equal("Ana", city.BladeHolder);
        Assert.True(city.Apply(CityAction.Blade(Legend.Arachnicalibur, "Bram")));
        Assert.Equal("Bram", city.WebHolder);
    }

    [Fact]
    public void Soren_lays_siege_burns_a_town_and_leaves_it_for_the_carpenters()
    {
        var city = CityEconomy.Start(7);
        city.Day = CityEconomy.SiegeEvery - 1;
        city.NextDay();
        var town = Assert.IsType<PirateSiege>(city.Siege).Town;
        Assert.Contains(town, CityEconomy.SiegeTowns);
        Assert.Equal(CityEconomy.SiegeBurns, city.FabricOf(town).Burnt);
        for (var i = 0; i < CityEconomy.SiegeDays; i++) city.NextDay();
        Assert.Null(city.Siege);
        Assert.True(city.FabricOf(town).Burnt > CityEconomy.SackBurns);
        var was = city.FabricOf(town).Burnt;
        city.Stock[Good.Timber] = 100;
        Assert.NotNull(city.Carpenters(new Rng(1)));
        Assert.Equal(was - 1, city.FabricOf(town).Burnt);
    }

    [Fact]
    public void Breaking_a_siege_saves_what_would_have_burned()
    {
        var city = CityEconomy.Start(7);
        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3 };
        Assert.True(city.Apply(CityAction.SiegeBroken("Ana")));
        Assert.Null(city.Siege);
        Assert.False(city.Apply(CityAction.SiegeBroken("Ana")));
    }

    [Fact]
    public void Its_base_is_remembered()
    {
        Assert.Equal("steel_sword", Forging.BaseOf(Serial(9)));
        Assert.Equal("steel_sword", Forging.BaseOf("steel_sword"));
    }
}
