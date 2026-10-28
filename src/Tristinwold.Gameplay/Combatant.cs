using Tristinwold.Nemesis;

namespace Tristinwold.Gameplay;

public enum AttackKind { Weapon, Spell }

/// <summary>What one hit did, for floating numbers, sounds and the fight log.</summary>
public sealed record HitResult(double Dealt, double Healed, double Reflected, bool Killed, bool Critical, bool LimitReady);

/// <summary>
/// Health, mana, stamina, armor and the auto-statuses (Haste, Protect, Shell, Regen, Reflect) for anything that fights:
/// the player, companions, enemies and nemeses. Real-time numbers; no engine code,
/// so the same rules run in the game and in tests.
/// </summary>
public sealed class Combatant
{
    public const double ArmorCap = 0.80;          // armor never stops more than 80% of a blow
    public const double ArmorPerPercent = 1 / 0.12; // 8.33 armor rating per percent of harm turned aside
    public const double ResistCap = 85;

    public string Name { get; set; }
    public double MaxHealth { get; private set; }
    public double Health { get; private set; }
    public double MaxMana { get; private set; }
    public double Mana { get; private set; }
    public double MaxStamina { get; private set; }
    public double Stamina { get; private set; }

    public double Armor { get; set; }
    public double MagicResist { get; set; }
    public double DamageMult { get; set; } = 1;
    public double SpellPower { get; set; } = 1;
    public double SpeedMult { get; set; } = 1;
    public double CritChance { get; set; } = 5;

    public Dictionary<DamageKind, double> Resist { get; } = [];
    public HashSet<DamageKind> Absorbs { get; } = [];
    public HashSet<AutoStatus> Statuses { get; } = [];

    /// <summary>The limit gauge, 0-100. Fills when hurt (fast) and when hurting (slowly).</summary>
    public double Limit { get; private set; }
    public bool LimitReady => Limit >= 100;

    public bool Dead => Health <= 0;

    public Combatant(string name, double health, double mana = 50, double stamina = 100, double armor = 0)
    {
        Name = name;
        MaxHealth = Health = health;
        MaxMana = Mana = mana;
        MaxStamina = Stamina = stamina;
        Armor = armor;
    }

    /// <summary>Swings and spells cost stamina or mana; returns false if there isn't enough.</summary>
    public bool Spend(AttackKind kind, double cost)
    {
        if (kind == AttackKind.Weapon) { if (Stamina < cost) return false; Stamina -= cost; }
        else { if (Mana < cost) return false; Mana -= cost; }
        return true;
    }

    /// <summary>How hard this combatant hits with a base damage, before the target's defenses.</summary>
    public double Outgoing(AttackKind kind, double baseDamage, Rng rng, out bool critical)
    {
        critical = kind == AttackKind.Weapon && rng.Chance(CritChance / 100);
        var damage = baseDamage * (kind == AttackKind.Weapon ? DamageMult : SpellPower);
        if (Statuses.Contains(AutoStatus.Haste)) damage *= 1.1; // quicker blows land harder
        return critical ? damage * 2 : damage;
    }

    /// <summary>
    /// Takes a hit. Order: absorb (heals instead), elemental resistance, armor or magic
    /// resistance, Protect/Shell (halve), then Reflect sends part of it back.
    /// </summary>
    public HitResult TakeHit(double damage, DamageKind kind, AttackKind attack, bool critical = false, Combatant? attacker = null)
    {
        if (Dead) return new HitResult(0, 0, 0, false, critical, LimitReady);

        if (kind != DamageKind.Physical && Absorbs.Contains(kind))
        {
            var healed = Math.Min(MaxHealth - Health, damage);
            Health += healed;
            return new HitResult(0, healed, 0, false, critical, LimitReady);
        }

        var dealt = damage;
        if (Resist.TryGetValue(kind, out var resist)) dealt *= 1 - Math.Clamp(resist, -100, 100) / 100;
        if (attack == AttackKind.Weapon)
        {
            dealt *= 1 - Math.Min(ArmorCap, Armor / ArmorPerPercent / 100);
            if (Statuses.Contains(AutoStatus.Protect)) dealt *= 0.5;
        }
        else
        {
            dealt *= 1 - Math.Clamp(MagicResist, -100, ResistCap) / 100;
            if (Statuses.Contains(AutoStatus.Shell)) dealt *= 0.5;
        }
        dealt = Math.Max(0, dealt);

        double reflected = 0;
        if (Statuses.Contains(AutoStatus.Reflect) && attacker is not null && attacker != this)
        {
            reflected = dealt * 0.3;
            attacker.Health = Math.Max(0, attacker.Health - reflected);
        }

        Health = Math.Max(0, Health - dealt);
        Limit = Math.Min(100, Limit + dealt / MaxHealth * 150);
        attacker?.GainLimit(dealt / Math.Max(1, attacker.MaxHealth) * 25);
        return new HitResult(dealt, 0, reflected, Dead, critical, LimitReady);
    }

    public void GainLimit(double amount) => Limit = Math.Min(100, Limit + amount);

    /// <summary>Unleashes the limit break: empties the gauge. The move itself is up to the game.</summary>
    public bool UseLimit()
    {
        if (!LimitReady) return false;
        Limit = 0;
        return true;
    }

    public void Heal(double amount) => Health = Math.Min(MaxHealth, Health + amount);

    /// <summary>Gives back mana and stamina (a warlock's kill, a sorcerer's surge).</summary>
    public void Restore(double mana, double stamina = 0)
    {
        Mana = Math.Min(MaxMana, Mana + mana);
        Stamina = Math.Min(MaxStamina, Stamina + stamina);
    }

    /// <summary>New maximums (a level gained, a class joined), keeping the same share of each pool.</summary>
    public void SetPools(double health, double mana, double stamina)
    {
        Health = health * (MaxHealth > 0 ? Health / MaxHealth : 1);
        Mana = mana * (MaxMana > 0 ? Mana / MaxMana : 1);
        Stamina = stamina * (MaxStamina > 0 ? Stamina / MaxStamina : 1);
        (MaxHealth, MaxMana, MaxStamina) = (health, mana, stamina);
    }

    /// <summary>Takes harm that nothing resists (starving, a fall).</summary>
    public void Lose(double amount) => Health = Math.Max(0, Health - amount);

    /// <summary>Grows tougher by a factor, keeping the same share of its health (a dragon growing a size).</summary>
    public void Grow(double factor)
    {
        MaxHealth = Math.Round(MaxHealth * factor);
        Health = Math.Min(MaxHealth, Math.Round(Health * factor));
    }

    /// <summary>Per-frame regeneration: stamina fast, mana slower, health only with Regen.</summary>
    public void Tick(double seconds, bool inCombat)
    {
        if (Dead) return;
        Stamina = Math.Min(MaxStamina, Stamina + MaxStamina * 0.10 * seconds);
        Mana = Math.Min(MaxMana, Mana + MaxMana * (inCombat ? 0.01 : 0.03) * seconds);
        var regen = Statuses.Contains(AutoStatus.Regen) ? 0.03 : inCombat ? 0 : 0.007;
        Health = Math.Min(MaxHealth, Health + MaxHealth * regen * seconds);
    }

    /// <summary>
    /// Turns this into a nemesis: the server's modifiers raise its pools and stats, set its
    /// elemental affinities and give it its statuses. It starts the fight at full strength.
    /// </summary>
    public void Apply(BattleModifiers mods)
    {
        Name = mods.DisplayName;
        MaxHealth = Health = mods.Apply(Stat.Health, MaxHealth);
        MaxMana = Mana = mods.Apply(Stat.Mana, MaxMana);
        MaxStamina = Stamina = mods.Apply(Stat.Stamina, MaxStamina);
        Armor = mods.Apply(Stat.Armor, Armor == 0 && Mult(mods, Stat.Armor) > 1 ? 50 : Armor);
        MagicResist = mods.Apply(Stat.MagicResist, MagicResist == 0 && Mult(mods, Stat.MagicResist) > 1 ? 20 : MagicResist);
        DamageMult = mods.Apply(Stat.Damage, DamageMult);
        SpellPower = mods.Apply(Stat.SpellPower, SpellPower);
        SpeedMult = mods.Apply(Stat.Speed, SpeedMult * 100) / 100;
        CritChance = mods.Apply(Stat.CritChance, CritChance);

        foreach (var (kind, affinity) in mods.Affinities)
        {
            Absorbs.Remove(kind);
            switch (affinity)
            {
                case Affinity.Weak: Resist[kind] = -50; break;
                case Affinity.Resist: Resist[kind] = 50; break;
                case Affinity.Immune: Resist[kind] = 100; break;
                case Affinity.Absorb: Resist[kind] = 100; Absorbs.Add(kind); break;
                default: Resist.Remove(kind); break;
            }
        }
        foreach (var status in mods.AutoStatuses) Statuses.Add(status);
        if (Statuses.Contains(AutoStatus.Haste)) SpeedMult = Math.Min(2.5, SpeedMult * 1.2);
    }

    static double Mult(BattleModifiers mods, Stat stat) => mods.StatMultipliers.TryGetValue(stat, out var m) ? m : 1;
}
