using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// Limit breaks that change the hero: the elemental druid's Elemental Form, the barbarian's Berserker Rage, and the
/// martial artist's Guardian Spirit. Each lasts as long as its rank and the hero's level make it, and hits as hard.
/// </summary>
public partial class Player
{
    /// <summary>The elements a druid can become, by name and the colour they burn.</summary>
    public static readonly IReadOnlyList<(string Id, string Name, DamageKind Kind, string Colour)> Elementals =
    [
        ("earth", "a golem of rock", DamageKind.Physical, "#8a8478"),
        ("fire", "a golem of flame", DamageKind.Fire, "#ff6a20"),
        ("storm", "a golem of lightning", DamageKind.Shock, "#9a8aff"),
        ("water", "a golem of water", DamageKind.Physical, "#3a8ad0"),
        ("frost", "a golem of ice", DamageKind.Frost, "#a8d8ff"),
    ];

    /// <summary>The form the druid has set (the game fills this in), or null to go by the element they're attuned to.</summary>
    public Func<string?>? ElementalPick { get; set; }

    /// <summary>What an Elemental Form would make them right now: the form they set, or else their attuned element's.</summary>
    public string ElementalKind => ElementalPick?.Invoke() ?? Element switch { DamageKind.Frost => "frost", DamageKind.Shock => "storm", _ => "fire" };

    string? _elemental;
    double _elementalMight = 1;
    DamageKind _elementWas;

    /// <summary>The elemental druid's limit break: a giant of their chosen element, as strong and as long-lasting as their rank and level make it.</summary>
    bool ElementalForm(double seconds, double might)
    {
        var kind = ElementalKind;
        var (_, name, element, colour) = Elementals.First(e => e.Id == kind);
        _elemental = kind;
        _elementalMight = 1 + might;
        _elementWas = Element;
        if (element != DamageKind.Physical) Element = element;
        Fit();
        Effects.Limit(GetParent(), GlobalPosition, new Color(colour));
        Possess(() => Adorned(kind, Body.For($"elemental_{kind}")), 2.1f, seconds, () =>
        {
            if (kind == "water") Stats.Statuses.Remove(AutoStatus.Regen);
            _elemental = null;
            Element = _elementWas;
            Fit();
            Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, element == DamageKind.Physical ? DamageKind.Poison : element, 1.6f);
            Said?.Invoke("The element lets you go, and you're yourself again.");
        });
        if (kind == "water") Stats.Statuses.Add(AutoStatus.Regen); // the water closes over its wounds
        Said?.Invoke($"LIMIT BREAK: Elemental Form! You are {name}, for {seconds:0} seconds.");
        return true;
    }

    /// <summary>What a golem of each element is made of, over the big bare body: slabs of rock, a mantle of fire, crackling light, running water, spikes of ice.</summary>
    static Body Adorned(string kind, Body body)
    {
        const float h = 4.4f; // how tall the golem stands
        StandardMaterial3D Glow(string hex, float alpha = 0.9f) => new()
        {
            AlbedoColor = new Color(hex) with { A = alpha }, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, VertexColorUseAsAlbedo = false,
        };
        CpuParticles3D Motes(int amount, string hex, float size, Vector3 gravity, float speed, double life) => new()
        {
            Amount = amount, Lifetime = life, Position = new Vector3(0, h * 0.55f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(h * 0.2f, h * 0.38f, h * 0.12f),
            Gravity = gravity, InitialVelocityMin = speed * 0.5f, InitialVelocityMax = speed, Direction = Vector3.Up, Spread = 60, LocalCoords = false,
            Mesh = new SphereMesh { Radius = size, Height = size * 2, Material = Glow(hex) },
        };
        switch (kind)
        {
            case "earth":
                var stone = BuildingMaterials.For(Surface.Stone);
                foreach (var (at, size) in new[] { (new Vector3(-0.75f, 0.8f, 0), 0.7f), (new Vector3(0.75f, 0.8f, 0), 0.7f), (new Vector3(0, 0.62f, 0.32f), 0.9f), (new Vector3(0, 0.66f, -0.3f), 1f), (new Vector3(-0.85f, 0.45f, 0.1f), 0.5f), (new Vector3(0.85f, 0.45f, 0.1f), 0.5f), (new Vector3(0, 0.4f, 0.25f), 0.6f) })
                    body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One * size }, MaterialOverride = stone, Position = new Vector3(at.X, at.Y * h, at.Z), Rotation = new Vector3(at.X * 0.6f, at.Y * 2, at.Z) });
                break;
            case "fire":
                body.AddChild(Motes(90, "#ff7a1a", 0.14f, new Vector3(0, 3, 0), 1.6f, 0.9));
                body.AddChild(new OmniLight3D { Position = new Vector3(0, h * 0.6f, 0), LightColor = new Color("#ff8a3a"), LightEnergy = 2.5f, OmniRange = 9 });
                break;
            case "storm":
                body.AddChild(Motes(70, "#c0c8ff", 0.06f, Vector3.Zero, 4, 0.25));
                body.AddChild(new OmniLight3D { Position = new Vector3(0, h * 0.6f, 0), LightColor = new Color("#a0a8ff"), LightEnergy = 2, OmniRange = 9 });
                break;
            case "water":
                body.AddChild(Motes(80, "#6ab8f0", 0.08f, new Vector3(0, -6, 0), 0.4f, 0.8));
                body.AddChild(new OmniLight3D { Position = new Vector3(0, h * 0.6f, 0), LightColor = new Color("#6ab0e0"), LightEnergy = 1, OmniRange = 7 });
                break;
            default:
                var ice = new StandardMaterial3D { AlbedoColor = new Color("#d8f4ff") with { A = 0.8f }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.2f };
                foreach (var (x, y, z, lean) in new[] { (-0.7f, 0.82f, -0.1f, -0.5f), (0.7f, 0.82f, -0.1f, 0.5f), (0f, 0.75f, -0.35f, 0f), (-0.3f, 0.68f, -0.38f, -0.3f), (0.3f, 0.68f, -0.38f, 0.3f) })
                    body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 0.22f, Height = 1.3f }, MaterialOverride = ice, Position = new Vector3(x, y * h, z), Rotation = new Vector3(-0.4f, 0, lean) });
                body.AddChild(Motes(30, "#e8f8ff", 0.05f, new Vector3(0, -1, 0), 0.3f, 1.5));
                break;
        }
        return body;
    }

    double _berserk, _berserkMight = 1;
    Vector3 _berserkScale = Vector3.One;

    /// <summary>Whether no blade, arrow or fist can hurt them just now: a barbarian in a berserker rage. Magic still can.</summary>
    public bool Impervious => _berserk > 0;

    /// <summary>The barbarian's limit break: they swell to half again their size and far stronger, and nothing physical hurts them, for a while.</summary>
    void Berserk(double seconds, double might)
    {
        if (_berserk <= 0) _berserkScale = Figure.Scale;
        _berserk = seconds;
        _berserkMight = 1 + might;
        Fit();
        Figure.Scale = _berserkScale * 1.5f;
        var own = Appearance?.Size ?? 1;
        Resize(1.8f * own * 1.5f, 0.55f);
        _pivot.Position = new Vector3(0, 1.6f * own * 1.5f, 0);
        Figure.Perform("Cheer", 0.9);
        Effects.Limit(GetParent(), GlobalPosition, new Color("#e03a1a"));
        Said?.Invoke($"LIMIT BREAK: Berserker Rage! Nothing that cuts or strikes can hurt you for {seconds:0} seconds.");
    }

    void TickBerserk(double delta)
    {
        if (_berserk <= 0 || (_berserk -= delta) > 0) return;
        _berserk = 0;
        Figure.Scale = _berserkScale;
        var own = Appearance?.Size ?? 1;
        Resize(1.8f * own, 0.4f);
        _pivot.Position = new Vector3(0, 1.6f * own, 0);
        Fit();
        Said?.Invoke("The rage burns out of you.");
    }

    /// <summary>The martial artist's limit break: a great golden spirit rises behind them, and its arms strike ten times, back to back.</summary>
    void GuardianSpirit(Enemy near, double dmg, DamageKind kind)
    {
        var spirit = new Guardian(near);
        GetParent().AddChild(spirit);
        spirit.GlobalPosition = GlobalPosition - GlobalTransform.Basis.Z * -2.4f;
        spirit.Rotation = Rotation;
        Figure.Perform("Cheer", 0.6);
        for (var i = 0; i < 10; i++)
        {
            var blow = i;
            GetTree().CreateTimer(0.5 + blow * 0.12).Timeout += () =>
            {
                if (!IsInstanceValid(near) || near.State == EnemyState.Dead) return;
                if (IsInstanceValid(spirit)) spirit.Strike(blow);
                Strike(near, dmg, kind, AttackKind.Weapon);
            };
        }
        Said?.Invoke("LIMIT BREAK: The Guardian Spirit!");
    }
}

/// <summary>
/// The martial artist's guardian: a giant of golden light that rises behind them, many-armed, a skull's calm face under a
/// crest of flame, a string of great beads about its neck and motes of gold drifting off it. Its fists go out one after
/// another at the foe, and then it fades.
/// </summary>
public partial class Guardian(Node3D foe) : Node3D
{
    readonly List<Node3D> _arms = [];
    double _life;
    StandardMaterial3D _gold = null!;
    const double Lasts = 2.4;

    static StandardMaterial3D Glow(string hex, float alpha, float energy) => new()
    {
        AlbedoColor = new Color(hex) with { A = alpha }, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add, EmissionEnabled = true, Emission = new Color(hex), EmissionEnergyMultiplier = energy, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    void Part(Mesh mesh, Vector3 at, Vector3? scale = null, Material? m = null, Node3D? on = null) =>
        (on ?? this).AddChild(new MeshInstance3D { Mesh = mesh, Position = at, Scale = scale ?? Vector3.One, MaterialOverride = m ?? _gold, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

    public override void _Ready()
    {
        _gold = Glow("#ffc04a", 0.55f, 1.6f);
        var bone = Glow("#ffe8b0", 0.75f, 2);
        // The body, rising out of the ground behind the hero, and the head over all.
        Part(new CapsuleMesh { Radius = 1.6f, Height = 6 }, new Vector3(0, 4, 0), new Vector3(1.3f, 1, 0.8f));
        Part(new SphereMesh { Radius = 1.1f, Height = 2.4f }, new Vector3(0, 8.2f, 0), m: bone);
        foreach (var x in new[] { -0.4f, 0.4f })
            Part(new SphereMesh { Radius = 0.22f, Height = 0.44f }, new Vector3(x, 8.35f, -0.95f), m: Glow("#3a2400", 0.9f, 0)); // the skull's eyes
        Part(new TorusMesh { InnerRadius = 1.7f, OuterRadius = 1.85f }, new Vector3(0, 8.4f, 0.6f), new Vector3(1, 1, 1), Glow("#fff0c0", 0.7f, 3)); // the halo
        Part(new PrismMesh { Size = new Vector3(0.8f, 1, 0.4f) }, new Vector3(0, 9.6f, 0), m: Glow("#ffffff", 0.9f, 4)); // the crest of flame
        for (var i = 0; i < 14; i++) // the beads
        {
            var a = i * Mathf.Tau / 14;
            Part(new SphereMesh { Radius = 0.28f, Height = 0.56f }, new Vector3(Mathf.Cos(a) * 1.7f, 6.6f + Mathf.Sin(a) * 0.25f, Mathf.Sin(a) * 1.1f), m: bone);
        }
        // Six arms, three to a side, each ending in a fist that will go out.
        for (var i = 0; i < 6; i++)
        {
            var side = i % 2 == 0 ? -1f : 1f;
            var tier = i / 2;
            var arm = new Node3D { Position = new Vector3(side * 1.9f, 6.2f - tier * 1.4f, 0), Rotation = new Vector3(0, 0, side * (0.9f + tier * 0.35f)) };
            Part(new CapsuleMesh { Radius = 0.42f, Height = 4.2f }, new Vector3(0, -2, 0), on: arm);
            Part(new SphereMesh { Radius = 0.6f, Height = 1.2f }, new Vector3(0, -4.2f, 0), m: bone, on: arm);
            AddChild(arm);
            _arms.Add(arm);
        }
        AddChild(new CpuParticles3D
        {
            Amount = 60, Lifetime = 1.8, Position = new Vector3(0, 5, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(3, 4, 1.5f),
            Gravity = new Vector3(0, 0.6f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.8f, Direction = Vector3.Up, Spread = 180,
            Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, Material = Glow("#ffe080", 0.9f, 3) },
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 5, -1), LightColor = new Color("#ffc860"), LightEnergy = 3, OmniRange = 18 });
        Scale = Vector3.One * 0.05f;
        CreateTween().TweenProperty(this, "scale", Vector3.One, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>One of its fists goes out at the foe and comes back.</summary>
    public void Strike(int blow)
    {
        var arm = _arms[blow % _arms.Count];
        var reach = IsInstanceValid(foe) ? foe.GlobalPosition + Vector3.Up : GlobalPosition - GlobalTransform.Basis.Z * 6;
        var was = arm.GlobalTransform;
        var tw = CreateTween();
        tw.TweenProperty(arm, "global_position", was.Origin.Lerp(reach, 0.55f), 0.06);
        tw.TweenProperty(arm, "global_position", was.Origin, 0.08);
        Effects.Burst(GetParent(), reach, DamageKind.Sun, 0.8f);
    }

    public override void _Process(double delta)
    {
        if ((_life += delta) < Lasts) return;
        var fade = (float)Math.Clamp(1 - (_life - Lasts) / 0.6, 0, 1);
        Scale = Vector3.One * Mathf.Max(0.05f, fade);
        if (fade <= 0) QueueFree();
    }
}
