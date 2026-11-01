namespace Tristinwold.World;

/// <summary>
/// The king's roads: from Tristinwold's gates (and the ends of the Northwood's own roads) to every city and town within a
/// wagon's reach. Each is found the way a road-builder would find it, over the low ground, round the water, along a
/// road already laid where one goes the right way, and through a pass where there's no way round; then it's cut and laid:
/// the trees cleared off it, the ground trodden to dirt, hills cut and dips banked so a cart never meets a grade it
/// can't take. Worked out once from the world, the same for everyone, and the land is shaped to it (see
/// <see cref="WorldGen.Height"/>).
/// </summary>
public sealed class Highways
{
    /// <summary>The road's width either side of its line; the steepest a laid road gets; how far out the roads go.</summary>
    public const double Half = 4.5, MaxGrade = 0.14, Reach = 4000;
    /// <summary>The road-builder's grid, how often the laid road is sampled, how far either way the land is averaged for its surface, and the grid's margin round everything.</summary>
    const double Cell = 50, Sample = 15, Smooth = 90, Margin = 600;

    /// <summary>One road, from a gate to a town's edge: its line, the distance along it at each point, and its surface there.</summary>
    public sealed class Road
    {
        public required string To { get; init; }
        public required string ToName { get; init; }
        public required (double X, double Z)[] Line { get; init; }
        public required double[] Along { get; init; }
        public required double[] Heights { get; init; }
        public double Length => Along[^1];
        public double MinX { get; init; }
        public double MaxX { get; init; }
        public double MinZ { get; init; }
        public double MaxZ { get; init; }

        public bool Within(double x, double z, double margin) => x >= MinX - margin && x <= MaxX + margin && z >= MinZ - margin && z <= MaxZ + margin;

        /// <summary>The nearest point of the road to here: how far off it, how far along it, and the road's surface there.</summary>
        public (double Distance, double Along, double Height) Nearest(double x, double z)
        {
            var best = (D2: double.MaxValue, Along: 0.0, Height: 0.0);
            for (var i = 1; i < Line.Length; i++)
            {
                var (ax, az) = Line[i - 1];
                var (bx, bz) = Line[i];
                var (dx, dz) = (bx - ax, bz - az);
                var len2 = dx * dx + dz * dz;
                var t = len2 > 0 ? Math.Clamp(((x - ax) * dx + (z - az) * dz) / len2, 0, 1) : 0;
                var (px, pz) = (ax + dx * t, az + dz * t);
                var d2 = (x - px) * (x - px) + (z - pz) * (z - pz);
                if (d2 < best.D2) best = (d2, Along[i - 1] + (Along[i] - Along[i - 1]) * t, Heights[i - 1] + (Heights[i] - Heights[i - 1]) * t);
            }
            return (Math.Sqrt(best.D2), best.Along, best.Height);
        }

        /// <summary>The point this far along the road (held to its ends).</summary>
        public (double X, double Z) At(double along)
        {
            along = Math.Clamp(along, 0, Length);
            var i = Array.BinarySearch(Along, along);
            if (i >= 0) return Line[i];
            i = ~i;
            if (i <= 0) return Line[0];
            if (i >= Line.Length) return Line[^1];
            var t = (along - Along[i - 1]) / (Along[i] - Along[i - 1]);
            return (Line[i - 1].X + (Line[i].X - Line[i - 1].X) * t, Line[i - 1].Z + (Line[i].Z - Line[i - 1].Z) * t);
        }
    }

    public IReadOnlyList<Road> Roads { get; }

    Highways(List<Road> roads) => Roads = roads;

    /// <summary>Where a road may set out from: the west gate, the east gate, where the Northwood's north road comes out of the wood, and where its west road ends at the Strand.</summary>
    public static readonly (double X, double Z)[] Starts = [(-108, -4), (92, 5), (Northwood.ExitX, Northwood.ExitZ), (-960, -260)];

    /// <summary>The road to a place, if one was laid.</summary>
    public Road? To(string id) => Roads.FirstOrDefault(r => r.To == id);

    /// <summary>How far a point is from the nearest road's line, and the road's surface there; nothing if no road comes within <paramref name="margin"/>.</summary>
    public (double Distance, double Height)? Nearest(double x, double z, double margin)
    {
        (double Distance, double Height)? best = null;
        foreach (var r in Roads)
        {
            if (!r.Within(x, z, margin)) continue;
            var (d, _, h) = r.Nearest(x, z);
            if (d <= margin && (best is null || d < best.Value.Distance)) best = (d, h);
        }
        return best;
    }

    /// <summary>How far a point is from the nearest road, out to a hundred metres.</summary>
    public double Near(double x, double z) => Nearest(x, z, 100)?.Distance ?? double.MaxValue;

    /// <summary>On a road: trodden to dirt, and no tree stands there.</summary>
    public bool OnRoad(double x, double z) => Near(x, z) < Half;

    /// <summary>
    /// The land with the roads laid on it: the road's own surface along it, and either side the ground cut or banked to
    /// meet it, at a slope a cart could climb, so a road through a hill runs in a cutting and one across a dip on a bank.
    /// </summary>
    public double Shape(double x, double z, double h)
    {
        if (Nearest(x, z, 120) is not { } n) return h;
        var verge = Half + 2 + 1.2 * Math.Abs(h - n.Height);
        if (n.Distance >= verge) return h;
        return Noise.Lerp(n.Height, h, Noise.SmoothStep(Half, verge, n.Distance));
    }

    // ---- laying them ----------------------------------------------------------------------------------------------

    /// <summary>Lays the roads of a world: to every city and town within reach, nearest first, each from whichever start the land makes nearest.</summary>
    public static Highways Lay(WorldGen world)
    {
        var roads = new List<Road>();
        var seen = new HashSet<string>();
        foreach (var s in world.CitiesNear(0, 0, Reach).Where(s => s.IsCity && s.Id is not ("city:tristinwold" or "city:hollowheart")).OrderBy(s => s.DistanceTo(0, 0)).ThenBy(s => s.Id, StringComparer.Ordinal))
        {
            if (!seen.Add(s.Id)) continue;
            if (Starts.Any(p => s.DistanceTo(p.X, p.Z) < s.Radius + 60)) continue; // a road already comes to its gate (Druitt, at the end of the west road)
            if (Route(world, roads, s) is { } line) roads.Add(Build(world, s, line));
        }
        return new Highways(roads);
    }

    /// <summary>
    /// The road-builder's way from the nearest start to a place: over a grid of the country, the cheapest path by distance
    /// and climb, never through water or the Northwood, and glad of a road already laid. Null if water cuts the place off.
    /// </summary>
    static (double X, double Z)[]? Route(WorldGen world, IReadOnlyList<Road> laid, Settlement to)
    {
        var xs = Starts.Select(p => p.X).Append(to.X).ToList();
        var zs = Starts.Select(p => p.Z).Append(to.Z).ToList();
        var (x0, z0) = (xs.Min() - Margin, zs.Min() - Margin);
        var (nx, nz) = ((int)Math.Ceiling((xs.Max() + Margin - x0) / Cell) + 1, (int)Math.Ceiling((zs.Max() + Margin - z0) / Cell) + 1);
        double X(int i) => x0 + (i + 0.5) * Cell;
        double Z(int j) => z0 + (j + 0.5) * Cell;
        int Node(double x, double z) => Math.Clamp((int)((z - z0) / Cell), 0, nz - 1) * nx + Math.Clamp((int)((x - x0) / Cell), 0, nx - 1);
        var n = nx * nz;
        var h = new double[n];
        var blocked = new bool[n];
        var onRoad = new bool[n];
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var (x, z) = (X(i), Z(j));
                var k = j * nx + i;
                h[k] = world.Lie(x, z);
                blocked[k] = Northwood.Sea(x, z) > 0.25 || h[k] < WorldGen.WaterLevel + 1 || x * x + z * z < 100 * 100
                    || Northwood.Inside(x, z) || Northwood.Inside(x - Cell / 2, z - Cell / 2) || Northwood.Inside(x + Cell / 2, z - Cell / 2) || Northwood.Inside(x - Cell / 2, z + Cell / 2) || Northwood.Inside(x + Cell / 2, z + Cell / 2); // the wood, to its edge: the maze is nobody's to cut
                onRoad[k] = laid.Any(r => r.Within(x, z, Cell) && r.Nearest(x, z).Distance < Half + Cell * 0.5);
            }
        var end = Node(to.X, to.Z);
        blocked[end] = false;
        var dist = new double[n];
        var prev = new int[n];
        Array.Fill(dist, double.MaxValue);
        Array.Fill(prev, -1);
        var queue = new PriorityQueue<int, double>();
        foreach (var p in Starts)
        {
            var k = Node(p.X, p.Z);
            blocked[k] = false;
            dist[k] = 0;
            queue.Enqueue(k, 0);
        }
        while (queue.TryDequeue(out var a, out var da))
        {
            if (da > dist[a]) continue;
            if (a == end) break;
            var (ai, aj) = (a % nx, a / nx);
            for (var dj = -1; dj <= 1; dj++)
                for (var di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    var (bi, bj) = (ai + di, aj + dj);
                    if (bi < 0 || bj < 0 || bi >= nx || bj >= nz) continue;
                    var b = bj * nx + bi;
                    if (blocked[b]) continue;
                    var step = di != 0 && dj != 0 ? Cell * Math.Sqrt(2) : Cell;
                    var grade = Math.Abs(h[b] - h[a]) / step;
                    var cost = step * (1 + 10 * grade + (h[b] > 90 ? 1 : 0)) * (onRoad[b] ? 0.35 : 1);
                    if (da + cost >= dist[b]) continue;
                    dist[b] = da + cost;
                    prev[b] = a;
                    queue.Enqueue(b, dist[b]);
                }
        }
        if (dist[end] == double.MaxValue) return null;
        var cells = new List<int>();
        for (var k = end; k >= 0; k = prev[k]) cells.Add(k);
        cells.Reverse();
        var first = cells[0];
        var start = Starts.MinBy(p => Math.Abs(X(first % nx) - p.X) + Math.Abs(Z(first / nx) - p.Z));
        var line = new List<(double X, double Z)> { start };
        line.AddRange(cells.Skip(1).Select(k => (X(k % nx), Z(k / nx))));
        return [.. line];
    }

    /// <summary>A found way made a road: ended at the town's edge, its corners eased, sampled every few metres, and its surface worked out.</summary>
    static Road Build(WorldGen world, Settlement to, (double X, double Z)[] way)
    {
        // It ends at the town's edge, not its square: the town's own ground takes over there.
        var kept = way.TakeWhile(p => to.DistanceTo(p.X, p.Z) > to.Radius).ToList();
        if (kept.Count == 0) kept.Add(way[0]);
        var last = kept[^1];
        var (dx, dz) = (to.X - last.X, to.Z - last.Z);
        var d = Math.Sqrt(dx * dx + dz * dz);
        kept.Add((to.X - dx / d * to.Radius, to.Z - dz / d * to.Radius));
        var line = Resample(Chaikin(Chaikin(Simplify(kept, 30))), Sample);
        if (line.Any(p => Northwood.Inside(p.X, p.Z))) line = Resample(Chaikin(kept), Sample); // the easing cut a corner into the wood: keep closer to the found way
        var along = new double[line.Length];
        for (var i = 1; i < line.Length; i++) along[i] = along[i - 1] + Math.Sqrt(Sq(line[i].X - line[i - 1].X) + Sq(line[i].Z - line[i - 1].Z));
        // The surface: the land averaged a way either side (so hills are cut and dips banked), never under water, and never steeper than a cart can take.
        var raw = line.Select(p => world.Lie(p.X, p.Z)).ToArray();
        var heights = new double[line.Length];
        for (var i = 0; i < line.Length; i++)
        {
            var (sum, count) = (0.0, 0);
            for (var j = 0; j < line.Length; j++)
                if (Math.Abs(along[j] - along[i]) <= Smooth) { sum += raw[j]; count++; }
            heights[i] = Math.Max(sum / count, WorldGen.WaterLevel + 1.5);
        }
        for (var i = 1; i < line.Length; i++) heights[i] = Math.Clamp(heights[i], heights[i - 1] - MaxGrade * (along[i] - along[i - 1]), heights[i - 1] + MaxGrade * (along[i] - along[i - 1]));
        for (var i = line.Length - 2; i >= 0; i--) heights[i] = Math.Clamp(heights[i], heights[i + 1] - MaxGrade * (along[i + 1] - along[i]), heights[i + 1] + MaxGrade * (along[i + 1] - along[i]));
        return new Road
        {
            To = to.Id, ToName = to.Name, Line = line, Along = along, Heights = heights,
            MinX = line.Min(p => p.X), MaxX = line.Max(p => p.X), MinZ = line.Min(p => p.Z), MaxZ = line.Max(p => p.Z),
        };
    }

    static double Sq(double v) => v * v;

    /// <summary>Fewer points, none further than the tolerance from the line they stood on (Douglas and Peucker's way).</summary>
    static List<(double X, double Z)> Simplify(List<(double X, double Z)> points, double tolerance)
    {
        if (points.Count < 3) return points;
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            var (worst, at) = (0.0, -1);
            for (var i = a + 1; i < b; i++)
            {
                var off = ToSegment(points[i], points[a], points[b]);
                if (off > worst) (worst, at) = (off, i);
            }
            if (worst <= tolerance || at < 0) continue;
            keep[at] = true;
            stack.Push((a, at));
            stack.Push((at, b));
        }
        return points.Where((_, i) => keep[i]).ToList();
    }

    static double ToSegment((double X, double Z) p, (double X, double Z) a, (double X, double Z) b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var len2 = dx * dx + dz * dz;
        var t = len2 > 0 ? Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2, 0, 1) : 0;
        return Math.Sqrt(Sq(p.X - (a.X + dx * t)) + Sq(p.Z - (a.Z + dz * t)));
    }

    /// <summary>The corners eased (Chaikin's way), the ends kept where they are.</summary>
    static List<(double X, double Z)> Chaikin(List<(double X, double Z)> points)
    {
        if (points.Count < 3) return points;
        var eased = new List<(double X, double Z)> { points[0] };
        for (var i = 0; i < points.Count - 1; i++)
        {
            var (a, b) = (points[i], points[i + 1]);
            eased.Add((a.X * 0.75 + b.X * 0.25, a.Z * 0.75 + b.Z * 0.25));
            eased.Add((a.X * 0.25 + b.X * 0.75, a.Z * 0.25 + b.Z * 0.75));
        }
        eased.Add(points[^1]);
        return eased;
    }

    /// <summary>The same line, as points an even distance apart.</summary>
    static (double X, double Z)[] Resample(List<(double X, double Z)> points, double spacing)
    {
        var line = new List<(double X, double Z)> { points[0] };
        var carry = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            var len = Math.Sqrt(Sq(b.X - a.X) + Sq(b.Z - a.Z));
            if (len <= 0) continue;
            var s = spacing - carry;
            while (s < len)
            {
                var t = s / len;
                line.Add((a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t));
                s += spacing;
            }
            carry = len - (s - spacing);
        }
        var end = points[^1];
        if (Math.Sqrt(Sq(end.X - line[^1].X) + Sq(end.Z - line[^1].Z)) > 1) line.Add(end);
        else line[^1] = end;
        return [.. line];
    }
}
