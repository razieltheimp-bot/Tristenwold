using System.Numerics;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay.Tests;

public class CombatTests
{
    static BattleModifiers Mods(Dictionary<Stat, double>? stats = null, Dictionary<DamageKind, Affinity>? aff = null, AutoStatus[]? statuses = null) =>
        new("Grask the Brutal", stats ?? [], aff ?? [], statuses ?? []);

    [Fact]
    public void Armor_softens_blows_and_never_stops_them_all()
    {
        var bare = new Combatant("Bandit", 100);
        var plated = new Combatant("Bandit", 100, armor: 5000);
        Assert.Equal(20, bare.TakeHit(20, DamageKind.Physical, AttackKind.Weapon).Dealt);
        Assert.Equal(20 * (1 - Combatant.ArmorCap), plated.TakeHit(20, DamageKind.Physical, AttackKind.Weapon).Dealt, 6);
    }

    [Fact]
    public void Final_fantasy_statuses_change_the_fight()
    {
        var target = new Combatant("Captain", 200);
        target.Apply(Mods(statuses: [AutoStatus.Protect, AutoStatus.Shell, AutoStatus.Reflect]));
        var player = new Combatant("Joe", 100);

        Assert.Equal(10, target.TakeHit(20, DamageKind.Physical, AttackKind.Weapon, attacker: player).Dealt); // Protect
        Assert.Equal(10, target.TakeHit(20, DamageKind.Fire, AttackKind.Spell, attacker: player).Dealt);      // Shell
        Assert.Equal(100 - 2 * 3, player.Health, 6);                                                          // Reflect sends 30% back

        var regen = new Combatant("Troll", 100);
        regen.Apply(Mods(statuses: [AutoStatus.Regen]));
        regen.TakeHit(50, DamageKind.Physical, AttackKind.Weapon);
        regen.Tick(10, inCombat: true);
        Assert.True(regen.Health > 50);
    }

    [Fact]
    public void A_fire_eater_heals_from_fire_and_a_frost_weak_one_takes_extra()
    {
        var n = new Combatant("Deviant", 100);
        n.Apply(Mods(aff: new() { [DamageKind.Fire] = Affinity.Absorb, [DamageKind.Frost] = Affinity.Weak }));
        n.TakeHit(40, DamageKind.Physical, AttackKind.Weapon);
        var fire = n.TakeHit(30, DamageKind.Fire, AttackKind.Spell);
        Assert.Equal(0, fire.Dealt);
        Assert.Equal(30, fire.Healed);
        Assert.Equal(15, n.TakeHit(10, DamageKind.Frost, AttackKind.Spell).Dealt);
    }

    [Fact]
    public void Deviant_modifiers_raise_its_pools_and_name()
    {
        var c = Bestiary.Get("dock_thug").NewCombatant();
        c.Apply(Mods(stats: new() { [Stat.Health] = 1.6, [Stat.Damage] = 1.35, [Stat.Speed] = 1.25 }, statuses: [AutoStatus.Haste]));
        Assert.Equal("Grask the Brutal", c.Name);
        Assert.Equal(176, c.MaxHealth);
        Assert.Equal(c.MaxHealth, c.Health);
        Assert.Equal(1.35, c.DamageMult, 6);
        Assert.Equal(1.25 * 1.2, c.SpeedMult, 6);
    }

    [Fact]
    public void Taking_a_beating_fills_the_limit_gauge()
    {
        var joe = new Combatant("Joe", 200);
        var thug = new Combatant("Thug", 100);
        for (var i = 0; i < 6 && !joe.LimitReady; i++) joe.TakeHit(25, DamageKind.Physical, AttackKind.Weapon, attacker: thug);
        Assert.True(joe.LimitReady);
        Assert.True(thug.Limit > 0); // dealing damage fills it a little too
        Assert.True(joe.UseLimit());
        Assert.Equal(0, joe.Limit);
        Assert.False(joe.UseLimit());
    }

    [Fact]
    public void Swings_cost_stamina_and_spells_cost_mana()
    {
        var joe = new Combatant("Joe", 100, mana: 30, stamina: 20);
        Assert.True(joe.Spend(AttackKind.Weapon, 15));
        Assert.False(joe.Spend(AttackKind.Weapon, 15));
        Assert.True(joe.Spend(AttackKind.Spell, 30));
        Assert.False(joe.Spend(AttackKind.Spell, 1));
        joe.Tick(5, inCombat: true);
        Assert.True(joe.Stamina > 5);
    }
}

public class EncounterTests
{
    [Fact]
    public void A_real_time_fight_is_gathered_announced_and_reported()
    {
        var starts = new List<IReadOnlyList<FormationSlot>>();
        var ends = new List<BattleReport>();
        var t = new EncounterTracker((_, s) => starts.Add(s), ends.Add, "Joe", quietSeconds: 5, gatherSeconds: 1);

        t.Engaged(10, "dock_thug", "Dock Thug", 110, new Vector3(10, 0, 0), "The Docks", Vector3.Zero, 0);
        t.Engaged(11, "dock_thug", "Dock Thug", 110, new Vector3(12, 0, 0), "The Docks", Vector3.Zero, 0.3);
        t.Tick(0.5, Vector3.Zero, _ => true, _ => null);
        Assert.Empty(starts);
        t.Tick(1.1, Vector3.Zero, _ => true, _ => null);
        Assert.Equal([0, 1], starts.Single().Select(s => s.Slot));

        var deviant = Guid.NewGuid();
        t.MarkDeviant(1, deviant);
        t.Hit(11, DamageKind.Fire, "Joe", 2);
        t.Died(10, "Joe", 2);
        t.Died(11, null, 2.1);
        t.Tick(2.2, Vector3.Zero, _ => false, _ => null);

        var report = ends.Single();
        Assert.Equal(BattleResult.Victory, report.Result);
        Assert.Equal("The Docks", report.AreaId);
        Assert.Equal(deviant, report.Enemies[1].DeviantId);
        Assert.Equal(DamageKind.Fire, report.Enemies[1].KillingBlow);
        Assert.Equal("Joe", report.Enemies[1].KilledBy);
        Assert.Equal("dock_thug", report.Enemies[1].BaseForm); // so Vann can send one hunting
        Assert.False(t.InFight);
    }

    [Fact]
    public void Running_away_and_going_down_are_told_apart()
    {
        var ends = new List<BattleReport>();
        var t = new EncounterTracker((_, _) => { }, ends.Add, "Joe", quietSeconds: 2, gatherSeconds: 1);
        t.Engaged(1, "draugr", "Draugr", 160, new Vector3(5, 0, 0), "The Warrens", Vector3.Zero, 0);
        t.Tick(1, new Vector3(40, 0, 0), _ => false, _ => new Vector3(6, 0, 0));
        t.Tick(4, new Vector3(60, 0, 0), _ => false, _ => new Vector3(6, 0, 0));
        Assert.Equal(BattleResult.PartyEscaped, ends[0].Result);

        t.Engaged(2, "draugr", "Draugr", 160, new Vector3(5, 0, 0), "The Warrens", Vector3.Zero, 10);
        t.Engaged(3, "draugr", "Draugr", 160, new Vector3(-5, 0, 0), "The Warrens", Vector3.Zero, 10);
        t.Downed(by: 3);
        t.Tick(10.1, Vector3.Zero, _ => true, _ => null);
        Assert.Equal(BattleResult.Defeat, ends[1].Result);
        // Only the one that struck twice running put them down.
        Assert.Equal([false, true], ends[1].Enemies.Select(e => e.PutDown == true));
        Assert.All(ends[1].Enemies, e => Assert.NotNull(e.PutDown));
    }
}
