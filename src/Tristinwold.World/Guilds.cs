using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

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
/// A guild in Tristinwold: the warriors' Iron Circle, the rogues' Velvet Knives, the mages of the Magi. Each has a
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
public sealed record StoryChoice(string Label, string Outcome, int Standing = 0, int Rivalry = 0, int Septims = 0, string? Flag = null,
    bool Duel = false, string? Loss = null);

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
                new("\"Corin's lying to look good.\"", "You call Corin a liar in front of the whole yard. Then Greta finds frost in the break: the cold snapped it, not a hand. Odo is cleared, Brannoc grunts, and Corin will never forget it.", 10, 2, Flag: "defended_odo"),
                new("\"Odo was there. Let him answer for it.\"", "Odo is set to scrub the barracks for a month. A week later Greta finds frost in the break: nobody broke it. Odo won't meet your eye, and Corin smiles at you like a friend.", -5, -1, Flag: "blamed_odo"),
                new("\"Look at the steel before you look at anyone.\"", "Greta holds the halves to the light: frost in the grain. The cold snapped it. Nobody is to blame, and Brannoc looks at you like a man who's just found a sergeant.", 15, Flag: "wise"),
            ]),
        new("tong_purse", Calling.Warrior, GuildRank.Member, "guild/greta", "The Tong's Purse",
            "A Ravenscrew Tong boss has sent a purse to the yard: fat, heavy, and asking only that the Iron Circle look the other way when the Tongs do business at the docks. The roof leaks, Brannoc's tempted, and he's asked you.",
            [
                new("\"Take it. Roofs don't mend themselves.\"", "The roof is mended. The docks get a little darker. Some of the yard look at you differently now.", -10, 0, 60, "tong_money"),
                new("\"Send it back, with a message.\"", "The purse goes back with a dented helmet in it. The Tongs take the point, and the yard cheers you in the drills.", 15, Flag: "tong_refused"),
                new("\"Take it, and use it against them.\"", "Half the purse mends the roof; the other half buys the names of the Tong's dock men, who find the Watch waiting for them. Brannoc laughs for the first time in a month.", 10, 0, 30, "double_game"),
            ]),
        new("first_blade", Calling.Warrior, GuildRank.Adept, "guild/brannoc", "First Blade",
            "Brannoc is getting old, and the yard needs someone to lead the drills: the First Blade. It's you or Corin, and the yard wants it settled the old way.",
            [
                new("\"I'll fight him for it.\"", "Corin goes down in the dust, and the yard roars. Brannoc hands you the drill-staff: First Blade of the Iron Circle. Corin gets up slowly, and says nothing at all.", 25, 2, Flag: "first_blade", Duel: true,
                    Loss: "Corin's blade is at your throat, and the yard goes quiet. He's First Blade. \"Next time,\" he says, and for once he doesn't smirk."),
                new("\"Let Corin have it. The yard needs him more.\"", "Corin stares at you, then nods, and something between you is a little less sharp. The yard thinks you're either very wise or very soft.", 5, -3, Flag: "gave_blade"),
            ]),
        new("marshals_last_fight", Calling.Warrior, GuildRank.Veteran, "guild/tamsin", "The Marshal's Last Fight",
            "Brannoc is going north. A frost troll's been seen at the old place where his brother died, and he means to finish it, alone if he must. He's asked you to keep the yard while he's gone.",
            [
                new("\"I'm going with him.\"", "You go north together, and come back together, with a troll's tusk and Brannoc's brother's ring. He gives you the ring. The yard calls you Master of the Circle.", 30, 0, 100, "went_north"),
                new("\"I'll keep the yard.\"", "The yard holds while he's gone; when he comes back, limping, with a troll's tusk, he finds the drills sharper than he left them. Master of the Circle, he calls you.", 30, Flag: "kept_yard"),
            ]),

        new("skimming", Calling.Rogue, GuildRank.Initiate, "guild/nell", "Skimming",
            "The take's forty septims light this month. The ledger's been doctored: Mags's hand, Nell's sure of it. Sable is saying it was Jory. Vesper hasn't said anything, which is worse.",
            [
                new("\"Cover for Mags.\"", "Mags is skimming for her sister's medicine. You quietly put forty of your own septims in the box. Vesper notices, of course, and says nothing: loyalty's worth forty septims to her.", 10, 0, -40, "covered_mags"),
                new("\"Tell Vesper the truth.\"", "Vesper hears it, sends Mags's sister a healer, and docks Mags a month. The Knives respect the truth, mostly. Sable is furious her story fell apart.", 10, 1, Flag: "told_vesper"),
                new("\"Let Sable have Jory.\"", "Jory takes the blame and a beating. Sable owes you one now. The rooftops go quiet when you pass.", -10, -2, Flag: "sold_jory"),
            ]),
        new("lantern_boys", Calling.Rogue, GuildRank.Member, "guild/jory", "The Lantern Boys",
            "The Lantern Boys are working Knives streets, and the Gutter Prince is laughing about it. Vesper wants a message sent. She's left the kind of message up to you.",
            [
                new("\"Rob their stash.\"", "You lift the Lantern Boys' whole stash from under the Prince's nose. They're a laughing-stock by morning, and you're richer.", 10, 0, 50, "robbed_lanterns"),
                new("\"Parley with the Gutter Prince.\"", "You sit down with the Gutter Prince over bad wine, and come away with a border, a truce, and the beginning of a strange friendship.", 15, Flag: "parley"),
                new("\"Burn their den.\"", "The Lantern Boys' den burns. Nobody dies, quite. Vesper looks at you for a long time, then pours you a drink.", 5, -1, Flag: "burned_den"),
            ]),
        new("who_sold_us", Calling.Rogue, GuildRank.Adept, "guild/vesper", "Who Sold Us",
            "The Watch was waiting at the counting-house job. Someone sold out the crew. Sable says it was you, loudly, to anyone who'll listen.",
            [
                new("\"Then Sable can prove it with a blade.\"", "Sable yields with your knife at her collar, and the yard believes you. The real informant is found a week later: a Watch spy among the fences.", 20, 2, Flag: "beat_sable", Duel: true,
                    Loss: "Sable's knife is at your collar. The yard doesn't believe her, quite, but they don't believe you either. The real informant turns up a week later, and nobody apologises."),
                new("\"I'll find the real one.\"", "Three nights on the rooftops with Jory, and you find him: a fence called Coyle, selling to the Watch. Vesper deals with him. Sable doesn't apologise, but she stops talking.", 20, Flag: "found_informant"),
            ]),
        new("velvet_crown", Calling.Rogue, GuildRank.Veteran, "guild/nell", "The Velvet Crown",
            "Vesper is tired. She wants to step back, and the Knives need someone with fingers in every pocket in the city. Nell says it's you. Sable says it's her.",
            [
                new("\"I'll take it.\"", "Vesper gives you her ring, and her ledger, and her rooftop. The Knives are yours. Sable leaves the city for a while.", 30, 1, 150, "velvet_crown"),
                new("\"Give it to Sable.\"", "You stand aside, and Sable takes the chair, astonished. She owes you everything, and she knows it. Vesper calls you the cleverest knife she ever sharpened.", 20, -5, Flag: "crowned_sable"),
            ]),

        new("missing_book", Calling.Mage, GuildRank.Initiate, "guild/anselm", "The Missing Book",
            "A book is missing from the locked shelves: \"On the Seams of the World\", about the Rift. Isadora was last in the stacks. Liesl has been dreaming of doors. Brother Anselm is beside himself.",
            [
                new("\"Let me look in Liesl's room.\"", "The book is under Liesl's pillow. She's been reading it to make the dreams stop. They haven't. You take it back quietly, and Liesl starts trusting you.", 10, Flag: "liesl_book"),
                new("\"Ask Isadora.\"", "Isadora is icily offended, didn't take it, and tells the whole Archive you suspected her. Anselm finds it under Liesl's pillow the next day.", 0, 2, Flag: "accused_isadora"),
                new("\"Tell the Archmage.\"", "Selwyn finds the book and the girl, and sits with Liesl a long while. He thanks you for not making it a scandal.", 15, Flag: "told_selwyn"),
            ]),
        new("rift_light", Calling.Mage, GuildRank.Member, "guild/liesl", "Rift-Light",
            "Liesl has a vial of gold light, bled from the vein of a corrupted dragon. She thinks it can show her what's behind the door in her dreams. She's asking you to help her study it.",
            [
                new("\"Destroy it. Now.\"", "The vial shatters on the flagstones and the light screams as it goes out. Liesl cries, then sleeps through the night for the first time in a year.", 10, Flag: "destroyed_vial"),
                new("\"Let's study it, carefully.\"", "You learn how the Rift moves through a dragon's blood, and how it can be slowed. Liesl learns too much, and draws doors in the margins of everything.", 5, Flag: "rift_study"),
                new("\"Take it to the Archmage.\"", "Selwyn locks the vial in the deepest vault, and tells the Magi to watch the corrupted dragons closely. He watches Liesl closely too.", 15, Flag: "vial_to_selwyn"),
            ]),
        new("prodigys_wager", Calling.Mage, GuildRank.Adept, "guild/isadora", "The Prodigy's Wager",
            "There's a seat on the Archive council, and Isadora wants it. So, apparently, do you. She proposes a wager: a mage's duel in the garden, winner takes the seat.",
            [
                new("\"Accept.\"", "Isadora's ward breaks and she lowers her hands, white-faced. The seat is yours. \"Again,\" she says, \"some day,\" and means it.", 25, 2, Flag: "council_seat", Duel: true,
                    Loss: "Your ward breaks first. Isadora takes the seat, and for a moment she almost looks sorry."),
                new("\"Take the seat yourself, Isadora.\"", "Isadora stares at you, suspicious of kindness. Then she takes the seat, and votes your way on the first thing that matters.", 5, -3, Flag: "gave_seat"),
            ]),
        new("what_ilse_keeps", Calling.Mage, GuildRank.Veteran, "guild/selwyn", "What Ilse Keeps",
            "Sister Ilse at the Archive keeps a second key, to a shelf that isn't on any list. Selwyn has known for years. Now he's afraid of what's on it: a record, in Ilse's hand, of a man who was saved from drowning, and the name he took.",
            [
                new("\"Keep her secret.\"", "You leave the shelf locked. Ilse thanks you without saying for what, and some nights you wonder about the saved man, and the name he took.", 25, Flag: "kept_ilse_secret"),
                new("\"Read it.\"", "The page is in Ilse's hand: a man pulled from the harbor, who should have died, and who took the name Vann. You put it back. You don't sleep well.", 20, Flag: "read_vann"),
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
    public static void Choose(GuildBook book, Storylet story, StoryChoice choice, bool wonDuel = true)
    {
        var record = book.For(story.Guild);
        record.Done.Add(story.Id);
        record.Standing = Math.Max(0, record.Standing + (choice.Duel && !wonDuel ? choice.Standing / 3 : choice.Standing));
        record.Rivalry += choice.Rivalry;
        if (choice.Flag is { } flag) book.Flags.Add(choice.Duel && !wonDuel ? flag + "_lost" : flag);
        record.NextDramaAt = book.PlaySeconds + DramaGapSeconds;
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
