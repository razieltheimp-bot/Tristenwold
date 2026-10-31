using Godot;
using System.Text.Json;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>What the start screen settled on: who to play, where, and their saved progress.</summary>
public sealed record StartChoice(CharacterSheet Character, string Server, string ServerPassword, string? Account, string? Token, string? Save, Graphics.Quality Quality);

/// <summary>
/// The login page and the character page. Log in to a server (or play alone), then pick one of
/// your characters or make a new one. On a server, characters and their progress belong to the
/// account, so they follow you from phone to PC. Alone, they live on this device. Remembers the
/// server and stays logged in (it keeps a login token, never the password).
/// </summary>
public partial class StartScreen : CanvasLayer
{
    /// <summary>The server to fill in when this device has none saved yet (--login-server: a phone playing through
    /// this computer starts out pointed at the server running here).</summary>
    public string? DefaultServer { get; init; }

    const string SettingsPath = "user://settings.json";
    const string LocalCharactersPath = "user://characters.json";

    public event Action<StartChoice>? Play;

    /// <summary>A portrait for each class (and the looks of characters made before there were nine).</summary>
    static Texture2D? Portrait(string look) =>
        ResourceLoader.Exists($"res://assets/kaykit/portraits/{look.ToLowerInvariant()}.png")
            ? GD.Load<Texture2D>($"res://assets/kaykit/portraits/{look.ToLowerInvariant()}.png") : null;
    static readonly string[] Elements = ["Fire", "Frost", "Shock"];

    readonly LineEdit _server = Field("Server: e.g. https://word-word-word.trycloudflare.com or 192.168.1.20:7777");
    readonly LineEdit _serverPassword = Field("Server password, if it has one", secret: true);
    readonly LineEdit _account = Field("Account name");
    readonly LineEdit _secret = Field("Account password", secret: true);
    readonly LineEdit _newName = Field("New character's name");
    readonly OptionButton _element = new() { CustomMinimumSize = new Vector2(150, 48) };
    readonly OptionButton _quality = new() { CustomMinimumSize = new Vector2(0, 44) };
    readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color("#ffb08a") };
    readonly Label _who = new() { Modulate = new Color("#e8dcc4") };
    readonly VBoxContainer _login = new(), _characters = new(), _list = new();
    readonly VBoxContainer _swatches = new();
    readonly Label _about = new() { AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color("#e8dcc4") };
    Dictionary<string, string> _settings = [];
    string _look = "Paladin";
    Avatar _avatar = Avatar.For("Paladin");
    bool _styled;                         // the player has changed the look, so picking a class keeps it
    CharacterSheet? _restyling;           // changing the look of a character already made
    readonly AvatarPreview _preview = new();
    ScrollContainer _scroll = null!;
    readonly VBoxContainer _newOnly = new(), _appearance = new();
    readonly Label _lookTitle = new() { Modulate = new Color("#ffd27a") };
    readonly Label _peopleText = new() { AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color("#e8dcc4") };
    readonly Dictionary<string, Label> _lookValues = [];
    readonly HBoxContainer _createButtons = new(), _restyleButtons = new();
    string? _token;
    bool _offline;
    List<CharacterSheet> _sheets = [];

    static LineEdit Field(string hint, bool secret = false) =>
        new() { PlaceholderText = hint, Secret = secret, CustomMinimumSize = new Vector2(0, 50) };

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color("#15130f"), AnchorRight = 1, AnchorBottom = 1 });
        var scroll = _scroll = new ScrollContainer { AnchorRight = 1, AnchorBottom = 1, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var centre = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(centre);
        var page = new VBoxContainer { CustomMinimumSize = new Vector2(660, 0) };
        page.AddThemeConstantOverride("separation", 10);
        centre.AddChild(page);
        page.AddChild(new Label { Text = "The Rings of Tristinwold", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("#ffd27a"), LabelSettings = new LabelSettings { FontSize = 40 } });

        // Page 1: log in.
        page.AddChild(_login);
        _login.AddThemeConstantOverride("separation", 10);
        foreach (var f in new Control[] { _server, _serverPassword, _account, _secret }) _login.AddChild(f);
        var buttons = new HBoxContainer();
        buttons.AddChild(MakeButton("Log in", () => _ = LogIn("login"), expand: true));
        buttons.AddChild(MakeButton("Create account", () => _ = LogIn("register"), expand: true));
        _login.AddChild(buttons);
        _login.AddChild(MakeButton("Play alone, on this device", () => ShowCharacters(offline: true)));
        var quality = new HBoxContainer();
        quality.AddChild(new Label { Text = "Graphics", CustomMinimumSize = new Vector2(110, 0) });
        foreach (var q in Enum.GetNames<Graphics.Quality>()) _quality.AddItem(q);
        _quality.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _quality.ItemSelected += i => { Humans.Enabled = Humans.WantedAt((Graphics.Quality)i); RebuildAppearance(); }; // the look follows the setting
        quality.AddChild(_quality);
        _login.AddChild(quality);
        _login.AddChild(Sound.Sliders());
        _login.AddChild(new Label
        {
            Text = "The server's host gives you its address. A server started with --tunnel prints an https:// address that works from anywhere, phones included. "
                 + "Your account keeps your characters and their progress on that server.",
            AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color(1, 1, 1, 0.55f),
        });

        // Page 2: characters.
        page.AddChild(_characters);
        _characters.Visible = false;
        _characters.AddThemeConstantOverride("separation", 10);
        var top = new HBoxContainer();
        _who.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_who);
        top.AddChild(MakeButton("Log out", LogOut));
        _characters.AddChild(top);
        _characters.AddChild(_list);
        _characters.AddChild(_newOnly);
        _newOnly.AddThemeConstantOverride("separation", 10);
        _newOnly.AddChild(new Label { Text = "A new character", Modulate = new Color("#ffd27a") });
        _newOnly.AddChild(_newName);
        // Pick a class: one row of callings, and only the chosen calling's classes open under it.
        var picks = new List<Button>();
        var callings = new HBoxContainer();
        callings.AddThemeConstantOverride("separation", 6);
        _swatches.AddChild(callings);
        var rows = new Dictionary<Calling, HBoxContainer>();
        var heads = new Dictionary<Calling, Button>();
        void Open(Calling chosen)
        {
            foreach (var (calling, row) in rows) row.Visible = calling == chosen;
            foreach (var (calling, head) in heads) head.ButtonPressed = calling == chosen;
        }
        foreach (var calling in Enum.GetValues<Calling>())
        {
            var head = new Button { Text = calling + "s", ToggleMode = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 34) };
            head.Pressed += () => Open(calling);
            heads[calling] = head;
            callings.AddChild(head);
            var row = new HBoxContainer { Visible = false };
            row.AddThemeConstantOverride("separation", 8);
            rows[calling] = row;
            foreach (var d in Classes.Of(calling))
            {
                var pick = new Button
                {
                    CustomMinimumSize = new Vector2(150, 118), ToggleMode = true, ButtonPressed = d.Look == _look, Text = d.Name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    Icon = Portrait(d.Look), IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top, ExpandIcon = true,
                };
                pick.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("#3a3226"), BorderColor = new Color("#ffd27a"), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
                pick.Pressed += () => { PickClass(d.Look); foreach (var b in picks) b.ButtonPressed = b == pick; };
                picks.Add(pick);
                row.AddChild(pick);
            }
            _swatches.AddChild(row);
        }
        Open(Classes.Get(_look).Calling);
        _newOnly.AddChild(_swatches);
        _newOnly.AddChild(_about);
        Describe(Classes.Get(_look));
        _characters.AddChild(BuildAppearance());
        var make = new HBoxContainer();
        make.AddChild(new Label { Text = "Starting spell", CustomMinimumSize = new Vector2(150, 0) });
        foreach (var e in Elements) _element.AddItem(e);
        make.AddChild(_element);
        _createButtons.AddChild(make);
        _createButtons.AddChild(MakeButton("Create character", () => _ = Create(), expand: true));
        _characters.AddChild(_createButtons);
        _restyleButtons.AddChild(MakeButton("Save this look", () => _ = SaveRestyle(), expand: true));
        _restyleButtons.AddChild(MakeButton("Cancel", () => EndRestyle(), expand: true));
        _restyleButtons.Visible = false;
        _characters.AddChild(_restyleButtons);

        page.AddChild(_status);
        LoadSettings();
    }

    /// <summary>What a class is like, and how it fights.</summary>
    void Describe(Discipline d) => _about.Text =
        $"{d.Name}: {d.Text}\n  Left: {d.Primary.Name}.  Right: {d.Signature.Name}.  Q: {d.Utility.Name}.  R: {d.Limit.Name}.  Always: {d.Passive.Name}.";

    /// <summary>Picks a class on the character page. A look the player hasn't touched follows the class.</summary>
    public void PickClass(string look)
    {
        _look = look;
        Describe(Classes.Get(look));
        if (!_styled) _avatar = Avatar.For(look);
        ShowLook();
    }

    // ---- the look -------------------------------------------------------------------------------

    /// <summary>
    /// The appearance editor: the hero turning on a little stage, and beside them a row for each part (head, body,
    /// arms, legs from any of the nine figures; a hat; clothes and skin colours; height), each stepped with arrows.
    /// </summary>
    /// <summary>Picks a graphics setting as the player would (the look follows it).</summary>
    public void SelectQuality(Graphics.Quality q)
    {
        _quality.Selected = (int)q;
        _quality.EmitSignal(OptionButton.SignalName.ItemSelected, (long)q);
    }

    /// <summary>Builds the appearance editor again (the rows differ between the figures and the real bodies), keeping the look picked.</summary>
    void RebuildAppearance()
    {
        if (_appearance.GetChildCount() == 0) return; // not built yet
        foreach (var keep in new Control[] { _lookTitle, _peopleText, _preview }) keep.GetParent()?.RemoveChild(keep);
        foreach (var child in _appearance.GetChildren().ToList()) { _appearance.RemoveChild(child); child.QueueFree(); }
        _refresh.Clear();
        _lookValues.Clear();
        BuildAppearance();
    }

    Control BuildAppearance()
    {
        _appearance.AddThemeConstantOverride("separation", 8);
        _appearance.AddChild(_lookTitle);
        _appearance.AddChild(_peopleText);
        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 12);
        split.AddChild(_preview);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 4);
        var figures = Avatar.Figures;
        var races = Races.All.Select(r => r.Id).ToArray();
        Row(rows, "People", () => Races.Get(_avatar.Race).Name, d => _avatar = _avatar with { Race = Step(races, _avatar.Race, d) });
        Row(rows, "Form", () => _avatar.Race == "plant" ? _avatar.Form : "(plant people only)",
            d => { if (_avatar.Race == "plant") _avatar = _avatar with { Form = Step(Avatar.Forms, _avatar.Form, d) }; });
        Row(rows, "Dragon", () => _avatar.IsDragonkind ? _avatar.Dragon : "(dragonkind only)",
            d => { if (_avatar.IsDragonkind) _avatar = _avatar with { Dragon = Step([.. Races.Dragons.Select(x => x.Colour)], _avatar.Dragon, d) }; });
        Row(rows, "Other half", () => !_avatar.IsDragonkind ? "(dragonkind only)" : _avatar.Half == "none" ? "None: pure dragon" : Races.Get(_avatar.Half).Name,
            d => { if (_avatar.IsDragonkind) _avatar = _avatar with { Half = Step(["none", .. Races.Halves.Select(r => r.Id)], _avatar.Half, d) }; });
        var real = Humans.Enabled; // a real body on a PC: who they are, not which figure's pieces
        if (real)
        {
            Row(rows, "Sex", () => _avatar.Sex, d => _avatar = _avatar with { Sex = Step(Avatar.Sexes, _avatar.Sex, d), Style = "Natural" });
            Row(rows, "Age", () => _avatar.Age, d => _avatar = _avatar with { Age = Step(Avatar.Ages, _avatar.Age, d) });
            Row(rows, "Face", () => _avatar.Face, d => _avatar = _avatar with { Face = Step(Avatar.Faces, _avatar.Face, d) });
            Row(rows, "Eyes", () => _avatar.Eyes, d => _avatar = _avatar with { Eyes = Step(Avatar.EyeColours, _avatar.Eyes, d) });
            Row(rows, "Hair", () => Avatar.StyleName(_avatar.Style), d => _avatar = _avatar with { Style = Step([.. Avatar.Styles(_avatar.Sex)], _avatar.Style, d) });
            Row(rows, "On the head", () => _avatar.Headwear == "none" ? "Nothing" : _avatar.Headwear, d => _avatar = _avatar with { Headwear = Step(Avatar.Headwears, _avatar.Headwear, d) });
            var cloths = Avatar.Cloths.Select(c => c.Name).ToArray();
            Row(rows, "Top", () => _avatar.Top == "Class" ? "As the class" : _avatar.Top, d => _avatar = _avatar with { Top = Step(cloths, _avatar.Top, d) });
            Row(rows, "Bottoms", () => _avatar.Bottom == "Class" ? "As the class" : _avatar.Bottom, d => _avatar = _avatar with { Bottom = Step(cloths, _avatar.Bottom, d) });
            Row(rows, "Sleeves", () => _avatar.Sleeves == "Class" ? "As the class" : _avatar.Sleeves, d => _avatar = _avatar with { Sleeves = Step(Avatar.SleeveStyles, _avatar.Sleeves, d) });
            Row(rows, "Over them", () => _avatar.Wear == "Class" ? "As the class" : _avatar.Wear, d => _avatar = _avatar with { Wear = Step(Avatar.Wears, _avatar.Wear, d) });
        }
        else
        {
            Row(rows, "Head", () => Avatar.Called(_avatar.Head), d => _avatar = _avatar with { Head = Step(figures, _avatar.Head, d) });
            Row(rows, "Body", () => Avatar.Called(_avatar.Body), d => _avatar = _avatar with { Body = Step(figures, _avatar.Body, d) });
            Row(rows, "Arms", () => Avatar.Called(_avatar.Arms), d => _avatar = _avatar with { Arms = Step(figures, _avatar.Arms, d) });
            Row(rows, "Legs", () => Avatar.Called(_avatar.Legs), d => _avatar = _avatar with { Legs = Step(figures, _avatar.Legs, d) });
            Row(rows, "On the head", () => _avatar.HatName, d => _avatar = _avatar with { Hat = Step(Avatar.Hats.Select(h => h.Id).ToArray(), _avatar.Hat, d) });
        }
        Row(rows, "Clothes", () => _avatar.Outfit, d => _avatar = _avatar with { Outfit = Step(Avatar.Outfits.Select(o => o.Name).ToArray(), _avatar.Outfit, d) });
        Row(rows, "Skin", () => _avatar.Skin, d => _avatar = _avatar with { Skin = Step(Avatar.Skins.Select(o => o.Name).ToArray(), _avatar.Skin, d) });
        Row(rows, "Height", () => _avatar.Height, d => _avatar = _avatar with { Height = Step(Avatar.Heights.Select(o => o.Name).ToArray(), _avatar.Height, d) });
        Row(rows, "Build", () => _avatar.Build, d => _avatar = _avatar with { Build = Step(Avatar.Builds.Select(o => o.Name).ToArray(), _avatar.Build, d) });
        if (!real) Row(rows, "Beard", () => _avatar.Beard == "none" ? "None" : _avatar.Beard, d => _avatar = _avatar with { Beard = Step(Avatar.Beards, _avatar.Beard, d) });
        Row(rows, real ? "Hair colour" : "Hair (beard)", () => _avatar.Hair, d => _avatar = _avatar with { Hair = Step(Avatar.Hairs.Select(o => o.Name).ToArray(), _avatar.Hair, d) });
        Row(rows, "Horns", () => _avatar.Horns == "none" ? (_avatar.IsDragonkind ? "Swept (dragonkind)" : "None") : _avatar.Horns,
            d => _avatar = _avatar with { Horns = Step(Avatar.HornStyles, _avatar.Horns, d) });
        var quick = new HBoxContainer();
        quick.AddChild(MakeButton("Class look", () =>
        {
            // The class's own clothes back, but they stay who they are.
            _avatar = Avatar.For(_restyling?.Look ?? _look) with { Race = _avatar.Race, Dragon = _avatar.Dragon, Half = _avatar.Half };
            _styled = _avatar.Race != "human";
            ShowLook();
        }, expand: true));
        quick.AddChild(MakeButton("Surprise me", () => { _avatar = Avatar.Random(new Random()); _styled = true; ShowLook(); }, expand: true));
        rows.AddChild(quick);
        split.AddChild(rows);
        _appearance.AddChild(split);
        ShowLook();
        return _appearance;
    }

    void Row(VBoxContainer rows, string name, Func<string> value, Action<int> step)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(104, 0), VerticalAlignment = VerticalAlignment.Center });
        row.AddChild(Arrow("<", () => { step(-1); _styled = true; ShowLook(); }));
        var shown = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _lookValues[name] = shown;
        row.AddChild(shown);
        row.AddChild(Arrow(">", () => { step(1); _styled = true; ShowLook(); }));
        rows.AddChild(row);
        _refresh.Add(() => shown.Text = value());
    }

    readonly List<Action> _refresh = [];

    static Button Arrow(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(46, 38) };
        b.Pressed += pressed;
        return b;
    }

    static string Step(string[] options, string current, int by) =>
        options[((Array.IndexOf(options, current) + by) % options.Length + options.Length) % options.Length];

    void ShowLook()
    {
        foreach (var r in _refresh) r();
        _lookTitle.Text = _restyling is { } sheet ? $"{sheet.Name}'s look" : "Who they are and how they look (the class decides what they carry and wear)";
        var people = _avatar.People;
        _peopleText.Text = $"{_avatar.Kind}{(_avatar.Blood is { } blood ? $" ({blood.Says})" : "")}: {people.Text}\n  + {people.Good}.\n  - {people.Bad}.";
        if (_preview.IsInsideTree()) _preview.Show(_restyling?.Look ?? _look, _avatar);
        else _preview.Ready += () => _preview.Show(_restyling?.Look ?? _look, _avatar);
    }

    /// <summary>Shows this look in the editor, scrolled into view (for screenshots).</summary>
    public void LookForSnapshot(Avatar a)
    {
        _avatar = a;
        _styled = true;
        ShowLook();
    }

    /// <summary>Scrolls the page down to the look editor (once the page has been laid out).</summary>
    public void ScrollToLook() => _scroll.ScrollVertical = (int)_appearance.GlobalPosition.Y - 20 + _scroll.ScrollVertical;

    /// <summary>The look as it's saved: null while it's still just the class's own.</summary>
    string? LookToSave => _styled ? _avatar.Encode() : null;

    /// <summary>Opens the look of a character already made, to change it.</summary>
    public void Restyle(CharacterSheet sheet)
    {
        _restyling = sheet;
        _avatar = Avatar.Parse(sheet.Avatar, sheet.Look) ?? Avatar.For(sheet.Look);
        _styled = sheet.Avatar is not null;
        (_newOnly.Visible, _createButtons.Visible, _restyleButtons.Visible, _list.Visible) = (false, false, true, false);
        ShowLook();
        _scroll.ScrollVertical = 0;
    }

    void EndRestyle()
    {
        _restyling = null;
        _styled = false;
        _avatar = Avatar.For(_look);
        (_newOnly.Visible, _createButtons.Visible, _restyleButtons.Visible, _list.Visible) = (true, true, false, true);
        ShowLook();
    }

    async Task SaveRestyle()
    {
        if (_restyling is not { } sheet) return;
        var restyled = sheet with { Avatar = LookToSave };
        if (_offline)
        {
            var i = _sheets.FindIndex(s => s.Name == sheet.Name);
            if (i >= 0) _sheets[i] = restyled;
            SaveLocalCharacters();
        }
        else
        {
            var reply = await Ask(new AccountRequest("restyle", _account.Text.Trim(), Token: _token, ServerPassword: _serverPassword.Text, Character: restyled));
            if (!reply.Ok) { Say(reply.Error ?? "Couldn't save the look. Is the server up to date?"); return; }
            _sheets = [.. reply.Characters];
        }
        EndRestyle();
        ShowCharacters(_offline);
        Say($"{sheet.Name}'s new look is saved.");
    }

    static Button MakeButton(string text, Action pressed, bool expand = false)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 54) };
        if (expand) b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        b.Pressed += pressed;
        return b;
    }

    // ---- logging in ---------------------------------------------------------------------------

    void LoadSettings()
    {
        try
        {
            if (Godot.FileAccess.FileExists(SettingsPath))
                _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString(SettingsPath)) ?? [];
        }
        catch (JsonException) { }
        _server.Text = _settings.GetValueOrDefault("server", DefaultServer ?? "");
        _serverPassword.Text = _settings.GetValueOrDefault("serverPassword", "");
        _account.Text = _settings.GetValueOrDefault("account", "");
        _quality.Selected = (int)(Enum.TryParse<Graphics.Quality>(_settings.GetValueOrDefault("quality", ""), out var q) ? q : Graphics.Default);
        if (Humans.Enabled != Humans.WantedAt((Graphics.Quality)_quality.Selected)) { Humans.Enabled = Humans.WantedAt((Graphics.Quality)_quality.Selected); RebuildAppearance(); }
        _token = _settings.GetValueOrDefault("token");
        if (_token is not null && _server.Text.Length > 0 && _account.Text.Length > 0) _ = LogIn("login", useToken: true);
    }

    void SaveSettings()
    {
        _settings["server"] = _server.Text.Trim();
        _settings["serverPassword"] = _serverPassword.Text;
        _settings["account"] = _account.Text.Trim();
        _settings["quality"] = ((Graphics.Quality)_quality.Selected).ToString();
        if (_token is null) _settings.Remove("token"); else _settings["token"] = _token;
        using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(_settings));
    }

    /// <summary>Fills the login page (for screenshots and tests).</summary>
    public void Fill(string server, string account, string secret, string serverPassword = "") =>
        (_server.Text, _account.Text, _secret.Text, _serverPassword.Text) = (server, account, secret, serverPassword);

    public async Task<bool> LogIn(string action, bool useToken = false)
    {
        if (_server.Text.Trim().Length == 0) { Say("Enter the server's address, or play alone."); return false; }
        Say(action == "register" ? "Creating your account..." : "Logging in...");
        var reply = await Ask(new AccountRequest(action, _account.Text.Trim(), useToken ? null : _secret.Text, useToken ? _token : null, _serverPassword.Text));
        if (!reply.Ok)
        {
            if (useToken) _token = null;
            Say(reply.Error ?? "That didn't work.");
            return false;
        }
        _token = reply.Token ?? _token;
        _secret.Text = "";
        SaveSettings();
        _sheets = [.. reply.Characters];
        ShowCharacters(offline: false);
        return true;
    }

    /// <summary>Logging out from inside the game: the next start screen asks for the password again.</summary>
    public static void ForgetLogin()
    {
        if (!Godot.FileAccess.FileExists(SettingsPath)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString(SettingsPath)) ?? [];
            if (!settings.Remove("token")) return;
            using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
            file?.StoreString(JsonSerializer.Serialize(settings));
        }
        catch (JsonException) { }
    }

    void LogOut()
    {
        _token = null;
        SaveSettings();
        _characters.Visible = false;
        _login.Visible = true;
        Say("");
    }

    Task<AccountReply> Ask(AccountRequest request) => AccountClient.AskAsync(_server.Text.Trim(), request);

    void Say(string text) => _status.Text = text;

    // ---- characters --------------------------------------------------------------------------

    void ShowCharacters(bool offline)
    {
        _offline = offline;
        if (offline) _sheets = LocalCharacters();
        _who.Text = offline ? "Playing alone, on this device." : $"{_account.Text.Trim()}, on {_server.Text.Trim()}";
        _login.Visible = false;
        _characters.Visible = true;
        foreach (var child in _list.GetChildren()) child.QueueFree();
        if (_sheets.Count == 0) _list.AddChild(new Label { Text = "No characters yet. Make one below.", Modulate = new Color(1, 1, 1, 0.6f) });
        foreach (var sheet in _sheets)
        {
            var row = new HBoxContainer();
            row.AddChild(new TextureRect { Texture = Portrait(sheet.Look) ?? Portrait("Knight"), CustomMinimumSize = new Vector2(56, 56), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
            row.AddChild(new Label
            {
                Text = $"{sheet.Name}\n   {(Avatar.Parse(sheet.Avatar, sheet.Look) is { } who ? who.Kind + " " : "")}{ClassName(sheet.Look)}, {sheet.Element}, {sheet.Crowns} crowns{(sheet.Where.Length > 0 ? $", last seen at {sheet.Where}" : "")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
            row.AddChild(MakeButton("Play", () => _ = Choose(sheet)));
            row.AddChild(MakeButton("Look", () => Restyle(sheet)));
            Button? delete = null;
            delete = MakeButton("Delete", () =>
            {
                // Deleting loses the character's progress, so it takes a second tap.
                if (delete!.Text == "Delete") { delete.Text = "Sure?"; return; }
                _ = Delete(sheet);
            });
            row.AddChild(delete);
            _list.AddChild(row);
        }
        Say("");
    }

    public IReadOnlyList<CharacterSheet> Characters => _sheets;

    static string ClassName(string look) => Classes.For(look).Name;

    /// <summary>Shows the characters page with an example character (for a screenshot of the layout).</summary>
    public Task PlayAloneForSnapshot()
    {
        _sheets = [new("Bram Ashmane", "Barbarian", "Frost", DateTime.UtcNow, 212, "Hawkwake Mine"), new("Ilka", "Warlock", "Shock", DateTime.UtcNow)];
        _offline = false;
        ShowCharacters(offline: false);
        _who.Text = "Joe, on https://seasonal-deck-organisms-sf.trycloudflare.com";
        _newName.Text = "Sorrel Wynn";
        return Task.CompletedTask;
    }

    public void NameNewCharacter(string name) => _newName.Text = name;

    public async Task<bool> Create()
    {
        var sheet = new CharacterSheet(_newName.Text.Trim(), _look, Elements[_element.Selected], DateTime.UtcNow, Avatar: LookToSave);
        if (!AccountStore.ValidName(sheet.Name)) { Say("Names are 3-24 letters, digits, spaces, _ - or '."); return false; }
        if (_offline)
        {
            if (_sheets.Any(s => s.Name.Equals(sheet.Name, StringComparison.OrdinalIgnoreCase))) { Say("You already have a character by that name."); return false; }
            _sheets.Add(sheet);
            SaveLocalCharacters();
        }
        else
        {
            var reply = await Ask(new AccountRequest("create", _account.Text.Trim(), Token: _token, ServerPassword: _serverPassword.Text, Character: sheet));
            if (!reply.Ok) { Say(reply.Error ?? "Couldn't make that character."); return false; }
            _sheets = [.. reply.Characters];
        }
        _newName.Text = "";
        _styled = false;
        _avatar = Avatar.For(_look);
        ShowLook();
        ShowCharacters(_offline);
        return true;
    }

    async Task Delete(CharacterSheet sheet)
    {
        if (_offline)
        {
            _sheets.RemoveAll(s => s.Name == sheet.Name);
            SaveLocalCharacters();
        }
        else
        {
            var reply = await Ask(new AccountRequest("delete", _account.Text.Trim(), Token: _token, ServerPassword: _serverPassword.Text, Character: sheet));
            if (!reply.Ok) { Say(reply.Error ?? "Couldn't delete."); return; }
            _sheets = [.. reply.Characters];
        }
        ShowCharacters(_offline);
    }

    /// <summary>Starts the game as this character, with their progress from the server if logged in.</summary>
    public async Task Choose(CharacterSheet sheet)
    {
        SaveSettings();
        var quality = (Graphics.Quality)_quality.Selected;
        if (_offline)
        {
            Play?.Invoke(new StartChoice(sheet, "off", "", null, null, null, quality));
            QueueFree();
            return;
        }
        Say($"Loading {sheet.Name}...");
        var reply = await Ask(new AccountRequest("load", _account.Text.Trim(), Token: _token, ServerPassword: _serverPassword.Text, Character: sheet));
        if (!reply.Ok) { Say(reply.Error ?? "Couldn't load that character."); return; }
        Play?.Invoke(new StartChoice(sheet, _server.Text.Trim(), _serverPassword.Text, _account.Text.Trim(), _token, reply.Save, quality));
        QueueFree();
    }

    List<CharacterSheet> LocalCharacters()
    {
        try
        {
            return Godot.FileAccess.FileExists(LocalCharactersPath)
                ? JsonSerializer.Deserialize<List<CharacterSheet>>(Tristinwold.Deviant.SaveUpgrade.Upgrade(Godot.FileAccess.GetFileAsString(LocalCharactersPath)), Wire.Options) ?? []
                : [];
        }
        catch (JsonException) { return []; }
    }

    void SaveLocalCharacters()
    {
        using var file = Godot.FileAccess.Open(LocalCharactersPath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(_sheets, Wire.Options));
    }
}
