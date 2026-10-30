using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Medieval kit for the real bodies, built to each body's own measure and hung from its bones so it moves with it:
/// a belt with a buckle, a cuirass of steel or a jerkin of leather, pauldrons, bracers, a tabard, a cloak, a hood, a
/// skullcap, a wizard's hat, a robe with wide sleeves. Each piece is simple geometry in the armoury's materials, sized
/// from where the hips, chest, neck, shoulders and arms actually are, so the same kit fits a dwarf and a half-giant.
/// Which pieces someone wears follows their class or trade (<see cref="For"/>); a hero's worn armour and helm put
/// their own on in their own metal.
/// </summary>
public static class Garments
{
    /// <summary>What someone wears over their clothes: the body piece ("cuirass", "jerkin", "robe", "fur" or none), what's on
    /// the head ("hood", "cap", "hat" or none), and the rest.</summary>
    public sealed record Kit(string? Body = null, string? Head = null, bool Belt = true, bool Pauldrons = false, bool Bracers = false, bool Cloak = false, bool Tabard = false, bool MetalBracers = false, bool Mantle = false, bool Sash = false);

    /// <summary>What a hero picked to wear over their clothes, in place of the class's kit (what's on the head stays the class's).</summary>
    static Kit Picked(string wear, Kit own) => wear switch
    {
        "Plain" => new(null, own.Head),
        "Vest" => new("vest", own.Head),
        "Jerkin" => new("jerkin", own.Head, Bracers: true),
        "Robe" => new("robe", own.Head, Belt: false),
        "Wrapped robe" => new("robe", own.Head, Belt: false, Mantle: true),
        "Sash" => new(null, own.Head, Belt: false, Sash: true),
        "Cloak" => new(null, own.Head, Cloak: true),
        _ => own,
    };

    public static Kit For(string look) => look switch
    {
        "Paladin" or "Knight" or "aldric" or "dragon_knight" => new("cuirass", null, Pauldrons: true, Bracers: true, MetalBracers: true, Tabard: true),
        "guard" or "marine" or "squire" or "hollow_soldier" or "ghost_captain" => new("cuirass", "cap", Pauldrons: true, Bracers: true, MetalBracers: true),
        "EtherAeronaut" => new("jerkin", "cap", Bracers: true, MetalBracers: true),
        "Barbarian" or "raider" or "raider_chief" or "warmaster" => new("fur", null, Pauldrons: true, Bracers: true),
        "dock_thug" or "worker" => new(null, null, Bracers: true),
        "Thief" or "Rogue" or "protege" or "bandit" or "bandit_chief" or "lantern_boy" or "velvet" => new("jerkin", null, Bracers: true),
        "Assassin" or "Saboteur" or "Rogue_Hooded" or "drowned_hand" => new("jerkin", "hood", Bracers: true, Cloak: true),
        "Ranger" or "hunter" or "traveller" => new("jerkin", "hood", Bracers: true, Cloak: true),
        "Wizard" or "Mage" or "apprentice" => new("robe", "hat", Belt: false),
        "Alchemist" => new("jerkin", null, Bracers: true),
        "Warlock" or "Sorcerer" or "scholar" or "laureate" or "Poet" or "Scribe" => new("robe", null, Belt: false),
        "priest" => new("robe", "hood", Belt: false),
        "pirate" or "sailor" => new(null, "bandana", Bracers: true),
        "pirate_captain" => new("jerkin", "bandana", Bracers: true, Cloak: true),
        "MartialArtist" => new(null, null, Bracers: true),
        "captive" or "child" or "youth" => new(null, null, Belt: false),
        _ => new(),
    };

    /// <summary>The measure of a body: where its bones rest, in the skeleton's space.</summary>
    sealed record Measure(Vector3 Hips, Vector3 Chest, Vector3 Neck, Vector3 Head, float Top, float HipHalf, float ShoulderHalf);

    static Measure? Measured(Skeleton3D sk, float top)
    {
        Vector3? At(string bone) => sk.FindBone(bone) is var i && i >= 0 ? sk.GetBoneGlobalRest(i).Origin : null;
        if (At("Hips") is not { } hips || At("Chest") is not { } chest || At("Neck") is not { } neck || At("Head") is not { } head) return null;
        var hipHalf = At("LeftUpperLeg") is { } ll && At("RightUpperLeg") is { } rl ? Mathf.Abs(ll.X - rl.X) / 2 + 0.07f : 0.17f;
        var shoulderHalf = At("LeftUpperArm") is { } la && At("RightUpperArm") is { } ra ? Mathf.Abs(la.X - ra.X) / 2 : 0.19f;
        return new(hips, chest, neck, head, top, hipHalf, shoulderHalf);
    }

    /// <summary>A place to put pieces in the skeleton's own space that follows a bone.</summary>
    static Node3D Hang(Skeleton3D sk, string bone, List<Node> made)
    {
        var i = sk.FindBone(bone);
        var slot = new BoneAttachment3D { BoneName = bone, Name = $"Garment_{bone}" };
        sk.AddChild(slot);
        made.Add(slot);
        var space = new Node3D { Transform = i >= 0 ? sk.GetBoneGlobalRest(i).AffineInverse() : Transform3D.Identity };
        slot.AddChild(space);
        return space;
    }

    static MeshInstance3D Part(Mesh mesh, Material m, Vector3 at, Vector3? turn = null, Vector3? scale = null) => new()
    {
        Mesh = mesh, MaterialOverride = m, Position = at, RotationDegrees = turn ?? Vector3.Zero, Scale = scale ?? Vector3.One,
    };

    /// <summary>
    /// Dresses a body in its kit. Armour worn from the pack (a tint and whether it's metal) puts a cuirass on in that
    /// metal; a helm worn puts a cap on in its tint. Returns the nodes made, so they can be taken off again.
    /// </summary>
    public static List<Node> Dress(Skeleton3D sk, string look, Humans.Outfit outfit, float top, (Color Tint, bool Metal)? armour = null, Color? helm = null, string wear = "Class", (string Style, Color Colour)? cut = null)
    {
        var made = new List<Node>();
        if (Measured(sk, top) is not { } m) return made;
        var kit = Picked(wear, For(look));
        if (armour is { } a) kit = kit with { Body = a.Metal ? "cuirass" : "jerkin", Pauldrons = kit.Pauldrons || a.Metal, Bracers = true, MetalBracers = a.Metal };
        if (helm is not null) kit = kit with { Head = "cap" };
        var leather = Armoury.Leather(outfit.Boots.Darkened(0.1f).ToHtml(false) is var b ? "#" + b : "#3a2a1c");
        var steel = armour is { Metal: true } ? Armoury.Metal(armour.Value.Tint, 0.4f) : Armoury.Metal(new Color("#9a9ea4"), 0.42f);
        var hide = armour is { Metal: false } ? Armoury.Leather("#" + armour.Value.Tint.ToHtml(false)) : Armoury.Leather("#5a3e28");
        var cloth = Armoury.Cloth("#" + outfit.Tunic.ToHtml(false));
        var torsoH = m.Neck.Y - m.Hips.Y;

        if (kit.Belt)
        {
            var hips = Hang(sk, "Hips", made);
            var r = m.HipHalf + 0.035f;
            hips.AddChild(Part(new TorusMesh { InnerRadius = r - 0.012f, OuterRadius = r + 0.012f, Rings = 40, RingSegments = 10 }, leather, new Vector3(0, m.Hips.Y + 0.05f, 0.01f), scale: new Vector3(1, 2.2f, 0.92f)));
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.055f, 0.06f, 0.016f) }, Armoury.Brass, new Vector3(0, m.Hips.Y + 0.05f, r * 0.92f + 0.012f)));
            hips.AddChild(Part(new CapsuleMesh { Radius = 0.5f, Height = 1.3f, RadialSegments = 16, Rings = 6 }, leather, new Vector3(-r * 0.75f, m.Hips.Y - 0.015f, r * 0.5f), new Vector3(0, 30, 0), new Vector3(0.075f, 0.075f, 0.05f))); // a pouch, soft and rounded
            hips.AddChild(Part(new TorusMesh { InnerRadius = 0.03f, OuterRadius = 0.04f, Rings = 20, RingSegments = 8 }, leather, new Vector3(-r * 0.75f, m.Hips.Y + 0.025f, r * 0.5f), new Vector3(0, 30, 0), new Vector3(1, 0.8f, 0.9f))); // its flap's roll
        }

        switch (kit.Body)
        {
            case "cuirass" or "jerkin":
            {
                var chest = Hang(sk, "Chest", made);
                var mat = kit.Body == "cuirass" ? steel : hide;
                var centreY = m.Hips.Y + torsoH * 0.56f;
                // A rounded shell round the trunk, from the belly to just under the collarbones.
                chest.AddChild(Part(new CapsuleMesh { Radius = 0.5f, Height = 1.6f, RadialSegments = 28, Rings = 10 }, mat, new Vector3(0, centreY, 0.012f), scale: new Vector3(m.ShoulderHalf * 1.05f, torsoH * 0.5f, m.ShoulderHalf * 0.78f)));
                if (kit.Body == "cuirass")
                {
                    // Rivets along the edge, and a gorget at the throat.
                    for (var i = -3; i <= 3; i++) chest.AddChild(Part(new SphereMesh { Radius = 0.009f, Height = 0.018f }, Armoury.Brass, new Vector3(i * m.ShoulderHalf * 0.26f, m.Hips.Y + torsoH * 0.2f, m.ShoulderHalf * 0.74f)));
                    chest.AddChild(Part(new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.1f, Rings = 24, RingSegments = 8 }, steel, new Vector3(0, m.Neck.Y - 0.015f, 0.01f), scale: new Vector3(1, 1.6f, 1)));
                }
                else
                {
                    // The lacing: a cord up the front through a row of eyelets.
                    var cord = Armoury.Leather("#2a1e14");
                    chest.AddChild(Part(new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = torsoH * 0.6f, RadialSegments = 8 }, cord, new Vector3(0, centreY, m.ShoulderHalf * 0.78f + 0.006f)));
                    for (var i = -2; i <= 2; i++) chest.AddChild(Part(new TorusMesh { InnerRadius = 0.006f, OuterRadius = 0.012f, Rings = 12, RingSegments = 6 }, Armoury.Brass, new Vector3(0, centreY + i * torsoH * 0.13f, m.ShoulderHalf * 0.78f + 0.004f), new Vector3(90, 0, 0)));
                }
                break;
            }
            case "robe":
            {
                var hips = Hang(sk, "Hips", made);
                var length = m.Hips.Y - 0.1f;
                hips.AddChild(Part(new CylinderMesh { TopRadius = m.HipHalf + 0.02f, BottomRadius = m.HipHalf + 0.17f, Height = length, RadialSegments = 28, Rings = 2 }, Robe(outfit), new Vector3(0, m.Hips.Y - length / 2 + 0.03f, 0)));
                hips.AddChild(Part(new TorusMesh { InnerRadius = m.HipHalf + 0.01f, OuterRadius = m.HipHalf + 0.045f, Rings = 36, RingSegments = 8 }, Armoury.Cloth("#" + outfit.Trousers.Lightened(0.2f).ToHtml(false)), new Vector3(0, m.Hips.Y + 0.09f, 0), scale: new Vector3(1, 1.5f, 1))); // a sash
                foreach (var side in new[] { "Left", "Right" })
                {
                    var arm = Hang(sk, $"{side}LowerArm", made);
                    if (sk.FindBone($"{side}LowerArm") is var li && li >= 0)
                    {
                        var rest = sk.GetBoneGlobalRest(li);
                        var toHand = rest.Basis.Y.Normalized();
                        var at = rest.Origin + toHand * 0.12f;
                        arm.AddChild(Part(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.11f, Height = 0.24f, RadialSegments = 20, CapTop = false, CapBottom = false }, Robe(outfit), at, scale: Vector3.One) is var sleeve ? Aim(sleeve, toHand) : null!);
                    }
                }
                break;
            }
            case "fur":
            {
                var chest = Hang(sk, "Chest", made);
                var fur = Armoury.Leather("#6a4a30");
                chest.AddChild(Part(new TorusMesh { InnerRadius = m.ShoulderHalf * 0.55f, OuterRadius = m.ShoulderHalf * 1.1f, Rings = 28, RingSegments = 10 }, fur, new Vector3(0, m.Neck.Y - 0.06f, 0.01f), scale: new Vector3(1, 1.3f, 0.85f))); // a mantle of hide
                break;
            }
        }

        if (kit.Body == "vest")
        {
            // An open vest: a panel down each side of the chest, and one across the back.
            var chest = Hang(sk, "Chest", made);
            var felt = Cloth(outfit.Trousers.Lightened(0.08f));
            var collar = m.Hips.Y + torsoH * 0.82f;
            var front = m.ShoulderHalf * 0.82f + 0.012f;
            foreach (var side in new[] { -1f, 1f })
                chest.AddChild(Part(Drape(m.ShoulderHalf * 0.5f, torsoH * 0.78f, m.ShoulderHalf * 0.2f, 0.1f), felt, new Vector3(side * m.ShoulderHalf * 0.62f, collar, front), new Vector3(0, side * 16, 0)));
            chest.AddChild(Part(Drape(m.ShoulderHalf * 1.8f, torsoH * 0.78f, m.ShoulderHalf * 0.4f, 0.1f), felt, new Vector3(0, collar, -front), new Vector3(0, 180, 0)));
        }

        if (kit.Mantle)
        {
            // A second cloth wrapped round the shoulders, thrown over one arm.
            var chest = Hang(sk, "Chest", made);
            var wrap = Cloth(outfit.Trousers.Lightened(0.12f), span: 0);
            chest.AddChild(Part(new TorusMesh { InnerRadius = m.ShoulderHalf * 0.5f, OuterRadius = m.ShoulderHalf * 1.15f, Rings = 28, RingSegments = 10 }, wrap, new Vector3(0, m.Neck.Y - 0.1f, 0.01f), new Vector3(-6, 0, 12), new Vector3(1, 1.5f, 0.9f)));
        }

        if (kit.Sash)
        {
            var hips = Hang(sk, "Hips", made);
            var band = Cloth(outfit.Trousers.Lightened(0.25f), span: 0);
            hips.AddChild(Part(new TorusMesh { InnerRadius = m.HipHalf + 0.005f, OuterRadius = m.HipHalf + 0.05f, Rings = 36, RingSegments = 8 }, band, new Vector3(0, m.Hips.Y + 0.07f, 0), scale: new Vector3(1, 1.8f, 1)));
            hips.AddChild(Part(Drape(0.1f, 0.34f, 0.01f, 0.4f, 6, 8, lean: 0.03f), Cloth(outfit.Trousers.Lightened(0.25f)), new Vector3(m.HipHalf * 0.6f, m.Hips.Y + 0.05f, m.HipHalf + 0.03f)));
        }

        if (kit.Tabard)
        {
            // A tabard: a panel of cloth down the chest, curved round the trunk and swinging free below the belt,
            // with a cross of a second colour on it.
            var chest = Hang(sk, "Chest", made);
            var heraldry = Cloth(outfit.Tunic.Lightened(0.25f));
            var cross = Cloth(new Color("#a82a2a"));
            var w = m.ShoulderHalf * 1.1f;
            var collar = m.Hips.Y + torsoH * 0.76f;
            var front = m.ShoulderHalf * 0.82f + 0.012f;
            var curve = m.ShoulderHalf * 0.4f;
            chest.AddChild(Part(Drape(w, torsoH * 0.62f, curve, 0.1f), heraldry, new Vector3(0, collar, front)));
            chest.AddChild(Part(Drape(w * 0.22f, torsoH * 0.5f, curve * 0.05f, 0.1f), cross, new Vector3(0, collar - torsoH * 0.06f, front + 0.006f)));
            chest.AddChild(Part(Drape(w * 0.7f, torsoH * 0.12f, curve * 0.49f, 0.1f), cross, new Vector3(0, collar - torsoH * 0.15f, front + 0.006f)));
            var hips = Hang(sk, "Hips", made);
            var skirt = Drape(w * 1.25f, 0.38f, m.HipHalf * 0.55f, 0.2f, 12, 16, lean: 0.05f);
            hips.AddChild(Part(skirt, heraldry, new Vector3(0, m.Hips.Y + 0.03f, m.HipHalf + 0.03f)));
            hips.AddChild(Part(skirt, heraldry, new Vector3(0, m.Hips.Y + 0.03f, -(m.HipHalf + 0.03f)), new Vector3(0, 180, 0)));
        }


        if (kit.Cloak)
        {
            // A cloak from the shoulders, wrapping round the back and swinging behind the legs.
            var chest = Hang(sk, "Chest", made);
            var cape = Cloth(outfit.Trousers.Darkened(0.15f));
            var length = m.Neck.Y - 0.25f;
            chest.AddChild(Part(Drape(m.ShoulderHalf * 2.2f, length, m.ShoulderHalf * 0.9f, 0.3f, 12, 18, lean: 0.08f), cape, new Vector3(0, m.Neck.Y - 0.02f, -(m.ShoulderHalf * 0.85f + 0.02f)), new Vector3(0, 180, 0)));
        }

        switch (kit.Head)
        {
            case "hood":
            {
                var head = Hang(sk, "Head", made);
                var hood = Cloth(outfit.Cap ?? outfit.Tunic.Darkened(0.25f), span: 0);
                var size = m.Top - m.Head.Y;
                // The hood sits back on the head so the face comes out of its opening: its front edge at the cheeks, the crown over the top.
                var centre = new Vector3(0, m.Head.Y + size * 0.5f, -0.075f);
                head.AddChild(Part(new SphereMesh { Radius = 0.118f, Height = 0.236f, RadialSegments = 32, Rings = 16 }, hood, centre, scale: new Vector3(1.02f, 1.12f, 0.95f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.105f, OuterRadius = 0.122f, Rings = 32, RingSegments = 8 }, hood, new Vector3(0, m.Head.Y + size * 0.45f, 0.03f), new Vector3(90, 0, 0), new Vector3(1, 1, 1.2f))); // the opening, framing the face
                head.AddChild(Part(Drape(0.2f, 0.16f, 0.05f, 0.3f, 8, 8, lean: 0.02f), hood, new Vector3(0, m.Head.Y + size * 0.3f, -0.1f), new Vector3(0, 180, 0))); // the fall of it down the back of the neck
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.13f, Rings = 24, RingSegments = 8 }, hood, new Vector3(0, m.Neck.Y + 0.02f, 0), scale: new Vector3(1, 1.3f, 1))); // the cowl at the neck
                break;
            }
            case "bandana":
            {
                // A kerchief tied over the crown, its ends hanging at the nape.
                var head = Hang(sk, "Head", made);
                var kerchief = Cloth(outfit.Cap ?? outfit.Tunic, span: 0);
                head.AddChild(Part(new SphereMesh { Radius = 0.128f, Height = 0.256f, RadialSegments = 32, Rings = 16 }, kerchief, new Vector3(0, m.Top - 0.045f, -0.012f), scale: new Vector3(1.0f, 0.62f, 1.04f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.114f, OuterRadius = 0.128f, Rings = 32, RingSegments = 8 }, kerchief, new Vector3(0, m.Top - 0.085f, -0.012f), new Vector3(8, 0, 0), new Vector3(1, 1.1f, 1.04f))); // the rolled edge over the brow
                head.AddChild(Part(new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 12, Rings = 6 }, kerchief, new Vector3(0, m.Top - 0.085f, -0.125f), scale: new Vector3(1.3f, 0.8f, 0.8f))); // the knot
                head.AddChild(Part(Drape(0.07f, 0.17f, 0.01f, 0.5f, 6, 8, lean: 0.03f), kerchief, new Vector3(0.012f, m.Top - 0.095f, -0.13f), new Vector3(0, 180, 12))); // its ends
                head.AddChild(Part(Drape(0.06f, 0.14f, 0.01f, 0.5f, 6, 8, lean: 0.03f), kerchief, new Vector3(-0.015f, m.Top - 0.095f, -0.13f), new Vector3(0, 180, -10)));
                break;
            }
            case "cap":
            {
                var head = Hang(sk, "Head", made);
                var mat = helm is { } h ? Armoury.Metal(h, 0.42f) : steel;
                // Sits on the crown like a skullcap: its rim just above the brows and ears, the dome over the top of the head.
                head.AddChild(Part(new SphereMesh { Radius = 0.122f, Height = 0.244f, RadialSegments = 24, Rings = 12 }, mat, new Vector3(0, m.Top - 0.075f, -0.012f), scale: new Vector3(1, 0.85f, 1.05f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.11f, OuterRadius = 0.132f, Rings = 28, RingSegments = 8 }, Armoury.Iron, new Vector3(0, m.Top - 0.1f, -0.012f), scale: new Vector3(1, 1.4f, 1.05f))); // the rim
                head.AddChild(Part(new BoxMesh { Size = new Vector3(0.016f, 0.08f, 0.02f) }, Armoury.Iron, new Vector3(0, m.Top - 0.125f, 0.112f))); // a nasal
                break;
            }
            case "hat":
            {
                var head = Hang(sk, "Head", made);
                var felt = Armoury.Cloth("#" + outfit.Trousers.Darkened(0.1f).ToHtml(false));
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.012f, RadialSegments = 32 }, felt, new Vector3(0, m.Top - 0.035f, -0.01f))); // the brim
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.12f, Height = 0.36f, RadialSegments = 24 }, felt, new Vector3(0, m.Top + 0.15f, -0.02f), new Vector3(8, 0, 0))); // the point, leaning a little
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.13f, Rings = 28, RingSegments = 8 }, Armoury.Leather("#2a1e14"), new Vector3(0, m.Top - 0.005f, -0.01f), scale: new Vector3(1, 1.5f, 1))); // a band
                break;
            }
        }
        if (cut is { } style && kit.Head is null && outfit.Cap is null) Hair(sk, m, style.Style, style.Colour, made);
        return made;
    }

    /// <summary>A cut built on the head to the body's own measure, for anyone: a close crop of the scalp, then whatever makes it that cut.</summary>
    static void Hair(Skeleton3D sk, Measure m, string style, Color colour, List<Node> made)
    {
        var head = Hang(sk, "Head", made);
        var hair = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.62f, RimEnabled = true, Rim = 0.2f };
        var crown = new Vector3(0, m.Top - 0.075f, -0.016f);
        void Crop() => head.AddChild(Part(new SphereMesh { Radius = 0.122f, Height = 0.244f, RadialSegments = 24, Rings = 12 }, hair, crown, new Vector3(-16, 0, 0), new Vector3(1.02f, 0.86f, 1.06f)));
        void Lock(Vector3 from, Vector3 way, float length, float width)
        {
            var mesh = new CapsuleMesh { Radius = width, Height = length, RadialSegments = 12, Rings = 6 };
            var part = Part(mesh, hair, from + way.Normalized() * length / 2);
            head.AddChild(part);
            Aim(part, way.Normalized());
        }
        switch (style)
        {
            case "Topknot":
                Crop();
                head.AddChild(Part(new SphereMesh { Radius = 0.058f, Height = 0.116f, RadialSegments = 16, Rings = 8 }, hair, new Vector3(0, m.Top + 0.035f, -0.03f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.022f, OuterRadius = 0.034f, Rings = 16, RingSegments = 6 }, Armoury.Leather("#a85a2a"), new Vector3(0, m.Top - 0.012f, -0.028f), scale: new Vector3(1, 1.3f, 1)));
                break;
            case "Bun":
                Crop();
                head.AddChild(Part(new SphereMesh { Radius = 0.066f, Height = 0.132f, RadialSegments = 16, Rings = 8 }, hair, new Vector3(0, m.Top - 0.06f, -0.145f)));
                break;
            case "Mohawk":
                for (var i = 0; i < 8; i++)
                {
                    var along = -0.6f + i * 0.34f;
                    var at = crown + new Vector3(0, Mathf.Cos(along) * 0.1f, -Mathf.Sin(along) * 0.115f);
                    Lock(at, new Vector3(0, Mathf.Cos(along), -Mathf.Sin(along)), 0.11f, 0.018f);
                }
                break;
            case "Spikes":
                Crop();
                for (var i = 0; i < 12; i++)
                {
                    var (a, up) = (i * Mathf.Tau / 12, 0.35f + i % 3 * 0.25f);
                    var way = new Vector3(Mathf.Cos(a) * (1 - up), up, Mathf.Sin(a) * (1 - up));
                    Lock(crown + way.Normalized() * 0.09f, way, 0.11f, 0.018f);
                }
                break;
            case "Mane":
                Crop();
                for (var i = -3; i <= 3; i++)
                    Lock(crown + new Vector3(i * 0.032f, 0.03f, -0.1f + Mathf.Abs(i) * 0.008f), new Vector3(i * 0.04f, -1, -0.28f), 0.3f, 0.034f);
                break;
            case "Twintails":
                Crop();
                foreach (var side in new[] { -1f, 1f })
                {
                    head.AddChild(Part(new SphereMesh { Radius = 0.026f, Height = 0.052f, RadialSegments = 10, Rings = 6 }, Armoury.Leather("#8a2a2a"), crown + new Vector3(side * 0.118f, 0.02f, -0.03f)));
                    Lock(crown + new Vector3(side * 0.128f, 0.012f, -0.03f), new Vector3(side * 0.3f, -1, -0.1f), 0.34f, 0.03f);
                }
                break;
        }
    }

    static Material Robe(Humans.Outfit outfit) => Cloth(outfit.Tunic, span: 2);

    static Shader? _clothShader;

    /// <summary>
    /// Cloth that hangs: a matte dyed weave, seen from both sides, that sways a little more the further it is from
    /// where it hangs (the top of its UV). <paramref name="span"/> is how much of the UV's height is the hang (2 for
    /// a cylinder, whose side takes the top half).
    /// </summary>
    static ShaderMaterial Cloth(Color dye, float span = 1)
    {
        _clothShader ??= new Shader { Code = ClothCode };
        var m = new ShaderMaterial { Shader = _clothShader };
        m.SetShaderParameter("dye", dye);
        m.SetShaderParameter("grain_tex", Styled.Grain);
        m.SetShaderParameter("span", span);
        return m;
    }

    const string ClothCode = """
        shader_type spatial;
        render_mode cull_disabled;
        uniform vec4 dye : source_color = vec4(1.0);
        uniform sampler2D grain_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
        uniform float span = 1.0;
        uniform float sway = 1.0;

        void vertex() {
            float hang = clamp(UV.y * span, 0.0, 1.0);
            hang *= hang;
            float phase = NODE_POSITION_WORLD.x * 2.9 + NODE_POSITION_WORLD.z * 2.1;
            float t = TIME * 1.6 + phase;
            float swing = sin(t + UV.x * 4.0) * 0.012 + sin(t * 0.63 + UV.y * 5.0) * 0.008;
            VERTEX.z += swing * hang * sway;
            VERTEX.x += cos(t * 0.8) * 0.006 * hang * sway;
        }

        void fragment() {
            float fine = texture(grain_tex, UV * vec2(9.0, 12.0)).r;
            float coarse = texture(grain_tex, UV * vec2(1.5, 2.0)).r;
            ALBEDO = dye.rgb * (0.93 + 0.14 * fine) * (0.94 + 0.12 * coarse);
            ROUGHNESS = 0.82 + 0.16 * fine;
            SPECULAR = 0.2;
            NORMAL = normalize(NORMAL + (fine - 0.5) * 0.1 * TANGENT + (coarse - 0.5) * 0.05 * BINORMAL);
            RIM = 0.2;
            RIM_TINT = 0.6;
        }
        """;

    /// <summary>
    /// A hanging panel of cloth, built as a smooth sheet rather than a box: it hangs from the origin down -Y facing
    /// +Z, curves back round the body by <paramref name="curve"/> at its edges, widens toward the hem by
    /// <paramref name="flare"/> of its width, leans out by <paramref name="lean"/> at the hem, and has a few soft
    /// folds. Its normals are its own, so it shades like cloth and not like a plank.
    /// </summary>
    static ArrayMesh Drape(float width, float length, float curve, float flare, int across = 10, int down = 14, float lean = 0.03f)
    {
        Vector3 At(float u, float v)
        {
            var bend = (u - 0.5f) * 2;
            var x = bend * width / 2 * (1 + flare * v);
            var folds = Mathf.Sin(u * Mathf.Pi * 3) * 0.012f * v + Mathf.Sin(u * Mathf.Pi * 7 + 1) * 0.004f * v;
            return new Vector3(x, -length * v, -curve * bend * bend + lean * v + folds);
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var j = 0; j <= down; j++)
            for (var i = 0; i <= across; i++)
            {
                float u = (float)i / across, v = (float)j / down;
                var n = (At(u + 0.01f, v) - At(u - 0.01f, v)).Cross(At(u, v + 0.01f) - At(u, v - 0.01f));
                st.SetNormal(-n.Normalized());
                st.SetUV(new Vector2(u, v));
                st.AddVertex(At(u, v));
            }
        for (var j = 0; j < down; j++)
            for (var i = 0; i < across; i++)
            {
                int a = j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                st.AddIndex(a); st.AddIndex(c); st.AddIndex(b);
                st.AddIndex(b); st.AddIndex(c); st.AddIndex(d);
            }
        st.GenerateTangents();
        return st.Commit();
    }

    /// <summary>Turns a Y-long piece to lie along a direction.</summary>
    static MeshInstance3D Aim(MeshInstance3D piece, Vector3 along)
    {
        var d = along.Normalized();
        var axis = Vector3.Up.Cross(d);
        if (axis.LengthSquared() > 1e-6f) piece.Basis = new Basis(new Quaternion(axis.Normalized(), Vector3.Up.AngleTo(d))) * Basis.FromScale(piece.Scale);
        else if (d.Y < 0) piece.Basis = new Basis(new Quaternion(Vector3.Right, Mathf.Pi)) * Basis.FromScale(piece.Scale);
        return piece;
    }
}
