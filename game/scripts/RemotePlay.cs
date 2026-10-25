using System.Text.Json;
using Godot;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Remote play: this computer runs the game and a phone plays it in its web browser, through a
/// Cloudflare tunnel, so an iPhone needs nothing installed. Each frame (as often as the phone
/// keeps up) the game's picture goes out as a JPEG; the phone's touches come back as real touches,
/// so the on-screen controls work exactly as on a phone, and its keyboard types into the game.
///   godot --path game -- --remote --tunnel
/// </summary>
public partial class RemotePlay(Main main, int port, bool tunnel, string cloudflared, bool lan) : Node
{
    RemoteScreen? _screen;
    Tunnel? _tunnel;
    Telegram? _telegram;
    double _since;
    bool _typing, _watched;

    /// <summary>The longest side of the pictures sent to the phone, in pixels.</summary>
    public int Width { get; set; } = 1024;

    /// <summary>JPEG quality, 0 to 1: lower is faster on a slow connection.</summary>
    public float Quality { get; set; } = 0.6f;

    /// <summary>The most pictures a second to send.</summary>
    public int Rate { get; set; } = 30;

    public string? Link { get; private set; }

    /// <summary>The remote play that's running: it sits on the tree's root, so logging out (which
    /// reloads the game) keeps the phone's link and connection.</summary>
    public static RemotePlay? Current { get; set; }

    /// <summary>The game it shows; a new one after logging out.</summary>
    public Main Main { get; set; } = main;

    public int Viewers => _screen?.Viewers ?? 0;

    public override void _Ready()
    {
        _screen = new RemoteScreen(port, log: Say, everywhere: lan);
        try { _screen.Start(); }
        catch (System.Net.Sockets.SocketException e)
        {
            Say($"Remote play couldn't open port {port}: {e.Message}. Try --remote {port + 1}.");
            _screen = null;
            return;
        }
        _screen.Connected += _ => Callable.From(() => Main.EnableTouchSoon()).CallDeferred();
        Say($"Remote play on this computer: {_screen.LocalUrl}");
        if (lan) Say($"On your home Wi-Fi, use this computer's address instead of localhost, port {_screen.Port}.");
        _telegram = Telegram.FromEnvironment(Say);
        if (!tunnel) return;
        _tunnel = new Tunnel(cloudflared, _screen.Port, Say);
        _tunnel.Start(url =>
        {
            Link = _screen.LinkFor(url);
            Say($"Play from your phone: {Link}");
            if (_telegram is null) Say("Telegram isn't set up, so the link wasn't sent there (set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID).");
            else _telegram.SendAsync($"Tristinwold is ready to play from your iPhone. Open this link in Safari, turn the phone sideways, and log in:\n{Link}\n\n(In Safari: Share, then Add to Home Screen, and it opens full screen. The link changes each time the game is started on the PC; the new one is always posted here.)")
                .ContinueWith(sent => { if (sent.Result) Say("Sent the link to your Telegram group."); });
            Callable.From(() => Main.ShowRemoteLink(Link)).CallDeferred();
        });
    }

    void Say(string text)
    {
        GD.Print(text);
        Callable.From(() => Main.RemoteNote(text)).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_screen is null) return;
        while (_screen.Input.TryDequeue(out var json)) Handle(json);
        if ((_screen.Viewers > 0) != _watched) { _watched = _screen.Viewers > 0; Main.RemoteQrVisible(!_watched); }

        var typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;
        if (typing != _typing && _screen.Viewers > 0) _screen.Tell(typing ? """{"keyboard":true}""" : """{"keyboard":false}""");
        _typing = typing;

        _since += delta;
        if (_screen.Viewers == 0 || !_screen.Wanted || _since < 1.0 / Rate) return;
        _since = 0;
        var image = GetViewport().GetTexture().GetImage();
        var (w, h) = (image.GetWidth(), image.GetHeight());
        if (Math.Max(w, h) > Width)
        {
            var s = (float)Width / Math.Max(w, h);
            image.Resize((int)(w * s), (int)(h * s), Image.Interpolation.Bilinear);
        }
        _screen.Show(image.SaveJpgToBuffer(Quality));
    }

    // ---- input from the phone --------------------------------------------------------------------

    readonly Dictionary<int, Vector2> _fingers = [];

    void Handle(string json)
    {
        JsonElement e;
        try { e = JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { return; }
        if (!e.TryGetProperty("t", out var t)) return;
        switch (t.GetString())
        {
            case "d" or "m" or "u":
                Touch(t.GetString()!, e.GetProperty("i").GetInt32(), e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle());
                break;
            case "text":
                foreach (var c in e.GetProperty("s").GetString() ?? "") Type(c);
                break;
            case "key":
                Press(e.GetProperty("k").GetString() ?? "", true);
                Press(e.GetProperty("k").GetString() ?? "", false);
                break;
            case "press":
                Press(e.GetProperty("k").GetString() ?? "", e.GetProperty("p").GetBoolean());
                break;
        }
    }

    /// <summary>A finger, as a fraction of the picture, becomes a touch at that spot in the game's window.</summary>
    void Touch(string kind, int finger, float fx, float fy)
    {
        var size = (Vector2)GetWindow().Size;
        var at = new Vector2(Mathf.Clamp(fx, 0, 1) * size.X, Mathf.Clamp(fy, 0, 1) * size.Y);
        var index = finger % 32;
        if (kind == "m")
        {
            var last = _fingers.GetValueOrDefault(index, at);
            _fingers[index] = at;
            Input.ParseInputEvent(new InputEventScreenDrag { Index = index, Position = at, Relative = at - last, ScreenRelative = at - last });
            return;
        }
        if (kind == "d") _fingers[index] = at; else _fingers.Remove(index);
        Input.ParseInputEvent(new InputEventScreenTouch { Index = index, Position = at, Pressed = kind == "d" });
    }

    static void Type(char c)
    {
        foreach (var pressed in new[] { true, false })
            Input.ParseInputEvent(new InputEventKey { Unicode = c, Pressed = pressed });
    }

    static void Press(string name, bool pressed)
    {
        var key = OS.FindKeycodeFromString(name);
        if (key == Key.None) return;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    }

    public override void _ExitTree()
    {
        _tunnel?.Dispose();
        _screen?.Dispose();
    }
}
