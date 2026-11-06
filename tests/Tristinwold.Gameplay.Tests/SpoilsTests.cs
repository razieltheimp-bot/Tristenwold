using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class SpoilsTests
{
    [Fact]
    public void The_winner_copies_what_the_loser_carries_but_gets_gear_as_crowns_and_the_loser_keeps_everything()
    {
        var pack = new Pack();
        pack.Add("healing_draught", 3);
        pack.Add("iron_ore", 10);
        pack.Add("iron_sword");
        pack.Add("drowned_mantle");
        pack.Equip("drowned_mantle");

        var haul = Spoils.Of(pack, 120);
        Assert.Equal(120, haul.Crowns);
        Assert.Equal(Items.Get("iron_sword")!.Value + Items.Get("drowned_mantle")!.Value, haul.GearCrowns);
        Assert.Equal([("healing_draught", 3), ("iron_ore", 10)], haul.Items.OrderBy(i => i.Id).ToList());
        Assert.DoesNotContain(haul.Items, i => Items.Get(i.Id)!.Kind == ItemKind.Gear);

        // Nothing left the loser.
        Assert.Equal(3, pack.Count("healing_draught"));
        Assert.Equal(1, pack.Count("iron_sword"));
        Assert.Equal("drowned_mantle", pack.WornIn(Slot.Body)!.Id);

        var told = Spoils.Tell(haul.Items, haul.Crowns, haul.GearCrowns, 40);
        Assert.Contains("3 Healing Draught", told);
        Assert.Contains("40 crowns on their head", told);
    }
}
