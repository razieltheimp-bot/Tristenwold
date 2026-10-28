using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Plays scripted fights headless and quits with 0 (pass) or 1 (fail), so the game can be
/// checked without anyone at the keyboard:
///   godot --headless --path game -- --selftest [--server 127.0.0.1:7777 --company Joe]
/// Alone it checks a fight is fought and reported, then explores the world: the map fills in,
/// a village is found, its people talk, and one quest of each kind is done for its reward.
/// With a server it also loses a fight, waits for that enemy to come back as a deviant, and kills it.
/// </summary>
public partial class SelfTest(Main main) : Node
{
    readonly List<string> _log = [];
    readonly List<BattleReport> _reports = [];
    readonly List<DeviantAppears> _appearances = [];

    public override async void _Ready()
    {
        main.FightEnded += _reports.Add;
        main.Received += m => { if (m is DeviantAppears a) _appearances.Add(a); };
        main.RoamersNode.WildDeviants = false; // the scripted fights below lose on purpose: only the hunt's own test makes deviants
        main.TownLifeNode.Stirring = false; // no young thief lifts the scripted hero's purse mid-test: the town's own test turns it on
        try
        {
            await Frames(10);
            if (OS.GetCmdlineUserArgs().Contains("--hunt-only")) { await HuntTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--living-only")) { await LivingWorldTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--frontier-only")) { await FrontierTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--story-only")) { await StoryTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--caravan-only")) { await CaravanTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--raiders-only")) { await RaidersTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--ageing-only")) { await AgeingTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--wild-only")) { await StrangeWildsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--trade-only")) { await TradeTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--sound-only"))
            {
                if (OS.GetCmdlineUserArgs().Contains("--after-classes")) { await Teleport(new Vector3(-150, 0, -120)); await ClassesTest(); } // a busy countryside first
                await SoundTest(); Finish(0); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--classes-only")) { await Teleport(new Vector3(-150, 0, -120)); await ClassesTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--town-only")) { await TownWorkTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--pursuits-only")) { await PursuitsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--city-only") && main.Client is { } c0)
            {
                await Until(() => c0.Status == ClientStatus.Connected, 20);
                Check(c0.Status == ClientStatus.Connected, $"connected to the shared world{(c0.LastError is { } e0 ? $" ({e0})" : "")}");
                await SharedCityTest();
                Finish(0);
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--save-only")) { await SaveTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--logout-only")) { await SaveTest(); GD.Print(string.Join("\n", _log)); main.LeaveForTest(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--company-only")) { await StoryTest(); await CompanyTest(); await GuildTest(); await RaidTest(); await LairTest(); await DeviantLairTest(); await HuntTest(); await LivingWorldTest(); await FrontierTest(); await CaravanTest(); await RaidersTest(); await AgeingTest(); await StrangeWildsTest(); await TradeTest(); await TownWorkTest(); await PursuitsTest(); await SoundTest(); await SaveTest(); Finish(0); return; } // just the new systems
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
            var a = Spawn("giant_rat", p.GlobalPosition + new Vector3(2, 0, -2));
            var b = Spawn("giant_rat", p.GlobalPosition + new Vector3(-2, 0, -2));
            await Frames(5);
            for (var i = 0; i < 12 && !p.Stats.LimitReady; i++) p.Stats.TakeHit(12, DamageKind.Physical, AttackKind.Weapon);
            Check(p.Stats.LimitReady, "the limit gauge filled");
            p.LimitBreak();
            await Frames(5);
            Check(a.State == EnemyState.Dead && b.State == EnemyState.Dead, "the limit break felled both giant rats");
            p.Stats.Heal(1000);
            await Until(() => !main.Tracker.InFight, 15);

            await Touch();
            await Explore();
            await CompanyTest();
            await GuildTest();
            await RaidTest();
            await LairTest();
            await DeviantLairTest();
            await StrangeWildsTest();
            await TradeTest();
            if (main.Choice.Account is { } account) await Account(account);
            await Teleport(new Vector3(-150, 0, -120));
            await Until(() => !main.Tracker.InFight, 15);

            if (main.Client is { } client)
            {
                await Until(() => client.Status == ClientStatus.Connected, 10);
                Check(client.Status == ClientStatus.Connected, client.LastError is { } err ? $"connected to the shared world ({err})" : "connected to the shared world");

                await SharedCityTest();

                // 3. Lose a fight: that kind of enemy becomes a deviant on the server.
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
                Enemy? deviant = null;
                for (var fight = 0; fight < 10 && deviant is null; fight++)
                {
                    var t = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -3));
                    t.Provoke(p);
                    await Until(() => _appearances.Count > 0 || t.DeviantId is not null, 4);
                    if (t.DeviantId is not null) { deviant = t; break; }
                    t.Stats.TakeHit(10_000, DamageKind.Physical, AttackKind.Weapon); // not him: end it quickly
                    main.Tracker.Died(t.Id, p.Company, main.Clock);
                    await Until(() => !main.Tracker.InFight, 15);
                }
                Check(deviant is not null, "the deviant came back in a later fight");
                Check(deviant!.Stats.MaxHealth > Bestiary.Get("dock_thug").Health, $"{deviant.Stats.Name} is stronger ({deviant.Stats.MaxHealth} health)");
                Check(deviant.Stats.Name != "Dock Thug", $"it has a name: {deviant.Stats.Name}");
                _log.Add($"Deviant returned: {deviant.Stats.Name}, {deviant.Stats.MaxHealth:0} health, taunt: {deviant.Taunt}");

                p.Stats.Heal(10_000);
                p.Element = DamageKind.Frost;
                await FightUntilDead(deviant, 60, spells: true);
                Check(deviant.State == EnemyState.Dead, "the deviant died");
                var reportsBefore = _reports.Count;
                await Until(() => _reports.Count > reportsBefore || !main.Tracker.InFight, 15);
                var last = _reports.Last();
                var line = last.Enemies.FirstOrDefault(e => e.DeviantId == deviant.DeviantId);
                Check(line is { Killed: true }, "the deviant kill was reported with its id");
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
        for (var i = 0; i < 30 && !touch.Visible; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // shown on the next drawn frame
        Check(touch.Visible, $"touch controls on screen (touch on {hud.TouchEnabled}, phone pad {hud.PhonePad}, window {hud.WindowOpen}, talking {hud.Talking}: {hud.TalkText}, map {hud.MapOpen})");
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
        var rat = Spawn("giant_rat", p.GlobalPosition - p.GlobalTransform.Basis.Z * 1.8f);
        await Frames(5);
        p.Face(rat.GlobalPosition);
        await Tap(Screen(touch.CentreOf("attack")));
        Check(rat.Stats.Health < rat.Stats.MaxHealth, $"the Strike button hits ({rat.Stats.Health:0}/{rat.Stats.MaxHealth:0})");
        rat.Stats.TakeHit(10_000, DamageKind.Physical, AttackKind.Weapon);
        main.Tracker.Died(rat.Id, p.Company, main.Clock);

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
        var purse = p.Crowns;
        main.Talk(await Visit(home, giver));
        Check(journal.Get(slay.Id) is { Stage: QuestStage.Done } && p.Crowns == purse + slay.Reward, $"paid {slay.Reward} crowns");
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
        Check(save.Quests.Count == 4 && save.Explored.Count > 0 && save.Discovered.Contains(home.Id) && save.Crowns == p.Crowns, $"saved to {main.SavePath}");

        // 8. Far past the first five cities, the world is still making new ones.
        var far = w.NearestCity(0, -12_000);
        await Teleport(new Vector3((float)far.X, 0, (float)(far.Z + far.Radius + 60)));
        await Until(() => main.Explored.Discovered.Contains(far.Id), 3);
        Check(!far.Authored && main.Explored.Discovered.Contains(far.Id), $"a city nobody wrote: {far.Name}, {far.Epithet}. {far.Story}");
        Check(main.Area == far.Name || main.Area.Contains(far.Name), $"area: {main.Area}");
        _log.Add($"Explored {main.Explored.Count} cells; {main.TerrainNode.Count} land chunks loaded.");
        p.Stats.Heal(10_000);
        await DragonsTest();
    }

    /// <summary>An ancient in its lair, a dragon deviant growing with its wins, and two flights at war.</summary>
    async Task DragonsTest()
    {
        var (p, w) = (main.PlayerNode, main.World);
        var lord = Dragons.Near(w, p.GlobalPosition.X, p.GlobalPosition.Z, 20000).MinBy(a => a.DistanceTo(p.GlobalPosition.X, p.GlobalPosition.Z))!;
        await Teleport(new Vector3((float)lord.X + 220, 0, (float)lord.Z));
        await Until(() => main.WildsNode.Creatures.Any(e => e.Size == DragonSize.Ancient), 8);
        var ancient = main.WildsNode.Creatures.FirstOrDefault(e => e.Size == DragonSize.Ancient);
        Check(ancient is not null && ancient.Stats.Name == lord.FullName && ancient.Figure.Scale.X > 3,
            $"{lord.FullName}, {lord.What}, waits in its lair ({ancient?.Stats.MaxHealth:0} hp, {ancient?.Figure.Scale.X:0.0}x size)");

        // A dragon deviant that has beaten the company twice is a giant now.
        var red = Spawn("dragon_red", p.GlobalPosition + new Vector3(0, 0, 60));
        var before = red.Stats.MaxHealth;
        red.MakeDeviant(Guid.NewGuid(), new BattleModifiers("Ashjaw", new Dictionary<Stat, double>(), new Dictionary<DamageKind, Affinity>(), [], Rank.Stray, Victories: 2), null);
        Check(red.Size == DragonSize.Giant && red.Stats.MaxHealth > before * 4 && red.Figure.Scale.X > 2,
            $"a red dragon that beat the company twice grew to a giant ({before:0} to {red.Stats.MaxHealth:0} hp)");
        red.QueueFree();

        // A winged dragon takes to the air to reach you from afar; the white frost dragon has no wings.
        main.TerrainNode.EnsureAround(p.GlobalPosition, 2);
        Vector3 Ground(Vector3 at) => at with { Y = City.FloorAt(at) + 0.5f };
        var flyer = Spawn("dragon_bronze", Ground(p.GlobalPosition + new Vector3(90, 0, 0)));
        var walker = Spawn("dragon_white", Ground(p.GlobalPosition + new Vector3(-90, 0, 0)));
        await Frames(10);
        flyer.Provoke(p);
        float Up(Enemy e) => e.GlobalPosition.Y - City.FloorAt(e.GlobalPosition);
        await Until(() => Up(flyer) > 4, 8);
        Check(flyer.CanFly && Up(flyer) > 4 && !walker.CanFly, $"a bronze dragon flies in from 90 m ({flyer.GlobalPosition.Y - City.FloorAt(flyer.GlobalPosition):0} m up); the frost dragon walks");
        flyer.QueueFree();
        walker.QueueFree();

        // Find a season when this ancient is at war, and watch the border.
        DragonWar? war = null;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 400 && war is null; i++)
            war = Dragons.WarsNear(w, now.AddHours(i * Dragons.SeasonHours), lord.X, lord.Z, 15000).FirstOrDefault();
        if (war is null) { Check(false, "some ancient goes to war within 400 seasons"); return; }
        main.WildsNode.Now = () => now.AddHours((war.Season - Dragons.SeasonOf(now)) * Dragons.SeasonHours);
        await Teleport(new Vector3((float)war.FrontX + 160, 0, (float)war.FrontZ + 160));
        await Until(() => main.WildsNode.Creatures.Any(e => e.Faction == war.A.Id) && main.WildsNode.Creatures.Any(e => e.Faction == war.B.Id), 8);
        var sides = main.WildsNode.Creatures.Where(e => e.Foe is not null).ToList();
        await Until(() => sides.Any(e => IsInstanceValid(e) && e.Stats.Health < e.Stats.MaxHealth), 25);
        var hurt = sides.Count(e => IsInstanceValid(e) && (e.Stats.Health < e.Stats.MaxHealth || e.State == EnemyState.Dead));
        Check(hurt > 0, $"{war.A.FullName} ({war.A.Colour}) and {war.B.FullName} ({war.B.Colour}) are at war: {sides.Count} dragons at the border, {hurt} wounded");
        main.WildsNode.Now = () => DateTime.UtcNow;
    }

    /// <summary>
    /// Work in town and the city's economy: a rod from Bryn, a fish off the middle pier, grilled at the Salt Barrel;
    /// a pick from Dagna and ore from a Greyhollow vein; an axe from Hollin and logs from a felled pine; selling a lot
    /// brings the price down; a contract for what's short, filled at the Exchange; the city's works; a shift on the docks;
    /// a letter run; the days turning over; and the city in the save.
    /// </summary>
    async Task TownWorkTest()
    {
        var p = main.PlayerNode;
        var pack = p.Pack;
        var city = main.Economy;
        var hud = main.HudNode;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        p.Crowns = 600;
        int FishCount() => pack.Contents.Where(c => CityEconomy.GoodOf(c.Item.Id) == Good.Fish || c.Item.Id is "old_boot" or "pearl").Sum(c => c.N);

        // Fishing: a rod from Bryn, then a line off the middle pier.
        await Teleport(new Vector3(68, 0, -9));
        main.Talk(Who(Shops.Fishmonger.Id));
        Check(hud.Choose("Let me see") && hud.Choose("Fishing Rod") && pack.Count("fishing_rod") == 1, "Bryn sells you a fishing rod");
        hud.EndTalk();
        await Teleport(new Vector3(124, 0, 0));
        // Earlier tests leave things about that might come for you; fish in peace.
        foreach (var e in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => !e.Friendly && e.GlobalPosition.DistanceTo(p.GlobalPosition) < 60).ToList()) e.QueueFree();
        await Until(() => !main.Tracker.InFight && !p.Attackers().Any(), 30);
        p.Face(new Vector3(124, 0, 10));
        await Frames(3);
        var water = main.WaterAhead();
        Check(water is { Kind: Water.Sea }, $"from the pier you face the harbour ({water?.Kind})");
        var fish = FishCount();
        if (water is { } w) main.CastForTest(w);
        Check(main.LineOut, "the line goes out");
        await Until(() => main.Biting, 25);
        Check(main.Biting, "something bites");
        main.StrikeForTest();
        Check(FishCount() == fish + 1 && pack.JobXp.GetValueOrDefault(Job.Fishing) > 0 && !main.LineOut, $"and you land it: {hud.LastLine}");

        // Cooking at the Salt Barrel.
        pack.Add("herring", 2);
        await Teleport(new Vector3(40, 0, 65));
        main.Talk(Who(Shops.SaltBarrel.Id));
        Check(hud.Choose("Can I use your kitchen"), "Fenna lets you use her kitchen");
        Check(hud.Choose("Make Grilled Fish"), $"and you grill a fish ({hud.TalkText[..Math.Min(40, hud.TalkText.Length)]}...)");
        Check(pack.Count("grilled_fish") >= 1 && pack.JobXp.GetValueOrDefault(Job.Cooking) > 0, "it's in your pack, and you've learned a little cooking");
        hud.EndTalk();

        // Mining at Greyhollow.
        await Teleport(Main.QuarryAt + new Vector3(5, 0, 6));
        main.Talk(Who(Shops.Quarry.Id));
        Check(hud.Choose("Let me see") && hud.Choose("Pickaxe") && pack.Count("pickaxe") == 1, "Dagna sells you a pickaxe");
        hud.EndTalk();
        await Until(() => main.WorkingsNode.Shown.Any(s => s.Kind == "iron"), 10);
        var vein = main.WorkingsNode.Shown.FirstOrDefault(s => s.Kind == "iron");
        Check(vein is not null, $"the quarry's veins are there ({main.WorkingsNode.Shown.Count(s => !s.IsTree)} near)");
        if (vein is not null)
        {
            await Teleport(vein.At + (Main.QuarryAt - vein.At).Normalized() * 2.2f);
            Check(main.WorkingsNode.Within(p.GlobalPosition) == vein, "you can reach the iron vein");
            var ore = pack.Count("iron_ore");
            main.WorkForTest(vein);
            await Until(() => !main.Working && pack.Count("iron_ore") > ore, 20);
            Check(pack.Count("iron_ore") > ore && pack.JobXp.GetValueOrDefault(Job.Mining) > 0, $"you mine it: {hud.LastLine}");
            Check(!main.WorkingsNode.Shown.Contains(vein), "and the vein's worked out for now");
        }

        // Woodcutting at Hollin's woodlot.
        await Teleport(Main.WoodlotAt + new Vector3(6, 0, 9));
        main.Talk(Who(Shops.Woodlot.Id));
        Check(hud.Choose("Let me see") && hud.Choose("Woodcutter's Axe") && pack.Count("woodcutters_axe") == 1, "Hollin sells you an axe");
        hud.EndTalk();
        await Until(() => main.WorkingsNode.Shown.Any(s => s.Kind == "pine"), 10);
        var pine = main.WorkingsNode.Shown.FirstOrDefault(s => s.Kind == "pine");
        if (pine is not null)
        {
            await Teleport(pine.At + new Vector3(0, 0, 2.2f));
            var logs = pack.Count("pine_log");
            main.WorkForTest(pine);
            await Until(() => !main.Working && pack.Count("pine_log") > logs, 20);
            Check(pack.Count("pine_log") >= logs + 2 && pack.JobXp.GetValueOrDefault(Job.Woodcutting) > 0, $"you fell a pine: {hud.LastLine}");
            await Frames(60);
            Check(!main.WorkingsNode.Shown.Contains(pine), "and it leaves a stump");
        }
        else Check(false, "the woodlot's pines are there");

        // Selling a lot at once brings the price down.
        var herring = Items.Get("herring")!;
        var offer = city.Offer(herring);
        var factor = city.Factor(Good.Fish);
        pack.Add("herring", 60);
        await Teleport(new Vector3(68, 0, -9));
        main.Talk(Who(Shops.Fishmonger.Id));
        hud.Choose("I've things to sell");
        var crowns = p.Crowns;
        Check(hud.Choose("Sell every"), "Bryn buys the lot");
        Check(p.Crowns > crowns && city.Factor(Good.Fish) < factor * 0.85, $"for {p.Crowns - crowns} crowns, and fish is cheaper now ({factor:0.00}x to {city.Factor(Good.Fish):0.00}x; herring {offer} to {city.Offer(herring)})");
        hud.EndTalk();

        // A contract for what's short, filled at the Exchange.
        city.Stock[Good.Timber] = 5;
        city.Contracts.Clear();
        city.NextDay();
        var contract = city.Contracts.FirstOrDefault(c => c.Good == Good.Timber);
        Check(contract is not null, $"timber's short, so a contract's posted: {string.Join("; ", city.Contracts.Select(c => $"{c.Buyer} want {c.Wanted} {c.Good} at {c.PricePer}"))}");
        await Teleport(Main.FactorAt + new Vector3(0, 0, 2));
        var factorPerson = Who("tristinwold/factor");
        if (contract is not null)
        {
            pack.Add("pine_log", contract.Left);
            crowns = p.Crowns;
            main.Talk(factorPerson);
            Check(hud.Choose("What's wanted"), "Ottilie shows the contracts");
            Check(hud.Choose(char.ToUpperInvariant(contract.Buyer[0]) + contract.Buyer[1..]), "you deliver the timber");
            Check(p.Crowns >= crowns + contract.Wanted * contract.PricePer && !city.Contracts.Contains(contract), $"and it's filled: +{p.Crowns - crowns} crowns");
            hud.EndTalk();
        }
        // The city's works.
        pack.Add("stone_block", 10);
        var stone = city.WorkGoods.GetValueOrDefault(Good.Stone);
        main.Talk(factorPerson);
        Check(hud.Choose("What's the city building"), $"the city's building: {city.Work?.Title}");
        Check(hud.Choose("Give stone") && city.WorkGoods.GetValueOrDefault(Good.Stone) == stone + 10, "you give it ten blocks of stone");
        hud.EndTalk();
        main.Talk(factorPerson);
        Check(hud.Choose("How's trade") && hud.TalkText.Contains("Day") && hud.TalkText.Contains("Fish"), "and the market report");
        hud.EndTalk();

        // A letter for someone in town.
        main.Talk(factorPerson);
        hud.Choose("Any errands");
        var to = main.ErrandTo;
        Check(to is not null, $"Ottilie gives you a letter for {to?.Npc.Name}");
        crowns = p.Crowns;
        if (to is not null) Check(main.DeliverForTest(to) && p.Crowns > crowns && main.ErrandTo is null, $"and it's delivered for {p.Crowns - crowns} crowns");
        hud.EndTalk();

        // A shift on the docks.
        await Teleport(Main.ForemanAt + new Vector3(-1, 0, 0));
        main.Talk(Who("tristinwold/foreman"));
        Check(hud.Choose("Any work on the docks") && main.OnShift, "Haskel puts you to work");
        hud.EndTalk();
        await Teleport(Main.CrateStack + new Vector3(-2, 0, 0));
        main.LiftForTest();
        Check(main.Holding && p.Burdened, "you lift a crate, and walk slower with it");
        await Teleport(Main.Warehouse + new Vector3(0, 0, 1.5f));
        crowns = p.Crowns;
        var wage = city.DockWage;
        main.SetDownForTest();
        Check(!main.Holding && !p.Burdened && p.Crowns == crowns + wage, $"and set it down in the warehouse: +{wage} crowns");

        // The days turn over by themselves as you play.
        var day = city.Day;
        city.NextDayAt = main.GuildBook.PlaySeconds;
        await Frames(120);
        Check(city.Day > day, $"a new day at the Exchange (day {city.Day}): {string.Join(" ", city.News.TakeLast(2))}");

        // And it's all kept with the company.
        var saved = CompanySave.FromJson(main.Save());
        Check(saved.City is { } c2 && c2.Day == city.Day && c2.Renown == city.Renown && saved.Pack!.JobXp.ContainsKey(Job.Fishing), $"the city and the trades are saved (renown {city.Renown}: {city.RenownTitle})");
    }

    /// <summary>Every sound the game plays is there, the volumes are kept, and the music follows the hero: the wilds,
    /// the city, the Bards' College, a fight.</summary>
    async Task SoundTest()
    {
        var p = main.PlayerNode;
        Check(Sound.Current is not null, "the sound is up");
        string[] names =
        [
            "foot/grass", "foot/stone", "foot/wood", "foot/snow", "move/jump", "move/land", "move/wings",
            "melee/swing", "melee/hit", "melee/hit_metal", "melee/crit", "melee/punch", "melee/bow", "melee/arrow_hit",
            "magic/cast", "magic/fire_cast", "magic/fire_boom", "magic/frost_cast", "magic/frost_shatter", "magic/shock", "magic/thunder",
            "magic/poison", "magic/heal", "magic/holy", "magic/limit",
            "creature/growl_small", "creature/growl_big", "creature/roar_dragon", "creature/howl", "creature/shriek", "creature/die",
            "creature/hurt_hero", "creature/die_hero",
            "ui/click", "ui/open", "ui/close", "ui/coin", "ui/pickup", "ui/equip", "ui/potion", "ui/herb", "ui/anvil", "ui/levelup", "ui/quest", "ui/error",
            "bard/lute", "bard/harp", "bard/drum", "bard/horn", "bard/page", "bard/quill", "bard/chant",
        ];
        var missing = names.Where(n => !Sound.Has(n)).ToList();
        Check(missing.Count == 0, missing.Count == 0 ? $"all {names.Length} kinds of sound are there" : $"sounds missing: {string.Join(", ", missing)}");
        var before = Sound.Volume(Sound.Music);
        Sound.SetVolume(Sound.Music, 0.3f);
        var bus = AudioServer.GetBusIndex(Sound.Music);
        Check(bus >= 0 && Mathf.Abs(AudioServer.GetBusVolumeDb(bus) - Mathf.LinearToDb(0.3f)) < 0.1f, $"the music slider sets the music bus ({AudioServer.GetBusVolumeDb(bus):0.0} dB)");
        Sound.SetVolume(Sound.Music, before);

        // Who's keeping a fight going, if anyone (for a failure's message).
        string Fighting() => $"in a fight: {main.Tracker.InFight}, attackers: {string.Join(", ", p.Attackers().Select(e => e.Stats.Name))}; fighting the company: " +
            string.Join(", ", GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => e.FightingCompany).Select(e => $"{e.Stats.Name} ({e.Kind.Id}, {e.GlobalPosition.DistanceTo(p.GlobalPosition):0} m{(e.Friendly ? ", friendly" : "")})"));
        // Fight music rings on a few seconds after a fight, and tunes cross-fade: give it time to settle.
        async Task<string> MoodAt(Vector3 at, string expect)
        {
            await Teleport(at);
            // Earlier tests leave the countryside busy, and the wilds bring more: listen with nothing hostile about.
            for (var round = 0; round < 4; round++)
            {
                foreach (var e in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => !e.Friendly && e.GlobalPosition.DistanceTo(at) < 90).ToList()) e.QueueFree();
                await Until(() => !main.Tracker.InFight && !p.Attackers().Any(), 30);
                await Frames(60);
                if (!main.Tracker.InFight && !p.Attackers().Any()) break;
            }
            await Until(() => Sound.Current!.Mood == expect, 15);
            return Sound.Current!.Mood;
        }
        var wild = await MoodAt(new Vector3(-150, 0, -120), "explore");
        Check(wild == "explore", $"out in the wilds the music is {wild}, and the place sounds of {Sound.Current!.Place}{(wild == "explore" ? "" : $" ({Fighting()})")}");
        var town = await MoodAt(new Vector3(20, 0, 20), "town");
        Check(town == "town", $"in the city the music is {town}, and the place sounds of {Sound.Current!.Place}");
        var college = Guilds.Of(Calling.Bard);
        var bards = await MoodAt(new Vector3((float)college.X + 3, 0, (float)college.Z + 3), "bard");
        Check(bards == "bard", $"at the Bards' College the music is {bards}");
        await Teleport(new Vector3(-150, 0, -120));
        var foe = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -3));
        foe.Provoke(p);
        await Until(() => Sound.Current!.Mood == "battle", 10);
        Check(Sound.Current!.Mood == "battle", $"a fight brings the battle music ({Sound.Current.Mood})");
        await FightUntilDead(foe, 30);
        await Until(() => Sound.Current!.Mood == "explore", 20);
        Check(Sound.Current!.Mood == "explore", $"and after it, the wilds' music again ({Sound.Current.Mood})");
    }

    /// <summary>Each of the twelve classes, against a dock thug: its moves fell it, and its limit break goes off.</summary>
    async Task ClassesTest()
    {
        var p = main.PlayerNode;
        foreach (var d in Classes.All)
        {
            p.Join(d.Id);
            p.Stats.Heal(1e6);
            p.Stats.Restore(1e6, 1e6);
            await Frames(3);
            var foe = Spawn("dock_thug", p.GlobalPosition + new Vector3(0, 0, -2.5f));
            await Frames(2);
            var used = new List<string>();
            for (var t = 0.0; t < 25 && foe.State != EnemyState.Dead; t += 1.0 / 60)
            {
                p.Face(foe.GlobalPosition);
                if (p.GlobalPosition.DistanceTo(foe.GlobalPosition) > 2.2f && d.Primary.Range < 6) p.GlobalPosition = foe.GlobalPosition + new Vector3(0, 0.3f, 1.8f);
                p.Stats.Heal(5);
                p.Stats.Restore(5, 5);
                foreach (var a in new[] { d.Signature, d.Utility, d.Primary })
                    if (p.Use(a) && !used.Contains(a.Id)) used.Add(a.Id);
                await Frames(1);
            }
            Check(foe.State == EnemyState.Dead, $"the {d.Name} fells a thug with {string.Join(", ", used)}");
            for (var i = 0; i < 60 && !p.Stats.LimitReady; i++) { p.Stats.TakeHit(12, DamageKind.Physical, AttackKind.Weapon); p.Stats.Heal(12); }
            var target = Spawn("giant_rat", p.GlobalPosition - p.GlobalTransform.Basis.Z * 2.5f);
            await Frames(3);
            p.Face(target.GlobalPosition);
            Check(p.LimitBreak(), $"the {d.Name}'s limit break: {d.Limit.Name}");
            await Frames(40);
            if (IsInstanceValid(target)) target.QueueFree();
            if (d.Id == "Warlock") Check(GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Any(e => e.Kind.Id == "familiar" && e.Leader == p), "the warlock's familiar answers");
            await Until(() => !main.Tracker.InFight, 10);
        }
    }

    /// <summary>
    /// Every class fights with its own moves; kills bring levels; a youngling is tamed and fights beside you;
    /// a youth becomes an apprentice; the road to Dragon Knight is walked from the egg to the saddle; and a
    /// dragon knight of the alliance fights a corrupted dragon on sight.
    /// </summary>
    async Task CompanyTest()
    {
        var (p, w) = (main.PlayerNode, main.World);
        var field = new Vector3(-150, 0, -120);
        await Teleport(field);
        var level = p.Hero.Level;
        static int Earned(Hero h) => Enumerable.Range(1, h.Level - 1).Sum(Hero.XpToNext) + h.Xp;
        var earned = Earned(p.Hero);

        await ClassesTest();
        Check(Earned(p.Hero) > earned, $"kills bring experience: {earned} to {Earned(p.Hero)} (level {level} to {p.Hero.Level})");
        var before = p.Hero.Level;
        main.GainXp((int)Math.Ceiling(Hero.XpToNext(before) / p.Race.Xp));
        Check(p.Hero.Level > before, $"and enough of it brings a level: level {before} to {p.Hero.Level}, {p.Hero.Points} skill points");
        var points = p.Hero.Points;
        p.Join("Barbarian");
        var health = p.Stats.MaxHealth;
        Check(p.Hero.Raise("thick_hide") || points == 0, "a skill point ranks up Thick Hide");
        p.Fit();
        Check(points == 0 || p.Stats.MaxHealth > health, $"Thick Hide rank 2: {health:0} to {p.Stats.MaxHealth:0} health");

        // A wolf pup with its mother close by can't be taken; alone, a ranger takes it easily.
        await Teleport(field + new Vector3(30, 0, 0));
        var pup = Enemy.Create(Bestiary.Get("wolf"), p.GlobalPosition + new Vector3(2, 0, 0), young: true);
        main.AddEnemy(pup);
        var mother = Spawn("wolf", p.GlobalPosition + new Vector3(8, 0, 4));
        await Frames(5);
        Check(pup.Stats.Name == "Wolf Pup" && pup.Figure.Scale.X < 0.6f, $"a wolf pup ({pup.Figure.Scale.X:0.00}x size, {pup.Stats.MaxHealth:0} hp)");
        Check(!main.Tame(pup) && mother.FightingCompany, "its mother won't allow it, and comes for you");
        mother.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, p);
        await Until(() => !main.Tracker.InFight, 10);
        p.Join("Ranger");
        p.Meat = 3;
        Check(main.Tame(pup), "with the mother gone, the ranger tames the pup");
        await Frames(10);
        var wolf = main.CompanionBodies.FirstOrDefault(e => e.Kind.Id == "wolf" && e.Bond is not null);
        Check(wolf is not null && wolf.Leader == p, $"{wolf?.Bond?.Name} follows you");
        var xp = wolf!.Bond!.Xp + wolf.Bond.Level * 1000;
        var prey = Spawn("giant_rat", p.GlobalPosition + new Vector3(-3, 0, -3));
        prey.Provoke(p);
        await Until(() => prey.State == EnemyState.Dead || prey.Stats.Health < prey.Stats.MaxHealth, 10);
        Check(prey.Stats.Health < prey.Stats.MaxHealth, $"{wolf.Bond.Name} fights what's attacking you");
        prey.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, p);
        await Frames(5);
        Check(wolf.Bond.Xp + wolf.Bond.Level * 1000 > xp, $"{wolf.Bond.Name} shares the kill ({wolf.Bond.Xp} xp, level {wolf.Bond.Level})");
        wolf.Bond.Fed = 10;
        main.Feed(wolf.Bond);
        Check(wolf.Bond.Fed > 50 && p.Meat < 3, $"fed: {wolf.Bond.Fed:0}% full, {p.Meat} meat left");

        // A youth in Tristinwold becomes a hero's apprentice.
        var youthNpc = w.YouthsOf(w.Tristinwold).First();
        await Teleport(new Vector3((float)youthNpc.X + 2, 0, (float)youthNpc.Z));
        await Until(() => main.PlacesNode.People.Any(x => x.Npc.Id == youthNpc.Id), 5);
        var youth = main.PlacesNode.People.FirstOrDefault(x => x.Npc.Id == youthNpc.Id);
        Check(youth is not null && youth.Figure.Scale.X <= 1, $"{youthNpc.Name}, {youthNpc.Age}, {youthNpc.Occupation} in Tristinwold");
        p.Hero.Level = Math.Max(p.Hero.Level, Main.RecruitLevel);
        p.Crowns += Main.RecruitFee;
        main.Talk(youth!);
        Check(main.HudNode.Choose("Would you become my"), $"you can ask {youthNpc.First} to follow you");
        await Frames(10);
        Check(main.Bonds.Any(b => b.Id == youthNpc.Id && b.Role == BondRole.Apprentice) && main.CompanionBodies.Any(e => e.Bond?.Id == youthNpc.Id),
            $"{youthNpc.First} becomes your apprentice");
        main.HudNode.EndTalk();

        // The road to Dragon Knight.
        await Teleport(new Vector3((float)DragonSaga.RookeryX + 4, 0, (float)DragonSaga.RookeryZ + 10));
        await Until(() => main.WildsNode.Creatures.Count(e => e.Rider) >= 2, 8);
        Check(main.WildsNode.Creatures.Count(e => e.Rider) >= 2, $"dragon knights at the rookery: {string.Join(", ", main.WildsNode.Creatures.Where(e => e.Rider).Select(e => e.Stats.Name))}");
        // Every day a rider of the Order comes down at the rookery, for anyone who's looking.
        main.RiderFlightsNode.LandForTest();
        await Until(() => main.RiderFlightsNode.Landed, 10);
        Check(main.RiderFlightsNode.Landed && main.RiderFlightsNode.Landings >= 1, "a dragon rider of the Order comes down at the rookery");
        var aldric = main.Locals.First(x => x.Npc.Id == "eyrie/aldric");
        main.Talk(aldric);
        Check(main.HudNode.Choose("How do I become") && main.HudNode.TalkText.Contains("earned"), "Ser Aldric doesn't hand out eggs: you earn one");
        Check(main.HudNode.Choose("I'll earn it") && main.Saga.Stage == SagaStage.Proving, "you take up the road: prove yourself");
        var heroLevel = p.Hero.Level;
        p.Hero.Level = 1;
        main.Talk(aldric);
        Check(!main.HudNode.Options.Any(o => o.StartsWith("I've done what")), "not at level 1, and not with his dragons unfed");
        main.HudNode.EndTalk();
        p.Hero.Level = Math.Max(heroLevel, DragonSaga.MinLevel);
        p.Meat = 12;
        main.Talk(aldric);
        Check(main.HudNode.Choose("Here's meat") && main.Saga.MeatGiven == DragonSaga.MeatDue && p.Meat == 2, "you feed the rookery's dragons with meat you hunted");
        main.Talk(aldric);
        Check(main.HudNode.Choose("I've done what you asked") && main.Saga.Stage == SagaStage.SeekEgg && !main.Saga.FoundNest,
            $"then he says where an egg is to be had, roughly: somewhere {main.Saga.Hint}, and no flight there");
        main.HudNode.EndTalk();
        var nestAt = new Vector3((float)main.Saga.NestX, 0, (float)main.Saga.NestZ);
        await Teleport(nestAt + new Vector3(60, 0, 40));
        await Until(() => main.Saga.FoundNest && main.Nest is not null, 6);
        Check(main.Saga.FoundNest, $"you find the lair of {main.Saga.MatriarchName} yourself");
        Check(main.Nest is not null, $"the nest at the lair of {main.Saga.MatriarchName}");
        await Teleport(nestAt + new Vector3(30, 0, 30));
        await Until(() => main.Enemies().Any(e => Dragons.IsCorrupted(e.Kind.Id)), 5);
        var raiders = main.Enemies().Where(e => Dragons.IsCorrupted(e.Kind.Id) && e.State != EnemyState.Dead).ToList();
        Check(raiders.Count >= 2, $"corrupted drakes at the nest: {string.Join(", ", raiders.Select(r => r.Stats.Name))}");
        foreach (var r in raiders) r.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => !main.Tracker.InFight, 10);
        p.GlobalPosition = main.Nest!.GlobalPosition + new Vector3(1.5f, 1, 0);
        await Frames(5);
        main.PickUp(main.Nest!);
        Check(main.Saga.Stage == SagaStage.CarryEgg, "you take an egg");
        main.RestNow();
        await Frames(10);
        var hatchling = main.CompanionBodies.FirstOrDefault(e => e.Bond?.Role == BondRole.Dragon);
        Check(hatchling is not null && hatchling.Figure.Scale.X < 0.3f, $"it hatches: {hatchling?.Bond?.Name}, a {hatchling?.Kind.Name.ToLowerInvariant()} hatchling ({hatchling?.Figure.Scale.X:0.00}x)");
        main.GainXp(60000);
        await Frames(5);
        Check(hatchling!.Bond!.Rideable && hatchling.Figure.Scale.X >= 1, $"{hatchling.Bond.Name} grows up: level {hatchling.Bond.Level}, a {hatchling.Bond.Stage}");
        await Teleport(new Vector3((float)DragonSaga.RookeryX + 4, 0, (float)DragonSaga.RookeryZ + 10));
        await Frames(30);
        main.Talk(aldric);
        Check(main.HudNode.Choose("I've raised"), "you bring your dragon to Ser Aldric");
        Check(main.HudNode.Choose("(Kneel"), "and kneel");
        Check(main.Saga.Stage == SagaStage.Knighted, "you rise a Dragon Knight");
        main.HudNode.EndTalk();
        var beside = p.GlobalPosition + new Vector3(3 + 3 * hatchling.Figure.Scale.X, 0, 0);
        hatchling.GlobalPosition = beside with { Y = City.FloorAt(beside) + 0.5f }; // on the ground, not in it
        hatchling.Velocity = Vector3.Zero;
        await Frames(10);
        main.RideNow();
        Check(p.Mount == hatchling, $"you climb onto {hatchling.Bond.Name} ({main.HudNode.LastLine}; {hatchling.State}, {hatchling.GlobalPosition.DistanceTo(p.GlobalPosition):0.0} m: you at {p.GlobalPosition}, {hatchling.Bond.Name} at {hatchling.GlobalPosition})");
        Input.ActionPress("jump");
        await Until(() => hatchling.GlobalPosition.Y - City.FloorAt(hatchling.GlobalPosition) > 10, 6);
        Input.ActionRelease("jump");
        var high = hatchling.GlobalPosition.Y - City.FloorAt(hatchling.GlobalPosition);
        Check(high > 10 && p.GlobalPosition.DistanceTo(hatchling.SaddleGlobal) < 0.5f, $"and fly: {high:0} m up, in the saddle");
        Input.ActionPress("descend");
        await Until(() => !hatchling.Flying, 12);
        Input.ActionRelease("descend");
        main.RideNow();
        Check(p.Mount is null && !hatchling.Flying, "you land and climb down");

        // A dragon knight of the alliance and a corrupted dragon fight on sight.
        main.TerrainNode.EnsureAround(p.GlobalPosition, 2);
        Vector3 Ground(Vector3 at) => at with { Y = City.FloorAt(at) + 0.5f };
        var knight = Enemy.Create(Bestiary.Get("dragon_gold"), Ground(p.GlobalPosition + new Vector3(60, 0, 40)), look: "rider:dragon_gold");
        knight.Allied = true;
        main.AddEnemy(knight);
        var fallen = Spawn("dragon_red_corrupt", Ground(p.GlobalPosition + new Vector3(60, 0, 100)));
        await Until(() => knight.Stats.Health < knight.Stats.MaxHealth || fallen.Stats.Health < fallen.Stats.MaxHealth, 25);
        Check(knight.Stats.Health < knight.Stats.MaxHealth || fallen.Stats.Health < fallen.Stats.MaxHealth,
            $"a dragon knight and a corrupted red dragon fight ({knight.Stats.Health:0} and {fallen.Stats.Health:0} hp left)");
        knight.QueueFree();
        fallen.QueueFree();

        // A corrupted dragon's wounds carry the Rift: a gold dragon that takes enough of them turns, and goes mad.
        // (Far from the rookery, whose knights would join in.)
        var away = new Vector3(-330, 0, 260);
        main.TerrainNode.EnsureAround(away, 2);
        var gold = Spawn("dragon_gold", Ground(away));
        var biter = Spawn("dragon_black_corrupt", Ground(away + new Vector3(0, 0, 25)));
        await Until(() => gold.Corrupted || gold.State == EnemyState.Dead || biter.State == EnemyState.Dead, 60);
        Check(gold.Corrupted && gold.Frenzy > 0 && gold.Kind.Id == "dragon_gold_corrupt",
            $"the gold dragon is infected and turns: {gold.Stats.Name} ({gold.Kind.Name}, frenzy {gold.Frenzy:0} s; gold {gold.Stats.Health:0}/{gold.Stats.MaxHealth:0} {gold.State} rival {gold.Rival?.Stats.Name}, infection {gold.Infection:P0}; biter {biter.Stats.Health:0}/{biter.Stats.MaxHealth:0} {biter.State} rival {biter.Rival?.Stats.Name}, {gold.GlobalPosition.DistanceTo(biter.GlobalPosition):0} m apart)");
        gold.QueueFree();
        biter.QueueFree();

        // Your own dragon can be taken too; beat it down and it's yours again.
        var rift = Spawn("dragon_red_corrupt", Ground(hatchling.GlobalPosition + new Vector3(0, 0, 40)));
        for (var i = 0; i < 8 && !hatchling.Corrupted; i++) { hatchling.TakeHitFrom(rift, hatchling.Stats.MaxHealth * 0.2, false); hatchling.Stats.Heal(1e9); }
        Check(hatchling.Corrupted && hatchling.Leader is null && hatchling.Frenzy > 0, $"the Rift takes {hatchling.Bond.Name}: {hatchling.Stats.Name}");
        rift.QueueFree();
        hatchling.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(5);
        Check(!hatchling.Corrupted && hatchling.Leader == p && hatchling.Stats.Name == hatchling.Bond.Name, $"beaten down, {hatchling.Bond.Name} is freed and yours again");
        main.RestNow();
        await Frames(5);
        Check(hatchling.State != EnemyState.Dead, $"{hatchling.Bond.Name} gets up");
        p.Join("Paladin");
        await Teleport(field);
    }

    /// <summary>
    /// The Iron Circle: a lesson from the paladins' teacher, all three of the Paladin's trials (the last a duel with
    /// Corin Vale), a piece of guild drama with a choice that sticks, and a duel lost.
    /// </summary>
    async Task GuildTest()
    {
        var (p, w, hud, journal) = (main.PlayerNode, main.World, main.HudNode, main.Journal);
        var g = Guilds.Of(Calling.Warrior);
        p.Join("Paladin");
        await Teleport(new Vector3((float)g.X + 3, 0, (float)g.Z + 3));
        Person Find(string id) => main.Locals.First(x => x.Npc.Id == id);
        var teacher = Find(g.TeacherOf("Paladin")!.Id);
        var rival = Find(g.Rival.Id);
        Check(main.Area == "The Iron Circle's yard", $"the Iron Circle's yard, outside the west gate: {main.Area}");

        // A lesson.
        p.Crowns += 1000;
        var points = p.Hero.Points;
        main.Talk(teacher);
        Check(hud.Choose("Teach me"), "Ser Edwyn offers a lesson");
        Check(p.Hero.Points == points + 1, "a lesson brings a skill point");

        // The three trials.
        for (var step = 0; step < ClassQuests.Steps; step++)
        {
            main.Talk(teacher);
            Check(hud.Choose("Is there a trial"), $"Ser Edwyn sets trial {step + 1}");
            Check(hud.Choose("I'll do it"), "you take it up");
            var trial = ClassQuests.For(w, p.Class, step)!;
            Check(journal.Get(trial.Id) is { Stage: QuestStage.Active }, $"{trial.Title}: {journal.Get(trial.Id)?.Objective}");
            switch (trial.Kind)
            {
                case QuestKind.Slay: for (var i = 0; i < trial.Count; i++) journal.Killed(trial.TargetId).ToList(); break;
                case QuestKind.Fetch: journal.PickedUp(trial.Id); break;
                default:
                    main.Talk(rival);
                    Check(hud.Choose("Spar with me"), $"you challenge {g.Rival.Name}");
                    await Frames(5);
                    var duel = main.Enemies().First(e => e.Sparring);
                    Check(duel.Stats.Name == g.Rival.Name, $"{duel.Stats.Name} fights as a {g.Rival.Teaches} ({duel.Stats.MaxHealth:0} hp)");
                    for (var t = 0.0; t < 40 && duel.Sparring; t += 1.0 / 60)
                    {
                        if (p.GlobalPosition.DistanceTo(duel.GlobalPosition) > 2.2f) p.GlobalPosition = duel.GlobalPosition + new Vector3(0, 0.3f, 1.8f);
                        p.Face(duel.GlobalPosition);
                        p.Stats.Heal(30);
                        p.Swing();
                        await Frames(1);
                    }
                    Check(!duel.Sparring && duel.State != EnemyState.Dead && journal.Get(trial.Id) is { Stage: QuestStage.Return }, $"{g.Rival.Name} yields, alive");
                    await Frames(200);
                    Check(rival.Visible, $"{g.Rival.Npc.First} steps back into the yard");
                    break;
            }
            main.Talk(teacher);
            await Frames(2);
            Check(journal.Get(trial.Id) is { Stage: QuestStage.Done } && main.GuildBook.Step("Paladin") == step + 1, $"trial {step + 1} done");
            hud.EndTalk();
        }
        Check(main.GuildBook.Titles.Contains(ClassQuests.MasteryTitle("Paladin")), $"you are {ClassQuests.MasteryTitle("Paladin")}, {p.GuildTitle}");

        // Drama, once enough time has passed.
        main.GuildBook.PlaySeconds += Guilds.DramaGapSeconds;
        var story = Guilds.NextDrama(main.GuildBook, Calling.Warrior)!;
        await Frames(400); // the guild sends word
        Check(story is not null, $"trouble in the Iron Circle: {story?.Title}");
        main.Talk(Find(story!.TellerId));
        Check(hud.Choose(story.Title), $"{Guilds.Member(story.TellerId)!.Name} tells you: {story.Intro[..60]}...");
        var choice = story.Choices.First(c => !c.Duel);
        Check(hud.Choose(choice.Label[..10]), $"you choose: {choice.Label}");
        Check(main.GuildBook.For(Calling.Warrior).Done.Contains(story.Id), $"it's settled: {hud.TalkText[..60]}...");
        hud.EndTalk();

        // A duel lost: the hero yields rather than falls.
        var lost = main.GuildBook.For(Calling.Warrior).DuelsLost;
        main.Talk(rival);
        hud.Choose("Spar with me");
        await Frames(5);
        p.Stats.Lose(p.Stats.Health - p.Stats.MaxHealth * 0.17);
        await Until(() => main.GuildBook.For(Calling.Warrior).DuelsLost > lost, 20);
        Check(main.GuildBook.For(Calling.Warrior).DuelsLost > lost && !p.Downed, "you lose a duel and yield, on your feet");
        await Frames(200);
    }

    /// <summary>
    /// The Ashen Crypt: thralls rise from the ash, then the Cinder Warden; his fire hurts; he falls, his hoard pays,
    /// the crypt falls quiet, and after a while it rekindles.
    /// </summary>
    async Task RaidTest()
    {
        var (p, crypt, raid) = (main.PlayerNode, main.CryptNode, Raids.AshenCrypt);
        foreach (var c in p.Companions().ToList()) c.QueueFree(); // a low-level hero alone
        await Teleport(new Vector3((float)raid.X, 0, (float)raid.Z + 30));
        await Until(() => crypt.Fighting, 10);
        Check(crypt.Thralls.Count == raid.Thralls, $"{raid.Thralls} cinder thralls claw up out of the ash");
        foreach (var t in crypt.Thralls.ToList()) t.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => crypt.Boss is not null, 5);
        var warden = crypt.Boss!;
        Check(warden.Kind.Id == raid.BossKind && warden.Undead && !warden.Named, $"the {warden.Stats.Name} rises, nameless ({warden.Stats.MaxHealth:0} hp against a level {p.Hero.Level} hero)");
        p.GlobalPosition = warden.GlobalPosition + new Vector3(0, 0.5f, 3);
        p.Stats.Heal(1e6);
        var health = p.Stats.Health;
        await Until(() => p.Stats.Health < health, 15);
        Check(p.Stats.Health < health, $"his claws and fire hurt ({health - p.Stats.Health:0} harm)");
        p.Stats.Heal(1e6);
        warden.TakeHit(warden.Stats.MaxHealth * 0.5, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(30);
        Check(crypt.Thralls.Count > raid.Thralls, "wounded, he calls more of the dead");
        warden.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => crypt.Hoard is not null, 5);
        Check(crypt.Hoard is not null, "he crumbles; his hoard lies open");
        var (crowns, xp, level) = (p.Crowns, p.Hero.Xp, p.Hero.Level);
        main.PickUp(crypt.Hoard!);
        Check(p.Crowns > crowns && (p.Hero.Level > level || p.Hero.Xp != xp), $"the hoard pays: {p.Crowns - crowns} crowns");
        Check(main.RaidLog.ClearsOf(raid) == 1 && !main.RaidLog.Ready(raid, main.GuildBook.PlaySeconds) && main.GuildBook.Titles.Contains("Warden-Breaker"),
            $"the crypt falls quiet for {raid.CooldownSeconds / 60:0} minutes; you are Warden-Breaker");
        await Frames(30);
        Check(!crypt.Fighting, "it doesn't wake again yet");
        main.GuildBook.PlaySeconds += raid.CooldownSeconds;
        await Until(() => crypt.Fighting, 5);
        Check(crypt.Fighting, "and when it rekindles, the dead rise again");
        await Teleport(new Vector3(-150, 0, -120)); // walking away lets them lie down
        await Frames(30);
        Check(!crypt.Fighting, "leave, and they lie down to wait");
    }

    /// <summary>Walks up to a lair from the city's side.</summary>
    async Task Approach(Raid raid, float distance = 30)
    {
        var centre = new Vector3((float)raid.X, 0, (float)raid.Z);
        await Teleport(centre - centre.Normalized() * distance);
    }

    /// <summary>
    /// Every lair in the wilds: built where it stands; its own come first; then its boss, nameless, in its own body,
    /// with its own moves; it falls; the hoard pays and gives the title.
    /// </summary>
    async Task LairTest()
    {
        var p = main.PlayerNode;
        foreach (var raid in Raids.All.Where(r => r != Raids.AshenCrypt))
        {
            var lair = main.LairOf(raid);
            p.Hero.Level = raid.MinLevel;
            p.Fit();
            await Approach(raid);
            await Until(() => lair.Fighting, 10);
            Check(lair.Built && lair.Thralls.Count == raid.Thralls && lair.Thralls.All(t => t.Kind.Id == raid.ThrallKind),
                $"{raid.Name}: {raid.Thralls} {Bestiary.Get(raid.ThrallKind).Name}s come out");
            foreach (var t in lair.Thralls.ToList()) t.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
            await Until(() => lair.Boss is not null, 5);
            var boss = lair.Boss!;
            Check(boss.Kind.Id == raid.BossKind && !boss.Named && boss.Think is not null && boss.Figure is not Beast,
                $"then the {boss.Stats.Name} ({boss.Figure.GetType().Name}, {boss.Stats.MaxHealth:0} hp, {string.Join("/", raid.Moves)})");
            p.Stats.Heal(1e6);
            p.GlobalPosition = boss.GlobalPosition + new Vector3(0, 0.5f, 3);
            var health = p.Stats.Health;
            await Until(() => p.Stats.Health < health, 15);
            Check(p.Stats.Health < health, $"it hurts ({health - p.Stats.Health:0})");
            p.Stats.Heal(1e6);
            boss.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
            await Until(() => lair.Hoard is not null, 5);
            var crowns = p.Crowns;
            if (lair.Hoard is { } hoard) main.PickUp(hoard);
            Check(p.Crowns > crowns && main.GuildBook.Titles.Contains(raid.Title) && !main.RaidLog.Ready(raid, main.GuildBook.PlaySeconds),
                $"{raid.Hoard} pays {p.Crowns - crowns} crowns; you are {raid.Title}");
            await Until(() => !main.Tracker.InFight, 10);
        }
    }

    /// <summary>
    /// A lair boss that beats the company takes a name, powers up and walks out to roam the world; the lair stands
    /// empty until its timer runs out, and then a new nameless one is there. The roamer wanders, comes for the hero
    /// when it finds them, and when it's killed it's gone for good, and its bounty paid.
    /// </summary>
    /// <summary>
    /// The world going about its business: the animals people keep, wild horses, eagles overhead, food to forage; the
    /// guilds' initiates on their errands in Tristinwold; a young thief at your purse; a cutthroat, and the Watch.
    /// </summary>
    async Task LivingWorldTest()
    {
        var p = main.PlayerNode;
        var pack = p.Pack;
        var life = main.TownLifeNode;
        IEnumerable<Enemy> Owned(string place) => GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e.Owner == place && e.State != EnemyState.Dead);
        foreach (var c in p.Companions().ToList()) c.QueueFree();

        // Tristinwold's own animals, and a farm's.
        await Teleport(City.PlayerStart);
        await Frames(30);
        var town = Owned("Tristinwold").ToList();
        Check(town.Count(e => e.Kind.Id == "dog") >= 2 && town.Any(e => e.Kind.Id == "cat") && town.Any(e => e.Kind.Id == "hen"),
            $"Tristinwold has its dogs, cats and hens ({string.Join(", ", town.GroupBy(e => e.Kind.Id).Select(g => $"{g.Count()} {g.Key}"))})");
        var farm = main.World.SettlementsNear(0, 0, 5000).Where(s => s.Kind == SettlementKind.Farmstead).MinBy(s => s.DistanceTo(0, 0))!;
        await Teleport(new Vector3((float)(farm.X + farm.Radius + 6), 0, (float)farm.Z));
        await Until(() => main.PlacesNode.IsBuilt(farm.Id) && Owned(farm.Name).Any(), 10);
        var stock = Owned(farm.Name).ToList();
        Check(stock.Any(e => e.Kind.Id == "cow") && stock.Any(e => e.Kind.Id == "sheep"),
            $"{farm.Name} keeps its livestock ({string.Join(", ", stock.GroupBy(e => e.Kind.Id).Select(g => $"{g.Count()} {g.Key}"))})");
        p.Crowns = 100;
        var sheep = stock.FirstOrDefault(e => e.Kind.Id == "sheep");
        if (sheep is not null)
        {
            var wool = pack.Count("wool");
            sheep.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, p);
            await Frames(5);
            Check(p.Crowns == 100 - Livestock.Fine("sheep"), $"killing somebody's sheep costs you {100 - p.Crowns} crowns ({main.HudNode.LastLine})");
        }

        // Eagles over the wilds.
        await Teleport(new Vector3(-150, 0, -160));
        await Until(() => main.SkyNode.Eagles > 0, 10);
        Check(main.SkyNode.Eagles > 0, $"eagles circle overhead ({main.SkyNode.Eagles}), songbirds in the woods ({main.SkyNode.Birds})");

        // Food to forage.
        var berries = Herbs.Near(main.World, -600, -600, 1400).FirstOrDefault(x => x.HerbId is "raspberries" or "bilberries" && main.HerbLog.Ready(x, main.GuildBook.PlaySeconds));
        if (berries is null) Check(false, "berries grow wild");
        else
        {
            var at = new Vector3((float)berries.X, City.FloorAt(new Vector3((float)berries.X, 0, (float)berries.Z)), (float)berries.Z);
            await Teleport(at + new Vector3(1, 0, 0));
            await Until(() => main.HerbPatchNode.Within(at) is not null, 5);
            p.GlobalPosition = at + new Vector3(0.5f, 0.5f, 0);
            await Frames(5);
            var had = pack.Count(berries.HerbId);
            Check(main.PickHere() && pack.Count(berries.HerbId) > had, $"you pick {Items.Get(berries.HerbId)!.Name.ToLowerInvariant()} in the wild");
        }

        // The guilds' initiates about Tristinwold.
        life.Stirring = true;
        await Teleport(City.PlayerStart);
        await Until(() => life.Walkers.Count >= 6, 10);
        Check(life.Walkers.Select(w => w.Who.Calling).Distinct().Count() == 4,
            $"the guilds' initiates are out on their errands: {string.Join("; ", life.Walkers.Select(w => w.Who.Title))}");
        var starts = life.Walkers.ToDictionary(w => w, w => w.Body.GlobalPosition);
        await Frames(240);
        Check(life.Walkers.Any(w => starts.TryGetValue(w, out var s) && w.Body.GlobalPosition.DistanceTo(s) > 2), "and they walk the town");

        // A young thief at your purse: strike them, and it's back.
        p.Crowns = 100;
        TownLife.Walker? thief = null;
        for (var i = 0; i < 8 && p.Crowns == 100; i++)
        {
            await Until(() => life.Walkers.Any(w => w.Who.Calling == Calling.Rogue && !w.Body.Criminal), 10);
            thief = life.Walkers.FirstOrDefault(w => w.Who.Calling == Calling.Rogue && !w.Body.Criminal);
            if (thief is null) break;
            thief.Body.GlobalPosition = p.GlobalPosition + new Vector3(1.2f, 0.4f, 0);
            life.LiftForTest(thief);
            await Frames(5);
        }
        Check(p.Crowns < 100 && thief is { Body.Criminal: true }, $"a young thief lifts your purse ({100 - p.Crowns} crowns): {main.HudNode.LastLine}");
        if (thief is not null && IsInstanceValid(thief.Body))
        {
            p.GlobalPosition = thief.Body.GlobalPosition + new Vector3(1.5f, 0.4f, 0);
            thief.Body.TakeHit(5, DamageKind.Physical, AttackKind.Weapon, false, p);
            await Frames(5);
            Check(p.Crowns == 100, $"strike them, and they drop it ({p.Crowns} crowns)");
        }

        // A cutthroat from the Velvet Knives: the Watch comes running, and pays for it.
        await Teleport(City.PlayerStart);
        var knife = life.Cutthroat();
        await Until(() => main.WatchNode.Guards.Any(g => g.Rival == knife), 20);
        Check(main.WatchNode.Guards.Any(g => g.Rival == knife), $"a Velvet Knife comes for you, and the Watch comes running ({main.WatchNode.Guards.Count(g => g.Rival == knife)} watchmen)");
        var crowns = p.Crowns;
        if (IsInstanceValid(knife)) knife.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(5);
        Check(p.Crowns > crowns, $"you bring them down, and the Watch pays for it (+{p.Crowns - crowns} crowns)");
        life.Stirring = false;
        await Until(() => !main.Tracker.InFight, 10);
        p.Stats.Heal(1e9);
    }

    /// <summary>
    /// Deviants of the wilds: what beats you out there takes a name; once it sees you it hunts you; you can outrun it,
    /// and it remembers; every blow teaches it how you fight, and it fights back with that; it won't follow you into
    /// town; it's all kept; and killed, it's gone for good.
    /// </summary>
    async Task HuntTest()
    {
        var p = main.PlayerNode;
        var roamers = main.RoamersNode;
        var hud = main.HudNode;
        roamers.WildDeviants = true;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        float Apart(Enemy e) => new Vector2(e.GlobalPosition.X - p.GlobalPosition.X, e.GlobalPosition.Z - p.GlobalPosition.Z).Length();

        // Beaten out in the wilds, by a frost troll: it takes a name.
        var here = new Vector3(-150, 0, -120);
        await Teleport(here);
        Check(!roamers.InTown(here), "out in the wilds, past the walls");
        var troll = Spawn("frost_troll", here + new Vector3(0, 0, -3));
        var plain = troll.Stats.MaxHealth;
        troll.Provoke(p);
        await Frames(10);
        p.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, troll);
        await Until(() => main.RaidLog.Roamers.Any(r => r.Kind == "frost_troll"), 5);
        var r = main.RaidLog.Roamers.FirstOrDefault(r => r.Kind == "frost_troll");
        Check(r is not null && troll.Named && troll.Stats.Name == r.Title && troll.Stats.MaxHealth > plain,
            $"the frost troll that beat you takes a name, {r?.Title}, and grows ({plain:0} -> {troll.Stats.MaxHealth:0} hp)");
        if (r is null) return;
        await Until(() => !p.Downed, 10);
        Check(!r.Hunting, "it has beaten you: it isn't hunting you, for now");

        // Out in the wilds again, within sight of it: it takes up your trail.
        await Frames(60);
        var (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 70, 0, (float)z));
        await Until(() => r.Hunting, 10);
        Check(r.Hunting, $"it sees you from 70 m, and comes for you: {hud.LastLine}");
        var body = roamers.BodyOf(r);
        Check(body is not null, "it's there, walking");
        if (body is null) return;
        var start = Apart(body);
        await Until(() => Apart(body) < start - 15, 40);
        Check(Apart(body) < start - 15, $"and closes in ({start:0} m, then {Apart(body):0} m)");

        // Run (across open country, clear of any village): it follows your trail at its own pace, and you can get
        // away. It remembers that you ran.
        var away = new[] { 0f, 45, 90, 135, 180, 225, 270, 315 }.Select(a => new Vector3(Mathf.Cos(Mathf.DegToRad(a)), 0, Mathf.Sin(Mathf.DegToRad(a))))
            .FirstOrDefault(dir => Enumerable.Range(1, 40).All(k => !roamers.InTown(p.GlobalPosition + dir * 30 * k)), new Vector3(1, 0, 0));
        for (var i = 0; i < 40 && r.Hunting; i++)
        {
            await Teleport(p.GlobalPosition + away * 30);
            p.Stats.Heal(1e9);
        }
        Check(!r.Hunting && r.Lessons.Escapes == 1, $"you outrun it, and it loses your trail ({hud.LastLine})");
        Check(Tactics.Read(r.Lessons).Speed > 1, "and it's quicker on your trail next time");

        // Find it again, and fight it with fire, from afar: it learns, there and then.
        await Frames(30);
        (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 60, 0, (float)z));
        await Until(() => r.Hunting && roamers.BodyOf(r) is not null, 10);
        body = roamers.BodyOf(r);
        if (body is null) { Check(false, "it's back on your trail"); return; }
        var dealt = new List<double>();
        for (var i = 0; i < Tactics.HitsToLearn + 2; i++)
        {
            p.GlobalPosition = body.GlobalPosition + new Vector3(15, 0.5f, 0);
            p.Stats.Heal(1e9);
            dealt.Add(body.TakeHit(40, DamageKind.Fire, AttackKind.Spell, false, p).Dealt);
            body.Stats.Heal(1e9);
            await Frames(3);
        }
        var counter = roamers.FightsWith(r);
        Check(counter.Resists == DamageKind.Fire && counter.Lunges && counter.Ward > 1 && body.Stats.Resist.GetValueOrDefault(DamageKind.Fire) >= 50,
            $"it learns how you fight: it takes your fire now, closes the gap on you, and shrugs off spells ({string.Join(" / ", counter.Lines.Select(l => l.Id))})");
        Check(dealt[^1] < dealt[0] * 0.6, $"your fire does far less to it than it did ({dealt[0]:0}, then {dealt[^1]:0})");

        // Into town, hale: it won't follow you in.
        await Teleport(City.GallowsPerch + new Vector3(0, 0, 6));
        await Until(() => !r.Hunting, 5);
        Check(!r.Hunting, $"while you're hale, it won't follow you inside the walls ({hud.LastLine})");

        // Hurt, though, it thinks it can finish you in town, and comes in after you. The town fights for you.
        var watch = main.WatchNode;
        Check(watch.Guards.Count() >= 7, $"the Watch is at its posts ({watch.Guards.Count()} watchmen)");
        (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 60, 0, (float)z));
        await Until(() => r.Hunting && roamers.BodyOf(r) is not null, 10);
        body = roamers.BodyOf(r);
        if (body is null) { Check(false, "it's back on your trail"); return; }
        p.Stats.Lose(p.Stats.MaxHealth * 0.7);
        var inTown = City.GallowsPerch + new Vector3(-6, 0, 10);
        await Teleport(inTown);
        p.Stats.Lose(Math.Max(0, p.Stats.Health - p.Stats.MaxHealth * 0.3));
        var behind = inTown + new Vector3(18, 0, 0); // it's followed you to the door
        body.GlobalPosition = behind with { Y = City.FloorAt(behind) + 0.4f };
        body.ResetPhysicsInterpolation();
        var helped = watch.Helped;
        await Until(() => watch.Guards.Any(g => g.Rival == body) && watch.Helped > helped, 30);
        Check(r.Hunting, $"hurt, you're no safer in town: it comes in after you ({hud.LastLine})");
        Check(watch.Guards.Any(g => g.Rival == body), $"the Watch comes running ({watch.Guards.Count(g => g.Rival == body)} watchmen on it)");
        Check(watch.Helped > helped, $"and the townsfolk join in ({watch.Helped - helped} blows, throws and poultices)");
        p.Stats.Heal(1e9);
        // With enough of the Watch around you, it thinks better of it.
        foreach (var (g, i) in watch.Guards.Select((g, i) => (g, i)))
        {
            var post = p.GlobalPosition + new Vector3(Mathf.Cos(i) * 4, 0, Mathf.Sin(i) * 4);
            g.GlobalPosition = post with { Y = City.FloorAt(post) + 0.4f };
            g.ResetPhysicsInterpolation();
        }
        await Until(() => !r.Hunting, 10);
        Check(!r.Hunting, $"with the Watch all round you, it breaks off ({hud.LastLine})");

        // All of it is kept: what it is, where it set out, what it learned.
        var saved = CompanySave.FromJson(main.Save()).Raids?.Roamers.FirstOrDefault(x => x.Id == r.Id);
        Check(saved is { Kind: "frost_troll", Lessons.Escapes: 1 } && saved.Lessons.Damage.GetValueOrDefault(DamageKind.Fire) > 0 && saved.Title == r.Title,
            "what it learned about you is saved with it");

        // Kill it, for good.
        (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 40, 0, (float)z));
        await Until(() => roamers.BodyOf(r) is not null, 10);
        var crowns = p.Crowns;
        roamers.BodyOf(r)?.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => main.RaidLog.Roamers.All(x => x.Id != r.Id), 5);
        Check(main.RaidLog.Roamers.All(x => x.Id != r.Id) && p.Crowns > crowns, $"killed, it's gone for good: a bounty of {p.Crowns - crowns} crowns");
        roamers.WildDeviants = false;
        await Teleport(new Vector3(-150, 0, -120));
        await Until(() => !main.Tracker.InFight, 15);
        p.Stats.Heal(1e9);
    }

    async Task DeviantLairTest()
    {
        var (p, raid) = (main.PlayerNode, Raids.GoblinWarren);
        var lair = main.LairOf(raid);
        main.GuildBook.PlaySeconds += raid.CooldownSeconds; // it's awake again after the last test
        p.Hero.Level = 5;
        p.Fit();
        await Approach(raid);
        await Until(() => lair.Fighting, 10);
        foreach (var t in lair.Thralls.ToList()) t.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => lair.Boss is not null, 5);
        var boss = lair.Boss!;
        var plain = boss.Stats.MaxHealth;
        p.GlobalPosition = boss.GlobalPosition + new Vector3(0, 0.5f, 3);
        await Frames(30);
        p.TakeHit(1e6, DamageKind.Fire, AttackKind.Weapon, false, boss); // it beats us
        await Until(() => main.RaidLog.Roamers.Count == 1, 5);
        var roamer = main.RaidLog.Roamers.SingleOrDefault();
        Check(roamer is not null && boss.Named && boss.Stats.Name == roamer.Title && boss.Stats.MaxHealth > plain,
            $"beaten: the goblin king takes a name, {roamer?.Title}, and grows ({plain:0} -> {boss.Stats.MaxHealth:0} hp)");
        Check(!lair.Fighting && lair.Boss is null && main.RaidLog.SecondsLeft(raid, main.GuildBook.PlaySeconds) <= RaidBook.MoveInSeconds,
            "it leaves its lair, and another is already moving in");
        await Until(() => !p.Downed, 8);
        await Frames(90);
        Check(!main.RoamersNode.Near.Any(), "while you're back in town, it's out of sight, roaming");

        // It roams: a while later it's somewhere else in the wilds. Find it, and it comes for you.
        main.GuildBook.PlaySeconds += 1800;
        var (x, z) = roamer!.Where(main.World, main.GuildBook.PlaySeconds);
        // Its road is its own (random, by its seed), so it may have wandered back near home: what matters is that it's been walking.
        var walked = 0.0;
        for (var t = roamer.Since; t < main.GuildBook.PlaySeconds; t += 30)
        {
            var (ax, az) = roamer.Where(main.World, t);
            var (bx, bz) = roamer.Where(main.World, Math.Min(t + 30, main.GuildBook.PlaySeconds));
            walked += Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        }
        Check(walked > 400, $"half an hour on it has walked {walked:0} m, and is {new Vector2((float)(x - raid.X), (float)(z - raid.Z)).Length():0} m from its lair");
        await Teleport(new Vector3((float)x + 25, 0, (float)z));
        await Until(() => main.RoamersNode.Near.Any(n => n.Body.FightingCompany), 10);
        var found = main.RoamersNode.Near.FirstOrDefault();
        var roamerHealth = found.Body?.Stats.MaxHealth ?? 0;
        Check(found.Body is { Named: true } && found.Body.FightingCompany, $"found, {found.Roamer?.Title} comes for you");

        // A new nameless boss has moved into the lair, no stronger than its kind.
        main.GuildBook.PlaySeconds += RaidBook.MoveInSeconds;
        var (crowns, xp) = (p.Crowns, p.Hero.Xp);
        found.Body!.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => main.RaidLog.Roamers.Count == 0, 5);
        Check(main.RaidLog.Roamers.Count == 0 && p.Crowns > crowns, $"killed, it's gone for good: a bounty of {p.Crowns - crowns} crowns");
        await Until(() => !main.Tracker.InFight, 10);
        await Approach(raid);
        await Until(() => lair.Fighting, 10);
        foreach (var t in lair.Thralls.ToList()) t.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => lair.Boss is not null, 5);
        Check(lair.Boss is { Named: false } fresh && fresh.Kind.Id == raid.BossKind && fresh.Stats.MaxHealth < roamerHealth,
            $"back at the warren, a new goblin king, nameless and weaker ({lair.Boss?.Stats.MaxHealth:0} hp, against the roamer's {roamerHealth:0}), has moved in");
        lair.Boss?.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => lair.Hoard is not null, 5);
        if (lair.Hoard is { } hoard) main.PickUp(hoard);
        await Teleport(new Vector3(-150, 0, -120));
        await Until(() => !main.Tracker.InFight, 15);
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
        Check(save is { Quests.Count: 4 } && save.Crowns == main.PlayerNode.Crowns, $"{c.Character.Name}'s progress is on the server under {account}'s account");
        var sheet = loaded!.Characters.Single(x => x.Name == c.Character.Name);
        Check(sheet.Crowns == main.PlayerNode.Crowns, $"the login screen will show {sheet.Crowns} crowns at {sheet.Where}");
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

    /// <summary>
    /// The pursuits besides fighting: growing things at the allotments, a fire out past the walls, a lap of the
    /// Tumblers' Run, lifting a purse, a snare in the wilds, a treasure hunt, and a house of your own, built and
    /// furnished; and the feats for it all, kept with the company.
    /// </summary>
    async Task PursuitsTest()
    {
        var p = main.PlayerNode;
        var pack = p.Pack;
        var hud = main.HudNode;
        var book = main.Pursuits;
        var guilds = main.GuildBook;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        foreach (var e in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => !e.Friendly).ToList()) e.QueueFree();
        p.Crowns = 2000;

        // The allotments: potato seed from Gilly, a bed, and a crop that grows while you play.
        await Teleport(Main.AllotmentsAt + new Vector3(8, 0, -4));
        main.Talk(Who(Shops.Allotments.Id));
        Check(hud.Choose("Let me see") && hud.Choose("Potato Seed") && pack.Count("potato_seed") == 1, $"Gilly sells you seed potatoes ({string.Join(" | ", hud.Options.Take(3))})");
        hud.EndTalk();
        var bed = main.BedAt("allotment_0");
        await Teleport(bed + new Vector3(0, 0, -1.8f));
        var act = main.PursuitActionForTest(p.GlobalPosition);
        Check(act?.Verb == "plant something", $"at an empty bed: E to {act?.Verb}");
        act?.Act();
        Check(hud.Choose("Plant potatoes") && book.Plots.ContainsKey("allotment_0") && pack.Count("potato_seed") == 0, "you plant the potatoes");
        await Frames(90);
        Check(main.BedPlants("allotment_0") > 0, "and shoots come up in the bed");
        guilds.PlaySeconds += 7 * 60;
        await Frames(90);
        act = main.PursuitActionForTest(p.GlobalPosition);
        Check(act?.Verb.StartsWith("harvest") == true, $"in a few minutes they're ready: E to {act?.Verb}");
        act?.Act();
        Check(pack.Count("potato") >= 3 && !book.Plots.ContainsKey("allotment_0") && pack.Level(Job.Farming) >= 1 && pack.JobXp.GetValueOrDefault(Job.Farming) > 0, $"and you dig up {pack.Count("potato")} potatoes: {hud.LastLine}");
        Check(book.Feats.Contains("green_fingers"), "a feat for your first harvest");

        // A fire, out past the walls; you can cook at it, and it warms you whole.
        pack.Add("tinderbox");
        pack.Add("pine_log", 4);
        await Teleport(new Vector3(-150, 0, -120));
        Check(main.PackPursuitsForTest().Any(o => o.StartsWith("Light a fire")), "the pack offers to light a fire");
        for (var i = 0; i < 8 && main.FiresLit == 0; i++) { main.LightFireForTest(); await Frames(5); }
        Check(main.FiresLit == 1 && pack.Count("pine_log") < 4 && book.Feats.Contains("first_fire"), $"you light one: {hud.LastLine}");
        p.Stats.TakeHit(p.Stats.MaxHealth * 0.4, DamageKind.Physical, AttackKind.Weapon);
        var hurt = p.Stats.Health;
        await Frames(120);
        Check(p.Stats.Health > hurt, $"and resting by it heals ({hurt:0} to {p.Stats.Health:0})");
        await Teleport(p.GlobalPosition - p.GlobalTransform.Basis.Z * 0.5f);
        act = main.PursuitActionForTest(p.GlobalPosition);
        Check(act?.Verb == "cook at the fire", $"by the fire: E to {act?.Verb}");

        // The Tumblers' Run: every obstacle in order makes a lap.
        await Teleport(Main.CourseAt + new Vector3(4, 0, 6));
        main.Talk(Who(Shops.Course.Id));
        Check(hud.Choose("Teach me about agility"), "Cole tells you how the Run works");
        hud.EndTalk();
        var laps = book.Counters.GetValueOrDefault("laps");
        var xp = pack.JobXp.GetValueOrDefault(Job.Agility);
        for (var tries = 0; tries < 8 && book.Counters.GetValueOrDefault("laps") == laps; tries++)
            for (var i = 0; i < Agility.Course.Count; i++)
            {
                p.GlobalPosition = main.CourseStart(i) + Vector3.Up * 0.05f; // some start up on the platform
                p.Velocity = Vector3.Zero;
                await Frames(10);
                Check(main.PursuitActionForTest(p.GlobalPosition)?.Verb == Agility.Course[i].Verb, $"at obstacle {i + 1}: E to {Agility.Course[i].Verb}");
                await main.RunForTest(i);
                await Frames(10);
                if (main.LapNext == 0 && i < Agility.Course.Count - 1) break; // fell: start the lap again
                if (i < Agility.Course.Count - 1) Check(p.GlobalPosition.DistanceTo(main.CourseEnd(i)) < 1.5f, $"over {Agility.Course[i].Name}, and down the other side ({p.GlobalPosition.DistanceTo(main.CourseEnd(i)):0.0} m off)");
            }
        Check(book.Counters.GetValueOrDefault("laps") > laps && pack.JobXp.GetValueOrDefault(Job.Agility) >= xp + Agility.LapBonus, $"a whole lap: {pack.JobXp.GetValueOrDefault(Job.Agility)} agility xp");
        Check(!p.Scripted && !main.Working, "and you're your own again");

        // Lifting a purse.
        await Teleport(Main.FactorAt + new Vector3(0, 0, 2));
        var marks = main.Locals.Where(x => x.Npc.Id == Shops.Jeweller.Id).ToList();
        var pockets = book.Counters.GetValueOrDefault("pockets") + book.Counters.GetValueOrDefault("caught");
        if (marks.FirstOrDefault() is { } mark)
        {
            main.Talk(mark);
            Check(hud.Options.Any(o => o.StartsWith("(Pick their pocket)")), "you could pick Ilka's pocket");
            hud.EndTalk();
            main.PickPocketForTest(mark);
            Check(book.Counters.GetValueOrDefault("pockets") + book.Counters.GetValueOrDefault("caught") == pockets + 1, $"you try it: {hud.LastLine}");
            if (book.WaryUntil.ContainsKey(mark.Npc.Id))
            {
                main.PickPocketForTest(mark);
                Check(book.Counters.GetValueOrDefault("pockets") + book.Counters.GetValueOrDefault("caught") == pockets + 1, "and once caught, they're wary of you");
            }
        }
        else Check(false, "Ilka's at her stall by the Exchange");

        // A snare in the wilds.
        pack.Add("snare");
        await Teleport(new Vector3(-150, 0, -160));
        main.SetSnareForTest();
        Check(book.Snares.Count == 1 && pack.Count("snare") == 0, $"you set a snare: {hud.LastLine}");
        var snare = book.Snares[0];
        guilds.PlaySeconds = Math.Max(guilds.PlaySeconds, snare.ReadyAt) + 1;
        await Teleport(new Vector3((float)snare.X, 0, (float)snare.Z + 1));
        await Frames(70);
        act = main.PursuitActionForTest(p.GlobalPosition);
        Check(act?.Verb == "check the snare", $"by the snare: E to {act?.Verb}");
        var before = pack.Contents.Sum(c => c.N);
        main.CheckSnareForTest(snare);
        Check(book.Snares.Count == 0 && pack.Contents.Sum(c => c.N) > before && book.Feats.Contains("first_snare"), $"and something's in it: {hud.LastLine}");

        // A treasure hunt, from a torn map.
        pack.Add("torn_map");
        pack.Add("spade");
        main.ReadMapForTest();
        Check(book.Hunt is { Clues.Count: >= 2 }, $"the map shows {book.Hunt?.Clues.Count} places: {book.Hunt?.Current?.Text}");
        hud.EndTalk();
        var crowns = p.Crowns;
        for (var step = 0; step < 6 && book.Hunt is { Current: { } clue }; step++)
        {
            await Teleport(new Vector3((float)clue.X + 30, 0, (float)clue.Z));
            main.DigForTest();
            Check(book.Hunt?.Current == clue, $"dig too far off and there's nothing: {hud.LastLine}");
            await Teleport(new Vector3((float)clue.X + 1, 0, (float)clue.Z));
            main.DigForTest();
            hud.EndTalk();
        }
        Check(book.Hunt is null && p.Crowns > crowns && book.Feats.Contains("x_marks"), $"dig at every mark, and the treasure's yours: {hud.LastLine}");

        // A house: the plot from the Exchange, and building it stage by stage.
        await Teleport(Main.FactorAt + new Vector3(0, 0, 2));
        main.Talk(Who("tristinwold/factor"));
        crowns = p.Crowns;
        Check(hud.Choose("I'd like to buy land") && book.House.Owned && p.Crowns == crowns - Housing.PlotPrice, "Ottilie sells you the plot at Hearthside");
        hud.EndTalk();
        pack.Practise(Job.Building, 4000);
        foreach (var s in Housing.Stages.Concat(Housing.Furniture)) foreach (var (id, n) in s.Needs) pack.Add(id, n);
        await Teleport(Main.HousePlotAt + new Vector3(0, 0, 7));
        for (var i = 0; i < Housing.Stages.Count; i++)
        {
            act = main.PursuitActionForTest(p.GlobalPosition);
            act?.Act();
            Check(hud.Choose(Housing.Stages[i].Name) && book.House.Built == i + 1, $"{Housing.Stages[i].Name.ToLowerInvariant()} ({hud.LastLine})");
            hud.EndTalk();
            await Frames(70);
        }
        Check(book.House.Finished && book.Feats.Contains("homeowner") && main.HouseShown.Contains(":5:"), "the house is built");
        foreach (var f in Housing.Furniture)
        {
            main.PursuitActionForTest(p.GlobalPosition)?.Act();
            Check(hud.Choose($"Make {f.Name.ToLowerInvariant()}") && book.House.Furniture.Contains(f.Id), $"and furnished: {f.Name.ToLowerInvariant()}");
            hud.EndTalk();
        }
        main.PursuitActionForTest(p.GlobalPosition)?.Act();
        Check(hud.Choose("Sleep in your own bed") && main.WellRested, "you sleep in your own bed, and wake well rested");
        hud.EndTalk();
        var (heroXp, heroLevel) = (p.Hero.Xp, p.Hero.Level);
        main.GainXp(100);
        Check(p.Hero.Level > heroLevel || p.Hero.Xp - heroXp == (int)Math.Round(100 * p.Race.Xp * Housing.RestedXp), $"well rested, you learn faster ({p.Hero.Xp - heroXp} for 100)");
        pack.Add("pine_log", 3);
        var logs = pack.Count("pine_log");
        main.PursuitActionForTest(p.GlobalPosition)?.Act();
        Check(hud.Choose("Open the chest") && hud.Choose($"Put in {logs} Pine") && book.House.Chest.GetValueOrDefault("pine_log") == logs && pack.Count("pine_log") == 0,
            $"you put {logs} logs in your chest ({string.Join(" | ", hud.Options.Take(3))})");
        hud.EndTalk();
        Check(book.Plots.Count == 0 && main.PursuitActionForTest(main.BedAt("home_0") + new Vector3(0, 0, -1.8f)) is { Verb: "plant something" }, "and there's a garden to plant by the house");

        // And all of it's kept.
        TradeFeatsCheck();
        var saved = CompanySave.FromJson(main.Save());
        Check(saved.Pursuits is { } kept && kept.House.Finished && kept.House.Chest.GetValueOrDefault("pine_log") == logs && kept.Feats.SetEquals(book.Feats) && kept.RestedUntil == book.RestedUntil,
            $"the house, the chest and {book.Feats.Count} feats ({book.Points} points) are saved: {string.Join(", ", book.Feats)}");

        void TradeFeatsCheck() => Check(book.Points > 0, $"{book.Points} feat points");
    }

    /// <summary>On a server, Tristinwold's economy is the server's: one city for every company, and each keeps their own renown.</summary>
    async Task SharedCityTest()
    {
        // The city is the server's, shared by every company; what you do to it goes to the server and comes back.
        await Until(() => main.SharedCity, 10);
        Check(main.SharedCity, $"Tristinwold's economy is the server's now (day {main.Economy.Day})");
        main.Economy.Renown = 77;
        CitySnapshot? back = null;
        void Watch(Message m) { if (m is CitySnapshot s) back = s; }
        main.Received += Watch;
        var landed = main.Economy.Stock[Good.Fish];
        main.CityForTest(CityAction.Cargo(Good.Fish, 10));
        await Until(() => back is not null && back.City.Stock[Good.Fish] >= landed + 9.9, 10);
        main.Received -= Watch;
        Check(back is not null && back.City.Stock[Good.Fish] >= landed + 9.9, $"cargo landed in this game reaches the server's city ({landed:0} fish, then {back?.City.Stock[Good.Fish]:0})");
        await Frames(5);
        Check(main.Economy.Renown == 77, "and your own name at the Exchange stays yours");
    }

    /// <summary>The main story: the hero is an Immortal, made under the Needle; the road from waking to warden of the Rings.</summary>
    async Task StoryTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var story = main.Story;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        Check(story.Chapter == Chapter.Waking && hud.Heard("ring of black iron"), $"you wake at the Gallows Perch with an iron ring on your finger, remembering nothing ({story.Chapter})");
        await Teleport(new Vector3(City.GallowsPerch.X - 2.5f, 0, City.GallowsPerch.Z + 13));
        main.Talk(Who(ImmortalSaga.Corva));
        Check(hud.Choose("What am I?") && hud.TalkText.Contains("Immortal") && story.Chapter == Chapter.Archive,
            $"Mother Corva tells you what you are: {hud.TalkText[..Math.Min(110, hud.TalkText.Length)]}...");
        hud.EndTalk();
        main.Talk(Who(ImmortalSaga.Ilse));
        Check(hud.Choose("Mother Corva says") && hud.TalkText.Contains("Nine woke") && story.Chapter == Chapter.Proving,
            "Sister Ilse shows you the Magi's ledger: of a hundred given to the Needle, nine woke");
        hud.EndTalk();

        // Prove the ring holds: fall, and come back.
        var here = new Vector3(-150, 0, -120);
        await Teleport(here);
        var wolf = Spawn("wolf", here + new Vector3(2, 0, 0));
        await Frames(5);
        p.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, wolf);
        await Until(() => !p.Downed, 10);
        Check(story.Chapter == Chapter.Breach && story.Returns == 1 && hud.Heard("It holds"),
            $"you fall, and the ring brings you back to the Gallows Perch ({p.GlobalPosition.DistanceTo(City.PlayerStart):0} m from where you woke): it holds");
        if (IsInstanceValid(wolf)) wolf.QueueFree();

        // The Host's breach, beyond the walls.
        await Until(() => main.BreachNode is not null, 5);
        var breach = main.BreachNode;
        Check(breach is not null, $"the Host tears a breach open beyond the walls ({breach?.At.X:0}, {breach?.At.Z:0})");
        if (breach is null) return;
        await Teleport(breach.At + new Vector3(12, 0, 0));
        var kinds = new HashSet<string>();
        for (var i = 0; i < 40 && story.Chapter == Chapter.Breach; i++)
        {
            await Until(() => breach.Host.Count > 0, 6);
            foreach (var e in breach.Host.ToList())
            {
                kinds.Add(e.Kind.Name);
                e.TakeHit(1e6, DamageKind.Sun, AttackKind.Spell, false, p);
            }
            await Frames(10);
            if (p.Downed) { await Until(() => !p.Downed, 10); await Teleport(breach.At + new Vector3(12, 0, 0)); }
        }
        await Frames(150);
        Check(story.Chapter == Chapter.Dragonfire && main.BreachNode is null && kinds.Contains("Dire Imp"),
            $"the Host pours out ({string.Join(", ", kinds)}); slay {ImmortalSaga.BreachToClose} of it and the breach closes");
        Check(hud.Tracked.Any(t => t.StartsWith("The Rings of Tristinwold: Bring down a dragon the Rift has taken") && t.EndsWith(")")),
            $"and the tracker points you at a dragon the Rift has taken: {hud.Tracked.FirstOrDefault(t => t.StartsWith("The Rings"))}");

        // A dragon the Rift has taken.
        var dragon = Spawn("dragon_red_corrupt", breach.At + new Vector3(40, 0, 0));
        await Frames(5);
        dragon.TakeHit(1e9, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(10);
        Check(story.Chapter == Chapter.Warden, "and you bring down a dragon the Rift has taken, as no soldier could");
        await Teleport(new Vector3(City.GallowsPerch.X - 2.5f, 0, City.GallowsPerch.Z + 13));
        main.Talk(Who(ImmortalSaga.Corva));
        Check(hud.Choose("The dragon is dead") && story.Chapter == Chapter.Done && hud.TalkText.Contains(ImmortalSaga.Title),
            $"Mother Corva writes you into her ledger: {ImmortalSaga.Title}");
        hud.EndTalk();
    }

    /// <summary>The years go by: the young grow up, a child is born for each who comes of age, the old pass on; the hero ages and doesn't die.</summary>
    async Task AgeingTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var world = main.World;
        var place = world.SettlementsNear(0, 0, 3000).Where(s => !s.Hostile && s.Id != "city:tristinwold" && world.YouthsOf(s).Any(y => y.Race == "human"))
            .OrderBy(s => s.DistanceTo(0, 0)).First();
        var youth = world.YouthsOf(place).First(y => y.Race == "human");
        var at = new Vector3((float)place.X + 8, 0, (float)place.Z + 8);
        await Teleport(at);
        await Frames(60);
        Person? Find(string id) => main.PlacesNode.People.FirstOrDefault(x => x.Npc.Id == id);
        var kid = Find(youth.Id);
        Check(kid is not null && People.IsYouth(kid.Npc) && Person.LookFor(kid.Npc) is "child" or "youth", $"in {place.Name}: {kid?.Npc.Name}, {kid?.Npc.Age}, {Ageing.Word(Ageing.Stage(youth.Race, youth.Age))}");
        main.Talk(kid!);
        Check(hud.TalkText.Length > 0 && hud.TalkTitle.Contains($"({youth.Age}, {Ageing.Word(Ageing.Stage(youth.Race, youth.Age))} human)"), $"they say their age and stage: {hud.TalkTitle}");
        hud.EndTalk();
        var heroAge = Ageing.AgeOf(p.Hero, main.Economy.Day);

        // Years go by: as many as it takes the youth to come of age, and then some.
        var years = Ageing.Of("human").Adult - youth.Age + 1;
        main.Economy.Day += years * Calendar.DaysPerYear;
        await Teleport(new Vector3((float)place.X + 2000, 0, (float)place.Z)); // away, so the place is let go of
        await Frames(30);
        await Teleport(at);
        await Frames(60);
        var grown = Find(youth.Id);
        var child = Find($"{youth.Id}/g1");
        Check(grown is not null && !People.IsYouth(grown.Npc) && grown.Npc.Occupation != youth.Occupation,
            $"{years} years on, {youth.Name} has grown up: {grown?.Npc.Age}, {grown?.Npc.Occupation}");
        Check(child is not null && People.IsYouth(child.Npc), $"and a child has been born into the family: {child?.Npc.Name}, {child?.Npc.Age}");
        Check(Ageing.AgeOf(p.Hero, main.Economy.Day) >= heroAge + years - 0.01, $"you've aged too: {Ageing.Describe(p.Hero, p.Race.Id, p.Race.Name, main.Economy.Day)}");
        main.ShowSkills();
        Check(hud.TalkText.StartsWith("Today is the") && hud.TalkText.Contains("You are "), $"the skills window says the date and your age: {hud.TalkText[..Math.Min(120, hud.TalkText.Length)]}");
        hud.EndTalk();

        // Long past their people's years, an Immortal is still standing.
        main.Economy.Day += 120 * Calendar.DaysPerYear;
        Check(!p.Stats.Dead && Ageing.Describe(p.Hero, p.Race.Id, p.Race.Name, main.Economy.Day).Contains("Immortals age; they don't die of it"),
            $"a hundred and twenty years on: {Ageing.Describe(p.Hero, p.Race.Id, p.Race.Name, main.Economy.Day)}");
        main.Economy.Day -= (120 + years) * Calendar.DaysPerYear;
    }

    /// <summary>Barbarian clans raiding the frontier's small places, and the couple of guards each place has.</summary>
    async Task RaidersTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var raiders = main.RaidersNode;
        var book = main.GuildBook;
        foreach (var c in p.Companions().ToList()) c.QueueFree();

        // A clan's next raid, and the place it falls on.
        if (raiders.NextRaid(p.GlobalPosition) is not { } next) { Check(false, "a clan with somewhere to raid"); return; }
        var (clan, place, start) = next;
        await Teleport(new Vector3((float)place.X + 10, 0, (float)place.Z + 10));
        await Frames(60);
        var guards = main.PlacesNode.GuardsOf(place.Id).ToList();
        Check(guards.Count == Warband.Guards(place) && guards.Count > 0, $"{place.Name}, {place.What}, has {guards.Count} guard{(guards.Count == 1 ? "" : "s")} of its own (no Watch out here)");
        book.PlaySeconds = start;
        await Until(() => raiders.Out.Any(b => b.W.Id == clan.Id), 6);
        var band = raiders.Out.FirstOrDefault(b => b.W.Id == clan.Id);
        Check(band.Raiding?.Id == place.Id && band.Bodies.Count == clan.Raiders && hud.Heard($"have fallen on {place.Name}"),
            $"{clan.Name} under {clan.Chief} fall on {place.Name}: {band.Bodies?.Count} raiders");
        await Frames(90);
        Check(guards.Any(g => IsInstanceValid(g) && g.Rival is { Raider: true }) || guards.Any(g => !IsInstanceValid(g) || g.State == EnemyState.Dead),
            "and its guards fight them");
        Check(hud.Tracked.Any(t => t.StartsWith($"Raiders: {clan.Name} are raiding {place.Name}")), "the tracker shows the raid");
        var purse = p.Crowns;
        foreach (var e in band.Bodies?.ToList() ?? []) if (IsInstanceValid(e)) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => raiders.Record.Defended == 1, 6);
        Check(raiders.Record.Defended == 1 && p.Crowns >= purse + clan.Reward && raiders.Record.IsBroken(clan, book.PlaySeconds),
            $"you break the raid: {place.Name} pays you {p.Crowns - purse} crowns, and the clan falls back");

        // A raid nobody stops, on one of Tristinwold's trade routes: the place is plundered.
        var other = Warband.Near(0, 0, 4000).Where(w => w.Id != clan.Id && w.Targets(main.World).Count > 0)
            .Select(w => (W: w, At: (Math.Floor(book.PlaySeconds / Warband.Round) + 2) * Warband.Round - Warband.RaidSeconds + 5))
            .Select(x => (x.W, x.At, Place: x.W.Where(main.World, x.At).Raiding)).FirstOrDefault(x => x.Place is not null && x.Place.Id != place.Id);
        if (other.W is null) { Check(false, "another clan to raid a trade route"); return; }
        var market = main.TradeLog.Market(other.Place!, main.Economy.Day);
        var stock = market.Stock.Values.Sum();
        await Teleport(City.PlayerStart);
        book.PlaySeconds = other.At;
        await Until(() => market.Raided > 0, 25);
        Check(market.Raided == 1 && market.Stock.Values.Sum() < stock * 0.7 && hud.Heard($"raided {other.Place!.Name}"),
            $"nobody stops {other.W.Name} at {other.Place!.Name}: its stores are plundered, and word comes to the city");
    }

    /// <summary>The caravan yard: load, drive, then trade on your own account, with your own wagon and horses.</summary>
    async Task CaravanTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var trade = main.TradeLog;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Any loading work"), "Garrick Wain at the caravan yard takes you on as a loader");
        hud.EndTalk();
        var purse = p.Crowns;
        for (var i = 0; i < TradeBook.LoadsToDrive; i++) main.LoadForTest();
        Check(trade.Rank == CaravanRank.Driver && p.Crowns == purse + TradeBook.LoadWage * TradeBook.LoadsToDrive,
            $"ten loads onto the wagon (+{p.Crowns - purse} crowns), and he makes you a driver");

        // Tristinwold has always traded with its nearest market towns; everything else is for the hero to find.
        var old = trade.Routes.Select(m => m.Name).ToList();
        Check(old.Count == 2 && trade.Routes.All(m => main.Explored.Rumored.Contains(m.Id) || main.Explored.Discovered.Contains(m.Id)),
            $"Tristinwold's old trade routes are open from the start: {string.Join(" and ", old)} (on the map)");
        Check(hud.Tracked.Any(t => t.StartsWith("Starter work: the caravan yard")), "and a starter's job on the tracker: the caravan yard is hiring guards");
        var village = main.World.SettlementsNear(0, 0, 2500).Where(s => TownMarket.Trades(s) && !trade.Towns.ContainsKey(s.Id)).OrderBy(s => s.DistanceTo(0, 0)).First();
        await Teleport(new Vector3((float)village.X + 5, 0, (float)village.Z + 5));
        await Frames(30);
        Check(trade.Routes.Any(m => m.Id == village.Id) && trade.Routes.Count() == 3, $"finding {village.Name} opens a third trade route");
        for (var run = 1; run <= TradeBook.RunsToOwn; run++)
        {
            await Teleport(Main.YardAt + new Vector3(0, 0, 4));
            main.Talk(Who("tristinwold/caravans"));
            var asked = hud.Choose("Give me a wagon to drive");
            hud.EndTalk();
            var r = trade.Run;
            if (run == 1) Check(asked && r is not null && main.WagonNode is not null, $"he gives you a wagon and a horse: {r?.Load} loads of {r?.Out} for {r?.ToName}, {r?.Pay} crowns");
            if (r is null) return;
            var town = main.World.Find(r.To)!;
            await Teleport(new Vector3((float)town.X + 6, 0, (float)town.Z + 6));
            await Until(() => r.Returning, 8);
            if (run == 1) Check(r.Returning && main.WagonNode!.GlobalPosition.DistanceTo(p.GlobalPosition) < 20, $"the wagon follows you to {r.ToName}: unloaded, and loaded with {r.Back} for home");
            var stock = main.Economy.Stock[r.Back];
            await Teleport(Main.YardAt + new Vector3(0, 0, 4));
            await Until(() => trade.Run is null, 8);
            if (run == 1) Check(trade.Run is null && trade.Runs == 1 && main.Economy.Stock[r.Back] > stock, $"home again: the run is paid, and {r.ToName}'s {r.Back} goes into Tristinwold's stores");
        }
        Check(trade.MayOwn, $"after {TradeBook.RunsToOwn} runs, Garrick will sell you a wagon of your own");

        // A trader: your own wagon, a draught horse to pull it (and ride).
        p.Crowns += 1000;
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Sell me a wagon") && trade.OwnWagon && trade.Rank == CaravanRank.Entrepreneur, "you buy a wagon: you're in business for yourself");
        hud.EndTalk();
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Sell me a draught horse") && main.Bonds.Any(b => b.Steed && b.Rideable), $"and a draught horse: {main.Bonds.FirstOrDefault(b => b.Steed)?.Name}");
        hud.EndTalk();
        await Frames(30);
        main.RideNow();
        await Frames(20);
        Check(p.Mount is { } mount && mount.Bond?.Steed == true, $"you can ride your horse ({p.Mount?.Stats.Name})");
        var from = p.GlobalPosition;
        if (p.Mount is { } m)
        {
            Input.ActionPress("move_forward");
            Input.ActionPress("sprint");
            await Frames(90);
            Input.ActionRelease("move_forward");
            Input.ActionRelease("sprint");
            Check(p.GlobalPosition.DistanceTo(from) > 4 && !m.Flying, $"and gallop on it ({p.GlobalPosition.DistanceTo(from):0} m), on the ground");
        }
        main.RideNow();
        await Frames(10);
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Hitch up my wagon") && trade.WagonOut && trade.Capacity == TradeBook.PerHorse, $"hitched: room for {trade.Capacity} loads");
        hud.EndTalk();
        main.Talk(Who("tristinwold/caravans"));
        hud.Choose("Load goods");
        var spent = p.Crowns;
        hud.Choose(hud.Options.First());
        Check(trade.Loaded > 0 && p.Crowns < spent, $"you buy goods at the Exchange for the road ({trade.Loaded} aboard)");
        hud.EndTalk();

        // Out to a town you know: it makes some things cheap and wants others.
        var known = trade.Towns.Values.OrderBy(t => t.DistanceTo(0, 0)).First();
        await Teleport(new Vector3((float)known.X + 4, 0, (float)known.Z + 4));
        await Frames(60);
        var local = main.PlacesNode.People.FirstOrDefault(x => x.Npc.HomeId == known.Id);
        Check(local is not null, $"at {known.Name}, someone to trade with ({local?.Npc.Name})");
        if (local is null) return;
        local.GlobalPosition = p.GlobalPosition + new Vector3(1.5f, 0, 0);
        main.Talk(local);
        Check(hud.Choose($"What does {known.Name} trade in") && hud.TalkText.Contains("We make"), $"{local.Npc.Name}: {hud.TalkText}");
        hud.EndTalk();
        main.Talk(local);
        var before = p.Crowns;
        Check(hud.Choose("Trade from my wagon") && hud.Choose("Sell") && p.Crowns > before, $"you sell from your wagon at {known.Name} (+{p.Crowns - before} crowns)");
        if (hud.Options.FirstOrDefault(o => o.StartsWith("Buy")) is { } buy)
        {
            hud.Choose(buy);
            Check(trade.Loaded > 0, $"and buy what they make cheap, for Tristinwold ({buy})");
        }
        hud.EndTalk();
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        if (trade.Loaded > 0)
        {
            main.Talk(Who("tristinwold/caravans"));
            before = p.Crowns;
            Check(hud.Choose("Sell what's on my wagon") && trade.Loaded == 0 && p.Crowns > before, $"home, the Exchange takes the load (+{p.Crowns - before}; {trade.Profit} made on the road all told)");
            hud.EndTalk();
        }
        main.Talk(Who("tristinwold/caravans"));
        hud.Choose("Put my wagon away");
        hud.EndTalk();

        // A starter's job: guarding the city's caravan along the nearest route, open to a green hero.
        p.Hero.Level = 2;
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Any caravans that need a guard") && hud.TalkText.StartsWith("New, are you") && hud.Choose("I'll guard it")
              && trade.Guarding is { Bandits: 2 } && main.GuardedWagon is not null, $"a green hero's first job: guard the city's caravan to {trade.Guarding?.ToName} (short road, two bandits)");
        hud.EndTalk();
        if (trade.Guarding is { } g && main.GuardedWagon is { } cart)
        {
            var start = cart.GlobalPosition;
            await Frames(120);
            Check(cart.GlobalPosition.DistanceTo(start) > 2, $"the carter drives off with you beside her ({cart.GlobalPosition.DistanceTo(start):0} m)");
            // Further along the road: the ambush.
            var target = new Vector3((float)g.X, 0, (float)g.Z);
            var mid = start.Lerp(target, (float)g.Ambushes[0] + 0.05f);
            cart.GlobalPosition = mid with { Y = City.FloorAt(mid) };
            await Teleport(mid + new Vector3(3, 0, 3));
            await Until(() => g.Sprung > 0, 4);
            await Frames(30);
            var bandits = GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e.Kind.Id is "bandit" or "bandit_chief" && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(mid) < 60).ToList();
            Check(g.Sprung == 1 && bandits.Count >= g.Bandits, $"ambush on the road: {bandits.Count} bandits (the wagon pulls up)");
            foreach (var b in bandits) b.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
            await Frames(30);
            var purse0 = p.Crowns;
            var near = target + (start - target).Normalized() * 8;
            cart.GlobalPosition = near with { Y = City.FloorAt(near) };
            await Teleport(near + new Vector3(3, 0, 3));
            await Until(() => trade.Guarding is null, 6);
            Check(trade.Guarded == 1 && p.Crowns > purse0 && hud.Heard("The caravan you guarded"), $"you see it through to {g.ToName}: +{p.Crowns - purse0} crowns, and a big load each way for Tristinwold");
        }
        Check(!hud.Tracked.Any(t => t.StartsWith("Starter work")), "the starter's job is done");
        // After that, the long roads are for those who've made a name.
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Any caravans that need a guard") && hud.TalkText.Contains("steel") && hud.Options.Any(o => o.StartsWith("The short road"))
              && !hud.Options.Any(o => o.StartsWith("To ")), "after that the short road is there any day, but Garrick won't send a green guard down the long roads");
        Check(hud.Choose("The short road") && trade.Guarding is { Bandits: 2 }, "and you can take the short road again");
        trade.Guarding = null; // (not today, after all: the next job's wagon replaces it)
        hud.EndTalk();
        p.Hero.Level = 8;
        main.Talk(Who("tristinwold/caravans"));
        hud.Choose("Any caravans that need a guard");
        var roads = hud.Options.Where(o => o.StartsWith("To ")).ToList();
        Check(roads.Count >= 3 && hud.Options.Any(o => o.StartsWith("The short road")), $"but a seasoned one, he will, along every route: {string.Join("; ", roads)}");
        hud.EndTalk();

        // A glut: someone sells the village a hundred fish, and the price there falls through the floor.
        var glutted = trade.Towns[village.Id];
        glutted.Sell(Good.Fish, 100);
        await Until(() => hud.Heard("drowning in fish"), 8);
        Check(hud.Heard("drowning in fish"), $"{village.Name} drowns in fish, and word comes to the yard");
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Any gluts to move"), "Garrick has work carting it");
        var glutJob = hud.Options.FirstOrDefault(o => o.Contains("fish from"));
        Check(glutJob is not null && hud.Choose(glutJob) && trade.Run is { Glut: true }, $"you take it: {glutJob}");
        hud.EndTalk();
        if (trade.Run is { Glut: true } gr)
        {
            await Teleport(new Vector3((float)gr.FromX + 6, 0, (float)gr.FromZ + 6));
            await Until(() => gr.Loaded, 6);
            Check(gr.Loaded, $"loaded at {gr.FromName} for next to nothing");
            var purseG = p.Crowns;
            var dest = gr.To == TradeBook.CityId ? Main.YardAt + new Vector3(0, 0, 4) : new Vector3((float)gr.X + 6, 0, (float)gr.Z + 6);
            await Teleport(dest);
            await Until(() => trade.Run is null, 6);
            Check(trade.Run is null && p.Crowns > purseG && trade.GlutsMoved == 1, $"and carted to {gr.ToName}, where they want it: +{p.Crowns - purseG} crowns");
        }

        // And Tristinwold's own caravans trade with the places it knows, a day at a time.
        main.Economy.NextDayAt = main.GuildBook.PlaySeconds;
        await Until(() => hud.Heard("A caravan from"), 6);
        Check(hud.Heard("A caravan from"), "a new day: Tristinwold's own caravans trade with the places you've found");
    }

    /// <summary>Far past the edge of every map: the land goes on, with its villages and dens, and lairs of its own.</summary>
    async Task FrontierTest()
    {
        var p = main.PlayerNode;
        var world = main.World;
        var far = new Vector3(30_000, 0, -8_000);
        await Teleport(far);
        await Frames(90);
        Check(main.TerrainNode.HasGround(p.GlobalPosition) && main.TerrainNode.Count > 20,
            $"31 km from Tristinwold the land goes on ({main.TerrainNode.Count} chunks around you, {world.BiomeAt(far.X, far.Z)})");
        var sites = world.SettlementsNear(far.X, far.Z, 3000).ToList();
        Check(sites.Count > 0, $"with places of its own ({string.Join(", ", sites.Take(4).Select(s => s.Name))})");
        var lair = FarLairs.Near(world, far.X, far.Z, 3000).MinBy(r => r.DistanceTo(far.X, far.Z));
        Check(lair is not null, $"and lairs no map in Tristinwold shows: {lair?.Name}, {lair?.Boss}, for heroes of level {lair?.MinLevel} to {lair?.MaxLevel}");
        if (lair is null) return;
        await Teleport(new Vector3((float)lair.X + 70, 0, (float)lair.Z));
        await Until(() => main.RaidLairs.Any(l => l.Raid.Id == lair.Id && l.Built), 12);
        Check(main.RaidLairs.Any(l => l.Raid.Id == lair.Id && l.Built), $"{lair.Name} is there, built, when you get to it");
        await Teleport(City.PlayerStart);
        await Until(() => main.RaidLairs.All(l => l.Raid.Id != lair.Id), 12);
        Check(main.RaidLairs.All(l => l.Raid.Id != lair.Id) && main.RaidLairs.Count >= Raids.All.Count,
            "and it's let go once you're far from it (the nine near home stay)");
    }

    async Task Teleport(Vector3 at)
    {
        var p = main.PlayerNode;
        main.TerrainNode.EnsureAround(at, 1);
        p.GlobalPosition = at with { Y = City.FloorAt(at) + 1 };
        p.Velocity = Vector3.Zero;
        await Frames(20);
    }

    /// <summary>
    /// The stranger things of the wilds, each in its own body with its own trick: the kipine dives on you and steals
    /// your coin (you get it back when it dies); deathcrest apes go after the walking dead, and blink to your flank;
    /// a dire imp vanishes and strikes from beside you; a hurt imp turns into a raven and flies; a winged ape throws
    /// rocks; and the wilds have them all, out past the safe country.
    /// </summary>
    async Task StrangeWildsTest()
    {
        var p = main.PlayerNode;
        var here = new Vector3(0, 0, 400); // open, safe country: nothing else around to interfere
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        await Teleport(here);
        p.Hero.Level = 8;
        p.Fit();
        Enemy Wild(string kind, Vector3 offset)
        {
            var e = Spawn(kind, here + offset with { Y = City.FloorAt(here + offset) - here.Y + 0.5f });
            WildBrain.Give(main, e);
            return e;
        }
        async Task<double> Hurt(Func<bool> also, double seconds)
        {
            p.Stats.Heal(1e6);
            var start = p.Stats.Health;
            await Until(() => p.Stats.Health < start && also(), seconds);
            var lost = start - p.Stats.Health;
            p.Stats.Heal(1e6);
            return lost;
        }
        void Clear(params Enemy[] es) { foreach (var e in es) if (IsInstanceValid(e)) e.QueueFree(); }

        // The kipine: up into the air and down on you; it takes your coin, and gives it back when it dies.
        var kipine = Wild("kipine", new Vector3(0, 0, -16));
        Check(kipine.Figure is Creature && kipine.Figure.FindChildren("*", "Node3D", true, false).OfType<FlapWings>().Any() && kipine.Think is not null, "a kipine: an owlbear with wings and antlers");
        kipine.Provoke(p);
        var flew = false;
        var lost = await Hurt(() => flew |= kipine.GlobalPosition.Y > City.FloorAt(kipine.GlobalPosition) + 4, 20);
        Check(flew && lost > 0, $"it beats up into the air and comes down on you ({lost:0} harm)");
        p.Crowns = 100;
        p.GlobalPosition = kipine.GlobalPosition + new Vector3(0, 0.5f, 2);
        await Until(() => p.Crowns < 100, 25);
        var taken = 100 - p.Crowns;
        Check(taken > 0, $"it snatches {taken} crowns");
        var purse = p.Crowns;
        kipine.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(5);
        Check(p.Crowns >= purse + taken, $"killed, it gives them back ({purse} -> {p.Crowns}, and it may drop a few coins of its own)");
        await Until(() => !main.Tracker.InFight, 10);

        // Deathcrest apes: they hunt the walking dead, and blink through shadow to your flank.
        p.GlobalPosition = (here + new Vector3(0, 0, 45)) with { Y = City.FloorAt(here + new Vector3(0, 0, 45)) + 1 }; // well out of their sight
        var apes = new[] { Wild("deathcrest_ape", new Vector3(-12, 0, 0)), Wild("deathcrest_ape", new Vector3(-13, 0, 2)) };
        var draugr = Spawn("draugr", here + new Vector3(-4, 0, 0));
        await Until(() => apes.Any(a => a.Rival == draugr), 6);
        Check(apes[0].Figure is Creature && apes.Any(a => a.Rival == draugr), "deathcrest apes go after a draugr on sight");
        Clear(draugr);
        p.GlobalPosition = here with { Y = City.FloorAt(here) + 1 };
        apes[0].Provoke(p);
        apes[1].Provoke(p);
        var blinked = false;
        var at = apes[0].GlobalPosition;
        lost = await Hurt(() => blinked |= apes.Any(a => a.GlobalPosition.DistanceTo(at) > 3.5f && a.GlobalPosition.DistanceTo(p.GlobalPosition) < 3.2f), 20);
        Check(lost > 0, $"the pack's poisoned claws ({lost:0} harm)");
        Clear(apes);
        await Until(() => !main.Tracker.InFight, 10);

        // A dire imp vanishes, and strikes from beside you.
        var dire = Wild("dire_imp", new Vector3(8, 0, -6));
        dire.Provoke(p);
        var vanished = false;
        lost = await Hurt(() => (vanished |= !dire.Figure.Visible) && dire.Figure.Visible, 25);
        Check(vanished && lost > 0, $"a dire imp vanishes, and strikes from beside you ({lost:0} harm)");
        Clear(dire);

        // An imp, badly hurt, turns into a raven and flies off.
        var imp = Wild("imp", new Vector3(3, 0, -3));
        imp.Provoke(p);
        await Frames(10);
        var devil = imp.Figure;
        imp.TakeHit(imp.Stats.MaxHealth * 0.7, DamageKind.Physical, AttackKind.Weapon, false, p);
        await Until(() => imp.State == EnemyState.Fleeing, 3);
        Check(imp.State == EnemyState.Fleeing && imp.Figure != devil, "a hurt imp turns into a raven and flies");
        Clear(imp);

        // A winged ape throws rocks at anyone who keeps away.
        var ape = Wild("winged_ape", new Vector3(0, 0, -18));
        Check(ape.Figure.FindChildren("*", "Node3D", true, false).OfType<FlapWings>().Any(), "a winged ape, wings and sword");
        ape.Provoke(p);
        lost = await Hurt(() => true, 12);
        Check(lost > 0, $"it hurls rocks and comes down on you ({lost:0} harm)");
        Clear(ape);
        await Until(() => !main.Tracker.InFight, 10);
    }

    /// <summary>
    /// Loot, the pack, the market and the trades: a wolf leaves its pelt; the smith sells a sword, which you wear, and
    /// it shows in your hand and makes you hit harder; the forge smelts scrap into iron; a herb picked in the wild goes
    /// in the pack; a potion heals; the shops buy back what you don't want.
    /// </summary>
    /// <summary>The game menu, saving on demand, and starting where the last session ended.</summary>
    async Task SaveTest()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        hud.EndTalk();
        await Press("game_menu");
        var leave = main.Choice.Account is null ? new[] { "Back to the start screen" } : ["Change character", "Log out"];
        Check(hud.Talking && hud.Options.Contains("Save now (F5)") && leave.All(hud.Options.Contains) && hud.Options.Contains("Quit the game") == (RemotePlay.Current is null),
            $"Esc opens the game menu: {string.Join(", ", hud.Options)}");
        await Press("game_menu");
        Check(!hud.Talking, "Esc again closes it");
        hud.ToggleMap();
        await Press("game_menu");
        Check(!hud.MapOpen && !hud.Talking, "Esc closes the map before anything else");

        p.GlobalPosition = new Vector3(-150, 1, -120);
        p.Rotation = new Vector3(0, 0.7f, 0);
        await Until(() => p.IsOnFloor(), 3);
        await Press("save");
        var said = main.Choice.Account is null ? "Saved." : "Saved, on the server too.";
        await Until(() => hud.LastLine == said || hud.LastLine.StartsWith("The server didn't"), 10);
        var saved = CompanySave.FromJson(Godot.FileAccess.GetFileAsString(main.SavePath));
        Check(saved.Where is { } w && Math.Abs(w.X + 150) < 1 && Math.Abs(w.Z + 120) < 1 && Math.Abs(w.Facing - 0.7f) < 0.05f,
            $"F5 saves where you stand ({saved.Where})");
        Check(hud.LastLine == said, $"and says so: {hud.LastLine}");

        main.StandAtForTest(new Spot(320, 0, 410, 2f));
        await Until(() => p.IsOnFloor(), 4);
        var floor = City.FloorAt(p.GlobalPosition);
        Check(p.GlobalPosition.DistanceTo(new Vector3(320, floor, 410)) < 1.5f && Math.Abs(p.Rotation.Y - 2f) < 0.05f,
            $"a saved spot puts you back there, on the ground ({p.GlobalPosition.X:0}, {p.GlobalPosition.Y:0.0}, {p.GlobalPosition.Z:0}; ground {floor:0.0})");
    }

    /// <summary>A key press the game's _Process sees (several physics frames can pass in one drawn frame).</summary>
    async Task Press(string action)
    {
        Input.ActionPress(action);
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ActionRelease(action);
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task TradeTest()
    {
        var p = main.PlayerNode;
        var pack = p.Pack;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        // Loot: wolves leave pelts.
        await Teleport(new Vector3(0, 0, 400));
        var pelts = pack.Count("wolf_pelt");
        for (var i = 0; i < 6 && pack.Count("wolf_pelt") == pelts; i++)
        {
            var wolf = Spawn("wolf", p.GlobalPosition + new Vector3(0, 0, -4));
            await Frames(5);
            wolf.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, p);
            await Frames(5);
        }
        Check(pack.Count("wolf_pelt") > pelts, $"a wolf leaves its pelt ({Items.Count("wolf_pelt", pack.Count("wolf_pelt"))} in the pack)");
        await Until(() => !main.Tracker.InFight, 10);

        // The market by the Gallows Perch: the smith sells a sword.
        await Teleport(new Vector3(80, 0, 8));
        var smith = main.GetChildren().OfType<Person>().FirstOrDefault(x => x.Npc.Id == Shops.Smithy.Id);
        Check(smith is not null && main.GetChildren().OfType<Person>().Count(x => Shops.All.Any(s => s.Id == x.Npc.Id)) == Shops.All.Count, $"all {Shops.All.Count} shopkeepers are at their stalls, yards and kitchens");
        p.Crowns = 200;
        main.Talk(smith!);
        Check(main.HudNode.Choose("Let me see"), "the smith shows her wares");
        Check(main.HudNode.Choose("Iron Sword"), "and sells you an iron sword");
        var sword = Shops.Charge(Items.Get("iron_sword")!.Value, p.Markup); // a human pays a little less
        Check(pack.Count("iron_sword") == 1 && p.Crowns == 200 - sword, $"for {sword} crowns");
        main.HudNode.EndTalk();

        // Worn, it shows in your hand and makes you hit harder.
        var damage = p.Stats.DamageMult;
        main.ShowPack();
        Check(main.HudNode.Choose("Wear Iron Sword"), "you put it on from your pack");
        main.HudNode.EndTalk();
        await Frames(5);
        Check(p.Stats.DamageMult > damage && p.Figure.FindChildren("Worn_iron_sword", "MeshInstance3D", true, false).Count == 1,
            $"it's in your hand, and you hit harder ({damage:0.00} -> {p.Stats.DamageMult:0.00})");
        pack.Add("iron_helm");
        pack.Equip("iron_helm");
        var armor = p.Stats.Armor;
        p.Dress();
        Check(p.Stats.Armor > armor && p.Figure.FindChildren("Worn_iron_helm", "MeshInstance3D", true, false).Count == 1, "a helm too, on your head");

        // The forge: scrap into iron.
        pack.Add("scrap_iron", 3);
        main.Talk(smith!);
        Check(main.HudNode.Choose("Can I use your forge"), "she lets you use her forge");
        Check(main.HudNode.Choose("Make Iron Ingot"), "you smelt scrap into an iron ingot");
        Check(pack.Count("iron_ingot") >= 1 && pack.JobXp.GetValueOrDefault(Job.Smithing) > 0, "and learn a little smithing");
        main.HudNode.EndTalk();

        // Sell what you don't need.
        var before = p.Crowns;
        main.Talk(smith!);
        main.HudNode.Choose("I've things to sell");
        Check(main.HudNode.Choose("Sell every"), "she buys your pelts");
        Check(p.Crowns > before && pack.Count("wolf_pelt") == 0, $"for {p.Crowns - before} crowns");
        main.HudNode.EndTalk();

        // Wren's Menagerie: a wolf pup, already tame, for a price; and never a dragon.
        var wren = main.GetChildren().OfType<Person>().First(x => x.Npc.Id == Shops.Menagerie.Id);
        p.Crowns = 1000;
        var bonds = main.Bonds.Count;
        main.Talk(wren);
        Check(main.HudNode.Choose("Show me your animals"), "the beastkeeper shows her animals");
        Check(main.HudNode.Options.Count() == Menagerie.Stock.Count + 1 && !main.HudNode.Options.Any(o => o.Contains("Wyrmling") || o.Contains("Dragon,")),
            $"{Menagerie.Stock.Count} young beasts, and no dragon among them");
        var pup = Menagerie.Stock.Single(o => o.KindId == "wolf");
        var paid = Menagerie.Price(pup, pack.Level(Job.Taming), p.Markup);
        Check(main.HudNode.Choose("Wolf Pup") && main.Bonds.Count == bonds + 1 && main.Bonds[^1].KindId == "wolf" && p.Crowns == 1000 - paid,
            $"a wolf pup joins you for {paid} crowns");
        Check(pack.JobXp.GetValueOrDefault(Job.Taming) > 0, "and you learn a little beast taming");
        main.HudNode.EndTalk();

        // A herb in the wild.
        var plant = Herbs.Near(main.World, 0, 400, 400).First(x => main.HerbLog.Ready(x, main.GuildBook.PlaySeconds));
        await Teleport(new Vector3((float)plant.X + 1, 0, (float)plant.Z));
        await Until(() => main.HerbPatchNode.Within(new Vector3((float)plant.X, City.FloorAt(new Vector3((float)plant.X, 0, (float)plant.Z)), (float)plant.Z)) is not null, 5);
        p.GlobalPosition = new Vector3((float)plant.X + 0.5f, City.FloorAt(new Vector3((float)plant.X, 0, (float)plant.Z)) + 0.5f, (float)plant.Z);
        await Frames(5);
        var herbs = pack.Count(plant.HerbId);
        Check(main.PickHere() && pack.Count(plant.HerbId) > herbs && !main.HerbLog.Ready(plant, main.GuildBook.PlaySeconds),
            $"you pick {Items.Get(plant.HerbId)!.Name.ToLowerInvariant()} in the wild, and it's gone until it grows back");

        // A potion heals.
        pack.Add("healing_draught");
        p.Stats.Lose(p.Stats.MaxHealth * 0.7);
        var hurt = p.Stats.Health;
        main.ShowPack();
        Check(main.HudNode.Choose("Drink Healing Draught") && p.Stats.Health > hurt, "a healing draught heals");
        main.HudNode.EndTalk();
        p.Stats.Heal(1e9);
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
        GD.Print(_log[^1]); // as it goes, so a long run shows how far it got
        if (!ok) throw new Exception(what);
    }

    void Finish(int code)
    {
        GD.Print(string.Join("\n", _log));
        GD.Print(code == 0 ? "SELFTEST PASSED" : "SELFTEST FAILED");
        GetTree().Quit(code);
    }
}
