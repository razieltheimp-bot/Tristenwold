namespace Tristinwold.Gameplay;

/// <summary>The kinds of work heroes do that a place shows for: ships mended at the yard, iron worked at its forges, crops brought in.</summary>
public enum HandiKind { Ships, Smithing, Farming }

/// <summary>What heroes have done in one place lately: how much of each kind of work, fading a little every day.</summary>
public sealed class Handiwork
{
    public double Ships { get; set; }
    public double Smithing { get; set; }
    public double Farming { get; set; }

    public double this[HandiKind kind]
    {
        get => kind switch { HandiKind.Ships => Ships, HandiKind.Smithing => Smithing, _ => Farming };
        set { switch (kind) { case HandiKind.Ships: Ships = value; break; case HandiKind.Smithing: Smithing = value; break; default: Farming = value; break; } }
    }
}

/// <summary>
/// How what heroes do shows in the world, a little at a time: a yard kept busy mending ships fills the bay with more of
/// them; a town whose forges are kept hot arms its guards better; a town that's well fed sees more of its children
/// grow up. It fades if nobody keeps at it, by about half in three weeks, so a place shows what's being done there now.
/// </summary>
public static class Handiworks
{
    /// <summary>What's left of a day's work the next day.</summary>
    public const double Fade = 0.97;
    /// <summary>The most a single piece of work can count for.</summary>
    public const int MostAtOnce = 10;

    /// <summary>Ships riding at anchor in Tristinwold's bay: two at rest, and another for every four planks fitted lately, up to nine.</summary>
    public static int BayShips(double ships) => Math.Clamp(2 + (int)(ships / 4), 2, 9);

    /// <summary>How well a town's guards are armed, by its forges: 0 as ever, 1 in good steel (from 15 pieces), 2 in plate (from 45).</summary>
    public static int GuardTier(double smithing) => smithing >= 45 ? 2 : smithing >= 15 ? 1 : 0;

    /// <summary>The youngsters a well-fed town has besides its usual folk: one for every eight harvests lately, up to six.</summary>
    public static int FedYouths(double farming) => Math.Min(6, (int)(farming / 8));
}

public sealed partial class CityEconomy
{
    /// <summary>What heroes have done in each place lately (Tristinwold under <see cref="Capital"/>).</summary>
    public Dictionary<string, Handiwork> Handiwork { get; set; } = [];

    double WorkIn(string town, HandiKind kind) => Handiwork.TryGetValue(town, out var w) ? w[kind] : 0;

    /// <summary>A piece of a hero's work done in a place, for everyone.</summary>
    bool Worked(string town, HandiKind kind, int n)
    {
        if (town.Length is 0 or > 80 || n <= 0) return false;
        if (!Handiwork.TryGetValue(town, out var w)) Handiwork[town] = w = new Handiwork();
        w[kind] += Math.Min(n, Handiworks.MostAtOnce);
        return true;
    }

    /// <summary>A day goes by: every place's work fades a little, and what's faded to nothing is forgotten.</summary>
    void FadeWork()
    {
        foreach (var (town, w) in Handiwork.ToList())
        {
            foreach (var k in Enum.GetValues<HandiKind>()) w[k] *= Handiworks.Fade;
            if (Enum.GetValues<HandiKind>().All(k => w[k] < 0.5)) Handiwork.Remove(town);
        }
    }

    /// <summary>Ships riding at anchor in Tristinwold's bay, for the work at its yard.</summary>
    public int BayShips => Handiworks.BayShips(WorkIn(Capital, HandiKind.Ships));
    /// <summary>How well a town's guards are armed, for the work at its forges.</summary>
    public int GuardTier(string town) => Handiworks.GuardTier(WorkIn(town, HandiKind.Smithing));
    /// <summary>The extra youngsters a town has, for the harvests brought in there.</summary>
    public int FedYouths(string town) => Handiworks.FedYouths(WorkIn(town, HandiKind.Farming));
}
