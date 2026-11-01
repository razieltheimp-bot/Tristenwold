using System.Text.Json.Serialization;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>What kind of place a lair is, which decides how it's built: a burned crypt, a cave in a heap of rock, a
/// ruin gone to the wild, a dungeon sunk into the ground, a broken fort, or an eyrie up on the heights.</summary>
public enum LairSite { Crypt, Cave, Ruin, Dungeon, Fort, Eyrie }

/// <summary>
/// The moves a lair's boss can have, besides its ordinary blows.
/// <list type="bullet">
/// <item><see cref="Burst"/>: it gathers itself for a moment, then strikes all around it (step away in time).</item>
/// <item><see cref="Bolt"/>: one hurled at anyone who keeps their distance.</item>
/// <item><see cref="Volley"/>: three at once, at a distance.</item>
/// <item><see cref="Marks"/>: the ground is marked where the hero stands, then struck there (keep moving).</item>
/// <item><see cref="Pounce"/>: it vanishes and comes down behind the hero.</item>
/// <item><see cref="Charge"/>: it rushes at a hero who keeps away, bowling over whatever is in its path.</item>
/// <item><see cref="Summon"/>: as it weakens, it calls more of its own.</item>
/// <item><see cref="Enrage"/>: near the end, it goes wild: faster and harder-hitting.</item>
/// </list>
/// </summary>
public enum BossMove { Burst, Bolt, Volley, Marks, Pounce, Charge, Summon, Enrage }

/// <summary>
/// A raid: a lair out in the wilds where something terrible waits, which a company can fight through again and again.
/// Its own come first (the dead, a goblin warband, a wolf pack); then the boss. Beat it and the place falls quiet for a
/// while, then wakes again.
/// </summary>
/// <param name="MaxLevel">Made for heroes up to this level: the boss grows to match them that far and no further, and
/// those far past it find little worth taking.</param>
/// <param name="Thralls">How many of the boss's own come first.</param>
/// <param name="Boss">What waits there, by kind: "the Goblin King". Every one is the same nobody until it beats a
/// company; then it earns a name and walks out into the world (see <see cref="Roamer"/>).</param>
public sealed record Raid(string Id, string Name, string Boss, string Story, string Taunt, double X, double Z,
    int MaxLevel, double CooldownSeconds, int Thralls)
{
    /// <summary>Never weaker than this: a hero below it who walks in finds the boss at this level anyway.</summary>
    public int MinLevel { get; init; } = 1;
    public LairSite Site { get; init; } = LairSite.Crypt;
    public string BossKind { get; init; } = "cinder_warden";
    public string ThrallKind { get; init; } = "cinder_thrall";
    /// <summary>How much tougher (and better-paid) than the ordinary run of raids at its level.</summary>
    public double Might { get; init; } = 1;
    /// <summary>What its moves do harm with.</summary>
    public DamageKind Element { get; init; } = DamageKind.Fire;
    public IReadOnlyList<BossMove> Moves { get; init; } = [BossMove.Burst, BossMove.Bolt, BossMove.Summon];
    /// <summary>What wakes it, as the hero comes close.</summary>
    public string Waking { get; init; } = "";
    /// <summary>What it shouts when it calls its own.</summary>
    public string Calling { get; init; } = "";
    /// <summary>How it dies.</summary>
    public string Falling { get; init; } = "";
    /// <summary>Its hoard, and what's in it.</summary>
    public string Hoard { get; init; } = "a hoard";
    public string HoardHolds { get; init; } = "";
    /// <summary>The title the first to beat it earns.</summary>
    public string Title { get; init; } = "";

    /// <summary>The boss's kind at the start of a sentence: "The Goblin King".</summary>
    public string BossCap => char.ToUpperInvariant(Boss[0]) + Boss[1..];
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

public static class Raids
{
    /// <summary>
    /// The Ashen Crypt, west of Tristinwold past the Iron Circle's yard. A warden of the Old Kings was burned alive
    /// there defending the royal dead; he never stopped defending them. His body is charred black and grown with
    /// spikes, his hands are claws, and where his head should be a skull burns.
    /// </summary>
    public static readonly Raid AshenCrypt = new("raid:ashen_crypt", "the Ashen Crypt", "the Cinder Warden",
        "Before Tristinwold had walls, a warden of the Old Kings was burned alive defending their crypt from looters. He never stopped defending it. Every so often the ash stirs, his thralls crawl out of the ground, and the Cinder Warden walks again.",
        "None... shall... take... from the KINGS.",
        -300, -80, MaxLevel: 6, CooldownSeconds: 20 * 60, Thralls: 4)
    {
        Waking = "The ash of the Ashen Crypt stirs. Something claws up out of the ground...",
        Calling = "The Cinder Warden calls the dead!",
        Falling = "The Cinder Warden crumbles into ash and embers. His hoard lies open on the dais.",
        Hoard = "the Warden's hoard", HoardHolds = "old crowns of the Old Kings, and the ash of a man who kept his oath too long",
        Title = "Warden-Breaker",
    };

    /// <summary>A goblin warren in a heap of boulders south-east of the city, and its king: a goblin grown huge on
    /// stolen meat, crowned with a cooking pot, who throws firepots at anyone who comes to take his things back.</summary>
    public static readonly Raid GoblinWarren = new("raid:goblin_warren", "the Goblin Warren", "the Goblin King",
        "Goblins have always lived in the rocks south-east of Tristinwold. Then one of them got big: very big, from eating everything he stole, and the rest started calling him king. Now the warren raids the farms every week and the firepots come flying at anyone who goes in after their chickens.",
        "MINE! All of it MINE! Get 'em, lads!",
        172, 440, MaxLevel: 8, CooldownSeconds: 20 * 60, Thralls: 5)
    {
        MinLevel = 3, Site = LairSite.Cave, BossKind = "goblin_king", ThrallKind = "goblin", Might = 1.05, Element = DamageKind.Fire,
        Moves = [BossMove.Marks, BossMove.Summon, BossMove.Enrage],
        Waking = "Goblin whistles from the rocks. The warren's awake, and it's seen you...",
        Calling = "The Goblin King bellows for more goblins!",
        Falling = "The Goblin King topples like a felled tree. His crown rolls off. His throne-pile of loot is yours.",
        Hoard = "the king's throne-pile", HoardHolds = "stolen farm silver, a dozen odd boots and the cooking-pot crown",
        Title = "Kingtoppler",
    };

    /// <summary>A cave bear's den in a hill of stone, north-west of the city, and the bear itself: so old that rock
    /// has grown over its shoulders and back like armour.</summary>
    public static readonly Raid RockBearDen = new("raid:rock_bear_den", "the Rock Bear's Den", "the Rock Bear",
        "Hunters in the north woods tell of a cave bear so old that the stone of its den has grown into its hide. Arrows break on its back. It sleeps through most winters and most summers, and the wolves that den at its door keep everyone away. Wake it and it will tear the hill apart to get at you.",
        "(a roar that shakes stones loose from the cave roof)",
        -200, -346, MaxLevel: 9, CooldownSeconds: 25 * 60, Thralls: 3)
    {
        MinLevel = 4, Site = LairSite.Cave, BossKind = "rock_bear", ThrallKind = "wolf", Might = 1.15, Element = DamageKind.Physical,
        Moves = [BossMove.Burst, BossMove.Bolt, BossMove.Enrage],
        Waking = "Wolves rise from the mouth of the den, hackles up. Deep inside, something huge stirs...",
        Falling = "The Rock Bear sinks down with a sound like a landslide, and is still. Bones and old gear lie heaped at the back of the den.",
        Hoard = "the den's bone-heap", HoardHolds = "the gear of every hunter who came here before you",
        Title = "Stonebreaker",
    };

    /// <summary>A manor gone to briars far south, and what grew from its garden: a mound of thorn and red blossom
    /// that walks, the Briar Mother, and her spriggan brood.</summary>
    public static readonly Raid BriarRuin = new("raid:briar_ruin", "the Briar Ruin", "the Briar Mother",
        "Rosmere Hall was a fine house once, with the finest rose garden in the south. When the last of the Rosmeres died the garden kept growing. Now the hall is a ruin swallowed in thorn, and the heart of the briar has learned to walk, and to sing to the spriggans, and to feed.",
        "Grow... grow into me, little one...",
        -192, 526, MaxLevel: 10, CooldownSeconds: 25 * 60, Thralls: 3)
    {
        MinLevel = 5, Site = LairSite.Ruin, BossKind = "briar_mother", ThrallKind = "spriggan", Might = 1.15, Element = DamageKind.Poison,
        Moves = [BossMove.Burst, BossMove.Volley, BossMove.Summon],
        Waking = "The briars rustle with no wind. Spriggans unfold themselves from the thorn...",
        Calling = "The Briar Mother sings, and the thorn gives birth!",
        Falling = "The Briar Mother withers, red petals falling like rain. At her root, the Rosmere strongbox.",
        Hoard = "the Rosmere strongbox", HoardHolds = "the family silver and a dried rose that still smells of summer",
        Title = "Thornbane",
    };

    /// <summary>A fort on the eastern road that fell to a siege nobody remembers, and its captain, who still holds
    /// it with the ghosts of his men: a spectral knight behind a wall of spectral shields.</summary>
    public static readonly Raid HollowBastion = new("raid:hollow_bastion", "the Hollow Bastion", "the Hollow Captain",
        "Two hundred years ago the Bastion on the eastern road held for forty days against an army, and then it fell, and every man in it died at his post. They are still at their posts. Their captain walks the walls at night in armour made of moonlight, and his legion forms its shield wall at the gate for anyone who comes too close.",
        "Hold the gate! HOLD THE GATE!",
        350, -345, MaxLevel: 12, CooldownSeconds: 30 * 60, Thralls: 4)
    {
        MinLevel = 7, Site = LairSite.Fort, BossKind = "ghost_captain", ThrallKind = "hollow_soldier", Might = 1.2, Element = DamageKind.Frost,
        Moves = [BossMove.Charge, BossMove.Volley, BossMove.Summon],
        Waking = "A cold mist pours over the walls. Pale soldiers form ranks at the gate...",
        Calling = "The Hollow Captain calls the legion to the wall!",
        Falling = "The Hollow Captain lowers his sword. \"Relieved... at last.\" He fades into the mist. The Bastion's paychest stands open.",
        Hoard = "the Bastion's paychest", HoardHolds = "two hundred years of unpaid wages, in coin nobody's minted since",
        Title = "Gatebreaker",
    };

    /// <summary>A cave far to the north-east whose walls are amethyst, and the basilisk that grew crystal along its
    /// spine from lying in it.</summary>
    public static readonly Raid AmethystHollow = new("raid:amethyst_hollow", "the Amethyst Hollow", "the Crystal Basilisk",
        "Miners found the Amethyst Hollow and its walls of violet crystal and thought themselves rich. Then they found what sleeps in it: a basilisk that has lain on the crystal so long it has grown through her, from her skull to the tip of her tail. The crystal sings when she's angry. The ground cracks, and spikes come up out of it.",
        "(the crystals ring, a sound like a thousand glasses)",
        340, -589, MaxLevel: 13, CooldownSeconds: 30 * 60, Thralls: 5)
    {
        MinLevel = 8, Site = LairSite.Cave, BossKind = "crystal_basilisk", ThrallKind = "crystal_rat", Might = 1.25, Element = DamageKind.Shock,
        Moves = [BossMove.Marks, BossMove.Bolt, BossMove.Summon],
        Waking = "The crystal walls begin to hum. Giant rats grown with violet shards pour out of the cracks...",
        Calling = "The basilisk's crystals ring, and more giant rats come scrabbling!",
        Falling = "The Crystal Basilisk shudders and is still. The singing stops. Among the shards, the miners' last haul.",
        Hoard = "the miners' last haul", HoardHolds = "raw amethyst, and the picks of men who never came out",
        Title = "Shardbreaker",
    };

    /// <summary>A dwarven forge sunk into the ground far to the west, where the goblins found the old
    /// smith-engine and lit its furnace: a great iron golem with a chimney for a head.</summary>
    public static readonly Raid SunkenForge = new("raid:sunken_forge", "the Sunken Forge", "the Forge Golem",
        "The dwarves who built the Sunken Forge are long gone, but they left their smith-engine behind: an iron giant with a furnace in its chest and a chimney for a head, made to swing a hammer forever. The goblins who moved in found out how to light the furnace. They haven't found out how to put it out.",
        "(the furnace roars; a voice like grinding gears) WORK. CONTINUES.",
        -520, -300, MaxLevel: 14, CooldownSeconds: 30 * 60, Thralls: 4)
    {
        MinLevel = 9, Site = LairSite.Dungeon, BossKind = "forge_golem", ThrallKind = "goblin_tinker", Might = 1.3, Element = DamageKind.Fire,
        Moves = [BossMove.Burst, BossMove.Bolt, BossMove.Marks, BossMove.Enrage],
        Waking = "Hammers ring in the pit below. Goblin tinkers drop their tools and pick up knives...",
        Falling = "The Forge Golem grinds to a halt, its furnace dying to a glow. The dwarves' strongroom door hangs open.",
        Hoard = "the dwarves' strongroom", HoardHolds = "dwarven ingots, a smith's hammer and a ledger no one can read",
        Title = "Furnace-Quencher",
    };

    /// <summary>A ruined shrine far to the south-east in the dusk-woods, and the great shadow-cat that hunts from it: black, veined
    /// with violet light, and never where you're looking.</summary>
    public static readonly Raid GloamingRuin = new("raid:gloaming_ruin", "the Gloaming Ruin", "the Duskprowler",
        "Nobody knows which god the Gloaming shrine was built for. Whoever it was left something behind: a hunting cat the size of a horse, black as the inside of a closed eye, with light running in its veins like violet blood. It hunts with shadow-wolves. It goes where the shadows go. You see it only when it wants you to.",
        "(a purr, from right behind you)",
        380, 658, MaxLevel: 15, CooldownSeconds: 35 * 60, Thralls: 3)
    {
        MinLevel = 10, Site = LairSite.Ruin, BossKind = "duskprowler", ThrallKind = "shadow_wolf", Might = 1.35, Element = DamageKind.Poison,
        Moves = [BossMove.Pounce, BossMove.Summon, BossMove.Enrage],
        Waking = "The light goes grey. Shadow-wolves slide out of the dark under the broken arches...",
        Calling = "The Duskprowler's shadow splits, and more wolves step out of it!",
        Falling = "The Duskprowler dissolves into dark smoke, and the dusk lifts. On the shrine's altar, the offerings of a forgotten faith.",
        Hoard = "the shrine's offerings", HoardHolds = "black pearls and the silver of a faith no one remembers",
        Title = "Dawnbringer",
    };

    /// <summary>A crag high in the north-west where a thunderhawk nests, a bird as big as a dragon that brings the
    /// storm with it.</summary>
    public static readonly Raid StormEyrie = new("raid:storm_eyrie", "the Storm Eyrie", "the Thunderhawk",
        "On the crags of the north-west there's a nest the size of a cottage, built of whole trees and the bones of cattle. The thunderhawk that keeps it is bigger than some dragons. Lightning follows it. When it screams, the sky answers, and the bolts come down on whatever it's looking at.",
        "(a scream, and the thunder answers it)",
        -705, -591, MaxLevel: 17, CooldownSeconds: 40 * 60, Thralls: 3)
    {
        MinLevel = 12, Site = LairSite.Eyrie, BossKind = "thunderhawk", ThrallKind = "stormwing", Might = 1.45, Element = DamageKind.Shock,
        Moves = [BossMove.Marks, BossMove.Burst, BossMove.Pounce, BossMove.Summon],
        Waking = "The sky darkens over the crag. Young stormwings scream from the nest...",
        Calling = "The Thunderhawk screams, and stormwings dive from the clouds!",
        Falling = "The Thunderhawk crashes down, and the storm breaks apart above the crag. In the nest, the glitter of everything it ever stole.",
        Hoard = "the thunderhawk's nest", HoardHolds = "a magpie's hoard of a hundred years: rings, buckles, a bishop's chain",
        Title = "Stormcaller",
    };

    public static IReadOnlyList<Raid> All { get; } = [AshenCrypt, GoblinWarren, RockBearDen, BriarRuin, HollowBastion, AmethystHollow, SunkenForge, GloamingRuin, StormEyrie];

    /// <summary>A raid by its id: one of the nine, or one of the far country's (see <see cref="FarLairs"/>).</summary>
    public static Raid? Get(string id) => All.FirstOrDefault(r => r.Id == id) ?? FarLairs.Get(id);

    /// <summary>How strong the boss stands against a hero: their level, within what the raid was made for.</summary>
    public static int Strength(Raid r, int heroLevel) => Math.Clamp(heroLevel, r.MinLevel, r.MaxLevel);

    /// <summary>The boss's health and the harm it does, for a hero of a level. Health keeps pace with heroes; harm
    /// grows quickly at first and slower after level 6, as heroes' own health does.</summary>
    public static (double Health, double Damage) Boss(Raid r, int heroLevel)
    {
        var s = Strength(r, heroLevel);
        return ((320 + 70 * s) * r.Might, (12 + 2.5 * Math.Min(s, 6) + 1.2 * Math.Max(0, s - 6)) * Math.Sqrt(r.Might));
    }

    /// <summary>One of the boss's own: its health and harm.</summary>
    public static (double Health, double Damage) Thrall(Raid r, int heroLevel)
    {
        var s = Strength(r, heroLevel);
        return ((35 + 8 * s) * r.Might, (5 + Math.Min(s, 6) + 0.5 * Math.Max(0, s - 6)) * Math.Sqrt(r.Might));
    }

    /// <summary>
    /// What the hoard holds: experience and crowns, double the first time. A hero far past the raid's level finds
    /// it hardly worth the walk.
    /// </summary>
    public static (int Xp, int Crowns) Reward(Raid r, int heroLevel, bool firstTime)
    {
        var s = Strength(r, heroLevel);
        var (xp, crowns) = ((int)Math.Round((150 + 45 * s) * r.Might), (int)Math.Round((40 + 12 * s) * r.Might));
        if (Outgrown(r, heroLevel)) (xp, crowns) = (xp / 4, crowns / 2);
        return firstTime ? (xp * 2, crowns * 2) : (xp, crowns);
    }

    /// <summary>Too strong for it: there's little here for this hero now.</summary>
    public static bool Outgrown(Raid r, int heroLevel) => heroLevel > r.MaxLevel + 2;

    /// <summary>Not strong enough yet: it will meet them at its own level, which is well past theirs.</summary>
    public static bool Daunting(Raid r, int heroLevel) => heroLevel < r.MinLevel - 1;

    /// <summary>The raids that suit a hero of this level, nearest first.</summary>
    public static IEnumerable<Raid> For(int heroLevel, double x, double z) =>
        All.Where(r => !Outgrown(r, heroLevel) && !Daunting(r, heroLevel)).OrderBy(r => r.DistanceTo(x, z));
}

/// <summary>
/// The far country. The nine lairs anyone in Tristinwold can name lie within a day's walk of it; past them the wilds go
/// on for ever, and so do the lairs: one in most <see cref="Cell"/>-metre squares of the far country, each a kind of
/// place already known (a crypt, a warren, a forge, an eyrie) under a name of its own, and harder the further out it
/// lies. Each is worked out from its square alone, so every company finds the same lair in the same place.
/// </summary>
public static class FarLairs
{
    public const double Cell = 1600;
    /// <summary>Nearer Tristinwold than this, there are only the nine.</summary>
    public const double Beyond = 2400;
    const string Prefix = "raid:far:";
    const ulong Salt = 0xFA2_1A12;

    static readonly Raid[] Kinds = [.. Raids.All.Where(r => r != Raids.AshenCrypt)];

    static readonly Dictionary<LairSite, string[]> Places = new()
    {
        [LairSite.Crypt] = ["Crypt", "Barrow", "Tomb", "Ossuary"],
        [LairSite.Cave] = ["Hollow", "Warren", "Den", "Deeps", "Grotto"],
        [LairSite.Ruin] = ["Ruin", "Shrine", "Fane", "Manse"],
        [LairSite.Dungeon] = ["Forge", "Pit", "Undercroft", "Delve"],
        [LairSite.Fort] = ["Bastion", "Hold", "Keep", "Redoubt"],
        [LairSite.Eyrie] = ["Eyrie", "Spire", "Crag", "Aerie"],
    };

    static readonly string[] Words =
    [
        "Weeping", "Bitter", "Sundered", "Black", "Drowned", "Hungry", "Silent", "Red", "Thorned", "Gallows", "Starless",
        "Broken", "Ember", "Grey", "Howling", "Rotting", "Gilded", "Sorrowing", "Iron", "Last",
    ];

    /// <summary>The lair in a square of the far country, if it has one (not every square does, and not in the water
    /// or on top of a town).</summary>
    public static Raid? In(WorldGen world, long cx, long cz)
    {
        if (Noise.Unit(Noise.Hash(Salt, cx, cz, 99)) < 0.2) return null; // empty country
        for (var k = 0; k < 6; k++)
        {
            if (Make(cx, cz, k) is not { } r) return null;
            if (world.Height(r.X, r.Z) < WorldGen.WaterLevel + 1.5) continue;
            if (world.SettlementsNear(r.X, r.Z, 300).Any(s => s.DistanceTo(r.X, r.Z) < s.Radius + 90)) continue;
            return r;
        }
        return null;
    }

    /// <summary>The far lairs within <paramref name="radius"/> metres of a place.</summary>
    public static IEnumerable<Raid> Near(WorldGen world, double x, double z, double radius)
    {
        for (var cx = (long)Math.Floor((x - radius) / Cell); cx <= (long)Math.Floor((x + radius) / Cell); cx++)
            for (var cz = (long)Math.Floor((z - radius) / Cell); cz <= (long)Math.Floor((z + radius) / Cell); cz++)
                if (In(world, cx, cz) is { } r && r.DistanceTo(x, z) <= radius) yield return r;
    }

    /// <summary>A far lair by its id (its square, and which spot in it, are in the id).</summary>
    public static Raid? Get(string id)
    {
        if (!id.StartsWith(Prefix)) return null;
        var parts = id[Prefix.Length..].Split(':');
        return parts.Length == 3 && long.TryParse(parts[0], out var cx) && long.TryParse(parts[1], out var cz) && int.TryParse(parts[2], out var k)
            ? Make(cx, cz, k) : null;
    }

    static Raid? Make(long cx, long cz, int k)
    {
        var rng = Noise.Dice(Salt, cx, cz, k);
        var (x, z) = ((cx + 0.15 + 0.7 * rng.NextDouble()) * Cell, (cz + 0.15 + 0.7 * rng.NextDouble()) * Cell);
        var far = Math.Sqrt(x * x + z * z);
        if (far < Beyond) return null;
        var kind = Kinds[rng.Next(Kinds.Length)];
        var place = rng.Pick(Places[kind.Site]);
        var name = rng.Chance(0.5) ? $"the {rng.Pick(Words)} {place}" : $"the {place} of {Names.Place(rng)}";
        // Every square further out, the harder (and the better paid).
        var tier = (far - Beyond) / Cell;
        string Named(string text) => text.Replace(kind.Name, name).Replace(Cap(kind.Name), Cap(name));
        return kind with
        {
            Id = $"{Prefix}{cx}:{cz}:{k}", Name = name, X = Math.Round(x), Z = Math.Round(z),
            Story = $"Out past where Tristinwold's maps give up, there is {name}. " + Named(kind.Story),
            Waking = Named(kind.Waking), Falling = Named(kind.Falling),
            MinLevel = kind.MinLevel + (int)(2 * tier), MaxLevel = kind.MaxLevel + 2 + (int)(3 * tier),
            Might = kind.Might * Math.Min(2.5, 1 + 0.08 * tier), Thralls = kind.Thralls + Math.Min(3, (int)(tier / 2)),
        };
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>When each raid can be fought again, how often each has been beaten, and the bosses that beat a company
/// and walked out into the world. Saved with the company.</summary>
public sealed class RaidBook
{
    public Dictionary<string, double> ReadyAt { get; set; } = [];
    public Dictionary<string, int> Clears { get; set; } = [];
    public List<Roamer> Roamers { get; set; } = [];
    /// <summary>The lairs the hero has found, near home or out in the far country: they stay on the map, whatever the game lets go of.</summary>
    public List<string> Seen { get; set; } = [];

    public void Saw(Raid r)
    {
        if (!Seen.Contains(r.Id)) Seen.Add(r.Id);
    }

    public bool Ready(Raid r, double playSeconds) => playSeconds >= ReadyAt.GetValueOrDefault(r.Id);

    public double SecondsLeft(Raid r, double playSeconds) => Math.Max(0, ReadyAt.GetValueOrDefault(r.Id) - playSeconds);

    public int ClearsOf(Raid r) => Clears.GetValueOrDefault(r.Id);

    /// <summary>Beaten: the place falls quiet, and wakes again after a while.</summary>
    public void Cleared(Raid r, double playSeconds)
    {
        Clears[r.Id] = ClearsOf(r) + 1;
        ReadyAt[r.Id] = playSeconds + r.CooldownSeconds;
    }

    /// <summary>How long a lair stands empty after its boss walks out, before the next one moves in.</summary>
    public const double MoveInSeconds = 90;

    /// <summary>
    /// The boss beat the company: it takes a name, grows stronger and leaves its lair to roam the world, a deviant
    /// of its own from then on. The lair doesn't stay empty: a moment later another moves in, nameless and no
    /// stronger than its kind, as they all start.
    /// </summary>
    public Roamer Escaped(Raid r, double playSeconds, ulong seed)
    {
        ReadyAt[r.Id] = playSeconds + MoveInSeconds;
        var roamer = new Roamer { Id = $"roamer:{seed:x}", RaidId = r.Id, Name = Roaming.NameFor(seed), Seed = seed, Since = playSeconds, Wins = 1 };
        Roamers.Add(roamer);
        return roamer;
    }

    /// <summary>A roamer killed for good.</summary>
    public void Slain(Roamer roamer) => Roamers.RemoveAll(x => x.Id == roamer.Id);

    /// <summary>How many deviants of the wilds roam at once; past that, the one that's done least moves on.</summary>
    public const int MostWild = 8;

    /// <summary>
    /// Something out in the wilds beat the company (a bandit, a troll, a wolf): it takes a name and roams from where it
    /// stood over them, a deviant like any lair boss that walked out of its lair.
    /// </summary>
    public Roamer Took(string kind, double x, double z, double playSeconds, ulong seed)
    {
        var wild = Roamers.Where(w => w.Lair is null).ToList();
        if (wild.Count >= MostWild) Roamers.Remove(wild.OrderBy(w => w.Wins).ThenBy(w => w.Since).First());
        var roamer = new Roamer { Id = $"roamer:{seed:x}", Kind = kind, StartX = x, StartZ = z, Name = Roaming.NameFor(seed), Seed = seed, Since = playSeconds, Wins = 1 };
        Roamers.Add(roamer);
        return roamer;
    }
}

/// <summary>
/// A deviant out in the world: a lair boss that beat a company and walked out of its lair, or anything in the wilds
/// that beat them where it stood. Named, stronger, wandering the wilds, hunting the hero when it sees them, learning
/// how they fight, and stronger again every time it beats them. In the shared world it is also on the roster.
/// </summary>
public sealed class Roamer
{
    public string Id { get; set; } = "";
    /// <summary>The lair it walked out of, if it was a lair's boss.</summary>
    public string RaidId { get; set; } = "";
    /// <summary>What it is, if it was born in the wilds (a lair boss is its lair's boss kind), and where it beat the company.</summary>
    public string? Kind { get; set; }
    public double StartX { get; set; }
    public double StartZ { get; set; }
    /// <summary>What it has learned about how the hero fights.</summary>
    public Lessons Lessons { get; set; } = new();
    /// <summary>It has the hero's trail, and is coming after them.</summary>
    public bool Hunting { get; set; }
    /// <summary>It won't come for anyone before this (seconds of play): it's making its plans.</summary>
    public double PlansUntil { get; set; }
    /// <summary>"Mokak the Grinning".</summary>
    public string Name { get; set; } = "";
    /// <summary>How many times it has beaten the company.</summary>
    public int Wins { get; set; } = 1;
    /// <summary>Play time when it left its lair; where it wanders from then on follows from this and the seed.</summary>
    public double Since { get; set; }
    public ulong Seed { get; set; }
    /// <summary>Its id on the shared world's roster, when there is a shared world.</summary>
    public Guid? DeviantId { get; set; }

    [JsonIgnore]
    public Raid? Lair => RaidId.Length == 0 ? null : Raids.Get(RaidId);

    /// <summary>Its kind in the bestiary.</summary>
    [JsonIgnore]
    public string KindId => Lair?.BossKind ?? Kind ?? "bandit";

    /// <summary>"Mokak the Grinning, the Goblin King"; "Vorn the Patient, Frost Troll".</summary>
    [JsonIgnore]
    public string Title => $"{Name}, {Lair?.Boss ?? Bestiary.Get(KindId).Name}";

    /// <summary>Where it set out from: its lair, or where it beat the company.</summary>
    [JsonIgnore]
    public (double X, double Z) Start => Lair is { } l ? (l.X, l.Z) : (StartX, StartZ);

    readonly List<(double X, double Z, double Start)> _legs = [];

    /// <summary>
    /// Where it is now: it wanders the wilds from its lair, a few hundred metres at a walk, rests, and sets off
    /// again, never into a town and never into the water. Every company sees it in the same place at the same time.
    /// </summary>
    public (double X, double Z) Where(WorldGen world, double playSeconds)
    {
        if (_legs.Count == 0) _legs.Add((Start.X, Start.Z, Since));
        var t = Math.Max(Since, playSeconds);
        // Each stop is where a leg starts; the leg in progress is the one after the last stop already reached.
        while (true)
        {
            var (x, z, start) = _legs[^1];
            if (t < start) break;
            var (nx, nz) = NextStop(world, x, z, _legs.Count);
            var length = Math.Sqrt((nx - x) * (nx - x) + (nz - z) * (nz - z));
            _legs.Add((nx, nz, start + Roaming.RestSeconds + length / Roaming.Pace));
        }
        var i = _legs.FindIndex(l => l.Start > t);
        var (fx, fz, from) = _legs[i - 1];
        var (tx, tz, arrive) = _legs[i];
        var f = Math.Clamp((t - from - Roaming.RestSeconds) / (arrive - from - Roaming.RestSeconds), 0, 1);
        return (fx + (tx - fx) * f, fz + (tz - fz) * f);
    }

    (double X, double Z) NextStop(WorldGen world, double x, double z, int leg)
    {
        var rng = Noise.Dice(Seed, leg);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var a = rng.NextDouble() * Math.Tau;
            var d = 150 + rng.NextDouble() * 200;
            var (nx, nz) = (x + Math.Cos(a) * d, z + Math.Sin(a) * d);
            var fromHome = Math.Sqrt(nx * nx + nz * nz);
            var fromStart = Math.Sqrt((nx - Start.X) * (nx - Start.X) + (nz - Start.Z) * (nz - Start.Z));
            if (fromHome < Roaming.NearestToCity || fromStart > Roaming.FarthestOut) continue;
            if (world.Height(nx, nz) < WorldGen.WaterLevel + 1.5) continue;
            if (world.SettlementsNear(nx, nz, 400).Any(s => s.DistanceTo(nx, nz) < s.Radius + 60)) continue;
            return (nx, nz);
        }
        return Start; // nowhere good: back to where it came from
    }
}

/// <summary>How the bosses that escape their lairs are named, how strong they get and what they're worth.</summary>
public static class Roaming
{
    /// <summary>How fast a roamer walks between stops (m/s), and how long it rests at each.</summary>
    public const double Pace = 1.1, RestSeconds = 90;
    /// <summary>Never nearer Tristinwold than this, and never further than <see cref="FarthestOut"/> from where it set out
    /// (its lair, or where it beat the company), however far out in the wilds that is.</summary>
    public const double NearestToCity = 240, FarthestOut = 1400;

    /// <summary>A name in the deviant roster's own style: "Mokak the Grinning".</summary>
    public static string NameFor(ulong seed)
    {
        var rng = new Rng(seed);
        var names = NamePack.Default;
        return $"{rng.Pick(names.FirstNames)}{rng.Pick(names.NameSuffixes)} {rng.Pick(names.Epithets)}";
    }

    /// <summary>Its health and harm: its lair's boss, a cut above where the lair stops growing, and more for every
    /// company it has beaten.</summary>
    public static (double Health, double Damage) Numbers(Roamer r, int heroLevel)
    {
        if (r.Lair is { } raid)
        {
            var (health, damage) = Raids.Boss(raid with { MaxLevel = raid.MaxLevel + 3 }, heroLevel);
            return (health * (1.15 + 0.2 * r.Wins), damage * (1.1 + 0.08 * r.Wins));
        }
        // One of the wilds: its kind as the land there makes them, then a good deal more, for the hero it has beaten.
        var kind = Bestiary.Get(r.KindId);
        var scale = Math.Pow(Math.Max(1, WorldGen.Danger(r.StartX, r.StartZ)), 0.8) * (1 + 0.04 * (heroLevel - 1));
        return (kind.Health * scale * (1.8 + 0.3 * r.Wins), kind.Damage * scale * (1.25 + 0.1 * r.Wins));
    }

    /// <summary>The price on its head, paid to whoever kills it: its lair's hoard, and more for every company it beat.</summary>
    public static (int Xp, int Crowns) Bounty(Roamer r, int heroLevel)
    {
        if (r.Lair is null)
        {
            var (health, _) = Numbers(r, heroLevel);
            return ((int)Math.Round(health / 4), 20 + 15 * r.Wins + (int)Math.Round(health / 12));
        }
        var (xp, crowns) = Raids.Reward(r.Lair, heroLevel, firstTime: true);
        var more = 1 + 0.25 * r.Wins;
        return ((int)Math.Round(xp * more), (int)Math.Round(crowns * more));
    }
}
