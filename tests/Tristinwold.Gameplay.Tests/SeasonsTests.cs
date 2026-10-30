using Xunit;

namespace Tristinwold.Gameplay.Tests;

/// <summary>The year goes round, the weather suits the season, and everyone gets the same sky.</summary>
public class SeasonsTests
{
    [Fact]
    public void Four_seasons_of_seven_days_go_round()
    {
        Assert.Equal(Season.Spring, Seasons.Of(1));
        Assert.Equal(Season.Spring, Seasons.Of(7));
        Assert.Equal(Season.Summer, Seasons.Of(8));
        Assert.Equal(Season.Fall, Seasons.Of(15));
        Assert.Equal(Season.Winter, Seasons.Of(22));
        Assert.Equal(Season.Spring, Seasons.Of(29));
    }

    [Fact]
    public void The_same_day_is_the_same_sky_and_the_sky_changes()
    {
        Assert.Equal(Seasons.WeatherOn(40, 1), Seasons.WeatherOn(40, 1));
        var skies = Enumerable.Range(1, Seasons.YearDays * 3).SelectMany(d => Enumerable.Range(0, Seasons.SpellsADay).Select(s => Seasons.WeatherOn(d, s))).ToList();
        Assert.True(skies.Distinct().Count() >= 5, string.Join(",", skies.Distinct()));
    }

    [Fact]
    public void It_only_snows_when_it_should()
    {
        for (var d = 1; d <= Seasons.YearDays; d++)
            for (var s = 0; s < Seasons.SpellsADay; s++)
            {
                if (Seasons.Of(d) != Season.Winter) Assert.NotEqual(Weather.Snow, Seasons.WeatherOn(d, s));
                else Assert.NotEqual(Weather.Rain, Seasons.WeatherOn(d, s));
                Assert.NotEqual(Weather.Rain, Seasons.WeatherOn(d, s, cold: true)); // up north it's snow already
                Assert.NotEqual(Weather.Storm, Seasons.WeatherOn(d, s, cold: true));
            }
    }

    [Fact]
    public void Snow_lies_only_in_winter_and_builds_up()
    {
        Assert.Equal(0, Seasons.SnowCover(1));
        Assert.Equal(0, Seasons.SnowCover(21));
        Assert.True(Seasons.SnowCover(22) > 0);
        Assert.True(Seasons.SnowCover(25) > Seasons.SnowCover(22));
        Assert.True(Seasons.SnowCover(28) < Seasons.SnowCover(25));
    }
}
