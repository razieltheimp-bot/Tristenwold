namespace Tristinwold.Gameplay;

/// <summary>
/// How the Rift spreads between dragons. Every wound a corrupted dragon deals another dragon carries the
/// corruption in, building up in the victim; it ebbs away slowly if the dragon gets clear. Once it fills, the
/// dragon turns: corrupted itself, and mad with it, in a frenzy that strikes at anything near.
/// </summary>
public static class Corruption
{
    /// <summary>How much corruption a wound carries in, for its share of the victim's health.</summary>
    public const double Virulence = 1.6;

    /// <summary>A dragon clear of the corrupted sheds its corruption over this many seconds.</summary>
    public const double EbbSeconds = 90;

    /// <summary>How long the madness lasts once a dragon turns.</summary>
    public const double FrenzySeconds = 45;

    /// <summary>In a frenzy: faster, and hitting harder.</summary>
    public const double FrenzySpeed = 1.4, FrenzyMight = 1.35;

    /// <summary>Corruption after a wound (0 to 1; at 1 the dragon turns).</summary>
    public static double Infect(double corruption, double wound, double maxHealth) =>
        Math.Min(1, corruption + Math.Max(0, wound) / Math.Max(1, maxHealth) * Virulence);

    public static bool Turns(double corruption) => corruption >= 1;

    /// <summary>Corruption after some time clear of the corrupted.</summary>
    public static double Ebb(double corruption, double seconds) => Math.Max(0, corruption - seconds / EbbSeconds);
}
