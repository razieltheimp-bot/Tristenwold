using Xunit;

namespace Tristinwold.Gameplay.Tests;

/// <summary>What a hero can wear and how they cut their hair, and that a saved look keeps all of it.</summary>
public class AvatarOptionsTests
{
    [Fact]
    public void A_mix_of_clothes_and_a_cut_comes_back_from_a_save()
    {
        var look = Avatar.For("Wizard") with { Style = "Topknot", Top = "Teal", Bottom = "Sand", Sleeves = "Short", Wear = "Wrapped robe", Hair = "Moss" };
        var back = Avatar.Parse(look.Encode(), "Wizard")!;
        Assert.Equal(("Topknot", "Teal", "Sand", "Short", "Wrapped robe", "Moss"), (back.Style, back.Top, back.Bottom, back.Sleeves, back.Wear, back.Hair));
    }

    [Fact]
    public void An_old_save_keeps_the_class_clothes()
    {
        var old = Avatar.Parse("head=Mage;style=Long01;sex=Female", "Wizard")!;
        Assert.Equal(("Class", "Class", "Class", "Class"), (old.Top, old.Bottom, old.Sleeves, old.Wear));
        Assert.Equal("Long01", old.Style);
    }

    [Fact]
    public void Cuts_are_for_everyone_and_class_cloth_has_no_colour_of_its_own()
    {
        Assert.All(Avatar.Cuts, c => { Assert.Contains(c.Id, Avatar.Styles("Male")); Assert.Contains(c.Id, Avatar.Styles("Female")); });
        Assert.DoesNotContain("Topknot", Avatar.MaleStyles.Select(s => s.Id));
        Assert.Equal("Top-knot", Avatar.StyleName("Topknot"));
        Assert.Null(Avatar.ClothHex("Class"));
        Assert.All(Avatar.Cloths.Skip(1), c => Assert.StartsWith("#", Avatar.ClothHex(c.Name)));
    }

    [Fact]
    public void A_random_look_is_always_one_that_can_be_read_back()
    {
        var rng = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var look = Avatar.Random(rng);
            var back = Avatar.Parse(look.Encode())!;
            Assert.Equal(look.Top, back.Top);
            Assert.Equal(look.Wear, back.Wear);
            Assert.Equal(look.Style, back.Style);
        }
    }
}
