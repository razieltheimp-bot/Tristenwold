using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The little pictures of gear in the pack. Each is drawn at once in the item's own colours (a tunic, a helm, a ring,
/// a blade); where the item has a model of its own (a sword, a shield, a helmet, a hammer), that model is then
/// photographed in a small studio of its own, turned to show its shape, and the photograph takes the drawing's place.
/// Each is made once.
/// </summary>
public static class GearIcons
{
    public const int Size = 48;
    static readonly Dictionary<string, Texture2D> Shot = [];
    static readonly Dictionary<string, ImageTexture> Drawn = [];
    static readonly Dictionary<string, List<Action<Texture2D>>> Waiting = [];
    static readonly Queue<Item> Queue = new();
    static SubViewport? _studio;
    static Camera3D? _camera;
    static bool _working;

    /// <summary>The best picture of it there is now; <paramref name="ready"/> is handed a better one once it's been photographed.</summary>
    public static Texture2D For(Item item, Action<Texture2D>? ready = null)
    {
        if (Shot.TryGetValue(item.Id, out var shot)) return shot;
        var drawn = Draw(item);
        if (!CanPhotograph || item.Look is null) return drawn;
        if (!Waiting.TryGetValue(item.Id, out var waiting)) { Waiting[item.Id] = waiting = []; Queue.Enqueue(item); }
        if (ready is not null) waiting.Add(ready);
        if (!_working) _ = Work();
        return drawn;
    }

    /// <summary>With no screen (a headless run) nothing can be photographed, and the drawings stay.</summary>
    static bool CanPhotograph => DisplayServer.GetName() != "headless";

    static async Task Work()
    {
        _working = true;
        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            if (_studio is null || !GodotObject.IsInstanceValid(_studio))
            {
                Studio();
                tree.Root.CallDeferred(Node.MethodName.AddChild, _studio!);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            while (Queue.Count > 0)
            {
                var item = Queue.Dequeue();
                Texture2D picture;
                try { picture = await Photograph(tree, item) ?? Draw(item); }
                catch (Exception e) { GD.PushWarning($"gear icon for {item.Id}: {e.Message}"); picture = Draw(item); }
                Shot[item.Id] = picture;
                if (Waiting.Remove(item.Id, out var waiting)) foreach (var ready in waiting) ready(picture);
            }
        }
        finally { _working = false; }
    }

    /// <summary>A little stage with nothing behind it: warm light from the front, a cool one from behind, and a flat camera.</summary>
    static void Studio()
    {
        _studio = new SubViewport
        {
            Name = "GearIconStudio", OwnWorld3D = true, TransparentBg = true, Size = new Vector2I(Size * 2, Size * 2),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa4X,
        };
        _studio.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#e8dcc8"), AmbientLightEnergy = 0.65f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
        _studio.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, 30, 0), LightEnergy = 1.4f });
        _studio.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10, 200, 0), LightEnergy = 0.5f, LightColor = new Color("#a8c0ff") });
        _studio.AddChild(_camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Far = 200 });
    }

    /// <summary>Takes the item's picture: a long thing (a blade, a staff) laid corner to corner, anything else turned a little to show its face.</summary>
    static async Task<Texture2D?> Photograph(SceneTree tree, Item item)
    {
        if (_studio is null || _camera is null || Wardrobe.Model(item) is not { } model) return null;
        var stand = new Node3D();
        stand.AddChild(model);
        _studio.AddChild(stand);
        var size = Bounds(stand).Size;
        var axes = new[] { (size.X, Vector3.Right), (size.Y, Vector3.Up), (size.Z, Vector3.Back) }.OrderByDescending(a => a.Item1).ToArray();
        if (axes[0].Item1 <= 0.0001f) { stand.QueueFree(); return null; }
        stand.Quaternion = axes[0].Item1 > axes[1].Item1 * 1.6f
            ? new Quaternion(axes[0].Item2, new Vector3(1, 1, 0.25f).Normalized()) // the long way across, corner to corner
            : Quaternion.FromEuler(new Vector3(Mathf.DegToRad(12), Mathf.DegToRad(-25), 0));
        var box = Bounds(stand);
        var centre = box.GetCenter();
        _camera.Size = Math.Max(box.Size.X, box.Size.Y) * 1.15f;
        _camera.Position = centre + new Vector3(0, 0, box.Size.Z + 5);
        _camera.LookAt(centre, Vector3.Up);
        _studio.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = _studio.GetTexture().GetImage();
        stand.QueueFree();
        if (image is null || image.IsEmpty() || image.GetUsedRect().Size == Vector2I.Zero) return null; // nothing came out
        image.Resize(Size, Size, Image.Interpolation.Lanczos);
        return ImageTexture.CreateFromImage(image);
    }

    static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        foreach (var m in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = m.GlobalTransform * m.GetAabb();
            box = box is { } was ? was.Merge(b) : b;
        }
        return box ?? new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One);
    }

    // ---- drawn --------------------------------------------------------------------------------

    /// <summary>A small drawing of the item in its colours: what's shown until (or instead of) a photograph.</summary>
    public static ImageTexture Draw(Item item)
    {
        var key = $"{item.Slot}|{item.Tint}|{item.Metal}|{item.Glow}";
        if (Drawn.TryGetValue(key, out var made)) return made;
        var main = new Color(item.Tint ?? (item.Metal ? "#a8aeb8" : item.Slot is Slot.Ring or Slot.Neck ? "#d8b048" : "#8a6a48"));
        var accent = item.Slot is Slot.Ring or Slot.Neck ? new Color(item.Glow ?? "#c03030") : main.Darkened(0.45f);
        var edge = item.Glow is { } glow ? new Color(glow) : main.Darkened(0.6f);
        var shape = ShapeOf(item.Slot);
        var part = new int[Size, Size];
        for (var x = 0; x < Size; x++)
            for (var y = 0; y < Size; y++)
                part[x, y] = shape((x + 0.5f) / Size, (y + 0.5f) / Size);
        bool Off(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size || part[x, y] == 0;
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (var x = 0; x < Size; x++)
            for (var y = 0; y < Size; y++)
            {
                if (part[x, y] == 0) continue;
                var v = (y + 0.5f) / Size;
                var shine = item.Metal && x < Size * 0.45f && v < 0.45f ? 0.18f : 0;
                var c = Off(x - 1, y) || Off(x + 1, y) || Off(x, y - 1) || Off(x, y + 1) ? edge
                    : (part[x, y] == 2 ? accent : main).Lightened(Math.Max(0, 0.15f - v * 0.3f) + shine).Darkened(Math.Max(0, v - 0.5f) * 0.4f);
                image.SetPixel(x, y, c with { A = 1 });
            }
        return Drawn[key] = ImageTexture.CreateFromImage(image);
    }

    static float Dist(float u, float v, float x, float y) => Mathf.Sqrt((u - x) * (u - x) + (v - y) * (v - y));

    /// <summary>Which part of a drawing a point is in: 0 none, 1 the item, 2 its trim (a belt, a clasp, a gem).</summary>
    static Func<float, float, int> ShapeOf(Slot slot) => slot switch
    {
        Slot.Body => (u, v) => // a tunic: body, sleeves narrowing to the elbow, a cut for the neck, a belt
            Dist(u, v, 0.5f, 0.2f) < 0.1f ? 0
            : v is > 0.62f and < 0.67f && Math.Abs(u - 0.5f) < 0.2f ? 2
            : (v is > 0.22f and < 0.92f && Math.Abs(u - 0.5f) < 0.2f) || (v is > 0.22f and < 0.5f && Math.Abs(u - 0.5f) < 0.38f - (v - 0.22f) * 0.5f) ? 1 : 0,
        Slot.Head => (u, v) => // a helm: a dome over a brim, a slit to see by
            v is > 0.46f and < 0.51f && Math.Abs(u - 0.5f) < 0.2f ? 2
            : (v < 0.64f && Dist(u, v, 0.5f, 0.6f) < 0.33f) || (v is > 0.6f and < 0.69f && Math.Abs(u - 0.5f) < 0.4f) ? 1 : 0,
        Slot.Cloak => (u, v) => // a cloak widening to the hem, clasped at the throat
            Dist(u, v, 0.5f, 0.2f) < 0.06f ? 2
            : v is > 0.15f and < 0.92f && Math.Abs(u - 0.5f) < 0.14f + 0.26f * (v - 0.15f) / 0.77f ? 1 : 0,
        Slot.Ring => (u, v) => // a band and its stone
            Dist(u, v, 0.5f, 0.32f) < 0.09f ? 2 : Dist(u, v, 0.5f, 0.6f) is > 0.17f and < 0.27f ? 1 : 0,
        Slot.Neck => (u, v) => // a chain hanging to a pendant with a stone in it
            Dist(u, v, 0.5f, 0.64f) < 0.07f ? 2
            : Dist(u, v, 0.5f, 0.64f) < 0.13f || (v > 0.16f && Dist(u, v, 0.5f, 0.16f) is > 0.3f and < 0.335f) ? 1 : 0,
        Slot.Offhand => (u, v) => // a kite shield and its boss
            Dist(u, v, 0.5f, 0.42f) < 0.07f ? 2
            : v is > 0.12f and < 0.92f && Math.Abs(u - 0.5f) < (v < 0.55f ? 0.34f : 0.34f * (1 - (v - 0.55f) / 0.37f)) ? 1 : 0,
        _ => (u, v) => // a blade laid corner to corner: grip, guard, blade
        {
            var (ax, ay, bx, by) = (0.2f, 0.82f, 0.82f, 0.18f);
            var (dx, dy) = (bx - ax, by - ay);
            var length = Mathf.Sqrt(dx * dx + dy * dy);
            var t = ((u - ax) * dx + (v - ay) * dy) / (length * length);
            var off = Math.Abs((u - ax) * dy - (v - ay) * dx) / length;
            return t is > 0.28f and < 1f && off < 0.045f * (t > 0.9f ? (1 - t) / 0.1f : 1) ? 1
                : t is > 0.23f and < 0.29f && off < 0.14f ? 2
                : t is > 0.04f and < 0.24f && off < 0.03f ? 2 : 0;
        },
    };
}
