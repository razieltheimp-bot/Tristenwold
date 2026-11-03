using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

public class WarlordTests
{
    const string Grey = "city:greywater", Druitt = "city:druitt", Dell = "hamlet:dell", Joe = "Joe's Company";

    static TownMarket Market(string id, string name, double x, double size = 0.35)
    {
        var m = new TownMarket { Id = id, Name = name, X = x, Size = size, Makes = [Good.Timber], Wants = [Good.Iron] };
        foreach (var g in Enum.GetValues<Good>()) m.Stock[g] = m.Usual(g);
        return m;
    }

    static CityEconomy World()
    {
        var c = CityEconomy.Start(5);
        foreach (var m in new[] { Market(Grey, "Greywater", 500), Market(Druitt, "Druitt", 900), Market(Dell, "Dell", 700, 0.15) })
        {
            c.Roads.Know(m);
            c.Apply(CityAction.CouncilMet(m.Id, m.Name, [$"{m.Id}/council0|Ana", $"{m.Id}/council1|Bram", $"{m.Id}/council2|Cass"]));
        }
        return c;
    }

    /// <summary>Joe sits on Greywater's council, and is the last of it standing.</summary>
    static CityEconomy JoeRulesGrey()
    {
        var c = World();
        c.Apply(CityAction.CouncilSeat(Grey, Joe));
        foreach (var id in c.CouncilOf(Grey).Members.Keys.ToList()) c.Apply(CityAction.CouncilFall(Grey, id));
        Assert.Equal(Joe, c.CouncilOf(Grey).Ruler);
        return c;
    }

    static Contender Grask(string kind = "bandit_chief", params string[] grudges) => new("d1", "Grask the Grinning", kind, 6, Rank.Ringleader, grudges);

    static void Days(CityEconomy c, int days, IReadOnlyList<Contender> who, Func<bool>? until = null)
    {
        for (var i = 0; i < days && until?.Invoke() != true; i++)
        {
            c.Day++;
            c.TendWarlords(who);
        }
    }

    [Fact]
    public void A_hero_who_rules_a_town_draws_a_rival_into_the_next_one_who_takes_it_over()
    {
        var c = JoeRulesGrey();
        var who = new[] { Grask("bandit_chief", Joe) };
        Days(c, 60, who, () => c.RivalOf(Joe) is { Lord.Rules: true });
        var (town, lord) = c.RivalOf(Joe) ?? throw new Xunit.Sdk.XunitException("no rival came");
        Assert.NotEqual(Grey, town);
        Assert.Equal("Grask the Grinning", lord.Name);
        Assert.True(lord.Rules);
        Assert.All(c.CouncilOf(town).Members.Keys, id => Assert.Contains(id, c.CouncilOf(town).Fallen));
        Assert.Contains(c.News, n => n.Contains("Grask the Grinning") && n.Contains("rules it now"));
    }

    [Fact]
    public void A_rival_at_the_head_of_a_table_sends_its_raiders_at_the_hero_and_can_throw_them_off_their_council()
    {
        var c = JoeRulesGrey();
        var who = new[] { Grask("bandit_chief", Joe) };
        Days(c, 400, who, () => c.CouncilOf(Grey).Heroes.Count == 0);
        var (town, lord) = c.RivalOf(Joe)!.Value;
        Assert.Contains(c.News, n => n.Contains("Grask the Grinning, of") && n.Contains("Greywater"));
        Assert.Empty(c.CouncilOf(Grey).Heroes);
        Assert.Contains(Grey, c.CouncilOf(town).Holds);
        Assert.Equal(Joe, lord.Ousted[Grey]);
        Assert.Null(c.CouncilOf(Grey).Ruler);
    }

    [Fact]
    public void Turlin_Soren_and_beasts_never_sit_at_a_table_and_nobody_comes_without_a_grudge()
    {
        var c = JoeRulesGrey();
        Days(c, 120, [Grask("turlin", Joe) with { Id = "t" }, Grask("dread_soren", Joe) with { Id = "s" }, Grask("wolf", Joe) with { Id = "w" }]);
        Assert.Empty(c.WarlordTowns);
        Days(c, 30, [Grask("bandit", "Someone Else") with { Rank = Rank.Stray }]);
        Assert.Null(c.RivalOf(Joe));
    }

    [Fact]
    public void Striking_one_down_frees_its_towns_gives_seats_back_and_puts_the_killer_at_the_head_of_its_table()
    {
        var c = JoeRulesGrey();
        var who = new[] { Grask("bandit_chief", Joe) };
        Days(c, 400, who, () => c.CouncilOf(Grey).Heroes.Count == 0);
        var (town, _) = c.RivalOf(Joe)!.Value;
        Assert.False(c.Apply(CityAction.CouncilSeat(town, "Mara's Band")));
        Assert.True(c.Apply(CityAction.WarlordFell(town, Joe)));
        Assert.Null(c.CouncilOf(town).Warlord);
        Assert.Empty(c.CouncilOf(town).Holds);
        Assert.Equal(Joe, c.CouncilOf(town).Ruler);
        Assert.Equal(Joe, c.CouncilOf(Grey).Ruler);
        Assert.Contains("d1", c.Unseated);
        // It never sits again, however long it lives.
        Days(c, 120, who);
        Assert.DoesNotContain(c.WarlordTowns, w => w.Lord.Id == "d1");
    }

    [Fact]
    public void One_gone_from_the_roster_leaves_its_table_and_an_ambitious_one_climbs_on_its_own()
    {
        var c = World();
        c.Day++;
        c.CouncilOf(Dell).Warlord = new Warlord { Id = "d9", Name = "Vorn", Kind = "raider", Rules = true };
        c.TendWarlords([]);
        Assert.Null(c.CouncilOf(Dell).Warlord);
        Assert.Contains(c.News, n => n.Contains("Vorn is dead"));

        var climber = new Contender("d2", "Mokak", "goblin", 4, Rank.Scourge, []);
        Days(c, 800, [climber], () => c.WarlordTowns.Any());
        Assert.Contains(c.WarlordTowns, w => w.Lord.Id == "d2" && w.Lord.Against is null);
    }

    [Fact]
    public void A_deviant_on_the_roster_bears_a_grudge_against_every_hero_it_has_fought()
    {
        var d = new Tristinwold.Deviant.Deviant { Name = "Grask", Kind = "bandit", Species = "Bandit", Epithet = "the Grinning" };
        d.Player(Joe).TimesKilledThem = 1;
        d.Player("Mara's Band").Memory = Memory.MetPlayer;
        var c = Warlords.From(d);
        Assert.Equal(["Joe's Company"], c.Grudges);
        Assert.Equal("Grask the Grinning", c.Name);
        Assert.Equal(d.Id.ToString(), c.Id);
    }
}
