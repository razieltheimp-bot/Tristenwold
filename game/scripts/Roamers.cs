using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The deviants out in the world (see <see cref="Roamer"/>): lair bosses that beat the company and walked out with a
/// name, and anything in the wilds that beat them where it stood. Each wanders the wilds on its own path, whether
/// anyone is near or not; come within sight of it and it's there, walking. Once it sees you it picks up your trail
/// and hunts you: across the wilds, at its own pace, until you're inside a town's walls or clear away, and every
/// time you get away it's quicker next time. Every blow you land teaches it how you fight, and it fights back with
/// that: it learns to take your fire, to close the gap on a hero who keeps their distance, to wear a harder hide for
/// one who fights up close, to shrug off spells, to go for the kill when it has seen you drink to stay up. Every time
/// it beats you it grows stronger; kill it and it's gone for good, and the price on its head is yours. In the shared
/// world it is the deviant the server named, on the roster with the rest.
/// </summary>
public partial class Roamers(Main main) : Node
{
    sealed class Out(Roamer roamer, Enemy body, BossBrain? brain)
    {
        public Roamer Roamer => roamer;
        public Enemy Body => body;
        public BossBrain? Brain => brain;
        public readonly List<Enemy> Adds = [];
        public double Taunted = -999;
        /// <summary>How it fights the hero right now, and its numbers before any of that.</summary>
        public Counter Applied = Counter.None;
        public double BaseDamage = 1, BaseArmor, BaseWard, BaseSpeed = 1;
        public double LungeIn;
        /// <summary>It has seen the hero, and is watching them till then.</summary>
        public double WatchingUntil;
        /// <summary>It has come into a town after the hero.</summary>
        public bool Inside;
    }

    readonly List<Out> _out = [];
    readonly List<Roamer> _fallen = [];
    readonly Tristinwold.Deviant.Rng _rng = new((ulong)GD.Randi() << 32 | GD.Randi());
    double _tick;

    public const float SightRange = 200, DespawnRange = 320;
    /// <summary>Beyond this, a hunter on the hero's trail is tracking them across ground the hero can't see.</summary>
    const float TrackingRange = 120;

    /// <summary>Whether anything that beats the hero in the wilds takes a name (the self-test turns it off for its own fights).</summary>
    public bool WildDeviants { get; set; } = true;
    /// <summary>Whether a deviant takes its time (the self-test turns it off, to get on with things).</summary>
    public bool Patient { get; set; } = true;

    /// <summary>Roamers out walking near the hero now, with their bodies.</summary>
    public IEnumerable<(Roamer Roamer, Enemy Body)> Near => _out.Where(o => IsInstanceValid(o.Body)).Select(o => (o.Roamer, o.Body));

    /// <summary>The deviants on the hero's trail now.</summary>
    public IEnumerable<Roamer> Hunters => main.RaidLog.Roamers.Where(r => r.Hunting);

    public override void _Ready()
    {
        Enemy.Slain += OnSlain;
        Enemy.RanOff += OnRanOff;
        main.PlayerNode.WentDown += OnHeroDown;
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        Enemy.RanOff -= OnRanOff;
        main.PlayerNode.WentDown -= OnHeroDown;
    }

    Vector3 Where(Roamer r)
    {
        var (x, z) = r.Where(main.World, main.GuildBook.PlaySeconds);
        var at = new Vector3((float)x, 0, (float)z);
        return at with { Y = City.FloorAt(at) };
    }

    /// <summary>Inside Tristinwold's walls, or in any town, village or farmstead: where a deviant won't follow.</summary>
    public bool InTown(Vector3 at) =>
        new Vector2(at.X, at.Z).Length() < City.OuterRadius + 30
        || main.World.SettlementsNear(at.X, at.Z, 400).Any(s => !s.Hostile && s.DistanceTo(at.X, at.Z) < s.Radius + 10);

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var p = main.PlayerNode;
        var heroInTown = InTown(p.GlobalPosition);
        _out.RemoveAll(o => !IsInstanceValid(o.Body));
        foreach (var r in main.RaidLog.Roamers.ToList())
        {
            var o = _out.FirstOrDefault(o => o.Roamer.Id == r.Id);
            if (o is null)
            {
                var at = Where(r);
                if (!p.Downed && Flat(at, p.GlobalPosition) < SightRange && main.TerrainNode.HasGround(at)) Spawn(r, at, null);
                else if (r.Hunting && (heroInTown || Flat(at, p.GlobalPosition) > Tactics.LoseRange)) r.Hunting = false; // the trail went cold while nobody was about
                continue;
            }
            var d = Flat(o.Body.GlobalPosition, p.GlobalPosition);
            if (o.Body.State == EnemyState.Dead) continue;
            if (r.Hunting)
            {
                var dares = heroInTown && Dares(o);
                if (!Tactics.StillHunting(d, heroInTown, dares)) LoseTrail(o, heroInTown);
                else if (!p.Downed)
                {
                    if (heroInTown && !o.Inside)
                    {
                        o.Inside = true;
                        Sound.At("creature/growl_big", o.Body.GlobalPosition + Vector3.Up, 8, 0.7f, 250);
                        main.HudNode.Toast($"{r.Title} doesn't stop at the walls: it thinks it can finish you here. It's coming in after you!", 8);
                        main.HudNode.Log($"{r.Title} came into town after you.");
                    }
                    Pursue(o, d);
                }
            }
            else if (!p.Downed && !heroInTown && d < Tactics.NoticeRange) Stalk(o);
            if (!r.Hunting)
            {
                if (d > DespawnRange || p.Downed && d > 60)
                {
                    Despawn(o);
                    continue;
                }
                if (!o.Body.FightingCompany) o.Body.Wander(Where(r));
            }
        }
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    void Despawn(Out o)
    {
        main.FeudsNode.Disband(o.Body);
        foreach (var add in o.Adds.Where(IsInstanceValid)) add.QueueFree();
        o.Body.QueueFree();
        _out.Remove(o);
    }

    // ---- the hunt -------------------------------------------------------------------------------

    /// <summary>It's seen the hero. It doesn't come at once: if it has plans it keeps to them, and otherwise it watches a while first.</summary>
    void Stalk(Out o)
    {
        var now = main.GuildBook.PlaySeconds;
        if (!Patient) { TakeUpTrail(o); return; }
        if (now < o.Roamer.PlansUntil) return;
        if (o.WatchingUntil <= 0)
        {
            o.WatchingUntil = Tactics.Watch(now, _rng);
            main.HudNode.Log($"Something's watching you from a long way off, and it isn't in any hurry.");
        }
        else if (now >= o.WatchingUntil)
        {
            o.WatchingUntil = 0;
            TakeUpTrail(o);
        }
    }

    /// <summary>It has seen the hero: it says so, and comes, ready for how they fought it last time.</summary>
    void TakeUpTrail(Out o)
    {
        var r = o.Roamer;
        r.Hunting = true;
        r.Lessons.Fights++;
        Fit(o, Tactics.Read(r.Lessons));
        Sound.At(o.Body.Kind.Height > 1.4 || o.Body.IsDragon ? "creature/growl_big" : "creature/growl_small", o.Body.GlobalPosition + Vector3.Up, 6, 0.8f, 220);
        main.HudNode.Toast($"{r.Title} has your scent, and is coming for you. \"{Tactics.Taunt(r, _rng)}\"", 8);
        main.HudNode.Log($"Hunted: {r.Title}.");
        o.Taunted = main.GuildBook.PlaySeconds;
        main.Save();
    }

    /// <summary>
    /// After the hero: close by, it runs them down and fights; further off, it follows their trail across the wilds at
    /// its own pace, so a hero who runs can get away from it, and one who stops will be caught.
    /// </summary>
    void Pursue(Out o, float apart)
    {
        var p = main.PlayerNode;
        var body = o.Body;
        body.Wander(p.GlobalPosition); // home is wherever the hero is: it doesn't turn back
        if (apart > TrackingRange)
        {
            var step = (float)(body.Kind.Speed * body.Stats.SpeedMult) * 0.95f;
            var to = p.GlobalPosition - body.GlobalPosition;
            to.Y = 0;
            var next = body.GlobalPosition + to.Normalized() * Math.Min(step, apart - TrackingRange * 0.9f);
            next.Y = City.FloorAt(next) + 0.4f;
            body.GlobalPosition = next;
            body.ResetPhysicsInterpolation();
            return;
        }
        if (!body.FightingCompany) body.Provoke(p);
        if (main.GuildBook.PlaySeconds - o.Taunted > 90 && apart < 30)
        {
            o.Taunted = main.GuildBook.PlaySeconds;
            main.HudNode.Toast($"{o.Roamer.Name}: \"{Tactics.Taunt(o.Roamer, _rng)}\"", 6);
        }
    }

    /// <summary>
    /// Whether it thinks it can kill the hero in the town they're in: bolder the more it has beaten them, the lower they
    /// are and the more it has learned of them; every watchman near them gives it pause.
    /// </summary>
    bool Dares(Out o)
    {
        var p = main.PlayerNode;
        return Tactics.Dares(o.Roamer.Wins, p.Stats.Health / p.Stats.MaxHealth, Tactics.Read(o.Roamer.Lessons).Lines.Count, main.WatchNode.GuardsNear(p.GlobalPosition, 40));
    }

    /// <summary>The hero got away, into a town or clear of it. Running teaches it too.</summary>
    void LoseTrail(Out o, bool heroInTown)
    {
        var r = o.Roamer;
        r.Hunting = false;
        r.PlansUntil = Tactics.Plan(main.GuildBook.PlaySeconds, _rng);
        if (heroInTown && o.Inside)
            main.HudNode.Toast($"{r.Title} sees the Watch closing in around you, and thinks better of it. It breaks off and makes for the walls.", 7);
        else if (heroInTown)
            main.HudNode.Toast($"{r.Title} won't follow you inside the walls. It'll be out there, waiting.", 6);
        else
        {
            r.Lessons.Escapes++;
            main.HudNode.Toast($"You've lost {r.Title}, for now. It remembers that you ran.", 6);
        }
        o.Inside = false;
        main.HudNode.Log($"{r.Title} lost your trail.");
        o.Body.Wander(Where(r));
        o.Body.LoseTrack(3);
        main.Save();
    }

    /// <summary>A potion drunk with a hunter at you: it notices.</summary>
    public void Drank()
    {
        var p = main.PlayerNode;
        foreach (var o in _out.Where(o => o.Roamer.Hunting && IsInstanceValid(o.Body) && o.Body.FightingCompany && Flat(o.Body.GlobalPosition, p.GlobalPosition) < 40))
            o.Roamer.Lessons.Potions++;
    }

    /// <summary>A blow the hero landed: it learns from it, and the moment it has learned something new, it shows.</summary>
    void Learn(Out o, double amount, DamageKind kind, AttackKind attack, Player by)
    {
        var r = o.Roamer;
        if (!IsInstanceValid(o.Body) || o.Body.State == EnemyState.Dead) return;
        r.Lessons.Struck(amount, kind, attack, Flat(by.GlobalPosition, o.Body.GlobalPosition));
        var counter = Tactics.Read(r.Lessons);
        var news = counter.Since(o.Applied).ToList();
        if (news.Count == 0) return;
        Fit(o, counter);
        Effects.Rise(o.Body, counter.Resists ?? DamageKind.Physical, 1.2f);
        main.HudNode.Toast($"{r.Name} is learning how you fight: {news[0].Said}", 6);
        main.HudNode.Log($"{r.Title} learned: {string.Join(" ", news.Select(n => n.Said))}");
    }

    /// <summary>Fights the hero the way it has learned to: the damage it takes, its hide, its ward, its speed.</summary>
    void Fit(Out o, Counter c)
    {
        var s = o.Body.Stats;
        if (c.Resists is { } kind) s.Resist[kind] = Math.Max(s.Resist.GetValueOrDefault(kind), 50);
        s.Armor = o.BaseArmor * c.Armor + (c.Armor > 1 ? 20 : 0);
        s.MagicResist = o.BaseWard * c.Ward + (c.Ward > 1 ? 15 : 0);
        s.SpeedMult = o.BaseSpeed * c.Speed;
        o.Applied = c;
    }

    /// <summary>Its moves in a fight with the hero, besides its blows: the kill when they're low, and the lunge.</summary>
    void Hunt(Out o, Enemy e, double delta)
    {
        if (e.Foe3D is not Player hero) return;
        var low = hero.Stats.Health < hero.Stats.MaxHealth * 0.35;
        e.Stats.DamageMult = o.BaseDamage * (low ? o.Applied.Finisher : 1);
        if (!o.Applied.Lunges || (o.LungeIn -= delta) > 0) return;
        var to = hero.GlobalPosition - e.GlobalPosition;
        to.Y = 0;
        var gap = to.Length();
        if (gap is < 7 or > 30) return;
        o.LungeIn = 6;
        var land = hero.GlobalPosition - to.Normalized() * 2;
        land.Y = City.FloorAt(land) + 0.3f;
        Effects.Burst(GetParent(), e.GlobalPosition + Vector3.Up * 0.3f, DamageKind.Physical, 1.4f);
        Sound.At("move/jump", e.GlobalPosition, 0, 0.7f);
        e.CreateTween().TweenProperty(e, "global_position", land, 0.3).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        main.HudNode.Log($"{o.Roamer.Name} closes the gap in a single bound!");
    }

    // ---- bodies ---------------------------------------------------------------------------------

    Out Spawn(Roamer r, Vector3 at, Enemy? body)
    {
        var raid = r.Lair;
        if (body is null)
        {
            body = Enemy.Create(Bestiary.Get(r.KindId), at + Vector3.Up * 0.4f, raid is null ? WorldGen.Danger(r.StartX, r.StartZ) : 1, look: r.KindId);
            main.AddEnemy(body);
            main.FeudsNode.Soldiers(body, (Tristinwold.Deviant.Rank)Math.Min(3, (r.Wins - 1) / 2), raid?.ThrallKind);
        }
        Out? o = null;
        var brain = raid is null ? null : new BossBrain(main, raid, r.Name, spot => o?.Adds.Add(Add(raid, spot, r.Name)));
        o = new Out(r, body, brain);
        Strengthen(o);
        body.Think = (e, delta) => { brain?.Think(e, delta); Hunt(o, e, delta); };
        body.Struck = (amount, kind, attack, by) => Learn(o, amount, kind, attack, by);
        body.Roam = 12;
        _out.Add(o);
        return o;
    }

    Enemy Add(Raid raid, Vector3 at, string? leader = null)
    {
        var e = Enemy.Create(Bestiary.Get(raid.ThrallKind), at with { Y = City.FloorAt(at) + 0.4f }, look: raid.ThrallKind);
        var (health, damage) = Raids.Thrall(raid, main.PlayerNode.Hero.Level);
        e.Tune(health * 1.2, damage * 1.1);
        if (leader is not null) { e.Stats.Name = Roaming.MinionOf(leader); e.RefreshPlate(); }
        main.AddEnemy(e);
        e.Provoke(main.PlayerNode);
        return e;
    }

    /// <summary>Its numbers, for its wins and the hero's level; its name on its plate; and what it has learned.</summary>
    void Strengthen(Out o)
    {
        var (health, damage) = Roaming.Numbers(o.Roamer, main.PlayerNode.Hero.Level);
        o.Body.Tune(health, damage);
        o.Body.Crown(o.Roamer.Title, o.Roamer.DeviantId);
        if (o.Brain is not null) o.Brain.Name = o.Roamer.Name;
        var s = o.Body.Stats;
        (o.BaseDamage, o.BaseArmor, o.BaseWard, o.BaseSpeed) = (s.DamageMult, s.Armor, s.MagicResist, s.SpeedMult);
        Fit(o, Tactics.Read(o.Roamer.Lessons));
    }

    /// <summary>The lair boss that just beat the company: it takes its name and walks away.</summary>
    public void Adopt(Roamer r, Enemy boss) => Spawn(r, boss.GlobalPosition, boss);

    /// <summary>
    /// The hero went down. A deviant that was at them has beaten them again, and grows (and, satisfied for now, stops
    /// hunting). Anything else out in the wilds that put them down takes a name and becomes one.
    /// </summary>
    void OnHeroDown()
    {
        var p = main.PlayerNode;
        foreach (var o in _out.Where(o => IsInstanceValid(o.Body) && o.Body.State != EnemyState.Dead
                     && (p.LastHitBy == o.Body || p.LastStruck == o.Body || o.Body.FightingCompany && Flat(o.Body.GlobalPosition, p.GlobalPosition) < 40)).ToList())
        {
            o.Roamer.Wins++;
            o.Roamer.Hunting = false;
            o.Roamer.PlansUntil = Tactics.Plan(main.GuildBook.PlaySeconds, _rng);
            Strengthen(o);
            main.HudNode.Toast($"{o.Roamer.Title} stands over you. That's {o.Roamer.Wins} times it has beaten you, and it grows stronger every time.", 10);
        }
        if (p.LastHitBy is { } killer && WildDeviants && Becomes(killer)) Rise(killer);
    }

    /// <summary>Whether what put the hero down takes a name for it: a foe of the wilds, not a lair's, a town's or a friend.</summary>
    bool Becomes(Enemy e) =>
        IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly && !e.Young && !e.Kind.Timid && !e.InLair && !e.Sparring && !e.HeroesRival
        && e.Bond is null && _out.All(o => o.Body != e) && !InTown(e.GlobalPosition);

    /// <summary>Something out in the wilds beat the hero: it takes a name, and from now on it roams, and hunts.</summary>
    void Rise(Enemy killer)
    {
        var at = killer.GlobalPosition;
        var was = killer.Stats.Name;
        var r = main.RaidLog.Took(killer.Kind.Id, at.X, at.Z, main.GuildBook.PlaySeconds, (ulong)GD.Randi() << 32 | GD.Randi());
        r.PlansUntil = Tactics.Plan(main.GuildBook.PlaySeconds, _rng);
        if (killer.DeviantId is { } id) (r.DeviantId, r.Name) = (id, was); // the shared world named it already
        killer.Think = null;
        Spawn(r, at, killer);
        killer.LoseTrack(5);
        main.HudNode.Toast($"The {was.ToLowerInvariant()} stands over you, and takes a name for itself: {r.Name}. It will remember how you fight. (A new deviant.)", 10);
        main.HudNode.Log($"Deviant: {r.Title} beat you, took a name and went roaming.");
    }

    /// <summary>One of ours got clear of the hero: it goes to ground far away, licks its wounds, and starts over, angrier.</summary>
    void OnRanOff(Enemy body, bool clear)
    {
        if (!clear || _out.FirstOrDefault(o => o.Body == body) is not { } o) return;
        _out.Remove(o);
        foreach (var add in o.Adds.Where(a => IsInstanceValid(a) && a.State != EnemyState.Dead)) add.Collapse();
        var r = o.Roamer;
        var away = (body.GlobalPosition - main.PlayerNode.GlobalPosition) with { Y = 0 };
        var to = body.GlobalPosition + (away.LengthSquared() > 1 ? away.Normalized() : Vector3.Forward) * 500;
        (r.StartX, r.StartZ, r.Seed, r.Wins, r.Hunting) = (to.X, to.Z, r.Seed + 1, r.Wins + 1, false);
        r.Since = main.GuildBook.PlaySeconds;
        r.PlansUntil = Tactics.Plan(r.Since, _rng);
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        var o = _out.FirstOrDefault(o => o.Body == victim);
        if (o is null) return;
        var r = o.Roamer;
        main.RaidLog.Slain(r);
        _out.Remove(o);
        foreach (var add in o.Adds.Where(a => IsInstanceValid(a) && a.State != EnemyState.Dead)) add.Collapse();
        main.HudNode.Log($"{r.Title} fell to {killer?.Stats.Name ?? "your own hand"} at {victim.GlobalPosition.X:0},{victim.GlobalPosition.Z:0}.");
        var watch = killer is { Watch: true };
        if (!byCompany && !watch)
        {
            main.HudNode.Toast($"{r.Title} has been killed, and not by you.", 8);
            return;
        }
        var (xp, crowns) = Roaming.Bounty(r, main.PlayerNode.Hero.Level);
        main.PlayerNode.Crowns += crowns;
        var dice = new Tristinwold.Deviant.Rng((ulong)GD.Randi());
        // A lair boss carried the best of its lair; one of the wilds, the pickings of every hero it beat.
        var spoils = r.Lair is null ? [.. Loot.Roll(r.KindId, dice).Items, .. Loot.Roll(r.KindId, dice).Items] : Loot.Hoard(r.RaidId, dice, first: true);
        main.Take(spoils);
        if (spoils.Count > 0) main.HudNode.Log($"From {r.Title}: {Loot.Describe(spoils)}");
        main.GainXp(xp);
        _fallen.Add(r);
        main.HudNode.Toast($"{r.Title} is dead, for good{(watch ? ", brought down by the Watch with you" : "")}. The price on its head: {crowns} crowns. (+{xp} experience)", 12);
        main.HudNode.Log($"Deviant slain: {r.Title}.");
    }

    // ---- the shared world -----------------------------------------------------------------------

    /// <summary>
    /// The shared world named a deviant: if it's one that just beat us and walked off, that's ours, and from now on it
    /// goes by the server's name and the fights with it count on the roster.
    /// </summary>
    public void Named(Tristinwold.Deviant.Deviant n)
    {
        var r = main.RaidLog.Roamers.Where(r => r.DeviantId is null && r.KindId == n.Kind && main.GuildBook.PlaySeconds - r.Since < 180)
            .OrderByDescending(r => r.Since).FirstOrDefault();
        if (r is null) return;
        var was = r.Title;
        (r.DeviantId, r.Name) = (n.Id, n.FullName);
        if (_out.FirstOrDefault(o => o.Roamer == r) is { } o) Strengthen(o);
        main.HudNode.Log($"{was} is known across the shared world as {r.Title}.");
    }

    /// <summary>A line for the quest tracker: whatever is hunting the hero, or else the nearest deviant they might meet.</summary>
    public string? Tracker(Vector3 from, Func<Vector3, (double, double), string> direction)
    {
        Vector3 At(Roamer r) => _out.FirstOrDefault(o => o.Roamer == r && IsInstanceValid(o.Body))?.Body.GlobalPosition ?? Where(r);
        if (Hunters.Select(r => (Roamer: r, At: At(r))).OrderBy(x => Flat(x.At, from)).FirstOrDefault() is { Roamer: not null } hunter)
            return $"Hunted by {hunter.Roamer.Title}: {Flat(hunter.At, from):0} m ({direction(from, (hunter.At.X, hunter.At.Z))})";
        var near = main.RaidLog.Roamers.Select(r => (Roamer: r, At: Where(r))).OrderBy(x => Flat(x.At, from)).FirstOrDefault();
        if (near.Roamer is null || Flat(near.At, from) > 900) return null;
        return $"Deviant: {near.Roamer.Title} ({direction(from, (near.At.X, near.At.Z))})";
    }

    /// <summary>Where each roamer is now, for the map.</summary>
    public IEnumerable<(double X, double Z)> Spots => main.RaidLog.Roamers.Select(r => Where(r)).Select(v => ((double)v.X, (double)v.Z));

    // ---- for the self-test ----------------------------------------------------------------------

    public Enemy? BodyOf(Roamer r) => _out.FirstOrDefault(o => o.Roamer == r && IsInstanceValid(o.Body))?.Body;
    public Counter FightsWith(Roamer r) => _out.FirstOrDefault(o => o.Roamer == r)?.Applied ?? Counter.None;
}
