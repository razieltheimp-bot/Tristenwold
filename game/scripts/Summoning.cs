using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A summoner calling one of the great beasts bound to their ring (see <see cref="Summons"/>): it rises out of a ring of
/// light beside them, strikes as it comes and again every few seconds while it stays, and goes back into the ring. It
/// isn't a fighter that can be struck down: it's the summoner's own power, so its blows are theirs (their spell power,
/// their crits) and whatever they bring down is theirs.
/// </summary>
public partial class Player
{
    double _summonReady;
    Enemy? _summoned;
    Summon? _called;

    /// <summary>Seconds until the ring will answer again.</summary>
    public double SummonReadyIn => Math.Max(0, _summonReady);

    /// <summary>A beast standing over the fight now.</summary>
    public bool Summoning => _summoned is { } e && IsInstanceValid(e) && e.State != EnemyState.Dead;

    /// <summary>The beast called now, if one is.</summary>
    public Enemy? SummonedBeast => Summoning ? _summoned : null;

    /// <summary>Each time a called beast strikes: the game mends the company for a healer, and so on.</summary>
    public event Action<Summon>? BeastStruck;

    /// <summary>A called beast was killed: it's torn out of the ring.</summary>
    public event Action<Summon>? BeastFell;

    void TendSummons(double delta)
    {
        _summonReady -= delta;
        if (_summoned is { } e && _called is { } s && (!IsInstanceValid(e) || e.State == EnemyState.Dead))
        {
            var killed = IsInstanceValid(e) && e.State == EnemyState.Dead; // gone back into the ring, or struck down
            (_summoned, _called) = (null, null);
            if (!killed) return;
            BeastFell?.Invoke(s);
            GetTree().CreateTimer(6).Timeout += () => { if (IsInstanceValid(e)) e.QueueFree(); };
        }
    }

    /// <summary>For the self-test: the ring warm again.</summary>
    public void ReadySummonForTest() => _summonReady = 0;

    /// <summary>Why the ring won't answer right now, or null if it will.</summary>
    public string? CantSummon(Summon s)
    {
        if (Downed) return "You're down.";
        if (Summoning) return "Your beast is already here.";
        if (_summonReady > 0) return $"Your ring is still cold: {Math.Ceiling(_summonReady)} seconds.";
        if (Stats.Mana < Stats.MaxMana * Summons.ManaShare) return $"Calling {s.Name} takes half your mana.";
        return null;
    }

    /// <summary>
    /// Calls a bound beast, for half the hero's mana and a long breath before the next. It comes up out of a ring of light
    /// beside them and fights for the company, with its own moves on top every few seconds, and goes back into the ring
    /// when its time is up. It's a fighter like any other: foes can turn on it, and if it's struck down it's gone from the
    /// ring (see <see cref="BeastFell"/>).
    /// </summary>
    public bool CallBeast(Summon s, double blow)
    {
        if (CantSummon(s) is not null || Summons.TrialOf(s.Id) is not { } trial || !Stats.Spend(AttackKind.Spell, Stats.MaxMana * Summons.ManaShare)) return false;
        _summonReady = Summons.Cooldown;
        var world = GetParent<Node3D>();
        var at = GlobalPosition + GlobalTransform.Basis.X * 4 + GlobalTransform.Basis.Z * 3;
        at.Y = City.FloorAt(at);
        var beast = Enemy.Create(Bestiary.Get(trial.Kind), at + Vector3.Up * 0.5f, size: s.Id == Summons.Draxon ? DragonSize.Giant : DragonSize.Normal, look: s.Look);
        beast.Stats.Grow(trial.Health * Summons.Sturdy / beast.Stats.MaxHealth);
        beast.Stats.Name = $"{s.Name} (called)";
        beast.Leader = this;
        beast.Expires = Summons.Stays;
        world.AddChild(beast);
        beast.AddToGroup("enemies");
        beast.RefreshPlate();
        if (Foes().Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < 40).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition)) is { } foe)
            beast.LookAt(foe.GlobalPosition with { Y = beast.GlobalPosition.Y }, Vector3.Up, true);
        (_summoned, _called) = (beast, s);
        Figure.Perform("Spellcast_Raise", 1, 1);
        Said?.Invoke($"SUMMON: {s.Name}, {s.Title}!");
        Effects.Limit(world, at, new Color(Colour(s.Kind)));
        Effects.Glyph(world, at, new Color(Colour(s.Kind)), 4, 0, s.Kind);

        // Up out of the ring, huge; it strikes as it lands, and again while it stays.
        var body = beast.Figure;
        var full = body.Scale;
        body.Scale = full * 0.05f;
        body.CreateTween().TweenProperty(body, "scale", full, 0.7).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        var strikes = (int)Math.Floor(Summons.Stays / Summons.Pulse);
        for (var i = 0; i < strikes; i++)
            GetTree().CreateTimer(0.8 + i * Summons.Pulse).Timeout += () =>
            {
                if (!IsInstanceValid(beast) || beast.State == EnemyState.Dead || !IsInstanceValid(this)) return;
                body.Attack(heavy: true);
                Strike(s, beast, blow);
                BeastStruck?.Invoke(s);
            };
        return true;
    }

    static string Colour(DamageKind k) => k switch
    {
        DamageKind.Fire => "#ff7a20", DamageKind.Frost => "#bfe8ff", DamageKind.Shock => "#8a9aff", DamageKind.Poison => "#5aff6a", DamageKind.Sun => "#ffe8a0", _ => "#d8c8a8",
    };

    /// <summary>One strike of a called beast, by its move.</summary>
    void Strike(Summon s, Node3D beast, double blow)
    {
        var world = GetParent();
        var at = beast.GlobalPosition;
        var top = at + Vector3.Up * 6 * s.Scale;
        switch (s.Move)
        {
            case SummonMove.Quake: // Orrogh: the ground split under every foe about; thrown, and dazed
                Effects.Burst(world, at + Vector3.Up * 0.3f, DamageKind.Physical, 10);
                foreach (var e in Around(12, at).ToList())
                {
                    Strike(e, blow, DamageKind.Physical, AttackKind.Spell);
                    e.Thrown(at, 4);
                    e.Stunned = Math.Max(e.Stunned, 1.5);
                }
                break;
            case SummonMove.Dawn: // Lumira: the company mended and quickened; a sting of light on every foe
                Heal(0.2);
                For(Summons.Pulse + 1, AutoStatus.Regen, AutoStatus.Haste);
                Effects.Song(world, at, new Color("#ffe8a0"), 10, 14);
                foreach (var e in Around(16, at).ToList())
                {
                    Effects.Bolt(world, top, e.GlobalPosition + Vector3.Up, DamageKind.Sun, 0.25f);
                    Strike(e, blow, DamageKind.Sun, AttackKind.Spell);
                }
                break;
            case SummonMove.Storm: // Quvirn: lightning down on every foe about, leaping from one to the next
                {
                    var prev = top;
                    foreach (var e in Around(25, at).OrderBy(e => e.GlobalPosition.DistanceTo(at)).Take(6).ToList())
                    {
                        Effects.Lightning(world, prev, e.GlobalPosition + Vector3.Up, new Color("#a8b8ff"), 0.35, 0.12f);
                        Effects.Lightning(world, e.GlobalPosition + new Vector3(1, 30, 1), e.GlobalPosition, new Color("#c8d0ff"), 0.3, 0.18f);
                        Strike(e, blow, DamageKind.Shock, AttackKind.Spell);
                        prev = e.GlobalPosition + Vector3.Up;
                    }
                }
                break;
            case SummonMove.Bulwark: // Aegrath: Protect, Shell and Reflect on the company; a stamp that throws foes back
                For(Summons.Pulse + 1, AutoStatus.Protect, AutoStatus.Shell, AutoStatus.Reflect);
                Effects.Burst(world, at + Vector3.Up * 0.3f, DamageKind.Physical, 7);
                foreach (var e in Around(9, at).ToList())
                {
                    Strike(e, blow, DamageKind.Physical, AttackKind.Spell);
                    e.Thrown(at, 6);
                }
                break;
            case SummonMove.Firestorm: // Blaze: a whirlwind of flame round it; foes flung out burning, the ground left alight
                Effects.Burst(world, at + Vector3.Up, DamageKind.Fire, 9);
                foreach (var e in Around(10, at).ToList())
                {
                    Strike(e, blow, DamageKind.Fire, AttackKind.Spell);
                    e.Thrown(at, 5);
                }
                Hazard(at, 9, blow * 0.25, DamageKind.Fire, Summons.Pulse);
                break;
            case SummonMove.Blizzard: // Shicene: ice falling from her wings over everything; foes frozen where they stand
                foreach (var e in Around(15, at).ToList())
                {
                    Effects.Bolt(world, e.GlobalPosition + new Vector3(0.5f, 14, 0.5f), e.GlobalPosition, DamageKind.Frost, 0.5f);
                    Effects.Burst(world, e.GlobalPosition + Vector3.Up * 0.5f, DamageKind.Frost, 1.6f);
                    Strike(e, blow, DamageKind.Frost, AttackKind.Spell);
                    e.Stunned = Math.Max(e.Stunned, 2.5);
                }
                break;
            case SummonMove.Dominion: // Draxon: green Rift-fire; whatever it corrupts and leaves standing obeys the summoner
                {
                    var ahead = -beast.GlobalTransform.Basis.Z;
                    if (Around(30, at).MinBy(e => e.GlobalPosition.DistanceTo(at)) is { } near) ahead = (near.GlobalPosition - at) with { Y = 0 };
                    ahead = ahead.LengthSquared() > 0.01f ? ahead.Normalized() : Vector3.Forward;
                    Effects.Cone(world, at + Vector3.Up * 4, ahead, DamageKind.Poison, 22);
                    foreach (var e in Around(24, at).Where(e => Mathf.RadToDeg(ahead.AngleTo((e.GlobalPosition - at) with { Y = 0 })) < 40).ToList())
                    {
                        Strike(e, blow, DamageKind.Poison, AttackKind.Spell);
                        if (!IsInstanceValid(e) || e.State == EnemyState.Dead || !Commanded(e)) continue;
                        e.Leader = this; // the King of Beasts' will: it fights for you now, until the corruption burns it out
                        e.Expires = Summons.Obeys;
                        Models.Tint(e.Figure, new Color("#2a3a2a"), 0.4f);
                        e.RefreshPlate();
                    }
                }
                break;
        }
    }

    /// <summary>What Draxon's fire can bend to the summoner's will: nothing great, nothing with a name, nothing with a will of its own.</summary>
    static bool Commanded(Enemy e) => e.Think is null && e.DeviantId is null && !e.IsDragon && e.Bond is null && e.Kind.Health < 1500 && e.Hero is null;
}
