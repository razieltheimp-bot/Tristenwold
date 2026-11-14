using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Everyone's name, drawn flat on the screen at one small size however near or far they stand, at their chest: the name,
/// then what they are (their level and calling) smaller under it, and a thin health bar when they're hurt or fighting.
/// Where two plates would print over each other the nearer one stays and the others fade back; one right up against the
/// camera fades out altogether (you can see who it is), and so does one behind a wall or out past where names carry.
/// <para>
/// The words come from each character's own <see cref="Label3D"/> (in the <see cref="Group"/> group), which carries the
/// text, colour and whether it's shown, as it always has; only the drawing moved here, so the labels themselves are kept
/// out of every camera's sight.
/// </para>
/// </summary>
public partial class Nameplates : Control
{
    public const string Group = "nameplate";

    /// <summary>Marks a character's label as a nameplate to draw here.</summary>
    public static void Adopt(Label3D label)
    {
        label.AddToGroup(Group);
        label.Layers = 0; // drawn here, not in the world
    }

    const float NameSize = 13, SubSize = 10, BarHeight = 3, Near = 2.2f, NearFade = 1.2f, Faded = 0.28f;
    const double LookAgain = 0.2; // seconds between checks for a wall in the way

    sealed class Seen
    {
        public float Alpha;
        public double NextLook;
        public bool Hidden;
    }

    readonly Dictionary<ulong, Seen> _seen = [];
    readonly List<Shown> _shown = [];

    /// <summary>A plate as drawn this frame.</summary>
    public readonly record struct Shown(Label3D Label, Rect2 Box, Vector2 At, string Name, string Sub, Color Colour, float? Health, float Alpha, float Distance);

    /// <summary>The plates drawn this frame, nearest first (for the HUD to keep out of their way, and for the self-test).</summary>
    public IReadOnlyList<Shown> Plates => _shown;

    /// <summary>Whether any plate on screen just now overlaps this box.</summary>
    public bool Covers(Rect2 box) => _shown.Any(s => s.Alpha > 0.05f && s.Box.Intersects(box));

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        _shown.Clear();
        var camera = GetViewport().GetCamera3D();
        if (camera is null || !Visible) { QueueRedraw(); return; }
        var eye = camera.GlobalPosition;
        var now = Time.GetTicksMsec() / 1000.0;
        var space = camera.GetWorld3D().DirectSpaceState;
        var font = ThemeDB.FallbackFont;
        var screen = GetViewportRect();
        var live = new HashSet<ulong>();
        var wanted = new List<(Label3D Label, Vector2 At, float Distance, Seen Seen, float Fade, string Name, string Sub, float? Health)>();

        foreach (var node in GetTree().GetNodesInGroup(Group))
        {
            if (node is not Label3D label || !IsInstanceValid(label) || !label.IsInsideTree()) continue;
            if (label.Layers != 0) label.Layers = 0;
            var id = label.GetInstanceId();
            live.Add(id);
            if (!_seen.TryGetValue(id, out var seen)) _seen[id] = seen = new Seen();
            var at = label.GlobalPosition;
            var distance = eye.DistanceTo(at);
            var fade = 1f;
            var text = label.Text;
            var reach = label.NoDepthTest ? 70f : 42f; // the hero's own company and other heroes carry further
            if (!label.IsVisibleInTree() || text.Length == 0 || camera.IsPositionBehind(at) || distance > reach) fade = 0;
            else
            {
                fade *= Mathf.Clamp((distance - Near) / NearFade, 0, 1); // right up close: you can see who it is
                fade *= Mathf.Clamp((reach - distance) / 8, 0, 1); // out past where a name carries
                if (!label.NoDepthTest)
                {
                    if (now >= seen.NextLook)
                    {
                        seen.NextLook = now + LookAgain + GD.Randf() * 0.05;
                        seen.Hidden = Walled(space, eye, at, distance, Owners(label, camera));
                    }
                    if (seen.Hidden) fade = 0;
                }
            }
            var lines = text.Split('\n');
            var health = HealthOf(label);
            wanted.Add((label, camera.UnprojectPosition(at), distance, seen, fade, lines[0], string.Join("   ", lines.Skip(1).Where(l => l.Length > 0)), health));
        }
        foreach (var gone in _seen.Keys.Where(k => !live.Contains(k)).ToList()) _seen.Remove(gone);

        // Nearest first: a plate keeps its full strength unless a nearer one is already printed where it would go.
        var step = (float)delta * 5;
        foreach (var w in wanted.OrderBy(w => w.Distance))
        {
            var box = Box(font, w.At, w.Name, w.Sub, w.Health is not null);
            var target = w.Fade;
            if (target > 0 && _shown.Any(s => s.Alpha > 0.5f && s.Box.Grow(-1).Intersects(box))) target = Mathf.Min(target, Faded);
            if (!screen.Grow(40).Intersects(box)) target = 0;
            w.Seen.Alpha = Mathf.MoveToward(w.Seen.Alpha, target, step);
            if (w.Seen.Alpha <= 0.01f) continue;
            _shown.Add(new Shown(w.Label, box, w.At, w.Name, w.Sub, w.Label.Modulate, w.Health, w.Seen.Alpha, w.Distance));
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // Farthest drawn first, so the nearest is on top.
        for (var i = _shown.Count - 1; i >= 0; i--)
        {
            var s = _shown[i];
            var ink = s.Colour with { A = s.Colour.A * s.Alpha };
            var dark = new Color(0, 0, 0, 0.85f * s.Alpha);
            var y = s.Box.Position.Y;
            var nameSize = font.GetStringSize(s.Name, HorizontalAlignment.Left, -1, (int)NameSize);
            var at = new Vector2(s.At.X - nameSize.X / 2, y + font.GetAscent((int)NameSize));
            DrawStringOutline(font, at, s.Name, HorizontalAlignment.Left, -1, (int)NameSize, 4, dark);
            DrawString(font, at, s.Name, HorizontalAlignment.Left, -1, (int)NameSize, ink);
            y += font.GetHeight((int)NameSize);
            if (s.Sub.Length > 0)
            {
                var subSize = font.GetStringSize(s.Sub, HorizontalAlignment.Left, -1, (int)SubSize);
                var sat = new Vector2(s.At.X - subSize.X / 2, y + font.GetAscent((int)SubSize));
                var soft = ink.Lerp(new Color(1, 1, 1, ink.A), 0.35f);
                DrawStringOutline(font, sat, s.Sub, HorizontalAlignment.Left, -1, (int)SubSize, 3, dark);
                DrawString(font, sat, s.Sub, HorizontalAlignment.Left, -1, (int)SubSize, soft);
                y += font.GetHeight((int)SubSize);
            }
            if (s.Health is { } h)
            {
                var width = Mathf.Clamp(nameSize.X, 40, 70);
                var bar = new Rect2(s.At.X - width / 2, y + 2, width, BarHeight);
                DrawRect(bar.Grow(1), dark);
                DrawRect(bar, new Color(0.25f, 0.08f, 0.06f, 0.9f * s.Alpha));
                DrawRect(bar with { Size = bar.Size with { X = width * Mathf.Clamp(h, 0, 1) } }, new Color(h > 0.5f ? "#7ad06a" : h > 0.25f ? "#e0c050" : "#e0503a") with { A = s.Alpha });
            }
        }
    }

    /// <summary>Where a plate goes on the screen: centred on the chest, the name over what they are, the bar under both.</summary>
    static Rect2 Box(Font font, Vector2 at, string name, string sub, bool bar)
    {
        var nameSize = font.GetStringSize(name, HorizontalAlignment.Left, -1, (int)NameSize);
        var width = nameSize.X;
        var height = font.GetHeight((int)NameSize);
        if (sub.Length > 0)
        {
            width = Mathf.Max(width, font.GetStringSize(sub, HorizontalAlignment.Left, -1, (int)SubSize).X);
            height += font.GetHeight((int)SubSize);
        }
        if (bar) height += BarHeight + 3;
        return new Rect2(at.X - width / 2, at.Y - height / 2, width, height);
    }

    /// <summary>The thin bar, for a fighter that's hurt or in a fight; none for anyone else.</summary>
    static float? HealthOf(Label3D label)
    {
        if (label.GetParent() is not Fighter f || f is Player) return null;
        var share = (float)(f.Stats.Health / Math.Max(1, f.Stats.MaxHealth));
        var fighting = f is Enemy { State: EnemyState.Fighting };
        if (f is Enemy { State: EnemyState.Dead }) return null;
        return share < 0.995f || fighting ? share : null;
    }

    /// <summary>Whether a wall, a rock or a building is between the camera and a plate (other people don't hide a name: it fades back instead).</summary>
    static bool Walled(PhysicsDirectSpaceState3D space, Vector3 eye, Vector3 at, float distance, Godot.Collections.Array<Rid> skip)
    {
        for (var tries = 0; tries < 4; tries++)
        {
            var ray = PhysicsRayQueryParameters3D.Create(eye, at);
            ray.Exclude = skip;
            var hit = space.IntersectRay(ray);
            if (hit.Count == 0 || hit["position"].AsVector3().DistanceTo(eye) >= distance - 0.6f) return false;
            if (hit["collider"].As<GodotObject>() is not CharacterBody3D body) return true;
            skip.Add(body.GetRid()); // someone standing in the way: look past them
        }
        return false;
    }

    /// <summary>What a look from the camera to a plate shouldn't count as being in the way: the one wearing it, and the hero.</summary>
    static Godot.Collections.Array<Rid> Owners(Label3D label, Camera3D camera)
    {
        var skip = new Godot.Collections.Array<Rid>();
        for (Node? n = label.GetParent(); n is not null; n = n.GetParent())
            if (n is CollisionObject3D body) { skip.Add(body.GetRid()); break; }
        if (camera.GetTree().GetFirstNodeInGroup("player") is CollisionObject3D hero) skip.Add(hero.GetRid());
        return skip;
    }
}
