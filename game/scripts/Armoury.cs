using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Gear built to real proportions for real hands: swords with a tapered, fullered blade, a crossguard, a wrapped grip
/// and a pommel; knives; axes with a forged head on an ash haft; staffs; wands; crossbows; shields; a mug and a
/// spellbook. All of it in physically based materials (steel, iron, bronze, wood, leather, brass) rather than the
/// flat colours of the cartoon figures' kit, and each piece sized as the thing would be. Every piece is built with
/// its grip at the origin and its length along +Y (a blade's edges along X, its flat facing Z), and
/// <see cref="InHand"/> turns it into a hand's own frame: the Godot humanoid hand has its fingers along +Y, its palm
/// facing +Z and (on the right) its thumb along +X, so a gripped blade runs along X.
/// </summary>
public static class Armoury
{
    /// <summary>Steel, iron, bronze and the rest, with a little scratch-roughness; a tint paints the metal (dragonbone pale, a cinder axe black).</summary>
    public static StandardMaterial3D Metal(Color? tint = null, float rough = 0.32f, string? glow = null) => new()
    {
        AlbedoColor = tint ?? new Color("#c4c8cc"), Metallic = 0.92f, Roughness = rough, MetallicSpecular = 0.75f,
        AnisotropyEnabled = true, Anisotropy = 0.35f,
        EmissionEnabled = glow is not null, Emission = glow is null ? Colors.Black : new Color(glow), EmissionEnergyMultiplier = 0.8f,
    };
    public static StandardMaterial3D Iron => Metal(new Color("#8a8c90"), 0.48f);
    public static StandardMaterial3D Bronze => Metal(new Color("#b08a4a"), 0.4f);
    public static StandardMaterial3D Brass => Metal(new Color("#c8a048"), 0.36f);
    public static StandardMaterial3D Wood(string hex = "#6a4a2c") => new() { AlbedoColor = new Color(hex), Roughness = 0.82f, MetallicSpecular = 0.2f };
    public static StandardMaterial3D Leather(string hex = "#3a2a1c") => new() { AlbedoColor = new Color(hex), Roughness = 0.9f, MetallicSpecular = 0.15f };
    public static StandardMaterial3D Cloth(string hex) => new() { AlbedoColor = new Color(hex), Roughness = 0.95f, MetallicSpecular = 0.1f };

    static MeshInstance3D Part(Mesh mesh, Material m, Vector3 at, Vector3? turn = null, Vector3? scale = null) => new()
    {
        Mesh = mesh, MaterialOverride = m, Position = at, RotationDegrees = turn ?? Vector3.Zero, Scale = scale ?? Vector3.One,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
    };

    /// <summary>A blade: a long box tapering to the point (a prism), with a fuller down the middle and a bevelled edge suggested by a thinner spine.</summary>
    static Node3D Blade(float length, float width, float thick, Material steel, float taper = 0.55f)
    {
        var blade = new Node3D { Name = "Blade" };
        var body = length * 0.82f;
        // The body of the blade narrows a little toward the point; the last fifth is the point itself.
        blade.AddChild(Part(new PrismMesh { Size = new Vector3(width, body, thick), LeftToRight = 0.5f }, steel, new Vector3(0, body / 2, 0), new Vector3(0, 0, 0), new Vector3(1, 1, 1)));
        blade.AddChild(Part(new PrismMesh { Size = new Vector3(width, body, thick), LeftToRight = 0.5f }, steel, new Vector3(0, body / 2, 0), new Vector3(180, 0, 0)));
        blade.AddChild(Part(new PrismMesh { Size = new Vector3(width * taper, length - body, thick * 0.8f), LeftToRight = 0.5f }, steel, new Vector3(0, body + (length - body) / 2, 0), new Vector3(0, 0, 0), new Vector3(1, 1, 1)));
        blade.AddChild(Part(new PrismMesh { Size = new Vector3(width * taper, length - body, thick * 0.8f), LeftToRight = 0.5f }, steel, new Vector3(0, body + (length - body) / 2, 0), new Vector3(180, 0, 0)));
        // The point: a flat wedge.
        blade.AddChild(Part(new PrismMesh { Size = new Vector3(width * taper, (length - body) * 0.6f, thick * 0.8f), LeftToRight = 0.5f }, steel, new Vector3(0, length - (length - body) * 0.3f, 0), new Vector3(0, 0, 0), new Vector3(1, 0.6f, 1)));
        // A fuller: a shallow dark groove down each face.
        var groove = new StandardMaterial3D { AlbedoColor = ((StandardMaterial3D)steel).AlbedoColor.Darkened(0.35f), Metallic = 0.9f, Roughness = 0.5f };
        foreach (var side in new[] { -1f, 1f })
            blade.AddChild(Part(new BoxMesh { Size = new Vector3(width * 0.18f, body * 0.85f, thick * 0.3f) }, groove, new Vector3(0, body * 0.5f, side * thick * 0.42f)));
        return blade;
    }

    /// <summary>A grip wrapped in leather, with a pommel below and a guard above.</summary>
    static void Hilt(Node3D into, float gripLength, float gripRadius, Material guardMetal, float guardWidth, float guardThick, string leather = "#3a2a1c")
    {
        into.AddChild(Part(new CylinderMesh { TopRadius = gripRadius, BottomRadius = gripRadius * 1.08f, Height = gripLength, RadialSegments = 12 }, Leather(leather), new Vector3(0, 0, 0)));
        for (var i = 0; i < 5; i++) // the wrap's turns
            into.AddChild(Part(new TorusMesh { InnerRadius = gripRadius * 0.95f, OuterRadius = gripRadius * 1.12f, Rings = 14, RingSegments = 8 }, Leather("#2a1e14"), new Vector3(0, -gripLength / 2 + gripLength * (i + 0.5f) / 5, 0)));
        into.AddChild(Part(new BoxMesh { Size = new Vector3(guardWidth, guardThick, guardThick * 1.6f) }, guardMetal, new Vector3(0, gripLength / 2 + guardThick / 2, 0)));
        into.AddChild(Part(new SphereMesh { Radius = gripRadius * 1.5f, Height = gripRadius * 3, RadialSegments = 14, Rings = 8 }, guardMetal, new Vector3(0, -gripLength / 2 - gripRadius * 1.2f, 0)));
    }

    public static Node3D Sword(bool twoHanded, Color? tint = null, string? glow = null)
    {
        var steel = Metal(tint, 0.3f, glow);
        var piece = new Node3D { Name = twoHanded ? "2H_Sword" : "1H_Sword" };
        var (grip, length, width) = twoHanded ? (0.26f, 1.05f, 0.052f) : (0.12f, 0.78f, 0.046f);
        Hilt(piece, grip, 0.016f, Iron, twoHanded ? 0.26f : 0.19f, 0.016f);
        var blade = Blade(length, width, 0.006f, steel);
        blade.Position = new Vector3(0, grip / 2 + 0.016f, 0);
        piece.AddChild(blade);
        return piece;
    }

    public static Node3D Knife(Color? tint = null)
    {
        var piece = new Node3D { Name = "Knife" };
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.013f, BottomRadius = 0.015f, Height = 0.1f, RadialSegments = 10 }, Wood("#4a3220"), Vector3.Zero));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.008f, 0.014f) }, Iron, new Vector3(0, 0.054f, 0)));
        var blade = Blade(0.24f, 0.03f, 0.004f, Metal(tint, 0.3f), 0.45f);
        blade.Position = new Vector3(0, 0.058f, 0);
        piece.AddChild(blade);
        return piece;
    }

    public static Node3D Axe(bool twoHanded, Color? tint = null, string? glow = null)
    {
        var piece = new Node3D { Name = twoHanded ? "2H_Axe" : "1H_Axe" };
        var haft = twoHanded ? 1.1f : 0.62f;
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.019f, Height = haft, RadialSegments = 10 }, Wood("#7a5a34"), new Vector3(0, haft / 2 - 0.1f, 0)));
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.14f, RadialSegments = 10 }, Leather(), new Vector3(0, 0, 0)));
        var head = Metal(tint, 0.45f, glow);
        var y = haft - 0.16f;
        // The head: a socket round the haft and a flared bit, bevelled to the edge.
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.028f, Height = 0.1f, RadialSegments = 10 }, head, new Vector3(0, y, 0)));
        foreach (var side in twoHanded ? new[] { -1f, 1f } : [1f])
        {
            piece.AddChild(Part(new PrismMesh { Size = new Vector3(0.16f, 0.14f, 0.012f), LeftToRight = 1 }, head, new Vector3(side * 0.085f, y, 0), new Vector3(0, 0, side * 90)));
            piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.09f, 0.06f, 0.02f) }, head, new Vector3(side * 0.045f, y, 0)));
        }
        if (!twoHanded) piece.AddChild(Part(new PrismMesh { Size = new Vector3(0.05f, 0.05f, 0.02f) }, head, new Vector3(-0.035f, y, 0), new Vector3(0, 0, -90))); // a hammer poll behind
        return piece;
    }

    public static Node3D Staff(string? glow = null)
    {
        var piece = new Node3D { Name = "2H_Staff" };
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.02f, Height = 1.7f, RadialSegments = 10 }, Wood("#5a3e26"), new Vector3(0, 0.45f, 0)));
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.022f, Height = 0.3f, RadialSegments = 10 }, Leather(), new Vector3(0, 0, 0)));
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.022f, Height = 0.05f, RadialSegments = 8 }, Iron, new Vector3(0, -0.4f, 0)));
        // A knotted head holding a stone that glows.
        for (var i = 0; i < 3; i++)
            piece.AddChild(Part(new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.014f, Height = 0.16f, RadialSegments = 7 }, Wood("#4a3220"), new Vector3(Mathf.Cos(i * 2.1f) * 0.03f, 1.36f, Mathf.Sin(i * 2.1f) * 0.03f), new Vector3(Mathf.Sin(i * 2.1f) * 25, 0, -Mathf.Cos(i * 2.1f) * 25)));
        var stone = new StandardMaterial3D { AlbedoColor = new Color(glow ?? "#7fb8ff"), EmissionEnabled = true, Emission = new Color(glow ?? "#7fb8ff"), EmissionEnergyMultiplier = 1.6f, Roughness = 0.2f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        stone.AlbedoColor = stone.AlbedoColor with { A = 0.85f };
        piece.AddChild(Part(new SphereMesh { Radius = 0.045f, Height = 0.09f }, stone, new Vector3(0, 1.4f, 0)));
        piece.AddChild(new OmniLight3D { Position = new Vector3(0, 1.4f, 0), LightColor = new Color(glow ?? "#7fb8ff"), OmniRange = 2.5f, LightEnergy = 0.5f, ShadowEnabled = false });
        return piece;
    }

    public static Node3D Wand(string? glow = null)
    {
        var piece = new Node3D { Name = "1H_Wand" };
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.012f, Height = 0.38f, RadialSegments = 8 }, Wood("#3a2a1c"), new Vector3(0, 0.12f, 0)));
        piece.AddChild(Part(new TorusMesh { InnerRadius = 0.01f, OuterRadius = 0.016f }, Brass, new Vector3(0, 0.04f, 0)));
        var stone = new StandardMaterial3D { AlbedoColor = new Color(glow ?? "#d0a0ff"), EmissionEnabled = true, Emission = new Color(glow ?? "#d0a0ff"), EmissionEnergyMultiplier = 1.4f, Roughness = 0.2f };
        piece.AddChild(Part(new SphereMesh { Radius = 0.016f, Height = 0.032f }, stone, new Vector3(0, 0.32f, 0)));
        return piece;
    }

    public static Node3D Crossbow(bool heavy)
    {
        var piece = new Node3D { Name = heavy ? "2H_Crossbow" : "1H_Crossbow" };
        var length = heavy ? 0.62f : 0.42f;
        var span = heavy ? 0.6f : 0.4f;
        // The tiller (the stock) lies along +Y from the grip; the bow across it at the far end, in the X direction.
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.035f, length, 0.045f) }, Wood("#5a3e26"), new Vector3(0, length / 2 - 0.08f, 0)));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.12f, 0.06f) }, Wood("#4a3220"), new Vector3(0, -0.06f, -0.02f), new Vector3(20, 0, 0))); // the grip and butt
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.012f, length * 0.6f, 0.01f) }, Iron, new Vector3(0, length * 0.5f - 0.02f, 0.028f))); // the groove's iron rail
        foreach (var side in new[] { -1f, 1f })
            piece.AddChild(Part(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.014f, Height = span / 2, RadialSegments = 8 }, Metal(new Color("#6a6c70"), 0.5f), new Vector3(side * span / 4, length - 0.1f, 0.01f), new Vector3(0, 0, side * 84)));
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.003f, Height = span * 0.96f, RadialSegments = 5 }, Cloth("#e8e0c8"), new Vector3(0, length - 0.14f, 0.012f), new Vector3(0, 0, 90))); // the string
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.06f, RadialSegments = 8 }, Iron, new Vector3(0, length * 0.3f, 0.005f), new Vector3(0, 0, 90))); // the nut's pin
        return piece;
    }

    /// <summary>A round shield: planked wood faced with hide, an iron rim and boss. Built facing +Z, its centre at the origin.</summary>
    public static Node3D RoundShield(Color? tint = null, bool metal = false)
    {
        var piece = new Node3D { Name = "Round_Shield" };
        var face = metal ? Metal(tint, 0.4f) : Leather(tint?.ToHtml(false) is { } h ? "#" + h : "#7a4a2c");
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.025f, RadialSegments = 36 }, face, Vector3.Zero, new Vector3(90, 0, 0)));
        piece.AddChild(Part(new TorusMesh { InnerRadius = 0.34f, OuterRadius = 0.375f, Rings = 36, RingSegments = 8 }, Iron, Vector3.Zero, new Vector3(90, 0, 0)));
        piece.AddChild(Part(new SphereMesh { Radius = 0.08f, Height = 0.16f }, Iron, new Vector3(0, 0, 0.02f), scale: new Vector3(1, 1, 0.55f)));
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8;
            piece.AddChild(Part(new SphereMesh { Radius = 0.012f, Height = 0.024f }, Iron, new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, 0.014f)));
        }
        // Planking lines on the face, and a grip bar behind.
        foreach (var x in new[] { -0.18f, 0, 0.18f })
            piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.006f, 0.6f, 0.004f) }, Leather("#2a1e14"), new Vector3(x, 0, 0.015f)));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.2f, 0.03f) }, Wood("#4a3220"), new Vector3(0, 0, -0.03f)));
        return piece;
    }

    /// <summary>A heater shield: a steel face with a painted badge. Built facing +Z.</summary>
    public static Node3D HeaterShield(Color? tint = null, bool metal = true)
    {
        var piece = new Node3D { Name = "Badge_Shield" };
        var face = metal ? Metal(tint ?? new Color("#aab0b8"), 0.42f) : Wood(tint?.ToHtml(false) is { } h ? "#" + h : "#6a4a2c");
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.46f, 0.3f, 0.025f) }, face, new Vector3(0, 0.2f, 0)));
        piece.AddChild(Part(new PrismMesh { Size = new Vector3(0.46f, 0.36f, 0.025f), LeftToRight = 0.5f }, face, new Vector3(0, -0.13f, 0), new Vector3(0, 0, 180)));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.5f, 0.03f, 0.03f) }, Iron, new Vector3(0, 0.355f, 0)));
        var badge = Cloth("#a82a2a");
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.3f, 0.006f) }, badge, new Vector3(0, 0.08f, 0.015f)));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.24f, 0.06f, 0.006f) }, badge, new Vector3(0, 0.14f, 0.015f)));
        piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.2f, 0.03f) }, Leather(), new Vector3(0, 0.05f, -0.03f)));
        return piece;
    }

    public static Node3D Mug()
    {
        var piece = new Node3D { Name = "Mug" };
        var pewter = Metal(new Color("#9a9c98"), 0.55f);
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.04f, Height = 0.13f, RadialSegments = 14 }, pewter, new Vector3(0, 0.03f, 0)));
        piece.AddChild(Part(new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.035f, Rings = 14, RingSegments = 8 }, pewter, new Vector3(0.055f, 0.03f, 0), new Vector3(0, 90, 0)));
        piece.AddChild(Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.01f, RadialSegments = 14 }, Cloth("#c8a050"), new Vector3(0, 0.09f, 0)));
        return piece;
    }

    public static Node3D Spellbook(bool open)
    {
        var piece = new Node3D { Name = open ? "Spellbook_open" : "Spellbook" };
        var cover = Leather("#4a2a3a");
        var pages = Cloth("#e8dcc0");
        if (!open)
        {
            piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.17f, 0.23f, 0.045f) }, cover, Vector3.Zero));
            piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.16f, 0.22f, 0.036f) }, pages, new Vector3(0.008f, 0, 0)));
            piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.02f, 0.06f, 0.05f) }, Brass, new Vector3(0.08f, 0, 0)));
        }
        else
        {
            foreach (var side in new[] { -1f, 1f })
            {
                piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.17f, 0.23f, 0.012f) }, cover, new Vector3(side * 0.085f, 0, 0), new Vector3(0, 0, side * -12)));
                piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.16f, 0.22f, 0.02f) }, pages, new Vector3(side * 0.082f, 0, 0.014f), new Vector3(0, 0, side * -12)));
            }
            var ink = new StandardMaterial3D { AlbedoColor = new Color("#8fd0ff"), EmissionEnabled = true, Emission = new Color("#8fd0ff"), EmissionEnergyMultiplier = 0.8f };
            for (var i = 0; i < 4; i++) piece.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.006f, 0.002f) }, ink, new Vector3(-0.08f, 0.07f - i * 0.04f, 0.027f)));
        }
        return piece;
    }

    /// <summary>A piece by its KayKit name (the looks and the gear items name them so), painted if a tint is given.</summary>
    public static Node3D? Make(string id, Color? tint = null, bool metal = false, string? glow = null) => id switch
    {
        "1H_Sword" => Sword(false, tint, glow),
        "2H_Sword" => Sword(true, tint, glow),
        "Knife" or "Knife_Offhand" => Knife(tint),
        "1H_Axe" => Axe(false, tint, glow),
        "2H_Axe" => Axe(true, tint, glow),
        "2H_Staff" => Staff(glow),
        "1H_Wand" => Wand(glow),
        "1H_Crossbow" => Crossbow(false),
        "2H_Crossbow" => Crossbow(true),
        "Round_Shield" or "Skeleton_Shield_Small_A" => RoundShield(tint, metal),
        "Badge_Shield" or "Spike_Shield" or "Rectangle_Shield" or "Skeleton_Shield_Large_A" => HeaterShield(tint, metal || tint is null),
        "Mug" => Mug(),
        "Spellbook" => Spellbook(false),
        "Spellbook_open" => Spellbook(true),
        "Skeleton_Blade" => Sword(false, new Color("#8a8c88"), glow),
        "Skeleton_Axe" => Axe(false, new Color("#8a8c88"), glow),
        "Skeleton_Staff" => Staff(glow ?? "#7fe0d8"),
        _ => null,
    };

    /// <summary>Whether a piece is worn on the forearm rather than gripped.</summary>
    public static bool IsShield(string id) => id.Contains("Shield");

    /// <summary>
    /// Puts a piece into a real hand's own frame. The hand bone's fingers run along +Y, its palm faces +Z and its
    /// thumb (on the right hand) along +X, so a gripped piece's length (+Y) goes along X, out past the thumb, with
    /// the edge toward the knuckles; a shield sits on the back of the forearm, its face out.
    /// </summary>
    public static Node3D InHand(string bone, Node3D piece)
    {
        var left = bone.StartsWith("Left");
        var hold = new Node3D { Name = $"Hold_{piece.Name}" };
        if (IsShield(piece.Name))
        {
            // The face (+Z) turned to the back of the hand (-Z), the shield's up (+Y) along the thumb; set back onto the forearm.
            hold.Basis = new Basis(new Vector3(left ? -1 : 1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1)) * Basis.FromEuler(new Vector3(0, 0, Mathf.DegToRad(90)));
            hold.Position = new Vector3(0, -0.12f, -0.07f);
        }
        else
        {
            // Columns: where the piece's X, Y and Z go in the hand. A proper rotation each (determinant +1).
            hold.Basis = left
                ? new Basis(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1))
                : new Basis(new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1));
            hold.Position = new Vector3(0, 0.04f, 0.025f); // into the palm, where the fingers close
        }
        hold.AddChild(piece);
        return hold;
    }
}
