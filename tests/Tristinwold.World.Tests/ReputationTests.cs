using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class ReputationTests
{
    const string Town = "city:mirewatch", Partner = "city:druitt";

    [Fact]
    public void Everyone_starts_at_nothing()
    {
        var r = new ReputationBook();
        Assert.Equal((0.0, 0.0, 0.0), (r.Town(Town), r.Person("a"), r.Country));
    }

    [Fact]
    public void A_good_deed_counts_for_the_town_that_saw_it_and_a_share_for_the_country()
    {
        var r = new ReputationBook();
        r.Deed(5, Town);
        Assert.Equal(ReputationBook.Weight(5), r.Own(Town), 6);
        Assert.Equal(ReputationBook.Weight(5) * ReputationBook.CountryShare, r.Country, 6);
        Assert.Equal(0, r.Own("city:greywater"));
    }

    [Fact]
    public void A_bad_deed_counts_against_and_nothing_goes_past_five()
    {
        var r = new ReputationBook();
        for (var i = 0; i < 40; i++) r.Deed(-25, Town);
        Assert.Equal(-5, r.Own(Town));
        Assert.Equal(-5, r.Country);
        for (var i = 0; i < 40; i++) r.Helped("a", "Ana");
        Assert.Equal(5, r.Person("a"));
    }

    [Fact]
    public void A_deed_in_the_wilds_only_reaches_the_country()
    {
        var r = new ReputationBook();
        r.Deed(4, null);
        Assert.True(r.Country > 0);
        Assert.Empty(r.Towns);
    }

    [Fact]
    public void A_town_out_of_touch_goes_on_its_own_view()
    {
        var r = new ReputationBook { Country = -4 };
        r.Towns[Town] = 3;
        Assert.Equal(3, r.Town(Town));
    }

    [Fact]
    public void Trade_brings_the_rumours_and_a_town_that_liked_you_may_stop()
    {
        var r = new ReputationBook { Country = -4 };
        r.Towns[Town] = 3;
        for (var i = 0; i < 10; i++) r.Trip(Town);
        Assert.Equal(-0.5, r.Town(Town), 6); // the average of what it saw and what it's heard
    }

    [Fact]
    public void The_old_partners_hear_a_little_from_the_start_and_the_capital_hears_it_all()
    {
        var r = new ReputationBook { Country = 4 };
        Assert.True(r.Town(Partner) > 0 && r.Town(Partner) < r.Town(ReputationBook.Capital));
        Assert.Equal(2, r.Town(ReputationBook.Capital), 6);
    }

    [Fact]
    public void A_port_and_its_town_are_one_place()
    {
        var r = new ReputationBook();
        r.Deed(5, "port:druitt");
        Assert.Equal(r.Own("port:druitt"), r.Own(Partner));
    }

    [Fact]
    public void Each_person_has_their_own_view_and_trade_only_buys_so_much()
    {
        var r = new ReputationBook();
        r.Refused("a", "Ana");
        Assert.True(r.Person("a") < 0);
        r.Helped("b", "Bram");
        r.Robbed("b", "Bram");
        Assert.Equal(ReputationBook.HelpRegard + ReputationBook.RobRegard, r.Person("b"), 6);
        for (var i = 0; i < 500; i++) r.TradedWith("c", "Corra");
        Assert.Equal(ReputationBook.TradeRegard, r.Person("c"), 6);
    }

    [Fact]
    public void Reputation_survives_a_save()
    {
        var save = new CompanySave { Standing = new Standing() };
        save.Standing.Reputation.Deed(5, Town);
        save.Standing.Reputation.Robbed("a", "Ana");
        save.Standing.Reputation.Trip(Town);
        var back = CompanySave.FromJson(save.ToJson()).Standing!.Reputation;
        Assert.Equal(save.Standing.Reputation.Own(Town), back.Own(Town), 6);
        Assert.Equal(ReputationBook.RobRegard, back.Person("a"), 6);
        Assert.Equal(save.Standing.Reputation.Link(Town), back.Link(Town), 6);
    }

    [Fact]
    public void The_number_on_the_meter_reads_cleanly()
    {
        Assert.Equal(("0", "+1", "-2.5"), (ReputationBook.Number(0), ReputationBook.Number(1), ReputationBook.Number(-2.5)));
    }
}
