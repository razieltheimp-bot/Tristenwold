using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Things grown onto a creature's bones (a crown, a chimney, stone plates, thorns and blossom), each placed and sized
/// in metres of the finished body, facing its forward (-Z), and riding on its bone so it moves as the creature does.
/// </summary>
public sealed class Graft
{
    readonly Skeleton3D _s;
    readonly Transform3D _toBody;

    /// <summary>How tall the body stands, and where the top of it is.</summary>
    public float Tall { get; }

    Graft(Node3D body, Skeleton3D s)
    {
        _s = s;
        var t = Transform3D.Identity;
        for (Node? n = s; n is not null && n != body; n = n.GetParent())
            if (n is Node3D n3) t = n3.Transform * t;
        _toBody = t;
        Tall = Models.Bounds(body).End.Y;
    }

    public static Graft? On(Node3D body) =>
        body.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is { } s ? new Graft(body, s) : null;

    public bool Has(string bone) => _s.FindBone(bone) >= 0;

    Transform3D Rest(string bone) => _s.FindBone(bone) is var i and >= 0 ? _s.GetBoneGlobalRest(i) : Transform3D.Identity;

    /// <summary>Where a bone is in the body at rest, in metres.</summary>
    public Vector3 Bone(string bone) => _toBody * Rest(bone).Origin;

    /// <summary>A node riding on a bone, standing at <paramref name="at"/> in the body: whatever goes in it is in metres.</summary>
    public Node3D Hold(string bone, Vector3 at, Basis? basis = null)
    {
        var attach = new BoneAttachment3D { BoneName = bone };
        _s.AddChild(attach);
        var node = new Node3D { Transform = Rest(bone).AffineInverse() * _toBody.AffineInverse() * new Transform3D(basis ?? Basis.Identity, at) };
        attach.AddChild(node);
        return node;
    }

    public MeshInstance3D Add(string bone, Vector3 at, Mesh mesh, Material? material, Basis? basis = null)
    {
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
        Hold(bone, at, basis).AddChild(m);
        return m;
    }

    /// <summary>A horn, thorn, spike or crystal: a pointed cone from a root along a direction.</summary>
    public void Spike(string bone, Vector3 root, Vector3 dir, float length, float radius, Material material, int sides = 6)
    {
        var up = dir.Normalized();
        Add(bone, root + up * length / 2, new CylinderMesh { TopRadius = 0, BottomRadius = radius, Height = length, RadialSegments = sides, Rings = 1 },
            material, Along(up));
    }

    /// <summary>A rotation whose up is <paramref name="up"/>.</summary>
    public static Basis Along(Vector3 up)
    {
        up = up.Normalized();
        var side = up.Cross(Mathf.Abs(up.Y) < 0.95f ? Vector3.Up : Vector3.Right).Normalized();
        return new Basis(side, up, side.Cross(up));
    }
}

/// <summary>
/// The bosses of the wild lairs and their own, built from the creature models and grown with what makes each one
/// itself: a goblin king with a crown and a club and firepots at his belt; a cave bear plated with stone; a walking
/// briar in red bloom; a ghost captain and his legion in armour of moonlight; a basilisk grown through with
/// amethyst; a dwarven forge-golem with a furnace in its chest and a chimney for a head; a shadow-cat veined with
/// violet light; and a thunderhawk with the storm in its feathers.
/// </summary>
public static class Lairfolk
{
    public static Body? For(string look) => look switch
    {
        "goblin_king" => GoblinKing(),
        "goblin_tinker" => Tinker(),
        "rock_bear" => RockBear(),
        "briar_mother" => BriarMother(),
        "ghost_captain" or "hollow_soldier" => Ghost(look),
        "crystal_basilisk" => Basilisk(),
        "crystal_skeever" => CrystalSkeever(),
        "forge_golem" => ForgeGolem(),
        "duskprowler" => Shadow("ice_wolf", 2.0f, boss: true),
        "shadow_wolf" => Shadow("wolf", 1.0f, boss: false),
        "thunderhawk" => Hawk(3.2f, boss: true),
        "stormwing" => Hawk(1.1f, boss: false),
        _ => null,
    };

    // ---- materials ------------------------------------------------------------------------------

    static StandardMaterial3D Plain(string hex, float rough = 0.8f, float metal = 0) =>
        new() { AlbedoColor = new Color(hex), Roughness = rough, Metallic = metal };

    static StandardMaterial3D Glowing(string hex, float energy) => new()
    {
        AlbedoColor = new Color(hex), EmissionEnabled = true, Emission = new Color(hex), EmissionEnergyMultiplier = energy, Roughness = 0.5f,
    };

    static readonly StandardMaterial3D Gold = Plain("#e0b040", 0.3f, 0.9f);
    static readonly StandardMaterial3D Iron = Plain("#3a3a3e", 0.45f, 0.85f);
    static readonly StandardMaterial3D Wood = Plain("#5a3e26");
    static readonly StandardMaterial3D Thorn = Plain("#2a2414", 0.9f);
    static readonly StandardMaterial3D Vine = Plain("#3a5a22", 0.85f);
    static readonly StandardMaterial3D Petal = new() { AlbedoColor = new Color("#c01c28"), Roughness = 0.6f, RimEnabled = true, Rim = 0.4f };
    static readonly StandardMaterial3D Pollen = Glowing("#f0c030", 0.6f);
    static readonly StandardMaterial3D Fire = Glowing("#ff7a20", 3f);
    static readonly StandardMaterial3D Crystal = new()
    {
        AlbedoColor = new Color(0.7f, 0.45f, 1f, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f,
        EmissionEnabled = true, Emission = new Color("#9a50ff"), EmissionEnergyMultiplier = 0.9f, RimEnabled = true, Rim = 0.8f,
    };

    /// <summary>Copies every material on a model and changes it (iron for the golem, soot for the tinkers).</summary>
    static void Recoat(Node model, Action<BaseMaterial3D> change)
    {
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (var s = 0; s < m.GetSurfaceOverrideMaterialCount(); s++)
                if ((m.GetActiveMaterial(s)?.Duplicate() ?? new StandardMaterial3D()) is BaseMaterial3D mat)
                {
                    change(mat);
                    m.SetSurfaceOverrideMaterial(s, mat);
                }
    }

    /// <summary>A light, flickering if asked (fire, lightning).</summary>
    static OmniLight3D Light(string hex, float energy, float range, float flicker = 0) =>
        flicker > 0 ? new Flicker { LightColor = new Color(hex), LightEnergy = energy, OmniRange = range, Base = energy, Amount = flicker }
            : new OmniLight3D { LightColor = new Color(hex), LightEnergy = energy, OmniRange = range };

    static CpuParticles3D Motes(Color colour, float size, int amount, Vector3 extents, float rise, float life = 1.6f, bool add = true) => new()
    {
        Mesh = new QuadMesh
        {
            Size = Vector2.One * size,
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = add ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            },
        },
        Amount = amount, Lifetime = life, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = extents,
        Direction = Vector3.Up, Spread = 40, Gravity = new Vector3(0, rise, 0), InitialVelocityMin = 0.05f, InitialVelocityMax = 0.3f,
        ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f, LocalCoords = false,
        ColorRamp = new Gradient { Offsets = [0, 0.25f, 1], Colors = [new Color(colour, 0), colour, new Color(colour, 0)] },
    };

    // ---- the goblin warren ----------------------------------------------------------------------

    /// <summary>A goblin grown huge: a crown of gold spikes, a spiked club, firepots at his belt.</summary>
    static Body? GoblinKing()
    {
        if (Creature.Create("goblin.glb", 2.4f) is not { } c || Graft.On(c) is not { } g) return null;
        var head = g.Bone("head");
        var top = new Vector3(head.X, g.Tall * 0.95f, head.Z);
        var r = g.Tall * 0.075f;
        g.Add("head", top, new CylinderMesh { TopRadius = r * 1.1f, BottomRadius = r, Height = g.Tall * 0.05f, RadialSegments = 12 }, Gold);
        for (var i = 0; i < 7; i++)
        {
            var a = i * Mathf.Tau / 7;
            var round = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r * 1.05f;
            g.Spike("head", top + round + Vector3.Up * g.Tall * 0.02f, Vector3.Up + round.Normalized() * 0.2f, g.Tall * 0.07f, g.Tall * 0.014f, Gold, 4);
        }
        g.Add("head", top + new Vector3(0, g.Tall * 0.03f, -r * 1.05f), new SphereMesh { Radius = g.Tall * 0.012f, Height = g.Tall * 0.024f }, Glowing("#d02020", 1.5f));
        Club(g, "hand_R", "forearm_R", g.Tall * 0.42f, g.Tall * 0.05f);
        // Firepots slung at his hip, smouldering.
        var hip = g.Bone("hip_L");
        for (var i = 0; i < 3; i++)
            g.Add("spine1", hip + new Vector3(-g.Tall * 0.06f, -g.Tall * (0.02f + i * 0.035f), g.Tall * (i - 1) * 0.04f),
                new SphereMesh { Radius = g.Tall * 0.022f, Height = g.Tall * 0.04f }, Glowing("#ff6a10", 1.2f));
        return c;
    }

    /// <summary>A club (or a hammer) in a fist, along the forearm.</summary>
    static void Club(Graft g, string hand, string forearm, float length, float thick, bool hammer = false)
    {
        var at = g.Bone(hand);
        var along = (at - g.Bone(forearm)).Normalized();
        var up = (along * 0.3f + Vector3.Up).Normalized(); // held upright, as a club is
        var holder = g.Hold(hand, at);
        holder.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = thick * (hammer ? 0.35f : 1), BottomRadius = thick * 0.4f, Height = length, RadialSegments = 8 },
            MaterialOverride = hammer ? Iron : Wood, Transform = new Transform3D(Graft.Along(up), up * length * 0.35f),
        });
        if (hammer)
        {
            holder.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(thick * 3.6f, thick * 2.2f, thick * 2.2f) }, MaterialOverride = Iron,
                Transform = new Transform3D(Graft.Along(up), up * length * 0.85f),
            });
            return;
        }
        for (var i = 0; i < 6; i++)
        {
            var a = i * 2.4f;
            var side = Graft.Along(up) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var stud = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = thick * 0.25f, Height = thick * 0.9f, RadialSegments = 4 }, MaterialOverride = Iron,
                Transform = new Transform3D(Graft.Along(side), up * length * (0.55f + i * 0.06f) + side * thick * 0.9f),
            };
            holder.AddChild(stud);
        }
    }

    static Body? Tinker()
    {
        if (Creature.Create("goblin.glb", 1.25f, new Color("#9a9a8a")) is not { } c || Graft.On(c) is not { } g) return null;
        Club(g, "hand_R", "forearm_R", g.Tall * 0.35f, g.Tall * 0.03f, hammer: true);
        return c;
    }

    // ---- the rock bear's den --------------------------------------------------------------------

    /// <summary>A cave bear so old the stone of its den has grown over its shoulders and back.</summary>
    static Body? RockBear()
    {
        if (Creature.Create("bear.glb", 2.6f, new Color("#b8a898")) is not { } c || Graft.On(c) is not { } g) return null;
        if (Models.MeshOf("ph:boulder.glb") is not { } rock) return c;
        var size = rock.GetAabb().Size;
        var unit = 1 / Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        var rng = new RandomNumberGenerator { Seed = 7 };
        var plates = new (string Bone, float Up, float Side, float Size)[]
        {
            ("Ursidae_Neck", 0.2f, 0, 0.32f), ("Ursidae_Spine_3", 0.26f, 0, 0.42f), ("Ursidae_Spine_3", 0.2f, 0.14f, 0.3f), ("Ursidae_Spine_3", 0.2f, -0.14f, 0.3f),
            ("Ursidae_Spine_2", 0.27f, 0.05f, 0.38f), ("Ursidae_Spine_1", 0.25f, -0.05f, 0.34f), ("Ursidae_Spine", 0.22f, 0.08f, 0.28f),
            ("Ursidae_Upperarm_R", 0.12f, 0.1f, 0.26f), ("Ursidae_Upperarm_L", 0.12f, -0.1f, 0.26f),
        };
        foreach (var (bone, up, side, s) in plates)
        {
            if (!g.Has(bone)) continue;
            var at = g.Bone(bone) + new Vector3(side * g.Tall, up * g.Tall, 0);
            var basis = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)).Scaled(new Vector3(1, 0.55f, 1) * s * g.Tall * unit);
            g.Add(bone, at, rock, null, basis);
        }
        return c;
    }

    // ---- the briar ruin -------------------------------------------------------------------------

    /// <summary>A walking mound of briar in red bloom, thorned all over, shedding pollen.</summary>
    static Body? BriarMother()
    {
        if (Creature.Create("forest_monster.glb", 3.6f, new Color("#7a9a5a")) is not { } c || Graft.On(c) is not { } g) return null;
        var rng = new RandomNumberGenerator { Seed = 11 };
        string[] bones = ["Spine1", "Spine2", "Spine3", "Neck", "Head", "Shoulder_L", "Shoulder_R", "Arm_L", "Arm_R", "Forearm_L", "Forearm_R"];
        foreach (var bone in bones.Where(g.Has))
        {
            var at = g.Bone(bone);
            // Thorns, splaying every way but inward.
            for (var i = 0; i < 4; i++)
            {
                var dir = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-0.2f, 1), rng.RandfRange(-1, 1)).Normalized();
                g.Spike(bone, at + dir * g.Tall * 0.07f, dir, g.Tall * rng.RandfRange(0.04f, 0.08f), g.Tall * 0.008f, Thorn, 4);
            }
            // Blooms: five red petals round a golden heart.
            for (var i = 0; i < 2; i++)
            {
                var dir = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(0, 1), rng.RandfRange(-1, 1)).Normalized();
                var bloom = g.Hold(bone, at + dir * g.Tall * 0.08f, Graft.Along(dir));
                var r = g.Tall * rng.RandfRange(0.025f, 0.04f);
                bloom.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r * 0.35f, Height = r * 0.5f }, MaterialOverride = Pollen });
                for (var p = 0; p < 5; p++)
                {
                    var a = p * Mathf.Tau / 5;
                    bloom.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = r * 0.5f, Height = r * 0.35f, RadialSegments = 8, Rings = 4 }, MaterialOverride = Petal,
                        Position = new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * r * 0.7f,
                    });
                }
            }
        }
        // Vines coiled round the forearms.
        foreach (var arm in new[] { "Forearm_L", "Forearm_R" }.Where(g.Has))
        {
            var along = (g.Bone(arm.Replace("Forearm", "Hand")) - g.Bone(arm)).Normalized();
            for (var i = 0; i < 3; i++)
                g.Add(arm, g.Bone(arm).Lerp(g.Bone(arm.Replace("Forearm", "Hand")), 0.2f + i * 0.3f),
                    new TorusMesh { InnerRadius = g.Tall * 0.03f, OuterRadius = g.Tall * 0.045f, Rings = 12, RingSegments = 6 }, Vine, Graft.Along(along));
        }
        c.AddChild(Placed(Motes(new Color(0.9f, 0.85f, 0.3f, 0.7f), 0.12f, 40, new Vector3(1.2f, 1.4f, 1.2f), 0.15f, 3), new Vector3(0, g.Tall * 0.6f, 0)));
        return c;
    }

    static T Placed<T>(T node, Vector3 at) where T : Node3D
    {
        node.Position = at;
        return node;
    }

    // ---- the hollow bastion ---------------------------------------------------------------------

    /// <summary>Armour of moonlight: the figure's own texture turned pale and see-through, brightest at its edges.</summary>
    static readonly Shader Spectral = new()
    {
        Code = """
            shader_type spatial;
            render_mode blend_mix, depth_draw_always, cull_back;
            uniform sampler2D tex : source_color, filter_linear_mipmap;
            uniform vec4 tint : source_color = vec4(0.55, 0.85, 1.0, 1.0);
            void fragment() {
                float l = dot(texture(tex, UV).rgb, vec3(0.3, 0.59, 0.11));
                float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
                float pulse = 0.85 + 0.15 * sin(TIME * 2.0 + VERTEX.y * 4.0);
                ALBEDO = tint.rgb * (0.25 + 0.75 * l);
                EMISSION = tint.rgb * (0.3 + 1.6 * fres) * pulse;
                ALPHA = clamp(0.32 + 0.6 * fres + 0.15 * l, 0.0, 0.92);
                ROUGHNESS = 0.4;
            }
            """,
    };

    static Body Ghost(string look)
    {
        var f = Figure.Create(look);
        var boss = look == "ghost_captain";
        var looks = new Dictionary<Texture2D, ShaderMaterial>();
        foreach (var m in f.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var tex = (m.GetActiveMaterial(0) as BaseMaterial3D)?.AlbedoTexture;
            if (tex is null) continue;
            if (!looks.TryGetValue(tex, out var mat))
            {
                looks[tex] = mat = new ShaderMaterial { Shader = Spectral };
                mat.SetShaderParameter("tex", tex);
                if (boss) mat.SetShaderParameter("tint", new Color(0.7f, 0.92f, 1f));
            }
            m.MaterialOverride = mat;
            m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        // A cold mist about the feet, and a pale light.
        f.AddChild(Placed(Motes(new Color(0.7f, 0.9f, 1f, boss ? 0.35f : 0.22f), boss ? 1.2f : 0.8f, boss ? 30 : 12, new Vector3(0.7f, 0.1f, 0.7f), 0.1f, 2.5f, add: false), new Vector3(0, 0.2f, 0)));
        f.AddChild(Placed(Light("#9ad8ff", boss ? 1.6f : 0.5f, boss ? 7 : 3), new Vector3(0, 1.6f, 0)));
        return f;
    }

    // ---- the amethyst hollow --------------------------------------------------------------------

    /// <summary>A wingless dragon-kin, deep amethyst, with crystal grown down its spine.</summary>
    static Body? Basilisk()
    {
        if (Creature.Create("dragon.glb", 2.4f, coat: "dragons/amethyst.png", glow: "dragons/amethyst_glow.png") is not { } c) return null;
        c.Crystallise();
        c.AddChild(Placed(Light("#a060ff", 2.2f, 8, flicker: 0.15f), new Vector3(0, 1.6f, 0)));
        c.AddChild(Placed(Motes(new Color(0.75f, 0.5f, 1f, 0.8f), 0.08f, 24, new Vector3(0.8f, 0.6f, 1.6f), 0.3f), new Vector3(0, 1.2f, 0)));
        return c;
    }

    static Body? CrystalSkeever()
    {
        if (Creature.Create("giant_rat.glb", 0.7f, new Color("#a888d8")) is not { } c || Graft.On(c) is not { } g) return null;
        foreach (var (bone, lean) in new[] { ("Backbone", -0.3f), ("Backbone.001", 0f), ("Backbone.002", 0.3f) }.Where(b => g.Has(b.Item1)))
        {
            var at = g.Bone(bone);
            g.Spike(bone, at + Vector3.Up * g.Tall * 0.25f, new Vector3(lean, 1, 0.2f), g.Tall * 0.45f, g.Tall * 0.06f, Crystal, 5);
            g.Spike(bone, at + Vector3.Up * g.Tall * 0.2f, new Vector3(-lean - 0.4f, 1, -0.3f), g.Tall * 0.3f, g.Tall * 0.05f, Crystal, 5);
        }
        return c;
    }

    // ---- the sunken forge -----------------------------------------------------------------------

    /// <summary>A dwarven smith-engine: an iron giant with a furnace in its chest, a chimney for a head, a great hammer.</summary>
    static Body ForgeGolem()
    {
        var f = Figure.Create("forge_golem");
        foreach (var m in f.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (m.Name.ToString().Contains("2H_Axe")) m.Visible = false;
        Recoat(f, m => { m.AlbedoColor = new Color("#6a6a72"); m.Metallic = 0.85f; m.Roughness = 0.42f; m.MetallicSpecular = 0.7f; });
        if (Graft.On(f) is not { } g) return f;
        var (head, chest) = (g.Bone("head"), g.Bone("chest"));
        var unit = g.Tall;
        // The chimney, smoking and glowing at its mouth.
        var stack = head + Vector3.Up * unit * 0.1f;
        g.Add("head", stack, new CylinderMesh { TopRadius = unit * 0.045f, BottomRadius = unit * 0.06f, Height = unit * 0.2f, RadialSegments = 10 }, Iron);
        g.Add("head", stack + Vector3.Up * unit * 0.1f, new CylinderMesh { TopRadius = unit * 0.06f, BottomRadius = unit * 0.06f, Height = unit * 0.025f, RadialSegments = 10 }, Iron);
        g.Add("head", stack + Vector3.Up * unit * 0.105f, new CylinderMesh { TopRadius = unit * 0.035f, BottomRadius = unit * 0.035f, Height = unit * 0.02f, RadialSegments = 10 }, Fire);
        var smoke = g.Hold("head", stack + Vector3.Up * unit * 0.12f);
        smoke.AddChild(Motes(new Color(0.12f, 0.11f, 0.1f, 0.55f), unit * 0.12f, 30, Vector3.One * unit * 0.02f, 0.8f, 3, add: false));
        smoke.AddChild(Motes(new Color(1, 0.55f, 0.15f, 1), unit * 0.02f, 14, Vector3.One * unit * 0.02f, 1.2f, 1.2f));
        // The furnace glowing through a grille in its chest.
        var front = Vector3.Forward * unit * 0.09f;
        g.Add("chest", chest + front + Vector3.Down * unit * 0.02f, new BoxMesh { Size = new Vector3(0.14f, 0.1f, 0.03f) * unit }, Fire);
        for (var i = 0; i < 4; i++)
            g.Add("chest", chest + front * 1.08f + new Vector3(0, (i - 1.5f) * 0.025f - 0.02f, 0) * unit, new BoxMesh { Size = new Vector3(0.15f, 0.008f, 0.02f) * unit }, Iron);
        g.Hold("chest", chest + front * 1.6f).AddChild(Light("#ff7a2a", 2.5f, 6, flicker: 0.25f));
        // Pauldrons like anvils.
        foreach (var arm in new[] { "upperarm.l", "upperarm.r" })
            g.Add(arm, g.Bone(arm) + Vector3.Up * unit * 0.03f, new BoxMesh { Size = new Vector3(0.12f, 0.06f, 0.12f) * unit }, Iron);
        Club(g, "handslot.r", "lowerarm.r", unit * 0.42f, unit * 0.035f, hammer: true);
        return f;
    }

    // ---- the gloaming ruin ----------------------------------------------------------------------

    /// <summary>Black fur veined with violet light, and a dark haze rising off it.</summary>
    static Body? Shadow(string model, float height, bool boss)
    {
        if (Creature.Create($"{model}.glb", height, coat: $"{model}_shadow.png", glow: $"{model}_shadow_glow.png") is not { } c) return null;
        c.AddChild(Placed(Motes(new Color(0.35f, 0.15f, 0.55f, 0.35f), height * 0.5f, boss ? 36 : 10, new Vector3(0.3f, 0.3f, 0.7f) * height, 0.4f, 2, add: false), new Vector3(0, height * 0.6f, 0)));
        if (boss) c.AddChild(Placed(Light("#a050ff", 1.4f, 6), new Vector3(0, height, 0)));
        return c;
    }

    // ---- the storm eyrie ------------------------------------------------------------------------

    /// <summary>A storm-blue hawk, lightning crackling in its feathers.</summary>
    static Body? Hawk(float height, bool boss)
    {
        if (Creature.Create("hawk.glb", height, coat: "hawk_storm.png", glow: "hawk_storm_glow.png") is not { } c) return null;
        c.AddChild(Placed(Motes(new Color(0.6f, 0.9f, 1f, 1), height * 0.05f, boss ? 40 : 10, new Vector3(0.6f, 0.4f, 0.6f) * height, 0, 0.35f), new Vector3(0, height * 0.55f, 0)));
        if (boss) c.AddChild(Placed(Light("#8ad0ff", 2.2f, 10, flicker: 0.8f), new Vector3(0, height * 0.8f, 0)));
        return c;
    }
}

/// <summary>A light that flickers: a furnace, crystal, lightning in feathers.</summary>
public partial class Flicker : OmniLight3D
{
    public float Base = 1, Amount = 0.2f;
    double _t;

    public override void _Process(double delta)
    {
        _t += delta;
        var noise = Mathf.Sin((float)_t * 13) * 0.5f + Mathf.Sin((float)_t * 31.7f) * 0.3f + Mathf.Sin((float)_t * 5.3f) * 0.2f;
        // Lightning: mostly a glow, now and then a flash.
        LightEnergy = Base * (1 + Amount * noise + (Amount > 0.5f && GD.Randf() < 0.02f ? 3 : 0));
    }
}
