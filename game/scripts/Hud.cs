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
    Label _talkWho = new(), _talkText = new(), _tracker = new();
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

        var bars = new VBoxContainer { Position = new Vector2(24, 24), CustomMinimumSize = new Vector2(260, 0) };
        root.AddChild(bars);
        _health = Bar(bars, "#b03a2e");
        _mana = Bar(bars, "#2e5cb0");
        _stamina = Bar(bars, "#3e9a4a");
        _limit = Bar(bars, "#d8b25a");
        bars.AddChild(ReputeMeter());
        bars.AddChild(_crowns);
        root.AddChild(_area = new Label { Position = new Vector2(24, 140) });
        root.AddChild(_status = new Label { Position = new Vector2(24, 166), Modulate = new Color(1, 1, 1, 0.92f), LabelSettings = new LabelSettings { FontSize = 15, OutlineSize = 4, OutlineColor = Colors.Black } }); // the controls, two rows, outlined so they read over anything
        root.AddChild(_toastLabel = new Label { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -520, OffsetTop = 300, OffsetRight = -24, AutowrapMode = TextServer.AutowrapMode.Word, HorizontalAlignment = HorizontalAlignment.Right });
        root.AddChild(_tracker = new Label { Position = new Vector2(24, 214), Modulate = new Color("#ffd27a"), CustomMinimumSize = new Vector2(480, 0), AutowrapMode = TextServer.AutowrapMode.Word });
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
        talkBox.AddChild(optionScroll);
        root.AddChild(_bar = MakeBar());
        root.AddChild(Touch);
        root.AddChild(_talk);
    }

    public override void _Process(double delta)
    {
        // The touch controls step aside while a menu, the map or a conversation is open.
        var show = TouchEnabled && !PhonePad && !_window.Visible && !Talking && !MapOpen;
        if (Touch.Visible && !show) Touch.ReleaseAll();
        Touch.Visible = show;
    }

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
        UpdateMouse();
    }

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

    public void Track(IEnumerable<string> lines) => _tracker.Text = string.Join("\n", lines);

    /// <summary>What the quest tracker shows (for the self-test).</summary>
    public IEnumerable<string> Tracked => _tracker.Text.Split('\n');

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
        _toastLabel.Text = string.Join("\n\n", _toasts.TakeLast(4).Select(t => t.Text));
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
