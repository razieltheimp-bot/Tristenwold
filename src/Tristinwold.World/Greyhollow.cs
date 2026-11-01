namespace Tristinwold.World;

/// <summary>How far the hero has got in Dagna Flint's work: hired, sent to the old face, the breach, the fight won, paid.</summary>
public enum MineStage { None, Hired, AtTheFace, Breached, Held, Done }

/// <summary>A chamber of the mine: where it is (metres from the adit), how wide, and how far down its floor lies.</summary>
public sealed record MineRoom(string Id, string Name, double X, double Z, double Radius, double Floor, bool Deep = false);

/// <summary>A sloping drift between two chambers.</summary>
public sealed record MineTunnel(string From, string To, bool Deep = false);

/// <summary>A vein in a chamber's wall: what it is, and where round the wall (radians).</summary>
public sealed record MineSeam(string Id, string Kind, string Room, double Angle);

/// <summary>What lives in a chamber, and how hard it hits.</summary>
public sealed record MineDen(string Room, string Kind, int Count, double Danger);

/// <summary>
/// The Greyhollow mine, laid out in metres from the adit: chambers joined by drifts, each deeper than the last. The
/// upper workings are open to anyone. Past the old face lies the deep, which nobody has seen since it was walled up;
/// Dagna Flint's work (see <see cref="MineBook"/>) is the only way through.
/// </summary>
public static class Greyhollow
{
    /// <summary>How far a drift reaches either side of its line.</summary>
    public const double HalfWidth = 2.4;

    /// <summary>The quarry's middle, and the mine's doorway at the end of the cutting into the mound behind it.</summary>
    public const double QuarryX = -112, QuarryZ = 100, DoorZ = QuarryZ - 16.1;

    const double HillZ = QuarryZ - 20, HillWide = 22, HillDeep = 18, HillHigh = 15, CutFloor = 3.4, CutFlare = 3.6;

    static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// How far the ground rises at a point of the world for the mound over the mine: a great heap of rock behind the
    /// quarry, with a level cutting driven through its face to the doorway, as if the miners dug their way in.
    /// </summary>
    public static double Hill(double x, double z)
    {
        var round = Math.Sqrt(Sq((x - QuarryX) / HillWide) + Sq((z - HillZ) / HillDeep));
        if (round >= 1) return 0;
        var heap = 1 - round;
        var rise = HillHigh * Ease(heap) + 1.3 * Math.Sin(x * 0.31) * Math.Sin(z * 0.27) * Ease(heap * 2.5);
        var across = 1 - Ease((Math.Abs(x - QuarryX) - CutFloor) / CutFlare);
        var along = 1 - Ease((DoorZ - 5.6 - z) / 1.6); // on through the doorway's timber sets, and it stops dead, with the mound over it
        return rise * (1 - across * along);
    }

    public static readonly IReadOnlyList<MineRoom> Rooms =
    [
        new("adit", "The Adit", 0, 0, 9, 0),
        new("timbering", "The Timbered Gallery", 0, -34, 11, -3),
        new("iron_stope", "The Iron Stope", -34, -50, 13, -7),
        new("coal_gallery", "The Coal Gallery", 30, -56, 12, -9),
        new("silver_cut", "The Silver Cut", 4, -92, 11, -14),
        new("old_face", "The Old Face", 4, -120, 9, -18),
        new("breach", "The Breach", 4, -150, 13, -22, Deep: true),
        new("barrows", "The Sunken Barrows", -38, -172, 15, -27, Deep: true),
        new("cloister", "The Bone Cloister", 44, -182, 14, -30, Deep: true),
        new("gold_seam", "The Gold Seam", 4, -216, 14, -36, Deep: true),
    ];

    public static readonly IReadOnlyList<MineTunnel> Tunnels =
    [
        new("adit", "timbering"),
        new("timbering", "iron_stope"),
        new("timbering", "coal_gallery"),
        new("iron_stope", "silver_cut"),
        new("coal_gallery", "silver_cut"),
        new("silver_cut", "old_face"),
        new("old_face", "breach", Deep: true),
        new("breach", "barrows", Deep: true),
        new("breach", "cloister", Deep: true),
        new("barrows", "gold_seam", Deep: true),
        new("cloister", "gold_seam", Deep: true),
    ];

    /// <summary>The veins that were round the quarry floor keep their ids, so what's been worked stays worked.</summary>
    public static readonly IReadOnlyList<MineSeam> Seams =
    [
        new("quarry_0", "stone", "timbering", -2.4),
        new("quarry_1", "iron", "timbering", 0.2),
        new("quarry_2", "coal", "timbering", 2.9),
        new("quarry_3", "stone", "iron_stope", 2.2),
        new("quarry_4", "iron", "iron_stope", 3.6),
        new("quarry_7", "iron", "iron_stope", -1.5),
        new("quarry_5", "coal", "coal_gallery", -0.6),
        new("quarry_6", "stone", "coal_gallery", 0.7),
        new("quarry_9", "coal", "coal_gallery", 3.4),
        new("quarry_8", "silver", "silver_cut", 0.1),
        new("quarry_10", "stone", "silver_cut", 3.1),
        new("deep_iron", "iron", "barrows", 0.6),
        new("deep_silver_0", "silver", "barrows", 3.2),
        new("deep_silver_1", "silver", "cloister", 0.2),
        new("deep_gold_0", "gold", "gold_seam", -0.5),
        new("deep_gold_1", "gold", "gold_seam", 0.5),
        new("deep_gold_2", "gold", "gold_seam", 2.6),
        new("deep_gold_3", "gold", "gold_seam", 3.7),
    ];

    /// <summary>Rats in the lower workings, and past the breach the dead: thicker the further down.</summary>
    public static readonly IReadOnlyList<MineDen> Dens =
    [
        new("coal_gallery", "giant_rat", 2, 1),
        new("silver_cut", "giant_rat", 3, 1.2),
        new("breach", "cinder_thrall", 3, 1.2),
        new("barrows", "hollow_soldier", 3, 1.4),
        new("barrows", "draugr", 1, 1.3),
        new("cloister", "hollow_soldier", 2, 1.6),
        new("cloister", "draugr", 2, 1.5),
        new("gold_seam", "cinder_warden", 1, 1.4),
        new("gold_seam", "cinder_thrall", 3, 1.6),
    ];

    public static MineRoom Room(string id) => Rooms.First(r => r.Id == id);

    /// <summary>Where a seam sits, just in from its chamber's wall.</summary>
    public static (double X, double Z, double Floor) SeamAt(MineSeam s)
    {
        var r = Room(s.Room);
        var at = r.Radius - 1.8;
        return (r.X + Math.Cos(s.Angle) * at, r.Z + Math.Sin(s.Angle) * at, r.Floor);
    }

    /// <summary>Where the wall across the deep's drift stands: just past the Old Face's chamber.</summary>
    public static (double X, double Z, double Floor) Face
    {
        get
        {
            var (from, to) = (Room("old_face"), Room("breach"));
            var along = (from.Radius + 2) / Math.Sqrt(Sq(to.X - from.X) + Sq(to.Z - from.Z));
            return (from.X + (to.X - from.X) * along, from.Z + (to.Z - from.Z) * along, Floor(from.X + (to.X - from.X) * along, from.Z + (to.Z - from.Z) * along));
        }
    }

    static double Sq(double x) => x * x;

    /// <summary>The nearest part of the mine to a point: how far outside it the point is (negative inside), and the floor there.</summary>
    static (double Gap, double Floor, MineRoom? Room) Nearest(double x, double z)
    {
        var best = (Gap: double.MaxValue, Floor: 0.0, Room: (MineRoom?)null);
        foreach (var r in Rooms)
        {
            var gap = Math.Sqrt(Sq(x - r.X) + Sq(z - r.Z)) - r.Radius;
            if (gap < best.Gap) best = (gap, r.Floor, r);
        }
        if (best.Gap <= 0) return best; // inside a chamber, its floor is level: drifts arrive at its height
        foreach (var t in Tunnels)
        {
            var (a, b) = (Room(t.From), Room(t.To));
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var length = Math.Sqrt(dx * dx + dz * dz);
            var along = Math.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / length, 0, length);
            var gap = Math.Sqrt(Sq(x - (a.X + dx * along / length)) + Sq(z - (a.Z + dz * along / length))) - HalfWidth;
            if (gap >= best.Gap) continue;
            // The slope runs from one chamber's edge to the other's, so a drift meets each at its floor.
            var slope = Math.Clamp((along - a.Radius) / (length - a.Radius - b.Radius), 0, 1);
            best = (gap, a.Floor + (b.Floor - a.Floor) * slope, null);
        }
        return best;
    }

    /// <summary>Whether a point is in the mine rather than in the rock.</summary>
    public static bool Open(double x, double z) => Nearest(x, z).Gap <= 0;

    /// <summary>The floor under a point (the nearest floor, in the rock).</summary>
    public static double Floor(double x, double z) => Nearest(x, z).Floor;

    /// <summary>The height from floor to roof: a low drift, doming up over the middle of a chamber.</summary>
    public static double Headroom(double x, double z) => 3.4 + Math.Min(3.6, Math.Max(0, -Nearest(x, z).Gap) * 0.5);

    /// <summary>The name of the chamber a point is in or nearest to.</summary>
    public static string NameAt(double x, double z) =>
        Nearest(x, z).Room?.Name ?? Rooms.MinBy(r => Math.Sqrt(Sq(x - r.X) + Sq(z - r.Z)) - r.Radius)!.Name;

    /// <summary>The chamber a point is in, or just at the mouth of (a drift counts for the chamber it's nearest); null out in a drift's middle.</summary>
    public static MineRoom? RoomAt(double x, double z) =>
        Rooms.Select(r => (Room: r, Off: Math.Sqrt(Sq(x - r.X) + Sq(z - r.Z)) - r.Radius)).Where(t => t.Off < 6).OrderBy(t => t.Off).Select(t => t.Room).FirstOrDefault();
}

/// <summary>
/// Dagna Flint's work, saved with the company. She takes the hero on, wants four veins worked to see they can swing a
/// pick, then sends them to break the old face. That lets out what was walled up behind it: three waves of the dead,
/// with Tam Rusk pinned by the fall of rock and the hero the only thing between them. Hold, and the deep is open for
/// good. Lose him, and the fall has to be cleared and the face broken again.
/// </summary>
public sealed class MineBook
{
    public MineStage Stage { get; set; }
    /// <summary>The chambers the hero has stood in: what the mine's own map shows.</summary>
    public HashSet<string> Seen { get; set; } = [];
    /// <summary>The veins worked for Dagna since she took the hero on.</summary>
    public int Worked { get; set; }
    /// <summary>How many waves of the dead have been beaten at the breach.</summary>
    public int Waves { get; set; }
    /// <summary>How much Tam has left in him; while the fight lasts.</summary>
    public double Tam { get; set; } = TamHealth;
    public int Failed { get; set; }

    public const int VeinsToProve = 4, WaveCount = 3, Pay = 250;
    public const double TamHealth = 100;

    /// <summary>The wall's down: the fight, or the mine beyond it.</summary>
    public bool Broken => Stage >= MineStage.Breached;
    /// <summary>The dead are beaten, and the deep is there to walk.</summary>
    public bool DeepOpen => Stage >= MineStage.Held;

    public bool Hire()
    {
        if (Stage != MineStage.None) return false;
        Stage = MineStage.Hired;
        Worked = 0;
        return true;
    }

    /// <summary>A vein worked in the mine. True if that's the last Dagna wanted to see.</summary>
    public bool VeinWorked()
    {
        if (Stage != MineStage.Hired) return false;
        if (++Worked < VeinsToProve) return false;
        Stage = MineStage.AtTheFace;
        return true;
    }

    /// <summary>The face comes down, and the fight starts.</summary>
    public bool Breach()
    {
        if (Stage != MineStage.AtTheFace) return false;
        Stage = MineStage.Breached;
        Waves = 0;
        Tam = TamHealth;
        return true;
    }

    /// <summary>What comes through in a wave.</summary>
    public static (string Kind, int Count)[] Wave(int wave) => wave switch
    {
        0 => [("cinder_thrall", 3)],
        1 => [("cinder_thrall", 2), ("hollow_soldier", 2)],
        _ => [("hollow_soldier", 2), ("draugr", 2)],
    };

    /// <summary>A wave beaten. True if it was the last, and the mine is held.</summary>
    public bool WaveBeaten()
    {
        if (Stage != MineStage.Breached) return false;
        if (++Waves < WaveCount) return false;
        Stage = MineStage.Held;
        return true;
    }

    /// <summary>The dead reach Tam. True if that's the end of him.</summary>
    public bool Harm(double by)
    {
        if (Stage != MineStage.Breached) return false;
        Tam = Math.Max(0, Tam - by);
        return Tam <= 0;
    }

    /// <summary>The fight's lost, or left: the face is walled up again, and it can be tried again.</summary>
    public void Lost()
    {
        if (Stage != MineStage.Breached) return;
        Stage = MineStage.AtTheFace;
        Failed++;
    }

    /// <summary>A game saved mid-fight comes back to the wall, with nothing lost by it.</summary>
    public void Reload()
    {
        if (Stage == MineStage.Breached) Stage = MineStage.AtTheFace;
    }

    /// <summary>Dagna pays for the job, once.</summary>
    public int? Collect()
    {
        if (Stage != MineStage.Held) return null;
        Stage = MineStage.Done;
        return Pay;
    }
}
