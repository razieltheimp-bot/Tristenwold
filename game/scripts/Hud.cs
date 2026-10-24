using Godot;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>Bars, where you are, news from the shared world, and the F10 window.</summary>
public partial class Hud : CanvasLayer
{
    readonly List<(string Text, double Until)> _toasts = [];
    readonly List<string> _chronicle = [];
    ProgressBar _health = new(), _magicka = new(), _stamina = new(), _limit = new();
    Label _area = new(), _toastLabel = new(), _status = new(), _septims = new(), _center = new();
    PanelContainer _window = new();
    VBoxContainer _board = new(), _companies = new(), _nemeses = new();
    Label _log = new();
    LineEdit _say = new();

    public Action<Message>? Send { get; set; }
    public string Company { get; set; } = "";
    public bool WindowOpen => _window.Visible;

    public override void _Ready()
    {
        var root = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);

        var bars = new VBoxContainer { Position = new Vector2(24, 24), CustomMinimumSize = new Vector2(260, 0) };
        root.AddChild(bars);
        _health = Bar(bars, "#b03a2e");
        _magicka = Bar(bars, "#2e5cb0");
        _stamina = Bar(bars, "#3e9a4a");
        _limit = Bar(bars, "#d8b25a");
        bars.AddChild(_septims);
        root.AddChild(_area = new Label { Position = new Vector2(24, 140) });
        root.AddChild(_status = new Label { Position = new Vector2(24, 166), Modulate = new Color(1, 1, 1, 0.6f) });
        root.AddChild(_toastLabel = new Label { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -520, OffsetTop = 24, OffsetRight = -24, AutowrapMode = TextServer.AutowrapMode.Word, HorizontalAlignment = HorizontalAlignment.Right });
        root.AddChild(_center = new Label { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.4f, OffsetLeft = -300, OffsetRight = 300, HorizontalAlignment = HorizontalAlignment.Center });

        _window = new PanelContainer { AnchorLeft = 0.1f, AnchorRight = 0.9f, AnchorTop = 0.08f, AnchorBottom = 0.92f, Visible = false };
        root.AddChild(_window);
        var tabs = new TabContainer();
        _window.AddChild(tabs);
        tabs.AddChild(Scroll("Board", _board));
        tabs.AddChild(Scroll("Companies", _companies));
        tabs.AddChild(Scroll("Nemeses", _nemeses));
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
    }

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
        if (DisplayServer.GetName() != "headless")
            Input.MouseMode = _window.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    public void Toast(string text, double seconds = 7)
    {
        _toasts.Add((text, Time.GetTicksMsec() / 1000.0 + seconds));
        Log(text);
    }

    public void Log(string text)
    {
        _chronicle.Add(text);
        if (_chronicle.Count > 120) _chronicle.RemoveAt(0);
        _log.Text = string.Join("\n", _chronicle);
    }

    public void Center(string text) => _center.Text = text;

    public void Update(Player p, string area, string status)
    {
        _health.Value = p.Stats.Health / p.Stats.MaxHealth * 100;
        _magicka.Value = p.Stats.Magicka / p.Stats.MaxMagicka * 100;
        _stamina.Value = p.Stats.Stamina / p.Stats.MaxStamina * 100;
        _limit.Value = p.Stats.Limit;
        _septims.Text = $"{p.Septims} septims    spell: {p.Element}{(p.Stats.LimitReady ? "    LIMIT READY (R)" : "")}";
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
            row.AddChild(new Label { Text = $"{c.Title}\n   {c.PostedBy} | {c.Where} | {c.Pay} gp", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.Word, Modulate = c.Status == ContractStatus.Done ? new Color(1, 1, 1, 0.4f) : Colors.White });
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

    public void ShowNemeses(string worldJson)
    {
        Clear(_nemeses);
        try
        {
            var world = Nemesis.NemesisWorld.FromJson(worldJson);
            foreach (var n in world.All.OrderByDescending(n => n.Alive).ThenByDescending(n => n.Rank))
                _nemeses.AddChild(new Label
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
