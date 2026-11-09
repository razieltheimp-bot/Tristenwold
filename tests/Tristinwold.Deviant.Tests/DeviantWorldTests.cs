using Tristinwold.Deviant;

namespace Tristinwold.Deviant.Tests;

public class DeviantWorldTests
{
    static readonly string[] Players = ["Joe", "Mira"];

    static EnemyReport Enemy(int slot, string kind = "Wolf", string species = "Wolf", int maxHp = 100,
        bool killed = false, bool escaped = false, Guid? deviant = null, DamageKind? blow = null) =>
        new(slot, kind, species, maxHp, killed, escaped, deviant, blow, killed ? "Joe" : null);

    static BattleReport Report(BattleResult result, params EnemyReport[] enemies) =>
        new("The Warrens", result, enemies, Players, PartyLevel: 10);

    static Deviant BirthRingleader(DeviantWorld world, string kind = "Wolf")
    {
        world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, kind, kind, maxHp: 50), Enemy(1, kind, kind, maxHp: 300)));
        return world.Living.Last();
    }

    [Fact]
    public void A_world_saved_under_the_old_name_still_loads_with_everyone_in_it()
    {
        var world = new DeviantWorld(7);
        var ringleader = BirthRingleader(world);
        ringleader.Memory |= Memory.Returned;
        // Written as it was before the rename: the old list name, the old rank, an old memory among others.
        var old = world.ToJson().Replace("\"deviants\"", "\"nemeses\"").Replace("\"Ringleader\"", "\"Captain\"").Replace("Returned", "CheatedDeath");
        Assert.Contains("\"nemeses\"", old);
        Assert.Contains("\"Captain\"", old);
        Assert.Contains("CheatedDeath\"", old);
        var back = Assert.Single(DeviantWorld.FromJson(old).Living);
        Assert.Equal(ringleader.Id, back.Id);
        Assert.Equal(Rank.Ringleader, back.Rank);
        Assert.True(back.Memory.HasFlag(Memory.Returned) && back.Memory.HasFlag(Memory.KilledPlayer));

        // The server keeps the world as a string inside its campaign file, under the old field name: the file is
        // upgraded as it's read, and the world inside it as that's read in turn.
        var campaign = SaveUpgrade.Upgrade($$"""{"nemesisWorldJson":{{System.Text.Json.JsonSerializer.Serialize(old)}}}""");
        var inner = System.Text.Json.JsonDocument.Parse(campaign).RootElement.GetProperty("deviantWorldJson").GetString()!;
        Assert.Equal(ringleader.Id, Assert.Single(DeviantWorld.FromJson(inner).Living).Id);
    }

    [Fact]
    public void The_one_that_kills_a_hero_carries_off_what_they_dropped_and_gives_it_up_when_brought_down()
    {
        var world = new DeviantWorld(3);
        var dropped = new Hoard { Crowns = 40, Items = { ["iron_ore"] = 3, ["oak_log"] = 2 } };
        // The killing blow came from the smaller wolf: it rises, not the biggest, and it carries the hoard.
        world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, maxHp: 80), Enemy(1, maxHp: 300)) with { KillerSlot = 0, Dropped = dropped });
        var n = Assert.Single(world.Living);
        Assert.Equal(40, n.Carried.Crowns);
        Assert.Equal(3, n.Carried.Items["iron_ore"]);
        Assert.Empty(world.TakeRecovered());

        // It kills again: more on its back.
        world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, deviant: n.Id)) with { KillerSlot = 0, Dropped = new Hoard { Crowns = 10, Items = { ["iron_ore"] = 1 } } });
        Assert.Equal(50, n.Carried.Crowns);
        Assert.Equal(4, n.Carried.Items["iron_ore"]);

        // Saved and loaded, still carrying it.
        var back = DeviantWorld.FromJson(world.ToJson());
        Assert.Equal(50, back.Find(n.Id)!.Carried.Crowns);

        // Brought down for good: whoever did it gets it all back, and it carries nothing.
        world.RecordBattle(Report(BattleResult.Victory, Enemy(0, killed: true, deviant: n.Id, blow: DamageKind.Fire)));
        if (n.Alive) return; // it got up and ran (BeatingDeath): it keeps its hoard
        var r = Assert.Single(world.TakeRecovered());
        Assert.Equal("Joe", r.By);
        Assert.Equal(50, r.Hoard.Crowns);
        Assert.Equal(2, r.Hoard.Items["oak_log"]);
        Assert.True(n.Carried.Empty);
        Assert.Empty(world.TakeRecovered());
    }

    [Fact]
    public void Wiping_the_party_turns_the_toughest_survivor_into_a_captain()
    {
        var world = new DeviantWorld(1);
        var events = world.RecordBattle(Report(BattleResult.Defeat,
            Enemy(0, maxHp: 50, killed: true), Enemy(1, maxHp: 300), Enemy(2, maxHp: 120)));

        var n = Assert.Single(world.Living);
        Assert.Equal(Rank.Ringleader, n.Rank);
        Assert.True(n.Memory.HasFlag(Memory.KilledPlayer));
        Assert.All(Players, p => Assert.Equal(1, n.Players[p].TimesKilledThem));
        Assert.Equal(2, n.Traits.Count(t => t.Kind == TraitKind.Strength));
        Assert.Single(n.Traits, t => t.Kind == TraitKind.Weakness);
        Assert.NotNull(n.Epithet);
        Assert.Contains(events, e => e.Kind == WorldEventKind.Born && e.DeviantId == n.Id);
    }

    [Fact]
    public void Running_away_makes_a_grunt_and_a_clean_victory_makes_nobody()
    {
        var world = new DeviantWorld(2);
        world.RecordBattle(Report(BattleResult.PartyEscaped, Enemy(0)));
        Assert.Equal(Rank.Stray, Assert.Single(world.Living).Rank);

        world.RecordBattle(Report(BattleResult.Victory, Enemy(0, killed: true), Enemy(1, killed: true)));
        Assert.Single(world.Living);
    }

    [Fact]
    public void Traits_never_contradict_each_other()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            var world = new DeviantWorld(seed);
            var n = BirthRingleader(world);
            var elements = n.Traits.Where(t => t.Affinities is not null).SelectMany(t => t.Affinities!.Keys).ToList();
            Assert.Equal(elements.Count, elements.Distinct().Count());
        }
    }

    [Fact]
    public void Deviants_only_replace_monsters_of_their_own_species()
    {
        var world = new DeviantWorld(3, new DeviantRules { AppearanceChance = 1, HomeAreaAppearanceChance = 1 });
        var n = BirthRingleader(world, kind: "Wolf");

        var none = world.PlanEncounter("The Warrens", [new FormationSlot(0, "Bandit", "Bandit")], Players);
        Assert.Empty(none);

        var plan = world.PlanEncounter("The Warrens",
            [new FormationSlot(0, "Bandit", "Bandit"), new FormationSlot(2, "Wolf", "Wolf")], Players);
        var spawn = Assert.Single(plan);
        Assert.Equal(2, spawn.Slot);
        Assert.Equal(n.Id, spawn.DeviantId);
        Assert.True(spawn.Modifiers.StatMultipliers[Stat.Health] > 1.0);
        Assert.NotNull(spawn.Taunt);
        Assert.DoesNotContain("{player}", spawn.Taunt);
    }

    [Fact]
    public void Repeated_wins_promote_a_captain_to_warchief_and_then_overlord()
    {
        var world = new DeviantWorld(4);
        var n = BirthRingleader(world);
        var promotions = new List<WorldEvent>();
        for (var i = 0; i < 6 && n.Rank != Rank.Tyrant; i++)
            promotions.AddRange(world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, deviant: n.Id))));

        Assert.Equal(Rank.Tyrant, n.Rank);
        Assert.Contains(promotions, e => e.Kind == WorldEventKind.Promoted && e.Text.Contains("Scourge"));
        Assert.True(world.ModifiersFor(n).StatMultipliers[Stat.Health] > 3.0);
    }

    [Fact]
    public void A_deviant_that_is_beaten_is_dead_and_stays_dead()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            var world = new DeviantWorld(seed);
            var n = BirthRingleader(world);
            var events = world.RecordBattle(Report(BattleResult.Victory,
                Enemy(0, killed: true, deviant: n.Id, blow: DamageKind.Fire)));
            Assert.False(n.Alive);
            Assert.Contains(events, e => e.Kind == WorldEventKind.Slain);
            Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.Returned);
            Assert.Empty(world.PlanEncounter("The Warrens", [new FormationSlot(0, "Wolf", "Wolf")], Players));
        }
    }

    [Fact]
    public void One_cut_down_that_gets_up_and_runs_comes_back_scarred_and_resistant()
    {
        var world = new DeviantWorld(5);
        var n = BirthRingleader(world);
        n.Traits.RemoveAll(t => t.Affinities?.ContainsKey(DamageKind.Fire) == true);
        var events = world.RecordBattle(Report(BattleResult.EnemiesEscaped, Enemy(0, killed: false, deviant: n.Id, blow: DamageKind.Fire) with { Escaped = true }));
        Assert.True(n.Alive);
        Assert.Contains(events, e => e.Kind == WorldEventKind.Returned);
        Assert.Single(n.Scars, s => s.Cause == DamageKind.Fire);
        Assert.Equal(Affinity.Resist, world.ModifiersFor(n).Affinities[DamageKind.Fire]);
    }

    [Fact]
    public void Its_chance_of_beating_death_runs_from_three_in_ten_to_six_in_ten()
    {
        Assert.Equal(0.3, BeatingDeath.Chance(Rank.Stray), 3);
        Assert.Equal(0.6, BeatingDeath.Chance(Rank.Tyrant), 3);
    }

    [Fact]
    public void A_slain_deviants_pact_sworn_hunts_the_killer()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            var world = new DeviantWorld(seed);
            var a = BirthRingleader(world);
            var b = BirthRingleader(world, kind: "Bandit");
            a.PactSworn = b.Id;
            b.PactSworn = a.Id;
            world.RecordBattle(Report(BattleResult.Victory, Enemy(0, killed: true, deviant: a.Id)));
            if (a.Alive) continue;

            Assert.True(b.Memory.HasFlag(Memory.Vengeful));
            Assert.True(b.Players["Joe"].Memory.HasFlag(Memory.Vengeful));
            return;
        }
        Assert.Fail("no seed killed the deviant outright");
    }

    [Fact]
    public void Save_and_load_round_trips_the_whole_world_including_the_dice()
    {
        var world = new DeviantWorld(5, new DeviantRules { AppearanceChance = 0.5 });
        for (var i = 0; i < 5; i++) BirthRingleader(world, $"Kind{i}");
        world.AdvanceDay();

        var json = world.ToJson();
        var copy = DeviantWorld.FromJson(json, world.Rules);
        Assert.Equal(json, copy.ToJson());

        FormationSlot[] formation = [new(0, "Kind0", "a"), new(1, "Kind1", "b"), new(2, "Kind2", "c")];
        var p1 = world.PlanEncounter("The Warrens", formation, Players).Select(s => (s.Slot, s.DeviantId));
        var p2 = copy.PlanEncounter("The Warrens", formation, Players).Select(s => (s.Slot, s.DeviantId));
        Assert.Equal(p1, p2);
    }

    [Fact]
    public void Same_seed_and_same_battles_give_the_same_world()
    {
        // Multiplayer relies on this: every machine replaying the host's reports agrees.
        string Play()
        {
            var world = new DeviantWorld(11);
            for (var i = 0; i < 20; i++)
            {
                var n = BirthRingleader(world, $"Kind{i % 4}");
                world.RecordBattle(Report(BattleResult.Victory, Enemy(0, n.Kind, killed: true, deviant: n.Id)));
                world.AdvanceDay();
            }
            return world.ToJson();
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Hierarchy_stays_consistent_over_a_long_campaign()
    {
        var rules = new DeviantRules();
        var world = new DeviantWorld(6, rules);
        var rng = new Rng(99);
        for (var day = 0; day < 300; day++)
        {
            var monster = $"Kind{rng.Next(12)}";
            var living = world.Living.Where(n => n.Kind == monster).ToList();
            var deviant = living.Count > 0 && rng.Chance(0.5) ? living[0].Id : (Guid?)null;
            var result = (BattleResult)(1 + rng.Next(4));
            world.RecordBattle(Report(result,
                Enemy(0, monster, killed: result == BattleResult.Victory, escaped: result == BattleResult.EnemiesEscaped,
                    deviant: deviant, blow: (DamageKind)rng.Next(6))));
            world.AdvanceDay();

            Assert.True(world.Living.Count(n => n.Rank == Rank.Tyrant) <= 1);
            Assert.True(world.Scourges.Count() <= rules.MaxScourges);
            Assert.True(world.Strays.Count() <= rules.MaxStrays);
            foreach (var w in world.Scourges)
                Assert.True(world.RingleadersUnder(w.Id).Count() <= rules.MaxRingleadersPerScourge);
            Assert.All(world.Living, n => Assert.True(n.Level <= rules.MaxLevel));
            Assert.All(world.Living, n => Assert.DoesNotContain(n.Id, n.Rivals));
        }
        Assert.True(world.All.Count > 5);
    }

    [Fact]
    public void Modifiers_stay_inside_what_the_game_plays_well_with()
    {
        var mods = new BattleModifiers("X", new Dictionary<Stat, double> { [Stat.MagicResist] = 10, [Stat.Health] = 0, [Stat.Speed] = 9 },
            new Dictionary<DamageKind, Affinity>(), []);
        Assert.Equal(85, mods.Apply(Stat.MagicResist, 30));   // the resistance cap
        Assert.Equal(1, mods.Apply(Stat.Health, 5000));       // never kill it outright
        Assert.Equal(250, mods.Apply(Stat.Speed, 100));
        Assert.Equal(40, mods.Apply(Stat.SpellPower, 40));
    }

    [Fact]
    public void Display_names_stay_short()
    {
        var world = new DeviantWorld(7, new DeviantRules { MaxDisplayNameLength = 12 });
        for (var i = 0; i < 30; i++)
        {
            var n = BirthRingleader(world, $"Kind{i}");
            Assert.True(world.DisplayName(n).Length <= 12);
        }
    }

    [Fact]
    public void Modifiers_tell_the_game_a_deviant_rank_and_how_often_it_has_won()
    {
        var world = new DeviantWorld(9);
        world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, "dragon_red", "Red Dragon", maxHp: 600)));
        var dragon = world.Living.Single(n => n.Kind == "dragon_red");
        var mods = world.ModifiersFor(dragon);
        Assert.Equal(dragon.Rank, mods.Rank);
        Assert.Equal(1, mods.Victories);
    }
}
