namespace Tristinwold.World;

/// <summary>A plant that can be picked: which herb it gives, and where it grows.</summary>
public sealed record Plant(string Id, string HerbId, double X, double Z)
{
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

/// <summary>
/// Herbs growing wild, worked out from the seed like the beasts, so everyone finds the same patch of emberleaf in the
/// same wood. Each kind of land grows its own: emberleaf, lady's mantle and bloodcap in the autumn woods; bluebells
/// and ghostcap under the pines; reeds and nightshade in the marsh; snowberries and frostbloom in the cold north;
/// thistle and silverthorn up on the crags. Nothing grows in the towns or in the water. Picked, a plant grows back
/// in a while.
/// </summary>
public static class Herbs
{
    public const double Cell = 45;

    /// <summary>How long a picked plant takes to grow back (seconds of play).</summary>
    public const double RegrowSeconds = 12 * 60;

    static (string, double)[] Growing(Biome b) => b switch
    {
        Biome.AutumnForest => [("emberleaf", 3), ("ladys_mantle", 2), ("bloodcap", 1), ("thistle", 0.6)],
        Biome.PineForest => [("bluebell", 3), ("ghostcap", 1.5), ("ladys_mantle", 1), ("emberleaf", 0.8)],
        Biome.Marsh => [("marsh_reed", 3), ("nightshade", 1.5), ("bloodcap", 1), ("ghostcap", 0.6)],
        Biome.Tundra => [("snowberry", 3), ("frostbloom", 1), ("thistle", 1)],
        Biome.Snowfield => [("frostbloom", 2), ("snowberry", 1)],
        _ => [("thistle", 3), ("silverthorn", 1.5), ("frostbloom", 0.4), ("bluebell", 0.6)],
    };

    public static IEnumerable<Plant> Near(WorldGen world, double x, double z, double radius)
    {
        var (c0, c1) = ((long)Math.Floor((x - radius) / Cell), (long)Math.Floor((x + radius) / Cell));
        var (r0, r1) = ((long)Math.Floor((z - radius) / Cell), (long)Math.Floor((z + radius) / Cell));
        for (var cx = c0; cx <= c1; cx++)
            for (var cz = r0; cz <= r1; cz++)
                foreach (var p in In(world, cx, cz))
                    if (p.DistanceTo(x, z) <= radius) yield return p;
    }

    /// <summary>The plants in one cell: often none, sometimes a little patch of one herb.</summary>
    public static IEnumerable<Plant> In(WorldGen world, long cx, long cz)
    {
        var rng = Noise.Dice(world.Seed, 77, cx, cz);
        if (!rng.Chance(0.55)) yield break;
        var x = (cx + 0.15 + rng.NextDouble() * 0.7) * Cell;
        var z = (cz + 0.15 + rng.NextDouble() * 0.7) * Cell;
        var h = world.Height(x, z);
        if (h < WorldGen.WaterLevel + 0.6) yield break;
        if (Math.Sqrt(x * x + z * z) < 115) yield break; // not inside Tristinwold's walls
        if (world.SettlementsNear(x, z, 60).Any(s => s.DistanceTo(x, z) < s.Radius + (s.IsCity ? 60 : 15))) yield break;
        var herb = rng.PickWeighted(Growing(world.BiomeAt(x, z, h)));
        var count = 1 + rng.Next(3);
        for (var i = 0; i < count; i++)
        {
            var (px, pz) = (x + (rng.NextDouble() * 2 - 1) * 4, z + (rng.NextDouble() * 2 - 1) * 4);
            if (world.Height(px, pz) < WorldGen.WaterLevel + 0.4) continue;
            yield return new Plant($"herb_{cx}_{cz}_{i}", herb, px, pz);
        }
    }

    /// <summary>How many a hero picks from one plant: more as they learn the trade.</summary>
    public static int Yield(int herbalism, double roll) => 1 + (roll < (herbalism - 1) * 0.12 ? 1 : 0) + (herbalism >= 8 && roll < 0.25 ? 1 : 0);
}

/// <summary>Which plants a company has picked, and when, so they grow back in their own time. Saved with the company.</summary>
public sealed class HerbBook
{
    public Dictionary<string, double> PickedAt { get; set; } = [];

    public bool Ready(Plant p, double playSeconds) =>
        !PickedAt.TryGetValue(p.Id, out var at) || playSeconds - at >= Herbs.RegrowSeconds;

    /// <summary>The same for anything else that's gathered and comes back (a mined vein, a felled tree).</summary>
    public bool Ready(string id, double playSeconds, double regrow) =>
        !PickedAt.TryGetValue(id, out var at) || playSeconds - at >= regrow;

    public void Worked(string id, double playSeconds) => PickedAt[id] = playSeconds;

    public void Picked(Plant p, double playSeconds)
    {
        PickedAt[p.Id] = playSeconds;
        // Forget plants long grown back, so the book doesn't grow forever.
        if (PickedAt.Count > 400)
            foreach (var old in PickedAt.Where(kv => playSeconds - kv.Value > Herbs.RegrowSeconds).Select(kv => kv.Key).ToList()) PickedAt.Remove(old);
    }
}

/// <summary>An ore vein out in the wild, on the crags and the cold heights: richer than the quarry's, and further to walk.</summary>
public sealed record Outcrop(string Id, string VeinId, double X, double Z)
{
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

/// <summary>
/// Where ore breaks the surface out in the wilds, worked out from the seed like the herbs: iron, coal, silver and
/// sometimes gold, up on the crags and in the cold north. Gold is only found out here, never at the quarry.
/// </summary>
public static class Outcrops
{
    public const double Cell = 110;

    public static IEnumerable<Outcrop> Near(WorldGen world, double x, double z, double radius)
    {
        var (c0, c1) = ((long)Math.Floor((x - radius) / Cell), (long)Math.Floor((x + radius) / Cell));
        var (r0, r1) = ((long)Math.Floor((z - radius) / Cell), (long)Math.Floor((z + radius) / Cell));
        for (var cx = c0; cx <= c1; cx++)
            for (var cz = r0; cz <= r1; cz++)
                foreach (var o in In(world, cx, cz))
                    if (o.DistanceTo(x, z) <= radius) yield return o;
    }

    public static IEnumerable<Outcrop> In(WorldGen world, long cx, long cz)
    {
        var rng = Noise.Dice(world.Seed, 91, cx, cz);
        if (!rng.Chance(0.45)) yield break;
        var x = (cx + 0.2 + rng.NextDouble() * 0.6) * Cell;
        var z = (cz + 0.2 + rng.NextDouble() * 0.6) * Cell;
        var h = world.Height(x, z);
        if (h < WorldGen.WaterLevel + 1) yield break;
        if (world.BiomeAt(x, z, h) is not (Biome.Crags or Biome.Tundra or Biome.Snowfield) && h < 25) yield break;
        if (Math.Sqrt(x * x + z * z) < 200) yield break;
        if (world.SettlementsNear(x, z, 80).Any(s => s.DistanceTo(x, z) < s.Radius + 30)) yield break;
        var vein = rng.PickWeighted(new (string, double)[] { ("iron", 3), ("coal", 2), ("silver", 2), ("gold", 1) });
        var count = 2 + rng.Next(3);
        for (var i = 0; i < count; i++)
        {
            var (px, pz) = (x + (rng.NextDouble() * 2 - 1) * 6, z + (rng.NextDouble() * 2 - 1) * 6);
            if (world.Height(px, pz) < WorldGen.WaterLevel + 0.6) continue;
            yield return new Outcrop($"vein_{cx}_{cz}_{i}", vein, px, pz);
        }
    }
}
