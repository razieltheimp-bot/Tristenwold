using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class SettlingTests
{
    [Fact]
    public void A_refuge_with_a_tavern_can_be_home_and_with_a_chamber_has_a_council()
    {
        var world = WorldGen.Default;
        var refuge = world.SettlementsNear(0, 0, 9000).First(s => s.Kind == SettlementKind.AshbornCamp && !s.Hostile);
        var grown = 0;
        TownCouncils.Grown = id => id == refuge.Id ? grown : 0;
        try
        {
            Assert.False(ImmortalSaga.Homely(refuge));
            Assert.False(TownCouncils.Keeps(refuge));
            grown = Settling.Tavern;
            Assert.True(ImmortalSaga.Homely(refuge));
            Assert.False(TownCouncils.Keeps(refuge));
            grown = Settling.Chamber;
            Assert.True(TownCouncils.Keeps(refuge));
        }
        finally { TownCouncils.Grown = null; }
    }
}
