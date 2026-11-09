using Godot;

namespace Tristinwold.Game;

/// <summary>
/// The bodies of the Summoner's road: Bubbles at Braz Algún's side, and the great ones a summoner beats and binds. Each is a
/// new form blended from the game's own: a creature's body and bones, with stone, crystal, plate, horn, wing and fire grown
/// on the bones so it all moves as the body does. Their looks follow the user's pictures in reference-art
/// (summoner-*.jpg, braz-algun-and-bubbles.jpg). In body space up is +Y and the back is +Z (the front is -Z).
/// </summary>
public static class Summonfolk
{
    public static Body? For(string look) => look switch
    {
        "orrogh" => Orrogh(),
        "lumira" => Lumira(),
        "quvirn" => Quvirn(),
        "aegrath" => Aegrath(),
        "blaze" => Blaze(),
        "shicene" => Shicene(),
        "draxon" => Shadow(1f, ragged: false),
        "bubbles" => Shadow(0.62f, ragged: true),
        _ => null,
    };

    // ---- materials and parts ----

    static StandardMaterial3D Glowing(string hex, float energy = 1.6f, float alpha = 1) => new()
    {
        AlbedoColor = new Color(hex) with { A = alpha }, EmissionEnabled = true, Emission = new Color(hex), EmissionEnergyMultiplier = energy, Roughness = 0.4f,
        Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };

    static StandardMaterial3D Solid(string hex, float rough = 0.6f, float metal = 0) => new() { AlbedoColor = new Color(hex), Roughness = rough, Metallic = metal };

    static T With<T>(T body, Node3D thing, Vector3 at) where T : Node3D
    {
        thing.Position = at;
        body.AddChild(thing);
        return body;
    }

    /// <summary>A light and motes round something, at a height.</summary>
    static void Glow(Node3D body, string hex, float range, float height, float flicker = 0.15f, int motes = 40, float spread = 1)
    {
        With(body, Lairfolk.Light(hex, 2.4f, range, flicker), Vector3.Up * height);
        With(body, Lairfolk.Motes(new Color(hex), 0.12f * spread, motes, new Vector3(0.8f, 1.2f, 0.8f) * spread, 1.4f), Vector3.Up * height * 0.8f);
    }

    /// <summary>Lifts a figure's insides off the ground, so it hangs in the air where it stands.</summary>
    static void Hover(Node3D body, float metres)
    {
        foreach (var c in body.GetChildren().OfType<Node3D>()) c.Position += Vector3.Up * metres;
    }

    /// <summary>A ball on a bone (an eye, a heart, a glowing core), squashed or stretched by <paramref name="shape"/>.</summary>
    static void Ball(Graft g, string bone, Vector3 at, float radius, Material m, Vector3? shape = null)
    {
        var s = shape ?? Vector3.One;
        g.Add(bone, at, new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 12, Rings = 6 }, m, Basis.Identity.Scaled(s));
    }

    /// <summary>A scanned rock grown on a bone, half sunk in.</summary>
    static void Rock(Graft g, string bone, Vector3 at, float size, Mesh rock, float spin, Vector3? squash = null)
    {
        var box = rock.GetAabb().Size;
        var unit = 1 / Mathf.Max(box.X, Mathf.Max(box.Y, box.Z));
        var basis = new Basis(Vector3.Up, spin).Scaled((squash ?? new Vector3(1, 0.6f, 1)) * size * unit);
        g.Hold(bone, at, basis).AddChild(new MeshInstance3D { Mesh = rock, Position = -rock.GetAabb().GetCenter() });
    }

    /// <summary>A ridge of spikes along a run of bones, rising up and swept back, every <paramref name="each"/> another.</summary>
    static void Ridge(Graft g, string[] bones, float tall, float thick, Material a, Material b, int each = 3, float lean = 0.6f, float side = 0)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)bones.Length * 31 };
        foreach (var bone in bones.Where(g.Has))
            for (var i = 0; i < 3; i++)
            {
                var x = side == 0 ? rng.RandfRange(-0.02f, 0.02f) : side;
                var root = g.Bone(bone) + new Vector3(x, 0.05f, (i - 1) * 0.04f) * g.Tall;
                g.Spike(bone, root, new Vector3(x * 4, 1, lean), g.Tall * tall * rng.RandfRange(0.7f, 1.2f), g.Tall * thick, i % each == 0 ? b : a, 5);
            }
    }

    /// <summary>Claws: long hooked spikes off a paw or hand, reaching forward and down.</summary>
    static void Claws(Graft g, string bone, float length, Material m, int n = 3)
    {
        if (!g.Has(bone)) return;
        for (var i = 0; i < n; i++)
        {
            var x = (i - (n - 1) / 2f) * 0.03f;
            g.Spike(bone, g.Bone(bone) + new Vector3(x, -0.01f, -0.03f) * g.Tall, new Vector3(x * 3, -0.5f, -1), g.Tall * length, g.Tall * 0.018f, m, 5);
        }
    }

    // ---- 1: Orrogh, the Stone-Heart ----

    /// <summary>
    /// An earth giant: a troll's hulking frame grown over with the barrow's stone. Boulders for shoulders and a ridge of
    /// them down its back, slabs on its forearms like gauntlets, moss in the cracks, garnet crystals breaking out of the
    /// stone, and a heart of molten rock showing through its chest.
    /// </summary>
    static Body? Orrogh()
    {
        if (Wildfolk.Troll(6.2f, new Color("#8a867c")) is not { } c || Graft.On(c) is not { } g) return null;
        var rock = Models.MeshOf("ph:boulder.glb");
        var slab = Models.MeshOf("ph:boulder_flat.glb") ?? rock;
        var moss = Solid("#4a6e30", 1);
        var garnet = Glowing("#d0303a", 0.7f);
        var spine = new[] { "Bone", "Bone.001", "Bone.002", "Bone.003", "Bone.004" }.Where(g.Has).OrderBy(b => g.Bone(b).Y).ToArray();
        var rng = new RandomNumberGenerator { Seed = 3 };
        if (rock is not null)
        {
            foreach (var (bone, i) in spine.Select((b, i) => (b, i)).Take(spine.Length - 1)) // a ridge of stone down the back
                Rock(g, bone, g.Bone(bone) + new Vector3(0, 0.02f, 0.09f) * g.Tall, (0.2f + 0.04f * i) * g.Tall, rock, rng.RandfRange(0, Mathf.Tau));
            foreach (var side in new[] { "L", "R" })
            {
                var x = side == "L" ? -1 : 1; // the rig's left is -X
                if (g.Has($"shoulder.{side}") && g.Has($"arm.{side}")) // great boulders for shoulders, sat on top of them
                {
                    var top = (g.Bone($"shoulder.{side}") + g.Bone($"arm.{side}") * 2) / 3 + new Vector3(0, 0.08f, 0.03f) * g.Tall;
                    Rock(g, $"arm.{side}", top, 0.26f * g.Tall, rock, rng.RandfRange(0, Mathf.Tau), new Vector3(1, 0.7f, 1));
                    for (var k = 0; k < 3; k++) // garnet breaking out of the stone
                        g.Spike($"arm.{side}", top + new Vector3(x * 0.03f * k, 0.06f, 0.04f * (k - 1)) * g.Tall, new Vector3(x * (0.5f + 0.3f * k), 1, 0.4f), g.Tall * (0.1f + 0.03f * k), g.Tall * 0.025f, garnet, 6);
                }
                if (g.Has($"forearm.{side}") && g.Has($"hand.{side}") && slab is not null) // stone gauntlets round the forearms
                {
                    var mid = (g.Bone($"forearm.{side}") + g.Bone($"hand.{side}")) / 2;
                    Rock(g, $"forearm.{side}", mid + new Vector3(x * 0.06f, 0.02f, 0.05f) * g.Tall, 0.2f * g.Tall, slab, rng.RandfRange(0, Mathf.Tau), new Vector3(1, 0.9f, 1));
                    Rock(g, $"forearm.{side}", mid + new Vector3(x * 0.02f, -0.04f, -0.06f) * g.Tall, 0.16f * g.Tall, rock, rng.RandfRange(0, Mathf.Tau), new Vector3(1, 0.9f, 1));
                }
            }
        }
        foreach (var bone in spine.Take(3)) // moss in the cracks
            Ball(g, bone, g.Bone(bone) + new Vector3(rng.RandfRange(-0.05f, 0.05f), 0.08f, 0.1f) * g.Tall, 0.07f * g.Tall, moss, new Vector3(1.4f, 0.4f, 1.2f));
        if (spine.Length >= 3) // the molten heart, showing through the chest
        {
            var chest = spine[^3];
            Ball(g, chest, g.Bone(chest) + new Vector3(0, 0, -0.1f) * g.Tall, 0.06f * g.Tall, Glowing("#ff8a20", 2.4f));
            g.Hold(chest, g.Bone(chest) + new Vector3(0, 0, -0.18f) * g.Tall).AddChild(Lairfolk.Light("#ff8a30", 2.2f, 7, 0.15f));
        }
        if (spine.Length > 0) // eyes like coals under a brow of stone
        {
            var head = spine[^1];
            foreach (var x in new[] { -0.025f, 0.025f })
                Ball(g, head, g.Bone(head) + new Vector3(x, 0.02f, -0.06f) * g.Tall, 0.012f * g.Tall, Glowing("#ffa040", 3));
            if (rock is not null) Rock(g, head, g.Bone(head) + new Vector3(0, 0.06f, 0.01f) * g.Tall, 0.12f * g.Tall, rock, 1.2f, new Vector3(1.3f, 0.5f, 1));
        }
        With(c, Lairfolk.Motes(new Color("#a89070"), 0.1f, 30, new Vector3(1.4f, 0.6f, 1.4f), 0.8f), Vector3.Up * 0.4f); // dust off it as it moves
        return c;
    }

    // ---- 2: Lumira, the Dawnmoth ----

    /// <summary>
    /// A great moth of living light: a slender figure of gold light with a furred white collar, two pairs of broad
    /// moth wings barred and eyed in amber, feathered feelers, and a glowing body trailing behind, hanging in the air.
    /// </summary>
    static Body? Lumira()
    {
        var f = Figure.Create("lumira_form");
        f.Scale = Vector3.One * 2.8f;
        if (Graft.On(f) is { } g && g.Has("chest"))
        {
            var back = g.Bone("chest") + new Vector3(0, 0.04f, 0.1f) * g.Tall;
            FlapWings.Grow(g, "chest", back, g.Tall * 0.05f, g.Tall * 1.5f, feathered: false, new Color(1f, 0.88f, 0.55f, 0.6f), new Color("#fff4d0"), beat: 0.8f, bars: new Color(0.7f, 0.45f, 0.15f, 0.8f));
            FlapWings.Grow(g, "chest", back - new Vector3(0, 0.16f, -0.02f) * g.Tall, g.Tall * 0.05f, g.Tall * 1.0f, feathered: false, new Color(1f, 0.78f, 0.45f, 0.55f), new Color("#fff0c0"), beat: 0.8f, bars: new Color(0.6f, 0.35f, 0.1f, 0.8f));
            var fur = Glowing("#fff6e0", 0.5f);
            for (var i = 0; i < 10; i++) // a furred collar
            {
                var a = Mathf.Tau * i / 10;
                Ball(g, "chest", g.Bone("chest") + new Vector3(Mathf.Cos(a) * 0.07f, 0.035f, Mathf.Sin(a) * 0.055f) * g.Tall, 0.018f * g.Tall, fur);
            }
            if (g.Has("head"))
                foreach (var side in new[] { -1f, 1f }) // feathered feelers, each with little barbs
                {
                    var root = g.Bone("head") + new Vector3(side * 0.04f, 0.12f, -0.03f) * g.Tall;
                    var dir = new Vector3(side * 0.6f, 1, -0.4f).Normalized();
                    g.Spike("head", root, dir, g.Tall * 0.3f, g.Tall * 0.012f, Glowing("#fff0c0", 1.4f));
                    for (var k = 1; k < 5; k++)
                        g.Spike("head", root + dir * g.Tall * 0.06f * k, new Vector3(side, 0.3f, 0), g.Tall * 0.05f, g.Tall * 0.005f, Glowing("#fff0c0", 1.2f), 4);
                }
            if (g.Has("hips")) // the moth's body, glowing, trailing behind
                Ball(g, "hips", g.Bone("hips") + new Vector3(0, -0.06f, 0.16f) * g.Tall, 0.08f * g.Tall, Glowing("#ffd870", 1.8f, 0.85f), new Vector3(0.9f, 0.9f, 2.2f));
        }
        Hover(f, 2.5f / 2.8f);
        Glow(f, "#ffe8a0", 20, 1.6f, 0.08f, 70, 1.8f);
        return f;
    }

    // ---- 3: Quvirn, the Storm-Beast ----

    /// <summary>
    /// A storm grown claws (the user's picture: summoner-3-quvirn.jpg). A great dark beast on a bear's frame, blue-violet,
    /// maned in black spines with storm-lit ones among them; great swept horns; a second head grown out of its shoulder,
    /// a pale skull with burning red eyes; claws like sickles; and lightning crawling over all of it.
    /// </summary>
    static Body? Quvirn()
    {
        if (Creature.Create("bear.glb", 5.6f, new Color("#2a2a4e")) is not { } c || Graft.On(c) is not { } g) return null;
        var storm = Glowing("#7a8aff", 1.3f);
        var dark = Solid("#16162e", 0.45f, 0.3f);
        var bone = Solid("#c8c4d4", 0.5f);
        var red = Glowing("#ff2a1a", 3.5f);
        // A mane and back of spines, thickest at the neck.
        Ridge(g, ["Ursidae_Head", "Ursidae_Neck", "Ursidae_Spine_3", "Ursidae_Spine_2", "Ursidae_Spine_1", "Ursidae_Spine"], 0.24f, 0.026f, dark, storm, each: 3, lean: 0.9f);
        foreach (var x in new[] { -0.07f, 0.07f })
            Ridge(g, ["Ursidae_Neck", "Ursidae_Spine_3"], 0.18f, 0.02f, dark, storm, each: 2, lean: 0.7f, side: x);
        foreach (var side in new[] { "L", "R" }) // spurs on the forearms, sickle claws
        {
            if (g.Has($"Ursidae_Forearm_{side}"))
                g.Spike($"Ursidae_Forearm_{side}", g.Bone($"Ursidae_Forearm_{side}") + new Vector3(0, 0.02f, 0.03f) * g.Tall, new Vector3(side == "L" ? 0.5f : -0.5f, 0.5f, 1), g.Tall * 0.16f, g.Tall * 0.022f, storm, 5);
            Claws(g, $"Ursidae_Claw_{side}", 0.11f, bone);
        }
        if (g.Has("Ursidae_Head"))
        {
            var head = g.Bone("Ursidae_Head");
            foreach (var side in new[] { -1f, 1f }) // great horns sweeping back, and red eyes
            {
                g.Spike("Ursidae_Head", head + new Vector3(side * 0.06f, 0.08f, 0.02f) * g.Tall, new Vector3(side * 0.9f, 0.5f, 1.2f), g.Tall * 0.34f, g.Tall * 0.04f, dark, 6);
                Ball(g, "Ursidae_Head", head + new Vector3(side * 0.045f, 0.04f, -0.1f) * g.Tall, 0.014f * g.Tall, red);
            }
        }
        if (g.Has("Ursidae_Neck")) // the second head: a pale skull out of its shoulder, horned, eyes burning
        {
            var at = g.Bone("Ursidae_Neck") + new Vector3(0.13f, 0.02f, -0.08f) * g.Tall;
            Ball(g, "Ursidae_Neck", at, 0.075f * g.Tall, bone, new Vector3(1, 0.9f, 1.25f));
            g.Add("Ursidae_Neck", at + new Vector3(0, -0.05f, -0.05f) * g.Tall, new BoxMesh { Size = new Vector3(0.09f, 0.03f, 0.1f) * g.Tall }, bone); // its jaw
            for (var k = 0; k < 4; k++) // fangs
                g.Spike("Ursidae_Neck", at + new Vector3(-0.03f + k * 0.02f, -0.04f, -0.09f) * g.Tall, Vector3.Down, g.Tall * 0.035f, g.Tall * 0.007f, bone, 4);
            foreach (var x in new[] { -0.03f, 0.03f })
            {
                Ball(g, "Ursidae_Neck", at + new Vector3(x, 0.02f, -0.07f) * g.Tall, 0.014f * g.Tall, red);
                g.Spike("Ursidae_Neck", at + new Vector3(x, 0.05f, 0) * g.Tall, new Vector3(x * 20, 1, 0.8f), g.Tall * 0.14f, g.Tall * 0.02f, dark, 5);
            }
            g.Hold("Ursidae_Neck", at + new Vector3(0, 0, -0.12f) * g.Tall).AddChild(Lairfolk.Light("#ff3a2a", 1.2f, 3, 0.2f));
        }
        Glow(c, "#8a9aff", 18, 3f, 0.6f, 60, 2.6f);
        c.AddChild(new Crackle { Colour = new Color("#a8b8ff"), Reach = 3.2f, High = 3.4f });
        return c;
    }

    // ---- 4: Aegrath, the Brass Colossus ----

    /// <summary>
    /// Brasswake's oldest guardian: a clockwork golem built to a colossus, with brass pauldrons, a great core of light
    /// turning in its chest, a crown of pipes letting off steam, and a shield-plate on each arm.
    /// </summary>
    static Body? Aegrath()
    {
        var c = Clockwork.For("Ballast");
        var box = Models.Bounds(c);
        var (tall, wide, deep) = (box.Size.Y, box.Size.X, box.Size.Z);
        var brass = Solid("#c8a050", 0.35f, 0.85f);
        var dark = Solid("#3a3428", 0.5f, 0.6f);
        foreach (var x in new[] { -1f, 1f })
        {
            var shoulder = new Vector3(x * wide * 0.42f, tall * 0.78f, 0);
            c.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = wide * 0.2f, Height = wide * 0.26f }, MaterialOverride = brass, Position = shoulder });
            for (var k = 0; k < 3; k++) // rivets of spikes down the pauldron
                c.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = wide * 0.03f, Height = wide * 0.12f, RadialSegments = 6 }, MaterialOverride = dark,
                    Position = shoulder + new Vector3(x * wide * 0.1f, wide * 0.08f, (k - 1) * wide * 0.08f), Rotation = new Vector3(0, 0, -x * 0.6f) });
            c.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = wide * 0.05f, BottomRadius = wide * 0.05f, Height = tall * 0.35f }, MaterialOverride = dark, // steam pipes
                Position = new Vector3(x * wide * 0.18f, tall * 0.98f, deep * 0.3f) });
            With(c, Lairfolk.Motes(new Color(0.9f, 0.9f, 0.9f, 0.5f), 0.12f, 18, new Vector3(0.1f, 0.1f, 0.1f), 1.6f, 2, add: false), new Vector3(x * wide * 0.18f, tall * 1.18f, deep * 0.3f));
        }
        c.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = wide * 0.16f, BottomRadius = wide * 0.16f, Height = deep * 0.15f, RadialSegments = 20 }, MaterialOverride = Glowing("#ffd070", 2.2f),
            Position = new Vector3(0, tall * 0.6f, -deep * 0.5f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) }); // the core
        c.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = wide * 0.16f, OuterRadius = wide * 0.22f, Rings = 20, RingSegments = 8 }, MaterialOverride = brass,
            Position = new Vector3(0, tall * 0.6f, -deep * 0.52f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        c.Scale = Vector3.One * 2.9f;
        Glow(c, "#ffc860", 14, tall * 0.6f, 0.05f, 20, 1);
        return c;
    }

    // ---- 5: Blaze, the Living Pyre ----

    /// <summary>
    /// A fire elemental the size of a keep (the user's picture: summoner-5-blaze.jpg): a hulking black-plated beast on a
    /// bear's frame, crested with black blades down its back and molten ones among them, cracks of fire along its flanks,
    /// arms ending in great hooked claws, a maw full of fire, and a whirlwind of flame turning round it.
    /// </summary>
    static Body? Blaze()
    {
        if (Creature.Create("bear.glb", 6.2f, new Color("#1c1612")) is not { } c || Graft.On(c) is not { } g) return null;
        var plate = Solid("#14100e", 0.35f, 0.4f);
        var molten = Glowing("#ff6a10", 2.6f);
        Ridge(g, ["Ursidae_Head", "Ursidae_Neck", "Ursidae_Spine_3", "Ursidae_Spine_2", "Ursidae_Spine_1", "Ursidae_Spine", "Ursidae_Pelvis"], 0.3f, 0.032f, plate, molten, each: 3, lean: 1.2f);
        foreach (var side in new[] { "L", "R" })
        {
            var x = side == "L" ? 1f : -1f;
            foreach (var bone in new[] { "Ursidae_Spine_2", "Ursidae_Spine_1" }.Where(g.Has)) // cracks of fire down the flanks
                g.Add(bone, g.Bone(bone) + new Vector3(x * 0.1f, 0, 0) * g.Tall, new BoxMesh { Size = new Vector3(0.01f, 0.06f, 0.14f) * g.Tall }, molten, new Basis(Vector3.Forward, x * 0.3f));
            if (g.Has($"Ursidae_Upperarm_{side}")) // plated shoulders
                g.Spike($"Ursidae_Upperarm_{side}", g.Bone($"Ursidae_Upperarm_{side}") + new Vector3(x * 0.05f, 0.06f, 0) * g.Tall, new Vector3(x, 0.8f, 0.6f), g.Tall * 0.2f, g.Tall * 0.04f, plate, 5);
            Claws(g, $"Ursidae_Claw_{side}", 0.16f, plate, 4);
        }
        if (g.Has("Ursidae_Head"))
        {
            var head = g.Bone("Ursidae_Head");
            Ball(g, "Ursidae_Head", head + new Vector3(0, -0.03f, -0.1f) * g.Tall, 0.04f * g.Tall, molten, new Vector3(1.2f, 0.7f, 1)); // fire in the maw
            foreach (var x in new[] { -0.04f, 0.04f }) Ball(g, "Ursidae_Head", head + new Vector3(x, 0.04f, -0.09f) * g.Tall, 0.012f * g.Tall, Glowing("#ffd040", 3)); // eyes
            g.Spike("Ursidae_Head", head + new Vector3(0, 0.1f, -0.02f) * g.Tall, new Vector3(0, 1, 1.4f), g.Tall * 0.32f, g.Tall * 0.045f, plate, 6); // the great crest
            g.Hold("Ursidae_Head", head + new Vector3(0, 0, -0.18f) * g.Tall).AddChild(Lairfolk.Light("#ff7a20", 2.4f, 8, 0.35f));
        }
        Glow(c, "#ff7a20", 24, 2.4f, 0.35f, 40, 1.8f);
        c.AddChild(Whirl(new Color(1f, 0.45f, 0.1f, 0.24f), 4.2f, 3.6f));
        c.AddChild(Whirl(new Color(1f, 0.75f, 0.2f, 0.16f), 3.0f, 5f));
        return c;
    }

    /// <summary>A whirlwind of flame (or snow) turning round something: motes swept round and up.</summary>
    static Node3D Whirl(Color colour, float radius, float height)
    {
        var flame = new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 64, Height = 64,
            Gradient = new Gradient { Offsets = [0, 1], Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)] },
        };
        return new CpuParticles3D
        {
            Amount = 140, Lifetime = 1.6, EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring, EmissionRingRadius = radius, EmissionRingInnerRadius = radius * 0.7f,
            EmissionRingHeight = height * 0.4f, EmissionRingAxis = Vector3.Up, Position = Vector3.Up * height * 0.3f,
            Direction = Vector3.Up, Spread = 15, Gravity = Vector3.Up * 1.2f, InitialVelocityMin = 0.6f, InitialVelocityMax = 1.6f, OrbitVelocityMin = 0.35f, OrbitVelocityMax = 0.6f,
            Mesh = new QuadMesh { Size = Vector2.One * radius * 0.35f }, ScaleAmountMin = 0.5f, ScaleAmountMax = 1.4f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoColor = colour, AlbedoTexture = flame,
            },
        };
    }

    // ---- 6: Shicene, the Winter Wing ----

    /// <summary>
    /// A giant of living ice (the user's picture: summoner-6-shicene.jpg): a tall pale-blue figure, translucent and glowing,
    /// on two pairs of great wings like frost on glass, a crown of ice swept up and back, long ice tresses down her back,
    /// shards along her arms, hanging in the air with snow falling round her from a clear sky.
    /// </summary>
    static Body? Shicene()
    {
        var f = Figure.Create("shicene_form");
        f.Scale = Vector3.One * 3.6f;
        if (Graft.On(f) is { } g && g.Has("chest"))
        {
            var ice = Glowing("#d8f4ff", 0.8f, 0.8f);
            var back = g.Bone("chest") + new Vector3(0, 0.06f, 0.12f) * g.Tall;
            FlapWings.Grow(g, "chest", back, g.Tall * 0.05f, g.Tall * 1.2f, feathered: false, new Color(0.75f, 0.92f, 1f, 0.45f), new Color("#e8f8ff"), beat: 0.6f, bars: new Color(0.95f, 0.85f, 1f, 0.6f));
            FlapWings.Grow(g, "chest", back - new Vector3(0, 0.16f, -0.02f) * g.Tall, g.Tall * 0.05f, g.Tall * 0.85f, feathered: false, new Color(0.7f, 0.85f, 1f, 0.4f), new Color("#f0f4ff"), beat: 0.6f, bars: new Color(1f, 0.8f, 0.95f, 0.5f));
            if (g.Has("head"))
            {
                var head = g.Bone("head");
                for (var i = 0; i < 11; i++) // a crown of ice, swept up and back
                {
                    var a = (i - 5) * 0.28f;
                    g.Spike("head", head + new Vector3(Mathf.Sin(a) * 0.08f, 0.12f, 0.02f) * g.Tall, new Vector3(Mathf.Sin(a) * 1.2f, 1.4f, 0.9f), g.Tall * (0.16f + 0.08f * (1 - Mathf.Abs(i - 5) / 5f)), g.Tall * 0.02f, ice);
                }
                for (var i = 0; i < 7; i++) // tresses of ice down her back
                    g.Spike("head", head + new Vector3((i - 3) * 0.02f, 0.06f, 0.06f) * g.Tall, new Vector3((i - 3) * 0.1f, -1, 0.35f), g.Tall * (0.3f + 0.04f * (i % 3)), g.Tall * 0.015f, ice, 4);
            }
            foreach (var arm in new[] { "lowerarm.l", "lowerarm.r" }.Where(g.Has)) // shards along the arms
                for (var k = 0; k < 3; k++)
                    g.Spike(arm, g.Bone(arm) + new Vector3(0, 0.02f * k, 0.03f) * g.Tall, new Vector3(arm.EndsWith('l') ? 1 : -1, 0.4f, 1), g.Tall * 0.08f, g.Tall * 0.012f, ice, 4);
        }
        Hover(f, 3f / 3.6f);
        Glow(f, "#c8ecff", 22, 1.4f, 0.05f, 40, 1.4f);
        f.AddChild(Whirl(new Color(0.85f, 0.95f, 1f, 0.2f), 2.4f, 2.2f)); // snow from a clear sky
        return f;
    }

    // ---- 7: Draxon, and Bubbles ----

    /// <summary>
    /// A black dragon of the Rift, lit green from inside (the user's pictures: summoner-last-draxon.jpg and
    /// braz-algun-and-bubbles.jpg): Draxon, the King of Beasts, solid black, horned and spined, green fire behind his teeth
    /// and between his ribs; or his mother Bubbles at Braz Algún's side, smaller (<paramref name="size"/>) and ragged.
    /// </summary>
    static Body Shadow(float size, bool ragged)
    {
        var d = Body.Dragon("black");
        if (size < 1) d.Scale = Vector3.One * size;
        if (Graft.On(d) is { } g)
        {
            var black = Solid("#0c0e0c", 0.35f, 0.3f);
            var green = Glowing("#4aff5a", 2.4f);
            Ridge(g, ["neck3", "neck2", "neck1", "back2", "back1", "base", "tail1", "tail2", "tail3"], ragged ? 0.09f : 0.12f, 0.016f, black, green, each: ragged ? 2 : 3, lean: 1.1f);
            if (g.Has("jaw_upper"))
            {
                var jaw = g.Bone("jaw_upper");
                foreach (var x in new[] { -1f, 1f }) // horns, curling back
                {
                    g.Spike("jaw_upper", jaw + new Vector3(x * 0.03f, 0.04f, 0.05f) * g.Tall, new Vector3(x * 0.6f, 0.9f, 1.3f), g.Tall * 0.2f, g.Tall * 0.022f, black, 6);
                    Ball(g, "jaw_upper", jaw + new Vector3(x * 0.025f, 0.025f, -0.01f) * g.Tall, 0.01f * g.Tall, Glowing("#a8ff9a", 4)); // eyes
                }
                Ball(g, "jaw_upper", jaw + new Vector3(0, -0.02f, -0.06f) * g.Tall, 0.022f * g.Tall, green, new Vector3(1, 0.6f, 1.6f)); // green fire behind the teeth
            }
            foreach (var bone in new[] { "back1", "back2" }.Where(g.Has)) // ribs lit green from inside
                for (var k = 0; k < 3; k++)
                    foreach (var x in new[] { -1f, 1f })
                        g.Add(bone, g.Bone(bone) + new Vector3(x * 0.07f, -0.05f, (k - 1) * 0.035f) * g.Tall, new BoxMesh { Size = new Vector3(0.008f, 0.08f, 0.01f) * g.Tall }, green, new Basis(Vector3.Forward, x * 0.5f));
        }
        Glow(d, "#5aff6a", 14 * Math.Max(size, 0.8f), 2.6f, 0.2f, 40, 1.6f);
        return d;
    }
}

/// <summary>Lightning crawling over something: little bolts leaping about it, now and then.</summary>
public partial class Crackle : Node3D
{
    public Color Colour { get; set; } = new("#a8b8ff");
    public float Reach { get; set; } = 2;
    public float High { get; set; } = 2;
    double _next;
    readonly RandomNumberGenerator _rng = new();

    public override void _Process(double delta)
    {
        if ((_next -= delta) > 0 || GetTree().CurrentScene is not { } world) return;
        _next = _rng.RandfRange(0.12f, 0.45f);
        Vector3 Spot() => GlobalPosition + GlobalBasis * new Vector3(_rng.RandfRange(-Reach, Reach), _rng.RandfRange(0.4f, 1.2f) * High, _rng.RandfRange(-Reach, Reach));
        Effects.Lightning(world, Spot(), Spot(), Colour, 0.12, 0.04f);
    }
}
