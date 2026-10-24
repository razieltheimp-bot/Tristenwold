using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Takes a tour of the world and saves screenshots, for checking how it looks without playing:
///   godot --path game -- --shots /some/folder --server off
/// </summary>
public partial class Shots(Main main, string dir) : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var (p, w) = (main.PlayerNode, main.World);
        await Frames(60);

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
