namespace Tristinwold.Gameplay;

/// <summary>
/// Something that happened out in the world, as people tell it: a hero's deed (a house going up, a man cut down on
/// the road, somebody's dogs killed) or a deviant's (a band's new chief, a hero it left for dead). The hero is never
/// named, only described as anyone who saw them would ("a tall, raven-haired dwarf woman with the look of a
/// paladin"). It's kept where it happened, so the folk nearby are the ones who talk of it.
/// </summary>
public sealed class Rumour
{
    /// <summary>The same key is the same story told again (one hero's dog killings round one town), and counts up.</summary>
    public string Key { get; set; } = "";
    /// <summary>The company it's about, if a hero's: only so they can be told it's them people mean. Never said aloud.</summary>
    public string? By { get; set; }
    /// <summary>How they're described, or null when <see cref="Did"/> is the whole story (a deviant's).</summary>
    public string? Who { get; set; }
    /// <summary>What they did, after their description ("has started building a house"), or the whole story.</summary>
    public string Did { get; set; } = "";
    /// <summary>The same, for more than once, with {n} for how many ("has killed {n} of the animals round Larkmere").</summary>
    public string? Many { get; set; }
    /// <summary>Where it happened, if known.</summary>
    public double? X { get; set; }
    public double? Z { get; set; }
    /// <summary>The area it happened in ("the Ashwood"), for a story with no spot to it.</summary>
    public string? Area { get; set; }
    /// <summary>The nearest town's name, for the telling.</summary>
    public string? Town { get; set; }
    public int Day { get; set; }
    public int Count { get; set; } = 1;
    /// <summary>A wicked deed: told with a lowered voice.</summary>
    public bool Bad { get; set; }

    /// <summary>What happened, as said: "a tall dwarf woman has been killing the dogs round Larkmere (four of them now)".</summary>
    public string Telling => (Count > 1 && Many is { } many ? many.Replace("{n}", Count.ToString(System.Globalization.CultureInfo.InvariantCulture)) : Did) is var did && Who is { } who
        ? $"{who} {did}" : Did;

    /// <summary>How long a story goes round before it's old news, in the city's days (ten minutes each).</summary>
    public const int KeepDays = 12;
    /// <summary>The most kept at once: the oldest go first.</summary>
    public const int Most = 80;

    /// <summary>
    /// How a hero looks to a stranger: "a tall, raven-haired dwarf woman with the look of a paladin". No name, ever:
    /// whoever it is can be guessed at, and maybe caught.
    /// </summary>
    public static string Describe(Avatar? a, string className)
    {
        var calling = className.Trim().ToLowerInvariant();
        if (a is null) return $"{Article(calling)} {calling}";
        var words = new List<string>();
        switch (a.Height) { case "Tall": words.Add("tall"); break; case "Short": words.Add("short"); break; }
        switch (a.Build) { case "Broad": words.Add("broad"); break; case "Slim": words.Add("slight"); break; }
        if (a.Age == "Elder") words.Add("old");
        if (!a.IsDragonkind && a.Race != "plant")
        {
            if (a.Headwear == "Hood") words.Add("hooded");
            else if (a.Style == "None") words.Add("shaven-headed");
            else words.Add($"{a.Hair.ToLowerInvariant()}-haired");
        }
        var kind = a.Kind.ToLowerInvariant();
        var body = a.Race == "plant" ? kind : $"{kind} {(a.Sex == "Female" ? "woman" : "man")}";
        var adjectives = string.Join(", ", words.Take(3));
        var looks = adjectives.Length > 0 ? $"{adjectives} {body}" : body;
        return $"{Article(looks)} {looks} with the look of {Article(calling)} {calling}";
    }

    static string Article(string word) => word.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(word[0])) ? "an" : "a";
}

public sealed partial class CityEconomy
{
    /// <summary>What people are saying, and where: heroes' deeds and the deviants'. The oldest first.</summary>
    public List<Rumour> Rumours { get; set; } = [];

    /// <summary>
    /// A story starts going round: dated today, kept within reason, and counted up if it's the same story again (the same
    /// key). Old ones are forgotten.
    /// </summary>
    public bool Heard(Rumour r)
    {
        static bool Fits(string? s, int most) => s is null || s.Length <= most;
        if (r.Key.Length is 0 or > 120 || r.Did.Length is 0 or > 240 || !Fits(r.Who, 140) || !Fits(r.Many, 240) || !Fits(r.By, 60)
            || !Fits(r.Area, 60) || !Fits(r.Town, 60)) return false;
        if (r.X is { } x && (!double.IsFinite(x) || Math.Abs(x) > 1e6) || r.Z is { } z && (!double.IsFinite(z) || Math.Abs(z) > 1e6)) return false;
        var told = Rumours.FindIndex(o => o.Key == r.Key);
        var count = Math.Clamp(r.Count, 1, 100);
        if (told >= 0)
        {
            count = Math.Min(Rumours[told].Count + count, 9999);
            Rumours.RemoveAt(told);
        }
        Rumours.Add(new Rumour
        {
            Key = r.Key, By = r.By, Who = r.Who, Did = r.Did, Many = r.Many, X = r.X, Z = r.Z, Area = r.Area, Town = r.Town,
            Day = Day, Count = count, Bad = r.Bad,
        });
        Rumours.RemoveAll(o => o.Day < Day - Rumour.KeepDays);
        if (Rumours.Count > Rumour.Most) Rumours.RemoveRange(0, Rumours.Count - Rumour.Most);
        return true;
    }
}
