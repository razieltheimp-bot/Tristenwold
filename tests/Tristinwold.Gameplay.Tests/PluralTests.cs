using System.Linq;
using Tristinwold.Gameplay;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

public class PluralTests
{
    [Theory]
    [InlineData("Bilberries", "Bilberries")]
    [InlineData("Raspberries", "Raspberries")]
    [InlineData("Potato", "Potatoes")]
    [InlineData("Emberleaf", "Emberleaves")]
    [InlineData("Snowberry", "Snowberries")]
    [InlineData("Wolf Pelt", "Wolf Pelts")]
    [InlineData("Travel Rations", "Travel Rations")]
    [InlineData("Iron Nails", "Iron Nails")]
    [InlineData("Tinderbox", "Tinderboxes")]
    [InlineData("Flax", "Flax")]
    [InlineData("Grilled Fish", "Grilled Fish")]
    [InlineData("Salmon", "Salmon")]
    [InlineData("Sea Bass", "Sea Bass")]
    [InlineData("Leviathan Tooth", "Leviathan Teeth")]
    [InlineData("Elixir of Might", "Elixirs of Might")]
    [InlineData("Torch", "Torches")]
    [InlineData("Healing Draught", "Healing Draughts")]
    [InlineData("Mantle of the Drowned Root", "Mantles of the Drowned Root")]
    [InlineData("Lady's Mantle", "Lady's Mantle")]
    public void Item_names_take_the_right_plural(string name, string plural) => Assert.Equal(plural, Items.Plural(name));

    [Fact]
    public void The_pack_counts_read_right()
    {
        Assert.Equal("3 Wolf Pelts", Items.Count("wolf_pelt", 3));
        Assert.Equal("Wolf Pelt", Items.Count("wolf_pelt", 1));
        Assert.Equal("2 Travel Rations", Items.Count("travel_rations", 2));
    }

    [Fact]
    public void No_item_in_the_whole_list_gets_a_broken_plural()
    {
        var bad = Items.All.Select(i => (i.Name, Plural: Items.Plural(i.Name)))
            .Where(x => x.Plural.EndsWith("ieses") || x.Plural.EndsWith("ses") && x.Name.EndsWith("s") && !x.Name.EndsWith("ss") && !x.Name.EndsWith("us")
                        || x.Plural.EndsWith("os") || x.Plural.EndsWith("xs") || x.Plural.EndsWith("leafs") || x.Plural.EndsWith("Fishes")
                        || x.Plural.EndsWith("Tooths") || x.Plural.Contains(" ofs") || x.Plural.EndsWith("eys") && x.Plural.EndsWith("ieys"))
            .ToList();
        Assert.True(bad.Count == 0, "Broken plurals: " + string.Join(", ", bad.Select(b => $"{b.Name} -> {b.Plural}")));
    }
}
