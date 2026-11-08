using Godot;
using Tristinwold.Shared;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Takes a tour of the world and saves screenshots, for checking how it looks without playing:
///   godot --path game -- --shots /some/folder --server off
/// </summary>
/// <param name="lairs">Tour the wild lairs (--lairs, or --lairs goblin_warren,storm_eyrie for some of them).</param>
/// <param name="wild">Portraits of the stranger things of the wilds (--wild).</param>
public partial class Shots(Main main, string dir, bool quick = false, string? dragon = null, bool skirmish = false, bool raid = false, string? lairs = null, bool wild = false, bool limit = false, bool market = false, bool audit = false, bool spells = false, bool town = false) : Node
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
        if (limit) { await Limit(); return; }
        if (market) { await Market(); return; }
        if (town) { await Town(); return; }
        if (audit) { await Audit(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--overlaps")) { await Overlaps(); return; }
        if (spells) { await Spells(); return; }
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
        foreach (var t in main.CryptNode.Thralls.ToList()) t.TakeHit(1e6, Tristinwold.Deviant.DamageKind.Fire, AttackKind.Spell, false, p);
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
            foreach (var t in lair.Thralls.ToList()) t.TakeHit(1e7, Tristinwold.Deviant.DamageKind.Sun, AttackKind.Spell, false, p);
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
                    p.TakeHit(1e7, Tristinwold.Deviant.DamageKind.Fire, AttackKind.Weapon, false, boss);
                    await Frames(3);
                    Portrait(boss, 0.6f);
                    await Shot($"lair-{id}-4-named");
                    await Frames(300);
                }
                if (IsInstanceValid(boss)) boss.TakeHit(1e8, Tristinwold.Deviant.DamageKind.Sun, AttackKind.Spell, false, p);
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

    /// <summary>Every kind of spell, a breath, a heal and a sword blow, slowed down and photographed (--spells).</summary>
    async Task Spells()
    {
        var p = main.PlayerNode;
        p.GlobalPosition = new Vector3(76, 1, 4); // the open square by the market
        await Frames(40);
        p.Rotation = Vector3.Zero;
        var world = main;
        var cam = new Camera3D { Fov = 55 };
        main.AddChild(cam);
        var origin = p.GlobalPosition;
        cam.GlobalPosition = origin + new Vector3(8, 2.4f, -4.5f);
        cam.LookAt(origin + new Vector3(0, 1.2f, -5.5f), Vector3.Up);
        cam.MakeCurrent();
        Engine.TimeScale = 0.12;
        var hand = origin + new Vector3(0.4f, 1.3f, -0.6f);
        var mark = origin + new Vector3(0, 1.0f, -10);
        foreach (var kind in new[] { DamageKind.Fire, DamageKind.Frost, DamageKind.Shock, DamageKind.Poison, DamageKind.Sun, DamageKind.Physical })
        {
            Effects.Bolt(world, hand, mark, kind, 0.25f);
            await Frames(2);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/bolt-{kind}.png");
            await Frames(4);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/burst-{kind}.png");
            await Frames(20);
        }
        Effects.Cone(world, origin + new Vector3(0, 1.5f, -1), new Vector3(0, -0.1f, -1), DamageKind.Fire, 9);
        await Frames(3);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/breath-fire.png");
        await Frames(20);
        Effects.Rise(p, DamageKind.Sun);
        await Frames(3);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/heal.png");
        await Frames(20);
        Effects.Impact(world, origin + new Vector3(0, 1.1f, -1.5f), new Vector3(0, 0, -1), DamageKind.Physical, true);
        await Frames(1);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/hit-crit.png");
        Engine.TimeScale = 1;
        GetTree().Quit();
    }

    // ---- the prop audit -------------------------------------------------------------------------

    /// <summary>
    /// Tours the city, its docks and market, a settlement of every kind and every lair, and lists each placed prop
    /// that floats above whatever is under it, is sunk into it, or stands inside a wall or building (--audit).
    /// </summary>
    async Task Audit()
    {
        var w = main.World;
        var stops = new List<(string Name, Vector3 At)>
        {
            ("Tristinwold", City.PlayerStart), ("the docks", new Vector3(122, 0, 0)), ("the Warrens", new Vector3(-20, 0, -86)),
        };
        foreach (var kind in Enum.GetValues<SettlementKind>().Where(k => k is not SettlementKind.City))
            if (w.SitesNear(0, 0, 3000).Where(x => x.Kind == kind).MinBy(x => x.DistanceTo(0, 0)) is { } site)
                stops.Add(($"{site.Name} ({kind})", new Vector3((float)site.X, 0, (float)site.Z)));
        foreach (var r in Raids.All) stops.Add((r.Name, new Vector3((float)r.X, 0, (float)r.Z)));
        var seen = new HashSet<ulong>();
        var problems = 0;
        foreach (var (name, at) in stops)
        {
            await Go(at + new Vector3(0, 0, 3), 40);
            var props = main.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
                .Where(n => n.HasMeta("prop") && IsInstanceValid(n) && n.IsInsideTree() && n.GlobalPosition.DistanceTo(at) < 140)
                .Where(n => !new[] { "nature/", "rock", "mountain", "banner", "mounted", "arch", "pillar" }.Any(((string)n.GetMeta("prop")).Contains)).ToList();
            var found = new List<string>();
            foreach (var prop in props)
            {
                if (!seen.Add(prop.GetInstanceId())) continue;
                if (Check(prop) is { } problem) found.Add($"  {prop.GetMeta("prop")} at ({prop.GlobalPosition.X:0.0}, {prop.GlobalPosition.Y:0.00}, {prop.GlobalPosition.Z:0.0}): {problem}");
            }
            problems += found.Count;
            GD.Print($"AUDIT {name}: {props.Count} props, {found.Count} wrong");
            foreach (var f in found) GD.Print("AUDIT" + f);
        }
        GD.Print($"AUDIT done: {problems} props wrong");
        GetTree().Quit();
    }

    /// <summary>
    /// Every building, hull and great structure that runs into another (--overlaps): Tristinwold in a ring of stops, the
    /// docks and the shipyard, every guild's yard, the places near the city and every lair. Wall pieces meeting round a
    /// curve, and piers meeting the boardwalk, are meant to.
    /// </summary>
    async Task Overlaps()
    {
        var stops = new List<(string Name, Vector3 At)> { ("Tristinwold's crown", Vector3.Zero), ("the docks", new Vector3(122, 0, 0)), ("the shipyard", Main.ShipyardAt + new Vector3(8, 0, 0)), ("the Warrens", new Vector3(-20, 0, -86)) };
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12;
            stops.Add(($"Tristinwold's outer ring ({i * 30} degrees)", new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 80));
            stops.Add(($"Tristinwold's middle ring ({i * 30} degrees)", new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 46));
        }
        foreach (var g in Guilds.All) stops.Add((g.Hall, new Vector3((float)g.X, 0, (float)g.Z)));
        foreach (var s in main.World.SitesNear(0, 0, 1800).Where(s => s.Kind is not SettlementKind.City || s.DistanceTo(0, 0) > 200).OrderBy(s => s.DistanceTo(0, 0)))
            stops.Add(($"{s.Name} ({s.Kind})", new Vector3((float)s.X, 0, (float)s.Z)));
        foreach (var r in Raids.All) stops.Add((r.Name, new Vector3((float)r.X, 0, (float)r.Z)));
        var told = new HashSet<(ulong, ulong)>();
        var count = 0;
        foreach (var (name, at) in stops)
        {
            await Go(at + new Vector3(0, 0, 3), 40);
            var near = GetTree().GetNodesInGroup(Buildings.Group).OfType<Node3D>()
                .Where(n => IsInstanceValid(n) && n.IsInsideTree() && !What(n).StartsWith("disc:") && n.GlobalPosition.DistanceTo(at) < 110).ToList();
            foreach (var s in near.Where(s => s.HasMeta("kind") && !Meant(What(s), What(s)) && What(s) is not ("pier" or "boardwalk" or "steam_wagon" or "mine_portal"))) // a mine's mouth is set into its hill
            {
                if (!told.Add((s.GetInstanceId(), 0)) || Slope(s) is not { } drop || drop < 1.4f) continue;
                count++;
                GD.Print($"OVERLAP {name}: {What(s)} at ({s.GlobalPosition.X:0.0}, {s.GlobalPosition.Z:0.0}) stands on ground {drop:0.0} m higher at one corner than another");
            }
            foreach (var s in near)
                foreach (var other in RunsInto(s))
                {
                    var (a, b) = (s.GetInstanceId(), other.GetInstanceId());
                    if (!told.Add(a < b ? (a, b) : (b, a)) || Meant(What(s), What(other))) continue;
                    count++;
                    GD.Print($"OVERLAP {name}: {What(s)} at ({s.GlobalPosition.X:0.0}, {s.GlobalPosition.Z:0.0}) runs into {What(other)} at ({other.GlobalPosition.X:0.0}, {other.GlobalPosition.Z:0.0})");
                }
        }
        GD.Print($"OVERLAP done: {count} found at {stops.Count} stops");
        GetTree().Quit();
    }

    /// <summary>How far the ground under a building rises from its lowest corner to its highest (rays down past the building itself, so a mound counts as well as the land).</summary>
    float? Slope(Node3D n)
    {
        var box = Solids(n).FirstOrDefault();
        if (box.Size == Vector3.Zero) return null;
        var space = n.GetWorld3D().DirectSpaceState;
        var mine = n is CollisionObject3D body ? new Godot.Collections.Array<Rid> { body.GetRid() } : [];
        var heights = new List<float>();
        foreach (var (x, z) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f), (0f, 0f) })
        {
            var at = box.At * new Vector3(box.Size.X * x, 0, box.Size.Z * z);
            var from = at with { Y = n.GlobalPosition.Y + 30 };
            var skip = new Godot.Collections.Array<Rid>(mine);
            float? ground = null;
            for (var tries = 0; tries < 6 && ground is null; tries++) // past other buildings' roofs, props and people, down to the land or a mound
            {
                var hit = space.IntersectRay(new PhysicsRayQueryParameters3D { From = from, To = from + Vector3.Down * 60, Exclude = skip });
                if (hit.Count == 0) break;
                var thing = hit["collider"].AsGodotObject() as Node;
                var owner = thing;
                while (owner is not null && !(owner is Node3D o && (o.HasMeta("kind") || o.HasMeta("prop")))) owner = owner.GetParent();
                if (owner is null && thing is not CharacterBody3D) ground = ((Vector3)hit["position"]).Y;
                else skip.Add((Rid)hit["rid"]);
            }
            heights.Add(ground ?? (float)main.World.Height(at.X, at.Z));
        }
        return heights.Max() - heights.Min();
    }

    static string What(Node3D n) => n.HasMeta("structure") ? (string)n.GetMeta("structure") : (string)n.GetMeta("kind");

    static bool Meant(string a, string b)
    {
        static bool Wall(string k) => k is "wall" or "wall_low" or "gate";
        static bool Boards(string k) => k is "pier" or "boardwalk";
        return Wall(a) && Wall(b) || Boards(a) && Boards(b) || Wall(a) && Boards(b) || Boards(a) && Wall(b) || a == "lantern" || b == "lantern" || a.StartsWith("disc:") || b.StartsWith("disc:");
    }

    /// <summary>The other buildings and structures this one's solid boxes run into.</summary>
    IEnumerable<Node3D> RunsInto(Node3D n)
    {
        var space = n.GetWorld3D().DirectSpaceState;
        var mine = n is CollisionObject3D body ? new Godot.Collections.Array<Rid> { body.GetRid() } : [];
        var found = new HashSet<Node3D>();
        foreach (var (at, size) in Solids(n))
        {
            var probe = new PhysicsShapeQueryParameters3D { Shape = new BoxShape3D { Size = size }, Transform = at, Exclude = mine, CollisionMask = uint.MaxValue };
            foreach (var hit in space.IntersectShape(probe, 16))
            {
                var owner = hit["collider"].AsGodotObject() as Node;
                while (owner is not null && !(owner is Node3D o3 && (o3.HasMeta("kind") || o3.HasMeta("structure")))) owner = owner.GetParent();
                if (owner is Node3D other && other != n && !n.IsAncestorOf(other) && !other.IsAncestorOf(n) && other.IsInGroup(Buildings.Group)) found.Add(other);
            }
        }
        return found;
    }

    /// <summary>Its solid boxes, turned as it's turned: a building's collision boxes, or, for a thing with none, its meshes' box in its own frame, a little in from the edges.</summary>
    static IEnumerable<(Transform3D At, Vector3 Size)> Solids(Node3D n)
    {
        static (Transform3D, Vector3) Plain(Transform3D t, Vector3 size) => (new Transform3D(t.Basis.Orthonormalized(), t.Origin), size * t.Basis.Scale);
        var shapes = n.GetChildren().OfType<CollisionShape3D>().Where(c => c.Shape is BoxShape3D).ToList();
        if (shapes.Count > 0)
        {
            foreach (var c in shapes) yield return Plain(c.GlobalTransform, ((BoxShape3D)c.Shape).Size * 0.9f);
            yield break;
        }
        var inverse = n.GlobalTransform.AffineInverse();
        Aabb? box = null;
        foreach (var m in n.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(n as MeshInstance3D).OfType<MeshInstance3D>().Where(m => m.IsVisibleInTree()))
        {
            var b = inverse * m.GlobalTransform * m.GetAabb();
            box = box is { } x ? x.Merge(b) : b;
        }
        if (box is { } a) yield return Plain(n.GlobalTransform * new Transform3D(Basis.Identity, a.GetCenter()), a.Size * 0.85f);
    }

    /// <summary>What's wrong with a prop, if anything: floating, sunk, or inside a wall.</summary>
    string? Check(Node3D prop)
    {
        Aabb? box = null;
        foreach (var m in prop.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.IsVisibleInTree()))
        {
            var b = m.GlobalTransform * m.GetAabb();
            box = box is { } x ? x.Merge(b) : b;
        }
        if (box is not { } aabb || aabb.Size.Y < 0.05f) return null;
        var space = prop.GetWorld3D().DirectSpaceState;
        var mine = prop is CollisionObject3D body ? new Godot.Collections.Array<Rid> { body.GetRid() } : [];
        var centre = aabb.GetCenter();
        float? Ground(Vector3 from, float down)
        {
            var hit = space.IntersectRay(new PhysicsRayQueryParameters3D { From = from, To = from + Vector3.Down * down, Exclude = mine });
            return hit.Count > 0 ? ((Vector3)hit["position"]).Y : null;
        }
        var bottom = aabb.Position.Y;
        // What's under the middle of it, looking down from a little above its foot.
        var under = Ground(new Vector3(centre.X, bottom + 0.15f, centre.Z), 40);
        var terrain = (float)main.World.Height(centre.X, centre.Z);
        var floor = under ?? terrain;
        if (bottom - floor > 0.3f) return $"floats {bottom - floor:0.00} m above {(under is null ? "the land" : "what's under it")}";
        var above = Ground(new Vector3(centre.X, bottom + aabb.Size.Y + 0.5f, centre.Z), aabb.Size.Y + 0.5f);
        if (above is { } top && top - bottom > Math.Max(0.35f, aabb.Size.Y * 0.4f) && top < bottom + aabb.Size.Y + 0.4f)
            return $"sunk {top - bottom:0.00} m into the ground or a floor (it's {aabb.Size.Y:0.00} m tall)";
        // Inside a wall: the middle of it overlaps some other solid that isn't the land.
        var probe = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = aabb.Size * 0.5f + Vector3.One * 0.01f }, Transform = new Transform3D(Basis.Identity, centre + Vector3.Up * aabb.Size.Y * 0.1f),
            Exclude = mine, CollisionMask = uint.MaxValue,
        };
        foreach (var hit in space.IntersectShape(probe, 8))
            if (hit["collider"].AsGodotObject() is Node3D other && !main.TerrainNode.IsAncestorOf(other) && !other.HasMeta("prop") && other is not CharacterBody3D)
                return $"inside {other.Name} ({other.GetParent()?.Name})";
        return null;
    }

    /// <summary>The market stalls on the Gallows road, the menagerie's pen at the end (--market).</summary>
    async Task Market()
    {
        var p = main.PlayerNode;
        p.GlobalPosition = new Vector3(97, 1, 2);
        await Frames(30);
        p.Face(new Vector3(95, 0, 16));
        await Shot("market-menagerie");
        p.GlobalPosition = new Vector3(78, 1, -4);
        await Frames(20);
        p.Face(new Vector3(78, 0, 16));
        await Shot("market-row");
        var cam = new Camera3D { Far = 500 };
        main.AddChild(cam);
        cam.GlobalPosition = new Vector3(75, 45, 45);
        cam.LookAt(new Vector3(72, 0, 12), Vector3.Up);
        cam.MakeCurrent();
        await Shot("market-above");
        GetTree().Quit();
    }

    /// <summary>The town's work (--town): the fish stall, the docks, the quarry, the woodlot, the Exchange, a line in the water.</summary>
    async Task Town()
    {
        var p = main.PlayerNode;
        var cam = new Camera3D { Far = 500 };
        main.AddChild(cam);
        async Task From(string name, Vector3 at, Vector3 look)
        {
            main.TerrainNode.EnsureAround(look, 1);
            p.GlobalPosition = look + new Vector3(0, 1, 6); // the hero nearby, so what's near them is shown
            await Frames(40);
            cam.GlobalPosition = at;
            cam.LookAt(look, Vector3.Up);
            cam.MakeCurrent();
            await Shot(name);
        }
        if (OS.GetCmdlineUserArgs().Contains("--party"))
        {
            foreach (var kind in new[] { "wolf", "fox", "bear", "kipine", "boar" }) main.AdoptForTest(kind);
            var lad = new Tristinwold.Gameplay.Bond { Id = "shot/liv", KindId = "tinker", Role = Tristinwold.Gameplay.BondRole.Apprentice, Trains = "EtherAeronaut", Name = "Liv", Level = 6, Race = "human", JoinAge = 14 };
            main.AddBondForTest(lad);
            main.BringForTest(lad);
            await Frames(60);
            main.ShowParty();
            await Frames(20);
            await Shot("party");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--warrens"))
        {
            // A country mine, a trench and the Depths, from just inside the way in and from down the tunnels.
            async Task Look(string name, Vector3 eye, Vector3 at)
            {
                main.PlayerNode.GlobalPosition = eye with { Y = Indoors.StageAt.Y + 0.6f };
                await Frames(20);
                cam.GlobalPosition = eye;
                cam.LookAt(at, Vector3.Up);
                cam.MakeCurrent();
                await Shot(name);
            }
            foreach (var kind in new[] { "mineshaft", "trench", "depths" })
            {
                if (kind == "mineshaft") main.GoInsideForTest("mineshaft", 4242, new Vector3(0, 0, 40));
                else main.GoDeepForTest(kind, kind == "trench" ? Tristinwold.World.Deeps.Around(main.World, 0, 0, 1).First(t => t.Mines.Count > 0) : null);
                await Frames(60);
                if (main.IndoorsNode is not { Maze: { } maze } room) { GD.Print($"no warren in {kind}"); continue; }
                var s = Indoors.StageAt;
                var door = room.WayIn;
                await Look($"{kind}-way-in", door + new Vector3(0, 1.7f, 0), door + new Vector3(0, 1.2f, -12));
                // Down a tunnel: the first cell outside any chamber with an opening to look along.
                for (var r = maze.Rows - 1; r >= 0; r--)
                    for (var c = 0; c < maze.Cols; c++)
                    {
                        if (maze.RoomAt(c, r) is not null || !maze.Open(c, r, 0, -1)) continue;
                        var mid = room.CellMiddle(c, r);
                        await Look($"{kind}-tunnel", mid + new Vector3(0, 1.7f, 0), room.CellMiddle(c, r - 1) + Vector3.Up * 1.4f);
                        r = -1;
                        break;
                    }
                foreach (var m in maze.Rooms.Where(m => m.Id != "door").Take(3))
                {
                    var (at, size) = room.RoomSpan(m);
                    await Look($"{kind}-{m.Id}", at + new Vector3(size.X * 0.35f, 2.2f, size.Y * 0.4f), at + Vector3.Up);
                }
                main.GoOutsideForTest();
                await Frames(30);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--ownship"))
        {
            // The hero's own ship at her berth: from the pier, from off her bow, from above, from her wheel and along her deck.
            (main.Sea.OwnShip, main.Sea.ShipName, main.Sea.Hull, main.Sea.Voyage) = (true, "Wren", 100, null);
            (main.Sea.ShipX, main.Sea.ShipZ) = (Main.ShipBerth.X, Main.ShipBerth.Z);
            main.PlayerNode.GlobalPosition = new Vector3(138, 2, 0);
            await Frames(10);
            main.BoardVesselForTest();
            main.LeaveWheelForTest();
            main.PlayerNode.GlobalPosition = new Vector3(120, 2, 0);
            await Frames(30);
            if (main.VesselNode is { } ship)
            {
                var b = ship.GlobalTransform.Basis;
                var o = ship.GlobalPosition;
                await From("ownship-side", o + b * new Vector3(14, 4, 2), o + b * new Vector3(0, 3, 0));
                await From("ownship-bow", o + b * new Vector3(6, 5, -16), o + b * new Vector3(0, 3, 0));
                await From("ownship-stern", o + b * new Vector3(-7, 5, 15), o + b * new Vector3(0, 2.5f, 3));
                await From("ownship-above", o + b * new Vector3(9, 16, 8), o + b * new Vector3(0, 2, -1));
                await From("ownship-wheel", o + b * new Vector3(0, 4.6f, 4.6f), o + b * new Vector3(0, 2.6f, -4));
                await From("ownship-deck", o + b * new Vector3(1.2f, 3.4f, -6.5f), o + b * new Vector3(0, 3.2f, 4.5f));
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--treecouncil"))
        {
            // Hollowheart's council door, in the foot of a great tree.
            var town = main.World.Find("city:hollowheart")!;
            main.TerrainNode.EnsureAround(new Vector3((float)town.X, 0, (float)town.Z), 1);
            main.PlayerNode.GlobalPosition = new Vector3((float)town.X + 4, (float)main.World.Height(town.X + 4, town.Z + 4) + 1, (float)town.Z + 4);
            await Frames(120);
            var clerk = main.PlacesNode.People.FirstOrDefault(x => x.Npc.Id == "city:hollowheart" + TownCouncils.ClerkId);
            if (clerk is not null && Buildings.DoorNear(clerk.GlobalPosition) is { } way)
            {
                var outward = (way.Step - clerk.GlobalPosition) with { Y = 0 };
                var door = way.Step - (way.Step - clerk.GlobalPosition).Normalized() * 0;
                main.PlayerNode.GlobalPosition = way.Step + Vector3.Up;
                await Frames(30);
                var dir = (way.Step - new Vector3((float)Northwood.Hearts.MinBy(t => (t.X - way.Step.X) * (t.X - way.Step.X) + (t.Z - way.Step.Z) * (t.Z - way.Step.Z))!.X, way.Step.Y, (float)Northwood.Hearts.MinBy(t => (t.X - way.Step.X) * (t.X - way.Step.X) + (t.Z - way.Step.Z) * (t.Z - way.Step.Z))!.Z)) with { Y = 0 };
                dir = dir.Normalized();
                await From("treecouncil-door", way.Step + dir * 9 + Vector3.Up * 3, way.Step - dir * 2 + Vector3.Up * 2.2f);
                await From("treecouncil-wide", way.Step + dir * 22 + dir.Cross(Vector3.Up) * 8 + Vector3.Up * 9, way.Step - dir * 4 + Vector3.Up * 5);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--colonywall"))
        {
            // A colony grown to its barracks and its first fortified wall, with its guards at their posts.
            Vector3? isle = null;
            for (var x = -5400; x <= 5400 && isle is null; x += 600)
                for (var z = -5400; z <= 5400 && isle is null; z += 600)
                    if (main.NoRoomForColonyForTest(new Vector3(x, 0, z)) is null) isle = new Vector3(x, 0, z);
            if (isle is { } land)
            {
                main.TerrainNode.EnsureAround(land, 1);
                main.PlayerNode.GlobalPosition = land with { Y = (float)main.World.Height(land.X, land.Z) + 1 };
                await Frames(30);
                while (main.PlayerNode.Hero.Level < 9) main.GainXp(Hero.XpToNext(main.PlayerNode.Hero.Level));
                main.PlayerNode.Crowns += 2000;
                main.FoundColonyForTest();
                if (main.Economy.HallOf(main.PlayerNode.Company, HallKind.Colony) is { } h)
                {
                    h.Members = 6;
                    h.Guards = 3;
                    main.Economy.Growth[h.Town] = Settling.Walls;
                    main.RedrawHallsForTest();
                    await Frames(20);
                    main.TendColoniesForTest();
                    await Frames(30);
                    var c = new Vector3((float)h.X, 0, (float)h.Z);
                    c.Y = City.FloorAt(c);
                    var gate = Mathf.Atan2(-c.Z, -c.X);
                    var outward = new Vector3(Mathf.Cos(gate), 0, Mathf.Sin(gate));
                    main.PlayerNode.GlobalPosition = c + outward * 60 + Vector3.Up * 2;
                    await From("colonywall-gate", c + outward * 52 + Vector3.Up * 6, c + outward * 20 + Vector3.Up * 3);
                    await From("colonywall-above", c + outward * 45 + new Vector3(0, 40, 0) + outward.Cross(Vector3.Up) * 30, c);
                    await From("colonywall-yard", c - outward * 6 + Vector3.Up * 3, c + outward * 18 + Vector3.Up * 2);
                }
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--colonyshop"))
        {
            // A colony founded out in open country, and an apprentice keeping its market stall.
            Vector3? isle = null;
            for (var x = -5400; x <= 5400 && isle is null; x += 600)
                for (var z = -5400; z <= 5400 && isle is null; z += 600)
                    if (main.NoRoomForColonyForTest(new Vector3(x, 0, z)) is null) isle = new Vector3(x, 0, z);
            if (isle is { } land)
            {
                main.TerrainNode.EnsureAround(land, 1);
                main.PlayerNode.GlobalPosition = land with { Y = (float)main.World.Height(land.X, land.Z) + 1 };
                await Frames(30);
                while (main.PlayerNode.Hero.Level < 5) main.GainXp(Hero.XpToNext(main.PlayerNode.Hero.Level));
                main.PlayerNode.Crowns += 1000;
                main.FoundColonyForTest();
                var lad = new Bond { Id = "shot/wyn", KindId = "apprentice", Role = BondRole.Apprentice, Name = "Wyn", Race = "human", JoinAge = 15, Following = false };
                main.AddBondForTest(lad);
                main.StartKeepingForTest(lad, Keeping.Colony);
                await Frames(30);
                if (main.ColonyStallForTest is { } stall)
                {
                    var centre = main.Economy.HallOf(main.PlayerNode.Company, HallKind.Colony) is { } h ? new Vector3((float)h.X, stall.Y, (float)h.Z) : stall;
                    var inward = (centre - stall).Normalized();
                    await From("colonyshop-front", stall + inward * 7 + Vector3.Up * 2.5f, stall + Vector3.Up * 1.2f);
                    await From("colonyshop-yard", centre + (centre - stall).Normalized() * 14 + Vector3.Up * 9, stall);
                }
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--skyboat"))
        {
            // The hero's skyboat at the end of the pier: from beside her, off her bow, her stern, from above, and up in the air from below.
            main.FittedForTest(Tristinwold.Gameplay.Engines.Skyboat, 10);
            main.PlayerNode.GlobalPosition = new Vector3((float)main.Sea.SkyX + 3, 2, (float)main.Sea.SkyZ);
            await Frames(20);
            main.TendSkyboatForTest();
            await Frames(10);
            if (main.SkyboatNode is { } boat)
            {
                var b = boat.GlobalTransform.Basis;
                var o = boat.GlobalPosition;
                await From("skyboat-side", o + b * new Vector3(9, 3.5f, 1), o + b * new Vector3(0, 2.4f, 0));
                await From("skyboat-bow", o + b * new Vector3(5, 3, -10), o + b * new Vector3(0, 2.4f, 0));
                await From("skyboat-stern", o + b * new Vector3(-5, 3, 9), o + b * new Vector3(0, 2.2f, 0));
                await From("skyboat-above", o + b * new Vector3(6, 11, 5), o + b * new Vector3(0, 1, 0));
                await From("skyboat-inside", o + b * new Vector3(0.6f, 2.2f, 2.6f), o + b * new Vector3(0, 0.9f, -1.5f));
                boat.GlobalPosition = o + Vector3.Up * 14;
                await From("skyboat-aloft", o + b * new Vector3(10, 4, 6), boat.GlobalPosition + Vector3.Up * 2);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--warehouses"))
        {
            // The dock warehouse from where the hero comes up the road, and from above; the old storehouse from the street, from behind, and inside.
            await From("warehouse-street", new Vector3(88, 2.4f, 6), new Vector3(90, 1.8f, -13));
            await From("warehouse-above", new Vector3(104, 22, 6), City.DockWarehouse);
            var hidden = City.OldStorehouse;
            await From("storehouse-street", hidden * 0.8f + new Vector3(0, 2.2f, 0), hidden + Vector3.Up * 1.5f);
            await From("storehouse-behind", hidden * 1.09f + new Vector3(4, 2.6f, 0), hidden + Vector3.Up * 1.5f);
            await From("storehouse-above", hidden * 0.85f + new Vector3(0, 26, 0), hidden);
            if (Buildings.DoorNear(hidden * 1.07f) is { } door)
            {
                main.GoInsideForTest("storehouse", door.Seed, door.Step);
                await Frames(40);
                main.TendUndyingForTest();
                await Frames(10);
                cam.GlobalPosition = Main.KorrinTankForTest + new Vector3(2.5f, 2, 6);
                cam.LookAt(Main.KorrinTankForTest + Vector3.Up * 1.8f, Vector3.Up);
                cam.MakeCurrent();
                await Shot("storehouse-inside");
                main.GoOutsideForTest();
            }
            if (Buildings.DoorNear(City.DockWarehouse + new Vector3(0, 0, 4.5f)) is { } dock)
            {
                main.GoInsideForTest(dock.Kind, dock.Seed, dock.Step);
                await Frames(40);
                cam.GlobalPosition = Indoors.StageAt + new Vector3(0, 2.4f, 5);
                cam.LookAt(Indoors.StageAt + new Vector3(0, 1, -4), Vector3.Up);
                cam.MakeCurrent();
                await Shot("warehouse-inside");
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--minigames"))
        {
            // Through the hero's own eyes: the tavern's tables, the wagon's box, and the ship's wheel.
            main.ForestLog.Turlin.Met = true;
            main.ForestLog.Turlin.NextAt = 1e9;
            var room = main.EnterTavernForTest();
            await Frames(40);
            p.GlobalPosition = room.CardTable + new Vector3(-3, 0.5f, 3);
            p.Face(room.CardTable);
            await Frames(30);
            await Shot("tavern");
            main.ActionForTest();
            p.GlobalPosition = room.CardTable + new Vector3(1.2f, 0.5f, 0);
            await Frames(10);
            main.ActionForTest()?.Act();
            main.HudNode.Choose("Play for 5");
            await Frames(10);
            await Shot("twenty-one");
            main.HudNode.EndTalk();
            main.LeaveTavernForTest();
            await Frames(30);
            main.StartCaravanForTest(driver: true);
            await Frames(20);
            p.GlobalPosition = main.GuardedWagon!.GlobalPosition + new Vector3(3, 1, 0);
            await Frames(20);
            main.ClimbForTest();
            await Frames(240);
            await Shot("wagon-driver");
            main.ClimbDownForTest();
            await Frames(10);
            await Shot("wagon-beside");
            main.AbandonCaravanForTest();
            if (main.Sea.Rank == Tristinwold.World.SeaRank.Landsman) main.Sea.SignOn();
            main.SailForTest("port:druitt");
            main.ExploreForTest();
            await Frames(40);
            main.AtSeaNode!.TakeHelm();
            main.AtSeaNode.Heading = 0.4;
            await Frames(200);
            main.HudNode.Visible = true;
            await Shot("helm");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--exchange")) goto exchange;
        if (OS.GetCmdlineUserArgs().Contains("--wood"))
        {
            main.ForestLog.Turlin.Met = true; // he'd stop the shoot to pick a fight
            main.ForestLog.Turlin.NextAt = 1e9;
            main.HudNode.Visible = false;
            if (OS.GetCmdlineUserArgs().Contains("--quays"))
            {
                cam.Far = 4000;
                await From("quay-greywater", new Vector3(690, 26, -520), new Vector3(800, 0, -600));
                await From("orchard-greywater", new Vector3(600, 18, -400), new Vector3(640, 2, -490));
                await From("quay-druitt", new Vector3(-960, 22, -180), new Vector3(-1040, 0, -262));
                await From("yard-druitt", new Vector3(-850, 10, -330), new Vector3(-850, 2, -362));
                GetTree().Quit();
                return;
            }
            // The wood from above, down its road, in the maze, and the mountains behind it.
            await From("wood-above", new Vector3(-140, 110, -470), new Vector3(-140, 0, -700));
            await From("village-road", new Vector3(-146, 3, -558), new Vector3(-140, 12, -640));
            await From("village-trunk", new Vector3(-118, 5, -612), new Vector3(-140, 14, -640));
            await From("village-above", new Vector3(-60, 34, -600), new Vector3(-140, 10, -645));
            await From("guardian", new Vector3(-406, 6, -880), new Vector3(-420, 5, -905));
            cam.Far = 4000;
            await From("cove-above", new Vector3(120, 180, -60), new Vector3(640, 0, 180));
            await From("cove-mouth", new Vector3(760, 40, 40), new Vector3(1100, 0, -400));
            await From("greywater", new Vector3(560, 30, -500), new Vector3(640, 0, -600));
            await From("druitt", new Vector3(-820, 30, -200), new Vector3(-900, 0, -262));
            await From("wood-road", new Vector3(-162, 3, -340), new Vector3(-150, 3, -420));
            await From("wood-maze", new Vector3(-280, 3, -900), new Vector3(-320, 3, -900));
            await From("wood-ridge", new Vector3(-400, 40, -1010), new Vector3(-1100, 80, -1430));
            await From("wood-strand", new Vector3(-930, 8, -260), new Vector3(-1500, 0, -250));
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--turlin"))
        {
            // Turlin in every shape he comes back in, one at a time, then side by side.
            var stand = new Vector3(-700, 0, -300);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 26);
            await Frames(60);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            foreach (var (name, deaths, back, high) in new[] { ("turlin-1-close", 0, 1.5f, 1.75f), ("turlin-1-human", 0, 3.4f, 1.15f), ("turlin-1-fruit-aura", 0, 3.6f, 1.15f), ("turlin-6-fruit-aura", 5, 12f, 3.8f), ("turlin-2-turning", 1, 3.8f, 1.2f), ("turlin-3-half-beast", 2, 5f, 1.5f), ("turlin-4-horned", 3, 6.5f, 2f), ("turlin-5-demon", 4, 8f, 2.5f), ("turlin-6-giant-ape", 5, 12f, 3.8f) })
            {
                var e = main.ForestNode!.PoseRival(deaths, stand);
                if (name.Contains("aura")) main.ForestNode!.PoseAura(e);
                await Frames(40);
                e.Rotation = Vector3.Zero;
                await Frames(4);
                cam.GlobalPosition = new Vector3(stand.X + back * 0.2f, ground + high, stand.Z - back);
                cam.LookAt(new Vector3(stand.X, ground + high, stand.Z), Vector3.Up);
                cam.MakeCurrent();
                await Shot(name);
                e.QueueFree();
                await Frames(4);
            }
            var row = new List<Enemy>();
            for (var i = 0; i < 6; i++) row.Add(main.ForestNode!.PoseRival(i, stand + new Vector3((2.5f - i) * (3 + i * 0.5f), 0, 0)));
            await Frames(40);
            foreach (var e in row) e.Rotation = Vector3.Zero;
            await Frames(4);
            cam.GlobalPosition = new Vector3(stand.X, ground + 5, stand.Z - 26);
            cam.LookAt(new Vector3(stand.X, ground + 3, stand.Z), Vector3.Up);
            cam.MakeCurrent();
            await Shot("turlin-0-all");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--soren"))
        {
            // Soren as he comes back: from the man to the sea in a red coat, side by side.
            var stand = new Vector3(-700, 0, -300);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 26);
            main.ForestLog.Turlin.Met = true;
            await Frames(60);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            for (var i = 0; i < 6; i++)
            {
                main.Economy.SorenDeaths = main.Economy.SorenFall = i;
                var at = stand with { Y = ground } + new Vector3((2.5f - i) * 2.1f, 0, 0);
                var e = Enemy.Create(Bestiary.Get("dread_soren"), at, look: Legends.Look(main.Economy));
                e.Carried = true;
                main.AddEnemy(e);
                e.GlobalPosition = at;
                e.Scale = Vector3.One * Legend.FormOf(i).Size;
                e.Rotation = Vector3.Zero;
                e.HidePlate();
            }
            await Frames(40);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 1.9f, -6.2f);
            cam.LookAt(stand with { Y = ground } + new Vector3(0, 1.3f, 0), Vector3.Up);
            cam.MakeCurrent();
            await Shot("soren-forms");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(5.25f, 1.75f, -3f);
            cam.LookAt(stand with { Y = ground } + new Vector3(5.25f, 1.45f, 0), Vector3.Up);
            await Shot("soren-first");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--wukong"))
        {
            // Lu as he comes back: from the singing ape to the one with no words left, and a hand of his crew.
            var stand = new Vector3(-700, 0, -300);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 26);
            main.ForestLog.Turlin.Met = true;
            await Frames(60);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            for (var i = 0; i < 6; i++)
            {
                main.Economy.WukongDeaths = main.Economy.WukongFall = i;
                var at = stand with { Y = ground } + new Vector3((2.5f - i) * 2.3f, 0, 0);
                var e = Enemy.Create(Bestiary.Get("lu_wukong"), at, look: Legends.LuLook(main.Economy));
                e.Carried = true;
                main.AddEnemy(e);
                e.GlobalPosition = at;
                e.Scale = Vector3.One * Legend.WukongOf(i).Size;
                e.Rotation = Vector3.Zero;
                e.HidePlate();
            }
            var hand = stand with { Y = ground } + new Vector3(8.2f, 0, 0);
            var mate = Enemy.Create(Bestiary.Get("monkey_hand"), hand);
            mate.Carried = true;
            main.AddEnemy(mate);
            mate.GlobalPosition = hand;
            mate.Rotation = Vector3.Zero;
            mate.HidePlate();
            await Frames(40);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(1.2f, 2.1f, -7.6f);
            cam.LookAt(stand with { Y = ground } + new Vector3(1.2f, 1.3f, 0), Vector3.Up);
            cam.MakeCurrent();
            await Shot("wukong-forms");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(5.75f, 1.75f, -3f);
            cam.LookAt(stand with { Y = ground } + new Vector3(5.75f, 1.35f, 0), Vector3.Up);
            await Shot("wukong-first");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(5.75f, 1.75f, 3f);
            cam.LookAt(stand with { Y = ground } + new Vector3(5.75f, 1.35f, 0), Vector3.Up);
            await Shot("wukong-back");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(-3.5f, 2.2f, -4.2f);
            cam.LookAt(stand with { Y = ground } + new Vector3(-3.5f, 1.5f, 0), Vector3.Up);
            await Shot("wukong-last");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--skills"))
        {
            main.ForestLog.Turlin.Met = true;
            p.Join("EtherAeronaut");
            await Frames(20);
            while (p.Hero.Level < 12) p.Hero.Gain(Hero.XpToNext(p.Hero.Level));
            p.Hero.Raise("harpoon");
            p.Hero.Raise("harpoon");
            main.ShowSkills();
            await Frames(30);
            await Shot("skills");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--docks"))
        {
            main.ForestLog.Turlin.Met = true;
            await Frames(60);
            for (var x = 88; x <= 112; x += 2) GD.Print($"[docks] x {x}: land {WorldGen.Default.Height(x, 0):0.00}, floor {City.FloorAt(new Vector3(x, 0, 0)):0.00}, off the pier {WorldGen.Default.Height(x, 7.5):0.00}");
            main.HudNode.Visible = false;
            await From("docks-root", new Vector3(92, 3.2f, 6), new Vector3(112, 0, 0));
            await From("docks-side", new Vector3(96, 2.2f, -8), new Vector3(106, 0, 0));
            await From("docks-steam", new Vector3(84, 3.5f, -24), new Vector3(92, 5, -13));
            await From("docks-crane", new Vector3(108, 2.2f, -3), new Vector3(122, 7, 7.5f));
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--isle"))
        {
            main.ForestLog.Turlin.Met = true;
            main.BoardForTest();
            await Frames(90);
            main.HudNode.EndTalk();
            main.HudNode.Visible = false;
            var s = AtSea.Stage;
            cam.MakeCurrent();
            async Task View(string name, Vector3 from, Vector3 to)
            {
                cam.GlobalPosition = s + from;
                cam.LookAt(s + to, Vector3.Up);
                await Frames(30);
                await Shot(name);
            }
            await View("isle-sea", new Vector3(240, 70, 220), new Vector3(0, 45, -10));
            await View("isle-bow", new Vector3(30, 40, 290), new Vector3(0, 50, 0));
            await View("isle-stern", new Vector3(-130, 60, -280), new Vector3(0, 40, -40));
            await View("isle-west", new Vector3(-200, 40, -10), new Vector3(-30, 50, 0));
            await View("isle-east", new Vector3(170, 30, 40), new Vector3(0, 55, 0));
            await View("isle-street", new Vector3(70, 12, 64), new Vector3(30, 34, -6));
            await View("isle-market", new Vector3(74, 11.5f, -38), new Vector3(58, 16, 34));
            await View("isle-top", new Vector3(-30, 96, 60), new Vector3(10, 70, 170));
            main.RaidForTest("flight");
            main.HudNode.Visible = false;
            for (var i = 0; i < 400 && main.IsleNode!.Foes.All(e => e.GlobalPosition.DistanceTo(main.IsleNode.ShipAt(0)!.Value) > 60); i++) await Frames(4);
            await Frames(40);
            var near = main.IsleNode!.ShipAt(0)!.Value - s;
            await View("isle-battle", near + new Vector3(70, 14, 70), near);
            var foe = main.IsleNode!.Foes.FirstOrDefault();
            if (foe is not null) await View("isle-battle2", foe.GlobalPosition - s + new Vector3(30, 8, 40), foe.GlobalPosition - s);
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--stone"))
        {
            main.ForestLog.Turlin.Met = true;
            main.HudNode.Visible = false;
            await From("stone-near", Main.BladeStone + new Vector3(4, 2.2f, -5), Main.BladeStone + Vector3.Up * 1.5f);
            await From("stone-town", Main.BladeStone + new Vector3(16, 10, -16), Main.BladeStone + Vector3.Up);
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--quickkeys"))
        {
            foreach (var id in new[] { "healing_draught", "healing_draught", "healing_draught", "greater_healing", "bread" }) p.Pack.Add(id);
            p.Hero.Bar.Stock(["healing_draught", "greater_healing"]);
            p.Hero.Bar.Set(7, "bread");
            await Frames(100);
            await From("quickkeys", p.GlobalPosition + new Vector3(0, 4, 8), p.GlobalPosition + Vector3.Up);
            main.EnableTouch();
            await Frames(10);
            await From("quickkeys-touch", p.GlobalPosition + new Vector3(0, 4, 8), p.GlobalPosition + Vector3.Up);
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--beasts"))
        {
            // The new beasts of the wilds in rows, side on; the crossings and the kipine close; then a winged ape and a griffon up in the air.
            var stand = new Vector3(-150, 0, -120);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, -16);
            await Frames(30);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            async Task Row(string name, string[] kinds, float gap, Vector3 lens, Vector3 look, float shift = 0, bool spread = false)
            {
                var stand0 = stand;
                stand += new Vector3(shift, 0, 0);
                var row = new List<Enemy>();
                for (var i = 0; i < kinds.Length; i++)
                {
                    var e = Enemy.Create(Bestiary.Get(kinds[i]), stand);
                    e.Carried = true;
                    main.AddChild(e);
                    var at = stand + new Vector3((i - (kinds.Length - 1) / 2f) * gap, 0, 0);
                    e.GlobalPosition = at with { Y = City.FloorAt(at) };
                    e.RotationDegrees = new Vector3(0, 90, 0);
                    row.Add(e);
                }
                await Frames(2);
                if (spread) foreach (var e in row) foreach (var w in e.Figure.FindChildren("*", "Node3D", true, false))
                    if (w is FlapWings f) f.Flying = true;
                    else if (w is DragonWings dw) dw.Spread = true;
                await Frames(40);
                cam.GlobalPosition = stand with { Y = ground } + lens;
                cam.LookAt(stand with { Y = ground } + look, Vector3.Up);
                cam.MakeCurrent();
                await Shot(name);
                foreach (var e in row) e.QueueFree();
                await Frames(2);
                stand = stand0;
            }
            await Row("beasts-big", ["black_bear", "white_bear", "tiger", "crag_lion", "aurochs", "crocodile"], 3.0f, new Vector3(0, 2.2f, 10.5f), new Vector3(0, 0.8f, 0));
            await Row("beasts-small", ["feral_hound", "arctic_fox", "mountain_goat", "hare"], 1.6f, new Vector3(0, 1.1f, 5.0f), new Vector3(0, 0.45f, 0));
            await Row("beasts-crossings", ["sky_fox", "jackalope", "pegasus", "griffon"], 2.6f, new Vector3(0, 1.6f, 7.5f), new Vector3(0, 0.8f, 0), shift: 30);
            await Row("beasts-kipine", ["kipine"], 1, new Vector3(-3.4f, 2.1f, 1.8f), new Vector3(-1.0f, 1.6f, 0));
            await Row("beasts-jackalope", ["jackalope", "sky_fox"], 1.4f, new Vector3(0.3f, 0.9f, 2.6f), new Vector3(0, 0.35f, 0), shift: 30);
            await Row("beasts-dragons", ["dragon_red"], 1, new Vector3(7f, 5.5f, 9f), new Vector3(0, 2.2f, 0), shift: 76);
            await Row("beasts-dragons-spread", ["dragon_red"], 1, new Vector3(4f, 9f, 13f), new Vector3(0, 3.2f, 0), shift: 76, spread: true);
            await Row("beasts-dragons-under", ["dragon_silver"], 1, new Vector3(2f, 1.2f, 12f), new Vector3(0, 4.5f, 0), shift: 76, spread: true);
            await Row("beasts-spread", ["griffon", "kipine", "pegasus"], 5.5f, new Vector3(0, 4.5f, 13f), new Vector3(0, 1.6f, 0), shift: 30, spread: true);
            await Row("beasts-spread2", ["winged_ape", "dire_imp", "sky_fox"], 4.5f, new Vector3(0, 3.5f, 10f), new Vector3(0, 1.3f, 0), shift: -30, spread: true);
            await Row("beasts-winged", ["winged_ape", "dire_imp", "imp"], 2.4f, new Vector3(0, 1.8f, 6.5f), new Vector3(0, 1.0f, 0), shift: -30);
            await Row("beasts-serpents", ["ember_wyrm", "sea_serpent", "leviathan"], 9f, new Vector3(0, 9f, 24f), new Vector3(0, 1.5f, 0));
            await Row("beasts-wyrm", ["ember_wyrm"], 1, new Vector3(-6.5f, 2.6f, 4.5f), new Vector3(-0.5f, 0.9f, 0));
            await Row("beasts-seaserpent", ["sea_serpent"], 1, new Vector3(-8f, 3.5f, 7f), new Vector3(1f, 1.4f, 0));
            // Up in the air: they take off as dragons do (with nobody near to come down and fight).
            p.GlobalPosition = stand + new Vector3(0, 1, -90);
            var fliers = new List<Enemy>();
            foreach (var (kind, dx) in new[] { ("winged_ape", -3f), ("griffon", 3f) })
            {
                var e = Enemy.Create(Bestiary.Get(kind), stand + new Vector3(dx, 0, 0));
                main.AddChild(e);
                var at = stand + new Vector3(dx, 0, 0);
                e.GlobalPosition = at with { Y = City.FloorAt(at) + 0.3f };
                e.TakeOff(9, 120);
                fliers.Add(e);
            }
            for (var k = 0; k < 4; k++)
            {
                await Frames(60);
                GD.Print($"flight {k}: {string.Join(", ", fliers.Select(f => $"{f.Kind.Id} {f.State} airborne={f.Airborne} wants {f.AltitudeForTest:0.0} up {f.GlobalPosition.Y - City.FloorAt(f.GlobalPosition):0.0}"))}");
            }
            var mid = fliers[1].GlobalPosition;
            GD.Print($"fliers: {string.Join(", ", fliers.Select(f => $"{f.Kind.Id} canfly={f.CanFly} airborne={f.Airborne} up {f.GlobalPosition.Y - City.FloorAt(f.GlobalPosition):0.0} m"))}");
            cam.GlobalPosition = mid + new Vector3(4f, 1.5f, 6f);
            cam.LookAt(mid, Vector3.Up);
            await Shot("beasts-flying");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--gear"))
        {
            // Close on the armoury's pieces laid out in a row, then heroes in armour, helms and hats, then the tails from behind (run with --humans).
            var stand = new Vector3(-150, 0, -120);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, -12);
            await Frames(30);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            var rack = new (string Id, string? Tint, bool Metal, string? Glow)[]
            {
                ("1H_Sword", "#e4ecf4", true, null), ("2H_Sword", null, true, null), ("Knife", null, true, null), ("1H_Axe", null, false, null), ("2H_Axe", "#e4ecf4", true, null),
                ("2H_Staff", null, false, null), ("1H_Wand", null, false, null), ("2H_Crossbow", null, false, null), ("Round_Shield", null, false, null), ("Badge_Shield", null, true, null),
            };
            var laid = new List<Node3D>();
            for (var i = 0; i < rack.Length; i++)
            {
                var piece = Armoury.Make(rack[i].Id, rack[i].Tint is { } t ? new Color(t) : null, rack[i].Metal, rack[i].Glow)!;
                main.AddChild(piece);
                var shield = Armoury.IsShield(rack[i].Id);
                piece.GlobalPosition = stand with { Y = ground } + new Vector3((i - (rack.Length - 1) / 2f) * 0.36f, shield ? 0.75f : 0.25f, 0);
                if (shield) piece.Scale = Vector3.One * 0.45f;
                if (rack[i].Id is "2H_Sword" or "2H_Axe" or "2H_Staff") piece.Scale = Vector3.One * 0.7f;
                laid.Add(piece);
            }
            await Frames(20);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 0.75f, 2.3f);
            cam.LookAt(stand with { Y = ground } + new Vector3(0, 0.6f, 0), Vector3.Up);
            cam.MakeCurrent();
            await Shot("gear-rack");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(-0.9f, 0.62f, 0.75f);
            cam.LookAt(stand with { Y = ground } + new Vector3(-0.9f, 0.55f, 0), Vector3.Up);
            await Shot("gear-blades");
            foreach (var n in laid) n.QueueFree();
            // Heroes kitted out.
            var who = new (string Class, Tristinwold.Gameplay.Avatar Look, string[] Gear)[]
            {
                ("Paladin", Tristinwold.Gameplay.Avatar.For("Paladin") with { Headwear = "none" }, ["steel_plate", "steel_helm", "steel_sword", "iron_shield"]),
                ("Barbarian", Tristinwold.Gameplay.Avatar.For("Barbarian") with { Headwear = "none" }, ["fur_armor", "horned_hat", "iron_axe"]),
                ("Wizard", Tristinwold.Gameplay.Avatar.For("Wizard") with { Headwear = "none" }, ["wizard_hat"]),
                ("Thief", Tristinwold.Gameplay.Avatar.For("Thief") with { Headwear = "none" }, ["leather_armor", "leather_cap", "steel_sword"]),
                ("Thief", Tristinwold.Gameplay.Avatar.For("Thief") with { Headwear = "none", Race = Tristinwold.Gameplay.Races.Dragonkind, Dragon = "Red" }, ["iron_mail"]),
                ("Thief", Tristinwold.Gameplay.Avatar.For("Thief") with { Headwear = "none", Race = "ape" }, []),
                ("Thief", Tristinwold.Gameplay.Avatar.For("Thief") with { Headwear = "none", Race = "lizard" }, []),
            };
            var row = new List<Figure>();
            for (var i = 0; i < who.Length; i++)
            {
                var f = Figure.Create(who[i].Class, who[i].Look);
                main.AddChild(f);
                f.GlobalPosition = stand with { Y = ground } + new Vector3((i - (who.Length - 1) / 2f) * 1.1f, 0, 0);
                f.RotationDegrees = new Vector3(0, 180, 0); // facing the lens
                var pack = new Tristinwold.Gameplay.Pack();
                foreach (var g in who[i].Gear) { pack.Add(g); pack.Equip(g); }
                if (pack.Worn.Count > 0) Wardrobe.Wear(f, pack);
                row.Add(f);
            }
            await Frames(40);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 1.35f, 4.6f);
            cam.LookAt(stand with { Y = ground } + new Vector3(0, 1.0f, 0), Vector3.Up);
            await Shot("gear-heroes");
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(-1.6f, 1.6f, 2.0f);
            cam.LookAt(stand with { Y = ground } + new Vector3(-1.6f, 1.3f, 0), Vector3.Up);
            await Shot("gear-heads");
            foreach (var f in row) f.RotationDegrees = new Vector3(0, -45, 0);
            await Frames(10);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(2.2f, 1.2f, 3.0f);
            cam.LookAt(stand with { Y = ground } + new Vector3(2.2f, 0.8f, 0), Vector3.Up);
            await Shot("gear-tails");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--kit"))
        {
            // Each of the game's own weapons and shields under the KayKit piece it was made from, five to a picture.
            var stand = new Vector3(-100, 0, 60);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 14);
            await Frames(30);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            var ids = KitDesigns.All.ToList();
            foreach (var look in new[] { "Knight/1H_Sword", "Knight/2H_Sword", "Barbarian/1H_Axe", "Barbarian/2H_Axe", "Rogue/Knife", "Mage/2H_Staff", "Mage/1H_Wand", "Rogue/1H_Crossbow", "Knight/Round_Shield", "Knight/Rectangle_Shield", "Knight/Badge_Shield" })
            {
                var parts = look.Split('/');
                var src = Models.Scene($"characters/{parts[0]}.glb")?.Instantiate() as Node3D;
                if (src?.FindChild(parts[1], true, false) is MeshInstance3D o)
                    GD.Print($"PROBE {look}: glb aabb {o.Mesh.GetAabb()} xform {o.Transform} | ours {KitDesigns.Base(parts[1])?.GetAabb()}");
                else GD.Print($"PROBE {look}: not found");
                src?.Free();
            }
            var groups = ids.Chunk(5).Select(c => c.ToList()).ToArray();
            var names = groups.Select((_, g) => $"kit-{g + 1}").ToArray();
            for (var g = 0; g < groups.Length; g++)
            {
                var laid = new List<Node3D>();
                var row = groups[g];
                for (var i = 0; i < row.Count; i++)
                {
                    var item = Tristinwold.Gameplay.Items.Get(row[i])!;
                    var x = (i - (row.Count - 1) / 2f) * 0.72f;
                    var ours = KitDesigns.Build(item, Transform3D.Identity);
                    var piece = item.Look?.Split('/') is [_, var name] ? name : "";
                    var theirs = KitDesigns.Base(piece) is { } m ? new MeshInstance3D { Mesh = m } : null;
                    foreach (var (node, up) in new[] { (ours, 0.35f), ((Node3D?)theirs, 1.35f) }) // KayKit's above, ours below
                    {
                        if (node is null) continue;
                        if (node is MeshInstance3D mi && node == theirs) Styled.Apply(mi);
                        main.AddChild(node);
                        node.Scale = Vector3.One * 0.42f;
                        node.GlobalPosition = stand with { Y = ground } + new Vector3(x, up, 0);
                        if (item.Slot == Tristinwold.Gameplay.Slot.Offhand) node.GlobalPosition += Vector3.Up * 0.2f;
                        if (piece.Contains("Crossbow")) node.RotationDegrees = new Vector3(-90, 0, 0);
                        laid.Add(node);
                    }
                    GD.Print($"kit {row[i]}: {item.Name}");
                }
                await Frames(20);
                cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 1.05f, 3.1f);
                cam.LookAt(stand with { Y = ground } + new Vector3(0, 1.05f, 0), Vector3.Up);
                cam.MakeCurrent();
                await Shot(names[g]);
                foreach (var n in laid) n.QueueFree();
            }
            // And on heroes, the way they're seen in the game.
            var who = new (string Class, string[] Gear)[]
            {
                ("Paladin", []), // KayKit's own sword and shield, to compare
                ("Paladin", ["steel_sword", "steel_shield"]), ("Barbarian", ["steel_greataxe"]), ("Thief", ["shadow_dagger", "parrying_dagger"]),
                ("Wizard", ["storm_staff"]), ("Barbarian", ["sorens_reaver"]), ("Paladin", ["dragonbone_sword", "hollow_shield"]), ("Ranger", ["heavy_crossbow"]),
            };
            for (var i = 0; i < who.Length; i++)
            {
                var f = Figure.Create(who[i].Class, Tristinwold.Gameplay.Avatar.For(who[i].Class) with { Headwear = "none" });
                main.AddChild(f);
                f.GlobalPosition = stand with { Y = ground } + new Vector3((i - (who.Length - 1) / 2f) * 1.1f, 0, 0);
                f.RotationDegrees = new Vector3(0, 180, 0);
                var pack = new Tristinwold.Gameplay.Pack();
                foreach (var gear in who[i].Gear) { pack.Add(gear); pack.Equip(gear); }
                Wardrobe.Wear(f, pack);
            }
            await Frames(40);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 1.35f, 4.8f);
            cam.LookAt(stand with { Y = ground } + new Vector3(0, 1.0f, 0), Vector3.Up);
            await Shot("kit-heroes");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--forged"))
        {
            // A row of knights, each holding a sword off a different forge, so the differences show.
            var stand = new Vector3(-150, 0, -120);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 12);
            await Frames(30);
            main.HudNode.Visible = false;
            var ground = City.FloorAt(stand);
            var forges = new[] { "city:tristinwold", "city:cinderhelm", "city:druitt", "city:argentdeep", "city:vespergate", "city:galeholt" };
            for (var i = 0; i < forges.Length; i++)
            {
                var pack = new Tristinwold.Gameplay.Pack();
                var serial = Tristinwold.Gameplay.Forging.Serial("steel_sword", (ulong)(i + 1) * 7919, forges[i], Tristinwold.Gameplay.Forging.ForgeAt(forges[i]).Name, 8, 20, "Shots");
                pack.Add(serial);
                pack.Equip(serial);
                var f = Figure.Create("Barbarian", Tristinwold.Gameplay.Avatar.For("Barbarian") with { Headwear = "none" });
                main.AddChild(f);
                f.GlobalPosition = stand with { Y = ground } + new Vector3((i - 2.5f) * 1.5f, 0, 0);
                Wardrobe.Wear(f, pack);
                GD.Print(Tristinwold.Gameplay.Items.Get(serial)!.Name);
            }
            await Frames(40);
            cam.GlobalPosition = stand with { Y = ground } + new Vector3(0, 1.3f, -4.2f);
            cam.LookAt(stand with { Y = ground } + new Vector3(0, 0.95f, 0), Vector3.Up);
            cam.MakeCurrent();
            await Shot("forged-row");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--seasons"))
        {
            // The same view of the woods in each season.
            foreach (var (name, day) in new[] { ("spring", 3), ("summer", 10), ("fall", 17), ("winter", 25) })
            {
                main.Economy.Day = day;
                await From("season-" + name, new Vector3(-150, 6, -96), new Vector3(-150, 2, -130));
                await Frames(160);
                await Shot("season-" + name);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--looks"))
        {
            // New cuts and mixed clothes in a row, then the artificers and the classes with new things in hand (run with --humans).
            var stand = new Vector3(-150, 0, -120);
            main.TerrainNode.EnsureAround(stand, 1);
            p.GlobalPosition = stand + new Vector3(0, 1, 9);
            await Frames(30);
            var (lens, lensAt) = (new Vector3(0, 1.3f, -5.6f), new Vector3(0, 1, 0));
            async Task Row(string name, (string Class, Tristinwold.Gameplay.Avatar Look)[] who)
            {
                var row = new List<Figure>();
                for (var i = 0; i < who.Length; i++)
                {
                    var f = Figure.Create(who[i].Class, who[i].Look);
                    main.AddChild(f);
                    var at = stand + new Vector3((i - (who.Length - 1) / 2f) * 1.25f, 0, 0);
                    f.GlobalPosition = at with { Y = City.FloorAt(at) };
                    row.Add(f);
                }
                await Frames(40);
                cam.GlobalPosition = stand with { Y = City.FloorAt(stand) } + lens;
                cam.LookAt(stand with { Y = City.FloorAt(stand) } + lensAt, Vector3.Up);
                cam.MakeCurrent();
                await Shot(name);
                foreach (var f in row) f.QueueFree();
            }
            Tristinwold.Gameplay.Avatar Base(string look, string sex) => Tristinwold.Gameplay.Avatar.For(look) with { Sex = sex, Headwear = "none" };
            (lens, lensAt) = (new Vector3(0, 1.62f, -1.7f), new Vector3(0, 1.52f, 0)); // close on the faces
            await Row("looks-beards", [
                ("Barbarian", Base("Barbarian", "Male") with { Beard = "Long", Hair = "Grey", Age = "Seasoned" }),
                ("Paladin", Base("Paladin", "Male") with { Beard = "Forked", Hair = "Copper" }),
                ("Thief", Base("Thief", "Male") with { Beard = "Short", Hair = "Black" }),
            ]);
            (lens, lensAt) = (new Vector3(0, 1.75f, -3.1f), new Vector3(0, 1.5f, 0)); // close on the heads
            await Row("looks-crowned", [
                ("Wizard", Base("Wizard", "Male") with { Race = "shroomie" }),
                ("Wizard", Base("Wizard", "Female") with { Race = "plant" }),
                ("Assassin", Base("Assassin", "Male") with { Race = "shroomie" }),
                ("Assassin", Base("Assassin", "Female") with { Race = "plant", Form = "Fall" }),
            ]);
            (lens, lensAt) = (new Vector3(0, 1.3f, -5.6f), new Vector3(0, 1, 0));
            await Row("looks-hair", [
                ("Thief", Base("Thief", "Male") with { Style = "Topknot", Hair = "Black", Top = "Forest", Bottom = "Sand", Wear = "Wrapped robe" }),
                ("Thief", Base("Thief", "Female") with { Style = "Bun", Hair = "Chestnut", Top = "Teal", Bottom = "Slate", Wear = "Vest" }),
                ("Thief", Base("Thief", "Male") with { Style = "Mohawk", Hair = "Red", Top = "Charcoal", Bottom = "Black", Sleeves = "Short", Wear = "Plain" }),
                ("Thief", Base("Thief", "Female") with { Style = "Spikes", Hair = "Platinum", Top = "Violet", Bottom = "Black", Wear = "Sash" }),
                ("Thief", Base("Thief", "Male") with { Style = "Mane", Hair = "Honey", Top = "Russet", Bottom = "Linen", Sleeves = "Short", Wear = "Cloak" }),
                ("Thief", Base("Thief", "Female") with { Style = "Twintails", Hair = "Rose", Top = "White", Bottom = "Royal", Wear = "Robe" }),
            ]);
            Tristinwold.Gameplay.Avatar Kin(string race, string sex) => Tristinwold.Gameplay.Avatar.For("Thief") with { Race = race, Sex = sex, Headwear = "none", Top = "Forest", Bottom = "Sand", Wear = "Wrapped robe" };
            await Row("looks-races", [("Thief", Kin("ape", "Male") with { Style = "Topknot", Hair = "Black" }), ("Thief", Kin("plant", "Female")), ("Thief", Kin("plant", "Female") with { Form = "Fall", Wear = "Plain" }), ("Thief", Kin("shroomie", "Male") with { Wear = "Vest" }), ("Thief", Kin("ape", "Female") with { Wear = "Vest", Top = "Crimson" })]);
            var (hands, wide) = (new Vector3(0, 1, -2.7f), new Vector3(0, 1.3f, -5.6f));
            (lens, lensAt) = (hands, new Vector3(0, 0.95f, 0));
            await Row("looks-grips", [.. new[] { "Alchemist", "Saboteur", "Paladin", "Barbarian" }.Select(c => (c, Base(c, "Male")))]);
            (lens, lensAt) = (wide, new Vector3(0, 1, 0));
            await Row("looks-classes", [.. new[] { "Alchemist", "EtherAeronaut", "Saboteur", "Sorcerer", "Poet", "Scribe", "Musician" }.Select(c => (c, Base(c, "Male")))]);
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--indoors"))
        {
            // The shops on the Gallows road, and a room of each kind.
            await From("store-road", new Vector3(74, 5, -2), new Vector3(78, 1, 13));
            await From("store-smithy", new Vector3(76, 2.2f, 5.5f), new Vector3(75, 1.2f, 13));
            foreach (var kind in new[] { "house", "tavern", "smithy", "church" })
            {
                main.GoInsideForTest(kind, 3, City.PlayerStart);
                await Frames(40);
                cam.GlobalPosition = p.GlobalPosition + new Vector3(0, 1.2f, 0.8f);
                cam.LookAt(p.GlobalPosition + new Vector3(0, 0.6f, -9), Vector3.Up);
                cam.MakeCurrent();
                await Shot("inside-" + kind);
                main.GoOutsideForTest();
                await Frames(10);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--mine"))
        {
            // The mound and its door, then the mine from the adit down to the gold (--mine-deep to see past the old face).
            await From("mine-mound", Main.MineDoor + new Vector3(3, 6, 24), Main.MineDoor + new Vector3(0, 3, -3));
            await From("mine-door", Main.MineDoor + new Vector3(1.5f, 2.4f, 7), Main.MineDoor + new Vector3(0, 1.8f, 0));
            main.EnterMineForTest();
            await Frames(90);
            if (OS.GetCmdlineUserArgs().Contains("--mine-deep")) main.MineLog.Stage = MineStage.Done;
            async Task Below(string name, Vector3 stand, Vector3 eye, Vector3 look)
            {
                p.GlobalPosition = stand + Vector3.Up;
                await Frames(40);
                cam.GlobalPosition = eye;
                cam.LookAt(look, Vector3.Up);
                cam.MakeCurrent();
                await Shot(name);
            }
            await Below("mine-adit", Cave.Landing, Cave.At(0, 7) + Vector3.Up * 2.2f, Cave.At(0, -8) + Vector3.Up);
            await Below("mine-timbering", Cave.InRoom("timbering"), Cave.At(0, -25) + Vector3.Up * 2.4f, Cave.At(0, -40) + Vector3.Up);
            await Below("mine-drift", Cave.At(-14, -40), Cave.At(-11, -39) + Vector3.Up * 1.7f, Cave.At(-22, -44) + Vector3.Up * 1.5f);
            await Below("mine-stope", Cave.InRoom("iron_stope"), Cave.At(-26, -44) + Vector3.Up * 2.5f, Cave.At(-38, -52) + Vector3.Up);
            await Below("mine-old-face", Cave.InRoom("old_face"), Cave.At(4, -113) + Vector3.Up * 2.2f, Cave.FaceAt + Vector3.Up * 1.5f);
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--scenery"))
        {
            // The photo-scanned scenery: a pine forest, the crags, and the harbour with its ships at anchor.
            Vector3? Find(Biome b)
            {
                for (var r = 300; r < 4000; r += 150)
                    for (var a = 0f; a < Mathf.Tau; a += 0.4f)
                    {
                        var (x, z) = (Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                        var steep = b != Biome.Crags || Math.Abs(main.World.Height(x + 4, z) - main.World.Height(x - 4, z)) + Math.Abs(main.World.Height(x, z + 4) - main.World.Height(x, z - 4)) > 4;
                        if (main.World.BiomeAt(x, z) == b && steep && main.World.SettlementAt(x, z) is null && main.World.Height(x, z) > WorldGen.WaterLevel + 2) return new Vector3(x, 0, z);
                    }
                return null;
            }
            await From("scenery-harbour", new Vector3(130, 9, 2), new Vector3(215, 2, -10));
            foreach (var (name, biome) in new[] { ("scenery-pines", Biome.PineForest), ("scenery-crags", Biome.Crags), ("scenery-autumn", Biome.AutumnForest) })
            {
                if (Find(biome) is not { } at) continue;
                p.GlobalPosition = at with { Y = City.FloorAt(at) + 1 };
                main.TerrainNode.EnsureAround(at, 2);
                await Frames(120);
                var ground = City.FloorAt(at);
                await From(name, at with { Y = ground + 6 } + new Vector3(0, 0, 18), at with { Y = ground + 3 } + new Vector3(0, 0, -20));
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--poise"))
        {
            // A horse across a hillside, its feet on the ground, watching the hero; a wolf beside.
            var world = main.World;
            // A hillside, not a cliff: a fair slope well above the water, with level enough ground round it for the camera.
            var best = (At: new Vector3(300, 0, 300), Slope: 0.0);
            for (var r = 350; r < 2500 && best.Slope < 0.22; r += 60)
                for (var a = 0.0; a < Math.Tau; a += 0.25)
                {
                    var (x, z) = (Math.Cos(a) * r, Math.Sin(a) * r);
                    if (world.Height(x, z) < WorldGen.WaterLevel + 8 || world.SettlementAt(x, z) is not null) continue;
                    var slope = Math.Abs(world.Height(x + 2, z) - world.Height(x - 2, z)) / 4;
                    var drop = world.Height(x, z) - Math.Min(world.Height(x, z + 8), world.Height(x + 8, z));
                    if (slope is > 0.18 and < 0.3 && drop < 3 && slope > best.Slope) best = (new Vector3((float)x, 0, (float)z), slope);
                }
            main.TerrainNode.EnsureAround(best.At, 2);
            p.GlobalPosition = best.At + new Vector3(5, 1, 1);
            await Frames(90);
            var ground = City.FloorAt(best.At);
            foreach (var (look, off, yaw) in new[] { ("horse", new Vector3(0, 0, 0), 0f), ("wolf", new Vector3(-3, 0, 2), 0.4f) })
                if (Body.For(look) is Creature c)
                {
                    main.AddChild(c);
                    var at = best.At + off;
                    c.GlobalPosition = at with { Y = City.FloorAt(at) };
                    c.LookAt(c.GlobalPosition + new Vector3(1, 0, 0.2f), Vector3.Up);
                    c.Move(0, true, false);
                }
            p.GlobalPosition = best.At + new Vector3(5, 1, 1) with { Y = City.FloorAt(best.At + new Vector3(5, 0, 1)) + 1 };
            await Frames(120);
            await From("poise-hillside", best.At + new Vector3(1, 0, 5.5f) with { Y = City.FloorAt(best.At + new Vector3(1, 0, 5.5f)) + 1.7f }, best.At + new Vector3(-0.5f, 0, 0) with { Y = ground + 1.1f });
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--visitors"))
        {
            // Two friends from the server by the start (--visitor-demo put them there), one of them walking over.
            var start = City.PlayerStart;
            main.VisitorsNode.Seen("Dax", new Moved("Barbarian", null, "Dax", start.X - 4, City.FloorAt(start + new Vector3(-4, 0, -3)), start.Z - 3, 0.9f, 3.8f, "Cheer", 1));
            await From("visitors", start + new Vector3(1, 2.2f, 5), start + new Vector3(0, 1, -2.5f));
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            await Shot("visitors-later");
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--faces"))
        {
            // Close up on a few of the townsfolk: their faces, hair and eyes (realistic people on).
            var picks = new List<Person>();
            foreach (var want in new Func<Person, bool>[] { x => x.Npc.Id == Main.WatchCaptain, x => x.Npc.Id == "tristinwold/harbourmaster", x => x.Npc.Race == "elf", x => x.Npc.Race == "dwarf", x => x.Npc.Race == "orc", x => x.Npc.Sex == Sex.Female && x.Npc.Age < 30 })
                if (main.Locals.Concat(main.PlacesNode.People).FirstOrDefault(x => want(x) && !picks.Contains(x)) is { } who) picks.Add(who);
            var n = 0;
            foreach (var who in picks)
            {
                var at = who.GlobalPosition;
                var head = at + Vector3.Up * (who.Figure is { } fig ? fig.Scale.Y * 1.5f : 1.5f);
                var from = head + new Vector3(0, 0.05f, 1.1f);
                p.GlobalPosition = at + new Vector3(0, 1, 3);
                await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout); // real seconds: let the idle settle
                who.TalkTo(from);
                await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
                await From($"face-{n++}-{who.Npc.Race ?? "human"}-{who.Npc.Name.Split(' ')[0]}", from, head);
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--sea"))
        {
            // The shipyard, then aboard at sea with pirates alongside.
            await From("sea-shipyard", Main.ShipyardAt + new Vector3(-10, 6, 10), Main.ShipyardAt + new Vector3(12, 1, -2));
            main.Sea.SignOn();
            main.SailForTest("port:druitt");
            await Frames(90);
            if (main.AtSeaNode is { } sea && main.Sea.Voyage is { } v)
            {
                (v.Out, v.Home) = ([0.05], []);
                sea.Along = 0.049;
                for (var i = 0; i < 900 && sea.Boarders.Count == 0; i++) await Frames(1);
                await Frames(30);
                await From("sea-boarded", AtSea.Aboard + new Vector3(-7, 4, -14), AtSea.Aboard + new Vector3(0, 0.5f, 2));
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--caravans"))
        {
            // The caravan yard; a wagon following the hero out on the road; the hero on a horse; the Host's breach.
            if (OS.GetCmdlineUserArgs().Contains("--breach")) goto breach;
            await From("caravan-yard", Main.YardAt + new Vector3(12, 7, 14), Main.YardAt + new Vector3(0, 1, -1));
            p.GlobalPosition = new Vector3(-215, 1, 40);
            main.WagonForShot();
            await Frames(10);
            p.GlobalPosition = new Vector3(-235, 1, 44);
            await Frames(120);
            var wagon = main.WagonNode!.GlobalPosition;
            await From("caravan-road", wagon + new Vector3(9, 4, 6), wagon + new Vector3(0, 1, -2));
            main.HorseForShot();
            await Frames(60);
            cam.GlobalPosition = p.GlobalPosition + new Vector3(5, 2.5f, 5);
            cam.LookAt(p.GlobalPosition, Vector3.Up);
            cam.MakeCurrent();
            await Shot("caravan-riding");
            breach:
            main.Story.Chapter = Chapter.Breach;
            await Frames(200);
            if (main.BreachNode is { } b)
            {
                p.GlobalPosition = b.At + new Vector3(14, 1, 10);
                await Frames(240);
                await From("story-breach", b.At + new Vector3(10, 5, 12), b.At + new Vector3(0, 2.5f, 0));
            }
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--pursuits"))
        {
            // The allotments, half of them growing (two ripe), the Tumblers' Run, and the house: a bare plot, half built, finished.
            var book = main.Pursuits;
            var now = main.GuildBook.PlaySeconds;
            book.Plots["allotment_0"] = new Planting { Seed = "potato_seed", PlantedAt = now - 9999 };
            book.Plots["allotment_1"] = new Planting { Seed = "cabbage_seed", PlantedAt = now - 200 };
            book.Plots["allotment_4"] = new Planting { Seed = "emberleaf_seed", PlantedAt = now - 9999 };
            await From("pursuit-allotments", Main.AllotmentsAt + new Vector3(0, 7, 13), Main.AllotmentsAt);
            await From("pursuit-course", Main.CourseAt + new Vector3(16, 14, 8), Main.CourseAt + new Vector3(-2, 0, -16));
            await From("pursuit-course-swing", Main.CourseAt + new Vector3(14, 4, -12), Main.CourseAt + new Vector3(7, 2, -18));
            await From("pursuit-plot", Main.HousePlotAt + new Vector3(0, 6, 14), Main.HousePlotAt);
            book.House.Owned = true;
            book.House.Built = 2;
            await From("pursuit-house-frame", Main.HousePlotAt + new Vector3(4, 6, 14), Main.HousePlotAt);
            book.House.Built = 3;
            await From("pursuit-house-walls", Main.HousePlotAt + new Vector3(4, 6, 14), Main.HousePlotAt);
            book.House.Built = 5;
            foreach (var f in Housing.Furniture) book.House.Furniture.Add(f.Id);
            await From("pursuit-house", Main.HousePlotAt + new Vector3(6, 7, 18), Main.HousePlotAt + new Vector3(0, 1, 2));
            // A fire out past the walls.
            p.Pack.Add("tinderbox");
            p.Pack.Add("pine_log", 3);
            p.GlobalPosition = new Vector3(-150, 1, -120);
            await Frames(30);
            for (var i = 0; i < 8 && main.FiresLit == 0; i++) { main.LightFireForTest(); await Frames(5); }
            await Frames(30);
            cam.GlobalPosition = p.GlobalPosition + new Vector3(3, 2.5f, 3);
            cam.LookAt(p.GlobalPosition - p.GlobalTransform.Basis.Z * 1.6f, Vector3.Up);
            cam.MakeCurrent();
            await Shot("pursuit-fire");
            GetTree().Quit();
            return;
        }
        await From("town-fishstall", new Vector3(64, 7, 0), new Vector3(64, 1, -13));
        await From("town-docks", new Vector3(92, 14, 22), new Vector3(118, 0, 0));
        await From("town-quarry", Main.QuarryAt + new Vector3(0, 14, 22), Main.QuarryAt + new Vector3(0, 1, -4));
        await From("town-woodlot", Main.WoodlotAt + new Vector3(0, 12, 22), Main.WoodlotAt + new Vector3(0, 2, -2));
        exchange:
        await From("town-exchange", new Vector3(30.3f, 5, -17.5f), Main.FactorAt + Vector3.Up);
        await From("town-exchange-above", Main.FactorAt + new Vector3(0, 30, 14), Main.FactorAt);
        await From("town-quarry-close", Main.QuarryAt + new Vector3(3, 4, 10), Main.QuarryAt + new Vector3(0, 0, -6));
        await From("town-saltbarrel", new Vector3(40, 5, 58), new Vector3(40, 1, 69));
        // A line in the water off the middle pier.
        p.GlobalPosition = new Vector3(124, 1, 0);
        await Frames(20);
        p.Face(new Vector3(124, 0, 10));
        p.Pack.Add("fishing_rod");
        if (main.WaterAhead() is { } w) main.CastForTest(w);
        await Frames(40);
        cam.GlobalPosition = new Vector3(118, 3, -4);
        cam.LookAt(new Vector3(124, 1, 5), Vector3.Up);
        cam.MakeCurrent();
        await Shot("town-fishing");
        GetTree().Quit();
    }

    /// <summary>The hero's limit break going off, a few moments of it (--limit, with --look for the class).</summary>
    async Task Limit()
    {
        var p = main.PlayerNode;
        p.GlobalPosition = new Vector3(-150, 1, -120);
        await Frames(30);
        for (var i = 0; i < 60 && !p.Stats.LimitReady; i++) { p.Stats.TakeHit(12, Tristinwold.Deviant.DamageKind.Physical, AttackKind.Weapon); p.Stats.Heal(12); }
        p.LimitBreak();
        for (var i = 0; i < 4; i++)
        {
            await Frames(1);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/limit-{p.Class.Id}-{i}.png");
        }
        GetTree().Quit();
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
