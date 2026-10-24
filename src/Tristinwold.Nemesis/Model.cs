using System.Text.Json.Serialization;

namespace Tristinwold.Nemesis;

/// <summary>Place in the hierarchy. Higher ranks hit harder and are harder to kill for good.</summary>
public enum Rank { Grunt = 0, Captain = 1, Warchief = 2, Overlord = 3 }

/// <summary>What an enemy remembers about the party, across sessions.</summary>
[Flags]
public enum Memory
{
    None = 0,
    MetPlayer = 1 << 0,
    KilledPlayer = 1 << 1,     // wiped the party
    PlayerFled = 1 << 2,       // the party ran from it
    FledFromPlayer = 1 << 3,   // it escaped the party
    CheatedDeath = 1 << 4,     // "killed", came back scarred
    Humiliated = 1 << 5,       // lost a feud to another nemesis
    Avenger = 1 << 6,          // swore revenge for a slain blood-brother
}

/// <summary>Damage families that leave scars and teach resistances.</summary>
public enum DamageKind { Physical, Fire, Ice, Lightning, Water, Holy, Gravity, Poison, Unknown }

public enum Stat { Hp, Mp, Strength, Defense, Magic, MagicDefense, Agility, Luck, Evasion, Accuracy }

public enum Affinity { Weak, Normal, Resist, Immune, Absorb }

/// <summary>Auto-statuses the game layer can grant at battle start.</summary>
public enum AutoStatus { Haste, Protect, Shell, Regen, Reflect, NulTide, NulBlaze, NulShock, NulFrost }

public enum TraitKind { Strength, Weakness }

/// <summary>
/// A strength or weakness. Effects are deliberately plain data so the FFX layer
/// can apply them without knowing which trait they came from.
/// </summary>
public sealed record Trait(
    string Id,
    TraitKind Kind,
    string Label,
    string? Epithet = null,
    Dictionary<Stat, double>? StatMultipliers = null,
    Dictionary<DamageKind, Affinity>? Affinities = null,
    AutoStatus[]? AutoStatuses = null);

public sealed record Scar(DamageKind Cause, string Description, int Day);

/// <summary>One line in a nemesis's life story, shown in the roster UI.</summary>
public sealed record HistoryEntry(int Day, string Text);

public sealed class PlayerRecord
{
    public Memory Memory { get; set; }
    public int TimesKilledThem { get; set; }
    public int TimesTheyFled { get; set; }
    public int TimesTheyBeatMe { get; set; }
}

public sealed class Nemesis
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Epithet { get; set; }

    /// <summary>Game monster id (FFX m### / battle monster index).</summary>
    public required int MonsterId { get; init; }
    public required string Species { get; init; }

    public Rank Rank { get; set; } = Rank.Captain;
    public int Level { get; set; } = 1;
    /// <summary>Renown. Earned by beating players and other nemeses; drives promotion.</summary>
    public int Power { get; set; }
    public bool Alive { get; set; } = true;
    public string? HomeArea { get; set; }

    public List<Trait> Traits { get; init; } = [];
    public List<Scar> Scars { get; init; } = [];
    public Memory Memory { get; set; }
    /// <summary>Per-player memory, keyed by player id, so a nemesis can single out one friend.</summary>
    public Dictionary<string, PlayerRecord> Players { get; init; } = [];
    public HashSet<Guid> Rivals { get; init; } = [];
    public Guid? BloodBrother { get; set; }
    public Guid? ServesUnder { get; set; }
    public List<HistoryEntry> History { get; init; } = [];

    public int Encounters { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    [JsonIgnore]
    public string FullName => Epithet is null ? Name : $"{Name} {Epithet}";

    public PlayerRecord Player(string playerId)
    {
        if (!Players.TryGetValue(playerId, out var record))
            Players[playerId] = record = new PlayerRecord();
        return record;
    }
}
