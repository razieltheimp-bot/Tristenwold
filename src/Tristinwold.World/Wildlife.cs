using Tristinwold.Nemesis;

namespace Tristinwold.World;

/// <summary>
/// A herd, pack or lone beast living out in the wilds: what it is, where it ranges, and how many.
/// Ids are stable, so a pack you thinned is the same pack when you come back.
/// </summary>
public sealed record Herd(string Id, string KindId, double X, double Z, int Count, double Roam)
{
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

/// <summary>
/// Life between the settlements, worked out from the seed like everything else, so every company
/// in the shared world meets the same wolves in the same woods. The land is cut into cells; most
/// hold something. Deer and foxes graze everywhere and run from you; the further from Tristinwold
/// (the higher the danger), the more of the land belongs to wolves, bears and trolls. Near home
/// only the harmless and the merely annoying (skeevers, mudcrabs) are about.
/// </summary>
public static class Wildlife
{
    public const double Cell = 150;

    /// <summary>Nothing that hunts people lives this close to Tristinwold.</summary>
    public const double SafeRadius = 450;

    /// <summary>Animals that run rather than fight.</summary>
    public static readonly IReadOnlySet<string> Timid = new HashSet<string> { "deer", "fox" };

    public static IEnumerable<Herd> Near(WorldGen world, double x, double z, double radius)
    {
        var (c0, c1) = ((long)Math.Floor((x - radius) / Cell), (long)Math.Floor((x + radius) / Cell));
        var (r0, r1) = ((long)Math.Floor((z - radius) / Cell), (long)Math.Floor((z + radius) / Cell));
        for (var cx = c0; cx <= c1; cx++)
            for (var cz = r0; cz <= r1; cz++)
                if (In(world, cx, cz) is { } h && h.DistanceTo(x, z) <= radius) yield return h;
    }

    /// <summary>What lives in one cell, if anything.</summary>
    public static Herd? In(WorldGen world, long cx, long cz)
    {
        var rng = Noise.Dice(world.Seed, 41, cx, cz);
        if (!rng.Chance(0.6)) return null;
        var x = (cx + 0.15 + rng.NextDouble() * 0.7) * Cell;
        var z = (cz + 0.15 + rng.NextDouble() * 0.7) * Cell;
        var h = world.Height(x, z);
        if (h < WorldGen.WaterLevel + 0.6) return null;
        // Settlements keep their own: no wild animals wander through a town or a bandit camp.
        if (world.SettlementsNear(x, z, 60).Any(s => s.DistanceTo(x, z) < s.Radius + (s.IsCity ? 90 : 40))) return null;

        var biome = world.BiomeAt(x, z, h);
        var danger = WorldGen.Danger(x, z);
        var safe = Math.Sqrt(x * x + z * z) < SafeRadius;
        var near = world.Height(x + 30, z) < WorldGen.WaterLevel || world.Height(x - 30, z) < WorldGen.WaterLevel
            || world.Height(x, z + 30) < WorldGen.WaterLevel || world.Height(x, z - 30) < WorldGen.WaterLevel;
        var d = safe ? 0 : danger;
        (string, double)[] choices = biome switch
        {
            Biome.AutumnForest => [("deer", 4), ("fox", 2), ("wolf", 0.8 * d), ("bear", 0.3 * d), ("spriggan", 0.25 * d)],
            Biome.PineForest => [("deer", 3.5), ("fox", 1.5), ("wolf", 1.1 * d), ("bear", 0.6 * d)],
            Biome.Marsh => [("skeever", 3), ("mudcrab", near ? 3 : 0.5), ("deer", 1), ("wolf", 0.5 * d)],
            Biome.Tundra => [("deer", 2), ("fox", 1), ("wolf", 2 * d), ("ice_wolf", 1 * d), ("bear", 0.5 * d)],
            Biome.Snowfield => [("ice_wolf", 1.4 * Math.Max(d, 0.1)), ("frost_troll", 0.5 * d), ("deer", 0.8), ("fox", 0.8)],
            _ => [("bear", 1.5 * Math.Max(d, 0.1)), ("frost_troll", 1 * d), ("wolf", 1.5 * d), ("deer", 0.6)],
        };
        if (near && biome != Biome.Snowfield) choices = [.. choices, ("mudcrab", 1.5)];
        choices = [.. choices.Where(c => c.Item2 > 0)];
        if (choices.Length == 0) return null;
        var kind = rng.PickWeighted(choices);
        var extra = (int)Math.Min(2, danger - 1.5); // packs grow out in the far wilds
        var count = kind switch
        {
            "deer" => 3 + rng.Next(3),
            "fox" => 1 + rng.Next(2),
            "wolf" => 2 + rng.Next(2) + extra,
            "ice_wolf" => 2 + rng.Next(2) + extra,
            "skeever" => 3 + rng.Next(3),
            "mudcrab" => 2 + rng.Next(3),
            "spriggan" => 1 + rng.Next(2),
            _ => 1, // bears and trolls keep to themselves
        };
        var roam = kind switch { "deer" => 22, "fox" => 18, "wolf" or "ice_wolf" => 16, "mudcrab" => 6, _ => 10 };
        return new Herd($"wild_{cx}_{cz}", kind, x, z, count, roam);
    }
}
