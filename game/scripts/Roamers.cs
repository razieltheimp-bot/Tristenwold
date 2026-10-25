using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The lair bosses that beat the company and walked out into the world with a name (see <see cref="Roamer"/>). Each
/// wanders the wilds on its own path, whether anyone is near or not; come within sight of where it is and it's there,
/// walking. It knows you, and comes for you. Every time it beats you again it grows stronger; kill it and it's gone
/// for good, and the price on its head is yours. In the shared world it is the nemesis the server named, on the
/// roster with the rest.
/// </summary>
public partial class Roamers(Main main) : Node
{
    sealed class Out(Roamer roamer, Enemy body, BossBrain brain)
    {
        public Roamer Roamer => roamer;
        public Enemy Body => body;
        public BossBrain Brain => brain;
        public readonly List<Enemy> Adds = [];
        public double Taunted = -999;
    }

    readonly List<Out> _out = [];
    readonly List<Roamer> _fallen = [];
    double _tick;

    public const float SightRange = 200, LoseRange = 320;

    /// <summary>Roamers out walking near the hero now, with their bodies.</summary>
    public IEnumerable<(Roamer Roamer, Enemy Body)> Near => _out.Where(o => IsInstanceValid(o.Body)).Select(o => (o.Roamer, o.Body));

    public override void _Ready()
    {
        Enemy.Slain += OnSlain;
        main.PlayerNode.WentDown += OnHeroDown;
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        main.PlayerNode.WentDown -= OnHeroDown;
    }

    Vector3 Where(Roamer r)
    {
        var (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        var at = new Vector3((float)x, 0, (float)z);
        return at with { Y = City.FloorAt(at) };
    }

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var p = main.PlayerNode;
        _out.RemoveAll(o => !IsInstanceValid(o.Body));
        foreach (var r in main.RaidLog.Roamers.ToList())
        {
            var o = _out.FirstOrDefault(o => o.Roamer.Id == r.Id);
            if (o is null)
            {
                var at = Where(r);
                if (!p.Downed && Flat(at, p.GlobalPosition) < SightRange && main.TerrainNode.HasGround(at)) Spawn(r, at, null);
                continue;
            }
            var d = Flat(o.Body.GlobalPosition, p.GlobalPosition);
            if (d > LoseRange || p.Downed && d > 60)
            {
                foreach (var add in o.Adds.Where(IsInstanceValid)) add.QueueFree();
                o.Body.QueueFree();
                _out.Remove(o);
                continue;
            }
            if (o.Body.State == EnemyState.Dead) continue;
            if (!o.Body.FightingCompany) o.Body.Wander(Where(r));
            // It knows you. It comes for you.
            if (!p.Downed && d < 32 && !o.Body.FightingCompany)
            {
                o.Body.Provoke(p);
                if (main.GuildBook.PlaySeconds - o.Taunted > 90)
                {
                    o.Taunted = main.GuildBook.PlaySeconds;
                    main.HudNode.Toast($"{r.Title}: \"{Taunt(r)}\"", 6);
                }
            }
        }
    }

    static string Taunt(Roamer r) => r.Wins switch
    {
        1 => "You again. I remember you face down in the dirt.",
        2 => "Twice I've put you down. Three's a charm.",
        _ => $"{r.Wins} times now. You never learn.",
    };

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    Out Spawn(Roamer r, Vector3 at, Enemy? body)
    {
        var raid = r.Raid;
        if (body is null)
        {
            body = Enemy.Create(Bestiary.Get(raid.BossKind), at + Vector3.Up * 0.4f, look: raid.BossKind);
            main.AddEnemy(body);
        }
        Out? o = null;
        var brain = new BossBrain(main, raid, r.Name, spot => o?.Adds.Add(Add(raid, spot)));
        o = new Out(r, body, brain);
        Strengthen(o);
        body.Think = brain.Think;
        body.Roam = 12;
        _out.Add(o);
        return o;
    }

    Enemy Add(Raid raid, Vector3 at)
    {
        var e = Enemy.Create(Bestiary.Get(raid.ThrallKind), at with { Y = City.FloorAt(at) + 0.4f }, look: raid.ThrallKind);
        var (health, damage) = Raids.Thrall(raid, main.PlayerNode.Hero.Level);
        e.Tune(health * 1.2, damage * 1.1);
        main.AddEnemy(e);
        e.Provoke(main.PlayerNode);
        return e;
    }

    /// <summary>Its numbers, for its wins and the hero's level; its name on its plate.</summary>
    void Strengthen(Out o)
    {
        var (health, damage) = Roaming.Numbers(o.Roamer, main.PlayerNode.Hero.Level);
        o.Body.Tune(health, damage);
        o.Body.Crown(o.Roamer.Title, o.Roamer.NemesisId);
        o.Brain.Name = o.Roamer.Name;
    }

    /// <summary>The lair boss that just beat the company: it takes its name and walks away.</summary>
    public void Adopt(Roamer r, Enemy boss) => Spawn(r, boss.GlobalPosition, boss);

    /// <summary>The hero went down: if a roamer was at them, it has beaten them again, and grows.</summary>
    void OnHeroDown()
    {
        var p = main.PlayerNode;
        foreach (var o in _out.Where(o => IsInstanceValid(o.Body) && o.Body.State != EnemyState.Dead
                     && (p.LastStruck == o.Body || o.Body.FightingCompany && Flat(o.Body.GlobalPosition, p.GlobalPosition) < 40)).ToList())
        {
            o.Roamer.Wins++;
            Strengthen(o);
            main.HudNode.Toast($"{o.Roamer.Title} stands over you. That's {o.Roamer.Wins} times it has beaten you, and it grows stronger every time.", 10);
        }
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        var o = _out.FirstOrDefault(o => o.Body == victim);
        if (o is null) return;
        var r = o.Roamer;
        main.RaidLog.Slain(r);
        _out.Remove(o);
        foreach (var add in o.Adds.Where(a => IsInstanceValid(a) && a.State != EnemyState.Dead)) add.Collapse();
        if (!byCompany)
        {
            main.HudNode.Toast($"{r.Title} has been killed, and not by you.", 8);
            return;
        }
        var (xp, septims) = Roaming.Bounty(r, main.PlayerNode.Hero.Level);
        main.PlayerNode.Septims += septims;
        var spoils = Loot.Hoard(r.RaidId, new Tristinwold.Nemesis.Rng((ulong)GD.Randi()), first: true); // it carried the best of its lair
        main.Take(spoils);
        main.HudNode.Log($"From {r.Title}: {Loot.Describe(spoils)}");
        main.GainXp(xp);
        _fallen.Add(r);
        main.HudNode.Toast($"{r.Title} is dead, for good. The price on its head: {septims} septims. (+{xp} experience)", 12);
        main.HudNode.Log($"Nemesis slain: {r.Title}.");
    }

    // ---- the shared world -----------------------------------------------------------------------

    /// <summary>
    /// The shared world named a nemesis: if it's a lair boss that just beat us and walked off, that's ours, and from
    /// now on it goes by the server's name and the fights with it count on the roster.
    /// </summary>
    public void Named(Tristinwold.Nemesis.Nemesis n)
    {
        var r = main.RaidLog.Roamers.Where(r => r.NemesisId is null && r.Raid.BossKind == n.Kind && main.GuildBook.PlaySeconds - r.Since < 180)
            .OrderByDescending(r => r.Since).FirstOrDefault();
        if (r is null) return;
        var was = r.Title;
        (r.NemesisId, r.Name) = (n.Id, n.FullName);
        if (_out.FirstOrDefault(o => o.Roamer == r) is { } o) Strengthen(o);
        main.HudNode.Log($"{was} is known across the shared world as {r.Title}.");
    }

    /// <summary>The shared world says one we killed wasn't as dead as it looked: it's out there again.</summary>
    public void CheatedDeath(Guid nemesisId)
    {
        if (_fallen.FirstOrDefault(r => r.NemesisId == nemesisId) is not { } r) return;
        _fallen.Remove(r);
        // Back from the dead at its old lair, and off again on a new path.
        r = new Roamer { Id = r.Id, RaidId = r.RaidId, Name = r.Name, Wins = r.Wins + 1, Seed = r.Seed + 1, Since = main.GuildBook.PlaySeconds, NemesisId = r.NemesisId };
        main.RaidLog.Roamers.Add(r);
        main.HudNode.Toast($"{r.Title} wasn't as dead as it looked. It's out there again, scarred and angrier.", 10);
    }

    /// <summary>A line for the quest tracker: the nearest roamer the hero might meet.</summary>
    public string? Tracker(Vector3 from, Func<Vector3, (double, double), string> direction)
    {
        var near = main.RaidLog.Roamers.Select(r => (Roamer: r, At: Where(r))).OrderBy(x => Flat(x.At, from)).FirstOrDefault();
        if (near.Roamer is null || Flat(near.At, from) > 900) return null;
        return $"Nemesis: {near.Roamer.Title} ({direction(from, (near.At.X, near.At.Z))})";
    }

    /// <summary>Where each roamer is now, for the map.</summary>
    public IEnumerable<(double X, double Z)> Spots => main.RaidLog.Roamers.Select(r => Where(r)).Select(v => ((double)v.X, (double)v.Z));
}
