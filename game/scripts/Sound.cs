using System.Text.Json;
using Godot;
using Tristinwold.Deviant;

namespace Tristinwold.Game;

/// <summary>
/// Everything the game sounds like: music that follows what's happening (exploring, the city, a tavern, a fight,
/// a boss, the title), ambience for where you are and the time of day, and effects for blows, spells, creatures,
/// footsteps and the menus. Each has its own volume (and there's one over all of them), kept on this device.
/// <para>
/// Sounds are found by name under <c>res://assets/audio</c>: <c>Play("melee/hit")</c> picks one of
/// <c>sfx/melee/hit*.ogg</c> at random, with a little change of pitch so no two blows sound the same. Music is
/// <c>music/{mood}*.ogg</c>, ambience <c>amb/{name}*.ogg</c>. A sound that isn't there is simply silent.
/// </para>
/// </summary>
public partial class Sound : Node
{
    public static Sound? Current { get; private set; }

    public const string Master = "Master", Music = "Music", Ambience = "Ambience", Effects = "Effects";
    public static readonly string[] Buses = [Master, Music, Ambience, Effects];
    const string Root = "res://assets/audio", SettingsPath = "user://sound.json";

    static readonly Dictionary<string, float> Volumes = new() { [Master] = 0.8f, [Music] = 0.55f, [Ambience] = 0.7f, [Effects] = 0.85f };
    static readonly Dictionary<string, string[]> Found = [];
    static readonly Dictionary<string, AudioStream?> Loaded = [];
    static bool _settingsRead;

    readonly AudioStreamPlayer[] _music = [new(), new()];
    readonly AudioStreamPlayer[] _ambience = [new(), new()];
    readonly List<AudioStreamPlayer> _flat = [];
    readonly List<AudioStreamPlayer3D> _placed = [];
    int _musicOn, _ambienceOn;
    string _mood = "", _place = "";
    string? _lastTrack;
    readonly Dictionary<string, double> _lastPlayed = [];

    public string Mood => _mood;
    public string Place => _place;

    public override void _Ready()
    {
        Current = this;
        ProcessMode = ProcessModeEnum.Always; // the music plays on in menus
        EnsureBuses();
        foreach (var p in _music) { p.Bus = Music; p.VolumeDb = -80; AddChild(p); p.Finished += NextTrack; }
        foreach (var p in _ambience) { p.Bus = Ambience; p.VolumeDb = -80; AddChild(p); p.Finished += () => { if (p == _ambience[_ambienceOn]) p.Play(); }; }
        // Every button in the game clicks.
        GetTree().NodeAdded += n => { if (n is BaseButton b) b.Pressed += () => Play("ui/click", volumeDb: -6); };
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
    }

    // ---- volumes --------------------------------------------------------------------------------------------

    /// <summary>Makes the music, ambience and effects buses (each feeding the master) and sets their volumes.</summary>
    static void EnsureBuses()
    {
        ReadSettings();
        foreach (var bus in Buses)
        {
            if (AudioServer.GetBusIndex(bus) < 0)
            {
                AudioServer.AddBus();
                var i = AudioServer.BusCount - 1;
                AudioServer.SetBusName(i, bus);
                AudioServer.SetBusSend(i, Master);
            }
            Apply(bus);
        }
    }

    public static float Volume(string bus) { ReadSettings(); return Volumes.GetValueOrDefault(bus, 1); }

    /// <summary>Sets a volume (0 to 1) and remembers it on this device.</summary>
    public static void SetVolume(string bus, float volume)
    {
        ReadSettings();
        Volumes[bus] = Mathf.Clamp(volume, 0, 1);
        if (AudioServer.GetBusIndex(bus) < 0) EnsureBuses();
        Apply(bus);
        try
        {
            using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
            file?.StoreString(JsonSerializer.Serialize(Volumes));
        }
        catch (Exception e) { GD.PrintErr($"Couldn't keep the sound settings: {e.Message}"); }
    }

    static void Apply(string bus)
    {
        var i = AudioServer.GetBusIndex(bus);
        if (i < 0) return;
        var v = Volumes.GetValueOrDefault(bus, 1);
        AudioServer.SetBusMute(i, v <= 0.001f);
        AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(Mathf.Max(v, 0.001f)));
    }

    static void ReadSettings()
    {
        if (_settingsRead) return;
        _settingsRead = true;
        try
        {
            if (!Godot.FileAccess.FileExists(SettingsPath)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, float>>(Godot.FileAccess.GetFileAsString(SettingsPath));
            foreach (var (bus, v) in saved ?? []) if (Volumes.ContainsKey(bus)) Volumes[bus] = Mathf.Clamp(v, 0, 1);
        }
        catch (JsonException) { }
    }

    /// <summary>A slider for each volume, for the start screen and the game menu.</summary>
    public static Control Sliders()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        foreach (var bus in Buses)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = bus == Master ? "Volume" : bus, CustomMinimumSize = new Vector2(110, 0), VerticalAlignment = VerticalAlignment.Center });
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = Volume(bus), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };
            var shown = new Label { Text = $"{Volume(bus) * 100:0}%", CustomMinimumSize = new Vector2(56, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var name = bus;
            slider.ValueChanged += v =>
            {
                SetVolume(name, (float)v);
                shown.Text = $"{v * 100:0}%";
                if (name == Effects) Current?.Play("ui/click", volumeDb: -4); // hear what it sounds like now
            };
            row.AddChild(slider);
            row.AddChild(shown);
            box.AddChild(row);
        }
        return box;
    }

    // ---- finding sounds -----------------------------------------------------------------------------------

    /// <summary>The sound files in a folder whose names start with <paramref name="prefix"/> (in an export too, where the files go by their imported names).</summary>
    static string[] Files(string folder, string prefix)
    {
        var key = $"{folder}|{prefix}";
        if (Found.TryGetValue(key, out var files)) return files;
        // The resource listing knows what an export has under its imported names; the folder itself is the fallback.
        var listed = ResourceLoader.ListDirectory($"{Root}/{folder}");
        var names = (listed.Length > 0 ? listed : DirAccess.GetFilesAt($"{Root}/{folder}"))
            .Select(f => f.EndsWith(".import") ? f[..^7] : f.EndsWith(".remap") ? f[..^6] : f)
            .Where(f => (f.EndsWith(".ogg") || f.EndsWith(".wav") || f.EndsWith(".mp3")) && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct().Order().Select(f => $"{Root}/{folder}/{f}").ToArray();
        return Found[key] = names;
    }

    static AudioStream? Load(string path)
    {
        if (!Loaded.TryGetValue(path, out var s))
            Loaded[path] = s = ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
        return s;
    }

    /// <summary>Whether there's any sound by this name (sfx/<paramref name="name"/>*).</summary>
    public static bool Has(string name) => Pick(name) is not null;

    /// <summary>One of the files by this name: "melee/hit" is hit_1, hit_2... (not hit_metal_1, which is "melee/hit_metal");
    /// a name with no numbered files of its own takes everything that starts with it ("creature/growl": small and big).</summary>
    static string? Pick(string name)
    {
        var slash = name.LastIndexOf('/');
        var stem = name[(slash + 1)..];
        var files = Files("sfx/" + name[..Math.Max(0, slash)], stem);
        var exact = files.Where(f => System.Text.RegularExpressions.Regex.IsMatch(f[(f.LastIndexOf('/') + 1)..], $"^{System.Text.RegularExpressions.Regex.Escape(stem)}(_\\d+)?\\.[a-z0-9]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray();
        if (exact.Length > 0) files = exact;
        return files.Length == 0 ? null : files[Random.Shared.Next(files.Length)];
    }

    // ---- effects --------------------------------------------------------------------------------------------

    /// <summary>
    /// Plays one of the sounds by this name: at a place in the world (it fades with distance and comes from that
    /// side), or flat. Each name is played at most once every few hundredths of a second, so a crowd of hits at once
    /// doesn't become a roar.
    /// </summary>
    public void Play(string name, Vector3? at = null, float volumeDb = 0, float pitch = 1, float vary = 0.08f, float range = 30)
    {
        var now = Time.GetTicksMsec() / 1000.0;
        if (_lastPlayed.TryGetValue(name, out var last) && now - last < 0.04) return;
        if (Pick(name) is not { } path || Load(path) is not { } stream) return;
        _lastPlayed[name] = now;
        var p = pitch * (1 + (float)Random.Shared.NextDouble() * vary * 2 - vary);
        if (at is { } where && GetViewport()?.GetCamera3D() is not null)
        {
            var player = _placed.FirstOrDefault(x => !x.Playing);
            if (player is null)
            {
                if (_placed.Count >= 24) player = _placed[0];
                else
                {
                    player = new AudioStreamPlayer3D { Bus = Effects, UnitSize = 6, MaxDistance = range, AttenuationFilterCutoffHz = 9000 };
                    AddChild(player);
                    _placed.Add(player);
                }
            }
            player.Stream = stream;
            player.MaxDistance = range;
            player.VolumeDb = volumeDb;
            player.PitchScale = p;
            player.GlobalPosition = where;
            player.Play();
        }
        else
        {
            var player = _flat.FirstOrDefault(x => !x.Playing);
            if (player is null)
            {
                if (_flat.Count >= 12) player = _flat[0];
                else { player = new AudioStreamPlayer { Bus = Effects }; AddChild(player); _flat.Add(player); }
            }
            player.Stream = stream;
            player.VolumeDb = volumeDb;
            player.PitchScale = p;
            player.Play();
        }
    }

    /// <summary>Plays a sound if the game's sound is up (any node can call this without checking).</summary>
    public static void At(string name, Vector3 where, float volumeDb = 0, float pitch = 1, float range = 30) =>
        Current?.Play(name, where, volumeDb, pitch, range: range);

    public static void Ui(string name, float volumeDb = 0) => Current?.Play("ui/" + name, null, volumeDb, vary: 0.02f);

    /// <summary>The sound of an element: what a spell of it sounds like when cast, flies and lands.</summary>
    public static string Element(DamageKind kind) => kind switch
    {
        DamageKind.Fire => "fire",
        DamageKind.Frost => "frost",
        DamageKind.Shock => "shock",
        DamageKind.Poison => "poison",
        DamageKind.Sun => "holy",
        _ => "arcane",
    };

    // ---- music and ambience --------------------------------------------------------------------------------

    /// <summary>
    /// Changes the music to suit what's happening: explore, town, tavern, battle, boss, title or bard. The new
    /// tune fades in over the old; the same mood carries on as it is.
    /// </summary>
    public void SetMood(string mood, double fade = 2.5)
    {
        if (mood == _mood) return;
        _mood = mood;
        Crossfade(_music, ref _musicOn, Track(mood), fade, -4);
    }

    string? Track(string mood)
    {
        var tracks = Files("music", mood);
        if (tracks.Length == 0) return null;
        var choice = tracks.Where(t => t != _lastTrack).ToArray();
        return _lastTrack = (choice.Length > 0 ? choice : tracks)[Random.Shared.Next(choice.Length > 0 ? choice.Length : tracks.Length)];
    }

    void NextTrack()
    {
        if (_mood == "") return;
        var mood = _mood;
        _mood = "";
        SetMood(mood, 1);
    }

    /// <summary>The sound of where you are: the wilds by day or night, the city, the docks, a cave, wind on the heights.</summary>
    public void SetPlace(string place, double fade = 3)
    {
        if (place == _place) return;
        _place = place;
        var loops = Files("amb", place);
        Crossfade(_ambience, ref _ambienceOn, loops.Length == 0 ? null : loops[0], fade, -2);
    }

    void Crossfade(AudioStreamPlayer[] pair, ref int on, string? path, double fade, float loud)
    {
        var old = pair[on];
        on = 1 - on;
        var next = pair[on];
        if (old.Playing)
        {
            var t = old.CreateTween();
            t.TweenProperty(old, "volume_db", -60f, fade);
            t.TweenCallback(Callable.From(old.Stop));
        }
        if (path is null || Load(path) is not { } stream) { next.Stop(); return; }
        next.Stream = stream;
        next.VolumeDb = -60;
        next.Play();
        next.CreateTween().TweenProperty(next, "volume_db", loud, fade);
    }
}
