using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The hero in miniature beside the pack, turning slowly in what they have on. Point at a piece of gear (or, on a
/// phone, tap it once) and they try it on, with a line saying what it would change; point away and they're back in
/// what they wear. Made fresh from the hero's own look each time the pack opens.
/// </summary>
public partial class GearPreview : VBoxContainer
{
    public const float Wide = 170, High = 220;
    readonly SubViewportContainer _box = new() { Stretch = true, CustomMinimumSize = new Vector2(Wide, High) };
    readonly SubViewport _view = new() { OwnWorld3D = true, Size = new Vector2I((int)Wide, (int)High), RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
    readonly Node3D _turntable = new();
    readonly Label _line = new() { AutowrapMode = TextServer.AutowrapMode.Word, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(Wide, 0) };
    Func<Body>? _make;
    Pack? _worn;
    Node3D? _wearing, _trying;
    Item? _pending;
    double _settle;

    /// <summary>The item being tried on (or about to be), if any.</summary>
    public string? TryingOn { get; private set; }

    /// <summary>What the line under the hero says.</summary>
    public string Line => _line.Text;

    /// <summary>Whether a figure is standing on the plinth trying something on (for the self-test).</summary>
    public bool ShowingTrial => _trying is not null && _trying.Visible && (_wearing is null || !_wearing.Visible);

    /// <summary>How it stands, for a self-test's report.</summary>
    public string State => $"pending {_pending?.Id ?? "-"}, settle {_settle:0.00}, trying {(_trying is null ? "none" : _trying.Visible ? "shown" : "hidden")}, " +
        $"wearing {(_wearing is null ? "none" : _wearing.Visible ? "shown" : "hidden")}, visible {Visible}, processing {IsProcessing()}";

    public override void _Ready()
    {
        AddChild(_box);
        _box.AddChild(_view);
        AvatarPreview.Stage(_view, _turntable);
        _line.LabelSettings = new LabelSettings { FontSize = 12, FontColor = new Color("#e6dcc4") };
        AddChild(_line);
    }

    /// <summary>Shows the hero (made by <paramref name="make"/>) in what the pack has on.</summary>
    public void Show(Func<Body> make, Pack pack)
    {
        _make = make;
        _worn = Copy(pack);
        _pending = null;
        TryingOn = null;
        Drop(ref _trying);
        Drop(ref _wearing);
        _wearing = Dressed(_worn);
        _line.Text = Summary(_worn);
        Visible = true;
    }

    /// <summary>Tries a piece of gear on: shown once the pointer has stayed on it a moment, so sweeping down the list doesn't dress and undress the hero for each.</summary>
    public void TryOn(Item item)
    {
        if (_worn is null || TryingOn == item.Id) return;
        TryingOn = item.Id;
        _pending = item;
        _settle = 0.12;
        _line.Text = $"{item.Name}: {Change(_worn, Wearing(_worn, item))}";
    }

    /// <summary>Back to what the hero has on.</summary>
    public void TakeOff()
    {
        if (TryingOn is null) return;
        TryingOn = null;
        _pending = null;
        Drop(ref _trying);
        if (_wearing is not null) _wearing.Visible = true;
        if (_worn is not null) _line.Text = Summary(_worn);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _turntable.RotateY((float)delta * 0.7f);
        if (_pending is null || (_settle -= delta) > 0 || _worn is null) return;
        var item = _pending;
        _pending = null;
        Drop(ref _trying);
        _trying = Dressed(Wearing(_worn, item));
        if (_wearing is not null) _wearing.Visible = false;
    }

    /// <summary>Puts it on at once, without the pause (for the self-test).</summary>
    public void SettleForTest() => _settle = 0;

    Node3D? Dressed(Pack pack)
    {
        if (_make is null) return null;
        var body = _make();
        _turntable.AddChild(body); // on the stage first: a body finds its own bones as it arrives
        if (body is Figure f) Wardrobe.Wear(f, pack);
        return body;
    }

    static void Drop(ref Node3D? figure)
    {
        if (figure is not null && IsInstanceValid(figure)) figure.QueueFree();
        figure = null;
    }

    static Pack Copy(Pack pack) => new() { Items = new(pack.Items), Worn = new(pack.Worn), JobXp = new(pack.JobXp) };

    /// <summary>The pack as it would be with this put on (and whatever it pushes off, like a shield for a greatsword).</summary>
    static Pack Wearing(Pack worn, Item item)
    {
        var trial = Copy(worn);
        trial.Add(item.Id);
        trial.Equip(item.Id);
        return trial;
    }

    static string Summary(Pack pack) =>
        pack.Worn.Count == 0 ? "Wearing nothing extra." : $"Wearing: +{pack.Armor:0} armour, +{pack.Damage:P0} damage.";

    /// <summary>What putting it on would change, in the pack's own terms: "+3 armour, −2% damage".</summary>
    public static string Change(Pack was, Pack now)
    {
        var parts = new List<string>();
        void Diff(double a, double b, string name, Func<double, string> show)
        {
            var d = b - a;
            if (Math.Abs(d) >= 0.0005) parts.Add($"{(d > 0 ? "+" : "−")}{show(Math.Abs(d))} {name}");
        }
        Diff(was.Armor, now.Armor, "armour", d => d.ToString("0"));
        Diff(was.Damage, now.Damage, "damage", d => d.ToString("P0"));
        Diff(was.Speed, now.Speed, "speed", d => d.ToString("0.0%"));
        Diff(was.Crit, now.Crit, "critical", d => $"{d:0.#}%");
        Diff(was.Health, now.Health, "health", d => d.ToString("P1"));
        return parts.Count == 0 ? "no change to armour or damage" : string.Join(", ", parts);
    }
}
