using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>
/// The player: third-person, sword on the left mouse button, a spell on the right (Q cycles
/// fire, frost and shock), and a limit break on R once the gauge is full.
/// </summary>
public partial class Player : Fighter
{
    public static Player? Local { get; private set; }

    public string Company { get; set; } = "Adventurer";
    public bool Downed { get; private set; }
    public DamageKind Element { get; set; } = DamageKind.Fire;
    public int Septims { get; set; }

    /// <summary>Their class, chosen when the character was made: Knight, Barbarian, Mage, Rogue or Rogue_Hooded.</summary>
    public string Outfit { get; init; } = "Knight";

    public event Action<string>? Said;
    public event Action? WentDown;
    public event Action<Enemy, HitResult, DamageKind>? Struck;

    const float WalkSpeed = 5, SprintSpeed = 9, JumpSpeed = 5;
    const double SwingCost = 12, SpellCost = 18, SwordDamage = 22, SpellDamage = 28;

    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());
    Node3D _pivot = new();
    SpringArm3D _arm = new();
    double _swingCooldown, _spellCooldown, _downTimer;

    public Camera3D Camera { get; private set; } = null!;

    public override void _Ready()
    {
        Local = this;
        Stats = new Combatant(Company, 150, magicka: 100, stamina: 120, armor: 80);
        Dress(Outfit, 1.8f);
        Plate.Text = "";
        _pivot = new Node3D { Position = new Vector3(0, 1.6f, 0) };
        AddChild(_pivot);
        _arm = new SpringArm3D { SpringLength = 5, Position = new Vector3(0.6f, 0, 0) };
        _pivot.AddChild(_arm);
        _arm.AddChild(Camera = new Camera3D { Current = true, Far = 1000 });
        if (DisplayServer.GetName() != "headless") Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
            Look(motion.Relative * 0.003f);
    }

    /// <summary>Turns the view: x is yaw, y is pitch (radians). From the mouse, or a finger on a touch screen.</summary>
    public void Look(Vector2 by)
    {
        if (Frozen) return;
        RotateY(-by.X);
        _pivot.Rotation = new Vector3(Mathf.Clamp(_pivot.Rotation.X - by.Y, -1.2f, 0.6f), 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        _swingCooldown -= delta;
        _spellCooldown -= delta;
        if (Downed)
        {
            _downTimer -= delta;
            if (_downTimer <= 0) Rise();
            FallAndSlide(delta, Vector3.Zero);
            return;
        }
        Stats.Tick(delta, InFight);
        if (Frozen)
        {
            FallAndSlide(delta, Vector3.Zero);
            Figure.Move(0, IsOnFloor(), Swimming);
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var sprint = Input.IsActionPressed("sprint") && Stats.Stamina > 1;
        var dir = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var speed = (sprint ? SprintSpeed : WalkSpeed) * (float)Stats.SpeedMult;
        if (sprint && input != Vector2.Zero) Stats.Spend(AttackKind.Weapon, 15 * delta);
        if (Input.IsActionJustPressed("jump") && (IsOnFloor() || Swimming)) Velocity = Velocity with { Y = JumpSpeed };
        FallAndSlide(delta, dir * speed);

        // The body turns to run the way you're going; it faces forward to strike or cast.
        var acting = _swingCooldown > 0.25 || _spellCooldown > 0.55;
        var target = acting ? 0 : input == Vector2.Zero ? Figure.Rotation.Y : Mathf.Atan2(-input.X, -input.Y); // -Z is forward
        Figure.Rotation = Figure.Rotation with { Y = Mathf.LerpAngle(Figure.Rotation.Y, target, (float)Math.Min(1, delta * 12)) };
        Figure.Move(GroundSpeed, IsOnFloor(), Swimming);

        if (Input.IsActionJustPressed("attack")) Swing();
        if (Input.IsActionJustPressed("spell")) Cast();
        if (Input.IsActionJustPressed("limit")) LimitBreak();
        if (Input.IsActionJustPressed("cycle_element"))
        {
            Element = Element switch { DamageKind.Fire => DamageKind.Frost, DamageKind.Frost => DamageKind.Shock, _ => DamageKind.Fire };
            Said?.Invoke($"Spell: {Element}");
        }
    }

    /// <summary>Set by the game while a fight is on, for regeneration.</summary>
    public bool InFight { get; set; }

    /// <summary>Set while talking or reading the map: no walking off mid-sentence.</summary>
    public bool Frozen { get; set; }

    /// <summary>A sword swing: hits the closest enemy in front, within reach.</summary>
    public bool Swing()
    {
        if (Downed || _swingCooldown > 0 || !Stats.Spend(AttackKind.Weapon, SwingCost)) return false;
        _swingCooldown = 0.6;
        Figure.Rotation = Figure.Rotation with { Y = 0 };
        Figure.Attack();
        var target = EnemiesInFront(2.4f, 70).FirstOrDefault();
        if (target is null) return true;
        Hit(target, Stats.Outgoing(AttackKind.Weapon, SwordDamage, _rng, out var crit), DamageKind.Physical, AttackKind.Weapon, crit);
        return true;
    }

    /// <summary>A bolt of the chosen element at the nearest enemy you're facing, up to 25 m.</summary>
    public bool Cast()
    {
        if (Downed || _spellCooldown > 0 || !Stats.Spend(AttackKind.Spell, SpellCost)) return false;
        _spellCooldown = 1.0;
        Figure.Rotation = Figure.Rotation with { Y = 0 };
        Figure.Cast();
        var target = EnemiesInFront(25, 25).FirstOrDefault();
        if (target is not null) Hit(target, Stats.Outgoing(AttackKind.Spell, SpellDamage, _rng, out _), Element, AttackKind.Spell, false);
        return true;
    }

    /// <summary>The limit break: a whirlwind that strikes everything around you, hard.</summary>
    public bool LimitBreak()
    {
        if (Downed || !Stats.UseLimit()) return false;
        Figure.Attack(heavy: true);
        Said?.Invoke("LIMIT BREAK: Ring-Breaker!");
        foreach (var e in Enemies().Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < 7).ToList())
            Hit(e, Stats.Outgoing(AttackKind.Weapon, SwordDamage * 4, _rng, out var crit), DamageKind.Physical, AttackKind.Weapon, crit);
        return true;
    }

    void Hit(Enemy target, double damage, DamageKind kind, AttackKind attack, bool crit)
    {
        var result = target.TakeHit(damage, kind, attack, crit, this);
        Struck?.Invoke(target, result, kind);
        if (result.Healed > 0) Said?.Invoke($"{target.Stats.Name} drinks the {kind.ToString().ToLowerInvariant()}!");
        if (result.Reflected > 0) Said?.Invoke($"{target.Stats.Name} turns the blow back on you.");
    }

    public void TakeHit(double damage, DamageKind kind, AttackKind attack, bool crit, Enemy from)
    {
        if (Downed) return;
        Stats.TakeHit(damage, kind, attack, crit, from.Stats);
        if (!Stats.Dead) Figure.Hurt();
        if (Stats.Dead) GoDown();
    }

    void GoDown()
    {
        Downed = true;
        _downTimer = 4;
        Figure.Die();
        WentDown?.Invoke();
    }

    /// <summary>Waking up at the Gallows Perch. Mother Corva doesn't ask.</summary>
    void Rise()
    {
        Downed = false;
        Figure.Rise();
        Stats.Heal(Stats.MaxHealth);
        GlobalPosition = City.PlayerStart;
        Said?.Invoke("You wake at the Gallows Perch. Someone dragged you here.");
    }

    IEnumerable<Enemy> Enemies() =>
        GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => e.State != EnemyState.Dead);

    IEnumerable<Enemy> EnemiesInFront(float range, float coneDegrees)
    {
        var forward = -GlobalTransform.Basis.Z;
        return Enemies()
            .Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) <= range)
            .Where(e =>
            {
                var to = e.GlobalPosition - GlobalPosition;
                to.Y = 0;
                return to.Length() < 0.5f || Mathf.RadToDeg(forward.AngleTo(to.Normalized())) <= coneDegrees;
            })
            .OrderBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
    }

    /// <summary>Turn to face a point (used by the self-test, and handy for lock-on later).</summary>
    public void Face(Vector3 point)
    {
        var to = point - GlobalPosition;
        to.Y = 0;
        if (to.Length() > 0.01f) LookAt(GlobalPosition + to, Vector3.Up);
    }
}
