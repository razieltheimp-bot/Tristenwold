using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

/// <summary>Wren's Menagerie: tame young beasts for sale, priced by danger and training, and never a dragon.</summary>
public class MenagerieTests
{
    [Fact]
    public void Every_beast_for_sale_can_be_tamed_and_none_is_a_dragon()
    {
        Assert.All(Menagerie.Stock, o => Assert.True(Taming.Tameable(o.KindId), o.KindId));
        Assert.DoesNotContain(Menagerie.Stock, o => o.KindId.StartsWith("dragon"));
        Assert.Equal(Menagerie.Stock.Count, Menagerie.Stock.Select(o => o.KindId).Distinct().Count());
        Assert.Equal("Hawk Chick", Menagerie.Stock.Single(o => o.KindId == "hawk").Name);
        Assert.Equal("Hawk", Bestiary.Get("hawk").Name);
    }

    [Fact]
    public void Danger_and_training_raise_the_price()
    {
        PetOffer Get(string id) => Menagerie.Stock.Single(o => o.KindId == id);
        Assert.True(Get("fox").BasePrice < Get("wolf").BasePrice);
        Assert.True(Get("wolf").BasePrice < Get("bear").BasePrice);
        Assert.True(Get("bear").BasePrice < Get("frost_troll").BasePrice);
        Assert.True(Get("wolf").BasePrice < Get("goblin").BasePrice); // as dangerous, harder to train
        Assert.Equal(Menagerie.Stock.Max(o => o.BasePrice), Get("frost_troll").BasePrice);
        Assert.All(Menagerie.Stock, o => Assert.Equal(0, o.BasePrice % 5));
    }

    [Fact]
    public void A_practised_tamer_pays_less_tames_better_and_trains_faster()
    {
        var bear = Menagerie.Stock.Single(o => o.KindId == "bear");
        Assert.Equal(bear.BasePrice, Menagerie.Price(bear, 1));
        Assert.True(Menagerie.Price(bear, 10) < Menagerie.Price(bear, 5));
        Assert.True(Menagerie.Price(bear, 10) >= bear.BasePrice * 0.72);
        Assert.True(Taming.Chance(false, false, false, 5, tamingLevel: 6) > Taming.Chance(false, false, false, 5));
        Assert.Equal(0, Taming.Chance(true, true, false, 5, tamingLevel: 10));
        Assert.True(Taming.Training(10) > Taming.Training(1));

        var pack = new Pack();
        Assert.Equal(1, pack.Level(Job.Taming));
        Assert.Equal(2, pack.Practise(Job.Taming, 40));
    }
}
