using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class TavernGamesTests
{
    [Fact]
    public void Aces_count_low_when_they_have_to()
    {
        Assert.Equal(21, TwentyOne.Value([new Card(1, 0), new Card(13, 0)]));
        Assert.Equal(12, TwentyOne.Value([new Card(1, 0), new Card(1, 1)]));
        Assert.Equal(21, TwentyOne.Value([new Card(1, 0), new Card(9, 0), new Card(1, 1)]));
        Assert.Equal(13, TwentyOne.Value([new Card(1, 0), new Card(2, 0), new Card(10, 0)]));
    }

    [Fact]
    public void Every_hand_ends_and_pays_what_it_should()
    {
        for (var i = 0; i < 400; i++)
        {
            var g = new TwentyOne(10, new Rng((ulong)i + 1));
            while (g.Result == TableResult.Playing && TwentyOne.Value(g.Hand) < 16) g.Hit();
            g.Stand();
            Assert.NotEqual(TableResult.Playing, g.Result);
            Assert.True(TwentyOne.Value(g.House) >= 17 || g.Result == TableResult.Lost || g.Result == TableResult.Natural);
            Assert.Equal(g.Result switch { TableResult.Natural => 25, TableResult.Won => 20, TableResult.Push => 10, _ => 0 }, g.Returned);
            if (TwentyOne.Value(g.Hand) > 21) Assert.Equal(TableResult.Lost, g.Result);
        }
    }

    [Fact]
    public void The_cards_are_all_different_in_a_hand()
    {
        var g = new TwentyOne(5, new Rng(3));
        Assert.Equal(4, g.Hand.Concat(g.House).Distinct().Count());
    }

    [Fact]
    public void The_house_has_a_small_edge_at_doubles()
    {
        var (rng, back) = (new Rng(9), 0);
        for (var i = 0; i < 60000; i++) back += Doubles.Returned(10, Doubles.Throw(rng));
        var share = back / (60000.0 * 10);
        Assert.InRange(share, 0.78, 0.88); // about 83 back on every hundred staked
        Assert.Equal(50, Doubles.Returned(10, (4, 4)));
        Assert.Equal(0, Doubles.Returned(10, (4, 5)));
    }

    [Fact]
    public void Tips_grow_with_level_and_goodwill()
    {
        Assert.True(Tavern.Tips(20, 0, new Rng(1)) > Tavern.Tips(1, 0, new Rng(1)));
        Assert.True(Tavern.Tips(5, 4, new Rng(1)) > Tavern.Tips(5, -4, new Rng(1)));
    }

    [Fact]
    public void The_helm_rewards_a_true_course()
    {
        var b = 1.2;
        Assert.Equal(Helm.BestPace, Helm.Pace(b, b, 1), 6);
        Assert.True(Helm.Pace(b, b, 1) > 1); // better than the crew alone
        Assert.True(Helm.Pace(b + Math.PI, b, 1) < 0); // sailing away loses ground
        Assert.True(Helm.Pace(b, b, Helm.ReefSail) < Helm.Pace(b, b, 1));
        Assert.Equal("steady on course", Helm.Steer(b, b));
        Assert.Contains("port", Helm.Steer(b - 1, b));
        Assert.Contains("starboard", Helm.Steer(b + 1, b));
    }

    [Fact]
    public void The_sea_is_the_same_each_time_and_clear_where_she_starts()
    {
        Assert.Empty(Helm.In(0, 0, "port:druitt"));
        Assert.Equal(Helm.In(3, -2, "port:druitt"), Helm.In(3, -2, "port:druitt"));
        var marks = Enumerable.Range(-8, 17).SelectMany(x => Enumerable.Range(-8, 17).SelectMany(z => Helm.In(x, z, "port:druitt"))).ToList();
        Assert.Contains(marks, m => m.Reef);
        Assert.Contains(marks, m => !m.Reef);
    }
}
