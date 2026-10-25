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
    string? _token;
    bool _offline;
    List<CharacterSheet> _sheets = [];

    static LineEdit Field(string hint, bool secret = false) =>
        new() { PlaceholderText = hint, Secret = secret, CustomMinimumSize = new Vector2(0, 50) };

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color("#15130f"), AnchorRight = 1, AnchorBottom = 1 });
        var scroll = new ScrollContainer { AnchorRight = 1, AnchorBottom = 1, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
        quality.AddChild(_quality);
        _login.AddChild(quality);
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
        _characters.AddChild(new Label { Text = "A new character", Modulate = new Color("#ffd27a") });
        _characters.AddChild(_newName);
        // Pick a class: three callings, three classes each, a portrait for each.
        var picks = new List<Button>();
        foreach (var calling in Enum.GetValues<Calling>())
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = calling + "s", CustomMinimumSize = new Vector2(96, 0), Modulate = new Color("#ffd27a"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var d in Classes.Of(calling))
            {
                var pick = new Button
                {
                    CustomMinimumSize = new Vector2(176, 150), ToggleMode = true, ButtonPressed = d.Look == _look, Text = d.Name,
                    Icon = Portrait(d.Look), IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top, ExpandIcon = true,
                };
                pick.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("#3a3226"), BorderColor = new Color("#ffd27a"), BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3 });
                pick.Pressed += () => { _look = d.Look; foreach (var b in picks) b.ButtonPressed = b == pick; Describe(d); };
                picks.Add(pick);
                row.AddChild(pick);
            }
            _swatches.AddChild(row);
        }
        _characters.AddChild(_swatches);
        _characters.AddChild(_about);
        Describe(Classes.Get(_look));
        var make = new HBoxContainer();
        make.AddChild(new Label { Text = "Starting spell", CustomMinimumSize = new Vector2(150, 0) });
        foreach (var e in Elements) _element.AddItem(e);
        make.AddChild(_element);
        _characters.AddChild(make);
        _characters.AddChild(MakeButton("Create character", () => _ = Create()));

        page.AddChild(_status);
        LoadSettings();
    }

    /// <summary>What a class is like, and how it fights.</summary>
    void Describe(Discipline d) => _about.Text =
        $"{d.Name}: {d.Text}\n  Left: {d.Primary.Name}.  Right: {d.Signature.Name}.  Q: {d.Utility.Name}.  R: {d.Limit.Name}.  Always: {d.Passive.Name}.";

    /// <summary>Picks a class on the character page (for screenshots).</summary>
    public void PickClass(string look) { _look = look; Describe(Classes.Get(look)); }

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
        _server.Text = _settings.GetValueOrDefault("server", "");
        _serverPassword.Text = _settings.GetValueOrDefault("serverPassword", "");
        _account.Text = _settings.GetValueOrDefault("account", "");
        _quality.Selected = (int)(Enum.TryParse<Graphics.Quality>(_settings.GetValueOrDefault("quality", ""), out var q) ? q : Graphics.Default);
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
                Text = $"{sheet.Name}\n   {ClassName(sheet.Look)}, {sheet.Element}, {sheet.Septims} septims{(sheet.Where.Length > 0 ? $", last seen at {sheet.Where}" : "")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
            row.AddChild(MakeButton("Play", () => _ = Choose(sheet)));
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
        var sheet = new CharacterSheet(_newName.Text.Trim(), _look, Elements[_element.Selected], DateTime.UtcNow);
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
                ? JsonSerializer.Deserialize<List<CharacterSheet>>(Godot.FileAccess.GetFileAsString(LocalCharactersPath), Wire.Options) ?? []
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
