using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class SettlingTests
{
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
