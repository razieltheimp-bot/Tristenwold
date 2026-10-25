using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Takes a tour of the world and saves screenshots, for checking how it looks without playing:
///   godot --path game -- --shots /some/folder --server off
/// </summary>
/// <param name="lairs">Tour the wild lairs (--lairs, or --lairs goblin_warren,storm_eyrie for some of them).</param>
/// <param name="wild">Portraits of the stranger things of the wilds (--wild).</param>
public partial class Shots(Main main, string dir, bool quick = false, string? dragon = null, bool skirmish = false, bool raid = false, string? lairs = null, bool wild = false) : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var (p, w) = (main.PlayerNode, main.World);
        await Frames(60);
        if (dragon is not null) { await Dragon(dragon); return; }
        if (skirmish) { await Skirmish(); return; }
        if (raid) { await Raid(); return; }
        if (lairs is not null) { await Lairs(lairs); return; }
        if (wild) { await Wild(); return; }
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

    /// <summary>A dragon of one colour circling over the pinewoods, seen from below: --shots dir --dragon green</summary>
    async Task Dragon(string colour)
    {
        var p = main.PlayerNode;
        var spot = new Vector3(-150, 0, -1500);
        await Go(spot, 120);
        var ground = City.FloorAt(spot);
        var e = Enemy.Create(Bestiary.Get("dragon_" + colour), spot with { Y = ground + 1 } + new Vector3(0, 0, -30));
        e.Roam = 6;
        main.AddEnemy(e);
        e.TakeOff(16, 600);
        p.Frozen = true;
        var cam = new Camera3D { Far = 2000, Fov = 55 };
        main.AddChild(cam);
        cam.MakeCurrent();
        for (var i = 0; i < 4; i++)
        {
            await Frames(90 + i * 37);
            var at = e.GlobalPosition + new Vector3(0, 3, 0);
            var from = new Vector3(at.X + 14, ground + 2.5f, at.Z + 22); // from the ground, looking up
            cam.GlobalPosition = from;
            cam.LookAt(at, Vector3.Up);
            await Shot($"dragon-{colour}-{i}");
        }
        GetTree().Quit();
    }

    /// <summary>A dragon knight of the alliance meets a corrupted dragon over the meadows by a village (--skirmish).</summary>
    async Task Skirmish()
    {
        var (p, w) = (main.PlayerNode, main.World);
        var village = w.SitesNear(-150, -1500, 1500).Where(s => s.Kind == SettlementKind.Hamlet).MinBy(s => s.DistanceTo(-150, -1500))!;
        var spot = new Vector3((float)village.X, 0, (float)(village.Z + village.Radius + 40));
        await Go(spot, 120);
        main.TerrainNode.EnsureAround(spot, 2);
        Vector3 Ground(Vector3 at) => at with { Y = City.FloorAt(at) + 0.5f };
        var knight = Enemy.Create(Bestiary.Get("dragon_gold"), Ground(spot + new Vector3(-25, 0, -10)), look: "rider:dragon_gold");
        knight.Allied = true;
        knight.Stats.Name = Dragons.KnightName(3);
        knight.RefreshPlate();
        main.AddEnemy(knight);
        var fallen = Enemy.Create(Bestiary.Get("dragon_red_corrupt"), Ground(spot + new Vector3(30, 0, -20)));
        main.AddEnemy(fallen);
        p.Frozen = true;
        p.Visible = false;
        var cam = new Camera3D { Far = 2000, Fov = 55 };
        main.AddChild(cam);
        cam.MakeCurrent();
        for (var i = 0; i < 4; i++)
        {
            await Frames(80 + i * 45);
            var mid = (knight.GlobalPosition + fallen.GlobalPosition) / 2 + new Vector3(0, 3, 0);
            var eye = mid + new Vector3(10, 0, 34);
            cam.GlobalPosition = eye with { Y = City.FloorAt(eye) + 2.5f };
            cam.LookAt(mid, Vector3.Up);
            await Shot($"skirmish-{i}");
        }
        GetTree().Quit();
    }

    /// <summary>The Ashen Crypt: the thralls rising, then the Cinder Warden (--raid).</summary>
    async Task Raid()
    {
        var (p, r) = (main.PlayerNode, Raids.AshenCrypt);
        var centre = new Vector3((float)r.X, 0, (float)r.Z);
        await Go(centre + new Vector3(0, 0, 60), 120);
        var cam = new Camera3D { Far = 2000, Fov = 50 };
        main.AddChild(cam);
        cam.MakeCurrent();
        // Heights are above the ground.
        void Look(Vector3 eye, Vector3 at) { cam.GlobalPosition = eye with { Y = City.FloorAt(eye) + eye.Y }; cam.LookAt(at with { Y = City.FloorAt(at) + at.Y }, Vector3.Up); }
        Look(centre + new Vector3(10, 4, 30), centre + new Vector3(0, 1.5f, 0));
        await Frames(60);
        await Shot("raid-0-crypt");
        p.GlobalPosition = (centre + new Vector3(0, 0, 25)) with { Y = City.FloorAt(centre + new Vector3(0, 0, 25)) + 1 };
        await Frames(50);
        Look(centre + new Vector3(3, 3, 12.5f), centre + new Vector3(0, 0.8f, 0));
        await Shot("raid-1-thralls");
        foreach (var t in main.CryptNode.Thralls.ToList()) t.TakeHit(1e6, Tristinwold.Nemesis.DamageKind.Fire, AttackKind.Spell, false, p);
        p.Frozen = true;
        for (var i = 0; i < 200; i++) { p.Stats.Heal(1e6); await Frames(1); }
        if (main.CryptNode.Boss is { } w)
        {
            for (var i = 0; i < 3; i++)
            {
                var at = w.GlobalPosition;
                Look((at + new Vector3(4.5f, 0, -4)) with { Y = 1.8f }, at with { Y = 2.6f });
                await Frames(40 + i * 30);
                p.Stats.Heal(1e6); // the photographer is in no danger
                await Shot($"raid-{2 + i}-warden");
            }
        }
        GetTree().Quit();
    }

    /// <summary>Each lair: the place, its own coming out, its boss up close, and a boss that beat us out roaming with a name.</summary>
    async Task Lairs(string which)
    {
        var p = main.PlayerNode;
        var pick = which == "true" ? [] : which.Split(',', StringSplitOptions.RemoveEmptyEntries); // plain --lairs: all of them
        var cam = new Camera3D { Far = 2000, Fov = 55 };
        main.AddChild(cam);
        void Look(Vector3 eye, Vector3 at) { cam.GlobalPosition = eye with { Y = City.FloorAt(eye) + eye.Y }; cam.LookAt(at with { Y = City.FloorAt(at) + at.Y }, Vector3.Up); }
        foreach (var r in Raids.All.Where(r => r != Raids.AshenCrypt && (pick.Length == 0 || pick.Any(x => r.Id.EndsWith(x)))))
        {
            var lair = main.LairOf(r);
            var id = r.Id[5..];
            var centre = new Vector3((float)r.X, 0, (float)r.Z);
            var toward = -centre.Normalized(); // the city side
            var side = new Vector3(-toward.Z, 0, toward.X);
            p.Hero.Level = r.MinLevel;
            p.Fit();
            await Go(centre + toward * 70, 100);
            p.Frozen = true;
            cam.MakeCurrent();
            Look(centre + toward * 32 + side * 10 + Vector3.Up * 9, centre + Vector3.Up * 2 - toward * 4);
            await Frames(40);
            await Shot($"lair-{id}-0-site");
            p.GlobalPosition = (centre + toward * 34) with { Y = City.FloorAt(centre + toward * 34) + 1 };
            await Frames(40);
            Look(centre + toward * 16 + side * 5 + Vector3.Up * 3.5f, centre + Vector3.Up * 1);
            await Shot($"lair-{id}-1-thralls");
            foreach (var t in lair.Thralls.ToList()) t.TakeHit(1e7, Tristinwold.Nemesis.DamageKind.Sun, AttackKind.Spell, false, p);
            // Stand our ground in the lair; the boss comes to us.
            var stand = centre + toward * 7;
            p.GlobalPosition = stand with { Y = City.FloorAt(stand) + 1 };
            for (var i = 0; i < 120; i++) { p.Stats.Heal(1e6); await Frames(1); }
            // Frames the boss from beside the fight, heights above the ground (Look adds the floor).
            void Portrait(Enemy boss, float swing)
            {
                var at = boss.GlobalPosition with { Y = 0 };
                var size = (float)boss.Kind.Height * (boss.Named ? 1.15f : 1);
                var toHero = (p.GlobalPosition - boss.GlobalPosition) with { Y = 0 };
                var dir = (toHero.LengthSquared() > 0.01f ? toHero.Normalized() : toward).Rotated(Vector3.Up, swing);
                Look(at + dir * size * 1.9f + Vector3.Up * size * 0.75f, at + Vector3.Up * size * 0.5f);
            }
            if (lair.Boss is { } boss)
            {
                for (var i = 0; i < 2; i++)
                {
                    await Frames(20 + i * 40);
                    p.Stats.Heal(1e6);
                    Portrait(boss, i == 0 ? 0.9f : -1.1f);
                    await Shot($"lair-{id}-{2 + i}-boss");
                }
                if (r == Raids.GoblinWarren || pick.Length > 0)
                {
                    // Let it beat us: it takes a name and goes roaming.
                    p.TakeHit(1e7, Tristinwold.Nemesis.DamageKind.Fire, AttackKind.Weapon, false, boss);
                    await Frames(3);
                    Portrait(boss, 0.6f);
                    await Shot($"lair-{id}-4-named");
                    await Frames(300);
                }
                if (IsInstanceValid(boss)) boss.TakeHit(1e8, Tristinwold.Nemesis.DamageKind.Sun, AttackKind.Spell, false, p);
            }
            p.Frozen = false;
            main.PlayerNode.Camera.MakeCurrent();
            await Frames(10);
        }
        GetTree().Quit();
    }

    /// <summary>Each of the stranger creatures standing for its portrait, then a kipine diving and a dire imp's band.</summary>
    async Task Wild()
    {
        var p = main.PlayerNode;
        var here = new Vector3(0, 0, 400);
        await Go(here + new Vector3(0, 0, 30), 100);
        p.Frozen = true;
        var cam = new Camera3D { Far = 2000, Fov = 50 };
        main.AddChild(cam);
        cam.MakeCurrent();
        void Look(Vector3 eye, Vector3 at) { cam.GlobalPosition = eye with { Y = City.FloorAt(eye) + eye.Y }; cam.LookAt(at with { Y = City.FloorAt(at) + at.Y }, Vector3.Up); }
        Enemy Put(string kind, Vector3 at, bool young = false)
        {
            var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.5f }, young: young);
            main.AddEnemy(e);
            WildBrain.Give(main, e);
            e.Roam = 0.1f;
            return e;
        }
        foreach (var kind in new[] { "kipine", "deathcrest_ape", "dire_imp", "imp", "winged_ape" })
        {
            var e = Put(kind, here);
            await Frames(40);
            e.LookAt(e.GlobalPosition + new Vector3(0.6f, 0, 1), Vector3.Up); // three-quarters toward the camera
            var size = (float)e.Kind.Height;
            for (var i = 0; i < 2; i++)
            {
                var from = (i == 0 ? new Vector3(1.3f, 0, 2.4f) : new Vector3(-2.2f, 0, -1.6f)) * size;
                Look(here + from + Vector3.Up * size * 0.75f, here + Vector3.Up * size * 0.5f);
                await Frames(20);
                await Shot($"wild-{kind}-{i}");
            }
            e.QueueFree();
            await Frames(5);
        }
        // A dire imp and its band, and a pack of deathcrest apes.
        var band = new List<Enemy> { Put("dire_imp", here) };
        for (var i = 0; i < 3; i++) band.Add(Put("imp", here + new Vector3(-1.6f + i * 1.6f, 0, 1.6f)));
        await Frames(40);
        Look(here + new Vector3(2.5f, 2.2f, 6), here + Vector3.Up * 1);
        await Shot("wild-band");
        foreach (var b in band) b.QueueFree();
        var pack = Enumerable.Range(0, 3).Select(i => Put("deathcrest_ape", here + new Vector3(-2 + i * 2, 0, i % 2))).ToList();
        await Frames(40);
        Look(here + new Vector3(3, 2.4f, 7), here + Vector3.Up * 1);
        await Shot("wild-pack");
        foreach (var a in pack) a.QueueFree();
        // A kipine diving.
        p.Frozen = false;
        var kipine = Put("kipine", here + new Vector3(0, 0, 10));
        kipine.Roam = 4;
        p.GlobalPosition = (here + new Vector3(0, 0, 24)) with { Y = City.FloorAt(here + new Vector3(0, 0, 24)) + 1 };
        kipine.Provoke(p);
        for (var i = 0; i < 400 && kipine.GlobalPosition.Y < City.FloorAt(kipine.GlobalPosition) + 6; i++) { p.Stats.Heal(1e6); await Frames(1); }
        var air = kipine.GlobalPosition;
        Look(p.GlobalPosition with { Y = 1.6f } + new Vector3(3, 0, 2), air with { Y = air.Y - City.FloorAt(air) });
        await Shot("wild-kipine-dive");
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
