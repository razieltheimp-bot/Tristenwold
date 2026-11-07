using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// A herd, pack or lone beast living out in the wilds: what it is, where it ranges, and how many.
/// Ids are stable, so a pack you thinned is the same pack when you come back.
/// </summary>
/// <remarks>
/// Dragons also carry their size, the ancient they follow (<paramref name="Faction"/>), the ancient they're at
/// war with (<paramref name="Foe"/>), and for an ancient, its name. A dragon herd may be <paramref name="Allied"/>
/// with people (a pact ancient's kin), and carry dragon knights (<paramref name="Riders"/>).
/// <paramref name="Young"/> younglings go with the grown ones: find one alone and it can be tamed. A dire imp has a
/// band of lesser imps at its heels (<paramref name="Band"/>, <paramref name="BandCount"/> of them).
/// </remarks>
public sealed record Herd(string Id, string KindId, double X, double Z, int Count, double Roam,
    DragonSize Size = DragonSize.Normal, string? Faction = null, string? Foe = null, string? Name = null,
    bool Allied = false, bool Riders = false, int Young = 0, string? Band = null, int BandCount = 0)
{
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

/// <summary>
/// Life between the settlements, worked out from the seed like everything else, so every company
/// in the shared world meets the same wolves in the same woods. The land is cut into cells; most
/// hold something. Deer and foxes graze everywhere and run from you; the further from Tristinwold
/// (the higher the danger), the more of the land belongs to wolves, boars, goblins, bears and trolls;
/// and in an ancient dragon's territory, to its dragons (see <see cref="Dragons"/>). Near home
/// only the harmless and the merely annoying (giant rats, mud crabs) are about.
/// <para>
/// Stranger things too, further out: the kipine, a winged owlbear with antlers, alone in the woods; packs of
/// deathcrest apes in the marsh and the crags, who eat the dead that walk; dire imps with their bands of lesser
/// imps; and winged apes, swords on their backs, up among the rocks.
/// </para>
/// </summary>
public static class Wildlife
{
    /// <summary>
    /// What the stranger creatures do besides bite and claw.
    /// <list type="bullet">
    /// <item><see cref="Dive"/>: up into the air, then down on you from above, hard enough to knock you flat.</item>
    /// <item><see cref="Jaunt"/>: a blink through shadow to your flank, and a poisoned bite.</item>
    /// <item><see cref="Vanish"/>: gone, invisible; then it strikes from beside you.</item>
    /// <item><see cref="Throw"/>: rocks hurled at anyone who keeps their distance.</item>
    /// <item><see cref="Steal"/>: snatches coin from you; kill it and you get it back.</item>
    /// <item><see cref="Shift"/>: badly hurt, it turns into a raven and flies off.</item>
    /// </list>
    /// </summary>
    public enum Trick { Dive, Jaunt, Vanish, Throw, Steal, Shift }

    public static IReadOnlyList<Trick> TricksOf(string kind) => kind switch
    {
        "kipine" => [Trick.Dive, Trick.Steal],
        "deathcrest_ape" => [Trick.Jaunt],
        "dire_imp" => [Trick.Vanish],
        "imp" => [Trick.Shift],
        "winged_ape" => [Trick.Throw, Trick.Dive],
        "griffon" => [Trick.Dive],
        _ => [],
    };

    /// <summary>Deathcrest apes eat the dead that walk: they go after any undead they see.</summary>
    public static bool HuntsUndead(string kind) => kind == "deathcrest_ape";

    public const double Cell = 150;

    /// <summary>Nothing that hunts people lives this close to Tristinwold.</summary>
    public const double SafeRadius = 450;

    /// <summary>What lives in the sea and won't leave it.</summary>
    public static bool Aquatic(string kind) => kind is "sea_serpent" or "leviathan";

    /// <summary>Animals that run rather than fight.</summary>
    public static readonly IReadOnlySet<string> Timid = new HashSet<string> { "deer", "fox", "wild_horse", "cow", "sheep", "pig", "hen", "horse", "dog", "cat",
        "arctic_fox", "mountain_goat", "hare", "sky_fox", "jackalope", "pegasus" };

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
        if (h < WorldGen.WaterLevel - 4)
        {
            // Open water: now and then a sea serpent, and far out a leviathan; never in sight of Tristinwold's harbour.
            var far = WorldGen.Danger(x, z);
            if (Math.Sqrt(x * x + z * z) < SafeRadius * 1.6 || !rng.Chance(Math.Min(0.25, 0.06 * far))) return null;
            var deep = far > 3 && rng.Chance(0.18) ? "leviathan" : "sea_serpent";
            return new Herd($"wild_{cx}_{cz}", deep, x, z, deep == "sea_serpent" && rng.Chance(0.3) ? 2 : 1, 40);
        }
        if (h < WorldGen.WaterLevel + 0.6) return null;
        // Settlements keep their own: no wild animals wander through a town or a bandit camp.
        if (world.SettlementsNear(x, z, 60).Any(s => s.DistanceTo(x, z) < s.Radius + (s.IsCity ? 90 : 40))) return null;

        // In an ancient dragon's land, some stretches belong to its kin rather than to beasts.
        if (Dragons.Flight(world, cx, cz, x, z) is { } flight) return flight;

        var biome = world.BiomeAt(x, z, h);
        var danger = WorldGen.Danger(x, z);
        var safe = Math.Sqrt(x * x + z * z) < SafeRadius;
        var near = world.Height(x + 30, z) < WorldGen.WaterLevel || world.Height(x - 30, z) < WorldGen.WaterLevel
            || world.Height(x, z + 30) < WorldGen.WaterLevel || world.Height(x, z - 30) < WorldGen.WaterLevel;
        var d = safe ? 0 : danger;
        (string, double)[] choices = biome switch
        {
            Biome.AutumnForest => [("deer", 4), ("fox", 2), ("wild_horse", 1.2), ("wolf", 0.8 * d), ("bear", 0.3 * d), ("spriggan", 0.25 * d), ("boar", 0.7 * d), ("goblin", 0.4 * d), ("kipine", 0.18 * d), ("imp", 0.12 * d),
                ("hare", 1.6), ("aurochs", 0.5), ("feral_hound", 0.35 * d), ("jackalope", 0.25), ("sky_fox", 0.3), ("griffon", 0.06 * d), ("ember_wyrm", 0.05 * d)],
            Biome.PineForest => [("deer", 3.5), ("fox", 1.5), ("wolf", 1.1 * d), ("bear", 0.6 * d), ("boar", 0.5 * d), ("goblin", 0.4 * d), ("kipine", 0.25 * d), ("deathcrest_ape", 0.15 * d), ("winged_ape", 0.12 * d),
                ("hare", 1), ("black_bear", 0.5 * d), ("tiger", 0.15 * d), ("sky_fox", 0.25), ("jackalope", 0.15)],
            Biome.Marsh => [("goblin", 0.3 * d), ("giant_rat", 3), ("mud_crab", near ? 3 : 0.5), ("deer", 1), ("wolf", 0.5 * d), ("deathcrest_ape", 0.35 * d), ("dire_imp", 0.2 * d),
                ("crocodile", near ? 1.4 * d : 0.15 * d), ("feral_hound", 0.3 * d), ("hare", 0.5)],
            Biome.Tundra => [("deer", 2), ("fox", 1), ("wild_horse", 1.5), ("wolf", 2 * d), ("ice_wolf", 1 * d), ("bear", 0.5 * d), ("winged_ape", 0.2 * d),
                ("arctic_fox", 0.8), ("aurochs", 1), ("hare", 0.8), ("pegasus", 0.3), ("white_bear", 0.2 * d)],
            Biome.Snowfield => [("ice_wolf", 1.4 * Math.Max(d, 0.1)), ("frost_troll", 0.5 * d), ("deer", 0.8), ("fox", 0.8),
                ("white_bear", 0.6 * Math.Max(d, 0.1)), ("arctic_fox", 1.2), ("tiger", 0.25 * d)],
            _ => [("bear", 1.5 * Math.Max(d, 0.1)), ("frost_troll", 1 * d), ("wolf", 1.5 * d), ("deer", 0.6), ("goblin", 0.6 * d), ("deathcrest_ape", 0.4 * d), ("dire_imp", 0.3 * d), ("winged_ape", 0.4 * d),
                ("mountain_goat", 2), ("crag_lion", 0.6 * d), ("griffon", 0.25 * d), ("pegasus", 0.2), ("ember_wyrm", 0.2 * d)],
        };
        if (near && biome != Biome.Snowfield) choices = [.. choices, ("mud_crab", 1.5)];
        choices = [.. choices.Where(c => c.Item2 > 0)];
        if (choices.Length == 0) return null;
        var kind = rng.PickWeighted(choices);
        var extra = (int)Math.Min(2, danger - 1.5); // packs grow out in the far wilds
        var count = kind switch
        {
            "deer" => 3 + rng.Next(3),
            "wild_horse" => 3 + rng.Next(4),
            "fox" => 1 + rng.Next(2),
            "wolf" => 2 + rng.Next(2) + extra,
            "ice_wolf" => 2 + rng.Next(2) + extra,
            "giant_rat" => 3 + rng.Next(3),
            "mud_crab" => 2 + rng.Next(3),
            "spriggan" => 1 + rng.Next(2),
            "boar" => 1 + rng.Next(3),
            "goblin" => 2 + rng.Next(3) + extra,
            "deathcrest_ape" => 3 + rng.Next(2) + extra, // they hunt in packs
            "imp" => 3 + rng.Next(3),
            "winged_ape" => 2 + rng.Next(2),
            "hare" => 2 + rng.Next(3),
            "arctic_fox" or "sky_fox" or "jackalope" => 1 + rng.Next(2),
            "mountain_goat" => 2 + rng.Next(3),
            "aurochs" => 3 + rng.Next(3),
            "pegasus" => 2 + rng.Next(3),
            "feral_hound" => 3 + rng.Next(2) + extra,
            "crocodile" or "crag_lion" => 1 + rng.Next(2),
            _ => 1, // bears, trolls, kipines and dire imps keep to themselves (a dire imp has its band)
        };
        var roam = kind switch { "hare" => 14, "aurochs" => 26, "mountain_goat" => 18, "pegasus" => 34, "sky_fox" => 22, "griffon" => 30, "feral_hound" => 16, "crocodile" => 8, "deer" => 22, "wild_horse" => 30, "fox" => 18, "wolf" or "ice_wolf" or "deathcrest_ape" => 16, "kipine" => 24, "mud_crab" => 6, _ => 10 };
        // About a third of herds have young with them; a lone bear or troll sometimes has a cub. Fiends don't.
        var fiend = kind is "imp" or "dire_imp";
        var young = !fiend && rng.Chance(count > 1 ? 0.4 : 0.25) ? 1 + (count > 3 && rng.Chance(0.3) ? 1 : 0) : 0;
        // A dire imp never goes anywhere without a band of lesser imps to order about.
        var (band, banded) = kind == "dire_imp" ? ("imp", 2 + rng.Next(3)) : ((string?)null, 0);
        return new Herd($"wild_{cx}_{cz}", kind, x, z, count, roam, Young: young, Band: band, BandCount: banded);
    }
}

/// <summary>
/// The animals people keep: cows, sheep, pigs and hens at a farmstead, with a horse and a dog; a few of each in a
/// hamlet or a town, and cats about the houses; horses at an inn; dogs at a hunting camp. Tristinwold has its dogs, its
/// cats, its hens in the yards and the Watch's horses. They stand by the people who keep them.
/// </summary>
public static class Livestock
{
    public static IReadOnlyList<(string Kind, int Count)> Of(Settlement s, Rng rng) => s.Kind switch
    {
        SettlementKind.Farmstead => [("cow", 2 + rng.Next(2)), ("sheep", 3 + rng.Next(3)), ("pig", 1 + rng.Next(2)), ("hen", 3 + rng.Next(3)), ("horse", rng.Next(2)), ("dog", 1)],
        SettlementKind.Hamlet => [("sheep", 2 + rng.Next(3)), ("hen", 2 + rng.Next(3)), ("pig", rng.Next(2)), ("dog", 1), ("cat", 1 + rng.Next(2))],
        SettlementKind.Town => [("hen", 2 + rng.Next(3)), ("dog", 1 + rng.Next(2)), ("cat", 2 + rng.Next(2)), ("horse", 1 + rng.Next(2))],
        SettlementKind.City => s.Id == "city:tristinwold"
            ? [("dog", 3), ("cat", 4), ("hen", 3), ("horse", 2)]
            : [("dog", 2), ("cat", 3), ("hen", 2), ("horse", 2)],
        SettlementKind.Inn => [("horse", 2 + rng.Next(2)), ("dog", 1), ("hen", 2)],
        SettlementKind.HuntingCamp => [("dog", 2)],
        SettlementKind.Mine => [("dog", 1)],
        _ => [],
    };

    /// <summary>What it costs to kill someone's animal where they can see it.</summary>
    public static int Fine(string kind) => kind switch { "cow" or "horse" => 40, "sheep" or "pig" => 20, "dog" or "cat" => 15, _ => 5 };
}