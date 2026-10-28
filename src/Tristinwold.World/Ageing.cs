using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>The stages of a life, the same names for every people, though they come at very different ages.</summary>
public enum LifeStage { Young, Adolescent, Adult, Seasoned, Elder }

/// <summary>When each stage of life begins for a people, and how long they live, in years.</summary>
public sealed record Lifespan(int Adolescent, int Adult, int Seasoned, int Elder, int Dies);

/// <summary>
/// The world's calendar. A day passes every ten minutes of play (the city's days: see <see cref="CityEconomy"/>); a
/// week is seven days (a little over an hour); a month four weeks; a year twelve months, 336 days, about 56 hours of
/// play. Day 1 is the first morning of year 1.
/// </summary>
public static class Calendar
{
    public const int DaysPerWeek = 7, WeeksPerMonth = 4, MonthsPerYear = 12;
    public const int DaysPerMonth = DaysPerWeek * WeeksPerMonth, DaysPerYear = DaysPerMonth * MonthsPerYear;

    static readonly string[] Months =
        ["Thawmoon", "Seedmoon", "Bloommoon", "Highsun", "Goldmoon", "Haymoon", "Harvestmoon", "Fallmoon", "Mistmoon", "Frostmoon", "Deepwinter", "Longnight"];

    /// <summary>How many years have passed since the first morning (fractional).</summary>
    public static double Years(int day) => Math.Max(0, day - 1) / (double)DaysPerYear;

    /// <summary>"the 3rd of Harvestmoon, year 2".</summary>
    public static string Date(int day)
    {
        var d = Math.Max(0, day - 1);
        var (year, month, date) = (d / DaysPerYear + 1, d % DaysPerYear / DaysPerMonth, d % DaysPerMonth + 1);
        var th = date is 1 or 21 ? "st" : date is 2 or 22 ? "nd" : date is 3 or 23 ? "rd" : "th";
        return $"the {date}{th} of {Months[month]}, year {year}";
    }
}

/// <summary>
/// Growing up and growing old. Every people ages its own way: a goblin is grown at nine and old at thirty; a human
/// grown at eighteen and old at sixty; a dwarf grown at forty; an elf not grown until a hundred, and not old for five
/// hundred years more. The villages' people age with the calendar: children grow into adolescents and then adults,
/// and every time one comes of age a new child is born into the family; the old pass on when their people's years are
/// done. An Immortal ages too, but never dies, and goes on long past what any of their people live to.
/// </summary>
public static class Ageing
{
    public static Lifespan Of(string? race) => race switch
    {
        "orc" => new(10, 14, 30, 45, 60),
        "goblin" => new(6, 9, 20, 30, 40),
        "elf" => new(40, 100, 300, 500, 700),
        "dwarf" => new(20, 40, 120, 200, 260),
        "half_giant" => new(14, 20, 45, 70, 95),
        Races.Dragonkind => new(25, 60, 200, 400, 600),
        _ => new(13, 18, 40, 60, 80),
    };

    public static LifeStage Stage(string? race, double age)
    {
        var l = Of(race);
        return age < l.Adolescent ? LifeStage.Young : age < l.Adult ? LifeStage.Adolescent : age < l.Seasoned ? LifeStage.Adult
            : age < l.Elder ? LifeStage.Seasoned : LifeStage.Elder;
    }

    public static string Word(LifeStage s) => s.ToString().ToLowerInvariant();

    /// <summary>Whether they're still growing up (young, or an adolescent).</summary>
    public static bool Growing(string? race, double age) => Stage(race, age) < LifeStage.Adult;

    /// <summary>A human's age said in another people's years, stage for stage (a forty-year-old human is a seasoned elf of three hundred).</summary>
    public static int InYearsOf(string? race, double humanAge)
    {
        var (h, r) = (Of("human"), Of(race));
        double[] hs = [0, h.Adolescent, h.Adult, h.Seasoned, h.Elder, h.Dies];
        double[] rs = [0, r.Adolescent, r.Adult, r.Seasoned, r.Elder, r.Dies];
        for (var i = 1; i < hs.Length; i++)
            if (humanAge < hs[i]) return (int)Math.Round(rs[i - 1] + (humanAge - hs[i - 1]) / (hs[i] - hs[i - 1]) * (rs[i] - rs[i - 1]));
        return (int)Math.Round(r.Dies * humanAge / h.Dies);
    }

    /// <summary>
    /// Who someone is, by birth: mostly human, but dwarves dig the mines and the crags, elves keep to the old woods,
    /// orcs and goblins come down from the hills and out of the marsh, and a half-giant turns up now and then. The
    /// same person is always the same people.
    /// </summary>
    public static string RaceFor(string npcId, Settlement home)
    {
        var rng = new Rng((ulong)WorldGen.StableHash(npcId) ^ 0xA6E);
        if (home.Id == "city:tristinwold" || home.Kind == SettlementKind.AshbornCamp) return rng.Chance(0.85) ? "human" : rng.Pick(["dwarf", "elf", "orc", "half_giant"]);
        (string, double)[] odds = home.Kind == SettlementKind.Mine || home.Biome == Biome.Crags
            ? [("human", 4), ("dwarf", 5), ("orc", 1), ("goblin", 0.5)]
            : home.Biome is Biome.AutumnForest or Biome.PineForest
                ? [("human", 6), ("elf", 2.5), ("dwarf", 0.5), ("half_giant", 0.3)]
                : home.Biome is Biome.Marsh ? [("human", 5), ("goblin", 2.5), ("orc", 1.2)]
                : [("human", 5), ("orc", 1.5), ("half_giant", 1), ("dwarf", 1)];
        return rng.PickWeighted(odds);
    }

    /// <summary>The hero's first morning: they woke at about twenty, in their own people's years.</summary>
    public static void Wake(Hero hero, string race, int day)
    {
        if (hero.StartAge > 0) return;
        hero.StartAge = InYearsOf(race, 20);
        hero.BornDay = day;
    }

    /// <summary>An Immortal's age now: they go on ageing, and never die of it.</summary>
    public static double AgeOf(Hero hero, int day) => hero.StartAge + Math.Max(0, day - hero.BornDay) / (double)Calendar.DaysPerYear;

    /// <summary>"27, an adult human"; and for one long past their people's years, that they should be dead.</summary>
    public static string Describe(Hero hero, string race, string raceName, int day)
    {
        var age = AgeOf(hero, day);
        var word = Word(Stage(race, age));
        var line = $"{(int)age}, {("aeiou".Contains(word[0]) ? "an" : "a")} {word} {raceName.ToLowerInvariant()}";
        return age >= Of(race).Dies
            ? $"{line}, {(int)(age - Of(race).Dies)} years past the oldest any {raceName.ToLowerInvariant()} lives to. Immortals age; they don't die of it"
            : line;
    }

    /// <summary>
    /// The people of a place a number of years into the world: everyone who was there at the start, aged that many
    /// years (and gone, if their people's years are done); and every child born since, one each time one of the
    /// family's young came of age. Each one's <see cref="Npc.Age"/> is their age now.
    /// </summary>
    public static IReadOnlyList<Npc> Villagers(WorldGen world, Settlement s, double years)
    {
        var people = new List<Npc>();
        foreach (var n in world.PeopleOf(s))
        {
            var age = n.Age + years;
            if (age < Of(n.Race).Dies) people.Add(n with { Age = (int)Math.Floor(age) });
        }
        // Each young one founds a line: when they come of age, another child is born into it, and so on down the years.
        foreach (var y in world.YouthsOf(s))
        {
            var span = Of(y.Race);
            for (var k = 0; ; k++)
            {
                var born = k * span.Adult - y.Age; // years after the start that this one was born
                if (born > years) break;
                var age = years - born;
                if (age >= span.Dies) continue;
                var n = k == 0 ? y : Child(s, y, k, born);
                n = n with { Age = (int)Math.Floor(age) };
                people.Add(Growing(n.Race, age) ? n : Grown(world, s, n));
            }
        }
        return people;
    }

    /// <summary>The k-th child born into a family line since the start.</summary>
    static Npc Child(Settlement s, Npc founder, int k, double born)
    {
        var rng = new Rng((ulong)WorldGen.StableHash($"{founder.Id}/g{k}") ^ 0xB1A7);
        var sex = rng.PickWeighted<Sex>([(Sex.Female, 0.47), (Sex.Male, 0.47), (Sex.Other, 0.06)]);
        var family = founder.Name.Split(' ').Last();
        var first = Names.Person(rng, sex, false).Split(' ')[0];
        var angle = rng.NextDouble() * Math.Tau;
        var r = s.Radius * (0.2 + rng.NextDouble() * 0.3);
        return founder with
        {
            Id = $"{founder.Id}/g{k}", Name = $"{first} {family}", Sex = sex, Occupation = rng.Pick(People.YouthJobs),
            Backstory = $"{first} was born in {s.Name} in year {(int)Math.Floor(Math.Max(0, born)) + 1}, into the {family} family, and hasn't been anywhere else yet.",
            X = s.X + Math.Cos(angle) * r, Z = s.Z + Math.Sin(angle) * r,
        };
    }

    /// <summary>One of the young, come of age: a grown person's work and words now.</summary>
    static Npc Grown(WorldGen world, Settlement s, Npc n)
    {
        var rng = new Rng((ulong)WorldGen.StableHash(n.Id) ^ 0x6A0);
        return n with
        {
            Occupation = People.JobFor(s, rng),
            Greeting = rng.Pick(["I grew up here. Every stone of it.", "You're new. Well. Welcome.", "Work doesn't wait. What do you need?"]),
            Interests = ["home", rng.Pick(new[] { "gossip", "money", "the roads", "old battles" })],
            Backstory = $"{n.First} grew up here in {s.Name}, and never left.",
        };
    }
}
