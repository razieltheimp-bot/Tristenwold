import { test } from "node:test";
import assert from "node:assert/strict";
import { EncounterTracker, type EnemyInfo } from "../src/encounter.ts";

const at = (x: number) => ({ x, y: 0, z: 0 });
const bandit = (id: number, x = 1000): EnemyInfo => ({ actorId: id, kind: "Bandit Marauder", species: "Bandit Marauder", maxHealth: 250, pos: at(x), baseForm: "Skyrim.esm|0x0003DF08" });

function track() {
  const starts: any[] = [];
  const ends: any[] = [];
  const t = new EncounterTracker((area, enemies) => starts.push({ area, enemies }), (r) => ends.push(r), { quietMs: 5000, gatherMs: 1000 });
  return { t, starts, ends };
}

test("gathers the enemies, asks about nemeses once, and reports a win", () => {
  const { t, starts, ends } = track();
  t.engaged(bandit(10), "Riverwood", at(0), 0);
  t.engaged(bandit(11), "Riverwood", at(0), 300);
  t.tick(500, at(0), () => true, () => undefined);
  assert.equal(starts.length, 0, "still gathering");
  t.tick(1000, at(0), () => true, () => undefined);
  assert.deepEqual(starts[0].enemies.map((e: any) => e.slot), [0, 1]);
  assert.equal(t.actorForSlot(1), 11);

  t.markNemesis(1, "abc");
  t.hit(11, "Fire", "Joe", 2000);
  t.died(10, "Joe", 2100);
  t.died(11, undefined, 2200);
  t.tick(2300, at(0), () => false, () => undefined);
  const report = ends[0];
  assert.equal(report.result, "Victory");
  assert.equal(report.enemies[1].nemesisId, "abc");
  assert.equal(report.enemies[1].killingBlow, "Fire");
  assert.equal(report.enemies[1].killedBy, "Joe");
  assert.equal(t.inFight, false);
});

test("going down in a fight is a defeat", () => {
  const { t, ends } = track();
  t.engaged(bandit(10), "Bleak Falls Barrow", at(0), 0);
  t.downed();
  t.tick(100, at(0), () => true, () => undefined);
  assert.equal(ends[0].result, "Defeat");
  assert.equal(ends[0].enemies[0].killed, false);
});

test("whoever ran the furthest is the one who fled", () => {
  const { t, ends } = track();
  t.engaged(bandit(10, 1000), "Whiterun", at(0), 0);
  t.tick(1000, at(4000), () => false, () => at(1100)); // the player sprinted away
  t.tick(7000, at(6000), () => false, () => at(1100));
  assert.equal(ends[0].result, "PartyEscaped");

  t.engaged(bandit(20, 1000), "Whiterun", at(0), 10000);
  t.tick(11000, at(0), () => false, () => at(5000)); // the bandit fled
  t.tick(16000, at(0), () => false, () => at(6000));
  assert.equal(ends[1].result, "EnemiesEscaped");
  assert.equal(ends[1].enemies[0].escaped, true);
});

test("a hunting nemesis isn't asked about again", () => {
  const { t, starts } = track();
  t.engaged(bandit(10), "Riverwood", at(0), 0, "hunter-1");
  t.tick(2000, at(0), () => true, () => undefined);
  assert.equal(starts.length, 0);
  assert.equal(t.isNemesis(10), true);
});
