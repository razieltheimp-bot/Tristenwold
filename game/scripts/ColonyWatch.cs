using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A colony looks after itself (see <see cref="ColonyDefence"/>). Its settlers go about their work in the yard; when
/// something hostile comes in, they put their tools down and go at it, and the more of them there are the harder it
/// goes for whatever came. Once the colony has its barracks it can hire town guards, who stand their posts round the
/// yard and do the fighting, and the settlers only take up arms again if every guard is down. A fallen guard's post is
/// filled again once the barracks has trained someone up.
/// </summary>
public partial class Main
{
    /// <summary>A colony drawn near the hero: its heart, its fence, and its settlers in the yard.</summary>
    sealed class Camp
    {
        public required Hall Hall;
        public required Vector3 Centre;
        public required float Yard;
        public readonly List<Body> Settlers = [];
        public readonly List<Enemy?> Guards = [];
        public readonly List<double> Refill = [];
        public readonly Dictionary<Body, double> Ready = [];
        public bool Alarm;
    }

    readonly Dictionary<string, Camp> _camps = [];
    double _campTick, _campClock;

    /// <summary>A colony's compound has just been drawn: keep hold of it for its defence.</summary>
    void Encamp(Hall h, Vector3 centre, float yard, IEnumerable<Body> settlers)
    {
        if (_camps.Remove(h.Key, out var old)) Decamp(old);
        var camp = new Camp { Hall = h, Centre = centre, Yard = yard };
        camp.Settlers.AddRange(settlers);
        _camps[h.Key] = camp;
    }

    /// <summary>A colony out of sight: its guards go off duty with it.</summary>
    void Decamp(Camp camp)
    {
        foreach (var g in camp.Guards.OfType<Enemy>().Where(IsInstanceValid)) g.QueueFree();
        camp.Guards.Clear();
        camp.Refill.Clear();
    }

    void TendColonies(double delta)
    {
        _campClock += delta;
        if ((_campTick -= delta) > 0) return;
        _campTick = 0.5;
        foreach (var key in _camps.Keys.ToList())
        {
            var camp = _camps[key];
            if (!_hallFronts.ContainsKey(key) || !_city.Halls.ContainsKey(key)) { Decamp(camp); _camps.Remove(key); continue; }
            camp.Hall = _city.Halls[key];
            camp.Settlers.RemoveAll(b => !IsInstanceValid(b));
            Post(camp);
            Defend(camp);
        }
    }

    /// <summary>Guards to the colony's posts, as many as it has hired; a fallen one's post filled when the barracks has someone.</summary>
    void Post(Camp camp)
    {
        var h = camp.Hall;
        var barracks = _city.GrownOf(h.Town) >= Settling.Barracks;
        var want = barracks ? h.Guards : 0;
        while (camp.Guards.Count > want)
        {
            if (camp.Guards[^1] is { } extra && IsInstanceValid(extra)) extra.QueueFree();
            camp.Guards.RemoveAt(camp.Guards.Count - 1);
            camp.Refill.RemoveAt(camp.Refill.Count - 1);
        }
        while (camp.Guards.Count < want) { camp.Guards.Add(null); camp.Refill.Add(0); }
        for (var i = 0; i < camp.Guards.Count; i++)
        {
            var g = camp.Guards[i];
            if (g is not null && IsInstanceValid(g) && g.State != EnemyState.Dead) continue;
            if (g is not null) // fallen: the barracks trains another
            {
                camp.Guards[i] = null;
                camp.Refill[i] = _campClock + ColonyDefence.Replace;
                _hud.Log($"A guard has fallen at {h.Name}. The barracks will have someone at the post again before long.");
                continue;
            }
            if (camp.Refill[i] > _campClock) continue;
            camp.Guards[i] = PostGuard(camp, i);
        }
    }

    Enemy PostGuard(Camp camp, int i)
    {
        var h = camp.Hall;
        var gate = Mathf.Atan2(-camp.Centre.Z, -camp.Centre.X);
        // Two at the gate, the rest spaced round the inside of the fence.
        var a = i < 2 ? gate + (i == 0 ? -0.22f : 0.22f) : gate + Mathf.Pi + (i - 2) * 1.1f - 1.6f;
        var at = camp.Centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (camp.Yard - 2.5f);
        var g = Enemy.Create(Bestiary.Get("watchman"), at with { Y = City.FloorAt(at) + 0.4f }, look: GuardLook(h.Town));
        ArmGuard(g, h.Town);
        g.Allied = true; // not the Watch: that only goes after outlaws in Tristinwold's streets, and these go after anything that comes in
        g.Roam = 6;
        g.Stats.Name = $"Guard of {h.Name}";
        AddEnemy(g);
        g.RefreshPlate();
        return g;
    }

    /// <summary>Anything hostile in or near the yard: the guards go at it, or (with none standing, or no barracks yet) the settlers do.</summary>
    void Defend(Camp camp)
    {
        var reach = camp.Yard + (float)ColonyDefence.Range;
        var threats = GetTree().GetNodesInGroup("enemies").OfType<Enemy>()
            .Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly && !e.Kind.Timid && e.Bond is null && e.Hero is null
                        && new Vector2(e.GlobalPosition.X - camp.Centre.X, e.GlobalPosition.Z - camp.Centre.Z).Length() < reach)
            .ToList();
        if (threats.Count == 0) { camp.Alarm = false; return; }
        var guards = camp.Guards.OfType<Enemy>().Where(g => IsInstanceValid(g) && g.State != EnemyState.Dead).ToList();
        var barracks = _city.GrownOf(camp.Hall.Town) >= Settling.Barracks;
        var settlersFight = ColonyDefence.SettlersFight(barracks, guards.Count);
        if (!camp.Alarm)
        {
            camp.Alarm = true;
            _hud.Toast(settlersFight
                ? $"{threats[0].Stats.Name} has come into {camp.Hall.Name}! Your settlers drop their tools and take up arms."
                : $"{threats[0].Stats.Name} has come into {camp.Hall.Name}! The town guard turns out.", 7);
        }
        foreach (var g in guards.Where(g => g.Rival is not { } r || !IsInstanceValid(r) || r.State == EnemyState.Dead || !threats.Contains(r)))
            g.Distract(threats.MinBy(t => t.GlobalPosition.DistanceTo(g.GlobalPosition))!);
        if (!settlersFight) return;
        foreach (var body in camp.Settlers)
        {
            if (camp.Ready.GetValueOrDefault(body) > _campClock) continue;
            var threat = threats.MinBy(t => Flat(t.GlobalPosition, body.GlobalPosition))!;
            var apart = Flat(threat.GlobalPosition, body.GlobalPosition);
            if (apart > reach) continue;
            camp.Ready[body] = _campClock + ColonyDefence.Every * (0.8 + 0.4 * GD.Randf());
            Settle(body, threat, apart);
        }
    }

    /// <summary>One settler's go at it: up close with a tool if it's near enough to reach (they close on it), or whatever's to hand, thrown.</summary>
    void Settle(Body body, Enemy threat, float apart)
    {
        var to = threat.GlobalPosition - body.GlobalPosition;
        to.Y = 0;
        if (apart > ColonyDefence.Reach && apart < ColonyDefence.Reach * 4) // close the gap
        {
            var step = body.GlobalPosition + to.Normalized() * Mathf.Min(apart - (float)ColonyDefence.Reach + 0.5f, 2.5f);
            body.GlobalPosition = step with { Y = City.FloorAt(step) };
            apart = Flat(threat.GlobalPosition, body.GlobalPosition);
        }
        body.Rotation = new Vector3(0, Mathf.Atan2(to.X, to.Z), 0);
        var close = apart <= ColonyDefence.Reach + 0.5f;
        if (close) body.Attack();
        else
        {
            body.Cast();
            Effects.Bolt(this, body.GlobalPosition + Vector3.Up * 1.5f, threat.GlobalPosition + Vector3.Up * 1.2f, DamageKind.Physical, 0.06f);
        }
        threat.Pelted(ColonyDefence.Blow(close, threat.Named), finish: !threat.Named);
        if (threat.State == EnemyState.Dead) _hud.Log($"Your settlers have seen off the {threat.Kind.Name.ToLowerInvariant()}.");
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>For the self-test: the colony's guards on duty, and its settlers.</summary>
    public IReadOnlyList<Enemy> ColonyGuardsForTest(Hall h) => _camps.TryGetValue(h.Key, out var c) ? [.. c.Guards.OfType<Enemy>().Where(IsInstanceValid)] : [];
    public int ColonySettlersForTest(Hall h) => _camps.TryGetValue(h.Key, out var c) ? c.Settlers.Count : 0;
    public void TendColoniesForTest() { _campTick = 0; TendColonies(0.5); }
}
