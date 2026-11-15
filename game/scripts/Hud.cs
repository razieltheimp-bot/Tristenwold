using Godot;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>Bars, where you are, the map, conversations, quests, news from the shared world, and the F10 window.</summary>
public partial class Hud : CanvasLayer
{
    readonly List<(string Text, double Until)> _toasts = [];
    readonly List<string> _chronicle = [];
    ProgressBar _health = new(), _mana = new(), _stamina = new(), _limit = new();
    Label _area = new(), _toastLabel = new(), _status = new(), _crowns = new(), _center = new(), _party = new();
    PanelContainer _window = new();
    VBoxContainer _board = new(), _companies = new(), _deviants = new(), _journal = new();
    Control _root = new();
    TabContainer _tabs = new();
    MapView? _mini, _full;
    PanelContainer _talk = new();
    Label _talkWho = new(), _talkText = new();
    VBoxContainer _tracker = new(); // the rows of the quest panel
    PanelContainer _column = new(); // the quest panel: a dark box under the mini-map
    Button _questsHead = new(); // "▾ Quests & news (N)": folds the panel
    ScrollContainer _questScroll = new();
    bool _questsFolded;

    /// <summary>Everyone's names, drawn flat on the screen (see <see cref="Nameplates"/>).</summary>
    public Nameplates Plates { get; } = new();

    /// <summary>The hero is in a fight: the quest column steps back.</summary>
    public bool InFight { get; set; }
    VBoxContainer _talkOptions = new();
    readonly List<(string Label, Action Then)> _options = [];

    /// <summary>On-screen controls for touch screens; hidden unless the game turns them on.</summary>
    public TouchControls Touch { get; } = new() { Visible = false };
    public bool TouchEnabled
    {
        get => _touchEnabled;
        set { _touchEnabled = value; PlaceParty(); }
    }
    bool _touchEnabled;

    /// <summary>An upright phone has its own controller under the picture: the on-screen one steps aside.</summary>
    public bool PhonePad { get; set; }
    Label _log = new();
    LineEdit _say = new();

    public Action<Message>? Send { get; set; }
    public string Company { get; set; } = "";
    public bool WindowOpen => _window.Visible;

    public override void _Ready()
    {
        var root = _root = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);
        root.AddChild(Plates); // under everything else on the screen

        var bars = new VBoxContainer { Position = new Vector2(24, 24), CustomMinimumSize = new Vector2(260, 0) };
        root.AddChild(bars);
        _health = Bar(bars, "#b03a2e");
        _mana = Bar(bars, "#2e5cb0");
        _stamina = Bar(bars, "#3e9a4a");
        _limit = Bar(bars, "#d8b25a");
        bars.AddChild(ReputeMeter());
        bars.AddChild(_crowns);
        _crowns.LabelSettings = Small(12, Colors.White); // level, crowns, meat: small, outlined
        root.AddChild(_area = new Label { Position = new Vector2(24, 136), LabelSettings = Small(13, Colors.White) });
        root.AddChild(_status = new Label { Position = new Vector2(24, 158), Modulate = new Color(1, 1, 1, 0.8f), LabelSettings = new LabelSettings { FontSize = 10, OutlineSize = 3, OutlineColor = Colors.Black } }); // the controls, small, outlined so they read over anything
        // What just happened, for a few seconds, small, down the left where the quest list used to be.
        root.AddChild(_toastLabel = new Label { Position = new Vector2(24, 200), Size = new Vector2(420, 0), AutowrapMode = TextServer.AutowrapMode.Word, LabelSettings = Small(12, Colors.White) });
        // The quests and the news: a small dark panel right under the mini-map (see PlaceColumn and Track).
        root.AddChild(_column = MakeQuestPanel());
        root.AddChild(_party = new Label { Modulate = new Color("#a8e0a0"), HorizontalAlignment = HorizontalAlignment.Right });
        PlaceParty();
        root.AddChild(_center = new Label { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.68f, OffsetLeft = -300, OffsetRight = 300, HorizontalAlignment = HorizontalAlignment.Center });

        _window = new PanelContainer { AnchorLeft = 0.1f, AnchorRight = 0.9f, AnchorTop = 0.08f, AnchorBottom = 0.92f, Visible = false };
        root.AddChild(_window);
        var windowBox = new VBoxContainer();
        _window.AddChild(windowBox);
        var close = new Button { Text = "Close", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, CustomMinimumSize = new Vector2(120, 40) };
        close.Pressed += ToggleWindow;
        windowBox.AddChild(close);
        var tabs = _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        windowBox.AddChild(tabs);
        tabs.AddChild(Scroll("Board", _board));
        tabs.AddChild(Scroll("Companies", _companies));
        tabs.AddChild(Scroll("Deviants", _deviants));
        var reputeScroll = new ScrollContainer { Name = "Reputation", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        reputeScroll.AddChild(_reputeSheet = new Label { AutowrapMode = TextServer.AutowrapMode.Word, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        tabs.AddChild(reputeScroll);
        var chronicle = new VBoxContainer { Name = "Chronicle" };
        var logScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        logScroll.AddChild(_log = new Label { AutowrapMode = TextServer.AutowrapMode.Word, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        chronicle.AddChild(logScroll);
        chronicle.AddChild(_say = new LineEdit { PlaceholderText = "Say something to every company (Enter)" });
        _say.TextSubmitted += text =>
        {
            if (text.Trim().Length > 0) Send?.Invoke(new Say(text.Trim()));
            _say.Clear();
        };
        tabs.AddChild(chronicle);
        tabs.AddChild(Scroll("Journal", _journal));

        // A conversation: who, what they said, and what you can say back.
        // Long lists (a shop's stock, a full pack) scroll.
        _talk = new PanelContainer { AnchorLeft = 0.2f, AnchorRight = 0.8f, AnchorTop = 0.4f, AnchorBottom = 0.97f, Visible = false };
        var talkBox = new VBoxContainer();
        _talk.AddChild(talkBox);
        talkBox.AddChild(_talkWho = new Label { Modulate = new Color("#a8d8a0") });
        talkBox.AddChild(_talkText = new Label { AutowrapMode = TextServer.AutowrapMode.Word });
        var optionScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        optionScroll.AddChild(_talkOptions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        optionScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var talkRow = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        talkRow.AddChild(optionScroll);
        talkRow.AddChild(Gear); // the hero in miniature, beside the pack's gear
        talkBox.AddChild(talkRow);
        root.AddChild(_bar = MakeBar());
        root.AddChild(Touch);
        Touch.ScrollsHere = OnQuests; // a swipe on the quest panel scrolls it, rather than turning the view
        Touch.Scroll = moved => _questScroll.ScrollVertical = Math.Max(0, _questScroll.ScrollVertical - (int)moved.Y);
        root.AddChild(_talk);
    }

    public override void _Process(double delta)
    {
        // The touch controls step aside while a menu, the map or a conversation is open.
        var show = TouchEnabled && !PhonePad && !_window.Visible && !Talking && !MapOpen;
        if (Touch.Visible && !show) Touch.ReleaseAll();
        Touch.Visible = show;
        PlaceColumn(delta);
    }

    // ---- the right-hand column: mini-map, quests, (on a touch screen) the party ------------------

    const float MapSize = 200, MapMargin = 24, ColumnGap = 8;
    const int RowsShown = 6, RowFont = 11, HeadFont = 10;

    static LabelSettings Small(int size, Color colour) => new()
    {
        FontSize = size, FontColor = colour, OutlineSize = 3, OutlineColor = new Color(0, 0, 0, 0.9f), ShadowSize = 2, ShadowColor = new Color(0, 0, 0, 0.45f), ShadowOffset = new Vector2(1, 1),
    };

    /// <summary>The quest panel: a dark see-through box with a header that folds it, and the rows in a scroll under it.</summary>
    PanelContainer MakeQuestPanel()
    {
        var panel = new PanelContainer { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Pass };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.03f, 0.55f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 6, ContentMarginRight = 4, ContentMarginTop = 2, ContentMarginBottom = 4,
        });
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        box.AddThemeConstantOverride("separation", 1);
        panel.AddChild(box);
        _questsHead = new Button { Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None, Text = "▾ Quests & news", ClipText = true };
        _questsHead.AddThemeFontSizeOverride("font_size", HeadFont);
        _questsHead.AddThemeColorOverride("font_color", new Color("#c8bea8"));
        _questsHead.AddThemeColorOverride("font_hover_color", new Color("#ffe6b0"));
        _questsHead.AddThemeConstantOverride("h_separation", 0);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) _questsHead.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _questsHead.Pressed += FoldQuests;
        box.AddChild(_questsHead);
        _questScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        var bar = _questScroll.GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(3, 0);
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f) });
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" }) bar.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = new Color("#c8a860") with { A = state == "grabber" ? 0.6f : 0.9f } });
        _questScroll.AddChild(_tracker = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass });
        _tracker.AddThemeConstantOverride("separation", 1);
        box.AddChild(_questScroll);
        return panel;
    }

    /// <summary>Folds the quest panel to its header, or opens it again.</summary>
    public void FoldQuests()
    {
        _questsFolded = !_questsFolded;
        _questScroll.Visible = !_questsFolded;
        TitleQuests();
        PlaceColumn(0);
    }

    public bool QuestsFolded => _questsFolded;

    void TitleQuests() => _questsHead.Text = $"{(_questsFolded ? "▸" : "▾")} Quests & news ({_tracker.GetChildCount()})";

    /// <summary>Scrolls the quest panel by this many rows (the mouse wheel in play, a swipe on a touch screen).</summary>
    public void ScrollQuests(float rows)
    {
        if (_questsFolded || !_column.Visible) return;
        _questScroll.ScrollVertical = Math.Max(0, _questScroll.ScrollVertical + (int)Math.Round(rows * RowHeight));
    }

    /// <summary>Where the quest panel scroll is (for the self-test).</summary>
    public int QuestScroll => _questScroll.ScrollVertical;

    /// <summary>Whether a point on the screen is on the quest panel (touches there scroll it instead of turning the view).</summary>
    public bool OnQuests(Vector2 at) => _column.Visible && _column.GetGlobalRect().HasPoint(at);

    float RowHeight => _tracker.GetChildCount() == 0 ? 15 : _tracker.GetChild<Control>(0).GetCombinedMinimumSize().Y + 1;

    public override void _UnhandledInput(InputEvent e)
    {
        // In play the pointer is held for the camera, so the wheel (free otherwise) scrolls the quest panel; with the
        // pointer out (a menu, the map) it scrolls whatever it's over, the panel included.
        if (Input.MouseMode != Input.MouseModeEnum.Captured || e is not InputEventMouseButton { Pressed: true } b) return;
        if (b.ButtonIndex == MouseButton.WheelUp) ScrollQuests(-1);
        else if (b.ButtonIndex == MouseButton.WheelDown) ScrollQuests(1);
        else return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// The quest panel hangs under the mini-map, lined up with its left edge and as wide as it is, showing six rows at most
    /// (the rest a scroll away), and stops short of the party list (at the bottom right; on a touch screen, the party list
    /// goes under the quests instead). It steps back to half strength in a fight, or where it would print over someone's
    /// name or the words in the middle of the screen.
    /// </summary>
    void PlaceColumn(double delta)
    {
        var screen = _root.GetRect().Size;
        var left = screen.X - MapMargin - MapSize;
        var top = MapMargin + MapSize + ColumnGap;
        _column.Position = new Vector2(left, top);
        _column.Visible = _tracker.GetChildCount() > 0;
        var frame = _column.GetThemeStylebox("panel").GetMinimumSize().Y + _questsHead.GetCombinedMinimumSize().Y + 1;
        var rows = Math.Min(_tracker.GetChildCount(), RowsShown);
        var wanted = !_column.Visible ? 0 : _questsFolded ? frame : frame + rows * RowHeight;
        float room;
        var line = Math.Max(1, _party.GetLineHeight() + _party.GetThemeConstant("line_spacing"));
        if (_touchEnabled)
        {
            // The touch controls have the bottom of the screen: quests, then the party, down the right side above them.
            var floor = screen.Y * 0.62f;
            room = Math.Max(0, floor - top - 2 * line);
            var used = Math.Min(wanted, room);
            (_party.AnchorTop, _party.AnchorBottom) = (0, 0);
            _party.OffsetTop = top + used + ColumnGap;
            _party.MaxLinesVisible = Math.Max(1, (int)((floor + 3 * line - _party.OffsetTop) / line));
            _party.OffsetBottom = _party.OffsetTop + _party.MaxLinesVisible * line;
        }
        else
        {
            // The party in the bottom-right corner, growing up from it, but never up into the mini-map; the quests stop short of it.
            _party.MaxLinesVisible = Math.Max(1, (int)((screen.Y - 20 - top - 3 * line) / line));
            var lines = _party.Text.Length == 0 ? 0 : Math.Min(_party.GetLineCount(), _party.MaxLinesVisible);
            var height = lines * line;
            _party.OffsetTop = -20 - Math.Max(height, 40);
            var partyTop = screen.Y - 20 - height;
            room = Math.Max(0, partyTop - ColumnGap - top);
        }
        _column.Size = new Vector2(MapSize, Math.Max(0, Math.Min(wanted, room)));
        _column.CustomMinimumSize = Vector2.Zero;
        if (room < frame) _column.Visible = false; // no room even for the header: off, rather than over the party

        var box = new Rect2(_column.Position, _column.Size);
        var crowded = InFight || Plates.Covers(box) || (_center.Text.Length > 0 && _center.GetGlobalRect().Intersects(box));
        var a = Mathf.MoveToward(_column.Modulate.A, crowded ? 0.5f : 1f, (float)delta * 3);
        _column.Modulate = new Color(1, 1, 1, a);
    }

    /// <summary>Lays the right-hand column out now, with this much time gone for its fading (for the self-test).</summary>
    public void Relayout(double delta = 0) => PlaceColumn(delta);

    /// <summary>The quest column's box on the screen (for the self-test).</summary>
    public Rect2 ColumnBox => new(_column.Position, _column.Size);

    /// <summary>The mini-map's box on the screen (for the self-test).</summary>
    public Rect2 MapBox => _mini?.GetGlobalRect() ?? default;

    /// <summary>The party list's box on the screen, as much of it as has words in it (for the self-test).</summary>
    public Rect2 PartyBox
    {
        get
        {
            var r = _party.GetGlobalRect();
            var lines = _party.Text.Length == 0 ? 0 : Math.Min(_party.GetLineCount(), _party.MaxLinesVisible < 0 ? int.MaxValue : _party.MaxLinesVisible);
            var h = lines * (_party.GetLineHeight() + _party.GetThemeConstant("line_spacing"));
            return _party.VerticalAlignment == VerticalAlignment.Bottom ? new Rect2(r.Position.X, r.End.Y - h, r.Size.X, h) : r with { Size = r.Size with { Y = h } };
        }
    }

    /// <summary>How strongly the quest column is drawn just now (for the self-test).</summary>
    public float ColumnStrength => _column.Modulate.A;

    // ---- the map ---------------------------------------------------------------------------

    public void AttachMap(WorldGen world, ExploredMap explored, Func<Vector3> player, Func<Vector3> facing, Func<IEnumerable<(double X, double Z)>> goals,
        Func<IEnumerable<(double X, double Z, string Name)>>? lairs = null, Func<StructureChart?>? structure = null, Func<IEnumerable<(double X, double Z, string Name, string Kind)>>? belongings = null)
    {
        lairs ??= () => [];
        belongings ??= () => [];
        _mini = new MapView { World = world, Explored = explored, Player = player, Facing = facing, Goals = goals, Lairs = lairs, Structure = structure, Belongings = belongings, Mini = true, MetresPerPixel = 3 };
        _mini.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        (_mini.OffsetLeft, _mini.OffsetTop, _mini.OffsetRight, _mini.OffsetBottom) = (-224, 24, -24, 224);
        _root.AddChild(_mini);
        _full = new MapView { World = world, Explored = explored, Player = player, Facing = facing, Goals = goals, Lairs = lairs, Structure = structure, Belongings = belongings, Visible = false, MetresPerPixel = 6 };
        _full.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_full);
        // Buttons for zooming and closing with a finger.
        var bar = new HBoxContainer();
        bar.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        (bar.OffsetLeft, bar.OffsetTop, bar.OffsetRight, bar.OffsetBottom) = (-330, -76, -16, -16);
        foreach (var (text, act) in new (string, Action)[] { ("-", () => _full.Zoom(1.5f)), ("+", () => _full.Zoom(1 / 1.5f)), ("Close", ToggleMap) })
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(text == "Close" ? 130 : 80, 56) };
            b.Pressed += act;
            bar.AddChild(b);
        }
        _full.AddChild(bar);
    }

    public void MapRevealed(List<(int Cx, int Cz)> cells)
    {
        if (cells.Count == 0) return;
        _mini?.Revealed(cells);
        _full?.Revealed(cells);
    }

    public bool MapOpen => _full?.Visible == true;

    public void ToggleMap()
    {
        if (_full is null) return;
        _full.Visible = !_full.Visible;
        _full.Recentre();
        Sound.Ui(_full.Visible ? "open" : "close", -4);
        UpdateMouse();
    }

    // ---- conversations ---------------------------------------------------------------------

    public bool Talking => _talk.Visible;
    public string TalkText => _talkText.Text;

    /// <summary>Who is talking (for the self-test).</summary>
    public string TalkTitle => _talkWho.Text;

    public void Talk(string who, string text, params (string Label, Action Then)[] options)
    {
        _talk.Visible = true;
        _talk.AnchorTop = 0.4f;
        Gear.Visible = false; // only the pack shows it
        _skillRows = 0;
        _talkWho.Text = who;
        _talkText.Text = text;
        Clear(_talkOptions);
        _options.Clear();
        foreach (var (label, then) in options)
        {
            _options.Add((label, then));
            var b = new Button { Text = label, Alignment = HorizontalAlignment.Left };
            b.Pressed += then;
            _talkOptions.AddChild(b);
        }
        UpdateMouse();
    }

    /// <summary>
    /// A conversation about several at once (the company): each one's line, and right under it a row of what can be
    /// done for that one, wrapping as it needs to; then the closing replies. The window takes the screen's height and
    /// the whole list scrolls.
    /// </summary>
    public void TalkGroups(string who, string text, IEnumerable<(string Line, List<(string Label, Action Then)> Options)> groups, params (string Label, Action Then)[] after)
    {
        Talk(who, text, after);
        TallTalk();
        var at = 0;
        var place = 0;
        foreach (var (line, options) in groups)
        {
            var block = new VBoxContainer();
            block.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color("#e8dcc4") });
            var row = new HFlowContainer();
            foreach (var (label, then) in options)
            {
                var b = new Button { Text = label };
                b.Pressed += then;
                row.AddChild(b);
                _options.Insert(at++, (label, then));
            }
            block.AddChild(row);
            block.AddChild(new HSeparator());
            _talkOptions.AddChild(block);
            _talkOptions.MoveChild(block, place++); // in order, above the closing replies
        }
    }

    /// <summary>Puts a control (the volume sliders) among the replies, above them.</summary>
    public void AddToTalk(Control c)
    {
        _talkOptions.AddChild(c);
        _talkOptions.MoveChild(c, 0);
    }

    int _skillRows;

    /// <summary>The window gets the height of the screen, for a long list.</summary>
    public void TallTalk() => _talk.AnchorTop = 0.06f;

    /// <summary>
    /// An ability in the skills window, above the replies: its name, what it does, and a row of switches, one for each rank,
    /// lit up to the rank it's at. The next one along is the one that can be clicked, for a skill point.
    /// </summary>
    public void AddSkill(string name, string what, int rank, int most, string? locked, Action? raise)
    {
        var row = new VBoxContainer();
        var top = new HBoxContainer();
        top.AddChild(new Label { Text = name, Modulate = locked is null ? new Color("#ffd27a") : new Color("#9a948a"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        if (locked is not null) top.AddChild(new Label { Text = locked, Modulate = new Color("#9a948a") });
        for (var i = 0; locked is null && i < most; i++)
        {
            var next = i == rank && raise is not null;
            var on = new CheckButton { ButtonPressed = i < rank, Disabled = !next, TooltipText = $"Rank {i + 1}" };
            if (next) on.Pressed += raise!;
            top.AddChild(on);
        }
        row.AddChild(top);
        row.AddChild(new Label { Text = what, AutowrapMode = TextServer.AutowrapMode.Word, Modulate = locked is null ? Colors.White : new Color("#9a948a") });
        row.AddChild(new HSeparator());
        _talkOptions.AddChild(row);
        _talkOptions.MoveChild(row, _skillRows++);
        if (raise is not null) _options.Insert(0, ($"Raise {name}", raise));
    }

    public void EndTalk()
    {
        _talk.Visible = false;
        Gear.Visible = false;
        UpdateMouse();
    }

    /// <summary>The hero in miniature beside the replies, trying gear on (the pack shows it; any other talk hides it).</summary>
    public GearPreview Gear { get; } = new() { Visible = false };

    /// <summary>The buttons of the replies now showing, in order.</summary>
    public IEnumerable<Button> OptionButtons => _talkOptions.GetChildren().OfType<Button>();

    /// <summary>Picks the first reply that starts with this text, as if clicked (for the self-test).</summary>
    public bool Choose(string startsWith)
    {
        var option = _options.FirstOrDefault(o => o.Label.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));
        if (option.Then is null) return false;
        option.Then();
        return true;
    }

    public IEnumerable<string> Options => _options.Select(o => o.Label);

    // ---- quests ----------------------------------------------------------------------------

    /// <summary>What a quest-panel row is, in the order they're listed.</summary>
    public enum QuestRow { Tracked, Threat, Quest, Work }

    /// <summary>One row of the quest panel: a short line, where it is, and the whole of it (for the pointer).</summary>
    public readonly record struct QuestEntry(QuestRow Kind, string Text, string Where, string Full, int Count);

    readonly List<QuestEntry> _entries = [];

    /// <summary>
    /// The quests and news, each cut down to one short line ("Bitterwell Fort: clear 0/5" and "2.9 km NW" at the right),
    /// the same thing twice shown once, in order: the tracked one bright at the top, then threats (deviants, raids, demons,
    /// a rival), then the other quests, then work and news. The panel shows six and scrolls for the rest.
    /// </summary>
    public void Track(IEnumerable<string> lines, string? tracked = null)
    {
        var all = lines.Where(l => l.Length > 0).ToList();
        var text = string.Join("\n", all) + "\n" + tracked;
        if (text == _trackedText) return;
        _trackedText = text;
        tracked ??= all.FirstOrDefault();
        _entries.Clear();
        var seen = new Dictionary<string, int>();
        foreach (var line in all)
        {
            var (head, where) = Shorten(line);
            var key = (head.IndexOf(": ", StringComparison.Ordinal) is var colon and > 0 ? head[..colon] : head).Trim().ToLowerInvariant();
            if (seen.TryGetValue(key, out var at)) { _entries[at] = _entries[at] with { Count = _entries[at].Count + 1 }; continue; }
            seen[key] = _entries.Count;
            _entries.Add(new QuestEntry(line == tracked ? QuestRow.Tracked : KindOf(line), head, where, line, 1));
        }
        var ordered = _entries.Select((e, i) => (e, i)).OrderBy(x => x.e.Kind).ThenBy(x => x.i).Select(x => x.e).ToList();
        _entries.Clear();
        _entries.AddRange(ordered);

        foreach (var old in _tracker.GetChildren()) { _tracker.RemoveChild(old); old.QueueFree(); } // gone now, not at the end of the frame, so the panel's height is right at once
        foreach (var e in _entries)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = e.Full };
            row.AddThemeConstantOverride("separation", 4);
            var colour = e.Kind switch { QuestRow.Tracked => "#ffd27a", QuestRow.Threat => "#ff9a7a", QuestRow.Work => "#c8c0ac", _ => "#e6dcc4" };
            row.AddChild(new Label
            {
                Text = e.Count > 1 ? $"{e.Text} ×{e.Count}" : e.Text, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = e.Full, LabelSettings = Small(RowFont, new Color(colour)),
            });
            if (e.Where.Length > 0)
                row.AddChild(new Label { Text = e.Where, MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = Small(RowFont - 1, new Color("#b8b0a0")) });
            _tracker.AddChild(row);
        }
        TitleQuests();
    }

    static readonly System.Text.RegularExpressions.Regex Bearing = new(
        @"\s*\((here|\d+(?:\.\d+)? k?m (?:north|south|east|west)(?:-(?:east|west))?)[^()]*\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>A tracker line cut to one short line, and where it is ("2.9 km NW") pulled out to show at the right.</summary>
    public static (string Text, string Where) Shorten(string line)
    {
        var where = "";
        if (Bearing.Match(line) is { Success: true } m)
        {
            where = m.Groups[1].Value.Replace("north", "N").Replace("south", "S").Replace("east", "E").Replace("west", "W").Replace("-", "").Replace("here", "here");
            line = line.Remove(m.Index, m.Length);
        }
        // Only the first clause of what to do: "Dock work: carry the crate to the warehouse. 3 to go" → "Dock work: carry the crate to the warehouse".
        var colon = line.IndexOf(": ", StringComparison.Ordinal);
        var from = colon is > 0 and < 48 ? colon + 2 : 0;
        var stop = line.Length;
        foreach (var mark in new[] { ". ", "; " })
            if (line.IndexOf(mark, from, StringComparison.Ordinal) is var i and >= 0 && i < stop) stop = i;
        line = line[..stop].TrimEnd('.', ' ', ',');
        return (line, where);
    }

    /// <summary>Threats first after the tracked quest, then quests, then work and news.</summary>
    static QuestRow KindOf(string line)
    {
        bool Starts(params string[] heads) => heads.Any(h => line.StartsWith(h, StringComparison.OrdinalIgnoreCase));
        if (Starts("Raid", "Deviant", "Hunted by", "KORRIN", "YOUR RIVAL", "Your rival", "Outlaw", "The ambush") || line.Contains("demon", StringComparison.OrdinalIgnoreCase)) return QuestRow.Threat;
        if (Starts("Starter work", "Dock work", "Errand", "Shipyard", "Well rested", "Snares", "Inside ", "News")) return QuestRow.Work;
        return QuestRow.Quest;
    }

    string _trackedText = "";

    /// <summary>Everything the quest tracker was given (for the self-test).</summary>
    public IEnumerable<string> Tracked => _entries.Select(e => e.Full);

    /// <summary>The rows of the quest panel, in order (for the self-test).</summary>
    public IReadOnlyList<QuestEntry> QuestRows => _entries;

    /// <summary>The panel's header (for the self-test).</summary>
    public string QuestsTitle => _questsHead.Text;

    /// <summary>How many rows show without scrolling (for the self-test).</summary>
    public int QuestRowsInView => _questsFolded ? 0 : (int)Math.Floor(Math.Max(0, _questScroll.Size.Y + 1) / RowHeight);

    public void ShowJournal(QuestLog log)
    {
        Clear(_journal);
        foreach (var e in log.Entries.OrderBy(e => e.Stage == QuestStage.Done))
            _journal.AddChild(new Label
            {
                Text = $"{e.Quest.Title}{(e.Stage == QuestStage.Done ? "  (done)" : "")}\n   From {e.Quest.GiverName} of {e.Quest.GiverHomeName}. {e.Objective}. Reward: {e.Quest.Reward} crowns.",
                AutowrapMode = TextServer.AutowrapMode.Word,
                Modulate = e.Stage == QuestStage.Done ? new Color(1, 1, 1, 0.45f) : Colors.White,
            });
        if (log.Entries.Count == 0) _journal.AddChild(new Label { Text = "No quests yet. Look for people marked with \"!\" and ask them for work." });
    }

    public void OpenJournal()
    {
        if (!_window.Visible) ToggleWindow();
        _tabs.CurrentTab = _tabs.GetTabIdxFromControl(_journal.GetParent<Control>());
    }

    void UpdateMouse()
    {
        if (DisplayServer.GetName() == "headless") return;
        Input.MouseMode = _window.Visible || Talking || MapOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    Label _reputeSheet = new(), _reputeNumber = new();
    ColorRect _reputeFill = new();

    /// <summary>The reputation meter: 0 in the middle, a short bar out to the right for good standing and to the left for bad.</summary>
    Control ReputeMeter()
    {
        var meter = new Control { CustomMinimumSize = new Vector2(260, 16), TooltipText = "Your reputation here, from -5 to +5", MouseFilter = Control.MouseFilterEnum.Ignore };
        var back = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        meter.AddChild(back);
        meter.AddChild(_reputeFill = new ColorRect { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore });
        meter.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.5f), AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorBottom = 1, OffsetLeft = -1, OffsetRight = 1, MouseFilter = Control.MouseFilterEnum.Ignore });
        meter.AddChild(_reputeNumber = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore });
        _reputeNumber.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _reputeNumber.AddThemeFontSizeOverride("font_size", 12);
        _reputeNumber.AddThemeColorOverride("font_outline_color", Colors.Black);
        _reputeNumber.AddThemeConstantOverride("outline_size", 4);
        return meter;
    }

    /// <summary>The number on the meter (for the self-test), and how far its bar reaches from the middle, as a share of the meter.</summary>
    public string ReputeNumber => _reputeNumber.Text;
    public float ReputeReach => _reputeFill.AnchorRight - _reputeFill.AnchorLeft;

    /// <summary>How the hero stands where they are, from -5 to +5.</summary>
    public void ShowReputeMeter(double view, string where)
    {
        _reputeNumber.Text = Tristinwold.World.ReputationBook.Number(view);
        var reach = (float)(view / 10);
        (_reputeFill.AnchorLeft, _reputeFill.AnchorRight) = reach < 0 ? (0.5f + reach, 0.5f) : (0.5f, 0.5f + reach);
        _reputeFill.Color = reach < 0 ? new Color("#b03a3a") : new Color("#3e9a6a");
        _reputeNumber.GetParent<Control>().TooltipText = $"Your reputation in {where}, from -5 to +5";
    }

    public void ShowRepute(string text) => _reputeSheet.Text = text;

    static ProgressBar Bar(Container parent, string colour)
    {
        var bar = new ProgressBar { MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 14) };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(colour) });
        parent.AddChild(bar);
        return bar;
    }

    static ScrollContainer Scroll(string name, VBoxContainer content)
    {
        var s = new ScrollContainer { Name = name };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        s.AddChild(content);
        return s;
    }

    public void ToggleWindow()
    {
        _window.Visible = !_window.Visible;
        UpdateMouse();
        Sound.Ui(_window.Visible ? "open" : "close", -4);
    }

    public void Toast(string text, double seconds = 7)
    {
        _toasts.Add((text, Time.GetTicksMsec() / 1000.0 + seconds));
        Log(text);
    }

    /// <summary>The last thing the chronicle heard (for the self-test).</summary>
    public string LastLine => _chronicle.Count > 0 ? _chronicle[^1] : "";

    /// <summary>Whether the chronicle has heard something lately (for the self-test).</summary>
    public bool Heard(string part) => _chronicle.Any(c => c.Contains(part));
    /// <summary>The last few lines of the chronicle, for the self-test's failure messages.</summary>
    public string Recent(int lines) => string.Join(" | ", _chronicle.TakeLast(lines));

    public void Log(string text)
    {
        _chronicle.Add(text);
        if (_chronicle.Count > 120) _chronicle.RemoveAt(0);
        _log.Text = string.Join("\n", _chronicle);
    }

    public void Center(string text) => _center.Text = text;

    /// <summary>A limit break's name across the screen, over a flash of its colour.</summary>
    public void LimitBanner(string name, string who, Color c)
    {
        var flash = new ColorRect { Color = new Color(c, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore };
        flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(flash);
        var f = flash.CreateTween();
        f.SetIgnoreTimeScale();
        f.TweenProperty(flash, "color", new Color(c, 0), 0.6);
        f.TweenCallback(Callable.From(flash.QueueFree));

        var banner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        banner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        banner.OffsetBottom = -120;
        banner.AddChild(new Label
        {
            Text = "LIMIT BREAK", HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 26, FontColor = Colors.White, OutlineSize = 8, OutlineColor = Colors.Black },
        });
        banner.AddChild(new Label
        {
            Text = name.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 72, FontColor = c.Lightened(0.35f), OutlineSize = 18, OutlineColor = c.Darkened(0.75f), ShadowSize = 12, ShadowColor = new Color(0, 0, 0, 0.6f) },
        });
        banner.AddChild(new Label
        {
            Text = who, HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 22, FontColor = new Color(1, 1, 1, 0.85f), OutlineSize = 6, OutlineColor = Colors.Black },
        });
        _root.AddChild(banner);
        banner.PivotOffset = GetViewport().GetVisibleRect().Size / 2;
        banner.Scale = new Vector2(1.6f, 1.6f);
        banner.Modulate = new Color(1, 1, 1, 0);
        var b = banner.CreateTween();
        b.SetIgnoreTimeScale();
        b.SetParallel();
        b.TweenProperty(banner, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        b.TweenProperty(banner, "modulate", Colors.White, 0.15);
        b.Chain().TweenInterval(1.1);
        b.Chain().TweenProperty(banner, "modulate", new Color(1, 1, 1, 0), 0.5);
        b.Chain().TweenCallback(Callable.From(banner.QueueFree));
    }

    /// <summary>The companions following, one line each.</summary>
    public void Party(IEnumerable<string> lines) => _party.Text = string.Join("\n", lines);

    /// <summary>
    /// The companions' names, levels and health, out of the way in the bottom-right corner; with the on-screen
    /// controls there, under the mini-map instead.
    /// </summary>
    void PlaceParty()
    {
        if (_party is null) return;
        (_party.AnchorLeft, _party.AnchorRight) = (1, 1);
        (_party.OffsetLeft, _party.OffsetRight) = (-360, -24);
        if (_touchEnabled)
        {
            (_party.AnchorTop, _party.AnchorBottom, _party.OffsetTop, _party.OffsetBottom) = (0, 0, 236, 420);
            _party.VerticalAlignment = VerticalAlignment.Top;
        }
        else
        {
            (_party.AnchorTop, _party.AnchorBottom, _party.OffsetTop, _party.OffsetBottom) = (1, 1, -200, -20);
            _party.VerticalAlignment = VerticalAlignment.Bottom;
        }
    }

    public void Update(Player p, string area, string status)
    {
        _health.Value = p.Stats.Health / p.Stats.MaxHealth * 100;
        _mana.Value = p.Stats.Mana / p.Stats.MaxMana * 100;
        _stamina.Value = p.Stats.Stamina / p.Stats.MaxStamina * 100;
        _limit.Value = p.Stats.Limit;
        _crowns.Text = $"Level {p.Hero.Level} {p.Class.Name}{(p.GuildTitle.Length > 0 ? $", {p.GuildTitle}" : "")}{(p.Hero.Points > 0 ? $" ({p.Hero.Points} to spend, K)" : "")}    {p.Crowns} crowns    {p.Meat} meat"
            + (p.Class.Elemental ? $"    {p.Element}" : "") + (p.Stats.LimitReady ? "    LIMIT READY (R)" : "");
        _area.Text = area;
        _status.Text = status;
        var now = Time.GetTicksMsec() / 1000.0;
        _toasts.RemoveAll(t => t.Until < now);
        _toastLabel.Text = string.Join("\n\n", _toasts.TakeLast(3).Select(t => t.Text));
    }

    public void ShowBoard(IReadOnlyList<PublicContract> contracts)
    {
        Clear(_board);
        foreach (var c in contracts)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = $"{c.Title}\n   {c.PostedBy} | {c.Where} | {c.Pay} crowns", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.Word, Modulate = c.Status == ContractStatus.Done ? new Color(1, 1, 1, 0.4f) : Colors.White });
            if (c.Status == ContractStatus.Open) AddButton(row, "Take it", new ClaimContract(c.Id));
            else if (c.Status == ContractStatus.Claimed && c.ClaimedBy == Company)
            {
                AddButton(row, "Done", new FinishContract(c.Id));
                AddButton(row, "Abandon", new AbandonContract(c.Id));
            }
            else row.AddChild(new Label { Text = c.Status == ContractStatus.Done ? "done" : $"{c.ClaimedBy}'s" });
            _board.AddChild(row);
        }
    }

    public void ShowCompanies(IReadOnlyList<GroupView> groups)
    {
        Clear(_companies);
        foreach (var g in groups)
            _companies.AddChild(new Label { Text = $"{g.Player}'s company {(g.Online ? "- " + (g.Area.Length > 0 ? g.Area : "somewhere") : "(resting)")}\n   {string.Join(", ", g.Party)}" });
    }

    string? _rival;
    Label? _rivalLabel;

    /// <summary>The hero's own rival, at the head of the deviants tab.</summary>
    public void ShowRival(string text)
    {
        _rival = text;
        if (_rivalLabel is not null && IsInstanceValid(_rivalLabel) && !_rivalLabel.IsQueuedForDeletion()) { _rivalLabel.Text = text; return; }
        _deviants.AddChild(_rivalLabel = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color("#ff9a7a") });
        _deviants.MoveChild(_rivalLabel, 0);
    }

    public void ShowDeviants(string worldJson)
    {
        Clear(_deviants);
        if (_rival is not null) { _rivalLabel = null; ShowRival(_rival); }
        try
        {
            var world = Deviant.DeviantWorld.FromJson(worldJson);
            foreach (var n in world.All.OrderByDescending(n => n.Alive).ThenByDescending(n => n.Rank))
                _deviants.AddChild(new Label
                {
                    Text = $"{n.Rank.ToString().ToUpperInvariant()}  {n.FullName}{(n.Alive ? "" : " (fallen)")}\n   {n.Species}, level {n.Level}, from {n.HomeArea}\n   {string.Join("; ", n.Traits.Select(t => t.Label))}",
                    AutowrapMode = TextServer.AutowrapMode.Word,
                });
        }
        catch (Exception) { /* keep the last good roster */ }
    }

    void AddButton(Container row, string text, Message message)
    {
        var b = new Button { Text = text };
        b.Pressed += () => Send?.Invoke(message);
        row.AddChild(b);
    }

    static void Clear(Node n)
    {
        foreach (var child in n.GetChildren()) child.QueueFree();
    }
}
