using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class LivingTownTests
{
    const string Grey = "city:greywater", Druitt = "city:druitt", Dell = "hamlet:dell";

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

    static void Thrive(CityEconomy c, string town)
    {
        c.Roads.Towns[town].LastTrade = c.Day;
        foreach (var g in c.Roads.Towns[town].Stock.Keys.ToList()) c.Roads.Towns[town].Stock[g] = c.Roads.Towns[town].Usual(g) * 1.2;
    }

    [Fact]
    public void A_town_with_wagons_and_full_stores_thrives_and_one_stripped_bare_fails()
    {
        var c = World();
        Thrive(c, Grey);
        Assert.True(c.Prosperity(Grey) >= CityEconomy.Thrives);
        c.Day += 100;
        foreach (var g in c.Roads.Towns[Druitt].Stock.Keys.ToList()) c.Roads.Towns[Druitt].Stock[g] = 0;
        Assert.True(c.Prosperity(Druitt) <= CityEconomy.Fails);
    }

    [Fact]
    public void Carpenters_build_out_past_the_wall_of_a_town_that_thrives()
    {
        var c = World();
        Thrive(c, Grey);
        c.Stock[Good.Timber] = 500;
        var rng = new Tristinwold.Deviant.Rng(1);
        for (var i = 0; i < 20; i++) { Thrive(c, Grey); c.Stock[Good.Timber] = 500; c.Carpenters(rng); }
        Assert.True(c.FabricOf(Grey).Grown > 0);
    }

    [Fact]
    public void Hard_times_leave_homes_to_rot_and_the_good_years_mend_them()
    {
        var c = World();
        c.Day = 100;
        foreach (var g in c.Roads.Towns[Grey].Stock.Keys.ToList()) c.Roads.Towns[Grey].Stock[g] = 0;
        for (var i = 0; i < 80; i++)
        {
            foreach (var g in c.Roads.Towns[Grey].Stock.Keys.ToList()) c.Roads.Towns[Grey].Stock[g] = 0;
            c.NextDay();
        }
        var left = c.FabricOf(Grey).Derelict;
        c.Siege = null; // what Soren burned in those years goes back up first, and that's another test
        c.Roads.Troubles.Clear();
        c.Roads.State.Clear();
        foreach (var f in c.Fabric.Values) f.Burnt = 0;
        Assert.InRange(left, 1, CityEconomy.DerelictCap);
        c.Stock[Good.Timber] = 500;
        Thrive(c, Grey);
        var rng = new Tristinwold.Deviant.Rng(2);
        for (var i = 0; i < 30 && c.FabricOf(Grey).Derelict == left; i++) { Thrive(c, Grey); c.Stock[Good.Timber] = 500; c.Carpenters(rng); }
        Assert.Equal(left - 1, c.FabricOf(Grey).Derelict);
    }

    [Fact]
    public void Three_members_sit_on_a_council_and_the_last_hero_standing_rules()
    {
        var c = World();
        Assert.True(c.Apply(CityAction.CouncilSeat(Grey, "Joe's Company")));
        Assert.Null(c.CouncilOf(Grey).Ruler);
        foreach (var id in c.CouncilOf(Grey).Members.Keys.ToList()) Assert.True(c.Apply(CityAction.CouncilFall(Grey, id)));
        Assert.Equal("Joe's Company", c.CouncilOf(Grey).Ruler);
        Assert.Contains(c.News, n => n.Contains("rules it now"));
    }

    [Fact]
    public void Nobody_rules_an_empty_table_or_one_shared_with_another_hero()
    {
        var c = World();
        foreach (var id in c.CouncilOf(Grey).Members.Keys.ToList()) c.Apply(CityAction.CouncilFall(Grey, id));
        Assert.Null(c.CouncilOf(Grey).Ruler);
        c.SeatHero(Grey, "One");
        c.SeatHero(Grey, "Two");
        Assert.Null(c.CouncilOf(Grey).Ruler);
    }

    [Fact]
    public void A_raid_costs_the_raiders_their_good_name_and_can_only_go_out_so_often()
    {
        var c = World();
        var said = c.Raid(Grey, Druitt, RaidAim.Supplies, new Tristinwold.Deviant.Rng(3));
        Assert.NotNull(said);
        Assert.Equal(-CityEconomy.RaidShame, c.CouncilOf(Grey).Standing);
        Assert.False(c.MayRaid(Grey));
        Assert.Null(c.Raid(Grey, Dell, RaidAim.Supplies, new Tristinwold.Deviant.Rng(3)));
    }

    [Fact]
    public void A_won_raid_takes_stores_tribute_or_the_place_itself()
    {
        for (ulong seed = 0; seed < 40; seed++)
        {
            var c = World();
            var before = c.Roads.Towns[Dell].Stock.Values.Sum();
            if (c.Raid(Grey, Dell, RaidAim.Supplies, new Tristinwold.Deviant.Rng(seed)) is { } s && s.Contains("carried off"))
                Assert.True(c.Roads.Towns[Dell].Stock.Values.Sum() < before);
            c = World();
            if (c.Raid(Grey, Dell, RaidAim.Trade, new Tristinwold.Deviant.Rng(seed)) is { } t && t.Contains("tribute"))
                Assert.True(c.CouncilOf(Grey).Tribute.ContainsKey(Dell));
            c = World();
            if (c.Raid(Grey, Dell, RaidAim.Land, new Tristinwold.Deviant.Rng(seed)) is { } l && l.Contains("took it"))
                Assert.Contains(Dell, c.CouncilOf(Grey).Holds);
        }
    }

    [Fact]
    public void The_neighbours_band_together_against_a_town_with_a_bad_name()
    {
        var c = World();
        c.CouncilOf(Grey).Standing = -5;
        var heard = new List<string>();
        for (var i = 0; i < 12; i++) heard.AddRange(c.NextDay());
        Assert.True(c.CouncilOf(Grey).LastBanded > 0);
        Assert.Contains(heard, n => n.Contains("banded together"));
    }

    [Fact]
    public void Every_town_has_a_smith_and_a_general_store_of_its_own()
    {
        var smith = Shops.ForTown(Grey, TownTrade.Smith, "Hale's Smithy", "Ona Hale");
        var store = Shops.ForTown(Grey, TownTrade.General, "Hale's General Goods", "Ona Hale");
        Assert.True(smith.Forge);
        Assert.Equal(Shops.Smithy.Sells, smith.Sells);
        Assert.Equal(Shops.Provisions.Sells, store.Sells);
        Assert.Same(smith, Shops.Of(smith.Id));
        Assert.Same(Shops.Sundries, Shops.Of(Shops.Sundries.Id));
    }

    [Fact]
    public void Tristinwold_has_a_smith_and_store_more_on_the_outer_ring_and_its_own_on_the_inner()
    {
        Assert.Equal(3, Shops.All.Count(s => s.Forge));
    }

    [Fact]
    public void A_council_is_usually_three_and_keeps_the_same_faces()
    {
        var world = WorldGen.Default;
        var towns = world.SettlementsNear(0, 0, 3000).Where(TownCouncils.Keeps).ToList();
        Assert.NotEmpty(towns);
        var sizes = towns.Select(s => TownCouncils.Members(s).Count).ToList();
        Assert.True(sizes.Count(n => n == 3) > sizes.Count / 2);
        Assert.Equal(TownCouncils.Members(towns[0]).Select(m => m.Name), TownCouncils.Members(towns[0]).Select(m => m.Name));
        Assert.All(TownCouncils.Members(towns[0]), m => Assert.True(TownCouncils.IsMember(m.Id)));
    }
}
