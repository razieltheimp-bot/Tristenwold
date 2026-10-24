using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The company's map: dark where nobody has been, drawn in as the company explores. Places
/// they have found are named; places they have only heard of show a "?". The same control is
/// the corner minimap and, larger, the full map on M (scroll to zoom, drag to pan).
/// </summary>
public partial class MapView : Control
{
    public required WorldGen World { get; init; }
    public required ExploredMap Explored { get; init; }
    public required Func<Vector3> Player { get; init; }
    public required Func<Vector3> Facing { get; init; }
    public Func<IEnumerable<(double X, double Z)>> Goals { get; init; } = () => [];
    public bool Mini { get; init; }

    /// <summary>Metres per pixel.</summary>
    public float MetresPerPixel { get; set; } = 4;
    Vector2 _pan;
    bool _dragging;

    readonly Dictionary<(int, int), Image> _images = [];
    readonly Dictionary<(int, int), ImageTexture> _textures = [];
    readonly HashSet<(int, int)> _dirty = [];
    double _sinceUpload;

    static readonly Color Fog = new("#15130f");
    static readonly Color Ink = new("#e8dcc4");

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = Mini ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
    }

    /// <summary>Colours in cells the company has just seen.</summary>
    public void Revealed(IEnumerable<(int Cx, int Cz)> cells)
    {
        foreach (var (cx, cz) in cells)
        {
            var tile = TileOf(cx, cz);
            if (!_images.TryGetValue(tile, out var image)) continue; // painted whole when first drawn
            image.SetPixel(cx - tile.Item1 * ExploredMap.TileCells, cz - tile.Item2 * ExploredMap.TileCells, CellColour(cx, cz));
            _dirty.Add(tile);
        }
    }

    public void Recentre() => _pan = Vector2.Zero;

    public override void _Process(double delta)
    {
        _sinceUpload += delta;
        if (_sinceUpload > 0.5)
        {
            _sinceUpload = 0;
            foreach (var tile in _dirty)
                if (_textures.TryGetValue(tile, out var tex)) tex.Update(_images[tile]);
            _dirty.Clear();
        }
        if (IsVisibleInTree()) QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Mini) return;
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }: MetresPerPixel = Mathf.Max(0.5f, MetresPerPixel / 1.25f); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }: MetresPerPixel = Mathf.Min(64, MetresPerPixel * 1.25f); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b: _dragging = b.Pressed; break;
            case InputEventMouseMotion m when _dragging: _pan -= m.Relative * MetresPerPixel; break;
        }
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Fog);
        var player = Player();
        var centre = new Vector2(player.X, player.Z) + _pan;
        Vector2 ToScreen(double x, double z) => new Vector2((float)x - centre.X, (float)z - centre.Y) / MetresPerPixel + size / 2;

        // The explored land, tile by tile.
        var tileMetres = (float)(ExploredMap.TileCells * ExploredMap.Cell);
        var half = size / 2 * MetresPerPixel;
        var (tx0, tz0) = ((int)Mathf.Floor((centre.X - half.X) / tileMetres), (int)Mathf.Floor((centre.Y - half.Y) / tileMetres));
        var (tx1, tz1) = ((int)Mathf.Floor((centre.X + half.X) / tileMetres), (int)Mathf.Floor((centre.Y + half.Y) / tileMetres));
        var known = Explored.Tiles.ToHashSet();
        for (var tx = tx0; tx <= tx1; tx++)
            for (var tz = tz0; tz <= tz1; tz++)
            {
                if (!known.Contains((tx, tz))) continue;
                var tl = ToScreen(tx * tileMetres, tz * tileMetres);
                DrawTextureRect(Texture((tx, tz)), new Rect2(tl, Vector2.One * tileMetres / MetresPerPixel), false);
            }

        // Places found, and places only heard of.
        var font = ThemeDB.FallbackFont;
        foreach (var id in Explored.Discovered.Concat(Explored.Rumored.Except(Explored.Discovered)))
        {
            if (World.Find(id) is not { } s) continue;
            var p = ToScreen(s.X, s.Z);
            if (p.X < -50 || p.Y < -20 || p.X > size.X + 50 || p.Y > size.Y + 20) continue;
            var heard = !Explored.Discovered.Contains(id);
            var colour = heard ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : s.Hostile ? new Color("#d05a40") : s.IsCity ? new Color("#ffd27a") : Ink;
            var r = s.IsCity ? 6f : 3.5f;
            if (s.Hostile) { DrawLine(p - new Vector2(r, r), p + new Vector2(r, r), colour, 2); DrawLine(p + new Vector2(-r, r), p + new Vector2(r, -r), colour, 2); }
            else if (s.IsCity) DrawRect(new Rect2(p - Vector2.One * r, Vector2.One * r * 2), colour);
            else DrawCircle(p, r, colour);
            if (Mini && !s.IsCity) continue;
            var label = heard ? $"{s.Name} ?" : s.Name;
            DrawString(font, p + new Vector2(r + 3, 4), label, HorizontalAlignment.Left, -1, s.IsCity ? 15 : 12, colour);
        }

        foreach (var (x, z) in Goals())
        {
            var p = ToScreen(x, z);
            p = new Vector2(Mathf.Clamp(p.X, 6, size.X - 6), Mathf.Clamp(p.Y, 6, size.Y - 6)); // pinned to the edge if off the map
            DrawColoredPolygon([p + new Vector2(0, -7), p + new Vector2(6, 0), p + new Vector2(0, 7), p + new Vector2(-6, 0)], new Color("#ffcc33"));
        }

        // You, as an arrow.
        var me = ToScreen(player.X, player.Z);
        var f = Facing();
        var dir = new Vector2(f.X, f.Z).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Up;
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon([me + dir * 9, me - dir * 6 + side * 6, me - dir * 3, me - dir * 6 - side * 6], Colors.White);

        if (Mini) DrawRect(new Rect2(Vector2.Zero, size), new Color(Ink, 0.5f), false, 2);
        else DrawString(font, new Vector2(16, 28), $"North is up. {MetresPerPixel:0.#} m per pixel. Scroll to zoom, drag to pan, M to close.", HorizontalAlignment.Left, -1, 14, Ink);
    }

    ImageTexture Texture((int, int) tile)
    {
        if (_textures.TryGetValue(tile, out var tex)) return tex;
        var image = Image.CreateEmpty(ExploredMap.TileCells, ExploredMap.TileCells, false, Image.Format.Rgba8);
        for (var bx = 0; bx < ExploredMap.TileCells; bx++)
            for (var bz = 0; bz < ExploredMap.TileCells; bz++)
            {
                var (cx, cz) = (tile.Item1 * ExploredMap.TileCells + bx, tile.Item2 * ExploredMap.TileCells + bz);
                image.SetPixel(bx, bz, Explored.IsRevealedCell(cx, cz) ? CellColour(cx, cz) : Colors.Transparent);
            }
        _images[tile] = image;
        return _textures[tile] = ImageTexture.CreateFromImage(image);
    }

    Color CellColour(int cx, int cz) => Palette.Map(World, (cx + 0.5) * ExploredMap.Cell, (cz + 0.5) * ExploredMap.Cell);

    static (int, int) TileOf(int cx, int cz) =>
        ((int)Mathf.Floor(cx / (float)ExploredMap.TileCells), (int)Mathf.Floor(cz / (float)ExploredMap.TileCells));
}
