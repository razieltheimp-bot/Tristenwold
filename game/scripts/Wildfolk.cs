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
        "sky_fox" => SkyFox(),
        "pegasus" => Pegasus(),
        "griffon" => Griffon(),
        "jackalope" => Jackalope(),
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
        if (feet < -0.05f && c.GetChildren().OfType<Node3D>().FirstOrDefault() is { } model)
        {
            model.Position += Vector3.Up * -feet;
            c.Footing?.Rebase(); // the footing stands it from here, not from where it was
        }
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

    static readonly StandardMaterial3D Disc = new() { AlbedoColor = new Color("#e6d9c2"), Roughness = 0.85f };
    /// <summary>The kipine's own dark fur (its coat's), for blending the owl into the bear.</summary>
    static readonly Color Fur = new("#33261f");

    static SphereMesh Ball() => new() { Radius = 1, Height = 2, RadialSegments = 40, Rings = 20 };

    /// <summary>A soft blend from one colour to another across a shape (towards <paramref name="toward"/> it's all <paramref name="front"/>), matte as feathers.</summary>
    static ShaderMaterial Blend(Color front, Color back, Vector3 toward)
    {
        var m = new ShaderMaterial { Shader = BlendShader };
        m.SetShaderParameter("front", front);
        m.SetShaderParameter("back", back);
        m.SetShaderParameter("axis", toward.Normalized());
        return m;
    }

    static readonly Shader BlendShader = new()
    {
        Code = """
            shader_type spatial;
            uniform vec4 front : source_color = vec4(1.0);
            uniform vec4 back : source_color = vec4(0.0);
            uniform vec3 axis = vec3(0.0, 0.0, -1.0);
            varying float f;
            varying vec3 p;
            void vertex() { f = smoothstep(-0.25, 0.95, dot(normalize(VERTEX), axis)); p = VERTEX; }
            void fragment() {
                // A faint feathering: fine streaks running back over the shape.
                float streak = 0.97 + 0.03 * sin(p.x * 40.0 + sin(p.y * 17.0) * 2.0);
                ALBEDO = mix(back.rgb, front.rgb, f) * streak;
                ROUGHNESS = 0.9;
                RIM = 0.35;
                RIM_TINT = 0.6;
            }
            """,
    };
    static readonly StandardMaterial3D DiscRim = Lairfolk.Plain("#5a4232", 0.9f);
    static readonly StandardMaterial3D Beak = new() { AlbedoColor = new Color("#c8a85a"), Roughness = 0.35f, MetallicSpecular = 0.6f };
    static readonly StandardMaterial3D Tawny = Lairfolk.Plain("#8a6a48", 0.9f);
    static readonly StandardMaterial3D Speckle = Lairfolk.Plain("#c8b08a", 0.9f);
    static readonly StandardMaterial3D AntlerBase = Lairfolk.Plain("#7a6248", 0.7f);
    static readonly StandardMaterial3D AntlerTip = Lairfolk.Plain("#e4d8bc", 0.5f);

    /// <summary>
    /// A bear's body with an owl's head on it, a stag's antlers and an owl's great wings: the facial disc in two pale
    /// hearts edged dark round huge amber eyes, a hooked beak between them, ear tufts, branching antlers dark at the root
    /// and pale at the points, a breast of speckled feathers, and tawny barred wings.
    /// </summary>
    static Body? Kipine()
    {
        if (Creature.Create("bear.glb", 2.4f, coat: "bear_kipine.png") is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var head = g.Has("Ursidae_Head") ? "Ursidae_Head" : "Ursidae_Neck";
        var h = g.Bone(head);
        // The owl's head, one smooth feathered dome over the bear's: tawny at the face, darkening back into the bear's own
        // fur, and a soft feathered neck carrying the same shading back into its shoulders, so the two are one creature.
        var front = g.Box.Position.Z;
        var R = Mathf.Max(t * 0.135f, Mathf.Abs(front - h.Z) * 0.56f);
        var centre = new Vector3(0, h.Y + t * 0.035f, front + R * 0.96f);
        var face = centre + Vector3.Forward * R * 0.9f;
        g.Add(head, centre + Vector3.Back * R * 0.15f, Ball(), Blend(new Color("#8a6a48"), Fur, new Vector3(0, 0.1f, -1)), Basis.FromScale(new Vector3(0.92f, 0.9f, 1.18f) * R));
        var neckBone = g.Has("Ursidae_Neck") ? "Ursidae_Neck" : head;
        var n = g.Bone(neckBone);
        var toBody = n - centre;
        if (toBody.Length() > R * 0.3f)
            g.Add(neckBone, centre.Lerp(n, 0.55f), Ball(), Blend(new Color("#6a5038"), Fur, new Vector3(0, -1, 0)),
                Graft.Along(toBody) * Basis.FromScale(new Vector3(R * 0.95f, toBody.Length() * 0.75f, R * 0.9f)));
        // The facial disc, set into the front of the head: a pale heart round each eye, rimmed a little darker, its edge
        // sunk into the feathers rather than standing off them.
        foreach (var side in new[] { -1f, 1f })
        {
            var lobe = centre + new Vector3(side * R * 0.42f, R * 0.04f, -R * 0.78f);
            var turn = new Basis(Vector3.Up, -side * 0.42f) * new Basis(Vector3.Forward, side * 0.2f);
            g.Add(head, lobe + Vector3.Back * R * 0.06f, Ball(), Blend(DiscRim.AlbedoColor, new Color("#8a6a48"), new Vector3(0, 0, -1)), turn * Basis.FromScale(new Vector3(0.52f, 0.64f, 0.15f) * R));
            g.Add(head, lobe + Vector3.Back * R * 0.02f, Ball(), Blend(new Color("#cdb896"), new Color("#9a7a56"), new Vector3(0, 0, -1)), turn * Basis.FromScale(new Vector3(0.46f, 0.57f, 0.14f) * R));
            // Great amber eyes, sunk in the disc, black at the middle.
            var eye = lobe + new Vector3(side * R * 0.02f, R * 0.07f, -R * 0.07f);
            g.Add(head, eye, new SphereMesh { Radius = R * 0.16f, Height = R * 0.32f, RadialSegments = 28, Rings = 14 }, Eye, Basis.FromScale(new Vector3(1, 1, 0.6f)));
            g.Add(head, eye + Vector3.Forward * R * 0.085f, new SphereMesh { Radius = R * 0.085f, Height = R * 0.17f, RadialSegments = 20, Rings = 10 }, Pupil, Basis.FromScale(new Vector3(1, 1, 0.5f)));
            // Ear tufts over the brow, soft and curving out.
            Smooth(g, head, centre + new Vector3(side * R * 0.5f, R * 0.72f, -R * 0.35f), new Vector3(side * 0.35f, 1, 0.25f), R * 0.65f, R * 0.14f, new Vector3(side * 0.5f, -0.3f, 0.4f), 0.05f, Tawny);
            // Antlers from the crown: a smooth beam sweeping up, out and back, a dark burr at its root, three tines forward off it.
            var root = centre + new Vector3(side * R * 0.4f, R * 0.82f, R * 0.12f);
            g.Add(head, root + Vector3.Up * t * 0.01f, new TorusMesh { InnerRadius = t * 0.026f, OuterRadius = t * 0.046f, Rings = 18, RingSegments = 8 }, AntlerBase, Graft.Along(new Vector3(side * 0.5f, 1, 0.25f)));
            var beam = Smooth(g, head, root, new Vector3(side * 0.5f, 1, 0.25f), t * 0.6f, t * 0.036f, new Vector3(side * 0.6f, -0.35f, 0.75f), 0.25f, AntlerTip);
            for (var k = 1; k <= 3; k++)
                Smooth(g, head, beam.Path[(int)(beam.Path.Length * k / 4.2f)], new Vector3(side * 0.2f, 1, -0.45f), t * (0.24f - k * 0.035f), t * 0.016f, new Vector3(side * 0.1f, 0.25f, 0.35f), 0.15f, AntlerTip);
        }
        // The hooked beak, from between the hearts of the disc, curving down, on a soft pale cere.
        var nose = centre + new Vector3(0, -R * 0.08f, -R * 0.9f);
        g.Add(head, nose, Ball(), Disc, Basis.FromScale(new Vector3(0.17f, 0.15f, 0.14f) * R));
        Smooth(g, head, nose + Vector3.Forward * R * 0.05f, new Vector3(0, -0.15f, -1), R * 0.62f, R * 0.15f, new Vector3(0, -2.0f, 0.5f), 0.05f, Beak, flat: 0.85f);
        // A breast of speckled down over its chest.
        var chest = Highest(g, "Ursidae_Spine_3", "Ursidae_Spine_2", "Ursidae_Neck");
        var breast = g.Bone(chest);
        g.Add(chest, breast + new Vector3(0, -t * 0.09f, face.Z - breast.Z > 0 ? t * 0.1f : -t * 0.12f), new SphereMesh { Radius = t * 0.13f, Height = t * 0.26f, RadialSegments = 32, Rings = 16 }, Speckle,
            Basis.Identity.Scaled(new Vector3(1.15f, 1.35f, 0.4f)));
        // An owl's great wings from its shoulders, tawny and barred dark.
        var shoulders = Highest(g, "Ursidae_Spine_3", "Ursidae_Spine_2");
        FlapWings.Grow(g, shoulders, g.Bone(shoulders) + Vector3.Up * t * 0.14f, t * 0.12f, t * 0.9f, feathered: true, new Color("#8a6a48"), new Color("#4a3628"), bars: new Color("#4a3426"));
        return c;
    }

    /// <summary>A smooth horn, antler beam, beak or tuft on a bone (see <see cref="Tails.Tube"/>): built in the body, riding the bone.</summary>
    static Tails.Built Smooth(Graft g, string bone, Vector3 root, Vector3 way, float length, float thick, Vector3 bend, float tip, Material material, float flat = 1)
    {
        var built = Tails.Tube(root, way, length, thick, bend, tip, 0.8f, material, 12, 12, flat);
        g.Hold(bone, Vector3.Zero).AddChild(built.Mesh);
        return built;
    }

    // ---- crossings: winged and antlered beasts ---------------------------------------------------

    static readonly StandardMaterial3D GoldBeak = new() { AlbedoColor = new Color("#e2b23c"), Roughness = 0.3f, MetallicSpecular = 0.7f, RimEnabled = true, Rim = 0.2f };
    static readonly StandardMaterial3D WhiteFeather = new() { AlbedoColor = new Color("#f2eee4"), Roughness = 0.8f, RimEnabled = true, Rim = 0.25f, RimTint = 0.4f };

    /// <summary>A red fox with an eagle's wings: brown and barred pale, folded along its back, and it flies.</summary>
    static Body? SkyFox()
    {
        if (Creature.Create("fox.glb", 0.55f) is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var back = g.Has("Fox_spine2") ? "Fox_spine2" : "Fox_spine1";
        FlapWings.Grow(g, back, g.Bone(back) + Vector3.Up * t * 0.16f, t * 0.12f, t * 1.5f, feathered: true, new Color("#7a5634"), new Color("#3a2a1c"), beat: 2.4f, bars: new Color("#e8dcc4"));
        return c;
    }

    /// <summary>A white horse with a swan's great wings, white to the tips: wild, it flies from you; grown at your side, it carries you.</summary>
    static Body? Pegasus()
    {
        if (Creature.Create("horse.glb", 1.8f, new Color("#f6f4ee")) is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var shoulders = g.Has("Shoulders") ? "Shoulders" : g.Has("Torso") ? "Torso" : "Body";
        FlapWings.Grow(g, shoulders, g.Bone(shoulders) + Vector3.Up * t * 0.1f, t * 0.1f, t * 1.15f, feathered: true, new Color("#f4f2ec"), new Color("#d8d2c4"), beat: 1.0f, bars: new Color("#e4ded2"));
        return c;
    }

    /// <summary>
    /// A griffon: a lion's body with an eagle's head, white-feathered with a hooked gold beak and fierce amber eyes, a
    /// collar of feathers where the eagle meets the lion, tufted ears, and great golden-brown wings.
    /// </summary>
    static Body? Griffon()
    {
        if (Creature.Create("lioness.glb", 1.5f, new Color("#e8c890")) is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var head = g.Has("Horse_Head") ? "Horse_Head" : "Horse_Neck1";
        var h = g.Bone(head);
        var snout = g.Box.Position.Z;
        var size = Mathf.Max(t * 0.19f, Mathf.Abs(snout - h.Z) * 0.55f);
        var centre = new Vector3(0, h.Y + t * 0.04f, Mathf.Min(h.Z - size * 0.45f, snout + size * 1.05f));
        // The eagle's head, over the lion's: white feathers, smooth.
        g.Add(head, centre, new SphereMesh { Radius = size, Height = size * 2, RadialSegments = 36, Rings = 18 }, WhiteFeather, Basis.Identity.Scaled(new Vector3(0.82f, 0.95f, 1.15f)));
        // The beak, hooked and gold, out of the front of it.
        var front = centre + new Vector3(0, -size * 0.1f, -size * 1.05f);
        Smooth(g, head, front + new Vector3(0, 0, size * 0.15f), new Vector3(0, -0.1f, -1), size * 0.95f, size * 0.42f, new Vector3(0, -2.4f, 0.4f), 0.04f, GoldBeak, flat: 0.8f);
        // Amber eyes under a hard brow.
        foreach (var side in new[] { -1f, 1f })
        {
            var eye = centre + new Vector3(side * size * 0.66f, size * 0.22f, -size * 0.55f);
            g.Add(head, eye, new SphereMesh { Radius = size * 0.17f, Height = size * 0.34f, RadialSegments = 20, Rings = 10 }, Eye);
            g.Add(head, eye + new Vector3(side * size * 0.06f, 0, -size * 0.1f), new SphereMesh { Radius = size * 0.09f, Height = size * 0.18f, RadialSegments = 16, Rings = 8 }, Pupil);
            g.Add(head, eye + new Vector3(0, size * 0.16f, -size * 0.02f), new SphereMesh { Radius = size * 0.2f, Height = size * 0.4f, RadialSegments = 16, Rings = 8 }, WhiteFeather, Basis.Identity.Scaled(new Vector3(1.1f, 0.4f, 1.2f)));
            // Tufted ears, swept back.
            Smooth(g, head, centre + new Vector3(side * size * 0.5f, size * 0.75f, size * 0.25f), new Vector3(side * 0.3f, 0.8f, 1), size * 0.7f, size * 0.16f, new Vector3(side * 0.2f, 0.4f, 0.3f), 0.05f, WhiteFeather);
        }
        // A collar of feathers where the eagle meets the lion.
        var neck = g.Has("Horse_Neck") ? "Horse_Neck" : head;
        var n = g.Bone(neck);
        g.Add(neck, n.Lerp(centre, 0.35f), new TorusMesh { InnerRadius = size * 0.65f, OuterRadius = size * 1.15f, Rings = 40, RingSegments = 14 }, WhiteFeather,
            Graft.Along(centre - n).Scaled(new Vector3(1, 0.7f, 1)));
        var back = g.Has("Horse_Spine1") ? "Horse_Spine1" : "Horse_Spine";
        FlapWings.Grow(g, back, g.Bone(back) + Vector3.Up * t * 0.12f, t * 0.1f, t * 1.3f, feathered: true, new Color("#a8783c"), new Color("#5a3a1c"), beat: 1.1f, bars: new Color("#ecdcb4"));
        return c;
    }

    /// <summary>A hare with a little stag's antlers, two tines each, between its ears.</summary>
    static Body? Jackalope()
    {
        if (Creature.Create("rabbit.glb", 0.42f) is not { } c || Graft.On(c) is not { } g) return null;
        var t = g.Tall;
        var head = g.Has("Deer01_Head") ? "Deer01_Head" : "Deer01_Neck1";
        var h = g.Bone(head);
        var snout = g.Box.Position.Z;
        foreach (var side in new[] { -1f, 1f })
        {
            var root = new Vector3(side * t * 0.06f, h.Y + t * 0.14f, h.Z + (snout - h.Z) * 0.3f);
            var beam = Smooth(g, head, root, new Vector3(side * 0.45f, 1, 0.25f), t * 0.75f, t * 0.05f, new Vector3(side * 0.4f, -0.3f, 0.6f), 0.2f, AntlerTip);
            Smooth(g, head, beam.Path[beam.Path.Length / 2], new Vector3(side * 0.1f, 1, -0.5f), t * 0.32f, t * 0.03f, new Vector3(0, 0.3f, 0.3f), 0.15f, AntlerTip);
        }
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
/// A pair of wings for a creature that has none in its model, riding on a back bone, built as real wings are: an arm
/// out to the wrist and a hand beyond it, folding at the wrist. A bird's (feathered) has rows of feathers, each a real
/// feather's shape: small coverts over the roots, the long secondaries off the arm, and the primaries off the hand that
/// splay into fingers at the tip. A bat's (leathery) has an arm, fingers fanning from the wrist and the skin between.
/// At rest they fold against its sides as a bird's do, the wrist forward and the flight feathers lying back along its
/// flank and over its tail; in the air (see <see cref="Flying"/>) they open wide and beat.
/// </summary>
public partial class FlapWings : Node3D
{
    sealed record Wing(Node3D Shoulder, Node3D Hand, float Side, List<(Node3D Pivot, float Open)> Primaries);
    readonly List<Wing> _wings = [];
    float _phase, _beat = 1.2f, _rate = 1.2f, _open;

    /// <summary>Beating hard to fly (a kipine's dive, a winged ape's leap, any of them up in the air).</summary>
    public bool Flying { get; set; }

    /// <summary>Its creature can fly with these, as a dragon does (see <see cref="Creature.Winged"/>).</summary>
    public override void _Ready()
    {
        for (var n = GetParent(); n is not null; n = n.GetParent())
            if (n is Creature c) { c.Flaps ??= this; break; }
    }

    public static FlapWings Grow(Graft g, string bone, Vector3 between, float apart, float span, bool feathered, Color skin, Color bones, float beat = 1.2f, Color? bars = null)
    {
        var w = new FlapWings { _rate = beat };
        var holder = g.Hold(bone, between);
        holder.AddChild(w);
        StandardMaterial3D Mat(Color c, float rough = 0.85f) => new()
        {
            AlbedoColor = c, Roughness = rough, CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = !feathered, Backlight = c * 0.35f,
            RimEnabled = true, Rim = 0.25f, RimTint = 0.5f,
        };
        var paints = new Paints(Mat(skin), Mat(skin.Lightened(0.08f)), Mat(skin.Darkened(0.18f)), bars is { } dark ? Mat(dark) : null,
            new StandardMaterial3D { AlbedoColor = feathered ? skin.Darkened(0.12f) : bones, Roughness = 0.6f, RimEnabled = true, Rim = 0.2f });
        foreach (var side in new[] { -1f, 1f })
        {
            var shoulder = new Node3D { Position = new Vector3(side * apart, 0, 0) };
            w.AddChild(shoulder);
            var wing = new Node3D { Scale = new Vector3(side, 1, 1) }; // the left wing is the right one mirrored
            shoulder.AddChild(wing);
            var wrist = (feathered ? new Vector3(0.42f, 0.08f, 0.02f) : new Vector3(0.45f, 0.18f, 0.05f)) * span;
            var hand = new Node3D { Position = wrist };
            wing.AddChild(hand);
            var primaries = new List<(Node3D, float)>();
            if (feathered) Feathered(wing, hand, wrist, span, paints, primaries);
            else Leathery(wing, hand, wrist, span, paints);
            w._wings.Add(new Wing(shoulder, hand, side, primaries));
        }
        w.Pose(0);
        return w;
    }

    sealed record Paints(Material Feather, Material Covert, Material Flight, Material? Bar, Material Bone);

    /// <summary>A bird's wing: coverts, secondaries off the arm; primaries (each on its own pivot, to fan and close) off the hand.</summary>
    static void Feathered(Node3D arm, Node3D hand, Vector3 wrist, float s, Paints p, List<(Node3D, float)> primaries)
    {
        // The leading edge: a smooth, tapering arm to the wrist, then the hand.
        arm.AddChild(Tails.Tube(Vector3.Zero, wrist, wrist.Length(), s * 0.04f, Vector3.Zero, 0.6f, 1, p.Bone, 6, 10).Mesh);
        var handWay = new Vector3(1, -0.04f, 0.1f).Normalized();
        hand.AddChild(Tails.Tube(Vector3.Zero, handWay, s * 0.42f, s * 0.026f, Vector3.Zero, 0.4f, 1, p.Bone, 6, 8).Mesh);
        // Off the arm: the secondaries, long and broad, pointing back; over their roots two rows of coverts.
        for (var k = 0; k < 12; k++)
        {
            var f = k / 11f;
            var root = wrist * (0.04f + 0.94f * f);
            Feather(arm, root + Vector3.Down * s * 0.004f, new Vector3(0.1f * f, -0.04f, 1), s * (0.33f + 0.07f * f), s * 0.08f, k % 3 == 1 && p.Bar is { } bar ? bar : p.Flight);
        }
        for (var k = 0; k < 11; k++)
        {
            var f = k / 10f;
            Feather(arm, wrist * (0.03f + 0.95f * f) + Vector3.Up * s * 0.006f, new Vector3(0.12f * f, -0.03f, 1), s * 0.19f, s * 0.075f, k % 3 == 2 && p.Bar is { } bar ? bar : p.Feather);
            Feather(arm, wrist * (0.05f + 0.93f * f) + Vector3.Up * s * 0.012f, new Vector3(0.15f * f, -0.02f, 1), s * 0.1f, s * 0.07f, p.Covert);
        }
        // Off the hand: the primaries, fanning from pointing back to pointing out, longest past the middle, the outer
        // ones narrower so the tip splays into fingers.
        for (var k = 0; k < 10; k++)
        {
            var f = k / 9f;
            var pivot = new Node3D { Position = handWay * s * 0.42f * (0.04f + 0.92f * f) + Vector3.Down * s * 0.004f };
            hand.AddChild(pivot);
            Feather(pivot, Vector3.Zero, Vector3.Back, s * (0.36f + 0.26f * Mathf.Sin(Mathf.Pi * 0.85f * f)), s * 0.075f * (1 - 0.35f * f), k % 3 == 1 && p.Bar is { } bar ? bar : p.Flight);
            primaries.Add((pivot, Mathf.Lerp(0.18f, 1.3f, f)));
        }
        for (var k = 0; k < 8; k++)
        {
            var f = k / 7f;
            Feather(hand, handWay * s * 0.42f * (0.03f + 0.85f * f) + Vector3.Up * s * 0.008f, new Vector3(0.5f + 0.4f * f, -0.02f, 1 - 0.3f * f), s * 0.17f, s * 0.07f, p.Feather);
        }
    }

    /// <summary>A bat's wing: an arm to the wrist, fingers fanning from it, the skin stretched between them and back to the flank.</summary>
    static void Leathery(Node3D arm, Node3D hand, Vector3 wrist, float s, Paints p)
    {
        Vector3[] tips = [new Vector3(0.55f, -0.06f, 0.15f) * s, new Vector3(0.43f, -0.38f, 0.37f) * s, new Vector3(0.17f, -0.56f, 0.5f) * s, new Vector3(-0.13f, -0.6f, 0.45f) * s]; // from the wrist
        var flank = new Vector3(0.05f, -0.3f, 0.35f) * s;
        var inner = new SurfaceTool();
        inner.Begin(Mesh.PrimitiveType.Triangles);
        inner.AddVertex(Vector3.Zero); inner.AddVertex(wrist); inner.AddVertex(flank);
        inner.GenerateNormals();
        arm.AddChild(new MeshInstance3D { Mesh = inner.Commit(), MaterialOverride = p.Feather });
        var outer = new SurfaceTool();
        outer.Begin(Mesh.PrimitiveType.Triangles);
        // The skin between the fingers sags a little between their tips.
        for (var i = 0; i < tips.Length - 1; i++)
        {
            var sag = (tips[i] + tips[i + 1]) * 0.42f;
            outer.AddVertex(Vector3.Zero); outer.AddVertex(tips[i]); outer.AddVertex(sag);
            outer.AddVertex(Vector3.Zero); outer.AddVertex(sag); outer.AddVertex(tips[i + 1]);
        }
        outer.AddVertex(Vector3.Zero); outer.AddVertex(tips[^1]); outer.AddVertex(flank - wrist);
        outer.GenerateNormals();
        hand.AddChild(new MeshInstance3D { Mesh = outer.Commit(), MaterialOverride = p.Feather });
        arm.AddChild(Tails.Tube(Vector3.Zero, wrist, wrist.Length(), s * 0.035f, Vector3.Zero, 0.6f, 1, p.Bone, 6, 10).Mesh);
        foreach (var tip in tips)
            hand.AddChild(Tails.Tube(Vector3.Zero, tip, tip.Length(), s * 0.016f, Vector3.Zero, 0.25f, 1, p.Bone, 6, 8).Mesh);
        // A claw at the wrist.
        hand.AddChild(Tails.Tube(Vector3.Zero, new Vector3(-0.2f, 0.6f, -0.6f), s * 0.07f, s * 0.018f, new Vector3(0, -1.2f, 0), 0.05f, 1, p.Bone, 4, 6).Mesh);
    }

    static ArrayMesh? _feather;

    /// <summary>
    /// One feather, along +Z from its root, one unit long and one wide: narrow at the quill, broadest a third of the
    /// way, tapering to a rounded point, its middle raised along the shaft and drooping a little to the tip.
    /// </summary>
    static ArrayMesh FeatherMesh()
    {
        if (_feather is not null) return _feather;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int n = 10;
        Vector3 P(int i, float across)
        {
            var t = i / (float)n;
            var half = 0.5f * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.62f)), 0.75f) + 0.06f * (1 - t);
            return new Vector3(across * half, 0.08f * half * (1 - across * across) - 0.05f * t * t, t);
        }
        for (var i = 0; i < n; i++)
            foreach (var (a, b) in new[] { (-1f, 0f), (0f, 1f) })
            {
                st.AddVertex(P(i, a)); st.AddVertex(P(i + 1, a)); st.AddVertex(P(i + 1, b));
                st.AddVertex(P(i, a)); st.AddVertex(P(i + 1, b)); st.AddVertex(P(i, b));
            }
        st.GenerateNormals();
        return _feather = st.Commit();
    }

    /// <summary>A feather from a root, pointing a way, lying flat in the wing (its face up).</summary>
    static void Feather(Node3D into, Vector3 root, Vector3 way, float length, float width, Material m)
    {
        var z = way.Normalized();
        var x = Vector3.Up.Cross(z).Normalized();
        var y = z.Cross(x);
        into.AddChild(new MeshInstance3D
        {
            Mesh = FeatherMesh(), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            Transform = new Transform3D(new Basis(x * width, y * width, z * length), root),
        });
    }

    /// <summary>
    /// Folded, the whole wing lies back along the creature's side: the arm runs back from the shoulder, a little down
    /// and clear of the flank, the feathers hang down over it, and the closed primaries carry on back past the tail.
    /// </summary>
    static readonly Basis Folded = MakeFolded();

    static Basis MakeFolded()
    {
        var x = new Vector3(0.15f, -0.2f, 0.95f).Normalized();
        var face = new Vector3(-1, 0, 0); // the wing's top against the body, its underside out
        var y = (face - x * x.Dot(face)).Normalized();
        return new Basis(x, y, x.Cross(y));
    }

    static readonly Basis Mirror = Basis.FromScale(new Vector3(-1, 1, 1));

    void Pose(float flap)
    {
        var fold = 1 - _open;
        var spread = new Quaternion(new Basis(Vector3.Back, flap + 0.12f)); // out level, rising and falling with the beat
        var q = spread.Slerp(new Quaternion(Folded), fold);
        foreach (var w in _wings)
        {
            var b = new Basis(q);
            w.Shoulder.Basis = w.Side < 0 ? Mirror * b * Mirror : b;
            w.Hand.Rotation = new Vector3(0, -0.3f * fold, Mathf.Sin(_phase - 0.7f) * 0.3f * _open);
            foreach (var (pivot, open) in w.Primaries)
                pivot.Rotation = new Vector3(0, Mathf.Lerp(1.45f, open, _open), 0); // closed, they lie along the hand; open, they fan
        }
    }

    public override void _Process(double delta)
    {
        var rate = Flying ? _rate * 2.6f : _rate * 0.35f;
        _beat = Mathf.MoveToward(_beat, rate, (float)delta * 3);
        _phase += (float)delta * _beat * Mathf.Tau;
        // Folded at rest, stirring a little; flung wide and beating hard in the air.
        _open = Mathf.MoveToward(_open, Flying ? 1 : 0, (float)delta * 2.5f);
        Pose(Mathf.Sin(_phase) * Mathf.Lerp(0.03f, 0.75f, _open));
    }
}
