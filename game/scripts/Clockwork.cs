using Godot;

namespace Tristinwold.Game;

/// <summary>
/// An artificer's golem, built as the machine it is rather than a knight in brass: a clockwork automaton of
/// primitives in the armoury's metals, moved by its own simple mechanics (a stamping walk, a swing of the working
/// arm, a wind-down when it falls). Each artificer's design is its own, by what they are:
/// <list type="bullet">
/// <item><b>Alembic</b> (the Alchemist's): a retort of brass and glass, a green reagent glowing in the chest vessel,
/// spindly copper limbs, pipes and a stoppered head.</item>
/// <item><b>Ballast</b> (the Ether-Aeronaut's): a riveted iron hull with room inside for its maker, a furnace core
/// glowing through the grate, chimney stacks on the shoulders, heavy clawed arms and a hatch on the chest.</item>
/// <item><b>Scrap</b> (the Saboteur's): a thing of mismatched plates and exposed gears, one great hammer arm, one
/// lantern eye, nothing matching and everything working.</item>
/// </list>
/// Other artificers' machines (the Foundry's, a rival's) take a design by name too (<see cref="For"/>).
/// </summary>
public partial class Clockwork : Body
{
    Node3D _torso = new(), _head = new(), _armL = new(), _armR = new(), _legL = new(), _legR = new();
    readonly List<Node3D> _gears = [];
    readonly List<StandardMaterial3D> _materials = [];
    readonly List<(MeshInstance3D Mesh, StandardMaterial3D Glow, float Energy)> _lamps = [];
    float _phase, _speed, _swing, _stride = 0.55f, _hip = 0.9f, _bob = 0.04f;
    bool _dead;
    double _flash, _busy;

    /// <summary>The design's name (for the self-test).</summary>
    public string Design { get; private set; } = "";

    static StandardMaterial3D Alloy(string hex, float rough = 0.42f, float metal = 0.9f) => new() { AlbedoColor = new Color(hex), Metallic = metal, Roughness = rough, MetallicSpecular = 0.7f };

    MeshInstance3D Part(Node3D parent, Mesh mesh, StandardMaterial3D mat, Vector3 at, Vector3? turn = null, Vector3? scale = null)
    {
        _materials.Add(mat);
        var m = new MeshInstance3D { Mesh = mesh, Position = at, RotationDegrees = turn ?? Vector3.Zero, Scale = scale ?? Vector3.One, MaterialOverride = mat };
        parent.AddChild(m);
        return m;
    }

    /// <summary>Something that glows: a furnace grate, a reagent, a lantern eye.</summary>
    MeshInstance3D Lamp(Node3D parent, Mesh mesh, string hex, float energy, Vector3 at, Vector3? scale = null)
    {
        var mat = new StandardMaterial3D { AlbedoColor = new Color(hex), EmissionEnabled = true, Emission = new Color(hex), EmissionEnergyMultiplier = energy, Roughness = 0.3f };
        var m = new MeshInstance3D { Mesh = mesh, Position = at, Scale = scale ?? Vector3.One, MaterialOverride = mat };
        parent.AddChild(m);
        _lamps.Add((m, mat, energy));
        parent.AddChild(new OmniLight3D { Position = at, LightColor = new Color(hex), OmniRange = 2.2f, LightEnergy = 0.45f, ShadowEnabled = false });
        return m;
    }

    /// <summary>A gear that turns: a short cylinder with teeth, flat to the given axis.</summary>
    void Gear(Node3D parent, StandardMaterial3D mat, Vector3 at, float radius, Vector3 turn)
    {
        var g = new Node3D { Position = at, RotationDegrees = turn };
        parent.AddChild(g);
        Part(g, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = radius * 0.3f, RadialSegments = 12 }, mat, Vector3.Zero);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8;
            Part(g, new BoxMesh { Size = new Vector3(radius * 0.35f, radius * 0.3f, radius * 0.3f) }, mat, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), new Vector3(0, -Mathf.RadToDeg(a), 0));
        }
        _gears.Add(g);
    }

    /// <summary>The frame every design shares: a torso on two legs, a head, two arms, each hung where the design wants them.</summary>
    static Clockwork Frame(float hip, float torsoH, float torsoW, float stride)
    {
        var c = new Clockwork { _hip = hip, _stride = stride };
        c._torso = new Node3D { Position = new Vector3(0, hip, 0) };
        c.AddChild(c._torso);
        c._head = new Node3D { Position = new Vector3(0, torsoH + 0.08f, 0) };
        c._torso.AddChild(c._head);
        c._armL = new Node3D { Position = new Vector3(torsoW / 2 + 0.06f, torsoH * 0.82f, 0) };
        c._armR = new Node3D { Position = new Vector3(-torsoW / 2 - 0.06f, torsoH * 0.82f, 0) };
        c._torso.AddChild(c._armL);
        c._torso.AddChild(c._armR);
        c._legL = new Node3D { Position = new Vector3(torsoW * 0.26f, hip, 0) };
        c._legR = new Node3D { Position = new Vector3(-torsoW * 0.26f, hip, 0) };
        c.AddChild(c._legL);
        c.AddChild(c._legR);
        return c;
    }

    /// <summary>The Alchemist's Alembic: brass and glass, a reagent glowing in the chest, copper limbs.</summary>
    public static Clockwork Alembic()
    {
        var c = Frame(0.95f, 0.9f, 0.5f, 0.5f);
        c.Design = "Alembic";
        var brass = Alloy("#c8a048", 0.36f);
        var copper = Alloy("#b87448", 0.45f);
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.95f, 0.9f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, Metallic = 0.1f, MetallicSpecular = 0.9f };
        var t = c._torso;
        c.Part(t, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.26f, Height = 0.5f, RadialSegments = 18 }, brass, new Vector3(0, 0.25f, 0)); // the boiler
        c.Part(t, new SphereMesh { Radius = 0.23f, Height = 0.46f, RadialSegments = 20, Rings = 10 }, glass, new Vector3(0, 0.66f, 0)); // the vessel
        c.Lamp(t, new SphereMesh { Radius = 0.14f, Height = 0.28f }, "#5fe08a", 1.6f, new Vector3(0, 0.6f, 0)); // the reagent, aglow
        c.Part(t, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.25f, Rings = 24, RingSegments = 8 }, brass, new Vector3(0, 0.5f, 0)); // the collar between
        c.Part(t, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.25f, Rings = 24, RingSegments = 8 }, brass, new Vector3(0, 0.84f, 0));
        foreach (var side in new[] { -1f, 1f }) // pipes down the sides
            c.Part(t, new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.6f, RadialSegments = 8 }, copper, new Vector3(side * 0.24f, 0.45f, 0.12f), new Vector3(0, 0, side * 6));
        c.Gear(t, brass, new Vector3(0, 0.3f, -0.27f), 0.09f, new Vector3(90, 0, 0));
        // The head: a stoppered retort with a glass eye.
        c.Part(c._head, new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.13f, Height = 0.22f, RadialSegments = 14 }, brass, new Vector3(0, 0.1f, 0));
        c.Part(c._head, new SphereMesh { Radius = 0.06f, Height = 0.12f }, copper, new Vector3(0, 0.25f, 0)); // the stopper
        c.Lamp(c._head, new SphereMesh { Radius = 0.035f, Height = 0.07f }, "#5fe08a", 1.2f, new Vector3(0, 0.1f, 0.12f));
        // Spindly copper arms ending in three-fingered graspers; thin legs on round feet.
        foreach (var (arm, side) in new[] { (c._armL, 1f), (c._armR, -1f) })
        {
            c.Part(arm, new SphereMesh { Radius = 0.07f, Height = 0.14f }, brass, Vector3.Zero);
            c.Part(arm, new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.03f, Height = 0.62f, RadialSegments = 10 }, copper, new Vector3(side * 0.04f, -0.32f, 0));
            for (var i = 0; i < 3; i++) c.Part(arm, new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.14f, RadialSegments = 6 }, brass, new Vector3(side * 0.04f + Mathf.Cos(i * 2.1f) * 0.03f, -0.68f, Mathf.Sin(i * 2.1f) * 0.03f), new Vector3(Mathf.Sin(i * 2.1f) * 20, 0, Mathf.Cos(i * 2.1f) * -20));
        }
        foreach (var leg in new[] { c._legL, c._legR })
        {
            c.Part(leg, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.04f, Height = 0.78f, RadialSegments = 10 }, copper, new Vector3(0, -0.42f, 0));
            c.Part(leg, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.11f, Height = 0.08f, RadialSegments = 12 }, brass, new Vector3(0, -0.88f, 0.02f));
        }
        return c;
    }

    /// <summary>The Ether-Aeronaut's Ballast: a riveted iron hull with a furnace core, chimneys, heavy claws and a hatch.</summary>
    public static Clockwork Ballast()
    {
        var c = Frame(1.0f, 1.1f, 0.8f, 0.45f);
        c.Design = "Ballast";
        c._bob = 0.06f;
        var iron = Alloy("#5a5e64", 0.5f);
        var dark = Alloy("#3a3c40", 0.6f);
        var brass = Alloy("#b08a3a", 0.4f);
        var t = c._torso;
        c.Part(t, new CapsuleMesh { Radius = 0.5f, Height = 1.6f, RadialSegments = 24, Rings = 8 }, iron, new Vector3(0, 0.55f, 0), scale: new Vector3(0.82f, 0.7f, 0.62f)); // the hull
        c.Part(t, new BoxMesh { Size = new Vector3(0.34f, 0.4f, 0.04f) }, dark, new Vector3(0, 0.62f, 0.3f)); // the hatch
        c.Part(t, new BoxMesh { Size = new Vector3(0.05f, 0.14f, 0.03f) }, brass, new Vector3(0.14f, 0.62f, 0.33f)); // its handle
        for (var i = 0; i < 6; i++) c.Part(t, new BoxMesh { Size = new Vector3(0.3f, 0.02f, 0.02f) }, dark, new Vector3(0, 0.22f + i * 0.05f, 0.31f)); // the grate
        c.Lamp(t, new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.06f) }, "#ff7a20", 1.8f, new Vector3(0, 0.34f, 0.28f)); // the furnace behind it
        foreach (var side in new[] { -1f, 1f })
        {
            c.Part(t, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 0.4f, RadialSegments = 12 }, dark, new Vector3(side * 0.26f, 1.2f, -0.08f)); // chimney stacks
            c.Part(t, new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.09f, Rings = 16, RingSegments = 6 }, brass, new Vector3(side * 0.26f, 1.38f, -0.08f));
            for (var r = 0; r < 5; r++) c.Part(t, new SphereMesh { Radius = 0.018f, Height = 0.036f }, brass, new Vector3(side * 0.3f, 0.2f + r * 0.18f, 0.2f)); // rivets
        }
        c.Gear(t, brass, new Vector3(0.3f, 0.45f, -0.25f), 0.1f, new Vector3(90, 0, 0));
        c.Gear(t, brass, new Vector3(-0.22f, 0.7f, -0.28f), 0.07f, new Vector3(90, 0, 0));
        // A squat head, a visor slit lit by the furnace.
        c.Part(c._head, new BoxMesh { Size = new Vector3(0.3f, 0.22f, 0.3f) }, iron, new Vector3(0, 0.08f, 0));
        c.Lamp(c._head, new BoxMesh { Size = new Vector3(0.2f, 0.03f, 0.02f) }, "#ff9a30", 1.2f, new Vector3(0, 0.1f, 0.15f));
        // Heavy arms, pistons along them, three-clawed hands.
        foreach (var (arm, side) in new[] { (c._armL, 1f), (c._armR, -1f) })
        {
            c.Part(arm, new SphereMesh { Radius = 0.15f, Height = 0.3f }, iron, Vector3.Zero);
            c.Part(arm, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.09f, Height = 0.5f, RadialSegments = 12 }, iron, new Vector3(side * 0.06f, -0.3f, 0));
            c.Part(arm, new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.4f, RadialSegments = 8 }, brass, new Vector3(side * 0.06f, -0.3f, 0.1f)); // a piston
            c.Part(arm, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.1f, Height = 0.3f, RadialSegments = 12 }, dark, new Vector3(side * 0.06f, -0.68f, 0));
            for (var i = 0; i < 3; i++) c.Part(arm, new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.03f, Height = 0.18f, RadialSegments = 6 }, iron, new Vector3(side * 0.06f + Mathf.Cos(i * 2.1f) * 0.07f, -0.9f, Mathf.Sin(i * 2.1f) * 0.07f), new Vector3(Mathf.Sin(i * 2.1f) * 25, 0, Mathf.Cos(i * 2.1f) * -25));
        }
        foreach (var leg in new[] { c._legL, c._legR })
        {
            c.Part(leg, new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.13f, Height = 0.8f, RadialSegments = 12 }, iron, new Vector3(0, -0.45f, 0));
            c.Part(leg, new BoxMesh { Size = new Vector3(0.26f, 0.1f, 0.34f) }, dark, new Vector3(0, -0.92f, 0.04f));
        }
        return c;
    }

    /// <summary>The Saboteur's Scrap: mismatched plates, exposed gears, one great hammer arm and a lantern eye.</summary>
    public static Clockwork Scrap()
    {
        var c = Frame(0.85f, 0.95f, 0.6f, 0.6f);
        c.Design = "Scrap";
        var rust = Alloy("#7a4a30", 0.75f, 0.6f);
        var tin = Alloy("#8a8e8a", 0.55f);
        var brass = Alloy("#a8883a", 0.45f);
        var t = c._torso;
        c.Part(t, new BoxMesh { Size = new Vector3(0.5f, 0.6f, 0.36f) }, rust, new Vector3(0.02f, 0.4f, 0), new Vector3(0, 0, 4)); // a crooked barrel of a chest
        c.Part(t, new BoxMesh { Size = new Vector3(0.3f, 0.26f, 0.1f) }, tin, new Vector3(-0.14f, 0.62f, 0.2f), new Vector3(0, 0, -12)); // a plate bolted on askew
        c.Part(t, new BoxMesh { Size = new Vector3(0.22f, 0.34f, 0.08f) }, brass, new Vector3(0.18f, 0.3f, 0.19f), new Vector3(0, 0, 9));
        c.Gear(t, brass, new Vector3(0.1f, 0.52f, -0.22f), 0.12f, new Vector3(90, 0, 0)); // the works, open at the back
        c.Gear(t, tin, new Vector3(-0.12f, 0.3f, -0.22f), 0.08f, new Vector3(90, 0, 0));
        c.Gear(t, brass, new Vector3(0.26f, 0.72f, 0.1f), 0.06f, new Vector3(0, 0, 90)); // and one sticking out of the shoulder
        // A lantern for a head, one eye lit.
        c.Part(c._head, new BoxMesh { Size = new Vector3(0.2f, 0.26f, 0.2f) }, tin, new Vector3(0.03f, 0.1f, 0), new Vector3(0, 15, 0));
        c.Lamp(c._head, new SphereMesh { Radius = 0.05f, Height = 0.1f }, "#ffd060", 1.6f, new Vector3(0.04f, 0.12f, 0.11f));
        c.Part(c._head, new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.03f, Height = 0.12f, RadialSegments = 6 }, rust, new Vector3(-0.06f, 0.28f, 0)); // a bent flue
        // The hammer arm (the right) and a spindly grabber (the left).
        c.Part(c._armR, new SphereMesh { Radius = 0.11f, Height = 0.22f }, rust, Vector3.Zero);
        c.Part(c._armR, new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.55f, RadialSegments = 10 }, tin, new Vector3(-0.05f, -0.32f, 0));
        c.Part(c._armR, new BoxMesh { Size = new Vector3(0.22f, 0.22f, 0.34f) }, rust, new Vector3(-0.05f, -0.7f, 0)); // the hammer head
        c.Part(c._armL, new SphereMesh { Radius = 0.07f, Height = 0.14f }, brass, Vector3.Zero);
        c.Part(c._armL, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.025f, Height = 0.6f, RadialSegments = 8 }, tin, new Vector3(0.04f, -0.32f, 0));
        foreach (var k in new[] { -1f, 1f }) c.Part(c._armL, new BoxMesh { Size = new Vector3(0.03f, 0.16f, 0.03f) }, brass, new Vector3(0.04f + k * 0.04f, -0.68f, 0), new Vector3(0, 0, k * -15));
        // Legs of two different makes.
        c.Part(c._legL, new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.06f, Height = 0.7f, RadialSegments = 10 }, tin, new Vector3(0, -0.4f, 0));
        c.Part(c._legL, new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.26f) }, rust, new Vector3(0, -0.8f, 0.03f));
        c.Part(c._legR, new BoxMesh { Size = new Vector3(0.14f, 0.72f, 0.14f) }, rust, new Vector3(0, -0.4f, 0));
        c.Part(c._legR, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.11f, Height = 0.08f, RadialSegments = 10 }, tin, new Vector3(0, -0.8f, 0.02f));
        return c;
    }

    /// <summary>A golem by its design's name (the names the Foundry gives: Alembic, Ballast, Scrap), or the discipline that owns it.</summary>
    public static Clockwork For(string design) => design switch
    {
        "Alembic" or "Alchemist" => Alembic(),
        "Ballast" or "EtherAeronaut" => Ballast(),
        _ => Scrap(),
    };

    public override void _Process(double delta)
    {
        var d = (float)delta;
        if (_flash > 0 && (_flash -= delta) <= 0) foreach (var m in _materials) m.EmissionEnabled = false;
        foreach (var (mesh, glow, energy) in _lamps) glow.EmissionEnergyMultiplier = energy * (0.85f + 0.15f * Mathf.Sin(_phase * 2.3f)); // the core breathes
        if (_dead) return;
        var pace = Mathf.Clamp(_speed / 5f, 0, 1);
        _phase += d * (1.5f + _speed * 2.4f);
        foreach (var g in _gears) g.RotateY(d * (0.8f + pace * 4));
        var step = Mathf.Sin(_phase) * _stride * pace;
        _legL.Rotation = new Vector3(step, 0, 0);
        _legR.Rotation = new Vector3(-step, 0, 0);
        _torso.Position = _torso.Position with { Y = _hip + _bob * Mathf.Abs(Mathf.Sin(_phase)) * pace };
        _torso.Rotation = new Vector3(0, 0, Mathf.Sin(_phase) * 0.04f * pace); // a stamping sway
        _swing = Mathf.MoveToward(_swing, 0, d * 2.5f);
        _armR.Rotation = new Vector3(-step * 0.6f - _swing * 2.2f, 0, -0.1f);
        _armL.Rotation = new Vector3(step * 0.6f, 0, 0.1f);
        if (_busy > 0) _busy -= delta;
    }

    public override void Move(float speed, bool grounded, bool swimming) => _speed = speed;
    public override void Attack(bool heavy = false) { _swing = heavy ? 1.3f : 1; _busy = 0.5; }
    public override void Cast() { _swing = 0.8f; _busy = 0.5; }
    public override void Perform(string clip, double seconds = 0.7, float speed = 1.3f) => Attack(clip.Contains("2H") || clip.Contains("Spin"));

    public override void Hurt()
    {
        foreach (var m in _materials) { m.EmissionEnabled = true; m.Emission = new Color("#ff4020"); m.EmissionEnergyMultiplier = 0.5f; }
        _flash = 0.15;
    }

    public override void Die()
    {
        _dead = true;
        var t = CreateTween();
        t.TweenProperty(this, "rotation:z", Mathf.Pi / 2 * 0.9f, 0.6).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        foreach (var (_, glow, _) in _lamps) glow.EmissionEnergyMultiplier = 0.1f; // the core dims
    }

    public override void Rise()
    {
        _dead = false;
        Rotation = Vector3.Zero;
    }

    public override void Mark(Color colour)
    {
        foreach (var m in _materials) m.AlbedoColor = m.AlbedoColor.Lerp(colour, 0.35f);
    }
}
