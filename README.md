# Tristinwold

A rebuild of Final Fantasy X HD (PC/Steam) as a sandbox for your own story, made of
[Fahrenheit](https://github.com/fahrenheit-crew/fahrenheit) mods:

| Part | Status |
|---|---|
| **Nemesis system** (`mods/Tristinwold.NemesisMod`, rules in `src/Tristinwold.Nemesis`) | Built, compiles against Fahrenheit 1.0.0-alpha12. Needs in-game testing. |
| **Story stripped** (no cutscenes/story gates, open travel) | Next |
| **Multiplayer** (each friend controls a party member in battle; shared nemesis world) | Planned |
| **Character creator** (mix existing models, weapons, names, stats, colours) | Planned |

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

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows or Linux:

```
dotnet test                                   # rules tests
dotnet build -c Release mods/Tristinwold.NemesisMod
```

The ready-to-install mod lands in `artifacts/deploy/mods/tristinwold-nemesis/`.

## Install (Windows, in your FFX folder)

1. Install Fahrenheit **1.0.0-alpha12** from its
   [releases page](https://github.com/fahrenheit-crew/fahrenheit/releases), following its
   [installation guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Installation-guide).
   You end up with `<game>/fahrenheit/{bin,mods,...}`.
2. Copy `artifacts/deploy/mods/tristinwold-nemesis` into `<game>/fahrenheit/mods/`.
3. Add a line `tristinwold-nemesis` to `<game>/fahrenheit/mods/loadorder`.
4. Launch with `fahrenheit\bin\fhstage0.exe ..\..\FFX.exe` (from `fahrenheit\bin`).
5. Set your player name in the Fahrenheit mod settings. Logs are in `<game>/fahrenheit/logs`.

## What to check on first launch

These hook points come from Fahrenheit and the Archipelago FFX client but haven't been run here:

1. **Battle start.** The `MsBtlReadManage` hook (state 13) renames and buffs a nemesis. Confirm the new
   name shows in battle and the HP is higher (Scan/Sensor).
2. **Battle end.** Victory and escape are read from `Btl.battle_end_type` at state `0x21`. Defeat comes
   from `MsGetBattleEndStatus == 1` at state `0x17`. The log should print the outcome line.
3. **Saving.** Save, reload, press F9, and the roster should still be there.

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
