using Godot;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Saving on demand, the game menu (Esc, or Menu on a phone) and leaving the game: logging out,
/// changing character or quitting all save first, and wait for the server to have the save.
/// </summary>
public partial class Main
{
    /// <summary>Set when the player logs out, so the reloaded game shows the start screen even if the
    /// command line would have logged straight in.</summary>
    static bool LeftToStart;

    Spot? _spot;
    double _savedAt;
    bool _leaving;

    /// <summary>Where the hero stands, for the save. While they're down, the last place they stood.</summary>
    Spot? Here()
    {
        if (_player.IsInsideTree() && !_player.Downed && !_player.Stats.Dead) // closing: the last place they stood
        {
            var p = _player.GlobalPosition;
            _spot = new Spot(p.X, _player.Mount is null ? p.Y : City.FloorAt(p), p.Z, _player.Rotation.Y); // off the dragon's back
        }
        return _spot;
    }

    /// <summary>Puts the hero back where the last session ended.</summary>
    void StandAt(Spot where)
    {
        var at = new Vector3(where.X, 0, where.Z);
        _terrain.EnsureAround(at, 2);
        _player.GlobalPosition = at with { Y = Mathf.Max(where.Y, City.FloorAt(at)) + 0.3f };
        _player.Rotation = new Vector3(0, where.Facing, 0);
        _player.ResetPhysicsInterpolation();
        _spot = where;
        GD.Print($"Back where {_player.Company} left off: {where.X:0}, {where.Z:0}");
    }

    /// <summary>
    /// Saves now. For a logged-in character it also waits for the server to say it has the save
    /// (false if it didn't). <paramref name="announce"/> says so on screen.
    /// </summary>
    public async Task<bool> SaveNow(bool announce = false)
    {
        var json = Save();
        _lastSave = _clock;
        if (Choice.Account is null)
        {
            if (announce) _hud.Toast("Saved.", 3);
            return true;
        }
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var reply = await AccountClient.AskAsync(Choice.Server, new AccountRequest("save", Choice.Account, Token: Choice.Token,
            ServerPassword: Choice.ServerPassword, Character: Choice.Character, Progress: new SaveProgress(json, _player.Crowns, _area)), wait.Token);
        if (reply.Ok) _lastSent = json;
        if (!reply.Ok) _hud.Toast($"The server didn't take the save: {reply.Error}", 8);
        else if (announce) _hud.Toast("Saved, on the server too.", 3);
        return reply.Ok;
    }

    /// <summary>Esc (or Android's back button): closes whatever is open, else opens the game menu.</summary>
    void Back()
    {
        if (_hud.Talking) _hud.EndTalk();
        else if (_hud.MapOpen) _hud.ToggleMap();
        else if (_hud.WindowOpen) _hud.ToggleWindow();
        else GameMenu();
    }

    public void GameMenu()
    {
        if (_leaving) return;
        var ago = (int)(_clock - _savedAt);
        var options = new List<(string, Action)> { ("Save now (F5)", () => { _ = SaveNow(announce: true); _hud.EndTalk(); }) };
        if (Choice.Account is not null)
        {
            options.Add(("Change character", () => _ = Leave("Change character", () => Restart(forget: false))));
            options.Add(("Log out", () => _ = Leave("Log out", () => Restart(forget: true))));
        }
        else options.Add(("Back to the start screen", () => _ = Leave("Leave", () => Restart(forget: false))));
        options.Add(("Sound and music", SoundMenu));
        if (RemotePlay.Current is null) options.Add(("Quit the game", () => _ = Leave("Quit", Quit))); // on a phone, the PC's game must stay open
        options.Add(("Back to the game", _hud.EndTalk));
        _hud.Talk("Tristinwold", $"{_player.Company}, {_player.Class.Name}, level {_player.Hero.Level}. " +
            $"Saved {(ago < 60 ? $"{ago} second{(ago == 1 ? "" : "s")}" : $"{ago / 60} minute{(ago < 120 ? "" : "s")}")} ago. " +
            "The game saves by itself as you play, and before you log out or quit.", [.. options]);
    }

    /// <summary>The volume sliders (all sound, music, ambience, effects), kept on this device.</summary>
    void SoundMenu()
    {
        _hud.Talk("Sound and music", "Slide to set each volume; it's kept on this device.", ("Back to the menu", GameMenu), ("Back to the game", _hud.EndTalk));
        _hud.AddToTalk(Sound.Sliders());
    }

    /// <summary>Saves, then does <paramref name="then"/>; if the server didn't take the save, asks first.</summary>
    async Task Leave(string doing, Action then)
    {
        if (_leaving) return;
        _leaving = true;
        _hud.Talk("Tristinwold", "Saving...");
        var ok = await SaveNow();
        _leaving = false;
        if (ok) { then(); return; }
        _hud.Talk("Tristinwold", "The server didn't answer, so the last moments of play may not be saved there " +
            "(everything up to the last autosave is). Is the server still running?",
            ("Try again", () => _ = Leave(doing, then)), ($"{doing} anyway", then), ("Back to the game", _hud.EndTalk));
    }

    /// <summary>Back to the start screen; <paramref name="forget"/> logs out, so it asks for the password again.</summary>
    void Restart(bool forget)
    {
        if (forget) StartScreen.ForgetLogin();
        LeftToStart = true;
        Input.MouseMode = Input.MouseModeEnum.Visible; // the start screen needs the pointer back
        GetTree().ReloadCurrentScene();
    }

    void Quit() => GetTree().Quit();

    public void StandAtForTest(Spot where) => StandAt(where);

    /// <summary>For the self-test: logs out the way the menu does; the reloaded game reports back and quits.</summary>
    public void LeaveForTest() => _ = Leave("Leave", () => Restart(forget: false));

    /// <summary>After a self-test's logout: the start screen is up again, so the reload worked.</summary>
    async void ReportBackAtStart()
    {
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var back = GetChildren().OfType<StartScreen>().Any();
        GD.Print(back ? "ok   logging out goes back to the start screen" : "FAIL logging out didn't bring back the start screen");
        GD.Print(back ? "SELFTEST PASSED" : "SELFTEST FAILED");
        GetTree().Quit(back ? 0 : 1);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            if (_player is null) Quit(); // still on the start screen
            else _ = Leave("Quit", Quit);
        }
        else if (what == NotificationWMGoBackRequest && _player is not null) Back();
        else if (what == NotificationApplicationPaused && _player is not null) Save(); // a phone switching away may never come back
    }
}
