# Tristinwold

A multiplayer game for *The Rings of Tristinwold*: a **standalone Godot 4 game**, with auto-statuses (Haste,
Protect, Shell, Regen, Reflect) and limit breaks in its fights, and summons planned. Leveling is skills and
perks.

| Part | Status |
|---|---|
| **The game** (`game/`, Godot 4 .NET) | Playable: the three-ring city of Tristinwold and an endless world around it. Buildings are generated in code (timber-framed and stone houses, taverns, churches, keeps, walls); the ground, buildings, trees, rocks and props are photo-textured from Poly Haven scans; characters come from the KayKit packs. Sword, spells, limit break, Tong lairs, deviants that come back, a fog-of-war map, villages and camps, people to talk to and quests to do, the shared-world board. A scripted headless test plays it against the real server. Open/CC0 art to come. |
| **The open world** (`src/Tristinwold.World`) | Built and tested. Grows the land, the cities, the villages, the people and their quests from one seed, with no edge. |
| **Combat rules** (`src/Tristinwold.Gameplay`) | Built and tested: health/mana/stamina, armour, resistances, auto-statuses, the limit gauge, the bestiary, fight tracking. |
| **Deviant rules** (`src/Tristinwold.Deviant`) | Built and tested: named enemies that remember, return and rise, with their own strengths, weaknesses and damage types. |
| **Shared world server + Vann** (`src/Tristinwold.Shared`, `tools/Tristinwold.Server`) | Built and tested end to end. Game-agnostic. |
| **The world** (`world/world.json`) | Nine cities with their own names, places, stories and contracts, and the characters written by hand. |
| **The main story** (`src/Tristinwold.World/Immortals.cs`, `game/scripts/Story.cs`) | Built and tested: every hero is an Immortal. See *The story* below. |
| **Summons, co-op inside a company** | Planned. |

## The story: the Immortals

Every hero in the world, yours and every other player's, is an **Immortal**, and none of them was born one.

When the Rift tore open and the **Hollow Host** came through it, the only cities left standing were the ones the Magi
had *ringed*: a **Wake** sunk under each, a well of Keystone-light the demons can't abide. Then the Magi found what the
Host fears most, and set about making it. In the undercroft of the Needle they took the people nobody would miss
(foundlings out of the Warrens, the condemned off the gallows, Ashborn orphans from outside the walls) and remade them
in **the Ringing**, the greatest working of magic the Magi ever wrought: nine nights of spellwork in a circle of
Keystone-light, the blood sung through with it, the old life burned out by the spell, a **ring of black iron** sunk into the
bone of one finger and bound to the Wake beneath the city. Of every hundred given to the Needle, a handful woke. Those
who did remember nothing before it. They're quicker and harder than anyone born, cold to pain, and **bound: when one
dies, the ring drags what's left of them back to the Wake, and the Wake makes them again**, at the Gallows Perch,
where Mother Corva waits with a blanket and a bottle (and writes every return in her ledger).

That's why they're the line the cities hold against the Host: a soldier dies once. And it's why an Immortal can bring
down a dragon the Rift has taken, which no army can: they can come back, try again, and know a little more of how it
fights each time. The townsfolk pay them and need them and don't love them: *Immortals* to their faces; *gallows-born*,
*cuckoos*, *the Needle's get* behind their backs.

The main quest, **The Rings of Tristinwold**, is on the tracker from the moment you first wake:

1. **Waking.** You open your eyes on the straw at the Gallows Perch with an iron ring on your finger. Mother Corva
   tells you what you are.
2. **The Archive.** Sister Ilse shows you the Magi's ledger of your intake: a hundred given to the Needle, nine woke.
3. **The Proving.** Fall in battle, and come back: the ring holds.
4. **The Breach.** The Host has torn a breach open beyond the walls: a wound of violet light that imps crawl out of,
   and worse. Slay eight of them and it closes.
5. **Dragonfire.** Bring down a dragon the Rift has taken. The tracker points you at the nearest.
6. **Warden of the Rings.** Mother Corva writes the title after your name, and tells you the Magi aren't telling you
   everything: one of them walked out of the Needle with the whole of the Ringing in his head, years back.

Every time you fall afterwards you still wake at the Gallows Perch, and Mother Corva has something to say about it.

## Play

You need [Godot 4.7 .NET](https://godotengine.org/download) and the .NET 10 SDK. Start the server
(see *Shared world* below), then each company runs:

```
godot --path game -- --server 192.168.1.20:7777 --company Joe --password secret
```

Without `--server`, the game opens on a login page: the server's address and password, then your account
name and password (**Create account** the first time). Then pick one of your characters or make a new one:
a name, a colour and a starting spell. On a server, your characters and their progress (map, quests,
crowns) belong to your account, so you can play the same character from your phone and your PC. Nobody
else can join as your character. The game stays logged in on that device (it keeps a login token, not
your password). **Play alone** keeps characters on the device instead. You can also open `game/project.godot`
in the Godot editor and press Play.

From the command line, `--account Joe --secret yourpassword --company "Bram Ashmane"` logs in, makes the
account and character if they're new, and plays. The server keeps accounts in `<campaign>.accounts.json`,
next to the campaign file, with passwords stored only as salted PBKDF2 hashes.

**Graphics** (on the login page, or `--graphics low|medium|high|ultra|cinematic`), done with Godot's own renderer:

| | Low | Medium | High | Ultra | Cinematic |
|---|---|---|---|---|---|
| View distance | 256 m | 384 m | 448 m | 576 m | 704 m |
| Water with waves, depth tint and shore foam; trees in the wind | | ✓ | ✓ | ✓ | ✓ |
| Grass around you | | | ✓ | ✓ (wider) | ✓ (widest, thickest) |
| Shadows | | ✓ | soft | soft, further | finest, to 400 m |
| Ambient occlusion, volumetric fog, AgX tone mapping | | | ✓ | ✓ | ✓ (full resolution) |
| Photo-scanned trees, rock faces, cliffs and undergrowth (PC) | | | ✓ | ✓ | ✓ |
| Indirect light (SSIL), reflections, AMD FSR 2 anti-aliasing | | | | ✓ | ✓ (finest) |
| **Global illumination** (SDFGI): light bouncing round the whole scene | | | | | ✓ |
| Every model at full detail however far off | | | | | ✓ |

**Cinematic** is everything Godot has, for a big gaming PC with a good graphics card. It's this game's answer to
Unreal Engine 5's showpieces: **global illumination** (sunlight and skylight bouncing round the whole scene, colour
bleeding off walls and grass, alleys lit by what's outside them; Godot's SDFGI, where UE5 has Lumen), the finest
reflections, shadows and fog, and **every model at full detail however far off** (UE5's Nanite streams detail in;
here the photo-scanned models simply never drop to simpler stand-ins). The detail comes from **Poly Haven's
photo-scanned models** (CC0): firs and pines, rock faces and cliffs, mossy boulders, shrubs and nettles in the wilds,
and Dutch merchantmen and a pinnace at anchor in Tristinwold's harbour, riding the swell. (Unreal's own photoscan
store and its MetaHuman characters are licensed for Unreal only, so they can't be used here.)

Phones start on Medium and render slightly below screen size (FSR 1) to stay smooth; PCs start on High.
The phone version always starts in landscape.

### Playing on an iPhone: remote play through Cloudflare

An iPhone can't install the game, so it plays it remotely instead: the game runs on your computer at home and
the iPhone's web browser shows it and controls it, from anywhere, with nothing installed. The picture streams
to the phone and every touch goes back, so the on-screen joystick and buttons work as they would on a phone.
On the computer (leave the game's window open, not minimised):

```
play-from-iphone.bat                 (Windows; or ./play-from-iphone.sh)
godot --path game -- --remote --tunnel   (the same thing by hand)
```

Hold the phone sideways and the game fills the screen with its own on-screen controls. Hold it upright and
the picture sits at the top with a controller underneath, where the keyboard would be: a joystick to walk, a
smaller one to turn the camera, the
class's moves (named for your class, glowing when the limit is ready or there's someone to talk to), and
Map, Quests, Pack, Party, Skills, Heal, Rest, World, Menu and Type buttons. Drag on the picture to look
around; tap it to work menus. Turning the phone switches between the two at once.

It prints a link like `https://….trycloudflare.com/?k=3f9a…` and shows it at the top of the game window; with
`TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` set it also sends the link to your Telegram. Open it in Safari,
turn the phone sideways, and play. In Safari, Share → **Add to Home Screen** makes it open full screen like an
app. The ⌨ button on the left opens the phone's keyboard for typing names and passwords (or start the game
with `--account`, `--secret` and `--server` so there's nothing to type). A Bluetooth keyboard plays with the
usual keys. The `k=` part of the link is its key: only people with the whole link can watch or play.

The game window also shows the link as a QR code (it steps aside while a phone is playing): point the
iPhone's camera at it and tap the link, no Telegram needed. `--login-server 127.0.0.1:7777` makes the phone land on the login page with that server already filled in
(the Windows download's `Play from iPhone.bat` does this for the server on the same PC). The link changes
each time the game starts; the new one is posted to Telegram again, so the chat always has the current one.
The game has no console window, so what remote play has to say (the tunnel, Telegram) shows in the top
corner of the game window. A pasted token is cleaned up (a `bot` prefix, quotes or @BotFather's whole message
still work), a wrong one is reported plainly, and a group that Telegram turned into a supergroup is followed
to its new ID.

`--remote 9000` picks another port (8090 by default); `--remote-lan` also lets a phone on the same Wi-Fi
connect directly at `http://<computer's address>:8090/?k=…` without the tunnel. Remote play needs
`cloudflared` like the server's `--tunnel` (`winget install --id Cloudflare.cloudflared`).

### An Android app

`game/export_presets.cfg` builds an APK for 64-bit Android phones. Install the Godot 4.7.2 .NET export
templates and the Android SDK (platform-tools, build-tools 35, platform 35), point Godot's editor settings at the
SDK and a keystore, then:

```
GODOT_ANDROID_KEYSTORE_RELEASE_PATH=~/release.keystore GODOT_ANDROID_KEYSTORE_RELEASE_USER=me \
GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD=… godot --headless --path game --export-release Android ../build/tristinwold.apk
```

Godot's Android template runs .NET 9, so the game and its libraries also build for net9.0 (the server and tests
stay on .NET 10). The APK is about 210 MB: Godot and the .NET runtime are over half of that. Copy it to the phone,
open it, and allow installing apps from that source when Android asks.

### Playing from a phone, or from anywhere: a Cloudflare tunnel

Phones (and friends outside your home network) reach the server through a free Cloudflare quick
tunnel. You don't need port forwarding or a Cloudflare account. On the computer running the server:

1. Install cloudflared: `winget install --id Cloudflare.cloudflared` on Windows, `brew install cloudflared`
   on a Mac, or from [Cloudflare's downloads page](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/).
2. Start the server with `--tunnel` and a password:
   ```
   dotnet run --project tools/Tristinwold.Server -- --tunnel --password secret
   ```
   It prints a line like `Cloudflare tunnel open. On a phone (or anywhere), enter this as the server:
   https://seasonal-deck-organisms-sf.trycloudflare.com`.
3. On the phone, type that address and the password on the start screen.

**Telegram.** Set `TELEGRAM_BOT_TOKEN` (from @BotFather) and `TELEGRAM_CHAT_ID` before starting the server,
and it posts the tunnel's address to that chat when it opens, so you can tap it on your phone. It also
posts who arrives and leaves. Message your bot once first so it's allowed to write to you. On Windows:
```
set TELEGRAM_BOT_TOKEN=123456:ABC...
set TELEGRAM_CHAT_ID=5319394533
dotnet run --project tools/Tristinwold.Server -- --tunnel --password secret
```

The address changes each time the server starts. (For a fixed address, set up a named tunnel on your own
domain in Cloudflare and point it at `http://localhost:7778`.) Anyone with the address can try to join,
so always use `--password` with `--tunnel`. PCs can use the https address too, or the usual `host:7777`.

On a touch screen the game shows on-screen controls: a joystick wherever your left thumb lands, drag on
the right to look, and buttons for your class's three moves, Limit, Jump, Use (talk, pick up or tame),
Run, Map, Quests, World, Rest, Party, Skills, Ride and Dive. Pinch to zoom the map. `--touch` turns them on with a mouse, for trying
them out on a PC.

The game is built with Godot's C# support, which can't run in a web browser yet (Godot 4.7), so a phone
plays it as an installed app. That means an Android export (an `.apk`) or an iOS export (which needs a Mac
and an Apple developer account).

| Key | Does |
|---|---|
| WASD, Space, Shift | move, jump, sprint (sprinting uses stamina) |
| Mouse | look |
| Left / right mouse | your class's main attack / its signature move |
| Q | your class's third move (a wizard or sorcerer switches between fire, frost and shock) |
| R | limit break, once the gold bar is full |
| E | talk to someone, pick something up, or tame a youngling |
| K | skills: spend the points your levels bring |
| P | your party: companions, feeding them, sending them home or calling them |
| V | climb onto your dragon, or down (Dragon Knights only); in the saddle Space climbs and C dives |
| M | the map (scroll to zoom, drag to pan) |
| J | your journal of quests |
| T | rest (the world moves on, companions mend, and a dragon egg warms) |
| F10 | the shared world: contract board, companies, deviants, chronicle and chat |

### Sound and music

The game has sound (`game/scripts/Sound.cs`). The music follows what's happening: a grand theme on the start
screen, wandering tunes in the wilds, market tunes in the city, an inn tune in the Gallows Perch and the Salt
Barrel, the Bard's Tale at the Bards' College, battle music when something comes for you (it rings on a few
seconds after), and a boss theme for named foes, deviants, grown dragons and raids. Tunes cross-fade into each other.
Under it, the sound of the place: birds and breeze in the wilds by day, crickets, frogs and an owl at night (by
your device's clock), the city's murmur, hoofbeats and a distant forge, waves and gulls on the docks, wind up high
and on dragonback, drips in caves, a crackling fire in the taverns.

Effects come from where they happen, louder near and on the side they're on: footsteps on stone, boards, grass
or snow, a jump and a landing; blades whooshing and landing (harder for a critical), punches, crossbow bolts
loosing and striking; fire whooshing and booming, frost freezing and shattering, lightning crackling (thunder
for the big ones), venom bubbling, holy light chiming, a heal, the rising roar of a limit break; growls big and
small, howls, shrieks, dragons roaring, creatures dying, and the hero grunting when hurt. Bards strum, pluck,
drum, sound a horn, chant, turn pages and scratch with a quill. Menus click; coins clink; herbs rustle; the
anvil rings; potions pour; a level up and a quest have their own fanfares.

Volumes (all sound, music, ambience, effects) are on the login page and in the game menu (Esc, then "Sound and
music"), kept on the device. Playing from a phone, the sound comes too: the game catches what it plays and sends
it along with the picture (one channel, 22 kHz, about 22 KB a second). A phone won't play a web page's sound until
it's touched, so it starts with your first tap; the speaker button at the top right turns it off and on (and the
iPhone's silent switch silences it). The PC plays it as well.

### Classes

Twelve classes, three to each of four callings (warriors, rogues, mages and bards), each fighting its own way (`src/Tristinwold.Gameplay/Classes.cs`). Pick
one when you make a character; learn another later from its teacher at the guilds outside Tristinwold.

| Class | Left | Right | Q | R (limit) | Always |
|---|---|---|---|---|---|
| Martial Artist | Flurry: punches, every third a kick | Flying Kick | Inner Focus: heal, haste | Hundred Fists | Evasion: slip blows |
| Barbarian | Cleave: all in front | Leap Slam | Rage | Whirlwind | Thick Hide: more health, wounds close |
| Paladin | Smite: sunlit sword | Holy Light: heal all, sear foes | Shield Wall: Protect and Shell | Divine Judgement | Devotion: harder to hurt, light burns the dead and corrupted |
| Ranger | Shoot: crossbow, 35 m | Volley: four foes | Call of the Wild: beasts healed and fierce | Rain of Bolts | Beastmaster: always tames, beasts grow faster |
| Thief | Quick Stab | Pilfer: steal as you strike | Smoke Bomb: foes lose you, next strike critical | Grand Heist | Light Fingers: more crowns, free running |
| Assassin | Stab: often critical | Shadowstep behind a foe | Poisoned Blades | Death Mark | Backstab: huge harm to foes not fighting you |
| Wizard | Elemental Bolt | Elemental Blast | Attune: fire, frost, shock | Meteor | Arcane Mastery: stronger spells |
| Warlock or Witch | Eldritch Blast | Drain Life | Summon Familiar | Soul Harvest | Dark Pact: kills restore you |
| Sorcerer | Arcane Missiles | Chain Lightning | Attune | Dragonfire | Wild Surge: spells sometimes go off twice |
| Poet | Barbed Verse: golden words that cut, 22 m | Rousing Ode: the company healed, quickened, fiercer | Mocking Couplet: foes nearby forget you | Epic of Ages | The Muse: stronger spells; kills give back mana and stamina |
| Scribe | Inkbolt: crackling ink, 24 m | Glyph of Warding: a rune circle that bursts under a foe | Scroll of Protection: Protect, Shell, Regen | Words of Unmaking | Marginalia: each blow on the same foe hits harder |
| Musician | Discord: a jarring chord, every fourth twice as hard | Thunderous Chord: a wave of sound that throws foes back | Battle Hymn: the company quickened and healing | Crescendo | Perfect Pitch: mana and stamina come back quickly |

Kills and jobs bring experience. Each level adds health, mana and stamina and a skill point; spend points (K)
to rank a move up to 5, each rank 15% stronger. Joining another class gives the points back.

**What spells and blows look like** (`game/scripts/Effects.cs`). Fireballs trail flame and grey smoke and burst
in embers; ice shards trail mist and shatter into splinters; lightning doesn't fly, it strikes, a jagged
crackling arc that jumps about, and throws more arcs where it lands; venom drips as it flies and leaves a
hanging green cloud; sunlight bursts in a shaft of light and rising motes; every burst swells with light and
sends a ring along the ground. Breath rolls smoke, mist or a green cloud off it, and crackles if it's lightning.
A heal is a ring of light at the feet, rising motes and a fading column. Where a weapon lands there's a bright
arc where it swept through and sparks thrown along the blow, gold and bigger for a critical. Figures have a soft
rim of light and a matte finish, they crouch and spring to jump and bend their knees to land, and the view
widens a little at a sprint. `--shots DIR --spells` photographs each of them, slowed down.

**Limit breaks.** The gold bar under stamina fills as you fight (hits taken and given); when it's full a toast
says so, and R (Limit on a phone) lets it go: a pillar of light and a shockwave in the class's colour, the
world slowing for a heartbeat, the camera shaking, and the move's name across the screen. What each does:
Hundred Fists, eight blows in a heartbeat on the foe in front; Whirlwind, a spin striking everything around
you, hard; Divine Judgement, sunfire on every foe around you and every companion healed whole; Rain of Bolts,
a storm of bolts on your target and everything near it; Grand Heist, a strike on every foe around you and
their purses emptied; Death Mark, one foe, one blow of terrible force; Meteor, a falling star on your target
and all around it; Soul Harvest, every foe around you drained at once; Dragonfire, fire poured over
everything in front of you; Epic of Ages, golden words searing every foe around you and every companion healed
whole; Words of Unmaking, a great rune circle under you that strikes every foe in it and leaves them lost;
Crescendo, a song in three swelling beats, every foe around you struck by each.

**Bards** carry what KayKit doesn't make, built from simple shapes: the Poet wears a laurel wreath and reads
from an open book, the Scribe a quill and a closed one, the Musician a lute. Their songs are notes of light
drifting up and rings of sound pulsing out along the ground; a Thunderous Chord is rings of sound racing
forward; a Scribe's glyph is a turning circle of runes written in light that bursts.

**Peoples.** A hero is born to one of seven peoples, each with a gift and a cost (shown on the character page):
humans learn 15% faster and pay 10% less in shops; orcs (green, tusked) have more health and hit harder with
weapons but less mana, and townsfolk charge them more; goblins (small, big-eared) are quick and strike true
more often but are frail; elves (pointed ears) have deep mana and stronger spells but less health and armour;
dwarves (short and broad) are armoured and tireless and shrug off fire and poison but are slower; half-giants are
huge, tough and hard-hitting but slow and poor at magic. Dragonkind are scaled, horned and tailed in the colour
of their dragon (red, blue, green, black, white, gold, silver, bronze, copper or brass): its breath hardly hurts
them and they're tough and hit hard, but its bane hurts them more and people fear them. A dragonkind can be pure,
or half of any other people (a half-dragon dwarf, elf, orc...): half of each people's gifts and costs, that
people's look with the dragon's horns, tail and some of its scales. Also on the character page: build (slim,
average, broad), a beard (stubble, short, long, forked, braided) and its colour, and horns.

**Your look.** On the character page a hero's look is picked apart from their class. Their head, body, arms
and legs can each come from any of the nine KayKit figures (knight, barbarian, mage, rogue, hooded rogue and
four skeletons: they share one skeleton, so any mix stands and moves), with something on the head (a knight's
helmet, horned hat, wizard's hat, an old war helm, a ragged hood, a lich's hat), clothes and skin colours and
a height. They turn on a little stage as you step through the choices; "Surprise me" throws a look together,
"Class look" puts the class's own back. The class decides only what they carry. Characters already made have
a Look button. Gear worn over it (a helm, armour) still shows.

### The guilds

Four guilds keep yards just outside Tristinwold's walls (`src/Tristinwold.World/Guilds.cs`), each with a
leader, a teacher for each of its three classes, members with their own stories, and a rival:

| Guild | Where | Leader | Teachers | Members | Rival |
|---|---|---|---|---|---|
| The Iron Circle (warriors) | a yard outside the west gate | Marshal Brannoc Steelhand | Master Yun Tsavo (Martial Artist), Hrothga Ironmane (Barbarian), Ser Edwyn Halloran (Paladin) | Sergeant Tamsin Brack, Odo Fennick, Greta Vosk | Corin Vale |
| The Velvet Knives (rogues) | the old tannery south of the walls | Vesper Quill | Wren Ashcombe (Ranger), Pip Marlowe (Thief), Silas Morrow (Assassin) | Nell Cutwater, Jory Ash, Mags Dunmore | Sable Rue |
| The Magi (mages) | the Archive Garden north-east of the walls | Archmage Selwyn Tarrow | Magister Orla Penhallow (Wizard), Morwen Hesk (Warlock or Witch), Daro Vey (Sorcerer) | Brother Anselm, Liesl Varro, Teodric Gale | Isadora Crane |
| The Bards' College (bards) | the Lyre Hall on a green south-east of the walls | Laureate Odalys Wren | Cassian Vell (Poet), Mother Hollis Quell (Scribe), Bram Tolliver (Musician) | Tobin Marrow, Elowen Sark, Hal Brandish | Fennick Lark |

- **Your guild** is your class's: you start as an Initiate and rise to Member, Adept, Veteran and Master by what
  you do for it (shown beside your level). The leader tells you where you stand.
- **Teachers.** Your class's teacher gives a lesson once a level (a skill point, for a fee) and sets your
  class's trials. Any other teacher will teach you their class instead.
- **Class trials.** Each of the twelve classes has its own three trials (`src/Tristinwold.World/ClassQuests.cs`): clear
  a lair the way your class would, bring back the relic of your art from where it was lost (the Scroll of the Open
  Hand, the Bloodhorn of the Old Clans, the Reliquary of Saint Aubrel, the Greywood Bow, the Lockbreaker's Picks,
  the Black Mercy, the Tome of the Three Fires, the Hollow Candle, the Dragonblood Pearl, the Laurel of Maren Ashe,
  the Unwritten Codex, the Lute of the Long Night), and beat your guild's
  rival in a duel. Pass all three for the class's title (Oathsworn of the Tide, Prince of Pockets, Dragonblood, Laureate of Tristinwold...)
  and two skill points.
- **Rivals.** Corin, Sable, Isadora and Fennick want everything you want. Spar with them whenever you like: duels are to
  the yield, and nobody dies. They fight as their class, as strong as you are, and stronger the hotter the rivalry
  runs; what they say to you depends on how it's been going. Every so often you hear what they've been up to.
- **Drama.** As time passes and you rise, trouble comes to you: someone in your guild wants a word. A snapped
  sword and an accused recruit, a Tong bribe, a fight for First Blade; a skimmed take, a war with the Lantern
  Boys, a betrayal, the Velvet Crown; a missing book about the Rift, a vial of corrupted dragon's blood, a wager
  for a council seat, and what Sister Ilse keeps on a shelf that isn't on any list; a stolen song, a Tong
  pamphlet full of lies, the Midwinter Contest, and the last verse of the Laureate's history of the city. Each choice changes how the
  guild sees you, how your rival burns, and what people say about you after; some are settled with a duel.

### Lairs and raids: mini bosses in the wilds

Nine lairs stand out in the wilds, each with a mini boss and its own that come at you first
(`src/Tristinwold.World/Raids.cs`; the places in `game/scripts/LairSites.cs`, the fight in `game/scripts/RaidLair.cs`,
the bosses' moves in `game/scripts/BossBrain.cs`, their bodies in `game/scripts/Lairfolk.cs` and `Cinder.cs`). The
creatures come from the *Aerois: Year One* bestiary sheet, built on the game's own models.

| Lair | Where | Levels | Its own | Boss | Moves |
| --- | --- | --- | --- | --- | --- |
| The Ashen Crypt (burned crypt) | west, past the Iron Circle's yard | 1-6 | cinder thralls | the Cinder Warden: a skull burning in a mane of fire | flame burst, hurled fire, calls the dead |
| The Goblin Warren (cave) | south-east | 3-8 | goblins | the Goblin King: huge, crowned, spiked club, firepots | firepots on marked ground, calls goblins, tantrum |
| The Rock Bear's Den (cave) | north-west | 4-9 | wolves | the Rock Bear: stone grown over its back | ground slam, hurled boulder, rage |
| The Briar Ruin (ruin) | south | 5-10 | spriggans | the Briar Mother: walking briar in red bloom | spore burst, thorn volleys, spawns spriggans |
| The Hollow Bastion (fort) | east road | 7-12 | ghost soldiers | the Hollow Captain: a knight of moonlight | shield charge, spear volleys, calls the legion |
| The Amethyst Hollow (cave) | far north-east | 8-13 | crystal rats | the Crystal Basilisk: amethyst grown through it | crystal spikes from marked ground, shard bolts, more giant rats |
| The Sunken Forge (dungeon) | far west | 9-14 | goblin tinkers | the Forge Golem: furnace chest, chimney head, hammer | hammer quake, slag, molten drops, overheats |
| The Gloaming Ruin (ruin) | far south-east | 10-15 | shadow wolves | the Duskprowler: a shadow-cat veined with violet | vanishes and pounces from behind, more shadows, rage |
| The Storm Eyrie (crag) | far north-west, high | 12-17 | stormwings | the Thunderhawk: storm-blue, lightning in its feathers | lightning on marked ground, thunderclap, dive, calls stormwings |

- Come close while a lair is awake and its own come out; put them down and the boss comes. Every big blow is shown
  before it lands: the boss gathers itself, or the ground it's about to strike glows. Watch, and move.
- The boss grows with you within its levels. Below them, it meets you at its own level anyway. Its hoard pays
  experience and crowns, double the first time, along with a title (Warden-Breaker, Kingtoppler, Stonebreaker,
  Thornbane, Gatebreaker, Shardbreaker, Furnace-Quencher, Dawnbringer, Stormcaller). Then the lair falls quiet and
  wakes again 20-40 minutes (of play) later. Walk away mid-fight and everything goes back to waiting.
- The tracker shows the nearest lair that suits your level; the map marks every one that's awake.
  `--shots ./shots --raid` photographs the crypt; `--shots ./shots --lairs` (or `--lairs goblin_warren,storm_eyrie`)
  tours the others.

#### When a boss beats you: a deviant is born

Every lair boss starts out a nameless one of its kind. **If it beats you, it takes a name** (in the deviant roster's
style: "Mokak the Grinning, the Goblin King"), grows stronger, and **leaves its lair to roam the world**
(`game/scripts/Roamers.cs`). It wanders the wilds on its own path, resting and moving on, never into a town. Come
within sight of it and it's there, and it knows you, and it comes for you. Every time it beats you again it grows
stronger still. Kill it and it's gone for good, and the price on its head is yours: more than its hoard, and more
for every time it beat you.

#### Deviants hunt you, and learn how you fight

It isn't only lair bosses. **Anything out in the wilds that puts you down** (a bandit, a frost troll, a wolf, a
dragon) stands over you, **takes a name** ("Vorn the Patient, Frost Troll") and roams from where it beat you, a
good deal stronger than its kind. Lair creatures and anything inside a town don't; up to eight roam at once, and the
least of them moves on when a ninth rises.

- **It hunts you.** Come within about 85 m and it sees you, says so, and **comes after you**. Close by it runs you
  down; further off it follows your trail across the wilds at its own pace. Stand and fight, or run: get far enough
  ahead and it loses you, but it **remembers that you ran**, and it's quicker on your trail every time. The tracker
  shows who's hunting you, how far off, and which way.
- **Towns aren't always safe.** Run into a town or village and it stops at the walls, unless **it thinks it can
  kill you there**: the more often it has beaten you, the more hurt you are and the more it has learned of you, the
  bolder it is, and every watchman near you gives it pause. If it dares, it comes in after you, and if the Watch
  closes in around you it thinks better of it and makes for the walls.
- **The town fights for you** (`game/scripts/Watch.cs`). **Tristinwold's Watch** stands at its posts (the Watch
  House, both gates, the Exchange, the Salt Barrel, the Warrens, the docks) and walks its beat; when a deviant comes
  into town, or something out of the wilds chases you through the streets, the Watch comes running (a fallen
  watchman's post is filled again in a couple of minutes). And the **townsfolk don't just stand there**, in
  Tristinwold or any village: smiths, dockhands, miners and anyone carrying steel go at it, most throw whatever's to
  hand, the priests and healers patch you up, and the young and the old take cover. The crowd wears it down but
  leaves the killing to you and the Watch; if the Watch brings it down with you, the price on its head is still
  yours.
- **It learns how you fight.** Every blow you land teaches it: what kind of damage, spell or weapon, from close or
  from far off, and every potion you drink to stay up. Once it has taken enough, **it fights back with that, there
  and then**, and tells you so:
  - **The damage you lean on** (half of everything you hit it with): it learns to take it, and it does half as much.
  - **Fighting from a distance**: it learns to close the gap, with a quicker step and a bound across the ground at you.
  - **Fighting up close**: a harder hide.
  - **Mostly spells**: it learns to shrug them off.
  - **Drinking potions to stay up**: when you're low, it goes for the kill.
  - **Running away**: quicker on your trail.
  It never forgets: what it has learned is saved with it, so the next time you meet it, it's ready for you. Mix up
  how you fight it and it has less to learn.

The lair doesn't stay empty: within a minute and a half **a new nameless boss has moved in**, no stronger than its kind (the one that beat you is out roaming, a deviant of its own, wherever its road takes it). Roamers are saved with
your company. In the shared world the server names it (the boss that beat you is the one the deviant system
promotes), so it's on everyone's roster under the same name, its fights count there, and if it cheats death there
it comes back roaming, scarred and angrier.

### A living world

(`game/scripts/TownLife.cs`, `Skylife.cs`, `Watch.cs`; `src/Tristinwold.World/TownLife.cs` and `Wildlife.cs`.)

- **Animals people keep.** Farmsteads keep cows, sheep, pigs, hens, a horse and a dog; hamlets and towns have sheep,
  hens, dogs and cats; inns their horses; Tristinwold its dogs, cats, hens and the Watch's horses. They stand about
  with the people who keep them. Kill one where its owners can see and you pay for it (and Tristinwold hears of it).
  Sheep give wool (the loom makes linen of it), cows meat and hide, hens feathers.
- **Wild horses** run the autumn woods and the tundra in herds; their foals can be tamed and raised.
- **Life overhead and in the water.** Eagles circle over the wilds; songbirds forage in the woods and scatter when
  you come close; fish swim in the water near you; and whales roll at the surface in the mouth of the harbour, for
  anyone on the docks to see.
- **Food to forage.** Raspberries, penny buns, wild garlic and flax in the autumn woods; bilberries and chanterelles
  under the pines; wild hives in the trees (the bees don't like a novice). Cook berry tarts, mushroom stew (restores
  mana), honey cakes and roast meat; spin flax and wool into linen.
- **The guilds' initiates are about town on their first errands.** The Velvet Knives' young thieves lift purses in
  the streets, the townsfolk's and **yours** if they get close (the better a thief you are, the more likely you
  catch the hand in your purse); strike a thief who has your purse and they drop it, and if the townsfolk or you see
  them at it, the Watch runs them down. The Iron Circle's initiates go bouts with one another, the Magi's
  apprentices work their cantrips, the College's busk for whoever'll listen.
- **Crime, and the Watch.** Now and then a Velvet Knife comes for a hero who's made a name ("The Velvet Knives send
  their regards"), and muggers work the docks and the Warrens. The Watch goes after any thief or cutthroat in the
  streets and pays for each one brought down.

`--selftest --living-only` walks through all of it. The animals are Quaternius's CC0 packs from OpenGameArt
(converted by `tools/creatures/quaternius.py`).

### Stranger things in the wilds

Out past the safe country (`src/Tristinwold.World/Wildlife.cs`; bodies in `game/scripts/Wildfolk.cs`, tricks in
`game/scripts/WildBrain.cs`):

- **The kipine**, alone in the autumn and pine woods: an owlbear gone to the sky, an owl's pale mask for a face,
  a stag's antlers, great leathery wings. It beats up into the air and comes down on you hard, and it snatches your
  coin to hoard; kill it and you get the coin back.
- **Deathcrest apes**, in packs in the marsh, the pine woods and the crags: pale, banded dark, ridged horns
  sweeping back, a blue snout. They blink through shadow to your flank and bite with poison, and they eat the dead
  that walk: they go after any draugr or skeleton they see.
- **Dire imps**, in the marsh and the crags, each with a band of lesser **imps** at its heels: red-brown devils
  with swept horns, bat wings, a barbed tail and burning blue eyes. A dire imp vanishes and strikes from beside you
  with burning claws; an imp, badly hurt, turns into a raven and flies off. Imps also flit about the autumn woods.
- **Winged apes**, up among the rocks and in the pine woods and tundra: black-furred, black-winged, a greatsword in
  the fist. They hurl rocks at anyone who keeps away, then drop on them from above.

Kipines and both kinds of ape sometimes have young with them: find one alone and you can raise it. Imps never.
`--shots ./shots --wild` takes their portraits.

### Loot, gear, trades and the market

(`src/Tristinwold.Gameplay/Items.cs` and `Trade.cs`, `src/Tristinwold.World/Herbs.cs`; in the game `Trading.cs`,
`Wardrobe.cs`, `Market.cs`.)

- **Loot.** Everything you kill leaves what it would: wolves their pelts and fangs, bears their pelts and claws,
  deer hides and antlers, goblins scrap iron and trinkets, the dead bone dust and old coins, kipines feathers and
  the shiny things they hoarded. **Dragons** leave an armful of their scales (in their colour), dragon bone, often
  their heart, and a pile of gold. A **lair's hoard** adds its boss's trophy and, the first time (and now and then
  after), a piece of gear you can't get anywhere else: the Goblin King's crown, the Warden's cinder axe, the
  Hollow Legion's shield, the Duskprowler's mantle, an amethyst staff, a stormcaller's staff. A roaming deviant
  carries the best of its lair.
- **The pack (I).** See what you carry, wear gear and drink potions; **H** drinks your best healing potion.
- **Gear you can see.** Weapons, shields, helms and hats, body armour and cloaks. Every adventurer shares one
  skeleton, so any class can wear any of it: a knight's helmet on a wizard, a barbarian's greataxe on a ranger.
  Steel shines, dragonbone is pale, dragonscale armour takes its dragon's colour, the cinder axe glows with embers.
  Two-handed weapons take both hands (and swing like it).
- **Herbalism.** Herbs grow wild by the kind of land (emberleaf and lady's mantle in the autumn woods, bluebells
  and ghostcap under the pines, reeds and nightshade in the marsh, snowberry and frostbloom in the north, thistle
  and silverthorn on the crags); pick them (E) and they grow back in twelve minutes of play. Brew healing,
  mana, stamina, might and warding potions with an alchemist's kit or at Old Saffron's bench. The better you
  get, the more you pick and the more you can brew.
- **Smithing.** At Hulda Ironhand's forge: smelt scrap into iron and iron with coal into steel, tan hides into
  leather, and forge iron, steel and in the end dragonbone weapons and dragonscale armour, and cloaks and armour
  from what the wilds leave you.
- **The market** on the Gallows Perch road, right where you start: Hollis's Provisions (food, meat for your
  beasts, cloth, leather, coal, a kit), the Anvil Gate (Hulda's smithy: ingots and iron gear), and Saffron's
  Remedies (potions, herbs, wands and staves). Every shopkeeper buys at half today's price (see the economy below).

### The years go by: growing up and growing old

**The calendar:** a day passes every ten minutes of play, a week is seven days (a little over an hour), a month four
weeks, and a year twelve months: 336 days, about 56 hours of play. The months run Thawmoon, Seedmoon, Bloommoon,
Highsun, Goldmoon, Haymoon, Harvestmoon, Fallmoon, Mistmoon, Frostmoon, Deepwinter and Longnight. The skills window (K)
says the date.

**Everyone ages, every people its own way**, through five stages: **young, adolescent, adult, seasoned, elder**.

| People | Adolescent | Adult | Seasoned | Elder | Lives to about |
|---|---|---|---|---|---|
| Goblin | 6 | 9 | 20 | 30 | 40 |
| Orc | 10 | 14 | 30 | 45 | 60 |
| Human | 13 | 18 | 40 | 60 | 80 |
| Half-giant | 14 | 20 | 45 | 70 | 95 |
| Dwarf | 20 | 40 | 120 | 200 | 260 |
| Dragonkind | 25 | 60 | 200 | 400 | 600 |
| Elf | 40 | 100 | 300 | 500 | 700 |

The villages aren't all human: dwarves dig the mines and the crags, elves keep to the old woods, goblins and orcs
come out of the marsh and the hills, and now and then there's a half-giant. Each villager's age is in their own
people's years, and ask anyone and they'll say it ("Finn Saltbrand, scullion (16, adolescent human)"). Children are
small, adolescents a little bigger, elders grey. **The villages age with the calendar**: children grow into
adolescents and adults and take up grown work; **every time one of the young comes of age, a new child is born into
the family**; and the old pass on when their people's years are done. Every company sees the same people at the same
ages.

**An Immortal ages too, but never dies of it.** You woke at about twenty (in your people's years) and grow older
with every year that passes, through all five stages, and on, long past what any of your people live to: "143, an
elder human, 63 years past the oldest any human lives to. Immortals age; they don't die of it."

### Barbarian raiders on the frontier

Out past Tristinwold's fields (more than a kilometre from the walls), **barbarian clans** roam the frontier: "the Red
Axe clan" under "Hrodgar the Burner", four to seven raiders and a warchief. They're the real terror of the small
places (hamlets, farmsteads, hunting camps, inns and mines), which have **a couple of guards at most** and no Watch
to call: a town keeps three, a hamlet or a mine two, a farmstead, an inn or a hunting camp one. Each clan works its own
stretch of country: on the road for a while, camped out of sight, then down on one of its places for a raid, and on
to the next. Every company sees the same clan in the same place at the same time.

- **Come near and it's there.** On the road, you'll find it camped; at a raid, the clan is fighting the place's
  guards. The tracker names the nearest clan within a mile and a half, and what it's doing.
- **Break the raid** (bring down every raider, warchief included) and the place pays you from what you saved; the
  clan falls back to lick its wounds for an hour.
- **A raid nobody stops** on a place you know plunders it: its market loses nearly half of everything, word comes to
  the city, and you'll see it in the prices (and in what Tristinwold's caravans bring home from it).

### Trade beyond the walls: town markets and the caravan yard

**Every place you find keeps its own little economy.** A hamlet, a farmstead, a mine, an inn, a town: each makes a few
goods (cheap there, because there's plenty) by its country and its trade, and is short of a few (dear there). Ask
anyone who lives there what it trades in. Sell it what it lacks and it pays well, but every load fills it up and the
price falls; left alone, its stores drift back a little each day. Once a place is known, its market is remembered with
your company, and **Tristinwold's own caravans start trading with it**: every day the Exchange hears of a caravan from
one of the places you've found, bringing what it makes and taking home what it wants, and Tristinwold's stores move
with it.

**The caravan yard** stands right up against the city wall south of the west gate, where the Watch can see it (it
has its own post). It's a busy place: wagons standing waiting for teams and drivers, goods piled everywhere, three
traders' stalls along the wall, a spice trader and an old harness-maker. Garrick Wain keeps it. Three ranks of work:

1. **Loader.** Lift goods from the stack onto the yard's wagon (E, then E at the wagon), four crowns a load. Ten
   loads and he makes you a driver.
2. **Driver.** He gives you a wagon and a horse and a run along one of the routes: Tristinwold's goods out to a town you've found, the town's goods home.
   The wagon follows you; half the pay when you get there, half when you're back. A loaded wagon on the road draws
   bandits.
3. **Entrepreneur.** After three runs he'll sell you **a wagon of your own** (300 crowns). Hitch your own grown
   horses to it, or buy a draught horse from him (140): each horse is room for a dozen loads, two at most. Buy goods at
   the Exchange, take them where they're wanted, buy what that place makes cheap, and bring it home to sell. The
   tracker keeps count of what's aboard, and Garrick of what you've made on the road.

**Gluts.** Sell a village a hundred fish and fish there is worth next to nothing: the village is *drowning* in it.
Whenever that happens anywhere along the routes (or in Tristinwold itself), word comes to the yard, and Garrick has
work for a driver: take the wagon to where it's piled up, load it for next to nothing, and cart it to the known place
that will pay most for it. The pay is half the difference in price, plus the road. A trader with their own wagon can
just buy up a glut where it lies (any glutted good is for sale, not only what the place makes) and sell it on.

**Guarding the city's caravans.** Tristinwold's caravans run along every trade route. It has always traded with its
**two nearest market towns**, which are on the routes (and on your map) from the start; beyond them, **the trade
routes are the places you've found, and only those**: each village, farmstead, mine or town you discover adds one.
**Guarding a caravan is one of the starter jobs**: it's on your tracker from the first day, and Garrick gives a new
hero, whatever their level, the city's wagon along the nearest route (the short road, one ambush of two bandits).
**The short road is repeatable**: it's on his list every day, for anyone. The longer roads are for adventurers of level
3 and up. He offers a caravan along each route, paying by the road: the further, the
more, and more again the more of a name you've made, with more bandits waiting on the longer roads (one ambush on a
short road, up to three on a long one). The carter drives herself at a walk, round water; she pulls up when there's
trouble or when you fall behind. Bandits at the wagon break into the load, and if it's all gone the caravan is lost.
Bring it through and you're paid (a quarter more if it's hardly scratched), and a big load goes each way between the
place and Tristinwold.

**Riding horses.** A horse that's grown (level 6, so a wild foal you've tamed and raised, or a draught horse from the
yard) can be ridden: stand by it and press V. Shift gallops. Horses stay on the ground; only dragons fly.

### Work in town, and the city's economy

(`src/Tristinwold.Gameplay/Gathering.cs` and `Economy.cs`, `src/Tristinwold.World/Herbs.cs` for the wild ore; in
the game `TownWork.cs`.) Four more trades, each levelling from 1 to 10 like herbalism and smithing (K shows them all):

- **Fishing.** Buy a rod from Bryn's Fish Stall (south of the Gallows road) or Hollis's, face open water (the
  piers, the shore, a lake or river) and press E to cast. Wait for the float to go under, then press E again,
  quick. The harbour gives herring, mackerel, cod, sea bass, eels, lobster and salmon (and now and then a pearl);
  lakes and rivers give perch, carp, trout, pike and, rarely, a golden carp; the cold north gives char and ice
  pike. Better anglers wait less, have longer to strike, and land the rarer fish. Sometimes it's a boot.
- **Mining.** At the Greyhollow Quarry west of the walls, Dagna Flint sells pickaxes and buys stone and ore. Its
  veins give grey stone, iron ore and coal, and a silver seam for skilled miners; out on the crags and in the cold
  north there are outcrops of iron, coal, silver and gold. A vein takes a few strokes (fewer as you get better),
  sometimes gives a rough garnet or sapphire, and comes back in eight minutes. A forge smelts two ore into an
  ingot (iron, silver or gold).
- **Woodcutting.** At Hollin's Woodlot south-west of the walls, fell the trees with a red band (pine; oak from
  level 2; yew from level 4) with a woodcutter's axe. They fall, leave a stump, and grow back.
- **Cooking.** In the Salt Barrel's kitchen (Fenna Rook) or the Gallows Perch's (Mother Corva): grill any fish,
  roast venison (deer and boar now leave it), and as you improve, fish stew (harder to hurt for two minutes),
  smoked eel (mana), hunter's pie and a seafood platter fit for the Crown (heals fully, hits harder).
- **Day work.** Haskel Brine, the dock foreman by the east gate, pays by the crate: lift one at the end of the
  middle pier and carry it (slowly; no running) to the warehouse by the gate, six to a shift. Factor Ottilie
  Marsh at the Exchange has sealed letters to carry to people around town.

**The economy.** Tristinwold keeps a stock of thirteen goods: fish, meat, bread, ale, timber, stone, ore, coal,
iron, leather and hides, herbs, potions, and silver, gold and gems. Every day (ten minutes of play) its boats,
farms, quarry, mine and woodlot add to them, its people use them up, the smelters turn ore and coal into iron and
the apothecaries herbs into potions, and merchants bring in what's short and ship out a glut. Every shop's price
for those goods follows the stock: scarce is dear (up to two and a half times), plentiful is cheap (down to under
half), and the shop lists say so. Everything you sell or buy moves it: sell sixty herring at once and herring gets
cheap; a storm empties the fishmongers. Things happen for a few days at a time: a storm keeps the boats in, the
herring run, the mine floods, a caravan brings iron and silver, the harvest festival drinks the taverns dry,
bandits stop the farm carts, marsh fever empties the apothecaries, a fire takes the lumber yard, a ship goes on the
slips, a dragon takes the cattle, a good harvest comes in. Townsfolk talk about it.

At the Exchange, **Factor Ottilie Marsh** has the market report (every price, which way it's going, what's
happening, the treasury), **contracts** for whatever's short (a business paying over the odds for fish, timber,
ore, potions... delivered to her, paid as it comes in, with a bonus for the lot), and the **city's works**: mend
the harbour wall, shore up the mine, new ovens for the bakers, lamps and drains for the Warrens, a new wing for the
Exchange, a shipyard. The city puts its taxes and spare stock into them each day; your goods and coin speed them
along, and each one finished makes a trade better for good (more fish, ore, bread; more taxes; better dock pay).
Everything you do for the city (contracts, works, letters, dock work) builds your name at the Exchange, from a
stranger to a known trader, a trusted supplier, a pillar of Tristinwold and the Exchange's favourite: shopkeepers
take up to 8% off and pay a little more. The city is saved with your company (on a server, everyone shares one city: see
[Shared world](#shared-world-everyone-runs-their-own-party-of-three)). `--selftest --town-only` walks
through all of it; `--shots DIR --town` photographs the new places.

### Life beyond the sword

(`src/Tristinwold.Gameplay/Pursuits.cs` and `src/Tristinwold.World/Pursuits.cs`; in the game `Pursuits.cs`.) Seven
more trades, levelling 1 to 10 like the rest, and things to do that aren't fighting:

- **Farming.** Gilly Thatch keeps the Commons Allotments, south of the walls, and sells seed. Take any empty bed
  (E) and plant potatoes, onions or cabbages, or, as you get better, herb seed: emberleaf, bluebells, bloodcaps,
  frostbloom, silverthorn. They grow while you play (six to fourteen minutes); you can watch them come up. Dig them
  up when they're ripe; better growers get more.
- **Firemaking.** A tinderbox (Hollis sells them) and a log: light a fire anywhere outside town (from the pack,
  I). Cook at it, or rest by it out of a fight and your wounds close. Better logs burn longer; better hands catch
  first time.
- **Agility: the Tumblers' Run.** Cole Sparrow's obstacle course west of the walls: the balance log, the climbing
  net, the rope swing over the pit, the stepping stones, the wall, and the ladder and slide. Take them in order for
  a lap and a bonus; fall off (it hurts, a little) and the lap starts over. Every level makes you a percent quicker
  everywhere.
- **Thieving.** Any grown townsfolk can have their pocket picked (talk to them). Commoners are easy and carry
  little; merchants more; the Watch and the gentry are harder, and carry better. Get caught and you're fined, the
  Exchange thinks less of you, and they keep a hand on their purse for a while. Rogues and thieves are better at it.
- **Trapping.** Buy a snare from Hollis, set it out in the wilds (from the pack), and come back in a few minutes:
  rabbits and their pelts, game birds and feathers, martens in the pines, snow hares in the north. Better trappers
  watch more snares at once.
- **Crafting.** Ilka Brightwater, the jeweller by the Exchange, lends her bench and loom: silver and gold rings and
  amulets (a garnet or sapphire, or a pearl, for the best), worn in two new slots, and linen tunics and padded
  jerkins.
- **Building: a house of your own.** Buy the plot at Hearthside, north of the walls, from Factor Marsh (250 crowns),
  saw planks on it (and Hulda makes nails), and build it stage by stage: foundation, frame, walls, roof, door and
  windows. You can watch it go up. Then furnish it: a bed (sleep in it to wake healed and well rested, a tenth more
  experience for half an hour), a chest to keep things in, a garden of two more beds, a hearth to cook at, and a
  trophy wall (each lair trophy hung makes your blows a percent harder, up to ten). Carve a home charm to come home
  from anywhere, every five minutes.
- **Treasure hunts.** Bandits and thugs sometimes carry a torn map. Read it, and it names a landmark (a village, a
  farmstead, the gates of a city) and how far from it to dig, and which way; dig there with a spade for the next
  scrap, two to four of them, and a chest at the end. Dig in the wrong place and you're told how warm you are.
- **Feats.** Thirty-one of them, for all of it: your first fish and your hundredth, a lap of the Run, a purse
  lifted (and getting caught), a treasure dug up, a house finished, five contracts filled, a hundred crates
  carried, a trade mastered, ten trades at five. The game menu and the skills window (K) list them, with your
  points.

Everything is saved with your company. `--selftest --pursuits-only` walks through all of it.

### Companions

- **Younglings.** About a third of the herds and packs in the wilds have young with them: wolf pups, bear cubs,
  fawns, fox kits, boarlets, troll whelps, goblin whelps, spriggan saplings, mud crab hatchlings, and now and
  then a dragon wyrmling. They're small and they run. Find one with none of its grown kin close by (they won't
  let you near it, and they'll come for you) and press E: a little meat helps, and a ranger never fails. It
  follows you from then on, levels up from the fights it shares, grows to full size, and learns moves
  (guarding you, a savage strike, a rally that heals you). Beasts get hungry: hunting gives meat, and you feed
  them from the party menu (P). One that starves weakens and collapses.
- **Squires and apprentices.** Every town has young people (11 to 16) who dream of adventure. Once you've made a
  name (level 3), ask one to follow you, with 25 crowns to their family: a warrior's squire, a mage's
  apprentice, a rogue's protege, a bard's understudy (with a lute of their own). They grow up at your side and learn Shield Bash and Battle Cry, or Firebolt,
  Mend and Ward.
- **Wren's Menagerie.** At the end of the market row by the Gallows Perch, Wren Halloway sells young beasts
  she raised by hand, tame already: a fox kit or fawn (25 crowns) up to a troll whelp (1155), with giant rat
  kits, hawk chicks, mud crab hatchlings, wolf and ice wolf pups, boarlets, goblin and deathcrest whelps,
  winged ape infants, spriggan saplings, bear cubs and kipine chicks between. The price goes by how
  dangerous it grows up (danger, 1 to 5) and how hard it is to train (training, 1 to 5). Never a dragon: a
  dragon has to be found as an egg and hatched. She sells meat too, and tells you how taming works.
- **Beast taming** is a trade, like herbalism and smithing: taming a youngling in the wild, buying one from
  Wren, feeding your beasts and their levelling up all teach it. Each level past the first makes a wild
  youngling 3% likelier to take to you, your beasts learn 5% faster, and Wren charges 3% less. It shows in
  your pack (I).
- Up to four follow you at once; the rest wait at the Gallows Perch. A companion knocked down gets up when the
  fight is over, and resting heals them all.

### The Dragon Knight

Any class can become one, **and it's earned**. Nobody tells a new hero about it. The Order of the Eyrie keeps its
rookery on the hill west of Tristinwold, and **every day one of its riders comes in over the wilds and lands there**
(you'll see it from the walls); rest a while, and go again. Go and see what's happening, or don't: that's on you.
Ser Aldric Brasswing doesn't hand out eggs. First he wants you on your own feet (level 5) and willing to do the work:
**feed his dragons ten meat you've hunted yourself**. Then he tells you, roughly, which way the lair of the nearest
metallic ancient that keeps a pact with people lies ("somewhere to the north-east"), and no more: **no map, no
flight there**. The tracker shows no spot until you've found it for yourself. Rift-corrupted drakes are tearing at the
clutch. Take an egg and keep it warm: it hatches as you carry it, quickest when you rest. The hatchling is the
size of a cat, hungry, and fragile: until it can fly, if it falls it is lost, and you must start again. Fight
beside it until level 10 (it learns Claw Rake, then its colour's Breath), bring it to Ser Aldric and kneel. From
then on it carries you (V): on the ground, and in the air (Space climbs, C dives, the attack button is its
breath). It keeps growing: large at level 20, giant at 30.

**Or win a wyrmling over in the wilds.** Dragon flights out in the wilds often have a wyrmling with them (about one
in three). Like any youngling it can't be taken while its kin are close, but a wyrmling is no fox kit: it's clever and
proud, and **it weighs you**. It won't so much as look at a hero below level 8. After that your years in the wilds
(level) and your way with beasts (beast taming) count for most, meat for little, and a ranger's knack helps but
doesn't decide it; the odds never go better than even. Turn one down and it remembers you for ten minutes, and screams
for its kin. Win one over and raise it to level 10, and Ser Aldric will knight you for it all the same, no egg needed.

### Corrupted dragons, pacts and the Dragonwatch

- **Corrupted dragons.** The Rift has twisted some dragons of every colour. A corrupted dragon keeps its own
  colour, bled into a vivid violet and veined with molten gold that pulses; its eyes burn violet-white, its wings
  are its own membrane turned violet on bones of gold, and a violet haze rises off it. Corrupted dragons are
  tougher and hit harder, and they attack everything living, dragons of every colour included. About one ancient
  in seven has fallen, and all its kin with it; a few dragons of any flight have turned.
  `tools/creatures/dragon_corruption.py` paints their hides.
- **The Rift spreads.** Every wound a corrupted dragon deals another dragon carries the corruption in; enough of
  them (about two thirds of its health) and that dragon turns. It takes on the corrupted look, grows tougher,
  throws any knight riding it, forgets its pact or its flight, and flies into a frenzy for 45 seconds: faster,
  harder-hitting, striking at whatever is nearest, people included. The corruption ebbs away if a dragon gets
  clear of the fight in time, and a fight against corrupted dragons can turn the defenders into raiders. Your
  own dragon can be taken too: it turns on you, and beating it down breaks the Rift's hold and gives it back to
  you. A paladin's Holy Light eases the corruption in their companions; Divine Judgement clears it.
- **Pacts.** Some metallic ancients keep a pact with the nearest town or village in their land. Their dragons
  guard people rather than hunting them, and the town has a Dragonwatch: dragon knights on the ancient's
  dragons, circling over it. Knights and pact dragons attack chromatic and corrupted dragons on sight.
- **Raids.** Some seasons (every three hours, real time) a band of chromatic or corrupted dragons raids a pact
  town, and its knights fly out to meet them. Ser Aldric hears of it.

You start outside the Gallows Perch, in the outer ring. Thugs work the docks, giant rats infest the Warrens,
the Lantern Boys keep their den, a draugr and a sea-wraith haunt the Drowned Chapel, and the Drowned Hand
hold the Tong sea-caves. A thug who kills you remembers it. Next time it comes back
named, stronger and taunting, and Vann may send it hunting for you. Fall, and you wake at the Gallows Perch.

### The world beyond the walls

The world has no edge, and there is nothing to find at one. **What happens as you walk out:** the ground streams in
64 m chunks around you, about 450 m in every direction, built ahead of you as you go and dropped behind you, all grown
from the world's seed, so the same hill is always in the same place. Villages, camps, barrows and dens are worked out
for every 320 m square as you come near it; a new city for every 2.4 km square past the nine. **Past the nine lairs
near home** (about 2.4 km out), the far country has lairs of its own, one in most 1.6 km squares, for ever: a crypt,
a warren, a forge, a dead fort, an eyrie, each under its own name ("the Thorned Hold", "the Manse of Coldton"), each
harder and better-paid the further out it lies, so a hero who has outgrown home always has somewhere to go. The
tracker suggests the nearest that suits you, near home or out there. The game lets go of a far lair's buildings once
you're well past it, but **every lair you've found stays on your map** (a violet triangle), and every place you've
found keeps its market. A deviant born out there roams from where it beat
you, not back towards Tristinwold. (The self-test walks 31 km out to check.)

Tristinwold sits at its centre, and you start knowing five cities:
Tristinwold, Cinderhelm to the north, Galeholt and Mirewatch to the north-west, and Gravenmoor to the west.
They show on your map with a "?" until you get there. The other four reworked cities (Argentdeep,
Vespergate, Duskwake, Hollowreach) are further out, waiting to be found. Past all nine, the generator
keeps making new cities as far as you care to walk, each with its own name, epithet, ruler, trouble and rumour.

- **Fog of war.** The map is black until you go somewhere. Walk north and it draws itself in behind you.
  Places you reach are named on it; places you have only heard about get a "?".
- **The land changes as you go.** Autumn woods around Tristinwold, pinewoods, marshes and lakes, then
  tundra, crags and snowfields to the north. Danger rises with distance, so enemies far from home hit harder.
- **Wilderness settlements** sit between the cities: hamlets, farmsteads, hunting camps, roadside inns,
  shrines, mines and Ashborn refuges. There are also bandit camps, barrows full of draugr, and beasts' dens
  (wolves, bears, trolls, spriggans).
- **Dragons.** Ten colours: red, blue, green, black and white (chromatic: they hunt people) and gold, silver,
  bronze, copper and brass (metallic: they leave you be unless you strike first). Out in the wilds, ancient dragons
  hold great territories, each with a name and title ("Korvegarde the Quiet Guardian, an ancient silver dragon"),
  a lair guarded by giants, and smaller dragons of their colour roaming their land. You're told when you cross
  into one's land, and villagers gossip about the nearest. Every three hours (real time, the same for every
  company) neighbouring ancients may go to war, chromatic against metallic most often, and their dragons fight at
  the border like warring tribes, sometimes led by the ancients themselves. Dragons come in four sizes (normal,
  large, giant, ancient), and a dragon deviant grows a size each time it beats a party or rises a rank. All but the
  white (frost) dragon have great wings (`DragonWings.cs`): folded along the back on the ground, beating in flight.
  They circle over their land, fly in to a distant fight and land to finish it, and take to the air to flee. The
  frost dragon is wingless and fights on foot, with a ridge of translucent ice spikes grown down its back and
  flanks and icicles at its jaw (`IceRidge.cs`), sized to the dragon. `--lineup ./shots --dragons --frost`
  photographs a normal, large and giant frost dragon side by side.
- **Wildlife.** The land between them is lived in. Deer herds graze and foxes prowl almost everywhere,
  and they bolt when you get close; hawks wheel overhead. Away from home, wolf packs and wild boars roam the
  woods, goblin bands the woods and crags, bears and trolls the crags,
  ice wolves the snow, giant rats the marshes and mud crabs the shores. Packs grow larger the further north
  you go; far in the north a frost dragon is sometimes seen. Within about 450 m of Tristinwold nothing hunts
  people. Wipe out a herd or a pack and that stretch
  stays quiet for ten minutes. Every company meets the same animals in the same places.
- **People.** Everyone in a city or village is generated: a name, an age, a job, a temper, a backstory
  (where they came from, what happened to them, what they want now) and two or three interests. Ask them
  who they are, what they care about, whether they have heard any news (which marks a new place on your
  map), and whether they have work.
- **Quests.** About half of them have a job for you, picked from what they care about and what is nearby.
  You might clear a lair, fetch something from a barrow or bandit camp, carry a letter to someone in
  another village, or find a missing relative. Quest targets go on your map; the journal (J) and the
  tracker on screen tell you how far and which way.

Every company sees the same world, because it all grows from the `seed` in `world/world.json`. Change the
seed for a different world. Each company's map, quests and crowns are saved on its own computer, in
Godot's user folder under `saves/`.

**Saving, logging out and quitting.** The game saves by itself every 20 seconds, after anything that
matters (a quest, a hoard, a level), when you close the window and when a phone switches away. It keeps
where you stood, so the next session starts there. **F5** saves now. **Esc** (or **Menu** on a phone, under
the little map; Android's back button too) closes whatever is open, else opens the game menu: Save now,
Change character, Log out (back to the login page, which then asks for the password), and Quit. A logged-in
character's save lives on the server; logging out, changing character and quitting wait for the server to
say it has the save, and ask before going on if it doesn't answer. During remote play the menu has no Quit
(the phone would close the game on the PC), and logging out keeps the phone's link.

`--lineup ./shots` photographs the twelve classes; add `--creatures`, `--young` (younglings and companions),
`--dragons` (all ten colours), `--dragons --corrupt`, `--dragons --riders` or `--dragons --frost`. `--shots ./shots --skirmish`
photographs a dragon knight and a corrupted dragon fighting over the meadows by a village.

The sky is a photograph (Poly Haven's "Kloofendal 48d Partly Cloudy (Pure Sky)", CC0), and the world is lit by
it: ambient light and reflections come from the sky, and the sun shines from where it stands in the picture.
On High and Ultra there is also light bouncing off the ground (SSIL), shafts of sunlight through the fog, eyes
that adjust to bright and dark, and softer, sharper shadows; Ultra adds reflections on the water and a little
haze-blur in the far distance, and the meadows grow two and a half (High) or three and a half (Ultra) times
as much grass, in drifts and patches. `tools/polyhaven/sky.py` finds the sun in a sky photograph.

Self-test (headless, exits 0 on success; `--company-only` runs just the classes, companions and Dragon Knight):

```
godot --headless --path game -- --selftest --server off                       # combat, exploring and one quest of each kind
godot --headless --path game -- --selftest --server 127.0.0.1:7777 --company Joe   # plus a deviant returning
godot --path game -- --shots ./shots --server off    # a screenshot tour of the world (needs a display; add --touch for the phone layout)
godot --headless --path game -- --selftest --server https://….trycloudflare.com --company Joe --password secret   # through a tunnel
```

## The Deviant system

- An enemy that **kills you** becomes a named **Ringleader**. One you **run from**, or one that **flees from
  you**, becomes a named **Stray**.
- Each deviant gets strengths and weaknesses: health, armour, damage, speed, spell power, crit chance,
  fire/frost/shock/poison/sun resistances (or absorbing them), plus auto-statuses (Haste, Protect, Shell,
  Regen, Reflect).
- It **remembers** who it killed, who ran and who beat it, per player, and taunts you by name.
- It **comes back**: the next time you fight its kind, especially on its home ground, it can take one of
  those enemies' place, renamed and stronger.
- **Killing it isn't always final.** It can come back scarred, and resist whatever killed it.
- **It hunts and it learns.** In the game, a deviant out in the wilds hunts you once it sees you, and learns how you
  fight it (your damage, your range, your spells, your potions, your escapes) and adapts to counter you
  (`src/Tristinwold.World/Tactics.cs`).
- **Ranks**: one Tyrant over the Scourges, each with Ringleaders under them, and the Strays below, with
  promotions, grudges fought out between deviants, and pacts (kill one of a pact, and the other comes for you).

Names, epithets and taunts are data (`NamePack` in `src/Tristinwold.Deviant/Content.cs`).

### Try the rules without the game

```
dotnet run --project tools/Tristinwold.DeviantSim -- 80 7
```

## The world: nine cities

Each city has its own name, places, story and contracts. In the game each one stands at its `at` position
in `world/world.json` (metres; x east, z south). Tristinwold is where the main saga happens, in every
campaign; the other cities have stories of their own, with threads back to it.

| City | Its story |
|---|---|
| **Tristinwold**, the City of Rings | A walled port of three rings, run from below by the Tongs. The first person each company saves here is Vann. (The main saga: see The Rings of Tristinwold.) |
| **Galeholt**, the Wind-Fort on the Plain | Horse-lords on an open plain, ruled from the Eyrie. Two clans, the Ashmanes and the Ironsworn, feud over whether to ride to Tristinwold's aid or bar the gates. The Warden-Thane's new court adviser knows too much about the coming war. |
| **Vespergate**, the City on the Arch | A widowed queen rules from the Sapphire Court above a sea-arch. Once a year, on Bell Night, the headsman frees one condemned prisoner and nobody knows how he chooses. This year he chose someone who should not exist yet. |
| **Cinderhelm**, the Hall of Cold Crowns | An old, proud city that walls its refugees into the Cinder Quarter. Since the Rift opened the refugees are Ashborn, half-demon children, and someone is killing them one by one while zealots call it holy work. |
| **Argentdeep**, the City Carved in Silver | A stone city cut into a cliff, owned by the Argent family's silver. The silver has started to rot into rust in the miners' hands, and the Argents are blaming the miners. |
| **Gravenmoor**, the Town of Graves | A logging town with more dead than living. Lately the dead aren't staying buried, and the gravekeeper has started bargaining with a voice in the pines. |
| **Mirewatch**, the Town in the Fog | Stilt houses in a marsh where time runs thin. Children walk into the fog and come back years older, or not at all. The Jarl's seer says it is a crack between eras. |
| **Duskwake**, the Sleepless Port | A cold harbor where the whole town dreams the same dream every night: a door, and faces they have never met. Nobody sleeps well, and the sleepless are starting to do terrible things. |
| **Hollowreach**, the Edge of the North | Half the town fell into the sea long ago. What stands is the Temple of the Loom, where the grey-robed Magi guard the device that can send people back in time. They say the town fell when someone used it carelessly. |

Each city's own contracts go on the shared board next to Tristinwold's. Campaign 3, The Last Ring, is
Tristinwold 300 years on, ruined down to the Crown.

Characters written by hand:

- **Mother Corva**, of The Gallows Perch. The Ravenscrew Tong's barkeep at the Gallows Perch, brought from Argentdeep's forges.
- **Sister Ilse Varrow**, of The Magi Archive. Head archivist of the Magi's quiet embassy in Tristinwold, brought from the Temple of the Loom.
- **Perrin Velk**, of The Last Toast. Galeholt threw him out. Gravenmoor took him in, against its better judgement.

**Everything is in [`world/world.json`](world/world.json)**: cities, their places, stories and contracts, and
the characters. Edit it and restart the server; the game reads the same file.

## Shared world: everyone runs their own party of three

This is "The Scattered Key" from *The Rings of Tristinwold*. Each friend plays their own game, with their
own company, at their own pace. One server, usually run by the DM, holds the city they all share:

- **One deviant world.** A monster that wipes Joe's party in his game can turn up in Mira's game and taunt
  her about it ("I killed Joe. You're next.").
- **One city economy.** Tristinwold's market is the server's, not each game's own: when Joe sells sixty herring,
  fish is cheap for Mira too. Stock and prices, the day, what's happening (a storm, the herring run, a caravan),
  the Exchange's contracts and the city's works are all shared, so companies can fill a contract or build the
  harbour wall together, and everyone hears when it's done. The city's days run on the server's clock while
  anyone is playing (ten minutes a day). Each hero keeps their own name at the Exchange, and the discounts that
  come with it. Play alone, or away from the server, and your game runs its own copy of the city as before.
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
- **Contract pay** arrives in crowns when you mark a contract done. **Resting** (saving)
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
- **Sending deviants.** He tells a deviant that already hates you where to find you, and it gets stronger.
- **The one thing he can't stop:** two Keystones meeting.

**Claude voices him** if the server machine has an Anthropic API key. Set `ANTHROPIC_API_KEY` before
starting it and every line is written fresh, in character, by Claude (low effort, a
couple of sentences per line; a few cents per hour of play). If a line is ever declined, a fallback
model voices it. Without a key, or if the call fails, he uses his written lines, so he always talks.

### Run the server

Anyone can host, including someone who's playing; the window doesn't show Vann's lines or private
messages.

```
set ANTHROPIC_API_KEY=sk-ant-...        (optional: Claude voices Vann; PowerShell: $env:ANTHROPIC_API_KEY="...")
dotnet run -c Release --project tools/Tristinwold.Server -- --password <something> --campaign tristinwold.json
```

Games connect over TCP **7777**; phones and browsers over HTTP **7778**. It saves everything to `tristinwold.json` after every change, so stopping and
starting it loses nothing. For friends outside your house, forward port 7778 on your router to this PC,
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
deviants                    living deviants
save                       save now
```

### Join (each company)

On the game's login page, type the server's address (`192.168.1.20:7777`, or a Tailscale address), a
company name and a password, and create an account the first time.

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows or Linux:

```
dotnet test                              # combat, open world, deviant rules, server, HTTP gateway
cd game && dotnet build && godot --headless --path . -- --selftest --server off   # the Godot game, played by a script
```

## Credits

Photographs: [Poly Haven](https://polyhaven.com) (CC0), in `game/assets/polyhaven`. Eight scanned ground
textures are blended by biome, slope, shoreline and settlement (`Graphics.Ground`). The buildings wear scanned
stone, plaster, weathered wood, slate, clay tile and thatch. Trees are built in code (`Foliage.cs`) from scanned
bark, with branch and leaf cards cut from Poly Haven's fir and island-tree scans; their own tree models run to
millions of triangles. Boulders, rocks, fallen logs, stumps, barrels, crates, lanterns, a fire pit, ferns and a
chest are Poly Haven scans that Blender cut down to a few thousand triangles each.
`tools/polyhaven/fetch.py` (textures and cards) and `tools/polyhaven/models.py` (models; needs `pip install bpy`)
rebuild all of it.

Buildings are generated by `game/scripts/Buildings.cs` and drawn by `BuildingMaterials.cs`: stone footings, timber
frames with plaster panels, jettied upper floors, chimneys, leaded windows with shutters, plank doors, and roofs
of clay tile, slate, shingle or thatch. They use the photographs above, or shaders with no texture files when
the photographs are missing. Taverns, longhalls, barns, smithies,
market halls, towers, churches, castle keeps, windmills, wells, city walls, gatehouses, piers, lanterns, a
lighthouse and ruins come in seeded variants. `godot --path game -- --showroom ./shots` photographs every kind.

Sound: every piece of music, ambience and sound effect is CC0, from OpenGameArt, in `game/assets/audio`; its
`CREDITS.md` names each track and sound, its author and its page. A few were made for this game and are CC0 too:
the city and docks ambience are mixed from several recordings, and some bard instruments (a plucked lute phrase
and strum, a harp glissando, two drums, a horn call and a sung chord) are synthesised.

Other 3D models: the KayKit packs by Kay Lousberg (www.kaylousberg.com): Adventurers, Skeletons, Medieval Hexagon,
Dungeon Remastered and Halloween Bits. They are CC0 (free for any use) and live in `game/assets/kaykit`.
Creatures (`game/assets/creatures`, see its CREDITS.md): wolves, ice wolves, foxes, brown and black bears, wild
boars and hawks come from 0 A.D. by Wildfire Games (art CC-BY-SA 3.0, so the converted models carry the same
licence), each with its own idle, walk, run, attack and death; `tools/creatures/zero_ad.py` converts them from a
checkout of github.com/0ad/0ad. Giant rats, goblins, forest monsters (spriggans), trolls and the frost dragon
come from OpenGameArt: CDmir's rat, goblin and forest monster and Drummyfish/Cethiel's dragon are CC0, and
piacenti's Troll Mauler is CC-BY 3.0 (`tools/creatures/oga.py` converts them). The deer is Polygonal Mind's (CC0),
its legs walked in code. Mud crabs are still built from shapes. The KayKit characters are re-proportioned as
they move (`Proportions` in `Models.cs`): a smaller head, a slimmer, taller trunk and longer arms and legs, so they
stand like people (and their stiff capes are left off). `godot --path game -- --lineup ./shots` photographs them
(add `--plain` for the originals).
