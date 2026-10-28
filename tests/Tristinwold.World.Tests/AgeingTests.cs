using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>Growing up and growing old, by the calendar, every people its own way; and an Immortal who never stops.</summary>
public class AgeingTests
{
    static readonly WorldGen W = WorldGen.Default;

    [Fact]
    public void Ten_minutes_a_day_seven_days_a_week_and_a_year_of_336_days()
    {
        Assert.Equal(336, Calendar.DaysPerYear);
        Assert.Equal("the 1st of Thawmoon, year 1", Calendar.Date(1));
        Assert.Equal("the 2nd of Thawmoon, year 1", Calendar.Date(2));
        Assert.Equal("the 1st of Seedmoon, year 1", Calendar.Date(29));
        Assert.Equal("the 1st of Thawmoon, year 2", Calendar.Date(337));
        Assert.Equal(1, Calendar.Years(337), 6);
        // A day is ten minutes of play: a week a little over an hour.
        Assert.Equal(70, Calendar.DaysPerWeek * CityEconomy.DaySeconds / 60);
    }

    [Fact]
    public void Every_people_goes_young_adolescent_adult_seasoned_elder_at_its_own_pace()
    {
        foreach (var race in Races.All.Select(r => r.Id))
        {
            var l = Ageing.Of(race);
            Assert.True(0 < l.Adolescent && l.Adolescent < l.Adult && l.Adult < l.Seasoned && l.Seasoned < l.Elder && l.Elder < l.Dies, race);
            Assert.Equal(LifeStage.Young, Ageing.Stage(race, 1));
            Assert.Equal(LifeStage.Adolescent, Ageing.Stage(race, l.Adolescent));
            Assert.Equal(LifeStage.Adult, Ageing.Stage(race, l.Adult));
            Assert.Equal(LifeStage.Seasoned, Ageing.Stage(race, l.Seasoned));
            Assert.Equal(LifeStage.Elder, Ageing.Stage(race, l.Elder));
        }
        // A goblin is old at thirty; an elf of thirty is a child.
        Assert.Equal(LifeStage.Elder, Ageing.Stage("goblin", 30));
        Assert.Equal(LifeStage.Young, Ageing.Stage("elf", 30));
        // A forty-year-old human is a seasoned one of any people.
        Assert.All(Races.All, r => Assert.Equal(LifeStage.Seasoned, Ageing.Stage(r.Id, Ageing.InYearsOf(r.Id, 40))));
    }

    [Fact]
    public void Villages_are_home_to_more_than_humans_and_their_ages_are_their_own_peoples()
    {
        var places = W.SettlementsNear(0, 0, 8000).Where(s => !s.Hostile && s.People > 0).Take(60).ToList();
        var people = places.SelectMany(W.PeopleOf).ToList();
        Assert.Contains(people, n => n.Race == "human");
        Assert.True(people.Select(n => n.Race).Distinct().Count() >= 3, string.Join(",", people.Select(n => n.Race).Distinct()));
        Assert.All(people, n => Assert.True(n.Age < Ageing.Of(n.Race).Dies, $"{n.Name}, a {n.Race} of {n.Age}"));
        Assert.All(places.SelectMany(W.YouthsOf), y => Assert.True(People.IsYouth(y), $"{y.Name}, a {y.Race} of {y.Age}, isn't young"));
    }

    [Fact]
    public void Children_grow_up_and_each_coming_of_age_brings_a_new_child_and_the_old_pass_on()
    {
        var s = W.SettlementsNear(0, 0, 6000).First(p => W.YouthsOf(p).Any(y => y.Race == "human") && W.PeopleOf(p).Count > 2);
        var youth = W.YouthsOf(s).First(y => y.Race == "human");
        var now = Ageing.Villagers(W, s, 0);
        Assert.Contains(now, n => n.Id == youth.Id && People.IsYouth(n));
        var born = now.Count;

        // Come of age: grown, at grown work, and a new child in the family.
        var grownUp = Ageing.Of("human").Adult - youth.Age + 0.5;
        var then = Ageing.Villagers(W, s, grownUp);
        var grown = then.Single(n => n.Id == youth.Id);
        Assert.False(People.IsYouth(grown));
        Assert.Equal(LifeStage.Adult, Ageing.Stage(grown.Race, grown.Age));
        Assert.NotEqual(youth.Occupation, grown.Occupation);
        var child = then.Single(n => n.Id == $"{youth.Id}/g1");
        Assert.Equal(LifeStage.Young, Ageing.Stage(child.Race, child.Age));
        Assert.EndsWith(youth.Name.Split(' ').Last(), child.Name);

        // Years on: the old are gone, the village still has its young.
        var later = Ageing.Villagers(W, s, 90);
        Assert.DoesNotContain(later, n => W.PeopleOf(s).Any(o => o.Id == n.Id && o.Race == "human")); // every human there at the start is gone
        Assert.Contains(later, n => People.IsYouth(n));
        Assert.All(later, n => Assert.True(n.Age < Ageing.Of(n.Race).Dies));
        Assert.Equal(Ageing.Villagers(W, s, 90).Select(n => n.Id), later.Select(n => n.Id)); // the same for every company
        Assert.True(born > 0);
    }

    [Fact]
    public void An_Immortal_ages_and_goes_on_long_past_their_peoples_years()
    {
        var hero = new Hero();
        Ageing.Wake(hero, "goblin", 10);
        Assert.Equal(Ageing.InYearsOf("goblin", 20), hero.StartAge);
        Ageing.Wake(hero, "goblin", 500); // only the first morning counts
        Assert.Equal(10, hero.BornDay);
        Assert.Equal(hero.StartAge + 2, Ageing.AgeOf(hero, 10 + 2 * Calendar.DaysPerYear), 6);
        Assert.Contains("adult goblin", Ageing.Describe(hero, "goblin", "Goblin", 10));
        var ancient = 10 + 60 * Calendar.DaysPerYear;
        Assert.Contains("years past the oldest any goblin lives to", Ageing.Describe(hero, "goblin", "Goblin", ancient));
        Assert.StartsWith($"{hero.StartAge + 60}, an elder goblin", Ageing.Describe(hero, "goblin", "Goblin", ancient));
    }
}
