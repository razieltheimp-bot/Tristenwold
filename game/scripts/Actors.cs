using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;
using Side = Tristinwold.World.Side;

namespace Tristinwold.Game;

/// <summary>Anything that fights: an animated body, a Combatant and a nameplate.</summary>
public abstract partial class Fighter : CharacterBody3D
{
    public Combatant Stats { get; protected set; } = new("?", 1);
    protected Label3D Plate = new();
    public Body Figure { get; private set; } = null!;
    protected float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);

    /// <summary>Gives this fighter its look (a character preset or an animal) and something to collide with.</summary>
    protected void Dress(string look, float height, float radius = 0.4f, Func<Body>? make = null)
    {
        Figure = make?.Invoke() ?? Body.For(look);
        AddChild(Figure);
        _shape = new CollisionShape3D();
        AddChild(_shape);
        Plate = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 8, PixelSize = 0.008f };
        AddChild(Plate);
        Resize(height, radius);
    }

    CollisionShape3D _shape = null!;

    /// <summary>Changes how it looks (a hero joining another class) without touching the rest.</summary>
    protected void Restyle(string look, Func<Body>? make = null)
    {
        Figure.QueueFree();
        Figure = make?.Invoke() ?? Body.For(look);
        AddChild(Figure);
    }

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

    /// <summary>How quickly a body gets up to speed, and how quickly it stops (metres a second, each second):
    /// quick enough to feel sharp, slow enough that it has weight. Less in the air, and in water.</summary>
    const float Speeding = 28, Stopping = 40, AirControl = 0.3f;

    protected void FallAndSlide(double delta, Vector3 horizontal)
    {
        var v = Velocity;
        var swimming = Depth > SwimDepth - 0.1f;
        var want = new Vector2(horizontal.X, horizontal.Z) * (swimming ? 0.55f : 1);
        var now = new Vector2(v.X, v.Z);
        var rate = want.LengthSquared() > now.LengthSquared() * 0.98f && want.LengthSquared() > 0.01f ? Speeding : Stopping;
        if (!IsOnFloor() && !swimming) rate *= AirControl;
        now = now.MoveToward(want, rate * (float)delta);
        (v.X, v.Z) = (now.X, now.Y);
        if (swimming)
        {
            // Bob up to float at the surface, rather than walking along the bottom.
            v.Y = Mathf.Lerp(v.Y, (Depth - SwimDepth) * 3f, 0.2f);
        }
        else v.Y = IsOnFloor() ? Math.Max(v.Y, -0.1f) : v.Y - Gravity * (float)delta;
        Velocity = v;
        MoveAndSlide();
        // Fell through land that wasn't built yet (after a long flight or a jump across the map), or was flung into
        // the sky by the physics for standing inside something: back on top of the land.
        var floor = City.FloorAt(GlobalPosition);
        if (GlobalPosition.Y < floor - 4 || (GlobalPosition.Y > floor + 30 && Velocity.Y > 0))
        {
            GlobalPosition = GlobalPosition with { Y = floor + 0.5f };
            Velocity = Vector3.Zero;
            ResetPhysicsInterpolation();
        }
    }
}

public enum EnemyState { Idle, Fighting, Fleeing, Dead }

/// <summary>
/// Anything in the world that fights besides the player: a Tong thug, a giant rat, a draugr, a dragon. Wanders its
/// lair and fights the company, sometimes runs. The same body serves the company's own: a beast raised from a
/// youngling, a squire or an apprentice, a warlock's familiar, the dragon a knight rides. And the dragon knights
/// of the alliance and their pact dragons, who fend off chromatic and corrupted dragons on sight.
/// </summary>
public partial class Enemy : Fighter
{
    static long _nextId = 1;

    public long Id { get; } = _nextId++;
    public EnemyKind Kind { get; private set; } = Bestiary.All[0];
    public Vector3 Home { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public Guid? DeviantId { get; private set; }
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

    /// <summary>A youngling: small, weak, it runs from people. Found alone, it can be tamed.</summary>
    public bool Young { get; private set; }

    /// <summary>A pact dragon: it guards people rather than hunting them.</summary>
    public bool Allied { get; set; }

    /// <summary>Carries a dragon knight of the alliance.</summary>
    public bool Rider { get; private set; }

    /// <summary>A companion's bond with its hero: its level, its hunger, what it has learned.</summary>
    public Bond? Bond { get; private set; }

    /// <summary>The hero it follows (a companion, or a summoned familiar).</summary>
    public Player? Leader { get; set; }

    /// <summary>Seconds until a summoned familiar fades away.</summary>
    public double Expires { get; set; } = double.MaxValue;

    /// <summary>The hero riding it.</summary>
    public Player? Carrying { get; private set; }

    /// <summary>On the company's side or the alliance's: the company's blows pass it by.</summary>
    public bool Friendly => Leader is not null || Allied || Rider;

    public Side Side => Leader is not null ? Side.Company
        : Allied || Rider ? Side.Alliance
        : Dragons.IsCorrupted(Kind.Id) ? Side.Corrupted
        : IsDragon ? (Kind.Wary ? Side.Metallic : Side.Chromatic)
        : Side.Wild;

    public bool IsDragon => Dragons.ColourOf(Kind.Id) is not null;
    public bool Undead => Kind.Id is "draugr" or "draugr_overlord" or "sea_wraith" or "familiar" or "cinder_warden" or "cinder_thrall" or "ghost_captain" or "hollow_soldier";

    /// <summary>Called every moment it's fighting: a boss's own moves (the raid sets it).</summary>
    public Action<Enemy, double>? Think { get; set; }

    /// <summary>Told of every blow the hero lands on it (how much, what kind, spell or weapon): a deviant learns from them.</summary>
    public Action<double, DamageKind, AttackKind, Player>? Struck { get; set; }

    /// <summary>A lair's boss or one of its creatures: the lair, not the wilds, decides what becomes of it.</summary>
    public bool InLair { get; set; }

    /// <summary>One of the town Watch: it keeps the peace in Tristinwold's streets (see <see cref="TownWatch"/>).</summary>
    public bool Watch { get; set; }

    /// <summary>Someone's animal (a farm's cow, a town's dog): the place it belongs to.</summary>
    public string? Owner { get; set; }

    /// <summary>Caught at a crime in the streets (a thief, a cutthroat, a mugger): the Watch is after it.</summary>
    public bool Criminal { get; set; }

    /// <summary>
    /// What the Watch will fight: a deviant in the town, or anything out of the wilds that's after the hero in the
    /// streets. A lair's or a camp's own creatures are the hero's business.
    /// </summary>
    bool Disturbs(Enemy e) =>
        !e.Friendly && !e.Sparring && e.State != EnemyState.Dead && (Keeps ?? TownWatch.InTristinwold)(e.GlobalPosition)
        && (e.Criminal || e.Raider || e.DeviantId is not null || e.Named || e.FightingCompany && e.SiteId is null && !e.InLair);

    /// <summary>The streets a guard keeps: Tristinwold's for the Watch, a village's own for its couple of guards.</summary>
    public Func<Vector3, bool>? Keeps { get; set; }

    /// <summary>One of a barbarian clan, raiding: every guard goes for it.</summary>
    public bool Raider { get; set; }

    /// <summary>
    /// A blow from one of the townsfolk (a stone, a bottle, a smith's hammer): a share of its health. It hurts, but it
    /// never finishes it: that's for the hero, or the Watch.
    /// </summary>
    public void Pelted(double share)
    {
        if (State == EnemyState.Dead) return;
        var blow = Math.Min(Stats.MaxHealth * share, Stats.Health - Stats.MaxHealth * TownDefence.FloorShare);
        if (blow <= 0) return;
        Stats.Lose(blow);
        Figure.Hurt();
        RefreshPlate();
    }

    /// <summary>Who it's fighting now, hero or creature.</summary>
    public Node3D? Foe3D => _target is not null ? _target : _rival is not null && IsInstanceValid(_rival) ? _rival : null;

    /// <summary>Sets a raid creature's numbers: how much it takes, how hard it hits.</summary>
    public void Tune(double health, double damage)
    {
        Stats.SetPools(health, Stats.MaxMana, Stats.MaxStamina);
        Stats.Heal(health);
        Stats.DamageMult = damage / Kind.Damage;
        RefreshPlate();
    }

    /// <summary>The Rift building up in a dragon wounded by corrupted ones (0 to 1; at 1 it turns).</summary>
    public double Infection { get; private set; }

    /// <summary>Seconds of madness left since it turned: it strikes at anything near.</summary>
    public double Frenzy { get; private set; }

    public bool Corrupted => Dragons.IsCorrupted(Kind.Id);

    /// <summary>A duel in a guild yard: whoever is beaten to the edge yields, and nobody dies.</summary>
    public bool Sparring { get; private set; }

    /// <summary>A duel ended: who, and whether the hero won it.</summary>
    public static event Action<Enemy, bool>? SparEnded;

    /// <summary>Makes this a duelling rival: their name, how much they can take, how hard they hit.</summary>
    public void Spar(string name, double health, double damage)
    {
        Sparring = true;
        Stats.SetPools(health, Stats.MaxMana, Stats.MaxStamina);
        Stats.Heal(health);
        Stats.DamageMult = damage / Kind.Damage;
        Stats.Name = name;
        RefreshPlate();
    }

    /// <summary>Lost or won a duel and stepped back: no longer a fight for anyone's records.</summary>
    public bool Yielded { get; private set; }

    void EndSpar(bool heroWon)
    {
        Sparring = false;
        Yielded = true;
        (_target, _rival, State, _ignore) = (null, null, EnemyState.Idle, 1e9);
        Stats.Heal(Stats.MaxHealth * 0.15);
        Figure.Perform(heroWon ? "Hit_B" : "Cheer", 1, 1);
        RefreshPlate();
        SparEnded?.Invoke(this, heroWon);
    }

    /// <summary>A dragon turned by the Rift.</summary>
    public static event Action<Enemy>? Turned;

    /// <summary>The hero whose companion this was, before the Rift took it.</summary>
    public Player? Master { get; private set; }

    /// <summary>Something died: who, the creature that killed it (if one did), and whether it was the company's doing.</summary>
    public static event Action<Enemy, Enemy?, bool>? Slain;

    /// <summary>A companion was knocked down.</summary>
    public static event Action<Enemy>? Felled;

    /// <summary>How much bigger than its kind's normal size it is drawn.</summary>
    float Bulk => Bond is not null ? (float)Bond.Size
        : (IsDragon ? Dragons.Scale(Size).Height / Dragons.Scale(DragonSize.Normal).Height : 1) * (Young ? (IsDragon ? 0.22f : 0.45f) : 1);

    /// <summary>A winged dragon: it can take to the air.</summary>
    public bool CanFly => Figure is Creature { Winged: true } && !Young && (Bond is null || Bond.Rideable);

    /// <summary>In the air (a dragon circling, or flying in to fight).</summary>
    public bool Airborne { get; private set; }

    float _altitude;
    double _flightClock = 10 + Random.Shared.NextDouble() * 20;

    Player? _target;
    Enemy? _rival;
    double _attackCooldown;
    double _wanderTimer;
    double _lookForRival;
    double _ignore;      // lost sight of the company in smoke
    double _empowered;   // a ranger's Call of the Wild
    double _revive;
    Vector3 _wanderTo;
    readonly Dictionary<string, double> _skillReady = [];
    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());

    /// <param name="young">A youngling of its kind.</param>
    /// <param name="look">How it looks, if not as its kind (a dragon with a knight on it).</param>
    /// <param name="bond">A companion: its numbers come from its level and growth.</param>
    public static Enemy Create(EnemyKind kind, Vector3 at, double danger = 1, string? siteId = null, DragonSize size = DragonSize.Normal,
        bool young = false, string? look = null, Bond? bond = null)
    {
        var (toughness, might, _) = Dragons.Scale(size);
        if (bond is not null) (toughness, might) = (bond.Strength * (bond.Size > 1 ? bond.Size * bond.Size : 1), bond.Strength * Math.Max(1, bond.Size));
        var e = new Enemy
        {
            Kind = kind, Home = at, Position = at, SiteId = siteId, Size = size, Young = young, Bond = bond,
            Rider = look?.StartsWith("rider:") == true,
            Stats = kind.NewCombatant(bond is null ? danger : 1, toughness, might),
        };
        if (young)
        {
            e.Stats.Grow(0.3);
            e.Stats.DamageMult *= 0.3;
            e.Stats.Name = Taming.YoungName(kind.Id, kind.Name);
        }
        if (bond is not null)
        {
            e.Stats.Name = bond.Name;
            e.Stats.Lose(e.Stats.MaxHealth * (1 - Math.Clamp(bond.Health, 0.05, 1)));
        }
        e.Dress(look ?? kind.Id, (float)Math.Max(kind.Height, 0.6) * (kind.Id == "frost_troll" ? 1.2f : 1),
            kind.Height < 1 ? 0.3f : kind.Id is "bear" or "frost_troll" ? 0.6f : kind.Height > 2.3 ? (float)kind.Height * 0.22f : 0.4f);
        e.Shape();
        if (size > DragonSize.Normal && bond is null) e.Stats.Name = $"{size} {kind.Name}";
        e.RefreshPlate();
        return e;
    }

    /// <summary>Fits the body and its collision to its size: a dragon's, a youngling's, a growing companion's.</summary>
    void Shape()
    {
        if (!IsDragon && !Young && Bond is null) return;
        var bulk = Bulk;
        Figure.Scale = Vector3.One * bulk;
        if (IsDragon)
        {
            var height = Dragons.Scale(DragonSize.Normal).Height * bulk;
            Resize(height * 0.55f, height * 0.22f);
        }
        else Resize((float)Math.Max(Kind.Height, 0.6) * bulk, (Kind.Height < 1 ? 0.3f : 0.4f) * Math.Max(0.5f, bulk));
    }

    /// <summary>
    /// A dragon deviant grows with every win: a size for each time it has beaten a party and each
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

    /// <summary>A companion grew a level: stronger, and maybe bigger.</summary>
    public void Grew()
    {
        if (Bond is null) return;
        var fresh = Kind.NewCombatant(1, Bond.Strength * (Bond.Size > 1 ? Bond.Size * Bond.Size : 1), Bond.Strength * Math.Max(1, Bond.Size));
        Stats.SetPools(fresh.MaxHealth, fresh.MaxMana, fresh.MaxStamina);
        Stats.DamageMult = fresh.DamageMult;
        Shape();
        RefreshPlate();
    }

    /// <summary>The server says this one is a deviant: rename, strengthen, and let it speak.</summary>
    public void MakeDeviant(Guid id, BattleModifiers mods, string? taunt)
    {
        DeviantId = id;
        Taunt = taunt;
        Stats.Apply(mods);
        if (IsDragon) Grow((DragonSize)Math.Min(3, Math.Max((int)mods.Rank, mods.Victories)));
        else Scale = new Vector3(1.15f, 1.15f, 1.15f);
        Figure.Mark(new Color("#ff5030"));
        RefreshPlate();
    }

    /// <summary>A lair boss that beat a company and took a name: it shows the name, stands taller and burns red.</summary>
    public bool Named { get; private set; }

    public void Crown(string title, Guid? deviantId = null)
    {
        Stats.Name = title;
        if (deviantId is not null) DeviantId = deviantId;
        if (!Named)
        {
            Named = true;
            Figure.Scale *= 1.15f;
            Figure.Mark(new Color("#ff5030"));
        }
        RefreshPlate();
    }

    /// <summary>Moves where it calls home (a roamer walking the wilds); it drifts there when it isn't fighting.</summary>
    public void Wander(Vector3 home) => Home = home;

    public void RefreshPlate()
    {
        var bar = new string('|', (int)Math.Ceiling(Stats.Health / Stats.MaxHealth * 20));
        Plate.Text = State == EnemyState.Dead ? (Bond is not null ? $"{Stats.Name} (down)" : "")
            : Bond is not null ? $"{Stats.Name}\nlevel {Bond.Level} {Bond.Stage}{(Bond.Hungry ? ", hungry" : "")}\n{bar}"
            : Rider ? $"Dragon Knight {Stats.Name}\n{bar}"
            : $"{Stats.Name}\n{bar}";
        if (State != EnemyState.Dead && Frenzy > 0) Plate.Text = $"FRENZIED\n{Plate.Text}";
        else if (State != EnemyState.Dead && Infection > 0.05 && !Corrupted) Plate.Text += $"\nRift {Infection:P0}";
        Plate.Modulate = Frenzy > 0 ? new Color("#d890ff") : Leader is not null ? new Color("#a8e0a0") : Friendly ? new Color("#a8c8f0") : DeviantId is null && !Named ? new Color("#e8dcc4") : new Color("#ff7a5a");
    }

    public void Provoke(Player by)
    {
        if (State is EnemyState.Dead or EnemyState.Fleeing || Friendly) return;
        if (Kind.Timid || Young) { _target = by; State = EnemyState.Fleeing; return; } // quarry and the young run
        _target = by;
        if (State != EnemyState.Fighting) Cry();
        State = EnemyState.Fighting;
    }

    double _criedAt = -10;

    /// <summary>
    /// The noise a creature makes coming for you, or falling: a dragon roars, a wolf howls, a spriggan or a hawk
    /// shrieks, the rest growl, big or small. People and the dead (figures) don't.
    /// </summary>
    void Cry(bool dying = false)
    {
        if (Figure is Figure || Friendly && !dying) return;
        var now = Time.GetTicksMsec() / 1000.0;
        if (!dying && now - _criedAt < 6) return;
        _criedAt = now;
        var name = IsDragon ? "creature/roar_dragon"
            : dying ? "creature/die"
            : Kind.Id.Contains("wolf") ? "creature/howl"
            : Kind.Id is "spriggan" or "hawk" ? "creature/shriek"
            : Kind.Height > 1.4 ? "creature/growl_big" : "creature/growl_small";
        var pitch = Young ? 1.5f : IsDragon ? (float)Math.Clamp(1.2 - Bulk * 0.12, 0.7, 1.1) * (dying ? 0.85f : 1) : 1;
        Sound.At(name, GlobalPosition + Vector3.Up, IsDragon ? 2 : -2, pitch, IsDragon ? 90 : 35);
    }

    /// <summary>A companion draws this foe off its hero and onto itself.</summary>
    public void Distract(Enemy by)
    {
        if (State == EnemyState.Dead) return;
        _target = null;
        _rival = by;
        State = EnemyState.Fighting;
    }

    /// <summary>Loses sight of the company for a while (a thief's smoke).</summary>
    public void LoseTrack(double seconds)
    {
        if (State == EnemyState.Dead || Friendly) return;
        _target = null;
        _rival = null;
        State = EnemyState.Idle;
        _ignore = seconds;
    }

    /// <summary>A ranger's call: healed, and fighting harder for a while.</summary>
    public void Empower(double seconds, double heal)
    {
        _empowered = seconds;
        Stats.Heal(Stats.MaxHealth * heal);
        Effects.Rise(this, DamageKind.Poison, 1.2f * Bulk);
        RefreshPlate();
    }

    /// <summary>Another creature attacks it (a dragon of a warring flight, a companion): it fights back.</summary>
    public void TakeHitFrom(Enemy attacker, double damage, bool crit, DamageKind? kind = null)
    {
        if (State == EnemyState.Dead) return;
        var result = Stats.TakeHit(damage, kind ?? attacker.Kind.DamageKind, AttackKind.Weapon, crit, attacker.Stats);
        if (_target is null && State != EnemyState.Fleeing) { _rival = attacker; State = EnemyState.Fighting; }
        // A corrupted dragon's wounds carry the Rift into another dragon; enough of them, and it turns.
        if (attacker.Corrupted && IsDragon && !Corrupted && !result.Killed)
        {
            Infection = Corruption.Infect(Infection, result.Dealt, Stats.MaxHealth);
            if (Corruption.Turns(Infection)) { Turn(); return; }
        }
        RefreshPlate();
        if (result.Killed) Die(attacker);
        else Figure.Hurt();
    }

    /// <summary>Who it should go after now, if anyone.</summary>
    Enemy? FindRival()
    {
        var all = GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e != this && e.State != EnemyState.Dead);
        if (Leader is { } leader)
        {
            // A companion fights what its hero is fighting: whatever is after the hero, what the hero last struck,
            // and whatever is attacking the hero's other companions.
            return all.Where(e => !e.Friendly && !e.Sparring && e.GlobalPosition.DistanceTo(leader.GlobalPosition) < 35
                    && (e.FightingCompany || e == leader.LastStruck || e._rival?.Leader == leader))
                .MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        }
        if (Watch) return all.Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < 70 && Disturbs(e)).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        var sight = IsDragon ? 120 : 60;
        // Mad with the Rift: anything at all, the nearest first.
        if (Frenzy > 0) return all.Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < 90).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        return all.Where(e => !e.Young && e.GlobalPosition.DistanceTo(GlobalPosition) < sight
                && ((Foe is not null && e.Faction == Foe) || Allegiance.Hostile(Side, e.Side) || (Hunter && e.Undead && !e.Named && e.Leader is null)))
            .MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
    }

    /// <summary>A deathcrest ape: it hunts the walking dead (and eats them).</summary>
    bool Hunter => Wildlife.HuntsUndead(Kind.Id);

    /// <summary>Picks fights with other creatures by itself: warring and hostile dragons, knights, companions, the apes that hunt the dead.</summary>
    bool SeeksFoes => !Young && (Watch || Foe is not null || Leader is not null || Hunter || Side is Side.Alliance or Side.Chromatic or Side.Corrupted or Side.Metallic);

    /// <summary>Takes another shape (an imp turning into a raven to get away).</summary>
    public void Reshape(string look) => Restyle(look);

    /// <summary>Runs from the hero, and settles wherever it gets clear.</summary>
    public void Flee(Player from) => (_target, _rival, State) = (from, null, EnemyState.Fleeing);

    /// <summary>Gone from sight (a dire imp turned invisible), or seen again.</summary>
    public void Unseen(bool unseen) => (Figure.Visible, Plate.Visible) = (!unseen, !unseen);

    public bool FightingCompany => State == EnemyState.Fighting && _target is not null;

    /// <summary>Who it's fighting, if a creature (for tests).</summary>
    public Enemy? Rival => _rival;

    public override void _PhysicsProcess(double delta)
    {
        if (State == EnemyState.Dead)
        {
            if (!IsOnFloor()) FallAndSlide(delta, Vector3.Zero); // a dragon killed in the air falls
            // A companion gets back up once the fighting's done.
            if (Bond is not null && Leader is { InFight: false } && (_revive -= delta) <= 0) Revive();
            return;
        }
        if (Expires < double.MaxValue && (Expires -= delta) <= 0)
        {
            Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, DamageKind.Shock);
            QueueFree();
            return;
        }
        Stats.Tick(delta, State == EnemyState.Fighting);
        _attackCooldown -= delta;
        _ignore -= delta;
        _empowered -= delta;
        if (Infection > 0 && !Corrupted && State != EnemyState.Fighting) Infection = Corruption.Ebb(Infection, delta); // once clear of the fight
        if (Frenzy > 0 && (Frenzy -= delta) <= 0) Calm();
        if (Carrying is not null) { Carry(delta); return; }
        if (State == EnemyState.Fighting) Think?.Invoke(this, delta);

        var bulk = Bulk;
        var speed = (float)(Kind.Speed * Stats.SpeedMult) * MathF.Pow(bulk, 0.35f);
        var reach = (float)Kind.Reach * MathF.Pow(bulk, 0.8f);
        var move = Vector3.Zero;

        // Wary creatures (metallic dragons) leave people alone unless attacked; friends and the young never start it.
        var player = _target ?? Player.Local;
        if (State == EnemyState.Idle && !Kind.Wary && !Friendly && !Young && _ignore <= 0 && player is not null && !player.Downed
            && player.GlobalPosition.DistanceTo(GlobalPosition) < (Kind.Timid ? 16 : 14) * MathF.Sqrt(bulk))
            Provoke(player);

        // Looking about for a fight: the enemy flight in a war, hostile dragons, the company's foes.
        if (SeeksFoes && _target is null && (_rival is null || !IsInstanceValid(_rival) || _rival.State == EnemyState.Dead) && (_lookForRival -= delta) <= 0)
        {
            _lookForRival = 0.5;
            _rival = FindRival();
            // In a frenzy it goes for whatever's nearest, people included.
            if (Frenzy > 0 && player is { Downed: false } && player.GlobalPosition.DistanceTo(GlobalPosition) is var pd and < 35
                && (_rival is null || pd < _rival.GlobalPosition.DistanceTo(GlobalPosition))) { _rival = null; Provoke(player); }
            if (_rival is not null && State != EnemyState.Fighting) { Cry(); State = EnemyState.Fighting; }
            else if (State == EnemyState.Fighting) State = EnemyState.Idle;
        }

        switch (State)
        {
            case EnemyState.Idle when Leader is not null:
                move = Follow(speed);
                break;

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
                // A companion doesn't chase a foe far from its hero; the Watch doesn't chase one out of town.
                if (Leader is not null && _rival.GlobalPosition.DistanceTo(Leader.GlobalPosition) > 45) { _rival = null; State = EnemyState.Idle; break; }
                if (Watch && !Disturbs(_rival)) { _rival = null; State = EnemyState.Idle; break; }
                var toRival = _rival.GlobalPosition - GlobalPosition;
                toRival.Y = 0;
                FaceAlong(toRival, delta * 1.5); // turns to face it, rather than snapping round
                if (Bond is not null) UseSkills(toRival.Length(), bulk);
                var gap = toRival.Length() - (reach + (float)_rival.Kind.Reach * MathF.Pow(_rival.Bulk, 0.8f) * 0.5f);
                if (gap > 0) move = toRival.Normalized() * speed;
                else if (_attackCooldown <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Frenzy > 0 ? 1.5 : 1);
                    var power = 1.0;
                    if (Bond?.Skills.FirstOrDefault(s => s.Id == "strike") is { } strike && Ready(strike)) power = strike.Power;
                    if (_empowered > 0) power *= 1.5;
                    if (Kind.Reach > 6) { Figure.Cast(); Effects.Bolt(GetParent(), GlobalPosition + Vector3.Up * 1.2f * bulk, _rival.GlobalPosition + Vector3.Up, Kind.DamageKind, 0.15f); }
                    else Figure.Attack();
                    var bite = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var bit) * power;
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
                if (Stats.Health < Stats.MaxHealth * 0.2 && Kind.Id is "giant_rat" or "lantern_boy" && DeviantId is null)
                {
                    State = EnemyState.Fleeing;
                    break;
                }
                var toTarget = _target.GlobalPosition - GlobalPosition;
                toTarget.Y = 0;
                FaceAlong(toTarget, delta * 1.5); // turns to face it, rather than snapping round
                if (toTarget.Length() > reach) move = toTarget.Normalized() * speed;
                else if (_attackCooldown <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Stats.Statuses.Contains(AutoStatus.Haste) ? 1.3 : 1) / (Frenzy > 0 ? 1.5 : 1);
                    Figure.Attack();
                    var damage = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var crit);
                    // In a duel, the hero yields rather than falls.
                    if (Sparring && _target.Stats.Health - damage <= _target.Stats.MaxHealth * 0.15) { EndSpar(false); break; }
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
        if (CanFly && Leader is null) move = Fly(move, speed, bulk, delta);
        if (State != EnemyState.Fighting || Airborne) FaceAlong(move, delta);
        if (Airborne || _altitude > 0) Hover(delta, move);
        else FallAndSlide(delta, move);
        Figure.Move(GroundSpeed, IsOnFloor() && !Airborne, Swimming);
    }

    /// <summary>Keeps up with its hero: trots after them, runs to catch up, and finds them again if left far behind.</summary>
    Vector3 Follow(float speed)
    {
        var leader = Leader!;
        var to = leader.GlobalPosition - GlobalPosition;
        to.Y = 0;
        var d = to.Length();
        if (d > 70)
        {
            var behind = leader.GlobalPosition + leader.GlobalTransform.Basis.Z * (4 + 3 * Bulk); // clear of the hero, however big
            GlobalPosition = behind with { Y = City.FloorAt(behind) + 0.5f };
            ResetPhysicsInterpolation();
            return Vector3.Zero;
        }
        if (d < 3.5f + Bulk) return Vector3.Zero;
        return to.Normalized() * Math.Max(speed, d > 12 ? 9 : 5);
    }

    bool Ready(BondSkill skill)
    {
        if (_skillReady.GetValueOrDefault(skill.Id) > Time.GetTicksMsec() / 1000.0) return false;
        _skillReady[skill.Id] = Time.GetTicksMsec() / 1000.0 + skill.Cooldown;
        return true;
    }

    /// <summary>A companion's moves in a fight: breath, guarding its hero, rallying them.</summary>
    void UseSkills(float distance, float bulk)
    {
        if (Leader is not { } leader || Bond is null) return;
        foreach (var skill in Bond.Skills)
            switch (skill.Id)
            {
                case "breath" when distance < 10 * Math.Max(1, bulk) && Ready(skill):
                    Breathe(skill.Power * Kind.Damage * Stats.DamageMult * (_empowered > 0 ? 1.5 : 1), 10 * Math.Max(1, bulk));
                    break;
                case "guard" when leader.Attackers().FirstOrDefault() is { } bully && Ready(skill):
                    bully.Distract(this);
                    break;
                case "rally" when leader.Stats.Health < leader.Stats.MaxHealth * 0.8 && Ready(skill):
                    leader.Stats.Heal(leader.Stats.MaxHealth * skill.Power);
                    Effects.Rise(leader, DamageKind.Sun);
                    break;
            }
    }

    /// <summary>A dragon's breath: its element poured over every foe in front of it.</summary>
    public void Breathe(double damage, float length)
    {
        var forward = -GlobalTransform.Basis.Z;
        var mouth = GlobalPosition + Vector3.Up * 2.2f * Bulk + forward * 2.5f * Bulk;
        Figure.Attack();
        Effects.Cone(GetParent(), mouth, forward with { Y = -0.15f }, Kind.DamageKind, length);
        foreach (var e in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e != this && e.State != EnemyState.Dead && !e.Friendly).ToList())
        {
            var to = e.GlobalPosition - GlobalPosition;
            to.Y = 0;
            if (to.Length() < length && forward.Dot(to.Normalized()) > 0.6f) e.TakeHitFrom(this, damage, false);
        }
    }

    // ---- carrying a rider ---------------------------------------------------------------------

    /// <summary>Takes the hero on its back.</summary>
    public void Bear(Player? rider)
    {
        Carrying = rider;
        _altitude = 0;
        _rival = null;
        State = EnemyState.Idle;
    }

    /// <summary>Where the rider sits, in the world.</summary>
    public Vector3 SaddleGlobal => Figure is Creature c ? c.ToGlobal(c.FindSaddle()) : GlobalPosition + Vector3.Up * 2;

    public bool Flying => Airborne;

    /// <summary>Carrying the hero: goes where they steer, climbs and dives on their word, flies if it can.</summary>
    void Carry(double delta)
    {
        var rider = Carrying!;
        var (steer, climb, fast) = rider.Reins;
        var d = (float)delta;
        if (CanFly)
        {
            if (climb > 0) _altitude = Math.Min(_altitude + 14 * d, 140);
            if (climb < 0) _altitude = Math.Max(0, _altitude - 18 * d);
        }
        var speed = (float)Kind.Speed * MathF.Pow(Bulk, 0.35f) * (fast ? 1.7f : 1.1f) * (Airborne ? 2.4f : 1);
        var move = steer * speed;
        FaceAlong(move, delta);
        if (Airborne || _altitude > 0) Hover(delta, move);
        else FallAndSlide(delta, move);
        Figure.Move(GroundSpeed, IsOnFloor() && !Airborne, Swimming);
    }

    /// <summary>
    /// Decides when a winged dragon takes to the air: now and then it rises to circle over its range and
    /// comes down again; it flies in to a fight that's far off and lands to fight it; it flees by air.
    /// Dragon knights keep to the sky, patrolling.
    /// </summary>
    Vector3 Fly(Vector3 move, float speed, float bulk, double delta)
    {
        var lift = MathF.Sqrt(bulk);
        Node3D? quarry = _target is not null ? _target : _rival is not null && IsInstanceValid(_rival) ? _rival : null;
        switch (State)
        {
            case EnemyState.Idle:
                if (Rider && _altitude <= 0) _altitude = (24 + (float)_rng.NextDouble() * 16) * lift;
                else if (!Rider && (_flightClock -= delta) <= 0)
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
                // Fly in, then fight: on the ground, or (two fliers) wing to wing.
                var bothFly = quarry is Enemy { Airborne: true };
                _altitude = away.Length() > 35 * lift ? 14 * lift : bothFly ? Math.Max(0, quarry.GlobalPosition.Y - City.FloorAt(quarry.GlobalPosition)) : 0;
                if (Airborne) return away.Length() > 3 * bulk ? away.Normalized() * speed * 1.8f : Vector3.Zero;
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
        Struck?.Invoke(result.Dealt, kind, attack, by);
        if (Sparring && (result.Killed || Stats.Health < Stats.MaxHealth * 0.15)) { EndSpar(true); return result; }
        Provoke(by);
        RefreshPlate();
        if (result.Killed) Die(null, byCompany: true);
        else Figure.Hurt();
        return result;
    }

    void Die(Enemy? killer = null, bool byCompany = false)
    {
        if (State != EnemyState.Dead) Cry(dying: true);
        State = EnemyState.Dead;
        _altitude = 0;
        Airborne = false;
        Carrying?.Dismount();
        Figure.Die();
        CollisionLayer = 0;
        Slain?.Invoke(this, killer, byCompany || killer?.Leader is not null);
        if (Bond is not null)
        {
            // A companion is knocked out, not killed (unless it's a dragon too young to fly: then the game decides).
            _revive = 20;
            RefreshPlate();
            Felled?.Invoke(this);
            return;
        }
        Plate.Text = "";
        var timer = GetTree().CreateTimer(20);
        timer.Timeout += QueueFree;
    }

    /// <summary>
    /// The Rift takes it: it becomes a corrupted dragon of its colour (tougher, its own colour bled violet and veined
    /// with gold), throws any rider, forgets every pact and master, and flies into a frenzy.
    /// </summary>
    public void Turn()
    {
        if (!IsDragon || Corrupted || State == EnemyState.Dead) return;
        var colour = Dragons.ColourOf(Kind.Id)!.Value;
        Carrying?.Dismount();
        if (Leader is not null) (Master, Leader) = (Leader, null);
        var named = Bond is not null || DeviantId is not null || Size == DragonSize.Ancient;
        Kind = Bestiary.Get(Dragons.CorruptedKindFor(colour));
        (Allied, Rider, Faction, Foe) = (false, false, Dragons.Rift, null);
        Stats.Grow(1.3);
        Stats.DamageMult *= 1.25 * Corruption.FrenzyMight;
        Stats.SpeedMult *= Corruption.FrenzySpeed;
        Stats.Statuses.Add(AutoStatus.Haste);
        Stats.Name = named ? $"{Stats.Name}, Rift-Mad" : Size > DragonSize.Normal ? $"{Size} {Kind.Name}" : Kind.Name;
        Infection = 1;
        Frenzy = Corruption.FrenzySeconds;
        (_rival, _target, State) = (null, null, EnemyState.Idle);
        Restyle(Dragons.CorruptedKindFor(colour));
        Shape();
        Effects.Burst(GetParent(), GlobalPosition + Vector3.Up * 2 * Bulk, DamageKind.Shock, 4 * Bulk);
        RefreshPlate();
        Turned?.Invoke(this);
    }

    /// <summary>The frenzy burns out; what's left is a corrupted dragon, cruel but no longer mad.</summary>
    void Calm()
    {
        Frenzy = 0;
        Stats.DamageMult /= Corruption.FrenzyMight;
        Stats.SpeedMult /= Corruption.FrenzySpeed;
        Stats.Statuses.Remove(AutoStatus.Haste);
        RefreshPlate();
    }

    /// <summary>Eases the Rift's hold on a dragon not yet turned (a paladin's light).</summary>
    public void Soothe(double amount)
    {
        if (Corrupted) return;
        Infection = Math.Max(0, Infection - amount);
        RefreshPlate();
    }

    /// <summary>
    /// A companion turned by the Rift, beaten down by its own hero: the Rift's hold breaks, and it is theirs again
    /// (it gets up once the fighting's done).
    /// </summary>
    public bool Cleanse()
    {
        if (Bond is null || !Corrupted) return false;
        if (Frenzy > 0) Calm();
        Kind = Bestiary.Get(Bond.KindId);
        (Faction, Infection, Leader) = (null, 0, Master);
        Restyle(Bond.KindId);
        Grew();
        Stats.Name = Bond.Name;
        if (State == EnemyState.Dead) Figure.Die();
        RefreshPlate();
        return true;
    }

    /// <summary>Falls where it stands (a companion starving).</summary>
    public void Collapse()
    {
        if (State != EnemyState.Dead) Die();
    }

    /// <summary>A knocked-out companion gets up again, sore.</summary>
    public void Revive()
    {
        if (State != EnemyState.Dead) return;
        State = EnemyState.Idle;
        CollisionLayer = 1;
        Stats.Heal(Stats.MaxHealth * 0.4);
        Figure.Rise();
        RefreshPlate();
    }
}
