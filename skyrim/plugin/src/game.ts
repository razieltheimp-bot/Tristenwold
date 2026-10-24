// Everything that touches Skyrim. Reads fights out of game events, turns enemies into nemeses
// when the server says so, spawns nemeses Vann sends, and shows the shared world's news.
import {
  Actor, Debug, Enchantment, Form, Game, HttpClient, Input, ObjectReference, Spell, Weapon,
  on, printConsole, settings,
} from "skyrimPlatform";
import { EncounterTracker, type EnemyInfo, type Vec3 } from "./encounter.ts";
import { absorbs, applyModifiers, damageKindForResistance } from "./modifiers.ts";
import { SharedWorldClient, type Transport } from "./net.ts";
import { PROTOCOL_VERSION, type BattleModifiers, type DamageKind, type ServerMessage } from "./protocol.ts";
import { Menu } from "./menu.ts";

const GOLD = 0x0000000f;

interface Settings {
  server: string;
  company: string;
  password: string;
  /** Only one game per company connects (in Skyrim Together, the party leader's). */
  connect: boolean;
  menuKey: number;
}

function readSettings(): Settings {
  const s = (settings["tristinwold"] ?? {}) as Record<string, unknown>;
  return {
    server: String(s["server"] ?? "http://127.0.0.1:7778"),
    company: String(s["company"] ?? "Adventurer"),
    password: String(s["password"] ?? ""),
    connect: s["connect"] !== false,
    menuKey: Number(s["menuKey"] ?? 0x44), // F10
  };
}

export function start(): void {
  const config = readSettings();
  if (!config.connect) {
    printConsole("[Tristinwold] connect is off in tristinwold-settings.txt; another game in this company is connected.");
    return;
  }

  const http = new HttpClient(config.server);
  const transport: Transport = {
    post: (path, body) => http.post(path, { body, contentType: "application/json" })
      .then((r) => ({ status: r.status, body: r.body })),
  };

  const menu = new Menu();
  const absorbing = new Map<number, DamageKind[]>();
  const client = new SharedWorldClient(
    transport,
    () => ({ type: "hello", player: config.company, password: config.password, protocolVersion: PROTOCOL_VERSION, party: party() }),
    (m) => handle(m),
  );

  const tracker = new EncounterTracker(
    (area, enemies) => client.send({ type: "encounter_started", area, enemies }),
    (report) => client.send({
      type: "report_battle",
      // A new nemesis starts at level 1; it grows by beating you, not by your level.
      report: { ...report, players: [config.company], partyLevel: 5 },
    }),
  );

  // ---- helpers -------------------------------------------------------------------------------

  const player = () => Game.getPlayer()!;
  const actorById = (id: number) => Actor.from(Game.getFormEx(id));
  const posOf = (ref: ObjectReference): Vec3 => ({ x: ref.getPositionX(), y: ref.getPositionY(), z: ref.getPositionZ() });
  const isCompany = (a: Actor | null) => !!a && (a.getFormID() === player().getFormID() || a.isPlayerTeammate());

  function area(): string {
    const p = player();
    return p.getCurrentLocation()?.getName() || p.getParentCell()?.getName() || "the wilds";
  }

  function party(): string[] {
    return [player()?.getDisplayName() || config.company];
  }

  /** "Skyrim.esm|0x0003DF08": a form the server can hand back to spawn this kind of enemy. */
  function formKey(form: Form | null): string | undefined {
    if (!form) return undefined;
    const id = form.getFormID() >>> 0;
    const top = id >>> 24;
    if (top === 0xff) return undefined; // made at runtime; can't be found again
    if (top === 0xfe) return `${Game.getLightModName((id >>> 12) & 0xfff)}|0x${(id & 0xfff).toString(16).padStart(6, "0")}`;
    return `${Game.getModName(top)}|0x${(id & 0xffffff).toString(16).padStart(6, "0")}`;
  }

  function fromKey(key: string): Form | null {
    const [file, hex] = key.split("|");
    return file && hex ? Game.getFormFromFile(parseInt(hex, 16), file) : null;
  }

  function enemyInfo(a: Actor): EnemyInfo {
    const kind = a.getLeveledActorBase()?.getName() || a.getDisplayName() || "Stranger";
    return {
      actorId: a.getFormID(), kind, species: kind, baseForm: formKey(a.getBaseObject()),
      maxHealth: a.getBaseActorValue("Health"), pos: posOf(a),
    };
  }

  function damageKindOf(source: Form | null): DamageKind {
    const spell = Spell.from(source) ?? Enchantment.from(source);
    const weapon = Weapon.from(source);
    const magic = spell ?? weapon?.getEnchantment() ?? null;
    if (!magic) return "Physical";
    for (let i = 0; i < magic.getNumEffects(); i++) {
      const kind = damageKindForResistance(magic.getNthEffectMagicEffect(i)?.getResistance() ?? "");
      if (kind !== "Physical") return kind;
    }
    return "Physical";
  }

  function makeNemesis(a: Actor, mods: BattleModifiers): void {
    applyModifiers(a, mods);
    a.setDisplayName(mods.displayName, true);
    const heals = absorbs(mods);
    if (heals.length > 0) absorbing.set(a.getFormID(), heals);
  }

  function say(text: string, big = false): void {
    if (big) Debug.messageBox(text);
    else Debug.notification(text);
    menu.log(text);
  }

  // ---- messages from the shared world ----------------------------------------------------------

  function handle(m: ServerMessage): void {
    switch (m.type) {
      case "welcome": say(`Tristinwold: ${config.company}'s company joins ${m.campaign}.`); break;
      case "rejected": say(`Tristinwold: the server turned us away: ${m.reason}`, true); break;
      case "notice": {
        const text = m.from ? `${m.from}: ${m.kind === "Vann" ? `"${m.text}"` : m.text}` : m.text;
        say(text, m.kind === "Vann" && m.from === "Vann");
        break;
      }
      case "nemesis_appears":
        for (const spawn of m.spawns) {
          const id = tracker.actorForSlot(spawn.slot);
          const a = id === undefined ? null : actorById(id);
          if (!a || a.isDead()) continue;
          makeNemesis(a, spawn.modifiers);
          tracker.markNemesis(spawn.slot, spawn.nemesisId);
          say(spawn.taunt ? `${spawn.modifiers.displayName}: "${spawn.taunt}"` : `${spawn.modifiers.displayName} is here.`);
        }
        break;
      case "nemesis_hunts": {
        const base = fromKey(m.baseForm);
        const ref = base ? player().placeAtMe(base, 1, false, false) : null;
        const hunter = Actor.from(ref);
        if (!hunter) { printConsole(`[Tristinwold] could not spawn ${m.species} from ${m.baseForm}`); break; }
        // Out of sight, a little behind the player, then it comes for them.
        const p = player();
        const behind = (p.getAngleZ() + 180) * Math.PI / 180;
        hunter.setPosition(p.getPositionX() + Math.sin(behind) * 1500, p.getPositionY() + Math.cos(behind) * 1500, p.getPositionZ());
        makeNemesis(hunter, m.modifiers);
        hunter.startCombat(p);
        tracker.engaged(enemyInfo(hunter), area(), posOf(p), Date.now(), m.nemesisId);
        say(`${m.modifiers.displayName} has found you.`);
        break;
      }
      case "reward":
        player().addItem(Game.getFormEx(GOLD), Math.round(m.gil / 100), false); // 1 gp = 1 septim
        break;
      case "keystone_revealed": menu.keystone(m.player, m.character); break;
      case "board": menu.board(m.contracts); break;
      case "roster": menu.roster(m.groups); break;
      case "drops":
        menu.drops(m.area, m.notes);
        for (const n of m.notes) say(`A hidden note from ${n.from}: "${n.text}"`);
        break;
      case "nemesis_snapshot": menu.nemeses(m.worldJson); break;
      case "nemesis_events": for (const e of m.events) menu.log(e.text); break;
    }
  }

  // ---- game events -----------------------------------------------------------------------------

  on("combatState", (e) => {
    const a = Actor.from(e.actor);
    const target = Actor.from(e.target);
    if (!e.isCombat || !a || a.isDead() || isCompany(a) || !isCompany(target)) return;
    tracker.engaged(enemyInfo(a), area(), posOf(player()), Date.now());
  });

  on("hit", (e) => {
    const target = Actor.from(e.target);
    const aggressor = Actor.from(e.aggressor);
    if (!target || !aggressor) return;
    if (isCompany(aggressor) && !isCompany(target)) {
      const kind = damageKindOf(e.source);
      if (!tracker.inFight) tracker.engaged(enemyInfo(target), area(), posOf(player()), Date.now());
      tracker.hit(target.getFormID(), kind, config.company, Date.now());
      // Elemental absorb: the hit heals it instead (roughly a tenth of its health).
      if (absorbing.get(target.getFormID())?.includes(kind)) {
        target.restoreActorValue("Health", target.getBaseActorValue("Health") * 0.1);
      }
    } else if (isCompany(target) && !isCompany(aggressor)) {
      tracker.engaged(enemyInfo(aggressor), area(), posOf(player()), Date.now());
    }
  });

  on("deathEnd", (e) => {
    const dying = Actor.from(e.actorDying);
    if (!dying) return;
    if (dying.getFormID() === player().getFormID()) { tracker.downed(); return; }
    tracker.died(dying.getFormID(), isCompany(Actor.from(e.actorKiller)) ? config.company : undefined, Date.now());
    absorbing.delete(dying.getFormID());
  });

  on("enterBleedout", (e) => {
    if (Actor.from(e.actor)?.getFormID() === player().getFormID()) tracker.downed();
  });

  // Sleeping is resting: the world moves on while the company sleeps.
  on("sleepStop", () => client.send({ type: "rested" }));

  let lastFightTick = 0;
  let lastPresence = 0;
  let sentArea = "";
  let menuKeyDown = false;

  on("update", () => {
    const now = Date.now();
    client.tick(now);
    const p = Game.getPlayer();
    if (!p) return;

    if (now - lastFightTick > 500) {
      lastFightTick = now;
      tracker.tick(now, posOf(p), (id) => {
        const a = actorById(id);
        return !!a && !a.isDead() && a.isInCombat() && isCompany(a.getCombatTarget());
      }, (id) => {
        const a = actorById(id);
        return a ? posOf(a) : undefined;
      });
    }

    if (now - lastPresence > 2000) {
      lastPresence = now;
      const here = area();
      menu.here(here);
      if (here !== sentArea && client.status === "connected") {
        sentArea = here;
        client.send({ type: "presence", area: here, party: party() });
      }
    }

    const down = Input.isKeyPressed(config.menuKey);
    if (down && !menuKeyDown) menu.toggle();
    menuKeyDown = down;
  });

  on("browserMessage", (e) => {
    const action = menu.action(e.arguments);
    if (!action) return;
    if (action.type === "leave_drop") client.send({ ...action, area: area() });
    else client.send(action);
  });

  menu.status(() => `${config.company} | ${client.status}${client.lastError && client.status !== "connected" ? ` (${client.lastError})` : ""}`);
  printConsole(`[Tristinwold] connecting to ${config.server} as ${config.company}. Press F10 for the shared world.`);
}
