using System.Text.Json.Serialization;

namespace Tristinwold.Deviant;

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
    Humiliated = 1 << 5,       // lost a feud to another deviant
    Avenger = 1 << 6,          // swore revenge for a slain blood-brother
}

/// <summary>The damage families, which leave scars and teach resistances (Sun is holy light, the bane of the undead).</summary>
public enum DamageKind { Physical, Fire, Frost, Shock, Poison, Sun, Unknown }

/// <summary>
/// Multipliers the game layer applies to a fighter's numbers: Health/Mana/Stamina pools,
/// AttackDamageMult (Damage), DamageResist (Armor), MagicResist, SpeedMult (Speed), spell
/// magnitude (SpellPower) and CritChance.
/// </summary>
public enum Stat { Health, Mana, Stamina, Damage, Armor, MagicResist, Speed, SpellPower, CritChance }

public enum Affinity { Weak, Normal, Resist, Immune, Absorb }

/// <summary>Statuses a deviant starts each fight with.</summary>
public enum AutoStatus { Haste, Protect, Shell, Regen, Reflect }

public enum TraitKind { Strength, Weakness }

/// <summary>
/// A strength or weakness. Effects are deliberately plain data so the game layer
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

/// <summary>One line in a deviant's life story, shown in the roster UI.</summary>
public sealed record HistoryEntry(int Day, string Text);

public sealed class PlayerRecord
{
    public Memory Memory { get; set; }
    public int TimesKilledThem { get; set; }
    public int TimesTheyFled { get; set; }
    public int TimesTheyBeatMe { get; set; }
}

public sealed class Deviant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Epithet { get; set; }

    /// <summary>
    /// What kind of fighter this is, as the game layer identifies it (an actor base or
    /// leveled-list editor ID such as "LvlBanditMelee1H"). A deviant only takes the place of
    /// the same kind, or is spawned as one.
    /// </summary>
    public required string Kind { get; init; }
    /// <summary>What players see: "Bandit Chief", "Draugr Deathlord".</summary>
    public required string Species { get; init; }
    /// <summary>An actor base to spawn it from when it comes hunting (its kind in the bestiary, say "bandit").</summary>
    public string? BaseForm { get; set; }

    public Rank Rank { get; set; } = Rank.Captain;
    public int Level { get; set; } = 1;
    /// <summary>Renown. Earned by beating players and other deviants; drives promotion.</summary>
    public int Power { get; set; }
    public bool Alive { get; set; } = true;
    public string? HomeArea { get; set; }

    public List<Trait> Traits { get; init; } = [];
    public List<Scar> Scars { get; init; } = [];
    public Memory Memory { get; set; }
    /// <summary>Per-player memory, keyed by player id, so a deviant can single out one friend.</summary>
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
