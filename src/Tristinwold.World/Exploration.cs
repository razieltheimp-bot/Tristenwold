using System.Text.Json;
using System.Text.Json.Serialization;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>
/// Fog of war: which parts of the world a company has seen. The world is split into 16 m
/// cells kept in 1 km tiles, so walking anywhere for as long as you like stays cheap.
/// </summary>
public sealed class ExploredMap
{
    public const double Cell = 16;
    public const int TileCells = 64;

    readonly Dictionary<(int, int), ulong[]> _tiles = [];

    public HashSet<string> Discovered { get; } = [];
    public HashSet<string> Rumored { get; } = [];
    public int Count { get; private set; }

    public IEnumerable<(int Tx, int Tz)> Tiles => _tiles.Keys;

    public static (int Cx, int Cz) CellOf(double x, double z) => ((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell));

    /// <summary>Uncovers everything within <paramref name="radius"/> and returns the cells that were new.</summary>
    public List<(int Cx, int Cz)> Reveal(double x, double z, double radius)
    {
        var fresh = new List<(int, int)>();
        var (c0x, c0z) = CellOf(x - radius, z - radius);
        var (c1x, c1z) = CellOf(x + radius, z + radius);
        for (var cx = c0x; cx <= c1x; cx++)
            for (var cz = c0z; cz <= c1z; cz++)
            {
                var (mx, mz) = ((cx + 0.5) * Cell - x, (cz + 0.5) * Cell - z);
                if (mx * mx + mz * mz > radius * radius) continue;
                if (Set(cx, cz)) fresh.Add((cx, cz));
            }
        return fresh;
    }

    public bool IsRevealed(double x, double z)
    {
        var (cx, cz) = CellOf(x, z);
        return IsRevealedCell(cx, cz);
    }

    public bool IsRevealedCell(int cx, int cz)
    {
        var (tx, tz, bx, bz) = Split(cx, cz);
        return _tiles.TryGetValue((tx, tz), out var rows) && (rows[bz] & (1UL << bx)) != 0;
    }

    bool Set(int cx, int cz)
    {
        var (tx, tz, bx, bz) = Split(cx, cz);
        if (!_tiles.TryGetValue((tx, tz), out var rows)) _tiles[(tx, tz)] = rows = new ulong[TileCells];
        var bit = 1UL << bx;
        if ((rows[bz] & bit) != 0) return false;
        rows[bz] |= bit;
        Count++;
        return true;
    }

    static (int Tx, int Tz, int Bx, int Bz) Split(int cx, int cz)
    {
        var tx = (int)Math.Floor(cx / (double)TileCells);
        var tz = (int)Math.Floor(cz / (double)TileCells);
        return (tx, tz, cx - tx * TileCells, cz - tz * TileCells);
    }

    // ---- saving ------------------------------------------------------------------------------

    public Dictionary<string, string> TilesToSave() =>
        _tiles.ToDictionary(kv => $"{kv.Key.Item1},{kv.Key.Item2}", kv =>
        {
            var bytes = new byte[TileCells * 8];
            Buffer.BlockCopy(kv.Value, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        });

    public void LoadTiles(Dictionary<string, string> tiles)
    {
        _tiles.Clear();
        Count = 0;
        foreach (var (key, value) in tiles)
        {
            var parts = key.Split(',');
            var rows = new ulong[TileCells];
            Buffer.BlockCopy(Convert.FromBase64String(value), 0, rows, 0, TileCells * 8);
            _tiles[(int.Parse(parts[0]), int.Parse(parts[1]))] = rows;
            Count += rows.Sum(r => System.Numerics.BitOperations.PopCount(r));
        }
    }
}

/// <summary>A place in the world (metres) and the way the hero faced (radians about the vertical).</summary>
public sealed record Spot(float X, float Y, float Z, float Facing);

/// <summary>
/// Everything a company carries between sessions: its map, its quests, its purse, its hero's class and levels,
/// its companions, the meat in its packs, and how far along the road to Dragon Knight it is.
/// </summary>
public sealed class CompanySave
{
    public int Version { get; set; } = 1;
    public ulong Seed { get; set; }
    public int Crowns { get; set; }
    public Dictionary<string, string> Explored { get; set; } = [];
    public List<string> Discovered { get; set; } = [];
    public List<string> Rumored { get; set; } = [];
    public List<QuestEntry> Quests { get; set; } = [];
    public Hero? Hero { get; set; }
    public List<Bond> Companions { get; set; } = [];
    public DragonSaga? Saga { get; set; }
    /// <summary>How far along the main story: what the hero is, and the road from waking to warden of the Rings.</summary>
    public ImmortalSaga? Story { get; set; }
    /// <summary>Trade beyond the walls: the markets of the places the hero knows, and their place at the caravan yard.</summary>
    public TradeBook? Trade { get; set; }
    /// <summary>The barbarian raids reckoned with, and the clans the hero has broken.</summary>
    public RaidRecord? Warbands { get; set; }
    /// <summary>Life at sea and in the shipyard: rank aboard, voyages, the hero's own ship and its hold.</summary>
    public SeaBook? Sea { get; set; }
    /// <summary>Dagna Flint's work at the Greyhollow mine, and how far it's got.</summary>
    public MineBook? Mine { get; set; }
    /// <summary>The hero's part in the Northwood.</summary>
    public ForestBook? Forest { get; set; }
    /// <summary>The hero's name in the world: honour, infamy, bounty, and their careers in the Watch and in a clan.</summary>
    public Standing? Standing { get; set; }
    public int Meat { get; set; }
    public GuildBook? Guilds { get; set; }
    public RaidBook? Raids { get; set; }
    /// <summary>What the hero carries and wears, and their trades.</summary>
    public Pack? Pack { get; set; }
    public HerbBook? Herbs { get; set; }
    /// <summary>Tristinwold's economy as this company has known it: its stock, prices, contracts, works and what it thinks of them.</summary>
    public CityEconomy? City { get; set; }
    /// <summary>Gardens, house, snares, treasure hunts and feats.</summary>
    public PursuitBook? Pursuits { get; set; }
    /// <summary>Where the hero stood, so the next session starts there.</summary>
    public Spot? Where { get; set; }

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static CompanySave Capture(ulong seed, int crowns, ExploredMap map, QuestLog log,
        Hero? hero = null, IEnumerable<Bond>? companions = null, DragonSaga? saga = null, int meat = 0, GuildBook? guilds = null, RaidBook? raids = null,
        Pack? pack = null, HerbBook? herbs = null, Spot? where = null, CityEconomy? city = null, PursuitBook? pursuits = null, ImmortalSaga? story = null, TradeBook? trade = null, RaidRecord? warbands = null, SeaBook? sea = null, Standing? standing = null, MineBook? mine = null, ForestBook? forest = null) => new()
    {
        Forest = forest,
        Standing = standing,
        Sea = sea,
        Mine = mine,
        Warbands = warbands,
        Trade = trade,
        Story = story,
        City = city,
        Pursuits = pursuits,
        Where = where,
        Pack = pack,
        Herbs = herbs,
        Raids = raids,
        Guilds = guilds,
        Hero = hero,
        Companions = companions?.ToList() ?? [],
        Saga = saga,
        Meat = meat,
        Seed = seed,
        Crowns = crowns,
        Explored = map.TilesToSave(),
        Discovered = [.. map.Discovered],
        Rumored = [.. map.Rumored],
        Quests = log.Entries,
    };

    public void Restore(ExploredMap map, QuestLog log)
    {
        map.LoadTiles(Explored);
        map.Discovered.Clear();
        map.Discovered.UnionWith(Discovered);
        map.Rumored.Clear();
        map.Rumored.UnionWith(Rumored);
        log.Entries.Clear();
        log.Entries.AddRange(Quests);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static CompanySave FromJson(string json) => JsonSerializer.Deserialize<CompanySave>(Tristinwold.Deviant.SaveUpgrade.Upgrade(json), Options) ?? new CompanySave();
}
