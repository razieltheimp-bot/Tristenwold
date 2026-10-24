using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Takes a tour of the world and saves screenshots, for checking how it looks without playing:
///   godot --path game -- --shots /some/folder --server off
/// </summary>
public partial class Shots(Main main, string dir, bool quick = false) : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var (p, w) = (main.PlayerNode, main.World);
        await Frames(60);
        if (quick) { await Quick(); return; }

        // Tristinwold from the Gallows Perch, looking into the city.
        p.Face(new Vector3(0, 0, 0));
        await Shot("1-tristinwold");

        // Walk north (the map fills in), then look over the land from above.
        for (var z = 0f; z >= -1500; z -= 100) await Go(new Vector3(-150, 0, z), 4);
        await Overview(new Vector3(-150, 0, -1500), new Vector3(-150, 0, -1700), 60, "2-north-of-tristinwold");

        // A village and someone in it.
        var village = w.SitesNear(-150, -1500, 1500).Where(s => s.Kind == SettlementKind.Hamlet).MinBy(s => s.DistanceTo(-150, -1500))!;
        await Go(new Vector3((float)village.X, 0, (float)(village.Z + village.Radius + 12)), 60);
        p.Face(new Vector3((float)village.X, 0, (float)village.Z));
        await Shot("3-village-" + village.Name);
        await Overview(new Vector3((float)village.X, 0, (float)village.Z), new Vector3((float)village.X, 0, (float)village.Z + 1), 70, "4-village-above");
        var person = main.PlacesNode.People.FirstOrDefault(x => x.Npc.HomeId == village.Id);
        if (person is not null)
        {
            await Go(person.GlobalPosition + new Vector3(0, 0, 2.5f), 30);
            main.Talk(person);
            main.HudNode.Choose("Who are you?");
            await Shot("5-talking");
            main.HudNode.EndTalk();
        }

        // Cinderhelm, the first city north.
        var cinderhelm = w.AuthoredCities.First(c => c.Name == "Cinderhelm");
        await Go(new Vector3((float)cinderhelm.X, 0, (float)(cinderhelm.Z + cinderhelm.Radius + 50)), 90);
        p.Face(new Vector3((float)cinderhelm.X, 0, (float)cinderhelm.Z));
        await Shot("6-cinderhelm");

        // The snow, far north.
        await Go(new Vector3(400, 0, -6500), 90);
        await Overview(new Vector3(400, 0, -6500), new Vector3(400, 0, -6800), 80, "7-far-north");

        // The map, after all that walking.
        await Go(new Vector3(-150, 0, -1500), 30);
        main.HudNode.ToggleMap();
        await Shot("8-map");
        GetTree().Quit();
    }

    /// <summary>Three views for comparing graphics settings: the harbor, a village, and woods by a lake.</summary>
    async Task Quick()
    {
        var (p, w) = (main.PlayerNode, main.World);
        p.Face(new Vector3(0, 0, 0));
        await Shot("q0-start");
        p.Face(City.GallowsPerch);
        await Shot("q0-gallows-perch");
        await Go(new Vector3(132, 0, 0), 90);
        p.GlobalPosition = new Vector3(132, 1, 0); // out on the middle pier
        await Frames(30);
        p.Face(new Vector3(210, 0, 70)); // the Needle across the water
        await Shot("q1-harbor");
        var village = w.SitesNear(-150, -1500, 1500).Where(s => s.Kind == SettlementKind.Hamlet).MinBy(s => s.DistanceTo(-150, -1500))!;
        await Go(new Vector3((float)village.X, 0, (float)(village.Z + village.Radius + 12)), 120);
        p.Face(new Vector3((float)village.X, 0, (float)village.Z));
        await Shot("q2-village");
        // The wilds: a deer herd grazing, then a wolf pack, seen from where they haven't noticed you yet.
        foreach (var (kind, name) in new[] { ("deer", "q2-deer"), ("wolf", "q2-wolves") })
        {
            var herd = Wildlife.Near(w, village.X, village.Z, 3000).Where(h => h.KindId == kind).MinBy(h => h.DistanceTo(village.X, village.Z));
            if (herd is null) continue;
            await Go(new Vector3((float)herd.X, 0, (float)herd.Z + 30), 150);
            var them = main.Enemies().Where(e => e.Kind.Id == kind).ToList();
            var middle = them.Count > 0 ? them.Aggregate(Vector3.Zero, (a, e) => a + e.GlobalPosition) / them.Count : new Vector3((float)herd.X, 0, (float)herd.Z);
            p.GlobalPosition = (middle + new Vector3(0, 0, 19)) with { Y = City.FloorAt(middle + new Vector3(0, 0, 19)) + 1 };
            p.Frozen = true;
            await Frames(20);
            p.Face(middle);
            await Shot(name);
            p.Frozen = false;
        }
        // Find a lakeshore near the village: a point on land with water close by.
        for (var r = 60.0; r < 900; r += 30)
            for (var a = 0.0; a < Math.Tau; a += 0.3)
            {
                var (x, z) = (village.X + Math.Cos(a) * r, village.Z + Math.Sin(a) * r);
                if (w.Height(x, z) > 1.5 || w.Height(x + Math.Cos(a) * 25, z + Math.Sin(a) * 25) > WorldGen.WaterLevel - 0.5) continue;
                await Go(new Vector3((float)x, 0, (float)z), 120);
                p.Face(new Vector3((float)(x + Math.Cos(a) * 30), 0, (float)(z + Math.Sin(a) * 30)));
                await Shot("q3-lake");
                GetTree().Quit();
                return;
            }
        GetTree().Quit();
    }

    async Task Go(Vector3 at, int frames)
    {
        main.TerrainNode.EnsureAround(at, 1);
        main.PlayerNode.GlobalPosition = at with { Y = City.FloorAt(at) + 1 };
        await Frames(frames);
    }

    async Task Overview(Vector3 from, Vector3 lookAt, float height, string name)
    {
        var cam = new Camera3D { Far = 2000 };
        main.AddChild(cam);
        cam.GlobalPosition = from + new Vector3(0, (float)main.World.Height(from.X, from.Z) + height, 80);
        cam.LookAt(lookAt with { Y = (float)main.World.Height(lookAt.X, lookAt.Z) }, Vector3.Up);
        cam.MakeCurrent();
        await Frames(90);
        await Shot(name);
        cam.QueueFree();
        main.PlayerNode.Camera.MakeCurrent();
        await Frames(2);
    }

    async Task Shot(string name)
    {
        await Frames(10);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng($"{dir}/{name}.png");
        GD.Print($"shot {name}");
    }

    async Task Frames(int n)
    {
        for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
