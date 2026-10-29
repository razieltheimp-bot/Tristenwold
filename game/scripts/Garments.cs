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
    public sealed record Kit(string? Body = null, string? Head = null, bool Belt = true, bool Pauldrons = false, bool Bracers = false, bool Cloak = false, bool Tabard = false, bool MetalBracers = false);

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
        "Warlock" or "Sorcerer" or "Alchemist" or "scholar" or "laureate" or "Poet" or "Scribe" => new("robe", null, Belt: false),
        "priest" => new("robe", "hood", Belt: false),
        "pirate" or "sailor" => new(null, null, Bracers: true),
        "pirate_captain" => new("jerkin", null, Bracers: true, Cloak: true),
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
    public static List<Node> Dress(Skeleton3D sk, string look, Humans.Outfit outfit, float top, (Color Tint, bool Metal)? armour = null, Color? helm = null)
    {
        var made = new List<Node>();
        if (Measured(sk, top) is not { } m) return made;
        var kit = For(look);
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
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.07f, 0.09f, 0.045f) }, leather, new Vector3(-r * 0.75f, m.Hips.Y - 0.01f, r * 0.5f), new Vector3(0, 30, 0))); // a pouch
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
                else chest.AddChild(Part(new BoxMesh { Size = new Vector3(0.02f, torsoH * 0.6f, 0.012f) }, Armoury.Leather("#2a1e14"), new Vector3(0, centreY, m.ShoulderHalf * 0.78f + 0.004f))); // the lacing
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
                        arm.AddChild(Part(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.1f, Height = 0.24f, RadialSegments = 16 }, Robe(outfit), at, scale: Vector3.One) is var sleeve ? Aim(sleeve, toHand) : null!);
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

        if (kit.Tabard)
        {
            var chest = Hang(sk, "Chest", made);
            var heraldry = Armoury.Cloth("#" + outfit.Tunic.Lightened(0.25f).ToHtml(false));
            var w = m.ShoulderHalf * 1.1f;
            chest.AddChild(Part(new BoxMesh { Size = new Vector3(w, torsoH * 0.62f, 0.01f) }, heraldry, new Vector3(0, m.Hips.Y + torsoH * 0.45f, m.ShoulderHalf * 0.82f + 0.012f)));
            chest.AddChild(Part(new BoxMesh { Size = new Vector3(w * 0.22f, torsoH * 0.5f, 0.004f) }, Armoury.Cloth("#a82a2a"), new Vector3(0, m.Hips.Y + torsoH * 0.45f, m.ShoulderHalf * 0.82f + 0.02f)));
            chest.AddChild(Part(new BoxMesh { Size = new Vector3(w * 0.7f, torsoH * 0.12f, 0.004f) }, Armoury.Cloth("#a82a2a"), new Vector3(0, m.Hips.Y + torsoH * 0.55f, m.ShoulderHalf * 0.82f + 0.02f)));
            var hips = Hang(sk, "Hips", made);
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(w * 0.9f, 0.3f, 0.01f) }, heraldry, new Vector3(0, m.Hips.Y - 0.12f, m.HipHalf + 0.04f)));
            hips.AddChild(Part(new BoxMesh { Size = new Vector3(w * 0.9f, 0.3f, 0.01f) }, heraldry, new Vector3(0, m.Hips.Y - 0.12f, -(m.HipHalf + 0.04f))));
        }

        if (kit.Pauldrons)
            foreach (var side in new[] { "Left", "Right" })
            {
                var i = sk.FindBone($"{side}UpperArm");
                if (i < 0) continue;
                var arm = Hang(sk, $"{side}UpperArm", made);
                var at = sk.GetBoneGlobalRest(i).Origin;
                var mat = kit.Body == "fur" ? Armoury.Leather("#6a4a30") : kit.Body == "jerkin" ? hide : steel;
                arm.AddChild(Part(new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 20, Rings = 10 }, mat, at + new Vector3(side == "Left" ? 0.02f : -0.02f, 0.03f, 0), scale: new Vector3(1.1f, 0.75f, 1)));
            }

        if (kit.Bracers)
            foreach (var side in new[] { "Left", "Right" })
            {
                var i = sk.FindBone($"{side}LowerArm");
                if (i < 0) continue;
                var arm = Hang(sk, $"{side}LowerArm", made);
                var rest = sk.GetBoneGlobalRest(i);
                var toHand = rest.Basis.Y.Normalized();
                var mat = kit.MetalBracers ? steel : leather;
                var bracer = Part(new CylinderMesh { TopRadius = 0.042f, BottomRadius = 0.05f, Height = 0.19f, RadialSegments = 16 }, mat, rest.Origin + toHand * 0.15f);
                arm.AddChild(Aim(bracer, toHand));
            }

        if (kit.Cloak)
        {
            var chest = Hang(sk, "Chest", made);
            var cape = Armoury.Cloth("#" + outfit.Trousers.Darkened(0.15f).ToHtml(false));
            var length = m.Neck.Y - 0.25f;
            chest.AddChild(Part(new BoxMesh { Size = new Vector3(m.ShoulderHalf * 2.3f, length, 0.012f) }, cape, new Vector3(0, m.Neck.Y - length / 2 - 0.02f, -(m.ShoulderHalf * 0.85f + 0.02f)), new Vector3(-4, 0, 0)));
        }

        switch (kit.Head)
        {
            case "hood":
            {
                var head = Hang(sk, "Head", made);
                var hood = Armoury.Cloth("#" + (outfit.Cap ?? outfit.Tunic.Darkened(0.25f)).ToHtml(false));
                var size = m.Top - m.Head.Y;
                // The hood sits back on the head so the face comes out of its opening: its front edge at the cheeks, the crown over the top.
                var centre = new Vector3(0, m.Head.Y + size * 0.5f, -0.075f);
                head.AddChild(Part(new SphereMesh { Radius = 0.125f, Height = 0.25f, RadialSegments = 24, Rings = 12 }, hood, centre, scale: new Vector3(1.05f, 1.15f, 0.9f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.125f, Rings = 28, RingSegments = 8 }, hood, new Vector3(0, m.Head.Y + size * 0.45f, 0.035f), new Vector3(90, 0, 0), new Vector3(1, 1, 1.2f))); // the opening, framing the face
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.13f, Rings = 24, RingSegments = 8 }, hood, new Vector3(0, m.Neck.Y + 0.02f, 0), scale: new Vector3(1, 1.3f, 1))); // the cowl at the neck
                break;
            }
            case "cap":
            {
                var head = Hang(sk, "Head", made);
                var mat = helm is { } h ? Armoury.Metal(h, 0.42f) : steel;
                head.AddChild(Part(new SphereMesh { Radius = 0.118f, Height = 0.236f, RadialSegments = 24, Rings = 12 }, mat, new Vector3(0, m.Top - 0.09f, -0.01f), scale: new Vector3(1, 0.8f, 1.05f)));
                head.AddChild(Part(new TorusMesh { InnerRadius = 0.105f, OuterRadius = 0.128f, Rings = 28, RingSegments = 8 }, Armoury.Iron, new Vector3(0, m.Top - 0.11f, -0.01f), scale: new Vector3(1, 1.4f, 1.05f))); // the rim
                head.AddChild(Part(new BoxMesh { Size = new Vector3(0.016f, 0.08f, 0.02f) }, Armoury.Iron, new Vector3(0, m.Top - 0.13f, 0.11f))); // a nasal
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
        return made;
    }

    static Material Robe(Humans.Outfit outfit) => new StandardMaterial3D { AlbedoColor = outfit.Tunic, Roughness = 0.95f, MetallicSpecular = 0.1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

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
