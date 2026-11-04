using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

public enum GuildRole { Leader, Teacher, Member, Rival }

/// <summary>Someone in a guild: who they are (an ordinary person in the world), what they do there, and how they look.</summary>
/// <param name="Teaches">For a teacher, the class they teach; for a rival, the class they fight as.</param>
public sealed record GuildMember(Npc Npc, GuildRole Role, string Look, string? Teaches = null)
{
    public string Id => Npc.Id;
    public string Name => Npc.Name;
}

/// <summary>
/// A guild in Tristinwold: the warriors' Iron Circle, the rogues' Velvet Knives, the mages of the Magi, the Bards' College. Each has a
/// hall outside the walls, a leader, a teacher for each of its three classes, a few members with their own stories,
/// and a rival who wants everything you want.
/// </summary>
public sealed record Guild(Calling Calling, string Id, string Name, string Hall, string HallStory, double X, double Z,
    GuildMember Leader, IReadOnlyList<GuildMember> Teachers, IReadOnlyList<GuildMember> Members, GuildMember Rival)
{
    public IEnumerable<GuildMember> Everyone => [Leader, .. Teachers, .. Members, Rival];
    public GuildMember? TeacherOf(string discipline) => Teachers.FirstOrDefault(t => t.Teaches == discipline);
}

/// <summary>How far a hero has risen in their guild, from what they've done for it.</summary>
public enum GuildRank { Initiate, Member, Adept, Veteran, Master }

/// <summary>A choice in a piece of guild drama, and what comes of it.</summary>
/// <param name="Standing">How the guild thinks of you after.</param>
/// <param name="Rivalry">How much hotter (or cooler) your rival burns.</param>
/// <param name="Duel">This choice is settled with a duel against your rival: <paramref name="Outcome"/> is told if you win, <paramref name="Loss"/> if you lose.</param>
/// <param name="Honour">What the world makes of it: the good in it (see <see cref="Standing"/>)...</param>
/// <param name="Infamy">...and the evil. Every story has a choice with some of each.</param>
/// <param name="Deed">How people tell of it after ("sold a Knife to the Watch"); the story's title if null.</param>
public sealed record StoryChoice(string Label, string Outcome, int Standing = 0, int Rivalry = 0, int Crowns = 0, string? Flag = null,
    bool Duel = false, string? Loss = null, int Honour = 0, int Infamy = 0, string? Deed = null)
{
    public bool Good => Honour > Infamy;
    public bool Evil => Infamy > Honour;
}

/// <summary>
/// A piece of guild drama. Each guild has its stories, told one after another as you rise and as time passes: a
/// theft, a bribe, a betrayal, a succession. Someone in the guild comes to you with it, and what you choose matters.
/// </summary>
public sealed record Storylet(string Id, Calling Guild, GuildRank Rank, string TellerId, string Title, string Intro, IReadOnlyList<StoryChoice> Choices);

public static class Guilds
{
    /// <summary>Real minutes of play between one piece of guild drama and the next.</summary>
    public const double DramaGapSeconds = 15 * 60;

    public static readonly int[] RankStanding = [0, 25, 75, 150, 250];

    public static GuildRank RankOf(int standing) => (GuildRank)Enumerable.Range(0, RankStanding.Length).Last(i => standing >= RankStanding[i]);

    public static string Title(GuildRank rank, Guild g) => $"{rank} of {g.Name}";

    static Npc Person(string id, string name, Sex sex, int age, string job, string backstory, string greeting, double x, double z, string temper, params string[] interests) =>
        new($"guild/{id}", name, sex, age, job, "city:tristinwold", "Tristinwold", backstory, interests, temper, greeting, x, z);

    public static IReadOnlyList<Guild> All { get; } =
    [
        new(Calling.Warrior, "iron_circle", "the Iron Circle", "the Iron Circle's yard",
            "A ring of trodden earth outside the west gate, with weapon racks, straw targets, and a flag that has been mended more often than washed.",
            -128, -40,
            new(Person("brannoc", "Marshal Brannoc Steelhand", Sex.Male, 58, "master of the Iron Circle",
                "Brannoc trained the city watch for twenty years and the Tongs' best leg-breakers for ten before that. Now he trains anyone who can stand up again after he knocks them down. His brother went north after a frost troll and never came back.",
                "You want to fight? Everyone wants to fight. Show me you can get up.", -128, -40, "gruff", "blades", "old battles"), GuildRole.Leader, "warmaster"),
            [
                new(Person("yun", "Master Yun Tsavo", Sex.Male, 63, "teacher of the open hand",
                    "Yun came over the eastern sea with nothing but his hands, and has never needed more. He breaks boards with his forehead for children on feast days.",
                    "Your fists are already weapons. Your feet are better ones.", -136, -34, "warm", "dreams", "the sea"), GuildRole.Teacher, "MartialArtist", "MartialArtist"),
                new(Person("hrothga", "Hrothga Ironmane", Sex.Female, 41, "teacher of the axe",
                    "Hrothga was a chieftain of the Old Clans until her clan was a memory. She laughs louder than anyone in the yard, and hits harder.",
                    "Ha! Pick up the big one. No, the bigger one.", -120, -33, "cheerful", "brewing", "old battles"), GuildRole.Teacher, "Barbarian", "Barbarian"),
                new(Person("edwyn", "Ser Edwyn Halloran", Sex.Male, 52, "teacher of the oath",
                    "Ser Edwyn swore his oath at the Temple of the Tide and has kept every word of it, which has cost him two fortunes and a wife.",
                    "Light first. Then the sword. Never the other way.", -132, -48, "proud", "faith", "the dead"), GuildRole.Teacher, "Paladin", "Paladin"),
            ],
            [
                new(Person("tamsin", "Sergeant Tamsin Brack", Sex.Female, 46, "sergeant of the Iron Circle",
                    "Tamsin runs the drills and the gossip in the yard, and is better at both than anyone admits.",
                    "Drills at dawn. Gossip at dusk. Which are you here for?", -122, -46, "dry", "gossip", "old battles"), GuildRole.Member, "guard"),
                new(Person("odo", "Odo Fennick", Sex.Male, 19, "recruit of the Iron Circle",
                    "Odo walked in from a turnip farm with his grandfather's rusty sword and a great deal of hope.",
                    "Is it true you killed a troll? I've only killed turnips.", -125, -32, "nervous", "dreams", "horses"), GuildRole.Member, "squire"),
                new(Person("greta", "Greta Vosk", Sex.Female, 37, "armourer of the Iron Circle",
                    "Greta mends everything the yard breaks, and keeps a list of who breaks the most.",
                    "If you're here to have something fixed, you're on the list. Near the top.", -134, -42, "gruff", "blades", "money"), GuildRole.Member, "worker"),
            ],
            new(Person("corin", "Corin Vale", Sex.Male, 24, "the Iron Circle's rising blade",
                "Corin is the son of a Watch captain, handsome, quick, and sure that the yard will be his one day. He has never lost a bout he couldn't explain.",
                "Oh, it's you. Still here?", -138, -38, "proud", "blades", "money"), GuildRole.Rival, "Paladin", "Paladin")),

        new(Calling.Rogue, "velvet_knives", "the Velvet Knives", "the old tannery yard",
            "A tannery south of the walls that hasn't tanned anything in years. Nobody goes near it for the smell, which is exactly why the Knives keep it.",
            -40, 128,
            new(Person("vesper", "Vesper Quill", Sex.Female, 49, "mistress of the Velvet Knives",
                "Nobody knows how long Vesper has run the Velvet Knives. The Lantern Boys pay her a tithe, and the Tongs pretend she doesn't exist, which is how she likes it.",
                "You walk like a tourist. We can fix that.", -40, 128, "dry", "cards", "the Tongs"), GuildRole.Leader, "velvet"),
            [
                new(Person("wren", "Wren Ashcombe", Sex.Female, 33, "teacher of the bow and the wild",
                    "Wren grew up in the Greywood with wolves for neighbours, and still talks to them more than to people.",
                    "Quiet. You're scaring the birds.", -48, 122, "wary", "hunting", "wolves"), GuildRole.Teacher, "Ranger", "Ranger"),
                new(Person("pip", "Pip Marlowe", Sex.Male, 28, "teacher of light fingers",
                    "Pip has picked the pocket of every magistrate in Tristinwold and returned the purses with a note, just to prove he could.",
                    "Lovely boots. Shame about the purse. Here, have it back.", -32, 122, "cheerful", "cards", "money"), GuildRole.Teacher, "Thief", "Thief"),
                new(Person("silas", "Silas Morrow", Sex.Male, 55, "teacher of the last step",
                    "They call Silas the Quiet Man. He has never once raised his voice, and never once needed to.",
                    "...", -44, 136, "wary", "the dead", "the stars"), GuildRole.Teacher, "Assassin", "Assassin"),
            ],
            [
                new(Person("nell", "Nell Cutwater", Sex.Female, 44, "fence of the Velvet Knives",
                    "Nell can price anything stolen in Tristinwold by weight, smell, and the face of whoever's selling it.",
                    "What've you brought me? Don't say nothing. Nobody brings me nothing.", -34, 134, "warm", "money", "gossip"), GuildRole.Member, "traveller"),
                new(Person("jory", "Jory Ash", Sex.Male, 20, "lookout of the Velvet Knives",
                    "Jory sees everything from the rooftops and tells Vesper all of it, and you some of it.",
                    "Watch's changing shifts at the south gate. Not that you heard it from me.", -50, 130, "nervous", "gossip", "the Tongs"), GuildRole.Member, "protege"),
                new(Person("mags", "Mags Dunmore", Sex.Female, 39, "forger of the Velvet Knives",
                    "Mags can copy any hand in the city. She keeps a sick sister in the Warrens and never talks about her.",
                    "Need a seal? A signature? A new name? Mind your thumbs on the wax.", -46, 140, "bitter", "letters", "money"), GuildRole.Member, "scholar"),
            ],
            new(Person("sable", "Sable Rue", Sex.Female, 26, "the Velvet Knives' sharpest blade",
                "Sable came up from the Lantern Boys, and wants Vesper's chair the way other people want water.",
                "Careful. The new ones always trip over something.", -36, 140, "bitter", "cards", "the Tongs"), GuildRole.Rival, "Assassin", "Assassin")),

        new(Calling.Mage, "magi", "the Magi", "the Archive Garden",
            "A walled garden of strange herbs and stranger statues north-east of the walls, where the Magi teach what the Archive will not let out of its doors.",
            40, -128,
            new(Person("selwyn", "Archmage Selwyn Tarrow", Sex.Male, 71, "Archmage of the Magi",
                "Selwyn has led the Magi since before the Rift opened, and blames himself, privately, for not seeing it coming.",
                "Ah. Another one with sparks behind the eyes. Sit. Not on the mandrake.", 40, -128, "warm", "the stars", "the Magi"), GuildRole.Leader, "priest"),
            [
                new(Person("orla", "Magister Orla Penhallow", Sex.Female, 50, "teacher of the elements",
                    "Orla has burned down two towers and frozen a third, and considers all three successful experiments.",
                    "Fire, frost and storm. Pick one to start. You'll want all three by supper.", 32, -122, "cheerful", "alchemy", "weather"), GuildRole.Teacher, "Wizard", "Wizard"),
                new(Person("morwen", "Morwen Hesk", Sex.Female, 44, "teacher of pacts",
                    "Morwen is the Archive's tolerated witch. Her patron has no name she will say aloud, and her tea is excellent.",
                    "Everyone makes bargains. I just read mine first.", 48, -122, "dry", "the dead", "dreams"), GuildRole.Teacher, "Warlock", "Warlock"),
                new(Person("daro", "Daro Vey", Sex.Male, 31, "teacher of the blood",
                    "Daro's grandmother was, he insists, a dragon. Nobody argues; when he sneezes, the curtains catch.",
                    "Don't think about the magic. Let it think about you.", 36, -136, "cheerful", "the Rift", "the stars"), GuildRole.Teacher, "Sorcerer", "Sorcerer"),
            ],
            [
                new(Person("anselm", "Brother Anselm", Sex.Male, 67, "copyist of the Magi",
                    "Anselm has copied every book in the Archive by hand, some of them twice, and remembers the ones that are missing.",
                    "Mind the ink. Mind the pages. Mind everything, really.", 46, -134, "nervous", "letters", "the Magi"), GuildRole.Member, "scholar"),
                new(Person("liesl", "Liesl Varro", Sex.Female, 17, "apprentice of the Magi",
                    "Liesl has nightmares about a door with gold light behind it, and has started to dream of it in the day.",
                    "Do you ever dream of a door? No? Good. Good.", 34, -130, "nervous", "dreams", "the Rift"), GuildRole.Member, "apprentice"),
                new(Person("teodric", "Teodric Gale", Sex.Male, 45, "alchemist of the Magi",
                    "Teodric brews potions for the Watch and poisons for no one, he says, with a wink.",
                    "Don't drink the green one. Or the blue one. Actually, don't drink anything.", 44, -120, "dry", "alchemy", "mushrooms"), GuildRole.Member, "scholar"),
            ],
            new(Person("isadora", "Isadora Crane", Sex.Female, 23, "the Magi's prodigy",
                "Isadora read the whole open Archive by fifteen and the locked shelves by seventeen, and has never forgiven anyone for being slower.",
                "Oh. You're the one they let in. How generous of them.", 38, -140, "proud", "the Magi", "maps"), GuildRole.Rival, "Wizard", "Wizard")),

        new(Calling.Bard, "bards_college", "the Bards' College", "the Lyre Hall",
            "A timbered hall on a green south-east of the walls, with a stage at one end, a library at the other, and a song going somewhere in it at every hour of the day and night.",
            60, 135,
            new(Person("odalys", "Laureate Odalys Wren", Sex.Female, 63, "Laureate of the Bards' College",
                "Odalys sang for three kings and outlived them all. She says the trick is never to rhyme anything with their names.",
                "Sit. Listen first. Everyone wants to sing before they've heard a single song.", 60, 135, "warm", "songs", "old battles"), GuildRole.Leader, "laureate"),
            [
                new(Person("cassian", "Cassian Vell", Sex.Male, 39, "teacher of verse",
                    "Cassian once ended a duel by reciting a sonnet so cruel his opponent wept and went home. He regrets it, he says, every day but the good ones.",
                    "Say it again. But mean it this time.", 54, 129, "dry", "letters", "songs"), GuildRole.Teacher, "Poet", "Poet"),
                new(Person("hollis", "Mother Hollis Quell", Sex.Female, 58, "teacher of the written word",
                    "Hollis has had ink under her nails for thirty years. She writes the city's laws, its contracts and, quietly, its curses.",
                    "Spell your name. Properly. Names matter more than you'd think.", 66, 129, "gruff", "letters", "the Magi"), GuildRole.Teacher, "Scribe", "Scribe"),
                new(Person("bram", "Bram Tolliver", Sex.Male, 46, "teacher of music",
                    "Bram has played every tavern from here to the Greywood, and been thrown out of most of them for playing too well.",
                    "Can you keep a beat? Then you can keep a company alive.", 60, 143, "cheerful", "songs", "brewing"), GuildRole.Teacher, "Musician", "Musician"),
            ],
            [
                new(Person("tobin", "Tobin Marrow", Sex.Male, 15, "the College's drummer boy",
                    "Tobin ran away from a Tong ship to join the College, and drums on every surface he passes.",
                    "Want to hear a rhythm? Too late, you're hearing it.", 52, 139, "cheerful", "songs", "the sea"), GuildRole.Member, "youth"),
                new(Person("elowen", "Elowen Sark", Sex.Female, 34, "keeper of the College library",
                    "Elowen catalogues every song ever sung in Tristinwold, and a few that never should have been.",
                    "Quietly, please. Some of these books are asleep.", 68, 141, "nervous", "letters", "dreams"), GuildRole.Member, "scholar"),
                new(Person("hal", "Hal Brandish", Sex.Male, 52, "a retired herald",
                    "Hal cried the fall of the Bell Keep to the whole city, and has never quite stopped crying things.",
                    "HEAR YE! Oh. Sorry. Habit.", 56, 145, "cheerful", "old battles", "gossip"), GuildRole.Member, "townsfolk"),
            ],
            new(Person("fennick", "Fennick Lark", Sex.Male, 24, "the College's golden voice",
                "Fennick has a voice like honey and a heart like a locked box. He wants the Laureate's chair, the crowd's love, and you a little quieter.",
                "Oh, you sing? How brave.", 64, 147, "proud", "songs", "cards"), GuildRole.Rival, "Musician", "Musician")),

        new(Calling.Druid, "root_circle", "the Root Circle", "the Heartwood",
            "A ring of old stones among the roots at the foot of the Heartwood, where the druids of the Northwood have met since long before anyone wrote it down. The stones are warm, and nobody will say why.",
            -128, -612,
            new(Person("circle_ilmara", "Ilmara Barkwise", Sex.Female, 88, "Eldest of the Root Circle",
                "Ilmara has been asking the forest the same question for sixty years. She says it's almost finished answering.",
                "The wood has noticed you. That's not a compliment, yet.", -128, -612, "kindly", "the forest", "herbs"), GuildRole.Leader, "priest"),
            [
                new(Person("circle_sekh", "Sekh Marshtongue", Sex.Male, 46, "teacher of the elements",
                    "A Scalkin druid from the deep marsh, Sekh talks to rain the way others talk to dogs.",
                    "Weather isn't a thing that happens. It's a thing that decides.", -116, -606, "dry", "the forest", "the sea"), GuildRole.Teacher, "Stormdruid", "Stormdruid"),
                new(Person("circle_hask", "Hask Nine-Hides", Sex.Male, 39, "teacher of the shapes",
                    "Hask has spent more of his life on four legs than on two, and is still getting used to chairs.",
                    "Fight it enough and you'll know it. Know it enough and you'll be it.", -138, -600, "gruff", "old battles", "wolves"), GuildRole.Teacher, "Shapeshifter", "Shapeshifter"),
                new(Person("circle_yrsa", "Yrsa Paleleaf", Sex.Female, 58, "teacher of the spirits",
                    "Yrsa keeps the company of everything the forest has ever buried, and says the conversation is better.",
                    "Nothing in this wood has ever really left it.", -146, -616, "warm", "the dead", "dreams"), GuildRole.Teacher, "Spiritcaller", "Spiritcaller"),
            ],
            [
                new(Person("circle_tam", "Tamsin Rootfoot", Sex.Female, 24, "novice of the Circle",
                    "Tamsin came to learn herbs and has stayed to learn why the herbs keep talking back.",
                    "It's not the mushrooms. I checked. Twice.", -120, -624, "cheerful", "herbs", "gossip"), GuildRole.Member, "traveller"),
                new(Person("circle_orrin", "Orrin Moss", Sex.Male, 62, "keeper of the stones",
                    "Orrin sweeps the leaves from the ring every morning and puts them back every night.",
                    "Mind the stones. They mind you.", -134, -628, "gruff", "the forest", "dreams"), GuildRole.Member, "hunter"),
                new(Person("circle_wynn", "Wynn Sorrel", Sex.Female, 35, "the Circle's healer",
                    "Wynn sees to anyone who comes in from the east of the wood with the rot on them, and doesn't ask what they were doing there.",
                    "Hold still. It'll hurt, and then it won't.", -112, -618, "kindly", "herbs", "the road"), GuildRole.Member, "priest"),
            ],
            new(Person("circle_vaun", "Vaun Thornhand", Sex.Male, 33, "the Circle's brightest",
                "Vaun can do anything the old druids can, faster, and has never once asked whether it should be done.",
                "Still asking the trees for permission? Sweet.", -142, -626, "proud", "the dead", "the forest"), GuildRole.Rival, "Shapeshifter", "Shapeshifter")),
        new(Calling.Artificer, "brass_foundry", "the Brass Foundry", "the Foundry yard",
            "A yard of furnaces, gantries and half-built engines west of the walls, past the Greyhollow quarry, under a brass chimney that smokes at all hours. Something is always being tested, and something is always on fire.",
            -165, 40,
            new(Person("hesper", "Foundress Hesper Coil", Sex.Female, 61, "Foundress of the Brass Foundry",
                "Hesper built the Foundry's first golem out of a shipwreck's fittings and a temple bell, and has been improving on it for thirty years. She has lost two fingers and gained a guild.",
                "Mind the acid. And the gears. And Jonquil.", -165, 40, "dry", "engines", "the sea"), GuildRole.Leader, "scholar"),
            [
                new(Person("ambrose", "Doctor Ambrose Vell", Sex.Male, 48, "teacher of reagents",
                    "Ambrose was the Exchange's assayer until he worked out he could make what he was assaying. He teaches alchemy, and the price of everything.",
                    "Everything is a reagent. Even you. Especially the Exchange.", -178, 48, "warm", "money", "herbs"), GuildRole.Teacher, "Alchemist", "Alchemist"),
                new(Person("thessaly", "Captain-Engineer Thessaly Brand", Sex.Female, 39, "teacher of ether and engines",
                    "Thessaly flew the Foundry's one airship, and brought the compass down with her when it burned. She fits the Brine Company's ships now, and dreams of the sky.",
                    "A ship's an engine with a hull on. So are you, with a golem on.", -153, 49, "brisk", "the sea", "engines"), GuildRole.Teacher, "EtherAeronaut", "EtherAeronaut"),
                new(Person("wick", "Wick Harrow", Sex.Male, 44, "teacher of the wrench",
                    "Wick learned his trade taking wagons apart that weren't his, and the Foundry took him on rather than keep buying its own gears back from him.",
                    "Anything built can be unbuilt. Quicker, usually.", -169, 49, "dry", "cards", "the Tongs"), GuildRole.Teacher, "Saboteur", "Saboteur"),
            ],
            [
                new(Person("marigold", "Marigold Spanner", Sex.Female, 23, "apprentice of the Foundry",
                    "Marigold keeps the furnaces and the stills, and has blown up only one of each.",
                    "If it's ticking, step back. If it's hissing, run.", -161, 45, "cheerful", "engines", "dreams"), GuildRole.Member, "worker"),
                new(Person("osgood", "Osgood Flint", Sex.Male, 57, "the Foundry's quartermaster",
                    "Osgood buys the Foundry's brass, coal and glass, and sells what it makes to the Exchange, the company and anyone else with coin.",
                    "Brass is dear this week. Everything is dear this week.", -175, 43, "gruff", "money", "the Tongs"), GuildRole.Member, "traveller"),
                new(Person("tilda", "Tilda Rook", Sex.Female, 31, "the Foundry's golem-wright",
                    "Tilda tunes the golems' hearts, and talks to them more kindly than to anyone with a pulse.",
                    "He likes you. The golem. He doesn't like many.", -155, 43, "warm", "engines", "wolves"), GuildRole.Member, "scholar"),
            ],
            new(Person("jonquil", "Jonquil Pike", Sex.Female, 27, "the Foundry's brightest spark",
                "Jonquil builds faster than anyone in the yard, tests nothing, and has never once admitted an engine of hers failed on its own.",
                "Still building the slow way? Sweet.", -159, 53, "proud", "engines", "money"), GuildRole.Rival, "EtherAeronaut", "EtherAeronaut")),
    ];

    public static Guild Of(Calling c) => All.First(g => g.Calling == c);
    public static Guild? OfMember(string npcId) => All.FirstOrDefault(g => g.Everyone.Any(m => m.Id == npcId));
    public static GuildMember? Member(string npcId) => All.SelectMany(g => g.Everyone).FirstOrDefault(m => m.Id == npcId);

    // ---- drama ------------------------------------------------------------------------------------

    public static IReadOnlyList<Storylet> Drama { get; } =
    [
        new("broken_blade", Calling.Warrior, GuildRank.Initiate, "guild/tamsin", "The Broken Blade",
            "Somebody snapped the Marshal's old sword in the night: the one he carried at the Bell Keep. Corin says he saw young Odo by the racks. Odo swears he never touched it, and he's near tears. Brannoc wants to know what you make of it.",
            [
                new("\"Corin's lying to look good.\"", "You call Corin a liar in front of the whole yard. Then Greta finds frost in the break: the cold snapped it, not a hand. Odo is cleared, Brannoc grunts, and Corin will never forget it.", 10, 2, Flag: "defended_odo", Honour: 3, Deed: "stood up for a farm boy in the Iron Circle's yard"),
                new("\"Odo was there. Let him answer for it.\"", "Odo is set to scrub the barracks for a month. A week later Greta finds frost in the break: nobody broke it. Odo won't meet your eye, and Corin smiles at you like a friend.", -5, -1, Flag: "blamed_odo", Infamy: 3, Deed: "let a farm boy take the blame for a broken sword"),
                new("\"Look at the steel before you look at anyone.\"", "Greta holds the halves to the light: frost in the grain. The cold snapped it. Nobody is to blame, and Brannoc looks at you like a man who's just found a sergeant.", 15, Flag: "wise", Honour: 2, Deed: "looked at the steel before the man"),
            ]),
        new("tong_purse", Calling.Warrior, GuildRank.Member, "guild/greta", "The Tong's Purse",
            "A Ravenscrew Tong boss has sent a purse to the yard: fat, heavy, and asking only that the Iron Circle look the other way when the Tongs do business at the docks. The roof leaks, Brannoc's tempted, and he's asked you.",
            [
                new("\"Take it. Roofs don't mend themselves.\"", "The roof is mended. The docks get a little darker. Some of the yard look at you differently now.", -10, 0, 60, "tong_money", Infamy: 4, Deed: "took the Tongs' money for the Iron Circle"),
                new("\"Send it back, with a message.\"", "The purse goes back with a dented helmet in it. The Tongs take the point, and the yard cheers you in the drills.", 15, Flag: "tong_refused", Honour: 4, Deed: "sent the Tongs' purse back in a dented helmet"),
                new("\"Take it, and use it against them.\"", "Half the purse mends the roof; the other half buys the names of the Tong's dock men, who find the Watch waiting for them. Brannoc laughs for the first time in a month.", 10, 0, 30, "double_game", Honour: 2, Deed: "turned the Tongs' own silver against them"),
            ]),
        new("first_blade", Calling.Warrior, GuildRank.Adept, "guild/brannoc", "First Blade",
            "Brannoc is getting old, and the yard needs someone to lead the drills: the First Blade. It's you or Corin, and the yard wants it settled the old way.",
            [
                new("\"I'll fight him for it.\"", "Corin goes down in the dust, and the yard roars. Brannoc hands you the drill-staff: First Blade of the Iron Circle. Corin gets up slowly, and says nothing at all.", 25, 2, Flag: "first_blade", Honour: 2, Deed: "won First Blade of the Iron Circle fair", Duel: true,
                    Loss: "Corin's blade is at your throat, and the yard goes quiet. He's First Blade. \"Next time,\" he says, and for once he doesn't smirk."),
                new("\"Let Corin have it. The yard needs him more.\"", "Corin stares at you, then nods, and something between you is a little less sharp. The yard thinks you're either very wise or very soft.", 5, -3, Flag: "gave_blade", Honour: 2, Deed: "gave up First Blade for the yard's sake"),
                new("\"Cut Corin's saddle-girth the night before.\"", "Corin's girth parts in the first pass and he goes down in the dust, white with rage. The yard cheers; Greta looks at the cut leather, then at you, and says nothing. First Blade, by a knife in the dark.", 15, 4, Flag: "cut_girth", Infamy: 5, Deed: "won First Blade by cutting a girth in the dark"),
            ]),
        new("marshals_last_fight", Calling.Warrior, GuildRank.Veteran, "guild/tamsin", "The Marshal's Last Fight",
            "Brannoc is going north. A frost troll's been seen at the old place where his brother died, and he means to finish it, alone if he must. He's asked you to keep the yard while he's gone.",
            [
                new("\"I'm going with him.\"", "You go north together, and come back together, with a troll's tusk and Brannoc's brother's ring. He gives you the ring. The yard calls you Master of the Circle.", 30, 0, 100, "went_north", Honour: 4, Deed: "went north after a troll with the Marshal"),
                new("\"I'll keep the yard.\"", "The yard holds while he's gone; when he comes back, limping, with a troll's tusk, he finds the drills sharper than he left them. Master of the Circle, he calls you.", 30, Flag: "kept_yard", Honour: 2, Deed: "kept the Iron Circle's yard while the Marshal went north"),
                new("\"Let him go alone, and take the yard while he's gone.\"", "He goes north alone. While he's gone you take the drill-staff, the ledger and the flag, and when he limps back with a troll's tusk the yard is yours and the men won't meet his eye. Master of the Circle, he calls you, and means something else.", 30, 1, 50, "took_yard", Infamy: 6, Deed: "took the Iron Circle's yard behind the Marshal's back"),
            ]),

        new("skimming", Calling.Rogue, GuildRank.Initiate, "guild/nell", "Skimming",
            "The take's forty crowns light this month. The ledger's been doctored: Mags's hand, Nell's sure of it. Sable is saying it was Jory. Vesper hasn't said anything, which is worse.",
            [
                new("\"Cover for Mags.\"", "Mags is skimming for her sister's medicine. You quietly put forty of your own crowns in the box. Vesper notices, of course, and says nothing: loyalty's worth forty crowns to her.", 10, 0, -40, "covered_mags", Honour: 3, Deed: "paid a forger's debt for her sick sister"),
                new("\"Tell Vesper the truth.\"", "Vesper hears it, sends Mags's sister a healer, and docks Mags a month. The Knives respect the truth, mostly. Sable is furious her story fell apart.", 10, 1, Flag: "told_vesper", Honour: 1, Deed: "told the Knives the truth about the skimming"),
                new("\"Let Sable have Jory.\"", "Jory takes the blame and a beating. Sable owes you one now. The rooftops go quiet when you pass.", -10, -2, Flag: "sold_jory", Infamy: 4, Deed: "let a lookout take a beating for someone else's theft"),
            ]),
        new("lantern_boys", Calling.Rogue, GuildRank.Member, "guild/jory", "The Lantern Boys",
            "The Lantern Boys are working Knives streets, and the Gutter Prince is laughing about it. Vesper wants a message sent. She's left the kind of message up to you.",
            [
                new("\"Rob their stash.\"", "You lift the Lantern Boys' whole stash from under the Prince's nose. They're a laughing-stock by morning, and you're richer.", 10, 0, 50, "robbed_lanterns", Infamy: 2, Deed: "robbed the Lantern Boys blind"),
                new("\"Parley with the Gutter Prince.\"", "You sit down with the Gutter Prince over bad wine, and come away with a border, a truce, and the beginning of a strange friendship.", 15, Flag: "parley", Honour: 3, Deed: "made peace with the Gutter Prince"),
                new("\"Burn their den.\"", "The Lantern Boys' den burns. Nobody dies, quite. Vesper looks at you for a long time, then pours you a drink.", 5, -1, Flag: "burned_den", Infamy: 5, Deed: "burned the Lantern Boys' den"),
            ]),
        new("who_sold_us", Calling.Rogue, GuildRank.Adept, "guild/vesper", "Who Sold Us",
            "The Watch was waiting at the counting-house job. Someone sold out the crew. Sable says it was you, loudly, to anyone who'll listen.",
            [
                new("\"Then Sable can prove it with a blade.\"", "Sable yields with your knife at her collar, and the yard believes you. The real informant is found a week later: a Watch spy among the fences.", 20, 2, Flag: "beat_sable", Honour: 1, Deed: "cleared your name with a blade", Duel: true,
                    Loss: "Sable's knife is at your collar. The yard doesn't believe her, quite, but they don't believe you either. The real informant turns up a week later, and nobody apologises."),
                new("\"I'll find the real one.\"", "Three nights on the rooftops with Jory, and you find him: a fence called Coyle, selling to the Watch. Vesper deals with him. Sable doesn't apologise, but she stops talking.", 20, Flag: "found_informant", Honour: 2, Deed: "found the Watch's spy among the fences"),
                new("\"Sell Coyle to the Watch yourself, first.\"", "You find the fence Coyle in three nights, and sell him to the Watch before Vesper can get to him: the reward is yours, and the Knives never learn who had their names. Jory looks at the coin and says nothing.", 10, 0, 80, "sold_coyle", Infamy: 4, Deed: "sold a fence to the Watch for the reward"),
            ]),
        new("velvet_crown", Calling.Rogue, GuildRank.Veteran, "guild/nell", "The Velvet Crown",
            "Vesper is tired. She wants to step back, and the Knives need someone with fingers in every pocket in the city. Nell says it's you. Sable says it's her.",
            [
                new("\"I'll take it.\"", "Vesper gives you her ring, and her ledger, and her rooftop. The Knives are yours. Sable leaves the city for a while.", 30, 1, 150, "velvet_crown", Honour: 1, Deed: "took the Velvet Knives' chair"),
                new("\"Give it to Sable.\"", "You stand aside, and Sable takes the chair, astonished. She owes you everything, and she knows it. Vesper calls you the cleverest knife she ever sharpened.", 20, -5, Flag: "crowned_sable", Honour: 3, Deed: "gave the Velvet Knives to your rival"),
                new("\"Take it, and sell the ledger to the Tongs.\"", "You take the ring and the ledger, and sell the ledger's pages to the Tongs one at a time. The Knives are yours, and poorer, and the Tongs know every fence in the city. Vesper leaves for the coast without a word.", 20, 2, 250, "sold_ledger", Infamy: 7, Deed: "sold the Velvet Knives' ledger to the Tongs"),
            ]),

        new("missing_book", Calling.Mage, GuildRank.Initiate, "guild/anselm", "The Missing Book",
            "A book is missing from the locked shelves: \"On the Seams of the World\", about the Rift. Isadora was last in the stacks. Liesl has been dreaming of doors. Brother Anselm is beside himself.",
            [
                new("\"Let me look in Liesl's room.\"", "The book is under Liesl's pillow. She's been reading it to make the dreams stop. They haven't. You take it back quietly, and Liesl starts trusting you.", 10, Flag: "liesl_book", Honour: 2, Deed: "found a missing book without making a scandal"),
                new("\"Ask Isadora.\"", "Isadora is icily offended, didn't take it, and tells the whole Archive you suspected her. Anselm finds it under Liesl's pillow the next day.", 0, 2, Flag: "accused_isadora", Infamy: 1, Deed: "accused a prodigy of theft in front of the whole Archive"),
                new("\"Tell the Archmage.\"", "Selwyn finds the book and the girl, and sits with Liesl a long while. He thanks you for not making it a scandal.", 15, Flag: "told_selwyn", Honour: 2, Deed: "took a lost girl's trouble to the Archmage"),
                new("\"Find it, and sell it.\"", "The book is under Liesl's pillow. You take it, and a collector in the Warrens pays well for a book from the locked stacks. Anselm reports it lost; Liesl stops sleeping again.", -5, 0, 90, "sold_book", Infamy: 4, Deed: "sold a book from the Archive's locked stacks"),
            ]),
        new("rift_light", Calling.Mage, GuildRank.Member, "guild/liesl", "Rift-Light",
            "Liesl has a vial of gold light, bled from the vein of a corrupted dragon. She thinks it can show her what's behind the door in her dreams. She's asking you to help her study it.",
            [
                new("\"Destroy it. Now.\"", "The vial shatters on the flagstones and the light screams as it goes out. Liesl cries, then sleeps through the night for the first time in a year.", 10, Flag: "destroyed_vial", Honour: 3, Deed: "smashed a vial of Rift-light"),
                new("\"Let's study it, carefully.\"", "You learn how the Rift moves through a dragon's blood, and how it can be slowed. Liesl learns too much, and draws doors in the margins of everything.", 5, Flag: "rift_study", Infamy: 1, Deed: "studied the Rift in a dragon's blood"),
                new("\"Take it to the Archmage.\"", "Selwyn locks the vial in the deepest vault, and tells the Magi to watch the corrupted dragons closely. He watches Liesl closely too.", 15, Flag: "vial_to_selwyn", Honour: 3, Deed: "locked a vial of Rift-light in the deepest vault"),
                new("\"Sell it to the Tongs. They'll pay anything.\"", "A Tong captain pays a fortune for a vial of screaming light, and asks no questions. You don't ask what it's for. A month later a dock warehouse burns violet for a night.", -10, 0, 200, "sold_vial", Infamy: 7, Deed: "sold a vial of Rift-light to the Tongs"),
            ]),
        new("prodigys_wager", Calling.Mage, GuildRank.Adept, "guild/isadora", "The Prodigy's Wager",
            "There's a seat on the Archive council, and Isadora wants it. So, apparently, do you. She proposes a wager: a mage's duel in the garden, winner takes the seat.",
            [
                new("\"Accept.\"", "Isadora's ward breaks and she lowers her hands, white-faced. The seat is yours. \"Again,\" she says, \"some day,\" and means it.", 25, 2, Flag: "council_seat", Honour: 1, Deed: "won a seat on the Magi's council", Duel: true,
                    Loss: "Your ward breaks first. Isadora takes the seat, and for a moment she almost looks sorry."),
                new("\"Take the seat yourself, Isadora.\"", "Isadora stares at you, suspicious of kindness. Then she takes the seat, and votes your way on the first thing that matters.", 5, -3, Flag: "gave_seat", Honour: 3, Deed: "gave a council seat to your rival"),
                new("\"Accept, and dose her tea first.\"", "Isadora's hands shake before the first ward, and her ward breaks like glass. The seat is yours. She knows, later, from the taste she remembers, and never says a word to anyone, which is worse.", 20, 5, Flag: "dosed_isadora", Infamy: 5, Deed: "dosed a rival's tea before a duel"),
            ]),
        new("what_ilse_keeps", Calling.Mage, GuildRank.Veteran, "guild/selwyn", "What Ilse Keeps",
            "Sister Ilse at the Archive keeps a second key, to a shelf that isn't on any list. Selwyn has known for years. Now he's afraid of what's on it: a record, in Ilse's hand, of a man who was saved from drowning, and the name he took.",
            [
                new("\"Keep her secret.\"", "You leave the shelf locked. Ilse thanks you without saying for what, and some nights you wonder about the saved man, and the name he took.", 25, Flag: "kept_ilse_secret", Honour: 2, Deed: "kept a librarian's secret"),
                new("\"Read it.\"", "The page is in Ilse's hand: a man pulled from the harbor, who should have died, and who took the name Vann. You put it back. You don't sleep well.", 20, Flag: "read_vann", Deed: "read what Ilse keeps"),
                new("\"Sell what Ilse keeps to the Tongs.\"", "The Tongs pay in gold for the name of the man pulled from the harbor. You don't see what they do with it. Ilse doesn't know it was you; Vann, somewhere, finds his door watched.", -15, 0, 150, "sold_vann", Infamy: 8, Deed: "sold a saved man's secret to the Tongs"),
            ]),

        new("marked_oak", Calling.Druid, GuildRank.Initiate, "guild/circle_tam", "The Marked Oak",
            "Loggers from Druitt have marked a great oak at the wood's edge for felling. Tamsin swears the tree flinched when the axe went in, and wants it stopped. The loggers have families, and Druitt lives on timber.",
            [
                new("\"Talk to the loggers. There are other trees.\"", "You walk the stand with the foreman and show him three that'll come down clean. He grumbles and takes them. The oak keeps its one scar, and Tamsin sits under it all afternoon.", 15, -1, Flag: "spared_oak", Honour: 3, Deed: "talked Druitt's loggers off an old oak"),
                new("\"Salt the stump of their camp. Teach them.\"", "Their tools rust and their water goes bad, and the loggers go home frightened. Tamsin is delighted, until a logger's child takes sick from the well.", 5, 0, Flag: "fouled_camp", Infamy: 5, Deed: "fouled a loggers' camp"),
                new("\"It's a tree, Tamsin.\"", "It takes them a day to bring it down. Tamsin doesn't speak to you for a month, and the wood goes quiet round you when you walk it.", -5, 2, Flag: "let_oak_fall", Infamy: 1, Deed: "stood by while the old oak fell"),
            ]),
        new("druitt_contract", Calling.Druid, GuildRank.Member, "guild/circle_orrin", "The Druitt Contract",
            "Druitt's reeve has offered the Circle a tenth of every load if it will bless the lumber trade and tell its loggers which stands to cut. Orrin thinks it'd save the old trees. Ilmara hasn't said what she thinks, which is never a good sign.",
            [
                new("\"Take the deal, and steer them from the old growth.\"", "The carts roll, the old stands are left alone, and the Circle has a new roof. Every new house in Druitt is another thing Ilmara frowns at.", 15, 0, 80, "blessed_trade", Honour: 2, Deed: "steered Druitt's axes away from the old wood"),
                new("\"No deal. The wood isn't ours to sell.\"", "The reeve goes home angry and cuts where he likes. But nobody can ever say the Circle sold a single tree, and Ilmara nods at you, once.", 10, 0, Flag: "refused_druitt", Honour: 3, Deed: "refused to sell the wood's blessing"),
                new("\"Take the deal. Pocket the tenth.\"", "You tell Druitt the old growth is fine timber and keep the coin. It's a very good year for you, and a terrible one for trees that had stood since before the city.", -10, 0, 200, "sold_old_growth", Infamy: 6, Deed: "sold out the old growth for a tithe"),
            ]),
        new("thornhand_rite", Calling.Druid, GuildRank.Adept, "guild/circle_vaun", "The Thornhand Rite",
            "Vaun says the old rites are slow and the Circle is frightened of its own power. He has found a working in the oldest stones: a merging, druid and tree, that would make one of you stronger than the whole Circle. He wants to try it, and he wants you to try to stop him.",
            [
                new("\"Fight me first.\"", "You put him on his back among the stones, and the working goes out like a candle. He lies there and laughs, and doesn't thank you. Ilmara does, and she's shaking when she does it.", 25, 2, Flag: "stopped_vaun", Honour: 3, Deed: "stopped a merging at the old stones", Duel: true,
                    Loss: "He's quicker than you, and worse: the roots are helping him. You wake in the leaves, the stones warm under you. He stopped the working himself, and he won't say why."),
                new("\"Show me how it's done.\"", "It takes the skin of your left hand for three days. It's bark. It goes back. You've never felt so strong, and you can't stop thinking about how much better it would be if it didn't.", 10, 4, Flag: "learned_merging", Infamy: 5, Deed: "learned a forbidden merging"),
                new("\"Tell Ilmara.\"", "The stones are moved in the night, by a lot of very old people who don't discuss it. Vaun doesn't forgive you. Nobody asks you to.", 15, 3, Flag: "told_ilmara", Honour: 2, Deed: "took the merging to the Eldest"),
            ]),
        new("wardens_of_the_maw", Calling.Druid, GuildRank.Veteran, "guild/circle_ilmara", "The Wardens of the Maw",
            "Ilmara takes you to the oldest stone and tells you what the Circle has kept for a thousand years. The guardians were druids: the greatest there ever were. They merged with the treants for power, and never knew the spell couldn't be undone. The fifth went wrong, and came to see every house and every cut branch as a sickness. The four turned on it and sealed it in the earth, and they are the seal. They aren't protecting the wood from the axe. They're protecting the world from what's under it.",
            [
                new("\"Then nobody must ever touch them. Tell the village.\"", "You stand up in Hollowheart and say it aloud for the first time in a thousand years. Lu Wukang looks at you for a long while. Oruun closes his eyes. Some of the Photosynthens weep. They'd always known, they say. They'd only never been told.", 30, Flag: "told_mythos", Honour: 5, Deed: "told Hollowheart what the guardians truly are"),
                new("\"Keep it. It's held this long.\"", "Ilmara names you her heir, and you carry it as she has, in the dark, alone. It's heavier than it looks.", 30, Flag: "kept_mythos", Honour: 2, Deed: "took up the Circle's oldest secret"),
                new("\"So who's cracking the seal?\"", "She shows you the light in the east of the wood, spreading into Ashvael like a stain. It isn't sickness, she says. It's aimed. Something wants the wardens killed, and it's made sure somebody will have to.", 25, Flag: "saw_the_light", Honour: 1, Deed: "learned what the Foul Light is"),
                new("\"How much would Raffkin pay to know that?\"", "He pays in old gold, and laughs until the butterflies leave him. Ilmara names no heir that year, and the stones go cold under your feet.", 10, 0, 300, "sold_mythos", Infamy: 9, Deed: "sold the Circle's oldest secret to Raffkin"),
            ]),
        new("stolen_song", Calling.Bard, GuildRank.Initiate, "guild/elowen", "The Stolen Song",
            "A song is going round the taverns that Tobin wrote, and it's a good one. Fennick is singing it as his own, and the crowds love him for it. Tobin says nothing, but he's stopped drumming.",
            [
                new("\"Call Fennick out in front of the crowd.\"", "You stand up in the Salt Barrel and sing Tobin's second verse, the one Fennick never heard. The crowd turns on him. Tobin drums all the way home.", 10, 2, Flag: "exposed_fennick", Honour: 2, Deed: "shamed a song-thief in the Salt Barrel"),
                new("\"Help Tobin write a better one.\"", "You and Tobin write a song so good nobody remembers the first. Fennick can't sing this one: it's about him.", 15, 1, Flag: "pips_song", Honour: 3, Deed: "helped a drummer write a better song"),
                new("\"Let it go. Songs belong to everyone.\"", "Tobin nods, and goes quiet. Fennick buys you a drink that tastes of something you can't name.", -5, -1, Flag: "let_song_go", Infamy: 1, Deed: "let a song-thief keep what he stole"),
                new("\"Sell Tobin the rest of his own song back.\"", "Tobin pays you half his winter's coin for the second verse he wrote himself, and thanks you. Fennick buys you a drink and asks how you did it.", -5, 0, 40, "sold_tobin", Infamy: 4, Deed: "sold a drummer his own song"),
            ]),
        new("the_pamphlet", Calling.Bard, GuildRank.Member, "guild/hollis", "The Pamphlet",
            "A Tong boss wants a pamphlet written: lies about a magistrate who won't take his bribes. He's offered the College a fat purse to print it. The roof leaks, Odalys hasn't said no, and Hollis has asked you.",
            [
                new("\"Write it. The College needs the money.\"", "The pamphlet goes out and the magistrate is ruined. The roof is mended. Hollis won't look at it.", -10, 0, 60, "wrote_libel", Infamy: 5, Deed: "ruined a magistrate with a pamphlet of lies"),
                new("\"Write the truth about the Tong instead.\"", "You write the truth, about the Tong boss, and the whole city reads it. The Watch pays him a visit. Hollis frames a copy.", 15, Flag: "wrote_truth", Honour: 4, Deed: "wrote the truth about a Tong boss for all to read"),
                new("\"Write it, with his name in the acrostic.\"", "The pamphlet goes out. Read the first letter of every line and it spells the Tong boss's name, and LIAR. The magistrate laughs; the boss can't prove a thing.", 10, 0, 30, "acrostic", Honour: 1, Deed: "spelled a Tong boss's name down the side of a pamphlet"),
            ]),
        new("midwinter_contest", Calling.Bard, GuildRank.Adept, "guild/bram", "The Midwinter Contest",
            "The Midwinter Contest: the College's best before the whole city, and the winner sings at the Crown. It's you or Fennick, and Bram wants it settled the old way, on the green.",
            [
                new("\"Then let's play.\"", "Fennick's last note cracks and the green goes silent; yours rings to the walls. You'll sing at the Crown. He shakes your hand, and his is cold.", 25, 2, Flag: "midwinter", Honour: 1, Deed: "won the Midwinter contest", Duel: true,
                    Loss: "Fennick's song lifts the whole green, and yours is lost under it. He'll sing at the Crown. \"Next winter,\" he says, kindly, which is worse."),
                new("\"Let Fennick sing. He needs it more.\"", "Fennick stares at you, then sings the best he ever has, and finds you after to say so. Something between you is a little less of a contest.", 5, -3, Flag: "gave_midwinter", Honour: 3, Deed: "gave the Midwinter song to your rival"),
                new("\"Loosen Fennick's strings before he plays.\"", "Fennick's top string goes in the second verse, and the green laughs him off. You'll sing at the Crown. He finds the cut later, and finds you, and only looks.", 20, 5, Flag: "cut_strings", Infamy: 5, Deed: "cut a rival's strings before the Midwinter contest"),
            ]),
        new("last_verse", Calling.Bard, GuildRank.Veteran, "guild/odalys", "The Last Verse",
            "Odalys is dying, slowly and with great style. She has one song unfinished: the history of Tristinwold, every verse but the last. She wants you to write the ending, and to choose what it says.",
            [
                new("\"An ending with hope in it.\"", "You write the city a hopeful last verse. Odalys hears it, closes her eyes, and says it's a lie worth telling. She names you Laureate after her.", 30, Flag: "hopeful_verse", Honour: 2, Deed: "wrote the city a hopeful last verse"),
                new("\"The truth, however it ends.\"", "You write the truth: the Rift, the Tongs, the corrupted dragons. Odalys laughs until she coughs, and names you Laureate after her. \"Somebody had to,\" she says.", 30, 0, 100, "true_verse", Honour: 3, Deed: "wrote the truth into the city's last verse"),
                new("\"Write yourself in, and Odalys out.\"", "The last verse of the history of Tristinwold is about you. Odalys hears it, and laughs, and laughs, and names you Laureate anyway: \"They always do,\" she says. Hollis never forgives it.", 25, 0, 100, "vain_verse", Infamy: 4, Deed: "wrote yourself into the city's last verse, and its Laureate out"),
            ]),

        // ---- the Brass Foundry ----
        new("leaking_still", Calling.Artificer, GuildRank.Initiate, "guild/marigold", "The Leaking Still",
            "Marigold's still has been leaking spirit into the furnace room for a week, and last night it caught. Nobody was hurt; the Foundress wants to know how it leaked. Jonquil says Marigold never tightened it. Marigold says the still's seams were cut.",
            [
                new("\"Look at the seams.\"", "The seams are cut, clean, with a wrench. Not Marigold's: she can't reach that side of it. Hesper looks at Jonquil a long time, says nothing, and has Wick weld it shut. Marigold brings you a pie.", 15, 2, Flag: "cut_seams", Honour: 3, Deed: "cleared an apprentice of a fire that wasn't hers"),
                new("\"Marigold's careless. Everyone knows it.\"", "Marigold scrubs furnaces for a month and stops talking at supper. The still leaks again a week later, from a cut seam, and Jonquil is somewhere else that night.", -5, -1, Flag: "blamed_marigold", Infamy: 3, Deed: "let an apprentice carry the blame for a cut still"),
                new("\"Sell the Foundress the formula for a still that can't leak.\"", "You sell Hesper your own design, which is Marigold's with the seams moved. It works. Marigold recognises it, and says nothing, and that's worse.", 5, 0, 60, "sold_still", Infamy: 4, Deed: "sold the Foundry an apprentice's design as your own"),
            ]),
        new("harbour_contract", Calling.Artificer, GuildRank.Member, "guild/osgood", "The Harbour Contract",
            "The Brine Company wants twelve bilge pumps for its ships, at the Foundry's price. The Ravenscrew Tongs want the same twelve, for boats that run things past the Watch at night, at twice the price, and they want the company's order lost. Osgood has both letters, and has asked you.",
            [
                new("\"The company's. It's honest work, and it's ours.\"", "Twelve pumps go to the company's ships, and Marit Hale shakes Osgood's hand on the pier. The Tongs send a man to say the Foundry will regret it. It doesn't, yet.", 15, Flag: "company_pumps", Honour: 4, Deed: "kept the Foundry's word to the Brine Company"),
                new("\"The Tongs'. Twice the price is twice the price.\"", "Twelve pumps go out of the yard at night on a Tong cart. The Foundry's roof is mended and its furnaces fed for a year. Three smugglers' boats stop sinking, and the Watch stops catching them.", -5, 0, 120, "tong_pumps", Infamy: 5, Deed: "sold the Foundry's pumps to the Tongs' smugglers"),
                new("\"Both. Build twenty-four, and fit the Tongs' with a flaw.\"", "The company's pumps pump. The Tongs' pumps pump for a month, then seize, one after another, a day out from the Teeth. The Tongs can't prove a thing. Osgood counts the money twice.", 10, 0, 60, "flawed_pumps", Honour: 1, Infamy: 2, Deed: "sold the Tongs pumps built to fail"),
            ]),
        new("rivals_engine", Calling.Artificer, GuildRank.Adept, "guild/hesper", "The Rival's Engine",
            "The company will buy one engine for its new ship: Jonquil's, or yours. Hesper has set a trial for the morning: the two engines run until one gives out, in the yard, before the whole Foundry and Marit Hale.",
            [
                new("\"Let them run.\"", "Jonquil's engine runs hot, and fast, and dies at noon with a bang that takes the gantry roof. Yours is still running at supper. Marit Hale buys it. Jonquil doesn't come to supper.", 25, 2, Flag: "engine_won", Honour: 2, Deed: "won the Foundry's engine trial fair", Duel: true,
                    Loss: "Yours seizes first, with a sound like a dropped bell. Jonquil's runs until dark. She pats it, and then you, on the shoulder. \"Next time,\" she says, and means it kindly, which is unbearable."),
                new("\"Loosen a bolt in hers the night before.\"", "Jonquil's engine throws a rod in the first hour, and a piece of it through the tent where Marit Hale was sitting. Nobody is hurt. Yours wins. Wick finds the bolt, and looks at you, and pockets it.", 15, 5, Flag: "loosened_bolt", Infamy: 5, Deed: "wrecked a rival's engine in the night"),
                new("\"Withdraw. Her engine's the better one.\"", "You tell Hesper so in front of the yard. Jonquil stares at you, suspicious of a trick, then shakes your hand very hard. The company buys hers; she asks you to fit it.", 5, -4, Flag: "yielded_engine", Honour: 3, Deed: "gave a rival the engine trial she deserved"),
            ]),
        new("golems_heart", Calling.Artificer, GuildRank.Veteran, "guild/tilda", "The Golem's Heart",
            "Tilda has found what makes the Foundry's golems live: a sliver of the Rift, sealed in brass, in every heart. Hesper has always known. The Magi would shut the Foundry for it; the Tongs would pay a fortune for the way of it; and the golems, Tilda says, are the gentlest things she knows.",
            [
                new("\"Keep it. Seal the hearts better, and tell no one.\"", "You and Tilda rebuild every heart with a second seal, and the Rift stays where it's put. Hesper, when she hears, names you master of the Foundry, and gives you her own golem's old heart in a box.", 30, Flag: "sealed_hearts", Honour: 3, Deed: "kept the golems' secret, and made it safe"),
                new("\"Tell the Archmage. It's the Rift.\"", "Selwyn comes himself, looks at a heart for a long while, and leaves it where it is: \"Sealed is sealed.\" The Magi keep a watch on the yard after. Hesper names you master, a little stiffly.", 20, Flag: "told_magi_hearts", Honour: 4, Deed: "took the golems' secret to the Magi"),
                new("\"Sell the Tongs the way of it.\"", "A Tong captain pays in gold for the making of a heart. A month later there are golems on the docks at night, badly sealed, and one of them screams. Hesper names you master anyway, because the yard needs the money, and never looks at you the same.", 30, 0, 250, "sold_hearts", Infamy: 8, Deed: "sold the making of golem hearts to the Tongs"),
            ]),
    ];

    /// <summary>The next piece of drama waiting for a hero in their guild, if any (it has to be time, and they have to have risen far enough).</summary>
    public static Storylet? NextDrama(GuildBook book, Calling guild)
    {
        var record = book.For(guild);
        if (book.PlaySeconds < record.NextDramaAt) return null;
        return Drama.FirstOrDefault(s => s.Guild == guild && !record.Done.Contains(s.Id) && RankOf(record.Standing) >= s.Rank);
    }

    /// <summary>A choice made: the guild remembers, the rival burns, and the next story waits a while.</summary>
    public static void Choose(GuildBook book, Storylet story, StoryChoice choice, bool wonDuel = true, Standing? standing = null)
    {
        var record = book.For(story.Guild);
        record.Done.Add(story.Id);
        record.Standing = Math.Max(0, record.Standing + (choice.Duel && !wonDuel ? choice.Standing / 3 : choice.Standing));
        record.Rivalry += choice.Rivalry;
        if (choice.Flag is { } flag) book.Flags.Add(choice.Duel && !wonDuel ? flag + "_lost" : flag);
        record.NextDramaAt = book.PlaySeconds + DramaGapSeconds;
        // The world hears of it (a duel lost settles nothing).
        if (standing is not null && (wonDuel || !choice.Duel))
        {
            var deed = choice.Deed ?? $"had a hand in {story.Title.ToLowerInvariant()}";
            standing.Good(choice.Honour, deed);
            standing.Evil(choice.Infamy, deed);
        }
    }

    /// <summary>What a rival says when you meet them, by how hot the rivalry runs and who's been winning.</summary>
    public static string RivalLine(Guild g, GuildRecord r)
    {
        var name = g.Rival.Npc.First;
        if (r.DuelsWon > r.DuelsLost + 1) return $"{name} won't meet your eye. \"Lucky. You've been lucky. That's all.\"";
        if (r.DuelsLost > r.DuelsWon + 1) return $"\"Back for another lesson?\" {name} doesn't even look up.";
        return r.Rivalry switch
        {
            >= 4 => $"\"One day,\" {name} says, very quietly, \"there won't be anyone to stop me.\"",
            >= 1 => $"\"Still here?\" {name} smiles with half their mouth. \"Shame.\"",
            <= -3 => $"{name} nods to you, almost friendly. \"Don't tell anyone I said so, but that was decent of you.\"",
            _ => g.Rival.Npc.Greeting,
        };
    }

    /// <summary>What a rival has been up to, heard now and then as time passes.</summary>
    public static string RivalNews(Guild g, int n) => (g.Calling, n % 4) switch
    {
        (Calling.Warrior, 0) => "Corin Vale beat three sellswords in the yard this morning, one after another, and is telling everyone.",
        (Calling.Warrior, 1) => "Corin Vale has been seen drinking with Watch officers. The yard is muttering.",
        (Calling.Warrior, 2) => "Corin Vale says he'll be First Blade by spring, and that you'll be scrubbing his armour.",
        (Calling.Warrior, _) => "Corin Vale came back from the north road with a troll's ear, and won't say whose troll it was.",
        (Calling.Rogue, 0) => "Sable Rue robbed a magistrate's house last night and left a velvet glove on the pillow. The whole city's talking.",
        (Calling.Rogue, 1) => "Sable Rue has been whispering to the Lantern Boys. Jory doesn't like it.",
        (Calling.Rogue, 2) => "Sable Rue says you couldn't steal a cold from a beggar.",
        (Calling.Rogue, _) => "Sable Rue's been seen at Vesper's door at midnight. Nobody knows why.",
        (Calling.Bard, 0) => "Fennick Lark sang at the Watch House last night, and three hard men wept into their ale.",
        (Calling.Bard, 1) => "Fennick Lark has been seen drinking with Tong captains. The College is muttering.",
        (Calling.Bard, 2) => "Fennick Lark says your voice could curdle milk at forty paces.",
        (Calling.Bard, _) => "Fennick Lark has written a song about you. It's catchy. That's the worst part.",
        (_, 0) => "Isadora Crane burned a new door into the Archive Garden wall. On purpose, she says.",
        (_, 1) => "Isadora Crane has been reading in the locked stacks again, with nobody's leave.",
        (_, 2) => "Isadora Crane says your spells have all the subtlety of a dropped kettle.",
        _ => "Isadora Crane froze the garden pond solid to win an argument. The fish are not pleased.",
    };

    /// <summary>Real minutes of play between news of your rival.</summary>
    public const double RivalNewsGapSeconds = 25 * 60;
}

/// <summary>How a hero stands with one guild.</summary>
public sealed class GuildRecord
{
    public int Standing { get; set; }
    public int Rivalry { get; set; }
    public int DuelsWon { get; set; }
    public int DuelsLost { get; set; }
    public double NextDramaAt { get; set; } = Guilds.DramaGapSeconds / 3; // the first story comes soon after joining
    public HashSet<string> Done { get; set; } = [];
}

/// <summary>
/// A hero's guild life, saved with the company: their standing, rival and stories in each guild, their class
/// quests, their lessons, their titles, and how long they've played (stories unfold over time).
/// </summary>
public sealed class GuildBook
{
    public Dictionary<string, GuildRecord> Records { get; set; } = [];
    public HashSet<string> Flags { get; set; } = [];
    public List<string> Titles { get; set; } = [];

    /// <summary>The step each class's quest line has reached (0 to 3; 3 is mastered).</summary>
    public Dictionary<string, int> ClassSteps { get; set; } = [];

    /// <summary>The hero level at which they last took a lesson from a teacher (one lesson a level).</summary>
    public int LessonLevel { get; set; }

    public double PlaySeconds { get; set; }
    public double NextRivalNewsAt { get; set; } = Guilds.RivalNewsGapSeconds;
    public int RivalNewsCount { get; set; }

    public GuildRecord For(Calling c)
    {
        var key = Guilds.Of(c).Id;
        if (!Records.TryGetValue(key, out var r)) Records[key] = r = new GuildRecord();
        return r;
    }

    public GuildRank Rank(Calling c) => Guilds.RankOf(For(c).Standing);
    public int Step(string discipline) => ClassSteps.GetValueOrDefault(discipline);
}
