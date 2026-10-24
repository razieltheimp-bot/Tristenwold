using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>Anything that fights: a capsule body, a Combatant and a nameplate.</summary>
public abstract partial class Fighter : CharacterBody3D
{
    public Combatant Stats { get; protected set; } = new("?", 1);
    protected Label3D Plate = new();
    protected MeshInstance3D Body = new();
    protected float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);

    protected void Shape(string colour, float height, float radius = 0.4f)
    {
        Body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = radius, Height = height },
            Position = new Vector3(0, height / 2, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(colour) },
        };
        AddChild(Body);
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = radius, Height = height }, Position = new Vector3(0, height / 2, 0) });
        Plate = new Label3D { Position = new Vector3(0, height + 0.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 10, PixelSize = 0.01f };
        AddChild(Plate);
    }

    protected void FallAndSlide(double delta, Vector3 horizontal)
    {
        var v = Velocity;
        v.X = horizontal.X;
        v.Z = horizontal.Z;
        v.Y = IsOnFloor() ? Math.Max(v.Y, -0.1f) : v.Y - Gravity * (float)delta;
        Velocity = v;
        MoveAndSlide();
    }
}

public enum EnemyState { Idle, Fighting, Fleeing, Dead }

/// <summary>A Tong thug, a skeever, a draugr: wanders its lair, fights the company, sometimes runs.</summary>
public partial class Enemy : Fighter
{
    static long _nextId = 1;

    public long Id { get; } = _nextId++;
    public EnemyKind Kind { get; private set; } = Bestiary.All[0];
    public Vector3 Home { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public Guid? NemesisId { get; private set; }
    public string? Taunt { get; private set; }

    Player? _target;
    double _attackCooldown;
    double _wanderTimer;
    Vector3 _wanderTo;
    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());

    public static Enemy Create(EnemyKind kind, Vector3 at)
    {
        var e = new Enemy { Kind = kind, Home = at, Position = at, Stats = kind.NewCombatant() };
        e.Shape(kind.Colour, (float)kind.Height, kind.Height < 1 ? 0.3f : 0.4f);
        e.RefreshPlate();
        return e;
    }

    /// <summary>The server says this one is a nemesis: rename, strengthen, and let it speak.</summary>
    public void MakeNemesis(Guid id, BattleModifiers mods, string? taunt)
    {
        NemesisId = id;
        Taunt = taunt;
        Stats.Apply(mods);
        Scale = new Vector3(1.15f, 1.15f, 1.15f);
        ((StandardMaterial3D)Body.MaterialOverride).Emission = new Color("#5a1010");
        ((StandardMaterial3D)Body.MaterialOverride).EmissionEnabled = true;
        RefreshPlate();
    }

    public void RefreshPlate()
    {
        Plate.Text = Stats.Dead ? "" : $"{Stats.Name}\n{new string('|', (int)Math.Ceiling(Stats.Health / Stats.MaxHealth * 20))}";
        Plate.Modulate = NemesisId is null ? new Color("#e8dcc4") : new Color("#ff7a5a");
    }

    public void Provoke(Player by)
    {
        if (State is EnemyState.Dead or EnemyState.Fleeing) return;
        _target = by;
        State = EnemyState.Fighting;
    }

    public bool FightingCompany => State == EnemyState.Fighting && _target is not null;

    public override void _PhysicsProcess(double delta)
    {
        if (State == EnemyState.Dead) return;
        Stats.Tick(delta, State == EnemyState.Fighting);
        _attackCooldown -= delta;
        var speed = (float)(Kind.Speed * Stats.SpeedMult);
        var move = Vector3.Zero;

        var player = _target ?? Player.Local;
        if (State == EnemyState.Idle && player is not null && !player.Downed && player.GlobalPosition.DistanceTo(GlobalPosition) < 14)
            Provoke(player);

        switch (State)
        {
            case EnemyState.Idle:
                _wanderTimer -= delta;
                if (_wanderTimer <= 0)
                {
                    _wanderTimer = 3 + _rng.NextDouble() * 4;
                    _wanderTo = Home + new Vector3((float)(_rng.NextDouble() * 8 - 4), 0, (float)(_rng.NextDouble() * 8 - 4));
                }
                var toWander = _wanderTo - GlobalPosition;
                toWander.Y = 0;
                if (toWander.Length() > 0.5f) move = toWander.Normalized() * speed * 0.3f;
                break;

            case EnemyState.Fighting when _target is not null:
                if (_target.Downed || _target.GlobalPosition.DistanceTo(Home) > 60)
                {
                    _target = null;
                    State = EnemyState.Idle;
                    break;
                }
                // The weak and cowardly run when it goes badly.
                if (Stats.Health < Stats.MaxHealth * 0.2 && Kind.Id is "skeever" or "lantern_boy" && NemesisId is null)
                {
                    State = EnemyState.Fleeing;
                    break;
                }
                var toTarget = _target.GlobalPosition - GlobalPosition;
                toTarget.Y = 0;
                LookAt(GlobalPosition + toTarget.Normalized(), Vector3.Up);
                if (toTarget.Length() > Kind.Reach) move = toTarget.Normalized() * speed;
                else if (_attackCooldown <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Stats.Statuses.Contains(AutoStatus.Haste) ? 1.3 : 1);
                    var damage = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var crit);
                    _target.TakeHit(damage, Kind.DamageKind, AttackKind.Weapon, crit, this);
                }
                break;

            case EnemyState.Fleeing:
                var away = player is null ? Vector3.Forward : GlobalPosition - player.GlobalPosition;
                away.Y = 0;
                move = away.Normalized() * speed * 1.2f;
                if (player is null || player.GlobalPosition.DistanceTo(GlobalPosition) > 45)
                {
                    _target = null; // got away
                }
                break;
        }
        FallAndSlide(delta, move);
    }

    public HitResult TakeHit(double damage, DamageKind kind, AttackKind attack, bool crit, Player by)
    {
        var result = Stats.TakeHit(damage, kind, attack, crit, by.Stats);
        Provoke(by);
        RefreshPlate();
        if (result.Killed) Die();
        return result;
    }

    void Die()
    {
        State = EnemyState.Dead;
        Body.RotationDegrees = new Vector3(90, 0, 0);
        Body.Position = new Vector3(0, 0.3f, 0);
        Plate.Text = "";
        CollisionLayer = 0;
        var timer = GetTree().CreateTimer(20);
        timer.Timeout += QueueFree;
    }
}
