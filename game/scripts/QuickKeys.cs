using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>The quick keys, 1 to 0: moves on the first five, potions, food and the like on the rest.</summary>
public partial class Main
{
    double _barTidied;

    /// <summary>Nothing goes off behind a conversation, a menu or the map.</summary>
    bool BarFree => !_hud.Talking && !_hud.WindowOpen && !_hud.MapOpen && !_player.Frozen && !_player.Scripted;

    void TendBar()
    {
        _hud.BarPressed ??= PressBar;
        var bar = _player.Hero.Bar;
        if (_clock >= _barTidied)
        {
            _barTidied = _clock + 1;
            bar.Arrange(_player.Hero);
            bar.Stock(_player.Pack.Contents.Where(c => c.Item.Usable && c.Item.Kind == ItemKind.Potion).Select(c => c.Item.Id));
        }
        for (var slot = 0; slot < QuickBar.Size; slot++)
            if (Input.IsActionJustPressed($"quick_{QuickBar.Key(slot)}")) PressBar(slot);
        _hud.ShowBar(Enumerable.Range(0, QuickBar.Size).Select(SquareOf));
    }

    /// <summary>A quick key, pressed or tapped.</summary>
    public void PressBar(int slot)
    {
        if (!BarFree || _player.Downed) return;
        if (_player.Hero.Bar.Slots[slot] is not { } id) { _hud.Toast($"Nothing on {QuickBar.Key(slot)}. Set your quick keys from Skills or your pack.", 3); return; }
        if (QuickBar.ForMoves(slot)) { if (Classes.Move(id) is { } move) _player.UseMove(move); }
        else if (Items.Get(id) is not { } item) return;
        else if (_player.Pack.Count(id) > 0) Consume(item);
        else _hud.Toast($"You're out of {item.Name.ToLowerInvariant()}.", 3);
    }

    string BarName(int slot) => _player.Hero.Bar.Slots[slot] is not { } id ? "(empty)"
        : QuickBar.ForMoves(slot) ? Classes.Move(id)?.Name ?? id : Items.Get(id)?.Name ?? id;

    BarSquare SquareOf(int slot)
    {
        var id = _player.Hero.Bar.Slots[slot];
        if (id is null) return new("", "", 0, false, true);
        if (!QuickBar.ForMoves(slot))
        {
            var n = _player.Pack.Count(id);
            return new(BarName(slot), $"{n}", 0, false, n == 0);
        }
        if (Classes.Move(id) is not { } a) return new(id, "", 0, false, true);
        var stats = _player.Stats;
        if (a == _player.Class.Limit) return new(a.Name, "", 1 - stats.Limit / 100, stats.LimitReady, !stats.LimitReady);
        var left = _player.Cooldown(a.Id);
        var cantPay = a.Cost > (a.Pays == AttackKind.Spell ? stats.Mana : stats.Stamina);
        return new(a.Name, left >= 1 ? $"{left:0}" : "", a.Cooldown > 0 ? left / a.Cooldown : 0, false, cantPay);
    }

    /// <summary>The quick keys, to swap about: from the skills or the pack, and back to whichever it was.</summary>
    public void ShowQuickBar(Action back)
    {
        _player.Hero.Bar.Arrange(_player.Hero);
        var options = Enumerable.Range(0, QuickBar.Size)
            .Select(slot => ($"{QuickBar.Key(slot)}: {BarName(slot)}", (Action)(() => PickForBar(slot, back)))).ToList();
        options.Add(("Back", back));
        _hud.Talk("Quick keys", "1 to 5 hold your moves, and any you've learned in another class. 6 to 0 hold potions, food and the like. Choose a key to change it.", [.. options]);
    }

    void PickForBar(int slot, Action back)
    {
        var bar = _player.Hero.Bar;
        var on = bar.Slots[slot];
        void Put(string? id) { bar.Set(slot, id); Save(); ShowQuickBar(back); }
        var options = QuickBar.ForMoves(slot)
            ? QuickBar.Moves(_player.Hero).Where(a => a.Id != on).Select(a => ($"{a.Name}: {a.Text}", (Action)(() => Put(a.Id)))).ToList()
            : _player.Pack.Contents.Where(c => c.Item.Usable && c.Item.Id != on).Select(c => ($"{c.Item.Name} ({c.N}): {c.Item.Text}", (Action)(() => Put(c.Item.Id)))).ToList();
        if (on is not null) options.Add(("Leave it empty", () => Put(null)));
        options.Add(("Back", () => ShowQuickBar(back)));
        _hud.Talk($"Quick key {QuickBar.Key(slot)}", $"On it now: {BarName(slot)}.", [.. options]);
    }
}

/// <summary>What one quick key shows: its name, a small note (a count, seconds left), how much of it is shaded, whether it's lit, and whether it's dimmed.</summary>
public readonly record struct BarSquare(string Name, string Note, double Shade, bool Lit, bool Dim);

public partial class Hud
{
    const int SquareSize = 62;

    BoxContainer _bar = new();
    readonly List<(Button Button, Label Name, Label Note, ColorRect Shade)> _squares = [];
    bool _barStacked;

    /// <summary>A quick key tapped or clicked on screen.</summary>
    public Action<int>? BarPressed { get; set; }

    /// <summary>What's on a quick key, as drawn (for the self-test).</summary>
    public string SquareName(int slot) => _squares[slot].Name.Text;

    /// <summary>Two rows of five: side by side on a keyboard, one over the other on a touch screen.</summary>
    BoxContainer MakeBar()
    {
        var bar = new BoxContainer { Visible = false, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        for (var slot = 0; slot < QuickBar.Size; slot += QuickBar.MoveSlots)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 4);
            for (var i = 0; i < QuickBar.MoveSlots; i++) row.AddChild(MakeSquare(slot + i));
            bar.AddChild(row);
        }
        return bar;
    }

    Button MakeSquare(int slot)
    {
        var edge = new Color(QuickBar.ForMoves(slot) ? "#d8b25a" : "#5ac0a8");
        var look = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.6f), BorderColor = edge with { A = 0.7f } };
        look.SetBorderWidthAll(2);
        look.SetCornerRadiusAll(4);
        var button = new Button { CustomMinimumSize = new Vector2(SquareSize, SquareSize), FocusMode = Control.FocusModeEnum.None, ClipContents = true };
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) button.AddThemeStyleboxOverride(state, look);
        var at = slot;
        button.Pressed += () => BarPressed?.Invoke(at);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.62f), AnchorRight = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(shade);
        Label Text(int size, HorizontalAlignment across, VerticalAlignment down, Color colour)
        {
            var label = new Label { HorizontalAlignment = across, VerticalAlignment = down, Modulate = colour, MouseFilter = Control.MouseFilterEnum.Ignore };
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            (label.OffsetLeft, label.OffsetTop, label.OffsetRight, label.OffsetBottom) = (4, 1, -4, -1);
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 4);
            button.AddChild(label);
            return label;
        }
        var name = Text(11, HorizontalAlignment.Center, VerticalAlignment.Center, Colors.White);
        name.AutowrapMode = TextServer.AutowrapMode.Word;
        (name.OffsetLeft, name.OffsetTop, name.OffsetRight) = (1, 10, -1);
        Text(13, HorizontalAlignment.Left, VerticalAlignment.Top, edge).Text = QuickBar.Key(slot);
        var note = Text(13, HorizontalAlignment.Right, VerticalAlignment.Bottom, Colors.White);
        _squares.Add((button, name, note, shade));
        return button;
    }

    /// <summary>Along the bottom middle, on a touch screen in two rows, to sit between the thumbs.</summary>
    void PlaceBar()
    {
        _barStacked = TouchEnabled;
        _bar.Vertical = _barStacked;
        _bar.AddThemeConstantOverride("separation", _barStacked ? 4 : 14);
        var (wide, tall) = _barStacked ? (5 * (SquareSize + 4), 2 * (SquareSize + 4)) : (10 * (SquareSize + 4) + 14, SquareSize);
        (_bar.AnchorLeft, _bar.AnchorRight, _bar.AnchorTop, _bar.AnchorBottom) = (0.5f, 0.5f, 1, 1);
        (_bar.OffsetLeft, _bar.OffsetRight, _bar.OffsetTop, _bar.OffsetBottom) = (-wide / 2f, wide / 2f, -tall - 12, -12);
    }

    public void ShowBar(IEnumerable<BarSquare> squares)
    {
        if (_barStacked != TouchEnabled || _bar.OffsetTop == 0) PlaceBar();
        _bar.Visible = !_window.Visible && !Talking && !MapOpen;
        var slot = 0;
        foreach (var s in squares)
        {
            var (button, name, note, shade) = _squares[slot++];
            name.Text = s.Name;
            note.Text = s.Note;
            shade.AnchorBottom = (float)Math.Clamp(s.Shade, 0, 1);
            button.Modulate = s.Lit ? new Color(1.5f, 1.3f, 0.8f) : s.Dim ? new Color(1, 1, 1, 0.55f) : Colors.White;
        }
    }
}
