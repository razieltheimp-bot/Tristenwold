using Godot;
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
        if (OS.GetCmdlineUserArgs().Contains("--exchange")) goto exchange;
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
        if (OS.GetCmdlineUserArgs().Contains("--sea"))
        {
            // The shipyard, then aboard at sea with pirates alongside.
            await From("sea-shipyard", Main.ShipyardAt + new Vector3(-10, 6, 10), Main.ShipyardAt + new Vector3(12, 1, -2));
            main.Sea.SignOn();
            main.SailForTest("port:saltmere");
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
