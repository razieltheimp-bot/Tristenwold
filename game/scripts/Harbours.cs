using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// What the two dock towns live on, out past their walls: Druitt's log quay and timber stacks, and Greywater's quay,
/// orchards and berry rows. The land's there whether or not anyone's near, so it's all drawn in a few multimeshes.
/// </summary>
public partial class Harbours(Main main) : Node3D
{
    /// <summary>Greywater's orchards, and how long a picked hero waits for more.</summary>
    public static readonly Rect2[] Orchards = [new(562, -528, 160, 92), new(530, -700, 92, 70)];
    public const double PickEvery = 45;
    static readonly string[] Fruit = ["apple", "pear", "plum", "brambleberries"];

    double _pickedAt = -PickEvery;

    public override void _Ready()
    {
        Quay(-900, -262, "The Log Quay", new Vector3(-1, 0, 0));
        Quay(640, -600, "Greywater Quay", new Vector3(1, 0, 0));
        Stacks();
        foreach (var plot in Orchards) Orchard(plot);
        Sign(new Vector3(640, 0, -420), "The Long Orchard");
    }

    void Sign(Vector3 at, string text)
    {
        at.Y = City.FloorAt(at) + 6;
        AddChild(new Label3D { Text = text, Position = at, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, OutlineSize = 12, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
    }

    /// <summary>Out from the town to the first deep water, the pier run on over it, and a ship tied up at the end.</summary>
    void Quay(double x, double z, string name, Vector3 way)
    {
        var (from, shore) = (new Vector3((float)x, 0, (float)z), Vector3.Zero);
        for (var r = 40f; r < 240; r += 4)
        {
            shore = from + way * r;
            if (main.World.Height(shore.X, shore.Z) < WorldGen.WaterLevel - 1) break;
        }
        var deck = (float)WorldGen.WaterLevel + 1.8f;
        var (start, length) = (shore - way * 10, 54f);
        var side = way.Cross(Vector3.Up);
        var timber = BuildingMaterials.For(Surface.Planks);
        var body = new StaticBody3D { Position = start + way * length / 2 + Vector3.Up * deck, Rotation = new Vector3(0, Mathf.Atan2(way.X, way.Z), 0) };
        var size = new Vector3(5, 0.4f, length);
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = timber });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
        for (var i = 0; i < 8; i++)
            foreach (var s in new[] { -1, 1 })
                AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.26f, Height = 6 }, MaterialOverride = timber, Position = start + way * (i * length / 7.5f) + side * s * 2.3f + Vector3.Up * (deck - 2) });
        if (Models.Scene("ph:ship.glb")?.Instantiate() is Node3D ship)
        {
            ship.Position = start + way * (length - 8) + side * 10 + Vector3.Up * (float)WorldGen.WaterLevel;
            ship.Rotation = new Vector3(0, Mathf.Atan2(way.X, way.Z), 0);
            AddChild(ship);
        }
        AddChild(new OmniLight3D { Position = start + way * length + Vector3.Up * (deck + 3), LightColor = new Color("#ffc080"), LightEnergy = 1.4f, OmniRange = 16 });
        AddChild(new Label3D { Text = name, Position = start + Vector3.Up * (deck + 5), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
    }

    /// <summary>Druitt's living: great stacks of cut timber waiting for the rafts.</summary>
    void Stacks()
    {
        var logs = new List<Transform3D>();
        foreach (var (x, z) in new[] { (-890f, -360f), (-850f, -368f), (-810f, -352f), (-896f, -170f), (-846f, -160f) })
            for (var row = 0; row < 4; row++)
                for (var i = 0; i < 6 - row; i++)
                {
                    var at = new Vector3(x + (i + row * 0.5f - 3) * 1.1f, 0, z);
                    at.Y = City.FloorAt(new Vector3(x, 0, z)) + 0.55f + row * 0.95f;
                    logs.Add(new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), at));
                }
        Many(new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 12, RadialSegments = 8, Rings = 1 }, new StandardMaterial3D { AlbedoColor = new Color("#7a5a3a"), Roughness = 0.9f }, logs);
        Sign(new Vector3(-850, 0, -360), "The Timber Yard");
    }

    void Orchard(Rect2 plot)
    {
        var (trunks, crowns, fruit) = (new List<Transform3D>(), new List<Transform3D>(), new List<Transform3D>());
        var rng = new Tristinwold.Deviant.Rng((ulong)(plot.Position.X * 7 + 9000));
        for (var x = plot.Position.X; x < plot.End.X; x += 9)
            for (var z = plot.Position.Y; z < plot.End.Y; z += 8)
            {
                var at = new Vector3(x, 0, z);
                if (main.World.Height(x, z) < WorldGen.WaterLevel + 1.5) continue;
                at.Y = City.FloorAt(at);
                var big = 0.85f + 0.3f * (float)rng.NextDouble();
                trunks.Add(new Transform3D(Basis.Identity.Scaled(Vector3.One * big), at + Vector3.Up * 1.3f * big));
                crowns.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(big, big * 0.8f, big)), at + Vector3.Up * 3.5f * big));
                for (var f = 0; f < 7; f++)
                {
                    var a = f * 2.4f + x;
                    fruit.Add(new Transform3D(Basis.Identity, at + new Vector3(Mathf.Cos(a) * 1.9f * big, (2.5f + f % 3 * 0.8f) * big, Mathf.Sin(a) * 1.9f * big)));
                }
            }
        Many(new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.26f, Height = 2.6f, RadialSegments = 7, Rings = 1 }, new StandardMaterial3D { AlbedoColor = new Color("#5a4632"), Roughness = 0.95f }, trunks);
        Many(new SphereMesh { Radius = 2.2f, Height = 4.4f, RadialSegments = 10, Rings = 6 }, new StandardMaterial3D { AlbedoColor = new Color("#5f8a3c"), Roughness = 0.95f }, crowns);
        Many(new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 6, Rings = 4 }, new StandardMaterial3D { AlbedoColor = new Color("#c23a2a"), Roughness = 0.5f }, fruit);
    }

    void Many(Mesh mesh, Material material, List<Transform3D> at)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = at.Count };
        for (var i = 0; i < at.Count; i++) mm.SetInstanceTransform(i, at[i]);
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
    }

    public static bool InOrchard(Vector3 p) => Orchards.Any(r => r.HasPoint(new Vector2(p.X, p.Z)));

    /// <summary>A hand or two of whatever's ripe, if the trees here have had time to give any.</summary>
    public (string Verb, Action Act)? Pick(Vector3 p)
    {
        if (!InOrchard(p)) return null;
        var left = _pickedAt + PickEvery - main.GuildBook.PlaySeconds;
        if (left > 0) return null;
        return ("pick the fruit", () =>
        {
            _pickedAt = main.GuildBook.PlaySeconds;
            var what = Fruit[GD.RandRange(0, Fruit.Length - 1)];
            var n = GD.RandRange(1, 3);
            main.Take([(what, n)]);
            main.HudNode.Toast($"{n} {Tristinwold.Gameplay.Items.Get(what)!.Name.ToLowerInvariant()}, warm from the sun.", 3);
        });
    }
}
