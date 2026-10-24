# Tristinwold

A rebuild of Final Fantasy X HD (PC/Steam) as a sandbox for your own story, made of
[Fahrenheit](https://github.com/fahrenheit-crew/fahrenheit) mods:

| Part | Status |
|---|---|
| **Nemesis system** (`mods/Tristinwold.Mod/nemesis`, rules in `src/Tristinwold.Nemesis`) | Built. Needs in-game testing. |
| **Shared world multiplayer** (`mods/Tristinwold.Mod/multiplayer`, server in `tools/Tristinwold.Server`) | Built. Server tested end to end; in-game side needs testing. |
| **Story stripped** (no cutscenes/story gates, open travel) | Next |
| **Character creator** (mix existing models, weapons, names, stats, colours) | Planned |

Everything installs as **one** Fahrenheit mod, `tristinwold`, with a module per feature.

You need your own copy of FFX/X-2 HD Remaster on PC. No game files are included or needed to build.

## The Nemesis system

- A monster that **wipes your party** becomes a named **Captain**. One you **run from**, or one that
  **runs from you**, becomes a named **Grunt**.
- Each nemesis gets strengths and weaknesses that map onto FFX itself: stat boosts, elemental
  absorb/immune/weak, auto-Haste/Protect/Shell/Regen/Reflect.
- It **remembers**: who it killed, who ran from it, who beat it. Per player, so in multiplayer it singles
  out the friend who fled. It taunts you by name when it shows up again.
- It **comes back**. When you meet its species again, especially in its home territory, it can take
  that monster's place: renamed, with buffed HP and stats.
- **Killing it isn't always final.** It can cheat death, come back scarred, and resist whatever killed
  it. Twice from the same element and it becomes immune.
- **Hierarchy.** Overlord → 5 Warchiefs → 4 Captains each → Grunts. Wins earn power, power earns
  promotion, and with no vacancy a captain challenges the weakest warchief.
- **The world moves while you rest.** Every save advances a day: rivals feud and duel (winner takes the
  title, loser dies or is humiliated and scarred), and nemeses swear blood oaths. Kill one and its
  blood-brother hunts you.
- **F9** in-game opens the Captain Grid: everyone's rank, traits, scars, grudges and life story.
- The world is stored with each save file.

Names, epithets and taunts are data. Copy `NamePack.Default` (in `src/Tristinwold.Nemesis/Content.cs`)
to a JSON file to give them your setting's flavour.

### Try the rules without the game

```
dotnet run --project tools/Tristinwold.NemesisSim -- 80 7
```
This plays 80 fake battles (seed 7) and prints every birth, promotion, feud and death, then the final
Captain Grid. Use it to tune `NemesisRules`.

## Shared world: everyone runs their own party of three

This is "The Scattered Key" from *The Rings of Tristinwold*. Each friend plays their own FFX, with their
own party, at their own pace. One server, usually run by the DM, holds the city they all share:

- **One nemesis world.** A monster that wipes Joe's party in his game can turn up in Mira's game and taunt
  her about it ("I killed Joe. You're next.").
- **One contract board.** The day-one board from Campaign 1 is preloaded: rats, spice wagon, the missing
  brother, the gambler's debt, the drowned chapel, the Gutter Prince. When one company takes a job,
  it's gone for everyone. Contract secrets stay on the DM's screen.
- **Vann's rule.** When the DM closes a table's first contract, the server notes who they saved:
  that's their Vann.
- **Keystones.** Each table secretly gets a Keystone from its own party. Only the DM sees it, until
  `keystone <player>` reveals it to everyone.
- **Near misses.** Arrive where another company is and you glimpse them before the crowd closes, or time
  skips and they're streets away. Arrive just after they left and you find fresh tracks.
- **Dead drops.** Hide a note in your current area for the next company. Some never arrive: Vann reads
  them. The DM can `release` an intercepted note.
- **Contract pay** arrives as gil (1 gp = 100 gil). **Resting** (saving) advances the shared calendar.
- **F10** in-game: board, companies, dead drops, chronicle and chat.

### Run the server (DM)

```
dotnet run -c Release --project tools/Tristinwold.Server -- --password <something> --campaign tristinwold.json
```

It listens on TCP **7777** and saves everything to `tristinwold.json` after every change, so stopping and
starting it loses nothing. For friends outside your house, forward TCP 7777 on your router to this PC,
or put everyone on a free [Tailscale](https://tailscale.com) network and use its address. Add
`--players-finish` if you want players to close their own contracts instead of you.

DM commands, typed into the server window:

```
who                        groups, areas, keystones, who their Vann is
board                      contracts with their secrets
post <id> <pay> <title>    pin a new contract
done <id>                  close a contract and pay whoever holds it
reopen <id>                put a contract back on the board
say <text>                 announce to every table
tell <player> <text>       whisper to one table
keystone <player> [name]   reveal that table's Keystone (or set it to <name>)
flag <name> | unflag <name>   world consequences shared by every table
drops                      list dead drops (* = intercepted by Vann)
release <n>                let intercepted drop n reach its area
day                        advance the world one day
nemeses                    living nemeses
save                       save now
```

### Join (each player)

In the Fahrenheit mod settings: set **player_name** (Nemesis), then the server **host**, **port** and
**password** (Shared World). Press **F10** → Connect, or turn on **auto_connect**.

Areas are FFX room numbers for now ("Area 152"). Once the story module maps rooms to Tristinwold places
("The Docks", "The Needle"), those names show instead.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows or Linux:

```
dotnet test                                   # rules tests
dotnet build -c Release mods/Tristinwold.Mod
```

The ready-to-install mod lands in `artifacts/deploy/mods/tristinwold/`.

## Install (Windows, in your FFX folder)

1. Install Fahrenheit **1.0.0-alpha12** from its
   [releases page](https://github.com/fahrenheit-crew/fahrenheit/releases), following its
   [installation guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Installation-guide).
   You end up with `<game>/fahrenheit/{bin,mods,...}`.
2. Copy `artifacts/deploy/mods/tristinwold` into `<game>/fahrenheit/mods/`.
3. Add a line `tristinwold` to `<game>/fahrenheit/mods/loadorder`.
4. Launch with `fahrenheit\bin\fhstage0.exe ..\..\FFX.exe` (from `fahrenheit\bin`).
5. Set your player name in the Fahrenheit mod settings. Logs are in `<game>/fahrenheit/logs`.

## What to check on first launch

These hook points come from Fahrenheit and the Archipelago FFX client but haven't been run here:

1. **Battle start.** The `MsBtlReadManage` hook (state 13) renames and buffs a nemesis. Confirm the new
   name shows in battle and the HP is higher (Scan/Sensor).
2. **Battle end.** Victory and escape are read from `Btl.battle_end_type` at state `0x21`. Defeat comes
   from `MsGetBattleEndStatus == 1` at state `0x17`. The log should print the outcome line.
3. **Saving.** Save, reload, press F9, and the roster should still be there.
4. **Shared world.** With the server running, F10 → Connect. Your company should appear under
   Companies, and moving between areas should update it. Contract pay should raise your gil.

If something's off, the log line plus what you saw is enough for me to fix it.

## Known limits

- FFX characters are single whole models; only weapons are separate. The character creator will
  combine whole models + weapons + names + stats, not separate heads and bodies.
- Killing-blow elements aren't read yet, so death-scars are "physical" for now.
- A nemesis only replaces a monster of its own species (that model is the one loaded for the battle).

## Credits

Built on Fahrenheit (LGPL-3.0). Hook points and memory layouts were cross-checked against the FFX-AP
Archipelago client, FFX_TAS_Python, Farplane, UnX, VBFExtract and FFXView. Fahrenheit is a work in
progress, so its APIs may change. Questions about Fahrenheit itself go to the developers'
[Discord](https://discord.gg/AGx2grw9nD); they don't accept AI-assisted contributions upstream.
