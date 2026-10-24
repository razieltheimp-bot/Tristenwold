// The F10 window: contract board, other companies, dead drops, the nemesis roster and the
// chronicle. It's an HTML page in SkyrimPlatform's browser; this side keeps the state and
// turns button presses into messages for the server.
import { browser } from "skyrimPlatform";
import type { ClientMessage, DeadDropView, GroupView, PublicContract } from "./protocol.ts";

const PAGE = "file:///Data/Platform/UI/tristinwold.html";
// Line and paragraph separators are valid in JSON but end a line in JavaScript source.
const LS = String.fromCharCode(0x2028);
const PS = String.fromCharCode(0x2029);

export interface NemesisSummary {
  name: string; rank: string; species: string; level: number; alive: boolean;
  home: string; traits: string[]; scars: string[]; history: string[]; grudges: string[];
}

type Action = Exclude<ClientMessage, { type: "hello" | "presence" | "report_battle" | "rested" | "encounter_started" }>;
type MenuAction = Action | (Omit<Extract<ClientMessage, { type: "leave_drop" }>, "area"> & { area?: string });

export class Menu {
  private open = false;
  private statusText: () => string = () => "";
  private state = {
    here: "",
    contracts: [] as PublicContract[],
    groups: [] as GroupView[],
    drops: {} as Record<string, DeadDropView[]>,
    nemeses: [] as NemesisSummary[],
    keystones: [] as string[],
    log: [] as string[],
  };

  status(fn: () => string): void { this.statusText = fn; }
  here(area: string): void { if (area !== this.state.here) { this.state.here = area; this.push(); } }
  board(contracts: PublicContract[]): void { this.state.contracts = contracts; this.push(); }
  roster(groups: GroupView[]): void { this.state.groups = groups; this.push(); }
  drops(area: string, notes: DeadDropView[]): void {
    this.state.drops[area] = (this.state.drops[area] ?? []).concat(notes);
    this.push();
  }
  keystone(player: string, character: string): void {
    this.state.keystones.push(`${character} of ${player}'s company is a Keystone.`);
    this.log(`${character} of ${player}'s company is a Keystone.`);
  }
  log(line: string): void {
    this.state.log.push(line);
    if (this.state.log.length > 150) this.state.log.splice(0, this.state.log.length - 150);
    this.push();
  }

  nemeses(worldJson: string): void {
    try {
      const world = JSON.parse(worldJson) as { nemeses?: any[] };
      this.state.nemeses = (world.nemeses ?? []).map(summarise);
      this.push();
    } catch {
      // A malformed snapshot shouldn't break the game; keep the last good roster.
    }
  }

  toggle(): void {
    if (this.open) this.close(); else this.show();
  }

  private show(): void {
    this.open = true;
    browser.loadUrl(PAGE);
    browser.setVisible(true);
    browser.setFocused(true);
  }

  private close(): void {
    this.open = false;
    browser.setFocused(false);
    browser.setVisible(false);
    browser.loadUrl(""); // give the default SkyrimPlatform page back to other plugins
  }

  /** A browserMessage from the page. Returns what to send to the server, if anything. */
  action(args: unknown[]): MenuAction | null {
    const a = args[0] as { tw?: string; id?: string; text?: string } | undefined;
    if (!a || typeof a.tw !== "string") return null;
    switch (a.tw) {
      case "ready": this.push(); return null;
      case "close": this.close(); return null;
      case "claim": return a.id ? { type: "claim_contract", contractId: a.id } : null;
      case "abandon": return a.id ? { type: "abandon_contract", contractId: a.id } : null;
      case "finish": return a.id ? { type: "finish_contract", contractId: a.id } : null;
      case "say": return a.text?.trim() ? { type: "say", text: a.text.trim().slice(0, 400) } : null;
      case "drop": return a.text?.trim() ? { type: "leave_drop", text: a.text.trim().slice(0, 600) } : null;
      default: return null;
    }
  }

  private push(): void {
    if (!this.open) return;
    const payload = JSON.stringify({ ...this.state, status: this.statusText() })
      .split(LS).join("\\u2028").split(PS).join("\\u2029");
    browser.executeJavaScript(`window.tw && window.tw.render(${payload});`);
  }
}

function summarise(n: any): NemesisSummary {
  return {
    name: n.epithet ? `${n.name} ${n.epithet}` : n.name,
    rank: n.rank, species: n.species, level: n.level, alive: n.alive, home: n.homeArea ?? "",
    traits: (n.traits ?? []).map((t: any) => t.label),
    scars: (n.scars ?? []).map((s: any) => s.description),
    history: (n.history ?? []).slice(-5).map((h: any) => `Day ${h.day}: ${h.text}`),
    grudges: Object.entries(n.players ?? {}).map(([p, r]: [string, any]) =>
      `${p}: killed them ${r.timesKilledThem}x, they fled ${r.timesTheyFled}x, beaten by them ${r.timesTheyBeatMe}x`),
  };
}
