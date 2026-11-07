using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class SettlingTests
{
    [Fact]
    public void Two_classes_learned_give_the_moves_of_both_and_one_limit_break_of_either()
    {
        var hero = new Hero { Discipline = "EtherAeronaut", Level = 12 };
        hero.Join("Shapeshifter"); // an artificer cross-classed into a druid
        var moves = QuickBar.Moves(hero).Select(a => a.Id).ToList();
        Assert.Contains("shift", moves);
        Assert.Contains("harpoon", moves); // the old class's moves are still there
        Assert.Equal("half_form", Classes.Get(hero.UltimateOf).Limit.Id);
        Assert.Equal("EtherAeronaut", hero.Artificer); // and the golem walks with them still
        hero.Ultimate = "EtherAeronaut";
        Assert.Equal("overdrive", Classes.Get(hero.UltimateOf).Limit.Id);
        Assert.Single(QuickBar.Moves(hero), a => a.Id is "overdrive" or "half_form"); // one limit break at a time
        hero.Ultimate = "Wizard"; // not one they've learned
        Assert.Equal("Shapeshifter", hero.UltimateOf);
        var druid = new Hero { Discipline = "Shapeshifter", Level = 12 };
        druid.Join("Saboteur"); // a druid who becomes an artificer
        druid.Ultimate = "Shapeshifter";
        Assert.Contains(QuickBar.Moves(druid), a => a.Id == "half_form");
        Assert.Contains(QuickBar.Moves(druid), a => a.Id == "shift");
        Assert.Equal("Saboteur", druid.Artificer);
    }

    [Fact]
    public void The_company_wears_what_the_hero_gives_them_and_its_stronger_for_it()
    {
        var pack = new Pack();
        foreach (var id in new[] { "steel_sword", "steel_plate", "iron_mail", "heavy_crossbow" }) pack.Add(id);
        var squire = new Bond { Role = BondRole.Squire, KindId = "squire" };
        var wolf = new Bond { Role = BondRole.Pet, KindId = "wolf" };
        Assert.True(Kit.Give(squire, pack, "steel_sword"));
        Assert.True(Kit.Give(squire, pack, "steel_plate"));
        Assert.Equal(0, pack.Count("steel_sword"));
        var (damage, armor, _) = Kit.Adds(squire);
        Assert.True(damage > 0.15 && armor >= 40, $"{damage} {armor}");
        Assert.False(Kit.Give(wolf, pack, "heavy_crossbow")); // a beast carries no weapon
        Assert.True(Kit.Give(wolf, pack, "iron_mail")); // but wears armour
        Assert.True(Kit.Give(squire, pack, "heavy_crossbow")); // a crossbow for the sword: the sword comes back
        Assert.Equal(1, pack.Count("steel_sword"));
        Kit.TakeBack(squire, pack, Slot.Body);
        Assert.Equal(1, pack.Count("steel_plate"));
        var kept = System.Text.Json.JsonSerializer.Deserialize<Bond>(System.Text.Json.JsonSerializer.Serialize(squire))!;
        Assert.Equal("heavy_crossbow", kept.Gear[Slot.Weapon]);
    }

    [Fact]
    public void A_colony_hires_guards_only_once_it_has_a_barracks_and_they_are_paid_from_the_till()
    {
        var city = CityEconomy.Start(5);
        var hall = new Hall { Owner = "Ana", Hero = "Ana", Kind = HallKind.Colony, Name = "Ana's Colony", X = 900, Z = 900 };
        Assert.True(city.Apply(CityAction.HallFound(hall)));
        var colony = city.HallOf("Ana", HallKind.Colony)!;
        Assert.False(city.Apply(CityAction.HallGuard(colony.Key))); // no barracks yet: the settlers defend it
        Assert.True(ColonyDefence.SettlersFight(barracks: false, guardsStanding: 0));
        for (var s = Settling.Market; s <= Settling.Barracks; s++) Assert.True(city.Apply(CityAction.Grew(colony.Town, s)));
        colony.Members = 4;
        for (var i = 0; i < ColonyDefence.MostGuards(colony); i++) Assert.True(city.Apply(CityAction.HallGuard(colony.Key)));
        Assert.False(city.Apply(CityAction.HallGuard(colony.Key))); // more people, more guards; not past that
        Assert.Equal(3, colony.Guards);
        Assert.False(ColonyDefence.SettlersFight(barracks: true, guardsStanding: 2));
        Assert.True(ColonyDefence.SettlersFight(barracks: true, guardsStanding: 0)); // every guard down: they take up arms again
        colony.Till = 50;
        city.TendHalls(Halls.JobSeconds);
        Assert.Equal(50 + 3 * 4 - ColonyDefence.Wage * 3, colony.Till);
        Assert.True(ColonyDefence.Blow(close: true, deviant: false) > ColonyDefence.Blow(close: true, deviant: true));
    }

    [Fact]
    public void A_world_saved_before_the_barracks_keeps_its_walls()
    {
        var old = """{"Grown":{"camp:rimecrag":4,"camp:holt":3,"colony:Ana":2}}""";
        var city = System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(old)!;
        Assert.Equal(Settling.Walls, city.GrownOf("camp:rimecrag")); // had its palisade: keeps it, and has its barracks
        Assert.Equal(Settling.Chamber, city.GrownOf("camp:holt"));
        Assert.Equal(Settling.Market, city.GrownOf("colony:Ana"));
        Assert.Equal(Settling.Barracks, Settling.Next(Settling.Chamber)!.Stage);
        var saved = System.Text.Json.JsonSerializer.Serialize(city);
        Assert.DoesNotContain("\"Grown\"", saved);
        Assert.Equal(Settling.Walls, System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(saved)!.GrownOf("camp:rimecrag"));
    }

    [Fact]
    public void A_refuge_grows_a_step_at_a_time_and_a_colony_starts_with_its_tavern()
    {
        var city = CityEconomy.Start(5);
        const string refuge = "camp:rimecrag", colony = "colony:Ana";
        Assert.Equal((0, Settling.Tavern), (city.GrownOf(refuge), city.GrownOf(colony)));
        Assert.False(city.Apply(CityAction.Grew(refuge, Settling.Market))); // not before the tavern
        Assert.True(city.Apply(CityAction.Grew(refuge, Settling.Tavern)));
        Assert.False(city.Apply(CityAction.Grew(refuge, Settling.Tavern))); // not twice
        Assert.True(city.Apply(CityAction.Grew(colony, Settling.Market))); // a colony goes straight on to its market
        for (var s = Settling.Market; s <= Settling.Walls; s++) Assert.True(city.Apply(CityAction.Grew(refuge, s)));
        Assert.Equal(Settling.Walls, city.GrownOf(refuge));
        Assert.Null(Settling.Next(city.GrownOf(refuge)));
        Assert.False(city.Apply(CityAction.Grew(refuge, Settling.Walls + 1)));
        Assert.All(Settling.Steps, s => Assert.All(s.Needs, n => Assert.NotNull(Items.Get(n.Id))));
        var copy = System.Text.Json.JsonSerializer.Deserialize<CityEconomy>(System.Text.Json.JsonSerializer.Serialize(city))!;
        Assert.Equal(Settling.Walls, copy.GrownOf(refuge));
    }
}
