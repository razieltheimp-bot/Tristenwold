using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The two great blades. Rubilacxe stands in the stone at Tristinwold's heart, for the one who is good and has brought down
/// the fifth guardian. Arachnicalibur hangs at the Dread Pirate Soren's hip, for the wicked hand that can take it from him
/// when he comes ashore to lay siege to a town. There's one of each on a server, and when they're gone, they're gone.
/// </summary>
public partial class Main
{
    /// <summary>Where the stone stands, in the middle of the town.</summary>
    public static readonly Vector3 BladeStone = new(64, 0, -2);
    Node3D? _stoneBlade;
    Legends? _legends;

    public Legends? LegendsNode => _legends;

    void AddLegends()
    {
        AddChild(_legends = new Legends(this));
        Sign(BladeStone + Vector3.Up * 5, "The Stone");
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#6e6c66"), Roughness = 1 };
        var at = BladeStone with { Y = City.FloorAt(BladeStone) };
        var body = new StaticBody3D { Position = at };
        body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.25f, Height = 1.7f, RadialSegments = 14, Rings = 6 }, MaterialOverride = stone, Position = Vector3.Up * 0.5f, Scale = new Vector3(1.2f, 1, 1) });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.25f, Height = 1.4f }, Position = Vector3.Up * 0.6f });
        AddChild(body);
        var steel = new StandardMaterial3D { AlbedoColor = new Color("#c8d4e0"), Metallic = 0.9f, Roughness = 0.25f };
        var gold = new StandardMaterial3D { AlbedoColor = new Color("#d8b050"), Metallic = 0.85f, Roughness = 0.3f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color("#2e2820"), Roughness = 0.8f };
        _stoneBlade = new Node3D { Position = at + Vector3.Up * 1.1f, Rotation = new Vector3(0.1f, 0, -0.08f) };
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.09f, 1.5f, 0.02f) }, MaterialOverride = steel, Position = Vector3.Up * 0.65f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.06f, 0.08f) }, MaterialOverride = gold, Position = Vector3.Up * 1.42f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.028f, Height = 0.36f, RadialSegments = 10 }, MaterialOverride = dark, Position = Vector3.Up * 1.62f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.075f, Height = 0.15f, RadialSegments = 12, Rings = 8 }, MaterialOverride = gold, Position = Vector3.Up * 1.84f });
        _stoneBlade.AddChild(new OmniLight3D { Position = Vector3.Up * 1, LightColor = new Color("#d8e8ff"), LightEnergy = 1.1f, OmniRange = 7 });
        AddChild(_stoneBlade);
    }

    /// <summary>Why this hero can't draw the sword, or null if they can.</summary>
    public string? WhyNotBlade() =>
        !_forest.DragonSlayer ? "It doesn't so much as shift. Cut in the stone, worn nearly smooth: \"Who ends the fifth guardian may try me.\""
        : _standing.Outlaw || _standing.Repute < Legend.BladeRepute ? "It moves. Then it goes cold in your hand, and sets like iron in ice. Whatever is in it knows what you've done, and wants better."
        : null;

    (string Verb, Action Act)? BladeAction(Vector3 pos)
    {
        if (_city.BladeHolder is not null || pos.DistanceTo(BladeStone with { Y = pos.Y }) > 3.2f) return null;
        return ("try to draw the sword from the stone", DrawBlade);
    }

    public void DrawBlade()
    {
        if (WhyNotBlade() is { } why) { Sound.Ui("error"); _hud.Toast(why, 9); return; }
        DoCity(CityAction.Blade(Legend.Rubilacxe, _player.Company));
        Take([(Legend.Rubilacxe, 1), (Legend.RubilacxeScabbard, 1)]);
        _standing.Good(20, "drew Rubilacxe from the stone");
        Sound.Ui("quest");
        _hud.Toast("The stone lets go as if it had only been waiting to be asked. Rubilacxe comes up into the light, and every bell in Tristinwold rings at once with no hand on the rope.", 12);
        Save();
    }

    /// <summary>The stone's blade shows till it's drawn, whoever drew it.</summary>
    void TendStone()
    {
        if (_stoneBlade is not null) _stoneBlade.Visible = _city.BladeHolder is null;
    }
}

/// <summary>The Dread Pirate Soren ashore: while a town's under siege, he and his crew are at its gates for whoever comes.</summary>
public partial class Legends(Main main) : Node
{
    Enemy? _soren;
    readonly List<Enemy> _crew = [];
    double _tick;

    public Enemy? Soren => _soren;

    public override void _Ready() => Enemy.Slain += OnSlain;
    public override void _ExitTree() => Enemy.Slain -= OnSlain;

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var city = main.Economy;
        if (city.Siege is not { } siege) { Clear(); return; }
        if (_soren is not null) return;
        var (x, z) = siege.Town == ReputationBook.Capital ? (150.0, 0.0) : main.World.Find(siege.Town) is { } town ? (town.X, town.Z) : (double.NaN, 0);
        if (double.IsNaN(x)) return;
        var at = new Vector3((float)x, 0, (float)z);
        var p = main.PlayerNode.GlobalPosition;
        if (new Vector2(p.X - at.X, p.Z - at.Z).Length() > 150 || !main.TerrainNode.HasGround(at)) return;
        Muster(at + new Vector3(0, 0, 40));
    }

    void Muster(Vector3 at)
    {
        var danger = WorldGen.Danger(at.X, at.Z) * (1 + 0.08 * main.PlayerNode.Hero.Level);
        _soren = Enemy.Create(Bestiary.Get("dread_soren"), at with { Y = City.FloorAt(at) + 0.5f }, danger * 1.4);
        _soren.Foresees = main.Economy.WebHolder is null ? Legend.SorenForesees : 0;
        _soren.Roam = 16;
        main.AddEnemy(_soren);
        for (var i = 0; i < 6; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 1.05f) * 7, 0, Mathf.Sin(i * 1.05f) * 7);
            var e = Enemy.Create(Bestiary.Get(i == 0 ? "pirate_captain" : "pirate"), spot with { Y = City.FloorAt(spot) + 0.5f }, danger);
            e.Roam = 12;
            main.AddEnemy(e);
            _crew.Add(e);
        }
        main.HudNode.Toast("The Dread Pirate Soren is at the gates, and his crew with him. His blade drips with a light like dew on a web.", 8);
    }

    void Clear()
    {
        foreach (var e in _crew.Where(IsInstanceValid)) e.QueueFree();
        _crew.Clear();
        if (_soren is not null && IsInstanceValid(_soren)) _soren.QueueFree();
        _soren = null;
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim != _soren) return;
        _soren = null;
        main.DoCity(CityAction.SiegeBroken(main.PlayerNode.Company));
        main.PlayerNode.Crowns += Legend.SiegeBounty;
        var wicked = main.Standing.Repute <= Legend.WebRepute;
        if (wicked && main.Economy.WebHolder is null)
        {
            main.DoCity(CityAction.Blade(Legend.Arachnicalibur, main.PlayerNode.Company));
            main.Take([(Legend.Arachnicalibur, 1), (Legend.ArachnicaliburScabbard, 1)]);
            main.HudNode.Toast($"Soren falls, and the whole web shivers. Arachnicalibur and its scabbard are yours. It seems to have known you were coming. +{Legend.SiegeBounty} crowns.", 12);
        }
        else main.HudNode.Toast(main.Economy.WebHolder is null
            ? $"The siege is broken: Soren goes down, and his bosun cuts the black blade from his belt and is over the wall before you can follow. It won't come to clean hands. +{Legend.SiegeBounty} crowns."
            : $"The siege is broken, and Soren is carried back to his boats. +{Legend.SiegeBounty} crowns.", 11);
        main.Save();
    }

    public void MusterForTest(Vector3 at) => Muster(at);
}
