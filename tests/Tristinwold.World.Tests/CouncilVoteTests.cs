using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>A council with more than one hero at it doesn't belong to anyone: they put motions, and everyone at the table votes.</summary>
public class CouncilVoteTests
{
    const string Grey = "city:greywater", Dell = "hamlet:dell";

    static CityEconomy Table(bool councillors, params string[] heroes)
    {
        var c = CityEconomy.Start(5);
        foreach (var (id, name, x) in new[] { (Grey, "Greywater", 500.0), (Dell, "Dell", 700.0) })
        {
            var m = new TownMarket { Id = id, Name = name, X = x, Size = 0.35, Makes = [Good.Timber], Wants = [Good.Iron] };
            foreach (var g in Enum.GetValues<Good>()) m.Stock[g] = m.Usual(g);
            c.Roads.Know(m);
            c.Apply(CityAction.CouncilMet(id, name, [$"{id}/council0|Ana", $"{id}/council1|Bram", $"{id}/council2|Cass"]));
        }
        if (!councillors) foreach (var id in c.CouncilOf(Grey).Members.Keys.ToList()) c.Apply(CityAction.CouncilFall(Grey, id));
        foreach (var h in heroes) c.Apply(CityAction.CouncilSeat(Grey, h));
        c.CouncilOf(Grey).Purse = 300;
        return c;
    }

    static bool Put(CityEconomy c, string by, MotionKind kind, string target = "", double thought = 3) =>
        c.Apply(CityAction.CouncilMotion(Grey, by, kind, target, RaidAim.Supplies, thought));

    static int Last(CityEconomy c) => c.CouncilOf(Grey).LastMotion;

    [Fact]
    public void A_motion_waits_for_every_hero_and_a_shared_purse_is_split_between_them()
    {
        var c = Table(false, "Joe", "Vann-Tres");
        Assert.Null(c.CouncilOf(Grey).Ruler);
        Assert.True(Put(c, "Joe", MotionKind.SharePurse));
        Assert.Single(c.CouncilOf(Grey).Motions);
        Assert.True(c.Apply(CityAction.CouncilVote(Grey, "Vann-Tres", Last(c), true, 1)));
        Assert.Empty(c.CouncilOf(Grey).Motions);
        Assert.Equal(150, c.CouncilOf(Grey).Owed["Joe"]);
        Assert.Equal(150, c.CouncilOf(Grey).Owed["Vann-Tres"]);
        Assert.Equal(0, c.CouncilOf(Grey).Purse);
        Assert.True(c.Apply(CityAction.CouncilCollect(Grey, "Joe")));
        Assert.False(c.CouncilOf(Grey).Owed.ContainsKey("Joe"));
    }

    [Fact]
    public void A_tie_goes_the_way_of_the_hero_the_town_thinks_best_of()
    {
        var c = Table(false, "Joe", "Vann-Tres");
        Put(c, "Joe", MotionKind.PayPurse, "Joe", thought: 4);
        c.Apply(CityAction.CouncilVote(Grey, "Vann-Tres", Last(c), false, 1));
        Assert.Equal(300, c.CouncilOf(Grey).Owed["Joe"]);

        c = Table(false, "Joe", "Vann-Tres");
        Put(c, "Joe", MotionKind.PayPurse, "Joe", thought: 1);
        c.Apply(CityAction.CouncilVote(Grey, "Vann-Tres", Last(c), false, 4));
        Assert.False(c.CouncilOf(Grey).Owed.ContainsKey("Joe"));
        Assert.Equal(300, c.CouncilOf(Grey).Purse);
        Assert.Contains(c.News, n => n.Contains("voted down"));
    }

    [Fact]
    public void Councillors_never_empty_the_purse_into_a_hero_s_pocket_but_back_spending_it_on_the_town()
    {
        var c = Table(true, "Joe");
        c.FabricOf(Grey).Burnt = 2;
        Assert.True(Put(c, "Joe", MotionKind.PayPurse, "Joe", thought: 5));
        Assert.Equal(300, c.CouncilOf(Grey).Purse); // three councillors against, and it's over at once: everyone's voted
        Assert.True(Put(c, "Joe", MotionKind.Spend));
        Assert.Equal(0, c.CouncilOf(Grey).Purse);
        Assert.Equal(0, c.FabricOf(Grey).Burnt);
    }

    [Fact]
    public void Putting_a_hero_off_the_table_takes_two_votes_in_three_and_they_can_t_vote_on_it()
    {
        var c = Table(false, "Joe", "Ann", "Vann-Tres");
        Put(c, "Joe", MotionKind.Unseat, "Vann-Tres");
        Assert.False(c.Apply(CityAction.CouncilVote(Grey, "Vann-Tres", Last(c), false, 5)));
        c.Apply(CityAction.CouncilVote(Grey, "Ann", Last(c), false, 1));
        Assert.Contains("Vann-Tres", c.CouncilOf(Grey).Heroes);

        Put(c, "Joe", MotionKind.Unseat, "Vann-Tres");
        c.Apply(CityAction.CouncilVote(Grey, "Ann", Last(c), true, 1));
        Assert.DoesNotContain("Vann-Tres", c.CouncilOf(Grey).Heroes);
    }

    [Fact]
    public void A_vote_closes_after_a_day_with_whoever_voted_and_a_held_place_can_be_set_free()
    {
        var c = Table(false, "Joe", "Vann-Tres");
        c.CouncilOf(Grey).Holds.Add(Dell);
        Put(c, "Joe", MotionKind.Free, Dell);
        c.TendMotions(CityEconomy.MotionSeconds / 2);
        Assert.Contains(Dell, c.CouncilOf(Grey).Holds);
        Assert.True(c.TendMotions(CityEconomy.MotionSeconds / 2 + 1));
        Assert.DoesNotContain(Dell, c.CouncilOf(Grey).Holds);
    }

    [Fact]
    public void Nobody_puts_motions_to_a_table_they_rule_alone_or_don_t_sit_at()
    {
        var c = Table(false, "Joe");
        Assert.Equal("Joe", c.CouncilOf(Grey).Ruler);
        Assert.False(Put(c, "Joe", MotionKind.SharePurse));
        Assert.False(Put(c, "Stranger", MotionKind.SharePurse));
        c.Apply(CityAction.CouncilSeat(Grey, "Ann"));
        Assert.True(Put(c, "Ann", MotionKind.Raid, Dell));
    }
}
