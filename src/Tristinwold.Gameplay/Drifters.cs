namespace Tristinwold.Gameplay;

/// <summary>Which way someone leans: a good one helps, a neutral one is fine until crossed, an evil one hunts.</summary>
public enum Leaning { Good, Neutral, Evil }

/// <summary>What a drifter can be paid to do: fall on a town, go after a hero, or guard or raid a caravan or a ship.</summary>
public enum HireKind { Town, Hero, GuardCaravan, RaidCaravan, GuardShip, RaidShip }

/// <summary>
/// One of the drifters: an incomplete immortal out in the world, like Turlin. None of them went through the Ringing
/// and none of them wears a ring, but each has their own way back from death, and each comes back a little stronger
/// and a little less whole. They live where they live, teach what they know, trade what they carry, and can be hired,
/// though never to travel at a hero's side: they're paid to fall on a town, to go after a hero, or to guard or raid a
/// wagon or a ship. A good one only takes work against an outlaw; a neutral one takes anything, for the right price.
/// </summary>
public sealed class Drifter
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>What they're called on their plate: "the Exile of Vampa".</summary>
    public required string Title { get; init; }
    public required Leaning Leaning { get; init; }
    /// <summary>Their bestiary kind (their numbers in a fight) and their look (see the game's Models).</summary>
    public required string Kind { get; init; }
    public required string Look { get; init; }
    public required Avatar Avatar { get; init; }
    public float Size { get; init; } = 1;
    /// <summary>Where they keep: one place, or a round of camps they move between, one a day.</summary>
    public required (double X, double Z, string Place)[] Camps { get; init; }
    public required string Story { get; init; }
    public required string Greeting { get; init; }
    /// <summary>How they came to be what they are, and what it costs them each time.</summary>
    public required string Undying { get; init; }
    /// <summary>The classes they'll teach, and what a lesson's like.</summary>
    public required string[] Teaches { get; init; }
    public required string Lesson { get; init; }
    /// <summary>What they say to anyone they won't teach.</summary>
    public required string NotTaught { get; init; }
    /// <summary>The thing they'll do for you once a day, free, if they do: a blessing, a dance, tea.</summary>
    public string? Help { get; init; }
    /// <summary>The one thing they want, how many, and what it's worth to them.</summary>
    public required string Wants { get; init; }
    public required int WantsN { get; init; }
    public required int WantsPay { get; init; }
    public required string Ask { get; init; }
    public required string Thanks { get; init; }
    /// <summary>What they'll sell.</summary>
    public required string[] Wares { get; init; }
    /// <summary>When they come for someone (paid to), when they win, and when they fall.</summary>
    public required string Hail { get; init; }
    public required string Won { get; init; }
    public required string Fell { get; init; }
    /// <summary>When they come back, the day after.</summary>
    public required string Back { get; init; }
    /// <summary>What they say to a job they won't take.</summary>
    public required string Refuse { get; init; }

    /// <summary>Where they are today.</summary>
    public (double X, double Z, string Place) CampOn(int day) => Camps[((day % Camps.Length) + Camps.Length) % Camps.Length];
}

/// <summary>A drifter paid to do something: who, what, against or for whom, who paid, and the day it runs out.</summary>
public sealed class Hire
{
    public string Id { get; set; } = "";
    public string Drifter { get; set; } = "";
    public HireKind Kind { get; set; }
    /// <summary>The town's id, or the hero's name (for a guard, the hero paying).</summary>
    public string Target { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string By { get; set; } = "";
    public int Ends { get; set; }
}

public static class Drifters
{
    /// <summary>How many days a job runs, and what it costs.</summary>
    public static int Days(HireKind k) => k is HireKind.GuardCaravan or HireKind.GuardShip ? 2 : 3;

    public static int Price(HireKind k) => k switch
    {
        HireKind.Town => 900,
        HireKind.Hero => 500,
        HireKind.RaidCaravan => 400,
        HireKind.RaidShip => 500,
        HireKind.GuardCaravan => 250,
        _ => 300,
    };

    public static bool Guarding(HireKind k) => k is HireKind.GuardCaravan or HireKind.GuardShip;

    /// <summary>
    /// The level they fight at, grown like any deviant's: ten to start, so they're a real threat from the first; two
    /// more for every hero they've beaten, and one more for every time they've died and come back. No ceiling.
    /// </summary>
    public const int LeastLevel = 10;
    public static int Level(int wins, int deaths) => LeastLevel + 2 * Math.Max(0, wins) + Math.Max(0, deaths);

    /// <summary>Why they won't take the job, or null if they will. A good one only goes against an outlaw, and never a town.</summary>
    public static string? Refuses(Drifter d, HireKind k, string target, string by, IReadOnlyDictionary<string, int> outlaws)
    {
        if (target.Length is 0 or > 80 || by.Length is 0 or > 60) return "Against who?";
        if (k is HireKind.Hero or HireKind.RaidCaravan or HireKind.RaidShip && target == by) return "Against yourself? No.";
        if (d.Leaning != Leaning.Good || Guarding(k)) return null;
        if (k == HireKind.Town) return d.Refuse;
        return outlaws.ContainsKey(target) ? null : d.Refuse;
    }

    /// <summary>What a job is, in a few words, for the town news and the drifter's own mouth.</summary>
    public static string Job(HireKind k, string name) => k switch
    {
        HireKind.Town => $"fall on {name}",
        HireKind.Hero => $"go after {name}",
        HireKind.GuardCaravan => $"ride guard on {name}'s wagon",
        HireKind.RaidCaravan => $"take {name}'s wagon on the road",
        HireKind.GuardShip => $"sail guard on {name}'s ship",
        _ => $"board {name}'s ship at sea",
    };

    public static Drifter? Get(string? id) => All.FirstOrDefault(d => d.Id == id);

    public static readonly IReadOnlyList<Drifter> All =
    [
        new()
        {
            Id = "broly", Name = "Broly", Title = "the Exile of Vampa", Leaning = Leaning.Neutral, Kind = "drifter_broly", Look = "drifter_broly", Size = 1.2f,
            Avatar = Avatar.For("Barbarian") with { Race = "ape", Sex = "Male", Hair = "Black", Style = "Mane", Build = "Broad", Height = "Tall", Top = "Class", Bottom = "Olive", Wear = "Class", Outfit = "Natural", Eyes = "Green" },
            Camps = [(-930, -300, "Vampa's Rock, at the far end of the Strand")],
            Story = "Of the ape folk, born with more strength in him than any of his people had ever seen, and cast out for it while he was still at the breast: left on the black rock they call Vampa to die. He didn't. Nobody taught him to talk until he was grown, and nobody taught him to keep his temper at all.",
            Greeting = "\"...You came a long way to look at me.\"",
            Undying = "He ate whatever grew on Vampa, and something that grew there was the red fruit from the foot of the Maw. He's died, he says, more times than he can count. Every time he gets up bigger, and angrier, and remembers a little less of why.",
            Teaches = ["Barbarian", "MartialArtist"],
            Lesson = "He doesn't explain anything. He hits you until you stop flinching, then he hits you harder. It works.",
            NotTaught = "\"I only know how to hit things. You don't hit things.\"",
            Wants = "bear_pelt", WantsN = 3, WantsPay = 320,
            Ask = "\"The rock is cold at night. Bring me three bearskins. I would kill the bears, but they run from me now.\"",
            Thanks = "He wraps himself in them at once, and for a moment looks like what he is: a man nobody ever gave a blanket. \"...Thank you.\"",
            Wares = ["venison", "bear_claw"],
            Hail = "A shadow falls across you, and keeps growing. Broly lands on the road hard enough to crack it. \"They paid me. That's all. Don't make me angry.\"",
            Won = "He stands over you breathing like a forge. Slowly the green goes out of his eyes. \"...You made me angry.\"",
            Fell = "He goes down roaring, still reaching for you. Then he's still, and the green light goes out of his hair.",
            Back = "Broly is back on Vampa's Rock, bigger than before, staring out to sea.",
            Refuse = "",
        },
        new()
        {
            Id = "gornok", Name = "Gornok Thornek", Title = "of the Cobalt Soul", Leaning = Leaning.Good, Kind = "drifter_gornok", Look = "drifter_gornok", Size = 1.05f,
            Avatar = Avatar.For("MartialArtist") with { Race = "half_giant", Sex = "Male", Style = "None", Hair = "Black", Build = "Broad", Height = "Tall", Top = "Charcoal", Bottom = "Russet", Wear = "Wrapped robe", Outfit = "Natural", Eyes = "Grey" },
            Camps = [(-380, 560, "the ruined shrine on the south road")],
            Story = "A half-giant monk of the Cobalt Soul, an order that keeps the truth of things written down whether anyone wants it or not. Red marks of the order run over his bald head and down his arms, and old scars across his forearm and shoulder. The dead follow him: he can't say why, and he's stopped asking.",
            Greeting = "\"Peace, traveller. Mind the cold spot by the altar. That's Edda. She means no harm.\"",
            Undying = "The ghosts that follow him won't let him go. When he dies, they carry him back, every time, and every time one more of them is gone, spent on the carrying. He keeps a count. It's getting short.",
            Teaches = ["MartialArtist", "Paladin"],
            Lesson = "Forms at dawn, until your breath and your fists keep the same time. Then he asks you questions you can't answer, and makes you sit with them.",
            NotTaught = "\"Your way isn't mine to teach. But sit, if you like. Nobody's turned away from the fire.\"",
            Help = "He lays a scarred hand on your head and speaks a few words of the order. The cold spot by the altar moves to stand beside you. (Protect and Shell, ten minutes.)",
            Wants = "bone_dust", WantsN = 10, WantsPay = 260,
            Ask = "\"The restless dead in the barrows: bring me their dust, ten handfuls. I'll give it the rites it never had. They'll sleep, and so will the ones who walk with me.\"",
            Thanks = "He takes the dust like something precious and writes each handful in his book. \"Ten names I'll never know, at rest. Thank you.\"",
            Wares = ["bread", "travel_rations"],
            Hail = "Gornok walks out of the mist with the cold spots drifting round him. \"There's a price on your head, and someone's paid me to collect. Yield, and I'll take you in alive.\"",
            Won = "He kneels by you. \"Enough. Pay what you owe, and the dead and I will leave you be.\"",
            Fell = "He sinks to his knees, smiling, and the cold spots gather round him like mourners. Then he's gone, and so are they.",
            Back = "Gornok sits at the ruined shrine again. One fewer of the dead stands with him.",
            Refuse = "\"I don't raise my hand to the innocent. Bring me an outlaw's name, and then we'll talk.\"",
        },
        new()
        {
            Id = "ren", Name = "Ren", Title = "the Dancer of Deals", Leaning = Leaning.Good, Kind = "drifter_ren", Look = "drifter_ren",
            Avatar = Avatar.For("Musician") with { Race = "dragonkind", Dragon = "Blue", Half = "human", Sex = "Female", Hair = "Sapphire", Style = "Long01", Horns = "Ram", Top = "White", Bottom = "Black", Wear = "Sash", Outfit = "Natural", Eyes = "Amber", Build = "Slim" },
            Camps = [(-300, -90, "the west road, past the caravan yard"), (60, 420, "the crossroads south of the city"), (-640, -250, "the road to the Strand")],
            Story = "Blue-skinned, horned, quick on her feet and quicker with a pen. She dances in the roads for whoever stops to watch, and afterwards she offers them a contract. Most of them sign. Most of them are glad they did. A few of them aren't.",
            Greeting = "\"Oh, an audience! Stay for the dance? It's free. The contract after is the part that costs.\"",
            Undying = "She made a deal with something older than the city, before she came here: her death, on lease, for a song a year. Each time she dies, it collects her, and sends her back with a little more of the debt paid in something she won't name.",
            Teaches = ["Musician", "Poet", "Scribe"],
            Lesson = "Footwork first, then timing, then the trick of making a crowd look where you want them to. \"Every performance is a deal,\" she says. \"Make sure you're the one who wrote it.\"",
            NotTaught = "\"You've two left feet, love, and that's being kind to one of them.\"",
            Help = "She dances for you, there on the road, and somehow your heart keeps her time for a long while after. (Haste and Regen, ten minutes.)",
            Wants = "sapphire", WantsN = 1, WantsPay = 340,
            Ask = "\"I need a sapphire for a seal: a very particular contract, with a very particular party. Bring me one and I'll pay you better than any jeweller.\"",
            Thanks = "She holds the stone to the light and grins with every one of her teeth. \"Perfect. They'll never see it coming.\"",
            Wares = ["honey_cake", "ale", "stamina_brew"],
            Hail = "Music, out on the road, and a blue shape dancing towards you. \"There's a contract with your name on it, outlaw, and I always deliver.\"",
            Won = "She bows over you. \"Signed, sealed and delivered. Pay your debts, darling. Everyone does, in the end.\"",
            Fell = "She goes down mid-step, laughing, and a smell of brimstone hangs where she was. \"Put it on my account!\"",
            Back = "Ren is dancing on the roads again, with something new behind her eyes.",
            Refuse = "\"I only sign against people who've earned it. Find me an outlaw, sweetheart.\"",
        },
        new()
        {
            Id = "irohe", Name = "Irohe", Title = "the Wandering Teapot", Leaning = Leaning.Neutral, Kind = "drifter_irohe", Look = "drifter_irohe", Size = 1.05f,
            Avatar = Avatar.For("MartialArtist") with { Race = "human", Sex = "Male", Age = "Seasoned", Hair = "Grey", Style = "Topknot", Beard = "Long", Build = "Broad", Height = "Average", Top = "Class", Bottom = "Crimson", Wear = "Class", Outfit = "Natural", Eyes = "Amber" },
            Camps = [(-470, -430, "a camp at the Northwood's edge"), (-520, 240, "a camp by the stream west of the city"), (-200, 900, "a camp on the south downs")],
            Story = "An old man with a long grey beard and the arms of a smith, bare to the waist, a red sash round a red kilt, and a kettle always on. He moves his camp every day, with a black hound called Zuko at his heels. The Watch has his name on their list of outlaws, and he has never once told anyone why.",
            Greeting = "\"Ah, a guest! Sit, sit. The water is nearly ready. There's no problem in the world that is not a little smaller after a good cup of tea.\"",
            Undying = "He was a general once, long ago, and burned the wrong city. He says the fire that took him that day never went out: it only went inside. When he dies he burns, and out of the ashes, given a day, he walks again, a little older, a little wiser, and he hopes a little kinder.",
            Teaches = ["MartialArtist", "Sorcerer", "Stormdruid", "Barbarian", "Paladin", "Ranger", "Thief", "Assassin", "Wizard", "Warlock", "Poet", "Scribe", "Musician", "Alchemist", "EtherAeronaut", "Saboteur", "Shapeshifter", "Spiritcaller"],
            Lesson = "He teaches you to breathe before he teaches you anything else. \"Fire is life, not only destruction. A strike that comes from anger burns out quickly. One that comes from breath can burn all day.\" By sunset your hands are steadier, and so are you.",
            NotTaught = "\"Everyone can learn to breathe.\"",
            Help = "He pours you a cup of tea and lets you sit by his fire as long as you like. It's the best you've felt in days. (Fully rested, and Regen for ten minutes.)",
            Wants = "emberleaf", WantsN = 8, WantsPay = 220,
            Ask = "\"I am almost out of emberleaf for my special blend. If you happen to find eight of them on your travels, I would be very grateful. And so would your next cup.\"",
            Thanks = "He sniffs the leaves and closes his eyes. \"Wonderful. A gift given for nothing is worth more than gold paid for everything. But here is some gold anyway, because you are young and I am sentimental.\"",
            Wares = ["travellers_tea", "travel_rations", "honey_cake"],
            Hail = "You smell tea on the wind, then woodsmoke, then Irohe is on the road ahead of you with Zuko at his side. \"I took the coin. I am sorry. I will try not to hurt you more than you need.\"",
            Won = "He helps you sit up and presses a warm cup into your hands. \"There. Pride is a heavy thing to carry into a fight. Next time, leave it at home.\"",
            Fell = "The old man laughs, softly, as he falls, and the laugh turns to fire. Where he lay there's only ash, and a kettle, still warm.",
            Back = "Irohe's kettle is on again at a new camp, and the old man looks a day older and not a day sadder.",
            Refuse = "",
        },
        new()
        {
            Id = "faye", Name = "Faye", Title = "the Twice-Woken", Leaning = Leaning.Neutral, Kind = "drifter_faye", Look = "drifter_faye",
            Avatar = Avatar.For("Wizard") with { Race = "elf", Sex = "Female", Hair = "White", Style = "Long01", Top = "White", Bottom = "Russet", Wear = "Jerkin", Outfit = "Natural", Eyes = "Brown", Build = "Slim", Headwear = "none" },
            Camps = [(-700, 380, "the fallen watchtower")],
            Story = "A young elf with a scribe-wizard's books and a great deal of curiosity, and hair gone white as bone. She was killed once, and brought back, and she's been writing down what she saw in between ever since. Lightning crawls over her hands when she's thinking.",
            Greeting = "\"Hold still. No, really. I'm writing you down. Everyone who comes here gets written down.\"",
            Undying = "Someone brought her back, once, and did it badly: the door they opened never quite shut. Now every time she dies she walks back through it, and every time she brings a page of what's on the other side, and leaves a page of herself behind.",
            Teaches = ["Wizard", "Scribe", "Sorcerer", "Warlock"],
            Lesson = "She lectures for three hours without stopping, makes you copy out a spell until your hand cramps, and then shows you the one line in it everyone else gets wrong.",
            NotTaught = "\"I could teach you, but you'd only get ink everywhere.\"",
            Wants = "ancient_coin", WantsN = 6, WantsPay = 280,
            Ask = "\"Kings nobody remembers minted coins nobody spends. Bring me six. I'm writing their names down before they're gone for good.\"",
            Thanks = "She lines them up and copies every mark on them into her book, humming. She forgets to say thank you, then remembers and says it three times.",
            Wares = ["mana_tonic", "warding_draught"],
            Hail = "The air goes sharp and smells of storm. Faye floats down the road with her book open and lightning in her hair. \"Nothing personal. I was paid. Hold still: I'm writing this down.\"",
            Won = "She writes something in her book and closes it. \"Fascinating. You'll want a healer.\"",
            Fell = "She falls with a crack of thunder, and her book flutters shut. Her last word is \"Interesting.\"",
            Back = "Faye is back at the watchtower, with a new page in her book and a little less colour in her eyes.",
            Refuse = "",
        },
        new()
        {
            Id = "yokin", Name = "Yokin Hearthbeat", Title = "of the High Camp", Leaning = Leaning.Good, Kind = "drifter_yokin", Look = "drifter_yokin", Size = 1.1f,
            Avatar = Avatar.For("Barbarian") with { Race = "half_giant", Sex = "Male", Style = "None", Build = "Broad", Height = "Tall", Skin = "Ashen", Top = "Class", Bottom = "Forest", Wear = "Class", Outfit = "Natural", Eyes = "Grey" },
            Camps = [(-800, -1330, "the high camp in the Westreach")],
            Story = "A half-giant exile with runes that move over his skin like slow fish, and scars all down his back. When he was last heard of, he was training in the mountains. He still is. A great beast's skull sits on his shoulder, and the sword on his back is taller than most men.",
            Greeting = "\"You climbed all this way? Good. Warm your hands. Then we'll see what your legs are made of.\"",
            Undying = "The runes on him were carved by someone who meant to make a weapon. When he dies they rewrite him, line by line, and he wakes up a little stronger and a little more theirs. He trains so there'll be more of him than of them.",
            Teaches = ["Barbarian", "Paladin", "MartialArtist"],
            Lesson = "Up and down the mountain with a boulder on your back, then sword drills in the snow until you can't feel your arms. \"The cold doesn't care how strong you are. Be stronger anyway.\"",
            NotTaught = "\"Not my way. But you're welcome by the fire.\"",
            Help = "He grips your arm and the runes on his skin flare. Something of their strength goes into you. (Fully healed, and Protect for ten minutes.)",
            Wants = "frost_pelt", WantsN = 4, WantsPay = 300,
            Ask = "\"The camp needs new hides before the hard snow. Four frost wolf pelts. The wolves up here are fast, so be faster.\"",
            Thanks = "He throws the pelts over the tent poles. \"Good hunting. You'll do.\"",
            Wares = ["bear_cloak", "frost_pelt"],
            Hail = "A shape the size of two men comes down the slope, a great sword on his back and runes burning on his arms. \"There's a bounty on you, outlaw. I've come to settle it.\"",
            Won = "He plants his sword in the ground beside you. \"Settle your debts. Next time I won't stop.\"",
            Fell = "He falls like a tree in the snow, and the runes on his skin flicker and crawl as if they're already working.",
            Back = "Yokin is back at the high camp, and there's a new line of runes on his arm.",
            Refuse = "\"I don't hunt honest people for coin. Name me an outlaw.\"",
        },
    ];
}

public sealed partial class CityEconomy
{
    /// <summary>How many times each drifter has died, and the day each one dead walks again.</summary>
    public Dictionary<string, int> DrifterDeaths { get; set; } = [];
    public Dictionary<string, int> DrifterBack { get; set; } = [];
    /// <summary>How many heroes each drifter has beaten.</summary>
    public Dictionary<string, int> DrifterWins { get; set; } = [];

    public int DrifterLevel(string id) => Drifters.Level(DrifterWins.GetValueOrDefault(id), DrifterDeaths.GetValueOrDefault(id));

    /// <summary>A drifter beat a hero: a job done if they were paid for it, and stronger for it either way.</summary>
    bool DrifterWon(string id, string? job)
    {
        if (Drifters.Get(id) is null) return false;
        DrifterWins[id] = DrifterWins.GetValueOrDefault(id) + 1;
        if (job is not null) Hires.RemoveAll(h => h.Id == job);
        return true;
    }
    /// <summary>The drifters paid to do something, with what and until when.</summary>
    public List<Hire> Hires { get; set; } = [];

    public bool DrifterDown(string id) => DrifterBack.GetValueOrDefault(id) > Day;
    public Hire? HireOf(string drifter) => Hires.FirstOrDefault(h => h.Drifter == drifter && h.Ends > Day);
    public IEnumerable<Hire> LiveHires => Hires.Where(h => h.Ends > Day);

    /// <summary>A drifter taken on, if they're free, alive, and willing.</summary>
    bool Hired(Hire? h)
    {
        if (h is null || Drifters.Get(h.Drifter) is not { } d || h.Id.Length is 0 or > 40) return false;
        Hires.RemoveAll(x => x.Ends <= Day);
        if (DrifterDown(d.Id) || HireOf(d.Id) is not null || Hires.Count >= 60) return false;
        if (Drifters.Refuses(d, h.Kind, h.Target, h.By, Outlaws) is not null) return false;
        h.Ends = Day + Drifters.Days(h.Kind);
        Hires.Add(h);
        if (h.Kind == HireKind.Town) News.Add($"Word on the roads: someone has paid {d.Name} to {Drifters.Job(h.Kind, h.TargetName)}.");
        else if (h.Kind == HireKind.Hero) News.Add($"Someone has paid {d.Name} to {Drifters.Job(h.Kind, h.TargetName)}.");
        return true;
    }

    /// <summary>A drifter killed: down until tomorrow, a little stronger when they're back, and whatever they were paid for is off.</summary>
    bool DrifterFell(string id, string? by)
    {
        if (Drifters.Get(id) is not { } d) return false;
        DrifterDeaths[id] = DrifterDeaths.GetValueOrDefault(id) + 1;
        DrifterBack[id] = Day + 1;
        Hires.RemoveAll(h => h.Drifter == id);
        News.Add(by is { Length: > 0 } ? $"{by} has killed {d.Name}. They'll be back: they always are." : $"{d.Name} has fallen. They'll be back: they always are.");
        return true;
    }
}
