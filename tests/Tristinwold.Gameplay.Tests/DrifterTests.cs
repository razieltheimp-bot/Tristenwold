using Tristinwold.Gameplay;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

public class DrifterTests
{
    static Hire Job(string drifter, HireKind kind, string target, string by = "Hirer") =>
        new() { Id = $"{drifter}-{kind}", Drifter = drifter, Kind = kind, Target = target, TargetName = target, By = by };

    [Fact]
    public void A_good_drifter_only_goes_after_outlaws_and_never_a_town()
    {
        var city = new CityEconomy();
        Assert.False(city.Apply(CityAction.Hired(Job("yokin", HireKind.Town, "city:druitt"))));
        Assert.False(city.Apply(CityAction.Hired(Job("yokin", HireKind.Hero, "Honest Abe"))));
        city.Apply(CityAction.Outlaw("Black Bart", 200));
        Assert.True(city.Apply(CityAction.Hired(Job("yokin", HireKind.Hero, "Black Bart"))));
        Assert.True(city.Apply(CityAction.Hired(Job("broly", HireKind.Town, "city:druitt")))); // a neutral one takes anything
        Assert.True(city.Apply(CityAction.Hired(Job("gornok", HireKind.GuardShip, "Hirer"))));
        Assert.Equal(city.Day + 2, city.HireOf("gornok")!.Ends);
    }

    [Fact]
    public void One_job_at_a_time_and_never_against_whoever_paid()
    {
        var city = new CityEconomy();
        Assert.False(city.Apply(CityAction.Hired(Job("faye", HireKind.Hero, "Hirer"))));
        Assert.True(city.Apply(CityAction.Hired(Job("faye", HireKind.RaidCaravan, "Someone"))));
        Assert.False(city.Apply(CityAction.Hired(Job("faye", HireKind.RaidShip, "Someone"))));
    }

    [Fact]
    public void They_start_at_ten_and_grow_with_no_ceiling()
    {
        var city = new CityEconomy();
        Assert.Equal(10, city.DrifterLevel("irohe"));
        city.Apply(CityAction.Hired(Job("irohe", HireKind.Hero, "Someone")));
        city.Apply(CityAction.DrifterWon("irohe", "irohe-Hero"));
        Assert.Null(city.HireOf("irohe"));
        Assert.Equal(12, city.DrifterLevel("irohe"));
        for (var i = 0; i < 50; i++) city.Apply(CityAction.DrifterFell("irohe", "Hero"));
        Assert.Equal(62, city.DrifterLevel("irohe"));
        Assert.True(city.DrifterDown("irohe"));
    }

    [Fact]
    public void Killing_one_ends_whatever_they_were_paid_for()
    {
        var city = new CityEconomy();
        city.Apply(CityAction.Hired(Job("broly", HireKind.Town, "city:tristinwold")));
        Assert.True(city.Apply(CityAction.DrifterFell("broly", "Somebody")));
        Assert.Null(city.HireOf("broly"));
        Assert.False(city.Apply(CityAction.Hired(Job("broly", HireKind.Town, "city:tristinwold")))); // dead till tomorrow
    }
}
