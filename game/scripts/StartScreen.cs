using Godot;
using System.Text.Json;

namespace Tristinwold.Game;

/// <summary>
/// The first screen: who you are and which server to join. Phones have no command line, so this
/// is how they get the Cloudflare tunnel's address. Remembers what was typed last time.
/// </summary>
public partial class StartScreen : CanvasLayer
{
    const string SettingsPath = "user://settings.json";

    public event Action<string, string, string>? Play; // company, server ("off" to play alone), password

    readonly LineEdit _company = new() { PlaceholderText = "Your company's name", CustomMinimumSize = new Vector2(0, 52) };
    readonly LineEdit _server = new() { PlaceholderText = "Blank to play alone, or e.g. https://word-word-word.trycloudflare.com", CustomMinimumSize = new Vector2(0, 52) };
    readonly LineEdit _password = new() { PlaceholderText = "Password, if the server has one", Secret = true, CustomMinimumSize = new Vector2(0, 52) };

    public override void _Ready()
    {
        var back = new ColorRect { Color = new Color("#15130f"), AnchorRight = 1, AnchorBottom = 1 };
        AddChild(back);
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(centre);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(640, 0) };
        box.AddThemeConstantOverride("separation", 12);
        centre.AddChild(box);

        box.AddChild(new Label { Text = "The Rings of Tristinwold", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("#ffd27a"), LabelSettings = new LabelSettings { FontSize = 40 } });
        box.AddChild(new Label { Text = "A nemesis-haunted world shared by every company.", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("#e8dcc4") });
        box.AddChild(_company);
        box.AddChild(_server);
        box.AddChild(_password);
        var play = new Button { Text = "Play", CustomMinimumSize = new Vector2(0, 64) };
        play.Pressed += Go;
        box.AddChild(play);
        box.AddChild(new Label
        {
            Text = "The server's host can give you its address. On a PC at home that is like 192.168.1.20:7777; "
                 + "a server started with --tunnel prints an https:// address that works from anywhere, phones included.",
            AutowrapMode = TextServer.AutowrapMode.Word, Modulate = new Color(1, 1, 1, 0.6f),
        });

        try
        {
            if (Godot.FileAccess.FileExists(SettingsPath) &&
                JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString(SettingsPath)) is { } saved)
            {
                _company.Text = saved.GetValueOrDefault("company", "");
                _server.Text = saved.GetValueOrDefault("server", "");
                _password.Text = saved.GetValueOrDefault("password", "");
            }
        }
        catch (JsonException) { /* start blank */ }
        _password.TextSubmitted += _ => Go();
    }

    public void Fill(string company, string server, string password) => (_company.Text, _server.Text, _password.Text) = (company, server, password);

    public void Go()
    {
        var company = _company.Text.Trim().Length > 0 ? _company.Text.Trim() : "Adventurer";
        var server = _server.Text.Trim();
        using (var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write))
            file?.StoreString(JsonSerializer.Serialize(new Dictionary<string, string> { ["company"] = company, ["server"] = server, ["password"] = _password.Text }));
        Play?.Invoke(company, server.Length == 0 ? "off" : server, _password.Text);
        QueueFree();
    }
}
