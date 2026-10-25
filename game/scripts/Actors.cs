using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

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
        _shape = new CollisionShape3D();
        AddChild(_shape);
        Plate = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 8, PixelSize = 0.008f };
        AddChild(Plate);
        Resize(height, radius);
    }

    CollisionShape3D _shape = null!;

    /// <summary>Sets how big it is to bump into, and where its name floats.</summary>
    protected void Resize(float height, float radius)
    {
        var h = Math.Max(height, radius * 2);
        _shape.Shape = new CapsuleShape3D { Radius = radius, Height = h };
        _shape.Position = new Vector3(0, h / 2, 0);
        Plate.Position = new Vector3(0, height + 0.45f, 0);
        Plate.PixelSize = 0.008f * Math.Max(1, height / 4);
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

    /// <summary>How big a dragon has grown (other creatures stay Normal).</summary>
    public DragonSize Size { get; private set; }

    /// <summary>The ancient dragon (or other leader) it follows, and the one it's at war with.</summary>
    public string? Faction { get; set; }
    public string? Foe { get; set; }

    public bool IsDragon => Dragons.ColourOf(Kind.Id) is not null;

    /// <summary>How much bigger than its kind's normal size it is drawn.</summary>
    float Bulk => IsDragon ? Dragons.Scale(Size).Height / Dragons.Scale(DragonSize.Normal).Height : 1;

    /// <summary>A winged dragon: it can take to the air.</summary>
    public bool CanFly => Figure is Creature { Winged: true };

    /// <summary>In the air (a dragon circling, or flying in to fight).</summary>
    public bool Airborne { get; private set; }

    float _altitude;
    double _flightClock = 10 + Random.Shared.NextDouble() * 20;

    Player? _target;
    Enemy? _rival;
    double _attackCooldown;
    double _wanderTimer;
    double _lookForRival;
    Vector3 _wanderTo;
    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());

    public static Enemy Create(EnemyKind kind, Vector3 at, double danger = 1, string? siteId = null, DragonSize size = DragonSize.Normal)
    {
        var (toughness, might, _) = Dragons.Scale(size);
        var e = new Enemy { Kind = kind, Home = at, Position = at, Stats = kind.NewCombatant(danger, toughness, might), SiteId = siteId, Size = size };
        e.Dress(kind.Id, (float)Math.Max(kind.Height, 0.6) * (kind.Id == "frost_troll" ? 1.2f : 1), kind.Height < 1 ? 0.3f : kind.Id is "bear" or "frost_troll" ? 0.6f : 0.4f);
        e.Shape();
        if (size > DragonSize.Normal) e.Stats.Name = $"{size} {kind.Name}";
        e.RefreshPlate();
        return e;
    }

    /// <summary>Fits the body and its collision to the dragon's size.</summary>
    void Shape()
    {
        if (!IsDragon) return;
        Figure.Scale = Vector3.One * Bulk;
        var height = Dragons.Scale(Size).Height;
        Resize(height * 0.55f, height * 0.22f);
    }

    /// <summary>
    /// A dragon nemesis grows with every win: a size for each time it has beaten a party and each
    /// rank it has risen to, from normal to large to giant to ancient.
    /// </summary>
    public void Grow(DragonSize size)
    {
        if (!IsDragon || size <= Size) return;
        var (toughness, might, _) = Dragons.Scale(size);
        var (oldToughness, oldMight, _) = Dragons.Scale(Size);
        Stats.Grow(toughness / oldToughness);
        Stats.DamageMult *= might / oldMight;
        Size = size;
        Shape();
    }

    /// <summary>The server says this one is a nemesis: rename, strengthen, and let it speak.</summary>
    public void MakeNemesis(Guid id, BattleModifiers mods, string? taunt)
    {
        NemesisId = id;
        Taunt = taunt;
        Stats.Apply(mods);
        if (IsDragon) Grow((DragonSize)Math.Min(3, Math.Max((int)mods.Rank, mods.Victories)));
        else Scale = new Vector3(1.15f, 1.15f, 1.15f);
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

    /// <summary>Another creature attacks it (a dragon of a warring flight): it fights back.</summary>
    public void TakeHitFrom(Enemy attacker, double damage, bool crit)
    {
        if (State == EnemyState.Dead) return;
        var result = Stats.TakeHit(damage, attacker.Kind.DamageKind, AttackKind.Weapon, crit, attacker.Stats);
        if (_target is null && State != EnemyState.Fleeing) { _rival = attacker; State = EnemyState.Fighting; }
        RefreshPlate();
        if (result.Killed) Die();
        else Figure.Hurt();
    }

    /// <summary>The nearest living fighter of the flight it's at war with, within sight.</summary>
    Enemy? FindRival() =>
        Foe is null ? null : GetTree().GetNodesInGroup("enemies").OfType<Enemy>()
            .Where(e => IsInstanceValid(e) && e.Faction == Foe && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(GlobalPosition) < 120)
            .MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));

    public bool FightingCompany => State == EnemyState.Fighting && _target is not null;

    public override void _PhysicsProcess(double delta)
    {
        if (State == EnemyState.Dead)
        {
            if (!IsOnFloor()) FallAndSlide(delta, Vector3.Zero); // a dragon killed in the air falls
            return;
        }
        Stats.Tick(delta, State == EnemyState.Fighting);
        _attackCooldown -= delta;
        var bulk = Bulk;
        var speed = (float)(Kind.Speed * Stats.SpeedMult) * MathF.Pow(bulk, 0.35f);
        var reach = (float)Kind.Reach * MathF.Pow(bulk, 0.8f);
        var move = Vector3.Zero;

        // Wary creatures (metallic dragons) leave people alone unless attacked.
        var player = _target ?? Player.Local;
        if (State == EnemyState.Idle && !Kind.Wary && player is not null && !player.Downed && player.GlobalPosition.DistanceTo(GlobalPosition) < (Kind.Timid ? 16 : 14) * MathF.Sqrt(bulk))
            Provoke(player);

        // At war: look about for the enemy flight.
        if (Foe is not null && _target is null && (_rival is null || !IsInstanceValid(_rival) || _rival.State == EnemyState.Dead) && (_lookForRival -= delta) <= 0)
        {
            _lookForRival = 0.5;
            _rival = FindRival();
            if (_rival is not null) State = EnemyState.Fighting;
            else if (State == EnemyState.Fighting) State = EnemyState.Idle;
        }

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

            case EnemyState.Fighting when _target is null && _rival is not null:
                if (!IsInstanceValid(_rival) || _rival.State == EnemyState.Dead) { _rival = null; break; }
                var toRival = _rival.GlobalPosition - GlobalPosition;
                toRival.Y = 0;
                if (toRival.LengthSquared() > 0.01f) LookAt(GlobalPosition + toRival.Normalized(), Vector3.Up);
                var gap = toRival.Length() - (reach + (float)_rival.Kind.Reach * MathF.Pow(_rival.Bulk, 0.8f) * 0.5f);
                if (gap > 0) move = toRival.Normalized() * speed;
                else if (_attackCooldown <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds;
                    Figure.Attack();
                    var bite = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var bit);
                    _rival.TakeHitFrom(this, bite, bit);
                }
                break;

            case EnemyState.Fighting when _target is not null:
                if (_target.Downed || _target.GlobalPosition.DistanceTo(Home) > Math.Max(IsDragon ? 250 : 60, Roam * 3))
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
                if (toTarget.Length() > reach) move = toTarget.Normalized() * speed;
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
        if (CanFly) move = Fly(move, speed, bulk, delta);
        if (State != EnemyState.Fighting || Airborne) FaceAlong(move, delta);
        if (Airborne || _altitude > 0) Hover(delta, move);
        else FallAndSlide(delta, move);
        Figure.Move(GroundSpeed, IsOnFloor() && !Airborne, Swimming);
    }

    /// <summary>
    /// Decides when a winged dragon takes to the air: now and then it rises to circle over its range and
    /// comes down again; it flies in to a fight that's far off and lands to fight it; it flees by air.
    /// </summary>
    Vector3 Fly(Vector3 move, float speed, float bulk, double delta)
    {
        var lift = MathF.Sqrt(bulk);
        Node3D? quarry = _target is not null ? _target : _rival is not null && IsInstanceValid(_rival) ? _rival : null;
        switch (State)
        {
            case EnemyState.Idle:
                if ((_flightClock -= delta) <= 0)
                {
                    _altitude = _altitude > 0 ? 0 : (22 + (float)_rng.NextDouble() * 20) * lift;
                    _flightClock = _altitude > 0 ? 15 + _rng.NextDouble() * 20 : 25 + _rng.NextDouble() * 40;
                }
                if (Airborne)
                {
                    // Wheel over home in wide circles.
                    var from = GlobalPosition - Home;
                    from.Y = 0;
                    var radius = Math.Max(Roam * 1.5f, 30 * lift);
                    var round = new Vector3(-from.Z, 0, from.X).Normalized() + from.Normalized() * (radius - from.Length()) / radius;
                    return round.Normalized() * speed * 1.4f;
                }
                break;
            case EnemyState.Fighting when quarry is not null:
                var away = quarry.GlobalPosition - GlobalPosition;
                away.Y = 0;
                _altitude = away.Length() > 35 * lift ? 14 * lift : 0; // fly in, then land to fight
                if (Airborne) return away.Normalized() * speed * 1.8f;
                break;
            case EnemyState.Fleeing:
                _altitude = 30 * lift;
                return move * 1.6f;
            default:
                _altitude = 0;
                break;
        }
        return move;
    }

    /// <summary>Sends a winged dragon up to circle at a height for a while (a show of strength, or a photograph).</summary>
    public void TakeOff(float altitude, double seconds = 60)
    {
        if (!CanFly) return;
        _altitude = altitude;
        _flightClock = seconds;
    }

    /// <summary>Flying: climb or sink towards the wanted height above the ground, no gravity.</summary>
    void Hover(double delta, Vector3 move)
    {
        var ground = City.FloorAt(GlobalPosition);
        var height = GlobalPosition.Y - ground;
        var climb = Mathf.Clamp((_altitude - height) * 1.2f, -9, 7);
        Velocity = new Vector3(move.X, climb, move.Z);
        MoveAndSlide();
        Airborne = height > 0.9f || _altitude > 0;
        if (_altitude <= 0 && height < 1f) Airborne = false;
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
        _altitude = 0;
        Airborne = false;
        Figure.Die();
        Plate.Text = "";
        CollisionLayer = 0;
        var timer = GetTree().CreateTimer(20);
        timer.Timeout += QueueFree;
    }
}
