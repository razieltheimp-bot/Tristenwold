using Godot;
using Tristinwold.Deviant;

namespace Tristinwold.Game;

/// <summary>
/// What magic, breath and blows look like: fireballs trailing flame and smoke, ice shards trailing mist, lightning
/// that strikes rather than flies, globs of venom; bursts with a swell of light, a ring along the ground and the
/// element's leavings (embers, shards, arcs, a green cloud, rays); cones of breath with smoke or mist rolling off;
/// a column of light when someone is healed; arcs and sparks where a weapon lands. Each lights up its surroundings
/// briefly and cleans itself up.
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
        var dwindle = new Curve(); // they shrink away rather than winking out
        dwindle.AddPoint(new Vector2(0, 1));
        dwindle.AddPoint(new Vector2(0.6f, 0.8f));
        dwindle.AddPoint(new Vector2(1, 0));
        return new CpuParticles3D
        {
            Mesh = mesh, Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.9f, Emitting = true,
            ColorRamp = fade, Gravity = Vector3.Zero, LocalCoords = false, ScaleAmountCurve = dwindle,
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

    // ---- the pieces every effect is made of -----------------------------------------------------------------

    /// <summary>Soft puffs that hang in the air: smoke, mist, a poison cloud. Not glowing; they grow as they fade.</summary>
    static CpuParticles3D Puffs(Color c, int amount, float size, double life)
    {
        // The particles' own colours come through black, so the colour is the material's; each puff swells, then
        // thins away to nothing.
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Dot,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            ProximityFadeEnabled = true, ProximityFadeDistance = 0.8f,  // no hard edge where a puff meets the ground or a wall
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha, DistanceFadeMinDistance = 0.4f, DistanceFadeMaxDistance = 2.2f, // nor in front of the camera
        };
        var swell = new Curve();
        swell.AddPoint(new Vector2(0, 0.3f));
        swell.AddPoint(new Vector2(0.6f, 1));
        swell.AddPoint(new Vector2(1, 0));
        return new CpuParticles3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * size, Material = mat }, Amount = amount, Lifetime = life, OneShot = true, Emitting = true,
            Explosiveness = 0.7f, ScaleAmountCurve = swell, LocalCoords = false, Gravity = Vector3.Zero, AngleMin = 0, AngleMax = 360,
        };
    }

    /// <summary>Glowing sparks that fall: embers, sparks off steel, drops of venom.</summary>
    static CpuParticles3D Embers(Color c, int amount, float size, double life, float gravity)
    {
        var p = Sparks(c, amount, size, life);
        p.Gravity = new Vector3(0, gravity, 0);
        var shrink = new Curve();
        shrink.AddPoint(new Vector2(0, 1));
        shrink.AddPoint(new Vector2(1, 0.1f));
        p.ScaleAmountCurve = shrink;
        return p;
    }

    /// <summary>A ring of light racing out along the ground and fading.</summary>
    static void Ring(Node3D n, Color c, float radius, double seconds, float height = 0.15f)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2,
        };
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1, Rings = 40, RingSegments = 6 }, MaterialOverride = mat,
            Position = new Vector3(0, height, 0), Scale = new Vector3(0.2f, 0.4f, 0.2f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        n.AddChild(ring);
        var t = ring.CreateTween().SetParallel();
        t.TweenProperty(ring, "scale", new Vector3(radius, 0.4f, radius), seconds).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        t.TweenProperty(mat, "albedo_color", new Color(c, 0), seconds);
    }

    /// <summary>A ball of light that swells and fades where something lands.</summary>
    static void Swell(Node3D n, Color c, float radius, double seconds)
    {
        var mat = Glow(new Color(c, 0.6f));
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        mat.EmissionEnergyMultiplier = 0.8f;
        var ball = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * radius * 2.0f, Material = mat }, Scale = Vector3.One * 0.3f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        n.AddChild(ball);
        var t = ball.CreateTween().SetParallel();
        t.TweenProperty(ball, "scale", Vector3.One, seconds * 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(mat, "albedo_color", new Color(c, 0), seconds);
    }

    /// <summary>Splinters flung out and falling: shards of ice, chips of stone.</summary>
    static CpuParticles3D Shards(Color c, int amount, float size, float speed)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c, Roughness = 0.15f, Metallic = 0.1f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true, Emission = c.Darkened(0.3f), EmissionEnergyMultiplier = 0.6f,
        };
        return new CpuParticles3D
        {
            Mesh = new PrismMesh { Size = new Vector3(size * 0.5f, size * 1.6f, size * 0.5f), Material = mat }, Amount = amount, Lifetime = 1.1,
            OneShot = true, Emitting = true, Explosiveness = 1, Direction = Vector3.Up, Spread = 70, InitialVelocityMin = speed * 0.5f,
            InitialVelocityMax = speed, Gravity = new Vector3(0, -14, 0), AngularVelocityMin = -400, AngularVelocityMax = 400,
            ParticleFlagRotateY = true, LocalCoords = false, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.3f,
        };
    }

    /// <summary>
    /// A crackling arc of lightning between two points: a jagged line of light, redrawn every few hundredths of a
    /// second so it flickers and jumps, gone in a moment.
    /// </summary>
    public static void Lightning(Node world, Vector3 from, Vector3 to, Color c, double seconds = 0.25, float width = 0.06f)
    {
        var n = Holder(world, from, seconds + 0.05);
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c.Lightened(0.5f), BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 4, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var bolt = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = mat };
        n.AddChild(bolt);
        n.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 3, OmniRange = from.DistanceTo(to) * 0.6f + 3, Position = (to - from) / 2 });
        var rng = new RandomNumberGenerator();
        void Draw()
        {
            if (!GodotObject.IsInstanceValid(bolt)) return;
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            var span = to - from;
            var steps = Math.Max(4, (int)(span.Length() / 0.6f));
            var side = span.Cross(Vector3.Up).Normalized();
            if (side.LengthSquared() < 0.01f) side = Vector3.Right;
            var up = side.Cross(span).Normalized();
            var last = Vector3.Zero;
            for (var i = 1; i <= steps; i++)
            {
                var along = span * i / steps;
                var jitter = i == steps ? Vector3.Zero : (side * rng.RandfRange(-1, 1) + up * rng.RandfRange(-1, 1)) * Mathf.Min(0.6f, span.Length() * 0.08f);
                var next = along + jitter;
                foreach (var w in new[] { side, up })
                {
                    var o = w * width;
                    st.AddVertex(last - o); st.AddVertex(last + o); st.AddVertex(next + o);
                    st.AddVertex(last - o); st.AddVertex(next + o); st.AddVertex(next - o);
                }
                last = next;
            }
            bolt.Mesh = st.Commit();
        }
        Draw();
        for (var k = 1; k * 0.05 < seconds; k++) n.GetTree().CreateTimer(k * 0.05).Timeout += Draw;
    }

    /// <summary>A burst where a spell lands: a flash and a swell of light, a ring along the ground, and what the element
    /// leaves behind: embers and smoke, shards and mist, arcs of lightning, a hanging green cloud, rays of light, dust.</summary>
    public static void Burst(Node world, Vector3 at, DamageKind kind, float radius = 1.5f)
    {
        var c = Of(kind);
        var n = Holder(world, at, 3.5);
        var p = Sparks(c, 24 + (int)(radius * 10), 0.25f + radius * 0.05f, 0.7);
        p.Direction = Vector3.Up;
        p.Spread = 180;
        p.InitialVelocityMin = radius * 1.5f;
        p.InitialVelocityMax = radius * 3f;
        (p.DampingMin, p.DampingMax) = (radius * 2, radius * 3);
        n.AddChild(p);
        Swell(n, c, radius * 0.55f, 0.4);
        var ground = Holder(world, at with { Y = at.Y - 0.9f }, 1.2);
        Ring(ground, c, radius * 1.5f, 0.55);
        Flash(n, c, 4, radius * 4 + 3, 0.5);
        var loud = Mathf.Clamp(-10 + radius * 2.5f, -10, 4);
        switch (kind)
        {
            case DamageKind.Fire: Sound.At("magic/fire_boom", at, loud, range: 40 + radius * 6); break;
            case DamageKind.Frost: Sound.At("magic/frost_shatter", at, loud); break;
            case DamageKind.Shock: Sound.At(radius >= 4 ? "magic/thunder" : "magic/shock", at, loud, range: radius >= 4 ? 80 : 35); break;
            case DamageKind.Poison: Sound.At("magic/poison", at, loud); break;
            case DamageKind.Sun: Sound.At("magic/holy", at, loud); break;
            default:
                if (radius < 1) Sound.At("melee/arrow_hit", at, -4); // a crossbow bolt landing
                else Sound.At("magic/fire_boom", at, loud - 4, 0.6f); // the ground struck: a deep thud
                break;
        }
        switch (kind)
        {
            case DamageKind.Fire:
                var embers = Embers(new Color("#ffb040"), 20 + (int)(radius * 8), 0.12f, 1.4, 2.5f);
                (embers.Direction, embers.Spread, embers.InitialVelocityMin, embers.InitialVelocityMax) = (Vector3.Up, 60, radius, radius * 2.5f);
                n.AddChild(embers);
                var smoke = Puffs(new Color(0.42f, 0.4f, 0.38f, 0.3f), 10, radius * 0.9f, 2.0);
                (smoke.Direction, smoke.Spread, smoke.InitialVelocityMin, smoke.InitialVelocityMax, smoke.Gravity) = (Vector3.Up, 40, 0.4f, 1.2f, new Vector3(0, 0.6f, 0));
                n.AddChild(smoke);
                break;
            case DamageKind.Frost:
                n.AddChild(Shards(new Color("#d8f0ff", 0.85f), 10 + (int)(radius * 4), 0.18f + radius * 0.03f, 3 + radius * 2));
                var mist = Puffs(new Color(0.85f, 0.94f, 1f, 0.5f), 16, radius * 1.1f, 1.8);
                (mist.Direction, mist.Spread, mist.InitialVelocityMin, mist.InitialVelocityMax, mist.Gravity) = (Vector3.Up, 180, 0.5f, radius, new Vector3(0, -0.3f, 0));
                n.AddChild(mist);
                break;
            case DamageKind.Shock:
                var rng = new RandomNumberGenerator();
                for (var i = 0; i < 5; i++)
                {
                    var a = rng.Randf() * Mathf.Tau;
                    Lightning(world, at, at + new Vector3(Mathf.Cos(a), rng.RandfRange(-0.6f, 0.4f), Mathf.Sin(a)) * radius * rng.RandfRange(0.8f, 1.6f), c, 0.3, 0.035f);
                }
                break;
            case DamageKind.Poison:
                var cloud = Puffs(new Color(0.45f, 0.75f, 0.25f, 0.45f), 18, radius * 1.2f, 3.0);
                (cloud.Direction, cloud.Spread, cloud.InitialVelocityMin, cloud.InitialVelocityMax, cloud.Gravity, cloud.Explosiveness) = (Vector3.Up, 90, 0.3f, radius * 0.7f, new Vector3(0, 0.15f, 0), 0.4f);
                n.AddChild(cloud);
                var drips = Embers(new Color("#9ae04a"), 16, 0.1f, 1.0, -9);
                (drips.Direction, drips.Spread, drips.InitialVelocityMin, drips.InitialVelocityMax) = (Vector3.Up, 70, radius, radius * 2);
                n.AddChild(drips);
                break;
            case DamageKind.Sun:
                var beam = Glow(c);
                beam.BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY;
                var shaft = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(radius * 0.9f, 7), Material = beam }, Position = new Vector3(0, 2.5f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                n.AddChild(shaft);
                shaft.CreateTween().TweenProperty(beam, "albedo_color", new Color(c, 0), 0.7);
                var motes = Embers(new Color("#fff4c0"), 24, 0.09f, 1.6, 0.8f);
                (motes.Direction, motes.Spread, motes.InitialVelocityMin, motes.InitialVelocityMax) = (Vector3.Up, 50, 0.6f, radius * 1.4f);
                n.AddChild(motes);
                break;
            default:
                var dust = Puffs(new Color(0.55f, 0.5f, 0.42f, 0.5f), 12, radius * 0.8f, 1.4);
                (dust.Direction, dust.Spread, dust.InitialVelocityMin, dust.InitialVelocityMax) = (Vector3.Up, 80, 0.5f, radius * 1.5f);
                n.AddChild(dust);
                break;
        }
    }

    /// <summary>
    /// A spell flying to its mark, and bursting there: a fireball trailing flame and smoke, an ice shard trailing mist,
    /// a glob of venom dripping as it goes, a mote of sunlight; lightning doesn't fly at all, it strikes. A plain
    /// shot (a crossbow bolt) is a quick streak.
    /// </summary>
    public static void Bolt(Node world, Vector3 from, Vector3 to, DamageKind kind, float size = 0.22f)
    {
        var c = Of(kind);
        if (kind == DamageKind.Shock)
        {
            Lightning(world, from, to, c, 0.22, size * 0.3f);
            Burst(world, to, kind, 1);
            return;
        }
        switch (kind) // the sound of it leaving the hand
        {
            case DamageKind.Fire: Sound.At("magic/fire_cast", from, -4); break;
            case DamageKind.Frost: Sound.At("magic/frost_cast", from, -4); break;
            case DamageKind.Poison: Sound.At("magic/poison", from, -8, 1.3f); break;
            case DamageKind.Physical: Sound.At("melee/bow", from, -2); break;
            default: Sound.At("magic/cast", from, -6); break;
        }
        var seconds = Math.Clamp(from.DistanceTo(to) / (kind == DamageKind.Physical ? 70 : 40), 0.08, 0.6);
        var n = Holder(world, from, seconds + 0.05);
        var heading = to - from;
        if (heading.Length() > 0.1f) n.LookAt(to, Mathf.Abs(heading.Normalized().Y) > 0.98f ? Vector3.Forward : Vector3.Up);
        switch (kind)
        {
            case DamageKind.Physical:
                var streak = Glow(new Color("#f0e8d8"));
                streak.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
                streak.AlbedoTexture = null;
                n.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.9f), Material = streak }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                break;
            case DamageKind.Frost:
                var ice = new StandardMaterial3D { AlbedoColor = new Color("#e8f6ff", 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 1.5f };
                n.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(size, size * 3.5f, size), Material = ice }, RotationDegrees = new Vector3(-90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                break;
            default:
                n.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = size, Height = size * 2 }, MaterialOverride = Glow(c), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                n.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = size * 0.5f, Height = size }, MaterialOverride = Glow(Colors.White), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                break;
        }
        var trail = kind switch
        {
            DamageKind.Fire => Embers(new Color("#ff9a30"), 50, size * 1.6f, 0.4, 1.5f),
            DamageKind.Poison => Embers(new Color("#8ad040"), 30, size * 0.8f, 0.6, -8),
            DamageKind.Physical => Sparks(new Color("#f0e8d8", 0.5f), 12, 0.06f, 0.15),
            _ => Sparks(c, 30, size * 1.2f, 0.35),
        };
        trail.OneShot = false;
        trail.Explosiveness = 0;
        n.AddChild(trail);
        if (kind is DamageKind.Fire or DamageKind.Frost)
        {
            var wake = Puffs(kind == DamageKind.Fire ? new Color(0.45f, 0.42f, 0.4f, 0.22f) : new Color(0.85f, 0.95f, 1, 0.3f), 16, size * 2f, 0.6);
            (wake.OneShot, wake.Explosiveness) = (false, 0);
            n.AddChild(wake);
        }
        if (kind != DamageKind.Physical) n.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 2, OmniRange = 4 });
        n.CreateTween().TweenProperty(n, "global_position", to, seconds);
        n.GetTree().CreateTimer(seconds).Timeout += () => Burst(world, to, kind, kind == DamageKind.Physical ? 0.6f : 1);
    }

    /// <summary>Breath or a spray of flame: a cone of fire with smoke rolling off it, of frost and mist, of crackling
    /// light, of green venom.</summary>
    public static void Cone(Node world, Vector3 from, Vector3 dir, DamageKind kind, float length)
    {
        var c = Of(kind);
        var n = Holder(world, from, 3);
        Sound.At(kind switch { DamageKind.Fire => "magic/fire_cast", DamageKind.Frost => "magic/frost_cast", DamageKind.Shock => "magic/thunder", DamageKind.Poison => "magic/poison", _ => "magic/cast" }, from, 2, 0.8f, 60);
        var p = Sparks(c, 90, 0.5f + length * 0.04f, 0.6);
        p.Explosiveness = 0.2f;
        p.Direction = dir.Normalized();
        p.Spread = 18;
        p.InitialVelocityMin = length * 1.4f;
        p.InitialVelocityMax = length * 1.9f;
        p.ScaleAmountMax = 2.5f;
        n.AddChild(p);
        var core = Sparks(Colors.White.Lerp(c, 0.4f), 40, 0.3f + length * 0.02f, 0.4);
        (core.Explosiveness, core.Direction, core.Spread, core.InitialVelocityMin, core.InitialVelocityMax) = (0.2f, dir.Normalized(), 8, length * 1.6f, length * 2.1f);
        n.AddChild(core);
        var after = kind switch
        {
            DamageKind.Fire => Puffs(new Color(0.45f, 0.42f, 0.4f, 0.25f), 18, 0.8f + length * 0.06f, 1.6),
            DamageKind.Frost => Puffs(new Color(0.88f, 0.95f, 1, 0.3f), 18, 0.8f + length * 0.06f, 1.5),
            DamageKind.Poison => Puffs(new Color(0.45f, 0.75f, 0.25f, 0.3f), 18, 0.8f + length * 0.08f, 2.2),
            _ => null,
        };
        if (after is not null)
        {
            (after.Explosiveness, after.Direction, after.Spread, after.InitialVelocityMin, after.InitialVelocityMax, after.Gravity) =
                (0.15f, dir.Normalized(), 22, length * 0.6f, length * 1.0f, new Vector3(0, kind == DamageKind.Fire ? 1.2f : 0, 0));
            (after.DampingMin, after.DampingMax) = (length * 0.4f, length * 0.6f);
            n.AddChild(after);
        }
        if (kind == DamageKind.Shock)
        {
            var rng = new RandomNumberGenerator();
            var d = dir.Normalized();
            for (var i = 0; i < 4; i++)
            {
                var side = d.Cross(Vector3.Up).Normalized() * rng.RandfRange(-0.35f, 0.35f) * length;
                Lightning(world, from, from + d * length * rng.RandfRange(0.6f, 1f) + side, c, 0.35, 0.04f);
            }
        }
        Flash(n, c, 5, length, 0.7);
    }

    /// <summary>A soft glow rising around someone (healing, a blessing): a ring of light at their feet, motes rising,
    /// a column of warm light that fades.</summary>
    public static void Rise(Node3D who, DamageKind kind = DamageKind.Sun, float height = 1.8f)
    {
        var c = Of(kind);
        var n = new Node3D();
        who.AddChild(n);
        who.GetTree().CreateTimer(2.2).Timeout += n.QueueFree;
        Sound.At(kind == DamageKind.Sun ? "magic/heal" : "magic/cast", who.GlobalPosition, -4);
        var p = Sparks(c, 40, 0.15f, 1.4);
        p.Explosiveness = 0.3f;
        p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring;
        p.EmissionRingAxis = Vector3.Up;
        p.EmissionRingRadius = 0.7f;
        p.EmissionRingInnerRadius = 0.5f;
        p.EmissionRingHeight = 0.1f;
        p.Position = new Vector3(0, 0.1f, 0);
        p.Direction = Vector3.Up;
        p.Spread = 8;
        p.InitialVelocityMin = 1.2f;
        p.InitialVelocityMax = 2.2f;
        n.AddChild(p);
        Ring(n, c, 1.4f, 0.8, 0.05f);
        var beam = Glow(c);
        beam.BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY;
        var column = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.4f, height * 1.6f), Material = beam }, Position = new Vector3(0, height * 0.7f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        beam.AlbedoColor = new Color(c, 0.5f);
        n.AddChild(column);
        column.CreateTween().TweenProperty(beam, "albedo_color", new Color(c, 0), 1.2);
        Flash(n, c, 2, 5, 1);
    }

    /// <summary>
    /// A weapon landing: a bright arc where the blade swept through, and sparks thrown off along the blow (gold and
    /// bigger for a critical); a spell-blade's element colours it.
    /// </summary>
    public static void Impact(Node world, Vector3 at, Vector3 along, DamageKind kind, bool crit)
    {
        var c = kind == DamageKind.Physical ? new Color(crit ? "#ffd27a" : "#fff4e0") : Of(kind);
        var n = Holder(world, at, 1);
        Sound.At(crit ? "melee/crit" : "melee/hit", at, crit ? 0 : -3);
        along.Y = 0;
        if (along.LengthSquared() < 0.01f) along = Vector3.Forward;
        n.LookAt(at + along.Normalized(), Vector3.Up);
        var sparks = Embers(c, crit ? 28 : 14, crit ? 0.12f : 0.08f, 0.45, -9);
        (sparks.Direction, sparks.Spread, sparks.InitialVelocityMin, sparks.InitialVelocityMax) = (new Vector3(0, 0.4f, -1), 45, 3, crit ? 8 : 5);
        n.AddChild(sparks);
        // The arc: a thin crescent of light across the line of the blow, there and gone.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var (r, w, arc) = (crit ? 1.0f : 0.8f, crit ? 0.14f : 0.09f, 2.2f);
        for (var i = 0; i < 16; i++)
        {
            float A(int k) => -arc / 2 + arc * k / 16;
            Vector3 P(int k, float rr) => new(Mathf.Sin(A(k)) * rr, Mathf.Cos(A(k)) * rr * 0.35f - 0.1f, -Mathf.Cos(A(k)) * rr * 0.3f);
            var taper = Mathf.Sin(Mathf.Pi * i / 16f) * w;
            st.AddVertex(P(i, r - taper)); st.AddVertex(P(i, r + taper)); st.AddVertex(P(i + 1, r + taper));
            st.AddVertex(P(i, r - taper)); st.AddVertex(P(i + 1, r + taper)); st.AddVertex(P(i + 1, r - taper));
        }
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2,
        };
        var slash = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, RotationDegrees = new Vector3(0, 0, crit ? -35 : -20) };
        n.AddChild(slash);
        var t = slash.CreateTween().SetParallel();
        t.TweenProperty(slash, "rotation_degrees", new Vector3(0, 0, crit ? 25 : 15), 0.18);
        t.TweenProperty(mat, "albedo_color", new Color(c, 0), 0.22);
        if (crit) Flash(n, c, 3, 4, 0.25);
    }

    /// <summary>
    /// A limit break going off: a pillar of light on the hero, a shockwave racing out along the ground,
    /// sparks thrown high and the whole place lit up in the class's colour.
    /// </summary>
    public static void Limit(Node world, Vector3 at, Color c)
    {
        var n = Holder(world, at, 3);
        Sound.At("magic/limit", at, 2, range: 80);
        var glow = Glow(c);
        glow.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
        glow.AlbedoTexture = null;
        glow.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        glow.AlbedoColor = new Color(c, 0.3f);
        glow.EmissionEnergyMultiplier = 0.8f;
        var pillar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.7f, Height = 16, RadialSegments = 20, CapTop = false, CapBottom = false },
            MaterialOverride = glow, Position = new Vector3(0, 8, 0), Scale = new Vector3(0.2f, 1, 0.2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        n.AddChild(pillar);
        var t = pillar.CreateTween();
        t.TweenProperty(pillar, "scale", new Vector3(1.1f, 1, 1.1f), 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(pillar, "scale", new Vector3(0.01f, 1.2f, 0.01f), 0.6).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

        var ringGlow = (StandardMaterial3D)glow.Duplicate();
        ringGlow.AlbedoColor = new Color(c, 0.8f);
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1, Rings = 48, RingSegments = 8 },
            MaterialOverride = ringGlow, Position = new Vector3(0, 0.3f, 0), Scale = new Vector3(0.5f, 0.3f, 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        n.AddChild(ring);
        var r = ring.CreateTween().SetParallel();
        r.TweenProperty(ring, "scale", new Vector3(12, 0.3f, 12), 0.9).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        r.TweenProperty(ringGlow, "albedo_color", new Color(c, 0), 0.9);

        var p = Sparks(c, 120, 0.35f, 1.4);
        p.Direction = Vector3.Up;
        p.Spread = 35;
        p.InitialVelocityMin = 6;
        p.InitialVelocityMax = 16;
        p.Gravity = new Vector3(0, -6, 0);
        p.Position = new Vector3(0, 0.5f, 0);
        n.AddChild(p);
        Flash(n, c, 9, 26, 1.2);
    }

    /// <summary>A puff of grey smoke (a thief's smoke bomb).</summary>
    public static void Smoke(Node world, Vector3 at, float radius)
    {
        var n = Holder(world, at, 4);
        Sound.At("magic/cast", at, -2, 0.55f);
        var c = new Color(0.62f, 0.62f, 0.65f, 0.75f);
        var puffs = Puffs(c, 40, radius * 0.9f, 3);
        (puffs.Explosiveness, puffs.Direction, puffs.Spread, puffs.InitialVelocityMin, puffs.InitialVelocityMax) = (0.8f, Vector3.Up, 180, radius * 0.4f, radius);
        (puffs.DampingMin, puffs.DampingMax, puffs.Gravity, puffs.ScaleAmountMin, puffs.ScaleAmountMax) = (radius * 0.5f, radius * 0.8f, new Vector3(0, 0.3f, 0), 1, 2.2f);
        n.AddChild(puffs);
    }

    // ---- the bards ---------------------------------------------------------------------------------------------

    /// <summary>A glowing note of music: a round head and a stem with a flag, made of light.</summary>
    static Node3D Note(Color c, float size)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c, 0.95f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2.5f,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled,
        };
        var note = new Node3D();
        note.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = size * 0.32f, Height = size * 0.5f, RadialSegments = 10, Rings = 5 }, MaterialOverride = mat, RotationDegrees = new Vector3(0, 0, 25), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        note.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(size * 0.07f, size * 1.1f, size * 0.07f) }, MaterialOverride = mat, Position = new Vector3(size * 0.27f, size * 0.55f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        note.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(size * 0.32f, size * 0.08f, size * 0.07f) }, MaterialOverride = mat, Position = new Vector3(size * 0.42f, size * 1.0f, 0), RotationDegrees = new Vector3(0, 0, -30), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return note;
    }

    /// <summary>
    /// A bard's song going out: notes of light drifting up and away from them, swaying as they go, and rings of
    /// sound pulsing out along the ground one after another.
    /// </summary>
    public static void Song(Node world, Vector3 at, Color c, float radius = 4, int notes = 8)
    {
        var n = Holder(world, at, 2.6);
        for (var i = 0; i < notes; i++)
        {
            var note = Note(c, 0.35f + (float)GD.RandRange(0, 0.15));
            n.AddChild(note);
            var angle = (float)GD.RandRange(0, Mathf.Tau);
            var start = new Vector3(Mathf.Cos(angle) * 0.6f, 1.2f + (float)GD.RandRange(0, 0.6), Mathf.Sin(angle) * 0.6f);
            note.Position = start;
            note.Scale = Vector3.One * 0.01f;
            var end = start + new Vector3(Mathf.Cos(angle) * radius * 0.5f, 1.6f + (float)GD.RandRange(0, 1.2), Mathf.Sin(angle) * radius * 0.5f);
            var t = note.CreateTween().SetParallel();
            var delay = i * 0.08;
            t.TweenProperty(note, "scale", Vector3.One, 0.25).SetDelay(delay);
            t.TweenProperty(note, "position", end, 1.6).SetDelay(delay).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            t.TweenProperty(note, "rotation_degrees", new Vector3(0, (float)GD.RandRange(-90, 90), (float)GD.RandRange(-30, 30)), 1.6).SetDelay(delay);
            t.TweenProperty(note, "scale", Vector3.One * 0.01f, 0.4).SetDelay(delay + 1.3);
        }
        for (var k = 0; k < 3; k++)
        {
            var wave = k;
            n.GetTree().CreateTimer(wave * 0.22).Timeout += () => { if (GodotObject.IsInstanceValid(n)) Ring(n, c, radius, 0.7, 0.1f + wave * 0.05f); };
        }
        Flash(n, c, 2.5f, radius + 3, 0.9);
    }

    /// <summary>A wave of sound before a bard: arcs of light racing out and widening, and notes thrown along it.</summary>
    public static void Wave(Node world, Vector3 from, Vector3 dir, Color c, float length)
    {
        dir.Y = 0;
        if (dir.LengthSquared() < 0.01f) dir = Vector3.Forward;
        dir = dir.Normalized();
        var n = Holder(world, from, 1.6);
        n.LookAt(from + dir, Vector3.Up);
        for (var k = 0; k < 4; k++)
        {
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c, 0.75f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2,
            };
            // A ring standing up across the way the sound goes, so it reads as a pulse travelling forward.
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.88f, OuterRadius = 1, Rings = 36, RingSegments = 6 }, MaterialOverride = mat,
                RotationDegrees = new Vector3(90, 0, 0), Scale = Vector3.One * 0.3f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            n.AddChild(ring);
            var t = ring.CreateTween().SetParallel();
            var delay = k * 0.12;
            t.TweenProperty(ring, "position", new Vector3(0, 0, -length), 0.6).SetDelay(delay).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            t.TweenProperty(ring, "scale", Vector3.One * (length * 0.35f), 0.6).SetDelay(delay);
            t.TweenProperty(mat, "albedo_color", new Color(c, 0), 0.6).SetDelay(delay);
        }
        for (var i = 0; i < 5; i++)
        {
            var note = Note(c, 0.4f);
            n.AddChild(note);
            note.Position = new Vector3((float)GD.RandRange(-0.6, 0.6), (float)GD.RandRange(-0.2, 0.5), -0.5f);
            var t = note.CreateTween().SetParallel();
            t.TweenProperty(note, "position", new Vector3((float)GD.RandRange(-length * 0.3, length * 0.3), (float)GD.RandRange(0, 1.5), -length * 0.9f), 0.8).SetDelay(i * 0.06);
            t.TweenProperty(note, "scale", Vector3.One * 0.01f, 0.3).SetDelay(0.6 + i * 0.06);
        }
        Flash(n, c, 2, length, 0.5);
    }

    /// <summary>
    /// A scribe's glyph: a circle of runes written in light on the ground under a foe, turning as it charges, that
    /// bursts after <paramref name="delay"/> seconds.
    /// </summary>
    public static void Glyph(Node world, Vector3 at, Color c, float radius, double delay, DamageKind burst)
    {
        var n = Holder(world, at, delay + 1.5);
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2.2f,
        };
        var circle = new Node3D { Position = new Vector3(0, 0.12f, 0) };
        n.AddChild(circle);
        foreach (var r in new[] { radius, radius * 0.72f })
            circle.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = r * 0.95f, OuterRadius = r, Rings = 48, RingSegments = 4 }, MaterialOverride = mat, Scale = new Vector3(1, 0.2f, 1), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // The runes: strokes between the rings, each its own little shape.
        var rng = new RandomNumberGenerator();
        for (var i = 0; i < 12; i++)
        {
            var a = i / 12f * Mathf.Tau;
            var mid = (radius + radius * 0.72f) / 2;
            var rune = new Node3D { Position = new Vector3(Mathf.Cos(a) * mid, 0, Mathf.Sin(a) * mid), RotationDegrees = new Vector3(0, -Mathf.RadToDeg(a), 0) };
            circle.AddChild(rune);
            for (var s = 0; s < 2; s++)
                rune.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(radius * 0.03f, 0.02f, radius * rng.RandfRange(0.1f, 0.2f)) }, MaterialOverride = mat, RotationDegrees = new Vector3(0, rng.RandfRange(-50, 50), 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        circle.Scale = Vector3.One * 0.2f;
        var t = circle.CreateTween().SetParallel();
        t.TweenProperty(circle, "scale", Vector3.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(circle, "rotation_degrees", new Vector3(0, 220, 0), delay + 0.2);
        Flash(n, c, 1.5f, radius + 2, delay + 0.3);
        n.GetTree().CreateTimer(delay).Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(n)) return;
            n.CreateTween().TweenProperty(mat, "albedo_color", new Color(c, 0), 0.3);
            Burst(world, n.GlobalPosition + Vector3.Up * 0.6f, burst, radius);
        };
    }
}
