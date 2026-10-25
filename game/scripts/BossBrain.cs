using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A lair boss's own moves (see <see cref="BossMove"/>), every moment it fights, whether in its lair or out roaming
/// with a name. Every big blow is shown before it lands: the boss gathers itself, or the ground it will strike glows,
/// so a hero who watches and moves can get clear.
/// </summary>
public sealed class BossBrain(Main main, Raid raid, string bossName, Action<Vector3> summon)
{
    double _burst = 5, _bolt = 3, _volley = 4, _marks = 4, _pounce = 6, _charge = 5;
    double _windUp;
    Action? _then;
    int _summons;
    bool _enraged, _charging, _chargeHit;

    public string Name { get; set; } = bossName;

    bool Has(BossMove m) => raid.Moves.Contains(m);
    Node World => main;

    public void Think(Enemy boss, double delta)
    {
        var p = main.PlayerNode;
        if (p.Downed) return;
        var d = boss.GlobalPosition.DistanceTo(p.GlobalPosition);
        var damage = boss.Kind.Damage * boss.Stats.DamageMult;
        var kind = raid.Element;

        // Gathering itself for something: let it land when the moment comes.
        if (_windUp > 0)
        {
            if ((_windUp -= delta) <= 0) { _then?.Invoke(); _then = null; }
            return;
        }
        if (_charging) return;
        (_burst, _bolt, _volley, _marks, _pounce, _charge) = (_burst - delta, _bolt - delta, _volley - delta, _marks - delta, _pounce - delta, _charge - delta);

        if (Has(BossMove.Burst) && _burst <= 0 && d < 7)
        {
            _burst = 9;
            boss.Figure.Perform("Spellcast_Raise", 1.4, 1);
            Effects.Rise(boss, kind, 3);
            Warn(boss.GlobalPosition, 6.5f, kind, 1.4);
            main.HudNode.Toast($"{Name} gathers itself...", 2);
            WindUp(1.4, () =>
            {
                Effects.Burst(World, boss.GlobalPosition + Vector3.Up * 0.5f, kind, 6);
                HitAround(boss, boss.GlobalPosition, 6.5f, damage * 1.6, kind);
            });
            return;
        }
        if (Has(BossMove.Marks) && _marks <= 0 && d < 35)
        {
            _marks = 8;
            boss.Figure.Perform("Spellcast_Summon", 1, 1);
            var spots = new List<Vector3> { p.GlobalPosition };
            for (var i = 0; i < 2; i++)
            {
                var a = (float)GD.RandRange(0, Mathf.Tau);
                spots.Add(p.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)GD.RandRange(3.0, 5.5));
            }
            foreach (var s in spots) Warn(s, 2.6f, kind, 1.6);
            main.HudNode.Toast(kind switch
            {
                DamageKind.Shock => "The ground crackles where you stand. Move!",
                DamageKind.Fire => $"{Name} hurls firepots! Move!",
                _ => "The ground cracks beneath you. Move!",
            }, 2);
            // These land on their own while the boss carries on.
            boss.GetTree().CreateTimer(1.6).Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(boss) || boss.State == EnemyState.Dead) return;
                foreach (var s in spots)
                {
                    var at = s with { Y = City.FloorAt(s) };
                    if (kind == DamageKind.Shock) Effects.Bolt(World, at + Vector3.Up * 18, at, kind, 0.5f);
                    else if (kind == DamageKind.Fire) Effects.Bolt(World, boss.GlobalPosition + Vector3.Up * 2.5f, at, kind, 0.35f);
                    Effects.Burst(World, at + Vector3.Up * 0.3f, kind, 2.5f);
                    HitAround(boss, at, 2.6f, damage * 1.3, kind);
                }
            };
            return;
        }
        if (Has(BossMove.Pounce) && _pounce <= 0 && d is > 4 and < 25)
        {
            _pounce = 10;
            Effects.Smoke(World, boss.GlobalPosition + Vector3.Up, 2);
            boss.Figure.Visible = false;
            WindUp(0.8, () =>
            {
                boss.Figure.Visible = true;
                var behind = p.GlobalPosition + p.GlobalTransform.Basis.Z * 3;
                boss.GlobalPosition = behind with { Y = City.FloorAt(behind) + 0.3f };
                Turn(boss, p.GlobalPosition);
                Effects.Smoke(World, boss.GlobalPosition + Vector3.Up, 2);
                if (kind != DamageKind.Poison) Effects.Burst(World, boss.GlobalPosition + Vector3.Up * 0.3f, kind, 3);
                boss.Figure.Attack(true);
                main.HudNode.Toast($"{Name} is behind you!", 2);
                if (!p.Downed && p.GlobalPosition.DistanceTo(boss.GlobalPosition) < 4.5f) p.TakeHit(damage * 1.4, kind, AttackKind.Weapon, false, boss);
            });
            return;
        }
        if (Has(BossMove.Charge) && _charge <= 0 && d is > 8 and < 30)
        {
            _charge = 9;
            Turn(boss, p.GlobalPosition);
            boss.Figure.Perform("Cheer", 0.8, 1.2f);
            main.HudNode.Toast($"{Name} lowers its shield to charge!", 2);
            var from = boss.GlobalPosition;
            var to = p.GlobalPosition;
            var line = (to - from) with { Y = 0 };
            var end = from + line.Normalized() * Math.Min(line.Length() + 3, 28);
            Warn(from.Lerp(end, 0.5f), 1.6f, kind, 0.8, stretch: new Vector3(1, 1, line.Length() / 3.2f + 1), facing: line);
            WindUp(0.8, () =>
            {
                (_charging, _chargeHit) = (true, false);
                var tween = boss.CreateTween();
                tween.TweenMethod(Callable.From<float>(f =>
                {
                    if (!GodotObject.IsInstanceValid(boss)) return;
                    var at = from.Lerp(end, f);
                    boss.GlobalPosition = at with { Y = City.FloorAt(at) + 0.2f };
                    if (!p.Downed && !_chargeHit && p.GlobalPosition.DistanceTo(boss.GlobalPosition) < 2.6f)
                    {
                        _chargeHit = true;
                        p.TakeHit(damage * 1.5, kind, AttackKind.Weapon, false, boss);
                        Effects.Burst(World, p.GlobalPosition + Vector3.Up, kind, 2);
                    }
                }), 0f, 1f, 0.6);
                tween.TweenCallback(Callable.From(() => _charging = false));
            });
            return;
        }
        if (Has(BossMove.Volley) && _volley <= 0 && d is > 6 and < 30)
        {
            _volley = 7;
            Turn(boss, p.GlobalPosition);
            boss.Figure.Perform("Spellcast_Shoot", 0.8, 1.2f);
            var mouth = boss.GlobalPosition + Vector3.Up * (float)boss.Kind.Height * 0.7f;
            for (var i = 0; i < 3; i++)
            {
                var aim = p.GlobalPosition + Vector3.Up + (i == 0 ? Vector3.Zero : new Vector3((float)GD.RandRange(-2.5, 2.5), 0, (float)GD.RandRange(-2.5, 2.5)));
                Effects.Bolt(World, mouth, aim, kind, 0.25f);
                if (aim.DistanceTo(p.GlobalPosition + Vector3.Up) < 1.5f) p.TakeHit(damage * 0.7, kind, AttackKind.Spell, false, boss);
            }
            return;
        }
        if (Has(BossMove.Bolt) && _bolt <= 0 && d is > 8 and < 30)
        {
            _bolt = 5;
            Turn(boss, p.GlobalPosition);
            boss.Figure.Perform("Spellcast_Shoot", 0.8, 1.2f);
            Effects.Bolt(World, boss.GlobalPosition + Vector3.Up * (float)boss.Kind.Height * 0.8f, p.GlobalPosition + Vector3.Up, kind, 0.4f);
            p.TakeHit(damage, kind, AttackKind.Spell, false, boss);
        }

        // As it weakens: its own come when called, and near the end it goes wild.
        var share = boss.Stats.Health / boss.Stats.MaxHealth;
        if (Has(BossMove.Summon) && ((_summons == 0 && share < 0.66) || (_summons == 1 && share < 0.33)))
        {
            _summons++;
            boss.Figure.Perform("Spellcast_Summon", 1.2, 1);
            main.HudNode.Toast(raid.Calling.Length > 0 ? raid.Calling.Replace(raid.BossCap, Name) : $"{Name} calls for help!", 4);
            for (var i = 0; i < 2; i++)
            {
                var a = (float)GD.RandRange(0, Mathf.Tau);
                summon(boss.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5);
            }
        }
        if (Has(BossMove.Enrage) && !_enraged && share < 0.25)
        {
            _enraged = true;
            boss.Stats.DamageMult *= 1.3;
            boss.Stats.SpeedMult *= 1.3;
            (_burst, _marks, _pounce) = (Math.Min(_burst, 1), Math.Min(_marks, 2), Math.Min(_pounce, 2));
            Effects.Rise(boss, DamageKind.Fire, 3);
            boss.Figure.Mark(new Color("#ff3020"));
            main.HudNode.Toast($"{Name} goes wild!", 4);
        }
    }

    void WindUp(double seconds, Action then) => (_windUp, _then) = (seconds, then);

    /// <summary>Everyone of the company within reach of a blow that lands all around a spot.</summary>
    void HitAround(Enemy boss, Vector3 at, float radius, double damage, DamageKind kind)
    {
        var p = main.PlayerNode;
        if (!p.Downed && Flat(p.GlobalPosition, at) < radius) p.TakeHit(damage, kind, AttackKind.Spell, false, boss);
        foreach (var c in p.Companions().Where(c => Flat(c.GlobalPosition, at) < radius).ToList()) c.TakeHitFrom(boss, damage, false, kind);
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>The ground glowing where a blow is about to land.</summary>
    void Warn(Vector3 at, float radius, DamageKind kind, double seconds, Vector3? stretch = null, Vector3? facing = null)
    {
        var colour = Effects.Of(kind);
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(colour, 0.0f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Effects.Dot, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var ring = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Vector2.One * radius * 2.3f }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        main.AddChild(ring);
        ring.GlobalPosition = at with { Y = City.FloorAt(at) + 0.08f };
        if (facing is { } f && f.LengthSquared() > 0.01f) ring.LookAt(ring.GlobalPosition + f, Vector3.Up);
        if (stretch is { } s) ring.Scale = s;
        var tween = ring.CreateTween();
        tween.TweenProperty(mat, "albedo_color:a", 0.75f, seconds * 0.8);
        tween.TweenProperty(mat, "albedo_color:a", 0.0f, 0.25);
        tween.TweenCallback(Callable.From(ring.QueueFree));
    }

    public static void Turn(Node3D who, Vector3 toward)
    {
        var to = toward - who.GlobalPosition;
        to.Y = 0;
        if (to.LengthSquared() > 0.01f) who.LookAt(who.GlobalPosition + to, Vector3.Up);
    }
}
