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
        "Stormdruid" or "Spiritcaller" => new("robe", null, Belt: false, Mantle: true),
        "Shapeshifter" => new("fur", null, Bracers: true),
        "Warlock" or "Sorcerer" or "scholar" or "laureate" or "Poet" or "Scribe" => new("robe", null, Belt: false),
        "priest" => new("robe", "hood", Belt: false),
        "pirate" or "sailor" or "monkey_hand" => new(null, "bandana", Bracers: true),
        "turlin" or "giant_ape" or "ape_elder" or "ape_warrior" => new(null, null, Belt: false),
        _ when look.StartsWith("soren_") => new(null, null, Bracers: true),
        _ when look.StartsWith("lu_") => new(null, null, Belt: false),
        "pirate_captain" or "dread_soren" => new("jerkin", "bandana", Bracers: true, Cloak: true),
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
    public static List<Node> Dress(Skeleton3D sk, string look, Humans.Outfit outfit, float top, (Color Tint, bool Metal)? armour = null, Color? helm = null, string wear = "Class", (string Style, Color Colour)? cut = null, (string Race, string Form)? kin = null)
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
        if (look == "turlin") Rival(sk, m, made);
        if (look == "giant_ape") GreatApe(sk, m, made);
        if (look is "ape_elder" or "ape_warrior") Kin(sk, m, "ape", "", false, made); // an Apekin's muzzle, brow, ears and tail on the furred body
        if (look.StartsWith("soren_")) Soren(sk, m, int.Parse(look[6..]), made);
        if (look.StartsWith("lu_")) Wukong(sk, m, int.Parse(look[3..]), made);
        if (cut is { } style && kit.Head is null && outfit.Cap is null) Hair(sk, m, style.Style, style.Colour, made);
        if (kin is { } people) Kin(sk, m, people.Race, people.Form, kit.Head is not null, made);
        return made;
    }

    /// <summary>
    /// Turlin as the hero first meets him: a man, covered in black fur but for his face and an open patch down his chest,
    /// with long black hair in great spikes, a black monkey's tail (one turn of it wrapped round his waist), long blue
    /// plates at his hips and forearms and blue boots, all edged in white, and the spiked fruit in his hand.
    /// </summary>
    static void Rival(Skeleton3D sk, Measure m, List<Node> made)
    {
        var fur = new StandardMaterial3D { AlbedoColor = new Color("#17151a"), Roughness = 0.95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var skin = Cloth(new Color("#6a4638"));
        var (plate, edge) = (Cloth(new Color("#3f5c96")), Cloth(new Color("#dfe1e8")));
        var torsoH = m.Neck.Y - m.Hips.Y;
        var chest = Hang(sk, "Chest", made);
        var front = m.ShoulderHalf * 0.8f;
        // The bare chest, and the fur standing up along its edges.
        chest.AddChild(Part(Drape(m.ShoulderHalf * 1.25f, torsoH * 0.76f, m.ShoulderHalf * 0.44f, 0.1f), skin, new Vector3(0, m.Hips.Y + torsoH * 0.92f, front + 0.012f)));
        for (var i = 0; i < 20; i++)
        {
            var (t, side) = (i / 20f, i % 2 == 0 ? -1f : 1f);
            var at = new Vector3(side * m.ShoulderHalf * (0.64f - 0.26f * t), m.Hips.Y + torsoH * (0.88f - 0.66f * t), front - 0.01f);
            var way = new Vector3(-side * 0.3f, -0.8f, 0.5f).Normalized();
            chest.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.022f, Height = 0.09f, RadialSegments = 6 }, fur, at + way * 0.04f), way));
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            if (sk.FindBone($"{side}UpperArm") is var ui && ui >= 0)
            {
                var rest = sk.GetBoneGlobalRest(ui);
                var along = rest.Basis.Y.Normalized();
                Hang(sk, $"{side}UpperArm", made).AddChild(Aim(Part(new CapsuleMesh { Radius = 0.082f, Height = 0.4f, RadialSegments = 14, Rings = 6 }, fur, rest.Origin + along * 0.13f), along));
            }
            if (sk.FindBone($"{side}LowerArm") is var li && li >= 0)
            {
                var rest = sk.GetBoneGlobalRest(li);
                var along = rest.Basis.Y.Normalized();
                var arm = Hang(sk, $"{side}LowerArm", made);
                arm.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.072f, Height = 0.22f, RadialSegments = 18 }, plate, rest.Origin + along * 0.16f), along));
                arm.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.058f, BottomRadius = 0.058f, Height = 0.03f, RadialSegments = 18 }, edge, rest.Origin + along * 0.27f), along));
                arm.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.078f, BottomRadius = 0.1f, Height = 0.05f, RadialSegments = 18 }, edge, rest.Origin + along * 0.05f), along)); // the white flare at the elbow
            }
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            // He's a heavy man: thick legs in black fur, in tall blue boots with a white cuff.
            if (sk.FindBone($"{side}UpperLeg") is var ti && ti >= 0)
            {
                var rest = sk.GetBoneGlobalRest(ti);
                var along = rest.Basis.Y.Normalized();
                Hang(sk, $"{side}UpperLeg", made).AddChild(Aim(Part(new CapsuleMesh { Radius = 0.095f, Height = 0.46f, RadialSegments = 14, Rings = 6 }, fur, rest.Origin + along * 0.19f), along));
            }
            if (sk.FindBone($"{side}LowerLeg") is var si && si >= 0)
            {
                var rest = sk.GetBoneGlobalRest(si);
                var along = rest.Basis.Y.Normalized();
                var shin = Hang(sk, $"{side}LowerLeg", made);
                shin.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.058f, BottomRadius = 0.082f, Height = 0.32f, RadialSegments = 18 }, plate, rest.Origin + along * 0.22f), along));
                shin.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.03f, RadialSegments = 18 }, edge, rest.Origin + along * 0.06f), along));
            }
            if (sk.FindBone($"{side}Foot") is var fi && fi >= 0)
            {
                var rest = sk.GetBoneGlobalRest(fi);
                var along = rest.Basis.Y.Normalized();
                Hang(sk, $"{side}Foot", made).AddChild(Aim(Part(new CapsuleMesh { Radius = 0.062f, Height = 0.27f, RadialSegments = 14, Rings = 6 }, plate, rest.Origin + along * 0.07f), along));
            }
        }
        var hips = Hang(sk, "Hips", made);
        var r0 = m.HipHalf + 0.01f;
        hips.AddChild(Part(new TorusMesh { InnerRadius = r0 - 0.035f, OuterRadius = r0 + 0.03f, Rings = 36, RingSegments = 12 }, fur, new Vector3(0, m.Hips.Y + 0.11f, 0), scale: new Vector3(1, 1.1f, 0.86f))); // a turn of his tail
        var flap = Drape(m.HipHalf * 0.6f, m.Hips.Y * 0.56f, m.HipHalf * 0.2f, 0.14f, 8, 12, lean: 0.04f);
        foreach (var side in new[] { -1f, 1f })
        {
            // Long plates front and side, split at the middle so a leg shows between them, each edged white.
            hips.AddChild(Part(flap, plate, new Vector3(side * m.HipHalf * 0.56f, m.Hips.Y + 0.08f, r0 * 0.86f), new Vector3(0, side * 14, side * 5)));
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.02f, m.Hips.Y * 0.54f, 0.02f) }, edge, new Vector3(side * m.HipHalf * 0.3f, m.Hips.Y * 0.8f, r0 * 0.98f), new Vector3(0, 0, side * 5)));
            hips.AddChild(Part(flap, plate, new Vector3(side * r0, m.Hips.Y + 0.08f, -0.02f), new Vector3(0, side * 90, 0)));
        }
        // The rest of the tail: long, thin and black, coming out behind and curling up at the end.
        var (at0, way0) = (new Vector3(0, m.Hips.Y - 0.02f, -m.HipHalf * 0.85f), new Vector3(0, -0.6f, -1).Normalized());
        for (var i = 0; i < 14; i++)
        {
            var part = Part(new CylinderMesh { TopRadius = 0.03f - i * 0.0016f, BottomRadius = 0.033f - i * 0.0016f, Height = 0.15f, RadialSegments = 10 }, fur, at0 + way0 * 0.075f);
            hips.AddChild(Aim(part, way0));
            at0 += way0 * 0.135f;
            way0 = (way0 + new Vector3(0, i > 6 ? 0.34f : 0.12f, i > 9 ? 0.3f : 0)).Normalized();
        }
        if (sk.FindBone("RightHand") >= 0)
        {
            var rest = sk.GetBoneGlobalRest(sk.FindBone("RightHand"));
            var hand = Hang(sk, "RightHand", made);
            var red = new StandardMaterial3D { AlbedoColor = new Color("#7a2416"), Roughness = 0.55f };
            var at = rest.Origin + rest.Basis.Y.Normalized() * 0.12f + rest.Basis.Z.Normalized() * 0.03f;
            hand.AddChild(Part(new SphereMesh { Radius = 0.058f, Height = 0.116f, RadialSegments = 12, Rings = 8 }, red, at));
            for (var i = 0; i < 8; i++)
            {
                var way = new Vector3(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 1.7f), Mathf.Sin(i * 2.4f)).Normalized();
                hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.001f, BottomRadius = 0.012f, Height = 0.04f, RadialSegments = 6 }, red, at + way * 0.066f), way));
            }
        }
        Hair(sk, m, "Wildmane", new Color("#121014"), made);
    }

    /// <summary>
    /// The Dread Pirate Soren, and what the sea has made of him: a long red coat open over a bare chest, a beard like a black
    /// storm, a feathered tricorn, a spiked iron gauntlet and the black blade. The further gone he is, the more of it's
    /// barnacle, coral and weed, grown on him and not hung about him, and by the end there's more sea than man.
    /// </summary>
    static void Soren(Skeleton3D sk, Measure m, int form, List<Node> made)
    {
        var sea = Tristinwold.Gameplay.Legend.FormOf(form).Sea;
        var torsoH = m.Neck.Y - m.Hips.Y;
        var (chest, hips, head) = (Hang(sk, "Chest", made), Hang(sk, "Hips", made), Hang(sk, "Head", made));
        var coat = Cloth(new Color("#9a1f1a").Lerp(new Color("#3a2a2a"), sea * 0.7f));
        var lining = Cloth(new Color("#e8e0d0").Lerp(new Color("#6a6a58"), sea));
        var skin = Cloth(new Color("#b08a70").Lerp(new Color("#5a6a68"), sea));
        var iron = Armoury.Metal(new Color("#3a3a40"), 0.5f);
        var brass = Armoury.Brass;
        var hair = new StandardMaterial3D { AlbedoColor = new Color("#121014").Lerp(new Color("#1f3a30"), sea), Roughness = 0.7f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var black = new StandardMaterial3D { AlbedoColor = new Color("#141216"), Metallic = 0.6f, Roughness = 0.35f };
        var front = m.ShoulderHalf * 0.82f;
        var length = m.Hips.Y * 0.86f;

        // The coat: long tails behind and at the sides, broad lapels, and the chest bare between them.
        chest.AddChild(Part(Drape(m.ShoulderHalf * 1.1f, torsoH * 0.78f, m.ShoulderHalf * 0.4f, 0.1f), skin, new Vector3(0, m.Hips.Y + torsoH * 0.9f, front + 0.01f)));
        foreach (var side in new[] { -1f, 1f })
        {
            chest.AddChild(Part(Drape(m.ShoulderHalf * 0.5f, torsoH * 1.02f, m.ShoulderHalf * 0.16f, 0.12f), coat, new Vector3(side * m.ShoulderHalf * 0.82f, m.Hips.Y + torsoH * 0.92f, front + 0.02f), new Vector3(0, side * 20, 0)));
            for (var i = 0; i < 3; i++)
                chest.AddChild(Part(new SphereMesh { Radius = 0.016f, Height = 0.032f, RadialSegments = 8, Rings = 4 }, brass, new Vector3(side * m.ShoulderHalf * 0.72f, m.Hips.Y + torsoH * (0.3f + i * 0.2f), front + 0.05f)));
            hips.AddChild(Part(Drape(m.HipHalf * 1.05f, length, m.HipHalf * 0.3f, 0.2f, 10, 14, lean: 0.06f), coat, new Vector3(side * (m.HipHalf + 0.05f), m.Hips.Y + 0.1f, -0.01f), new Vector3(0, side * 80, 0)));
            hips.AddChild(Part(Drape(m.HipHalf * 0.6f, length * 0.92f, m.HipHalf * 0.18f, 0.22f, 8, 12, lean: 0.05f), lining, new Vector3(side * m.HipHalf * 0.72f, m.Hips.Y + 0.08f, m.HipHalf + 0.03f), new Vector3(0, side * 22, 0)));
        }
        chest.AddChild(Part(Drape(m.ShoulderHalf * 2.2f, torsoH * 1.02f, m.ShoulderHalf * 0.5f, 0.1f), coat, new Vector3(0, m.Hips.Y + torsoH * 0.95f, -front - 0.01f), new Vector3(0, 180, 0)));
        hips.AddChild(Part(Drape(m.HipHalf * 2.6f, length, m.HipHalf * 0.5f, 0.24f, 12, 16, lean: 0.08f), coat, new Vector3(0, m.Hips.Y + 0.1f, -m.HipHalf - 0.05f), new Vector3(0, 180, 0)));
        foreach (var side in new[] { "Left", "Right" })
            if (sk.FindBone($"{side}UpperArm") is var ui && ui >= 0)
            {
                var rest = sk.GetBoneGlobalRest(ui);
                var along = rest.Basis.Y.Normalized();
                Hang(sk, $"{side}UpperArm", made).AddChild(Aim(Part(new CapsuleMesh { Radius = 0.072f, Height = 0.36f, RadialSegments = 12, Rings = 5 }, coat, rest.Origin + along * 0.13f), along));
            }

        // The beard, and the tricorn with its feathers. What was beard turns to weed.
        var chin = new Vector3(0, m.Top - 0.17f, 0.085f);
        for (var i = -4; i <= 4; i++)
        {
            var way = new Vector3(i * 0.1f, -1, 0.26f).Normalized();
            var lockOf = Part(new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.045f, Height = 0.3f + (4 - Math.Abs(i)) * 0.03f + sea * 0.22f, RadialSegments = 7 }, hair, chin + new Vector3(i * 0.024f, 0.02f, 0) + way * 0.15f);
            head.AddChild(lockOf);
            Aim(lockOf, way);
        }
        head.AddChild(Part(new SphereMesh { Radius = 0.09f, Height = 0.15f, RadialSegments = 12, Rings = 6 }, hair, chin + new Vector3(0, 0.015f, -0.01f), scale: new Vector3(1.05f, 0.8f, 0.72f)));
        var felt = Cloth(new Color("#2a1a18").Lerp(new Color("#1e2a26"), sea), span: 0);
        var brim = new Vector3(0, m.Top + 0.03f, -0.03f);
        head.AddChild(Part(new CylinderMesh { TopRadius = 0.115f, BottomRadius = 0.13f, Height = 0.12f, RadialSegments = 18 }, felt, brim + Vector3.Up * 0.05f));
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3 + Mathf.Pi / 2;
            head.AddChild(Part(new BoxMesh { Size = new Vector3(0.34f, 0.1f, 0.012f) }, felt, brim + new Vector3(Mathf.Cos(a) * 0.15f, 0.04f, Mathf.Sin(a) * 0.15f), new Vector3(-30, Mathf.RadToDeg(-a) + 90, 0)));
        }
        if (sea < 0.8f)
        {
            var plume = Cloth(new Color("#e8e4d8"));
            head.AddChild(Part(new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.03f, Height = 0.42f, RadialSegments = 6 }, plume, brim + new Vector3(0.12f, 0.15f, -0.03f), new Vector3(35, 0, -68)));
        }
        if (sea < 0.4f)
            head.AddChild(Part(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.14f, RadialSegments = 6 }, Armoury.Leather("#3a2418"), new Vector3(0.05f, m.Top - 0.165f, 0.16f), new Vector3(70, 0, 15))); // the pipe

        // The iron fist on his left, and the black blade in his right.
        if (sk.FindBone("LeftHand") >= 0)
        {
            var rest = sk.GetBoneGlobalRest(sk.FindBone("LeftHand"));
            var hand = Hang(sk, "LeftHand", made);
            hand.AddChild(Part(new SphereMesh { Radius = 0.1f, Height = 0.18f, RadialSegments = 12, Rings = 6 }, iron, rest.Origin + rest.Basis.Y.Normalized() * 0.07f));
            for (var i = 0; i < 7; i++)
            {
                var way = new Vector3(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 1.7f), Mathf.Sin(i * 2.4f)).Normalized();
                hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.022f, Height = 0.09f, RadialSegments = 6 }, iron, rest.Origin + rest.Basis.Y.Normalized() * 0.07f + way * 0.12f), way));
            }
        }
        if (sk.FindBone("RightHand") >= 0)
        {
            var rest = sk.GetBoneGlobalRest(sk.FindBone("RightHand"));
            var hand = Hang(sk, "RightHand", made);
            var grip = rest.Origin + rest.Basis.Y.Normalized() * 0.08f;
            var way = rest.Basis.Z.Normalized() * -1;
            hand.AddChild(Aim(Part(new BoxMesh { Size = new Vector3(0.13f, 1.25f, 0.03f) }, black, grip + way * 0.68f), way));
            hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.022f, Height = 0.26f, RadialSegments = 8 }, Armoury.Leather("#2a1a14"), grip), way));
            var across = way.Cross(rest.Basis.Y.Normalized()).Normalized();
            for (var i = 0; i < 6; i++)
            {
                var hook = (across * (i % 2 == 0 ? 1 : -1) + way * 0.5f).Normalized();
                hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.05f, Height = 0.2f, RadialSegments = 5 }, black, grip + way * (0.2f + i * 0.2f) + across * (i % 2 == 0 ? 0.08f : -0.08f)), hook));
            }
        }

        if (sea <= 0) return;
        // What the sea's done: barnacles on the skin and coat, coral on the shoulders, weed trailing from him.
        var rng = new Tristinwold.Deviant.Rng(1234);
        var crust = new StandardMaterial3D { AlbedoColor = new Color("#8a8d80"), Roughness = 1 };
        var coral = new StandardMaterial3D { AlbedoColor = new Color("#c0705a"), Roughness = 0.9f };
        for (var i = 0; i < 46 * sea; i++)
        {
            var (a, y) = ((float)rng.NextDouble() * Mathf.Tau, m.Hips.Y + torsoH * (float)rng.NextDouble());
            var at = new Vector3(Mathf.Cos(a) * m.ShoulderHalf * 0.9f, y, Mathf.Sin(a) * m.ShoulderHalf * 0.72f);
            chest.AddChild(Part(new SphereMesh { Radius = 0.03f, Height = 0.036f, RadialSegments = 7, Rings = 3 }, sea > 0.4f && i % 3 == 0 ? coral : crust, at));
        }
        for (var i = 0; i < 30 * sea; i++)
        {
            var a = (float)rng.NextDouble() * Mathf.Tau;
            var way = new Vector3(Mathf.Cos(a) * 0.2f, -1, Mathf.Sin(a) * 0.2f).Normalized();
            var strand = Part(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.002f, Height = 0.3f + 0.5f * sea, RadialSegments = 5 }, hair, new Vector3(Mathf.Cos(a) * m.HipHalf * 1.25f, m.Hips.Y - 0.05f - (float)rng.NextDouble() * 0.3f, Mathf.Sin(a) * m.HipHalf * 1.2f) + way * 0.2f);
            hips.AddChild(strand);
            Aim(strand, way);
        }
        if (sea >= 0.45f)
            foreach (var side in new[] { -1f, 1f })
                head.AddChild(Part(new SphereMesh { Radius = 0.018f, Height = 0.03f, RadialSegments = 8, Rings = 4 }, new StandardMaterial3D { AlbedoColor = new Color("#04080c"), Roughness = 0.05f }, new Vector3(side * 0.036f, m.Top - 0.115f, 0.118f)));
        if (sea >= 0.65f)
            foreach (var side in new[] { -1f, 1f })
                for (var i = 0; i < 4; i++)
                {
                    var way = new Vector3(side * 0.6f, -0.8f, 0.2f + i * 0.1f).Normalized();
                    var arm = Part(new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.03f, Height = 0.4f, RadialSegments = 6 }, hair, new Vector3(side * 0.08f, m.Top - 0.2f - i * 0.02f, 0.08f) + way * 0.2f);
                    head.AddChild(arm);
                    Aim(arm, way);
                }
    }

    /// <summary>
    /// A wild head of hair: hundreds of long, curved, tapering locks gathered in tufts, flung up and out from the crown and
    /// falling thick down his back and over his shoulders, all one mesh, streaked along its length so it reads as strands.
    /// </summary>
    static void Mane(Node3D head, Vector3 crown, Color colour, float big = 1)
    {
        var (mesh, hair) = _mane ??= MakeMane(colour);
        head.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = hair, Position = crown, Scale = Vector3.One * big });
    }

    /// <summary>Made once and shared: every Turlin wears the same head of hair, in whatever shape he's come back.</summary>
    static (ArrayMesh, StandardMaterial3D)? _mane;

    static (ArrayMesh, StandardMaterial3D) MakeMane(Color colour)
    {
        var crown = Vector3.Zero;
        var streaks = new NoiseTexture2D
        {
            Width = 256, Height = 256, Seamless = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.08f },
            ColorRamp = new Gradient { Offsets = [0, 1], Colors = [new Color(0.18f, 0.18f, 0.2f), new Color(2.4f, 2.4f, 2.6f)] },
        };
        var strands = new NoiseTexture2D { Width = 256, Height = 256, Seamless = true, AsNormalMap = true, BumpStrength = 14, Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.08f } };
        var hair = new StandardMaterial3D
        {
            AlbedoColor = colour.Lightened(0.09f), AlbedoTexture = streaks, Uv1Scale = new Vector3(3, 0.05f, 1), Roughness = 0.46f, MetallicSpecular = 0.5f,
            NormalEnabled = true, NormalTexture = strands, NormalScale = 1.4f,
            RimEnabled = true, Rim = 0.12f, RimTint = 0.5f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new Tristinwold.Deviant.Rng(11);
        float Rand(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        void Lock(Vector3 root, Vector3 way, Vector3 sweep, float length, float width)
        {
            const int rings = 6, sides = 5;
            way = way.Normalized();
            var flat = way.Cross(Vector3.Forward).Length() > 0.1f ? way.Cross(Vector3.Forward).Normalized() : Vector3.Right;
            var deep = way.Cross(flat).Normalized();
            Vector3 Point(int r, int s)
            {
                var t = r / (float)rings;
                var centre = root + way * length * t + sweep * length * t * t;
                var girth = width * Mathf.Pow(1 - t, 0.75f) * (0.6f + 0.4f * Mathf.Sin(Mathf.Min(t * 3, 1) * Mathf.Pi / 2));
                var a = s * Mathf.Tau / sides;
                return centre + flat * Mathf.Cos(a) * girth + deep * Mathf.Sin(a) * girth * 0.55f;
            }
            for (var r = 0; r < rings; r++)
                for (var s = 0; s < sides; s++)
                    foreach (var (i, j) in new[] { (r, s), (r, s + 1), (r + 1, s + 1), (r, s), (r + 1, s + 1), (r + 1, s) })
                    {
                        st.SetUV(new Vector2(j / (float)sides + root.X * 3 + root.Z * 2, i / (float)rings * length * 4));
                        st.AddVertex(Point(i, j));
                    }
        }
        // One big solid lock, with a few finer ones lying close along it so it isn't a smooth cone.
        void Tuft(Vector3 root, Vector3 way, Vector3 sweep, float length, float width, int n)
        {
            Lock(root, way, sweep, length, width);
            for (var i = 0; i < n; i++)
            {
                var aim = way.Normalized() + new Vector3(Rand(-0.075f, 0.075f), Rand(-0.075f, 0.075f), Rand(-0.075f, 0.075f));
                Lock(root + new Vector3(Rand(-0.04f, 0.04f), Rand(-0.02f, 0.02f), Rand(-0.04f, 0.04f)), aim, sweep, length * Rand(0.5f, 0.88f), width * Rand(0.26f, 0.6f));
            }
        }
        // Up and out from the crown, thick, each tuft curling away at its point.
        foreach (var (x, y, z, length) in new[] { (0f, 1f, -0.1f, 0.5f), (-0.5f, 0.9f, -0.2f, 0.44f), (0.45f, 0.95f, -0.25f, 0.5f), (-0.9f, 0.55f, -0.3f, 0.46f), (0.9f, 0.5f, -0.35f, 0.44f), (-1f, 0.15f, -0.4f, 0.4f), (1f, 0.1f, -0.45f, 0.42f), (-0.9f, -0.2f, -0.5f, 0.36f), (0.9f, -0.25f, -0.5f, 0.36f), (0.2f, 0.8f, -0.8f, 0.42f), (-0.3f, 0.5f, -0.9f, 0.42f) })
        {
            var way = new Vector3(x, y, z).Normalized();
            Tuft(crown + way * 0.075f + Vector3.Up * 0.03f, way, new Vector3(x * 0.1f, 0.05f, -0.08f), length, 0.135f, 10);
        }
        // Down his back in a great thick fall.
        for (var i = -3; i <= 3; i++)
            Tuft(crown + new Vector3(i * 0.04f, 0.02f, -0.09f), new Vector3(i * 0.1f, -1, -0.3f), new Vector3(i * 0.02f, 0, 0.14f), 0.76f + (3 - Math.Abs(i)) * 0.05f, 0.13f, 9);
        // Forward over his shoulders, framing the face, and a few locks at the brow.
        foreach (var side in new[] { -1f, 1f })
        {
            Tuft(crown + new Vector3(side * 0.125f, 0, 0), new Vector3(side * 0.16f, -1, 0.14f), new Vector3(side * 0.02f, 0, 0.1f), 0.56f, 0.095f, 7);
            Tuft(crown + new Vector3(side * 0.1f, 0.07f, 0.07f), new Vector3(side * 0.5f, -0.75f, 0.2f), new Vector3(0, -0.1f, 0), 0.18f, 0.045f, 1);
        }
        Tuft(crown + new Vector3(-0.03f, 0.1f, 0.095f), new Vector3(-0.45f, 0.4f, 0.75f), new Vector3(-0.2f, -0.4f, -0.1f), 0.2f, 0.055f, 2);
        // The thick of it on the crown: an ellipsoid of the same locks, sitting back from the brow so the face shows.
        for (var r = 0; r < 12; r++)
            for (var c = 0; c < 20; c++)
                foreach (var (i, j) in new[] { (r, c), (r, c + 1), (r + 1, c + 1), (r, c), (r + 1, c + 1), (r + 1, c) })
                {
                    var (up, round) = (i * Mathf.Pi / 12, j * Mathf.Tau / 20);
                    st.SetUV(new Vector2(j / 5f, i * 0.5f));
                    st.AddVertex(new Vector3(Mathf.Sin(up) * Mathf.Cos(round) * 0.154f, Mathf.Cos(up) * 0.135f + 0.05f, Mathf.Sin(up) * Mathf.Sin(round) * 0.157f - 0.05f));
                }
        st.GenerateNormals();
        return (st.Commit(), hair);
    }

    /// <summary>
    /// What Turlin comes back as: a great ape stood up on a man's frame, all brown fur and muscle with a bare, lined face
    /// and a short crest. The frame is a person's, so it stands on its own feet, walks and swings like one.
    /// </summary>
    static void GreatApe(Skeleton3D sk, Measure m, List<Node> made, Color? coat = null, Color? eyes = null)
    {
        var turlin = coat is null;
        var fur = new StandardMaterial3D
        {
            AlbedoColor = coat ?? new Color("#57362a"), Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Uv1Scale = new Vector3(5, 7, 1),
            AlbedoTexture = new NoiseTexture2D { Width = 256, Height = 256, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.09f }, ColorRamp = new Gradient { Offsets = [0, 1], Colors = [new Color(0.45f, 0.42f, 0.4f), new Color(1.3f, 1.25f, 1.2f)] } },
            NormalEnabled = true, NormalScale = 1.1f,
            NormalTexture = new NoiseTexture2D { Width = 256, Height = 256, Seamless = true, AsNormalMap = true, BumpStrength = 9, Noise = new FastNoiseLite { Frequency = 0.14f } },
        };
        var bare = new StandardMaterial3D { AlbedoColor = new Color("#a98470"), Roughness = 0.8f };
        // A muscle: a long swelling on a bone, at a share of the way along it, pushed off to one side of it.
        void Muscle(string bone, float from, float radius, float length, Vector3 off, Material paint)
        {
            if (sk.FindBone(bone) is var i && i < 0) return;
            var rest = sk.GetBoneGlobalRest(i);
            var along = rest.Basis.Y.Normalized();
            Hang(sk, bone, made).AddChild(Aim(Part(new SphereMesh { Radius = radius, Height = length, RadialSegments = 18, Rings = 10 }, paint, rest.Origin + along * from + off), along));
        }
        void Mass(Node3D on, Vector3 where, float radius, float height, Vector3? scale = null) => on.AddChild(Part(new SphereMesh { Radius = radius, Height = height, RadialSegments = 18, Rings = 10 }, fur, where, scale: scale));
        var chest = Hang(sk, "Chest", made);
        var torsoH = m.Neck.Y - m.Hips.Y;
        var y = m.Hips.Y + torsoH * 0.72f;
        Mass(chest, new Vector3(0, m.Hips.Y + torsoH * 0.58f, 0), m.ShoulderHalf * 0.86f, torsoH * 1.15f, new Vector3(1.05f, 1, 0.8f)); // the trunk, narrowing to the waist
        Mass(chest, new Vector3(0, y, -0.1f), 0.17f, torsoH * 0.8f, new Vector3(1.2f, 1, 0.7f)); // the back
        Mass(chest, m.Neck + new Vector3(0, -0.03f, -0.05f), 0.16f, 0.22f, new Vector3(1.5f, 1, 0.9f)); // the traps
        foreach (var side in new[] { -1f, 1f })
        {
            Mass(chest, new Vector3(side * 0.105f, y + 0.02f, 0.135f), 0.135f, 0.2f, new Vector3(1.05f, 1, 0.62f)); // a slab of chest
            Mass(chest, new Vector3(side * m.ShoulderHalf * 0.84f, y - 0.1f, -0.02f), 0.07f, torsoH * 0.56f, new Vector3(0.9f, 1, 1)); // a lat
            for (var i = 0; i < 3; i++)
                Mass(chest, new Vector3(side * 0.05f, m.Hips.Y + torsoH * (0.1f + i * 0.17f), 0.125f - i * 0.0f), 0.05f, 0.075f, new Vector3(1, 1, 0.6f)); // the belly, in blocks
            Mass(chest, new Vector3(side * (m.ShoulderHalf + 0.05f), m.Neck.Y - 0.1f, 0), 0.135f, 0.22f); // a shoulder
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            var (fwd, back) = (new Vector3(0, 0, 0.05f), new Vector3(0, 0, -0.05f));
            Muscle($"{side}UpperArm", 0.13f, 0.082f, 0.32f, fwd, fur); // biceps
            Muscle($"{side}UpperArm", 0.16f, 0.086f, 0.34f, back, fur); // triceps
            Muscle($"{side}LowerArm", 0.1f, 0.095f, 0.3f, Vector3.Zero, fur);
            Muscle($"{side}LowerArm", 0.2f, 0.066f, 0.3f, new Vector3(0, 0, 0.02f), fur);
            Muscle($"{side}Hand", 0.07f, 0.082f, 0.17f, Vector3.Zero, bare);
            Muscle($"{side}UpperLeg", 0.19f, 0.105f, 0.42f, new Vector3(0, 0, 0.045f), fur); // the front of the thigh
            Muscle($"{side}UpperLeg", 0.2f, 0.09f, 0.4f, new Vector3(side == "Left" ? 0.05f : -0.05f, 0, 0), fur);
            Muscle($"{side}UpperLeg", 0.2f, 0.09f, 0.38f, new Vector3(0, 0, -0.05f), fur); // the back of it
            Muscle($"{side}LowerLeg", 0.13f, 0.092f, 0.3f, new Vector3(0, 0, -0.04f), fur); // a calf
            Muscle($"{side}LowerLeg", 0.2f, 0.066f, 0.42f, new Vector3(0, 0, 0.01f), fur);
            Muscle($"{side}Foot", 0.06f, 0.075f, 0.28f, Vector3.Zero, bare);
        }
        // What's left of his plates: the long blue ones at his waist, edged in white.
        var (plate, edge) = (Cloth(new Color("#3f5c96")), Cloth(new Color("#dfe1e8")));
        var hips = Hang(sk, "Hips", made);
        Mass(hips, new Vector3(0, m.Hips.Y + 0.02f, -0.01f), m.HipHalf * 1.15f, 0.32f, new Vector3(1, 1, 0.9f));
        var r0 = m.HipHalf * 1.15f;
        if (turlin) hips.AddChild(Part(new TorusMesh { InnerRadius = r0 - 0.025f, OuterRadius = r0 + 0.03f, Rings = 36, RingSegments = 10 }, edge, new Vector3(0, m.Hips.Y + 0.13f, 0), scale: new Vector3(1, 0.8f, 0.92f)));
        var flap = Drape(m.HipHalf * 0.72f, m.Hips.Y * 0.5f, m.HipHalf * 0.2f, 0.14f, 8, 12, lean: 0.04f);
        foreach (var side in turlin ? new[] { -1f, 1f } : [])
        {
            hips.AddChild(Part(flap, plate, new Vector3(side * m.HipHalf * 0.6f, m.Hips.Y + 0.1f, r0 * 0.96f), new Vector3(0, side * 14, side * 5)));
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.02f, m.Hips.Y * 0.48f, 0.02f) }, edge, new Vector3(side * m.HipHalf * 0.3f, m.Hips.Y * 0.84f, r0 * 1.08f), new Vector3(0, 0, side * 5)));
            hips.AddChild(Part(flap, plate, new Vector3(side * r0, m.Hips.Y + 0.1f, -0.02f), new Vector3(0, side * 90, 0)));
        }
        var head = Hang(sk, "Head", made);
        var at = new Vector3(0, m.Top - 0.11f, 0.01f);
        Mass(head, at, 0.15f, 0.3f);
        Mass(Hang(sk, "Neck", made), m.Neck + new Vector3(0, 0.03f, -0.03f), 0.13f, 0.24f); // the great slope of the neck
        void Face(Vector3 where, float r, Vector3 scale, Material paint) => head.AddChild(Part(new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 16, Rings = 10 }, paint, at + where, scale: scale));
        Face(new Vector3(0, -0.045f, 0.125f), 0.09f, new Vector3(1.15f, 0.8f, 0.95f), bare); // the muzzle
        Face(new Vector3(0, 0.03f, 0.128f), 0.075f, new Vector3(1.25f, 0.62f, 0.5f), bare); // round the eyes
        Face(new Vector3(0, 0.075f, 0.13f), 0.07f, new Vector3(1.5f, 0.3f, 0.5f), fur); // the brow
        var eye = new StandardMaterial3D { AlbedoColor = eyes ?? new Color("#c81a20"), Roughness = 0.2f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color("#1c1210"), Roughness = 0.7f };
        foreach (var side in new[] { -1f, 1f })
        {
            Face(new Vector3(side * 0.05f, 0.042f, 0.178f), 0.017f, Vector3.One, eye);
            Face(new Vector3(side * 0.024f, -0.03f, 0.205f), 0.011f, Vector3.One, dark); // a nostril
            Face(new Vector3(side * 0.155f, 0.03f, 0), 0.05f, new Vector3(0.4f, 1.1f, 0.85f), bare); // an ear
        }
        Face(new Vector3(0, -0.09f, 0.19f), 0.06f, new Vector3(1, 0.1f, 0.5f), dark); // the mouth
        // The same hair he had as a man, made for a bigger head.
        if (turlin) Mane(head, new Vector3(0, m.Top - 0.075f, -0.016f) + new Vector3(0, 0.02f, 0), new Color("#121014"), 1.3f);
        // His tail, loose and long, out behind him and curling up at the end.
        var (root, tail) = (new Vector3(0, m.Hips.Y, -m.HipHalf * 1.2f), new Vector3(0.25f, -0.55f, -1).Normalized());
        var (links, thin) = turlin ? (20, 1f) : (11, 0.7f); // a shorter tail on a smaller ape
        for (var i = 0; i < links; i++)
        {
            hips.AddChild(Aim(Part(new CylinderMesh { TopRadius = (0.052f - i * 0.002f) * thin, BottomRadius = (0.055f - i * 0.002f) * thin, Height = 0.16f, RadialSegments = 10 }, fur, root + tail * 0.08f), tail));
            root += tail * 0.14f;
            tail = (tail + new Vector3(i > links * 0.4f ? 0.12f : 0.02f, i > links * 0.4f ? 0.26f : 0.08f, i > links * 0.7f ? 0.24f : 0)).Normalized();
        }
    }

    /// <summary>
    /// Lu Wukong, and what the gourd has left of him: a brown ape with amber eyes, in a patched blue hood and a red-and-gold
    /// sash, the gourd at his hip and a chained staff in his hand. The further gone he is, the greyer and the bigger he is:
    /// the hood splits and comes down to a rag, the gourd goes to his teeth and then it's gone, the sash goes, and the staff
    /// gives way to a broken yard-arm with the ship's chain round his neck.
    /// </summary>
    static void Wukong(Skeleton3D sk, Measure m, int form, List<Node> made)
    {
        var beast = Tristinwold.Gameplay.Legend.WukongOf(form).Beast;
        GreatApe(sk, m, made, new Color("#6e5238").Lerp(new Color("#77767a"), beast), new Color("#e8a020").Lerp(new Color("#a02818"), beast));
        var (chest, hips, head) = (Hang(sk, "Chest", made), Hang(sk, "Hips", made), Hang(sk, "Head", made));
        var torsoH = m.Neck.Y - m.Hips.Y;
        var at = new Vector3(0, m.Top - 0.11f, 0.01f);
        var hood = Armoury.Cloth("#" + new Color("#2a3a58").Lerp(new Color("#34343a"), beast).ToHtml(false));
        var patches = new[] { Cloth(new Color("#a8853a")), Cloth(new Color("#8a2f24")), Cloth(new Color("#5a6e4a")) };
        var (red, gold) = (Cloth(new Color("#8a2418")), new StandardMaterial3D { AlbedoColor = new Color("#c9a040"), Metallic = 0.7f, Roughness = 0.4f });
        var iron = Armoury.Metal(new Color("#4a4a50"), 0.6f);
        var gourd = new StandardMaterial3D { AlbedoColor = new Color("#b5893c"), Roughness = 0.55f };

        // The hood, thrown round his head and over his shoulders, open at the face, with its patches.
        if (beast < 0.5f)
        {
            var torn = beast > 0.3f ? 0.92f : 1;
            head.AddChild(Part(new SphereMesh { Radius = 0.195f * torn, Height = 0.2f * torn, IsHemisphere = true, RadialSegments = 24, Rings = 10 }, hood, at + new Vector3(0, 0.015f, 0.012f), new Vector3(-62, 0, 0), new Vector3(1, 1.25f, 1)));
            for (var i = 0; i < 6; i++)
            {
                var a = -1.25f + i * 0.5f;
                head.AddChild(Part(new BoxMesh { Size = new Vector3(0.08f, 0.07f, 0.012f) }, patches[i % patches.Length], at + new Vector3(Mathf.Sin(a) * 0.19f, 0.06f + (i % 2) * 0.07f, 0.012f - Mathf.Cos(a) * 0.18f), new Vector3(-24, Mathf.RadToDeg(a), i * 13)));
            }
            chest.AddChild(Part(new TorusMesh { InnerRadius = 0.11f, OuterRadius = 0.22f, Rings = 28, RingSegments = 12 }, hood, m.Neck + new Vector3(0, -0.05f, -0.03f), scale: new Vector3(1.2f, 0.75f, 1)));
            chest.AddChild(Part(Drape(m.ShoulderHalf * 2.1f, torsoH * 0.85f, m.ShoulderHalf * 0.5f, 0.14f), hood, m.Neck + new Vector3(0, 0, -m.ShoulderHalf * 0.86f), new Vector3(0, 180, 0)));
        }
        else if (beast < 0.9f)
            chest.AddChild(Part(Drape(m.ShoulderHalf * 0.8f, torsoH * 0.5f, m.ShoulderHalf * 0.3f, 0.2f), hood, new Vector3(m.ShoulderHalf + 0.17f, m.Neck.Y + 0.08f, -0.03f), new Vector3(0, 100, -10))); // the rag of it on one shoulder

        // The sash, its end hanging down one hip.
        var r0 = m.HipHalf * 1.15f;
        if (beast < 0.7f)
        {
            hips.AddChild(Part(new TorusMesh { InnerRadius = r0 * 0.74f, OuterRadius = r0 * 0.96f, Rings = 36, RingSegments = 10 }, red, new Vector3(0, m.Hips.Y + 0.12f, 0), scale: new Vector3(1, 1.1f, 0.86f)));
            hips.AddChild(Part(Drape(m.HipHalf * 0.4f, m.Hips.Y * 0.5f, m.HipHalf * 0.1f, 0.1f, 6, 10), red, new Vector3(-m.HipHalf * 0.72f, m.Hips.Y + 0.08f, r0 * 0.84f), new Vector3(0, -18, 3)));
        }

        // The gourd: at his hip on a cord, then in his teeth, and at the end, gone.
        void Gourd(Node3D on, Vector3 where, float size)
        {
            on.AddChild(Part(new SphereMesh { Radius = 0.1f * size, Height = 0.2f * size, RadialSegments = 16, Rings = 10 }, gourd, where));
            on.AddChild(Part(new SphereMesh { Radius = 0.062f * size, Height = 0.124f * size, RadialSegments = 14, Rings = 8 }, gourd, where + Vector3.Up * 0.135f * size));
            on.AddChild(Part(new CylinderMesh { TopRadius = 0.022f * size, BottomRadius = 0.026f * size, Height = 0.06f * size, RadialSegments = 8 }, Armoury.Leather("#3a2418"), where + Vector3.Up * 0.21f * size));
        }
        if (beast < 0.5f) Gourd(hips, new Vector3(r0 + 0.07f, m.Hips.Y - 0.1f, 0.05f), 1);
        else if (beast < 0.9f) Gourd(head, at + new Vector3(0, -0.1f, 0.25f), 0.7f);

        // The staff in his right hand, chain wrapped round its head; at the end, a broken yard-arm, and the ship's chain on him.
        if (sk.FindBone("RightHand") >= 0)
        {
            var rest = sk.GetBoneGlobalRest(sk.FindBone("RightHand"));
            var hand = Hang(sk, "RightHand", made);
            var grip = rest.Origin + rest.Basis.Y.Normalized() * 0.08f;
            var way = rest.Basis.Y.Normalized() * -1; // planted like a walking stick: down the arm, up past the shoulder
            var yard = beast >= 0.9f;
            var wood = yard ? Armoury.Leather("#4a3626") : Armoury.Leather("#6a3a24");
            hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = yard ? 0.05f : 0.026f, BottomRadius = yard ? 0.07f : 0.03f, Height = yard ? 2.6f : 2.1f, RadialSegments = 10 }, wood, grip + way * (yard ? 0.42f : 0.3f)), way));
            if (!yard)
            {
                foreach (var along in new[] { -0.74f, 0.02f, 1.34f })
                    hand.AddChild(Aim(Part(new CylinderMesh { TopRadius = 0.036f, BottomRadius = 0.036f, Height = 0.05f, RadialSegments = 10 }, gold, grip + way * along), way));
                var across = way.Cross(rest.Basis.Y.Normalized()).Normalized();
                for (var i = 0; i < 9; i++)
                    hand.AddChild(Part(new TorusMesh { InnerRadius = 0.022f, OuterRadius = 0.04f, Rings = 12, RingSegments = 6 }, iron, grip + way * (0.94f + i * 0.04f) + across * (i % 2 == 0 ? 0.03f : -0.03f)));
            }
        }
        if (beast >= 0.8f)
            for (var i = 0; i < 16; i++)
            {
                var a = i * Mathf.Tau / 16;
                chest.AddChild(Part(new TorusMesh { InnerRadius = 0.022f, OuterRadius = 0.044f, Rings = 12, RingSegments = 6 }, iron, m.Neck + new Vector3(Mathf.Cos(a) * 0.3f, -0.06f - Math.Max(0, Mathf.Sin(a)) * 0.12f, 0.02f + Mathf.Sin(a) * 0.27f), new Vector3(i % 2 == 0 ? 0 : 90, Mathf.RadToDeg(a), 0)));
            }
    }

    /// <summary>What a people grows that isn't a body's: an ape's muzzle and tail, a Photosynthen's leaves or branches and roots, a shroomie's cap.</summary>
    static void Kin(Skeleton3D sk, Measure m, string race, string form, bool covered, List<Node> made)
    {
        var (head, hips) = (Hang(sk, "Head", made), Hang(sk, "Hips", made));
        var face = new Vector3(0, m.Top - 0.13f, 0.1f);
        MeshInstance3D Limb(Vector3 from, Vector3 way, float length, float r0, float r1, Material paint)
        {
            var part = Part(new CylinderMesh { TopRadius = r1, BottomRadius = r0, Height = length, RadialSegments = 10 }, paint, from + way.Normalized() * length / 2);
            return Aim(part, way.Normalized());
        }
        void Blob(Node3D on, Vector3 at, float r, Material paint, Vector3 scale) =>
            on.AddChild(Part(new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 14, Rings = 8 }, paint, at, scale: scale));
        switch (race)
        {
            case "ape":
            {
                var (fur, bare) = (Armoury.Leather("#3e2c20"), Armoury.Leather("#a0806a"));
                Blob(head, face + new Vector3(0, -0.02f, 0.015f), 0.058f, bare, new Vector3(1.05f, 0.82f, 0.9f)); // the muzzle
                Blob(head, face + new Vector3(0, 0.075f, -0.01f), 0.07f, fur, new Vector3(1.3f, 0.32f, 0.5f)); // the brow
                foreach (var side in new[] { -1f, 1f }) Blob(head, new Vector3(side * 0.112f, m.Top - 0.11f, -0.015f), 0.04f, bare, new Vector3(0.45f, 1, 0.9f)); // round ears
                // The tail grows out of the small of the back: a root of fur sunk into the body, then the tail, curling up at the end.
                var at = new Vector3(0, m.Hips.Y - 0.01f, -m.HipHalf * 0.6f);
                var way = new Vector3(0, -0.55f, -1).Normalized();
                Blob(hips, at, 0.045f, fur, new Vector3(1.3f, 0.9f, 1.1f));
                for (var i = 0; i < 7; i++)
                {
                    hips.AddChild(Limb(at, way, 0.15f, 0.03f - i * 0.0022f, 0.028f - i * 0.0022f, fur));
                    at += way * 0.14f;
                    way = (way + new Vector3(0, 0.3f, 0)).Normalized();
                }
                break;
            }
            case "lizard":
            {
                var (scale, belly, dark) = (Armoury.Leather("#4f7a3e"), Armoury.Leather("#c8bc7a"), Armoury.Leather("#1e2a18"));
                // The snout is the mouth: it grows from the whole lower face, so there's no human mouth under it, the upper jaw over
                // the pale lower one with the dark line of the lips between, and the nostrils at its tip.
                Blob(head, face + new Vector3(0, -0.015f, 0.055f), 0.065f, scale, new Vector3(0.9f, 0.6f, 2.6f)); // the upper jaw
                Blob(head, face + new Vector3(0, -0.055f, 0.05f), 0.055f, belly, new Vector3(0.84f, 0.34f, 2.6f)); // the lower jaw
                head.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.006f, 0.26f) }, dark, face + new Vector3(0, -0.042f, 0.08f))); // the mouth, a line from the hinge to the tip
                foreach (var side in new[] { -1f, 1f }) Blob(head, face + new Vector3(side * 0.018f, 0.005f, 0.21f), 0.008f, dark, Vector3.One); // nostrils
                foreach (var side in new[] { -1f, 1f }) Blob(head, new Vector3(side * 0.07f, m.Top - 0.085f, 0.075f), 0.024f, belly, Vector3.One); // the eye-ridges
                // Scales rather than skin: a scaled hood over the whole scalp and down the neck, with the crest along its ridge.
                Blob(head, new Vector3(0, m.Top - 0.09f, -0.02f), 0.125f, scale, new Vector3(1.02f, 0.9f, 1.04f));
                Blob(head, new Vector3(0, m.Top - 0.2f, -0.05f), 0.07f, scale, new Vector3(1.2f, 1, 0.9f));
                for (var i = 0; i < 6; i++) // the crest, down the back of the skull
                    head.AddChild(Limb(new Vector3(0, m.Top - 0.02f - i * 0.03f, -0.03f - i * 0.022f), new Vector3(0, 0.7f, -1), 0.06f, 0.016f, 0.002f, scale));
                // Bony plates down the spine to the tail, and the tail sunk into the hips so it grows out of the body rather than off it.
                for (var i = 0; i < 5; i++) hips.AddChild(Limb(new Vector3(0, m.Hips.Y + 0.3f - i * 0.065f, -m.HipHalf * 0.55f - i * 0.004f), new Vector3(0, 0.5f, -1), 0.05f, 0.02f, 0.003f, scale));
                var at = new Vector3(0, m.Hips.Y + 0.02f, -m.HipHalf * 0.55f);
                var way = new Vector3(0, -0.5f, -1).Normalized();
                Blob(hips, at, 0.095f, scale, new Vector3(1.25f, 0.9f, 1.1f));
                for (var i = 0; i < 9; i++) // the heavy tail, tapering to the ground
                {
                    hips.AddChild(Limb(at, way, 0.17f, 0.09f - i * 0.009f, 0.082f - i * 0.009f, scale));
                    at += way * 0.15f;
                    way = (way + new Vector3(0, 0.05f, 0)).Normalized();
                }
                break;
            }
            case "plant":
            {
                var spring = form != "Fall";
                var (wood, leaf) = (Armoury.Leather(spring ? "#4a3c22" : "#3e3024"), Armoury.Leather("#4f8a30"));
                Blob(head, new Vector3(0, m.Top - 0.08f, -0.016f), 0.123f, wood, new Vector3(1.02f, 0.88f, 1.06f)); // bark over the whole scalp: no hair, ever; the leaves grow from it
                for (var i = 0; i < 12; i++) // the roots, spreading from the hips to the ground
                {
                    var a = i * Mathf.Tau / 12 + 0.2f;
                    var out_ = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    hips.AddChild(Limb(new Vector3(0, m.Hips.Y + 0.02f, 0) + out_ * m.HipHalf * 0.85f, out_ * 0.42f + Vector3.Down, m.Hips.Y + 0.08f, 0.05f, 0.012f, wood));
                }
                if (covered) break;
                for (var i = 0; i < (spring ? 16 : 9); i++)
                {
                    var (a, up) = (i * 2.4f, 0.35f + i % 4 * 0.18f);
                    var way = new Vector3(Mathf.Cos(a) * (1 - up), up, Mathf.Sin(a) * (1 - up)).Normalized();
                    var at = new Vector3(0, m.Top - 0.08f, -0.015f) + way * 0.085f;
                    if (spring) Blob(head, at + way * 0.07f, 0.075f, leaf, new Vector3(1.2f, 0.62f, 1.2f));
                    else head.AddChild(Limb(at, way, 0.2f + i % 3 * 0.07f, 0.016f, 0.003f, wood));
                }
                break;
            }
            case "shroomie":
            {
                // No hair at all: the whole scalp is the stalk's pale flesh under the cap, dusted white with spore.
                var (cap, gills, spot, flesh) = (Armoury.Leather("#a85a3a"), Armoury.Leather("#e4d4b8"), Armoury.Leather("#f0e4cc"), Armoury.Leather("#e8dcc4"));
                Blob(head, new Vector3(0, m.Top - 0.08f, -0.016f), 0.123f, flesh, new Vector3(1.02f, 0.88f, 1.06f));
                if (covered) break;
                var top = new Vector3(0, m.Top + 0.005f, -0.01f);
                head.AddChild(Part(new SphereMesh { Radius = 0.27f, Height = 0.54f, IsHemisphere = true, RadialSegments = 24, Rings = 10 }, cap, top, scale: new Vector3(1, 0.62f, 1)));
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.265f, BottomRadius = 0.2f, Height = 0.03f, RadialSegments = 24 }, gills, top + Vector3.Down * 0.012f));
                for (var i = 0; i < 7; i++)
                {
                    var (a, out_) = (i * 2.4f, i == 0 ? 0 : 0.11f + i % 3 * 0.05f);
                    Blob(head, top + new Vector3(Mathf.Cos(a) * out_, 0.16f - out_ * 0.5f - out_ * out_, Mathf.Sin(a) * out_), 0.03f, spot, new Vector3(1, 0.5f, 1));
                }
                for (var i = 0; i < 14; i++) // spore, white, drifting off the cap's rim and settled on the shoulders
                {
                    var a = i * 1.7f + 0.4f;
                    var r = 0.2f + i % 3 * 0.035f;
                    Blob(head, top + new Vector3(Mathf.Cos(a) * r, -0.06f - i % 4 * 0.03f, Mathf.Sin(a) * r), 0.009f + i % 3 * 0.003f, spot, Vector3.One);
                }
                break;
            }
        }
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
            case "Wildmane":
                Mane(head, crown, colour);
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
