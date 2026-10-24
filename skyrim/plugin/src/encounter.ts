// Skyrim has no battle screen, so this decides what counts as one fight and how it ended.
// A fight starts when an enemy first turns on the company and ends once nobody has been
// fighting them for a few seconds. No game API in here: the game layer feeds it facts.
import type { BattleReport, BattleResult, DamageKind, FormationSlot } from "./protocol.ts";

export interface Vec3 { x: number; y: number; z: number }

export interface EnemyInfo {
  actorId: number;
  kind: string;
  species: string;
  baseForm?: string;
  maxHealth: number;
  pos: Vec3;
}

interface Tracked {
  slot: number;
  info: EnemyInfo;
  startPos: Vec3;
  lastPos: Vec3;
  nemesisId?: string;
  killed: boolean;
  killedBy?: string;
  blow?: DamageKind;
  lastFightMs: number;
}

export interface EncounterOptions {
  /** How long without fighting before a fight is over. */
  quietMs: number;
  /** How long to gather enemies before asking the server which of them are nemeses. */
  gatherMs: number;
}

const DEFAULTS: EncounterOptions = { quietMs: 8000, gatherMs: 1500 };

export class EncounterTracker {
  private enemies = new Map<number, Tracked>();
  private bySlot = new Map<number, Tracked>();
  private nextSlot = 0;
  private active = false;
  private playerDowned = false;
  private playerStart: Vec3 = { x: 0, y: 0, z: 0 };
  private playerLast: Vec3 = { x: 0, y: 0, z: 0 };
  private announceAtMs = 0;
  private unannounced: number[] = [];
  private area = "";
  private readonly options: EncounterOptions;
  private readonly onStart: (area: string, enemies: FormationSlot[]) => void;
  private readonly onEnd: (report: Omit<BattleReport, "players" | "partyLevel">) => void;

  constructor(
    onStart: (area: string, enemies: FormationSlot[]) => void,
    onEnd: (report: Omit<BattleReport, "players" | "partyLevel">) => void,
    options: Partial<EncounterOptions> = {},
  ) {
    this.onStart = onStart;
    this.onEnd = onEnd;
    this.options = { ...DEFAULTS, ...options };
  }

  get inFight(): boolean { return this.active; }

  /** The game actor in a slot, so the game layer can find the enemy the server picked. */
  actorForSlot(slot: number): number | undefined { return this.bySlot.get(slot)?.info.actorId; }

  isNemesis(actorId: number): boolean { return this.enemies.get(actorId)?.nemesisId !== undefined; }

  /** An enemy is fighting the company (or the company it). */
  engaged(enemy: EnemyInfo, area: string, playerPos: Vec3, nowMs: number, nemesisId?: string): void {
    if (!this.active) {
      this.active = true;
      this.playerDowned = false;
      this.playerStart = { ...playerPos };
      this.area = area;
      this.announceAtMs = nowMs + this.options.gatherMs;
    }
    this.playerLast = { ...playerPos };
    const known = this.enemies.get(enemy.actorId);
    if (known) {
      known.lastFightMs = nowMs;
      known.lastPos = { ...enemy.pos };
      return;
    }
    const tracked: Tracked = {
      slot: this.nextSlot++, info: enemy, startPos: { ...enemy.pos }, lastPos: { ...enemy.pos },
      nemesisId, killed: false, lastFightMs: nowMs,
    };
    this.enemies.set(enemy.actorId, tracked);
    this.bySlot.set(tracked.slot, tracked);
    // A nemesis that came hunting is already known; only plain enemies go to the server.
    if (nemesisId === undefined) this.unannounced.push(enemy.actorId);
  }

  /** The server said this enemy is a nemesis (or it was spawned as one). */
  markNemesis(slot: number, nemesisId: string): void {
    const t = this.bySlot.get(slot);
    if (t) t.nemesisId = nemesisId;
  }

  /** Remember the last thing that hurt an enemy, for the scar it may get. */
  hit(actorId: number, kind: DamageKind, byCompanyMember: string | undefined, nowMs: number): void {
    const t = this.enemies.get(actorId);
    if (!t) return;
    t.blow = kind;
    if (byCompanyMember) t.killedBy = byCompanyMember;
    t.lastFightMs = nowMs;
  }

  died(actorId: number, killer: string | undefined, nowMs: number): void {
    const t = this.enemies.get(actorId);
    if (!t) return;
    t.killed = true;
    if (killer) t.killedBy = killer;
    t.lastFightMs = nowMs;
  }

  /** The player went down (bleedout or death). In this game that's losing the fight. */
  downed(): void {
    if (this.active) this.playerDowned = true;
  }

  /**
   * Call often. `stillFighting(actorId)` says whether that enemy is still in combat with the
   * company right now; `posOf` where it is.
   */
  tick(nowMs: number, playerPos: Vec3, stillFighting: (actorId: number) => boolean, posOf: (actorId: number) => Vec3 | undefined): void {
    if (!this.active) return;
    this.playerLast = { ...playerPos };

    if (this.unannounced.length > 0 && nowMs >= this.announceAtMs) {
      const slots = this.unannounced.map((id) => this.enemies.get(id)!).map((t) => ({
        slot: t.slot, kind: t.info.kind, species: t.info.species, baseForm: t.info.baseForm ?? null,
      }));
      this.unannounced = [];
      this.onStart(this.area, slots);
    }

    let anyFighting = false;
    for (const t of this.enemies.values()) {
      if (t.killed) continue;
      const pos = posOf(t.info.actorId);
      if (pos) t.lastPos = pos;
      if (stillFighting(t.info.actorId)) {
        t.lastFightMs = nowMs;
        anyFighting = true;
      } else if (nowMs - t.lastFightMs < this.options.quietMs) {
        anyFighting = true;
      }
    }
    if (!anyFighting || this.playerDowned) this.finish();
  }

  private finish(): void {
    const tracked = [...this.enemies.values()];
    const alive = tracked.filter((t) => !t.killed);

    let result: BattleResult;
    if (this.playerDowned) {
      result = "Defeat";
    } else if (alive.length === 0) {
      result = "Victory";
    } else {
      // Somebody broke off with enemies still alive. Whoever moved further is the one who ran.
      const playerMoved = dist(this.playerStart, this.playerLast);
      const enemyMoved = alive.reduce((sum, t) => sum + dist(t.startPos, t.lastPos), 0) / alive.length;
      result = playerMoved > enemyMoved + 500 ? "PartyEscaped" : "EnemiesEscaped";
    }

    this.onEnd({
      areaId: this.area,
      result,
      enemies: tracked.map((t) => ({
        slot: t.slot,
        kind: t.info.kind,
        species: t.info.species,
        maxHealth: Math.round(t.info.maxHealth),
        killed: t.killed,
        escaped: !t.killed && result === "EnemiesEscaped",
        nemesisId: t.nemesisId ?? null,
        killingBlow: t.killed ? (t.blow ?? "Physical") : null,
        killedBy: t.killed ? (t.killedBy ?? null) : null,
        baseForm: t.info.baseForm ?? null,
      })),
    });

    this.enemies.clear();
    this.bySlot.clear();
    this.unannounced = [];
    this.nextSlot = 0;
    this.active = false;
    this.playerDowned = false;
  }
}

function dist(a: Vec3, b: Vec3): number {
  return Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);
}
