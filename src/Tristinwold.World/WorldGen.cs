using System.Collections.Concurrent;
using Tristinwold.Deviant;
using Tristinwold.Shared;

namespace Tristinwold.World;

public enum Biome { AutumnForest, PineForest, Marsh, Tundra, Snowfield, Crags }

public enum SettlementKind { City, Town, Hamlet, Farmstead, HuntingCamp, Inn, Shrine, Mine, AshbornCamp, BanditCamp, Barrow, Den }

/// <summary>Who holds a hostile site: some of one kind, and maybe a boss.</summary>
public sealed record SiteLair(string KindId, int Count, string? BossKindId = null)
{
    public int Total => Count + (BossKindId is null ? 0 : 1);
}

/// <summary>A city, a village, a camp or a lair, anywhere in the world.</summary>
public sealed record Settlement(
    string Id,
    SettlementKind Kind,
    string Name,
    string Epithet,
    string Story,
    double X,
    double Z,
    double Radius,
    double Floor,
    Biome Biome,
    IReadOnlyList<string> Places,
    SiteLair? Lair,
    int People,
    bool Authored = false,
    bool StartsKnown = false)
{
    public bool Hostile => Lair is not null;
    public bool IsCity => Kind is SettlementKind.City or SettlementKind.Town;
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));

    /// <summary>"a hamlet", "a bandit camp": what the map and the chronicle call it.</summary>
    public string What => Kind switch
    {
        SettlementKind.City => "a city",
        SettlementKind.Town => "a town",
        SettlementKind.Hamlet => "a hamlet",
        SettlementKind.Farmstead => "a farmstead",
        SettlementKind.HuntingCamp => "a hunting camp",
        SettlementKind.Inn => "a roadside inn",
        SettlementKind.Shrine => "a shrine",
        SettlementKind.Mine => "a mine",
        SettlementKind.AshbornCamp => "an Ashborn refuge",
        SettlementKind.BanditCamp => "a bandit camp",
        SettlementKind.Barrow => "a barrow",
        _ => "a beast's den",
    };
}

/// <summary>
/// The whole world, grown from one seed. Nothing is stored and there is no edge: ask for any
/// point and the land, the weather band, the villages and the cities there are worked out on the
/// spot, the same for every company. Tristinwold sits at the origin, the eight other reworked
/// cities from world.json around it, and beyond them the generator keeps making new cities.
/// North is negative z, as in Godot.
/// </summary>
public sealed class WorldGen
{
    public const double WaterLevel = -0.6;
    public const double CityCell = 2400;
    public const double SiteCell = 320;

    /// <summary>The cities a company has heard of on day one: Tristinwold and its four neighbours.</summary>
    public const double StartingRadius = 3200;

    public ulong Seed { get; }
    public Atlas Atlas { get; }
    public IReadOnlyList<Settlement> AuthoredCities { get; }
    public Settlement Tristinwold { get; }

    readonly ConcurrentDictionary<(long, long), Settlement?> _cities = new();
    readonly ConcurrentDictionary<(long, long), Settlement?> _sites = new();
    readonly ConcurrentDictionary<string, IReadOnlyList<Npc>> _people = new(), _youths = new();
    readonly ConcurrentDictionary<string, Quest?> _quests = new();

    static readonly Dictionary<string, double> CitySizes = new()
    {
        ["tristinwold"] = 110, ["galeholt"] = 75, ["vespergate"] = 80, ["cinderhelm"] = 75, ["argentdeep"] = 70,
        ["gravenmoor"] = 50, ["mirewatch"] = 50, ["duskwake"] = 55, ["hollowreach"] = 55,
    };

    public WorldGen(Atlas? atlas = null, ulong? seed = null)
    {
        Atlas = atlas ?? Atlas.Default;
        Seed = seed ?? (Atlas.Seed != 0 ? Atlas.Seed : 1777);
        AuthoredCities = Atlas.Cities.Select(MakeAuthored).ToList();
        Tristinwold = AuthoredCities.First(c => c.Id == "city:tristinwold");
    }

    static WorldGen? _default;
    public static WorldGen Default => _default ??= new WorldGen();

    // ---- land --------------------------------------------------------------------------------

    /// <summary>Ground height before villages and cities level their patch of it.</summary>
    public double RawHeight(double x, double z)
    {
        var d = Math.Sqrt(x * x + z * z);
        var wild = Noise.SmoothStep(250, 2200, d); // the Tristinwold lowlands are gentle
        var hills = Noise.Fbm(Seed ^ 0x11, x / 650, z / 650, 5);
        var mask = Noise.Fbm(Seed ^ 0x22, x / 2800, z / 2800, 3) + Noise.SmoothStep(-1500, -6000, z) * 0.35; // more mountains north
        var mountains = Noise.SmoothStep(0.1, 0.55, mask);
        var ridge = 1 - Math.Abs(Noise.Fbm(Seed ^ 0x33, x / 900, z / 900, 4));
        var h = 7 + hills * (14 + 26 * wild) + mountains * wild * ridge * ridge * 150;

        var lake = Noise.Fbm(Seed ^ 0x44, x / 1100, z / 1100, 3);
        if (lake < -0.3) h -= (-0.3 - lake) * 90 * (0.3 + 0.7 * wild);

        // Tristinwold stands on flat ground at height 0, its harbor to the east.
        h = Noise.Lerp(h, 0, 1 - Noise.SmoothStep(130, 420, d));
        var harbor = Sq((x - 260) / 165) + Sq(z / 230);
        return Noise.Lerp(h, -4, 1 - Noise.SmoothStep(0.7, 1.0, harbor));
    }

    /// <summary>Ground height, with every settlement's ground levelled.</summary>
    public double Height(double x, double z) => Level(x, z, RawHeight(x, z), Affecting(x, z, 0));

    /// <summary>
    /// Heights for a square patch, (n x n) samples <paramref name="step"/> apart starting at
    /// (x0, z0), row by row. Much faster than asking point by point.
    /// </summary>
    public float[] HeightField(double x0, double z0, int n, double step)
    {
        var size = (n - 1) * step;
        var near = Affecting(x0 + size / 2, z0 + size / 2, size * 0.75);
        var heights = new float[n * n];
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
            {
                var (x, z) = (x0 + c * step, z0 + r * step);
                heights[r * n + c] = (float)Level(x, z, RawHeight(x, z), near);
            }
        return heights;
    }

    static double Level(double x, double z, double h, IEnumerable<Settlement> near)
    {
        foreach (var s in near)
        {
            if (s.Id == "city:tristinwold") continue; // built into the raw land
            var band = s.IsCity ? 110 : 45;
            var w = 1 - Noise.SmoothStep(s.Radius, s.Radius + band, s.DistanceTo(x, z));
            if (w > 0) h = Noise.Lerp(h, s.Floor, w);
        }
        return h;
    }

    List<Settlement> Affecting(double x, double z, double extra) =>
        SettlementsNear(x, z, extra + 120).Where(s => s.DistanceTo(x, z) < s.Radius + (s.IsCity ? 110 : 45) + extra).ToList();

    public Biome BiomeAt(double x, double z) => BiomeAt(x, z, Height(x, z));

    /// <summary>Colder going north and going up; marsh in wet lowland; autumn woods round Tristinwold.</summary>
    public Biome BiomeAt(double x, double z, double h)
    {
        var t = z / 4500 + Noise.Fbm(Seed ^ 0x55, x / 1800, z / 1800, 3) * 0.5 - Math.Max(0, h - 40) / 160;
        var m = Noise.Fbm(Seed ^ 0x66, x / 1400, z / 1400, 3);
        if (h > 80) return t < -0.45 ? Biome.Snowfield : Biome.Crags;
        if (t < -1.15) return Biome.Snowfield;
        if (t < -0.7) return Biome.Tundra;
        if (m > 0.28 && h < 16) return Biome.Marsh;
        return t > -0.22 ? Biome.AutumnForest : Biome.PineForest;
    }

    public static string BiomeName(Biome b) => b switch
    {
        Biome.AutumnForest => "autumn woods",
        Biome.PineForest => "pinewoods",
        Biome.Marsh => "marshes",
        Biome.Tundra => "tundra",
        Biome.Snowfield => "snowfields",
        _ => "crags",
    };

    /// <summary>How hard the wilds are here: 1 at Tristinwold, rising the further you go.</summary>
    public static double Danger(double x, double z) => 1 + Math.Sqrt(x * x + z * z) / 4000;

    // ---- where things are ---------------------------------------------------------------------

    public IEnumerable<Settlement> CitiesNear(double x, double z, double radius)
    {
        foreach (var c in AuthoredCities)
            if (c.DistanceTo(x, z) <= radius + c.Radius) yield return c;
        foreach (var c in Cells(x, z, radius + 800, CityCell, cities: true))
            if (c.DistanceTo(x, z) <= radius + c.Radius) yield return c;
    }

    public IEnumerable<Settlement> SitesNear(double x, double z, double radius)
    {
        foreach (var s in Cells(x, z, radius + 150, SiteCell, cities: false))
            if (s.DistanceTo(x, z) <= radius + s.Radius) yield return s;
    }

    public IEnumerable<Settlement> SettlementsNear(double x, double z, double radius) =>
        CitiesNear(x, z, radius).Concat(SitesNear(x, z, radius));

    /// <summary>The settlement whose ground this is, if any.</summary>
    public Settlement? SettlementAt(double x, double z) =>
        SettlementsNear(x, z, 0).Where(s => s.DistanceTo(x, z) <= s.Radius + 10).OrderBy(s => s.IsCity ? 1 : 0).FirstOrDefault();

    public Settlement NearestCity(double x, double z)
    {
        var best = AuthoredCities.MinBy(c => c.DistanceTo(x, z))!;
        foreach (var c in Cells(x, z, CityCell * 2.5, CityCell, cities: true))
            if (c.DistanceTo(x, z) < best.DistanceTo(x, z)) best = c;
        return best;
    }

    /// <summary>A name for wherever this is, for the screen and for the deviant records.</summary>
    public string AreaAt(double x, double z)
    {
        if (SettlementAt(x, z) is { } s) return s.Name;
        if (x * x + z * z < 700 * 700) return "The Tristinwold countryside";
        return $"The {BiomeName(BiomeAt(x, z))} near {NearestCity(x, z).Name}";
    }

    public Settlement? Find(string id)
    {
        var parts = id.Split(':');
        if (parts is ["city", _]) return AuthoredCities.FirstOrDefault(c => c.Id == id);
        if (parts.Length != 3 || !long.TryParse(parts[1], out var i) || !long.TryParse(parts[2], out var j)) return null;
        return parts[0] switch { "c" => CityAt(i, j), "s" => SiteAt(i, j), _ => null };
    }

    /// <summary>The people of a settlement: always the same people, for every company.</summary>
    public IReadOnlyList<Npc> PeopleOf(Settlement s) => _people.GetOrAdd(s.Id, _ => World.People.Generate(this, s));

    /// <summary>The young people of a settlement (not counted in <see cref="Settlement.People"/>).</summary>
    public IReadOnlyList<Npc> YouthsOf(Settlement s) => _youths.GetOrAdd(s.Id, _ => World.People.Youths(this, s));

    /// <summary>The work this person has to offer, if any.</summary>
    public Quest? QuestOf(Npc npc) => _quests.GetOrAdd(npc.Id, _ => Quests.For(this, npc));

    IEnumerable<Settlement> Cells(double x, double z, double radius, double cell, bool cities)
    {
        var (i0, i1) = ((long)Math.Floor((x - radius) / cell), (long)Math.Floor((x + radius) / cell));
        var (j0, j1) = ((long)Math.Floor((z - radius) / cell), (long)Math.Floor((z + radius) / cell));
        for (var i = i0; i <= i1; i++)
            for (var j = j0; j <= j1; j++)
                if ((cities ? CityAt(i, j) : SiteAt(i, j)) is { } s) yield return s;
    }

    // ---- cities ------------------------------------------------------------------------------

    Settlement MakeAuthored(City city)
    {
        var (x, z) = city.Position;
        var radius = CitySizes.GetValueOrDefault(city.Id, 60);
        var floor = city.Id == "tristinwold" ? 0 : Math.Max(RawHeight(x, z), WaterLevel + 2);
        var places = city.Places.Where(p => p != city.Name).Distinct().ToList();
        return new Settlement($"city:{city.Id}", radius >= 70 ? SettlementKind.City : SettlementKind.Town, city.Name, city.Epithet,
            city.Story, x, z, radius, floor, BiomeAt(x, z, floor), places, null, city.Id == "tristinwold" ? 10 : 8,
            Authored: true, StartsKnown: Math.Sqrt(x * x + z * z) <= StartingRadius);
    }

    Settlement? CityAt(long i, long j) => _cities.GetOrAdd((i, j), k => MakeCity(k.Item1, k.Item2));

    Settlement? MakeCity(long i, long j)
    {
        var rng = Noise.Dice(Seed, 1, i, j);
        if (!rng.Chance(0.72)) return null;
        var x = (i + 0.5) * CityCell + (rng.NextDouble() * 2 - 1) * 700;
        var z = (j + 0.5) * CityCell + (rng.NextDouble() * 2 - 1) * 700;
        if (Math.Sqrt(x * x + z * z) < StartingRadius + 300) return null; // the first five are the only cities near home
        if (AuthoredCities.Any(a => a.DistanceTo(x, z) < 2000)) return null;
        var raw = RawHeight(x, z);
        if (raw < WaterLevel + 1) return null;
        var town = rng.Chance(0.45);
        var name = Names.Place(rng);
        var floor = Math.Max(raw, WaterLevel + 2);
        var biome = BiomeAt(x, z, floor);
        var (epithet, story, places) = Lore.City(rng, name, town, biome);
        return new Settlement($"c:{i}:{j}", town ? SettlementKind.Town : SettlementKind.City, name, epithet, story,
            x, z, town ? 40 + rng.Next(15) : 60 + rng.Next(20), floor, biome, places, null, town ? 6 : 8);
    }

    // ---- villages, camps and lairs ------------------------------------------------------------

    Settlement? SiteAt(long i, long j) => _sites.GetOrAdd((i, j), k => MakeSite(k.Item1, k.Item2));

    Settlement? MakeSite(long i, long j)
    {
        var rng = Noise.Dice(Seed, 2, i, j);
        if (!rng.Chance(0.5)) return null;
        var x = (i + 0.5) * SiteCell + (rng.NextDouble() * 2 - 1) * 110;
        var z = (j + 0.5) * SiteCell + (rng.NextDouble() * 2 - 1) * 110;
        var fromHome = Math.Sqrt(x * x + z * z);
        if (fromHome < 480) return null; // Tristinwold's own lairs are close enough
        if (CitiesNear(x, z, 300).Any(c => c.DistanceTo(x, z) < c.Radius + 260)) return null;
        var raw = RawHeight(x, z);
        if (raw < WaterLevel + 0.8) return null;
        var slope = Math.Abs(RawHeight(x + 15, z) - raw) + Math.Abs(RawHeight(x, z + 15) - raw);
        if (slope > 14) return null; // nobody builds on a cliff
        var biome = BiomeAt(x, z, raw);
        var kind = PickKind(rng, biome, fromHome);
        return Lore.Site(rng, $"s:{i}:{j}", kind, biome, x, z, raw, Danger(x, z));
    }

    static SettlementKind PickKind(Rng rng, Biome biome, double fromHome)
    {
        var cold = biome is Biome.Tundra or Biome.Snowfield or Biome.Crags;
        var hostile = 0.5 + Math.Min(fromHome / 4000, 1.2); // the further out, the wilder
        return rng.PickWeighted<SettlementKind>(
        [
            (SettlementKind.Hamlet, cold ? 1.5 : 3),
            (SettlementKind.Farmstead, cold ? 0.5 : biome == Biome.Marsh ? 1 : 3),
            (SettlementKind.HuntingCamp, cold ? 2.5 : 2),
            (SettlementKind.Inn, 1),
            (SettlementKind.Shrine, 1),
            (SettlementKind.Mine, cold ? 2.5 : 1),
            (SettlementKind.AshbornCamp, 0.8),
            (SettlementKind.BanditCamp, 1.3 * hostile),
            (SettlementKind.Barrow, (cold ? 1.6 : 0.8) * hostile),
            (SettlementKind.Den, 1.4 * hostile),
        ]);
    }

    static double Sq(double v) => v * v;

    /// <summary>A stable 64-bit hash of a string (string.GetHashCode changes every run).</summary>
    public static long StableHash(string s)
    {
        var h = 14695981039346656037UL;
        foreach (var ch in s) h = (h ^ ch) * 1099511628211UL;
        return (long)h;
    }
}
