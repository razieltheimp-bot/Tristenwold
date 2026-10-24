using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Plays scripted fights headless and quits with 0 (pass) or 1 (fail), so the game can be
/// checked without anyone at the keyboard:
///   godot --headless --path game -- --selftest [--server 127.0.0.1:7777 --company Joe]
/// Alone it checks a fight is fought and reported. With a server it also loses a fight, waits
/// for that enemy to come back as a nemesis, and kills it.
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

    Enemy Spawn(string kind, Vector3 at)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at);
        main.AddEnemy(e);
        return e;
    }

    async Task FightUntilDead(Enemy e, double seconds, bool spells = false)
    {
        var p = main.PlayerNode;
        for (var t = 0.0; t < seconds && e.State != EnemyState.Dead; t += 1.0 / 60)
        {
            if (p.Downed) { await Until(() => !p.Downed, 10); p.GlobalPosition = e.GlobalPosition + new Vector3(0, 0, 2); }
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
