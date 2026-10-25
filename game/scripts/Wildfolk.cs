using Godot;

namespace Tristinwold.Game;

/// <summary>
/// The stranger things of the wilds, built from the creature models and grown with what makes each one itself
/// (see <see cref="Graft"/>): the kipine, an owlbear gone to the sky, with an owl's pale mask for a face, a stag's
/// antlers and great leathery wings; deathcrest apes, pale and banded dark, ridged horns sweeping back, a blue snout
/// and long black claws; dire imps, red-brown devils with swept horns, bat wings, a barbed tail and burning blue
/// eyes, and the little crimson imps at their heels; and winged apes, black-furred, with black feathered wings and
/// a greatsword.
/// </summary>
public static class Wildfolk
{
    public static Body? For(string look) => look switch
    {
        "kipine" => Kipine(),
        "deathcrest_ape" => DeathcrestApe(),
        "dire_imp" => Imp(1.8f, "direimp", dire: true),
        "imp" => Imp(0.7f, "imp", dire: false),
        "winged_ape" => WingedApe(),
        "raven" => (Body?)Creature.Create("hawk.glb", 0.45f, new Color("#1a1a20")),
        _ => null,
    };

    static readonly StandardMaterial3D Antler = Lairfolk.Plain("#cfc6b4", 0.6f);
    static readonly StandardMaterial3D Horn = Lairfolk.Plain("#3a2a1e", 0.5f);
    static readonly StandardMaterial3D HornBand = Lairfolk.Plain("#a8946a", 0.55f);
    static readonly StandardMaterial3D Claw = Lairfolk.Plain("#1a1614", 0.4f);
    static readonly StandardMaterial3D Mask = new() { AlbedoColor = new Color("#e8e2d6"), Roughness = 0.5f, RimEnabled = true, Rim = 0.3f };
    static readonly StandardMaterial3D Eye = new() { AlbedoColor = new Color("#d8c070"), Roughness = 0.1f, MetallicSpecular = 1, EmissionEnabled = true, Emission = new Color("#806a20"), EmissionEnergyMultiplier = 0.4f };
    static readonly StandardMaterial3D Pupil = new() { AlbedoColor = new Color("#050505"), Roughness = 0.05f, MetallicSpecular = 1 };
    static readonly StandardMaterial3D Steel = Lairfolk.Plain("#b8c0c8", 0.25f, 0.9f);
    static readonly StandardMaterial3D Leather = Lairfolk.Plain("#5a3420", 0.8f);

    /// <summary>
    /// The troll model, stood on its feet. Its skinned mesh measures taller than it stands, so going by its box
    /// leaves it waist-deep in the ground; its foot bones say where the ground really is.
    /// </summary>
    public static Creature? Troll(float height, Color? tint = null)
    {
        if (Creature.Create("troll.glb", height, tint) is not { } c || Graft.On(c) is not { } g) return null;
        var feet = new[] { "foot_tip.L", "foot_tip.R", "foot.L", "foot.R" }.Where(g.Has).Select(b => g.Bone(b).Y).DefaultIfEmpty(0).Min();
        if (feet < -0.05f && c.GetChildren().OfType<Node3D>().FirstOrDefault() is { } model) model.Position += Vector3.Up * -feet;
        return c;
    }

    /// <summary>A bone that's highest in the body of the ones named (the troll's spine bones have no telling names).</summary>
    static string Highest(Graft g, params string[] bones) => bones.Where(g.Has).MaxBy(b => g.Bone(b).Y) ?? bones[0];

    /// <summary>Swaps one of a model's textures for a coat of our own (the troll's skin, not its loincloth or eyes).</summary>
    static void Retexture(Node model, string was, string coat)
    {
        var texture = GD.Load<Texture2D>($"res://assets/creatures/{coat}");
        Lairfolk.Recoat(model, m => { if (m.AlbedoTexture?.ResourcePath.Contains(was) == true) m.AlbedoTexture = texture; });
    }

    /// <summary>A horn, antler beam or tail: a chain of tapering segments bending a little each step.</summary>
    static Vector3 Curve(Graft g, string bone, Vector3 root, Vector3 dir, Vector3 bend, float length, float radius, int steps, Func<int, Material> material)
    {
        var at = root;
        var d = dir.Normalized();
        for (var i = 0; i < steps; i++)
        {
            var seg = length / steps;
            var r0 = radius * (1 - i / (float)steps);
            var r1 = radius * (1 - (i + 1) / (float)steps);
            g.Add(bone, at + d * seg / 2, new CylinderMesh { TopRadius = Mathf.Max(r1, radius * 0.04f), BottomRadius = r0, Height = seg * 1.05f, RadialSegments = 7, Rings = 1 },
                material(i), Graft.Along(d));
            at += d * seg;
            d = (d + bend / steps).Normalized();
        }
        return at;
    }

    // ---- the kipine -----------------------------------------------------------------------------

    static Body? Kipine()
    {
        if (Creature.Create("bear.glb", 2.4f, coat: "bear_kipine.png") is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var head = g.Has("Ursidae_Head") ? "Ursidae_Head" : "Ursidae_Neck";
        var h = g.Bone(head);
        // The owl's mask over the bear's face: a pale flat disc just behind the tip of the snout, two great
        // moonlike eyes, a hooked beak.
        var face = new Vector3(0, h.Y + t * 0.02f, g.Box.Position.Z + t * 0.07f);
        g.Add(head, face, new SphereMesh { Radius = t * 0.12f, Height = t * 0.24f, RadialSegments = 16, Rings = 8 }, Mask, Basis.Identity.Scaled(new Vector3(1, 1.1f, 0.45f)));
        foreach (var side in new[] { -1f, 1f })
        {
            var eye = face + new Vector3(side * t * 0.05f, t * 0.025f, -t * 0.045f);
            g.Add(head, eye, new SphereMesh { Radius = t * 0.03f, Height = t * 0.06f }, Eye);
            g.Add(head, eye + Vector3.Forward * t * 0.02f, new SphereMesh { Radius = t * 0.016f, Height = t * 0.032f }, Pupil);
            // Antlers: a beam from the crown sweeping up, out and back, tines off its front.
            var root = new Vector3(side * t * 0.07f, h.Y + t * 0.13f, face.Z + t * 0.12f);
            var tip = Curve(g, head, root, new Vector3(side * 0.45f, 1, 0.2f), new Vector3(side * 0.7f, -0.3f, 0.8f), t * 0.55f, t * 0.026f, 6, _ => Antler);
            for (var k = 1; k <= 3; k++)
                Curve(g, head, root.Lerp(tip, k / 4f), new Vector3(side * 0.15f, 1, -0.4f), new Vector3(0, 0.2f, 0.4f), t * (0.2f - k * 0.03f), t * 0.013f, 3, _ => Antler);
        }
        g.Spike(head, face + new Vector3(0, -t * 0.035f, -t * 0.05f), new Vector3(0, -0.7f, -1), t * 0.06f, t * 0.018f, Claw, 5);
        // A ruff of feathers round the mask.
        for (var k = 0; k < 10; k++)
        {
            var a = k * Mathf.Tau / 10;
            g.Add(head, face + new Vector3(Mathf.Cos(a) * t * 0.13f, Mathf.Sin(a) * t * 0.14f, t * 0.04f), new SphereMesh { Radius = t * 0.05f, Height = t * 0.05f },
                Lairfolk.Plain("#4a3c34", 0.9f), new Basis(Vector3.Forward, a).Scaled(new Vector3(1.4f, 0.6f, 0.7f)));
        }
        // Great leathery wings, smoke-grey, from its shoulders.
        var shoulders = Highest(g, "Ursidae_Spine_3", "Ursidae_Spine_2");
        FlapWings.Grow(g, shoulders, g.Bone(shoulders) + Vector3.Up * t * 0.14f, t * 0.12f, t * 0.85f, feathered: false, new Color("#7a726c"), new Color("#2a2420"));
        return c;
    }

    // ---- deathcrest apes ------------------------------------------------------------------------

    static Body? DeathcrestApe()
    {
        if (Troll(1.9f) is not { } c || Graft.On(c) is not { } g) return null;
        Retexture(c, "troll_baseTex", "troll_deathcrest.png");
        var t = 1.9f; // its own height (the box measures wrong: see Troll)
        var head = Highest(g, "Bone.003", "Bone.002", "Bone.001", "Bone");
        var h = g.Bone(head);
        // Ridged horns sweeping back, banded light and dark.
        foreach (var side in new[] { -1f, 1f })
            Curve(g, head, h + new Vector3(side * t * 0.06f, t * 0.1f, -t * 0.02f), new Vector3(side * 0.6f, 1, 0.6f), new Vector3(side * 0.3f, -0.6f, 1.2f),
                t * 0.34f, t * 0.03f, 7, i => i % 2 == 0 ? Horn : HornBand);
        // The blue snout.
        g.Add(head, h + new Vector3(0, t * 0.02f, -t * 0.1f), new SphereMesh { Radius = t * 0.045f, Height = t * 0.07f }, Lairfolk.Plain("#3a74b0", 0.4f),
            Basis.Identity.Scaled(new Vector3(1.2f, 0.8f, 0.7f)));
        // Long black claws.
        foreach (var hand in new[] { "hand.L", "hand.R" }.Where(g.Has))
        {
            var at = g.Bone(hand);
            var reach = (at - g.Bone(hand.Replace("hand", "forearm"))).Normalized();
            for (var k = 0; k < 3; k++)
                g.Spike(hand, at + reach * t * 0.05f + Vector3.Forward * (k - 1) * t * 0.025f, reach + Vector3.Down * 0.6f + Vector3.Forward * (k - 1) * 0.2f, t * 0.12f, t * 0.012f, Claw, 5);
        }
        return c;
    }

    // ---- imps -----------------------------------------------------------------------------------

    static Body? Imp(float height, string coat, bool dire)
    {
        if (Creature.Create("goblin.glb", height, coat: $"goblin_{coat}.png", glow: $"goblin_{coat}_glow.png") is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var head = g.Has("head") ? "head" : "neck";
        var h = g.Bone(head);
        var horn = dire ? Horn : Lairfolk.Plain("#2a0a10", 0.5f);
        // Swept-back horns, long on a dire imp.
        foreach (var side in new[] { -1f, 1f })
            Curve(g, head, h + new Vector3(side * t * 0.05f, t * 0.09f, 0), new Vector3(side * 0.25f, 1, 0.3f), new Vector3(side * 0.1f, -0.3f, 0.9f),
                t * (dire ? 0.3f : 0.14f), t * (dire ? 0.022f : 0.028f), 6, _ => horn);
        // Bat wings and a barbed tail.
        var back = g.Has("spine3") ? "spine3" : "spine2";
        FlapWings.Grow(g, back, g.Bone(back) + new Vector3(0, t * 0.04f, t * 0.06f), t * 0.06f, t * (dire ? 0.75f : 0.65f), feathered: false,
            dire ? new Color("#b8481e") : new Color("#9a1c30"), dire ? new Color("#3a140a") : new Color("#3a0810"), beat: dire ? 1.6f : 5);
        var hips = g.Has("spine1") ? "spine1" : "master";
        var tip = Curve(g, hips, g.Bone(hips) + new Vector3(0, -t * 0.02f, t * 0.06f), new Vector3(0, -0.6f, 1), new Vector3(0.2f, dire ? 0.4f : 1.6f, 0.6f),
            t * (dire ? 0.6f : 0.5f), t * 0.02f, 8, _ => horn);
        g.Spike(hips, tip, dire ? new Vector3(0.1f, 0.2f, 1) : new Vector3(0, 1, -0.4f), t * 0.06f, t * 0.03f, horn, 4);
        c.AddChild(Lairfolk.Placed(Lairfolk.Light(dire ? "#ff7a3a" : "#ff4a5a", dire ? 0.8f : 0.3f, dire ? 4 : 2), new Vector3(0, t * 0.8f, 0)));
        return c;
    }

    // ---- winged apes ----------------------------------------------------------------------------

    static Body? WingedApe()
    {
        if (Troll(2.0f) is not { } c || Graft.On(c) is not { } g) return null;
        Retexture(c, "troll_baseTex", "troll_wingedape.png");
        var t = 2.0f;
        var chest = Highest(g, "Bone.002", "Bone.001", "Bone");
        FlapWings.Grow(g, chest, g.Bone(chest) + new Vector3(0, t * 0.05f, t * 0.08f), t * 0.1f, t * 1.0f, feathered: true, new Color("#141414"), new Color("#0a0a0a"));
        // A greatsword in its fist, and a harness strap across its chest.
        if (g.Has("hand.R"))
        {
            var at = g.Bone("hand.R");
            var along = (at - g.Bone("forearm.R")).Normalized();
            var up = (along * 0.2f + Vector3.Up).Normalized();
            var holder = g.Hold("hand.R", at, Graft.Along(up));
            holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.75f, 0.012f) * t }, MaterialOverride = Steel, Position = Vector3.Up * t * 0.45f });
            holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.02f, 0.03f) * t }, MaterialOverride = Lairfolk.Plain("#a88a3a", 0.3f, 0.9f), Position = Vector3.Up * t * 0.07f });
            holder.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.013f * t, BottomRadius = 0.013f * t, Height = 0.14f * t }, MaterialOverride = Leather });
        }
        g.Add(chest, g.Bone(chest) + Vector3.Down * t * 0.04f, new TorusMesh { InnerRadius = t * 0.15f, OuterRadius = t * 0.17f, Rings = 16, RingSegments = 4 }, Leather,
            new Basis(Vector3.Forward, 0.7f));
        return c;
    }
}

/// <summary>
/// A pair of wings for a creature that has none in its model, riding on a back bone: leathery bat wings (an arm,
/// fingers fanning from the wrist, a membrane between them) or feathered ones (long dark primaries fanned from the
/// arm). They beat slowly as it stands, and hard when it takes to the air (see <see cref="Flying"/>).
/// </summary>
public partial class FlapWings : Node3D
{
    readonly List<(Node3D Pivot, float Side)> _wings = [];
    float _phase, _beat = 1.2f, _rate = 1.2f, _open;

    /// <summary>Beating hard to fly (a kipine's dive, a winged ape's leap).</summary>
    public bool Flying { get; set; }

    public static FlapWings Grow(Graft g, string bone, Vector3 between, float apart, float span, bool feathered, Color skin, Color bones, float beat = 1.2f)
    {
        var w = new FlapWings { _rate = beat };
        var holder = g.Hold(bone, between);
        holder.AddChild(w);
        var membrane = new StandardMaterial3D
        {
            AlbedoColor = skin, Roughness = 0.75f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = !feathered,
            Backlight = skin * 0.35f,
        };
        var bone3 = new StandardMaterial3D { AlbedoColor = bones, Roughness = 0.6f };
        foreach (var side in new[] { -1f, 1f })
        {
            var pivot = new Node3D { Position = new Vector3(side * apart, 0, 0) };
            w.AddChild(pivot);
            var wing = new Node3D { Scale = new Vector3(side, 1, 1) }; // the left wing is the right one mirrored
            pivot.AddChild(wing);
            if (feathered) Feathers(wing, span, membrane, bone3);
            else Membrane(wing, span, membrane, bone3);
            w._wings.Add((pivot, side));
        }
        return w;
    }

    /// <summary>A bat's wing, reaching out along +X and back along +Z (the body's back).</summary>
    static void Membrane(Node3D wing, float s, Material skin, Material bone)
    {
        var shoulder = Vector3.Zero;
        var wrist = new Vector3(0.45f, 0.22f, 0.05f) * s;
        Vector3[] tips = [new Vector3(1f, 0.12f, 0.2f) * s, new Vector3(0.88f, -0.2f, 0.42f) * s, new Vector3(0.62f, -0.38f, 0.55f) * s, new Vector3(0.32f, -0.42f, 0.5f) * s];
        var flank = new Vector3(0.05f, -0.3f, 0.35f) * s;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Tri(Vector3 a, Vector3 b, Vector3 c) { st.AddVertex(a); st.AddVertex(b); st.AddVertex(c); }
        Tri(shoulder, wrist, tips[0]);
        for (var i = 0; i < tips.Length - 1; i++) Tri(wrist, tips[i], tips[i + 1]);
        Tri(wrist, tips[^1], flank);
        Tri(shoulder, wrist, flank);
        st.GenerateNormals();
        wing.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = skin });
        Rod(wing, shoulder, wrist, s * 0.03f, bone);
        foreach (var tip in tips) Rod(wing, wrist, tip, s * 0.012f, bone);
    }

    /// <summary>A bird's wing: the arm, and long feathers fanned back from it, longest at the tip.</summary>
    static void Feathers(Node3D wing, float s, Material feather, Material bone)
    {
        Rod(wing, Vector3.Zero, new Vector3(0.55f, 0.15f, 0) * s, s * 0.04f, bone);
        // Flight feathers, each a long flat taper, overlapping; then a row of short coverts over their roots.
        var quill = new SphereMesh { Radius = 0.5f, Height = 1, RadialSegments = 10, Rings = 5 };
        foreach (var (count, reach, scale) in new[] { (18, 1f, 1f), (12, 0.45f, 0.55f) })
            for (var i = 0; i < count; i++)
            {
                var f = i / (count - 1f);
                var root = new Vector3(0.04f + f * 0.56f, 0.15f * f + (scale < 1 ? 0.01f : 0), 0) * s;
                var length = s * (0.3f + f * 0.5f) * reach;
                var dir = new Vector3(f * 0.75f, -0.3f + f * 0.15f, 1 - f * 0.5f).Normalized();
                var basis = Basis.LookingAt(dir, Vector3.Up).Scaled(new Vector3(s * 0.085f * scale, s * 0.012f, length));
                wing.AddChild(new MeshInstance3D { Mesh = quill, MaterialOverride = feather, Transform = new Transform3D(basis, root + dir * length * 0.45f) });
            }
    }

    static void Rod(Node3D into, Vector3 a, Vector3 b, float radius, Material m) => into.AddChild(new MeshInstance3D
    {
        Mesh = new CylinderMesh { TopRadius = radius * 0.6f, BottomRadius = radius, Height = a.DistanceTo(b), RadialSegments = 5, Rings = 1 },
        MaterialOverride = m, Transform = new Transform3D(Graft.Along(b - a), (a + b) / 2),
    });

    public override void _Process(double delta)
    {
        var rate = Flying ? _rate * 2.6f : _rate;
        _beat = Mathf.MoveToward(_beat, rate, (float)delta * 3);
        _phase += (float)delta * _beat * Mathf.Tau;
        // Folded back along the body at rest, stirring now and then; flung wide and beating hard in the air.
        _open = Mathf.MoveToward(_open, Flying ? 1 : 0, (float)delta * 2.5f);
        var flap = Mathf.Sin(_phase) * Mathf.Lerp(0.08f, 0.75f, _open);
        foreach (var (pivot, side) in _wings)
            pivot.Rotation = new Vector3(0, -side * (1 - _open) * 1.35f, side * (flap + Mathf.Lerp(0.45f, 0.15f, _open))); // the arm swung back along the body
    }
}
