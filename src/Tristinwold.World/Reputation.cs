namespace Tristinwold.World;

/// <summary>
/// What people think of a hero, from -5 (they hate you) to +5 (they love you), with 0 for a stranger, kept with the hero.
/// Each town has its own opinion, built from what they've seen done there; the country has one too, built from all of it.
/// A town out of touch goes on its own view. As trade links it to the country, rumour reaches it and the two blend, until
/// a well-connected town's view is the average of them: so a hero they like may find they like them less once the wagons
/// start bringing the talk from elsewhere. And each person has a view of their own, from what's been done to them.
/// </summary>
public sealed class ReputationBook
{
    public const double Least = -5, Most = 5;
    /// <summary>How much of a town's opinion of a deed reaches the country, and how far trade can move one person.</summary>
    public const double CountryShare = 0.25, TradeRegard = 2;
    /// <summary>How well Tristinwold's old partners hear the country's talk from the start, and how much each trip adds.</summary>
    public const double OldRouteLink = 0.25, TripLink = 0.15;
    public const double HelpRegard = 1, RefuseRegard = -0.25, RobRegard = -2, HarmRegard = -1.5, TradedRegard = 0.05;

    public const string Capital = "city:tristinwold";
    static readonly HashSet<string> OldRoutes = ["city:hollowheart", "city:druitt", "city:greywater"];

    /// <summary>What each town has seen for itself, by place.</summary>
    public Dictionary<string, double> Towns { get; set; } = [];
    /// <summary>What each person thinks, by their id; and their names, for the book.</summary>
    public Dictionary<string, double> People { get; set; } = [];
    public Dictionary<string, string> Names { get; set; } = [];
    /// <summary>How far trading has moved each person so far: it only goes so far.</summary>
    public Dictionary<string, double> Traded { get; set; } = [];
    /// <summary>How well each town hears the country's talk, from 0 (not at all) to 1.</summary>
    public Dictionary<string, double> Links { get; set; } = [];
    public double Country { get; set; }

    /// <summary>The most anyone can think of whoever has Arachnicalibur in hand, however good they've been.</summary>
    public const double WebMark = -1;
    /// <summary>The black blade's in the hero's hand. Set by the game from what they're holding; what people really thought comes back when it's put away.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Marked { get; set; }

    double Seen(double view) => Marked ? Math.Min(view, WebMark) : view;

    /// <summary>What the country thinks, as it stands.</summary>
    public double CountryView => Seen(Country);

    static double Clamp(double v) => Math.Clamp(v, Least, Most);

    /// <summary>The one place, whether you came to it by road or by sea.</summary>
    public static string Key(string place) => place.StartsWith("port:") ? "city:" + place[5..] : place;

    public double Link(string place)
    {
        var key = Key(place);
        return key == Capital ? 1 : Links.TryGetValue(key, out var link) ? link : OldRoutes.Contains(key) ? OldRouteLink : 0;
    }

    /// <summary>What a town has seen for itself.</summary>
    public double Own(string place) => Towns.GetValueOrDefault(Key(place));

    /// <summary>What a town thinks: its own view, moved toward the average of it and the country's as far as the talk reaches it.</summary>
    public double Town(string place)
    {
        var (own, link) = (Own(place), Link(place));
        return Seen(Clamp(own + (Country - own) * link / 2));
    }

    public double Person(string id) => Seen(People.GetValueOrDefault(id));

    /// <summary>How much a deed of this size moves the people who saw it: small things a little, the great ones never more than a point and a half.</summary>
    public static double Weight(int size) => Math.Min(1.5, 0.1 + 0.12 * Math.Abs(size));

    /// <summary>Something done, good or bad by its sign: where it's seen (if anywhere), and the country hears a share of it.</summary>
    public void Deed(int size, string? place)
    {
        var moved = Weight(size) * Math.Sign(size);
        if (place is not null) Towns[Key(place)] = Clamp(Own(place) + moved);
        Country = Clamp(Country + moved * CountryShare);
    }

    void Regard(string id, string name, double by)
    {
        People[id] = Clamp(People.GetValueOrDefault(id) + by);
        Names[id] = name;
    }

    public void Helped(string id, string name) => Regard(id, name, HelpRegard);
    public void Refused(string id, string name) => Regard(id, name, RefuseRegard);
    public void Robbed(string id, string name) => Regard(id, name, RobRegard);
    public void Harmed(string id, string name) => Regard(id, name, HarmRegard);

    /// <summary>A bit of business done with someone: a little goodwill, up to a point.</summary>
    public void TradedWith(string id, string name)
    {
        if (Traded.GetValueOrDefault(id) >= TradeRegard) return;
        Traded[id] = Traded.GetValueOrDefault(id) + TradedRegard;
        Regard(id, name, TradedRegard);
    }

    /// <summary>A trip's trade with a town: it hears more of the country's talk. What changed, if it's enough to notice.</summary>
    public double Trip(string place)
    {
        var was = Town(place);
        Links[Key(place)] = Math.Min(1, Link(place) + TripLink);
        return Town(place) - was;
    }

    /// <summary>What a share of the price each point is worth, and where people stop dealing with you: no work first, then no service.</summary>
    public const double PricePerPoint = 0.03, NoWorkAt = -3, ShutOutAt = -4, FavourAt = 3, FavourBonus = 0.1;

    /// <summary>How someone stands with the hero: their own view and their town's, equally. Out of any town, it's only theirs.</summary>
    public double Regard(string? place, string person) => place is null ? Person(person) : (Town(place) + Person(person)) / 2;

    /// <summary>What's asked, as a share of the price, of someone this well regarded: from 15% less to 15% more.</summary>
    public static double BuyFactor(double regard) => 1 - PricePerPoint * regard;
    /// <summary>What's paid, as a share of the offer: from 15% more to 15% less.</summary>
    public static double SellFactor(double regard) => 1 + PricePerPoint * regard;
    public static bool RefusesWork(double regard) => regard <= NoWorkAt;
    public static bool ShutsDoor(double regard) => regard <= ShutOutAt;
    /// <summary>A reward as paid to someone they think well of.</summary>
    public static int Reward(int crowns, double regard) => regard >= FavourAt ? (int)Math.Round(crowns * (1 + FavourBonus)) : crowns;

    /// <summary>How they take it when the hero walks up, or nothing if they've no feelings on the matter.</summary>
    public static string? Manner(double regard, string name) => regard switch
    {
        <= -4 => $"{name} sees who it is and goes very still.",
        <= -2 => $"{name} looks you over without warmth.",
        >= 4 => $"{name}'s whole face lights up when they see you.",
        >= 2 => $"{name} gives you a real smile.",
        _ => null,
    };

    public static string Word(double view) => view switch
    {
        <= -4 => "hated",
        <= -2 => "disliked",
        < -0.5 => "not well thought of",
        <= 0.5 => "nobody much",
        < 2 => "well thought of",
        < 4 => "liked",
        _ => "loved",
    };

    public static string Number(double view) => Math.Abs(view) < 0.05 ? "0" : view.ToString("+0.#;-0.#");
}
