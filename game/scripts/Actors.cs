using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>Anything that fights: an animated body, a Combatant and a nameplate.</summary>
public abstract partial class Fighter : CharacterBody3D
{
    public Combatant Stats { get; protected set; } = new("?", 1);
    protected Label3D Plate = new();
    public Body Figure { get; private set; } = null!;
    protected float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);

    /// <summary>Gives this fighter its look (a character preset or an animal) and something to collide with.</summary>
    protected void Dress(string look, float height, float radius = 0.4f)
    {
        Figure = Body.For(look);
        AddChild(Figure);
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = radius, Height = Math.Max(height, radius * 2) }, Position = new Vector3(0, Math.Max(height, radius * 2) / 2, 0) });
        Plate = new Label3D { Position = new Vector3(0, height + 0.45f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 8, PixelSize = 0.008f };
        AddChild(Plate);
    }

    /// <summary>Turns the body to face where it's going.</summary>
    protected void FaceAlong(Vector3 move, double delta)
    {
        move.Y = 0;
        if (move.LengthSquared() < 0.01f) return;
        var target = Mathf.Atan2(-move.X, -move.Z);
        Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, target, (float)Math.Min(1, delta * 10)) };
    }

    protected float GroundSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

    /// <summary>How deep the water is where this one stands; above 0 means wading or swimming.</summary>
    public float Depth => (float)Tristinwold.World.WorldGen.WaterLevel - GlobalPosition.Y;

    /// <summary>Deeper than this, you swim: you float with your head above water.</summary>
    public const float SwimDepth = 1.3f;

    public bool Swimming => Depth > SwimDepth;

    protected void FallAndSlide(double delta, Vector3 horizontal)
    {
        var v = Velocity;
        v.X = horizontal.X;
        v.Z = horizontal.Z;
        if (Depth > SwimDepth - 0.1f)
        {
            // Bob up to float at the surface, rather than walking along the bottom.
            v.Y = Mathf.Lerp(v.Y, (Depth - SwimDepth) * 3f, 0.2f);
            v.X *= 0.55f;
            v.Z *= 0.55f;
        }
        else v.Y = IsOnFloor() ? Math.Max(v.Y, -0.1f) : v.Y - Gravity * (float)delta;
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

    /// <summary>The camp, barrow or den this one guards, if it came from one.</summary>
    public string? SiteId { get; private set; }

    /// <summary>How far from home it wanders when nothing's happening (wild herds range wider).</summary>
    public float Roam { get; set; } = 4;

    Player? _target;
    double _attackCooldown;
    double _wanderTimer;
    Vector3 _wanderTo;
    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());

    public static Enemy Create(EnemyKind kind, Vector3 at, double danger = 1, string? siteId = null)
    {
        var e = new Enemy { Kind = kind, Home = at, Position = at, Stats = kind.NewCombatant(danger), SiteId = siteId };
        e.Dress(kind.Id, (float)Math.Max(kind.Height, 0.6) * (kind.Id == "frost_troll" ? 1.2f : 1), kind.Height < 1 ? 0.3f : kind.Id is "bear" or "frost_troll" ? 0.6f : kind.Id == "dragon" ? 1.5f : 0.4f);
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
        Figure.Mark(new Color("#ff5030"));
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
        if (Kind.Timid) { _target = by; State = EnemyState.Fleeing; return; } // quarry runs
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
        if (State == EnemyState.Idle && player is not null && !player.Downed && player.GlobalPosition.DistanceTo(GlobalPosition) < (Kind.Timid ? 16 : 14))
            Provoke(player);

        switch (State)
        {
            case EnemyState.Idle:
                _wanderTimer -= delta;
                if (_wanderTimer <= 0)
                {
                    _wanderTimer = 3 + _rng.NextDouble() * 4;
                    _wanderTo = Home + new Vector3((float)(_rng.NextDouble() * 2 - 1) * Roam, 0, (float)(_rng.NextDouble() * 2 - 1) * Roam);
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
                if (toTarget.LengthSquared() > 0.01f) LookAt(GlobalPosition + toTarget.Normalized(), Vector3.Up);
                if (toTarget.Length() > Kind.Reach) move = toTarget.Normalized() * speed;
                else if (_attackCooldown <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Stats.Statuses.Contains(AutoStatus.Haste) ? 1.3 : 1);
                    Figure.Attack();
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
                    // Got away: settle down here.
                    _target = null;
                    State = EnemyState.Idle;
                    Home = GlobalPosition;
                }
                break;
        }
        if (State != EnemyState.Fighting) FaceAlong(move, delta);
        FallAndSlide(delta, move);
        Figure.Move(GroundSpeed, IsOnFloor(), Swimming);
    }

    public HitResult TakeHit(double damage, DamageKind kind, AttackKind attack, bool crit, Player by)
    {
        var result = Stats.TakeHit(damage, kind, attack, crit, by.Stats);
        Provoke(by);
        RefreshPlate();
        if (result.Killed) Die();
        else Figure.Hurt();
        return result;
    }

    void Die()
    {
        State = EnemyState.Dead;
        Figure.Die();
        Plate.Text = "";
        CollisionLayer = 0;
        var timer = GetTree().CreateTimer(20);
        timer.Timeout += QueueFree;
    }
}
