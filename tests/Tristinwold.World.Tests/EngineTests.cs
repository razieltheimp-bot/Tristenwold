using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Xunit;

namespace Tristinwold.World.Tests;

/// <summary>An artificer's steam: who can fit it, the orders left for a friend, and a ship in the air that no pirate boards.</summary>
public class EngineTests
{
    [Fact]
    public void A_ship_owner_sails_without_signing_on()
    {
        var sea = new SeaBook { OwnShip = true, ShipName = "Wren" };
        Assert.NotNull(sea.Sail(sea.Ports.First(), new Rng(1)));
    }

    [Fact]
    public void A_flying_ship_meets_no_pirates_and_one_out_of_fuel_does()
    {
        var port = new SeaBook().Ports.MaxBy(p => p.Pirates)!;
        var attacked = 0;
        for (var seed = 1UL; seed < 40; seed++)
        {
            var flying = new SeaBook { OwnShip = true, ShipName = "Wren", Steam = true, Flies = true, Fuel = 600 };
            var v = flying.Sail(port, new Rng(seed))!;
            Assert.Empty(v.Out);
            Assert.Empty(v.Home);
            var cold = new SeaBook { OwnShip = true, ShipName = "Wren", Steam = true, Flies = true, Fuel = 0 };
            var w = cold.Sail(port, new Rng(seed))!;
            attacked += w.Out.Count + w.Home.Count;
        }
        Assert.True(attacked > 0, "with no fire in her she's on the water, where the Teeth are");
    }

    [Fact]
    public void Only_artificers_fit_and_an_order_waits_for_one()
    {
        Assert.True(Engines.IsArtificer("Saboteur") && Engines.IsArtificer("Alchemist") && !Engines.IsArtificer("Barbarian"));
        var city = CityEconomy.Start(1);
        Assert.True(city.Apply(CityAction.OrderPost("Joe", Engines.ShipSteam, 100)));
        Assert.False(city.Apply(CityAction.OrderPost("Joe", Engines.ShipSteam, 100)));
        var key = $"Joe#{Engines.ShipSteam}";
        Assert.False(city.Apply(CityAction.OrderFit(key, "Joe", 9))); // nobody does their own order
        Assert.True(city.Apply(CityAction.OrderFit(key, "Mira", 9)));
        Assert.False(city.Apply(CityAction.OrderFit(key, "Bram", 9)));
        Assert.Equal(("Mira", 9), (city.Orders[key].FittedBy, city.Orders[key].Level));
        Assert.True(city.Apply(CityAction.OrderDone(key)));
        Assert.Empty(city.Orders);
        Assert.False(city.Apply(CityAction.OrderPost("Joe", "a_cannon", 0)));
    }

    [Fact]
    public void A_ships_lift_costs_far_more_than_a_skyboat_and_more_the_bigger_she_is()
    {
        var lift = Engines.Works[Engines.ShipFlight].Needs.ToDictionary(n => n.Id, n => n.N);
        var sky = Engines.Works[Engines.Skyboat].Needs.ToDictionary(n => n.Id, n => n.N);
        Assert.All(sky, n => Assert.True(lift[n.Key] >= n.Value * 4, n.Key));
        var (sloop, galleon) = (new SeaBook { OwnShip = true, Size = ShipSize.Small }, new SeaBook { OwnShip = true, Size = ShipSize.Large });
        Assert.True(sloop.LiftYardPrice >= Engines.LifeboatPrice * 5);
        Assert.True(galleon.LiftYardPrice > sloop.LiftYardPrice * 2 && galleon.LiftIslePrice > sloop.LiftIslePrice * 2);
    }

    [Fact]
    public void Every_work_has_makings_and_only_ships_cost_crowns_at_brasswake()
    {
        foreach (var (id, w) in Engines.Works)
        {
            Assert.NotEmpty(w.Needs);
            Assert.All(w.Needs, n => Assert.NotNull(Items.Get(n.Id)));
            Assert.Equal(id is Engines.ShipSteam or Engines.ShipFlight, w.Crowns > 0);
        }
    }
}
