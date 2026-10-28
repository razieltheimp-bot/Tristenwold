using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

public enum SagaStage { NotStarted, SeekEgg, CarryEgg, Raising, Knighted, Proving }

/// <summary>
/// The road to becoming a Dragon Knight, open to every class, and earned. Nobody comes to tell you of it: the Order of
/// the Eyrie keeps its rookery on the hill west of Tristinwold, and its riders come down there every day, for anyone
/// who cares to go and look. Ser Aldric doesn't hand out eggs. First he wants to see you can stand (level 5) and that
/// you'll put in the work: feed his dragons, with meat you've hunted yourself. Then he tells you, roughly, where the
/// nearest metallic ancient that keeps a pact with people has her lair, and you go and find it yourself. Take
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

    /// <summary>What Ser Aldric asks before he'll say where an egg is to be had: a hero who can stand, and meat for his dragons.</summary>
    public const int MinLevel = 5, MeatDue = 10;
    public int MeatGiven { get; set; }
    /// <summary>The hero has found the matriarch's lair for themselves.</summary>
    public bool FoundNest { get; set; }
    /// <summary>All Ser Aldric will say of where it is: "far to the north-east".</summary>
    public string Hint { get; set; } = "";

    /// <summary>Meat for the rookery's dragons, while proving yourself. Returns how much was taken.</summary>
    public int Feed(int meat)
    {
        if (Stage != SagaStage.Proving) return 0;
        var took = Math.Clamp(MeatDue - MeatGiven, 0, meat);
        MeatGiven += took;
        return took;
    }

    /// <summary>Whether the hero has earned it: on their feet at level 5, and the dragons fed.</summary>
    public bool Proven(int heroLevel) => Stage == SagaStage.Proving && heroLevel >= MinLevel && MeatGiven >= MeatDue;

    /// <summary>Ser Aldric is satisfied: he says, roughly, where the nest is. Finding it is up to the hero.</summary>
    public bool Bless(int heroLevel)
    {
        if (!Proven(heroLevel)) return false;
        Stage = SagaStage.SeekEgg;
        var (dx, dz) = (NestX - RookeryX, NestZ - RookeryZ);
        string[] names = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];
        var bearing = (Math.Atan2(dx, -dz) * 180 / Math.PI + 360 + 22.5) % 360;
        Hint = $"{(Math.Sqrt(dx * dx + dz * dz) > 6000 ? "far " : "")}to the {names[(int)(bearing / 45)]}";
        return true;
    }

    /// <summary>The hero has come upon the lair for themselves.</summary>
    public void Found()
    {
        if (Stage == SagaStage.SeekEgg) FoundNest = true;
    }

    /// <summary>The metallic ancient in pact with people nearest a point (searching further out until one is found).</summary>
    public static Ancient? Matriarch(WorldGen world, double x = 0, double z = 0)
    {
        foreach (var radius in new[] { 9000.0, 16000, 30000 })
            if (Dragons.Near(world, x, z, radius).Where(a => a.Allied).MinBy(a => a.DistanceTo(x, z)) is { } a) return a;
        return null;
    }

    /// <summary>Ser Aldric will consider you, if you prove yourself.</summary>
    public bool Begin(WorldGen world)
    {
        if (Stage is not SagaStage.NotStarted) return false;
        if (Matriarch(world) is not { } m) return false;
        (MatriarchId, MatriarchName, Colour) = (m.Id, m.FullName, m.Colour);
        (NestX, NestZ) = (m.X - 30, m.Z - 20);
        Warmth = 0;
        Stage = SagaStage.Proving;
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
    public string? Objective(Bond? dragon, int heroLevel = MinLevel) => Stage switch
    {
        SagaStage.Proving => $"Prove yourself to {Knight}: " + string.Join(", ", new[]
        {
            heroLevel < MinLevel ? $"reach level {MinLevel} (level {heroLevel})" : null,
            MeatGiven < MeatDue ? $"bring meat for the rookery's dragons ({MeatGiven}/{MeatDue})" : null,
        }.OfType<string>().DefaultIfEmpty($"go back to {Knight} at the rookery")),
        SagaStage.SeekEgg when !FoundNest => $"Find the lair of {MatriarchName} for yourself, somewhere {Hint} of the rookery",
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
        SagaStage.SeekEgg when FoundNest => (NestX, NestZ),
        SagaStage.Proving => (RookeryX, RookeryZ),
        SagaStage.Raising when dragon is { Level: >= 10 } => (RookeryX, RookeryZ),
        _ => null,
    };
}
