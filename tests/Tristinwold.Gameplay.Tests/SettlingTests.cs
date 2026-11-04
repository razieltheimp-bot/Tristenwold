using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class SettlingTests
{
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
