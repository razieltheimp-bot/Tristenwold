using System.Text.Json.Serialization;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// A character's growth: experience from fights and jobs, levels, and the skill points each level brings,
/// spent to rank up the moves of their class (each from rank 1 to 5). Saved with the company.
/// </summary>
public sealed class Hero
{
    public const int MaxRank = 5;

    public string Discipline { get; set; } = "Paladin";
    /// <summary>How old they were when they first woke at the Gallows Perch (0 until they have), and the day they did.</summary>
    public int StartAge { get; set; }
    public int BornDay { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Points { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = [];

    /// <summary>What's on the quick keys, 1 to 0.</summary>
    public QuickBar Bar { get; set; } = new();

    /// <summary>Experience needed to go from a level to the next.</summary>
    public static int XpToNext(int level) => (int)Math.Round(80 * Math.Pow(level, 1.4));

    /// <summary>Adds experience; returns how many levels it brought (each one a skill point).</summary>
    public int Gain(int xp)
    {
        Xp += Math.Max(0, xp);
        var gained = 0;
        while (Xp >= XpToNext(Level))
        {
            Xp -= XpToNext(Level);
            Level++;
            Points++;
            gained++;
        }
        return gained;
    }

    public int Rank(string skill) => Math.Clamp(Ranks.GetValueOrDefault(skill, 1), 1, MaxRank);

    /// <summary>A move's rank wherever it was earned: one from a class left behind, on the quick keys, keeps what it had.</summary>
    public int Earned(string skill)
    {
        if (Ranks.TryGetValue(skill, out var rank)) return Math.Clamp(rank, 1, MaxRank);
        foreach (var kept in Learned.Values)
            if (kept.TryGetValue(skill, out rank)) return Math.Clamp(rank, 1, MaxRank);
        return 1;
    }

    /// <summary>How much stronger a move is for its rank: 15% for each rank above the first.</summary>
    public double Power(string skill) => 1 + 0.15 * (Rank(skill) - 1);

    /// <summary><see cref="Power"/>, by the rank it was earned at.</summary>
    /// <summary>What the years add to every move: a little to each one, and a good deal to a limit break.</summary>
    public double LevelPower(bool limit) => 1 + (limit ? 0.05 : 0.02) * (Level - 1);

    public double MovePower(string skill) => 1 + 0.15 * (Earned(skill) - 1);

    public bool CanRaise(string skill) => Points > 0 && Rank(skill) < MaxRank;

    public bool Raise(string skill)
    {
        if (!CanRaise(skill)) return false;
        Ranks[skill] = Rank(skill) + 1;
        Points--;
        return true;
    }

    /// <summary>Joining another class: its moves start fresh, and the points spent come back.</summary>
    /// <summary>The skill ranks of every class learned before, by class: an immortal keeps what they were.</summary>
    public Dictionary<string, Dictionary<string, int>> Learned { get; set; } = [];

    /// <summary>Every class this hero has learned, the present one first.</summary>
    [JsonIgnore]
    public IEnumerable<string> Classes => new[] { Discipline }.Concat(Learned.Keys.Where(k => k != Discipline));

    public bool Knows(string discipline) => discipline == Discipline || Learned.ContainsKey(discipline);

    /// <summary>
    /// The one limit break the hero calls on: their present class's, unless they've chosen another class they've
    /// learned (a cross-classed artificer taking the shapeshifter's half-form). One at a time.
    /// </summary>
    public string? Ultimate { get; set; }

    /// <summary>The class whose limit break is the hero's now.</summary>
    [JsonIgnore]
    public string UltimateOf => Ultimate is { } u && Knows(u) ? u : Discipline;

    /// <summary>The artificer's calling learned, if any, the present one first: whose golem walks with the hero, whatever they're doing now.</summary>
    [JsonIgnore]
    public string? Artificer => Classes.FirstOrDefault(c => Gameplay.Classes.Get(c).Calling == Calling.Artificer);

    /// <summary>
    /// Takes up a class: a new one, or one learned before. The class left keeps its skill ranks for the day it's taken
    /// up again; a class taken up for the first time gets half the hero's levels in skill points to spend on it, since
    /// an immortal doesn't start from nothing twice.
    /// </summary>
    public void Join(string discipline)
    {
        if (discipline == Discipline) return;
        Learned[Discipline] = new Dictionary<string, int>(Ranks);
        var fresh = !Learned.ContainsKey(discipline);
        Ranks = Learned.TryGetValue(discipline, out var kept) ? new Dictionary<string, int>(kept) : [];
        Learned.Remove(discipline);
        Discipline = discipline;
        if (fresh) Points += Level / 2;
    }

    /// <summary>Health, mana and stamina a level brings on top of the class's own.</summary>
    [JsonIgnore]
    public (double Health, double Mana, double Stamina) Growth => ((Level - 1) * 12, (Level - 1) * 6, (Level - 1) * 5);

    /// <summary>Experience for a kill: tougher foes and the far wilds teach more.</summary>
    public static int XpForKill(EnemyKind kind, double danger = 1, double toughness = 1) =>
        Math.Max(3, (int)Math.Round(kind.Health / 6 * Math.Pow(Math.Max(1, danger), 0.8) * Math.Sqrt(toughness)));
}

/// <summary>What a companion is to you: a tamed beast, a squire or apprentice from a town, or your own dragon.</summary>
/// <summary>What a companion is to its hero. A golem is an artificer's machine: it doesn't eat, and it's as grown as its maker is.</summary>
public enum BondRole { Pet, Squire, Apprentice, Dragon, Golem, Mite }

/// <summary>A move a companion learns as it grows: <paramref name="Level"/> is when.</summary>
public sealed record BondSkill(string Id, string Name, int Level, string Text, double Cooldown, double Power);

/// <summary>
/// A companion: a beast you raised from a youngling, a squire or apprentice from a town, or your dragon.
/// It levels up from the fights it shares with you, grows from young to full size as it does, learns moves,
/// and (beasts and dragons) gets hungry: feed it or it weakens and, if it starves, dies. A companion that
/// falls in a fight is knocked out and gets up again; a dragon too young to fly that falls is lost.
/// </summary>
public sealed class Bond
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string KindId { get; set; } = "wolf";
    public string Name { get; set; } = "";
    public BondRole Role { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }

    /// <summary>How full it is, 0-100. Beasts and dragons only; people feed themselves.</summary>
    public double Fed { get; set; } = 100;

    /// <summary>Its health when last saved, as a share of full.</summary>
    public double Health { get; set; } = 1;

    /// <summary>The Northwood's rot: in its blood; or got the better of it (turned on its hero, then beaten down and bound) until a druid cures it.</summary>
    public bool Blighted { get; set; }
    public bool Rotted { get; set; }

    /// <summary>Following you now, or waiting at home (the Gallows Perch).</summary>
    public bool Following { get; set; } = true;
    /// <summary>The hero named this one themselves: the name stays, whatever a Foundry refit might otherwise call it.</summary>
    public bool Renamed { get; set; }
    /// <summary>A steed hitched to your wagon: it goes where the wagon goes, not at your heels, until you unhitch it to ride.</summary>
    public bool Hitched { get; set; }
    /// <summary>Keeping shop for the hero (see <see cref="Keeping"/>): at home, at the wagon or aboard the ship; null when not.</summary>
    public string? Keeps { get; set; }
    /// <summary>The class an apprentice or squire is learning: their hero's, unless the hero has had them trained another way. Null in an older save.</summary>
    public string? Trains { get; set; }
    /// <summary>What the hero has given them to wear and carry (see <see cref="Kit"/>), by slot.</summary>
    public Dictionary<Slot, string> Gear { get; set; } = [];

    /// <summary>A dragon too young to fly is lost for good if it falls; anything else is only knocked out.</summary>
    [JsonIgnore]
    public bool DiesIfFelled => Role == BondRole.Dragon && Level < 10 || Role is BondRole.Squire or BondRole.Apprentice or BondRole.Mite; // apprentices are mortal, and a mite wrecked is scrap: no getting up after the fight

    /// <summary>An apprentice's people, how old they were when they joined, and the day they did: they age, and in the end they die of it.</summary>
    public string? Race { get; set; }
    public double JoinAge { get; set; }
    public int JoinDay { get; set; }

    public const double HungerSeconds = 1500; // a full belly lasts 25 minutes of play

    [JsonIgnore]
    public bool Eats => Role is BondRole.Pet or BondRole.Dragon;

    public static int XpToNext(int level) => (int)Math.Round(45 * Math.Pow(level, 1.35));

    public int Gain(int xp)
    {
        Xp += Math.Max(0, xp);
        var gained = 0;
        while (Xp >= XpToNext(Level) && Level < 40)
        {
            Xp -= XpToNext(Level);
            Level++;
            gained++;
        }
        return gained;
    }

    /// <summary>How grown it is, 0-1: young at level 1, full grown at level 6 (a dragon at 10).</summary>
    [JsonIgnore]
    public double Growth => Role == BondRole.Dragon
        ? Math.Min(1, 0.12 + 0.88 * (Level - 1) / 9.0)
        : Role is BondRole.Golem or BondRole.Mite ? 1
        : Math.Min(1, 0.35 + 0.65 * (Level - 1) / 5.0);

    /// <summary>An artificer's golem: built, not born; its level is its maker's.</summary>
    [JsonIgnore]
    public bool Machine => Role is BondRole.Golem or BondRole.Mite;

    /// <summary>
    /// How big a golem is built, compared with the Foundry's pattern: an ether-aeronaut's is a hull with room for
    /// its maker inside, so it stands a good deal taller and heavier than an alchemist's or a saboteur's.
    /// </summary>
    public double Build { get; set; } = 1;

    public const double AeronautBuild = 1.35;

    /// <summary>The build of the golem the Foundry makes for each artificer: the aeronaut's is the one you climb inside.</summary>
    public static double GolemBuild(string discipline) => discipline == "EtherAeronaut" ? AeronautBuild : 1;

    /// <summary>How its numbers compare to a grown one of its kind: smaller while young, stronger with every level.</summary>
    [JsonIgnore]
    public double Strength => (0.3 + 0.7 * Growth) * (1 + 0.07 * (Level - 1)) * (Fed <= 0 && Eats ? 0.6 : 1);

    /// <summary>How big it is drawn, compared with a grown one.</summary>
    [JsonIgnore]
    public double Size => Role switch
    {
        BondRole.Dragon => Level >= 30 ? 10.5 / 4.5 : Level >= 20 ? 7 / 4.5 : Growth,
        BondRole.Pet => Math.Min(1, 0.4 + 0.12 * (Level - 1)),
        BondRole.Golem => Build,
        BondRole.Mite => 0.45, // knee-high
        _ => 0.82 + 0.18 * Growth, // a youth grows into a squire's or apprentice's full height
    };

    /// <summary>A dragon of level 10 or more can carry its knight; a horse, once it's grown (level 6).</summary>
    [JsonIgnore]
    public bool Rideable => Role == BondRole.Dragon ? Level >= 10 : Steed && Growth >= 1;

    /// <summary>A horse: it can be ridden once grown, and pull a wagon.</summary>
    [JsonIgnore]
    public bool Steed => IsSteed(KindId);

    public static bool IsSteed(string kindId) => kindId is "horse" or "wild_horse";

    [JsonIgnore]
    public string Stage => Role switch
    {
        BondRole.Dragon => Level < 5 ? "hatchling" : Level < 10 ? "wyrmling" : Level < 20 ? "young dragon" : Level < 30 ? "large dragon" : "giant dragon",
        BondRole.Pet => Growth < 1 ? "young" : "grown",
        BondRole.Squire => Level < 10 ? "squire" : "sworn squire",
        BondRole.Mite => "clockwork mite",
        _ => Level < 10 ? "apprentice" : "journeyman",
    };

    /// <summary>Gets hungrier as time passes.</summary>
    public void Tick(double seconds)
    {
        if (Eats) Fed = Math.Max(0, Fed - seconds * 100 / HungerSeconds);
    }

    [JsonIgnore]
    public bool Hungry => Eats && Fed < 35;
    [JsonIgnore]
    public bool Starving => Eats && Fed <= 0;

    public void Feed(double amount = 45) => Fed = Math.Min(100, Fed + amount);

    [JsonIgnore]
    public IEnumerable<BondSkill> Skills => SkillsFor(Role, KindId).Where(s => s.Level <= Level);
    [JsonIgnore]
    public IEnumerable<BondSkill> AllSkills => SkillsFor(Role, KindId);
    public bool Knows(string id) => Skills.Any(s => s.Id == id);

    /// <summary>What a companion learns as it grows, by what it is.</summary>
    public static IEnumerable<BondSkill> SkillsFor(BondRole role, string kindId) => kindId switch
    {
        // The young who learn from a hero, by what they're learning.
        "pupil" =>
        [
            new("strike", "Palm Strike", 3, "An open-handed blow that lands like a hammer.", 5, 2.5),
            new("rally", "Inner Calm", 5, "A breath, and you heal a little in a fight.", 14, 0.1),
            new("guard", "Iron Body", 8, "Steps in front of a foe that is hurting you.", 8, 0),
        ],
        "tinker" =>
        [
            new("strike", "Spark Shot", 3, "A crackling bolt from a brass gun.", 5, 2.5),
            new("rally", "Patch Up", 5, "A quick fix that heals you a little in a fight.", 14, 0.1),
            new("guard", "Decoy", 8, "A clattering little decoy that draws a foe off you.", 10, 0),
        ],
        "acolyte" =>
        [
            new("strike", "Thorn Lash", 3, "A whip of thorns at your foe.", 5, 2.5),
            new("rally", "Green Mend", 5, "Heals you a little in a fight.", 14, 0.12),
            new("guard", "Tangle", 8, "Roots that drag a foe off you.", 10, 0),
        ],
        "protege" =>
        [
            new("strike", "Quick Cut", 3, "A knife where it hurts.", 5, 2.5),
            new("rally", "Bandage", 5, "Binds your wounds a little in a fight.", 14, 0.1),
            new("guard", "Feint", 8, "Draws a foe that is hurting you onto itself.", 8, 0),
        ],
        "understudy" =>
        [
            new("strike", "Cutting Note", 3, "A note sharp enough to cut.", 5, 2.5),
            new("rally", "Rousing Song", 5, "A song that heals you a little in a fight.", 14, 0.12),
            new("guard", "Heckle", 8, "Draws a foe that is hurting you onto itself.", 8, 0),
        ],
        _ => SkillsByRole(role, kindId),
    };

    static IEnumerable<BondSkill> SkillsByRole(BondRole role, string kindId) => role switch
    {
        BondRole.Mite => [],
        BondRole.Squire =>
        [
            new("guard", "Shield Your Master", 3, "Draws a foe that is hurting you onto itself.", 8, 0),
            new("strike", "Shield Bash", 5, "A heavy blow with the shield.", 6, 2.5),
            new("rally", "Battle Cry", 8, "You heal a little and fight with haste.", 18, 0.1),
        ],
        BondRole.Apprentice =>
        [
            new("strike", "Firebolt", 3, "A bolt of fire at your foe.", 5, 2.5),
            new("rally", "Mend", 5, "Heals you a little in a fight.", 14, 0.12),
            new("guard", "Ward", 8, "Casts Protect on you.", 20, 0),
        ],
        BondRole.Dragon =>
        [
            new("strike", "Claw Rake", 3, "A raking blow of the claws.", 6, 2.5),
            new("breath", "Breath", 5, "Breathes its element over every foe in front of it.", 9, 2.2),
            new("ride", "Take Wing", 10, "Strong enough to carry you: mount up and fly.", 0, 0),
            new("rally", "Roar", 15, "A roar that heals you and sends weaker foes running.", 20, 0.15),
        ],
        _ =>
        [
            new("guard", "Guard", 3, "Draws a foe that is hurting you onto itself.", 8, 0),
            new("strike", StrikeName(kindId), 5, "A savage blow.", 6, 2.5),
            new("rally", "Loyal Heart", 8, "Its courage heals you a little in a fight.", 18, 0.08),
        ],
    };

    static string StrikeName(string kindId) => kindId switch
    {
        "wolf" or "ice_wolf" or "fox" => "Savage Bite",
        "bear" => "Maul",
        "boar" or "deer" => "Gore",
        "frost_troll" or "goblin" => "Club Smash",
        "spriggan" => "Thorn Lash",
        "giant_rat" => "Gnaw",
        "mud_crab" => "Pincer Crush",
        _ => "Savage Strike",
    };
}

/// <summary>Finding and taming the young of the wilds.</summary>
public static class Taming
{
    /// <summary>How hard a mount that can't fly leaps (metres a second upward), by how fast it is and how grown: a good horse clears about its own height.</summary>
    public static double MountJump(double speed, int level) => 2.2 + speed * 0.35 + Math.Min(level, 30) * 0.06;

    /// <summary>The young of these can be raised: beasts and monsters (not people, and not the dead).</summary>
    public static bool Tameable(string kindId) => kindId is "giant_rat" or "wolf" or "ice_wolf" or "bear" or "frost_troll" or "mud_crab"
        or "spriggan" or "goblin" or "boar" or "deer" or "fox" or "kipine" or "deathcrest_ape" or "winged_ape" or "hawk" or "wild_horse"
        || (kindId.StartsWith("dragon_") && !kindId.EndsWith("_corrupt"));

    /// <summary>What its young is called.</summary>
    public static string YoungName(string kindId, string kindName) => kindId switch
    {
        "wolf" => "Wolf Pup",
        "ice_wolf" => "Ice Wolf Pup",
        "bear" => "Bear Cub",
        "fox" => "Fox Kit",
        "hawk" => "Hawk Chick",
        "kipine" => "Kipine Chick",
        "deathcrest_ape" => "Deathcrest Whelp",
        "winged_ape" => "Winged Ape Infant",
        "deer" => "Fawn",
        "boar" => "Boarlet",
        "giant_rat" => "Giant Rat Kit",
        "mud_crab" => "Mud Crab Hatchling",
        "spriggan" => "Spriggan Sapling",
        "goblin" => "Goblin Whelp",
        "frost_troll" => "Troll Whelp",
        _ when kindId.StartsWith("dragon_") => kindName.Replace("Dragon", "Wyrmling"),
        _ => $"Young {kindName}",
    };

    /// <summary>
    /// The chance a youngling follows you. Its kin won't stand for it: while a grown one of its herd is alive
    /// close by, it can't be done (deal with them first, or find it alone). Offering meat helps; a Ranger
    /// never fails.
    /// </summary>
    /// <param name="tamingLevel">The hero's beast taming (a trade, like herbalism): 3% better for each level past the first.</param>
    public static double Chance(bool kinNearby, bool offeringMeat, bool beastmaster, int heroLevel, int tamingLevel = 1, bool dragon = false)
    {
        if (kinNearby) return 0;
        if (dragon) return DragonChance(offeringMeat, beastmaster, heroLevel, tamingLevel);
        if (beastmaster) return 1;
        return Math.Min(0.95, 0.45 + (offeringMeat ? 0.3 : 0) + 0.02 * heroLevel + 0.03 * (tamingLevel - 1));
    }

    /// <summary>A wyrmling won't so much as look at a hero below this level.</summary>
    public const int DragonLevel = 8;

    /// <summary>How long a wyrmling that turned you down won't let you try again (seconds of play).</summary>
    public const double DragonSpurns = 600;

    /// <summary>
    /// A wyrmling is no fox kit: it's clever, and proud, and it knows what you are. It won't look at a hero below level
    /// 8; after that it weighs you: your years in the wilds (your level) and your way with beasts (beast taming) count
    /// for most; meat counts for little; a ranger's knack helps, but doesn't decide it. It never goes better than an
    /// even chance.
    /// </summary>
    public static double DragonChance(bool offeringMeat, bool beastmaster, int heroLevel, int tamingLevel = 1)
    {
        if (heroLevel < DragonLevel) return 0;
        return Math.Min(0.5, 0.05 + 0.015 * heroLevel + 0.025 * (tamingLevel - 1) + (offeringMeat ? 0.05 : 0) + (beastmaster ? 0.12 : 0));
    }

    /// <summary>How much faster a tamer's beasts learn: 5% for each level of beast taming past the first.</summary>
    public static double Training(int tamingLevel) => 1 + 0.05 * (tamingLevel - 1);
}

/// <summary>
/// Who may travel with a hero, counted kind by kind. Up to three apprentices or squires (as many as they like for a
/// Paladin or a Wizard: every youth who joins them is an apprentice). Pets by the class's limit (four,
/// or as many as they like for a Ranger or a Shapeshifter). As many mounts as they own, kept and fed at the Gallows Perch,
/// but only as many with them as there are riders: the hero and each apprentice at their side. A golem is a machine and
/// doesn't count, and a foal or a dragon too small to ride is a pet until it's grown.
/// </summary>
public static class PartyRules
{
    public const int MaxApprentices = 3, MaxPets = 4;
    /// <summary>The level an artificer who works beside their construct builds a second. The Ether-Aeronaut climbs inside theirs, and has only the one.</summary>
    public const int SecondConstructLevel = 6;

    /// <summary>How many full-size constructs this class can keep at this level: none but for an artificer, and a second only for an Alchemist. A Saboteur forges mites instead.</summary>
    public static int Constructs(Calling calling, string classId, int level) =>
        calling != Calling.Artificer ? 0 : classId == "Alchemist" && level >= SecondConstructLevel ? 2 : 1;

    /// <summary>What a Saboteur needs at a forge to make one clockwork mite, a log for the fire besides. As many as they have the makings for.</summary>
    public static readonly (string Id, int N)[] MiteNeeds = [("iron_ingot", 2), ("iron_nails", 4), ("scrap_iron", 2)];
    public static readonly string[] MiteNames = ["Cog", "Sprocket", "Washer", "Tack", "Ratchet", "Spindle", "Grommet", "Shim", "Pawl", "Burr", "Clevis", "Bodkin"];

    /// <summary>A name for the next mite: the list, then round again with a number.</summary>
    public static string MiteName(int made) => made < MiteNames.Length ? MiteNames[made] : $"{MiteNames[made % MiteNames.Length]} the {made / MiteNames.Length + 1}{(made / MiteNames.Length + 1) switch { 2 => "nd", 3 => "rd", _ => "th" }}";

    public static bool IsApprentice(Bond b) => b.Role is BondRole.Squire or BondRole.Apprentice;

    /// <summary>
    /// What a youth who follows a hero becomes, by the class they learn: a warrior's squire (a martial artist's pupil), a
    /// mage's apprentice, a druid's acolyte, a rogue's protege, a bard's understudy, an artificer's tinker with a little
    /// robot of their own. The hero's own class, unless the hero has them trained another way.
    /// </summary>
    public static (BondRole Role, string Kind, string Word) Follower(string classId)
    {
        var d = Classes.Get(classId);
        return d.Calling switch
        {
            Calling.Warrior when d.Id == "MartialArtist" => (BondRole.Squire, "pupil", "pupil"),
            Calling.Warrior => (BondRole.Squire, "squire", "squire"),
            Calling.Mage => (BondRole.Apprentice, "apprentice", "apprentice"),
            Calling.Druid => (BondRole.Apprentice, "acolyte", "acolyte"),
            Calling.Bard => (BondRole.Apprentice, "understudy", "understudy"),
            Calling.Artificer => (BondRole.Apprentice, "tinker", "apprentice"),
            _ => (BondRole.Apprentice, "protege", "protege"),
        };
    }

    /// <summary>Sets a youth learning a class: what they are, what they look like and what they learn, all at once. Their name and their levels stay.</summary>
    public static void Train(Bond b, string classId)
    {
        var (role, kind, _) = Follower(classId);
        (b.Trains, b.Role, b.KindId) = (Classes.Get(classId).Id, role, kind);
    }

    /// <summary>A Paladin, a Wizard and a Martial Artist take on as many apprentices as they like, the way a Ranger keeps as many beasts.</summary>
    public static bool FreeApprentices(string classId) => classId is "Paladin" or "Wizard" or "MartialArtist";
    public static bool IsMount(Bond b) => b.Role == BondRole.Dragon ? b.Rideable : b.Steed && b.Rideable;
    public static bool IsPet(Bond b) => !IsApprentice(b) && !IsMount(b) && !b.Machine;

    /// <summary>The hero, and every apprentice at their side.</summary>
    public static int Riders(IEnumerable<Bond> bonds) => 1 + bonds.Count(b => b.Following && IsApprentice(b));

    /// <summary>Whether this one can join those following, as things stand (it isn't counted itself).</summary>
    public static bool MayFollow(IReadOnlyCollection<Bond> bonds, Bond who, bool freePets, bool freeApprentices = false)
    {
        if (who.Machine) return true; // a golem comes with the calling, and a saboteur makes as many mites as they like
        var others = bonds.Where(b => b != who && b.Following).ToList();
        if (IsApprentice(who)) return freeApprentices || others.Count(IsApprentice) < MaxApprentices;
        if (IsMount(who)) return others.Count(IsMount) < Riders(others);
        return freePets || others.Count(IsPet) < MaxPets;
    }

    /// <summary>Pets at the hero's heels over the four (an older save), the newest first: they go and wait at home.</summary>
    public static IEnumerable<Bond> SparePets(IReadOnlyList<Bond> bonds, bool freePets) =>
        freePets ? [] : bonds.Where(b => b.Following && IsPet(b)).Skip(MaxPets);

    /// <summary>The mounts following over and above the riders, last first: an apprentice sent home leaves their mount at the Perch too.</summary>
    public static IEnumerable<Bond> SpareMounts(IReadOnlyCollection<Bond> bonds, Bond? ridden = null)
    {
        var mounts = bonds.Where(b => b.Following && IsMount(b) && b != ridden).ToList();
        var room = Math.Max(0, Riders(bonds) - (ridden is null ? 0 : 1));
        return mounts.Skip(room);
    }
}

/// <summary>
/// What the air costs. A fall hurts by the metre past the first few, the same for everyone, so a strong enough hero lives
/// through a drop that would kill a green one. A maker stays in their machine longer the more levels they have.
/// </summary>
public static class Flight
{
    /// <summary>How far anyone can drop unhurt, and what every metre past it costs.</summary>
    public const double SafeFall = 6, FallPerMetre = 8;

    public static double FallDamage(double metres) => Math.Max(0, metres - SafeFall) * FallPerMetre;

    /// <summary>Seconds more inside the machine for every level.</summary>
    public static double SuitSeconds(int level) => 2.0 * Math.Max(0, level - 1);
}
