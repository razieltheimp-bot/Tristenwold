using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Playing on a touch screen: a joystick wherever your left thumb lands, drag anywhere on the
/// right to look, and buttons under the right thumb. Every finger is tracked on its own, so you
/// can run, look and strike at once. The buttons press the same actions as the keys, so the
/// rest of the game doesn't know the difference.
/// </summary>
public partial class TouchControls : Control
{
    sealed record Pad(string Action, string Label, Func<Vector2, Vector2> Centre, float Radius, bool Toggle = false);

    public Action<Vector2>? Look { get; set; }
    public Func<string, string?>? LabelFor { get; set; } // lets the game relabel a button (the spell's element, say)
    public Func<string, bool>? Lit { get; set; }        // glowing buttons: limit ready, something to talk to

    const float StickRadius = 90, LookSpeed = 0.006f;

    readonly List<Pad> _pads =
    [
        new("attack", "Strike", s => new(s.X - 130, s.Y - 130), 64),
        new("spell", "Spell", s => new(s.X - 270, s.Y - 92), 46),
        new("limit", "Limit", s => new(s.X - 240, s.Y - 238), 40),
        new("jump", "Jump", s => new(s.X - 105, s.Y - 285), 40),
        new("interact", "Use", s => new(s.X - 380, s.Y - 190), 42),
        new("cycle_element", "Fire", s => new(s.X - 395, s.Y - 70), 32),
        new("sprint", "Run", s => new(300, s.Y - 70), 36, Toggle: true),
        new("map", "Map", s => new(s.X / 2 - 165, 34), 28),
        new("journal", "Quests", s => new(s.X / 2 - 55, 34), 28),
        new("menu", "World", s => new(s.X / 2 + 55, 34), 28),
        new("rest", "Rest", s => new(s.X / 2 + 165, 34), 28),
        new("party", "Party", s => new(s.X / 2 + 275, 34), 28),
        new("skills", "Skills", s => new(s.X / 2 - 275, 34), 28),
        new("ride", "Ride", s => new(s.X - 395, s.Y - 300), 32),
        new("descend", "Dive", s => new(s.X - 105, s.Y - 375), 30),
        new("stealth", "Hide", s => new(s.X - 250, s.Y - 360), 30),
        new("game_menu", "Menu", s => new(s.X - 124, 258), 28), // under the little map
    ];

    // Which finger is doing what.
    int _stickFinger = -1, _lookFinger = -1;
    Vector2 _stickBase, _stickAt;
    readonly Dictionary<int, Pad> _held = [];
    readonly HashSet<string> _toggled = [];

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // touches are read in _Input; taps still reach menus as mouse clicks
    }

    /// <summary>The visible screen. (Size isn't kept up to date while the controls are hidden.)</summary>
    public Vector2 Screen => GetViewportRect().Size;

    /// <summary>Where a button is drawn, in this control's coordinates.</summary>
    public Vector2 CentreOf(string action) => _pads.First(p => p.Action == action).Centre(Screen);

    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree()) return;
        switch (MakeInputLocal(e))
        {
            case InputEventScreenTouch { Pressed: true } t: Down(t.Index, t.Position); break;
            case InputEventScreenTouch { Pressed: false } t: Up(t.Index); break;
            case InputEventScreenDrag d: Drag(d.Index, d.Position, d.Relative); break;
        }
    }

    void Down(int finger, Vector2 at)
    {
        if (_pads.FirstOrDefault(p => p.Centre(Screen).DistanceTo(at) <= p.Radius * 1.15f) is { } pad)
        {
            if (pad.Toggle)
            {
                if (!_toggled.Remove(pad.Action)) { _toggled.Add(pad.Action); Input.ActionPress(pad.Action); }
                else Input.ActionRelease(pad.Action);
            }
            else
            {
                _held[finger] = pad;
                Input.ActionPress(pad.Action);
            }
        }
        else if (at.X < Screen.X * 0.4f && at.Y > Screen.Y * 0.35f && _stickFinger < 0)
        {
            _stickFinger = finger;
            _stickBase = _stickAt = at;
        }
        else if (_lookFinger < 0) _lookFinger = finger;
        QueueRedraw();
    }

    void Drag(int finger, Vector2 at, Vector2 relative)
    {
        if (finger == _stickFinger)
        {
            _stickAt = _stickBase + (at - _stickBase).LimitLength(StickRadius);
            var v = (_stickAt - _stickBase) / StickRadius;
            Axis("move_right", "move_left", v.X);
            Axis("move_back", "move_forward", v.Y);
            QueueRedraw();
        }
        else if (finger == _lookFinger) Look?.Invoke(relative * LookSpeed);
    }

    void Up(int finger)
    {
        if (finger == _stickFinger)
        {
            _stickFinger = -1;
            foreach (var a in new[] { "move_left", "move_right", "move_forward", "move_back" }) Input.ActionRelease(a);
        }
        if (finger == _lookFinger) _lookFinger = -1;
        if (_held.Remove(finger, out var pad)) Input.ActionRelease(pad.Action);
        QueueRedraw();
    }

    static void Axis(string positive, string negative, float value)
    {
        if (value > 0.15f) { Input.ActionPress(positive, value); Input.ActionRelease(negative); }
        else if (value < -0.15f) { Input.ActionPress(negative, -value); Input.ActionRelease(positive); }
        else { Input.ActionRelease(positive); Input.ActionRelease(negative); }
    }

    /// <summary>Lets go of everything, when a menu opens over the controls.</summary>
    public void ReleaseAll()
    {
        foreach (var finger in _held.Keys.ToList()) Up(finger);
        if (_stickFinger >= 0) Up(_stickFinger);
        _lookFinger = -1;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var ink = new Color(1, 1, 1, 0.85f);
        if (_stickFinger >= 0)
        {
            DrawCircle(_stickBase, StickRadius, new Color(1, 1, 1, 0.12f));
            DrawArc(_stickBase, StickRadius, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.4f), 2);
            DrawCircle(_stickAt, 34, new Color(1, 1, 1, 0.45f));
        }
        else DrawString(font, new Vector2(60, Screen.Y - 150), "move: touch and drag here", HorizontalAlignment.Left, -1, 16, new Color(1, 1, 1, 0.35f));

        foreach (var pad in _pads)
        {
            var c = pad.Centre(Screen);
            var on = _held.ContainsValue(pad) || _toggled.Contains(pad.Action);
            var lit = Lit?.Invoke(pad.Action) ?? false;
            DrawCircle(c, pad.Radius, on ? new Color(1, 0.82f, 0.48f, 0.55f) : lit ? new Color(1, 0.82f, 0.48f, 0.3f) : new Color(0, 0, 0, 0.35f));
            DrawArc(c, pad.Radius, 0, Mathf.Tau, 40, lit ? new Color("#ffd27a") : new Color(1, 1, 1, 0.5f), 2);
            var label = LabelFor?.Invoke(pad.Action) ?? pad.Label;
            var size = pad.Radius > 50 ? 22 : pad.Radius > 35 ? 17 : 14;
            var width = font.GetStringSize(label, HorizontalAlignment.Left, -1, size).X;
            DrawString(font, c + new Vector2(-width / 2, size * 0.35f), label, HorizontalAlignment.Left, -1, size, ink);
        }
    }
}
