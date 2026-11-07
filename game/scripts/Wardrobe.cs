using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// Dresses a hero in what they wear. Every KayKit adventurer shares one skeleton, and each carries its gear on bone
/// slots (the right hand, the left, the head, the chest), so a piece can be borrowed from any of them: a knight's
/// helmet for a mage, a barbarian's greataxe for a ranger. The hero's own pieces in that slot are put away, the
/// borrowed one goes on, painted (steel bright, dragonbone pale, a cinder axe black with embers in it). Body armour
/// paints the body itself: leather brown, mail grey, plate bright, dragonscale in its dragon's colour.
/// </summary>
public static class Wardrobe
{
    /// <summary>The slot each kind of gear goes on, and the names of the hero's own pieces it replaces there.</summary>
    static readonly Dictionary<Slot, (string Bone, string[] Own)> Slots = new()
    {
        [Slot.Weapon] = ("handslot.r", ["1H_Sword", "2H_Sword", "1H_Axe", "2H_Axe", "Knife", "1H_Crossbow", "2H_Crossbow", "1H_Wand", "2H_Staff", "Mug", "Throwable"]),
        [Slot.Offhand] = ("handslot.l", ["Shield", "Spellbook", "_Offhand"]),
        [Slot.Head] = ("head", ["Helmet", "_Hat"]),
        [Slot.Cloak] = ("chest", ["_Cape"]),
    };

    static readonly Dictionary<string, Node3D?> Sources = [];

    /// <summary>Puts the pack's worn gear on a figure (one built fresh from its look).</summary>
    public static void Wear(Figure f, Pack pack)
    {
        if (f.IsHuman) { f.WearHuman(pack, Piece); return; } // a real body: gear in real hands, armour and helms painted on
        if (f.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault() is not { } skeleton) return;
        var meshes = f.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
        foreach (var (slot, (bone, own)) in Slots)
        {
            if (pack.WornIn(slot) is not { } item) continue;
            foreach (var m in meshes.Where(m => own.Any(o => m.Name.ToString().Contains(o)))) m.Visible = false;
            var piece = Piece(item);
            if (piece is null) continue;
            Holder(skeleton, bone).AddChild(piece);
            if (slot == Slot.Weapon) f.Held = item.Grip;
        }
        if (pack.WornIn(Slot.Body) is { Tint: { } tint } armour)
            foreach (var m in meshes.Where(m => m.Visible && (m.Name.ToString().Contains("_Body") || m.Name.ToString().Contains("_Arm") || m.Name.ToString().Contains("_Leg"))))
                Paint(m, new Color(tint), armour.Metal, armour.Glow);
    }

    static Node3D Holder(Skeleton3D skeleton, string bone) =>
        skeleton.GetChildren().OfType<BoneAttachment3D>().FirstOrDefault(b => b.BoneName == bone) is { } found ? found : Attach(skeleton, bone);

    static BoneAttachment3D Attach(Skeleton3D skeleton, string bone)
    {
        var a = new BoneAttachment3D { BoneName = bone };
        skeleton.AddChild(a);
        return a;
    }

    /// <summary>The piece for an item: borrowed from the model that has it ("Knight/Knight_Helmet"), or grown.</summary>
    static Node3D? Piece(Item item)
    {
        if (item.Look is null) return null;
        if (item.Look == "graft:crown") return Crown(item);
        var parts = item.Look.Split('/');
        if (parts.Length != 2) return null;
        if (!Sources.TryGetValue(parts[0], out var source))
            Sources[parts[0]] = source = Models.Scene($"characters/{parts[0]}.glb")?.Instantiate() as Node3D;
        if (source?.FindChild(parts[1], true, false) is not MeshInstance3D original) return null;
        if (KitDesigns.Build(item, original.Transform) is { } designed) return designed; // the game's own make of it
        var copy = new MeshInstance3D { Mesh = original.Mesh, Transform = original.Transform, Name = $"Worn_{item.Id}" };
        Styled.Apply(copy);
        if ((item.Tint ?? (item.Forged ? "#c8ccd4" : null)) is { } tint) Paint(copy, new Color(tint), item.Metal, item.Glow, item.Forged ? 0.22f : 0.9f);
        return item.Forged && item.Slot == Slot.Weapon ? Parted(copy, item) : copy;
    }

    /// <summary>A forged weapon's own shape: its blade drawn out or broadened, and a guard, a wrapped grip and a pommel of its own put on, measured off the piece.</summary>
    static Node3D Parted(MeshInstance3D blade, Item item)
    {
        blade.Scale = new Vector3(blade.Scale.X * item.Width, blade.Scale.Y * item.Length, blade.Scale.Z * item.Width);
        var box = blade.GetAabb();
        var (low, tall, across) = (box.Position.Y, box.Size.Y, Math.Max(0.02f, Math.Min(box.Size.X, box.Size.Z)));
        var hilt = low + tall * 0.2f;
        static StandardMaterial3D Made(string? tint, string fallback, bool metal = true) => new() { AlbedoColor = new Color(tint ?? fallback), Metallic = metal ? 0.7f : 0, Roughness = metal ? 0.35f : 0.8f };
        if (item.GuardWidth > 0)
            blade.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(tall * 0.2f * item.GuardWidth, tall * 0.022f, across * 1.2f) }, MaterialOverride = Made(item.GuardTint, "#8a8f98"), Position = new Vector3(0, hilt, 0) });
        blade.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = across * 0.42f, BottomRadius = across * 0.42f, Height = tall * 0.13f, RadialSegments = 10 }, MaterialOverride = Made(item.GripTint, "#5a3e28", metal: false), Position = new Vector3(0, low + tall * 0.11f, 0) });
        blade.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = across * 0.62f, Height = across * 1.24f, RadialSegments = 12, Rings = 6 }, MaterialOverride = Made(item.PommelTint, "#8a8f98"), Position = new Vector3(0, low + tall * 0.025f, 0) });
        return blade;
    }

    /// <summary>Paints a piece (or the body): its own texture with the colour multiplied in, shining if metal,
    /// glowing if it has a glow.</summary>
    public static void Paint(MeshInstance3D m, Color tint, bool metal, string? glow, float strength = 0.9f)
    {
        for (var s = 0; s < m.GetSurfaceOverrideMaterialCount(); s++)
            m.SetSurfaceOverrideMaterial(s, Styled.Painted(m.GetActiveMaterial(s), tint, glow, glow is null ? 0 : strength, metal ? true : null));
    }

    /// <summary>The Goblin King's crown, cut down to a hero's head: a gold band and its spikes, a red stone at the front.</summary>
    static Node3D Crown(Item item)
    {
        var gold = new StandardMaterial3D { AlbedoColor = new Color(item.Tint ?? "#e0b040"), Metallic = 0.9f, Roughness = 0.3f };
        var crown = new Node3D { Name = $"Worn_{item.Id}", Position = new Vector3(0, 0.62f, 0) };
        crown.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.33f, Height = 0.14f, RadialSegments = 14 }, MaterialOverride = gold });
        for (var i = 0; i < 7; i++)
        {
            var a = i * Mathf.Tau / 7;
            crown.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 0.05f, Height = 0.22f, RadialSegments = 4 }, MaterialOverride = gold,
                Position = new Vector3(Mathf.Cos(a) * 0.34f, 0.17f, Mathf.Sin(a) * 0.34f),
            });
        }
        crown.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#c01818"), EmissionEnabled = true, Emission = new Color("#c01818"), EmissionEnergyMultiplier = 1.2f },
            Position = new Vector3(0, 0.02f, 0.36f),
        });
        return crown;
    }
}
