using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit.Abstractions;

namespace Tristinwold.World.Tests;

public class WorldTests(ITestOutputHelper output)
{
    static readonly WorldGen World = WorldGen.Default;

    [Fact]
    public void Tristinwold_is_at_the_centre_and_four_neighbours_are_known_from_the_start()
    {
        Assert.Equal((0.0, 0.0), (World.Tristinwold.X, World.Tristinwold.Z));
        Assert.Equal(12, World.AuthoredCities.Count);
        var known = World.AuthoredCities.Where(c => c.StartsKnown).Select(c => c.Name).ToList();
        Assert.Equal(8, known.Count);
        Assert.Contains("Tristinwold", known);
        Assert.Equal(0, World.Height(0, 0), 3);
        Assert.True(World.Height(260, 0) < WorldGen.WaterLevel, "the harbor is under water");
    }

    [Fact]
    public void Past_the_first_five_the_world_keeps_making_cities_with_no_edge()
    {
        foreach (var (x, z) in new[] { (0.0, -12_000.0), (9_000.0, 4_000.0), (250_000.0, -1_000_000.0), (-3e6, 2e6) })
        {
            var city = World.NearestCity(x, z);
            Assert.False(city.Authored);
            Assert.True(city.DistanceTo(x, z) < 6000, $"a city within reach of ({x}, {z})");
            Assert.False(string.IsNullOrWhiteSpace(city.Name));
            Assert.NotEmpty(city.Places);
            Assert.True(double.IsFinite(World.Height(x, z)));
            Assert.Same(city, World.Find(city.Id));
        }
        // Nothing new crowds in among the first five.
        Assert.All(World.CitiesNear(0, 0, WorldGen.StartingRadius + 250), c => Assert.True(c.Authored));
    }

    [Fact]
    public void The_same_seed_makes_the_same_world_and_another_seed_a_different_one()
    {
        var again = new WorldGen();
        var other = new WorldGen(seed: 99);
        string Names(WorldGen w) => string.Join(",", w.SettlementsNear(-1500, -1500, 2000).Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(Names(World), Names(again));
        Assert.NotEqual(Names(World), Names(other));
        Assert.Equal(World.Height(1234, -5678), again.Height(1234, -5678));
    }

    [Fact]
    public void The_wilds_hold_villages_camps_and_lairs_but_none_inside_a_city()
    {
        var sites = World.SitesNear(-2000, -2000, 4000).ToList();
        output.WriteLine($"{sites.Count} sites within 4 km of (-2000,-2000)");
        foreach (var g in sites.GroupBy(s => s.Kind)) output.WriteLine($"  {g.Key}: {g.Count()} e.g. {g.First().Name}");
        Assert.True(sites.Count > 100);
        Assert.True(sites.Select(s => s.Kind).Distinct().Count() >= 9);
        foreach (var s in sites)
        {
            Assert.Equal(s.Hostile, s.Kind is SettlementKind.BanditCamp or SettlementKind.Barrow or SettlementKind.Den);
            Assert.True(s.Hostile ? s.People == 0 : s.People > 0);
            if (s.Lair is { } lair) Assert.Equal(lair.KindId, Bestiary.Get(lair.KindId).Id);
            Assert.DoesNotContain(World.CitiesNear(s.X, s.Z, 0), c => c.DistanceTo(s.X, s.Z) < c.Radius);
            Assert.Equal(s.Floor, World.Height(s.X, s.Z), 1); // the village's ground is levelled
        }
    }

    [Fact]
    public void It_gets_colder_going_north()
    {
        int Cold(double z) => Enumerable.Range(-10, 21).Count(i => World.BiomeAt(i * 300, z) is Biome.Snowfield or Biome.Tundra);
        Assert.True(Cold(-7000) > Cold(0) + 10, $"north {Cold(-7000)} vs home {Cold(0)}");
        Assert.Equal("The Tristinwold countryside", World.AreaAt(-150, -120));
        output.WriteLine(World.AreaAt(400, -4000));
    }

    [Fact]
    public void Everyone_has_a_name_a_past_and_interests_and_is_always_the_same_person()
    {
        var hamlet = World.SitesNear(-1000, -1000, 2500).First(s => s.Kind == SettlementKind.Hamlet);
        var people = World.PeopleOf(hamlet);
        Assert.Equal(hamlet.People, people.Count);
        Assert.Equal(people.Select(p => p.Name), new WorldGen().PeopleOf(new WorldGen().Find(hamlet.Id)!).Select(p => p.Name));
        Assert.Equal(people.Count, people.Select(p => p.Id).Distinct().Count());
        foreach (var p in people)
        {
            output.WriteLine($"{p.Name} ({p.Age}, {p.Occupation}, {p.Temper}): {p.Greeting}\n  {p.Backstory}\n  Likes {string.Join(", ", p.Interests)}");
            Assert.Contains(p.First, p.Backstory);
            Assert.DoesNotContain("{", p.Backstory + p.Greeting);
            Assert.InRange(p.Interests.Count, 2, 3);
            Assert.True(hamlet.DistanceTo(p.X, p.Z) <= hamlet.Radius);
        }
    }

    [Fact]
    public void Quests_point_at_real_places_and_people()
    {
        var quests = World.SettlementsNear(-1500, -1500, 3000).Where(s => !s.Hostile)
            .SelectMany(World.PeopleOf).Select(n => Quests.For(World, n)).OfType<Quest>().ToList();
        output.WriteLine($"{quests.Count} quests");
        foreach (var g in quests.GroupBy(q => q.Kind)) output.WriteLine($"  {g.Key}: {g.Count()}: {g.First().Title} / {g.First().Pitch}");
        Assert.Equal(4, quests.Select(q => q.Kind).Distinct().Count());
        foreach (var q in quests)
        {
            var target = World.Find(q.TargetId)!;
            Assert.Equal(q.TargetName, target.Name);
            Assert.True(q.Reward > 0);
            Assert.DoesNotContain("{", q.Pitch);
            switch (q.Kind)
            {
                case QuestKind.Slay: Assert.True(target.Hostile); Assert.Equal(target.Lair!.Total, q.Count); break;
                case QuestKind.Fetch: Assert.True(target.Hostile); Assert.NotNull(q.Item); break;
                case QuestKind.Rescue: Assert.True(target.Hostile); Assert.NotNull(q.PersonName); break;
                case QuestKind.Deliver when QuestLog.ForCouncil(q): Assert.True(TownCouncils.Keeps(target)); Assert.Equal($"{target.Id}/council", q.PersonId); break;
                case QuestKind.Deliver: Assert.Contains(World.PeopleOf(target), p => p.Id == q.PersonId); break;
            }
        }
    }

    [Fact]
    public void Debts_for_a_town_go_to_its_council_and_anyone_on_it_takes_them()
    {
        var log = new QuestLog();
        var debts = log.Accept(new Quest("q-debts", QuestKind.Deliver, "A purse of debts for Hollowheart", "p", "giver", "Asta", "home", "Rimeford", 0, 0,
            "city:hollowheart", "Hollowheart", 10, 10, 40, Item: "purse of debts", PersonId: "city:hollowheart/council", PersonName: "the council of Hollowheart"));
        Assert.Contains("any councillor", debts.Objective);
        Assert.Empty(log.TalkedTo("city:druitt/council0")); // not another town's
        Assert.Empty(log.TalkedTo("city:hollowheart/smith"));
        Assert.Equal(40, log.TalkedTo("city:hollowheart/council_clerk").Single().Reward);
        Assert.Equal(QuestStage.Done, debts.Stage);
    }

    [Fact]
    public void A_quest_log_follows_each_kind_of_quest_to_its_reward()
    {
        Quest Make(QuestKind kind) => new($"q-{kind}", kind, "t", "p", "giver", "Asta Frostmane", "home", "Rimeford", 0, 0,
            "site", "Wolfjaw Cave", 100, 100, 50, Count: 2, Item: "letter", PersonId: "person", PersonName: "Bram Frostmane");
        var log = new QuestLog();

        var slay = log.Accept(Make(QuestKind.Slay));
        Assert.Empty(log.Killed("elsewhere"));
        Assert.Empty(log.Killed("site"));
        Assert.Single(log.Killed("site"));
        Assert.Equal(QuestStage.Return, slay.Stage);
        Assert.Equal((0.0, 0.0), slay.Goal);

        var fetch = log.Accept(Make(QuestKind.Fetch));
        Assert.NotNull(log.PickedUp(fetch.Quest.Id));
        var rescue = log.Accept(Make(QuestKind.Rescue));
        Assert.Contains(log.TalkedTo("person"), u => u.Quest.Kind == QuestKind.Rescue);
        Assert.Equal(QuestStage.Return, rescue.Stage);
        var deliver = log.Accept(Make(QuestKind.Deliver));
        var paid = log.TalkedTo("person").ToList();
        Assert.Equal(50, paid.Single().Reward);
        Assert.Equal(QuestStage.Done, deliver.Stage);

        var back = log.TalkedTo("giver").ToList();
        Assert.Equal(3, back.Count);
        Assert.All(back, u => Assert.Equal(50, u.Reward));
        Assert.Empty(log.Open);
    }

    [Fact]
    public void Fog_of_war_lifts_where_you_walk_and_is_saved()
    {
        var map = new ExploredMap();
        Assert.False(map.IsRevealed(0, -3000));
        for (var z = 0.0; z > -3000; z -= 20) map.Reveal(0, z, 80);
        Assert.True(map.IsRevealed(0, -2990));
        Assert.True(map.IsRevealed(-70, -1500));
        Assert.False(map.IsRevealed(-200, -1500));
        Assert.Empty(map.Reveal(0, -1500, 50)); // already seen
        map.Discovered.Add("city:cinderhelm");

        var log = new QuestLog();
        log.Accept(new Quest("q", QuestKind.Slay, "t", "p", "g", "G", "h", "H", 0, 0, "s", "S", 1, 1, 10, Count: 1));
        var json = CompanySave.Capture(World.Seed, 42, map, log).ToJson();
        var (map2, log2) = (new ExploredMap(), new QuestLog());
        var save = CompanySave.FromJson(json);
        save.Restore(map2, log2);
        Assert.Equal(map.Count, map2.Count);
        Assert.True(map2.IsRevealed(0, -2990));
        Assert.Contains("city:cinderhelm", map2.Discovered);
        Assert.Equal(42, save.Crowns);
        Assert.Equal(QuestKind.Slay, log2.Entries.Single().Quest.Kind);
    }

    [Fact]
    public void Enemies_grow_tougher_further_from_home()
    {
        var wolf = Bestiary.Get("wolf");
        Assert.Equal(wolf.Health, wolf.NewCombatant().MaxHealth);
        Assert.True(wolf.NewCombatant(WorldGen.Danger(0, -8000)).MaxHealth > wolf.Health * 2);
    }

    [Fact]
    public void Home_country_is_level_one_out_to_the_nearest_towns_and_it_climbs_every_way_from_there()
    {
        var w = new WorldGen();
        var first = w.SettlementsNear(0, 0, 1500).Where(s => s.Kind is SettlementKind.Town && !Northwood.Inside(s.X, s.Z)).MinBy(s => s.DistanceTo(0, 0))!;
        Assert.Equal(1, WorldGen.LevelAt(first.X, first.Z), 1); // the nearest town is still home country
        Assert.Equal(1, WorldGen.Danger(300, 300));
        foreach (var (dx, dz) in new[] { (1.0, 0.0), (0.0, 1.0), (-0.7, 0.7), (0.6, 0.8) })
        {
            var (near, far) = (WorldGen.LevelAt(dx * 1500, dz * 1500), WorldGen.LevelAt(dx * 4000, dz * 4000));
            if (Northwood.Inside(dx * 1500, dz * 1500)) continue;
            Assert.True(near > 2 && far > near + 10, $"({dx},{dz}): level {near:0.0} at 1.5 km, {far:0.0} at 4 km");
        }
        Assert.True(WorldGen.Danger(4000, 0) > WorldGen.Danger(2000, 0));
    }
}
