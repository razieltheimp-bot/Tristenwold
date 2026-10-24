using Tristinwold.Nemesis;

namespace Tristinwold.Nemesis.Tests;

public class NemesisWorldTests
{
    static readonly string[] Players = ["Joe", "Mira"];

    static EnemyReport Enemy(int slot, int monsterId = 0x1010, string species = "Dingo", int maxHp = 100,
        bool killed = false, bool escaped = false, Guid? nemesis = null, DamageKind? blow = null) =>
        new(slot, monsterId, species, maxHp, killed, escaped, nemesis, blow, killed ? "Joe" : null);

    static BattleReport Report(BattleResult result, params EnemyReport[] enemies) =>
        new("besaid", result, enemies, Players, PartyLevel: 10);

    static Nemesis BirthCaptain(NemesisWorld world, int monsterId = 0x1010)
    {
        world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, monsterId, maxHp: 50), Enemy(1, monsterId, maxHp: 300)));
        return world.Living.Last();
    }

    [Fact]
    public void Wiping_the_party_turns_the_toughest_survivor_into_a_captain()
    {
        var world = new NemesisWorld(1);
        var events = world.RecordBattle(Report(BattleResult.Defeat,
            Enemy(0, maxHp: 50, killed: true), Enemy(1, maxHp: 300), Enemy(2, maxHp: 120)));

        var n = Assert.Single(world.Living);
        Assert.Equal(Rank.Captain, n.Rank);
        Assert.True(n.Memory.HasFlag(Memory.KilledPlayer));
        Assert.All(Players, p => Assert.Equal(1, n.Players[p].TimesKilledThem));
        Assert.Equal(2, n.Traits.Count(t => t.Kind == TraitKind.Strength));
        Assert.Single(n.Traits, t => t.Kind == TraitKind.Weakness);
        Assert.NotNull(n.Epithet);
        Assert.Contains(events, e => e.Kind == WorldEventKind.Born && e.NemesisId == n.Id);
    }

    [Fact]
    public void Running_away_makes_a_grunt_and_a_clean_victory_makes_nobody()
    {
        var world = new NemesisWorld(2);
        world.RecordBattle(Report(BattleResult.PartyEscaped, Enemy(0)));
        Assert.Equal(Rank.Grunt, Assert.Single(world.Living).Rank);

        world.RecordBattle(Report(BattleResult.Victory, Enemy(0, killed: true), Enemy(1, killed: true)));
        Assert.Single(world.Living);
    }

    [Fact]
    public void Traits_never_contradict_each_other()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            var world = new NemesisWorld(seed);
            var n = BirthCaptain(world);
            var elements = n.Traits.Where(t => t.Affinities is not null).SelectMany(t => t.Affinities!.Keys).ToList();
            Assert.Equal(elements.Count, elements.Distinct().Count());
        }
    }

    [Fact]
    public void Nemeses_only_replace_monsters_of_their_own_species()
    {
        var world = new NemesisWorld(3, new NemesisRules { AppearanceChance = 1, HomeAreaAppearanceChance = 1 });
        var n = BirthCaptain(world, monsterId: 0x1010);

        var none = world.PlanEncounter("besaid", [new FormationSlot(0, 0x1020, "Condor")], Players);
        Assert.Empty(none);

        var plan = world.PlanEncounter("besaid",
            [new FormationSlot(0, 0x1020, "Condor"), new FormationSlot(2, 0x1010, "Dingo")], Players);
        var spawn = Assert.Single(plan);
        Assert.Equal(2, spawn.Slot);
        Assert.Equal(n.Id, spawn.NemesisId);
        Assert.True(spawn.Modifiers.StatMultipliers[Stat.Hp] > 1.0);
        Assert.NotNull(spawn.Taunt);
        Assert.DoesNotContain("{player}", spawn.Taunt);
    }

    [Fact]
    public void Repeated_wins_promote_a_captain_to_warchief_and_then_overlord()
    {
        var world = new NemesisWorld(4);
        var n = BirthCaptain(world);
        var promotions = new List<WorldEvent>();
        for (var i = 0; i < 6 && n.Rank != Rank.Overlord; i++)
            promotions.AddRange(world.RecordBattle(Report(BattleResult.Defeat, Enemy(0, nemesis: n.Id))));

        Assert.Equal(Rank.Overlord, n.Rank);
        Assert.Contains(promotions, e => e.Kind == WorldEventKind.Promoted && e.Text.Contains("Warchief"));
        Assert.True(world.ModifiersFor(n).StatMultipliers[Stat.Hp] > 3.0);
    }

    [Fact]
    public void Killing_a_nemesis_either_ends_it_or_leaves_a_scar_that_teaches_resistance()
    {
        var sawDeath = false;
        var sawScar = false;
        for (ulong seed = 0; seed < 100 && !(sawDeath && sawScar); seed++)
        {
            var world = new NemesisWorld(seed);
            var n = BirthCaptain(world);
            n.Traits.RemoveAll(t => t.Affinities?.ContainsKey(DamageKind.Fire) == true);
            var events = world.RecordBattle(Report(BattleResult.Victory,
                Enemy(0, killed: true, nemesis: n.Id, blow: DamageKind.Fire)));

            if (!n.Alive)
            {
                sawDeath = true;
                Assert.Contains(events, e => e.Kind == WorldEventKind.Slain);
                Assert.Empty(world.PlanEncounter("besaid", [new FormationSlot(0, 0x1010, "Dingo")], Players));
            }
            else
            {
                sawScar = true;
                Assert.Single(n.Scars, s => s.Cause == DamageKind.Fire);
                Assert.Equal(Affinity.Resist, world.ModifiersFor(n).Affinities[DamageKind.Fire]);
                Assert.True(n.Memory.HasFlag(Memory.CheatedDeath));
            }
        }
        Assert.True(sawDeath && sawScar);
    }

    [Fact]
    public void A_slain_nemesis_blood_brother_hunts_the_killer()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            var world = new NemesisWorld(seed);
            var a = BirthCaptain(world);
            var b = BirthCaptain(world, monsterId: 0x1020);
            a.BloodBrother = b.Id;
            b.BloodBrother = a.Id;
            world.RecordBattle(Report(BattleResult.Victory, Enemy(0, killed: true, nemesis: a.Id)));
            if (a.Alive) continue;

            Assert.True(b.Memory.HasFlag(Memory.Avenger));
            Assert.True(b.Players["Joe"].Memory.HasFlag(Memory.Avenger));
            return;
        }
        Assert.Fail("no seed killed the nemesis outright");
    }

    [Fact]
    public void Save_and_load_round_trips_the_whole_world_including_the_dice()
    {
        var world = new NemesisWorld(5, new NemesisRules { AppearanceChance = 0.5 });
        for (var i = 0; i < 5; i++) BirthCaptain(world, 0x1010 + i);
        world.AdvanceDay();

        var json = world.ToJson();
        var copy = NemesisWorld.FromJson(json, world.Rules);
        Assert.Equal(json, copy.ToJson());

        FormationSlot[] formation = [new(0, 0x1010, "a"), new(1, 0x1011, "b"), new(2, 0x1012, "c")];
        var p1 = world.PlanEncounter("besaid", formation, Players).Select(s => (s.Slot, s.NemesisId));
        var p2 = copy.PlanEncounter("besaid", formation, Players).Select(s => (s.Slot, s.NemesisId));
        Assert.Equal(p1, p2);
    }

    [Fact]
    public void Same_seed_and_same_battles_give_the_same_world()
    {
        // Multiplayer relies on this: every machine replaying the host's reports agrees.
        string Play()
        {
            var world = new NemesisWorld(11);
            for (var i = 0; i < 20; i++)
            {
                var n = BirthCaptain(world, 0x1000 + i % 4);
                world.RecordBattle(Report(BattleResult.Victory, Enemy(0, n.MonsterId, killed: true, nemesis: n.Id)));
                world.AdvanceDay();
            }
            return world.ToJson();
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Hierarchy_stays_consistent_over_a_long_campaign()
    {
        var rules = new NemesisRules();
        var world = new NemesisWorld(6, rules);
        var rng = new Rng(99);
        for (var day = 0; day < 300; day++)
        {
            var monster = 0x1000 + rng.Next(12);
            var living = world.Living.Where(n => n.MonsterId == monster).ToList();
            var nemesis = living.Count > 0 && rng.Chance(0.5) ? living[0].Id : (Guid?)null;
            var result = (BattleResult)(1 + rng.Next(4));
            world.RecordBattle(Report(result,
                Enemy(0, monster, killed: result == BattleResult.Victory, escaped: result == BattleResult.EnemiesEscaped,
                    nemesis: nemesis, blow: (DamageKind)rng.Next(8))));
            world.AdvanceDay();

            Assert.True(world.Living.Count(n => n.Rank == Rank.Overlord) <= 1);
            Assert.True(world.Warchiefs.Count() <= rules.MaxWarchiefs);
            Assert.True(world.Grunts.Count() <= rules.MaxGrunts);
            foreach (var w in world.Warchiefs)
                Assert.True(world.CaptainsUnder(w.Id).Count() <= rules.MaxCaptainsPerWarchief);
            Assert.All(world.Living, n => Assert.True(n.Level <= rules.MaxLevel));
            Assert.All(world.Living, n => Assert.DoesNotContain(n.Id, n.Rivals));
        }
        Assert.True(world.All.Count > 5);
    }

    [Fact]
    public void Modifiers_clamp_to_what_ffx_can_store()
    {
        var mods = new BattleModifiers("X", new Dictionary<Stat, double> { [Stat.Strength] = 10, [Stat.Hp] = 0 },
            new Dictionary<DamageKind, Affinity>(), []);
        Assert.Equal(255, mods.Apply(Stat.Strength, 200));
        Assert.Equal(1, mods.Apply(Stat.Hp, 5000));
        Assert.Equal(40, mods.Apply(Stat.Magic, 40));
    }

    [Fact]
    public void Display_names_fit_the_game_name_buffer()
    {
        var world = new NemesisWorld(7, new NemesisRules { MaxDisplayNameLength = 12 });
        for (var i = 0; i < 30; i++)
        {
            var n = BirthCaptain(world, 0x1000 + i);
            Assert.True(world.DisplayName(n).Length <= 12);
        }
    }
}
