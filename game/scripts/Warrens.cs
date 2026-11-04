using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The tunnels under the world laid into a room: a country mine's drifts and side chambers, the Deep Trenches' old dwarf
/// roads round the city's hall, the Dark Depths' burrows up from the Rift. The room's own floor and walls hold it; the
/// warren (<see cref="Warren"/>) puts rock between the cells that don't open into each other, a lower roof over the
/// tunnels, and leaves its chambers open: the way in, the city, the Rift, and a few side rooms with something in them.
/// </summary>
public partial class Indoors
{
    /// <summary>The tunnels in here, if this is a place of tunnels.</summary>
    public Warren? Maze { get; private set; }
    /// <summary>Its side rooms, apart from the chambers every one of its kind has.</summary>
    public IReadOnlyList<WarrenRoom> SideRooms { get; private set; } = [];

    /// <summary>How each kind of underground place is cut: its grid, the chambers it always has, its side rooms, and how high the tunnels' roof is.</summary>
    static (int Cols, int Rows, WarrenRoom[] Kept, int Side, int SideW, int SideH, float Roof)? WarrenPlan(string kind) => kind switch
    {
        "mineshaft" => (6, 9, [new("door", 2, 8, 2, 1), new("face", 1, 0, 4, 2)], 2, 2, 2, 0),
        "trench" => (6, 18, [new("door", 2, 16, 2, 2), new("city", 0, 6, 6, 7), new("mouth", 2, 0, 2, 2)], 3, 2, 1, 5.5f),
        "depths" => (8, 19, [new("door", 3, 17, 2, 2), new("rift", 0, 0, 8, 6)], 5, 2, 2, 7.5f),
        _ => null,
    };

    /// <summary>Cuts and lays the tunnels, if this kind has them: before it's furnished, so the furnishing knows where the rooms are.</summary>
    void CutWarren(float wide, float deep, float high)
    {
        if (WarrenPlan(kind) is not { } plan) return;
        var side = Warren.SideRooms(plan.Cols, plan.Rows, plan.Kept, plan.Side, plan.SideW, plan.SideH, (ulong)Math.Abs(seed) + 0x77);
        SideRooms = side;
        var w = Maze = Warren.Carve(plan.Cols, plan.Rows, [.. plan.Kept, .. side], (ulong)Math.Abs(seed) + 0x4D17);
        var (cw, cd) = (wide / w.Cols, deep / w.Rows);
        var t = Math.Min(cw, cd) * (kind == "mineshaft" ? 0.22f : 0.18f);
        var rock = kind == "depths" ? Surface.DarkStone : Surface.DarkStone;
        // The rock between cells, along each line of the grid, a run of wall at a time.
        for (var c = 0; c < w.Cols - 1; c++)
        {
            var x = -wide / 2 + (c + 1) * cw;
            for (var r = 0; r < w.Rows;)
            {
                if (w.OpenEast(c, r)) { r++; continue; }
                var start = r;
                while (r < w.Rows && !w.OpenEast(c, r)) r++;
                var (z0, z1) = (-deep / 2 + start * cd, -deep / 2 + r * cd);
                Slab(new Vector3(x, high / 2, (z0 + z1) / 2), new Vector3(t, high, z1 - z0 + t), rock);
            }
        }
        for (var r = 0; r < w.Rows - 1; r++)
        {
            var z = -deep / 2 + (r + 1) * cd;
            for (var c = 0; c < w.Cols;)
            {
                if (w.OpenSouth(c, r)) { c++; continue; }
                var start = c;
                while (c < w.Cols && !w.OpenSouth(c, r)) c++;
                var (x0, x1) = (-wide / 2 + start * cw, -wide / 2 + c * cw);
                Slab(new Vector3((x0 + x1) / 2, high / 2, z), new Vector3(x1 - x0 + t, high, t), rock);
            }
        }
        // A lower roof of rock over the tunnels (the chambers keep the cavern's height).
        if (plan.Roof > 0 && plan.Roof < high)
            for (var r = 0; r < w.Rows; r++)
                for (var c = 0; c < w.Cols;)
                {
                    if (w.RoomAt(c, r) is not null) { c++; continue; }
                    var start = c;
                    while (c < w.Cols && w.RoomAt(c, r) is null) c++;
                    var (x0, x1) = (-wide / 2 + start * cw, -wide / 2 + c * cw);
                    Part(new Vector3((x0 + x1) / 2, plan.Roof + 0.25f, -deep / 2 + (r + 0.5f) * cd), new Vector3(x1 - x0, 0.5f, cd + 0.02f), rock);
                }
        // Lamps along the tunnels: a glowing pane on the rock every few cells, a real light now and then.
        var lamp = new StandardMaterial3D { AlbedoColor = new Color(kind == "depths" ? "#ff6a3a" : "#ffc070"), EmissionEnabled = true, Emission = new Color(kind == "depths" ? "#ff4020" : "#ffa040"), EmissionEnergyMultiplier = 2.2f };
        var n = 0;
        for (var c = 0; c < w.Cols; c++)
            for (var r = 0; r < w.Rows; r++)
            {
                if (w.RoomAt(c, r) is not null || (c * 7 + r * 3) % 4 != 0) continue;
                var mid = CellMiddle(c, r);
                // On whichever side has rock.
                var (dx, dz) = !w.Open(c, r, 1, 0) && c < w.Cols - 1 ? (cw / 2 - t, 0f) : !w.Open(c, r, -1, 0) && c > 0 ? (-cw / 2 + t, 0f) : !w.Open(c, r, 0, -1) && r > 0 ? (0f, -cd / 2 + t) : (0f, cd / 2 - t);
                var at = mid - Stage + new Vector3(dx, 2.2f, dz);
                AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.4f, 0.3f) }, MaterialOverride = lamp, Position = Stage + at });
                if (n++ % 3 == 0) Light(at with { Y = 2.6f }, 0.9f, Math.Max(cw, cd) * 1.6f, kind == "depths" ? "#ff7040" : "#ffb070");
            }
        // A mine's drifts are propped with timber: a frame at every opening between tunnels.
        if (kind == "mineshaft")
            for (var c = 0; c < w.Cols; c++)
                for (var r = 0; r < w.Rows; r++)
                    foreach (var (dc, dr) in new[] { (1, 0), (0, 1) })
                    {
                        if (!w.Open(c, r, dc, dr) || w.RoomAt(c, r) is not null && w.RoomAt(c, r) == w.RoomAt(c + dc, r + dr)) continue;
                        var edge = (CellMiddle(c, r) + CellMiddle(c + dc, r + dr)) / 2 - Stage;
                        var across = dc == 1 ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
                        var half = (dc == 1 ? cd : cw) / 2 - 0.35f;
                        foreach (var s in new[] { -1f, 1f }) Part(edge + across * (s * half) + Vector3.Up * (high / 2), new Vector3(0.28f, high, 0.28f), Surface.Timber);
                        Part(edge + Vector3.Up * (high - 0.25f), dc == 1 ? new Vector3(0.3f, 0.3f, half * 2 + 0.3f) : new Vector3(half * 2 + 0.3f, 0.3f, 0.3f), Surface.Timber);
                    }
    }

    /// <summary>The middle of a cell of the tunnels, on the floor.</summary>
    public Vector3 CellMiddle(int c, int r)
    {
        var (wide, deep, _) = Plan(kind);
        var w = Maze!;
        return Stage + new Vector3(-wide / 2 + (c + 0.5f) * wide / w.Cols, 0, -deep / 2 + (r + 0.5f) * deep / w.Rows);
    }

    /// <summary>The middle of one of its chambers, on the floor, and how big it is.</summary>
    public (Vector3 At, Vector2 Size) RoomSpan(WarrenRoom m)
    {
        var (wide, deep, _) = Plan(kind);
        var w = Maze!;
        var (cw, cd) = (wide / w.Cols, deep / w.Rows);
        return (Stage + new Vector3(-wide / 2 + (m.Col + m.Cols / 2f) * cw, 0, -deep / 2 + (m.Row + m.Rows / 2f) * cd), new Vector2(m.Cols * cw, m.Rows * cd));
    }

    /// <summary>Something solid in a chamber (rubble, a tomb, a block of ore) or not (a pool's surface), for those who furnish it.</summary>
    public void Block(Vector3 at, Vector3 size, Surface s, bool solid = true)
    {
        if (solid) Slab(at - Stage, size, s);
        else Part(at - Stage, size, s);
    }

    /// <summary>A side room's own light.</summary>
    public void Glow(Vector3 at, string colour, float energy = 1.4f, float reach = 12) => Light(at - Stage, energy, reach, colour);
}
