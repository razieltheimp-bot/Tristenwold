using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>Someone who lives somewhere in the world: a name, a past, and things they care about.</summary>
public sealed record Npc(
    string Id,
    string Name,
    Sex Sex,
    int Age,
    string Occupation,
    string HomeId,
    string HomeName,
    string Backstory,
    IReadOnlyList<string> Interests,
    string Temper,
    string Greeting,
    double X,
    double Z,
    bool Ashborn = false,
    string Race = "human")
{
    public string First => Name.Split(' ')[0];
    public string They => Sex switch { Sex.Female => "she", Sex.Male => "he", _ => "they" };
    public string Their => Sex switch { Sex.Female => "her", Sex.Male => "his", _ => "their" };
    public string Them => Sex switch { Sex.Female => "her", Sex.Male => "him", _ => "them" };

    /// <summary>What they say when you ask what they care about.</summary>
    public string InterestsSpeech => string.Join(" ", Interests.Select(i => People.InterestLines.TryGetValue(i, out var line) ? line : $"I like {i}."));
}

/// <summary>Makes the people of a settlement. The same seed always makes the same people.</summary>
public static class People
{
    internal static readonly Dictionary<SettlementKind, string[]> Jobs = new()
    {
        [SettlementKind.City] = ["guard", "smith", "merchant", "priest", "scholar", "beggar", "bard", "innkeeper", "fishwife", "courier", "alchemist", "sellsword", "dockhand", "scribe"],
        [SettlementKind.Town] = ["guard", "smith", "trader", "priest", "innkeeper", "hunter", "farmer", "carpenter", "gravedigger", "herbalist"],
        [SettlementKind.Hamlet] = ["farmer", "miller", "woodcutter", "herbalist", "shepherd", "village elder", "brewer", "trapper", "midwife"],
        [SettlementKind.Farmstead] = ["farmer", "farmhand", "beekeeper", "goatherd"],
        [SettlementKind.HuntingCamp] = ["hunter", "trapper", "tanner"],
        [SettlementKind.Inn] = ["innkeeper", "cook", "traveller", "bard", "courier"],
        [SettlementKind.Shrine] = ["shrine-keeper", "pilgrim"],
        [SettlementKind.Mine] = ["miner", "foreman", "assayer"],
        [SettlementKind.AshbornCamp] = ["Ashborn refugee", "Ashborn smith", "healer", "lookout"],
    };

    public static readonly Dictionary<string, string> InterestLines = new()
    {
        ["hunting"] = "Nothing clears the head like tracking an elk through fresh snow.",
        ["alchemy"] = "Half the weeds you walk past will cure you and the other half will kill you. I like knowing which.",
        ["old coins"] = "I collect coins of the Old Kings. Every barrow has a few, if you're brave or stupid.",
        ["songs"] = "I know two hundred songs, and every tavern only lets me sing three.",
        ["the stars"] = "The stars moved last winter. Only a hair. Nobody believes me.",
        ["cards"] = "Give me a deck and a fool and I'll eat well for a week.",
        ["fishing"] = "Fish don't lie, don't steal and don't pay Tong tithes.",
        ["horses"] = "I had a grey mare once, faster than the wind off Galeholt. I'd trade a year of my life to ride her again.",
        ["woodcarving"] = "I carve birds. One for every person I've lost. It's a big flock now.",
        ["the Magi"] = "The Magi keep books nobody's allowed to read. I'd give an eye for one page.",
        ["the Tongs"] = "Everyone pretends the Tongs aren't in charge. I keep count of who pays them.",
        ["cooking"] = "Anyone can roast a boar. Making it taste like home is the trick.",
        ["brewing"] = "My mead could wake the dead. Don't tell the gravedigger.",
        ["old battles"] = "Ask me about the siege at the Bell Keep. Go on. I've got all night.",
        ["gossip"] = "I know who's sleeping with whom in every house for a mile. Ask nicely.",
        ["the dead"] = "I talk to my grandmother's grave on feast days. Lately she's been answering.",
        ["maps"] = "I want to see the edge of the world. They say there isn't one.",
        ["blades"] = "A good blade is honest. Steel only does what you ask it.",
        ["faith"] = "The gods kept me alive once. I owe them the rest.",
        ["bees"] = "Bees know when someone's about to die. They go quiet.",
        ["dreams"] = "I dream of a door, and someone on the other side who knows my name.",
        ["Angels"] = "I saw wings over the pines the night the Rift opened. White, and far too many.",
        ["letters"] = "I write to family in the cities. The couriers lose half of what I send.",
        ["money"] = "Coin's the only god that answers every prayer.",
        ["mushrooms"] = "There's a blue cap grows only on barrow stones. Lovely in a stew, if you're sure which one it is.",
        ["weather"] = "Storm's coming. I can feel it in my bad knee. There's always a storm coming.",
        ["the sea"] = "Saltwater gets in your blood. I've been ashore ten years and still sway when I stand.",
        ["children's tales"] = "Every tale I tell the little ones is true. I just leave out the endings.",
        ["the Rift"] = "Something came through the Rift besides the Ashborn. I'd bet my life on it.",
        ["wolves"] = "Wolves are just honest dogs. It's the ones that walk on two legs you watch.",
    };

    static readonly string[] Interests = [.. InterestLines.Keys];

    static readonly string[] Tempers = ["warm", "wary", "gruff", "cheerful", "bitter", "nervous", "proud", "dry"];

    static readonly Dictionary<string, string[]> Greetings = new()
    {
        ["warm"] = ["Well met, stranger. Sit, if you like. Welcome to {home}.", "Come in out of the cold. You look like you've walked a long way."],
        ["wary"] = ["You're not from {home}. What do you want?", "Keep your hands where I can see them, and we'll get on fine."],
        ["gruff"] = ["What.", "If you're selling something, I'm not buying."],
        ["cheerful"] = ["Oh, a new face! We don't get many of those in {home}.", "Hello, hello! Lovely day for it, whatever it is."],
        ["bitter"] = ["Another sellsword. The last one took our money and our mule.", "Welcome to {home}, where nothing good happens."],
        ["nervous"] = ["Oh! You startled me. Sorry. Sorry.", "Are you with the Tongs? Please say you're not with the Tongs."],
        ["proud"] = ["You stand in {home}. Mind your manners.", "My family has lived in {home} for nine generations. And yours?"],
        ["dry"] = ["Ah. A hero. We've had a few. The barrow's full of them.", "Lost? Most people who come this way are."],
    };

    static readonly string[] Origins =
    [
        "{First} was born in {city}, the youngest of {n} children of a {parent}.",
        "{First} grew up on the roads with a troupe of players and never learned to stay put.",
        "{First} came to {home} from {city} with nothing but {their} {belonging}.",
        "{First} was left as a baby on the steps of a temple of {divine}.",
        "{First} has lived in {home} all {their} life, as {their} {elder} did before {them}.",
        "{First} sailed out of Tristinwold for twenty years until the Tongs burned {their} ship.",
        "{First} was a servant in a great house in {city} until the family fell.",
    ];

    static readonly string[] Turns =
    [
        "{They} served six years in the {city} watch and quit the night {they} saw something {they} still won't talk about.",
        "The Tongs took {their} family's boat to settle a debt {their} {kin} never owed.",
        "{They} studied with the Magi until {they} {was} caught reading a book that was meant to stay shut.",
        "A fever took {their} {kin} the winter the Rift opened.",
        "{They} once ran with the Lantern Boys, and still flinch{es} at lantern light.",
        "{They} fought bandits on the north road and came home with a limp and a medal {they} sold for bread.",
        "{They} swear{s} {they} saw an Angel over {place} once, and nobody can talk {them} out of it.",
        "{They} buried {their} {kin} near {place}, and the grave was empty the next spring.",
        "{They} lost everything at cards to a stranger who seemed to know how every hand would fall.",
        "{They} {was} to be married to a merchant from {city} who vanished a week before the wedding.",
        "{They} sheltered an Ashborn child one winter, and the neighbours have not forgiven {them}.",
        "{They} {was} the only one to walk out of a cave-in at {place}.",
    ];

    static readonly string[] Nows =
    [
        "Now {they} keep{s} to {home} and hope{s} to {hope}.",
        "These days {they} only want{s} to {hope}.",
        "{They} won't say it aloud, but {they} want{s} to {hope}.",
        "{They} {is} saving every coin to {hope}.",
    ];

    static readonly string[] Hopes =
    [
        "leave for {city} and never come back", "find out what happened to {their} {kin}", "see the Rift closed before {they} die{s}",
        "open a tavern of {their} own", "be left alone", "learn to read", "pay back what {they} owe{s}", "make the Tongs pay",
        "see the edge of the world", "be remembered in a song", "buy back {their} family's land",
    ];

    static readonly string[] Parents = ["fisherman", "smith", "washerwoman", "soldier", "priest", "thief", "farmer", "brewer", "Tong debt-collector"];
    static readonly string[] Belongings = ["mother's knife", "father's lute", "name", "boots", "lucky coin", "dog"];
    static readonly string[] Elders = ["parents", "grandparents", "mother", "father"];
    public static readonly string[] Kin = ["brother", "sister", "son", "daughter", "husband", "wife", "father", "mother", "best friend", "apprentice"];

    internal static IReadOnlyList<Npc> Generate(WorldGen world, Settlement s)
    {
        var rng = Noise.Dice(world.Seed, 3, WorldGen.StableHash(s.Id));
        var jobs = Jobs.GetValueOrDefault(s.Kind, Jobs[SettlementKind.Hamlet]);
        var people = new List<Npc>();
        var family = Names.Family(rng); // small places are mostly one family
        for (var i = 0; i < s.People; i++)
        {
            var sex = rng.PickWeighted<Sex>([(Sex.Female, 0.47), (Sex.Male, 0.47), (Sex.Other, 0.06)]);
            var ashborn = s.Kind == SettlementKind.AshbornCamp ? !(i == 0 && rng.Chance(0.5)) : s.Name == "Cinderhelm" && rng.Chance(0.3);
            var name = Names.Person(rng, sex, ashborn);
            if (!ashborn && s.Kind is SettlementKind.Farmstead or SettlementKind.Hamlet && rng.Chance(0.5))
                name = $"{name.Split(' ')[0]} {family}";
            var job = ashborn && s.Kind != SettlementKind.AshbornCamp ? "Ashborn labourer" : rng.Pick(jobs);
            var age = 17 + rng.Next(58);
            var angle = (i + rng.NextDouble() * 0.6) / Math.Max(1, s.People) * Math.Tau;
            var r = s.Radius * (0.25 + rng.NextDouble() * 0.35);
            if (s.Id == "city:tristinwold")
            {
                // Tristinwold's streets are full of houses: its people stand along the gate roads.
                angle = (i % 2 == 0 ? Math.PI + (i % 4 == 0 ? 0.1 : -0.1) : 0.1);
                r = 64 + i / 2 * 6;
            }
            var temper = rng.Pick(Tempers);
            var interests = new List<string>();
            while (interests.Count < 2 + rng.Next(2))
            {
                var interest = rng.Pick(Interests);
                if (!interests.Contains(interest)) interests.Add(interest);
            }
            var npc = new Npc($"{s.Id}/{i}", name, sex, age, job, s.Id, s.Name, "", interests, temper,
                Fill(rng, rng.Pick(Greetings[temper]), null, s, world), s.X + Math.Cos(angle) * r, s.Z + Math.Sin(angle) * r, ashborn);
            people.Add(npc with { Backstory = Backstory(rng, npc, s, world) });
        }
        return people;
    }

    /// <summary>A grown person's work in a place of this kind.</summary>
    internal static string JobFor(Settlement s, Rng rng) => rng.Pick(Jobs.GetValueOrDefault(s.Kind) ?? Jobs[SettlementKind.Hamlet]);

    internal static readonly string[] YouthJobs = ["page", "stable hand", "apprentice smith", "temple novice", "errand runner", "goatherd", "scullion", "fletcher's apprentice"];

    static readonly string[] Dreams =
    [
        "to be a knight, a real one, with a horse", "to learn magic from the Magi", "to see a dragon up close, just once",
        "to leave {home} for {city} and never come back", "to be a famous thief, the kind they write songs about",
        "to ride a dragon over the sea", "to find out who {their} real parents were", "to go adventuring, like you",
    ];

    /// <summary>
    /// The young people of a settlement, 11 to 16 years old, working at whatever their family can find them. They
    /// give no work, but one might follow a hero as a squire or an apprentice.
    /// </summary>
    internal static IReadOnlyList<Npc> Youths(WorldGen world, Settlement s)
    {
        if (s.Hostile || s.People == 0 || s.Kind is SettlementKind.Shrine or SettlementKind.Mine or SettlementKind.HuntingCamp) return [];
        var rng = Noise.Dice(world.Seed, 5, WorldGen.StableHash(s.Id));
        var count = s.Kind switch { SettlementKind.City => 3, SettlementKind.Town => 2 + rng.Next(2), SettlementKind.Hamlet => 1 + rng.Next(2), _ => rng.Next(2) };
        var youths = new List<Npc>();
        for (var i = 0; i < count; i++)
        {
            var sex = rng.PickWeighted<Sex>([(Sex.Female, 0.47), (Sex.Male, 0.47), (Sex.Other, 0.06)]);
            var name = Names.Person(rng, sex, false);
            var angle = rng.NextDouble() * Math.Tau;
            var r = s.Radius * (0.2 + rng.NextDouble() * 0.3);
            if (s.Id == "city:tristinwold") (angle, r) = (i % 2 == 0 ? Math.PI - 0.2 : 0.2, 70 + i * 7);
            var npc = new Npc($"{s.Id}/y{i}", name, sex, 11 + rng.Next(6), rng.Pick(YouthJobs), s.Id, s.Name, "", ["children's tales"], "eager",
                Fill(rng, rng.Pick(YouthGreetings), null, s, world), s.X + Math.Cos(angle) * r, s.Z + Math.Sin(angle) * r);
            var story = "{First} is the child of a " + rng.Pick(Parents) + " in {home}, and works as a " + npc.Occupation + ". {They} dream{s} " + rng.Pick(Dreams) + ".";
            youths.Add(npc with { Backstory = Fill(rng, story, npc, s, world) });
        }
        return youths;
    }

    static readonly string[] YouthGreetings =
    [
        "Are you an adventurer? A real one? Have you killed a troll?", "Mum says I'm not to talk to strangers. You don't look strange, though.",
        "I could carry your things! I'm stronger than I look.", "Is that a real sword? Can I hold it?",
    ];

    /// <summary>Still growing up: young, or an adolescent, by their own people's years.</summary>
    public static bool IsYouth(Npc npc) => npc.Age > 0 && Ageing.Growing(npc.Race, npc.Age);

    static string Backstory(Rng rng, Npc npc, Settlement home, WorldGen world)
    {
        var origin = npc.Ashborn
            ? "{First} was born in the year the Rift opened, to a mother who never said who the father was."
            : rng.Pick(Origins);
        var turn = rng.Pick(Turns);
        var now = rng.Pick(Nows).Replace("{hope}", rng.Pick(Hopes));
        return string.Join(" ", new[] { origin, turn, now }.Select(t => Fill(rng, t, npc, home, world)));
    }

    /// <summary>Fills a template: names, places, and pronouns that agree with their verbs.</summary>
    static string Fill(Rng rng, string t, Npc? npc, Settlement home, WorldGen world)
    {
        var plural = npc?.Sex == Sex.Other;
        var cities = world.AuthoredCities;
        string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
        t = t.Replace("{home}", home.Name)
            .Replace("{city}", rng.Pick(cities).Name)
            .Replace("{divine}", Names.Divine(rng))
            .Replace("{place}", rng.Chance(0.5) ? Names.Place(rng) : rng.Pick(cities).Name)
            .Replace("{n}", rng.Pick(["three", "five", "seven", "eleven"]))
            .Replace("{parent}", rng.Pick(Parents))
            .Replace("{belonging}", rng.Pick(Belongings))
            .Replace("{elder}", rng.Pick(Elders))
            .Replace("{kin}", rng.Pick(Kin));
        if (npc is null) return t;
        return t.Replace("{First}", npc.First)
            .Replace("{They}", Cap(npc.They)).Replace("{they}", npc.They)
            .Replace("{their}", npc.Their).Replace("{them}", npc.Them)
            .Replace("{was}", plural ? "were" : "was").Replace("{is}", plural ? "are" : "is")
            .Replace("{es}", plural ? "" : "es").Replace("{s}", plural ? "" : "s");
    }
}
