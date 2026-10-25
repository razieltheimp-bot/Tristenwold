# Tristinwold

A multiplayer game for *The Rings of Tristinwold*. It runs on its own, as a **standalone Godot 4 game**
that needs no copy of Skyrim. A Skyrim add-on is the second way to play. Final Fantasy-style mechanics
come along: Haste, Protect, Shell, Regen, Reflect and limit breaks today, with summons planned. Leveling
works like Skyrim's: skills and perk trees, no sphere grid.

| Part | Status |
|---|---|
| **The game** (`game/`, Godot 4 .NET) | Playable: the three-ring city of Tristinwold and an endless world around it. Buildings are generated in code (timber-framed and stone houses, taverns, churches, keeps, walls); the ground, buildings, trees, rocks and props are photo-textured from Poly Haven scans; characters come from the KayKit packs. Sword, spells, limit break, Tong lairs, nemeses that come back, a fog-of-war map, villages and camps, people to talk to and quests to do, the shared-world board. A scripted headless test plays it against the real server. Open/CC0 art to come. |
| **The open world** (`src/Tristinwold.World`) | Built and tested. Grows the land, the cities, the villages, the people and their quests from one seed, with no edge. |
| **Combat rules** (`src/Tristinwold.Gameplay`) | Built and tested: health/magicka/stamina, Skyrim-style armor, resistances, auto-statuses, the limit gauge, the bestiary, fight tracking. |
| **Nemesis rules** (`src/Tristinwold.Nemesis`) | Built and tested. Uses Skyrim actor kinds, actor values and damage types. |
| **Shared world server + Vann** (`src/Tristinwold.Shared`, `tools/Tristinwold.Server`) | Built and tested end to end. Game-agnostic. |
| **The world** (`world/world.json`) | Nine reworked cities with their own names, places, stories and contracts; characters moved between them. |
| **Tristinwold.esp builder** (`skyrim/esp-builder`) | Built and tested against a stand-in Skyrim.esm. Renames every city's places in game and moves characters. |
| **Skyrim add-on** (`skyrim/plugin`, a SkyrimPlatform plugin) | Built: fights, nemeses, Vann's hunts, contract board, dead drops, F10 window. Tested against the real server in a simulated game; not yet run in Skyrim. |
| **Story, character creator, summons, co-op inside a company** | Planned. |

## Play without Skyrim (the Godot game)

You need [Godot 4.7 .NET](https://godotengine.org/download) and the .NET 10 SDK. Start the server
(see *Shared world* below), then each company runs:

```
godot --path game -- --server 192.168.1.20:7777 --company Joe --password secret
```

Without `--server`, the game opens on a login page: the server's address and password, then your account
name and password (**Create account** the first time). Then pick one of your characters or make a new one:
a name, a colour and a starting spell. On a server, your characters and their progress (map, quests,
septims) belong to your account, so you can play the same character from your phone and your PC. Nobody
else can join as your character. The game stays logged in on that device (it keeps a login token, not
your password). **Play alone** keeps characters on the device instead. You can also open `game/project.godot`
in the Godot editor and press Play.

From the command line, `--account Joe --secret yourpassword --company "Bram Ashmane"` logs in, makes the
account and character if they're new, and plays. The server keeps accounts in `<campaign>.accounts.json`,
next to the campaign file, with passwords stored only as salted PBKDF2 hashes.

**Graphics** (on the login page, or `--graphics low|medium|high|ultra`). The ideas come from the Skyrim and
Oblivion graphics projects (Community Shaders' lighting, OptiScaler's upscaling, the water and vegetation
overhauls), done with Godot's renderer, since those mods only run inside those games:

| | Low | Medium | High | Ultra |
|---|---|---|---|---|
| View distance | 256 m | 384 m | 448 m | 576 m |
| Water with waves, depth tint and shore foam; trees in the wind | | ✓ | ✓ | ✓ |
| Grass around you | | | ✓ | ✓ (wider) |
| Shadows | | ✓ | soft | soft, further |
| Ambient occlusion, volumetric fog, AgX tone mapping | | | ✓ | ✓ |
| Indirect light (SSIL), AMD FSR 2 anti-aliasing | | | | ✓ |

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

It prints a link like `https://….trycloudflare.com/?k=3f9a…` and shows it at the top of the game window; with
`TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` set it also sends the link to your Telegram. Open it in Safari,
turn the phone sideways, and play. In Safari, Share → **Add to Home Screen** makes it open full screen like an
app. The ⌨ button on the left opens the phone's keyboard for typing names and passwords (or start the game
with `--account`, `--secret` and `--server` so there's nothing to type). A Bluetooth keyboard plays with the
usual keys. The `k=` part of the link is its key: only people with the whole link can watch or play.

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
| F10 | the shared world: contract board, companies, nemeses, chronicle and chat |

### Classes

Nine classes, three to each calling, each fighting its own way (`src/Tristinwold.Gameplay/Classes.cs`). Pick
one when you make a character; learn another later from its teacher at the guilds outside Tristinwold.

| Class | Left | Right | Q | R (limit) | Always |
|---|---|---|---|---|---|
| Martial Artist | Flurry: punches, every third a kick | Flying Kick | Inner Focus: heal, haste | Hundred Fists | Evasion: slip blows |
| Barbarian | Cleave: all in front | Leap Slam | Rage | Whirlwind | Thick Hide: more health, wounds close |
| Paladin | Smite: sunlit sword | Holy Light: heal all, sear foes | Shield Wall: Protect and Shell | Divine Judgement | Devotion: harder to hurt, light burns the dead and corrupted |
| Ranger | Shoot: crossbow, 35 m | Volley: four foes | Call of the Wild: beasts healed and fierce | Rain of Bolts | Beastmaster: always tames, beasts grow faster |
| Thief | Quick Stab | Pilfer: steal as you strike | Smoke Bomb: foes lose you, next strike critical | Grand Heist | Light Fingers: more septims, free running |
| Assassin | Stab: often critical | Shadowstep behind a foe | Poisoned Blades | Death Mark | Backstab: huge harm to foes not fighting you |
| Wizard | Elemental Bolt | Elemental Blast | Attune: fire, frost, shock | Meteor | Arcane Mastery: stronger spells |
| Warlock or Witch | Eldritch Blast | Drain Life | Summon Familiar | Soul Harvest | Dark Pact: kills restore you |
| Sorcerer | Arcane Missiles | Chain Lightning | Attune | Dragonfire | Wild Surge: spells sometimes go off twice |

Kills and jobs bring experience. Each level adds health, magicka and stamina and a skill point; spend points (K)
to rank a move up to 5, each rank 15% stronger. Joining another class gives the points back.

### The guilds

Three guilds keep yards just outside Tristinwold's walls (`src/Tristinwold.World/Guilds.cs`), each with a
leader, a teacher for each of its three classes, members with their own stories, and a rival:

| Guild | Where | Leader | Teachers | Members | Rival |
|---|---|---|---|---|---|
| The Iron Circle (warriors) | a yard outside the west gate | Marshal Brannoc Steelhand | Master Yun Tsavo (Martial Artist), Hrothga Ironmane (Barbarian), Ser Edwyn Halloran (Paladin) | Sergeant Tamsin Brack, Odo Fennick, Greta Vosk | Corin Vale |
| The Velvet Knives (rogues) | the old tannery south of the walls | Vesper Quill | Wren Ashcombe (Ranger), Pip Marlowe (Thief), Silas Morrow (Assassin) | Nell Cutwater, Jory Ash, Mags Dunmore | Sable Rue |
| The Magi (mages) | the Archive Garden north-east of the walls | Archmage Selwyn Tarrow | Magister Orla Penhallow (Wizard), Morwen Hesk (Warlock or Witch), Daro Vey (Sorcerer) | Brother Anselm, Liesl Varro, Teodric Gale | Isadora Crane |

- **Your guild** is your class's: you start as an Initiate and rise to Member, Adept, Veteran and Master by what
  you do for it (shown beside your level). The leader tells you where you stand.
- **Teachers.** Your class's teacher gives a lesson once a level (a skill point, for a fee) and sets your
  class's trials. Any other teacher will teach you their class instead.
- **Class trials.** Each of the nine classes has its own three trials (`src/Tristinwold.World/ClassQuests.cs`): clear
  a lair the way your class would, bring back the relic of your art from where it was lost (the Scroll of the Open
  Hand, the Bloodhorn of the Old Clans, the Reliquary of Saint Aubrel, the Greywood Bow, the Lockbreaker's Picks,
  the Black Mercy, the Tome of the Three Fires, the Hollow Candle, the Dragonblood Pearl), and beat your guild's
  rival in a duel. Pass all three for the class's title (Oathsworn of the Tide, Prince of Pockets, Dragonblood...)
  and two skill points.
- **Rivals.** Corin, Sable and Isadora want everything you want. Spar with them whenever you like: duels are to
  the yield, and nobody dies. They fight as their class, as strong as you are, and stronger the hotter the rivalry
  runs; what they say to you depends on how it's been going. Every so often you hear what they've been up to.
- **Drama.** As time passes and you rise, trouble comes to you: someone in your guild wants a word. A snapped
  sword and an accused recruit, a Tong bribe, a fight for First Blade; a skimmed take, a war with the Lantern
  Boys, a betrayal, the Velvet Crown; a missing book about the Rift, a vial of corrupted dragon's blood, a wager
  for a council seat, and what Sister Ilse keeps on a shelf that isn't on any list. Each choice changes how the
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
| The Amethyst Hollow (cave) | far north-east | 8-13 | crystal skeevers | the Crystal Basilisk: amethyst grown through it | crystal spikes from marked ground, shard bolts, more skeevers |
| The Sunken Forge (dungeon) | far west | 9-14 | goblin tinkers | the Forge Golem: furnace chest, chimney head, hammer | hammer quake, slag, molten drops, overheats |
| The Gloaming Ruin (ruin) | far south-east | 10-15 | shadow wolves | the Duskprowler: a shadow-cat veined with violet | vanishes and pounces from behind, more shadows, rage |
| The Storm Eyrie (crag) | far north-west, high | 12-17 | stormwings | the Thunderhawk: storm-blue, lightning in its feathers | lightning on marked ground, thunderclap, dive, calls stormwings |

- Come close while a lair is awake and its own come out; put them down and the boss comes. Every big blow is shown
  before it lands: the boss gathers itself, or the ground it's about to strike glows. Watch, and move.
- The boss grows with you within its levels. Below them, it meets you at its own level anyway. Its hoard pays
  experience and septims, double the first time, along with a title (Warden-Breaker, Kingtoppler, Stonebreaker,
  Thornbane, Gatebreaker, Shardbreaker, Furnace-Quencher, Dawnbringer, Stormcaller). Then the lair falls quiet and
  wakes again 20-40 minutes (of play) later. Walk away mid-fight and everything goes back to waiting.
- The tracker shows the nearest lair that suits your level; the map marks every one that's awake.
  `--shots ./shots --raid` photographs the crypt; `--shots ./shots --lairs` (or `--lairs goblin_warren,storm_eyrie`)
  tours the others.

#### When a boss beats you: a nemesis is born

Every lair boss starts out a nameless one of its kind. **If it beats you, it takes a name** (in the nemesis roster's
style: "Mokak the Grinning, the Goblin King"), grows stronger, and **leaves its lair to roam the world**
(`game/scripts/Roamers.cs`). It wanders the wilds on its own path, resting and moving on, never into a town. Come
within sight of it and it's there, and it knows you, and it comes for you. Every time it beats you again it grows
stronger still. Kill it and it's gone for good, and the price on its head is yours: more than its hoard, and more
for every time it beat you.

The lair doesn't stay empty: when its timer runs out, **a new nameless boss has moved in**. Roamers are saved with
your company. In the shared world the server names it (the boss that beat you is the one the nemesis system
promotes), so it's on everyone's roster under the same name, its fights count there, and if it cheats death there
it comes back roaming, scarred and angrier.

### Companions

- **Younglings.** About a third of the herds and packs in the wilds have young with them: wolf pups, bear cubs,
  fawns, fox kits, boarlets, troll whelps, goblin whelps, spriggan saplings, mudcrab hatchlings, and now and
  then a dragon wyrmling. They're small and they run. Find one with none of its grown kin close by (they won't
  let you near it, and they'll come for you) and press E: a little meat helps, and a ranger never fails. It
  follows you from then on, levels up from the fights it shares, grows to full size, and learns moves
  (guarding you, a savage strike, a rally that heals you). Beasts get hungry: hunting gives meat, and you feed
  them from the party menu (P). One that starves weakens and collapses.
- **Squires and apprentices.** Every town has young people (11 to 16) who dream of adventure. Once you've made a
  name (level 3), ask one to follow you, with 25 septims to their family: a warrior's squire, a mage's
  apprentice, a rogue's protege. They grow up at your side and learn Shield Bash and Battle Cry, or Firebolt,
  Mend and Ward.
- Up to four follow you at once; the rest wait at the Gallows Perch. A companion knocked down gets up when the
  fight is over, and resting heals them all.

### The Dragon Knight

Any class can become one. Ser Aldric Brasswing keeps the Order of the Eyrie's rookery on the hill west of
Tristinwold, with two dragon knights and their dragons. He sends you to the nest of the nearest metallic ancient
that keeps a pact with people (his knights can fly you there), where Rift-corrupted drakes are tearing at the
clutch. Take an egg and keep it warm: it hatches as you carry it, quickest when you rest. The hatchling is the
size of a cat, hungry, and fragile: until it can fly, if it falls it is lost, and you must start again. Fight
beside it until level 10 (it learns Claw Rake, then its colour's Breath), bring it to Ser Aldric and kneel. From
then on it carries you (V): on the ground, and in the air (Space climbs, C dives, the attack button is its
breath). It keeps growing: large at level 20, giant at 30.

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

You start outside the Gallows Perch, in the outer ring. Thugs work the docks, skeevers infest the Warrens,
the Lantern Boys keep their den, a draugr and a sea-wraith haunt the Drowned Chapel, and the Drowned Hand
hold the Tong sea-caves. A thug who kills you remembers it. Next time it comes back
named, stronger and taunting, and Vann may send it hunting for you. Fall, and you wake at the Gallows Perch.

### The world beyond the walls

The world has no edge. Tristinwold sits at its centre, and you start knowing five cities:
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
  large, giant, ancient), and a dragon nemesis grows a size each time it beats a party or rises a rank. All but the
  white (frost) dragon have great wings (`DragonWings.cs`): folded along the back on the ground, beating in flight.
  They circle over their land, fly in to a distant fight and land to finish it, and take to the air to flee. The
  frost dragon is wingless and fights on foot, with a ridge of translucent ice spikes grown down its back and
  flanks and icicles at its jaw (`IceRidge.cs`), sized to the dragon. `--lineup ./shots --dragons --frost`
  photographs a normal, large and giant frost dragon side by side.
- **Wildlife.** The land between them is lived in. Deer herds graze and foxes prowl almost everywhere,
  and they bolt when you get close; hawks wheel overhead. Away from home, wolf packs and wild boars roam the
  woods, goblin bands the woods and crags, bears and trolls the crags,
  ice wolves the snow, skeevers the marshes and mudcrabs the shores. Packs grow larger the further north
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
seed for a different world. Each company's map, quests and septims are saved on its own computer, in
Godot's user folder under `saves/`.

`--lineup ./shots` photographs the nine classes; add `--creatures`, `--young` (younglings and companions),
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
godot --headless --path game -- --selftest --server 127.0.0.1:7777 --company Joe   # plus a nemesis returning
godot --path game -- --shots ./shots --server off    # a screenshot tour of the world (needs a display; add --touch for the phone layout)
godot --headless --path game -- --selftest --server https://….trycloudflare.com --company Joe --password secret   # through a tunnel
```

## Play in Skyrim (optional)

You need your own copy of Skyrim Special Edition on PC. Co-op runs on
[Skyrim Together Reborn](https://www.nexusmods.com/skyrimspecialedition/mods/69993); a solo player
with followers is a company too.

## Install in Skyrim (each company)

1. Skyrim SE/AE **1.6.1170**, with [SKSE](https://skse.silverlock.org/) and
   [SkyrimPlatform](https://www.nexusmods.com/skyrimspecialedition/mods/54909). For co-op, add
   Skyrim Together Reborn.
2. Build the add-on (or take a built copy): `cd skyrim/plugin && npm install && npm run build`.
3. Copy `skyrim/plugin/dist/Platform` into Skyrim's `Data` folder, and build and enable
   `Tristinwold.esp` (see "Build Tristinwold.esp").
4. Edit `Data/Platform/Plugins/tristinwold-settings.txt` (plain JSON):

   | Setting | Meaning |
   |---|---|
   | `server` | the Tristinwold server's HTTP address, e.g. `http://192.168.1.20:7778` |
   | `company` | your company's name, shown to everyone |
   | `password` | the server password, if it has one |
   | `connect` | `true` on one game per company. In a Skyrim Together party, only the leader connects; set `false` on the others |
   | `menuKey` | key that opens the Tristinwold window, as a DirectInput scan code (68 = F10) |

5. In game, **F10** opens the board, companies, nemesis roster, dead drops and chronicle. Sleeping
   advances the shared calendar.

What happens in a fight: when enemies turn on you, the add-on asks the server whether one of them is
a nemesis. If so, it's renamed, strengthened (Skyrim actor values: health, armor, damage, speed,
resistances) and given its Final Fantasy statuses (Haste, Protect, Shell, Regen, Reflect), and it
taunts you. When the fight ends (you go down, they all die, or someone runs), the result goes back
to the server. Vann can also send a nemesis to hunt you; it spawns out of sight behind you.

**Not yet tested in Skyrim itself**, only against the real server with a simulated game
(`npm run e2e` in `skyrim/plugin`, with a server running). Things to check on first launch: the
F10 window opens, a lost fight creates a nemesis, and the same enemy type later shows up renamed.
SkyrimPlatform and Skyrim Together haven't been confirmed to run together; if they conflict, the
add-on still works solo.

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

## The world: every city reworked

Every Skyrim city is reworked into a city of this world, with its own name, its own renamed
places and its own story. In the Godot game each one stands at its `at` position in `world/world.json`
(metres; x east, z south), laid out roughly as Skyrim's holds lie around Riften. Tristinwold (built from Riften) is where the main saga happens, in
every campaign; the other cities have stories of their own, with threads back to it.

| City | Built from | Its story |
|---|---|---|
| **Tristinwold**, the City of Rings | Riften | A walled port of three rings, run from below by the Tongs. The first person each company saves here is Vann. (The main saga: see The Rings of Tristinwold.) |
| **Galeholt**, the Wind-Fort on the Plain | Whiterun | Horse-lords on an open plain, ruled from the Eyrie. Two clans, the Ashmanes and the Ironsworn, feud over whether to ride to Tristinwold's aid or bar the gates. The Warden-Thane's new court adviser knows too much about the coming war. |
| **Vespergate**, the City on the Arch | Solitude | A widowed queen rules from the Sapphire Court above a sea-arch. Once a year, on Bell Night, the headsman frees one condemned prisoner and nobody knows how he chooses. This year he chose someone who should not exist yet. |
| **Cinderhelm**, the Hall of Cold Crowns | Windhelm | An old, proud city that walls its refugees into the Cinder Quarter. Since the Rift opened the refugees are Ashborn, half-demon children, and someone is killing them one by one while zealots call it holy work. |
| **Argentdeep**, the City Carved in Silver | Markarth | A stone city cut into a cliff, owned by the Argent family's silver. The silver has started to rot into rust in the miners' hands, and the Argents are blaming the miners. |
| **Gravenmoor**, the Town of Graves | Falkreath | A logging town with more dead than living. Lately the dead aren't staying buried, and the gravekeeper has started bargaining with a voice in the pines. |
| **Mirewatch**, the Town in the Fog | Morthal | Stilt houses in a marsh where time runs thin. Children walk into the fog and come back years older, or not at all. The Jarl's seer says it is a crack between eras. |
| **Duskwake**, the Sleepless Port | Dawnstar | A cold harbor where the whole town dreams the same dream every night: a door, and faces they have never met. Nobody sleeps well, and the sleepless are starting to do terrible things. |
| **Hollowreach**, the Edge of the North | Winterhold | Half the town fell into the sea long ago. What stands is the Temple of the Loom, where the grey-robed Magi guard the device that can send people back in time. They say the town fell when someone used it carelessly. |

Each city's own contracts go on the shared board next to Tristinwold's. Campaign 3, The Last
Ring, is Tristinwold 300 years on, ruined down to the Crown; that's planned as later changes to
Tristinwold.esp.

**Characters move between cities.** Any named Skyrim character can be taken from their city
and set down in another, under a new name if you like:

- **Ghorza gra-Bagol** goes to The Ragged Flagon as **Mother Corva**. The Ravenscrew Tong's barkeep at the Gallows Perch, brought from Argentdeep's forges.
- **Colette Marence** goes to Riften Hall of the Dead as **Sister Ilse Varrow**. Head archivist of the Magi's quiet embassy in Tristinwold, brought from the Temple of the Loom.
- **Nazeem** goes to Dead Man's Drink. Galeholt threw him out. Gravenmoor took him in, against its better judgement.

A moved character's original placement is disabled, a new one is made in the destination
interior beside something already standing there, and their daily routine becomes "wander
around where you stand", so they don't walk home. The builder warns when a character is
essential to a quest, since moving them may break it.

**Everything is in [`world/world.json`](world/world.json)**: cities, places (Skyrim name -> new
name), stories, contracts and moves. Edit it, then rebuild Tristinwold.esp and restart the
server. Both read the same file, so a company without the plugin still sees the new names in
the shared world.

What it doesn't change: NPCs still say Skyrim names in recorded dialogue, and a few signs are
painted textures. If a place doesn't rename in game, its in-game name differs from the file;
the builder lists everything it renamed and the server window shows the names the game reports.

### Build Tristinwold.esp (once, on your PC)

```
dotnet run --project skyrim/esp-builder -- "C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data"
```

It reads your own `Skyrim.esm`, writes `Tristinwold.esp` (light, ESL-flagged) into that Data
folder and lists every rename and move, with any it had to skip. Enable it in your mod manager.
Everyone in the game needs the same file.

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

The Skyrim add-on connects over HTTP **7778** (TCP 7777 is for tools). It saves everything to `tristinwold.json` after every change, so stopping and
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
nemeses                    living nemeses
save                       save now
```

### Join (each company)

See "Install in Skyrim" above. The add-on talks to the server's HTTP port (7778 by default).

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows or Linux:

```
dotnet test                              # combat, open world, nemesis rules, server, HTTP gateway
cd game && dotnet build && godot --headless --path . -- --selftest --server off   # the Godot game, played by a script
cd skyrim/plugin && npm install && npm test && npx tsc --noEmit   # add-on logic and types
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

Other 3D models: the KayKit packs by Kay Lousberg (www.kaylousberg.com): Adventurers, Skeletons, Medieval Hexagon,
Dungeon Remastered and Halloween Bits. They are CC0 (free for any use) and live in `game/assets/kaykit`.
Creatures (`game/assets/creatures`, see its CREDITS.md): wolves, ice wolves, foxes, brown and black bears, wild
boars and hawks come from 0 A.D. by Wildfire Games (art CC-BY-SA 3.0, so the converted models carry the same
licence), each with its own idle, walk, run, attack and death; `tools/creatures/zero_ad.py` converts them from a
checkout of github.com/0ad/0ad. Giant rats (skeevers), goblins, forest monsters (spriggans), trolls and the frost dragon
come from OpenGameArt: CDmir's rat, goblin and forest monster and Drummyfish/Cethiel's dragon are CC0, and
piacenti's Troll Mauler is CC-BY 3.0 (`tools/creatures/oga.py` converts them). The deer is Polygonal Mind's (CC0),
its legs walked in code. Mudcrabs are still built from shapes. The KayKit characters are re-proportioned as
they move (`Proportions` in `Models.cs`): a smaller head, a slimmer, taller trunk and longer arms and legs, so they
stand like people (and their stiff capes are left off). `godot --path game -- --lineup ./shots` photographs them
(add `--plain` for the originals).

Earlier FFX prototypes drew on Fahrenheit, the FFX-AP Archipelago client, FFX_TAS_Python, Farplane,
UnX, VBFExtract and FFXView. None of that code ships here any more.
