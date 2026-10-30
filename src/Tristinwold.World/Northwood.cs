using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>
/// The Northwood: the great forest north of Tristinwold, thick with giant trees. A road runs from the west gate through
/// it to the treetop village and on out of the north, and a road runs west to the Strand, the long beach under the
/// western mountains. Everything else in there is a maze of narrow paths between walls of giant trunks, and at the
/// ends of those paths are the guardians. North of the wood the Westreach mountains begin at its edge, run along behind
/// it, then swoop far to the west and down to the coast.
/// </summary>
public static class Northwood
{
    public const double CentreX = -40, CentreZ = -760, Wide = 640, Deep = 530;
    public const double VillageX = -140, VillageZ = -640, ExitX = -100, ExitZ = -1300;
    /// <summary>A maze cell's size, how far a path reaches from its line, and how far apart the trunks of a wall stand.</summary>
    public const double Cell = 44, PathHalf = 2.8, RoadHalf = 4.5, Trunks = 4.6;

    /// <summary>The road from the west gate to the village and on out of the north.</summary>
    public static readonly (double X, double Z)[] NorthRoad =
        [(-108, -4), (-180, -130), (-160, -300), (VillageX, VillageZ), (-170, -800), (-120, -1000), (-130, -1150), (ExitX, ExitZ)];
    /// <summary>The road from the west gate to the Strand, past the wood's western edge.</summary>
    public static readonly (double X, double Z)[] WestRoad = [(-108, -4), (-400, -120), (-690, -260), (-960, -260)];

    /// <summary>The Westreach: where its crest runs, from the wood's north-west edge, far to the west and down to the sea.</summary>
    static readonly (double X, double Z)[] Ridge =
        [(-620, -1330), (-1100, -1430), (-1750, -1220), (-2350, -720), (-2620, 0), (-2460, 820), (-2200, 1500)];
    const double RidgeHigh = 175, RidgeWide = 210;
    const double BayX = -1500, BayZ = -250, BayWide = 520, BayDeep = 430;

    static double Sq(double x) => x * x;

    static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    static double ToSegment(double x, double z, (double X, double Z) a, (double X, double Z) b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var t = Math.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / (dx * dx + dz * dz), 0, 1);
        return Math.Sqrt(Sq(x - (a.X + dx * t)) + Sq(z - (a.Z + dz * t)));
    }

    static double ToLine((double X, double Z)[] line, double x, double z)
    {
        var best = double.MaxValue;
        for (var i = 1; i < line.Length; i++) best = Math.Min(best, ToSegment(x, z, line[i - 1], line[i]));
        return best;
    }

    /// <summary>Whether a point is in the wood.</summary>
    public static bool Inside(double x, double z) => Sq((x - CentreX) / Wide) + Sq((z - CentreZ) / Deep) < 1;

    /// <summary>The land as this corner of the world has it: the mountains raised, and the bay of the Strand let in.</summary>
    public static double Shape(double x, double z, double h)
    {
        if (x < -300 && z > -1900)
        {
            var off = ToLine(Ridge, x, z);
            if (off < RidgeWide * 3)
            {
                var jag = 0.72 + 0.28 * Math.Sin(x * 0.0071 + z * 0.0043) * Math.Cos(z * 0.0093 - x * 0.0031);
                h += RidgeHigh * jag * Math.Exp(-Sq(off / RidgeWide));
            }
            var bay = Math.Sqrt(Sq((x - BayX) / BayWide) + Sq((z - BayZ) / BayDeep));
            if (bay < 1.25) h += (-5 - h) * (1 - Ease((bay - 0.82) / 0.43));
        }
        return h;
    }

    // ---- the roads and the maze -----------------------------------------------------------------------------------

    static readonly HashSet<(int, int, int, int)> Open = [];
    static readonly HashSet<(int, int)> Cells = [];

    static (int I, int J) CellOf(double x, double z) => ((int)Math.Floor((x - CentreX) / Cell), (int)Math.Floor((z - CentreZ) / Cell));
    static (double X, double Z) Middle(int i, int j) => (CentreX + (i + 0.5) * Cell, CentreZ + (j + 0.5) * Cell);

    /// <summary>The maze's cells: everything in the wood north of the village, carved once by a walk that never crosses itself.</summary>
    static Northwood()
    {
        for (var i = -16; i <= 16; i++)
            for (var j = -13; j <= 13; j++)
            {
                var (x, z) = Middle(i, j);
                if (Inside(x, z) && z < VillageZ - 30) Cells.Add((i, j));
            }
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int I, int J)>();
        var start = CellOf(VillageX, VillageZ - 90);
        stack.Push(start);
        seen.Add(start);
        var state = 0x51ED27u;
        uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        while (stack.Count > 0)
        {
            var (i, j) = stack.Peek();
            var ways = new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) }.Where(c => Cells.Contains(c) && !seen.Contains(c)).ToArray();
            if (ways.Length == 0) { stack.Pop(); continue; }
            var next = ways[Next() % ways.Length];
            Open.Add(Key((i, j), next));
            seen.Add(next);
            stack.Push(next);
        }
    }

    static (int, int, int, int) Key((int I, int J) a, (int I, int J) b) =>
        a.I < b.I || a.I == b.I && a.J < b.J ? (a.I, a.J, b.I, b.J) : (b.I, b.J, a.I, a.J);

    /// <summary>Every cell of the maze can be reached from every other: how many there are, for the tests.</summary>
    public static int MazeCells => Cells.Count;
    public static int MazePaths => Open.Count;

    /// <summary>On a road, safe from the guardians if not from everything else.</summary>
    public static bool OnRoad(double x, double z) => ToLine(NorthRoad, x, z) < RoadHalf || ToLine(WestRoad, x, z) < RoadHalf;

    /// <summary>On a road or a path of the maze: kept clear of trees, and trodden to dirt.</summary>
    public static bool OnPath(double x, double z)
    {
        if (OnRoad(x, z)) return true;
        var (i, j) = CellOf(x, z);
        if (!Cells.Contains((i, j))) return false;
        var (mx, mz) = Middle(i, j);
        var (dx, dz) = (x - mx, z - mz);
        if (Math.Abs(dx) < PathHalf && Math.Abs(dz) < PathHalf) return true;
        if (dx > 0 && Math.Abs(dz) < PathHalf && Open.Contains(Key((i, j), (i + 1, j)))) return true;
        if (dx < 0 && Math.Abs(dz) < PathHalf && Open.Contains(Key((i, j), (i - 1, j)))) return true;
        if (dz > 0 && Math.Abs(dx) < PathHalf && Open.Contains(Key((i, j), (i, j + 1)))) return true;
        return dz < 0 && Math.Abs(dx) < PathHalf && Open.Contains(Key((i, j), (i, j - 1)));
    }

    /// <summary>The giant trees standing in a square of the world: the walls of the maze, trunk beside trunk, so its paths can't be cut across.</summary>
    public static IEnumerable<(double X, double Z)> GiantsIn(double x0, double z0, double size)
    {
        if (x0 + size < CentreX - Wide || x0 > CentreX + Wide || z0 + size < CentreZ - Deep || z0 > CentreZ + Deep) yield break;
        // Each cell's walls lie between its middle and the next cell's: a cross of lines at the cell corners' grid.
        var (i0, j0) = CellOf(x0, z0);
        var (i1, j1) = CellOf(x0 + size, z0 + size);
        for (var i = i0 - 1; i <= i1; i++)
            for (var j = j0 - 1; j <= j1; j++)
            {
                var (mx, mz) = Middle(i, j);
                foreach (var (x, z) in Wall((i, j), (i + 1, j), mx + Cell / 2, mz, true).Concat(Wall((i, j), (i, j + 1), mx, mz + Cell / 2, false)))
                    if (x >= x0 && x < x0 + size && z >= z0 && z < z0 + size && !OnPath(x, z)) yield return (x, z);
                // South of the maze they stand free, thick among the rest.
                if (mz >= VillageZ - 30 && Inside(mx, mz))
                    for (var k = 0; k < 3; k++)
                    {
                        var (sx, sz) = (mx + Math.Sin(i * 37.1 + j * 11.3 + k * 5.7) * Cell * 0.45, mz + Math.Cos(i * 13.7 - j * 29.9 + k * 3.1) * Cell * 0.45);
                        if (sx >= x0 && sx < x0 + size && sz >= z0 && sz < z0 + size && !OnPath(sx, sz) && !NearVillage(sx, sz)) yield return (sx, sz);
                    }
            }
    }

    /// <summary>The trunks along the side between two cells, with a gap if the maze runs through it.</summary>
    static IEnumerable<(double X, double Z)> Wall((int I, int J) a, (int I, int J) b, double x, double z, bool alongZ)
    {
        if (!Cells.Contains(a) && !Cells.Contains(b)) yield break;
        var open = Open.Contains(Key(a, b));
        for (var along = -Cell / 2; along < Cell / 2; along += Trunks)
        {
            if (open && Math.Abs(along + Trunks / 2) < PathHalf + 1.5) continue;
            var wobble = Math.Sin(x * 12.9 + z * 78.2 + along * 37.7) * 1.1;
            yield return alongZ ? (x + wobble, z + along) : (x + along, z + wobble);
        }
    }

    // ---- Hollowheart, the guardians and the dragon ---------------------------------------------------------------

    /// <summary>The great trees the village grew up through, the first the Heartwood, and how high their decks are grown.</summary>
    public sealed record HeartTree(double X, double Z, double Radius, double Height);
    public const float DeckHeight = 13;

    public static readonly IReadOnlyList<HeartTree> Hearts =
    [
        new(VillageX, VillageZ, 9, 62),
        new(VillageX - 34, VillageZ - 8, 6, 50),
        new(VillageX + 32, VillageZ - 12, 6.5f, 52),
        new(VillageX - 20, VillageZ + 36, 5, 44),
        new(VillageX + 26, VillageZ + 38, 5.5f, 46),
    ];

    /// <summary>A guardian of the wood: where it stands at the end of the small paths, and how far its ground runs.</summary>
    /// <param name="Loot">What it leaves: worth the terrible thing it is to take it.</param>
    public sealed record Guardian(string Id, string Name, string Kind, double X, double Z, double Territory, string Loot, bool Corrupted = false);

    public static readonly IReadOnlyList<Guardian> Guardians =
    [
        new("mosswarden", "Ulmoth the Mosswarden", "guardian_moss", -420, -905, 70, "mosswarden_staff"),
        new("blightroot", "Ashvael the Blighted", "guardian_blight", 318, -782, 70, "blightthorn", Corrupted: true),
        new("barkbreaker", "Korrun Barkbreaker", "guardian_bark", 142, -1124, 70, "barkbreaker_maul"),
        new("drowned", "Selmeth of the Drowned Root", "guardian_elder", -312, -1176, 70, "drowned_mantle"),
    ];

    /// <summary>Where the rot runs, round the guardian that turned.</summary>
    public const double RotRadius = 175;
    public static bool Corrupted(double x, double z)
    {
        var g = Guardians.First(g => g.Corrupted);
        return Math.Sqrt(Sq(x - g.X) + Sq(z - g.Z)) < RotRadius;
    }

    /// <summary>The cave under the western front, where the wood and the mountains overlap, and what sleeps there.</summary>
    public const double LairX = -640, LairZ = -1190;
    public const string DragonName = "Verdaxis, the Fifth Guardian";

    /// <summary>Rafikki's perch, off the road where a hero looking for trouble will find it.</summary>
    public const double RafikkiX = -262, RafikkiZ = -470;

    /// <summary>The logging camp at the wood's edge.</summary>
    public const double CampX = 40, CampZ = -222;

    // ---- what's out there ------------------------------------------------------------------------------------------

    /// <summary>How hard this part of the wood is, as the level a hero should be: 3 on the way to the village, rising to 8 at the northern exit.</summary>
    public static double Level(double x, double z) => z >= VillageZ ? 3 : 3 + 5 * Ease((VillageZ - z) / (VillageZ - ExitZ));

    /// <summary>How hard the things of the wood hit, for a level (<see cref="WorldGen.Danger"/>'s scale).</summary>
    public static double DangerFor(double level) => 1 + (level - 1) * 0.12;

    public static bool NearVillage(double x, double z) => Math.Sqrt(Sq(x - VillageX) + Sq(z - VillageZ)) < 95;

    /// <summary>Kept clear of the wild trees: the village's ground, the camp's cut, the guardians' glades.</summary>
    public static bool Clearing(double x, double z) =>
        Math.Sqrt(Sq(x - VillageX) + Sq(z - VillageZ)) < 62 || Math.Sqrt(Sq(x - CampX) + Sq(z - CampZ)) < 36 ||
        Math.Sqrt(Sq(x - LairX) + Sq(z - LairZ)) < 20 || Guardians.Any(g => Math.Sqrt(Sq(x - g.X) + Sq(z - g.Z)) < 26);

    /// <summary>What lives in the wood: a band in about one cell of three, harder the further north, and none near the village.</summary>
    public static IEnumerable<Herd> Near(double x, double z, double radius)
    {
        const double step = Cell * 2;
        for (var cx = Math.Floor((x - radius) / step); cx * step <= x + radius; cx++)
            for (var cz = Math.Floor((z - radius) / step); cz * step <= z + radius; cz++)
            {
                var (hx, hz) = ((cx + 0.5) * step, (cz + 0.5) * step);
                if (!Inside(hx, hz) || NearVillage(hx, hz) || Math.Sqrt(Sq(hx - x) + Sq(hz - z)) > radius) continue;
                var roll = Math.Abs(((long)cx * 73856093L) ^ ((long)cz * 19349663L));
                if (roll % 3 != 0) continue;
                var level = Level(hx, hz);
                string[] kinds = level < 4.5 ? ["wolf", "boar", "bandit"] : level < 6.5 ? ["goblin", "bandit", "wolf"] : ["deathcrest_ape", "draugr", "goblin"];
                var kind = kinds[roll / 7 % kinds.Length];
                yield return new Herd($"northwood:{cx}:{cz}", kind, hx, hz, 2 + (int)(level / 3), 18,
                    Name: Corrupted(hx, hz) ? $"Corrupted {Bestiary.Get(kind).Name}" : null);
            }
    }
}
