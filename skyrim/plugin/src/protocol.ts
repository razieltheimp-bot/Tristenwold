// The wire format of the Tristinwold server (src/Tristinwold.Shared/Protocol.cs), as TypeScript.
// JSON is camelCase with a "type" discriminator; enums travel as their names.

export const PROTOCOL_VERSION = 1;

export type Stat = "Health" | "Magicka" | "Stamina" | "Damage" | "Armor" | "MagicResist" | "Speed" | "SpellPower" | "CritChance";
export type DamageKind = "Physical" | "Fire" | "Frost" | "Shock" | "Poison" | "Sun" | "Unknown";
export type Affinity = "Weak" | "Normal" | "Resist" | "Immune" | "Absorb";
export type AutoStatus = "Haste" | "Protect" | "Shell" | "Regen" | "Reflect";
export type BattleResult = "Defeat" | "Victory" | "PartyEscaped" | "EnemiesEscaped";
export type NoticeKind = "Chat" | "Dm" | "NearMiss" | "World" | "Contract" | "Error" | "Vann";
export type ContractStatus = "Open" | "Claimed" | "Done";

export interface FormationSlot { slot: number; kind: string; species: string; baseForm?: string | null }

export interface EnemyReport {
  slot: number;
  kind: string;
  species: string;
  maxHealth: number;
  killed: boolean;
  escaped: boolean;
  nemesisId?: string | null;
  killingBlow?: DamageKind | null;
  killedBy?: string | null;
  baseForm?: string | null;
}

export interface BattleReport {
  areaId: string;
  result: BattleResult;
  enemies: EnemyReport[];
  players: string[];
  partyLevel: number;
}

export interface BattleModifiers {
  displayName: string;
  statMultipliers: Partial<Record<Stat, number>>;
  affinities: Partial<Record<DamageKind, Affinity>>;
  autoStatuses: AutoStatus[];
}

export interface NemesisSpawn { slot: number; nemesisId: string; modifiers: BattleModifiers; taunt?: string | null }

export interface PublicContract {
  id: string; title: string; postedBy: string; pay: number; where?: string | null;
  status: ContractStatus; claimedBy?: string | null;
}

export interface DeadDropView { from: string; text: string; day: number }
export interface GroupView { player: string; area: string; party: string[]; online: boolean }

export interface WorldEvent { kind: string; nemesisId: string; text: string; otherId?: string | null }

// client -> server
export type ClientMessage =
  | { type: "hello"; player: string; password: string; protocolVersion: number; party: string[] }
  | { type: "presence"; area: string; party: string[] }
  | { type: "report_battle"; report: BattleReport }
  | { type: "rested" }
  | { type: "claim_contract"; contractId: string }
  | { type: "abandon_contract"; contractId: string }
  | { type: "finish_contract"; contractId: string }
  | { type: "leave_drop"; area: string; text: string }
  | { type: "say"; text: string }
  | { type: "encounter_started"; area: string; enemies: FormationSlot[] };

// server -> client
export type ServerMessage =
  | { type: "welcome"; player: string; campaign: string; flags: string[] }
  | { type: "rejected"; reason: string }
  | { type: "nemesis_snapshot"; worldJson: string }
  | { type: "nemesis_events"; events: WorldEvent[] }
  | { type: "board"; contracts: PublicContract[] }
  | { type: "drops"; area: string; notes: DeadDropView[] }
  | { type: "roster"; groups: GroupView[] }
  | { type: "notice"; kind: NoticeKind; text: string; from?: string | null }
  | { type: "reward"; contractId: string; gil: number }
  | { type: "keystone_revealed"; player: string; character: string }
  | { type: "nemesis_appears"; spawns: NemesisSpawn[] }
  | { type: "nemesis_hunts"; nemesisId: string; kind: string; species: string; baseForm: string; modifiers: BattleModifiers; taunt?: string | null };

export type Hello = Extract<ClientMessage, { type: "hello" }>;

export interface SyncResponse { session?: string | null; messages: ServerMessage[]; error?: string | null }
