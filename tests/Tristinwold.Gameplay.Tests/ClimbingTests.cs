namespace Tristinwold.Gameplay.Tests;

/// <summary>Who can climb: rogues, martial artists, the Apekin and Scalkin, druids in a climbing shape, an aeronaut in their golem, a saboteur with mites, and anyone with gear.</summary>
public class ClimbingTests
{
    static string? Why(string cls, string race = "human", string? shape = null, bool golem = false, int mites = 0, bool gear = false) =>
        Climbing.Why([cls], race, shape, golem, mites, gear);

    [Fact]
    public void Rogues_and_martial_artists_climb_by_their_training()
    {
        Assert.All(Classes.All.Where(c => c.Calling == Calling.Rogue), c => Assert.NotNull(Why(c.Id)));
        Assert.NotNull(Why("MartialArtist"));
        Assert.Null(Why("Paladin"));
        Assert.Null(Why("Wizard"));
    }

    [Fact]
    public void The_Apekin_and_Scalkin_climb_by_nature()
    {
        Assert.NotNull(Why("Paladin", "ape"));
        Assert.NotNull(Why("Paladin", "lizard"));
        Assert.Null(Why("Paladin", "elf"));
    }

    [Fact]
    public void A_druid_climbs_only_in_a_shape_that_can()
    {
        Assert.Null(Why("Druid"));
        Assert.NotNull(Why("Druid", shape: "bear"));
        Assert.NotNull(Why("Druid", shape: "deathcrest_ape"));
        Assert.Null(Why("Druid", shape: "wolf"));
        Assert.Null(Why("Druid", "ape", shape: "boar")); // an Apekin turned boar has hooves, not hands
    }

    [Fact]
    public void Artificers_climb_with_their_machines()
    {
        Assert.Null(Why("EtherAeronaut"));
        Assert.NotNull(Why("EtherAeronaut", golem: true));
        Assert.Null(Why("Saboteur", mites: Climbing.MitesToClimb - 1));
        Assert.NotNull(Why("Saboteur", mites: Climbing.MitesToClimb));
    }

    [Fact]
    public void Anyone_climbs_with_gear_from_the_shop()
    {
        Assert.NotNull(Why("Paladin", gear: true));
        Assert.Contains(Climbing.Gear, Shops.Provisions.Sells);
        Assert.NotNull(Items.Get(Climbing.Gear));
    }
}
