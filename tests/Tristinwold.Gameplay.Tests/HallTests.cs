using System.Text.Json;
using Xunit;

namespace Tristinwold.Gameplay.Tests;

/// <summary>Heroes found halls by their class, take people on, and the halls work for them on the shared world's clock.</summary>
public class HallTests
{
    static Hall Found(CityEconomy city, HallKind kind, string owner = "Joe", double x = 500, double z = 500)
    {
        var hall = new Hall { Owner = owner, Hero = owner, Kind = kind, Name = $"{owner}'s {Halls.PlanOf(kind).Word}", X = x, Z = z };
        Assert.True(city.Apply(CityAction.HallFound(hall)));
        return city.HallOf(owner, kind)!;
    }

    [Fact]
    public void Each_class_founds_its_own_kind_and_artificers_and_druids_none()
    {
        Assert.Equal(HallKind.ThievesGuild, Halls.For("Thief")!.Kind);
        Assert.Equal(HallKind.ThievesGuild, Halls.For("Assassin")!.Kind);
        Assert.Equal(HallKind.HuntingLodge, Halls.For("Ranger")!.Kind);
        Assert.Equal(HallKind.FightClub, Halls.For("Barbarian")!.Kind);
        Assert.Equal(HallKind.Monastery, Halls.For("MartialArtist")!.Kind);
        Assert.Equal(HallKind.Chapel, Halls.For("Paladin")!.Kind);
        Assert.Equal(HallKind.Spire, Halls.For("Sorcerer")!.Kind);
        Assert.Equal(HallKind.Playhouse, Halls.For("Poet")!.Kind);
        Assert.Null(Halls.For("Alchemist"));
        Assert.Null(Halls.For("Stormdruid"));
        foreach (var plan in Halls.Plans) Assert.InRange(plan.Buildings.Count, 3, 5); // a courtyard of three to five
    }

    [Fact]
    public void A_hall_is_founded_once_and_never_on_top_of_another()
    {
        var city = CityEconomy.Start(1);
        var hall = Found(city, HallKind.FightClub);
        Assert.Equal(1, hall.Members);
        Assert.False(city.Apply(CityAction.HallFound(new Hall { Owner = "Joe", Hero = "Joe", Kind = HallKind.FightClub, Name = "Again", X = 900, Z = 900 })));
        Assert.False(city.Apply(CityAction.HallFound(new Hall { Owner = "Mira", Hero = "Mira", Kind = HallKind.Chapel, Name = "Too close", X = 520, Z = 510 })));
        Assert.True(city.Apply(CityAction.HallFound(new Hall { Owner = "Mira", Hero = "Mira", Kind = HallKind.Chapel, Name = "Mira's Chapel", X = 600, Z = 500 })));
        Assert.Contains(city.News, n => n.Contains("founded Mira's Chapel"));
    }

    [Fact]
    public void Recruits_are_capped_by_the_founders_level()
    {
        var city = CityEconomy.Start(1);
        var hall = Found(city, HallKind.Monastery);
        for (var i = 0; i < 20; i++) city.Apply(CityAction.HallRecruit(hall.Key, 6));
        Assert.Equal(Halls.MostMembers(6), hall.Members);
        Assert.True(city.Apply(CityAction.HallRecruit(hall.Key, 30)));
        Assert.Equal(12, Halls.MostMembers(60));
    }

    [Fact]
    public void Halls_work_while_their_founder_is_away()
    {
        var city = CityEconomy.Start(3);
        var lodge = Found(city, HallKind.HuntingLodge);
        var club = Found(city, HallKind.FightClub, "Mira", 800, 800);
        city.Apply(CityAction.HallRecruit(lodge.Key, 9));
        city.TendHalls(Halls.JobSeconds * 5);
        Assert.True(lodge.Chest.Values.Sum() >= 10);
        Assert.True(lodge.Till > 0 && club.Till > 0);
        Assert.True(club.Prowess > 0);
        var take = lodge.Till;
        Assert.True(city.Apply(CityAction.HallTill(lodge.Key, take)));
        Assert.Equal(0, lodge.Till);
        Assert.False(city.Apply(CityAction.HallTill(lodge.Key, 1)));
        var id = lodge.Chest.Keys.First();
        var had = lodge.Chest[id];
        Assert.True(city.Apply(CityAction.HallTake(lodge.Key, id, 7)));
        Assert.Equal(had - 1, lodge.Chest.GetValueOrDefault(id));
        Assert.Equal(7, lodge.Till);
    }

    [Fact]
    public void A_thieves_guild_too_hot_loses_thieves_and_lying_low_cools_it()
    {
        var city = CityEconomy.Start(5);
        var den = Found(city, HallKind.ThievesGuild);
        for (var i = 0; i < 6; i++) city.Apply(CityAction.HallRecruit(den.Key, 30));
        city.TendHalls(Halls.JobSeconds * 40);
        Assert.True(den.Members < 7, $"the Watch took some of them: {den.Members} {den.Heat} {den.Till}");
        Assert.Contains(city.News, n => n.Contains("thieves"));
        city.Apply(CityAction.HallRecruit(den.Key, 30)); // someone left to lie low
        Assert.True(city.Apply(CityAction.HallLow(den.Key)));
        var (heat, till) = (den.Heat, den.Till);
        city.TendHalls(Halls.JobSeconds * 3);
        Assert.True(den.Heat < heat || heat == 0);
        Assert.Equal(till, den.Till);
    }

    [Fact]
    public void Halls_travel_with_the_city()
    {
        var city = CityEconomy.Start(1);
        var hall = Found(city, HallKind.Spire);
        city.TendHalls(Halls.JobSeconds * 3);
        var back = JsonSerializer.Deserialize<CityEconomy>(JsonSerializer.Serialize(city))!;
        Assert.Equal(hall.Till, back.HallOf("Joe", HallKind.Spire)!.Till);
        Assert.True(back.Apply(CityAction.HallClose(hall.Key)));
        Assert.Empty(back.Halls);
    }

    [Fact]
    public void A_druids_trees_grow_and_keep_their_distance()
    {
        var city = CityEconomy.Start(1);
        Assert.True(city.Apply(CityAction.Plant("Oak", 10, 10)));
        Assert.False(city.Apply(CityAction.Plant("Oak", 11, 10)));
        Assert.True(city.Apply(CityAction.Plant("Oak", 14, 10)));
        var p = city.Grove[0];
        Assert.Equal(0, Groves.Stage(p, p.Day));
        Assert.Equal(1, Groves.Stage(p, p.Day + 1));
        Assert.Equal(2, Groves.Stage(p, p.Day + 3));
        for (var i = 0; i < Groves.MostEach + 5; i++) city.Apply(CityAction.Plant("Oak", 100 + i * 3, 0));
        Assert.Equal(Groves.MostEach, city.Grove.Count(t => t.Owner == "Oak"));
        Assert.True(Groves.MayPlant("Spiritcaller") && !Groves.MayPlant("Wizard"));
    }
}
