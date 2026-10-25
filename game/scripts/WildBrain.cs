using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;
using Trick = Tristinwold.World.Wildlife.Trick;

namespace Tristinwold.Game;

/// <summary>
/// What the stranger creatures do in a fight besides bite and claw (see <see cref="Wildlife.Trick"/>): a kipine
/// beats up into the air and comes down on you, and snatches your coin; a deathcrest ape blinks through shadow to
/// your flank and bites; a dire imp vanishes and strikes from beside you; an imp, badly hurt, turns into a raven and
/// flies off; a winged ape hurls rocks, then drops on you from above. They do it to whatever they're fighting: you,
/// your companions, or (for the apes) the walking dead they hunt.
/// </summary>
public sealed class WildBrain(Main main, IReadOnlyList<Trick> tricks)
{
    double _dive = 3 + GD.Randf() * 3, _jaunt = 1 + GD.Randf() * 2, _vanish = 3 + GD.Randf() * 3, _throw = 1 + GD.Randf() * 2, _steal = 2;
    bool _busy, _shifted, _warned;
    int _stolen;

    /// <summary>Gives a creature its tricks, if its kind has any (the young have none yet).</summary>
    public static void Give(Main main, Enemy e)
    {
        var tricks = Wildlife.TricksOf(e.Kind.Id);
        if (tricks.Count == 0 || e.Young) return;
        var brain = new WildBrain(main, tricks);
        e.Think = brain.Think;
        if (tricks.Contains(Trick.Steal))
        {
            // Kill it, and the coin it took is yours again.
            Action<Enemy, Enemy?, bool>? paid = null;
            paid = (victim, _, _) =>
            {
                if (victim != e || brain._stolen == 0) return;
                main.PlayerNode.Septims += brain._stolen;
                main.HudNode.Toast($"You get back the {brain._stolen} septims the {e.Kind.Name.ToLowerInvariant()} took.", 5);
                brain._stolen = 0;
            };
            Enemy.Slain += paid;
            e.TreeExiting += () => Enemy.Slain -= paid;
        }
    }

    bool Has(Trick t) => tricks.Contains(t);

    public void Think(Enemy e, double delta)
    {
        if (_busy || e.Foe3D is not { } foe || !GodotObject.IsInstanceValid(foe)) return;
        var d = Flat(e.GlobalPosition, foe.GlobalPosition);
        var damage = e.Kind.Damage * e.Stats.DamageMult;
        (_dive, _jaunt, _vanish, _throw, _steal) = (_dive - delta, _jaunt - delta, _vanish - delta, _throw - delta, _steal - delta);

        if (Has(Trick.Shift) && !_shifted && e.Stats.Health < e.Stats.MaxHealth * 0.4)
        {
            // Turns into a raven and flies off.
            _shifted = true;
            Effects.Smoke(main, e.GlobalPosition + Vector3.Up * 0.5f, 1.2f);
            e.Reshape("raven");
            if (main.PlayerNode is { } p) e.Flee(p);
            return;
        }
        if (Has(Trick.Dive) && _dive <= 0 && d < 28)
        {
            _dive = 9 + GD.Randf() * 4;
            Dive(e, foe, damage);
            return;
        }
        if (Has(Trick.Throw) && _throw <= 0 && d is > 7 and < 30)
        {
            _throw = 4 + GD.Randf() * 2;
            BossBrain.Turn(e, foe.GlobalPosition);
            e.Figure.Attack();
            Effects.Bolt(main, e.GlobalPosition + Vector3.Up * (float)e.Kind.Height * 0.8f, foe.GlobalPosition + Vector3.Up, DamageKind.Physical, 0.3f);
            Hit(e, foe, damage * 0.8, DamageKind.Physical);
            return;
        }
        if (Has(Trick.Jaunt) && _jaunt <= 0 && d < 4)
        {
            // A blink through shadow to the flank, and a poisoned bite.
            _jaunt = 4 + GD.Randf() * 3;
            Effects.Smoke(main, e.GlobalPosition + Vector3.Up, 1);
            var a = GD.Randf() > 0.5f ? 1.6f : -1.6f;
            var to = foe.GlobalPosition + (foe.GlobalTransform.Basis.Z.Rotated(Vector3.Up, a) with { Y = 0 }).Normalized() * 2.5f;
            e.GlobalPosition = to with { Y = City.FloorAt(to) + 0.3f };
            BossBrain.Turn(e, foe.GlobalPosition);
            Effects.Smoke(main, e.GlobalPosition + Vector3.Up, 1);
            e.Figure.Attack();
            Hit(e, foe, damage, DamageKind.Poison);
            return;
        }
        if (Has(Trick.Vanish) && _vanish <= 0 && d < 20)
        {
            _vanish = 10 + GD.Randf() * 4;
            Vanish(e, foe, damage);
            return;
        }
        if (Has(Trick.Steal) && _steal <= 0 && d < 3.2f && foe is Player hero && e.Leader is null && hero.Septims > 0)
        {
            _steal = 14;
            var take = Math.Min(hero.Septims, 3 + (int)(GD.Randi() % 9));
            hero.Septims -= take;
            _stolen += take;
            main.HudNode.Toast($"The {e.Kind.Name.ToLowerInvariant()} snatches {take} septims and tucks them away!", 4);
        }
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>Harm to whatever it's fighting, hero or creature.</summary>
    static void Hit(Enemy e, Node3D foe, double damage, DamageKind kind)
    {
        if (foe is Player p) { if (!p.Downed) p.TakeHit(damage, kind, AttackKind.Weapon, false, e); }
        else if (foe is Enemy r && r.State != EnemyState.Dead) r.TakeHitFrom(e, damage, false, kind);
    }

    /// <summary>Up into the air on beating wings, then down on the foe from above, hard enough to knock it flat.</summary>
    void Dive(Enemy e, Node3D foe, double damage)
    {
        _busy = true;
        var wings = e.Figure.FindChildren("*", "Node3D", true, false).OfType<FlapWings>().FirstOrDefault();
        if (wings is not null) wings.Flying = true;
        var from = e.GlobalPosition;
        var high = from + Vector3.Up * 9;
        if (!_warned && foe is Player)
        {
            _warned = true;
            main.HudNode.Toast($"The {e.Kind.Name.ToLowerInvariant()} beats up into the air... it's coming down on you!", 3);
        }
        var tween = e.CreateTween();
        tween.TweenProperty(e, "global_position", high, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.4);
        tween.TweenMethod(Callable.From<float>(f =>
        {
            if (!GodotObject.IsInstanceValid(foe) || !GodotObject.IsInstanceValid(e)) return;
            var land = foe.GlobalPosition;
            var at = high.Lerp(land, f);
            e.GlobalPosition = at with { Y = Mathf.Lerp(high.Y, City.FloorAt(land) + 0.3f, f * f) };
            BossBrain.Turn(e, land);
        }), 0f, 1f, 0.55);
        tween.TweenCallback(Callable.From(() =>
        {
            _busy = false;
            if (wings is not null && GodotObject.IsInstanceValid(wings)) wings.Flying = false;
            if (!GodotObject.IsInstanceValid(e) || e.State == EnemyState.Dead) return;
            Effects.Smoke(main, e.GlobalPosition, 2.5f);
            e.Figure.Attack(true);
            if (GodotObject.IsInstanceValid(foe) && Flat(foe.GlobalPosition, e.GlobalPosition) < 3.5f) Hit(e, foe, damage * 1.6, DamageKind.Physical);
        }));
    }

    /// <summary>Gone, invisible, for a few breaths, and then it's beside you, claws burning.</summary>
    void Vanish(Enemy e, Node3D foe, double damage)
    {
        _busy = true;
        Effects.Smoke(main, e.GlobalPosition + Vector3.Up, 1.4f);
        e.Unseen(true);
        if (!_warned && foe is Player)
        {
            _warned = true;
            main.HudNode.Toast($"The {e.Kind.Name.ToLowerInvariant()} vanishes...", 3);
        }
        e.GetTree().CreateTimer(2.2).Timeout += () =>
        {
            _busy = false;
            if (!GodotObject.IsInstanceValid(e)) return;
            e.Unseen(false);
            if (e.State == EnemyState.Dead || !GodotObject.IsInstanceValid(foe)) return;
            var beside = foe.GlobalPosition + (foe.GlobalTransform.Basis.X with { Y = 0 }).Normalized() * 1.8f;
            e.GlobalPosition = beside with { Y = City.FloorAt(beside) + 0.3f };
            BossBrain.Turn(e, foe.GlobalPosition);
            Effects.Burst(main, e.GlobalPosition + Vector3.Up, DamageKind.Fire, 1.5f);
            e.Figure.Attack(true);
            Hit(e, foe, damage * 1.5, DamageKind.Fire);
        };
    }
}
