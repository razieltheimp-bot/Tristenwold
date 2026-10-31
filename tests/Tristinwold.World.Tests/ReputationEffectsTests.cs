using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class ReputationEffectsTests
{
    [Fact]
    public void A_stranger_pays_the_usual()
    {
        Assert.Equal((1.0, 1.0), (ReputationBook.BuyFactor(0), ReputationBook.SellFactor(0)));
        Assert.Equal(100, ReputationBook.Reward(100, 0));
        Assert.Null(ReputationBook.Manner(0, "Ana"));
    }

    [Fact]
    public void Prices_swing_fifteen_percent_either_way()
    {
        Assert.Equal((0.85, 1.15), (ReputationBook.BuyFactor(5), ReputationBook.SellFactor(5)));
        Assert.Equal((1.15, 0.85), (ReputationBook.BuyFactor(-5), ReputationBook.SellFactor(-5)));
    }

    [Fact]
    public void Someone_weighs_their_own_view_and_their_towns_equally()
    {
        var r = new ReputationBook();
        r.Towns["city:mirewatch"] = 4;
        r.People["a"] = -2;
        Assert.Equal(1, r.Regard("city:mirewatch", "a"), 6);
        Assert.Equal(-2, r.Regard(null, "a"), 6);
    }

    [Fact]
    public void They_stop_offering_work_before_they_stop_serving_you()
    {
        Assert.Equal((false, false), (ReputationBook.RefusesWork(-2.9), ReputationBook.ShutsDoor(-2.9)));
        Assert.Equal((true, false), (ReputationBook.RefusesWork(-3), ReputationBook.ShutsDoor(-3)));
        Assert.Equal((true, true), (ReputationBook.RefusesWork(-4), ReputationBook.ShutsDoor(-4)));
    }

    [Fact]
    public void Friends_pay_a_little_extra()
    {
        Assert.Equal(100, ReputationBook.Reward(100, 2.9));
        Assert.Equal(110, ReputationBook.Reward(100, 3));
    }
}
