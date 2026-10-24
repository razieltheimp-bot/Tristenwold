import { test } from "node:test";
import assert from "node:assert/strict";
import { absorbs, applyModifiers, type ActorValues } from "../src/modifiers.ts";

function actor(values: Record<string, number>): ActorValues & { values: Record<string, number> } {
  const base = { ...values };
  return {
    values,
    getBaseActorValue: (n) => base[n] ?? 0,
    getActorValue: (n) => values[n] ?? 0,
    setActorValue: (n, v) => { values[n] = v; },
    restoreActorValue: () => {},
  };
}

test("a captain's buffs land on Skyrim actor values, inside sane caps", () => {
  const a = actor({ Health: 200, DamageResist: 100, MagicResist: 0, SpeedMult: 100, AttackDamageMult: 1, FireResist: 0 });
  applyModifiers(a, {
    displayName: "Grask the Brutal",
    statMultipliers: { Health: 1.6, Damage: 1.35, MagicResist: 1.5, Speed: 3 },
    affinities: { Fire: "Absorb", Frost: "Weak" },
    autoStatuses: ["Haste", "Shell"],
  });
  assert.equal(a.values.Health, 320);
  assert.equal(a.values.AttackDamageMult, 1.35);
  assert.equal(a.values.FireResist, 100);
  assert.equal(a.values.FrostResist, -50);
  assert.equal(a.values.SpeedMult, 250, "speed capped even after Haste");
  assert.equal(a.values.MagicResist, 65, "flat boost from zero, then Shell");
  assert.equal(a.values.WeaponSpeedMult, 0.3);
});

test("knows which elements heal it", () => {
  assert.deepEqual(absorbs({ displayName: "x", statMultipliers: {}, affinities: { Shock: "Absorb", Fire: "Weak" }, autoStatuses: [] }), ["Shock"]);
});
