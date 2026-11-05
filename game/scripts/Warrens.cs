using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The dwarves' door down to the Deep Trenches: a squared arch of dressed stone, a door of dark oak bound in iron
/// with a great ring, runes cut over it that glow blue, and a word cut under them. It stands at the back of a mine, where
/// the way down is, so nobody has to stumble on a crack to find it.
/// </summary>
public static class DwarfDoor
{
    public static Node3D Build(Node3D parent, Vector3 at, float yaw, string words = "The Deep Door\n(down to the Deep Trenches)")
    {
        var root = new Node3D { Position = at, Rotation = new Vector3(0, yaw, 0) };
        parent.AddChild(root);
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#6a6460"), Roughness = 0.95f };
        var oak = new StandardMaterial3D { AlbedoColor = new Color("#3a2a1c"), Roughness = 0.85f };
        var iron = new StandardMaterial3D { AlbedoColor = new Color("#2e2e32"), Roughness = 0.45f, Metallic = 0.8f };
        var rune = new StandardMaterial3D { AlbedoColor = new Color("#8ac8ff"), EmissionEnabled = true, Emission = new Color("#4aa0ff"), EmissionEnergyMultiplier = 2.4f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        void Box(Vector3 p, Vector3 s, Material m) => root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = s }, MaterialOverride = m, Position = p });
        // The arch: two posts, a lintel, a step; the door set in it.
        foreach (var side in new[] { -1f, 1f }) Box(new Vector3(side * 1.45f, 1.7f, 0), new Vector3(0.7f, 3.4f, 0.8f), stone);
        Box(new Vector3(0, 3.65f, 0), new Vector3(3.8f, 0.6f, 0.9f), stone);
        Box(new Vector3(0, 0.08f, 0.45f), new Vector3(3.4f, 0.16f, 0.6f), stone);
        Box(new Vector3(0, 1.7f, -0.1f), new Vector3(2.2f, 3.4f, 0.2f), oak);
        foreach (var y in new[] { 0.6f, 1.7f, 2.8f }) Box(new Vector3(0, y, 0.02f), new Vector3(2.24f, 0.14f, 0.06f), iron); // its bands
        root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.22f }, MaterialOverride = iron, Position = new Vector3(0.6f, 1.5f, 0.08f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        for (var i = 0; i < 7; i++) // runes across the lintel
            Box(new Vector3(-1.5f + i * 0.5f, 3.65f, 0.46f), new Vector3(0.12f + (i % 3) * 0.05f, 0.32f - (i % 2) * 0.1f, 0.02f), rune);
        root.AddChild(new OmniLight3D { Position = new Vector3(0, 3, 1.2f), LightColor = new Color("#8ac8ff"), LightEnergy = 1.2f, OmniRange = 7 });
        root.AddChild(new Label3D { Text = words, Position = new Vector3(0, 4.5f, 0.3f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#a8d0ff") });
        return root;
    }
}

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

    /// <summary>A mine's way down: the dwarf door at the back of its face.</summary>
    public Vector3 DeepDoorAt => Stage + new Vector3(0, 0, -Plan(kind).Deep / 2 + 0.6f);

    /// <summary>Ore in a mine's walls to work with a pick: the mine's own ore (silver for a silver mine, iron for the rest), coal and grey stone, and at the face, now and then, gold.</summary>
    void Seams(float wide, float deep)
    {
        if (Maze is not { } w || kind != "mineshaft") return;
        var rng = new Tristinwold.Deviant.Rng((ulong)Math.Abs(seed) + 0x0E5);
        var what = main.Shafts.TryGetValue(seed, out var mine) ? mine.What : "";
        var own = what.Contains("silver") ? "silver" : "iron";
        var spots = new List<(Vector3 At, string Kind)>();
        // At the face, along its back wall either side of the door.
        foreach (var (x, k) in new[] { (-6.2f, own), (-3.4f, "coal"), (3.4f, own), (6.2f, rng.Chance(0.25) ? "gold" : "stone") })
            spots.Add((Stage + new Vector3(x, 0, -deep / 2 + 1.3f), k));
        // In each side room, a seam against its far wall.
        foreach (var m in SideRooms)
        {
            var (at, size) = RoomSpan(m);
            spots.Add((at + new Vector3(-size.X / 2 + 1.4f, 0, -size.Y / 2 + 1.4f), rng.Chance(0.5) ? own : "coal"));
        }
        // And a few along the drifts, against the rock.
        var cells = Enumerable.Range(0, w.Cols).SelectMany(c => Enumerable.Range(0, w.Rows).Select(r => (c, r))).Where(cr => w.RoomAt(cr.c, cr.r) is null).OrderBy(_ => rng.Next(1000)).Take(4);
        var (cw, cd) = (wide / w.Cols, deep / w.Rows);
        foreach (var (c, r) in cells)
        {
            var (dx, dz) = !w.Open(c, r, 1, 0) && c < w.Cols - 1 ? (cw / 2 - 1, 0f) : !w.Open(c, r, -1, 0) && c > 0 ? (-cw / 2 + 1, 0f) : !w.Open(c, r, 0, -1) && r > 0 ? (0f, -cd / 2 + 1) : (0f, cd / 2 - 1);
            spots.Add((CellMiddle(c, r) + new Vector3(dx, 0, dz), rng.Chance(0.6) ? own : rng.Chance(0.5) ? "coal" : "stone"));
        }
        main.IndoorVeins.Clear();
        for (var i = 0; i < spots.Count; i++) main.IndoorVeins.Add(new WorkSpot($"shaft:{seed}:{i}", spots[i].Kind, false, spots[i].At));
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
