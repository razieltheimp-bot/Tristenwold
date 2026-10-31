using Tristinwold.Deviant;

namespace Tristinwold.World.Tests;

/// <summary>Finding new trade: ships past the charts, and the yard's scouts past the known roads.</summary>
public class ExplorationLinesTests
{
    [Fact]
    public void A_voyage_past_the_charts_finds_a_port_for_good()
    {
        var sea = new SeaBook { Rank = SeaRank.Deckhand };
        Assert.Null(sea.NextChart); // a deckhand doesn't get a chart
        sea.Rank = SeaRank.Bosun;
        var edge = sea.NextChart!;
        Assert.DoesNotContain(edge, sea.Ports);
        Assert.Null(sea.Sail(edge, new Rng(1))); // not on an ordinary voyage

        var v = sea.Sail(edge, new Rng(1), explore: true)!;
        Assert.True(v.Explore);
        Assert.True(v.Pay > sea.PayFor(edge));
        Assert.True(sea.Arrive());
        Assert.Contains(edge, sea.Ports); // found: on the list from now on, however far out it is
        Assert.NotEqual(edge, sea.NextChart);
        sea.CastOff();
        sea.Voyage = null;
        Assert.NotNull(sea.Sail(edge, new Rng(2))); // and an ordinary run goes there now
    }

    [Fact]
    public void There_is_only_so_much_to_chart_and_every_new_port_can_be_traded_with()
    {
        var sea = new SeaBook { Rank = SeaRank.Captain };
        foreach (var p in Seas.ToChart)
        {
            Assert.NotEmpty(p.Makes);
            Assert.NotEmpty(p.Wants);
            Assert.Same(p, Seas.Get(p.Id));
            Assert.NotNull(sea.Market(p, 0));
            sea.Charted.Add(p.Id);
        }
        Assert.Null(sea.NextChart);
        Assert.Equal("master of the charts", SeaBook.Explorer(sea.Charted.Count));
        Assert.Equal(Seas.Uncharted.Count + Seas.Ports.Count(p => p.Leagues <= sea.Reach), sea.Ports.Count());
        Assert.Equal(["port:druitt", "port:greywater"], new SeaBook { Rank = SeaRank.Deckhand }.Ports.Select(p => p.Id)); // all anyone knows, to begin with
        Assert.Equal(Seas.Ports.Concat(Seas.Uncharted).Count(), Seas.Ports.Concat(Seas.Uncharted).Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Charts_are_saved()
    {
        var sea = new SeaBook();
        sea.Charted.Add("port:verdane");
        var saved = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog(), sea: sea).ToJson());
        Assert.Contains("port:verdane", saved.Sea!.Charted);
    }

    [Fact]
    public void A_scout_finds_a_place_past_the_known_roads_and_opens_a_road_to_it()
    {
        var world = WorldGen.Default;
        var book = new TradeBook();
        var place = book.NextFrontier(world)!;
        Assert.False(book.Towns.ContainsKey(place.Id));
        Assert.Null(book.Found(place, 0)); // not sent yet
        Assert.True(book.Scout(place));
        Assert.False(book.Scout(place)); // one at a time
        var pay = book.Found(place, 0);
        Assert.True(pay > TradeBook.ScoutPay);
        Assert.True(book.Towns.ContainsKey(place.Id));
        Assert.Null(book.Scouting);
        Assert.Equal(1, book.Scouted);
        Assert.NotEqual(place.Id, book.NextFrontier(world)?.Id); // on to the next
    }
}
