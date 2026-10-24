namespace Tristinwold.Nemesis;

/// <summary>Matches FFX's BtlResultId: LOSE=1, WIN=2, PLY_ESCAPE=3, MON_ESCAPE=4.</summary>
public enum BattleResult { Defeat = 1, Victory = 2, PartyEscaped = 3, EnemiesEscaped = 4 }

/// <summary>One monster in the battle, read from its Chr before the battle unloads.</summary>
public sealed record EnemyReport(
    int Slot,
    int MonsterId,
    string Species,
    int MaxHp,
    bool Killed,
    bool Escaped,
    Guid? NemesisId = null,
    DamageKind? KillingBlow = null,
    string? KilledBy = null);

/// <summary>What the game layer hands the nemesis world after every battle.</summary>
public sealed record BattleReport(
    string AreaId,
    BattleResult Result,
    IReadOnlyList<EnemyReport> Enemies,
    IReadOnlyList<string> Players,
    int PartyLevel = 1);

/// <summary>A monster slot as the battle is being set up, before stats are applied.</summary>
public sealed record FormationSlot(int Slot, int MonsterId, string Species);

/// <summary>Everything the game layer needs to turn an ordinary monster into a nemesis.</summary>
public sealed record BattleModifiers(
    string DisplayName,
    IReadOnlyDictionary<Stat, double> StatMultipliers,
    IReadOnlyDictionary<DamageKind, Affinity> Affinities,
    IReadOnlyList<AutoStatus> AutoStatuses)
{
    /// <summary>Applies a multiplier to a stat, clamped to what FFX can store.</summary>
    public int Apply(Stat stat, int baseValue)
    {
        var mult = StatMultipliers.TryGetValue(stat, out var m) ? m : 1.0;
        var max = stat switch { Stat.Hp => 99_999_999, Stat.Mp => 9_999_999, _ => 255 };
        return (int)Math.Clamp(Math.Round(baseValue * mult), 1, max);
    }
}

public sealed record NemesisSpawn(int Slot, Guid NemesisId, BattleModifiers Modifiers, string? Taunt);

public enum WorldEventKind
{
    Born, Promoted, Demoted, Slain, CheatedDeath, Escaped, Triumph, Taunted,
    FeudStarted, FeudWon, FeudLost, BloodOath, Avenger, Succession,
}

/// <summary>Something worth telling the players about ("Grask Gutter-Fang is now a Warchief").</summary>
public sealed record WorldEvent(WorldEventKind Kind, Guid NemesisId, string Text, Guid? OtherId = null);
