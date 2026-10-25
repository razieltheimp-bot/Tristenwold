using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.World;

public enum SagaStage { NotStarted, SeekEgg, CarryEgg, Raising, Knighted }

/// <summary>
/// The road to becoming a Dragon Knight, open to every class. Ser Aldric of the Order of the Eyrie, at the rookery
/// outside Tristinwold, sends you to the nest of the nearest metallic ancient that keeps a pact with people. Take
/// an egg (Rift-corrupted drakes prey on the clutch), keep it warm until it hatches (resting by a fire is quickest),
/// then raise the hatchling: feed it, keep it alive (a dragon too young to fly that falls is lost, and you must
/// begin again), and fight beside it until it reaches level 10. Bring it back to Ser Aldric to be knighted; from
/// then on it carries you, on the ground and in the air.
/// </summary>
public sealed class DragonSaga
{
    /// <summary>The Order of the Eyrie's rookery, on the hill west of Tristinwold's walls.</summary>
    public const double RookeryX = -150, RookeryZ = -165;

    public const string Knight = "Ser Aldric Brasswing";

    /// <summary>Seconds an egg takes to hatch while carried; resting makes it six times quicker.</summary>
    public const double HatchSeconds = 300;

    public SagaStage Stage { get; set; }
    public string? MatriarchId { get; set; }
    public string? MatriarchName { get; set; }
    public DragonColour Colour { get; set; } = DragonColour.Gold;
    public double NestX { get; set; }
    public double NestZ { get; set; }
    public double Warmth { get; set; }

    /// <summary>How many hatchlings have been lost. Ser Aldric remembers.</summary>
    public int Lost { get; set; }

    /// <summary>The metallic ancient in pact with people nearest a point (searching further out until one is found).</summary>
    public static Ancient? Matriarch(WorldGen world, double x = 0, double z = 0)
    {
        foreach (var radius in new[] { 9000.0, 16000, 30000 })
            if (Dragons.Near(world, x, z, radius).Where(a => a.Allied).MinBy(a => a.DistanceTo(x, z)) is { } a) return a;
        return null;
    }

    /// <summary>Ser Aldric sends you for an egg.</summary>
    public bool Begin(WorldGen world)
    {
        if (Stage is not (SagaStage.NotStarted or SagaStage.SeekEgg)) return false;
        if (Matriarch(world) is not { } m) return false;
        (MatriarchId, MatriarchName, Colour) = (m.Id, m.FullName, m.Colour);
        (NestX, NestZ) = (m.X - 30, m.Z - 20);
        Warmth = 0;
        Stage = SagaStage.SeekEgg;
        return true;
    }

    public bool TakeEgg()
    {
        if (Stage != SagaStage.SeekEgg) return false;
        Stage = SagaStage.CarryEgg;
        return true;
    }

    /// <summary>Time passes with the egg carried; returns true the moment it hatches.</summary>
    public bool Warm(double seconds, bool resting)
    {
        if (Stage != SagaStage.CarryEgg) return false;
        Warmth = Math.Min(1, Warmth + seconds / HatchSeconds * (resting ? 6 : 1));
        if (Warmth < 1) return false;
        Stage = SagaStage.Raising;
        return true;
    }

    /// <summary>The hatchling: a dragon of the matriarch's colour, with a name of the old tongue.</summary>
    public Bond Hatch(Rng rng) => new()
    {
        KindId = Dragons.KindFor(Colour), Role = BondRole.Dragon,
        Name = Dragons.NameFor(rng), Fed = 70,
    };

    /// <summary>The hatchling died: Ser Aldric will find you another egg.</summary>
    public void Lose()
    {
        if (Stage != SagaStage.Raising) return;
        Lost++;
        Stage = SagaStage.SeekEgg;
        Warmth = 0;
    }

    public bool ReadyToKnight(Bond? dragon) => Stage == SagaStage.Raising && dragon is { Role: BondRole.Dragon, Level: >= 10 };

    public bool Knighted(Bond? dragon)
    {
        if (!ReadyToKnight(dragon)) return false;
        Stage = SagaStage.Knighted;
        return true;
    }

    /// <summary>What to do next, for the quest tracker.</summary>
    public string? Objective(Bond? dragon) => Stage switch
    {
        SagaStage.SeekEgg => $"Take an egg from the nest of {MatriarchName}",
        SagaStage.CarryEgg => $"Keep the dragon egg warm until it hatches ({Warmth:P0}). Resting (T) by a fire is quickest",
        SagaStage.Raising when dragon is null => "Find your dragon",
        SagaStage.Raising when dragon.Level < 10 => $"Raise {dragon.Name} to level 10 (level {dragon.Level}) and keep {dragon.Name} fed",
        SagaStage.Raising => $"Bring {dragon.Name} to {Knight} at the rookery",
        _ => null,
    };

    /// <summary>Where the next step is.</summary>
    public (double X, double Z)? Goal(Bond? dragon) => Stage switch
    {
        SagaStage.SeekEgg => (NestX, NestZ),
        SagaStage.Raising when dragon is { Level: >= 10 } => (RookeryX, RookeryZ),
        _ => null,
    };
}
