using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Plays scripted fights headless and quits with 0 (pass) or 1 (fail), so the game can be
/// checked without anyone at the keyboard:
///   godot --headless --path game -- --selftest [--server 127.0.0.1:7777 --company Joe]
/// Alone it checks a fight is fought and reported, then explores the world: the map fills in,
/// a village is found, its people talk, and one quest of each kind is done for its reward.
/// With a server it also loses a fight, waits for that enemy to come back as a nemesis, and kills it.
/// </summary>
public partial class SelfTest(Main main) : Node
{
    readonly List<string> _log = [];
    readonly List<BattleReport> _reports = [];
    readonly List<NemesisAppears> _appearances = [];

    public override async void _Ready()
    {
        main.FightEnded += _reports.Add;
        main.Received += m => { if (m is NemesisAppears a) _appearances.Add(a); };
        try
        {
            await Frames(10);
            var p = main.PlayerNode;
            p.GlobalPosition = new Vector3(-150, 1, -120); // open ground, away from every lair
            await Frames(30);
            Check(City.AreaAt(p.GlobalPosition) == "The Tristinwold countryside", $"area is {City.AreaAt(p.GlobalPosition)}");
            Check(City.AreaAt(City.GallowsPerch) == "The Gallows Perch", "the Gallows Perch is where it should be");

            // 1. A plain fight: the sword kills a dock thug and the fight is reported as won.
            var thug = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -2.5f));
            await FightUntilDead(thug, 30);
            Check(thug.State == EnemyState.Dead, "the thug died");
            await Until(() => _reports.Count >= 1, 5);
            Check(_reports.Count == 1 && _reports[0].Result == BattleResult.Victory, $"fight reported as {(_reports.Count > 0 ? _reports[0].Result : "nothing")}");
            Check(_reports[0].Enemies.Single().KilledBy == p.Company, "the kill is credited to the company");

            // 2. The limit break fills from a beating and hits everything nearby.
            var a = Spawn("skeever", p.GlobalPosition + new Vector3(2, 0, -2));
            var b = Spawn("skeever", p.GlobalPosition + new Vector3(-2, 0, -2));
            await Frames(5);
            for (var i = 0; i < 12 && !p.Stats.LimitReady; i++) p.Stats.TakeHit(12, DamageKind.Physical, AttackKind.Weapon);
            Check(p.Stats.LimitReady, "the limit gauge filled");
            p.LimitBreak();
            await Frames(5);
            Check(a.State == EnemyState.Dead && b.State == EnemyState.Dead, "the limit break felled both skeevers");
            p.Stats.Heal(1000);
            await Until(() => !main.Tracker.InFight, 15);

            await Touch();
            await Explore();
            if (main.Choice.Account is { } account) await Account(account);
            await Teleport(new Vector3(-150, 0, -120));
            await Until(() => !main.Tracker.InFight, 15);

            if (main.Client is { } client)
            {
                await Until(() => client.Status == ClientStatus.Connected, 10);
                Check(client.Status == ClientStatus.Connected, client.LastError is { } err ? $"connected to the shared world ({err})" : "connected to the shared world");

                // 3. Lose a fight: that kind of enemy becomes a nemesis on the server.
                var killer = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -2.5f));
                killer.Provoke(p);
                await Frames(10);
                p.TakeHit(10_000, DamageKind.Physical, AttackKind.Weapon, false, killer);
                await Until(() => _reports.Any(r => r.Result == BattleResult.Defeat), 15);
                Check(_reports.Any(r => r.Result == BattleResult.Defeat), "the lost fight was reported");
                await Until(() => !p.Downed, 10);
                killer.QueueFree();
                p.GlobalPosition = new Vector3(-150, 1, -120);
                await Frames(30);

                // 4. Fight dock thugs until it comes back, then kill it.
                Enemy? nemesis = null;
                for (var fight = 0; fight < 10 && nemesis is null; fight++)
                {
                    var t = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -3));
                    t.Provoke(p);
                    await Until(() => _appearances.Count > 0 || t.NemesisId is not null, 4);
                    if (t.NemesisId is not null) { nemesis = t; break; }
                    t.Stats.TakeHit(10_000, DamageKind.Physical, AttackKind.Weapon); // not him: end it quickly
                    main.Tracker.Died(t.Id, p.Company, main.Clock);
                    await Until(() => !main.Tracker.InFight, 15);
                }
                Check(nemesis is not null, "the nemesis came back in a later fight");
                Check(nemesis!.Stats.MaxHealth > Bestiary.Get("dock_thug").Health, $"{nemesis.Stats.Name} is stronger ({nemesis.Stats.MaxHealth} health)");
                Check(nemesis.Stats.Name != "Dock Thug", $"it has a name: {nemesis.Stats.Name}");
                _log.Add($"Nemesis returned: {nemesis.Stats.Name}, {nemesis.Stats.MaxHealth:0} health, taunt: {nemesis.Taunt}");

                p.Stats.Heal(10_000);
                p.Element = DamageKind.Frost;
                await FightUntilDead(nemesis, 60, spells: true);
                Check(nemesis.State == EnemyState.Dead, "the nemesis died");
                var reportsBefore = _reports.Count;
                await Until(() => _reports.Count > reportsBefore || !main.Tracker.InFight, 15);
                var last = _reports.Last();
                var line = last.Enemies.FirstOrDefault(e => e.NemesisId == nemesis.NemesisId);
                Check(line is { Killed: true }, "the nemesis kill was reported with its id");
            }

            Finish(0);
        }
        catch (Exception ex)
        {
            _log.Add($"FAIL: {ex.Message}");
            Finish(1);
        }
    }

    // ---- a touch screen ----------------------------------------------------------------------

    async Task Touch()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        var touch = hud.Touch;
        main.EnableTouch();
        await Frames(3);
        Check(touch.Visible, "touch controls on screen");
        Vector2 Screen(Vector2 local) => touch.GetGlobalTransformWithCanvas() * local;

        // 9. The left thumb walks.
        var start = p.GlobalPosition;
        var forward = -p.GlobalTransform.Basis.Z;
        var stick = Screen(new Vector2(150, touch.Screen.Y - 150));
        await Finger(0, stick, true);
        await Slide(0, stick + new Vector2(0, -80), new Vector2(0, -80));
        await Frames(40);
        var walked = (p.GlobalPosition - start).Dot(forward);
        Check(walked > 2, $"the joystick walks you forward ({walked:0.0} m)");
        await Finger(0, stick + new Vector2(0, -80), false);
        await Frames(10);
        var stopped = p.GlobalPosition;
        await Frames(10);
        Check(stopped.DistanceTo(p.GlobalPosition) < 0.3f, "letting go stops you");

        // 10. The right thumb looks around.
        var yaw = p.Rotation.Y;
        var look = Screen(new Vector2(touch.Screen.X * 0.6f, touch.Screen.Y * 0.3f));
        await Finger(1, look, true);
        await Slide(1, look + new Vector2(120, 0), new Vector2(120, 0));
        await Finger(1, look + new Vector2(120, 0), false);
        Check(Math.Abs(Mathf.AngleDifference(yaw, p.Rotation.Y)) > 0.3f, "dragging on the right turns the view");

        // 11. The Strike button swings the sword.
        var skeever = Spawn("skeever", p.GlobalPosition - p.GlobalTransform.Basis.Z * 1.8f);
        await Frames(5);
        p.Face(skeever.GlobalPosition);
        await Tap(Screen(touch.CentreOf("attack")));
        Check(skeever.Stats.Health < skeever.Stats.MaxHealth, $"the Strike button hits ({skeever.Stats.Health:0}/{skeever.Stats.MaxHealth:0})");
        skeever.Stats.TakeHit(10_000, DamageKind.Physical, AttackKind.Weapon);
        main.Tracker.Died(skeever.Id, p.Company, main.Clock);

        // 12. The Map button opens the map, and the controls step aside for it.
        await Tap(Screen(touch.CentreOf("map")));
        await Frames(2);
        Check(hud.MapOpen && !touch.Visible, "the Map button opens the map");
        hud.ToggleMap();
        await Until(() => !main.Tracker.InFight, 15);
    }

    async Task Finger(int index, Vector2 at, bool down)
    {
        GetViewport().PushInput(new InputEventScreenTouch { Index = index, Position = at, Pressed = down }, inLocalCoords: true); // already in the game's own coordinates
        await Frames(2);
    }

    async Task Slide(int index, Vector2 at, Vector2 by)
    {
        GetViewport().PushInput(new InputEventScreenDrag { Index = index, Position = at, Relative = by }, inLocalCoords: true);
        await Frames(2);
    }

    async Task Tap(Vector2 at)
    {
        await Finger(5, at, true);
        await Finger(5, at, false);
        await Frames(3);
    }

    // ---- the world ---------------------------------------------------------------------------

    async Task Explore()
    {
        var (p, w, hud, journal) = (main.PlayerNode, main.World, main.HudNode, main.Journal);
        Check(w.AuthoredCities.Where(c => c.StartsKnown).All(c => main.Explored.Rumored.Contains(c.Id) || main.Explored.Discovered.Contains(c.Id)) && main.Explored.Rumored.Count == 4, "five cities are known from the start: Tristinwold, and four more on the map");

        // 3. Walk north: the land streams in under you and the map fills in behind you.
        for (var z = -150f; z >= -1500; z -= 150) await Teleport(new Vector3(0, 0, z));
        await Frames(30);
        Check(p.IsOnFloor() && Math.Abs(p.GlobalPosition.Y - w.Height(0, -1500)) < 1.5f, $"standing on the land at {main.Area} ({p.GlobalPosition.Y:0.0} m)");
        Check(main.Explored.IsRevealed(0, -1500) && main.Explored.IsRevealed(0, -750) && !main.Explored.IsRevealed(600, -1500), "the map fills in where you go and nowhere else");

        // The wilds are lived in: herds and packs come out around you, and a deer bolts when you get close.
        await Frames(60);
        Check(main.WildsNode.Count > 0, $"the wilds are lived in: {main.WildsNode.Count} creatures about ({string.Join(", ", main.WildsNode.Kinds)})");
        var herd = Wildlife.Near(w, 0, -1500, 2500).First(h => h.KindId == "deer");
        await Teleport(new Vector3((float)herd.X + 45, 0, (float)herd.Z));
        await Until(() => main.Enemies().Any(e => e.Kind.Id == "deer" && e.GlobalPosition.DistanceTo(p.GlobalPosition) < 80), 5);
        var deer = main.Enemies().Where(e => e.Kind.Id == "deer").MinBy(e => e.GlobalPosition.DistanceTo(p.GlobalPosition));
        if (deer is null) Check(false, "a deer herd grazes where the wilds say it should");
        else
        {
            var near = deer.GlobalPosition + new Vector3(8, 0, 0);
            p.GlobalPosition = near with { Y = City.FloorAt(near) + 1 };
            await Frames(150);
            var away = deer.GlobalPosition.DistanceTo(p.GlobalPosition);
            Check(deer.State == EnemyState.Fleeing || away > 20, $"a deer bolts when you come close (8 m, then {away:0} m)");
        }

        // A deep lake: you swim at the surface instead of walking on the bottom.
        var lake = FindDeepWater(0, -1500);
        await Teleport(new Vector3((float)lake.X, 0, (float)lake.Z));
        await Frames(120);
        var floating = (float)WorldGen.WaterLevel - p.GlobalPosition.Y;
        Check(p.Swimming && floating < Fighter.SwimDepth + 0.3f && w.Height(lake.X, lake.Z) < WorldGen.WaterLevel - 3, $"swimming in deep water, {floating:0.0} m under the surface");

        // 4. Find a village with work going, and talk to the one offering it.
        var (home, giver, slay) = FindQuest(QuestKind.Slay, 0, -1500);
        var person = await Visit(home, giver);
        Check(main.Explored.Discovered.Contains(home.Id), $"discovered {home.Name}, {home.What}");
        main.Talk(person);
        Check(hud.Talking && hud.TalkText == giver.Greeting, $"{giver.Name} says: {giver.Greeting}");
        hud.Choose("Who are you?");
        Check(hud.TalkText == giver.Backstory, $"backstory: {giver.Backstory}");
        hud.Choose("What do you care about?");
        Check(hud.TalkText.Length > 10, $"interests: {string.Join(", ", giver.Interests)}");
        hud.Choose("Any work");
        Check(hud.TalkText == slay.Pitch, $"the pitch: {slay.Pitch}");
        hud.Choose("I'll do it");
        Check(journal.Get(slay.Id) is { Stage: QuestStage.Active }, $"quest taken: {slay.Title}");
        Check(main.Explored.Rumored.Contains(slay.TargetId) || main.Explored.Discovered.Contains(slay.TargetId), "the target is on the map");
        var heard = main.Explored.Rumored.Count;
        hud.Choose("Heard any news");
        Check(main.Explored.Rumored.Count == heard + 1 || hud.TalkText.StartsWith("Nothing"), $"news: {hud.TalkText}");
        hud.Choose("Farewell");
        Check(!hud.Talking, "the conversation ends");

        // 5. Clear the lair and go back for the money.
        var lair = w.Find(slay.TargetId)!;
        await Teleport(new Vector3((float)lair.X, 0, (float)(lair.Z + lair.Radius + 4)));
        await Until(() => main.PlacesNode.IsBuilt(lair.Id), 5);
        var foes = main.Enemies().Where(e => e.SiteId == lair.Id).ToList();
        Check(foes.Count == lair.Lair!.Total, $"{lair.Name} holds {foes.Count} ({string.Join(", ", foes.Select(f => $"{f.Stats.Name} {f.Stats.MaxHealth:0}hp"))})");
        foreach (var foe in foes) await FightUntilDead(foe, 60, chase: true);
        Check(journal.Get(slay.Id) is { Stage: QuestStage.Return }, $"{lair.Name} is clear: {journal.Get(slay.Id)!.Objective}");
        var purse = p.Septims;
        main.Talk(await Visit(home, giver));
        Check(journal.Get(slay.Id) is { Stage: QuestStage.Done } && p.Septims == purse + slay.Reward, $"paid {slay.Reward} septims");
        hud.EndTalk();

        // 6. A letter to carry, a thing to fetch, a person to find.
        var deliver = FindQuest(QuestKind.Deliver, 0, -1500).Quest;
        main.Accept(deliver);
        var recipientHome = w.Find(deliver.TargetId)!;
        main.Talk(await Visit(recipientHome, w.PeopleOf(recipientHome).First(n => n.Id == deliver.PersonId)));
        Check(journal.Get(deliver.Id) is { Stage: QuestStage.Done }, $"delivered: {deliver.Title}");
        hud.EndTalk();

        var fetch = FindQuest(QuestKind.Fetch, 0, -1500).Quest;
        main.Accept(fetch);
        var item = await AtLair(fetch, () => main.PlacesNode.Pickups.FirstOrDefault(i => i.QuestId == fetch.Id));
        main.PickUp(item!);
        Check(journal.Get(fetch.Id) is { Stage: QuestStage.Return }, $"found {fetch.Item}");

        var rescue = FindQuest(QuestKind.Rescue, 0, -1500).Quest;
        main.Accept(rescue);
        var captive = await AtLair(rescue, () => main.PlacesNode.People.FirstOrDefault(c => c.Captive && c.Npc.Id == rescue.PersonId));
        main.Talk(captive!);
        Check(journal.Get(rescue.Id) is { Stage: QuestStage.Return }, $"found {rescue.PersonName}");
        hud.EndTalk();

        // 7. The map and quests are saved.
        main.Save();
        var save = CompanySave.FromJson(Godot.FileAccess.GetFileAsString(main.SavePath));
        Check(save.Quests.Count == 4 && save.Explored.Count > 0 && save.Discovered.Contains(home.Id) && save.Septims == p.Septims, $"saved to {main.SavePath}");

        // 8. Far past the first five cities, the world is still making new ones.
        var far = w.NearestCity(0, -12_000);
        await Teleport(new Vector3((float)far.X, 0, (float)(far.Z + far.Radius + 60)));
        await Until(() => main.Explored.Discovered.Contains(far.Id), 3);
        Check(!far.Authored && main.Explored.Discovered.Contains(far.Id), $"a city nobody wrote: {far.Name}, {far.Epithet}. {far.Story}");
        Check(main.Area == far.Name || main.Area.Contains(far.Name), $"area: {main.Area}");
        _log.Add($"Explored {main.Explored.Count} cells; {main.TerrainNode.Count} land chunks loaded.");
        p.Stats.Heal(10_000);
    }

    /// <summary>A logged-in character's progress reaches the server, ready for the next device.</summary>
    async Task Account(string account)
    {
        var c = main.Choice;
        await Until(() => main.Client?.Status == ClientStatus.Connected, 10);
        main.Save();
        AccountReply? loaded = null;
        for (var i = 0; i < 40 && loaded?.Save is null; i++)
        {
            loaded = await AccountClient.AskAsync(c.Server, new AccountRequest("load", account, Token: c.Token, ServerPassword: c.ServerPassword, Character: c.Character));
            if (loaded.Save is null) await Frames(15);
        }
        var save = loaded?.Save is { } json ? CompanySave.FromJson(json) : null;
        Check(save is { Quests.Count: 4 } && save.Septims == main.PlayerNode.Septims, $"{c.Character.Name}'s progress is on the server under {account}'s account");
        var sheet = loaded!.Characters.Single(x => x.Name == c.Character.Name);
        Check(sheet.Septims == main.PlayerNode.Septims, $"the login screen will show {sheet.Septims} septims at {sheet.Where}");
    }

    (double X, double Z) FindDeepWater(double x, double z)
    {
        for (var r = 0.0; r < 4000; r += 40)
            for (var a = 0.0; a < Math.Tau; a += 0.2)
            {
                var (px, pz) = (x + Math.Cos(a) * r, z + Math.Sin(a) * r);
                if (main.World.Height(px, pz) < WorldGen.WaterLevel - 4) return (px, pz);
            }
        return (260, 0); // Tristinwold's harbor
    }

    (Settlement Home, Npc Giver, Quest Quest) FindQuest(QuestKind kind, double x, double z)
    {
        var w = main.World;
        return w.SettlementsNear(x, z, 3000).Where(s => !s.Hostile && s.Id != "city:tristinwold").OrderBy(s => s.DistanceTo(x, z))
            .SelectMany(s => w.PeopleOf(s).Select(n => (Home: s, Giver: n, Quest: w.QuestOf(n))))
            .Where(t => t.Quest?.Kind == kind && main.Journal.Get(t.Quest.Id) is null)
            .Select(t => (t.Home, t.Giver, t.Quest!)).First();
    }

    /// <summary>Goes to someone and waits for their settlement to be built around them.</summary>
    async Task<Person> Visit(Settlement s, Npc npc)
    {
        var toCentre = new Vector2((float)(s.X - npc.X), (float)(s.Z - npc.Z)).Normalized() * 1.6f;
        await Teleport(new Vector3((float)npc.X + toCentre.X, 0, (float)npc.Z + toCentre.Y));
        await Until(() => Find(npc.Id) is not null, 5);
        return Find(npc.Id) ?? throw new Exception($"{npc.Name} is not at {s.Name}");
    }

    Person? Find(string npcId) => main.PlacesNode.People.FirstOrDefault(p => p.Npc.Id == npcId);

    async Task<T?> AtLair<T>(Quest q, Func<T?> thing) where T : class
    {
        await Teleport(new Vector3((float)q.TargetX + 1, 0, (float)q.TargetZ + 3));
        await Until(() => thing() is not null, 5);
        Check(thing() is not null, $"{typeof(T).Name.ToLowerInvariant()} waiting at {q.TargetName}");
        main.PlayerNode.Stats.Heal(10_000);
        return thing();
    }

    async Task Teleport(Vector3 at)
    {
        var p = main.PlayerNode;
        main.TerrainNode.EnsureAround(at, 1);
        p.GlobalPosition = at with { Y = City.FloorAt(at) + 1 };
        p.Velocity = Vector3.Zero;
        await Frames(20);
    }

    Enemy Spawn(string kind, Vector3 at)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at);
        main.AddEnemy(e);
        return e;
    }

    async Task FightUntilDead(Enemy e, double seconds, bool spells = false, bool chase = false)
    {
        var p = main.PlayerNode;
        for (var t = 0.0; t < seconds && e.State != EnemyState.Dead; t += 1.0 / 60)
        {
            if (p.Downed) { await Until(() => !p.Downed, 10); p.GlobalPosition = e.GlobalPosition + new Vector3(0, 0, 2); }
            if (chase && p.GlobalPosition.DistanceTo(e.GlobalPosition) > 2.2f) p.GlobalPosition = e.GlobalPosition + new Vector3(0, 0.3f, 1.8f);
            p.Face(e.GlobalPosition);
            p.Stats.Heal(5); // the scripted player is a good fighter, not an immortal one
            if (!(spells && p.Cast())) p.Swing();
            await Frames(1);
        }
    }

    async Task Frames(int n)
    {
        for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    async Task Until(Func<bool> condition, double seconds)
    {
        for (var t = 0.0; t < seconds && !condition(); t += 1.0 / 60) await Frames(1);
    }

    void Check(bool ok, string what)
    {
        _log.Add($"{(ok ? "ok  " : "FAIL")} {what}");
        if (!ok) throw new Exception(what);
    }

    void Finish(int code)
    {
        GD.Print(string.Join("\n", _log));
        GD.Print(code == 0 ? "SELFTEST PASSED" : "SELFTEST FAILED");
        GetTree().Quit(code);
    }
}
