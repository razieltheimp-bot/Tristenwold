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
///
/// Players who've gone are here too, asleep where they left (see <see cref="Sleepers"/>): standing about, shifting a
/// few steps this way and that the way townsfolk do, "(asleep)" by their name. Anyone ready to fight (B) can go for
/// them; the server counts the blows. One cut down lies where they fell until their player comes back.
/// </summary>
public partial class Visitors : Node3D
{
    const double Stale = 40;
    readonly Dictionary<string, Visitor> _here = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Visitor> _asleep = new(StringComparer.OrdinalIgnoreCase);
    double _clock;

    /// <summary>How far a sleeper wanders from where they were left: a few feet each way.</summary>
    public const float SleeperStray = 1.3f;

    public int AsleepCount => _asleep.Count;

    public int Count => _here.Count;

    /// <summary>Where a player was last seen, if they're about.</summary>
    public Vector3? Where(string player) => _here.TryGetValue(player, out var v) ? v.Target : null;

    /// <summary>Whether a player's hero is about and in the shadows (for the self-test).</summary>
    public bool HiddenForTest(string player) => _here.TryGetValue(player, out var v) && v.Hidden && v.GetChildren().OfType<Body>().All(b => !b.Visible);

    /// <summary>The stand-in for a player's hero, if they're about, or asleep about and there to be struck.</summary>
    public Enemy? StandIn(string player) =>
        _here.TryGetValue(player, out var v) && IsInstanceValid(v.Stand) ? v.Stand
        : _asleep.TryGetValue(player, out var s) && s.Strikable && IsInstanceValid(s.Stand) ? s.Stand : null;

    /// <summary>Every player about, and every sleeper there to be struck, as stand-ins.</summary>
    public IEnumerable<Enemy> StandIns => _here.Values.Where(v => !v.Hidden).Concat(_asleep.Values.Where(v => v.Strikable)).Select(v => v.Stand).Where(IsInstanceValid); // a hero in the shadows can't be picked out to strike

    /// <summary>What a player's hero is called, if they're about (awake or asleep).</summary>
    public string? NameOf(string player) =>
        StandIn(player)?.Stats.Name ?? (_asleep.TryGetValue(player, out var s) && IsInstanceValid(s.Stand) ? s.Stand.Stats.Name : null);

    /// <summary>Which players this hero is fighting: their names show red.</summary>
    public Func<string, bool>? Fighting { get; set; }

    /// <summary>A player's hero takes a blow from this one: they flinch (their own game decides the rest, or the server for a sleeper).</summary>
    public void Flinch(string player) { if (_here.TryGetValue(player, out var v) || _asleep.TryGetValue(player, out v)) v.Flinch(); }

    /// <summary>Where a sleeper stands now, if there's one by that name (for the self-test).</summary>
    public Vector3? SleeperAt(string player) => _asleep.TryGetValue(player, out var v) ? v.Position : null;

    /// <summary>What's written over a sleeper (for the self-test).</summary>
    public string? SleeperPlate(string player) => _asleep.TryGetValue(player, out var v) ? v.PlateText : null;

    /// <summary>
    /// The server's word on who's asleep where: new ones are left standing there, ones gone (back in the game) go, and
    /// the rest take any change (a blow, a fall). <paramref name="self"/> is never drawn: this hero's awake.
    /// </summary>
    public void Sleepers(IReadOnlyList<SleeperView> all, string? self = null)
    {
        var now = all.Where(s => !s.Player.Equals(self, StringComparison.OrdinalIgnoreCase)).ToList();
        var keep = now.Select(s => s.Player).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (player, v) in _asleep.ToList())
            if (!keep.Contains(player)) { _asleep.Remove(player); v.QueueFree(); }
        foreach (var s in now)
        {
            if (!_asleep.TryGetValue(s.Player, out var v))
            {
                v = new Visitor { Name = $"Sleeper_{s.Player}", Player = s.Player, Asleep = true };
                _asleep[s.Player] = v;
                AddChild(v);
                v.Position = new Vector3(s.X, s.Y, s.Z);
                v.Rotation = new Vector3(0, s.Yaw, 0);
            }
            v.Sleep(s, _clock);
        }
    }

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
        foreach (var (player, v) in _asleep)
        {
            v.Drift(delta, _clock);
            v.Mark(v.Strikable && Fighting?.Invoke(player) == true);
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

        /// <summary>Gone from the game, and left standing where they were.</summary>
        public bool Asleep;
        bool _fallen, _safe;
        double _health = 1;
        string _name = "";
        /// <summary>Where they were left, the next spot they're shuffling to, and when they'll move again.</summary>
        Vector3 _home, _toward;
        double _stillUntil;
        readonly Random _dice = new();

        /// <summary>Asleep, still standing, and not in a tavern: there to be struck.</summary>
        public bool Strikable => Asleep && !_fallen && !_safe;

        public string PlateText => _plate?.Text ?? "";

        /// <summary>The server's word on this sleeper: how they look, where they were left, how they're faring.</summary>
        public void Sleep(SleeperView s, double now)
        {
            var first = _figure is null;
            Take(new Moved(s.Look, s.Avatar, s.Name, s.X, s.Y, s.Z, s.Yaw, 0, null, 0), now);
            _name = s.Name;
            _safe = s.Safe;
            var home = new Vector3(s.X, s.Y, s.Z);
            if (first || home.DistanceTo(_home) > 0.5f) { _home = _toward = home; Position = home; }
            if (s.Health < _health - 1e-6 && !first) Flinch();
            _health = s.Health;
            if (s.Fallen && !_fallen) { _figure?.Die(); _toward = Position; }
            else if (!s.Fallen && _fallen) _figure?.Rise();
            _fallen = s.Fallen;
            if (_plate is not null)
                _plate.Text = _fallen ? $"{_name} (cut down)" : _health < 0.999 ? $"{_name} (asleep, {_health:P0})" : $"{_name} (asleep)";
        }

        /// <summary>A sleeper stands about, now and then shuffling a step or two to somewhere near where they were left.</summary>
        public void Drift(double delta, double now)
        {
            if (_fallen) return;
            var gap = _toward - Position;
            gap.Y = 0;
            var far = gap.Length();
            const float Pace = 0.7f;
            if (far > 0.05f)
            {
                var step = Math.Min(far, Pace * (float)delta);
                Position += gap / far * step;
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(gap.X, gap.Z), (float)Math.Min(1, delta * 6)), 0);
                _figure?.Move(Pace, true, false);
                if (far - step <= 0.05f) _stillUntil = now + 3 + _dice.NextDouble() * 6;
                return;
            }
            _figure?.Move(0, true, false);
            if (now < _stillUntil) return;
            var angle = _dice.NextDouble() * Math.Tau;
            var reach = SleeperStray * (0.3 + 0.7 * _dice.NextDouble());
            _toward = _home + new Vector3((float)(Math.Sin(angle) * reach), 0, (float)(Math.Cos(angle) * reach));
            _stillUntil = double.MaxValue; // until they get there
        }

        public void Take(Moved m, double now)
        {
            Heard = now;
            if (!IsInstanceValid(Stand)) AddChild(Stand = Enemy.ForHero(Player, m.Name));
            if (Stand.Stats.Name != m.Name) Stand.Stats.Name = m.Name;
            if (_figure is null || m.Look != _look || (m.Avatar ?? "") != _avatar) Dress(m);
            Target = new Vector3(m.X, m.Y, m.Z);
            _yaw = m.Yaw;
            _speed = m.Speed;
            if (_plate is not null && _plate.Text != m.Name && !Asleep) _plate.Text = m.Name;
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
                Text = Asleep ? $"{m.Name} (asleep)" : m.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 10, PixelSize = 0.008f,
                NoDepthTest = true, RenderPriority = 10, Modulate = new Color(Asleep ? "#b8c4d0" : "#dff4ff"),
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
            _plate.Modulate = new Color(fighting ? "#ff6a5a" : Asleep ? "#b8c4d0" : "#dff4ff");
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
