using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

/// <summary>A hero's look, apart from their class.</summary>
public class AvatarTests
{
    [Fact]
    public void Every_class_starts_with_its_own_figure()
    {
        Assert.Equal(new Avatar("Knight", "Knight", "Knight", "Knight", "Knight_Helmet", "Natural", "Natural", "Average"), Avatar.For("Paladin"));
        Assert.Equal("Mage_Hat", Avatar.For("Wizard").Hat);
        Assert.All(Classes.All, c => Assert.Contains(Avatar.For(c.Id).Body, Avatar.Figures));
    }

    [Fact]
    public void A_look_survives_being_saved_and_read_back()
    {
        var mixed = new Avatar("Skeleton_Mage", "Knight", "Barbarian", "Rogue", "Mage_Hat", "Crimson", "Tan", "Tall");
        Assert.Equal(mixed, Avatar.Parse(mixed.Encode()));
        var rng = new Random(4);
        for (var i = 0; i < 50; i++) { var a = Avatar.Random(rng); Assert.Equal(a, Avatar.Parse(a.Encode())); }
    }

    [Fact]
    public void A_bad_or_missing_look_falls_back_to_the_class()
    {
        Assert.Null(Avatar.Parse(null));
        Assert.Null(Avatar.Parse(""));
        var odd = Avatar.Parse("head=Dragon;body=Mage;nonsense;height=Giant", "Barbarian")!;
        Assert.Equal("Barbarian", odd.Head);
        Assert.Equal("Mage", odd.Body);
        Assert.Equal("Average", odd.Height);
        Assert.Equal(0.8f, (Avatar.For("Mage") with { Height = "Tall" }).Scale);
        Assert.Equal("#ffffff", Avatar.For("Mage").OutfitHex);
    }
}
