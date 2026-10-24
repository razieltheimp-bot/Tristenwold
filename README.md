# Tristinwold

A multiplayer game for *The Rings of Tristinwold*, being rebuilt on **Skyrim Special Edition**
(Final Fantasy X has been dropped). Final Fantasy-style mechanics come along: Haste, Protect, Shell,
Regen and Reflect today; limit breaks, a Sphere Grid board and summons are planned.

| Part | Status |
|---|---|
| **Nemesis rules** (`src/Tristinwold.Nemesis`) | Built and tested. Uses Skyrim actor kinds, actor values and damage types. |
| **Shared world server + Vann** (`src/Tristinwold.Shared`, `tools/Tristinwold.Server`) | Built and tested end to end. Game-agnostic. |
| **Skyrim game layer** (in-game client) | Next: choosing between Skyrim Together Reborn and SkyMP. |
| **Story, character creator, Final Fantasy mechanics** | Planned, on Skyrim. |

You need your own copy of Skyrim Special Edition on PC.

## The Nemesis system

- An enemy that **kills you** becomes a named **Captain**. One you **run from**, or one that **flees from
  you**, becomes a named **Grunt**.
- Each nemesis gets strengths and weaknesses in Skyrim terms: health, armor, damage, speed, spell power,
  crit chance, fire/frost/shock/poison/sun resistances (or absorbing them), plus Final Fantasy statuses.
- It **remembers** who it killed, who ran and who beat it, per player, and taunts you by name.
- It **comes back**: the next time you fight its kind, especially on its home ground, it can take one of
  those enemies' place, renamed and stronger.
- **Killing it isn't always final.** It can cheat death, come back scarred and resist whatever killed it.
- **Hierarchy**: Overlord → Warchiefs → Captains → Grunts, with promotions, feuds, duels and blood oaths.

Names, epithets and taunts are data (`NamePack` in `src/Tristinwold.Nemesis/Content.cs`).

### Try the rules without the game

```
dotnet run --project tools/Tristinwold.NemesisSim -- 80 7
```

## Shared world: everyone runs their own party of three

This is "The Scattered Key" from *The Rings of Tristinwold*. Each friend plays their own game, with their
own company, at their own pace. One server, usually run by the DM, holds the city they all share:

- **One nemesis world.** A monster that wipes Joe's party in his game can turn up in Mira's game and taunt
  her about it ("I killed Joe. You're next.").
- **One contract board.** The day-one board from Campaign 1 is preloaded: rats, spice wagon, the missing
  brother, the gambler's debt, the drowned chapel, the Gutter Prince. When one company takes a job,
  it's gone for everyone. Contract secrets stay on the DM's screen.
- **Vann's rule.** The first person a company saves (whoever was in danger on their first contract) is
  their Vann.
- **Keystones.** Each company secretly gets a Keystone from its own party. Nobody knows who, until two
  Keystones end up in the same place and time stops around them.
- **Near misses.** Arrive where another company is and you glimpse them before the crowd closes, or time
  skips and they're streets away. Arrive just after they left and you find fresh tracks.
- **Dead drops.** Hide a note in your current area for the next company. Some never arrive: Vann reads
  them. The DM can `release` an intercepted note.
- **Contract pay** arrives as gil (1 gp = 100 gil) when you mark a contract done. **Resting** (saving)
  advances the shared calendar.

### Vann plays himself

No DM needed. The server plays Vann, following "How Vann keeps them apart" from the campaign doc. He
never attacks a meeting directly; he nudges:

- **The Saved Man.** After your first rescue, the person you saved thanks you and offers to travel
  with you. From then on he whispers helpful, plausible advice that keeps you away from the other
  companies ("I've heard Mira's company is trouble. Let's try the Docks instead.").
- **The reveal.** After your third finished contract the mask comes off: "You've done this before. You
  saved me. You always save me." The other companies hear that you've learned who you saved.
- **Rival jobs.** Take a contract and a well-dressed stranger may pin a better-paying job on the board
  for the others: make sure your company fails.
- **Bad information.** Some dead drops arrive rewritten, still signed with your friend's name.
- **Reputations.** Rumours spread about a company's crimes, all of them lies.
- **Strange time.** A clock strikes thirteen; you find a note in your own handwriting.
- **Sending nemeses.** He tells a nemesis that already hates you where to find you, and it gets stronger.
- **The one thing he can't stop:** two Keystones meeting.

**Claude voices him** if the server machine has an Anthropic API key. Set `ANTHROPIC_API_KEY` before
starting it and every line is written fresh, in character, by Claude (`claude-opus-5-5`, low effort, a
couple of sentences per line; a few cents per hour of play). If a line is ever declined, a fallback
model voices it. Without a key, or if the call fails, he uses his written lines, so he always talks.

### Run the server

Anyone can host, including someone who's playing; the window doesn't show Vann's lines or private
messages.

```
set ANTHROPIC_API_KEY=sk-ant-...        (optional: Claude voices Vann; PowerShell: $env:ANTHROPIC_API_KEY="...")
dotnet run -c Release --project tools/Tristinwold.Server -- --password <something> --campaign tristinwold.json
```

It listens on TCP **7777** and saves everything to `tristinwold.json` after every change, so stopping and
starting it loses nothing. For friends outside your house, forward TCP 7777 on your router to this PC,
or put everyone on a free [Tailscale](https://tailscale.com) network and use its address.

| Option | Effect |
|---|---|
| `--vann-minutes 6` | average minutes between Vann's unprompted schemes |
| `--voice claude` / `--voice off` | force Claude's voice on or off (default: on when an API key is set) |
| `--no-vann` | Vann only reacts; no unprompted schemes |
| `--dm` | a human DM closes contracts from the window instead of players |
| `--spoilers` | print Vann's lines and private messages in the window (for a DM who isn't playing) |

Optional DM commands, typed into the server window:

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
drops                      list dead drops (* = intercepted, ~ = forged by Vann)
release <n>                let intercepted drop n reach its area
day                        advance the world one day
nemeses                    living nemeses
save                       save now
```

### Join (each player)

The in-game client comes with the Skyrim game layer.

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows or Linux:

```
dotnet test
```

## Credits

Earlier FFX prototypes drew on Fahrenheit, the FFX-AP Archipelago client, FFX_TAS_Python, Farplane,
UnX, VBFExtract and FFXView. None of that code ships here any more.
