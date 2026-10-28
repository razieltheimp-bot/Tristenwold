using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The barbarian clans of the frontier, out in the world (see <see cref="Warband"/>). Come near one and it's there:
/// camped on the road between raids, or down on some hamlet or farmstead with its couple of guards fighting for their
/// lives. Break the raid (bring down every raider) and the place pays you, and the clan pulls back for an hour. A
/// raid nobody stops plunders the place: if it's one of Tristinwold's trade routes, its market loses nearly half of
/// everything, and word comes to the city.
/// </summary>
public partial class Raiders(Main main) : Node
{
    sealed class Band(Warband w, int round, Settlement? raiding, List<Enemy> bodies)
    {
        public Warband W => w;
        public int Round => round;
        public Settlement? Raiding => raiding;
        public List<Enemy> Bodies => bodies;
    }

    readonly Dictionary<string, Band> _out = [];
    double _tick, _plunderTick = 5;

    public RaidRecord Record { get; set; } = new();

    /// <summary>The clans out near the hero now (for the self-test).</summary>
    public IEnumerable<(Warband W, Settlement? Raiding, IReadOnlyList<Enemy> Bodies)> Out => _out.Values.Select(b => (b.W, b.Raiding, (IReadOnlyList<Enemy>)b.Bodies));

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var t = main.GuildBook.PlaySeconds;
        var hero = main.PlayerNode.GlobalPosition;
        TendBands(t, hero);
        FindNearest(t, hero);
        if ((_plunderTick -= 1) <= 0)
        {
            _plunderTick = 20;
            Plunder(t, hero);
        }
    }

    void TendBands(double t, Vector3 hero)
    {
        // Out and about near the hero.
        foreach (var w in Warband.Near(hero.X, hero.Z, 500))
        {
            if (_out.ContainsKey(w.Id) || Record.IsBroken(w, t)) continue;
            var (round, x, z, raiding) = w.Where(main.World, t);
            if (new Vector2((float)x - hero.X, (float)z - hero.Z).Length() > 450) continue;
            Spawn(w, round, x, z, raiding);
        }
        foreach (var b in _out.Values.ToList())
        {
            var alive = b.Bodies.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).ToList();
            if (alive.Count == 0)
            {
                Broken(b, t);
                continue;
            }
            var centre = alive.Aggregate(Vector3.Zero, (s, e) => s + e.GlobalPosition) / alive.Count;
            var (round, x, z, _) = b.W.Where(main.World, t);
            var drifted = new Vector2((float)x - centre.X, (float)z - centre.Z).Length() > 200 && alive.All(e => e.State == EnemyState.Idle);
            if (centre.DistanceTo(hero) > 650 || drifted || round != b.Round && alive.All(e => e.State == EnemyState.Idle))
            {
                foreach (var e in alive) e.QueueFree();
                _out.Remove(b.W.Id);
            }
        }
    }

    void Spawn(Warband w, int round, double x, double z, Settlement? raiding)
    {
        var bodies = new List<Enemy>();
        var danger = WorldGen.Danger(x, z);
        for (var i = 0; i < w.Raiders; i++)
        {
            var a = i * Mathf.Tau / w.Raiders;
            var r = raiding is null ? 5f : (float)raiding.Radius * 0.4f;
            var at = new Vector3((float)x + Mathf.Cos(a) * r, 0, (float)z + Mathf.Sin(a) * r);
            var chief = i == 0;
            var e = Enemy.Create(Bestiary.Get(chief ? "raider_chief" : "raider"), at with { Y = City.FloorAt(at) + 0.5f }, danger);
            e.Raider = true;
            e.Roam = raiding is null ? 10 : (float)raiding.Radius * 0.6f;
            if (chief) e.Stats.Name = $"{w.Chief}, warchief of {w.Name}";
            main.AddEnemy(e);
            e.RefreshPlate();
            bodies.Add(e);
        }
        // Down on a place: they go for its guards.
        if (raiding is not null)
            foreach (var (e, g) in bodies.Zip(main.PlacesNode.GuardsOf(raiding.Id).Concat(main.PlacesNode.GuardsOf(raiding.Id)).Concat(main.PlacesNode.GuardsOf(raiding.Id))))
                e.Distract(g);
        _out[w.Id] = new Band(w, round, raiding, bodies);
        main.HudNode.Toast(raiding is null
            ? $"Smoke on the road ahead: {w.Name}, barbarian raiders under {w.Chief}, camped between raids."
            : $"Raiders! {w.Name} under {w.Chief} have fallen on {raiding.Name}. Its {Warband.Guards(raiding)} guard{(Warband.Guards(raiding) == 1 ? "" : "s")} can't hold them alone.", 8);
    }

    void Broken(Band b, double t)
    {
        _out.Remove(b.W.Id);
        Record.Break(b.W, b.Round, t);
        if (b.Raiding is { } place)
        {
            main.PlayerNode.Crowns += b.W.Reward;
            Sound.Ui("coin", -2);
            main.HudNode.Toast($"You've broken the raid on {place.Name}. {b.W.Name} falls back to lick its wounds, and {place.Name} pays you {b.W.Reward} crowns from what you saved.", 9);
        }
        else main.HudNode.Toast($"You've broken {b.W.Name}. It'll be a while before they raid again.", 7);
        main.Save();
    }

    /// <summary>Raids nobody stops, on places the hero knows: the place's market is plundered, and word comes to the city.</summary>
    void Plunder(double t, Vector3 hero)
    {
        var day = main.Economy.Day;
        foreach (var m in main.TradeLog.Towns.Values.ToList())
        {
            if (main.World.Find(m.Id) is not { } place || !Warband.Raidable(place)) continue;
            if (hero.DistanceTo(new Vector3((float)m.X, hero.Y, (float)m.Z)) < 600) continue; // the hero's there: it's a fight, not a story
            foreach (var w in Warband.Near(m.X, m.Z, 0))
            {
                if (Record.IsBroken(w, t)) continue;
                var (round, _, _, raiding) = w.Where(main.World, t);
                if (raiding?.Id != m.Id || !Record.Reckon(w, round)) continue;
                var loads = m.Plundered(day);
                main.HudNode.Toast($"Word from the road: {w.Name} under {w.Chief} raided {m.Name} and carried off {loads} loads from its stores.", 8);
                main.HudNode.Log($"Raiders: {w.Name} plundered {m.Name}.");
            }
        }
    }

    (Warband W, double X, double Z, Settlement? Raiding)? _nearest;

    /// <summary>The nearest clan within a mile and a half, worked out once a second.</summary>
    void FindNearest(double t, Vector3 pos) => _nearest = Warband.Near(pos.X, pos.Z, 1500).Where(w => !Record.IsBroken(w, t))
        .Select(w => (W: w, At: w.Where(main.World, t)))
        .Select(x => (x.W, x.At.X, x.At.Z, x.At.Raiding, D: new Vector2((float)x.At.X - pos.X, (float)x.At.Z - pos.Z).Length()))
        .Where(x => x.D < 1500).OrderBy(x => x.D).Select(x => ((Warband, double, double, Settlement?)?)(x.W, x.X, x.Z, x.Raiding)).FirstOrDefault();

    /// <summary>The nearest clan's doings, for the tracker.</summary>
    public string? Tracker(Vector3 pos, Func<Vector3, (double X, double Z), string> direction) => _nearest is not { } n ? null
        : n.Raiding is { } r ? $"Raiders: {n.W.Name} are raiding {r.Name} ({direction(pos, (r.X, r.Z))})"
        : $"Raiders: {n.W.Name} on the move ({direction(pos, (n.X, n.Z))})";

    /// <summary>For the self-test: the hero put down at a clan's next raid.</summary>
    public (Warband W, Settlement Place, double At)? NextRaid(Vector3 from)
    {
        var t = main.GuildBook.PlaySeconds;
        foreach (var w in Warband.Near(from.X, from.Z, 3000).OrderBy(w => new Vector2((float)w.X - from.X, (float)w.Z - from.Z).Length()))
        {
            if (w.Targets(main.World).Count == 0) continue;
            var round = (int)Math.Floor(t / Warband.Round);
            var start = (round + 1) * Warband.Round - Warband.RaidSeconds + 5;
            if (w.Where(main.World, start).Raiding is { } place) return (w, place, start);
        }
        return null;
    }
}
