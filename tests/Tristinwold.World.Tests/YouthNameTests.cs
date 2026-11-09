using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>The young of the towns: plenty of names to go round, their own for each hero, and new faces after one leaves.</summary>
public class YouthNameTests
{
    [Fact]
    public void The_young_draw_on_a_far_bigger_pool_of_names()
    {
        var (girls, boys, either) = Names.YoungPool;
        Assert.True(girls >= 100 && boys >= 100 && either >= 25, $"{girls} girls' names, {boys} boys', {either} either");
        var rng = new Rng(7);
        var firsts = Enumerable.Range(0, 60).Select(_ => Names.YoungGiven(rng, Sex.Male)).ToList();
        Assert.True(firsts.Distinct().Count() >= 40, $"{firsts.Distinct().Count()} different among 60 boys");
    }

    [Fact]
    public void Each_hero_meets_their_own_town_children()
    {
        var world = new WorldGen();
        var towns = world.SettlementsNear(0, 0, 3000).Where(s => world.YouthsOf(s).Count > 0).Take(6).ToList();
        var mine = towns.SelectMany(world.YouthsOf).Select(y => y.Name).ToList();
        world.YouthSalt = 12345;
        var theirs = towns.SelectMany(world.YouthsOf).Select(y => y.Name).ToList();
        Assert.NotEqual(mine, theirs);
        Assert.True(mine.Intersect(theirs).Count() <= mine.Count / 4, "hardly any the same");
        world.YouthSalt = 12345;
        Assert.Equal(theirs, towns.SelectMany(world.YouthsOf).Select(y => y.Name)); // the same hero, the same children
        Assert.All(towns.SelectMany(world.YouthsOf), y => Assert.True(People.IsYouth(y)));
    }

    [Fact]
    public void A_youth_who_leaves_with_a_hero_is_followed_by_a_new_face()
    {
        var world = new WorldGen();
        var town = world.SettlementsNear(0, 0, 3000).First(s => world.YouthsOf(s).Count > 0);
        var left = world.YouthsOf(town)[0];
        var next = People.Newcomer(world, town, left, 0);
        var after = People.Newcomer(world, town, left, 1);
        Assert.NotEqual(left.Id, next.Id);
        Assert.NotEqual(next.Id, after.Id);
        Assert.NotEqual(left.Name, next.Name);
        Assert.NotEqual(next.Name, after.Name);
        Assert.True(People.IsYouth(next) && next.HomeId == town.Id);
        Assert.Equal(next, People.Newcomer(world, town, left, 0)); // the same newcomer every time you look
        Assert.Contains("came to", next.Backstory);
    }
}
