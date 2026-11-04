using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>A chamber in a warren: a block of cells left open as one room (a city's hall, the Rift, a side room).</summary>
public sealed record WarrenRoom(string Id, int Col, int Row, int Cols, int Rows)
{
    public bool Holds(int c, int r) => c >= Col && c < Col + Cols && r >= Row && r < Row + Rows;
    public bool Overlaps(WarrenRoom o, int gap = 0) =>
        Col - gap < o.Col + o.Cols && o.Col - gap < Col + Cols && Row - gap < o.Row + o.Rows && o.Row - gap < Row + Rows;
}

/// <summary>
/// A maze of tunnels cut on a grid of cells: a mine's drifts, the Deep Trenches' old roads, the Dark Depths' burrows.
/// Every cell can be reached from every other (a winding tree of passages, carved by the seed's dice), with a few
/// extra ways through so it isn't all dead ends, and the chambers asked for are left open inside, each with a door or
/// two into the tunnels. Row 0 is the far end; the last row is where the way in is.
/// </summary>
public sealed class Warren
{
    public int Cols { get; }
    public int Rows { get; }
    public IReadOnlyList<WarrenRoom> Rooms { get; }
    readonly bool[,] _east, _south;

    Warren(int cols, int rows, IReadOnlyList<WarrenRoom> rooms) => (Cols, Rows, Rooms, _east, _south) = (cols, rows, rooms, new bool[cols, rows], new bool[cols, rows]);

    /// <summary>Whether there's a way from a cell to the one east of it (+1 column) or south of it (+1 row).</summary>
    public bool OpenEast(int c, int r) => c >= 0 && c < Cols - 1 && r >= 0 && r < Rows && _east[c, r];
    public bool OpenSouth(int c, int r) => c >= 0 && c < Cols && r >= 0 && r < Rows - 1 && _south[c, r];

    /// <summary>The chamber a cell is part of, if any.</summary>
    public WarrenRoom? RoomAt(int c, int r) => Rooms.FirstOrDefault(m => m.Holds(c, r));

    /// <summary>Whether two neighbouring cells open into each other.</summary>
    public bool Open(int c, int r, int dc, int dr) => (dc, dr) switch
    {
        (1, 0) => OpenEast(c, r),
        (-1, 0) => OpenEast(c - 1, r),
        (0, 1) => OpenSouth(c, r),
        (0, -1) => OpenSouth(c, r - 1),
        _ => false,
    };

    void Set(int c, int r, int dc, int dr)
    {
        if (dc == 1) _east[c, r] = true;
        else if (dc == -1) _east[c - 1, r] = true;
        else if (dr == 1) _south[c, r] = true;
        else if (dr == -1) _south[c, r - 1] = true;
    }

    static readonly (int, int)[] Ways = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Cuts a warren. <paramref name="loops"/> is the share of the remaining walls between tunnels knocked through, so there's more than one way round.</summary>
    public static Warren Carve(int cols, int rows, IReadOnlyList<WarrenRoom> rooms, ulong seed, double loops = 0.12)
    {
        var w = new Warren(cols, rows, rooms);
        var rng = new Rng(seed ^ 0x3A77E2C1UL);
        // The chambers are open inside.
        foreach (var m in rooms)
            for (var c = m.Col; c < m.Col + m.Cols; c++)
                for (var r = m.Row; r < m.Row + m.Rows; r++)
                {
                    if (c + 1 < m.Col + m.Cols) w._east[c, r] = true;
                    if (r + 1 < m.Row + m.Rows) w._south[c, r] = true;
                }
        // A winding tree through everything: a chamber is entered as a whole, so it gets few doors, not a wall of them.
        var seen = new bool[cols, rows];
        void Visit(int c, int r)
        {
            if (w.RoomAt(c, r) is { } m)
            {
                for (var x = m.Col; x < m.Col + m.Cols; x++)
                    for (var y = m.Row; y < m.Row + m.Rows; y++) seen[x, y] = true;
            }
            else seen[c, r] = true;
        }
        var stack = new Stack<(int C, int R)>();
        Visit(cols / 2, rows - 1);
        stack.Push((cols / 2, rows - 1));
        while (stack.Count > 0)
        {
            var (c, r) = stack.Peek();
            // From a chamber, any of its edge cells can lead on.
            var from = w.RoomAt(c, r) is { } room
                ? Enumerable.Range(room.Col, room.Cols).SelectMany(x => Enumerable.Range(room.Row, room.Rows).Select(y => (x, y))).ToList()
                : [(c, r)];
            var next = from.SelectMany(f => Ways.Select(d => (F: f, D: d, To: (f.Item1 + d.Item1, f.Item2 + d.Item2))))
                .Where(o => o.To.Item1 >= 0 && o.To.Item1 < cols && o.To.Item2 >= 0 && o.To.Item2 < rows && !seen[o.To.Item1, o.To.Item2]).ToList();
            if (next.Count == 0) { stack.Pop(); continue; }
            var pick = next[rng.Next(next.Count)];
            w.Set(pick.F.Item1, pick.F.Item2, pick.D.Item1, pick.D.Item2);
            Visit(pick.To.Item1, pick.To.Item2);
            stack.Push(pick.To);
        }
        // A few more ways through, between tunnels (and now and then a second door into a chamber).
        for (var c = 0; c < cols; c++)
            for (var r = 0; r < rows; r++)
                foreach (var (dc, dr) in new[] { (1, 0), (0, 1) })
                {
                    var (x, y) = (c + dc, r + dr);
                    if (x >= cols || y >= rows || w.Open(c, r, dc, dr)) continue;
                    var chamber = w.RoomAt(c, r) is not null || w.RoomAt(x, y) is not null;
                    if (rng.Chance(chamber ? loops / 3 : loops)) w.Set(c, r, dc, dr);
                }
        return w;
    }

    /// <summary>How many steps from one cell to another through the tunnels, or -1 if there's no way.</summary>
    public int Steps((int C, int R) from, (int C, int R) to)
    {
        var far = new int[Cols, Rows];
        for (var c = 0; c < Cols; c++) for (var r = 0; r < Rows; r++) far[c, r] = -1;
        var q = new Queue<(int, int)>();
        far[from.C, from.R] = 0;
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var (c, r) = q.Dequeue();
            if ((c, r) == to) return far[c, r];
            foreach (var (dc, dr) in Ways)
            {
                var (x, y) = (c + dc, r + dr);
                if (x < 0 || y < 0 || x >= Cols || y >= Rows || far[x, y] >= 0 || !Open(c, r, dc, dr)) continue;
                far[x, y] = far[c, r] + 1;
                q.Enqueue((x, y));
            }
        }
        return -1;
    }

    /// <summary>Side rooms, scattered where the chambers asked for aren't: <paramref name="n"/> of them, each a block of the given size.</summary>
    public static List<WarrenRoom> SideRooms(int cols, int rows, IReadOnlyList<WarrenRoom> kept, int n, int w, int h, ulong seed, string prefix = "side")
    {
        var rng = new Rng(seed ^ 0x51DE5EEDUL);
        var made = new List<WarrenRoom>();
        for (var tries = 0; tries < 200 && made.Count < n; tries++)
        {
            var m = new WarrenRoom($"{prefix}{made.Count}", rng.Next(Math.Max(1, cols - w + 1)), rng.Next(Math.Max(1, rows - h + 1)), w, h);
            if (m.Col + w > cols || m.Row + h > rows || kept.Concat(made).Any(o => o.Overlaps(m, 1))) continue;
            made.Add(m);
        }
        return made;
    }
}
