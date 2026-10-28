using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>The town's own life: the guilds' initiates about their errands, thieves, cutthroats and muggers.</summary>
public class TownLifeTests
{
    [Fact]
    public void Every_guild_sends_initiates_out_on_their_first_errand()
    {
        var rng = new Rng(4);
        foreach (var calling in Enum.GetValues<Calling>())
        {
            var i = Initiates.Make(calling, rng);
            Assert.Contains(i.Look, Classes.Of(calling).Select(d => d.Look));
            Assert.Contains(Guilds.Of(calling).Name, i.Title);
            Assert.False(string.IsNullOrWhiteSpace(i.Errand));
        }
        Assert.Contains("purses", Initiates.ErrandOf(Calling.Rogue));
    }

    [Fact]
    public void A_young_thief_has_a_harder_time_with_a_hero_who_knows_the_trade()
    {
        Assert.True(Initiates.LiftChance(1, rogue: false) > Initiates.LiftChance(8, rogue: false));
        Assert.True(Initiates.LiftChance(1, rogue: true) < Initiates.LiftChance(1, rogue: false));
        Assert.Equal(0.1, Initiates.LiftChance(10, rogue: true));
        Assert.Equal(3, Initiates.Purse(3, new Rng(1))); // never more than you have
        Assert.InRange(Initiates.Purse(500, new Rng(1)), 5, 20);
    }

    [Fact]
    public void Cutthroats_come_for_heroes_with_a_name_and_muggers_work_the_rough_streets()
    {
        Assert.True(Crime.AssassinChance(10) > Crime.AssassinChance(1));
        Assert.True(Crime.AssassinChance(99) <= 0.35);
        Assert.True(Crime.MuggingChance("The Docks") > Crime.MuggingChance("The Exchange"));
        Assert.True(Initiates.Reward("velvet_assassin") > Initiates.Reward("young_thief"));
    }
}
