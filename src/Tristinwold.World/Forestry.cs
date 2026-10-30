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
    /// <summary>Rafikki has told this hero where the true guardians are.</summary>
    public bool Told { get; set; }
    /// <summary>This hero has taken Blightthorn from the turned guardian: the first kill always gives it, later ones seldom.</summary>
    public bool Thorn { get; set; }
    /// <summary>The rot is in this hero's blood, and it stays there until a druid draws it out.</summary>
    public bool Blighted { get; set; }

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
