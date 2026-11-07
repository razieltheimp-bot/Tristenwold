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
        main.RoamersNode.Patient = false; // the scripted fights get on with it
        main.RoamersNode.WildDeviants = false; // the scripted fights below lose on purpose: only the hunt's own test makes deviants
        Enemy.DeathOdds = 0; // and what's cut down in them stays down, bar the one test of running off
        main.TownLifeNode.Stirring = false; // no young thief lifts the scripted hero's purse mid-test: the town's own test turns it on
        try
        {
            GD.PrintErr("[selftest] ready; waiting ten physics frames");
            await Frames(10);
            GD.PrintErr("[selftest] ten physics frames passed; starting");
            if (OS.GetCmdlineUserArgs().Contains("--rival-only")) { await TheRival(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--repute-only")) { await TheRepute(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--feud-only")) { await TheFeud(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--tavern-only")) { await TheTavern(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--herofight-only")) { await HeroFightTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--ride-only")) { await TheRide(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--helm-only")) { await TheHelm(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--forge-only")) { await TheForge(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--legends-only")) { await TheLegends(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--quickkeys-only")) { await TheQuickKeys(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--pirates-only")) { await ThePirates(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--isle-only")) { await TheIsleTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--growth-only")) { await TheGrowth(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--lair-only")) { await DeviantLairTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--village-only")) { await TheVillageDeck(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--towns-only")) { await LivingTownsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--marks-only")) { await ClassMarksTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--limits-only")) { await NewLimitsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--drifters-only")) { await DriftersTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--undying-only")) { await UndyingTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--deeps-only")) { await DeepsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--syndicate-only")) { await SyndicateTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--steps-only")) { await StepsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--warlords-only")) { await WarlordsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--stash-only")) { await StashTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--heist-only")) { await HeistTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--handiwork-only")) { await HandiworkTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--panic-only")) { await PanicTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--halfform-only")) { await TheHalfForm(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--armoured-shift-only")) { await ArmouredShift(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--hunt-only")) { await HuntTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--living-only")) { await LivingWorldTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--hunt-living")) { await HuntTest(); await LivingWorldTest(); Finish(0); return; } // the one after the other, as the full run has them
            if (OS.GetCmdlineUserArgs().Contains("--frontier-only")) { await FrontierTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--story-only")) { await StoryTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--caravan-only")) { await CaravanTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--raiders-only")) { await RaidersTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--sea-only")) { await SeaTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--opensea-only")) { await OpenSeaTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--keepers-only")) { await KeeperTest(); await FleetTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--pupils-only")) { await PupilTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--refuge-only")) { await GrowthTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--spread-only")) { await SpreadTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--kit-only")) { await KitTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--multiclass-only")) { await MulticlassTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--elemental-only")) { await ElementalFormTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--seams-only")) { await SeamsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--standing-only")) { await StandingTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--artificer-only")) { await ArtificerTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--shadows-only")) { await ShadowsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--humans-only")) { await HumansTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--poise-only")) { await PoiseTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--ageing-only")) { await AgeingTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--wild-only")) { await StrangeWildsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--dragons-only")) { await DragonsTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--seahunt-only")) { await SeaHuntTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--climb-only")) { await ClimbTest(); Finish(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--lairs-only")) { await LairTest(); Finish(0); return; }
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
            if (OS.GetCmdlineUserArgs().Contains("--company-only")) { await StoryTest(); await CompanyTest(); await GuildTest(); await RaidTest(); await LairTest(); await DeviantLairTest(); await HuntTest(); await LivingWorldTest(); await FrontierTest(); await CaravanTest(); await RaidersTest(); await SeaTest(); await StandingTest(); await ArtificerTest(); await ShadowsTest(); await HumansTest(); await PoiseTest(); await AgeingTest(); await StrangeWildsTest(); await TradeTest(); await TownWorkTest(); await PursuitsTest(); await SoundTest(); await SaveTest(); Finish(0); return; } // just the new systems
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
        Check(w.AuthoredCities.Where(c => c.StartsKnown).All(c => main.Explored.Rumored.Contains(c.Id) || main.Explored.Discovered.Contains(c.Id)) && main.Explored.Rumored.Count >= 4, $"five cities are known from the start: Tristinwold, and four more on the map (with the old trade routes, {main.Explored.Rumored.Count} places heard of)");

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

        // Mining down Greyhollow.
        await Teleport(Main.QuarryAt + new Vector3(5, 0, 6));
        main.Talk(Who(Shops.Quarry.Id));
        Check(hud.Choose("Let me see") && hud.Choose("Pickaxe") && pack.Count("pickaxe") == 1, "Dagna sells you a pickaxe");
        hud.EndTalk();
        main.Talk(Who(Shops.Quarry.Id));
        Check(hud.Choose("Is there work down the mine") && main.MineLog.Stage == MineStage.Hired, "Dagna takes you on");
        hud.EndTalk();
        Check(Main.MineDoor.DistanceTo(Main.QuarryAt) < 20, $"the mine door is at the quarry ({Main.MineDoor.DistanceTo(Main.QuarryAt):0} m from its middle)");
        await Teleport(Main.MineDoor + new Vector3(0, 0, 2.5f));
        Check(p.GlobalPosition.Y < Main.MineDoor.Y + 3, "the cutting is level with the quarry floor");
        main.EnterMineForTest();
        await Until(() => main.InMine, 5);
        Check(main.InMine && p.GlobalPosition.Y > Cave.Threshold && !City.Sun.Visible, "the way in through the mound takes you down the mine, out of the sun");
        await Frames(30);
        Check(p.GlobalPosition.Y > Cave.Landing.Y - 2, $"and the rock holds you up ({p.GlobalPosition.Y - Cave.Stage.Y:0.0} m from the adit)");
        Check(City.AreaAt(p.GlobalPosition) == "The Adit", $"this is {City.AreaAt(p.GlobalPosition)}");
        await Teleport(Cave.InRoom("timbering"));
        await Until(() => main.WorkingsNode.Shown.Any(s => s.Kind == "iron"), 10);
        var vein = main.WorkingsNode.Shown.FirstOrDefault(s => s.Kind == "iron");
        Check(vein is not null, $"the quarry's veins are down here now ({main.WorkingsNode.Shown.Count(s => !s.IsTree)} near)");
        if (vein is not null)
        {
            var room = Cave.InRoom(Greyhollow.Seams.First(s => s.Id == vein.Id).Room);
            await Teleport(vein.At + (room - vein.At).Normalized() * 2.2f);
            Check(main.WorkingsNode.Within(p.GlobalPosition) == vein, "you can reach the iron vein");
            var ore = pack.Count("iron_ore");
            main.WorkForTest(vein);
            await Until(() => !main.Working && pack.Count("iron_ore") > ore, 20);
            Check(pack.Count("iron_ore") > ore && pack.JobXp.GetValueOrDefault(Job.Mining) > 0, $"you mine it: {hud.LastLine}");
            Check(!main.WorkingsNode.Shown.Contains(vein), "and the vein's worked out for now");
            Check(main.MineLog.Worked == 1, "and Dagna counts it");
        }
        await TheOldFace();

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

        // A wyrmling is another matter: it won't look at a green hero, it weighs a seasoned one, and it remembers a refusal.
        var levelBefore = p.Hero.Level;
        p.Hero.Level = 3;
        var wyrm = Enemy.Create(Bestiary.Get("dragon_red"), p.GlobalPosition + new Vector3(3, 0, 0), young: true);
        main.AddEnemy(wyrm);
        await Frames(5);
        Check(!main.Tame(wyrm) && main.HudNode.LastLine.Contains("won't so much as look"), $"a {wyrm.Stats.Name} won't look at a level-3 hero");
        p.Hero.Level = 40;
        var tamed = false;
        var tries = 0;
        for (; tries < 14 && !tamed; tries++)
        {
            if (tries > 0)
            {
                Check(tries > 1 || !main.Tame(wyrm) && main.HudNode.LastLine.Contains("remembers you"), "one that turned you down remembers you");
                foreach (var d in main.Enemies().Where(e => e.IsDragon && e.State != EnemyState.Dead && e.Bond is null).ToList()) d.QueueFree(); // its screaming kin, and it
                await Frames(5);
                wyrm = Enemy.Create(Bestiary.Get("dragon_red"), p.GlobalPosition + new Vector3(3, 0, 0), young: true);
                main.AddEnemy(wyrm);
                await Frames(5);
            }
            p.Meat = 3;
            tamed = main.Tame(wyrm);
        }
        Check(tamed && main.Bonds.Any(b => b.Role == BondRole.Dragon), $"a seasoned ranger wins one over in the end ({tries} tries): {main.Bonds.FirstOrDefault(b => b.Role == BondRole.Dragon)?.Name}");
        p.Hero.Level = levelBefore;
        foreach (var b in main.Bonds.Where(b => b.Role == BondRole.Dragon).ToList()) main.ForgetForTest(b); // the egg's road comes next
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
        main.TerrainNode.EnsureAround(new Vector3((float)DragonSaga.RookeryX, 0, (float)DragonSaga.RookeryZ), 2); // the knights come once the hill is built
        main.WildsNode.ClearForTest("rookery"); // the story's demons may have had them already, and the dead don't regrow for ten minutes
        await Until(() => main.WildsNode.Creatures.Count(e => e.Rider) >= 2, 25);
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
        await Frames(10);
        knight.Distract(fallen); // they'd find each other anyway; this spares the wait on their wandering
        fallen.Distract(knight);
        await Until(() => knight.Stats.Health < knight.Stats.MaxHealth || fallen.Stats.Health < fallen.Stats.MaxHealth, 40);
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
        Check(main.WatchNode.Guards.Any(g => g.Rival == knife), $"a Velvet Knife comes for you, and the Watch comes running ({main.WatchNode.Guards.Count(g => g.Rival == knife)} watchmen; {main.WatchNode.Guards.Count()} at their posts, {main.WatchNode.Guards.Count(g => g.Allied)} on your side; you're {main.Standing.Name})");
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
        Check(found.Body is { Named: true } && found.Body.FightingCompany, found.Body is null
            ? $"found, {roamer.Title} comes for you (nothing out: still in the log {main.RaidLog.Roamers.Contains(roamer)} of {main.RaidLog.Roamers.Count}, hunting {roamer.Hunting}, ground at it {main.TerrainNode.HasGround(new Vector3((float)x, 0, (float)z))}, hero in town {main.RoamersNode.InTown(p.GlobalPosition)}, downed {p.Downed}, hero {p.GlobalPosition.X:0},{p.GlobalPosition.Z:0}, it {x:0},{z:0} in {main.World.AreaAt(x, z)}, height {main.World.Height(x, z):0.0}; lately: {main.HudNode.Recent(5)})"
            : $"found, {found.Roamer?.Title} comes for you (fighting {found.Body.FightingCompany}, named {found.Body.Named}, {found.Body.GlobalPosition.DistanceTo(p.GlobalPosition):0} m off)");

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

    /// <summary>Climbing: walking into a wall does nothing for a hero who can't; with gear from the shop they climb it, and over the top.</summary>
    async Task ClimbTest()
    {
        var p = main.PlayerNode;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        await Teleport(new Vector3(60, 0, 260));
        await Frames(10);
        var forward = -(p.GlobalTransform.Basis.Z with { Y = 0 }).Normalized();
        var foot = p.GlobalPosition;
        var ground = City.FloorAt(foot);
        // A wall of our own, six metres high, three ahead.
        var wall = new StaticBody3D();
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(12, 6, 3) } });
        wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(12, 6, 3) } });
        main.AddChild(wall);
        wall.GlobalPosition = foot with { Y = ground + 3 } + forward * 4.5f;
        wall.LookAt(wall.GlobalPosition + forward, Vector3.Up);
        await Frames(5);
        p.Pack.Take(Climbing.Gear);
        var by = p.ClimbsBy;
        Input.ActionPress("move_forward");
        if (by is null)
        {
            await Frames(120);
            Check(!p.Climbing && p.GlobalPosition.Y < ground + 1.5f, $"without climbing in them, a hero walks into a wall and stops ({p.GlobalPosition.Y - ground:0.0} m up)");
            p.Pack.Add(Climbing.Gear);
            by = p.ClimbsBy;
        }
        Check(by is not null, $"climbing gear lets anyone climb ({by})");
        await Until(() => p.Climbing, 3);
        Check(p.Climbing, "walking into the wall, they take hold of it");
        await Until(() => p.GlobalPosition.Y > ground + 3, 4);
        Check(p.GlobalPosition.Y > ground + 3, $"and climb it ({p.GlobalPosition.Y - ground:0.0} m up)");
        await Until(() => !p.Climbing, 6);
        Input.ActionRelease("move_forward"); // up and over: they stop pushing
        await Until(() => p.IsOnFloor(), 3);
        Input.ActionRelease("move_forward");
        Check(!p.Climbing && p.GlobalPosition.Y > ground + 5.5f, $"and over the top onto it ({p.GlobalPosition.Y - ground:0.0} m up, climbing {p.Climbing})");
        p.Pack.Take(Climbing.Gear);
        wall.QueueFree();
        await Teleport(foot);
    }

    /// <summary>
    /// The water's edge: nothing from the land swims out after you, and what lives in the sea hunts everything in it,
    /// swimmer or beast, and lets go of what gets out.
    /// </summary>
    async Task SeaHuntTest()
    {
        var (p, w) = (main.PlayerNode, main.World);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        var (dx, dz) = FindDeepWater(0, -1500);
        var deep = new Vector3((float)dx, 0, (float)dz);
        // Walk out of the water to the nearest shore.
        Vector3? shore = null;
        for (var r = 4f; r < 600 && shore is null; r += 2)
            for (var a = 0f; a < Mathf.Tau && shore is null; a += 0.1f)
            {
                var at = deep + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                if (w.Height(at.X, at.Z) > WorldGen.WaterLevel + 0.8) shore = at;
            }
        if (shore is not { } land) { Check(false, "a shore by deep water"); return; }
        var outward = (deep - land) with { Y = 0 };
        outward = outward.Normalized();
        // The hero a little way out, in water over their head; a bandit up the beach.
        var swim = land + outward * 2;
        for (var i = 0; i < 80 && WorldGen.WaterLevel - w.Height(swim.X, swim.Z) < 3; i++) swim += outward * 1.5f;
        await Teleport(swim);
        await Frames(30);
        var bandit = Enemy.Create(Bestiary.Get("raider"), (land - outward * 3) with { Y = City.FloorAt(land - outward * 3) + 0.5f }, Northwood.DangerFor(3));
        bandit.Roam = 30;
        main.AddEnemy(bandit);
        await Frames(5);
        bandit.Provoke(p); // it's after you
        var gap = bandit.GlobalPosition.DistanceTo(p.GlobalPosition);
        await Frames(360);
        Check(p.Swimming, $"the hero's swimming ({WorldGen.WaterLevel - w.Height(swim.X, swim.Z):0.0} m of water)");
        Check(bandit.State == EnemyState.Fighting && bandit.Depth < Fighter.SwimDepth && bandit.GlobalPosition.DistanceTo(p.GlobalPosition) > 3,
            $"a bandit chasing you stops at the water's edge ({gap:0} m off, then {bandit.GlobalPosition.DistanceTo(p.GlobalPosition):0} m, {bandit.Depth:0.0} m deep)");
        bandit.QueueFree();

        // A sea serpent in the deep water: it comes for the swimmer.
        var serpentAt = swim + outward * 22;
        var serpent = Enemy.Create(Bestiary.Get("sea_serpent"), serpentAt with { Y = (float)WorldGen.WaterLevel - 1 }, Northwood.DangerFor(3));
        main.AddEnemy(serpent);
        var start = serpent.GlobalPosition.DistanceTo(p.GlobalPosition);
        await Until(() => serpent.State == EnemyState.Fighting, 4);
        await Frames(90);
        Check(serpent.State == EnemyState.Fighting && serpent.GlobalPosition.DistanceTo(p.GlobalPosition) < start,
            $"a sea serpent hunts a swimmer ({start:0} m, then {serpent.GlobalPosition.DistanceTo(p.GlobalPosition):0} m)");

        // Out of the water and up the beach, the hero's let go; and it goes for whatever else is swimming.
        await Teleport(land - outward * 12);
        await Until(() => serpent.State != EnemyState.Fighting, 3);
        Check(serpent.State != EnemyState.Fighting, "out of the water, the serpent lets you go");
        var wolf = Enemy.Create(Bestiary.Get("wolf"), (serpent.GlobalPosition + outward * 6) with { Y = (float)WorldGen.WaterLevel - 1.2f }, Northwood.DangerFor(3));
        main.AddEnemy(wolf);
        var full = wolf.Stats.Health;
        await Until(() => wolf.Stats.Health < full || wolf.State == EnemyState.Dead, 6);
        Check(wolf.Stats.Health < full || wolf.State == EnemyState.Dead, "and goes after a wolf swimming in its water: it hunts everything in the sea");
        foreach (var e in new[] { serpent, wolf }) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
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




    /// <summary>
    /// The Watch career (beats walked post to post, a footpad collared, the captain's pay and promotion), a clan's
    /// offer at its camp and a raid ridden with it, the price on an outlaw's head and the Watch that stops them, the
    /// smuggler who makes it go away, and a town that knows a hero when it sees one.
    /// </summary>
    async Task StandingTest()
    {
        main.CaughtSure = true; // every crime seen, so the prices and the dismissals the test looks for are sure
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var s = main.Standing;
        var raiders = main.RaidersNode;
        var book = main.GuildBook;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        var captainSpot = Main.WatchHouseAt + new Vector3(1.5f, 0, 1.5f);

        // The Watch House: Captain Vale takes you on.
        await Teleport(captainSpot);
        main.Talk(Who(Main.WatchCaptain));
        Check(hud.Choose("I want to join the Watch") && s.Watch.Rank == WatchRank.Recruit, "Captain Harlan Vale takes you on as a recruit of the Watch");
        hud.EndTalk();

        // A beat walked, post to post, with a footpad to collar on the way.
        async Task WalkBeat()
        {
            await Teleport(captainSpot);
            main.Talk(Who(Main.WatchCaptain));
            Check(hud.Choose("Give me a beat") && s.Watch.Patrol is { } b && b.Posts.Count == s.Watch.BeatLength,
                $"a beat to walk: {string.Join(", ", s.Watch.Patrol?.Posts ?? [])}{(s.Watch.Patrol?.TroubleAt >= 0 ? $" (trouble waits at {s.Watch.Patrol.Posts[s.Watch.Patrol.TroubleAt]})" : " (a quiet one)")}");
            hud.EndTalk();
            await Until(() => hud.Tracked.Any(t => t.StartsWith("The Watch: walk to")), 3);
            Check(hud.Tracked.Any(t => t.StartsWith("The Watch: walk to")), "the tracker shows the next post");
            var beat = s.Watch.Patrol!;
            while (!beat.Walked)
            {
                var post = beat.NextPost!;
                var next = beat.Next;
                await Teleport(TownWatch.PostAt(post)!.Value + new Vector3(1, 0, 1));
                await Until(() => beat.Next > next || main.Footpad is not null, 4);
                if (main.Footpad is { } t)
                {
                    Check(t.Criminal && hud.Heard("A footpad at"), $"a footpad at {post} comes at you");
                    var arrests = s.Watch.Arrests;
                    t.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
                    await Until(() => main.Footpad is null && beat.TroubleMet, 4);
                    Check(beat.TroubleMet && s.Watch.Arrests > arrests, "collared: an arrest to your name");
                    await Until(() => beat.Next > next, 4);
                }
                Check(beat.Next > next, $"{post}: walked");
            }
            var purse = p.Crowns;
            await Teleport(captainSpot);
            main.Talk(Who(Main.WatchCaptain));
            Check(hud.Choose("Reporting back") && s.Watch.Patrol is null && p.Crowns > purse, $"the beat reported to the captain: +{p.Crowns - purse} crowns ({s.Watch.Patrols} walked, {s.Watch.Arrests} arrests)");
            hud.EndTalk();
        }
        await WalkBeat();
        s.Watch.Patrols = WatchBook.ToWatchman - 1;
        await WalkBeat();
        Check(s.Watch.Rank == WatchRank.Watchman && hud.Heard("makes you a watchman of Tristinwold"), "three beats walked, and Captain Vale makes you a watchman");
        Check(s.Honour > 0 && s.Repute >= Repute.Unknown, $"honour {s.Honour}: the town knows you as {s.Name}");

        // The town knows a hero when it sees one.
        s.Honour = 130;
        var osric = Who("tristinwold/shipwright");
        main.Talk(osric);
        Check(hud.TalkText.StartsWith("\"") && hud.TalkText.EndsWith(osric.Npc.Greeting) && hud.TalkText.Length > osric.Npc.Greeting.Length, $"a hero of Tristinwold: {hud.TalkText[..Math.Min(70, hud.TalkText.Length)]}...");
        hud.EndTalk();
        Check(s.PriceFactor < 1, "and the shops knock a little off");

        // A clan's camp, a raider beaten, and the warchief's offer.
        if (raiders.NextCamp(p.GlobalPosition) is not { } camp) { Check(false, "a clan camped somewhere"); return; }
        book.PlaySeconds = camp.At;
        await Teleport(new Vector3((float)camp.X + 12, 0, (float)camp.Z + 12));
        await Until(() => raiders.Out.Any(b => b.W.Id == camp.W.Id), 6);
        var band = raiders.Out.FirstOrDefault(b => b.W.Id == camp.W.Id);
        Check(band.Raiding is null && band.Bodies?.Count == camp.W.Raiders && hud.Heard("camped between raids"), $"{camp.W.Name} under {camp.W.Chief}, camped on the road: {band.Bodies?.Count} raiders");
        band.Bodies![1].TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => hud.Talking, 4);
        Check(hud.Talking && hud.TalkText.Contains("Ride with us"), $"you beat one of them, and {camp.W.Chief} calls a halt: \"Ride with us.\"");
        Check(hud.Choose("I'll ride with you") && s.Clan.Rank == ClanRank.Spearman && s.Clan.ClanId == camp.W.Id, $"you're a spear of {camp.W.Name}");
        Check(s.Watch.Rank != WatchRank.None && s.Infamy > 0 && hud.Heard("doesn't know yet"), "the Watch doesn't know yet (a double life), though the city hears you've sworn to barbarians");
        await Frames(5);
        var kin = band.Bodies.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).ToList();
        Check(kin.Count == camp.W.Raiders - 1 && kin.All(e => e.Kin && e.State != EnemyState.Fighting), "the clan is kin now: spears lowered");
        if (raiders.Chief is not { } chief) { Check(false, "its warchief stands among them"); return; }
        await Teleport(chief.GlobalPosition + new Vector3(2, 0, 0));
        var act = main.ClanActionForTest(p.GlobalPosition);
        Check(act?.Verb == $"speak with {camp.W.Chief}", $"by the warchief: E to {act?.Verb}");
        act?.Act();
        Check(hud.Choose("How do I stand") && hud.TalkText.Contains("spear of"), $"\"{hud.TalkText[..Math.Min(60, hud.TalkText.Length)]}...\"");
        hud.EndTalk();

        // Riding with the clan on a place.
        var round = (int)Math.Floor(book.PlaySeconds / Warband.Round);
        var start = (round + 1) * Warband.Round - Warband.RaidSeconds + 5;
        var place = camp.W.Where(main.World, start).Raiding!;
        await Teleport(new Vector3((float)place.X + 10, 0, (float)place.Z + 10));
        await Frames(60);
        book.PlaySeconds = start;
        await Until(() => raiders.Mine is { Raiding: not null }, 8);
        Check(raiders.Mine is { Raiding: { } r } && r.Id == place.Id && hud.Heard("Ride with them"), $"your clan falls on {place.Name}, {place.What}: ride with them");
        var guards = main.PlacesNode.GuardsOf(place.Id).ToList();
        Check(guards.Count > 0 && guards.All(g => !g.Allied), $"its {guards.Count} guard{(guards.Count == 1 ? "" : "s")} turn on you too");
        var (purse2, bounty, infamy) = (p.Crowns, s.Bounty, s.Infamy);
        foreach (var g in guards) g.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => s.Clan.Raids == 1, 6);
        Check(s.Clan.Raids == 1 && p.Crowns > purse2 && s.Bounty == bounty + Standing.RaidBounty && s.Infamy > infamy,
            $"the place is plundered: your share {p.Crowns - purse2} crowns, {Standing.RaidBounty} crowns on your head in Tristinwold, infamy {s.Infamy}");
        await Until(() => hud.Tracked.Any(t => t.StartsWith("Outlaw:")), 3);
        Check(hud.Tracked.Any(t => t.StartsWith("Outlaw:")), "the tracker says you're an outlaw");

        // Home, an outlaw: the Watch stops you at sight.
        await Teleport(new Vector3(88, 0, 5));
        await Until(() => hud.Heard("Halt, in the name of the Watch"), 6);
        var gate = main.WatchNode.Guards.MinBy(g => g.GlobalPosition.DistanceTo(p.GlobalPosition))!;
        Check(hud.Heard("Halt, in the name of the Watch") && !gate.Allied, "\"Halt, in the name of the Watch!\": the gate's watchman turns on you");
        await Until(() => gate.FightingCompany, 6);
        Check(gate.FightingCompany, "and comes for you");
        var owed = s.Bounty;
        p.Crowns = Math.Max(p.Crowns, 500);
        await Teleport(Main.Smugglers + new Vector3(1, 0, 1));
        main.Talk(Who("tristinwold/smuggler"));
        var purse3 = p.Crowns;
        Check(hud.Choose("Make the Watch forget") && !s.Outlaw && purse3 - p.Crowns == (int)Math.Ceiling(owed * (1 + Standing.SmugglersCut)), $"Nell Corran makes the Watch forget your face, for {purse3 - p.Crowns} crowns");
        hud.EndTalk();
        await Frames(5);
        Check(main.WatchNode.Guards.All(g => g.Allied), "the Watch stands down");
        Check(s.WhyNotWatch is null && !s.Watch.In, "struck off when you were seen, but with the price paid the Watch would have you back, clansman or not: a double life lasts until you're caught");

        // The guilds' stories have a good choice and a wicked one, and the town gossips about what you've done.
        Check(Guilds.Drama.All(d => d.Choices.Any(c => c.Good) && d.Choices.Any(c => c.Evil)), $"every guild story ({Guilds.Drama.Count}) has a good choice and a wicked one");
        var gossip = false;
        for (var i = 0; i < 16 && !gossip; i++)
        {
            main.Talk(Who(Main.WatchCaptain));
            hud.Choose("Heard any news");
            gossip = hud.TalkText.StartsWith("Only that you");
            hud.EndTalk();
        }
        Check(gossip, $"ask for news and you hear about yourself: {hud.TalkText}");

        // And it's all kept with the company.
        var saved = CompanySave.FromJson(main.Save()).Standing;
        Check(saved is { } sv && sv.Clan.Raids == 1 && sv.Watch.Dismissals == 1 && sv.Honour == s.Honour && sv.Deeds.Count == s.Deeds.Count, "your name, your clan and your service are saved");
    }

    /// <summary>
    /// The fifth calling: the Foundry and its golem (built, not born; levels with its maker; stands down when they
    /// leave), the alchemist's hazards and market transmutation, the ether-aeronaut's harpoon, anchor, merge and ship
    /// modules, the saboteur's wrench, caltrops, trap, black-market scrapping, and a tracker on a wagon sprung into an
    /// ambush; and a trade perk on every class's sheet.
    /// </summary>
    async Task ArtificerTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var s = main.Standing;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        await Teleport(new Vector3(-150, 0, -120));
        Bond? Golem() => main.Bonds.FirstOrDefault(b => b.Role == BondRole.Golem);
        Enemy? GolemBody() => main.CompanionBodies.FirstOrDefault(e => e.Bond?.Role == BondRole.Golem && e.State != EnemyState.Dead);

        // The Foundry, and a golem that follows an artificer out of it.
        var g = Guilds.Of(Calling.Artificer);
        Check(main.Locals.Any(x => x.Npc.Id == g.Leader.Id) && g.Teachers.Count == 3, $"{g.Name} is in its yard: {g.Leader.Name} and three teachers");
        p.Join("Alchemist");
        await Until(() => GolemBody() is not null, 6);
        var golem = GolemBody();
        Check(golem is { Bond: { Name: "Alembic", Eats: false } } && golem.Leader == p, $"an alchemist's golem, {golem?.Bond?.Name}, stamps after you (level {golem?.Bond?.Level}): not a pet, never fed");
        Check(golem?.Figure is Clockwork { Design: "Alembic" }, "and it's the Alchemist's own design: a retort of brass and glass, a reagent glowing in its chest");
        var level = p.Hero.Level;
        main.GainXp(Hero.XpToNext(level) * 2);
        await Until(() => Golem()?.Level == p.Hero.Level && GolemBody() is not null, 6);
        Check(p.Hero.Level > level && Golem()?.Level == p.Hero.Level, $"it levels with you: level {p.Hero.Level}, both");

        // Vials and a hazard on the ground.
        var thug = Spawn("dock_thug", p.GlobalPosition - p.GlobalTransform.Basis.Z * 5);
        await Frames(3);
        p.Face(thug.GlobalPosition);
        p.Stats.Restore(1e6, 1e6);
        Check(p.Use(p.Class.Signature), "a hazard flask flung at a thug");
        await Until(() => thug.Stats.Health < thug.Stats.MaxHealth * 0.9 || thug.State == EnemyState.Dead, 6);
        Check(thug.Stats.Health < thug.Stats.MaxHealth * 0.9 || thug.State == EnemyState.Dead, $"the pool burns him as he stands in it ({thug.Stats.Health:0}/{thug.Stats.MaxHealth:0})");
        thug.QueueFree();

        // Market transmutation, through Garrick Wain, for a town the city trades with.
        var town = main.World.SettlementsNear(0, 0, 5000).Where(x => x.Kind == SettlementKind.Town).OrderBy(x => x.DistanceTo(0, 0)).First();
        var market = main.TradeLog.Market(town, main.Economy.Day);
        var herb = Items.All.First(i => i.Kind == ItemKind.Herb).Id;
        p.Pack.Add(herb, 3);
        var good = market.Makes.First();
        market.Stock[good] = 0.5;
        var dear = market.Asks(good);
        await Teleport(Main.YardAt + new Vector3(2, 0, 2));
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose($"Steady {town.Name}") && market.Steadied == main.Economy.Day && market.Asks(good) < dear && !p.Pack.Contents.Any(x => x.Item.Id == herb),
            $"three herbs to Garrick Wain, and {town.Name}'s market steadies: {CityEconomy.Info(good).Name} from {dear} to {market.Asks(good)}");
        hud.EndTalk();

        // The ether-aeronaut: a harpoon that drags, an anchor that pins, the golem worn as armour.
        p.Join("EtherAeronaut");
        await Until(() => Golem()?.Level == p.Hero.Level + 2 && GolemBody() is not null, 6);
        Check(Golem()?.Level == p.Hero.Level + 2, "a tinker's golem is sturdier: two levels up on you");
        Check(Golem()?.Build == Bond.AeronautBuild && GolemBody()?.Figure.Scale.X > 1.3f, $"and built bigger than the others, a hull with room inside (x{GolemBody()?.Figure.Scale.X:0.00})");
        Check(GolemBody()?.Figure is Clockwork { Design: "Ballast" }, "the Aeronaut's own design: a riveted iron hull with a furnace core and chimneys");
        await Teleport(new Vector3(-150, 0, -120));
        thug = Spawn("dock_thug", p.GlobalPosition - p.GlobalTransform.Basis.Z * 11);
        await Frames(3);
        p.Face(thug.GlobalPosition);
        p.Stats.Restore(1e6, 1e6);
        Check(p.Use(p.Class.Primary), "a harpoon at a thug 11 m off");
        await Until(() => thug.GlobalPosition.DistanceTo(p.GlobalPosition) < 4, 3);
        Check(thug.GlobalPosition.DistanceTo(p.GlobalPosition) < 4, $"and he's dragged to you ({thug.GlobalPosition.DistanceTo(p.GlobalPosition):0.0} m)");
        Check(p.Use(p.Class.Signature) && thug.Rooted > 0, $"an anchor shot pins him ({thug.Rooted:0} s)");
        thug.QueueFree();
        var body = GolemBody()!;
        Check(p.Use(p.Class.Utility) && !body.Visible && p.Stats.Statuses.Contains(AutoStatus.Protect), "you climb inside the golem: it's gone from the field, and you wear its plate (Protect, Shell)");
        await Frames(2);
        Check(p.Possessing && p.Figure is Clockwork { Design: "Ballast" }, "and you drive the machine: the golem's body is yours for the while, at its full size");
        var biter = Enemy.Create(Bestiary.Get("wolf"), p.GlobalPosition + new Vector3(3, 0, 0));
        main.AddEnemy(biter);
        biter.Carried = true; // it stands still: the test does the biting
        await Frames(2);
        var (hero, hull) = (p.Stats.Health, body.Stats.Health);
        for (var i = 0; i < 12 && body.Stats.Health >= hull; i++) p.TakeHit(40, DamageKind.Physical, AttackKind.Weapon, false, biter);
        Check(p.Stats.Health == hero && body.Stats.Health < hull, $"a blow lands on the machine and not on you: you {hero:0} to {p.Stats.Health:0}, its hull {hull:0} to {body.Stats.Health:0}");
        Check(hud.Tracked.Any(t => t.Contains("hull")) || main.WornGolem is not null, "and its hull shows while you're inside");
        await Until(() => body.Visible, 20);
        Check(body.Visible && !p.Possessing && p.Hull is null, "and it steps back out beside you after");
        body.Stats.Heal(1e9);
        p.Stats.Restore(1e9, 1e9);
        await Frames(10);
        p.CoolForTest();
        Check(p.Use(p.Class.Utility) && p.Possessing, "back inside the machine");
        await Frames(2);
        for (var i = 0; i < 60 && p.Possessing; i++) p.TakeHit(1e7, DamageKind.Physical, AttackKind.Weapon, false, biter);
        await Frames(4);
        Check(!p.Possessing && body.State == EnemyState.Dead && body.Visible && p.Stats.Health == hero, "beaten to scrap, it throws you clear, unhurt, and lies where it fell");
        Check(hud.Heard("throw you clear"), "and it says so");
        biter.QueueFree();
        body.Revive();
        await Teleport(Main.ShipyardAt + new Vector3(0, 0, 1));
        p.Crowns = Math.Max(p.Crowns, 600);
        main.Talk(Who("tristinwold/shipwright"));
        Check(hud.Choose("Fit light sails") && main.Sea.HasModule("light_sails") && main.Sea.Pace > 1, "Osric fits your light sails: a quarter off every leg");
        hud.EndTalk();

        // The saboteur: a wrench, caltrops, a trap; parts for Nell; a tracker and an ambush.
        p.Join("Saboteur");
        await Until(() => Golem()?.Level == p.Hero.Level && GolemBody() is not null, 6);
        await Teleport(new Vector3(-150, 0, -120));
        thug = Spawn("dock_thug", p.GlobalPosition - p.GlobalTransform.Basis.Z * 2);
        await Frames(3);
        p.Face(thug.GlobalPosition);
        for (var i = 0; i < 4; i++) { p.Stats.Restore(1e6, 1e6); p.Use(p.Class.Primary); await Frames(35); }
        Check(thug.Stunned > 0 || thug.State == EnemyState.Dead, "four blows of the wrench, and the fourth rings his head");
        p.Stats.Restore(1e6, 1e6);
        Check(p.Use(p.Class.Signature) && (thug.Hobbled > 0 || thug.State == EnemyState.Dead), $"caltrops hobble him ({thug.Hobbled:0} s)");
        thug.QueueFree();
        p.Stats.Restore(1e6, 1e6);
        Check(p.Use(p.Class.Utility), "a trap set at your feet");
        var walker = Spawn("giant_rat", p.GlobalPosition + new Vector3(6, 0, 0));
        await Frames(3);
        walker.GlobalPosition = p.GlobalPosition + new Vector3(0.8f, 0.3f, 0);
        await Until(() => walker.Rooted > 0 || walker.State == EnemyState.Dead, 4);
        Check(walker.Rooted > 0 || walker.State == EnemyState.Dead, "a rat steps on it: bitten and held");
        walker.QueueFree();
        p.Pack.Add("iron_sword", 2);
        await Teleport(Main.Smugglers + new Vector3(1, 0, 1));
        var purse = p.Crowns;
        main.Talk(Who("tristinwold/smuggler"));
        var (scrapHad, nailsHad) = (p.Pack.Count("scrap_iron"), p.Pack.Count("iron_nails"));
        Check(hud.Choose("Break my spare gear") && p.Crowns > purse && !p.Pack.Contents.Any(x => x.Item.Id == "iron_sword"), $"Nell Corran buys two swords as parts: +{p.Crowns - purse} crowns");
        Check(p.Pack.Count("scrap_iron") > scrapHad && p.Pack.Count("iron_nails") > nailsHad, $"and the iron's yours to keep: {p.Pack.Count("scrap_iron") - scrapHad} scrap iron, {p.Pack.Count("iron_nails") - nailsHad} nails");
        hud.EndTalk();
        await Teleport(Main.ParkedWagonAt + new Vector3(2, 0, 2));
        var act = main.ArtificerActionForTest(p.GlobalPosition);
        Check(act?.Verb.StartsWith("plant a tracker") == true, $"by a parked wagon at the yard: E to {act?.Verb}");
        act?.Act();
        var t = s.Tracker;
        Check(t is not null && hud.Heard("A tracker under the wagon"), $"the tracker ticks: bound for {t?.TownName}");
        if (t is null) return;
        t.PlantedAt -= t.Length / 2 / WagonTracker.Pace; // half-way up the road already
        var (x, z) = t.Where(main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 3, 0, (float)z + 3));
        await Until(() => hud.Tracked.Any(l => l.StartsWith("Tracker:")), 3);
        Check(hud.Tracked.Any(l => l.StartsWith("Tracker:")), "the tracker shows where the wagon is on the road");
        act = main.ArtificerActionForTest(p.GlobalPosition);
        Check(act?.Verb.StartsWith("spring the ambush") == true, $"meet it there: E to {act?.Verb}");
        act?.Act();
        await Frames(5);
        var guards = main.Enemies().Where(e => e.Stats.Name == "Caravan guard").ToList();
        Check(guards.Count == 2 && t.Sprung, "the wagon pulls up and two guards come off the box");
        var (purse2, infamy, bounty) = (p.Crowns, s.Infamy, s.Bounty);
        main.CaughtSure = true; // the driver's seen 60% of the time; the price on your head is what's being checked
        foreach (var e in guards) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => s.Tracker is null, 6);
        main.CaughtSure = false;
        Check(s.Tracker is null && p.Crowns > purse2 && s.Infamy > infamy && s.Bounty == bounty + Standing.RaidBounty,
            $"the guards down, the load is yours: +{p.Crowns - purse2} crowns, infamy {s.Infamy}, bounty {bounty} to {s.Bounty}, tracker {s.Tracker is null}");
        s.Bounty = 0;
        main.WatchNode.Hostile = false;

        // Every class has its trade, on the sheet; and a golem follows only an artificer.
        Check(Classes.All.All(d => Perks.Of(d.Id).Name != "None"), $"every one of the {Classes.All.Count} classes has a trade perk");
        p.Join("Paladin");
        await Frames(240);
        Check(Golem() is not null, "take up another calling, and the golem still walks with you: once an artificer, always its maker");
    }

    /// <summary>
    /// The wrong side of every honest job: the Tongs' thug and his round of the shops (and the Watch sent for when one
    /// won't pay), the poacher's pelts for Nell, the smuggler's crate to a port, the highwayman's tip and ambush; the
    /// Watch won't have a Tong.
    /// </summary>
    async Task ShadowsTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var s = main.Standing;
        var sh = s.Shadows;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        p.Join("Paladin");
        await Frames(5);

        // The Tongs' man, a round of three shops, and the due collected.
        await Teleport(Main.TongCorner + new Vector3(1, 0, 1));
        main.Talk(Who(Main.TongMan));
        Check(hud.Choose("I want to work for the Tongs") && sh.In(ShadowLine.Thug), "Corvin Rask of the Ravenscrew Tongs takes you on");
        hud.EndTalk();
        main.Talk(Who(Main.TongMan));
        Check(hud.Choose("Give me a round") && sh.Round.Count == ShadowBook.RoundShops, $"a round to collect: {string.Join(", ", sh.Round)}");
        hud.EndTalk();
        await Until(() => hud.Tracked.Any(t => t.StartsWith("The Tongs: collect")), 3);
        Check(hud.Tracked.Any(t => t.StartsWith("The Tongs: collect")), "the tracker shows who's still owed");
        main.ShadowsSure = true;
        var purse = p.Crowns;
        foreach (var shop in sh.Round.ToList())
        {
            main.Talk(Who(shop));
            Check(hud.Choose("The Tongs' due") && !sh.Owes(shop), $"{Shops.All.First(x => x.Id == shop).Name} pays the Tongs' due");
            hud.EndTalk();
        }
        Check(sh.RoundDone && p.Crowns > purse && sh.For(ShadowLine.Thug).Done == 3, $"the round's done: +{p.Crowns - purse} crowns, infamy {s.Infamy}");
        main.ShadowsSure = false;
        Check(s.WhyNotWatch is null, "and the Watch would still have you: nobody's caught you at it yet");
        // One who won't pay: the Watch is sent for.
        var bounty = s.Bounty;
        sh.StartRound([.. Shops.All.Where(x => main.Locals.Any(l => l.Npc.Id == x.Id)).Select(x => x.Id)], new Tristinwold.Deviant.Rng(3));
        var refused = false;
        for (var i = 0; i < 12 && !refused; i++)
        {
            sh.Collected.Clear();
            main.Talk(Who(sh.Round[0]));
            hud.Choose("The Tongs' due");
            refused = s.Bounty > bounty;
            hud.EndTalk();
        }
        Check(refused && hud.Heard("won't pay, and the Watch is sent for"), $"a shopkeeper who won't pay sends for the Watch: {s.Bounty} crowns on your head");
        sh.For(ShadowLine.Thug).Done = 3; // those who paid before one refused don't count toward the ranks below
        s.Bounty = 0;
        main.WatchNode.Hostile = false;

        // The poacher: a farm's sheep is a pelt for Nell, not a fine.
        await Teleport(Main.Smugglers + new Vector3(1, 0, 1));
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("What would the farms' animals fetch") && sh.In(ShadowLine.Poacher), "Nell Corran buys poached animals by the head");
        hud.EndTalk();
        var farm = main.World.SettlementsNear(0, 0, 5000).Where(x => x.Kind == SettlementKind.Farmstead).MinBy(x => x.DistanceTo(0, 0))!;
        await Teleport(new Vector3((float)(farm.X + farm.Radius + 6), 0, (float)farm.Z));
        await Until(() => main.PlacesNode.IsBuilt(farm.Id) && main.Enemies().Any(e => e.Owner == farm.Name && e.State != EnemyState.Dead), 10);
        var beast = main.Enemies().First(e => e.Owner == farm.Name && e.State != EnemyState.Dead);
        purse = p.Crowns;
        beast.TakeHit(1e6, DamageKind.Physical, AttackKind.Weapon, false, p);
        await Until(() => sh.Pelts == 1, 5);
        Check(sh.Pelts == 1 && p.Crowns >= purse, $"a farm's {beast.Kind.Name.ToLowerInvariant()} killed: a pelt for Nell, and no fine");
        await Teleport(Main.Smugglers + new Vector3(1, 0, 1));
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("Here's what I took") && p.Crowns == purse + ShadowBook.PeltPrice && sh.Pelts == 0, $"Nell pays {ShadowBook.PeltPrice} a head");
        hud.EndTalk();

        // The smuggler: a crate for a port, delivered at the rail.
        if (main.Sea.Rank == SeaRank.Landsman) main.Sea.SignOn();
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("Anything that needs carrying") && sh.In(ShadowLine.Smuggler), "Nell has crates for the ports");
        hud.EndTalk();
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("A crate to run") && sh.CrateFor is not null, $"a crate for {Seas.Get(sh.CrateFor!)?.Name}");
        hud.EndTalk();
        main.SailForTest(sh.CrateFor!);
        await Frames(30);
        var v = main.Sea.Voyage;
        if (main.AtSeaNode is null || v is null) { Check(false, "a voyage with the crate aboard"); return; }
        (v.Out, v.Home) = ([], []);
        main.AtSeaNode.Along = 0.999;
        await Until(() => v.InPort && main.AtSeaNode.Agent is not null, 8);
        purse = p.Crowns;
        main.Talk(main.AtSeaNode.Agent!);
        Check(hud.Choose("Nell Corran's crate") && sh.CrateFor is null && p.Crowns == purse + ShadowBook.CratePay, $"her man takes it at the rail: +{ShadowBook.CratePay} crowns");
        Check(hud.Choose("Cast off for home"), "and home");
        main.AtSeaNode.Along = 0.999;
        await Until(() => main.Sea.Voyage is null, 8);
        await Frames(20);

        // The highwayman: a tip bought, a wagon waylaid.
        await Teleport(Main.Smugglers + new Vector3(1, 0, 1));
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("Heard of a wagon worth waylaying") && sh.In(ShadowLine.Highwayman), "Nell sells tips on wagons");
        hud.EndTalk();
        p.Crowns = Math.Max(p.Crowns, 100);
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("A wagon worth waylaying") && s.Tracker is not null, $"a tip bought: a wagon for {s.Tracker?.TownName}");
        hud.EndTalk();
        var t = s.Tracker!;
        t.PlantedAt -= t.Length / 2 / WagonTracker.Pace;
        var (x, z) = t.Where(main.GuildBook.PlaySeconds);
        await Teleport(new Vector3((float)x + 3, 0, (float)z + 3));
        var act = main.ArtificerActionForTest(p.GlobalPosition);
        Check(act?.Verb.StartsWith("spring the ambush") == true, $"a paladin gone to the bad, on the road: E to {act?.Verb}");
        act?.Act();
        await Frames(5);
        var guards = main.Enemies().Where(e => e.Stats.Name == "Caravan guard").ToList();
        purse = p.Crowns;
        foreach (var e in guards) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => s.Tracker is null, 6);
        Check(s.Tracker is null && p.Crowns > purse, $"the wagon's yours: +{p.Crowns - purse} crowns; you're {s.Name}");
        s.Bounty = 0;
        main.WatchNode.Hostile = false;
        Check(sh.TitleOf(ShadowLine.Thug) == "a Tong enforcer" && ShadowBook.Title(ShadowLine.Thug, 0) == "a Tong knuckle" && ShadowBook.Title(ShadowLine.Thug, 3) == "a Tong captain", $"the Tongs' ranks run from knuckle to captain: three doors in, you're {sh.TitleOf(ShadowLine.Thug)}");
    }

    /// <summary>Over the rise, two bands at it: each has its soldiers, and they go for each other, not for the hero.</summary>
    async Task TheFeud()
    {
        var f = main.FeudsNode;
        await Teleport(new Vector3(-150, 0, -130));
        await Frames(60);
        var begun = false;
        for (var i = 0; i < 8 && !begun; i++)
        {
            begun = f.Begin();
            if (!begun) main.PlayerNode.RotateY(0.8f);
        }
        Check(begun, $"somewhere to have it, from {main.PlayerNode.GlobalPosition}");
        await Frames(120);
        Check(f.Fighters.Count >= 4 && f.Fighters.Count(e => e.Named) == 2, $"a feud: {f.Fighters.Count} fighters, two of them named");
        var fighting = f.Fighters.Count(e => e.Rival is not null);
        Check(fighting >= 2, $"and they're at each other: {fighting} locked in a fight");
        var hurt = 0;
        for (var i = 0; i < 40 && hurt == 0; i++)
        {
            await Frames(30);
            hurt = f.Fighters.Count(e => !IsInstanceValid(e) || e.Stats.Health < e.Stats.MaxHealth);
        }
        Check(hurt > 0, $"with real blows: {hurt} hurt");
    }

    /// <summary>A tavern: the bar, the card and dice tables, and a Bard's set, with the takings right.</summary>
    async Task TheTavern()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        p.Crowns = 500;
        var room = main.EnterTavernForTest();
        await Frames(40);
        await Teleport(room.Bar);
        await Frames(10);
        var brute = Enemy.Create(Bestiary.Get("wolf"), p.GlobalPosition + new Vector3(1, 0, 0));
        var hp = p.Stats.Health;
        p.TakeHit(40, Tristinwold.Deviant.DamageKind.Physical, AttackKind.Weapon, true, brute);
        Check(p.Safe && p.Stats.Health == hp, "in a tavern nobody can hurt you");
        brute.Free();
        Check(main.ActionForTest()?.Verb == "order at the bar", $"at the bar: E to {main.ActionForTest()?.Verb}");
        main.ActionForTest()?.Act();
        Check(hud.TalkTitle == "The bar" && hud.Choose("Honey Mead") && p.Pack.Contents.Any(c => c.Item.Id == "mead") && p.Crowns < 500, $"a mead, bought: {p.Crowns} crowns left");
        hud.EndTalk();
        await Teleport(room.CardTable + new Vector3(1.2f, 0, 0));
        await Frames(10);
        Check(main.ActionForTest()?.Verb.Contains("Twenty-One") == true, $"at the card table: E to {main.ActionForTest()?.Verb}");
        main.ActionForTest()?.Act();
        var before = p.Crowns;
        Check(hud.Choose("Play for 25"), "a stake of 25");
        for (var i = 0; i < 12 && main.TavernHand?.Result == TableResult.Playing; i++) hud.Choose("I'll stand");
        var h = main.TavernHand!;
        Check(h.Result != TableResult.Playing && p.Crowns == before - h.Stake + h.Returned && hud.TalkText.Contains("The house:"), $"a hand played out, {h.Result}: {before} to {p.Crowns}");
        hud.EndTalk();
        await Teleport(room.DiceTable + new Vector3(1.2f, 0, 0));
        await Frames(10);
        main.ActionForTest()?.Act();
        before = p.Crowns;
        Check(hud.Choose("Play for 5") && hud.TalkText.Contains("dice stop") && (p.Crowns == before - 5 || p.Crowns == before + 20), $"a throw of the dice: {hud.TalkText}");
        hud.EndTalk();
        await Teleport(room.Fireside + new Vector3(0, 0, 1));
        await Frames(10);
        Check(main.ActionForTest()?.Verb != "play a set by the fire", "only a Bard plays for tips");
    }

    /// <summary>A caravan as driver: up on the box, through your own eyes, the pace in your hands; an ambush, and down to fight.</summary>
    async Task TheRide()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        main.StartCaravanForTest(driver: true);
        await Frames(20);
        var w = main.GuardedWagon!;
        await Teleport(w.GlobalPosition + new Vector3(3, 0, 0));
        await Frames(10);
        Check(main.ActionForTest()?.Verb.Contains("reins") == true, $"by the wagon: E to {main.ActionForTest()?.Verb}");
        main.ClimbForTest();
        await Frames(10);
        Check(p.Seat is not null && !p.Figure.Visible && main.RiddenWagon == w, "up on the box, seeing the road through your own eyes");
        Check(w.Path is { } way && way.Line[0] == Highways.Yard && way.Length > 100, $"the carter has her way by road from the yard ({w.Path?.Length:0} m, {w.Path?.Line.Length} points)");
        var start = w.GlobalPosition;
        await Frames(180);
        Check(w.GlobalPosition.DistanceTo(start) > 3 && p.GlobalPosition.DistanceTo(w.SeatAt(true)) < 1.5f, $"the wagon rolls, and you with it: {w.GlobalPosition.DistanceTo(start):0.0}m");
        // The reins are yours: haul her right and she goes right, off the carter's line; let go, and she finds her road again.
        var yaw = w.Rotation.Y;
        Input.ActionPress("move_right");
        await Frames(90);
        Input.ActionRelease("move_right");
        var turned = Mathf.AngleDifference(yaw, w.Rotation.Y);
        Check(turned < -0.4f, $"haul right and she turns right, off the carter's line ({turned:0.00} rad)");
        await Frames(420);
        Check(Math.Abs(Mathf.AngleDifference(yaw, w.Rotation.Y)) < 0.6f, $"let go, and she finds her road again ({Mathf.AngleDifference(yaw, w.Rotation.Y):0.00} rad off her old line)");
        Check(main.ActionForTest()?.Verb == "jump down", "and E gets you down");
        Check(main.MayStealForTest, "a nobody on the box could keep the yard's wagon");
        main.ClimbDownForTest();
        await Frames(5);
        Check(p.Seat is null && p.Figure.Visible && p.GlobalPosition.DistanceTo(w.GlobalPosition) < 6, "down beside the wagon, ready to fight");
        // Back up, and off with it: the yard's wagon, horse and load are yours, and Tristinwold wants you for it.
        await Teleport(w.GlobalPosition + new Vector3(3, 0, 0));
        main.ClimbForTest();
        await Frames(5);
        var (bounty, bonds, trade) = (main.Standing.Bounty, main.Bonds.Count, main.TradeLog);
        main.StealForTest();
        Check(trade is { Guarding: null, OwnWagon: true, WagonOut: true, Stolen: 1 } && trade.Loaded > 0 && main.WagonNode == w && main.GuardedWagon is null && main.RiddenWagon == w,
            $"you keep the wagon, still up on its box: {trade.Loaded} loads aboard, yours to sell in any town");
        Check(main.Standing.Bounty > bounty && main.Bonds.Count == bonds + 1 && trade.Banned(main.Economy.Day) && !main.MayStealForTest,
            $"and a price on your head ({main.Standing.Bounty} crowns), the horse your own, and no work at the yard for {TradeBook.BanDays} days");
        main.ClimbDownForTest();
    }

    /// <summary>A shapeshifter's limit break merges them with the beast they wear, or the one they've worn most; timed, stronger, and each beast lends its own body.</summary>
    /// <summary>The elemental druid's form of their choosing, the barbarian's berserker rage, and the martial artist's guardian spirit.</summary>
    async Task NewLimitsTest()
    {
        var p = main.PlayerNode;
        await Teleport(new Vector3(-150, 0, -120));
        await Frames(10);
        p.Join("Stormdruid");
        p.Fit();
        p.Element = DamageKind.Fire;
        main.Pursuits.Counters["elemental_form"] = Player.Elementals.ToList().FindIndex(e => e.Id == "earth") + 1;
        await Frames(2);
        var (might, armor) = (p.Stats.DamageMult, p.Stats.Armor);
        p.Stats.GainLimit(100);
        Check(p.ElementalKind == "earth" && p.LimitBreak() && p.Possessing, $"a fire-casting druid set on stone becomes {p.ElementalKind}, for {p.SuitLeft:0} s");
        await Frames(10);
        Check(p.Stats.DamageMult > might && p.Stats.Armor >= armor + 60, $"the rock golem hits harder and takes more: x{p.Stats.DamageMult / might:0.00}, armour +{p.Stats.Armor - armor:0}");
        var lasts = p.SuitLeft;
        p.Unpossess();
        await Frames(10);
        Check(!p.Possessing && Math.Abs(p.Stats.DamageMult - might) < 0.001 && p.Element == DamageKind.Fire, "and it passes, and they're themselves again");
        main.Pursuits.Counters["elemental_form"] = 0;
        p.Stats.GainLimit(100);
        Check(p.ElementalKind == "fire" && p.LimitBreak(), "left unset, the form follows their attunement");
        p.Unpossess();
        await Frames(10);

        p.Join("Barbarian");
        p.Fit();
        p.Stats.Heal(1e9);
        var foe = Enemy.Create(Bestiary.Get("bandit"), p.GlobalPosition + new Vector3(3, 0.3f, 0));
        main.AddEnemy(foe);
        await Frames(5);
        might = p.Stats.DamageMult;
        var height = p.Figure.Scale.Y;
        p.Stats.GainLimit(100);
        Check(p.LimitBreak() && p.Impervious && p.Figure.Scale.Y > height * 1.4f && p.Stats.DamageMult > might, $"the berserker swells (x{p.Figure.Scale.Y / height:0.0}) and grows stronger (x{p.Stats.DamageMult / might:0.00})");
        var health = p.Stats.Health;
        p.TakeHit(80, DamageKind.Physical, AttackKind.Weapon, false, foe);
        Check(p.Stats.Health == health, "no blade can hurt him");
        p.TakeHit(20, DamageKind.Fire, AttackKind.Spell, false, foe);
        Check(p.Stats.Health < health, $"but magic still can ({health - p.Stats.Health:0} from a spell)");
        for (var i = 0; i < 60 && p.Impervious; i++) await Frames(30);
        Check(!p.Impervious && Mathf.Abs(p.Figure.Scale.Y - height) < 0.01f && Math.Abs(p.Stats.DamageMult - might) < 0.001, "and the rage burns out, back to his own size");

        p.Join("MartialArtist");
        p.Fit();
        foe.QueueFree();
        var target = Enemy.Create(Bestiary.Get("bandit_chief"), p.GlobalPosition - p.GlobalTransform.Basis.Z * 2.5f + Vector3.Up * 0.3f);
        main.AddEnemy(target);
        await Frames(5);
        var hp = target.Stats.Health;
        p.Stats.GainLimit(100);
        var spirits = 0;
        Check(p.LimitBreak(), "the martial artist calls the guardian spirit");
        await Frames(20);
        spirits = p.GetParent().GetChildren().OfType<Guardian>().Count();
        await Frames(120);
        Check(spirits == 1 && target.Stats.Health < hp, $"a great golden spirit rises behind them and strikes ({hp - target.Stats.Health:0} harm)");
        target.QueueFree();
    }

    /// <summary>A shapeshifter in armour, a sword in hand, takes a beast's shape all the same, and keeps what the gear gives.</summary>
    async Task ArmouredShift()
    {
        var p = main.PlayerNode;
        p.Join("Shapeshifter");
        main.ForestLog.Shapes = ["wolf", "bear"];
        foreach (var id in new[] { "steel_plate", "steel_sword" }) { p.Pack.Add(id); p.Pack.Equip(id); }
        p.Dress();
        await Frames(10);
        var (own, might, armor) = (p.Figure, p.Stats.DamageMult, p.Stats.Armor);
        await Until(() => p.Cooldown("shift") <= 0, 4);
        var used = p.UseMove(p.Class.Utility);
        await Frames(10);
        Check(used && p.Form == "wolf" && p.Figure != own && p.Figure.Visible && !own.Visible, $"in steel plate with a sword, they still take the wolf's shape (used {used}, form {p.Form}, own body hidden {!own.Visible})");
        Check(p.Stats.Armor >= armor && p.Stats.DamageMult > might, $"and the plate and the sword count in the wolf's body (armour {armor:0} to {p.Stats.Armor:0})");
        // Changing gear in a beast's shape dresses the hero's own body under it, and leaves them the beast.
        p.Pack.Unequip(Slot.Weapon);
        p.Dress();
        await Frames(5);
        Check(p.Form == "wolf" && p.Figure is not Figure { IsHuman: true } && p.Figure.Visible, "taking the sword off in wolf shape, they're still a wolf");
        p.Pack.Equip("steel_sword");
        p.Dress();
        var beast = (p.Stats.Armor, p.Stats.DamageMult);
        await Until(() => p.Cooldown("shift") <= 0, 4);
        p.UseMove(p.Class.Utility); // to the bear
        p.UseMove(p.Class.Utility);
        await Until(() => p.Cooldown("shift") <= 0, 4);
        p.UseMove(p.Class.Utility); // and back to themselves
        await Frames(5);
        if (p.Form is not null) { await Until(() => p.Cooldown("shift") <= 0, 4); p.UseMove(p.Class.Utility); await Frames(5); }
        var (plain, plainMight) = (p.Stats.Armor, p.Stats.DamageMult);
        p.Stats.GainLimit(100);
        p.LimitBreak();
        await Frames(10);
        var gearArmour = p.Pack.Armor;
        Check(p.Possessing && p.Stats.Armor >= plain + 30 + gearArmour * 2 - 0.5, $"in the half-form the plate counts three times over (armour {plain:0} to {p.Stats.Armor:0}, the plate's {gearArmour:0})");
        p.Unpossess();
        await Frames(5);
        foreach (var id in new[] { "steel_plate", "steel_sword" }) { p.Pack.Unequip(id == "steel_plate" ? Slot.Body : Slot.Weapon); p.Pack.Take(id); }
        p.Dress();
    }

    async Task TheHalfForm()
    {
        var p = main.PlayerNode;
        p.Join("Shapeshifter");
        p.Fit();
        main.ForestLog.Shapes = ["wolf", "bear", "fox"];
        main.ForestLog.Worn = new() { ["wolf"] = 5, ["bear"] = 90 };
        Check(p.Class.Limit.Id == "half_form", $"a shapeshifter's limit break is {p.Class.Limit.Name}");
        var (might, armor) = (p.Stats.DamageMult, p.Stats.Armor);
        p.Stats.GainLimit(100);
        Check(p.HalfFormKind == "bear" && p.LimitBreak() && p.Possessing, $"out of beast form it reaches for the beast worn most: {p.HalfFormKind}, {p.SuitLeft:0} s of it");
        await Frames(10);
        Check(p.Figure is Figure { IsHuman: true } && p.Stats.DamageMult > might && p.Stats.Armor > armor, $"on a real body, stronger and tougher: x{p.Stats.DamageMult / might:0.00}");
        p.Stats.GainLimit(100);
        Check(!p.LimitBreak() && p.Stats.LimitReady, "a second call while it's on is refused, and doesn't spend the gauge");
        p.Unpossess();
        await Frames(10);
        Check(!p.Possessing && Math.Abs(p.Stats.DamageMult - might) < 0.001 && p.Stats.Armor == armor, "it passes, and they're themselves, no stronger than before");
        await Until(() => p.Cooldown("shift") <= 0, 4);
        Check(p.UseMove(p.Class.Utility) && p.Form == "wolf", $"a beast form, as long as they like: {p.Form}");
        await Frames(10);
        p.Stats.GainLimit(100);
        Check(p.LimitBreak() && p.HalfFormKind == "wolf" && p.Form is null, "called in beast form, it merges with that beast and not the favourite");
        p.Unpossess();
        await Frames(10);
        Check(p.Form is null && Math.Abs(p.Stats.DamageMult - might) < 0.001, "and comes back to their own body");
        // A fall hurts by the metre, and a strong enough hero lives through it.
        await Frames(5);
        p.Stats.Heal(1e9);
        var whole = p.Stats.Health;
        p.FallForTest(Flight.SafeFall + 5);
        await Frames(3);
        Check(Math.Abs(whole - p.Stats.Health - Flight.FallDamage(Flight.SafeFall + 5)) < 1 && !p.Downed, $"a fall of 11 m costs {whole - p.Stats.Health:0} health");
        p.Stats.Heal(1e9);
        p.FallForTest(Flight.SafeFall);
        await Frames(3);
        Check(p.Stats.Health == p.Stats.MaxHealth, "and a short drop costs nothing");
        // Pets and machines take the names their hero gives them; a person keeps their own.
        p.Join("Alchemist");
        p.Hero.Level = PartyRules.SecondConstructLevel;
        await Until(() => main.Bonds.Count(b => b.Role == BondRole.Golem) == 2, 6);
        var made = main.Bonds.First(b => b.Role == BondRole.Golem);
        Check(main.Bonds.Count(b => b.Role == BondRole.Golem) == 2 && main.Rename(made, "  Bessie "), "an alchemist at level 6 has two constructs, and can name them");
        await Frames(30);
        Check(made.Name == "Bessie" && main.CompanionBodies.First(e => e.Bond == made).Stats.Name == "Bessie", $"and the name stays, on its plate too: {made.Name}");
        Check(!main.Rename(new Bond { Role = BondRole.Apprentice, Name = "Tobin" }, "Fred"), "an apprentice keeps the name they came with");
        foreach (var kind in ForestBook.Beasts)
        {
            var f = Figure.Human($"hybrid:{kind}", "human", Tristinwold.World.Sex.Male, LifeStage.Adult, 7, Hybrids.Fur(kind));
            Check(f is not null, $"{Hybrids.Name(kind)} has a body");
            if (f is null) continue;
            main.AddChild(f);
            await Frames(5);
            f.QueueFree();
        }
    }

    /// <summary>Hollowheart's heart-trees: walk up each ramp and you come out on the deck, which is wide and solid all the way round.</summary>
    /// <summary>A town of the world as a hero finds it: its smith and store and council, a home that keeps trading with its owner away, waking in the town, a mine, and a deviant running from death.</summary>
    async Task LivingTownsTest()
    {
        var (p, world, city, hud) = (main.PlayerNode, main.World, main.Economy, main.HudNode);
        var druitt = world.Find("city:druitt")!;
        await Teleport(new Vector3((float)(druitt.X + druitt.Radius * 0.82), 0, (float)druitt.Z));
        await Until(() => main.PlacesNode.IsBuilt(druitt.Id), 8);
        var people = main.PlacesNode.People.Where(x => x.Npc.HomeId == druitt.Id).ToList();
        var smith = people.FirstOrDefault(x => x.Npc.Id.EndsWith("/smith"));
        var store = people.FirstOrDefault(x => x.Npc.Id.EndsWith("/general"));
        Check(smith is not null && store is not null, $"{druitt.Name} has its smith and its general store ({smith?.Npc.Name}, {store?.Npc.Name})");
        Check(city.CouncilOf(druitt.Id).Members.Count >= 3 && people.Any(x => x.Npc.Id.EndsWith(TownCouncils.ClerkId)), $"and its council of {city.CouncilOf(druitt.Id).Members.Count}, with a clerk");
        main.Talk(smith!);
        Check(hud.Choose("Let me see") && hud.TalkText.Length > 0, "the smith sells you what the Anvil Gate does");
        hud.EndTalk();

        // Waking there: on the open road, and on your feet, not in the keep.
        main.Story.Home = new Hometown(druitt.Id, druitt.Name, druitt.X, druitt.Z);
        await Teleport(main.WakeSpotForTest() with { Y = 0 });
        p.GlobalPosition = main.WakeSpotForTest();
        await Frames(60);
        var ground = City.FloorAt(p.GlobalPosition);
        Check(Mathf.Abs(p.GlobalPosition.Y - ground) < 2 && p.GlobalPosition.DistanceTo(new Vector3((float)druitt.X, ground, (float)druitt.Z)) > druitt.Radius * 0.5f, $"the ring wakes you on your feet in {druitt.Name} ({p.GlobalPosition.Y - ground:0.0} m off the ground)");
        main.Story.Home = null;

        // A seat, and the table to yourself.
        main.ReputeForTest.Towns[druitt.Id] = 4;
        var clerk = main.PlacesNode.People.First(x => x.Npc.Id.EndsWith(TownCouncils.ClerkId) && x.Npc.HomeId == druitt.Id);
        main.Talk(clerk);
        Check(hud.Choose("I'd like to speak") && city.CouncilOf(druitt.Id).Heroes.Contains(p.Company), $"well thought of, you're given a seat ({hud.TalkText}; heroes: {string.Join(",", city.CouncilOf(druitt.Id).Heroes)}; you {p.Company}; options {string.Join(" | ", hud.Options)})");
        hud.EndTalk();
        foreach (var id in city.CouncilOf(druitt.Id).Members.Keys.ToList()) main.DoCity(CityAction.CouncilFall(druitt.Id, id));
        Check(city.CouncilOf(druitt.Id).Ruler == p.Company, "the last at the table, you rule");
        main.Talk(clerk);
        Check(hud.Choose("Send out raiders") && hud.Options.Any(o => o.StartsWith("Raid ")), $"and the raiders are yours to send ({hud.Options.Count()} options)");
        hud.EndTalk();

        // A deviant cut down that wins its roll runs instead of falling, and when it's caught it falls for good.
        Enemy.DeathOdds = 1;
        var ran = false;
        Action<Enemy, bool> saw = (_, clear) => ran |= !clear;
        Enemy.RanOff += saw;
        var at = p.GlobalPosition + new Vector3(4, 0, 0);
        var deviant = Enemy.Create(Bestiary.Get("wolf"), at with { Y = City.FloorAt(at) + 0.5f });
        main.AddEnemy(deviant);
        deviant.MakeDeviant(Guid.NewGuid(), new BattleModifiers("Grey Gnash", new Dictionary<Stat, double>(), new Dictionary<DamageKind, Affinity>(), []), null);
        await Frames(5);
        deviant.Shelled(deviant.Stats.Health + 1, true);
        await Frames(10);
        Check(ran && deviant.State == EnemyState.Fleeing, $"a deviant that beats death gets up and runs ({deviant.State})");
        deviant.Shelled(deviant.Stats.Health + 1, true);
        await Frames(10);
        Enemy.RanOff -= saw;
        Enemy.DeathOdds = 0;
        Check(deviant.State == EnemyState.Dead, "catch it, and it falls for good");

        // The boardwalk: from the gate's pier along to the last of them, and dry the whole way.
        await Teleport(new Vector3(City.BoardwalkX, 0, -28));
        await Frames(30);
        Check(City.OnPier(p.GlobalPosition) && !p.Swimming && Mathf.Abs(p.GlobalPosition.Y) < 1, $"the boardwalk carries you to the far pier (height {p.GlobalPosition.Y:0.0})");

        // The steam wagon: no horse, a fire to feed, and a shop when she's parked.
        main.SteamForTest(0);
        await Frames(5);
        Check(main.WagonNode is { Steam: true, Teamed: false }, "a steam wagon with a cold boiler goes nowhere");
        main.SteamForTest(120);
        await Frames(5);
        Check(main.WagonNode is { Steam: true, Teamed: true }, "with something burning she's ready to go");
        main.OpenWagonShopForTest();
        await Frames(5);
        Check(city.StoreOf(main.WagonShopKeyForTest) is { Wagon: true } && main.WagonNode is { Teamed: false }, "parked, she opens up as a shop, and won't roll while she's open");
        main.CloseWagonShopForTest();
        Check(city.StoreOf(main.WagonShopKeyForTest) is null, "and closes up to get back on the road");

        // A mine, walked down.
        var mine = world.SettlementsNear(0, 0, 4000).First(s => s.Kind == SettlementKind.Mine && !s.Hostile);
        await Teleport(new Vector3((float)mine.X + 4, 0, (float)mine.Z + 8));
        await Until(() => main.PlacesNode.IsBuilt(mine.Id), 8);
        Check(main.PlacesNode.People.Any(x => x.Npc.Id.EndsWith("/mine")), $"{mine.Name}'s foreman keeps trading");
        var seed = main.Shafts.First(s => s.Value.Id == mine.Id).Key;
        main.GoInsideForTest("mineshaft", seed, p.GlobalPosition);
        await Frames(20);
        Check(main.IndoorsNode is not null && Mathf.Abs(p.GlobalPosition.Y - Indoors.Threshold) < 500, $"you can walk down it ({main.TroubleAt(mine)})");
        main.GoOutsideForTest();
        await Frames(10);
        Check(main.ShaftFoes.Count == 0, "and what's down there stays there when you leave");

        // One deviant, one body: the shared world names it again as each new foe joins the fight, and nothing's copied.
        await Teleport(new Vector3(-150, 0, -120)); // out in open country: nobody brings a band into a settlement
        var hordeAt = p.GlobalPosition + new Vector3(6, 0, 6);
        var chief = Enemy.Create(Bestiary.Get("wolf"), hordeAt with { Y = City.FloorAt(hordeAt) + 0.5f });
        main.AddEnemy(chief);
        var chiefId = Guid.NewGuid();
        var mods = new BattleModifiers("Old Scar", new Dictionary<Stat, double>(), new Dictionary<DamageKind, Affinity>(), [], Rank.Ringleader);
        chief.MakeDeviant(chiefId, mods, null);
        main.FeudsNode.Soldiers(chief, Rank.Ringleader, wait: 0.02);
        await Frames(30);
        var minions = main.Enemies().Where(e => main.FeudsNode.IsSoldier(e)).ToList();
        var bystander = Enemy.Create(Bestiary.Get("wolf"), hordeAt + new Vector3(4, 1, 0));
        main.AddEnemy(bystander);
        Check(minions.Count == Bands.Soldiers(Rank.Ringleader) && minions.All(m => m.Stats.Name.EndsWith("Minion")), $"a deviant brings its soldiers, its minions by name ({minions.Count})");
        foreach (var joined in minions.Append(bystander))
        {
            main.Tracker.Engaged(joined.Id, joined.Kind.Id, joined.Kind.Name, joined.Stats.MaxHealth, default, "test", default, main.Clock);
            var slot = Enumerable.Range(0, 64).First(s => main.Tracker.ActorForSlot(s) == joined.Id);
            main.HandleForTest(new DeviantAppears([new DeviantSpawn(slot, chiefId, mods, null)]));
        }
        await Frames(10);
        Check(main.Enemies().Count(e => e.DeviantId == chiefId) == 1 && minions.All(m => main.FeudsNode.BandOf(m) == 0), $"told again and again, it's still one deviant, and its soldiers raise none of their own ({main.Enemies().Count(e => e.DeviantId == chiefId)})");
        foreach (var e in minions.Append(bystander).Append(chief)) e.QueueFree();
    }

    /// <summary>The marks heroes leave: a hall founded and run, another hero's hall to do business at, a druid's tree, and the Drowned Chapel's crypt.</summary>
    async Task ClassMarksTest()
    {
        var (p, city, hud) = (main.PlayerNode, main.Economy, main.HudNode);
        while (p.Hero.Level < 8) main.GainXp(Hero.XpToNext(p.Hero.Level));
        p.Crowns = 5000;
        p.Join("Barbarian");
        Check(main.NoRoomForHallForTest(Vector3.Zero) is not null, "nobody founds a hall in the middle of Tristinwold");
        await Teleport(new Vector3(-150, 0, -120));
        await Frames(10);
        var plan = Halls.PlanOf(HallKind.FightClub);
        main.FoundHallForTest();
        var club = city.HallOf(p.Company, HallKind.FightClub);
        Check(club is { Members: 1 } && p.Crowns == 5000 - plan.FoundCost, $"a barbarian founds a fight club where they stand ({club?.Name}, {p.Crowns} crowns left)");
        main.TendHallsForTest();
        await Frames(5);
        var steward = main.SharedFolkForTest.FirstOrDefault(x => x.Npc.Id == $"hall:{club?.Key}");
        Check(steward is not null, "it stands, with its pit boss in the yard");
        if (steward is null || club is null) return;
        main.Talk(steward);
        Check(hud.Choose("Take on another") && city.HallOf(p.Company, HallKind.FightClub)!.Members == 2, $"and takes on another fighter ({hud.TalkText})");
        hud.EndTalk();
        city.TendHalls(Halls.JobSeconds * 3);
        var till = city.HallOf(p.Company, HallKind.FightClub)!.Till;
        var purse = p.Crowns;
        main.Talk(steward);
        Check(till > 0 && hud.Choose("Empty the till") && p.Crowns == purse + till, $"the club earns while you're away, and the till's yours ({till} crowns)");
        hud.EndTalk();
        main.Talk(steward);
        Check(hud.Choose("Step into the pit") && main.Dueling, "you can step into your own pit");
        var champion = main.Enemies().FirstOrDefault(e => e.Sparring);
        for (var i = 0; i < 6 && champion is { Yielded: false }; i++) { champion.TakeHit(champion.Stats.MaxHealth * 2, DamageKind.Physical, AttackKind.Weapon, false, p); await Frames(3); }
        await Frames(10);
        Check(champion is not null && !main.Dueling && champion.Yielded, $"and the champion yields when beaten ({champion?.Stats.Name}, {champion?.Stats.Health:0} of {champion?.Stats.MaxHealth:0}, duel {main.Dueling})");

        // Someone else's monastery: training, once a day, paid into its till.
        var at = p.GlobalPosition + new Vector3(90, 0, 0);
        main.DoCity(CityAction.HallFound(new Hall { Owner = "Mira", Hero = "Mira", Kind = HallKind.Monastery, Name = "Mira's Monastery", X = at.X, Z = at.Z }));
        await Teleport(at + new Vector3(4, 0, 6));
        await Frames(10);
        main.TendHallsForTest();
        await Frames(5);
        var monastery = city.HallOf("Mira", HallKind.Monastery)!;
        var elder = main.SharedFolkForTest.FirstOrDefault(x => x.Npc.Id == $"hall:{monastery.Key}");
        var (xp, level, crowns) = (p.Hero.Xp, p.Hero.Level, p.Crowns);
        if (elder is not null) main.Talk(elder);
        Check(elder is not null && hud.Choose("Train with the disciples") && (p.Hero.Xp > xp || p.Hero.Level > level) && p.Crowns == crowns - 40 && city.HallOf("Mira", HallKind.Monastery)!.Till >= 40,
            $"another hero's monastery trains you, and Mira's till takes the fee ({hud.TalkText})");
        hud.EndTalk();
        if (elder is not null) main.Talk(elder);
        Check(hud.Choose("Train with the disciples") && hud.TalkText.Contains("Once a day"), "once a day");
        hud.EndTalk();

        // A druid's tree.
        p.Join("Spiritcaller");
        p.Stats.Restore(1e9, 1e9);
        var trees = city.Grove.Count;
        main.PlantForTest();
        Check(city.Grove.Count == trees + 1 && main.ShownGroveForTest > 0, $"a druid plants a tree where they stand, and there it is ({city.Grove.Count})");
        city.Grove[^1].Day -= Groves.GrownAt;
        main.TendHallsForTest();
        Check(Groves.Stage(city.Grove[^1], city.Day) == 2, "and in three days it's grown");

        // The Drowned Chapel, out on the north shore, and the dead in its crypt.
        await Teleport(Main.CryptStairForTest);
        await Frames(20);
        Check(City.AreaAt(p.GlobalPosition) == "The Drowned Chapel" && main.ChapelActionForTest(p.GlobalPosition) is { Verb: "go down into the crypt" }, $"the Drowned Chapel stands in ruins outside the walls, with steps going down ({City.AreaAt(p.GlobalPosition)})");
        main.EnterCryptForTest();
        await Frames(20);
        Check(main.IndoorsNode is not null && main.CryptFoes.Count >= 8 && main.CryptFoes.Any(e => e.Kind.Id == "hollow_soldier") && main.CryptFoes.Any(e => e.Kind.Id == "sea_wraith"),
            $"down in the crypt, the drowned dead ({string.Join(", ", main.CryptFoes.Select(e => e.Kind.Id))})");
        crowns = p.Crowns;
        for (var i = 0; i < 8; i++) { foreach (var e in main.CryptFoes.ToList()) e.Shelled(e.Stats.MaxHealth * 4, true); await Frames(4); }
        await Frames(30);
        Check(main.CryptCleared && p.Crowns > crowns, $"clear them, and the Temple pays ({p.Crowns - crowns} crowns; standing: {string.Join(", ", main.CryptFoes.Where(e => GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead).Select(e => $"{e.Kind.Id} {e.State} {e.Stats.Health:0}"))})");
        main.GoOutsideForTest();
        await Frames(10);
        await EnginesTest();
    }

    /// <summary>An order left for an artificer friend and fitted, and a skyboat taken up and brought down again.</summary>
    async Task EnginesTest()
    {
        var (p, city, sea) = (main.PlayerNode, main.Economy, main.Sea);
        (sea.OwnShip, sea.ShipName, sea.Steam) = (true, "Wren", false);
        main.DoCity(CityAction.OrderPost(p.Company, Engines.ShipSteam, 50));
        main.DoCity(CityAction.OrderFit($"{p.Company}#{Engines.ShipSteam}", "Mira", 12));
        main.CollectOrdersForTest();
        Check(sea.Steam && sea.SteamLevel == 12 && city.Orders.Count == 0, "an artificer friend does your order, and your ship has her boiler, as good as they are");

        main.FittedForTest(Engines.Skyboat, 10);
        sea.SkyFuel = 300;
        await Teleport(new Vector3((float)sea.SkyX + 3, 0, (float)sea.SkyZ));
        await Frames(10);
        main.TendSkyboatForTest();
        await Frames(5);
        var boat = main.SkyboatNode;
        Check(boat is not null, $"the skyboat waits at the end of the pier ({sea.SkyX:0},{sea.SkyZ:0})");
        if (boat is null) return;
        main.BoardSkyboatForTest();
        var y = boat.GlobalPosition.Y;
        Input.ActionPress("jump");
        Input.ActionPress("move_forward");
        await Frames(120);
        Input.ActionRelease("jump");
        Input.ActionRelease("move_forward");
        Check(main.AboardSkyboat && boat.GlobalPosition.Y > y + 3 && sea.SkyFuel < 300, $"at the helm she climbs ({boat.GlobalPosition.Y - y:0.0} m) and burns her fuel ({sea.SkyFuel:0} left)");
        main.LeaveSkyboatForTest();
        Check(main.AboardSkyboat, "nobody steps off in the air");
        Input.ActionPress("descend");
        for (var i = 0; i < 40 && !boat.Landed; i++) await Frames(10);
        Input.ActionRelease("descend");
        main.LeaveSkyboatForTest();
        Check(!main.AboardSkyboat && Mathf.Abs((float)sea.SkyX - boat.GlobalPosition.X) < 0.1f, "brought down, you step off, and she's left where she landed");
        await CarpentryTest();
    }

    /// <summary>The carpenters: a job taken from Abel Joist, planks to the site, frame, walls and roof, and the town has its house.</summary>
    async Task CarpentryTest()
    {
        var (p, city, hud) = (main.PlayerNode, main.Economy, main.HudNode);
        await Teleport(Main.CarpentersYard + new Vector3(4, 0, 6));
        await Frames(10);
        var abel = main.Locals.FirstOrDefault(x => x.Npc.Id == "tristinwold/carpenter");
        Check(abel is not null, "Abel Joist keeps the carpenters' yard");
        if (abel is null) return;
        city.FabricOf(ReputationBook.Capital).Burnt = 1;
        main.Talk(abel);
        Check(hud.Choose("Raise a burned home in Tristinwold") && main.CarpentryJobForTest is { Work: CarpentryWork.Raise, Stage: 0 }, $"anyone can take a carpenter's job ({string.Join(" | ", hud.Options)})");
        hud.EndTalk();
        var site = main.SiteForTest(ReputationBook.Capital)!.Value;
        await Teleport(site + new Vector3(4, 0, 4));
        await Frames(10);
        main.TendSiteForTest();
        p.Pack.Add("plank", Carpentry.Planks);
        var (crowns, done) = (p.Crowns, 0);
        for (var stage = 0; stage < Carpentry.Stages; stage++)
        {
            if (main.SiteActionForTest(p.GlobalPosition) is not { } act) break;
            act.Act();
            await Frames(10);
            for (var i = 0; i < 40 && main.FramingForTest; i++) await Frames(10);
            done++;
        }
        Check(done == Carpentry.Stages && main.CarpentryJobForTest is null && city.FabricOf(ReputationBook.Capital).Burnt == 0 && p.Crowns > crowns && p.Pack.Count("plank") == 0,
            $"frame, walls and roof, and the burned home stands again ({done} stages, {p.Crowns - crowns} crowns)");
        await ShopsInsideTest();
    }

    /// <summary>A town's general store walked into, and its keeper behind the counter, trading the same as out front.</summary>
    async Task ShopsInsideTest()
    {
        var (p, world, hud) = (main.PlayerNode, main.World, main.HudNode);
        var druitt = world.Find("city:druitt")!;
        await Teleport(new Vector3((float)(druitt.X + druitt.Radius * 0.82), 0, (float)druitt.Z));
        await Until(() => main.PlacesNode.IsBuilt(druitt.Id), 8);
        var door = main.ShopDoors.FirstOrDefault(d => d.Value.Npc.HomeId == druitt.Id && d.Value.Npc.Id.EndsWith("/general"));
        Check(door.Value.Npc is not null, $"{druitt.Name}'s general store has a door");
        if (door.Value.Npc is null) return;
        main.GoInsideForTest("store", door.Key, p.GlobalPosition);
        await Frames(20);
        var keeper = main.KeeperInsideForTest;
        Check(main.IndoorsNode is not null && keeper?.Npc.Id == door.Value.Npc.Id, $"inside, {door.Value.Npc.Name} is behind the counter");
        if (keeper is not null) main.Talk(keeper);
        Check(hud.Choose("Let me see"), $"and sells to you the same as out front ({hud.TalkText})");
        hud.EndTalk();
        main.GoOutsideForTest();
        await Frames(10);
        Check(main.KeeperInsideForTest is null && main.IndoorsNode is null, "and stays at the counter when you leave");
        await OpenSeaTest();
        var standing = main.Standing;
        standing.Wanted(60);
        Check(main.KnownOutlawsForTest() is var said && said.Contains("Soren") && said.Contains("Wukong") && said.Contains("60 crowns on your head") && main.Economy.Outlaws.GetValueOrDefault(p.Company) == 60,
            "a hero with a price on their head is on the list of known outlaws, with Soren and Lu Wukong");
        standing.Bounty = 0;
        Check(!main.KnownOutlawsForTest().Contains("on your head") && !main.Economy.Outlaws.ContainsKey(p.Company), "and off it once it's paid");
        await DriftersTest();
        await UndyingTest();
        await DeepsTest();
        await SyndicateTest();
        await StepsTest();
        await WarlordsTest();
        await StashTest();
        await HeistTest();
        await HandiworkTest();
        await PanicTest();
        await KeeperTest();
        await FleetTest();
        await HeroFightTest();
    }

    /// <summary>
    /// Heroes fighting heroes: another player's unseen stand-in, B to draw on them, a blow sent to their game, theirs
    /// taken here, and a fall to them that pays them (a copy of the pack, gear as crowns, the purse, the bounty) while
    /// this hero keeps everything.
    /// </summary>
    async Task HeroFightTest()
    {
        var (p, visitors) = (main.PlayerNode, main.VisitorsNode);
        await Teleport(City.PlayerStart + new Vector3(0, 0, 6));
        await Frames(10);
        var at = p.GlobalPosition - p.GlobalTransform.Basis.Z * 1.6f;
        visitors.Seen("Mira", new Moved("Wizard", null, "Mira", at.X, at.Y, at.Z, 0, 0, null, 0));
        await Frames(20);
        var mira = visitors.StandIn("Mira");
        Check(mira is { Hero: "Mira" } && !mira.Figure.Visible && mira.Stats.Name == "Mira" && mira.GlobalPosition.DistanceTo(at) < 0.5f, "another player has an unseen stand-in where her hero is drawn");
        p.Face(mira!.GlobalPosition);
        p.CoolForTest();
        p.Stats.Restore(p.Stats.MaxMana, p.Stats.MaxStamina);
        p.Swing();
        await Frames(40);
        Check(main.LastHeroMessage is null && !main.FightingHeroForTest("Mira"), "not drawn on players, a swing doesn't touch her");
        main.DrawOnHeroesForTest();
        Check(main.DrawnForTest && main.FightingHeroForTest("Mira"), "B draws on every player about");
        for (var i = 0; i < 6 && main.LastHeroMessage is null; i++)
        {
            p.Face(mira.GlobalPosition);
            p.CoolForTest();
            p.Stats.Restore(p.Stats.MaxMana, p.Stats.MaxStamina);
            p.Swing();
            await Frames(40);
        }
        Check(main.LastHeroMessage is StrikeHero { Target: "Mira", Damage: > 0 }, $"and a swing lands on Mira: the blow goes to her game ({main.LastHeroMessage})");
        main.DrawOnHeroesForTest();
        Check(!main.DrawnForTest && main.FightingHeroForTest("Mira"), "put away, she's still one you're fighting");

        // A blow from her, taken like any other; and from someone nowhere near, nothing.
        var hp = p.Stats.Health;
        for (var i = 0; i < 10 && p.Stats.Health >= hp; i++) main.HandleForTest(new HeroStruck("Mira", 20, DamageKind.Physical, AttackKind.Weapon, false));
        Check(p.Stats.Health < hp, $"Mira's blow lands on you ({hp:0} to {p.Stats.Health:0})");
        hp = p.Stats.Health;
        main.HandleForTest(new HeroStruck("Nobody", 20, DamageKind.Physical, AttackKind.Weapon, false));
        Check(p.Stats.Health == hp, "a player nowhere near can't reach you");

        // She puts you down: she gets her spoils, you keep it all, and your bounty is hers.
        p.Pack.Add("healing_draught", 2);
        p.Pack.Add("iron_sword");
        p.Crowns = 300;
        main.Standing.Bounty = 40;
        var (draughts, swords) = (p.Pack.Count("healing_draught"), p.Pack.Count("iron_sword"));
        for (var i = 0; i < 10 && !p.Downed; i++) main.HandleForTest(new HeroStruck("Mira", 1e6, DamageKind.Physical, AttackKind.Weapon, true));
        Check(p.Downed, "and Mira can put you down");
        var fell = main.LastHeroMessage as HeroFell;
        Check(fell is { Killer: "Mira", Crowns: 300, Bounty: 40 } && fell.GearCrowns >= Items.Get("iron_sword")!.Value
              && fell.Items.Any(i => i.Id == "healing_draught" && i.N == draughts) && fell.Items.All(i => Items.Get(i.Id)?.Kind != ItemKind.Gear),
            $"she wins a copy of your pack, your gear as crowns ({fell?.GearCrowns}), a purse like yours and the bounty");
        Check(p.Crowns == 300 && p.Pack.Count("healing_draught") == draughts && p.Pack.Count("iron_sword") == swords && main.Standing.Bounty == 0,
            "and you keep everything: only the bounty is gone, collected");
        await Until(() => !p.Downed, 8);

        // Again within the hour: nothing more for her, and a new bounty stands.
        main.Standing.Bounty = 15;
        for (var i = 0; i < 10 && !p.Downed; i++) main.HandleForTest(new HeroStruck("Mira", 1e6, DamageKind.Physical, AttackKind.Weapon, true));
        Check(p.Downed && main.LastHeroMessage is HeroFell { Crowns: 0, GearCrowns: 0, Bounty: 0, Items.Count: 0 } && main.Standing.Bounty == 15,
            "beaten by her again within the hour: she wins nothing more, and the new bounty stands");
        main.Standing.Bounty = 0;
        await Until(() => !p.Downed, 8);

        // You put her down: yours for it.
        var crowns = p.Crowns;
        main.HandleForTest(new HeroSpoils("Mira", [new("mana_tonic", 3)], 100, 50, 25));
        Check(p.Crowns == crowns + 175 && p.Pack.Count("mana_tonic") >= 3 && !main.FightingHeroForTest("Mira"), $"beat her, and her purse, her gear's worth and her bounty are yours ({crowns} to {p.Crowns})");
        visitors.Roster([]);
        await Frames(5);
        Check(visitors.StandIn("Mira") is null, "and when she's gone, so is her stand-in");
    }

    /// <summary>A country mine's maze has ore in its walls to work with a pick, and the way down is a dwarf door at the back of the face.</summary>
    async Task SeamsTest()
    {
        var p = main.PlayerNode;
        var mine = main.World.SettlementsNear(0, 0, 6000).First(s => s.Kind == SettlementKind.Mine);
        main.Shafts[777001] = mine;
        main.GoInsideForTest("mineshaft", 777001, p.GlobalPosition);
        await Frames(40);
        Check(main.IndoorVeins.Count >= 6, $"{mine.Name} ({mine.What}) has ore in its walls: {string.Join(", ", main.IndoorVeins.Select(v => v.Kind))}");
        var spot = main.IndoorVeins[0];
        p.GlobalPosition = spot.At + new Vector3(0, 0.3f, 1.6f);
        await Until(() => main.WorkingsNode.Shown.Any(s => s.Id == spot.Id), 4);
        p.Pack.Add("pickaxe");
        var ore = Gathering.Vein(spot.Kind).Ore;
        var had = p.Pack.Count(ore);
        main.WorkForTest(spot);
        await Until(() => p.Pack.Count(ore) > had, 8);
        Check(p.Pack.Count(ore) > had, $"work it with a pick and it gives up its {Items.Get(ore)!.Name.ToLowerInvariant()}");
        p.GlobalPosition = main.IndoorsNode!.DeepDoorAt + new Vector3(0, 0.3f, 1.8f);
        await Frames(10);
        Check(main.NearestForTest()?.Verb == "open the dwarf door down to the Deep Trenches", $"at the back of the face, a dwarf door down to the Deep Trenches ({main.NearestForTest()?.Verb})");
        main.GoOutsideForTest();
        await Frames(20);
    }

    /// <summary>An elemental druid in Elemental Form: a slam of its fists for what's close, and its spells as strong as its blows.</summary>
    async Task ElementalFormTest()
    {
        var p = main.PlayerNode;
        p.Join("Stormdruid");
        p.Hero.Level = 10;
        p.Fit();
        await Frames(10);
        var spells = p.Stats.SpellPower;
        p.Stats.GainLimit(100);
        Check(p.LimitBreak() && p.Possessing, "an elemental druid calls up Elemental Form");
        await Frames(10);
        Check(p.Stats.SpellPower > spells * 1.3, $"and its spells are the stronger for it (x{p.Stats.SpellPower / spells:0.00})");
        var foe = Enemy.Create(Bestiary.Get("bandit"), p.GlobalPosition - p.GlobalTransform.Basis.Z * 3 + Vector3.Up * 0.5f, 3);
        main.AddEnemy(foe);
        foe.Carried = true; // it stands still for the test
        await Frames(5);
        var health = foe.Stats.Health;
        p.Stats.Restore(0, 0);
        var mana = p.Stats.Mana;
        Check(p.Swing() && foe.Stats.Health < health && p.Stats.Mana >= mana - 0.01, $"close up, the basic attack is a slam of its fists, no mana ({health - foe.Stats.Health:0} damage)");
        foe.QueueFree();
        p.Unpossess();
        await Frames(10);
        Check(Math.Abs(p.Stats.SpellPower - spells) < 0.01, "and out of the form, its spells are as they were");
    }

    /// <summary>An aeronaut who takes up the shapeshifter's way keeps the golem and the harpoon, and can take the half-form as their one limit break.</summary>
    async Task MulticlassTest()
    {
        var p = main.PlayerNode;
        p.Hero.Level = 12;
        p.Join("EtherAeronaut");
        await Until(() => main.Bonds.Any(b => b.Role == BondRole.Golem), 6);
        p.Join("Shapeshifter");
        main.ForestLog.Shapes = ["wolf", "bear"];
        await Frames(240);
        Check(main.Bonds.Any(b => b.Role == BondRole.Golem), "a shapeshifter now, the aeronaut's golem still walks with them");
        Check(main.CompanionBodies.FirstOrDefault(e => e.Bond?.Role == BondRole.Golem)?.Figure is Clockwork { Design: "Ballast" }, "and it's still Ballast, the aeronaut's hull, not a saboteur's scrap");
        Check(QuickBar.Moves(p.Hero).Any(a => a.Id == "harpoon") && p.Limit.Id == "half_form", $"the harpoon still to hand, and the half-form their limit break ({p.Limit.Name})");
        p.Join("EtherAeronaut");
        // Renamed, it's still the aeronaut's hull you climb into.
        var golem = main.Bonds.First(b => b.Role == BondRole.Golem);
        main.Rename(golem, "Old Faithful");
        await Until(() => p.Cooldown("merge") <= 0, 4);
        p.UseMove(Classes.Move("merge")!);
        await Frames(10);
        Check(p.Possessing && p.Figure is Clockwork { Design: "Ballast" }, $"renamed {golem.Name}, merging still makes you the aeronaut's hull ({(p.Figure as Clockwork)?.Design})");
        // Merged, close up, the basic attack is a slam of the hull's brass fists.
        var foe = Enemy.Create(Bestiary.Get("bandit"), p.GlobalPosition - p.GlobalTransform.Basis.Z * 3 + Vector3.Up * 0.5f, 3);
        main.AddEnemy(foe);
        foe.Carried = true;
        await Frames(5);
        var had = foe.Stats.Health;
        p.Stats.Restore(1e6, 1e6);
        var (mana, stamina) = (p.Stats.Mana, p.Stats.Stamina);
        Check(p.Swing() && foe.Stats.Health < had && p.Stats.Mana >= mana - 0.01 && p.Stats.Stamina >= stamina - 0.01, $"merged, close up, the basic attack is a slam of the hull's fists ({had - foe.Stats.Health:0} damage)");
        foe.QueueFree();
        p.Unpossess();
        await Frames(10);
        p.Hero.Ultimate = "Shapeshifter";
        p.Stats.GainLimit(100);
        Check(p.Limit.Id == "half_form" && p.LimitBreak() && p.Possessing, "an aeronaut again, they can call the half-form as their one limit break");
        p.Unpossess();
        await Until(() => p.Cooldown("shift") <= 0, 4);
        Check(p.UseMove(Classes.Move("shift")!) && p.Form is not null, $"and take a beast's shape ({p.Form})");
        p.Hero.Ultimate = null;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
    }

    /// <summary>A squire given a sword and plate wears them and is the stronger for it; taken back, they're in the pack again.</summary>
    async Task KitTest()
    {
        var p = main.PlayerNode;
        p.Join("Paladin");
        var (role, kind, _) = PartyRules.Follower(p.Class.Id);
        var lad = new Bond { Id = "test/kit", KindId = kind, Role = role, Trains = p.Class.Id, Name = "Osk", Level = 5, Race = "human", JoinAge = 15 };
        main.AddBondForTest(lad);
        var body = main.BringForTest(lad);
        await Frames(30);
        var (health, armour, might) = (body.Stats.MaxHealth, body.Stats.Armor, body.Stats.DamageMult);
        foreach (var id in new[] { "steel_sword", "steel_plate" }) { p.Pack.Add(id); Kit.Give(lad, p.Pack, id); }
        main.RegearForTest(lad);
        await Frames(30);
        var now = main.CompanionBodies.First(e => e.Bond == lad);
        Check(now.Stats.Armor >= armour + 40 && now.Stats.DamageMult > might * 1.15, $"given a steel sword and plate, {lad.Name} hits harder and is harder to hurt (armour {armour:0} to {now.Stats.Armor:0}, damage x{now.Stats.DamageMult / might:0.00})");
        Check(now.Figure is not Figure { IsHuman: true } fig || fig.HeldIn("RightHand") is not null, "and carries the sword where you can see it");
        Kit.TakeBack(lad, p.Pack, Slot.Weapon);
        Kit.TakeBack(lad, p.Pack, Slot.Body);
        Check(p.Pack.Count("steel_sword") == 1 && p.Pack.Count("steel_plate") == 1 && lad.Gear.Count == 0, "taken back, both are in your pack again");
        // A pet eats fish as gladly as meat.
        var pup = main.AdoptForTest("wolf");
        pup.Fed = 30;
        p.Meat = 0;
        p.Pack.Add("herring", 2);
        main.Feed(pup);
        Check(pup.Fed > 30 && p.Pack.Count("herring") == 1, $"with no meat, a pet eats a herring ({pup.Fed:0}% fed)");
        // Your own things on the map, wherever you left them.
        main.Sea.OwnShip = true;
        Check(main.BelongingsForTest.Any(b => b.Kind == "ship"), $"your ship is on the map ({string.Join(", ", main.BelongingsForTest.Select(b => b.Name))})");
        foreach (var c in p.Companions().ToList()) c.QueueFree();
    }

    /// <summary>The company spread out behind the hero, each where you can see them, not stood on one another.</summary>
    async Task SpreadTest()
    {
        var p = main.PlayerNode;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        await Teleport(new Vector3(-150, 0, 40));
        foreach (var kind in new[] { "wolf", "bear", "fox", "boar" }) main.AdoptForTest(kind);
        // The party: each one's line, and right under it what can be done for them.
        main.ShowParty();
        Check(main.HudNode.Options.Count(o => o.StartsWith("Feed ")) == 4 && main.HudNode.Options.Count(o => o.StartsWith("Send ")) == 4, $"the party lists each of them with their own buttons ({main.HudNode.Options.Count()} buttons)");
        main.HudNode.EndTalk();
        await Frames(30);
        Input.ActionPress("move_forward");
        await Frames(150);
        Input.ActionRelease("move_forward");
        await Frames(240);
        var all = p.Companions().ToList();
        var closest = all.SelectMany((a, i) => all.Skip(i + 1).Select(b => new Vector2(a.GlobalPosition.X - b.GlobalPosition.X, a.GlobalPosition.Z - b.GlobalPosition.Z).Length())).DefaultIfEmpty(99).Min();
        Check(all.Count == 4 && closest > 1.8f, $"four companions stand apart behind you, the closest two {closest:0.0} m apart");
        foreach (var c in p.Companions().ToList()) c.QueueFree();
    }

    /// <summary>A refuge built up a step at a time: a tavern to make it home, a market, a council chamber with its council inside, walls; and every town's council in its chamber.</summary>
    async Task GrowthTest()
    {
        var (p, hud, city) = (main.PlayerNode, main.HudNode, main.Economy);
        var refuge = main.World.SettlementsNear(0, 0, 6000).Where(s => s.Kind == SettlementKind.AshbornCamp && !s.Hostile).MinBy(s => s.X * s.X + s.Z * s.Z);
        if (refuge is null) { Check(false, "a refuge to build up"); return; }
        await Teleport(new Vector3((float)refuge.X + 6, 0, (float)refuge.Z + 6));
        await Frames(60);
        Person Someone() => main.PlacesNode.People.First(x => x.Npc.HomeId == refuge.Id && !TownCouncils.IsMember(x.Npc.Id));
        Node3D? Built(string kind) => GetTree().GetNodesInGroup(Buildings.Group).OfType<Node3D>()
            .FirstOrDefault(n => IsInstanceValid(n) && n.HasMeta("kind") && (string)n.GetMeta("kind") == kind && refuge.DistanceTo(n.GlobalPosition.X, n.GlobalPosition.Z) < refuge.Radius + 25);
        main.Talk(Someone());
        Check(hud.Choose("What does") && hud.TalkText.Contains("a tavern"), $"{refuge.Name} has tents and a fire, and wants a tavern first");
        hud.EndTalk();
        for (var stage = 1; stage <= Settling.Walls; stage++)
        {
            var step = Settling.Next(city.GrownOf(refuge.Id))!;
            foreach (var (id, n) in step.Needs) p.Pack.Add(id, n);
            p.Crowns += step.Crowns;
            main.Talk(Someone());
            Check(hud.Choose("I've brought what") && city.GrownOf(refuge.Id) == stage, $"bring what {step.Name} takes, and {refuge.Name} raises it");
            await Frames(90);
            if (stage == Settling.Tavern)
                Check(Built("tavern") is not null && ImmortalSaga.Homely(refuge), "a tavern stands, and the refuge can be made a home at its bar");
            if (stage == Settling.Chamber)
            {
                Check(TownCouncils.Keeps(refuge) && main.CouncilChambers.ContainsValue(refuge.Id), "with its chamber it has a council");
                var seed = main.CouncilChambers.First(kv => kv.Value == refuge.Id).Key;
                main.GoInsideForTest("council", seed, p.GlobalPosition);
                await Frames(30);
                Check(main.CouncilFolkForTest.Count >= 3, $"and its councillors sit at the table inside ({main.CouncilFolkForTest.Count})");
                main.GoOutsideForTest();
                await Frames(30);
            }
        }
        Check(Settling.Next(city.GrownOf(refuge.Id)) is null, "walls and more homes, and it's a town");
        // Every town's council has its chamber.
        var town = main.World.SettlementsNear(0, 0, 3000).Where(s => s.Kind is SettlementKind.Town or SettlementKind.City && s.Id is not ("city:tristinwold" or "city:hollowheart")).MinBy(s => s.X * s.X + s.Z * s.Z)!;
        await Teleport(new Vector3((float)town.X + 30, 0, (float)town.Z + 30));
        await Frames(60);
        Check(main.CouncilChambers.ContainsValue(town.Id), $"{town.Name}'s council meets in its own chamber");
    }

    /// <summary>An apprentice learns their hero's class: an aeronaut's is a tinker with a little robot of their own, until they're trained another way.</summary>
    async Task PupilTest()
    {
        var p = main.PlayerNode;
        p.Join("EtherAeronaut");
        var (role, kind, _) = PartyRules.Follower(p.Class.Id);
        var lad = new Bond { Id = "test/wren", KindId = kind, Role = role, Trains = p.Class.Id, Name = "Wren", Level = 4, Race = "human", JoinAge = 14 };
        main.AddBondForTest(lad);
        var body = main.BringForTest(lad);
        await Frames(150);
        Check(body.Kind.Id == "tinker" && main.LittleRobotsForTest == 1, $"an aeronaut's apprentice is a tinker, with a little robot of their own ({body.Kind.Id}, {main.LittleRobotsForTest} robot)");
        Check(main.LittleRobotOfForTest(lad)?.Figure is Clockwork, $"and the little robot is a machine, not a person ({main.LittleRobotOfForTest(lad)?.Figure.GetType().Name})");
        Check(lad.Skills.Any(s => s.Name == "Spark Shot"), "who shoots sparks");
        main.RetrainForTest(lad, "MartialArtist");
        await Frames(150);
        var now = main.CompanionBodies.FirstOrDefault(e => e.Bond == lad);
        Check(now?.Kind.Id == "pupil" && main.LittleRobotsForTest == 0 && lad.Skills.Any(s => s.Name == "Palm Strike") && lad.Level == 4,
            $"trained another way, as a martial artist: a pupil now, the robot put away, the levels kept ({now?.Kind.Id})");
        main.RetrainForTest(lad, "Shapeshifter");
        await Frames(150);
        var beast = main.LittleRobotOfForTest(lad);
        Check(beast is { Kind.Id: "wolf" } && beast.Figure is not Figure, $"trained as a shapeshifter: an acolyte with a wolf at their heels, a real beast ({beast?.Kind.Id}, {beast?.Figure.GetType().Name})");
        foreach (var c in p.Companions().ToList()) c.QueueFree();
    }

    /// <summary>An apprentice set to keep shop aboard the hero's ship: goods put out sell a little at a time, the till's the hero's, the shop shuts while she sails and opens where she ties up.</summary>
    async Task KeeperTest()
    {
        var (p, sea, city) = (main.PlayerNode, main.Sea, main.Economy);
        (sea.OwnShip, sea.ShipName, sea.Hull, sea.Voyage) = (true, "Wren", 100, null);
        (sea.ShipX, sea.ShipZ) = (Main.ShipBerth.X, Main.ShipBerth.Z);
        await Teleport(new Vector3(138, 0, 0));
        await Frames(10);
        main.BoardVesselForTest();
        main.LeaveVesselForTest(); // she's drawn at her berth, and you're on the pier
        await Frames(10);
        var lad = new Bond { Id = "test/tobin", KindId = "apprentice", Role = BondRole.Apprentice, Name = "Tobin", Race = "human", JoinAge = 15, Following = false };
        main.AddBondForTest(lad);
        Check(main.KeepingOptionsForTest(lad).Any(o => o.Contains("aboard Wren")), "an apprentice can be set to keep shop aboard your ship");
        main.StartKeepingForTest(lad, Keeping.Ship);
        Check(lad.Keeps == Keeping.Ship && city.StoreOf(main.ShipShopKeyForTest) is { Ship: true }, "Tobin goes aboard and opens a shop on her deck");
        var keeper = main.KeeperForTest(lad);
        Check(keeper is not null && main.VesselNode is { } ship && ship.OnDeck(keeper.GlobalPosition), "and stands on her deck to mind it");
        p.Pack.Add("iron_ingot", 6);
        main.PutOutForTest(Keeping.Ship, "iron_ingot", 6);
        city.TendStores(Homestead.JobSeconds * 4 + 1);
        var store = city.StoreOf(main.ShipShopKeyForTest);
        Check(store is { House: var h } && h.Chest.GetValueOrDefault("iron_ingot") == 2 && h.Till == 4 * Homestead.SellsFor("iron_ingot"),
            $"the ingots sell one customer at a time ({store?.House.Chest.GetValueOrDefault("iron_ingot")} left, {store?.House.Till} crowns in the till)");
        if (keeper is not null)
        {
            var purse = p.Crowns;
            main.Talk(keeper);
            Check(main.HudNode.Choose("I'll take the takings") && p.Crowns == purse + 4 * Homestead.SellsFor("iron_ingot"), "and the takings are yours");
            main.HudNode.EndTalk();
        }
        main.BoardVesselForTest();
        Check(city.StoreOf(main.ShipShopKeyForTest) is null && main.KeeperForTest(lad) is null, "taking her out shuts the shop, and Tobin goes below");
        main.LeaveVesselForTest();
        Check(city.StoreOf(main.ShipShopKeyForTest) is { House.Chest: var c } && c.GetValueOrDefault("iron_ingot") == 2, "tied up again, the shop opens where she lies, the shelves as they were");
        main.StopKeepingForTest(lad);
        Check(lad.Keeps is null && city.StoreOf(main.ShipShopKeyForTest) is null, "and brought back, Tobin shuts up shop");
        foreach (var c2 in p.Companions().ToList()) c2.QueueFree();
    }

    /// <summary>A fleet: a second ship bought for it, tied up off the piers, crewed with hired hands, sent trading and home with crowns, and made the ship the hero sails.</summary>
    async Task FleetTest()
    {
        var (p, sea) = (main.PlayerNode, main.Sea);
        (sea.OwnShip, sea.ShipName, sea.Size, sea.Hull, sea.Voyage, sea.Hands) = (true, "Wren", ShipSize.Medium, 100, null, 0);
        sea.Fleet.Clear();
        await Teleport(new Vector3(130, 0, 5));
        await Frames(10);
        var f = main.BuyForFleetForTest(ShipSize.Small);
        Check(f is not null && sea.Fleet.Count == 1 && f.Name != sea.ShipName, $"a second ship of your own, for your fleet ({f?.Name})");
        if (f is null) return;
        main.TendFleetForTest();
        var drawn = main.FleetVesselForTest(f);
        Check(drawn is not null && drawn.GlobalPosition.DistanceTo(Main.FleetBerth(f.Berth) with { Y = drawn.GlobalPosition.Y }) < 0.5f, "she's tied up off the end of the piers");
        p.Crowns = 10000;
        var bonds = main.Bonds.Count;
        main.HireHandsForTest(f);
        main.TendFleetForTest();
        Check(f.Crewed && f.Hands == SeaBook.HandsFor(ShipSize.Small) && main.FleetVesselForTest(f) is { HandsAboard: 4 } && main.Bonds.Count == bonds,
            $"hands signed on for her stand about her deck, and they aren't your company ({f.Hands} hands)");
        var port = Seas.Ports.First(x => x.Id == "port:saltmere");
        SeaBook.SendOnRun(f, port, main.PlaySecondsForTest, new Tristinwold.Deviant.Rng(7));
        main.TendFleetForTest();
        Check(f.Away && main.FleetVesselForTest(f) is null, "sent trading, she casts off and is gone from her berth");
        var purse = p.Crowns;
        f.RunBack = main.PlaySecondsForTest - 1;
        main.TendFleetForTest();
        Check(!f.Away && p.Crowns > purse, $"and comes home with crowns ({p.Crowns - purse})");

        // An apprentice in command: sent orders, she sails, makes port and trades there.
        var mate = new Bond { Name = "Pell", Role = BondRole.Squire, Level = 6, Following = false };
        main.AddBondForTest(mate);
        main.GiveCommandForTest(f, mate);
        Check(f.Master == mate.Id && mate.Sails == f.Name && !mate.Following, "an apprentice takes command of her");
        SeaBook.StakeHer(f, 800);
        Check(sea.Order(f, ["port:greywater"], false, main.PlaySecondsForTest), "and is sent word to sail to Greywater");
        main.TendMastersForTest();
        main.TendFleetForTest();
        Check(f.Sailing && main.FleetVesselForTest(f) is null, $"she casts off ({string.Join("; ", f.Log)})");
        f.Arrives = main.PlaySecondsForTest - 1;
        main.TendMastersForTest();
        Check(f.At == "port:greywater" && sea.Markets.ContainsKey("port:greywater"), $"she makes port at Greywater and trades there ({string.Join("; ", f.Log)})");
        f.At = null;
        f.Plan.Clear();
        var had = p.Crowns;
        main.StandDownForTest(f);
        Check(!f.Mastered && mate.Sails is null && p.Crowns >= had, "brought ashore, her purse comes home with them");
        main.RemoveBondForTest(mate);
        main.HireHandsForTest(null);
        Check(sea.Hands == SeaBook.HandsFor(ShipSize.Medium) && sea.CrewSaves > 0.3, "hands for the ship you sail patch and pump while you fight: a third off what boarders do to her");
        main.MakeFlagshipForTest(f);
        Check(sea.ShipName == f.Name ? false : sea.Size == ShipSize.Small && f.Name == "Wren" && f.Size == ShipSize.Medium && sea.Hands == 4 && f.Hands == 8,
            $"any of the fleet can be made the ship you sail, and the old one ties up with her crew ({sea.ShipName} now, {f.Name} in the fleet)");
        sea.Fleet.Clear();
        main.TendFleetForTest();
        (sea.OwnShip, sea.Hands) = (false, 0);
    }

    /// <summary>Raiders in the street: the townsfolk run for a door or a guard and hide till it's over, and the guard comes to the hero's help.</summary>
    async Task PanicTest()
    {
        var p = main.PlayerNode;
        foreach (var c in p.Companions().ToList()) c.QueueFree();
        var place = main.World.SettlementsNear(0, 0, 3000).Where(s => s.Id != "city:tristinwold" && Warband.Guards(s) > 0).MinBy(s => s.X * s.X + s.Z * s.Z);
        if (place is null) { Check(false, "a town with guards of its own"); return; }
        await Teleport(new Vector3((float)place.X + 30, 0, (float)place.Z + 30));
        await Frames(60);
        var folk = main.PlacesNode.People.Where(x => !x.Captive && place.DistanceTo(x.GlobalPosition.X, x.GlobalPosition.Z) < place.Radius).ToList();
        if (folk.Count == 0) { Check(false, $"folk in {place.Name}"); return; }
        var who = folk.MinBy(x => x.GlobalPosition.DistanceTo(new Vector3((float)place.X, x.GlobalPosition.Y, (float)place.Z)))!;
        var stood = who.GlobalPosition;
        var at = stood + new Vector3(8, 0.5f, 0);
        var raider = Enemy.Create(Bestiary.Get("raider"), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(8));
        raider.Raider = true;
        raider.Roam = 3;
        main.AddEnemy(raider);
        Person.ResurveyForTest();
        await Until(() => who.Sheltering, 2);
        Check(who.Sheltering, $"{who.Npc.Name} sees a raider in the street and runs for it");
        var start = stood.DistanceTo(raider.GlobalPosition);
        await Frames(120);
        Check(who.GlobalPosition.DistanceTo(raider.GlobalPosition) > start + 3 && (who.Hidden || who.Sheltering),
            $"away from it, {(who.Hidden ? "and in at a door" : who.ShelteringIndoors ? "for a door" : "to a guard")} ({start:0} m to {who.GlobalPosition.DistanceTo(raider.GlobalPosition):0} m)");
        var near = folk.Where(x => IsInstanceValid(x) && x != who && x.GlobalPosition.DistanceTo(at) < Person.Scare - 4).ToList();
        var far = folk.Where(x => IsInstanceValid(x) && x.GlobalPosition.DistanceTo(at) > Person.Scare + 30).ToList();
        Check(near.All(x => x.Sheltering) && far.All(x => !x.Sheltering),
            $"and everyone else near it with them ({near.Count(x => x.Sheltering)} of {near.Count}), while folk further off carry on ({far.Count(x => !x.Sheltering)} of {far.Count})");
        var guards = main.PlacesNode.GuardsOf(place.Id).ToList();
        Check(guards.Any(g => IsInstanceValid(g) && g.Rival == raider), $"{place.Name}'s guards go for the raider");
        raider.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => !who.Sheltering && !who.Hidden && who.GlobalPosition.DistanceTo(stood) < 1, 30);
        Check(!who.Hidden && who.GlobalPosition.DistanceTo(stood) < 1, $"when it's quiet again {who.Npc.Name} comes back out and goes back to where they stood ({who.GlobalPosition.DistanceTo(stood):0.0} m off)");

        // Set on in the street, and the guard comes to help.
        var alive = guards.Where(g => IsInstanceValid(g) && g.State != EnemyState.Dead).ToList();
        if (alive.Count == 0) { Check(false, $"a guard left standing to help ({guards.Count} had been)"); return; }
        await Until(() => !p.Downed, 10); // if the raider got to you first
        p.Stats.Heal(1e6);
        alive = main.PlacesNode.GuardsOf(place.Id).Where(g => IsInstanceValid(g) && g.State != EnemyState.Dead).ToList(); // the town's own, as it stands now
        var thug = Enemy.Create(Bestiary.Get("bandit"), p.GlobalPosition + new Vector3(3, 0.5f, 0), Northwood.DangerFor(3));
        thug.Roam = 4;
        main.AddEnemy(thug);
        await Frames(3);
        thug.Provoke(p);
        var helped = false;
        await Until(() => { p.Stats.Heal(1e6); return helped = alive.Any(g => IsInstanceValid(g) && IsInstanceValid(thug) && g.Rival == thug); }, 4);
        Check(helped, $"a bandit sets on you in the street, and the guard comes to help you ({(IsInstanceValid(thug) ? thug.State.ToString() : "gone")})");
        if (IsInstanceValid(thug)) thug.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(10);
    }

    /// <summary>What heroes do shows in the world: a busy yard fills the bay, hot forges arm the guards, full plates raise more children.</summary>
    async Task HandiworkTest()
    {
        var city = main.Economy;
        main.TendBayForTest();
        var before = main.BayShipsShownForTest;
        Check(before == city.BayShips, $"the bay shows as many ships as the yard's kept busy for ({before})");
        for (var i = 0; i < 12; i++) main.DoCity(CityAction.Worked(CityEconomy.Capital, HandiKind.Ships));
        main.TendBayForTest();
        Check(main.BayShipsShownForTest == city.BayShips && city.BayShips > before, $"mend ships at the yard and more come to anchor ({before} to {main.BayShipsShownForTest})");
        for (var i = 0; i < 45; i++) main.DoCity(CityAction.Worked(CityEconomy.Capital, HandiKind.Smithing));
        Check(main.GuardLook(CityEconomy.Capital) == "guard_plate", "keep Tristinwold's forges hot and its Watch goes in plate");
        var g = Enemy.Create(Bestiary.Get("watchman"), main.PlayerNode.GlobalPosition + new Vector3(4, 0.5f, 0), look: main.GuardLook(CityEconomy.Capital));
        main.AddEnemy(g);
        var plain = g.Stats.MaxHealth;
        main.ArmGuard(g, CityEconomy.Capital);
        Check(g.Stats.MaxHealth > plain * 1.5, $"and harder to bring down for it ({plain:0} to {g.Stats.MaxHealth:0})");
        g.QueueFree();
        var town = main.World.SettlementsNear(0, 0, 3000).First(TownCouncils.Keeps);
        for (var i = 0; i < 24; i++) main.DoCity(CityAction.Worked(town.Id, HandiKind.Farming));
        var young = Places.Youngsters(town, city.FedYouths(town.Id)).ToList();
        Check(young.Count == 3 && young.All(People.IsYouth), $"bring in the harvests in {town.Name} and three more of its children grow up ({young.Count}, all youths: {young.All(People.IsYouth)})");
        Check(young.Select(y => y.Name).SequenceEqual(Places.Youngsters(town, 3).Select(y => y.Name)), "the same youngsters every time");
    }

    /// <summary>The Velvet Knives' job: Vesper's offer, the strongroom's door, the diamond, the pay; and a diamond broken on a foe.</summary>
    async Task HeistTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        await Until(() => !p.Downed, 40);
        var vesper = main.Locals.First(x => x.Npc.Id == "guild/vesper");
        await Teleport(vesper.GlobalPosition + new Vector3(1.5f, 0, 1.5f));
        await Frames(10);
        if (main.HeistForTest == 0)
        {
            main.Talk(vesper);
            Check(hud.Choose("Any work for a light pair of hands?") && hud.TalkText.Contains("Warrens") && hud.Choose("Take the job.") && main.HeistForTest == 1, "Vesper has a job: the diamond in the Warrens' storehouse");
            hud.EndTalk();
        }
        Check(main.HeistLinesForTest(p.GlobalPosition).Any(l => l.Contains("Warrens")), "and it's on your tracker");
        await Teleport(Main.OldStorehouseForTest * 1.07f);
        await Frames(20);
        var door = Buildings.DoorNear(p.GlobalPosition);
        main.GoInsideForTest("storehouse", door?.Seed ?? 31, door?.Step ?? p.GlobalPosition);
        await Frames(10);
        p.GlobalPosition = Indoors.StageAt + new Vector3(0, 0.4f, Indoors.VaultWall + 0.9f);
        await Frames(5);
        if (!main.VaultOpenForTest)
        {
            var open = main.VaultActionForTest(p.GlobalPosition);
            Check(open?.Verb.StartsWith("open the vault") == true, $"at the strongroom's door, the Knives' combination opens it ({open?.Verb ?? "nothing"})");
            open?.Act();
        }
        Check(main.VaultOpenForTest, "the iron door swings open");
        p.GlobalPosition = Main.Pedestal + new Vector3(0.9f, 0.4f, 0.9f);
        await Frames(5);
        var take = main.VaultActionForTest(p.GlobalPosition);
        if (take?.Verb == "take the Starfall Diamond") take.Value.Act();
        Check(p.Pack.Count(Main.Diamond) == 1, "the Starfall Diamond, off its pedestal and in your pack");
        main.GoOutsideForTest();
        await Frames(5);
        await Teleport(vesper.GlobalPosition + new Vector3(1.5f, 0, 1.5f));
        await Frames(10);
        var purse = p.Crowns;
        main.Talk(vesper);
        Check(hud.Choose("I have your diamond.") && p.Crowns == purse + 500 && p.Pack.Count(Main.Diamond) == 0 && main.HeistForTest == 2, "handed to Vesper: 500 crowns, and the job's done");
        hud.EndTalk();
        // Another diamond, kept and broken on a foe: the sky comes down on it, once.
        p.Pack.Add(Main.Diamond);
        var foe = Enemy.Create(Bestiary.Get("bandit"), p.GlobalPosition + new Vector3(0, 0.5f, -12));
        main.AddEnemy(foe);
        await Frames(5);
        var health = foe.Stats.Health;
        main.BreakDiamondForTest(foe.GlobalPosition);
        Check(p.Pack.Count(Main.Diamond) == 0 && main.MeteorsInFlightForTest > 0, "broken, the diamond's gone, and a stone falls out of the sky");
        await Frames(140);
        Check(!IsInstanceValid(foe) || foe.State == EnemyState.Dead || foe.Stats.Health < health, $"and it lands on the bandit ({(IsInstanceValid(foe) ? foe.Stats.Health : 0):0} of {health:0} left)");
        if (IsInstanceValid(foe)) foe.QueueFree();
    }

    /// <summary>A house's chest, unseen and seen: the haul and the practice, then the fine, the name and the price on your head.</summary>
    async Task StashTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40);
        await Teleport(City.PlayerStart);
        await Frames(10);
        main.GoInsideForTest("house", 4242, City.PlayerStart);
        await Frames(10);
        var room = main.IndoorsNode!;
        Check(room.Stash is not null, "a house has a chest worth going through");
        p.GlobalPosition = room.Stash!.Value + Vector3.Up * 0.4f;
        await Frames(5);
        var act = main.StashActionForTest(p.GlobalPosition);
        Check(act?.Verb == "search the chest (it isn't yours)", $"and E goes through it ({act?.Verb ?? "nothing"})");
        var (crowns, xp, infamy, bounty) = (p.Crowns, p.Pack.JobXp.GetValueOrDefault(Job.Thieving), main.Standing.Infamy, main.Standing.Bounty);
        main.StashCaughtForTest = false;
        act?.Act();
        Check(p.Pack.JobXp.GetValueOrDefault(Job.Thieving) > xp && main.Standing.Infamy == infamy && main.Standing.Bounty == bounty, $"unseen: the haul ({p.Crowns - crowns} crowns) and the practice, and nobody the wiser");
        Check(main.StashActionForTest(p.GlobalPosition)?.Verb.StartsWith("look in") == true, "and it's empty till it fills again");
        main.GoOutsideForTest();
        await Frames(5);
        main.GoInsideForTest("tavern", 4343, City.PlayerStart);
        await Frames(10);
        p.GlobalPosition = main.IndoorsNode!.Stash!.Value + Vector3.Up * 0.4f;
        await Frames(5);
        main.StashCaughtForTest = true;
        main.StashActionForTest(p.GlobalPosition)?.Act();
        Check(main.Standing.Infamy > infamy && main.Standing.Bounty >= bounty + 20, $"seen at the tavern's takings: a worse name (infamy {infamy} to {main.Standing.Infamy}) and a price on your head ({main.Standing.Bounty} crowns)");
        main.StashCaughtForTest = null;
        main.Standing.Bounty = bounty;
        main.GoOutsideForTest();
        await Frames(5);
    }

    /// <summary>A deviant at the head of a town's table: in the street with its band when you come, and the table yours when it falls.</summary>
    async Task WarlordsTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40);
        var city = main.Economy;
        var place = main.World.SettlementsNear(0, 0, 3000).Where(TownCouncils.Keeps).Where(s => s.Id != CityEconomy.Capital)
            .OrderBy(s => s.DistanceTo(0, 0)).First();
        var table = city.CouncilOf(place.Id);
        table.Fallen.UnionWith(table.Members.Keys); // it killed its way to the head of the table
        table.Heroes.Remove(p.Company);
        table.Warlord = new Warlord { Id = "roamer:selftest", Name = "Grask the Grinning", Kind = "bandit_chief", Level = 3, Against = p.Company, Rules = true };
        city.WarlordDay = city.Day; // nobody prunes it from the roster today
        await Teleport(new Vector3((float)place.X + 6, 0, (float)place.Z + 6));
        await Frames(20);
        main.TendWarlordSeatForTest();
        await Frames(5);
        var lord = main.WarlordForTest;
        Check(lord is not null && main.WarlordBandForTest == 4 && lord.Stats.Name.Contains("Lord of " + place.Name), $"in {place.Name}, Grask holds the street with its band ({lord?.Stats.Name ?? "nobody"}, {main.WarlordBandForTest} with it)");
        Check(main.WarlordLinesForTest(p.GlobalPosition).Any(l => l.Contains("YOUR RIVAL Grask")), "and it's on your tracker as your rival");
        for (var i = 0; i < 8 && lord is not null && lord.State != EnemyState.Dead; i++) { lord.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        var council = city.CouncilOf(place.Id);
        Check(council.Warlord is null && council.Ruler == p.Company && city.Unseated.Contains("roamer:selftest"),
            $"struck down: {place.Name} is free, and the head of its table is yours (ruler: {council.Ruler ?? "nobody"})");
        main.TendWarlordSeatForTest();
        Check(main.WarlordForTest is null && main.WarlordBandForTest == 0, "and its band is gone from the street");
    }

    /// <summary>A kerb is walked up; a wall isn't.</summary>
    async Task StepsTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40);
        async Task<(float Rise, float Went)> Walk(float high)
        {
            var at = new Vector3(-150, 0, -120);
            await Teleport(at);
            await Frames(20);
            var floor = p.GlobalPosition.Y;
            var block = new StaticBody3D { Position = p.GlobalPosition + new Vector3(0, high / 2 - 0.3f, -20.7f) }; // its near face 0.7 m ahead, and long enough to still be on at the end
            block.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, high + 0.6f, 40) } });
            main.AddChild(block);
            await Frames(3);
            var start = p.GlobalPosition;
            p.Rotation = Vector3.Zero; // facing the block: forward is -Z
            main.HudNode.EndTalk();
            Input.ActionPress("move_forward");
            for (var i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ActionRelease("move_forward");
            var (rise, went) = (p.GlobalPosition.Y - floor, start.Z - p.GlobalPosition.Z);
            block.QueueFree();
            p.Velocity = Vector3.Zero;
            return (rise, went);
        }
        var (kerbRise, kerbWent) = await Walk(0.3f);
        Check(kerbRise > 0.2f && kerbWent > 2.5f, $"a knee-high step is walked up, not jumped (up {kerbRise:0.00} m, on {kerbWent:0.0} m)");
        var (wallRise, wallWent) = await Walk(1.0f);
        Check(wallRise < 0.3f && wallWent < 2.2f, $"a wall chest-high stops you (up {wallRise:0.00} m, on {wallWent:0.0} m)");
    }

    /// <summary>Korrin's empire: the blockade's prices, the fences, the depots and their captains, and the runs that break it.</summary>
    async Task SyndicateTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40); // the ring brings them back first, if the last test put them down
        var hud = main.HudNode;
        var city = main.Economy;
        var item = Items.Get("stamina_brew")!;
        var before = main.ShopPriceFor(item);
        main.DoCity(CityAction.KorrinFreed(p.Company));
        Check(city.KorrinInfluence == Syndicate.StartInfluence && Math.Abs(city.KorrinMarkup - 2.6) < 0.01 && main.ShopPriceFor(item) > before * 2.4,
            $"Korrin out: his influence is {city.KorrinInfluence}%, and a stamina brew goes from {before} to {main.ShopPriceFor(item)} crowns");
        await Teleport(new Vector3(60, 0, 10));
        await Frames(10);
        main.TendSyndicateForTest();
        await Frames(5);
        Check(main.FencesForTest.Count == 2, "fences in the city's alleys sell what the stalls can't");
        main.Talk(main.FencesForTest[0]);
        Check(hud.Choose("I can't pay") && hud.Choose("Do it") && main.Standing.Bounty >= 25, "or a favour instead: a window broken, and a price on your head");
        main.Standing.Bounty = 0;
        hud.EndTalk();

        // A depot: the captain bought, and his ground is safe.
        var (bid, bname, bat) = Main.DepotsForTest[0];
        await Teleport(bat + new Vector3(30, 0, 0));
        await Frames(20);
        main.TendSyndicateForTest();
        await Frames(5);
        Check(main.CaptainForTest is not null && main.DepotGuardForTest.Count == 4, $"{bname}: a captain and four guards in black and violet");
        p.Crowns += 5000;
        if (main.CaptainForTest is { } cap) main.Talk(cap);
        Check(hud.Choose("Make it worth") && city.CaptainsBought.Contains(bid) && main.DepotGuardForTest.Count == 0, "bought: his men stay off you");
        hud.EndTalk();

        // Another: the captain killed, his men break; the depot burned, and assassins on the way.
        var (did, dname, dat) = Main.DepotsForTest[1];
        await Teleport(dat + new Vector3(30, 0, 0));
        await Frames(20);
        main.TendSyndicateForTest();
        await Frames(5);
        if (main.CaptainForTest is { } captain) main.Talk(captain);
        Check(hud.Choose("Draw on him") && main.CaptainBodyForTest is not null, $"at {dname} you can draw on the captain (captain {main.CaptainForTest?.Npc.Name ?? "none"}; {string.Join(" | ", hud.Options)})");
        var guards = main.DepotGuardForTest.ToList();
        var cb = main.CaptainBodyForTest;
        for (var i = 0; i < 8 && cb is not null && cb.State != EnemyState.Dead; i++) { cb.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(420);
        Check(guards.Count == 4 && guards.All(g => !IsInstanceValid(g) || g.State == EnemyState.Dead), "the captain falls and his men break and run");
        var fire = main.SyndicateActionForTest(dat + new Vector3(1, 0, 1));
        Check(fire?.Verb == "set the depot alight", "with nobody left, you can burn it");
        fire?.Act();
        Check(city.DepotsBurned.Contains(did) && city.KorrinInfluence == Syndicate.StartInfluence - Syndicate.PerDepot && main.AssassinsComingForTest, $"burned: his influence falls to {city.KorrinInfluence}%, and he sends assassins");

        // Food smuggled in past him, and a councillor hanged for it.
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        await Frames(10);
        main.Talk(main.Locals.First(x => x.Npc.Id == "tristinwold/caravans"));
        Check(hud.Choose("Is there any way to get food in") && main.SmuggleRunForTest == 1, "Garrick has a smuggling run");
        hud.EndTalk();
        await Teleport(new Vector3(-520, 0, 172));
        await Frames(20);
        main.SyndicateActionForTest(new Vector3(-520, 0, 170))?.Act();
        Check(main.SmuggleRunForTest == 2, "you take the food from the old sheepfold, and the Syndicate jumps you");
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        await Frames(10);
        var councillors = city.CouncilOf("city:tristinwold").Living.Count();
        var influence = city.KorrinInfluence;
        main.Talk(main.Locals.First(x => x.Npc.Id == "tristinwold/caravans"));
        Check(hud.Choose("I've brought the food") && city.KorrinInfluence == influence - Syndicate.PerSmuggle && city.FedUntil > city.Day, "the food gets in: his influence falls, and the starving merchants open their good stock");
        Check(city.CouncilOf("city:tristinwold").Living.Count() < councillors || councillors == 0, "and Korrin hangs a councillor for it");
        hud.EndTalk();
        main.DoCity(CityAction.KorrinFell(p.Company, true));
        Check(city.KorrinInfluence == 0 && city.KorrinMarkup == 1 && city.DepotsBurned.Count == 0, "back in the tank: his influence gone, prices back, the depots forgotten");
    }

    /// <summary>The Deep Trenches under the mines, the Dark Depths under them, and the demons coming up.</summary>
    async Task DeepsTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40); // the ring brings them back first, if the last test put them down
        var hud = main.HudNode;
        var city = main.Economy;
        var home = Deeps.Get(main.World, 0, 0);
        var near = Deeps.Around(main.World, 0, 0, 1).ToList();
        Check(home.Home && near.Count(t => t.Mines.Count > 0) >= 2 && near.Sum(t => t.Mines.Count) >= 4,
            $"under the mines, the Deep Trenches: {string.Join("; ", near.Take(4).Select(t => $"{t.City} under {t.Mines.Count} mines"))}");
        var trench = near.First(t => t.Mines.Count > 0);
        await Teleport(new Vector3(-150, 0, -120));
        await Frames(10);
        main.GoDeepForTest("trench", trench);
        await Frames(20);
        Check(main.IndoorsNode?.Kind == "trench" && main.DeepFolkForTest.Count == 5 && main.DeepFolkForTest.All(f => f.Npc.Race == "dwarf"), $"down in {trench.City}, the dwarves of the deep");
        main.Talk(main.DeepFolkForTest.First(f => f.Npc.Id.EndsWith(":thane")));
        Check(hud.Choose("Where do the dwarves come from") && hud.TalkText.Contains("came up out of these roads"), "the thane tells you every dwarf came up from here");
        hud.EndTalk();
        var act = main.DeepActionForTest(main.DeepRoadSignForTest);
        act?.Act();
        Check(act?.Verb.StartsWith("take the deep road") == true && hud.Options.Count(o => o.StartsWith("Up to")) == trench.Mines.Count + (trench.Home ? 1 : 0), $"the deep road climbs to every mine over it ({string.Join(", ", hud.Options)})");
        hud.EndTalk();
        act = main.DeepActionForTest(Indoors.StageAt + new Vector3(0, 0, -110 / 2f + 2.5f));
        Check(act?.Verb.Contains("Dark Depths") == true, "and one tunnel goes down");
        act?.Act();
        await Frames(20);
        var lord = main.DuragonForTest;
        Check(main.IndoorsNode?.Kind == "depths" && lord is not null && lord.Stats.Name.Contains("level 25"), $"the Dark Depths: the Rift, and Duragon by it ({lord?.Stats.Name})");
        act = main.DeepActionForTest(main.DeepRoadSignForTest);
        act?.Act();
        Check(hud.Options.Count(o => o.StartsWith("Up to")) >= 3, $"and ways up to trenches all over the world ({hud.Options.Count() - 1})");
        hud.EndTalk();
        main.GoOutsideForTest();
        await Frames(10);

        // A raid: up into a dwarf city first.
        (city.Demons, city.NextRaid) = (null, city.Day);
        await Teleport(new Vector3(-150, 0, -120));
        main.BeginRaidForTest();
        var r = city.Demons;
        Check(r is not null && !r.Surfaced && city.News.Last().Contains(r.City), $"demons raid upward: {city.News.Last()}");
        if (r is null) return;
        main.GoDeepForTest("trench", Deeps.Parse(main.World, r.Trench));
        await Frames(20);
        var demons = main.DeepFoesForTest.Where(e => e.Faction == "hell").ToList();
        Check(demons.Count == 7 && main.DeepFoesForTest.Count(e => e.Allied) == 3, "the demons are in the streets, and the dwarves are holding");
        foreach (var e in demons) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(10);
        main.TendDeepsForTest();
        Check(city.Demons is null, $"kill them all and the raid is broken ({city.News.Last()})");
        main.GoOutsideForTest();
        await Frames(10);

        // Another, left alone: out of a mine a day later, and heaven's answer.
        city.NextRaid = city.Day;
        main.BeginRaidForTest();
        r = city.Demons;
        if (r is null) { Check(false, "a second raid"); return; }
        r.Started = city.Day - 1;
        city.TendRaid();
        Check(r.Surfaced, $"left alone, they burst out of {r.MineName} the next day");
        await Teleport(new Vector3((float)r.X + 30, 0, (float)r.Z));
        await Frames(30);
        main.TendDeepsForTest();
        await Frames(10);
        Check(main.RaidBandForTest.Count(e => e.Faction == "hell") == 8, $"on the surface by {r.MineName}, a band of demons");
        main.HeavenNowForTest();
        await Frames(10);
        Check(main.RaidBandForTest.Count(e => e.Faction == "heaven" && e.Foe == "hell") == 5, "the gates of heaven open, and the host comes down on them");
        foreach (var e in main.RaidBandForTest.Where(e => e.Faction == "hell").ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(10);
        main.TendDeepsForTest();
        Check(city.Demons is null, "and when the demons are dead, it's over");
        Check(DemonRaids.HeavenPercent == 30 && Enumerable.Range(0, 400).Count(i => DemonRaids.HeavenAnswers($"raid-{i}")) is > 80 and < 160, "heaven answers about three raids in ten");
        p.Stats.Heal(1e9);
    }

    /// <summary>Korrin let out of his tank, the city shut, his two forms, and back in the tank; Ronacse at the breach, his meteors, and his true face.</summary>
    async Task UndyingTest()
    {
        var p = main.PlayerNode;
        await Until(() => !p.Downed, 40); // the ring brings them back first, if the last test put them down
        var hud = main.HudNode;
        var city = main.Economy;
        Check(main.TankActionForTest(Main.KorrinTankForTest + new Vector3(0, 0, 2.6f)) is null, "nothing on any street gives the tank away");
        await Teleport(Main.OldStorehouseForTest * 1.07f); // round the back, between the old storehouse and the wall
        await Frames(20);
        var door = Buildings.DoorNear(p.GlobalPosition);
        Check(door?.Kind == "storehouse", $"at the back of the Warrens, an old storehouse with its door to the wall ({door?.Kind ?? "no door"})");
        main.GoInsideForTest("storehouse", door?.Seed ?? 31, door?.Step ?? p.GlobalPosition);
        await Frames(10);
        Check(main.TankActionForTest(Main.KorrinTankForTest + new Vector3(0, 0, 2.6f)) is null || main.VaultOpenForTest, "the tank's behind the strongroom's iron door");
        if (!main.VaultOpenForTest) main.OpenVaultForTest();
        p.GlobalPosition = Main.KorrinTankForTest + new Vector3(0, 0.4f, 1.6f);
        await Frames(5);
        main.TendUndyingForTest();
        var act = main.TankActionForTest(p.GlobalPosition);
        Check(act?.Verb == "look at the tank" && !city.KorrinFree, $"inside, a tank with someone asleep in it ({act?.Verb})");
        act?.Act();
        Check(hud.TalkText.Contains("SUBJECT ONE") && !hud.TalkText.Contains("evil"), "the plate says only SUBJECT ONE: nothing says what he is");
        Check(hud.Choose("Pull the lever") && hud.Choose("Pull it") && city.KorrinFree && city.KorrinHolds && city.TradeShut("city:tristinwold"), "pull the lever, and he's out, and Tristinwold is his");
        main.GoOutsideForTest();
        await Frames(5);
        main.Talk(main.Locals.First(x => x.Npc.Id == Shops.Fishmonger.Id));
        Check(hud.Choose("Are you open") && !hud.Options.Any(o => o.StartsWith("Let me see")), $"every stall is shut ({hud.TalkText})");
        hud.EndTalk();

        await Teleport(Main.KorrinSeatForTest + new Vector3(0, 0, 30));
        await Frames(20);
        main.TendUndyingForTest();
        await Frames(10);
        var k = main.KorrinForTest;
        Check(k is not null && main.KorrinCourtForTest == 6 && k.Stats.Name.Contains("(level 10)"), $"he holds court on the dock road at level ten, with six thugs round him ({k?.Stats.Name})");
        if (k is null) return;
        foreach (var e in main.Enemies().Where(e => e.Stats.Name.StartsWith("Korrin's")).ToList()) e.QueueFree();
        for (var i = 0; i < 8 && k.State != EnemyState.Dead; i++) { k.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        var k2 = main.KorrinForTest;
        Check(k2 is not null && k2 != k && main.KorrinFinalForTest && k2.Stats.Health >= k2.Stats.MaxHealth * 0.99, "put down, he gets up again in his second form, whole");
        if (k2 is null) return;
        for (var i = 0; i < 8 && k2.State != EnemyState.Dead; i++) { k2.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        Check(hud.Choose("Drag him back") && !city.KorrinFree && !city.TradeShut("city:tristinwold") && city.KorrinDeaths == 1, "beaten twice, he can be dragged back into the tank, and the stalls open");

        // Ronacse: a paladin in gold by the breach, until he isn't.
        var vigil = main.PrideVigilForTest;
        await Teleport(vigil + new Vector3(6, 0, 6));
        await Frames(20);
        main.TendUndyingForTest();
        await Frames(10);
        var pride = main.PrideForTest;
        Check(pride is not null && pride.Npc.Name == "Ronacse" && pride.Npc.Occupation.Contains("paladin"), "by the breach, a paladin in gold keeps his vigil");
        if (pride is null) return;
        var wasLevel = p.Hero.Level;
        p.Hero.Level = Math.Max(wasLevel, 30); // seasoned enough to stand a meteor and see his second face
        p.Fit();
        p.Stats.Heal(1e9);
        main.Talk(pride);
        Check(hud.Choose("I challenge you"), "you can challenge him");
        await Frames(10);
        var r = main.PrideBodyForTest;
        Check(r is not null && r.Stats.Name.Contains("Pride") && r.Stats.Name.Contains("(level 10)"), $"he fights you: {r?.Stats.Name}");
        if (r is null) return;
        p.Stats.Heal(1e9);
        var before = p.Stats.Health;
        for (var i = 0; i < 40 && main.MeteorsForTest == 0; i++) await Frames(10);
        Check(main.MeteorsForTest > 0, "he calls a meteor down on you");
        for (var i = 0; i < 40 && main.MeteorsForTest > 0; i++) await Frames(5);
        Check(p.Stats.Health < before, $"and it lands ({before - p.Stats.Health:0} harm)");
        p.Stats.Heal(1e9);
        for (var i = 0; i < 8 && r.State != EnemyState.Dead; i++) { r.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        var r2 = main.PrideBodyForTest;
        Check(r2 is not null && r2 != r && main.PrideUnboundForTest && r2.Stats.Health >= r2.Stats.MaxHealth * 0.99, "the gold falls away: Pride Unbound, whole again, with the sun at his back");
        if (r2 is null) return;
        for (var i = 0; i < 8 && r2.State != EnemyState.Dead; i++) { r2.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        Check(city.PrideDeaths == 1 && city.PrideDown && city.PrideLevel == Drifters.LeastLevel + 1, "brought down, he's gone till tomorrow, and a level the stronger for it");
        (city.PrideDeaths, city.PrideBack, city.KorrinDeaths) = (0, 0, 0);
        p.Hero.Level = wasLevel;
        p.Fit();
    }

    /// <summary>The drifters: at their camps, talking, training, hired to guard and sent against a town, fought to the death and back.</summary>
    async Task DriftersTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var city = main.Economy;
        city.Hires.Clear();
        var spot = main.DrifterSpotForTest("gornok");
        await Teleport(spot + new Vector3(8, 0, 6));
        await Frames(20);
        main.PlaceDriftersForTest();
        await Frames(10);
        var gornok = main.DrifterForTest("gornok");
        Check(gornok is not null && gornok.Npc.Name == "Gornok Thornek", $"Gornok Thornek keeps the ruined shrine on the south road ({spot.X:0}, {spot.Z:0})");
        if (gornok is null) return;
        var said = main.DrifterTalkForTest(gornok).Select(o => o.Item1).ToList();
        Check(said.Any(t => t.StartsWith("Will you train")) && said.Any(t => t.StartsWith("I have work")) && said.Any(t => t.StartsWith("Spar")) && said.Any(t => t.StartsWith("Fight me")) && said.Any(t => t.StartsWith("Is there anything") || t.StartsWith("Give Gornok")),
            $"he'll talk, train you, take work, want something, spar or fight ({said.Count} things to say)");
        var g = Drifters.Get("gornok")!;
        Check(Drifters.Refuses(g, HireKind.Town, ReputationBook.Capital, p.Company, city.Outlaws) is not null && Drifters.Refuses(g, HireKind.Hero, "Somebody Honest", p.Company, city.Outlaws) is not null
              && Drifters.Refuses(g, HireKind.GuardCaravan, p.Company, p.Company, city.Outlaws) is null && Drifters.Refuses(Drifters.Get("broly")!, HireKind.Town, ReputationBook.Capital, p.Company, city.Outlaws) is null,
            "a good one guards your wagon but won't fall on a town or hunt an honest hero; Broly will do anything");

        // Hired to guard the wagon: when the bandits come, he's there, on your side.
        main.DoCity(CityAction.Hired(new Hire { Id = "test-guard", Drifter = "gornok", Kind = HireKind.GuardCaravan, Target = p.Company, TargetName = p.Company, By = p.Company }));
        Check(city.HireOf("gornok")?.Id == "test-guard" && city.HireOf("gornok")!.Ends == city.Day + 2, "hired to guard your wagon, for two days");
        Check(main.DrifterGuards(HireKind.GuardCaravan, p.GlobalPosition + new Vector3(3, 0, 3)) && main.DrifterFoeForTest is { Friendly: true }, "when bandits come for the wagon he's there, on your side, not in your party");
        main.DrifterFoeForTest?.QueueFree();
        await Frames(5);
        main.TendDriftersForTest();
        city.Hires.Clear();

        // Fought to the death: level ten at the least, and back tomorrow, a level up.
        main.Talk(gornok);
        Check(hud.Choose("Fight me") && hud.Choose("Draw"), "you can call him out, to the death");
        await Frames(10);
        var foe = main.DrifterFoeForTest;
        Check(foe is not null && foe.Stats.Name.Contains("(level 10)") && city.DrifterLevel("gornok") == Drifters.LeastLevel, $"he fights at level ten from the first ({foe?.Stats.Name})");
        if (foe is null) return;
        for (var i = 0; i < 8 && foe.State != EnemyState.Dead; i++) { foe.TakeHit(foe.Stats.MaxHealth * 2, DamageKind.Sun, AttackKind.Spell, false, p); await Frames(3); }
        await Frames(10);
        Check(city.DrifterDown("gornok") && city.DrifterDeaths.GetValueOrDefault("gornok") == 1 && city.DrifterLevel("gornok") == Drifters.LeastLevel + 1, "killed, he's gone until tomorrow, and comes back a level stronger");
        main.PlaceDriftersForTest();
        await Frames(5);
        Check(main.DrifterForTest("gornok") is null, "and his shrine stands empty meanwhile");
        city.DrifterBack.Clear();

        // Irohe, paid to fall on Tristinwold: met in its streets, and at the end of his strength, fire.
        main.DoCity(CityAction.Hired(new Hire { Id = "test-town", Drifter = "irohe", Kind = HireKind.Town, Target = ReputationBook.Capital, TargetName = "Tristinwold", By = "Someone Else" }));
        Check(city.HireOf("irohe") is not null && city.News.Last().Contains("Irohe"), $"Irohe hired to fall on Tristinwold, and the roads hear of it ({city.News.Last()})");
        await Teleport(new Vector3(-30, 0, 20));
        await Frames(20);
        for (var i = 0; i < 5 && main.DrifterFoeForTest is null; i++) { main.TendDriftersForTest(); await Frames(5); }
        var irohe = main.DrifterFoeForTest;
        Check(irohe is not null && irohe.Stats.Name.StartsWith("Irohe"), "in the city's streets, he comes for you");
        if (irohe is not null)
        {
            irohe.TakeHit(irohe.Stats.MaxHealth * 0.5, DamageKind.Sun, AttackKind.Spell, false, p);
            await Frames(3);
            main.TendDriftersForTest();
            await Frames(10);
            var serpents = main.Enemies().Count(e => e.Kind.Id == "flame_serpent");
            Check(serpents == 2 && irohe.Scale.X > 1.2f, $"hurt, he becomes a giant of fire and calls up {serpents} burning serpents");
            for (var i = 0; i < 8 && !p.Downed; i++) { p.TakeHit(p.Stats.MaxHealth * 3, DamageKind.Fire, AttackKind.Spell, false, irohe); await Frames(3); }
            await Frames(20);
            Check(city.HireOf("irohe") is null && city.DrifterWins.GetValueOrDefault("irohe") == 1 && city.DrifterLevel("irohe") == Drifters.LeastLevel + 2, "he beats you: the job's done, and he's two levels the stronger for it");
            await Frames(120);
        }
        city.Hires.Clear();
        (city.DrifterDeaths, city.DrifterWins, city.DrifterBack) = ([], [], []);
    }

    /// <summary>The hero's own ship taken out from the berth and steered like a wagon; and a colony on an island nobody lives on.</summary>
    async Task OpenSeaTest()
    {
        var (p, sea, city) = (main.PlayerNode, main.Sea, main.Economy);
        (sea.OwnShip, sea.ShipName, sea.Hull, sea.Voyage) = (true, "Wren", 100, null);
        (sea.ShipX, sea.ShipZ) = (Main.ShipBerth.X, Main.ShipBerth.Z);
        await Teleport(new Vector3(138, 0, 0));
        await Frames(10);
        main.AdoptForTest("wolf"); // a companion along for the voyage
        await Frames(20);
        main.BoardVesselForTest();
        var ship = main.VesselNode;
        Check(ship is not null && main.AboardShip, "you take your own ship out from her berth");
        if (ship is null) return;
        // Let go of the wheel and walk about her deck.
        main.LeaveWheelForTest();
        await Frames(40);
        Check(!main.AboardShip && p.Seat is null && ship.OnDeck(p.GlobalPosition) && p.GlobalPosition.Y > ship.GlobalPosition.Y + OwnVessel.QuarterTop - 0.3f,
            $"let go of the wheel and you're standing on her quarterdeck ({p.GlobalPosition.Y - ship.GlobalPosition.Y:0.0} m above her waterline)");
        main.GoAboardForTest();
        p.Rotation = new Vector3(0, ship.Rotation.Y, 0);
        await Frames(20);
        var midships = p.GlobalPosition;
        Input.ActionPress("move_forward");
        await Frames(50);
        Input.ActionRelease("move_forward");
        await Frames(20);
        Check(ship.OnDeck(p.GlobalPosition) && p.GlobalPosition.DistanceTo(midships) > 1.0f && Mathf.Abs(p.GlobalPosition.Y - midships.Y) < 0.6f,
            $"walk about her deck, the planks underfoot ({p.GlobalPosition.DistanceTo(midships):0.0} m, still aboard: {ship.OnDeck(p.GlobalPosition)})");
        p.GlobalPosition = ship.BesideWheel;
        await Frames(10);
        Check(main.VesselVerbForTest == "take the wheel", $"up at the wheel, E takes it ({main.VesselVerbForTest})");
        main.BoardVesselForTest();
        Check(main.AboardShip && p.Seat is not null, "and you're at the helm again");
        await Frames(10);
        var crew = main.CrewForTest.ToList();
        Check(crew.Count > 0 && crew.All(c => ship.OnDeck(c.GlobalPosition)), $"your company rides on her deck ({crew.Count} aboard)");
        main.LeaveVesselForTest();
        Check(!main.AboardShip && City.OnPier(p.GlobalPosition), "and can step off onto the pier");
        main.BoardVesselForTest();
        var from = ship.GlobalPosition;
        ship.Rotation = new Vector3(0, -Mathf.Pi / 2, 0); // her bow out to sea
        Input.ActionPress("move_forward");
        await Frames(240);
        Input.ActionRelease("move_forward");
        Check(ship.GlobalPosition.DistanceTo(from) > 20, $"W drives her across the water ({ship.GlobalPosition.DistanceTo(from):0} m)");
        Check(main.CrewForTest.All(c => ship.OnDeck(c.GlobalPosition)), "and her crew are still on her deck, not swimming after her");
        // Left out at sea, then traded in for another hull: the new one's at the berth, and on the map.
        main.LeaveWheelForTest();
        var traded = Enum.GetValues<ShipSize>().First(s => s != sea.Size);
        main.BuyHullForTest(traded);
        await Frames(10);
        Check(sea.Size == traded && new Vector2((float)sea.ShipX - Main.ShipBerth.X, (float)sea.ShipZ - Main.ShipBerth.Z).Length() < 1 && main.BelongingsForTest.Any(b => b.Kind == "ship" && Math.Abs(b.X - Main.ShipBerth.X) < 1),
            $"traded in for a {traded}, she's waiting at the berth by the middle pier, and on the map");
        await Teleport(new Vector3(138, 0, 0));
        await Frames(10);
        main.BoardVesselForTest();
        ship = main.VesselNode!;
        ship.Rotation = new Vector3(0, -Mathf.Pi / 2, 0);
        Input.ActionPress("move_forward");
        await Frames(240);
        Input.ActionRelease("move_forward");
        var heading = ship.Rotation.Y;
        Input.ActionPress("move_left");
        await Frames(60);
        Input.ActionRelease("move_left");
        Check(Mathf.Abs(ship.Rotation.Y - heading) > 0.3f, "and A and D bring her round, wherever you want her");
        main.LeaveVesselForTest();
        Check(main.AboardShip, "out on open water there's nowhere to step off");
        (sea.Steam, sea.Flies, sea.Fuel) = (true, true, 600);
        var sea0 = ship.GlobalPosition.Y;
        Input.ActionPress("jump");
        await Frames(120);
        Input.ActionRelease("jump");
        Check(ship.GlobalPosition.Y > sea0 + 5 && sea.Fuel < 600, $"a ship with a lift goes up off the water ({ship.GlobalPosition.Y - sea0:0} m) and burns her fuel");
        Input.ActionPress("descend");
        for (var i = 0; i < 30 && ship.GlobalPosition.Y > OwnVessel.Floor(ship.GlobalPosition) + 0.3f; i++) await Frames(10);
        Input.ActionRelease("descend");
        Check(ship.GlobalPosition.Y < sea0 + 1, "and comes back down onto the sea");
        (sea.Steam, sea.Flies) = (false, false);

        // Open country, as far from any town as Tristinwold is from its neighbours.
        Check(main.NoRoomForColonyForTest(new Vector3(300, 0, -300)) is not null, $"no colony next door to Tristinwold: it wants {main.ColonySpacingForTest:0} m of room");
        Vector3? isle = null;
        for (var x = -5400; x <= 5400 && isle is null; x += 600)
            for (var z = -5400; z <= 5400 && isle is null; z += 600)
                if (main.NoRoomForColonyForTest(new Vector3(x, 0, z)) is null) isle = new Vector3(x, 0, z);
        Check(isle is not null, $"there's open country out there with room for a colony ({isle})");
        if (isle is null) return;
        p.StandUp(isle.Value);
        await Teleport(isle.Value);
        await Frames(20);
        while (p.Hero.Level < 5) main.GainXp(Hero.XpToNext(p.Hero.Level));
        p.Crowns += 1000;
        main.FoundColonyForTest();
        var colony = city.HallOf(p.Company, HallKind.Colony);
        Check(colony is { Members: 1 } && city.CouncilOf(Halls.ColonyTown(p.Company)).Heroes.Contains(p.Company), $"a hero can found a colony there, and sits on its council ({colony?.Name})");
        city.TendHalls(Halls.JobSeconds * 4);
        Check(colony is { Till: > 0 } && colony.Chest.Values.Sum() > 0, $"and its settlers fish, cut and quarry while you're away ({string.Join(", ", colony?.Chest.Select(kv => $"{kv.Value} {kv.Key}") ?? [])})");

        // Grown all the way to a walled town, its buildings still keep out of each other's way and inside its fence.
        if (colony is not null)
        {
            var wasGrown = city.GrownOf(colony.Town);
            city.Grown[colony.Town] = Settling.Walls;
            main.RedrawHallsForTest();
            await Frames(5);
            var heart = new Vector3((float)colony.X, 0, (float)colony.Z);
            var near = GetTree().GetNodesInGroup(Buildings.Group).OfType<Node3D>().Where(n => IsInstanceValid(n) && n.IsInsideTree() && new Vector2(n.GlobalPosition.X - heart.X, n.GlobalPosition.Z - heart.Z).Length() < 70).ToList();
            var clash = near.SelectMany((a, i) => near.Skip(i + 1).Where(b => Buildings.Overlap(Buildings.Footprint(a), Buildings.Footprint(b))).Select(b => $"{Buildings.KindOf(a)} and {Buildings.KindOf(b)}")).ToList();
            Check(near.Count >= 10 && clash.Count == 0, $"a grown colony's {near.Count} buildings don't run into each other{(clash.Count > 0 ? $" ({string.Join("; ", clash)})" : "")}");
            var stallAt = new Vector3(heart.X, 0, heart.Z) + (main.ColonyStallSpotForTest(colony) - heart) with { Y = 0 };
            Check(near.All(n => Buildings.Footprint(n).All(f => f.Centre.DistanceTo(new Vector2(stallAt.X, stallAt.Z)) > Mathf.Max(f.HalfU, f.HalfV) + 2)), "and leave room in the yard for its market stall");
            city.Grown[colony.Town] = wasGrown;
            main.RedrawHallsForTest();
        }

        // An apprentice sent to keep a market stall in the colony's yard: customers come to it, as to a shop at home.
        var lad = new Bond { Id = "test/wyn", KindId = "apprentice", Role = BondRole.Apprentice, Name = "Wyn", Race = "human", JoinAge = 15, Following = false };
        main.AddBondForTest(lad);
        Check(main.KeepingOptionsForTest(lad).Any(o => o.Contains("market stall")), "an apprentice can be set to keep a market stall at your colony");
        main.StartKeepingForTest(lad, Keeping.Colony);
        var keeper = main.KeeperForTest(lad);
        Check(city.StoreOf(main.ColonyShopKeyForTest) is { Stall: true } && keeper is not null && main.ColonyStallDrawnForTest
            && main.ColonyStallForTest is { } stall && keeper.GlobalPosition.DistanceTo(stall) < 4, $"Wyn sets up the stall in the colony's yard and stands at it (store {city.StoreOf(main.ColonyShopKeyForTest) is { Stall: true, Open: true }}, keeper {keeper?.GlobalPosition}, stall drawn {main.ColonyStallDrawnForTest} at {main.ColonyStallForTest})");
        p.Pack.Add("pine_log", 5);
        main.PutOutForTest(Keeping.Colony, "pine_log", 5);
        city.TendStores(Homestead.JobSeconds * 3 + 1);
        Check(city.StoreOf(main.ColonyShopKeyForTest) is { House: var stallHouse } && stallHouse.Chest.GetValueOrDefault("pine_log") == 2 && stallHouse.Till == 3 * Homestead.SellsFor("pine_log"),
            "and the colony's customers buy off it, one at a time, while you're away");
        main.StopKeepingForTest(lad);
        Check(city.StoreOf(main.ColonyShopKeyForTest) is null && !main.ColonyStallDrawnForTest, "brought back, Wyn packs the stall away");

        // A second colony, out in other open country: a hero may found as many as they can find room for.
        Vector3? second = null;
        for (var x = 5400; x >= -5400 && second is null; x -= 600)
            for (var z = 5400; z >= -5400 && second is null; z -= 600)
                if (main.NoRoomForColonyForTest(new Vector3(x, 0, z)) is null) second = new Vector3(x, 0, z);
        if (second is { } far)
        {
            p.StandUp(far);
            await Teleport(far);
            await Frames(20);
            p.Crowns += 1000;
            main.FoundColonyForTest();
            var both = city.ColoniesOf(p.Company).ToList();
            Check(both.Count == 2 && both[1].Number == 2 && both[1].Name.Contains("Second") && city.CouncilOf(both[1].Town).Heroes.Contains(p.Company),
                $"and a second colony elsewhere, with a council of its own ({string.Join(", ", both.Select(h => h.Name))})");
            Check(main.KeepingOptionsForTest(lad).Count(o => o.Contains("market stall")) == 2, "each colony has a market stall to keep");
        }
        else Check(false, "room in the world for a second colony");
        foreach (var c2 in p.Companions().ToList()) c2.QueueFree();
    }

    async Task TheVillageDeck()
    {
        var p = main.PlayerNode;
        for (var i = 0; i < Northwood.Hearts.Count; i++)
        {
            var tree = Northwood.Hearts[i];
            var centre = new Vector3((float)tree.X, 0, (float)tree.Z);
            var top = Forest.RampTop(tree, i + 1);
            var (deckIn, deckOut) = Forest.DeckSpan(tree);
            var level = Forest.DeckLevel(tree);
            // Onto the ramp a little below the top, where it runs outside the deck's rim.
            var a = top - 1.0f;
            var rampY = level - 1.0f * (Northwood.DeckHeight / (2 * Mathf.Tau));
            var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (deckOut + 0.4f + 1.4f);
            await Teleport(at);
            p.GlobalPosition = at with { Y = rampY + 0.7f };
            p.Velocity = Vector3.Zero;
            await Frames(20);
            Check(Mathf.Abs(p.GlobalPosition.Y - rampY) < 0.9f, $"tree {i + 1}: standing on the ramp near its top ({p.GlobalPosition.Y - rampY:0.0} m off its surface, on the floor {p.IsOnFloor()})");
            // Walk up and round, keeping to the ramp, until past the top.
            float Round() => Mathf.Atan2(p.GlobalPosition.Z - centre.Z, p.GlobalPosition.X - centre.X);
            Input.ActionPress("move_forward");
            for (var f = 0; f < 360 && Mathf.AngleDifference(Round(), top) > -0.02f; f++)
            {
                p.Rotation = new Vector3(0, Mathf.Pi - Round(), 0);
                await Frames(1);
            }
            Input.ActionRelease("move_forward");
            await Frames(10);
            var r = new Vector2(p.GlobalPosition.X - centre.X, p.GlobalPosition.Z - centre.Z).Length();
            Check(Mathf.AngleDifference(Round(), top) <= 0 && Mathf.Abs(p.GlobalPosition.Y - level) < 0.6f && r < deckOut + 0.5f && r > deckIn,
                $"tree {i + 1}: up the ramp and straight onto the deck, nothing in the way ({Mathf.AngleDifference(Round(), top):0.00} rad past the top, {p.GlobalPosition.Y - level:0.0} m off the deck, {r:0.0} m out of {deckIn:0.0} to {deckOut:0.0})");
            // The deck is solid and wide, right round.
            var b = top + 2.5f;
            var on = centre + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * (deckIn + 5.2f);
            p.GlobalPosition = on with { Y = level + 0.8f };
            p.Velocity = Vector3.Zero;
            await Frames(25);
            Check(p.IsOnFloor() && Mathf.Abs(p.GlobalPosition.Y - level) < 0.6f, $"tree {i + 1}: the deck holds you near its railing, {deckIn + 5.2f - deckIn:0.0} m out ({p.GlobalPosition.Y - level:0.0} m off)");
        }
    }

    /// <summary>A voyage of exploration: take the wheel, turn her, and the sea comes past with a compass to steer by.</summary>
    async Task TheHelm()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        if (main.Sea.Rank == SeaRank.Landsman) main.Sea.SignOn();
        main.SailForTest("port:druitt");
        main.ExploreForTest();
        await Frames(30);
        var sea = main.AtSeaNode!;
        await Teleport(new Vector3(AtSea.Stage.X, AtSea.DeckY + 0.5f, AtSea.Stage.Z - 7));
        await Frames(10);
        Check(main.ActionForTest()?.Verb == "take the helm", $"at the wheel: E to {main.ActionForTest()?.Verb}");
        sea.TakeHelm();
        await Frames(10);
        Check(sea.AtHelm && p.Seat is not null, "you take the wheel");
        Check(sea.DeckChart(p) is { Outline.Count: > 5 } plan && plan.Player.Length() < 30, "and the ship's own plan is what the map shows while you're aboard");
        Check(sea.Line()?.StartsWith("At the helm") == true, sea.Line() ?? "no line");
        var bearing = Helm.Bearing("port:druitt");
        sea.Heading = bearing;
        var (along, from) = (sea.Along, sea.SeaPosition);
        await Frames(120);
        Check(sea.Along > along && sea.SeaPosition.DistanceTo(from) > 10, $"steering true she makes way: {sea.Along - along:P2} of the leg, {sea.SeaPosition.DistanceTo(from):0}m");
        Check(sea.Line()!.Contains("steady on course"), sea.Line()!);
        sea.Heading = bearing + Math.PI;
        along = sea.Along;
        await Frames(120);
        Check(sea.Along < along, $"steer away and she loses ground: {sea.Along - along:P2}");
        sea.LeaveHelm();
        await Frames(5);
        Check(!sea.AtHelm && p.Seat is null, "you give her back to the bosun");
    }

    /// <summary>What people think of the hero: the meter starts at nothing, deeds move it where they're done, and each town and person keeps their own view.</summary>
    async Task TheRepute()
    {
        var (p, hud, r, s) = (main.PlayerNode, main.HudNode, main.ReputationLog, main.Standing);
        await Teleport(new Vector3(0, 0, 0));
        await Frames(50);
        Check(hud.ReputeNumber == "0" && Mathf.IsZeroApprox(hud.ReputeReach), $"a stranger in Tristinwold: the meter reads {hud.ReputeNumber}");
        s.Good(8, "a test of good deeds");
        await Frames(50);
        Check(hud.ReputeNumber.StartsWith('+') && hud.ReputeReach > 0, $"a good deed in town moves it up: {hud.ReputeNumber}");
        var here = hud.ReputeNumber;
        await Teleport(new Vector3(-900, 0, -262));
        await Frames(50);
        Check(hud.ReputeNumber != here, $"it's a different number in Druitt, which only heard of it: {hud.ReputeNumber} after {here}");
        s.Evil(25, "a test of bad deeds");
        await Frames(50);
        Check(hud.ReputeNumber.StartsWith('-') && hud.ReputeReach > 0, $"a bad one there sends it down: {hud.ReputeNumber}");
        await Teleport(new Vector3(0, 0, 0));
        await Frames(50);
        Check(hud.ReputeNumber != "0" && r.Own(ReputationBook.Capital) > 0, $"and home again, the city's own view stands, whatever it's heard: {hud.ReputeNumber}");
        var keeper = main.Locals.First(x => Shops.All.Any(o => o.Id == x.Npc.Id));
        r.Robbed(keeper.Npc.Id, keeper.Npc.Name);
        Check(r.Person(keeper.Npc.Id) == ReputationBook.RobRegard, $"and {keeper.Npc.Name} has their own opinion");
        await Teleport(keeper.GlobalPosition + new Vector3(1.5f, 0, 1.5f));
        await Frames(10);
        r.People.Remove(keeper.Npc.Id);
        r.Towns[ReputationBook.Capital] = 4;
        r.Country = 4;
        r.People[keeper.Npc.Id] = 4;
        Check(main.RepBuy(keeper) < 0.9 && main.RepSell(keeper) > 1.1, $"loved, you pay {main.RepBuy(keeper):P0} of the price and get {main.RepSell(keeper):P0} of the offer");
        main.Talk(keeper);
        Check(hud.TalkText.Contains("lights up"), "and they're glad to see you");
        hud.EndTalk();
        r.Towns[ReputationBook.Capital] = -5;
        r.Country = -5;
        r.People[keeper.Npc.Id] = -5;
        Check(main.RepBuy(keeper) > 1.1 && main.RepSell(keeper) < 0.9, $"hated, you pay {main.RepBuy(keeper):P0} and get {main.RepSell(keeper):P0}");
        main.Talk(keeper);
        Check(hud.TalkText.Contains("very still") && hud.Choose("Let me see what you have"), "and they don't want you there");
        Check(hud.TalkText.Contains("coin's no good"), "and they won't serve you");
        hud.EndTalk();
        (r.Towns[ReputationBook.Capital], r.Country, r.People[keeper.Npc.Id]) = (0, 0, 0);
    }

    /// <summary>The quick keys: moves on 1 to 5, what's used up on 6 to 0, and swapping them about.</summary>
    async Task TheQuickKeys()
    {
        var (p, hud) = (main.PlayerNode, main.HudNode);
        async Task Drawn(int n) // keys are read once a drawn frame, and several physics frames can pass in one
        {
            for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        async Task Press(int digit) // the same event a keyboard sends
        {
            var key = Key.Key0 + digit;
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            await Drawn(3);
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            await Drawn(3);
        }
        await Teleport(new Vector3(-400, 0, 120));
        await Frames(30);
        var bar = p.Hero.Bar;
        Check(hud.SquareName(0) == p.Class.Primary.Name && hud.SquareName(1) == p.Class.Signature.Name && hud.SquareName(2) == p.Class.Utility.Name && hud.SquareName(3) == p.Class.Limit.Name, $"the class's moves are on 1 to 4: {hud.SquareName(0)}, {hud.SquareName(1)}, {hud.SquareName(2)}, {hud.SquareName(3)}");
        Check(hud.SquareName(4) == "", "and 5 is free");

        p.Pack.Add("healing_draught");
        p.Pack.Add("healing_draught");
        await Frames(90);
        var draught = Items.Get("healing_draught")!;
        Check(bar.Slots[5] == "healing_draught" && hud.SquareName(5) == draught.Name, $"a potion finds its own key: 6 is {hud.SquareName(5)}");
        p.Stats.Lose(p.Stats.MaxHealth * 0.6);
        var hurt = p.Stats.Health;
        await Press(6);
        await Frames(30);
        Check(p.Pack.Count("healing_draught") == 1 && p.Stats.Health > hurt, $"pressing 6 drinks it: {hurt:0} to {p.Stats.Health:0} health, {p.Pack.Count("healing_draught")} left");

        hud.Talk("Nobody", "A test.", ("Close", hud.EndTalk));
        await Press(6);
        Check(p.Pack.Count("healing_draught") == 1, "nothing goes off in the middle of a conversation");
        hud.EndTalk();
        await Frames(5);

        p.Stats.Restore(1e9, 1e9);
        await Press(3);
        Check(p.Cooldown(p.Class.Utility.Id) > 0, $"pressing 3 does {p.Class.Utility.Name}");
        Check(!p.Stats.LimitReady, "the limit isn't ready yet");
        await Press(4);
        Check(p.Cooldown(p.Class.Limit.Id) == 0, "so pressing 4 does nothing");

        main.ShowSkills();
        Check(hud.Choose("Quick keys") && hud.Choose("6:") && hud.Choose("Leave it empty") && bar.Slots[5] is null, "taken off from the skills window");
        hud.EndTalk();
        await Frames(90);
        Check(bar.Slots[5] is null && hud.SquareName(5) == "", "and it stays off, though it's still in the pack");
        main.ShowPack();
        Check(hud.Choose("Quick keys") && hud.Choose("6:") && hud.Choose(draught.Name) && bar.Slots[5] == "healing_draught", "put back from the pack");
        hud.EndTalk();

        p.Join("Paladin");
        await Frames(90);
        p.Join("Wizard");
        await Frames(90);
        var wizard = Classes.Get("Wizard");
        Check(hud.SquareName(0) == wizard.Primary.Name, $"a new class takes the keys: 1 is {hud.SquareName(0)}");
        Check(bar.Slots[4] == "smite" && hud.SquareName(4) == "Smite", $"and what you learned before goes on the free key: 5 is {hud.SquareName(4)}");
        p.Stats.Restore(1e9, 1e9);
        await Press(5);
        Check(p.Cooldown("smite") > 0, "and a Wizard can still Smite");
        p.Stats.Heal(1e9);
    }

    /// <summary>The two great blades, Soren's siege, and armour that blasts people away.</summary>
    async Task TheLegends()
    {
        var (p, hud, s, f, city) = (main.PlayerNode, main.HudNode, main.Standing, main.ForestLog, main.Economy);
        await Teleport(Main.BladeStone + new Vector3(2.6f, 0, 0));
        await Frames(20);
        Check(main.WhyNotBlade() is { } a && a.Contains("fifth guardian"), "it won't come for someone who hasn't ended the dragon");
        f.DragonSlayer = true;
        s.Evil(60, "a test of being wicked");
        Check(main.WhyNotBlade() is { } b && b.Contains("wants better"), "nor for a wicked hand, even a dragon-slayer's");
        main.DrawBlade();
        Check(city.BladeHolder is null, "and it stays put");
        s.Good(200, "a test of being good");
        Check(main.WhyNotBlade() is null, "good, and the dragon ended: it will come");
        main.DrawBlade();
        Check(city.BladeHolder == p.Company && p.Pack.Count(Legend.Rubilacxe) == 1 && p.Pack.Count(Legend.RubilacxeScabbard) == 1, "the sword and its scabbard come away");
        await Frames(50);
        Check(p.Pack.Equip(Legend.Rubilacxe), $"{Items.Get(Legend.Rubilacxe)!.Name} goes in the hand");
        p.Dress();
        await Frames(5);
        Check(p.Pack.ToHit == 4 && p.Pack.Power == 4, "+4 to hit and +4 damage, as the book has it");

        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3 };
        var spot = new Vector3(-900, 0, -262);
        await Teleport(spot);
        await Until(() => main.LegendsNode?.Soren is not null, 8);
        var soren = main.LegendsNode?.Soren;
        Check(soren is { Foresees: > 0 }, "Soren is at the gates of Druitt, and sees blows coming");
        if (soren is null) return;
        soren.Foresees = 0;
        var gems = p.Pack.Contents.Count();
        main.LegendsNode!.WebChance = 0;
        soren.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => city.Siege is null, 5);
        Check(city.Siege is null && city.SorenDeaths == 1 && city.WebHolder is null && p.Pack.Contents.Count() >= gems + 5, $"killed, he leaves a heap of rare things and no blade: {p.Pack.Contents.Count() - gems} new kinds of thing");
        Check(p.Pack.Contents.Any(x => x.Item.Forged && x.Item.Text.Contains("The Red Reckoning")), "among them a piece off his own forge");

        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3 };
        await Until(() => main.LegendsNode?.Soren is not null, 8);
        soren = main.LegendsNode?.Soren;
        Check(soren is not null && Mathf.IsEqualApprox(soren.Scale.X, Legend.FormOf(city.SorenChanges).Size), $"he comes back, and wears the form the sea's left him ({city.SorenChanges} of {city.SorenDeaths} deaths took something): {soren?.Scale.X:0.00} times the size");
        if (soren is null) return;
        soren.Foresees = 0;
        soren.Provoke(p);
        await Frames(4);
        soren.TakeHit(soren.Stats.MaxHealth * 0.8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Frames(5);
        soren.Provoke(p);
        var low = soren.GlobalPosition.Y;
        await Until(() => city.Siege is null, 12);
        Check(city.Siege is null && city.SorenDeaths == 1, "at a quarter of his blood he makes for his ship: the siege is broken, but he isn't dead");

        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3 };
        await Until(() => main.LegendsNode?.Soren is not null, 8);
        soren = main.LegendsNode!.Soren!;
        soren.Foresees = 0;
        main.LegendsNode!.WebChance = 1;
        soren.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => city.Siege is null, 5);
        Check(city.WebHolder == p.Company && p.Pack.Count(Legend.Arachnicalibur) == 1 && p.Pack.Count(Legend.ArachnicaliburScabbard) == 1, "and once in a long while, the black blade itself");

        // Rune armour: struck, it blows back whatever's close.
        var plate = Enumerable.Range(1, 200000).Select(i => Forging.Serial("leather_armor", (ulong)i * 104729, "city:tristinwold", "The Anvil Gate", 6, 10, "Test")).First(id => Items.Get(id)!.Powers!.ContainsKey("rune_fire"));
        p.Pack.Add(plate);
        p.Pack.Equip(plate);
        await Teleport(new Vector3(-400, 0, 120)); // open ground: nothing for it to fetch up against
        await Frames(30);
        var spot2 = p.GlobalPosition + new Vector3(2, 0, 0);
        var foe = Enemy.Create(Bestiary.Get("wolf"), spot2 with { Y = City.FloorAt(spot2) + 0.3f });
        main.AddEnemy(foe);
        foe.Stats.Grow(1000); // it has to live through this, to show how far it flies
        await Frames(3);
        var (was, near) = (foe.Stats.Health, foe.GlobalPosition.DistanceTo(p.GlobalPosition));
        for (var i = 0; i < 12 && foe.Stats.Health >= was; i++) p.TakeHit(1, DamageKind.Physical, AttackKind.Weapon, false, foe); // a blow it dodges or that glances doesn't set the rune off
        Check(foe.State != EnemyState.Dead && foe.Stats.Health < was, $"the rune lets go, and it burns what's near: {was - foe.Stats.Health:0} damage");
        await Frames(24);
        var far = foe.GlobalPosition.DistanceTo(p.GlobalPosition);
        Check(far > near + 3, $"and blasts it away: {near:0.0}m to {far:0.0}m");
        foe.QueueFree();

        // Whoever took his blade: he comes for them, and if he wins, he has it back.
        var rep = main.ReputationLog;
        rep.Country = 4;
        s.Good(400, "a test of being very good");
        await Teleport(new Vector3(-400, 0, 120));
        await Frames(50);
        var clean = hud.ReputeNumber;
        p.Pack.Equip(Legend.Arachnicalibur);
        p.Pack.Equip(Legend.ArachnicaliburScabbard);
        await Frames(50);
        Check(clean.StartsWith('+') && hud.ReputeNumber == "-1" && s.Repute == Repute.Rough, $"the black blade in hand, a hero at {clean} reads {hud.ReputeNumber}, and is known as {s.Name}");
        var deaths = city.SorenDeaths;
        main.LegendsNode.HuntForTest();
        await Frames(5);
        var hunter = main.LegendsNode.Soren;
        Check(hunter is not null && hunter.GlobalPosition.DistanceTo(p.GlobalPosition) < 30 && f.SorenHuntAt > main.GuildBook.PlaySeconds, "he hunts down whoever took his blade");
        if (hunter is null) return;
        for (var i = 0; i < 40 && !p.Downed; i++) p.TakeHit(1e9, DamageKind.Physical, AttackKind.Weapon, false, hunter); // the scabbard dodges three blows in ten
        await Frames(10);
        Check(city.WebHolder is null && p.Pack.Count(Legend.Arachnicalibur) == 0 && p.Pack.WornIn(Slot.Weapon)?.Id != Legend.Arachnicalibur && p.Pack.WornIn(Slot.Neck) is null && main.LegendsNode.Soren is null, "beaten, they lose it: blade and scabbard go back to him");
        Check(city.SorenDeaths == deaths, "and he didn't have to die for it");
        await Frames(50);
        Check(!rep.Marked && rep.CountryView == rep.Country && rep.CountryView >= 4, $"with it gone, people remember who they were: {rep.CountryView:0.0} again");
    }

    /// <summary>Lu Wukong at the siege, his berth, his deaths, and the small crews on the water.</summary>
    async Task ThePirates()
    {
        var (p, hud, s, city, sea, legends) = (main.PlayerNode, main.HudNode, main.Standing, main.Economy, main.Sea, main.LegendsNode!);
        p.Stats.Heal(1e9);
        var spot = new Vector3(-900, 0, -262);

        // A siege he comes to: he and his crew are on the town's side, and the two crews go for each other.
        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3, Lu = true };
        await Teleport(spot);
        await Until(() => legends.Lu is not null, 8);
        var lu = legends.Lu;
        Check(lu is { Friendly: true } && legends.Hands.Count == 5 && legends.Hands.All(e => e.Friendly), $"Lu Wukong lands at Druitt with {legends.Hands.Count} hands, on the town's side");
        if (lu is null) return;
        Check(hud.Heard("Who's robbing the poor now"), "and he says so");
        var soren = legends.Soren!;
        soren.Foresees = 0;
        foreach (var e in legends.Hands.Append(lu)) { e.Stats.Grow(1000); e.GlobalPosition = soren.GlobalPosition + new Vector3(2, 0, 0); }
        await Until(() => soren.Rival is not null || legends.Crew.Any(c => c.Rival is not null), 12);
        Check(soren.Rival is not null || legends.Crew.Any(c => c.Rival is not null), "Soren's crew and Lu's go for each other, with no hero in it");
        var was = p.Stats.Health;
        var left = lu.Stats.Health;
        p.Swing();
        await Frames(30);
        Check(lu.Stats.Health <= left, "your blows pass him by");

        // Lu's blows drive Soren off: the siege is broken, and no pay for a hero who did nothing.
        var purse = p.Crowns;
        soren.Stats.Heal(1e9);
        foreach (var e in legends.Hands.ToList().Append(lu)) e.LoseTrack(0);
        for (var i = 0; i < 30 && soren.Stats.Health > soren.Stats.MaxHealth * 0.2; i++) soren.TakeHitFrom(lu, soren.Stats.MaxHealth * 0.2, false);
        await Until(() => city.Siege is null, 14);
        Check(city.Siege is null && city.SorenDeaths == 0 && p.Crowns == purse, $"Lu drives Soren off on his own, and it pays you nothing (siege {city.Siege?.Town}, deaths {city.SorenDeaths}, paid {p.Crowns - purse}, blood {soren.Stats.Health / soren.Stats.MaxHealth:P0})");
        await Until(() => hud.Talking, 6);
        Check(hud.Talking && hud.TalkTitle.Contains("Lu Wukong"), "with the town safe, Lu has a word with you");
        sea.Rank = SeaRank.Bosun;
        var good = s.Honour;
        Check(hud.Choose("I'll sail with you"), "and you take the berth");
        await Frames(10);
        Check(sea.Outlaw && sea.Sworn.Good && sea.Sworn == PirateCrew.DrunkenMonkey && sea.Pirate == PirateRank.Swab && sea.Rank == SeaRank.Landsman, "a swab of the Drunken Monkey, off the company's books");
        Check(s.Honour > good && !s.Outlaw, "and it's a good name to have, not a price on your head");
        await Until(() => legends.Lu is null, 4);
        Check(legends.Lu is null && legends.Hands.Count == 0, "and he sails");

        // Out with Lu's crew: the prey is other pirates.
        await Teleport(Main.Smugglers + new Vector3(1, 0, -1.5f));
        main.Talk(main.Locals.First(x => x.Npc.Id == "tristinwold/smuggler"));
        Check(hud.Choose("Row me out to the Drunken Monkey"), "Nell Corran rows you out to the Drunken Monkey");
        await Frames(20);
        var at = main.AtSeaNode!;
        var v = sea.Voyage!;
        Check(v.Crew is not null && PirateCrew.Get(v.Crew) is { Good: false }, $"hunting {v.PortName}: another crew, not a merchantman");
        (v.Out, v.Home) = ([0.05], []);
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        Check(at.Boarders.All(e => e.Kind.Id.StartsWith("pirate")), "it's pirates who come to meet you, not marines");
        var hunted = PirateCrew.Get(v.Crew)!;
        foreach (var e in at.Boarders.ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => sea.Prizes == 1, 8);
        Check(sea.Prizes == 1 && sea.IsBroken(hunted, main.GuildBook.PlaySeconds), $"a prize taken, and {hunted.Name} are broken for it");
        at.Along = 0.999;
        await Until(() => sea.Voyage is null, 8);
        await Frames(10);
        Check(hud.Heard(PirateCrew.DrunkenMonkey.Haven), "home to Monkey's Rest");

        // A company voyage: the small crew on the lane boards, and if it wins with you on your feet, it offers a place.
        sea.Pardon();
        sea.Rank = SeaRank.Bosun;
        sea.Broken.Clear();
        var port = Seas.Get("port:greywater")!;
        var crew = PirateCrew.Small[0];
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        main.SailForTest("port:greywater");
        await Frames(20);
        at = main.AtSeaNode!;
        v = sea.Voyage!;
        v.Crew = crew.Id;
        v.Pirates = crew.Hands;
        (v.Out, v.Home, v.Flag) = ([0.05], [], null);
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        Check(hud.Heard(crew.Captain) && at.Boarders.Any(e => e.Stats.Name == crew.Captain) && at.Boarders.Count == crew.Hands, $"{crew.Ship} comes alongside: {crew.Hands} of {crew.Name} under {crew.Captain}");
        sea.Hull = 0.5;
        await Until(() => hud.Talking, 6);
        Check(hud.TalkTitle.Contains(crew.Captain), "they take the ship with you still on your feet, and their captain makes an offer");
        Check(hud.Choose("Join the pirates") && sea.Sworn == crew && sea.Outlaw, $"you're one of {crew.Name}");
        await Frames(20);
        Check(main.AtSeaNode is null, "and ashore");

        // Beat one off, and it goes quiet; and no offer comes to a hero who's down.
        sea.Pardon();
        sea.Rank = SeaRank.Bosun;
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        main.SailForTest("port:greywater");
        await Frames(20);
        at = main.AtSeaNode!;
        v = sea.Voyage!;
        v.Crew = crew.Id;
        (v.Out, v.Home, v.Flag) = ([0.05, 0.1], [], null);
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        purse = p.Crowns;
        foreach (var e in at.Boarders.ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => sea.IsBroken(crew, main.GuildBook.PlaySeconds), 8);
        Check(sea.IsBroken(crew, main.GuildBook.PlaySeconds) && p.Crowns >= purse + crew.Bounty, $"beaten off, {crew.Name} keep their heads down, and their purses are yours: {p.Crowns - purse} crowns");
        v.Crew = null;
        v.Sprung = 1;
        at.Along = 0.11;
        await Until(() => at.Boarders.Count > 0, 20);
        var boarder = at.Boarders[0];
        for (var i = 0; i < 40 && !p.Downed; i++) p.TakeHit(1e9, DamageKind.Physical, AttackKind.Weapon, false, boarder);
        sea.Hull = 0.5;
        await Frames(30);
        Check(p.Downed && !hud.Talking, "with the hero down, nobody offers anything");
        await Until(() => !p.Downed, 15);
        await Frames(30);
        Check(sea.Voyage is null, "the ring brings them back, and the ship's gone without them");

        // Lu on a pirate's deck: a wicked crew's cruise home, and he dies for it, and comes back less.
        p.Stats.Heal(1e9);
        sea.JoinPirates();
        main.ProwlForTest();
        await Frames(20);
        at = main.AtSeaNode!;
        v = sea.Voyage!;
        (v.Out, v.Home, v.Homeward, v.Flag, v.Along, v.Sprung) = ([], [0.05], true, "wukong", 0, 0);
        await Frames(2);
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        var ape = at.Boarders.FirstOrDefault(e => e.Kind.Id == "lu_wukong");
        Check(ape is not null && at.Boarders.Where(e => e != ape).All(e => e.Kind.Id == "monkey_hand"), $"Lu Wukong comes aboard a wicked crew's ship, with his hands behind him ({string.Join(", ", at.Boarders.Select(e => e.Kind.Id))}; along {at.Along:0.000}, sprung {v.Sprung}, flag {v.Flag}, homeward {v.Homeward}, pirates {v.Pirates}, marines {v.Marines}, voyage same {sea.Voyage == v})");
        if (ape is null) return;
        Check(!ape.Friendly, "and here he's no friend of yours");
        foreach (var e in at.Boarders.Where(e => e != ape).ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        ape.Stats.Grow(100000);
        ape.Stats.Heal(1e9);
        ape.TakeHit(ape.Stats.MaxHealth * 0.8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => ape.Carried, 3);
        Check(ape.Carried && hud.Heard("Bring him down now"), "at a quarter of his blood he goes for the rail");
        var pay = p.Crowns;
        ape.TakeHit(1e8, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => city.WukongDeaths == 1, 4);
        Check(city.WukongDeaths == 1 && hud.Heard("second verse"), "caught, he dies, and it costs him a verse");
        await Until(() => sea.Voyage?.Sprung != 0 || at.Boarders.Count == 0, 10);
        await Frames(30);
        Check(p.Pack.Count("wukongs_staff") == 1 && p.Crowns > pay, $"the price on him, and his staff: {p.Crowns - pay} crowns");
        await Frames(20);
        at.Along = 0.999;
        await Until(() => sea.Voyage is null, 10);
        Check(sea.Voyage is null, "and that cruise does come home: a boarding on the way back only happens once");

        // He comes back bigger; and if he falls at a siege, there's nobody holding the town.
        city.Siege = new PirateSiege { Town = "city:druitt", Ends = city.Day + 3, Lu = true };
        await Teleport(spot);
        await Until(() => legends.Lu is not null, 8);
        lu = legends.Lu!;
        Check(Mathf.IsEqualApprox(lu.Scale.X, Legend.WukongOf(1).Size) && hud.Heard("I died, you know"), $"he comes back, greyer and bigger: {lu.Scale.X:0.00} times the size");
        lu.TakeHitFrom(legends.Soren!, 1e9, false);
        await Until(() => city.WukongDeaths == 2, 4);
        Check(city.WukongDeaths == 2 && city.Siege is { Lu: false } && legends.Lu is null && legends.Hands.Count == 0, "Soren kills him, and his crew carry him off: nobody's holding the town now");
        Check(legends.Soren is not null, "and Soren's still at the gates");
        city.Siege = null;
        await Frames(90);
    }

    /// <summary>Brasswake: finding it, going stale, being let aboard, the lifts, the valves, the closed market, flying, and leaving it all behind.</summary>
    async Task TheIsleTest()
    {
        var (p, hud, sea, isle) = (main.PlayerNode, main.HudNode, main.Sea, main.Sea.Isle);
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        p.Crowns += 1000;

        // Known to everyone, always moving: Marit will ship you, and the Factor knows how far.
        await Teleport(Main.BrassFactorAt + new Vector3(1.5f, 0, 1.5f));
        main.Talk(Who("tristinwold/brassfactor"));
        Check(hud.Choose("Where's Brasswake today?"), "the Brass Factor keeps the board on the harbour road");
        hud.EndTalk();
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        sea.Rank = SeaRank.Bosun;
        main.Talk(Who("tristinwold/harbourmaster"));
        Check(hud.Choose("Ship me out to Brasswake") && sea.Voyage?.Port == Brasswake.Port, "Marit Hale ships you out on the trade route");
        await Frames(20);
        var at = main.AtSeaNode!;
        at.Along = 0.999;
        await Until(() => main.IsleNode is not null, 8);
        await Frames(40);
        var node = main.IsleNode;
        Check(node is not null && hud.Talking && hud.TalkTitle.Contains("Warden"), "the island comes out of the steam, and the warden has terms");
        if (node is null) return;
        Check(isle.Aboard && isle.Berthed && isle.Rank == IsleRank.Guest && isle.Visits == 1, "aboard, a guest, your ship tied up at the Strangers' Dock");
        Check(hud.Choose("I understand"), "you take the terms");
        await Frames(30);
        Check(Mathf.Abs(p.GlobalPosition.Y - node.TierFloor(0)) < 3 && main.AtSeaNode is null, $"standing on the gangway ({p.GlobalPosition.Y - node.TierFloor(0):0.0} m above the deck), not falling through it");
        Check(City.Afloat is not null, "and the game knows it's afloat");
        Check(node.Flyers >= 5, $"{node.Flyers} ships flying round it");

        // The dock: the trade route, and the way home by sea.
        var dockmaster = Who("isle/dockmaster");
        Check(City.FloorAt(dockmaster.GlobalPosition) >= node.TierFloor(0) - 12, "the dockmaster's on the dock");
        main.Talk(dockmaster);
        Check(hud.Options.Any(o => o.StartsWith("What do you buy")) && hud.Options.Any(o => o.StartsWith("Cast off for home")), "the dockmaster trades, and will cast you off");
        hud.EndTalk();

        // The closed market.
        main.Talk(Who("isle/stores"));
        var crowns = p.Crowns;
        p.Crowns = 0;
        Check(hud.Choose("Steamweave Coat") && p.Pack.Count("steamweave_coat") == 0 && p.Crowns == 0, "no crowns, no coat");
        p.Crowns = crowns;
        hud.EndTalk();

        // The valves.
        main.Talk(Who("isle/engineer"));
        Check(hud.Choose("Put me on the valves") && node.OnShift, "Dorrin Pike puts you on the valves");
        for (var i = 0; i < IsleBook.Valves; i++)
        {
            p.GlobalPosition = node.LitValve + Vector3.Up * 1.2f;
            await Frames(3);
            var near = main.NearestForTest();
            Check(near?.Verb == "ease the valve", $"valve {i + 1} wants easing");
            near?.Act();
        }
        Check(!node.OnShift && isle.Earned == IsleBook.Valves * IsleBook.VentPay + IsleBook.ShiftPay && isle.Shifts == 1 && p.Crowns >= crowns + isle.Earned, $"a shift worked: {isle.Earned} crowns ({p.Crowns - crowns} more in your purse)");

        // Up the lifts.
        for (var i = 0; i < 3; i++)
        {
            p.GlobalPosition = node.LiftFoots[i] + Vector3.Up * 1.2f;
            await Frames(3);
            var near = main.NearestForTest();
            Check(near?.Verb == "ride the steam lift up", $"lift {i + 1}");
            near?.Act();
            await Until(() => p.Seat is null, 9);
            await Frames(20);
            Check(Mathf.Abs(p.GlobalPosition.Y - node.TierFloor(i + 1)) < 3, $"up to tier {i + 2}: {p.GlobalPosition.Y - node.TierFloor(i + 1):0.0} m above its floor");
        }

        // Flying.
        await Frames(5);
        p.GlobalPosition = Who("isle/skymaster").GlobalPosition + new Vector3(1, 1, 1);
        main.Talk(Who("isle/skymaster"));
        Check(hud.Choose("Teach me to fly") && isle.Pilot, "Aune Rook teaches you to fly");
        hud.EndTalk();
        main.Talk(Who("isle/skymaster"));
        Check(hud.Choose("Give me a skiff") && node.Flying, "and gives you a skiff");
        var from = node.SkiffAt;
        await Frames(60);
        Check(node.SkiffAt.DistanceTo(from) > 3 && p.Seat is not null, $"she flies: {node.SkiffAt.DistanceTo(from):0} m already");
        (await Near())?.Act();
        await Frames(10);
        Check(!node.Flying && p.Seat is null, "E brings her home");

        // The air war: the fleet goes for what comes, on its own, and the hero can man a gun.
        var flock = Brasswake.Raids.First(r => r.Id == "stormwings");
        main.RaidForTest("stormwings");
        await Frames(10);
        Check(node.Foes.Count == 9 && hud.Heard("stormwings"), $"the horns go: {node.Foes.Count} stormwings over the island");
        var gun = node.DeckGunsAt[0];
        p.GlobalPosition = gun + Vector3.Up * 1.2f;
        await Frames(3);
        var manning = main.NearestForTest();
        Check(manning?.Verb == "man the deck gun", "there's a deck gun to man");
        manning?.Act();
        Check(node.Manning && p.Seat is not null, "you take the gun");
        var inTheSky = node.Foes.Count;
        await Until(() => node.Foes.Count < inTheSky, 40);
        Check(node.Foes.Count < inTheSky && isle.AirKills == 0, $"the fleet's guns bring them down without you: {inTheSky - node.Foes.Count} so far, in the time it took");
        var (kills, marks) = (isle.AirKills, p.Crowns);
        for (var i = 0; i < 40 && isle.AirKills == kills && node.Foes.Count > 0; i++)
        {
            var f = node.Foes[0];
            p.Face(f.GlobalPosition);
            p.Camera.LookAt(f.GlobalPosition);
            await Frames(2);
            node.ShootForTest();
            await Frames(45);
        }
        Check(isle.AirKills > kills && p.Crowns > marks, $"a shell of yours brings one down: {isle.AirKills} kills, {p.Crowns - marks} crowns");
        p.Camera.Rotation = Vector3.Zero;
        (await Near())?.Act();
        await Frames(5);
        Check(!node.Manning && p.Seat is null, "and you leave the gun");
        await Until(() => !node.RaidOn, 60);
        Check(!node.RaidOn, "the sky's clear");

        // A dragon that fights back: it goes for the ships, and they feel it.
        p.Stats.Heal(1e9);
        main.RaidForTest("young");
        await Frames(10);
        Check(node.Foes.All(e => e.IsDragon && e.Rival is not null), "young dragons come in, each going for a ship");
        foreach (var f in node.Foes) f.Stats.Grow(6); // long enough to reach one
        var shipHurt = node.FleetHullLost;
        await Until(() => node.FleetHullLost > shipHurt || !node.RaidOn, 70);
        Check(node.FleetHullLost > shipHurt, $"their teeth reach the ships in the air: {node.FleetHullLost - shipHurt:0} damage to the fleet ({string.Join("; ", node.Foes.Select(e => $"{e.Stats.Health:0}/{e.Stats.MaxHealth:0} at {e.GlobalPosition.Y - AtSea.Stage.Y:0} up, rival {e.Rival?.Stats.Name} {(e.Rival is null ? 0 : new Vector2(e.GlobalPosition.X - e.Rival.GlobalPosition.X, e.GlobalPosition.Z - e.Rival.GlobalPosition.Z).Length()):0} m off, state {e.State}, airborne {e.Airborne}"))}; {node.FlyersUp} up)");
        var dragons = node.Foes.Sum(e => e.Stats.Health);
        await Until(() => node.Foes.Sum(e => e.Stats.Health) < dragons || !node.RaidOn, 30);
        Check(!node.RaidOn || node.Foes.Sum(e => e.Stats.Health) < dragons, $"and the fleet gives it back: {node.FlyersUp} ships up");
        foreach (var f in node.Foes.ToList()) f.Shelled(1e9, false);
        await Until(() => !node.RaidOn, 4);

        // Riders of the Eyrie get the guns too: on their own, the island's guns start falling on them.
        main.RaidForTest("riders");
        await Frames(10);
        Check(node.Foes.Count == 4 && node.Foes.All(e => e.Stats.Name == "Rider of the Eyrie") && hud.Heard("Nobody who rides a dragon"), "riders on gold and silver come to look, and every gun swings round");
        var blood = node.Foes.Sum(e => e.Stats.Health);
        await Until(() => node.Foes.Sum(e => e.Stats.Health) < blood || !node.RaidOn, 45);
        Check(node.Foes.Sum(e => e.Stats.Health) < blood, "the guns hit them, with nobody at them");
        foreach (var f in node.Foes.ToList()) f.Shelled(1e9, false);
        await Until(() => !node.RaidOn, 4);

        // The air crews.
        p.GlobalPosition = Who("isle/skymaster").GlobalPosition + new Vector3(1, 1, 1);
        await Frames(5);
        main.Talk(Who("isle/skymaster"));
        Check(hud.Choose("Sign me on with the air crews") && isle.Air == AirRank.Hand, "Aune Rook signs you on with the air crews");
        hud.EndTalk();
        main.Talk(Who("isle/skymaster"));
        Check(hud.Choose("Take me up on patrol") && node.OnPatrol && p.Seat is not null, "and takes you up on patrol");
        await Until(() => node.RaidOn, 10);
        Check(node.RaidOn, "something's sighted");
        var homeMarks = p.Crowns;
        foreach (var f in node.Foes.ToList()) f.Shelled(1e9, false);
        await Until(() => !node.OnPatrol, 6);
        await Frames(10);
        Check(!node.OnPatrol && p.Seat is null && isle.Sorties == 1 && p.Crowns > homeMarks, $"sky clear, back on the dock and paid: {p.Crowns - homeMarks} crowns");
        isle.Sorties = IsleBook.ToHelmsman;
        isle.AirKills = IsleBook.HelmsmanKills;
        isle.Air = AirRank.Helmsman;
        main.Talk(Who("isle/skymaster"));
        Check(hud.Choose("Take me up on patrol") && node.OnPatrol && node.Flying, "a helmsman takes the wheel on patrol");
        await Frames(10);
        (await Near())?.Act();
        await Frames(10);
        Check(!node.OnPatrol && !node.Flying && p.Seat is null, "and can go down the wire if it goes badly");
        foreach (var f in node.Foes.ToList()) f.Shelled(1e9, false);
        await Until(() => !node.RaidOn, 4);

        // What they made stays behind.
        p.Crowns += 90;
        var before = p.Crowns;
        main.Talk(Who("isle/stores"));
        Check(hud.Choose("Steamweave Coat") && p.Pack.Count("steamweave_coat") == 1 && p.Crowns == before - 90, "crowns buy the coat");
        hud.EndTalk();
        main.Talk(Who("isle/warden"));
        Check(hud.Choose("Fly me back to the mainland"), "you ask to go");
        await Until(() => main.IsleNode is null, 12);
        await Frames(30);
        Check(!isle.Aboard && p.Pack.Count("steamweave_coat") == 0 && isle.Locker.Contains("steamweave_coat"), "the coat stays at the gangway");
        Check(p.GlobalPosition.Y < 100 && City.Afloat is null, "and you're on the south pier, with no idea where you've been");

        // Home by sea, from the dock.
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        main.Talk(Who("tristinwold/harbourmaster"));
        Check(hud.Choose("Ship me out to Brasswake"), "back out on the route");
        await Frames(20);
        main.AtSeaNode!.Along = 0.999;
        await Until(() => main.IsleNode is not null, 8);
        await Frames(30);
        hud.EndTalk();
        Check(p.Pack.Count("steamweave_coat") == 1, "the coat's handed back at the gangway");
        main.Talk(Who("isle/dockmaster"));
        Check(hud.Choose("Cast off for home") && sea.Voyage?.Homeward == true && !isle.Berthed, "you cast off from the Strangers' Dock");
        await Frames(20);
        Check(main.AtSeaNode is not null && main.IsleNode is null && p.Pack.Count("steamweave_coat") == 0, "at sea on your own ship, with the coat left behind");
        main.AtSeaNode!.Along = 0.999;
        await Until(() => sea.Voyage is null, 8);
        await Frames(20);

        // A citizen's beacon.
        isle.Rank = IsleRank.Citizen;
        await Teleport(Main.Smugglers + new Vector3(1, 0, -1.5f));
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("Light the brass beacon"), "a citizen lights the beacon");
        await Frames(20);
        Check(main.IsleNode is not null && p.Pack.Count("steamweave_coat") == 1 && isle.Locker.Count == 0, "and a skiff takes them back, and the coat's handed over");
        main.LeaveIsleForTest();
        await Frames(20);
    }

    async Task<(string Verb, Action Act)?> Near()
    {
        await Frames(2);
        return main.NearestForTest();
    }

    /// <summary>What was running away with itself: a deviant's band, how a hunter comes; and what a hero grows into.</summary>
    async Task TheGrowth()
    {
        var (p, hud, feuds) = (main.PlayerNode, main.HudNode, main.FeudsNode);
        await Teleport(new Vector3(-400, 0, 120));
        await Frames(20);
        // A band: lesser folk, a few at a time, and only the one.
        var at = p.GlobalPosition + new Vector3(30, 0, 0);
        var mother = Enemy.Create(Bestiary.Get("briar_mother"), at with { Y = City.FloorAt(at) + 0.5f });
        main.AddEnemy(mother);
        mother.Carried = true;
        Check(Bands.Soldiers(Tristinwold.Deviant.Rank.Stray) == 0, "a new deviant stands alone");
        feuds.Soldiers(mother, Tristinwold.Deviant.Rank.Ringleader, wait: 0.5);
        feuds.Soldiers(mother, Tristinwold.Deviant.Rank.Ringleader, wait: 0.5);
        feuds.Soldiers(mother, Tristinwold.Deviant.Rank.Ringleader, wait: 0.5);
        Check(feuds.BandOf(mother) == 0, "its soldiers don't appear out of the air");
        await Until(() => feuds.BandOf(mother) >= Bands.Soldiers(Tristinwold.Deviant.Rank.Ringleader), 6);
        await Frames(120);
        Check(feuds.BandOf(mother) == Bands.Soldiers(Tristinwold.Deviant.Rank.Ringleader), $"seen three times, it still has the one band: {feuds.BandOf(mother)}");
        var kinds = GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => e != mother && e.GlobalPosition.DistanceTo(mother.GlobalPosition) < 16).Select(e => e.Kind.Id).Distinct().ToList();
        Check(kinds.Contains("spriggan") && !kinds.Contains("briar_mother"), $"they're her thralls, not more of her ({string.Join(", ", kinds)})");
        Check(Feuds.Underling("dock_thug") == "lantern_boy", "and a dock thug's are lantern boys");
        feuds.Disband(mother);
        await Frames(4);
        Check(feuds.BandOf(mother) == 0, "and they go when it goes");
        mother.QueueFree();
        var town = Main.BladeStone;
        var thug = Enemy.Create(Bestiary.Get("dock_thug"), town with { Y = City.FloorAt(town) + 0.5f });
        main.AddEnemy(thug);
        await Frames(3);
        feuds.Soldiers(thug, Tristinwold.Deviant.Rank.Tyrant, wait: 0.1);
        await Frames(40);
        Check(feuds.BandOf(thug) == 0 && !feuds.HasBand(thug), "and nothing raises an army inside the town");
        thug.QueueFree();

        // A deviant takes its time.
        var hero = p.Hero;
        p.Join("EtherAeronaut");
        await Frames(10);
        while (hero.Level < Advances.Third) hero.Gain(Hero.XpToNext(hero.Level));
        p.Fit();
        p.Stats.Heal(1e9);
        p.Stats.Restore(1e9, 1e9);
        Check(hero.LevelPower(true) > hero.LevelPower(false) && hero.LevelPower(false) > 1, $"a limit break grows fastest: x{hero.LevelPower(true):0.00} against x{hero.LevelPower(false):0.00}");
        await Frames(4);
        Check(QuickBar.Moves(hero).Any(a => a.Id == "ether_lance") && QuickBar.Moves(hero).Any(a => a.Id == "boiler_burst"), "the new moves can go on the keys");

        // The skills window: what each does, and a switch for each rank.
        hero.Points = 2;
        main.ShowSkills();
        Check(hud.Talking && hud.TalkText.Contains("click a switch"), "K shows the abilities");
        var rank = hero.Rank("ether_lance");
        Check(hud.Choose("Raise Ether Lance") && hero.Rank("ether_lance") == rank + 1 && hero.Points == 1, "a switch raises it, for a point");
        hud.EndTalk();

        var foe = Enemy.Create(Bestiary.Get("wolf"), p.GlobalPosition + new Vector3(0, 0.5f, -6));
        main.AddEnemy(foe);
        foe.Carried = true;
        foe.Stats.Grow(50);
        await Frames(5);
        p.Face(foe.GlobalPosition);
        await Frames(3);
        var was = foe.Stats.Health;
        Check(p.Use(Classes.Move("ether_lance")!) && foe.Stats.Health < was, $"Ether Lance strikes what's in front: {was - foe.Stats.Health:0} damage");
        p.CoolForTest();
        await Frames(30);
        was = foe.Stats.Health;
        Check(p.Use(Classes.Move("boiler_burst")!) && foe.Stats.Health < was, $"Boiler Burst hits what's around: {was - foe.Stats.Health:0} damage");
        foe.QueueFree();
        foreach (var advance in Advances.Everything.Where(a => a.Style is MoveStyle.Mend or MoveStyle.Ward or MoveStyle.Haste))
            Check(advance.Move.Seconds > 0 || advance.Move.Power > 0, $"{advance.Move.Name} does something");

        // A home: wake there, and on the quay if you're one of a ship's people.
        var saga = main.SagaForTest;
        Check(saga.Home is null && main.WakeSpotForTest().DistanceTo(City.PlayerStart) < 2, "with no home chosen, the ring brings you back to the Perch");
        var greywater = main.World.Find("city:greywater")!;
        main.ShowSkills();
        Check(!hud.Options.Any(o => o.Contains("my hometown")), "you can't change your hometown just anywhere");
        hud.EndTalk();
        await Teleport(new Vector3((float)greywater.X, 0, (float)greywater.Z));
        main.EnterTavernForTest();
        await Frames(10);
        main.ShowBarForTest();
        Check(hud.Choose($"Make {greywater.Name} my hometown") && saga.Home?.Id == greywater.Id, $"at the bar of a tavern in {greywater.Name}, you make it your hometown");
        hud.EndTalk();
        main.ShowBarForTest();
        Check(!hud.Options.Any(o => o.Contains("my hometown")), "and the bar doesn't ask again");
        hud.EndTalk();
        main.LeaveTavernForTest();
        await Frames(10);
        Check(new Vector2(main.WakeSpotForTest().X - (float)greywater.X, main.WakeSpotForTest().Z - (float)greywater.Z).Length() < greywater.Radius, $"and that's where the ring brings you now, on the open road inside its walls (wakes {main.WakeSpotForTest()}, Greywater at {greywater.X:0},{greywater.Z:0}, a {greywater.Kind} of {greywater.Radius:0} m)");
        Check(hero.Discipline == "EtherAeronaut", "still an aeronaut");
        var sea = main.Sea;
        sea.Rank = SeaRank.Deckhand;
        Check(main.WakeDockForTest() == "Greywater Quay", $"one of a ship's people wakes on their home town's quay: {main.WakeDockForTest()}");
        main.SailForTest("port:greywater");
        await Frames(20);
        sea.Voyage!.Out = [];
        sea.Voyage!.Home = [];
        p.Stats.Heal(1e9);
        var ship = sea.Ship;
        var boarder = Enemy.Create(Bestiary.Get("wolf"), p.GlobalPosition + new Vector3(2, 0, 0));
        main.AddEnemy(boarder);
        boarder.Carried = true;
        await Frames(2);
        for (var i = 0; i < 40 && !p.Downed; i++) p.TakeHit(1e9, DamageKind.Physical, AttackKind.Weapon, false, boarder);
        await Until(() => !p.Downed, 12);
        await Frames(20);
        boarder.QueueFree();
        Check(sea.Voyage is null && main.AtSeaNode is null && hud.Heard("ties up at the quay"), $"killed at sea, you wake at the dock and {ship} comes in with you");
        Check(p.GlobalPosition.Y < 60 && new Vector2(p.GlobalPosition.X - (float)greywater.X, p.GlobalPosition.Z - (float)greywater.Z).Length() < 400, $"on the quay at {greywater.Name} ({p.GlobalPosition.Y:0.0} m up)");
        await Frames(60);
        Check(!p.Downed && p.GlobalPosition.Y > -2, "and standing on it, not in the water");
        sea.Rank = SeaRank.Landsman;
        saga.Home = null;
        await Teleport(new Vector3(-400, 0, 120));

        // The construct takes to the air.
        p.Stats.Restore(1e9, 1e9);
        p.CoolForTest();
        var ground = p.GlobalPosition.Y;
        Check(p.Use(p.Class.Utility), "inside the construct");
        await Frames(4);
        p.JetForTest();
        Check(p.Jetting, "at level 6 and up, it can fly");
        Input.ActionPress("jump");
        await Frames(50);
        Input.ActionRelease("jump");
        Check(p.GlobalPosition.Y > ground + 5, $"holding jump climbs: {p.GlobalPosition.Y - ground:0.0} m up");
        var high = p.GlobalPosition.Y;
        await Frames(30);
        Check(Mathf.Abs(p.GlobalPosition.Y - high) < 1, "and it holds its height with nothing pressed, no falling");
        await Until(() => !p.Possessing, 60);
        await Frames(10);
        Check(!p.Jetting, "when the engine winds down, the flight's over");
    }

    /// <summary>The forge's work: two swords off the same anvil are two different swords, and they show it in the hand and in the fight.</summary>
    async Task TheForge()
    {
        var (p, hud, pack) = (main.PlayerNode, main.HudNode, main.PlayerNode.Pack);
        await Teleport(new Vector3(75, 0, 14) + new Vector3(1, 0, 1));
        await Frames(30);
        var hulda = main.Locals.First(x => x.Npc.Id == Shops.Smithy.Id);
        foreach (var (id, n) in new[] { ("steel_ingot", 8), ("leather", 6), ("pine_log", 6) }) pack.Add(id, n);
        pack.Practise(Job.Smithing, Pack.XpFor(6));
        main.Talk(hulda);
        Check(hud.Choose("Can I use your forge?") && hud.TalkText.Contains("inside"), $"Hulda lets you at the fire, inside ({hud.TalkText})");
        hud.EndTalk();
        main.GoInsideForTest("smithy", Math.Abs(Shops.Smithy.Id.GetHashCode()), hulda.GlobalPosition);
        await Frames(10);
        Check(main.KeeperInsideForTest?.Npc.Id == Shops.Smithy.Id, "and she's in there behind the counter");
        p.GlobalPosition = Main.Anvil + new Vector3(0.8f, 0.4f, 0.8f);
        await Frames(5);
        var forge = main.SmithyActionForTest(p.GlobalPosition);
        Check(forge?.Verb == "work at the forge", $"at the anvil, the forge is yours ({forge?.Verb ?? "nothing"})");
        forge?.Act();
        Check(hud.Choose("Make Steel Sword"), "and you make a sword");
        Check(hud.Choose("Make Steel Sword"), "and another");
        var made = pack.Contents.Where(x => x.Item.Forged).Select(x => x.Item).ToList();
        Check(made.Count == 2 && made[0].Id != made[1].Id && made[0].Text != made[1].Text, $"two swords, not the same sword: {made[0].Name}, {made[1].Name}");
        Check(made.All(x => x.Name.Contains("+2") && x.Text.Contains("The Anvil Gate") && x.Text.Contains(p.Company)), $"each carries its forge and its maker: {made[0].Text}");
        hud.EndTalk();
        // Hulda's own work, by the piece, at the same anvil.
        if (main.KeeperInsideForTest is { } smith) main.Talk(smith);
        Check(hud.Choose("Any work at the forge?") && main.AnvilShiftForTest == 5, "she has work: five pieces off the anvil");
        hud.EndTalk();
        var (purse, xp) = (p.Crowns, pack.JobXp.GetValueOrDefault(Job.Smithing));
        Check(main.SmithyActionForTest(p.GlobalPosition)?.Verb.StartsWith("hammer out") == true, "and the anvil's for hammering now");
        for (var i = 0; i < 5; i++) main.HammerNowForTest();
        Check(main.AnvilShiftForTest is null && p.Crowns > purse + 10 && pack.JobXp.GetValueOrDefault(Job.Smithing) > xp,
            $"five pieces hammered out: +{p.Crowns - purse} crowns, and smithing practised (+{pack.JobXp.GetValueOrDefault(Job.Smithing) - xp} xp)");
        main.GoOutsideForTest();
        await Frames(5);
        var fists = p.Stats.DamageMult;
        Check(pack.Equip(made[0].Id), "a forged sword goes in the hand");
        p.Fit();
        p.Dress();
        await Frames(10);
        Check(p.Stats.DamageMult > fists, $"and it counts: {fists:0.00} to {p.Stats.DamageMult:0.00} damage");
        Check(pack.Power >= 1 && pack.Power == made[0].Power && pack.ToHit == made[0].ToHit, $"with +{pack.ToHit} to hit and +{pack.Power} damage on every blow");
    }

    /// <summary>Turlin: the meeting at the edge of the wood, the fight, the wings at a quarter of his blood, and what each ending does.</summary>
    async Task TheRival()
    {
        var (p, hud, t, f) = (main.PlayerNode, main.HudNode, main.ForestLog.Turlin, main.ForestNode!);
        var edge = new Vector3((float)Northwood.TurlinX, 0, (float)Northwood.TurlinZ - 26);
        await Teleport(edge);
        await Until(() => t.Met && hud.Talking, 6);
        Check(t.Met && hud.Talking && hud.TalkTitle == "Turlin" && !Northwood.NearVillage(p.GlobalPosition.X, p.GlobalPosition.Z), "at the edge of the wood, before the village, Turlin steps out");
        Check(f.RivalBody is { } first && first.Stats.Name == "Turlin" && first.Stats.MaxHealth < 400, $"and he isn't too much for a hero starting out: {f.RivalBody?.Stats.MaxHealth:0} health");
        Check(hud.Choose("Fight."), "you can refuse to talk");
        await Frames(10);
        var e = f.RivalBody!;
        Check(e.FightingCompany, "and he comes at you");
        var (crowns, health) = (p.Crowns, p.Stats.MaxHealth);
        e.TakeHit(e.Stats.Health - e.Stats.MaxHealth * 0.2, DamageKind.Physical, AttackKind.Weapon, false, p);
        await Frames(4);
        var high = e.GlobalPosition.Y;
        await Until(() => e.GlobalPosition.Y > high + 8, 4);
        Check(IsInstanceValid(e) && e.GlobalPosition.Y > high + 8, $"at a quarter of his blood he grows wings and goes: {e.GlobalPosition.Y - high:0.0}m up");
        await Until(() => f.RivalBody is null, 10);
        Check(f.RivalBody is null && t.Deaths == 0 && t.Edge == 1, "let him go and he's gone, whole, and you're stronger for beating him");
        await Frames(2);
        p.Fit();
        Check(p.Stats.MaxHealth > health, $"which is for good: {health:0} health to {p.Stats.MaxHealth:0}");
        t.NextAt = 0;
        main.MeetTurlinForTest();
        await Frames(2);
        hud.Choose("Fight.");
        await Frames(10);
        e = f.RivalBody!;
        var before = t.Form.Demon;
        t.ChangeChanceForTest = 1; // this death, be sure it takes something
        e.TakeHit(1e7, DamageKind.Fire, AttackKind.Spell, false, p);
        await Until(() => f.RivalBody is null, 4);
        Check(t.Deaths == 1 && t.Self.Name == "Turlin" && t.Form.Demon > before && t.Self.Scars.Count == 1, $"catch him and he dies, and comes back less himself: {t.Form.Is}");
        for (var i = 0; i < 6; i++) t.HeLost("test", DamageKind.Fire, 1, 0, changes: true);
        t.ChangeChanceForTest = null;
        t.NextAt = 0;
        await Teleport(edge);
        main.MeetTurlinForTest();
        await Frames(2);
        Check(f.RivalBody is { } last && last.Kind.Id == "giant_ape" && last.Stats.Name == "Turlin", $"still Turlin at the last, in a {f.RivalBody?.Kind.Name}");
        hud.EndTalk();
        f.RivalBody?.QueueFree();
        await Frames(2);
    }

    /// <summary>Creatures stand on the ground under each foot, look at the hero, and swing their tails.</summary>
    async Task PoiseTest()
    {
        var p = main.PlayerNode;
        // A slope: where the land climbs most within a short walk of the city.
        var world = main.World;
        var best = (At: new Vector3(300, 0, 300), Slope: 0.0);
        for (var r = 350; r < 2500 && best.Slope < 0.35; r += 60)
            for (var a = 0.0; a < Math.Tau; a += 0.25)
            {
                var (x, z) = (Math.Cos(a) * r, Math.Sin(a) * r);
                var h = world.Height(x, z);
                if (h < WorldGen.WaterLevel + 2 || world.SettlementAt(x, z) is not null) continue;
                var slope = Math.Abs(world.Height(x + 2, z) - world.Height(x - 2, z)) / 4;
                if (slope > best.Slope) best = (new Vector3((float)x, 0, (float)z), slope);
            }
        await Teleport(best.At + new Vector3(3, 0, 3));
        await Frames(30);
        var horse = Body.For("horse") as Creature;
        if (horse?.Footing is null) { Check(false, "a horse with known bones"); return; }
        AddChild(horse);
        horse.GlobalPosition = best.At with { Y = City.FloorAt(best.At) };
        horse.LookAt(horse.GlobalPosition + new Vector3(1, 0, 0), Vector3.Up); // across the slope
        horse.Move(0, true, false); // standing, its idle playing
        async Task Passes(int n) { var from = horse.Footing!.Passes; await Until(() => horse.Footing.Passes >= from + n, 30); }
        await Passes(30);
        var lifts = horse.Footing.Lifts;
        Check(lifts.Count == 4, $"four legs on IK (slope {best.Slope:0.00} at {best.At.X:0},{best.At.Z:0})");
        Check(lifts.Max() - lifts.Min() > 0.04f, $"standing across a slope, its feet find the ground: lifts {string.Join(", ", lifts.Select(l => $"{l:+0.00;-0.00}"))} m");
        var skeleton = horse.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
        var seen = horse.Footing.Witness;
        var offGround = seen.FeetAt.Select(f => Math.Abs(f.Y - City.FloorAt(f))).ToList();
        Check(seen.Misses.Count == 4 && seen.Misses.Max() < 0.12f && offGround.Max() < 0.15f,
            $"and the leg IK puts every foot where it's sent: {string.Join(", ", seen.Misses.Select(d => $"{d:0.00}"))} m from its mark, {string.Join(", ", offGround.Select(d => $"{d:0.00}"))} m off the ground, the body settled {horse.Footing.Drop:0.00} m");
        // A look: the hero walks round in front, and the horse's head turns.
        var footing = horse.Footing;
        var head = skeleton.FindBone("Head");
        var ahead = -horse.GlobalTransform.Basis.Z;
        var aside = ahead.Cross(Vector3.Up);
        await Teleport(horse.GlobalPosition + ahead * 5 + aside * 3);
        await Passes(40);
        var turned = footing.Gazing;
        var before = seen.HeadBasis.Y;
        await Teleport(horse.GlobalPosition + ahead * 5 - aside * 3);
        await Passes(40);
        var after = seen.HeadBasis.Y;
        var swung = Mathf.RadToDeg(before.AngleTo(after));
        Check(turned > 0.9f && swung > 20 && footing.HeadTurn > 10, $"it watches the hero: its muzzle swung {swung:0}Â° as they crossed in front (a {footing.HeadTurn:0}Â° turn of the neck now)");
        await Teleport(horse.GlobalPosition - ahead * 30);
        await Passes(40);
        Check(footing.Gazing < 0.1f, "and looks away when they're gone");
        // A sharp turn flings the tail out; it settles again.
        horse.RotateY(1.2f);
        await Passes(3);
        var flung = Math.Abs(footing.TailSwing);
        await Passes(60);
        Check(flung > 4 && Math.Abs(footing.TailSwing) < flung * 0.5f, $"a sharp turn flings its tail {flung:0}Â° aside, and it settles ({Math.Abs(footing.TailSwing):0}Â° after)");
        horse.QueueFree();
        var wolf = Body.For("wolf") as Creature;
        var dragon = Body.For("dragon") as Creature;
        Check(wolf?.Footing is not null && dragon?.Footing is not null, "wolves and dragons have their footing too");
        wolf?.QueueFree();
        dragon?.QueueFree();
    }

    /// <summary>Realistic people: every townsperson a baked body of their people, sex and age, in their trade's clothes, moved by KayKit's clips.</summary>
    async Task HumansTest()
    {
        var hud = main.HudNode;
        if (!Humans.Available) { Check(true, "no baked bodies on this build: the KayKit figures stand in"); return; }
        var was = Humans.Enabled;
        Humans.Enabled = true;
        var who = main.Locals.First(x => x.Npc.Id == "tristinwold/harbourmaster");
        var figure = Figure.Human("guard", who.Npc.Race, who.Npc.Sex, Ageing.Stage(who.Npc.Race, who.Npc.Age), 7);
        Check(figure is not null, $"a body for {who.Npc.Name} ({who.Npc.Race}, {who.Npc.Age}): {Humans.BodyFor(who.Npc.Race, who.Npc.Sex, Ageing.Stage(who.Npc.Race, who.Npc.Age))}");
        if (figure is null) { Humans.Enabled = was; return; }
        AddChild(figure);
        await Frames(5);
        var skeleton = figure.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
        var mesh = figure.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First(m => m.Skin is not null);
        var anim = figure.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
        Check(skeleton.Name == "GeneralSkeleton" && skeleton.FindBone("Hips") >= 0 && skeleton.FindBone("LeftHand") >= 0, $"on Godot's humanoid skeleton ({skeleton.GetBoneCount()} bones)");
        Check(anim.GetAnimationList().Length >= 70 && anim.HasAnimation("Walking_A") && anim.HasAnimation("1H_Melee_Attack_Chop"), $"with KayKit's {anim.GetAnimationList().Length} moves retargeted onto it");
        var colours = mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color].AsColorArray();
        var distinct = colours.Select(c => c.ToHtml(false)).Distinct().Count();
        Check(distinct >= 4, $"dressed: {distinct} colours painted on (skin, tunic, trousers, boots, hair)");
        var box = mesh.GetAabb();
        Check(box.Size.Y is > 1.2f and < 2.6f && box.Position.Y > -0.15f, $"{box.Size.Y:0.00} m tall, feet on the ground");
        // A face: skin from the textures, eyes, brows, lashes and hair, each dressed.
        var parts = Enumerable.Range(1, mesh.Mesh.GetSurfaceCount() - 1).Select(i => mesh.Mesh.SurfaceGetMaterial(i) as StandardMaterial3D).ToList();
        Check(mesh.Mesh.GetSurfaceCount() >= 6 && parts.All(m => m?.AlbedoTexture is not null), $"with a face: {mesh.Mesh.GetSurfaceCount() - 1} parts (eyes, brows, lashes, hair), each with its texture");
        Check(mesh.Mesh.SurfaceGetMaterial(0) is ShaderMaterial skinned && skinned.GetShaderParameter("skin").AsGodotObject() is Texture2D, "and a skin texture under the painted clothes");
        // Real clothes, baked onto the body and dyed for the trade: a shirt, trousers and shoes, each with its own weave.
        var clothes = Enumerable.Range(1, mesh.Mesh.GetSurfaceCount() - 1).Select(i => mesh.Mesh.SurfaceGetMaterial(i) as StandardMaterial3D)
            .Where(m => m?.AlbedoTexture is NoiseTexture2D || m?.AlbedoTexture?.ResourcePath.Contains("shoes") == true).ToList(); // the shirt and trousers dyed over a plain weave, the shoes in their leather
        Check(clothes.Count >= 3 && clothes.All(m => m!.CullMode == BaseMaterial3D.CullModeEnum.Disabled), $"wearing real clothes: {clothes.Count} pieces (a shirt, trousers, shoes), dyed for the trade");
        var arm = skeleton.FindBone("LeftUpperArm");
        var elbow = skeleton.FindBone("LeftLowerArm");
        await Until(() => (skeleton.GetBoneGlobalPose(elbow).Origin - skeleton.GetBoneGlobalPose(arm).Origin).Normalized().Y < -0.6f, 3);
        var hang = (skeleton.GetBoneGlobalPose(elbow).Origin - skeleton.GetBoneGlobalPose(arm).Origin).Normalized();
        Check(hang.Y < -0.6f, $"standing, the arms hang (the upper arm points {hang.Y:0.00} down)");
        figure.Move(3, true, false);
        await Frames(30);
        var hips = skeleton.GetBoneGlobalPose(skeleton.FindBone("Hips")).Origin;
        var foot = skeleton.GetBoneGlobalPose(skeleton.FindBone("LeftFoot")).Origin;
        Check(anim.CurrentAnimation == "Walking_A" && hips.Y > 0.5f && hips.Y > foot.Y + 0.3f, $"walks: hips {hips.Y:0.00} m up, a foot at {foot.Y:0.00}");
        figure.QueueFree();
        // The townsfolk already standing about are these bodies too.
        var people = main.Locals.Count(p => p.Figure.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Any(s => s.Name == "GeneralSkeleton"));
        Check(people > 10, $"{people} of Tristinwold's people wear real bodies (the rest are figures no body was made for)");
        // The hero, as they made themselves: a real body picked by people, sex and age, dressed from the sheet.
        var made = Avatar.For("Ranger") with { Race = "elf", Sex = "Female", Age = "Adult", Face = "Dark", Eyes = "Green", Style = "Ponytail01", Hair = "Blond", Outfit = "Crimson", Height = "Tall", Build = "Slim" };
        var hero = Figure.Create("Ranger", made);
        Check(hero.IsHuman, "a hero made in the creator stands in a real body: an elf woman, a ranger");
        AddChild(hero);
        await Frames(3);
        var herSkeleton = hero.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
        var herMesh = herSkeleton.GetChildren().OfType<MeshInstance3D>().First(m => m.Skin is not null && !m.Name.ToString().StartsWith("BorrowedHair"));
        var herEyes = Enumerable.Range(1, herMesh.Mesh.GetSurfaceCount() - 1).Select(i => herMesh.Mesh.SurfaceGetMaterial(i) as StandardMaterial3D).FirstOrDefault(m => m?.AlbedoTexture?.ResourcePath.Contains("eyes__") == true);
        Check(herEyes?.AlbedoTexture?.ResourcePath.Contains("eyes__green") == true, $"with the eyes she chose ({herEyes?.AlbedoTexture?.ResourcePath.Split('/').Last()})");
        Check(herMesh.Mesh.SurfaceGetMaterial(0) is ShaderMaterial herSkin && (herSkin.GetShaderParameter("skin").AsGodotObject() as Texture2D)?.ResourcePath.Contains("african_female") == true, "the face she chose, on a woman's body");
        Check(herSkeleton.GetChildren().OfType<MeshInstance3D>().Any(m => m.Name.ToString().StartsWith("BorrowedHair")) || Humans.HairOf("elf", Sex.Female, LifeStage.Adult) == "Ponytail01", "and the hair style she chose, borrowed from a body made with it");
        Check(hero.GetChildren().OfType<Node3D>().First().Scale.Y > 1.05f && hero.GetChildren().OfType<Node3D>().First().Scale.X < 0.95f, "tall and slim, as she chose");
        Check(hero.HeldIn("RightHand") is BoneAttachment3D { BoneName: "RightHand" } hand && hand.FindChildren("Hold_2H_Crossbow", "", true, false).Count == 1, "the ranger's crossbow, forged to real size, in a real hand");
        Check(herSkeleton.GetChildren().OfType<HumanGaze>().Any(), "and a head that turns to whoever comes near");
        Check(Armoury.Make("1H_Sword") is { } sword && sword.FindChildren("Blade", "", true, false).Count == 1 && Armoury.Make("Round_Shield") is not null && Armoury.Make("2H_Staff") is not null, "the armoury forges swords, shields and staffs to real proportions");
        hero.QueueFree();
        // Dragonkind grow horns from a real head; a hooded class starts hooded.
        var scaled = Figure.Create("Paladin", Avatar.For("Paladin") with { Race = Races.Dragonkind, Dragon = "Gold", Horns = "Ram" });
        Check(scaled.IsHuman && scaled.FindChildren("Adorn_Head", "", true, false).Count == 1, "a gold dragonkind paladin: a real body with horns from the head");
        scaled.QueueFree();
        Check(Avatar.For("Assassin").Headwear == "Hood", "an assassin starts with the hood up");
        // Everyone else who is somebody gets a body too: a guard, a bandit chief (a head taller than his men), a squire (a youth).
        var guard = Figure.Create("guard");
        var chief = Figure.Create("bandit_chief");
        var squire = Figure.Create("squire");
        Check(guard.IsHuman && chief.IsHuman && squire.IsHuman, "guards, bandit chiefs and squires are real people now, cast for the part");
        Check(chief.GetChildren().OfType<Node3D>().First().Scale.Y > 1.1f, $"the chief stands {chief.GetChildren().OfType<Node3D>().First().Scale.Y:0.00} times his men");
        Check(!Figure.Create("draugr").IsHuman && !Figure.Create("golem").IsHuman, "the undead and the golem keep their own figures");
        foreach (var f in new[] { guard, chief, squire }) f.QueueFree();
        // Worn gear on a real body: armour paints the tunic in its colour and metal, a helm a cap, a sword goes in the hand.
        var armed = Figure.Create("Thief", Avatar.For("Thief"));
        AddChild(armed);
        var pack = new Pack();
        foreach (var id in new[] { "steel_sword", "steel_helm", "iron_mail" }) { pack.Add(id); pack.Equip(id); }
        Wardrobe.Wear(armed, pack);
        await Frames(2);
        var armedMesh = armed.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First().GetChildren().OfType<MeshInstance3D>().First(m => m.Skin is not null);
        Check(armedMesh.Mesh.SurfaceGetMaterial(0) is ShaderMaterial m2 && m2.GetShaderParameter("metal").AsSingle() > 0.5f, "a mail shirt makes the painted tunic metal");
        Check(armed.Held == Grip.OneHand && armed.HeldIn("RightHand")?.FindChildren("Hold_1H_Sword", "", true, false).Count == 1, "and the steel sword, forged in steel, is held one-handed in a real hand");
        armed.QueueFree();
        Humans.Enabled = was;
    }

    /// <summary>The shipyard, a voyage with pirates and a port call, and the pirates' offer to a hero whose ship they take.</summary>
    async Task SeaTest()
    {
        var p = main.PlayerNode;
        var hud = main.HudNode;
        var sea = main.Sea;
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        foreach (var c in p.Companions().ToList()) c.QueueFree();

        // The shipyard: planks off the stack and onto the hull.
        await Teleport(Main.ShipyardAt + new Vector3(0, 0, 1));
        main.Talk(Who("tristinwold/shipwright"));
        Check(hud.Choose("Any work in the yard"), "Osric Tarr at the shipyard puts you to work");
        hud.EndTalk();
        var purse = p.Crowns;
        for (var i = 0; i < SeaBook.PlanksPerJob; i++) main.PlankForTest();
        Check(sea.Wright == WrightRank.Apprentice && sea.Repairs == 1 && p.Crowns == purse + SeaBook.PlanksPerJob * 6 + 5,
            $"four planks fitted to a holed hull: an apprentice shipwright, +{p.Crowns - purse} crowns");

        // Signed on, and out to sea.
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        main.Talk(Who("tristinwold/harbourmaster"));
        Check(hud.Choose("Sign me on") && sea.Rank == SeaRank.Deckhand, "Captain Marit Hale signs you on as a deckhand");
        hud.EndTalk();
        main.SailForTest("port:druitt");
        await Frames(30);
        var at = main.AtSeaNode;
        var v = sea.Voyage;
        if (at is null || v is null) { Check(false, "a voyage under way"); return; }
        (v.Out, v.Home) = ([0.05], []);
        await Until(() => p.IsOnFloor() && Math.Abs(p.GlobalPosition.Y - AtSea.DeckY) < 2.5f, 8);
        Check(p.IsOnFloor() && Math.Abs(p.GlobalPosition.Y - AtSea.DeckY) < 2.5f, $"aboard the Gull, standing on her deck out at sea ({v.LegSeconds / 60:0} minutes to Druitt; hero at {p.GlobalPosition.Y:0.0}, deck {AtSea.DeckY:0.0})");
        await Until(() => at.Pending > 0, 10);
        Check(at.Pending > 0 && hud.Tracked.Any(t => t.Contains("On deck:")), "there's work on deck, and the tracker says what");
        at.WorkForTest();
        Check(v.Chores > 0 && v.Chores == v.ChoresDue, $"the deck work done ({v.Chores}/{v.ChoresDue})");

        // Pirates.
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        Check(at.Boarders.Count == v.Pirates && hud.Heard("Pirates!"), $"pirates come alongside and board: {at.Boarders.Count} of them");
        await Frames(60);
        Check(at.Boarders.All(e => e.GlobalPosition.Y > AtSea.DeckY - 3), "and they fight on her deck, not through it");
        foreach (var e in at.Boarders.ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => sea.Repelled == 1, 8);
        Check(sea.Repelled == 1 && hud.Heard("Beaten off"), "you beat them off");

        // The port, and home.
        at.Along = 0.999;
        await Until(() => v.InPort && at.Agent is not null, 8);
        Check(v.InPort && at.Agent is not null && hud.Heard("Land ho: Druitt"), "Druitt, and its harbour master comes aboard");
        main.Talk(at.Agent!);
        Check(hud.Choose("Cast off for home") && v.Homeward, "you cast off for home");
        purse = p.Crowns;
        at.Along = 0.999;
        await Until(() => sea.Voyage is null, 8);
        await Frames(20);
        Check(sea.Voyages == 1 && p.Crowns > purse && main.AtSeaNode is null && p.GlobalPosition.Y < 50,
            $"home to Tristinwold, paid {p.Crowns - purse} crowns, and ashore");

        // Promotion, then a ship taken by pirates, and their offer.
        sea.Voyages = SeaBook.ToBosun - 1;
        main.SailForTest("port:druitt");
        await Frames(20);
        sea.Voyage!.Out = [];
        sea.Voyage!.Home = [];
        main.AtSeaNode!.Along = 0.999;
        await Until(() => sea.Voyage?.InPort == true, 8);
        sea.CastOff();
        main.AtSeaNode!.LeavePort();
        main.AtSeaNode!.Along = 0.999;
        await Until(() => sea.Voyage is null, 8);
        Check(sea.Rank == SeaRank.Bosun && hud.Heard("makes you bosun"), "three voyages home, and Marit makes you bosun");

        main.SailForTest("port:greywater");
        await Frames(20);
        at = main.AtSeaNode!;
        (sea.Voyage!.Out, sea.Voyage!.Home) = ([0.05], []);
        at.Along = 0.049;
        sea.Voyage!.Crew = null; // nobody small on this run: it's the Teeth
        await Until(() => at.Boarders.Count > 0, 20);
        sea.Hull = 0.5;
        await Until(() => hud.Talking, 6);
        Check(hud.TalkTitle.Contains("Black Ysolde") && hud.Choose("Join the pirates"), "the pirates take the ship, and their captain offers you a place");
        await Frames(20);
        Check(sea.Outlaw && sea.Pirate == PirateRank.Swab && sea.Rank == SeaRank.Landsman && main.AtSeaNode is null, "you're a swab of the Widow's Tooth, struck off the company's books");

        // Out on the account: a merchantman to take.
        await Teleport(Main.Smugglers + new Vector3(1, 0, -1.5f));
        main.Talk(Who("tristinwold/smuggler"));
        Check(hud.Choose("Row me out"), "Nell Corran rows you out to the Widow's Tooth");
        await Frames(20);
        at = main.AtSeaNode!;
        (sea.Voyage!.Out, sea.Voyage!.Home) = ([0.05], []);
        at.Along = 0.049;
        await Until(() => at.Boarders.Count > 0, 20);
        Check(at.Boarders.All(e => e.Kind.Id == "marine"), "her marines come to meet you");
        foreach (var e in at.Boarders.ToList()) e.TakeHit(1e7, DamageKind.Sun, AttackKind.Spell, false, p);
        await Until(() => sea.Prizes == 1, 8);
        purse = p.Crowns;
        at.Along = 0.999;
        await Until(() => sea.Voyage is null, 8);
        Check(sea.Prizes == 1 && sea.Raids == 1 && p.Crowns > purse, $"a prize taken, and your share at the Teeth: {p.Crowns - purse} crowns");

        // And back to honest work, for a price.
        p.Crowns += SeaBook.PardonPrice;
        await Teleport(Main.HarbourPost + new Vector3(1.5f, 0, 1.5f));
        main.Talk(Who("tristinwold/harbourmaster"));
        Check(hud.Choose("I want the company's pardon") && !sea.Outlaw && sea.Rank == SeaRank.Deckhand, "the company's pardon, bought: a deckhand again");
        hud.EndTalk();
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
        Check(old.Count == 3 && trade.Routes.All(m => main.Explored.Rumored.Contains(m.Id) || main.Explored.Discovered.Contains(m.Id)),
            $"Tristinwold's old trade routes are open from the start: {string.Join(" and ", old)} (on the map)");
        Check(hud.Tracked.Any(t => t.StartsWith("Starter work: the caravan yard")), "and a starter's job on the tracker: the caravan yard is hiring guards");
        var village = main.World.SettlementsNear(0, 0, 2500).Where(s => TownMarkets.Trades(s) && !trade.Towns.ContainsKey(s.Id)).OrderBy(s => s.DistanceTo(0, 0)).First();
        await Teleport(new Vector3((float)village.X + 5, 0, (float)village.Z + 5));
        await Frames(30);
        Check(trade.Routes.Any(m => m.Id == village.Id) && trade.Routes.Count() == 4, $"finding {village.Name} opens a fourth trade route");
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
        Check(main.Bonds.Any(b => b.Hitched) && !main.Bonds.Any(b => b.Steed && b.Following), "a horse you buy for the wagon goes in its shafts");
        main.UnhitchForTest();
        await Frames(30);
        main.RideNow();
        await Frames(20);
        Check(p.Mount is { } mount && mount.Bond?.Steed == true, $"unhitched, you can ride your horse ({p.Mount?.Stats.Name})");
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
        Check(hud.Choose("Hitch up my wagon") && !trade.WagonOut && trade.Capacity == 0 && hud.TalkText.Contains("can't carry you and pull"), "the horse at your side can't also pull the wagon");
        hud.EndTalk();
        main.HitchForTest();
        await Frames(10);
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Hitch up my wagon") && trade.WagonOut && trade.Capacity == TradeBook.PerHorse && main.WagonNode is { Teamed: true }, $"hitched: room for {trade.Capacity} loads");
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
        Check(hud.Choose("The short road") && hud.Choose("Guard") && trade.Guarding is { Bandits: 2 }, "and you can take the short road again");
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

        // A hundred of anything sold, and it's cheap for a couple of days (gear too, not only cartload goods).
        var sword = Items.Get("iron_sword")!;
        var full = main.Economy.Price(sword);
        for (var i = 0; i < 100; i++) main.Economy.Sold("iron_sword", 1);
        Check(main.Economy.Price(sword) <= full / 2, $"a hundred swords sold to the shops: a sword goes from {full} to {main.Economy.Price(sword)} crowns");

        // A caravan lost on a road: known trouble. Garrick pays over the odds to guard it, and a bounty to clear the camp.
        var ways = main.Economy.Roads;
        var route = ways.Known.First();
        ways.RaidersAbout(route.Id, main.Economy.Day, "the Bone Fang");
        ways.Road(route.Id).Lost.Add(main.Economy.Day);
        var camp = new RoadTrouble { Id = "road-test", Town = route.Id, TownName = route.Name, X = route.X * 0.5 + 60, Z = route.Z * 0.5, By = "bandits", Bandits = 3, Bounty = 80, Day = main.Economy.Day };
        ways.Troubles.Add(camp);
        await Teleport(Main.YardAt + new Vector3(0, 0, 4));
        main.Talk(Who("tristinwold/caravans"));
        hud.Choose("Any caravans that need a guard");
        var priced = hud.Options.FirstOrDefault(o => o.Contains(route.Name) && o.Contains("known trouble"));
        Check(priced is not null && hud.TalkText.Contains("known trouble") && hud.TalkText.Contains("times the rate"), $"known danger pays: {priced}");
        hud.EndTalk();
        main.Talk(Who("tristinwold/caravans"));
        Check(hud.Choose("Trouble on the roads") && hud.Options.Any(o => o.Contains(route.Name) && o.Contains("80 crowns")), "and he has a camp to clear: " + hud.Options.FirstOrDefault(o => o.Contains(route.Name)));
        hud.Choose(hud.Options.First(o => o.Contains(route.Name) && o.Contains("80 crowns")));
        await Frames(40);
        Check(trade.Hunting == "road-test" && hud.Tracked.Any(t => t.StartsWith("Garrick's bounty")), "you take the bounty, and it's on the tracker");
        hud.EndTalk();
        await Teleport(new Vector3((float)camp.X + 25, 0, (float)camp.Z + 25));
        bool AtCamp(Enemy e) => e.Kind.Id is "bandit" or "bandit_chief" && new Vector2(e.GlobalPosition.X - (float)camp.X, e.GlobalPosition.Z - (float)camp.Z).Length() < 12;
        await Until(() => main.Enemies().Count(AtCamp) >= 3, 8);
        var robbers = main.Enemies().Where(AtCamp).ToList();
        Check(robbers.Count == 3 && hud.Heard("round a fire"), "the camp: three bandits round their fire");
        var purseC = p.Crowns;
        foreach (var r in robbers) await FightUntilDead(r, 60, chase: true);
        await Until(() => trade.Hunting is null, 6);
        Check(trade.Hunting is null && p.Crowns >= purseC + 80 && ways.Troubles.All(t => t.Id != "road-test") && hud.Heard("open again"),
            $"cleared: +{p.Crowns - purseC} crowns from Garrick, and the road to {route.Name} is open again");
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

    /// <summary>Dagna's job to its end: the wall comes down, the dead come through, Tam's held, the deep's open, and she pays.</summary>
    async Task TheOldFace()
    {
        Person Who(string id) => main.Locals.First(x => x.Npc.Id == id);
        var (p, hud, book, cave) = (main.PlayerNode, main.HudNode, main.MineLog, main.CaveNode);
        if (cave is null) { Check(false, "down the mine for the old face"); return; }
        Check(cave.WallStands && cave.Dens.All(e => e.Kind.Id == "giant_rat"), "the deep is walled off, and only rats live on this side of it");
        book.Worked = MineBook.VeinsToProve - 1;
        Check(book.VeinWorked() && book.Stage == MineStage.AtTheFace, "enough veins, and Dagna's ready to send you to the old face");
        await Teleport(Cave.FaceAt + new Vector3(0, 0, 3));
        main.BreakFaceForTest();
        await Until(() => cave.Wave.Count > 0, 10);
        Check(book.Stage == MineStage.Breached && !cave.WallStands && cave.Wave.Count == MineBook.Wave(0).Sum(w => w.Count), $"the face comes down, and {cave.Wave.Count} of the dead come through");
        var waves = 0;
        for (var t = 0.0; t < 150 && book.Stage == MineStage.Breached; t += 0.5)
        {
            var next = cave.Wave.FirstOrDefault(e => IsInstanceValid(e) && e.State != EnemyState.Dead);
            if (next is null) { await Frames(30); continue; }
            book.Tam = MineBook.TamHealth; // the scripted player isn't being tested on their footwork
            await FightUntilDead(next, 15, chase: true);
            waves = book.Waves;
        }
        Check(book.DeepOpen, $"you hold them through {MineBook.WaveCount} waves ({book.Stage}, {waves} beaten)");
        Check(cave.Dens.Any(e => e.Kind.Id == "draugr"), "and the dead walk the deep from now on");
        await Teleport(Cave.InRoom("gold_seam"));
        Check(City.AreaAt(p.GlobalPosition) == "The Gold Seam" && p.GlobalPosition.Y < Cave.Landing.Y - 30, $"you can stand at the bottom of the mine, {Cave.Landing.Y - p.GlobalPosition.Y:0} m down");
        await Until(() => main.WorkingsNode.Shown.Any(s => s.Kind == "gold"), 5);
        Check(main.WorkingsNode.Shown.Any(s => s.Kind == "gold"), "where there's gold in the walls");
        await main.LeaveMineForTest();
        await Frames(10);
        Check(!main.InMine && p.GlobalPosition.Y < 100 && City.Sun.Visible, "the way up brings you back to daylight");
        await Teleport(Main.QuarryAt + new Vector3(5, 0, 6));
        var purse = p.Crowns;
        main.Talk(Who(Shops.Quarry.Id));
        Check(hud.Choose("It's done") && p.Crowns == purse + MineBook.Pay && book.Stage == MineStage.Done, $"Dagna pays {MineBook.Pay} crowns");
        hud.EndTalk();
        var saved = CompanySave.FromJson(main.Save());
        Check(saved.Mine?.Stage == MineStage.Done, "and the save remembers it");
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
        var sword = main.ShopPriceFor(Items.Get("iron_sword")!); // their people's markup, the city's regard, their standing and their perks (and dearer once bought: one fewer on her rack)
        Check(main.HudNode.Choose("Iron Sword"), "and sells you an iron sword");
        Check(pack.Count("iron_sword") == 1 && p.Crowns == 200 - sword, $"for {sword} crowns ({p.Crowns} left of 200)");
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
        Check(main.HudNode.Choose("Make Iron Ingot") && pack.Count("iron_ingot") == 0, "a forge with nothing to burn won't work the iron");
        pack.Add("pine_log", 2);
        main.HudNode.Choose("Make Iron Ingot");
        Check(pack.Count("iron_ingot") >= 1 && pack.Count("pine_log") == 1, "with a log on the fire you smelt scrap into an iron ingot");
        Check(pack.JobXp.GetValueOrDefault(Job.Smithing) > 0, "and learn a little smithing");
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
