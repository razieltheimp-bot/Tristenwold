using Godot;

namespace Tristinwold.Game;

/// <summary>
/// The burned dead of the Ashen Crypt. The Cinder Warden is a skeleton warrior made monstrous: charred black, grown
/// with spikes and a great horn, his hands long claws, cracks of molten light down his chest, and where his head
/// should be a skull burning in a mane of fire. His thralls are smaller, charred, with embers for eyes. Everything
/// grown on them rides on their bones, so it moves as they do.
/// </summary>
public static class Cinder
{
    static readonly StandardMaterial3D Char = new() { AlbedoColor = new Color("#1c1510"), Roughness = 0.85f, Metallic = 0.1f };
    static readonly StandardMaterial3D Molten = new()
    {
        AlbedoColor = new Color("#ffb040"), EmissionEnabled = true, Emission = new Color("#ff8a20"), EmissionEnergyMultiplier = 3.5f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
    static readonly StandardMaterial3D Burning = new()
    {
        AlbedoColor = new Color("#c84a10"), EmissionEnabled = true, Emission = new Color("#ff5a10"), EmissionEnergyMultiplier = 1.6f, Roughness = 0.7f,
    };

    /// <summary>Makes a skeleton warrior figure into the Cinder Warden.</summary>
    public static void Warden(Figure f)
    {
        if (Skeleton(f) is not { } s) return;
        Paint(f, Burning, "Head", "Jaw", "Eyes"); // the rest keeps its (charred) texture

        Vector3 At(string bone) => s.FindBone(bone) is var b and >= 0 ? s.GetBoneGlobalRest(b).Origin : Vector3.Zero;
        var (head, chest, spine) = (At("head"), At("chest"), At("spine"));
        var (shoulderL, shoulderR) = (At("upperarm.l"), At("upperarm.r"));
        var (elbowL, elbowR, wristL, wristR) = (At("lowerarm.l"), At("lowerarm.r"), At("wrist.l"), At("wrist.r"));
        var (handL, handR) = (At("hand.l"), At("hand.r"));
        var tall = Math.Max(0.5f, head.Y);
        var back = Vector3.Back * -1; // the rig faces +Z, so its back is -Z

        // The great horn rising behind the burning head, and spikes along the shoulders and back.
        Spike(s, "chest", chest + back * 0.2f * tall + Vector3.Up * 0.15f * tall, (Vector3.Up * 3 + back * 0.4f + Vector3.Right * 0.3f).Normalized(), 0.95f * tall, 0.07f * tall);
        foreach (var (shoulder, side) in new[] { (shoulderL, Mathf.Sign(shoulderL.X)), (shoulderR, Mathf.Sign(shoulderR.X)) })
        {
            var outward = Vector3.Right * side;
            Spike(s, "chest", shoulder + Vector3.Up * 0.05f * tall, (Vector3.Up * 1.4f + outward + back * 0.3f).Normalized(), 0.42f * tall, 0.05f * tall);
            Spike(s, "chest", shoulder + outward * 0.08f * tall, (Vector3.Up * 0.6f + outward * 1.2f + back * 0.5f).Normalized(), 0.32f * tall, 0.04f * tall);
            Spike(s, "chest", shoulder - outward * 0.1f * tall + back * 0.12f * tall, (Vector3.Up + back * 0.8f).Normalized(), 0.28f * tall, 0.04f * tall);
        }
        Spike(s, "spine", spine + back * 0.16f * tall, (back + Vector3.Up * 0.6f).Normalized(), 0.22f * tall, 0.035f * tall);

        // Spiked forearms and long claws.
        foreach (var (elbow, wrist, hand, bone, side) in new[] { (elbowL, wristL, handL, "lowerarm.l", Mathf.Sign(elbowL.X)), (elbowR, wristR, handR, "lowerarm.r", Mathf.Sign(elbowR.X)) })
        {
            var along = (wrist - elbow).Normalized();
            var outward = (Vector3.Right * side - along * along.Dot(Vector3.Right * side)).Normalized();
            for (var i = 0; i < 3; i++)
                Spike(s, bone, elbow.Lerp(wrist, 0.2f + i * 0.3f), (outward + back * 0.4f - along * 0.4f).Normalized(), (0.16f - i * 0.03f) * tall, 0.025f * tall);
            var handBone = bone.Replace("lowerarm", "hand");
            var reach = (hand - wrist).Normalized();
            var across = reach.Cross(Vector3.Up).Normalized();
            for (var c = 0; c < 4; c++)
                Spike(s, handBone, hand + across * (c - 1.5f) * 0.03f * tall, (reach + across * (c - 1.5f) * 0.15f + Vector3.Down * 0.4f).Normalized(), 0.5f * tall, 0.026f * tall, Char);
        }

        // Cracks of molten light down the chest, dripping embers.
        var front = -back;
        foreach (var (x, y, length) in new[] { (-0.05f, 0.02f, 0.22f), (0.03f, -0.03f, 0.3f), (0.09f, 0.05f, 0.15f), (-0.11f, -0.06f, 0.18f) })
            Grow(s, "chest", new Transform3D(Basis.Identity, chest + front * 0.2f * tall + new Vector3(x, y - length / 2, 0) * tall),
                new BoxMesh { Size = new Vector3(0.012f, length, 0.01f) * tall }, Molten);

        // The skull's mane of fire, and its light.
        var fire = Holder(s, "head", head + Vector3.Up * 0.08f * tall);
        fire.AddChild(Flames(0.2f * tall, 140, 1.4f * tall));
        fire.AddChild(new OmniLight3D { LightColor = new Color("#ff8a30"), LightEnergy = 3.5f, OmniRange = 9, ShadowEnabled = true });
        var embers = Holder(s, "chest", chest + front * 0.2f * tall);
        embers.AddChild(Embers(0.12f * tall));
    }

    /// <summary>Makes a skeleton minion into a cinder thrall: charred, with burning eyes.</summary>
    public static void Thrall(Figure f)
    {
        if (Skeleton(f) is not { } s) return;
        Paint(f, Molten, "Eyes");
        var head = s.FindBone("head") is var b and >= 0 ? s.GetBoneGlobalRest(b).Origin : Vector3.Up;
        var glow = Holder(s, "head", head + Vector3.Up * 0.1f);
        glow.AddChild(Flames(0.08f, 24, 0.5f));
        glow.AddChild(new OmniLight3D { LightColor = new Color("#ff7a20"), LightEnergy = 1.2f, OmniRange = 3 });
    }

    static Skeleton3D? Skeleton(Node f) => f.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

    static void Paint(Node f, Material m, params string[] parts)
    {
        foreach (var mesh in f.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (parts.Any(p => mesh.Name.ToString().Contains(p))) mesh.MaterialOverride = m;
    }

    /// <summary>Something fixed to a bone, placed in the skeleton's space at rest.</summary>
    static void Grow(Skeleton3D s, string bone, Transform3D at, Mesh mesh, Material material) =>
        Attach(s, bone).AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Transform = Rest(s, bone).AffineInverse() * at });

    /// <summary>A node riding on a bone, standing where <paramref name="at"/> is in the skeleton at rest.</summary>
    static Node3D Holder(Skeleton3D s, string bone, Vector3 at)
    {
        var node = new Node3D { Transform = Rest(s, bone).AffineInverse() * new Transform3D(Basis.Identity, at) };
        Attach(s, bone).AddChild(node);
        return node;
    }

    static BoneAttachment3D Attach(Skeleton3D s, string bone)
    {
        var attach = new BoneAttachment3D { BoneName = bone };
        s.AddChild(attach);
        return attach;
    }

    static Transform3D Rest(Skeleton3D s, string bone) => s.FindBone(bone) is var i and >= 0 ? s.GetBoneGlobalRest(i) : Transform3D.Identity;

    /// <summary>A horn or spike: a dark cone from a point along a direction.</summary>
    static void Spike(Skeleton3D s, string bone, Vector3 root, Vector3 dir, float length, float radius, Material? material = null)
    {
        var up = dir.Normalized();
        var side = up.Cross(Mathf.Abs(up.Y) < 0.95f ? Vector3.Up : Vector3.Right).Normalized();
        var basis = new Basis(side, up, side.Cross(up));
        Grow(s, bone, new Transform3D(basis, root + up * length / 2),
            new CylinderMesh { TopRadius = 0, BottomRadius = radius, Height = length, RadialSegments = 6, Rings = 1 }, material ?? Char);
    }

    static CpuParticles3D Flames(float radius, int amount, float height)
    {
        var fade = new Gradient
        {
            Offsets = [0, 0.25f, 0.6f, 1],
            Colors = [new Color(1, 0.95f, 0.6f, 0.9f), new Color(1, 0.65f, 0.15f, 0.85f), new Color(0.9f, 0.25f, 0.05f, 0.5f), new Color(0.3f, 0.05f, 0, 0)],
        };
        return new CpuParticles3D
        {
            Mesh = new QuadMesh
            {
                Size = Vector2.One * radius * 2.2f,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                },
            },
            Amount = amount, Lifetime = 0.9, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius,
            Direction = Vector3.Up, Spread = 20, Gravity = new Vector3(0, height, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f,
            ScaleAmountMin = 0.8f, ScaleAmountMax = 1.8f, ColorRamp = fade, LocalCoords = false,
        };
    }

    static CpuParticles3D Embers(float radius)
    {
        var fade = new Gradient { Offsets = [0, 1], Colors = [new Color(1, 0.7f, 0.2f, 1), new Color(1, 0.3f, 0, 0)] };
        return new CpuParticles3D
        {
            Mesh = new QuadMesh
            {
                Size = Vector2.One * radius * 0.25f,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                },
            },
            Amount = 16, Lifetime = 1.5, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius,
            Direction = Vector3.Up, Spread = 60, Gravity = new Vector3(0, 0.6f, 0), InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f,
            ColorRamp = fade, LocalCoords = false,
        };
    }
}
