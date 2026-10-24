// End-to-end check without Skyrim: runs the built plugin (dist/.../tristinwold.js) with a fake
// skyrimPlatform API against a real Tristinwold server, and plays a few fights.
//
//   node e2e/run.mjs http://127.0.0.1:7778
import { readFileSync } from "node:fs";
import vm from "node:vm";

const server = process.argv[2] ?? "http://127.0.0.1:7778";
const handlers = {};
const notes = [];
const sent = [];
let nextId = 0x100;

class FakeForm {
  constructor(id, name) { this.id = id; this.name = name; }
  getFormID() { return this.id; }
  getName() { return this.name; }
}

class FakeActor extends FakeForm {
  constructor(name, { player = false, base = new FakeForm(0x0003df08, name), health = 250 } = {}) {
    super(player ? 0x14 : nextId++, name);
    this.display = name; this.base = base; this.dead = false; this.player = player;
    this.av = { Health: health, DamageResist: 50, MagicResist: 0, SpeedMult: 100, AttackDamageMult: 1 };
    this.baseAv = { ...this.av };
    this.x = 0; this.y = 0; this.z = 0; this.combatTarget = null;
  }
  isDead() { return this.dead; }
  isPlayerTeammate() { return false; }
  getDisplayName() { return this.display; }
  setDisplayName(n) { this.display = n; }
  getLeveledActorBase() { return this.base; }
  getBaseObject() { return this.base; }
  getBaseActorValue(n) { return this.baseAv[n] ?? 0; }
  getActorValue(n) { return this.av[n] ?? 0; }
  setActorValue(n, v) { this.av[n] = v; }
  restoreActorValue() {}
  getPositionX() { return this.x; } getPositionY() { return this.y; } getPositionZ() { return this.z; }
  setPosition(x, y, z) { this.x = x; this.y = y; this.z = z; }
  getAngleZ() { return 0; }
  isInCombat() { return !!this.combatTarget && !this.dead; }
  getCombatTarget() { return this.combatTarget; }
  startCombat(t) { this.combatTarget = t; }
  getCurrentLocation() { return { getName: () => "Riverwood" }; }
  getParentCell() { return null; }
  placeAtMe(base) { const a = new FakeActor(base.getName(), { base }); actors.set(a.id, a); return a; }
  addItem(_f, n) { notes.push(`+${n} gold`); }
}

const player = new FakeActor("Dovahkiin", { player: true, health: 300 });
const actors = new Map([[player.id, player]]);
const fireSpell = { getNumEffects: () => 1, getNthEffectMagicEffect: () => ({ getResistance: () => "FireResist" }) };

const sp = {
  settings: { tristinwold: { server, company: "Joe", password: "", connect: true, menuKey: 68 } },
  on: (name, cb) => { (handlers[name] ??= []).push(cb); },
  printConsole: (...a) => notes.push(a.join(" ")),
  Debug: { notification: (t) => notes.push(t), messageBox: (t) => notes.push(`[box] ${t}`) },
  Input: { isKeyPressed: () => false },
  browser: { loadUrl() {}, setVisible() {}, setFocused() {}, executeJavaScript() {} },
  Game: {
    getPlayer: () => player,
    getFormEx: (id) => (id === 0xf ? new FakeForm(0xf, "Gold") : actors.get(id) ?? null),
    getModName: () => "Skyrim.esm",
    getLightModName: () => "",
    getFormFromFile: (id, file) => new FakeForm(id, file === "Skyrim.esm" ? "Bandit Marauder" : "?"),
  },
  Actor: { from: (x) => (x instanceof FakeActor ? x : null) },
  ObjectReference: {},
  Form: {},
  Spell: { from: (x) => (x === fireSpell ? fireSpell : null) },
  Enchantment: { from: () => null },
  Weapon: { from: () => null },
  HttpClient: class {
    constructor(base) { this.base = base; }
    async post(path, { body }) {
      sent.push(JSON.parse(body));
      try {
        const r = await fetch(this.base + path, { method: "POST", body, headers: { "content-type": "application/json" } });
        return { status: r.status, body: await r.text(), error: "" };
      } catch (e) { return { status: 0, body: "", error: String(e) }; }
    }
  },
};

const code = readFileSync(new URL("../dist/Platform/Plugins/tristinwold.js", import.meta.url), "utf8");
vm.runInNewContext(code, { skyrimPlatform: sp, Date, Math, JSON, String, Number, Object, Array, Map, Promise, console });

const emit = (name, e) => (handlers[name] ?? []).forEach((cb) => cb(e));
const pump = setInterval(() => emit("update"), 20);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const fail = (msg) => { console.error("FAIL:", msg); console.error(notes.join("\n")); process.exit(1); };

async function fight(outcome) {
  const bandit = new FakeActor("Bandit Marauder");
  bandit.x = 800;
  actors.set(bandit.id, bandit);
  bandit.startCombat(player);
  emit("combatState", { actor: bandit, target: player, isCombat: true, isSearching: false });
  await sleep(4500); // gather (1.5s) + the server's answer
  if (outcome === "lose") {
    emit("enterBleedout", { actor: player });
  } else {
    emit("hit", { target: bandit, aggressor: player, source: fireSpell });
    bandit.dead = true;
    emit("deathEnd", { actorDying: bandit, actorKiller: player });
  }
  await sleep(1500);
  return bandit;
}

await sleep(2500);
if (!notes.some((n) => n.includes("joins"))) fail("never joined the server");

await fight("lose");
await sleep(1500);
const born = sent.flatMap((s) => s.messages ?? []).find((m) => m.type === "report_battle");
if (!born || born.report.result !== "Defeat") fail("the lost fight wasn't reported as a defeat");
console.log("Fight 1: lost to a Bandit Marauder ->", born.report.enemies[0].baseForm);

let nemesis = null;
for (let i = 0; i < 8 && !nemesis; i++) {
  const b = await fight("win");
  if (b.display !== "Bandit Marauder") nemesis = b;
}
if (!nemesis) fail("the nemesis never came back in 8 fights");
console.log(`Nemesis returned as "${nemesis.display}" with ${nemesis.av.Health} health (a plain one has 250).`);
if (!(nemesis.av.Health > 250)) fail("nemesis wasn't strengthened");

await sleep(2500);
const reports = sent.flatMap((s) => s.messages ?? []).filter((m) => m.type === "report_battle");
const last = reports.at(-1).report.enemies.find((e) => e.nemesisId);
if (!last || last.killingBlow !== "Fire" || last.killedBy !== "Joe") fail("the nemesis fight wasn't reported with its killer and element");
console.log("Killed it with fire. Server heard:", JSON.stringify({ killedBy: last.killedBy, killingBlow: last.killingBlow }));
console.log("Notifications in game:\n  " + notes.filter((n) => !n.startsWith("[Tristinwold]")).join("\n  "));
clearInterval(pump);
process.exit(0);
