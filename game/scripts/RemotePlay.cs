using System.Text.Json;
using Godot;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Remote play: this computer runs the game and a phone plays it in its web browser, through a
/// Cloudflare tunnel, so an iPhone needs nothing installed. Each frame (as often as the phone
/// keeps up) the game's picture goes out as a JPEG, and its sound goes too; the phone's touches come back as real touches,
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
    public int Rate { get; set; } = 45;

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
            GD.Print($"Play from your phone: {Link}"); // on screen it is the "Phone link" line already, and the QR code
            if (_telegram is null) Say("Telegram isn't set up, so the link wasn't sent there (set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID).");
            else _telegram.SendAsync($"Tristinwold is ready to play from your iPhone. Open this link in Safari and log in (hold the phone sideways for the full picture, or upright for a controller under it):\n{Link}\n\n(In Safari: Share, then Add to Home Screen, and it opens full screen. The link changes each time the game is started on the PC; the new one is always posted here.)")
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
        if ((_screen.Viewers > 0) != _watched)
        {
            _watched = _screen.Viewers > 0;
            Main.RemoteQrVisible(!_watched);
            _padSent = "";
            if (!_watched)
            {
                // The phone's gone: let go of its sticks, so nothing keeps walking or turning.
                _pan = Vector2.Zero;
                foreach (var a in new[] { "move_left", "move_right", "move_forward", "move_back" }) Input.ActionRelease(a);
            }
        }
        if (_watched && Upright) TellPad(delta);
        // The camera stick turns the view for as long as it's held, faster the further it's pushed.
        if (_pan != Vector2.Zero) Main.LookFromPhone(new Vector2(_pan.X * Mathf.Abs(_pan.X), _pan.Y * Mathf.Abs(_pan.Y)) * PanSpeed * (float)delta);

        var typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;
        if (typing != _typing && _screen.Viewers > 0) _screen.Tell(typing ? """{"keyboard":true}""" : """{"keyboard":false}""");
        _typing = typing;

        Listen();
        _since += delta;
        if (_screen.Viewers == 0 || !_screen.Wanted || _encoding || _since < 1.0 / Rate) return;
        _since = 0;
        // Only the picture is taken here; shrinking and squeezing it to a JPEG happens off the game's own loop,
        // so streaming to the phone doesn't cost the game frames.
        var image = GetViewport().GetTexture().GetImage();
        var (screen, width, quality) = (_screen, Width, Quality);
        _encoding = true;
        Task.Run(() =>
        {
            try
            {
                var (w, h) = (image.GetWidth(), image.GetHeight());
                if (Math.Max(w, h) > width)
                {
                    var s = (float)width / Math.Max(w, h);
                    image.Resize((int)(w * s), (int)(h * s), Image.Interpolation.Bilinear);
                }
                screen.Show(image.SaveJpgToBuffer(quality));
            }
            catch (Exception e) { GD.PushWarning($"Remote play couldn't send a picture: {e.Message}"); }
            finally { _encoding = false; }
        });
    }

    volatile bool _encoding;

    // ---- the sound -----------------------------------------------------------------------------

    AudioEffectCapture? _capture;
    double _phase;

    /// <summary>
    /// The game's sound to the phone too: everything the game plays is caught as it goes out, mixed down to one
    /// channel, brought down to <see cref="RemoteAudio.Rate"/> and sent every twentieth of a second or so. The
    /// phone plays it once it's been tapped (a phone won't play a web page's sound before that).
    /// </summary>
    void Listen()
    {
        if (_screen is null) return;
        if (_capture is null)
        {
            if (_screen.Viewers == 0) return;
            _capture = new AudioEffectCapture { BufferLength = 0.5f };
            AudioServer.AddBusEffect(AudioServer.GetBusIndex("Master"), _capture);
        }
        var mix = AudioServer.GetMixRate();
        var available = _capture.GetFramesAvailable();
        if (_screen.Viewers == 0) { if (available > 0) _capture.ClearBuffer(); return; }
        if (available < mix * 0.05) return;
        var frames = _capture.GetBuffer(available);
        var step = mix / RemoteAudio.Rate;
        var mono = new List<float>((int)(frames.Length / step) + 2);
        // Each sample the phone gets is the average of the game's samples it stands for, which keeps the sound
        // clean as the rate comes down.
        for (; _phase < frames.Length; _phase += step)
        {
            var (from, to) = ((int)_phase, Math.Min(frames.Length, (int)(_phase + step)));
            var sum = 0f;
            for (var i = from; i < Math.Max(from + 1, to); i++) sum += (frames[i].X + frames[i].Y) * 0.5f;
            mono.Add(sum / Math.Max(1, to - from));
        }
        _phase -= frames.Length;
        _screen.Hear(RemoteAudio.Packet(mono.ToArray()));
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
            // The upright phone's controller.
            case "layout":
                Upright = e.GetProperty("upright").GetBoolean();
                Main.PhonePad(Upright);
                break;
            case "act" when e.GetProperty("a").GetString() is { } action && PadActions.Contains(action):
                // Through the input queue, like a key, so the game sees it as just pressed on its next frame.
                Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = e.GetProperty("p").GetBoolean(), Strength = 1 });
                break;
            case "stick":
                Axis("move_right", "move_left", e.GetProperty("x").GetSingle());
                Axis("move_back", "move_forward", e.GetProperty("y").GetSingle());
                break;
            case "pan":
                _pan = new Vector2(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle());
                break;
            case "look":
                Main.LookFromPhone(new Vector2(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle()) * LookSpeed);
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

    /// <summary>The phone is upright, playing with the controller under the picture.</summary>
    public bool Upright { get; private set; }

    /// <summary>What the controller's buttons may press: the game's own actions, nothing else.</summary>
    static readonly HashSet<string> PadActions =
    [
        "attack", "spell", "limit", "jump", "interact", "cycle_element", "sprint", "ride", "descend",
        "map", "journal", "pack", "party", "skills", "drink", "rest", "menu", "game_menu",
    ];

    /// <summary>Radians of turn for each point the thumb drags across the phone's picture.</summary>
    const float LookSpeed = 0.009f;

    /// <summary>Radians a second the camera stick turns the view, pushed all the way (sideways; half that up and down).</summary>
    static readonly Vector2 PanSpeed = new(2.6f, 1.3f);

    Vector2 _pan;

    static void Axis(string positive, string negative, float value)
    {
        if (value > 0.15f) { Input.ActionPress(positive, value); Input.ActionRelease(negative); }
        else if (value < -0.15f) { Input.ActionPress(negative, -value); Input.ActionRelease(positive); }
        else { Input.ActionRelease(positive); Input.ActionRelease(negative); }
    }

    double _padSince;
    string _padSent = "";

    /// <summary>Tells an upright phone what its buttons are called now (the class's moves) and which are lit.</summary>
    void TellPad(double delta)
    {
        if ((_padSince += delta) < 0.4 || _screen is null) return;
        _padSince = 0;
        if (Main.PadState() is not { } pad) return;
        var json = JsonSerializer.Serialize(new { pad });
        if (json == _padSent) return;
        _padSent = json;
        _screen.Tell(json);
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
