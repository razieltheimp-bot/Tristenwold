using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

public class RumourTests
{
    static readonly ServerOptions Quiet = new() { RewindChance = 0, InterceptChance = 0, VannEnabled = false, KeystoneMeetChance = 0 };

    static Hello Hi(string player) => new(player, "", Wire.ProtocolVersion, [$"{player}-A"]);

    const string Dwarf = "a tall, raven-haired dwarf woman with the look of a paladin";

    [Fact]
    public void A_hero_is_described_and_never_named()
    {
        var look = Avatar.For("Paladin") with { Race = "dwarf", Sex = "Female", Height = "Tall", Hair = "Raven" };
        Assert.Equal(Dwarf, Rumour.Describe(look, "Paladin"));
        Assert.Equal("an old, hooded elf man with the look of a wizard",
            Rumour.Describe(Avatar.For("Wizard") with { Race = "elf", Age = "Elder", Headwear = "Hood", Height = "Average", Build = "Average" }, "Wizard"));
        Assert.Equal("an assassin", Rumour.Describe(null, "Assassin"));
    }

    [Fact]
    public void The_same_story_counts_up_and_old_ones_are_forgotten()
    {
        var city = CityEconomy.Start(1);
        Rumour Dogs() => new() { Key = "beasts|Larkmere|Ashgrip", By = "Ashgrip", Who = Dwarf, Did = "killed somebody's dog round Larkmere", Many = "has killed {n} of folks' animals round Larkmere", X = 10, Z = 20, Bad = true };
        Assert.True(city.Apply(CityAction.Spread(Dogs())));
        Assert.True(city.Apply(CityAction.Spread(Dogs())));
        Assert.True(city.Apply(CityAction.Spread(Dogs())));
        var r = Assert.Single(city.Rumours);
        Assert.Equal(3, r.Count);
        Assert.Equal($"{Dwarf} has killed 3 of folks' animals round Larkmere", r.Telling);

        // Nothing silly kept, and a fortnight on, nobody remembers.
        Assert.False(city.Heard(new Rumour { Key = "x", Did = new string('a', 500) }));
        Assert.False(city.Heard(new Rumour { Key = "y", Did = "did a thing", X = double.NaN }));
        city.Day += Rumour.KeepDays + 1;
        city.Heard(new Rumour { Key = "fresh", Did = "a new story" });
        Assert.Equal("fresh", Assert.Single(city.Rumours).Key);
        for (var i = 0; i < Rumour.Most + 20; i++) city.Heard(new Rumour { Key = $"k{i}", Did = "chatter" });
        Assert.Equal(Rumour.Most, city.Rumours.Count);
    }

    [Fact]
    public void A_heros_word_reaches_every_game_through_the_server()
    {
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 1);
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        server.Handle("Joe", new CityTrade(CityAction.Spread(new Rumour { Key = "house|Joe", By = "Joe", Who = Dwarf, Did = "has started building a house", X = 5, Z = 6 })));
        var r = Assert.Single(server.City.Rumours);
        Assert.Equal("has started building a house", r.Did);
        Assert.Equal(server.City.Day, r.Day);
    }

    [Fact]
    public void A_deviants_doings_are_told_where_the_fight_was_with_the_hero_described()
    {
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 1);
        server.Connect(Hi("Joe"));
        server.Handle("Joe", new ReportBattle(new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, "Draugr", "Draugr Wight", 900, Killed: false, Escaped: false)], ["Joe"], KillerSlot: 0,
            Dropped: new Hoard { Crowns = 40 }, X: 120, Z: -40, Who: Dwarf)));
        var deviant = Assert.Single(server.World.Living);
        var told = server.City.Rumours.Where(r => r.Did.Contains(deviant.FullName)).ToList();
        Assert.NotEmpty(told);
        var cutDown = Assert.Single(told, r => r.By == "Joe");
        Assert.StartsWith($"{Dwarf} was cut down by a draugr wight", cutDown.Did);
        Assert.Contains("carried off", cutDown.Did);
        Assert.Contains($"calls itself {deviant.FullName}", cutDown.Did); // and so it rose
        Assert.Equal((120.0, -40.0), (cutDown.X, cutDown.Z));
        Assert.DoesNotContain(server.City.Rumours, r => r.Did.Contains("Joe") || r.Who?.Contains("Joe") == true);

        // Brought down later by someone else: told of too, them described.
        for (var i = 0; i < 40 && deviant.Alive; i++)
            server.Handle("Joe", new ReportBattle(new BattleReport("The Docks", BattleResult.Victory,
                [new EnemyReport(0, "Draugr", "Draugr Wight", 900, Killed: true, Escaped: false, DeviantId: deviant.Id, KillingBlow: DamageKind.Fire, KilledBy: "Joe")],
                ["Joe"], X: 130, Z: -40, Who: "a short elf man with the look of a ranger")));
        Assert.False(deviant.Alive);
        Assert.Contains(server.City.Rumours, r => r.Did == $"a short elf man with the look of a ranger killed {deviant.FullName}, a draugr wight");
    }
}
