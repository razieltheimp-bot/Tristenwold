using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The barbarian clans of the frontier, out in the world (see <see cref="Warband"/>). Come near one and it's there:
/// camped on the road between raids, or down on some hamlet or farmstead with its couple of guards fighting for their
/// lives. Break the raid (bring down every raider) and the place pays you, and the clan pulls back for an hour. A
/// raid nobody stops plunders the place: if it's one of Tristinwold's trade routes, its market loses nearly half of
/// everything, and word comes to the city. And a clan has a place for a hero who beats one of its raiders at its camp:
/// its warchief calls a halt and makes the offer. Sworn, the clan is kin (it won't fight you, and you can't strike it),
/// you ride with it when it falls on a place, and when the place's guards are down you take a share of the plunder
/// and a price on your head in Tristinwold (see <see cref="ClanBook"/>).
/// </summary>
public partial class Raiders(Main main) : Node
{
    sealed class Band(Warband w, int round, Settlement? raiding, List<Enemy> bodies)
    {
        public Warband W => w;
        public int Round => round;
        public Settlement? Raiding => raiding;
        public List<Enemy> Bodies => bodies;
        /// <summary>The warchief has made the hero his offer this time out.</summary>
        public bool Offered { get; set; }
        /// <summary>The hero's own clan.</summary>
        public bool Mine { get; set; }
        /// <summary>The hero has had their share of this raid.</summary>
        public bool Rode { get; set; }
    }

    readonly Dictionary<string, Band> _out = [];
    double _tick, _plunderTick = 5;

    public RaidRecord Record { get; set; } = new();

    /// <summary>The clans out near the hero now (for the self-test).</summary>
    public IEnumerable<(Warband W, Settlement? Raiding, IReadOnlyList<Enemy> Bodies)> Out => _out.Values.Select(b => (b.W, b.Raiding, (IReadOnlyList<Enemy>)b.Bodies));

    /// <summary>The hero's own clan, if it's out near them: where it is, and the place it's down on.</summary>
    public (Warband W, Settlement? Raiding)? Mine => _out.Values.FirstOrDefault(b => b.Mine) is { } b ? (b.W, b.Raiding) : null;

    /// <summary>The hero's own warchief, if he's about and on his feet.</summary>
    public Enemy? Chief => _out.Values.FirstOrDefault(b => b.Mine)?.Bodies.FirstOrDefault(e => IsInstanceValid(e) && e.State != EnemyState.Dead && e.Kind.Id == "raider_chief");

    static List<Enemy> Alive(Band b) => b.Bodies.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).ToList();

    public override void _Ready() => Enemy.Slain += OnSlain;
    public override void _ExitTree() => Enemy.Slain -= OnSlain;

    /// <summary>A raider of a camped clan, beaten by the hero: if its warchief still stands, he makes an offer.</summary>
    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (!byCompany || _out.Values.FirstOrDefault(b => b.Bodies.Contains(victim)) is not { } b) return;
        if (b.Raiding is not null || b.Mine || b.Offered || main.Standing.Clan.In) return;
        var chief = b.Bodies[0];
        if (victim == chief || !IsInstanceValid(chief) || chief.State == EnemyState.Dead) return;
        b.Offered = true;
        foreach (var e in Alive(b)) e.LoseTrack(20);
        main.HudNode.Talk($"{b.W.Chief}, warchief of {b.W.Name}",
            $"The warchief raises a hand and the clan lowers its spears. \"Hold! Hold, I said.\" He looks at the body, then at you. \"That was one of my best, and you put him down like a dog. {char.ToUpper(b.W.Name[0])}{b.W.Name[1..]} could use a blade like that. Ride with us. There's plunder in it, and a name the soft folk behind their walls will learn to fear. Or we finish this, here.\"",
            ("I'll ride with you.", () =>
            {
                main.HudNode.EndTalk();
                main.JoinClan(b.W);
                Adopt(b);
            }),
            ("I ride alone. Come on, then.", () =>
            {
                main.HudNode.EndTalk();
                foreach (var e in Alive(b)) e.Provoke(main.PlayerNode);
            }));
    }

    /// <summary>The clan is the hero's now: kin, who won't fight them.</summary>
    void Adopt(Band b)
    {
        b.Mine = true;
        foreach (var e in Alive(b)) { e.Kin = true; e.RefreshPlate(); }
    }

    /// <summary>The hero has turned their back on the clan: no more kin, and they take it badly.</summary>
    public void Disown()
    {
        foreach (var b in _out.Values.Where(b => b.Mine))
        {
            b.Mine = false;
            foreach (var e in Alive(b))
            {
                e.Kin = false;
                e.RefreshPlate();
                if (e.GlobalPosition.DistanceTo(main.PlayerNode.GlobalPosition) < 40) e.Provoke(main.PlayerNode);
            }
            if (b.Raiding is { } place) Guard(place, true);
        }
    }

    /// <summary>A place's guards stand with the hero, or, when the hero rides with the clan on it, against them.</summary>
    void Guard(Settlement place, bool allied)
    {
        foreach (var g in main.PlacesNode.GuardsOf(place.Id)) { g.Allied = allied; g.RefreshPlate(); }
    }

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
            // Riding with your own clan: when the place's guards are down and you're there, you've had your share.
            if (b.Mine && b.Raiding is { } place && !b.Rode && !main.PlacesNode.GuardsOf(place.Id).Any()
                && hero.DistanceTo(new Vector3((float)place.X, hero.Y, (float)place.Z)) < 120)
            {
                b.Rode = true;
                main.RodeWith(b.W, place, b.Round);
            }
            var centre = alive.Aggregate(Vector3.Zero, (s, e) => s + e.GlobalPosition) / alive.Count;
            var (round, x, z, _) = b.W.Where(main.World, t);
            var drifted = new Vector2((float)x - centre.X, (float)z - centre.Z).Length() > 200 && alive.All(e => e.State == EnemyState.Idle);
            if (centre.DistanceTo(hero) > 650 || drifted || round != b.Round && alive.All(e => e.State == EnemyState.Idle))
            {
                foreach (var e in alive) e.QueueFree();
                if (b.Mine && b.Raiding is { } left) Guard(left, true);
                _out.Remove(b.W.Id);
            }
        }
    }

    void Spawn(Warband w, int round, double x, double z, Settlement? raiding)
    {
        var bodies = new List<Enemy>();
        var danger = WorldGen.Danger(x, z);
        var mine = main.Standing.Clan.ClanId == w.Id;
        for (var i = 0; i < w.Raiders; i++)
        {
            var a = i * Mathf.Tau / w.Raiders;
            var r = raiding is null ? 5f : (float)raiding.Radius * 0.4f;
            var at = new Vector3((float)x + Mathf.Cos(a) * r, 0, (float)z + Mathf.Sin(a) * r);
            var chief = i == 0;
            var e = Enemy.Create(Bestiary.Get(chief ? "raider_chief" : "raider"), at with { Y = City.FloorAt(at) + 0.5f }, danger);
            e.Raider = true;
            e.Roam = raiding is null ? 10 : (float)raiding.Radius * 0.6f;
            if (chief) e.Stats.Name = mine && main.Standing.Clan.Rank == ClanRank.Warchief ? $"{w.Chief} of {w.Name}" : $"{w.Chief}, warchief of {w.Name}";
            if (mine) e.Kin = true;
            main.AddEnemy(e);
            e.RefreshPlate();
            bodies.Add(e);
        }
        // Down on a place: they go for its guards.
        if (raiding is not null)
            foreach (var (e, g) in bodies.Zip(main.PlacesNode.GuardsOf(raiding.Id).Concat(main.PlacesNode.GuardsOf(raiding.Id)).Concat(main.PlacesNode.GuardsOf(raiding.Id))))
                e.Distract(g);
        _out[w.Id] = new Band(w, round, raiding, bodies) { Mine = mine };
        if (mine && raiding is not null) Guard(raiding, false); // its guards fight anyone with the clan, you included
        main.HudNode.Toast(mine
            ? raiding is null ? $"Your clan, {w.Name}, camped ahead between raids." : $"Your clan, {w.Name}, has fallen on {raiding.Name}. Ride with them: bring down its guards, and the place is plundered."
            : raiding is null
            ? $"Smoke on the road ahead: {w.Name}, barbarian raiders under {w.Chief}, camped between raids."
            : $"Raiders! {w.Name} under {w.Chief} have fallen on {raiding.Name}. Its {Warband.Guards(raiding)} guard{(Warband.Guards(raiding) == 1 ? "" : "s")} can't hold them alone.", 8);
    }

    void Broken(Band b, double t)
    {
        _out.Remove(b.W.Id);
        Record.Break(b.W, b.Round, t);
        if (b.Mine)
        {
            if (b.Raiding is { } at) Guard(at, true);
            main.HudNode.Toast($"{char.ToUpper(b.W.Name[0])}{b.W.Name[1..]} is cut down to the last spear. It'll be a while before your clan rides again.", 8);
            return;
        }
        if (b.Raiding is { } place)
        {
            main.PlayerNode.Crowns += b.W.Reward;
            main.Standing.Good(6, $"broke the raid on {place.Name}");
            Sound.Ui("coin", -2);
            main.HudNode.Toast($"You've broken the raid on {place.Name}. {b.W.Name} falls back to lick its wounds, and {place.Name} pays you {b.W.Reward} crowns from what you saved.", 9);
        }
        else
        {
            main.Standing.Good(3, $"broke {b.W.Name} at their camp");
            main.HudNode.Toast($"You've broken {b.W.Name}. It'll be a while before they raid again.", 7);
        }
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
                // A clan in the town's country: the road there is known trouble, and the city's caravans on it take their chances.
                if (main.Economy.Roads.Road(m.Id).RaidersDay != day) main.DoCity(CityAction.Raiders(m.Id, w.Name));
                var (round, _, _, raiding) = w.Where(main.World, t);
                if (raiding?.Id != m.Id || !Record.Reckon(w, round)) continue;
                var loads = m.Plundered(day);
                main.Report(CityAction.Plundered(m.Id));
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

    /// <summary>For the self-test: the nearest clan with somewhere to raid, and when and where it next camps on the road.</summary>
    public (Warband W, double X, double Z, double At)? NextCamp(Vector3 from)
    {
        var t = main.GuildBook.PlaySeconds;
        foreach (var w in Warband.Near(from.X, from.Z, 3000).OrderBy(w => new Vector2((float)w.X - from.X, (float)w.Z - from.Z).Length()))
        {
            if (w.Targets(main.World).Count == 0) continue;
            var start = ((int)Math.Floor(t / Warband.Round) + 1) * Warband.Round + 60;
            var (_, x, z, raiding) = w.Where(main.World, start);
            if (raiding is null) return (w, x, z, start);
        }
        return null;
    }

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
