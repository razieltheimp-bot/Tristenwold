namespace Tristinwold.Gameplay;

/// <summary>A tree a druid set in the ground: where, by whom, and the day it went in. It grows for everyone.</summary>
public sealed class Planted
{
    public string Owner { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public int Day { get; set; }
}

/// <summary>
/// A druid's planting. A druid can put a tree in anywhere, out in the wilds or in the middle of a town, and the land
/// keeps it: a sapling the day it goes in, a young tree a day on, and full grown on the third. In the Northwood, every
/// tree a druid plants makes good one the hero has cut, as far as the guardians are concerned.
/// </summary>
public static class Groves
{
    public const int MostEach = 80, MostAll = 800;
    /// <summary>How close two planted trees may stand, and what one costs in mana.</summary>
    public const double Spacing = 2.5, Mana = 25;
    public const int YoungAt = 1, GrownAt = 3;

    /// <summary>0 a sapling, 1 a young tree, 2 grown.</summary>
    public static int Stage(Planted p, int today) => today - p.Day >= GrownAt ? 2 : today - p.Day >= YoungAt ? 1 : 0;

    /// <summary>How tall it stands, as a share of a grown tree.</summary>
    public static double Size(Planted p, int today) => Stage(p, today) switch { 0 => 0.18, 1 => 0.5, _ => 1 };

    /// <summary>Whether a druid may only plant here once they've the knack: the deep forest wants a druid who's grown with it.</summary>
    public static bool MayPlant(string classId) => classId is "Stormdruid" or "Shapeshifter" or "Spiritcaller";
}

public sealed partial class CityEconomy
{
    /// <summary>Every tree the druids have planted, for everyone.</summary>
    public List<Planted> Grove { get; set; } = [];

    public bool RoomToPlant(string owner, double x, double z) =>
        Grove.Count < Groves.MostAll && Grove.Count(p => p.Owner == owner) < Groves.MostEach &&
        Grove.All(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) >= Groves.Spacing * Groves.Spacing);

    bool Plant(string owner, double x, double z)
    {
        if (owner.Length is 0 or > 60 || !double.IsFinite(x) || !double.IsFinite(z) || !RoomToPlant(owner, x, z)) return false;
        Grove.Add(new Planted { Owner = owner, X = x, Z = z, Day = Day });
        return true;
    }
}
