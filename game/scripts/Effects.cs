using Godot;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>
/// What magic and breath look like: glowing bolts that fly to their mark, bursts of light and sparks where they
/// land, cones of fire or frost from a dragon's jaws, and a rising glow when someone is healed. Each lights up
/// its surroundings briefly and cleans itself up.
/// </summary>
public static class Effects
{
    public static Color Of(DamageKind kind) => kind switch
    {
        DamageKind.Fire => new Color("#ff7a2a"),
        DamageKind.Frost => new Color("#9ad8ff"),
        DamageKind.Shock => new Color("#c8b4ff"),
        DamageKind.Poison => new Color("#86e04a"),
        DamageKind.Sun => new Color("#ffe48a"),
        _ => new Color("#f0e8d8"),
    };

    /// <summary>A soft round spot, bright in the middle and fading out: every spark, puff and wisp is one.</summary>
    public static readonly GradientTexture2D Dot = new()
    {
        Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0),
        Gradient = new Gradient { Offsets = [0, 0.35f, 1], Colors = [Colors.White, new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 0)] },
    };

    static StandardMaterial3D Glow(Color c) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c, VertexColorUseAsAlbedo = true, AlbedoTexture = Dot,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2,
    };

    static CpuParticles3D Sparks(Color c, int amount, float size, double life)
    {
        var mesh = new QuadMesh { Size = Vector2.One * size, Material = Glow(c) };
        var fade = new Gradient();
        fade.SetColor(0, c);
        fade.SetColor(1, new Color(c, 0));
        return new CpuParticles3D
        {
            Mesh = mesh, Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.9f, Emitting = true,
            ColorRamp = fade, Gravity = Vector3.Zero, LocalCoords = false,
        };
    }

    static void Flash(Node3D at, Color c, float energy, float range, double seconds)
    {
        var light = new OmniLight3D { LightColor = c, LightEnergy = energy, OmniRange = range };
        at.AddChild(light);
        light.CreateTween().TweenProperty(light, "light_energy", 0, seconds);
    }

    static Node3D Holder(Node world, Vector3 at, double seconds)
    {
        var n = new Node3D();
        world.AddChild(n);
        n.GlobalPosition = at;
        n.GetTree().CreateTimer(seconds).Timeout += n.QueueFree;
        return n;
    }

    /// <summary>A burst where a spell lands: sparks flung out, a flash of light.</summary>
    public static void Burst(Node world, Vector3 at, DamageKind kind, float radius = 1.5f)
    {
        var c = Of(kind);
        var n = Holder(world, at, 2);
        var p = Sparks(c, 24 + (int)(radius * 10), 0.25f + radius * 0.05f, 0.7);
        p.Direction = Vector3.Up;
        p.Spread = 180;
        p.InitialVelocityMin = radius * 1.5f;
        p.InitialVelocityMax = radius * 3f;
        (p.DampingMin, p.DampingMax) = (radius * 2, radius * 3);
        n.AddChild(p);
        Flash(n, c, 4, radius * 4 + 3, 0.5);
    }

    /// <summary>A glowing bolt that flies from one point to another, and bursts there.</summary>
    public static void Bolt(Node world, Vector3 from, Vector3 to, DamageKind kind, float size = 0.22f)
    {
        var c = Of(kind);
        var seconds = Math.Clamp(from.DistanceTo(to) / 40, 0.08, 0.6);
        var n = Holder(world, from, seconds + 0.05);
        n.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = size, Height = size * 2 }, MaterialOverride = Glow(c), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var trail = Sparks(c, 30, size * 1.2f, 0.35);
        trail.OneShot = false;
        trail.Explosiveness = 0;
        n.AddChild(trail);
        n.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 2, OmniRange = 4 });
        n.CreateTween().TweenProperty(n, "global_position", to, seconds);
        n.GetTree().CreateTimer(seconds).Timeout += () => Burst(world, to, kind, 1);
    }

    /// <summary>Breath or a spray of flame: a cone of sparks from a point along a direction.</summary>
    public static void Cone(Node world, Vector3 from, Vector3 dir, DamageKind kind, float length)
    {
        var c = Of(kind);
        var n = Holder(world, from, 2);
        var p = Sparks(c, 90, 0.5f + length * 0.04f, 0.6);
        p.Explosiveness = 0.2f;
        p.Direction = dir.Normalized();
        p.Spread = 18;
        p.InitialVelocityMin = length * 1.4f;
        p.InitialVelocityMax = length * 1.9f;
        p.ScaleAmountMax = 2.5f;
        n.AddChild(p);
        Flash(n, c, 5, length, 0.7);
    }

    /// <summary>A soft glow rising around someone: healing, or a blessing.</summary>
    public static void Rise(Node3D who, DamageKind kind = DamageKind.Sun, float height = 1.8f)
    {
        var c = Of(kind);
        var n = new Node3D();
        who.AddChild(n);
        who.GetTree().CreateTimer(2).Timeout += n.QueueFree;
        var p = Sparks(c, 30, 0.15f, 1.2);
        p.Explosiveness = 0.3f;
        p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Box;
        p.EmissionBoxExtents = new Vector3(0.5f, height / 2, 0.5f);
        p.Position = new Vector3(0, height / 2, 0);
        p.Direction = Vector3.Up;
        p.Spread = 10;
        p.InitialVelocityMin = 1;
        p.InitialVelocityMax = 2;
        n.AddChild(p);
        Flash(n, c, 2, 5, 1);
    }

    /// <summary>A puff of grey smoke (a thief's smoke bomb).</summary>
    public static void Smoke(Node world, Vector3 at, float radius)
    {
        var n = Holder(world, at, 4);
        var c = new Color(0.55f, 0.55f, 0.58f, 0.7f);
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Dot,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var fade = new Gradient();
        fade.SetColor(0, c);
        fade.SetColor(1, new Color(c, 0));
        n.AddChild(new CpuParticles3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * radius * 0.5f, Material = mat }, Amount = 40, Lifetime = 3, OneShot = true,
            Explosiveness = 0.8f, Direction = Vector3.Up, Spread = 180, InitialVelocityMin = radius * 0.4f, InitialVelocityMax = radius,
            DampingMin = radius * 0.5f, DampingMax = radius * 0.8f, Gravity = new Vector3(0, 0.3f, 0), ColorRamp = fade, ScaleAmountMin = 1, ScaleAmountMax = 2.5f,
        });
    }
}
