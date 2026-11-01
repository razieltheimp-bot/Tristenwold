namespace Tristinwold.World;

/// <summary>Where a hero stands with Hollowheart: not asked yet, sent to the turned guardian, or come back from killing it.</summary>
public enum ForestStage { None, Asked, Slain }

/// <summary>
/// A hero's part in the Northwood, saved with the company: the village's asking, how often they've answered, and how many
/// trees they've cut (the guardians count). What's happened to the guardians and the dragon is everybody's, on the
/// server, in the city's book.
/// </summary>
public sealed class ForestBook
{
    public ForestStage Stage { get; set; }
    public int Ended { get; set; }
    public int TreesCut { get; set; }
    /// <summary>Raffkin has told this hero where the true guardians are.</summary>
    public bool Told { get; set; }
    /// <summary>This hero has taken Blightthorn from the turned guardian: the first kill always gives it, later ones seldom.</summary>
    public bool Thorn { get; set; }
    /// <summary>The rot is in this hero's blood, and it stays there until a druid draws it out.</summary>
    public bool Blighted { get; set; }
    /// <summary>This hero helped bring down the fifth guardian: the stone asks that much.</summary>
    public bool DragonSlayer { get; set; }
    /// <summary>When Soren next comes after this hero, if they hold his blade: seconds of play.</summary>
    public double SorenHuntAt { get; set; }
    /// <summary>A shapeshifter's studies: how many of each beast they've fought and killed, and the shapes they've learned.</summary>
    public Dictionary<string, int> Studied { get; set; } = [];
    public List<string> Shapes { get; set; } = ["wolf"];
    /// <summary>How long they've spent in each shape, in seconds: the one they've used most is the one a half-form reaches for.</summary>
    public Dictionary<string, double> Worn { get; set; } = [];
    /// <summary>The shape they've worn the most, or the first they learned if they've worn none.</summary>
    public string Favourite => Worn.Where(w => Shapes.Contains(w.Key)).OrderByDescending(w => w.Value).ThenBy(w => w.Key, StringComparer.Ordinal).Select(w => w.Key).FirstOrDefault() ?? Shapes[0];
    /// <summary>What this hero's had to do with Turlin.</summary>
    public TurlinBook Turlin { get; set; } = new();

    /// <summary>How many of a beast it takes to learn its shape.</summary>
    public const int StudyKills = 8;
    /// <summary>What can be studied: beasts, and nothing undead, nothing dragon, nothing that thinks.</summary>
    public static readonly IReadOnlyList<string> Beasts = ["wolf", "ice_wolf", "bear", "boar", "giant_rat", "fox", "mud_crab", "deathcrest_ape"];

    public static string ShapeName(string kind) => kind switch
    {
        "wolf" => "Werewolf", "ice_wolf" => "Frost Werewolf", "bear" => "War Bear", "boar" => "Great Boar", "giant_rat" => "Wererat", "fox" => "Fox Spirit",
        "mud_crab" => "Shellback", "deathcrest_ape" => "Deathcrest", _ => kind,
    };

    /// <summary>One more of a beast fought and killed. The shape's name when that's enough to learn it.</summary>
    public string? Study(string kind)
    {
        if (!Beasts.Contains(kind) || Shapes.Contains(kind)) return null;
        Studied[kind] = Studied.GetValueOrDefault(kind) + 1;
        if (Studied[kind] < StudyKills) return null;
        Shapes.Add(kind);
        return ShapeName(kind);
    }

    public const int Pay = 400;
    /// <summary>The crowns found on a true guardian, and how often the turned one's thorn turns up.</summary>
    public const int Hoard = 1500;
    public const double ThornChance = 0.15;
    /// <summary>What the rot takes: a share of the hero's or the beast's health, every so many seconds, until it's cured.</summary>
    public const double BlightShare = 0.06, BlightSeconds = 25, BlightChance = 0.35;
    public const int CurePrice = 60;
    /// <summary>How often a bound beast may go feral again, and how likely it is each time.</summary>
    public const double FeralSeconds = 5 * 60, FeralChance = 0.25;
    /// <summary>How long the turned guardian stays down: seconds of play.</summary>
    public const double Regrow = 15 * 60;
}
