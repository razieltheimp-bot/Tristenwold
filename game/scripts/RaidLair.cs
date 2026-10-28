using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A raid's lair and its fight (see <see cref="Raids"/>): the Ashen Crypt, a goblin warren, a bear's den, a ruin, a
/// sunken forge, a dead fort, a thunderhawk's eyrie. Come close while it's awake and the boss's own come at you
/// first; put them down and the boss comes out. Every boss starts out a nameless one of its kind, with its own moves
/// (see <see cref="BossBrain"/>). Beat it and its hoard is yours; the lair falls quiet, and in a while it wakes again.
/// If the company runs, everything goes back to waiting. But if the boss beats the company, it takes a name, grows
/// stronger and walks out into the world to roam (see <see cref="Roamers"/>), and when the lair's timer runs out a
/// new nameless one has moved in.
/// </summary>
public partial class RaidLair(Main main, Raid raid) : Node3D
{
    enum Phase { Waiting, Thralls, Boss, Spoils }

    Phase _phase;
    LairLayout? _site;
    readonly List<Enemy> _thralls = [];
    Enemy? _boss;

    public Raid Raid => raid;
    public Pickup? Hoard { get; private set; }
    public Enemy? Boss => _boss;
    public IReadOnlyList<Enemy> Thralls => _thralls;
    public bool Fighting => _phase is Phase.Thralls or Phase.Boss;
    public bool Built => _site is not null;

    Vector3 Centre => new((float)raid.X, City.FloorAt(new Vector3((float)raid.X, 0, (float)raid.Z)), (float)raid.Z);

    bool Awake => main.RaidLog.Ready(raid, main.GuildBook.PlaySeconds);

    public override void _Process(double delta)
    {
        var p = main.PlayerNode;
        var d = new Vector2(p.GlobalPosition.X - (float)raid.X, p.GlobalPosition.Z - (float)raid.Z).Length();
        if (_site is null && d < 350 && main.TerrainNode.HasGround(Centre))
        {
            _site = LairSites.Build(raid, Centre, this);
            main.RaidLog.Saw(raid);
        }
        else if (_site is not null && d > 500 && _phase == Phase.Waiting) { _site.Root.QueueFree(); _site = null; }
        if (_site is null) return;

        switch (_phase)
        {
            case Phase.Waiting when Awake && d < 40 && !p.Downed:
                Wake();
                break;
            case Phase.Boss when p.Downed && _boss is not null && IsInstanceValid(_boss) && _boss.State != EnemyState.Dead:
                Escape();
                break;
            case Phase.Thralls or Phase.Boss when p.Downed || d > 120:
                Reset(p.Downed ? $"You fall in {raid.Name}. Its creatures go back to waiting." : $"You leave {raid.Name} behind. Its creatures go back to waiting.");
                break;
            case Phase.Thralls when _thralls.All(t => !IsInstanceValid(t) || t.State == EnemyState.Dead):
                RaiseBoss();
                break;
            case Phase.Boss when _boss is null || !IsInstanceValid(_boss) || _boss.State == EnemyState.Dead:
                Fallen();
                break;
        }
    }

    // ---- the fight ------------------------------------------------------------------------------

    Enemy Rise(string kind, Vector3 at, (double Health, double Damage) numbers, string clip, double delay)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.4f }, look: kind);
        e.InLair = true;
        e.Tune(numbers.Health, numbers.Damage);
        e.Roam = 6;
        main.AddEnemy(e);
        e.Figure.Perform(clip, delay, 1);
        BossBrain.Turn(e, main.PlayerNode.GlobalPosition);
        GetTree().CreateTimer(delay).Timeout += () => { if (IsInstanceValid(e) && e.State != EnemyState.Dead) e.Provoke(main.PlayerNode); };
        return e;
    }

    bool Crypt => raid.Site == LairSite.Crypt;

    Enemy Thrall(Vector3 at) => Rise(raid.ThrallKind, at, Raids.Thrall(raid, main.PlayerNode.Hero.Level), Crypt ? "Spawn_Ground_Skeletons" : "Cheer", Crypt ? 1.4 : 0.8);

    void Wake()
    {
        _phase = Phase.Thralls;
        var level = main.PlayerNode.Hero.Level;
        main.HudNode.Toast(Raids.Outgrown(raid, level)
            ? $"{Cap(raid.Name)} stirs, half-heartedly. You've grown past this place; there's little here for you now."
            : Raids.Daunting(raid, level) ? $"{raid.Waking} (This place is beyond you yet: what lives here is made for heroes of level {raid.MinLevel} and up.)"
            : raid.Waking, 9);
        for (var i = 0; i < raid.Thralls; i++)
        {
            var a = i * Mathf.Tau / raid.Thralls + 0.4f;
            _thralls.Add(Thrall(_site!.ThrallsAt + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _site.ThrallSpread));
        }
    }

    void RaiseBoss()
    {
        _phase = Phase.Boss;
        var level = main.PlayerNode.Hero.Level;
        _boss = Rise(raid.BossKind, _site!.BossAt, Raids.Boss(raid, level), Crypt ? "Skeletons_Awaken_Standing" : "Cheer", Crypt ? 2.4 : 1.2);
        _boss.Roam = 4;
        var brain = new BossBrain(main, raid, raid.BossCap, at => _thralls.Add(Thrall(at)));
        _boss.Think = brain.Think;
        Effects.Burst(GetParent(), _site.BossAt + Vector3.Up * 2, raid.Element, 4);
        main.HudNode.Toast($"{raid.BossCap} {(Crypt ? "rises from the ash" : "comes out")}. \"{raid.Taunt}\"", 9);
    }

    /// <summary>The boss beat the company: it takes a name and leaves to roam, and the lair waits for the next.</summary>
    void Escape()
    {
        var boss = _boss!;
        var roamer = main.RaidLog.Escaped(raid, main.GuildBook.PlaySeconds, (ulong)GD.Randi() << 32 | GD.Randi());
        foreach (var t in _thralls.Where(t => IsInstanceValid(t) && t.State != EnemyState.Dead)) t.Collapse();
        _thralls.Clear();
        _boss = null;
        _phase = Phase.Waiting;
        main.RoamersNode.Adopt(roamer, boss);
        main.HudNode.Toast($"{raid.BossCap} stands over you, and takes a name for itself: {roamer.Name}. It leaves {raid.Name} to roam the wilds, stronger for beating you. (A new deviant.) Another, nameless and no stronger than its kind, is already moving into the lair.", 12);
        main.HudNode.Log($"Deviant: {roamer.Title} beat you at {raid.Name}, took a name and went roaming.");
    }

    void Fallen()
    {
        _phase = Phase.Spoils;
        foreach (var t in _thralls.Where(t => IsInstanceValid(t) && t.State != EnemyState.Dead)) t.Collapse();
        main.HudNode.Toast(raid.Falling, 9);
        Hoard = new Pickup { QuestId = raid.Id, Item = raid.Hoard };
        AddChild(Hoard);
        Hoard.GlobalPosition = _site?.HoardAt ?? Centre;
    }

    /// <summary>The hoard taken: the reward, and the lair falls quiet until it wakes again.</summary>
    public (int Xp, int Crowns, bool First) Loot()
    {
        var first = main.RaidLog.ClearsOf(raid) == 0;
        var reward = Raids.Reward(raid, main.PlayerNode.Hero.Level, first);
        main.RaidLog.Cleared(raid, main.GuildBook.PlaySeconds);
        Hoard?.QueueFree();
        Hoard = null;
        _phase = Phase.Waiting;
        _thralls.Clear();
        _boss = null;
        return (reward.Xp, reward.Crowns, first);
    }

    void Reset(string why)
    {
        foreach (var t in _thralls.Where(IsInstanceValid)) t.QueueFree();
        if (_boss is not null && IsInstanceValid(_boss)) _boss.QueueFree();
        _thralls.Clear();
        _boss = null;
        _phase = Phase.Waiting;
        main.HudNode.Toast(why, 7);
    }

    /// <summary>A line for the quest tracker, for heroes it suits.</summary>
    public string? Tracker(Vector3 from, Func<Vector3, (double, double), string> direction)
    {
        var left = main.RaidLog.SecondsLeft(raid, main.GuildBook.PlaySeconds);
        var state = Fighting ? "fighting" : left > 0 ? $"wakes in {Math.Ceiling(left / 60)} min" : "awake";
        return $"Raid: {Cap(raid.Name)}, {state} ({direction(from, (raid.X, raid.Z))})";
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
