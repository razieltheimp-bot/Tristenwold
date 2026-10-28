namespace Tristinwold.Deviant;

/// <summary>
/// How a fight ended. There's no battle screen, so the game layer decides: the player died
/// (Defeat), every enemy died or fled (Victory / EnemiesEscaped), or the player got away with
/// enemies still alive (PartyEscaped).
/// </summary>
public enum BattleResult { Defeat = 1, Victory = 2, PartyEscaped = 3, EnemiesEscaped = 4 }

/// <summary>One enemy from the fight. Slot is the game layer's local index for it.</summary>
public sealed record EnemyReport(
    int Slot,
    string Kind,
    string Species,
    int MaxHealth,
    bool Killed,
    bool Escaped,
    Guid? DeviantId = null,
    DamageKind? KillingBlow = null,
    string? KilledBy = null,
    /// <summary>The actor base the game can spawn this kind from (its kind in the bestiary, say "bandit").</summary>
    string? BaseForm = null,
    /// <summary>In a defeat, whether this is the one that put the player down: it struck them twice running, with no
    /// other between. Null when the game layer doesn't say, and then every enemy left standing shares the credit.</summary>
    bool? PutDown = null);

/// <summary>What the game layer hands the deviant world after every battle.</summary>
public sealed record BattleReport(
    string AreaId,
    BattleResult Result,
    IReadOnlyList<EnemyReport> Enemies,
    IReadOnlyList<string> Players,
    int PartyLevel = 1);

/// <summary>An enemy about to fight the player, before any deviant takes its place.</summary>
public sealed record FormationSlot(int Slot, string Kind, string Species, string? BaseForm = null);

/// <summary>Everything the game layer needs to turn an ordinary enemy into a deviant.</summary>
/// <param name="Rank">The deviant's place in the hierarchy, so the game can show it (a dragon grows a size with each rank).</param>
/// <param name="Victories">How many times it has beaten a party (a dragon grows a size each time, too).</param>
public sealed record BattleModifiers(
    string DisplayName,
    IReadOnlyDictionary<Stat, double> StatMultipliers,
    IReadOnlyDictionary<DamageKind, Affinity> Affinities,
    IReadOnlyList<AutoStatus> AutoStatuses,
    Rank Rank = Rank.Stray,
    int Victories = 0)
{
    /// <summary>Applies a multiplier to an actor value, kept inside what the game plays well with.</summary>
    public double Apply(Stat stat, double baseValue)
    {
        var value = baseValue * (StatMultipliers.TryGetValue(stat, out var m) ? m : 1.0);
        return stat switch
        {
            Stat.Health or Stat.Mana or Stat.Stamina => Math.Clamp(Math.Round(value), 1, 100_000),
            Stat.Speed => Math.Clamp(value, 50, 250),          // SpeedMult: 100 is normal
            Stat.Armor => Math.Clamp(value, 0, 1000),          // armor rating; 567 is the vanilla cap
            Stat.MagicResist => Math.Clamp(value, -100, 85),   // percent; 85 is the vanilla cap
            Stat.CritChance => Math.Clamp(value, 0, 100),
            _ => Math.Max(0, value),
        };
    }
}

public sealed record DeviantSpawn(int Slot, Guid DeviantId, BattleModifiers Modifiers, string? Taunt);

public enum WorldEventKind
{
    Born, Promoted, Demoted, Slain, Returned, Escaped, Triumph, Taunted,
    GrudgeStarted, GrudgeWon, GrudgeLost, Pact, Vengeful, Succession,
}

/// <summary>Something worth telling the players about ("Grask Gutter-Fang is now a Scourge").</summary>
public sealed record WorldEvent(WorldEventKind Kind, Guid DeviantId, string Text, Guid? OtherId = null);
