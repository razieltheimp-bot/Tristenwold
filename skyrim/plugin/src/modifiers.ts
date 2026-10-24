// Turns a nemesis's modifiers into Skyrim actor-value changes, including the Final Fantasy
// statuses, without needing any plugin (.esp) records: everything is an actor value.
import type { AutoStatus, BattleModifiers, DamageKind, Stat } from "./protocol.ts";

/** The slice of an Actor this needs, so it can be tested without the game. */
export interface ActorValues {
  getBaseActorValue(name: string): number;
  getActorValue(name: string): number;
  setActorValue(name: string, value: number): void;
  restoreActorValue(name: string, amount: number): void;
}

const STAT_AV: Record<Stat, string> = {
  Health: "Health",
  Magicka: "Magicka",
  Stamina: "Stamina",
  Damage: "AttackDamageMult",
  Armor: "DamageResist",
  MagicResist: "MagicResist",
  Speed: "SpeedMult",
  SpellPower: "DestructionPowerModifier",
  CritChance: "CritChance",
};

export const RESIST_AV: Partial<Record<DamageKind, string>> = {
  Fire: "FireResist",
  Frost: "FrostResist",
  Shock: "ElectricResist",
  Poison: "PoisonResist",
};

/** The AV a damage kind is resisted by, the other way round, for working out what hit something. */
export function damageKindForResistance(resistAv: string): DamageKind {
  switch (resistAv) {
    case "FireResist": return "Fire";
    case "FrostResist": return "Frost";
    case "ElectricResist": return "Shock";
    case "PoisonResist": return "Poison";
    default: return "Physical";
  }
}

// What the Final Fantasy statuses become in Skyrim.
const STATUS_EFFECTS: Record<AutoStatus, Array<{ av: string; add?: number; mult?: number; max?: number }>> = {
  Haste: [{ av: "SpeedMult", mult: 1.2, max: 250 }, { av: "WeaponSpeedMult", add: 0.3 }],
  Protect: [{ av: "DamageResist", add: 150, max: 1000 }],
  Shell: [{ av: "MagicResist", add: 40, max: 85 }],
  Regen: [{ av: "HealRate", add: 3 }, { av: "CombatHealthRegenMult", add: 1 }],
  Reflect: [{ av: "ReflectDamage", add: 30, max: 100 }], // Skyrim reflects melee damage, not spells
};

export interface Change { av: string; from: number; to: number }

/**
 * Works out every actor-value change for a nemesis and applies it. Pools (health, magicka,
 * stamina) are raised from their base and refilled, so it starts the fight at full strength.
 */
export function applyModifiers(actor: ActorValues, mods: BattleModifiers): Change[] {
  const changes: Change[] = [];
  const set = (av: string, to: number) => {
    const from = actor.getActorValue(av);
    actor.setActorValue(av, to);
    changes.push({ av, from, to });
  };

  for (const [stat, mult] of Object.entries(mods.statMultipliers) as Array<[Stat, number]>) {
    if (mult === 1) continue;
    const av = STAT_AV[stat];
    if (stat === "Health" || stat === "Magicka" || stat === "Stamina") {
      const base = actor.getBaseActorValue(av);
      set(av, Math.max(1, Math.min(100000, Math.round(base * mult))));
      actor.restoreActorValue(av, 1e7);
    } else if (stat === "SpellPower") {
      set(av, actor.getActorValue(av) + (mult - 1) * 100); // percent bonus to spell magnitude
    } else if (stat === "Damage") {
      set(av, Math.max(0.1, (actor.getActorValue(av) || 1) * mult));
    } else {
      const caps: Partial<Record<Stat, [number, number]>> = {
        Armor: [0, 1000], MagicResist: [-100, 85], Speed: [50, 250], CritChance: [0, 100],
      };
      const [lo, hi] = caps[stat] ?? [0, Infinity];
      const current = actor.getActorValue(av);
      // A stat that starts at zero (most enemies' magic resist) gets a flat boost instead.
      const scaled = current === 0 && mult > 1 ? (mult - 1) * 50 : current * mult;
      set(av, Math.max(lo, Math.min(hi, scaled)));
    }
  }

  for (const [kind, affinity] of Object.entries(mods.affinities) as Array<[DamageKind, string]>) {
    const av = RESIST_AV[kind];
    if (!av) continue; // Sun has no resistance value; absorbing it is handled on hit
    const value = { Weak: -50, Normal: 0, Resist: 50, Immune: 100, Absorb: 100 }[affinity] ?? 0;
    set(av, value);
  }

  for (const status of mods.autoStatuses) {
    for (const e of STATUS_EFFECTS[status]) {
      const current = actor.getActorValue(e.av);
      let to = e.mult !== undefined ? current * e.mult : current + (e.add ?? 0);
      if (e.max !== undefined) to = Math.min(e.max, to);
      set(e.av, to);
    }
  }
  return changes;
}

/** Damage kinds this nemesis heals from, for the on-hit absorb handler. */
export function absorbs(mods: BattleModifiers): DamageKind[] {
  return (Object.entries(mods.affinities) as Array<[DamageKind, string]>)
    .filter(([, a]) => a === "Absorb")
    .map(([k]) => k);
}
