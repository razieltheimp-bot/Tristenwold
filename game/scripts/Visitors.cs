using Godot;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// The other players on the shared server, walking about this game's world. Each is drawn as their own hero
/// (class look and character page choices) from what the server last said of them, eased toward where they
/// were, playing the moves they made; a name floats over them. Nothing here blocks: they're company, not
/// obstacles. Each has an unseen stand-in (<see cref="Enemy.ForHero"/>) where they're drawn, for this hero's blows
/// to land on when the two are fighting; their name goes red then. A player not heard of for a while, or gone from
/// the roster, fades out.
/// </summary>
public partial class Visitors : Node3D
{
    const double Stale = 40;
    readonly Dictionary<string, Visitor> _here = new(StringComparer.OrdinalIgnoreCase);
    double _clock;

    public int Count => _here.Count;

    /// <summary>Where a player was last seen, if they're about.</summary>
    public Vector3? Where(string player) => _here.TryGetValue(player, out var v) ? v.Target : null;

    /// <summary>Whether a player's hero is about and in the shadows (for the self-test).</summary>
    public bool HiddenForTest(string player) => _here.TryGetValue(player, out var v) && v.Hidden && v.GetChildren().OfType<Body>().All(b => !b.Visible);

    /// <summary>The stand-in for a player's hero, if they're about.</summary>
    public Enemy? StandIn(string player) => _here.TryGetValue(player, out var v) && IsInstanceValid(v.Stand) ? v.Stand : null;

    /// <summary>Every player about, as stand-ins.</summary>
    public IEnumerable<Enemy> StandIns => _here.Values.Where(v => !v.Hidden).Select(v => v.Stand).Where(IsInstanceValid); // a hero in the shadows can't be picked out to strike

    /// <summary>What a player's hero is called, if they're about.</summary>
    public string? NameOf(string player) => StandIn(player)?.Stats.Name;

    /// <summary>Which players this hero is fighting: their names show red.</summary>
    public Func<string, bool>? Fighting { get; set; }

    /// <summary>A player's hero takes a blow from this one: they flinch (their own game decides the rest).</summary>
    public void Flinch(string player) { if (_here.TryGetValue(player, out var v)) v.Flinch(); }

    public void Seen(string player, Moved where)
    {
        if (!_here.TryGetValue(player, out var v))
        {
            v = new Visitor { Name = $"Visitor_{player}", Player = player };
            _here[player] = v;
            AddChild(v);
            v.Position = new Vector3(where.X, where.Y, where.Z);
            v.Rotation = new Vector3(0, where.Yaw, 0);
        }
        v.Take(where, _clock);
    }

    /// <summary>The roster says who's online: anyone else is gone.</summary>
    public void Roster(IReadOnlyList<GroupView> groups)
    {
        var online = groups.Where(g => g.Online).Select(g => g.Player).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (player, v) in _here.ToList())
            if (!online.Contains(player)) { _here.Remove(player); v.QueueFree(); }
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        foreach (var (player, v) in _here.ToList())
        {
            if (_clock - v.Heard > Stale) { _here.Remove(player); v.QueueFree(); continue; }
            v.Ease(delta);
            v.Mark(Fighting?.Invoke(player) == true);
        }
    }

    sealed partial class Visitor : Node3D
    {
        Body? _figure;
        Label3D? _plate;
        string _look = "", _avatar = "";
        int _beat = -1;
        float _yaw, _speed;
        public Vector3 Target;
        public double Heard;
        public string Player = "";
        public Enemy Stand = null!;
        bool _marked;
        /// <summary>In the shadows: not drawn, no name over them, and not there to be struck.</summary>
        public bool Hidden;

        public void Take(Moved m, double now)
        {
            Heard = now;
            if (!IsInstanceValid(Stand)) AddChild(Stand = Enemy.ForHero(Player, m.Name));
            if (Stand.Stats.Name != m.Name) Stand.Stats.Name = m.Name;
            if (_figure is null || m.Look != _look || (m.Avatar ?? "") != _avatar) Dress(m);
            Target = new Vector3(m.X, m.Y, m.Z);
            _yaw = m.Yaw;
            _speed = m.Speed;
            if (_plate is not null && _plate.Text != m.Name) _plate.Text = m.Name;
            Hidden = m.Hidden;
            if (_figure is not null) _figure.Visible = !Hidden;
            if (_plate is not null) _plate.Visible = !Hidden;
            if (m.Clip is { } clip && m.Beat != _beat)
            {
                if (_beat >= 0 && _figure is not null)
                    switch (clip)
                    {
                        case "Attack": _figure.Attack(); break;
                        case "Attack!": _figure.Attack(true); break;
                        case "Cast": _figure.Cast(); break;
                        default: _figure.Perform(clip); break;
                    }
                _beat = m.Beat;
            }
        }

        void Dress(Moved m)
        {
            _figure?.QueueFree();
            _plate?.QueueFree();
            _look = m.Look;
            _avatar = m.Avatar ?? "";
            _figure = Figure.Create(m.Look, Tristinwold.Gameplay.Avatar.Parse(m.Avatar, m.Look));
            AddChild(_figure);
            var top = Models.Bounds(_figure).End.Y;
            _marked = false;
            _plate = new Label3D
            {
                Text = m.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 10, PixelSize = 0.008f,
                NoDepthTest = true, RenderPriority = 10, Modulate = new Color("#dff4ff"),
                Position = new Vector3(0, Math.Max(1.6f, top) + 0.45f, 0),
            };
            AddChild(_plate);
            _figure.Visible = _plate.Visible = !m.Hidden;
        }

        public void Flinch() => _figure?.Hurt();

        /// <summary>Their name red while the two heroes are fighting, pale blue otherwise.</summary>
        public void Mark(bool fighting)
        {
            if (_plate is null || fighting == _marked) return;
            _marked = fighting;
            _plate.Modulate = new Color(fighting ? "#ff6a5a" : "#dff4ff");
        }

        public void Ease(double delta)
        {
            var gap = Target - Position;
            var far = gap.Length();
            float moved;
            if (far > 25) { Position = Target; moved = 0; } // too far behind to catch up: just be there
            else if (far > 0.02f)
            {
                var pace = Math.Max(_speed, far * 3); // their own speed, or faster to close a gap
                var step = Math.Min(far, pace * (float)delta);
                Position += gap / far * step;
                moved = step / (float)Math.Max(delta, 1e-4);
            }
            else moved = 0;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _yaw, (float)Math.Min(1, delta * 10)), 0);
            _figure?.Move(Math.Min(moved, Math.Max(_speed, 0.5f) * 1.2f) is var pace2 && far > 0.05f ? Math.Max(pace2, 0.6f) : 0, true, false);
        }
    }
}
