using Tristinwold.Nemesis;

namespace Tristinwold.World;

/// <summary>The dragon colours: five chromatic (cruel, they hunt people) and five metallic (proud, they leave you be unless you strike first).</summary>
public enum DragonColour { Red, Blue, Green, Black, White, Gold, Silver, Bronze, Copper, Brass }

/// <summary>How big a dragon has grown. A nemesis dragon that keeps winning grows a size at each promotion.</summary>
public enum DragonSize { Normal = 0, Large = 1, Giant = 2, Ancient = 3 }

/// <summary>
/// An ancient dragon: its name, its lair, and the land it rules (where smaller dragons of its colour follow it).
/// Some have been twisted by the Rift (<paramref name="Corrupted"/>), and they and all their kin with them. Some
/// metallic ancients keep a pact with the people of a town in their land (<paramref name="Ward"/>): their dragons
/// and the dragon knights who ride them guard it against chromatic and corrupted dragons.
/// </summary>
public sealed record Ancient(string Id, string Name, string Title, DragonColour Colour, double X, double Z, double Territory,
    bool Corrupted = false, Settlement? Ward = null)
{
    public bool Metallic => Dragons.IsMetallic(Colour);
    public string FullName => $"{Name} {Title}";
    public string What => $"an ancient {(Corrupted ? "corrupted " : "")}{Colour.ToString().ToLowerInvariant()} dragon";
    public string KindId => Corrupted ? Dragons.CorruptedKindFor(Colour) : Dragons.KindFor(Colour);
    public bool Allied => Ward is not null;
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

/// <summary>Two ancients at war this season, and where their dragons meet: the border between their lands.</summary>
public sealed record DragonWar(Ancient A, Ancient B, double FrontX, double FrontZ, long Season, bool AncientsFight)
{
    public string Id => $"war_{Season}_{A.Id}_{B.Id}";
    public string Story => $"{A.FullName} and {B.FullName} are at war. Their dragons clash where their lands meet.";
}

/// <summary>
/// The dragons of the world. Ancient dragons hold great territories out in the wilds, one or none in each
/// large stretch of land, never near Tristinwold. Smaller dragons of the same colour roam each territory,
/// and giants guard every ancient's lair. Every few hours (by the real clock, so every company in the shared
/// world sees the same thing) neighbouring ancients may go to war: chromatic against metallic most often,
/// sometimes rivals of a kind, and their dragons fight at the border like warring tribes, sometimes with the
/// ancients themselves at their head. Everything here comes from the seed and the clock.
/// </summary>
public static class Dragons
{
    public const double Cell = 6000;
    public const double NearestToHome = 4000;
    public const double SeasonHours = 3;
    public const double WarReach = 10000;

    public static bool IsMetallic(DragonColour c) => c >= DragonColour.Gold;

    public static string KindFor(DragonColour c) => "dragon_" + c.ToString().ToLowerInvariant();

    public static DragonColour? ColourOf(string kindId) =>
        kindId.StartsWith("dragon_") && Enum.TryParse<DragonColour>(kindId[7..].Replace("_corrupt", ""), true, out var c) ? c : null;

    /// <summary>A dragon twisted by the Rift (its kind ends in "_corrupt").</summary>
    public static bool IsCorrupted(string kindId) => kindId.StartsWith("dragon_") && kindId.EndsWith("_corrupt");

    public static string CorruptedKindFor(DragonColour c) => KindFor(c) + "_corrupt";

    /// <summary>Health, damage and height for each size: an ancient is a mountain with teeth.</summary>
    public static (double Health, double Damage, float Height) Scale(DragonSize s) => s switch
    {
        DragonSize.Large => (2.5, 1.6, 7f),
        DragonSize.Giant => (6, 2.4, 10.5f),
        DragonSize.Ancient => (16, 3.5, 16f),
        _ => (1, 1, 4.5f),
    };

    /// <summary>The ancient whose lair is in this cell, if there is one.</summary>
    public static Ancient? In(WorldGen world, long cx, long cz)
    {
        var rng = Noise.Dice(world.Seed, 61, cx, cz);
        if (!rng.Chance(0.75)) return null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var x = (cx + 0.2 + rng.NextDouble() * 0.6) * Cell;
            var z = (cz + 0.2 + rng.NextDouble() * 0.6) * Cell;
            if (Math.Sqrt(x * x + z * z) < NearestToHome) continue;
            var h = world.Height(x, z);
            if (h < WorldGen.WaterLevel + 2) continue;
            if (world.SettlementsNear(x, z, 300).Any(s => s.DistanceTo(x, z) < s.Radius + 300)) continue;
            var colour = rng.PickWeighted(Habitat(world.BiomeAt(x, z, h)));
            var name = Name(rng);
            var title = rng.Pick(Titles[colour]);
            var territory = 1800 + rng.NextDouble() * 900;
            var corrupted = rng.Chance(0.15);
            if (corrupted) title = rng.Pick(Corruption);
            // A metallic ancient may keep a pact with the nearest town or village in its land.
            var ward = !corrupted && IsMetallic(colour) && rng.Chance(0.55)
                ? world.SettlementsNear(x, z, territory).Where(s => s.Kind is SettlementKind.City or SettlementKind.Town or SettlementKind.Hamlet && s.People > 0)
                    .MinBy(s => s.DistanceTo(x, z))
                : null;
            return new Ancient($"ancient_{cx}_{cz}", name, title, colour, x, z, territory, corrupted, ward);
        }
        return null;
    }

    static (DragonColour, double)[] Habitat(Biome b) => b switch
    {
        Biome.Snowfield => [(DragonColour.White, 5), (DragonColour.Silver, 4), (DragonColour.Blue, 1)],
        Biome.Crags => [(DragonColour.Red, 4), (DragonColour.Blue, 3), (DragonColour.Copper, 3), (DragonColour.Gold, 1), (DragonColour.Silver, 1)],
        Biome.Tundra => [(DragonColour.Blue, 3), (DragonColour.Brass, 3), (DragonColour.Red, 2), (DragonColour.Bronze, 2), (DragonColour.White, 1)],
        Biome.Marsh => [(DragonColour.Black, 5), (DragonColour.Green, 2), (DragonColour.Bronze, 2), (DragonColour.Copper, 1)],
        Biome.PineForest => [(DragonColour.Green, 4), (DragonColour.Bronze, 2), (DragonColour.Gold, 2), (DragonColour.Black, 1), (DragonColour.Red, 1)],
        _ => [(DragonColour.Green, 3), (DragonColour.Gold, 3), (DragonColour.Copper, 2), (DragonColour.Brass, 2), (DragonColour.Red, 1)],
    };

    static readonly string[] First = ["Vael", "Kor", "Ithra", "Mor", "Sael", "Thra", "Ulo", "Zar", "Ves", "Gor", "Ny", "Aur", "Bael", "Cyr", "Dra", "Esk", "Fyr", "Hal", "Ora", "Rhy"];
    static readonly string[] Middle = ["", "", "a", "e", "i", "o", "ra", "ve", "th", "ma"];
    static readonly string[] Last = ["thrax", "gorn", "mir", "vyx", "dras", "thys", "kaar", "nox", "vaun", "zeth", "ryn", "loth", "maw", "garde", "sith", "quor"];

    static string Name(Rng rng) => rng.Pick(First) + rng.Pick(Middle) + rng.Pick(Last);

    /// <summary>A dragon's name, for a hatchling you raise.</summary>
    public static string NameFor(Rng rng) => Name(rng);

    static readonly string[] Corruption = ["the Rift-Touched", "the Hollow Crown", "Voidmaw", "the Blighted", "the Unmade"];

    static readonly string[] Knights =
    [
        "Ser Aldra Vane", "Ser Corwin Hale", "Dame Isolde Brightmere", "Ser Tamsin Rook", "Dame Wren Ashby", "Ser Oric Stormhollow",
        "Dame Maelis Corde", "Ser Bastian Fell", "Dame Sabine Thorne", "Ser Edric Gale", "Dame Rhosyn Vale", "Ser Kael Marrow",
    ];

    /// <summary>A dragon knight's name.</summary>
    public static string KnightName(long seed) => Knights[(int)(Math.Abs(seed) % Knights.Length)];
    static readonly Dictionary<DragonColour, string[]> Titles = new()
    {
        [DragonColour.Red] = ["the Ember Tyrant", "Ashmaw", "the Burning Crown", "Kingsbane"],
        [DragonColour.Blue] = ["the Storm Throne", "Thunderhide", "the Lightning in the Peaks"],
        [DragonColour.Green] = ["the Poison Whisper", "Mossfang", "the Rot Beneath the Leaves"],
        [DragonColour.Black] = ["the Mire Queen", "Acidtongue", "the Drowned Dark"],
        [DragonColour.White] = ["the Frost Wyrm", "Rimeclaw", "the Endless Winter"],
        [DragonColour.Gold] = ["the Sun Sovereign", "Goldheart", "the Just Flame"],
        [DragonColour.Silver] = ["the Moon in the Snow", "Silverwing", "the Quiet Guardian"],
        [DragonColour.Bronze] = ["the Tidewarden", "Stormscale", "the Shield of the Shore"],
        [DragonColour.Copper] = ["the Laughing Flame", "Riddlefang", "the Canyon Trickster"],
        [DragonColour.Brass] = ["the Desert Talker", "Sandsong", "the Bright Wind"],
    };

    public static IEnumerable<Ancient> Near(WorldGen world, double x, double z, double radius)
    {
        var (c0, c1) = ((long)Math.Floor((x - radius) / Cell), (long)Math.Floor((x + radius) / Cell));
        var (r0, r1) = ((long)Math.Floor((z - radius) / Cell), (long)Math.Floor((z + radius) / Cell));
        for (var cx = c0; cx <= c1; cx++)
            for (var cz = r0; cz <= r1; cz++)
                if (In(world, cx, cz) is { } a && a.DistanceTo(x, z) <= radius) yield return a;
    }

    /// <summary>The ancient whose land this is (the nearest, where territories meet), if any.</summary>
    public static Ancient? TerritoryAt(WorldGen world, double x, double z) =>
        Near(world, x, z, 3000).Where(a => a.DistanceTo(x, z) < a.Territory).MinBy(a => a.DistanceTo(x, z) / a.Territory);

    /// <summary>The season (a few hours of real time) a moment falls in. Wars last a season.</summary>
    public static long SeasonOf(DateTime utc) => (long)Math.Floor((utc - DateTime.UnixEpoch).TotalHours / SeasonHours);

    /// <summary>The wars being fought this season between ancients near a point.</summary>
    public static IEnumerable<DragonWar> WarsNear(WorldGen world, DateTime utc, double x, double z, double radius)
    {
        var season = SeasonOf(utc);
        var ancients = Near(world, x, z, radius + WarReach).ToList();
        foreach (var a in ancients)
            foreach (var b in ancients)
            {
                if (string.CompareOrdinal(a.Id, b.Id) >= 0 || a.DistanceTo(b.X, b.Z) > WarReach) continue;
                var rng = Noise.Dice(world.Seed, 67, WorldGen.StableHash(a.Id + b.Id), season);
                // The corrupted war on everything; otherwise old enmity, or a feud between kin.
                var odds = a.Corrupted || b.Corrupted ? 0.5 : a.Metallic != b.Metallic ? 0.35 : 0.12;
                if (!rng.Chance(odds)) continue;
                var t = a.Territory / (a.Territory + b.Territory);
                var (fx, fz) = (a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                if (Math.Sqrt((fx - x) * (fx - x) + (fz - z) * (fz - z)) > radius) continue;
                yield return new DragonWar(a, b, fx, fz, season, rng.Chance(0.4));
            }
    }

    /// <summary>A dragon flight roaming a territory: sometimes a cell in an ancient's land holds its kin instead of beasts.</summary>
    public static Herd? Flight(WorldGen world, long cx, long cz, double x, double z)
    {
        if (TerritoryAt(world, x, z) is not { } lord) return null;
        var rng = Noise.Dice(world.Seed, 63, cx, cz);
        var closeness = 1 - lord.DistanceTo(x, z) / lord.Territory; // thicker near the lair
        if (!rng.Chance(0.15 + 0.35 * closeness)) return null;
        var large = rng.Chance(0.25 + 0.3 * closeness);
        var count = large ? 1 : 1 + rng.Next(2);
        var young = rng.Chance(0.15) ? 1 : 0; // a wyrmling, now and then
        // Some of a lord's kin have fallen to the Rift: they hunt everything, their own colour included.
        if (!lord.Corrupted && rng.Chance(0.08))
            return new Herd($"flight_{cx}_{cz}", CorruptedKindFor(lord.Colour), x, z, count, 30, large ? DragonSize.Large : DragonSize.Normal, Rift);
        return new Herd($"flight_{cx}_{cz}", lord.KindId, x, z, count, 30,
            Size: large ? DragonSize.Large : DragonSize.Normal, Faction: lord.Id, Allied: lord.Allied, Young: young);
    }

    /// <summary>The faction of corrupted dragons that follow no ancient.</summary>
    public const string Rift = "rift";

    /// <summary>Who keeps an ancient's lair: the ancient itself and two giants of its colour.</summary>
    public static IEnumerable<Herd> Lair(Ancient a)
    {
        yield return new Herd($"lair_{a.Id}", a.KindId, a.X, a.Z, 1, 40, DragonSize.Ancient, a.Id, Name: a.FullName, Allied: a.Allied);
        yield return new Herd($"lair_{a.Id}_guard", a.KindId, a.X + 45, a.Z + 30, 2, 50, DragonSize.Giant, a.Id, Allied: a.Allied);
    }

    /// <summary>
    /// The Dragonwatch over a pact ancient's town: dragon knights on its dragons, circling the town, with a
    /// larger dragon and its knight standing guard.
    /// </summary>
    public static IEnumerable<Herd> Watch(Ancient a)
    {
        if (a.Ward is not { } town) yield break;
        yield return new Herd($"watch_{a.Id}", a.KindId, town.X, town.Z, 2, Math.Max(60, town.Radius), DragonSize.Normal, a.Id, Allied: true, Riders: true);
        yield return new Herd($"watch_{a.Id}_captain", a.KindId, town.X + town.Radius * 0.6, town.Z, 1, 20, DragonSize.Large, a.Id, Allied: true, Riders: true);
    }

    /// <summary>The ancients near a point that keep a pact with people.</summary>
    public static IEnumerable<Ancient> PactsNear(WorldGen world, double x, double z, double radius) =>
        Near(world, x, z, radius + 3000).Where(a => a.Ward is { } w && w.DistanceTo(x, z) <= radius);

    /// <summary>
    /// Raids on the pact towns this season: a band of chromatic or corrupted dragons comes for the town, and its
    /// dragon knights meet them. The raiders are the kin of the nearest hostile ancient, or Rift-corrupted drakes.
    /// </summary>
    public static IEnumerable<Herd> RaidsNear(WorldGen world, DateTime utc, double x, double z, double radius)
    {
        var season = SeasonOf(utc);
        foreach (var a in PactsNear(world, x, z, radius))
        {
            var town = a.Ward!;
            var rng = Noise.Dice(world.Seed, 71, WorldGen.StableHash(a.Id), season);
            if (!rng.Chance(0.4)) continue;
            var foe = Near(world, a.X, a.Z, WarReach).Where(b => b.Corrupted || !b.Metallic).MinBy(b => b.DistanceTo(a.X, a.Z));
            var (kind, faction) = foe is not null && rng.Chance(0.7) ? (foe.KindId, foe.Id) : (CorruptedKindFor(rng.Pick(Enum.GetValues<DragonColour>())), Rift);
            var angle = rng.NextDouble() * Math.Tau;
            var (rx, rz) = (town.X + Math.Cos(angle) * (town.Radius + 120), town.Z + Math.Sin(angle) * (town.Radius + 120));
            yield return new Herd($"raid_{season}_{a.Id}", kind, rx, rz, 3, 30, DragonSize.Normal, faction, a.Id);
            yield return new Herd($"raid_{season}_{a.Id}_large", kind, rx + 25, rz, 1, 30, DragonSize.Large, faction, a.Id);
        }
    }

    /// <summary>The two warbands at a war's front: each ancient's smaller dragons, and sometimes the ancients.</summary>
    public static IEnumerable<Herd> Warbands(DragonWar w)
    {
        var (dx, dz) = (w.B.X - w.A.X, w.B.Z - w.A.Z);
        var len = Math.Sqrt(dx * dx + dz * dz);
        (dx, dz) = (dx / len, dz / len);
        foreach (var (side, foe, sign) in new[] { (w.A, w.B, -1.0), (w.B, w.A, 1.0) })
        {
            var (bx, bz) = (w.FrontX + dx * 30 * sign, w.FrontZ + dz * 30 * sign);
            yield return new Herd($"{w.Id}_{side.Id}_drakes", side.KindId, bx, bz, 4, 25, DragonSize.Normal, side.Id, foe.Id);
            yield return new Herd($"{w.Id}_{side.Id}_large", side.KindId, bx + dx * 25 * sign, bz + dz * 25 * sign, 2, 25, DragonSize.Large, side.Id, foe.Id);
            if (w.AncientsFight)
                yield return new Herd($"{w.Id}_{side.Id}_ancient", side.KindId, bx + dx * 60 * sign, bz + dz * 60 * sign, 1, 40, DragonSize.Ancient, side.Id, foe.Id, side.FullName);
        }
    }
}
