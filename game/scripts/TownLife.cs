using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Tristinwold going about its business (see <see cref="Initiates"/> and <see cref="Crime"/>). The guilds' initiates walk
/// its streets on their first errands: the Velvet Knives' young thieves lift purses (the townsfolk's, and the hero's if
/// they get close; strike a thief who has your purse and they drop it; if the townsfolk see, the Watch runs them
/// down); the Iron Circle's go bouts with one another; the Magi's apprentices work cantrips; the College's busk. Now and
/// then a Velvet Knife comes for a hero who's made a name, and muggers work the docks and the Warrens. The Watch goes
/// after any criminal in the streets, and pays for each one brought down.
/// </summary>
public partial class TownLife(Main main) : Node
{
    public sealed class Walker(Enemy body, Initiate who)
    {
        public Enemy Body => body;
        public Initiate Who => who;
        public Vector3 Goal;
        public Vector3 Last;
        public double Still, Busy, SparUntil;
        public int Purse;
        public bool Stalking, Fled;
        public Walker? Partner;
    }

    readonly List<Walker> _walkers = [];
    readonly List<Enemy> _criminals = [];
    readonly Tristinwold.Deviant.Rng _rng = new((ulong)GD.Randi() << 32 | GD.Randi());
    List<Vector3> _spots = [];
    double _tick, _clock, _nextLift = 60, _nextCrime = Crime.CheckSeconds;

    /// <summary>How many of each guild's initiates are about at once.</summary>
    static readonly (Calling Calling, int Count)[] Out = [(Calling.Rogue, 2), (Calling.Warrior, 2), (Calling.Mage, 1), (Calling.Bard, 1)];

    /// <summary>Whether the guilds' initiates and the town's criminals are about (the self-test turns them off for its own fights).</summary>
    public bool Stirring { get; set; } = true;

    public IReadOnlyList<Walker> Walkers => _walkers;
    public IEnumerable<Enemy> Criminals => _criminals.Where(IsInstanceValid);

    public override void _Ready() => Enemy.Slain += OnSlain;
    public override void _ExitTree() => Enemy.Slain -= OnSlain;

    bool Near => new Vector2(main.PlayerNode.GlobalPosition.X, main.PlayerNode.GlobalPosition.Z).Length() < 320;

    public override void _Process(double delta)
    {
        _clock += delta;
        if ((_tick -= delta) > 0) return;
        _tick = 0.5;
        _walkers.RemoveAll(w => !IsInstanceValid(w.Body) || w.Body.State == EnemyState.Dead);
        _criminals.RemoveAll(c => !IsInstanceValid(c) || c.State == EnemyState.Dead);
        if (!Near || !Stirring)
        {
            foreach (var w in _walkers) w.Body.QueueFree();
            _walkers.Clear();
            return;
        }
        if (_spots.Count == 0) _spots = Spots();
        foreach (var (calling, count) in Out)
            while (_walkers.Count(w => w.Who.Calling == calling) < count) Send(calling);
        foreach (var w in _walkers.ToList()) Tend(w, 0.5);
        Streets();
    }

    /// <summary>Where people stand in the town (beside the townsfolk, at the Watch's posts): where initiates go.</summary>
    List<Vector3> Spots()
    {
        var spots = main.Locals.Where(p => TownWatch.InTristinwold(p.GlobalPosition))
            .Select(p => p.GlobalPosition + new Vector3(_rng.Chance(0.5) ? 2.2f : -2.2f, 0, _rng.Chance(0.5) ? 2.2f : -2.2f)).ToList();
        spots.AddRange(TownWatch.PostSpots);
        return spots;
    }

    Vector3 Somewhere(Vector3 from, float within = 70)
    {
        var near = _spots.Where(s => s.DistanceTo(from) is > 6 and var d && d < within).ToList();
        var pick = near.Count > 0 ? near[_rng.Next(near.Count)] : _spots[_rng.Next(_spots.Count)];
        return pick with { Y = City.FloorAt(pick) };
    }

    void Send(Calling calling)
    {
        var who = Initiates.Make(calling, _rng);
        var hero = main.PlayerNode.GlobalPosition;
        // They come in from out of sight, not out of thin air in front of you.
        var far = _spots.Where(s => s.DistanceTo(hero) > 30).ToList();
        var at = (far.Count > 0 ? far[_rng.Next(far.Count)] : _spots[_rng.Next(_spots.Count)]) + Vector3.Up * 0.5f;
        var body = Enemy.Create(Bestiary.Get(calling == Calling.Rogue ? "young_thief" : "initiate"), at with { Y = City.FloorAt(at) + 0.5f }, look: who.Look);
        body.Stats.Name = who.Title;
        body.Roam = 1;
        main.AddEnemy(body);
        body.RefreshPlate();
        var w = new Walker(body, who) { Goal = Somewhere(at), Last = at };
        body.Struck = (amount, kind, attack, by) => Struck(w);
        body.Wander(w.Goal);
        // The Iron Circle sends them out in pairs, to go bouts.
        if (calling == Calling.Warrior && _walkers.FirstOrDefault(x => x.Who.Calling == Calling.Warrior && x.Partner is null) is { } mate)
            (w.Partner, mate.Partner, w.Goal) = (mate, w, mate.Goal + new Vector3(2.5f, 0, 0));
        _walkers.Add(w);
    }

    void Tend(Walker w, double dt)
    {
        var body = w.Body;
        var p = main.PlayerNode;
        var hero = p.GlobalPosition;
        if (w.Fled)
        {
            // A thief with your purse, running: away clear, and it's gone with it.
            if (body.GlobalPosition.DistanceTo(hero) > 70 && !main.WatchNode.Guards.Any(g => g.Rival == body))
            {
                if (w.Purse > 0) main.HudNode.Toast($"{w.Who.Name} got away with your {w.Purse} crowns.", 5);
                Gone(w);
            }
            return;
        }
        if (body.Criminal && main.WatchNode.Guards.FirstOrDefault(g => g.Rival == body) is { } guard && body.Stats.Health < body.Stats.MaxHealth * 0.6)
        {
            Arrested(w, guard);
            return;
        }
        if (w.SparUntil > 0)
        {
            body.Stats.Heal(body.Stats.MaxHealth); // bouts, not murder
            if (_clock < w.SparUntil) return;
            w.SparUntil = 0;
            body.LoseTrack(2);
            w.Goal = Somewhere(body.GlobalPosition);
        }
        if ((w.Busy -= dt) > 0) return;

        // A young thief who sees a hero with a full purse close by goes for it.
        if (w.Who.Calling == Calling.Rogue && !body.Criminal && !p.Downed && !main.Tracker.InFight && p.Crowns >= 5)
        {
            var d = body.GlobalPosition.DistanceTo(hero);
            if (!w.Stalking && d < 22 && _clock >= _nextLift && _rng.Chance(0.15)) w.Stalking = true;
            if (w.Stalking)
            {
                if (d > 35) w.Stalking = false;
                else if (d < 1.8f) { Lift(w); return; }
                else { w.Goal = hero; body.Wander(hero); return; }
            }
        }

        var moved = body.GlobalPosition.DistanceTo(w.Last);
        w.Last = body.GlobalPosition;
        if (body.GlobalPosition.DistanceTo(w.Goal) < 2.5f) { Arrive(w); return; }
        // Stuck on a wall: try somewhere else, and if nobody's watching, just be there.
        w.Still = moved < 0.25f ? w.Still + dt : 0;
        if (w.Still > 6)
        {
            w.Still = 0;
            if (body.GlobalPosition.DistanceTo(hero) > 35 && w.Goal.DistanceTo(hero) > 35)
            {
                body.GlobalPosition = w.Goal + Vector3.Up * 0.5f;
                body.ResetPhysicsInterpolation();
            }
            else w.Goal = Somewhere(body.GlobalPosition);
        }
        body.Wander(w.Goal);
    }

    /// <summary>At the end of a walk: the errand.</summary>
    void Arrive(Walker w)
    {
        var body = w.Body;
        var hero = main.PlayerNode.GlobalPosition;
        var watching = body.GlobalPosition.DistanceTo(hero) < 30;
        switch (w.Who.Calling)
        {
            case Calling.Rogue when main.Locals.Where(IsInstanceValid).MinBy(x => x.GlobalPosition.DistanceTo(body.GlobalPosition)) is { } mark
                                    && mark.GlobalPosition.DistanceTo(body.GlobalPosition) < 5:
                w.Busy = 2;
                body.Figure.Perform("Interact", 1, 1.2f);
                if (_rng.Chance(Initiates.Noticed))
                {
                    mark.TalkTo(body.GlobalPosition);
                    Caught(w, $"{mark.Npc.Name} shouts \"Thief!\" {w.Who.Name} runs, and the Watch is after them.", watching);
                }
                else if (body.GlobalPosition.DistanceTo(hero) < 12)
                {
                    // The hero saw it, even if the mark didn't.
                    Caught(w, $"You see {w.Who.Name} lift {mark.Npc.Name}'s purse, smooth as you like. The Watch sees you see it.", true);
                }
                break;
            case Calling.Warrior when w.Partner is { } mate && IsInstanceValid(mate.Body) && mate.Body.GlobalPosition.DistanceTo(body.GlobalPosition) < 8 && mate.SparUntil <= 0:
                w.SparUntil = mate.SparUntil = _clock + 18;
                body.Stats.DamageMult = mate.Body.Stats.DamageMult = 0.02;
                body.Distract(mate.Body);
                mate.Body.Distract(body);
                if (watching) main.HudNode.Log($"{w.Who.Name} and {mate.Who.Name} of the Iron Circle square off for a bout.");
                return;
            case Calling.Warrior when w.Partner is { } mate && IsInstanceValid(mate.Body):
                mate.Goal = body.GlobalPosition + new Vector3(2.5f, 0, 0); // come here
                w.Busy = 3;
                return;
            case Calling.Mage:
                w.Busy = 3;
                body.Figure.Cast();
                var kinds = new[] { DamageKind.Fire, DamageKind.Frost, DamageKind.Shock, DamageKind.Sun };
                Effects.Rise(body, kinds[_rng.Next(kinds.Length)], 1.1f);
                break;
            case Calling.Bard:
                w.Busy = 14;
                body.Figure.Perform("Cheer", 2, 1);
                Busk(body);
                if (watching) main.HudNode.Log($"{w.Who.Name} of the College strikes up a tune for whoever'll listen.");
                break;
        }
        w.Goal = Somewhere(body.GlobalPosition);
    }

    /// <summary>A tune in the street, for the length of a busk.</summary>
    void Busk(Enemy body)
    {
        const string tune = "res://assets/audio/music/bard_1.ogg";
        if (!ResourceLoader.Exists(tune)) return;
        var player = new AudioStreamPlayer3D { Stream = GD.Load<AudioStream>(tune), Bus = Sound.Effects, Autoplay = true, UnitSize = 4, MaxDistance = 30, VolumeDb = -8 };
        body.AddChild(player);
        GetTree().CreateTimer(13).Timeout += () => { if (IsInstanceValid(player)) player.QueueFree(); };
    }

    /// <summary>A young thief at the hero's purse.</summary>
    void Lift(Walker w, bool sure = false)
    {
        var p = main.PlayerNode;
        w.Stalking = false;
        _nextLift = _clock + 120;
        if (sure || _rng.Chance(Initiates.LiftChance(p.Pack.Level(Job.Thieving), p.Class.Calling == Calling.Rogue)))
        {
            w.Purse = Initiates.Purse(p.Crowns, _rng);
            p.Crowns -= w.Purse;
            Sound.Ui("coin", -4);
            main.HudNode.Toast($"You feel a tug at your belt: {w.Who.Name} has your purse ({w.Purse} crowns) and runs! Strike them to get it back.", 7);
        }
        else main.HudNode.Toast($"You catch {w.Who.Name}'s hand in your purse! They twist free and run.", 6);
        w.Body.Criminal = true;
        w.Fled = true;
        _criminals.Add(w.Body);
        w.Body.Flee(p);
    }

    /// <summary>Seen at it: the Watch is after them.</summary>
    void Caught(Walker w, string said, bool tell)
    {
        w.Body.Criminal = true;
        w.Fled = true;
        _criminals.Add(w.Body);
        w.Body.Wander(Somewhere(w.Body.GlobalPosition, 200));
        if (tell) main.HudNode.Log(said);
    }

    /// <summary>The hero landed a blow on an initiate: a thief drops what they took; anyone else takes it badly.</summary>
    void Struck(Walker w)
    {
        if (!w.Body.Criminal) return;
        if (w.Purse > 0)
        {
            main.PlayerNode.Crowns += w.Purse;
            Sound.Ui("coin");
            main.HudNode.Toast($"{w.Who.Name} drops your purse ({w.Purse} crowns) and bolts. The Watch will have them.", 6);
            w.Purse = 0;
        }
        else main.HudNode.Log($"{w.Who.Name} yields.");
        Gone(w);
    }

    void Arrested(Walker w, Enemy guard)
    {
        if (w.Purse > 0)
        {
            main.PlayerNode.Crowns += w.Purse;
            main.HudNode.Toast($"{guard.Stats.Name} collars {w.Who.Name} and hands you back your {w.Purse} crowns.", 6);
            w.Purse = 0;
        }
        else if (w.Body.GlobalPosition.DistanceTo(main.PlayerNode.GlobalPosition) < 40)
            main.HudNode.Log($"{guard.Stats.Name} collars {w.Who.Name} and drags them off to the cells.");
        guard.LoseTrack(1);
        Gone(w);
    }

    void Gone(Walker w)
    {
        Effects.Burst(GetParent(), w.Body.GlobalPosition + Vector3.Up, DamageKind.Physical, 0.6f);
        w.Body.QueueFree();
        _walkers.Remove(w);
    }

    // ---- crime in the streets -----------------------------------------------------------------------

    /// <summary>Now and then: a cutthroat for a hero with a name, muggers on the rough streets.</summary>
    void Streets()
    {
        var p = main.PlayerNode;
        if (_clock < _nextCrime || p.Downed || main.Tracker.InFight || !TownWatch.InTristinwold(p.GlobalPosition)) return;
        _nextCrime = _clock + Crime.CheckSeconds;
        if (_rng.Chance(Crime.AssassinChance(p.Hero.Level))) Cutthroat();
        else if (_rng.Chance(Crime.MuggingChance(main.Area))) Muggers();
    }

    Vector3 Behind(float metres)
    {
        var p = main.PlayerNode;
        var at = p.GlobalPosition + p.GlobalTransform.Basis.Z * metres;
        return at with { Y = City.FloorAt(at) + 0.5f };
    }

    /// <summary>A Velvet Knife, come for the hero.</summary>
    public Enemy Cutthroat()
    {
        var p = main.PlayerNode;
        var e = Enemy.Create(Bestiary.Get("velvet_assassin"), Behind(14), look: "velvet");
        e.Stats.Name = "a Velvet Knife";
        e.Criminal = true;
        main.AddEnemy(e);
        _criminals.Add(e);
        e.Provoke(p);
        Sound.At("melee/swing_1", e.GlobalPosition, 0);
        main.HudNode.Toast("A hooded figure steps out of the crowd behind you, a blade low at its side: \"The Velvet Knives send their regards.\"", 7);
        return e;
    }

    /// <summary>A pair of dock thugs who want your purse.</summary>
    public IReadOnlyList<Enemy> Muggers()
    {
        var p = main.PlayerNode;
        var them = new List<Enemy>();
        for (var i = 0; i < 2; i++)
        {
            var e = Enemy.Create(Bestiary.Get("dock_thug"), Behind(8) + new Vector3(i * 2 - 1, 0, 0));
            e.Stats.Name = "Mugger";
            e.Criminal = true;
            main.AddEnemy(e);
            _criminals.Add(e);
            e.Provoke(p);
            them.Add(e);
        }
        main.HudNode.Toast("Two men step out of an alley: \"Your purse, or your teeth.\"", 6);
        return them;
    }

    /// <summary>A criminal brought down by the hero or the Watch: the Watch pays for it.</summary>
    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (!victim.Criminal || !(byCompany || killer is { Watch: true })) return;
        var w = _walkers.FirstOrDefault(x => x.Body == victim);
        var reward = Initiates.Reward(victim.Kind.Id) + (w?.Purse ?? 0);
        main.PlayerNode.Crowns += reward;
        main.HudNode.Log($"The Watch pays {Initiates.Reward(victim.Kind.Id)} crowns for {victim.Stats.Name}{(w?.Purse > 0 ? $", and your {w.Purse} crowns are back in your purse" : "")}.");
        if (w is not null) _walkers.Remove(w);
    }

    // ---- for the self-test ----------------------------------------------------------------------

    /// <summary>For the self-test: a thief at the purse, and (the dice aside) one who gets it.</summary>
    public void LiftForTest(Walker w, bool sure = true) => Lift(w, sure);
}
