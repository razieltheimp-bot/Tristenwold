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

    /// <summary>Puts another body on this one (a shapeshifter's beast), or its own back, keeping both.</summary>
    protected void Wear(Body body)
    {
        Figure.Visible = false;
        body.Visible = true;
        Figure = body;
    }
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

    /// <summary>Walks up a kerb, a step or a lip in the ground rather than stopping dead at it (the hero does).</summary>
    protected bool StepsUp { get; set; }

    /// <summary>The highest thing that's stepped onto rather than jumped: a doorstep, a kerb, a root, a seam in the land.</summary>
    public const float StepHeight = 0.45f;

    /// <summary>
    /// Blocked at the feet but clear a step up: lifts onto it. Tries the move from a step higher, and if that's clear,
    /// comes down onto whatever's there, so long as it's ground to stand on and not a wall's face.
    /// </summary>
    void StepUp(Vector3 ahead)
    {
        var here = GlobalTransform;
        if (!TestMove(here, ahead) || TestMove(here, Vector3.Up * StepHeight)) return;
        var up = here.Translated(Vector3.Up * StepHeight);
        if (TestMove(up, ahead)) return;
        var over = up.Translated(ahead);
        var landing = new KinematicCollision3D();
        if (!TestMove(over, Vector3.Down * (StepHeight + 0.05f), landing) || landing.GetNormal().Y < 0.7f) return;
        GlobalPosition = over.Origin + landing.GetTravel();
    }

    /// <summary>How quickly a body gets up to speed, and how quickly it stops (metres a second, each second):
    /// quick enough to feel sharp, slow enough that it has weight. Less in the air, and in water.</summary>
    const float Speeding = 28, Stopping = 40, AirControl = 0.3f;

    /// <summary>Seconds left of a leap up out of the water, when the water doesn't hold them back (see <see cref="Breach"/>).</summary>
    double _breaching;

    /// <summary>A leap up out of the water: for a moment the water lets go, so the leap carries them up and out.</summary>
    protected void Breach() => _breaching = 0.4;

    protected void FallAndSlide(double delta, Vector3 horizontal)
    {
        _breaching -= delta;
        // The land here isn't built yet (right after a long jump across the map, or a wake at home): stand on top of where
        // it will be until it is, rather than fall through it.
        if (City.GroundBuilt?.Invoke(GlobalPosition) == false && City.Below?.Invoke(GlobalPosition) is null && City.Afloat?.Invoke(GlobalPosition) is null)
        {
            var ground = City.FloorAt(GlobalPosition);
            if (GlobalPosition.Y < ground + 0.5f) { GlobalPosition = GlobalPosition with { Y = ground + 0.5f }; ResetPhysicsInterpolation(); }
            Velocity = Vector3.Zero;
            return;
        }
        var v = Velocity;
        var swimming = Depth > SwimDepth - 0.1f && _breaching <= 0;
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
        if (StepsUp && IsOnFloor() && !swimming && want.LengthSquared() > 0.25f) // where they're pushing, not how fast: a kerb has already stopped them
            StepUp(new Vector3(want.X, 0, want.Y).Normalized() * Math.Max(0.12f, want.Length() * (float)delta));
        MoveAndSlide();
        // Fell through land that wasn't built yet (after a long flight or a jump across the map), or was flung into
        // the sky by the physics for standing inside something: back on top of the land.
        var floor = City.FloorAt(GlobalPosition);
        var give = City.Afloat?.Invoke(GlobalPosition) is null ? 4 : 8; // a ship's decks go down a way below her waist deck
        if (GlobalPosition.Y < floor - give || (GlobalPosition.Y > floor + 30 && Velocity.Y > 0))
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

    /// <summary>What it carried off from the hero it struck down, when no shared world keeps it: bring it down to take it back.</summary>
    public Hoard Plunder { get; set; } = new();

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

    /// <summary>Empties a street: a raider, or anything that's come into town fighting (the hero, the guard, a rival crew).</summary>
    public bool Frightens => !Friendly && !Sparring && !Young && State != EnemyState.Dead
        && (Raider || State == EnemyState.Fighting && (_target is not null || _rival is not null) && SiteId is null && !InLair && Owner is null);

    /// <summary>One of the hero's own clan: it won't fight the company, and the company can't strike it (the guards still can).</summary>
    public bool Kin { get; set; }

    /// <summary>Seconds left pinned where it stands (an anchor shot, a sprung trap): it can still strike what's in reach.</summary>
    public double Rooted { get; set; }
    /// <summary>Seconds left hobbled (caltrops): a third of its pace.</summary>
    public double Hobbled { get; set; }
    /// <summary>Seconds left stunned (a wrench to the head): it neither moves nor strikes.</summary>
    public double Stunned { get; set; }

    /// <summary>Dragged to a spot (a harpoon), over a moment.</summary>
    public void Pull(Vector3 to)
    {
        if (State == EnemyState.Dead) return;
        to.Y = Math.Max(to.Y, City.FloorAt(to) + 0.2f);
        CreateTween().TweenProperty(this, "global_position", to, 0.3).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// A blow from one of the townsfolk (a stone, a bottle, a smith's hammer): a share of its health. It hurts, but it
    /// never finishes it: that's for the hero, or the Watch. A colony's settlers, defending their own, may <paramref name="finish"/> it.
    /// </summary>
    public void Pelted(double share, bool finish = false)
    {
        if (State == EnemyState.Dead) return;
        var blow = Math.Min(Stats.MaxHealth * share, Stats.Health - (finish ? 0 : Stats.MaxHealth * TownDefence.FloorShare));
        if (blow <= 0) return;
        Stats.Lose(blow);
        if (Stats.Dead) { Die(); return; }
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

    /// <summary>
    /// Another player on the server, as this game has them: an unseen stand-in where their hero is drawn (see
    /// <see cref="Visitors"/>), for this hero's blows to land on. It takes no harm here: every blow goes to the other
    /// player's own game (<see cref="HeroStruck"/>), which decides what it does to them.
    /// </summary>
    public string? Hero { get; private set; }

    /// <summary>A blow landed on another player's stand-in: how much, of what, and whether it was a critical.</summary>
    public static event Action<Enemy, double, DamageKind, AttackKind, bool>? HeroStruck;

    /// <summary>A stand-in for another player's hero: nothing drawn, nothing solid, no mind of its own.</summary>
    public static Enemy ForHero(string player, string name)
    {
        var e = Create(Bestiary.Get("bandit"), Vector3.Zero);
        e.Hero = player;
        e.Stats.Name = name;
        e.Figure.Visible = false;
        e.Plate.Visible = false;
        (e.CollisionLayer, e.CollisionMask) = (0, 0);
        return e;
    }

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
    /// <summary>An enemy's blow has landed: on another enemy or a companion, or (null) on the hero.</summary>
    public static event Action<Enemy, Enemy?>? Landed;

    /// <summary>How much bigger than its kind's normal size it is drawn.</summary>
    float Bulk => Bond is not null ? (float)Bond.Size
        : (IsDragon ? Dragons.Scale(Size).Height / Dragons.Scale(DragonSize.Normal).Height : 1) * (Young ? (IsDragon ? 0.22f : 0.45f) : 1);

    /// <summary>A winged dragon: it can take to the air.</summary>
    public bool CanFly => Figure is Creature { Winged: true } && !Young && (Bond is null || Bond.Rideable);

    /// <summary>In the air (a dragon circling, or flying in to fight).</summary>
    public bool Airborne { get; private set; }

    float _altitude;
    public float AltitudeForTest => _altitude;
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
        // The name and the bars float just over the figure's actual head (a golem built big, a dragon grown), and a
        // companion's show through its own body, so they're never lost behind it.
        var top = FigureTop();
        if (top > 0) Plate.Position = Plate.Position with { Y = Math.Max(Plate.Position.Y, top + 0.4f) };
        if (Bond is not null) { Plate.NoDepthTest = true; Plate.RenderPriority = 10; Plate.FontSize = 34; Plate.OutlineSize = 10; }
        _plateY = Plate.Position.Y;
    }

    float _plateY = -1;

    /// <summary>Where its plate floats, over its head: lifted when another's would be drawn over it (see Main.StackPlates).</summary>
    public Vector3 PlateAt => Plate.GlobalPosition;

    public void LiftPlate(float by)
    {
        if (_plateY < 0) _plateY = Plate.Position.Y;
        Plate.Position = Plate.Position with { Y = _plateY + by };
    }

    /// <summary>How high the figure's meshes reach, in this body's own space (their rest shapes, through every transform down to them).</summary>
    float FigureTop()
    {
        var top = 0f;
        foreach (var m in Figure.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (!m.Visible || m.Mesh is null) continue;
            var t = Transform3D.Identity;
            for (Node? n = m; n is Node3D n3 && n != this; n = n.GetParent()) t = n3.Transform * t;
            top = Math.Max(top, (t * m.GetAabb()).End.Y);
        }
        return top;
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
        _rank = mods.Rank;
        Taunt = taunt;
        Stats.Apply(mods);
        if (IsDragon) Grow((DragonSize)Math.Min(3, Math.Max((int)mods.Rank, mods.Victories)));
        else Scale = new Vector3(1.15f, 1.15f, 1.15f);
        if (!HeroesRival) Figure.Mark(new Color("#ff5030"));
        RefreshPlate();
    }

    /// <summary>The kind of the last blow that landed on it.</summary>
    public DamageKind LastBlow { get; private set; }
    /// <summary>A hero's own rival: a deviant kept in their save, so what happens to it isn't the shared world's to record.</summary>
    public bool HeroesRival { get; set; }

    /// <summary>Blown back from somewhere: a moment in the air, and it lands dazed.</summary>
    public void Thrown(Vector3 from, float metres)
    {
        var away = GlobalPosition - from;
        away = new Vector3(away.X, 0, away.Z);
        var to = GlobalPosition + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * metres;
        Stunned = Math.Max(Stunned, 1);
        CreateTween().TweenProperty(this, "global_position", to with { Y = City.FloorAt(to) + 0.2f }, 0.3).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }
    public void HidePlate() => Plate.Visible = false;

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
            if (!HeroesRival) Figure.Mark(new Color("#ff5030"));
        }
        RefreshPlate();
    }

    /// <summary>Moves where it calls home (a roamer walking the wilds); it drifts there when it isn't fighting.</summary>
    public void Wander(Vector3 home) => Home = home;

    public void RefreshPlate()
    {
        var bar = new string('|', (int)Math.Ceiling(Stats.Health / Stats.MaxHealth * 20));
        Plate.Text = State == EnemyState.Dead ? (Bond is not null ? $"{Stats.Name} (down)" : "")
            : Bond is not null ? $"{Stats.Name}, level {Bond.Level} {Bond.Stage}{(Bond.Hungry ? ", hungry" : "")}\n{bar}"
            : Rider ? $"Dragon Knight {Stats.Name}\n{bar}"
            : $"{Stats.Name}\n{bar}";
        if (State != EnemyState.Dead && Frenzy > 0) Plate.Text = $"FRENZIED\n{Plate.Text}";
        else if (State != EnemyState.Dead && Infection > 0.05 && !Corrupted) Plate.Text += $"\nRift {Infection:P0}";
        Plate.Modulate = Frenzy > 0 ? new Color("#d890ff") : Leader is not null ? new Color("#a8e0a0") : Friendly || Kin ? new Color("#a8c8f0") : DeviantId is null && !Named ? new Color("#e8dcc4") : new Color("#ff7a5a");
    }

    public void Provoke(Player by)
    {
        if (State is EnemyState.Dead or EnemyState.Fleeing || Friendly || Kin) return;
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
        if (Hero is not null) { HeroStruck?.Invoke(this, damage, kind ?? attacker.Kind.DamageKind, AttackKind.Weapon, crit); return; } // a hero's dragon, at another player
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
        // The guard comes to whoever's in trouble in its streets, from further off when it's the hero being set on.
        if (Watch) return all.Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < (e.FightingCompany || e.Raider ? 95 : 70) && Disturbs(e)).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        // A sea serpent or a leviathan: anything at all that's in the deep water with it, swimmer, beast or company.
        if (SeaHunter) return all.Where(e => !Wildlife.Aquatic(e.Kind.Id) && e.Hero is null && e.GlobalPosition.DistanceTo(GlobalPosition) < 60 && InTheSea(e.GlobalPosition)).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        var sight = IsDragon ? 120 : 60;
        // Mad with the Rift: anything at all, the nearest first.
        if (Frenzy > 0) return all.Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) < 90).MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
        return all.Where(e => !e.Young && e.GlobalPosition.DistanceTo(GlobalPosition) < sight
                && ((Foe is not null && e.Faction == Foe) || Allegiance.Hostile(Side, e.Side) || (Hunter && e.Undead && !e.Named && e.Leader is null)))
            .MinBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));
    }

    /// <summary>What lives in the sea and hunts in it: it doesn't care whether what's in its water is a ship, a swimmer or a beast.</summary>
    bool SeaHunter => Wildlife.Aquatic(Kind.Id) && !Friendly;

    /// <summary>Over water deep enough for a sea serpent to reach: swimming, or on a deck out on it.</summary>
    public static bool InTheSea(Vector3 at) => Tristinwold.World.WorldGen.WaterLevel - City.FloorAt(at) > 1.6f;

    /// <summary>A deathcrest ape: it hunts the walking dead (and eats them).</summary>
    bool Hunter => Wildlife.HuntsUndead(Kind.Id);

    /// <summary>Picks fights with other creatures by itself: warring and hostile dragons, knights, companions, the apes that hunt the dead.</summary>
    bool SeeksFoes => !Young && (Watch || Foe is not null || Leader is not null || Hunter || SeaHunter || Side is Side.Alliance or Side.Chromatic or Side.Corrupted or Side.Metallic);

    /// <summary>Takes another shape (an imp turning into a raven to get away).</summary>
    public void Reshape(string look) => Restyle(look);

    /// <summary>Runs from the hero, and settles wherever it gets clear.</summary>
    public void Flee(Player from) => (_target, _rival, State) = (from, null, EnemyState.Fleeing);

    /// <summary>Gone from sight (a dire imp turned invisible), or seen again.</summary>
    public void Unseen(bool unseen) => (Figure.Visible, Plate.Visible) = (!unseen, !unseen);

    public bool FightingCompany => State == EnemyState.Fighting && _target is not null;

    /// <summary>Who it's fighting, if a creature (for tests).</summary>
    public Enemy? Rival => _rival;

    /// <summary>Something else is moving it (a shapeshifter on the wing): it goes where it's put, and the ground has no say.</summary>
    public bool Carried { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        if (Hero is not null) return; // another player's hero: their own game moves them
        if (Carried && State != EnemyState.Dead) return;
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
        if (_runningOff > 0 && (_runningOff -= delta) <= 0)
        {
            RanOff?.Invoke(this, true);
            QueueFree();
            return;
        }
        Stats.Tick(delta, State == EnemyState.Fighting);
        _attackCooldown -= delta;
        _ignore -= delta;
        Rooted -= delta;
        Hobbled -= delta;
        Stunned -= delta;
        _empowered -= delta;
        if (Infection > 0 && !Corrupted && State != EnemyState.Fighting) Infection = Corruption.Ebb(Infection, delta); // once clear of the fight
        if (Frenzy > 0 && (Frenzy -= delta) <= 0) Calm();
        if (Carrying is not null) { Carry(delta); return; }
        if (State == EnemyState.Fighting) Think?.Invoke(this, delta);

        var bulk = Bulk;
        var speed = (float)(Kind.Speed * Stats.SpeedMult) * MathF.Pow(bulk, 0.35f) * (Hobbled > 0 ? 0.35f : 1);
        var reach = (float)Kind.Reach * MathF.Pow(bulk, 0.8f);
        var move = Vector3.Zero;

        // Wary creatures (metallic dragons) leave people alone unless attacked; friends and the young never start it.
        var player = _target ?? Player.Local;
        if (State == EnemyState.Idle && !Kind.Wary && !Friendly && !Kin && !Young && _ignore <= 0 && player is not null && !player.Downed && !player.Hidden
            && (!SeaHunter || InTheSea(player.GlobalPosition))
            && player.GlobalPosition.DistanceTo(GlobalPosition) < (SeaHunter ? 30 : Kind.Timid ? 16 : 14) * MathF.Sqrt(bulk))
            Provoke(player);
        // A sea serpent hunts whatever's in its water, and lets go of whatever gets out of it.
        if (SeaHunter && _target is not null && !InTheSea(_target.GlobalPosition)) { _target = null; State = EnemyState.Idle; }

        // Looking about for a fight: the enemy flight in a war, hostile dragons, the company's foes.
        if (SeeksFoes && _target is null && (_rival is null || !IsInstanceValid(_rival) || _rival.State == EnemyState.Dead) && (_lookForRival -= delta) <= 0)
        {
            _lookForRival = 0.5;
            _rival = FindRival();
            // In a frenzy it goes for whatever's nearest, people included.
            if (Frenzy > 0 && player is { Downed: false, Safe: false } && player.GlobalPosition.DistanceTo(GlobalPosition) is var pd and < 35
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
                    var (wx, wz) = Northwood.OutOfTrunks(_wanderTo.X, _wanderTo.Z); // never into one of Hollowheart's great trees
                    _wanderTo = _wanderTo with { X = (float)wx, Z = (float)wz };
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
                else if (_attackCooldown <= 0 && Stunned <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Frenzy > 0 ? 1.5 : 1);
                    var power = 1.0;
                    if (Bond?.Skills.FirstOrDefault(s => s.Id == "strike") is { } strike && Ready(strike)) power = strike.Power;
                    if (_empowered > 0) power *= 1.5;
                    if (Kind.Reach > 6) { Figure.Cast(); Effects.Bolt(GetParent(), GlobalPosition + Vector3.Up * 1.2f * bulk, _rival.GlobalPosition + Vector3.Up, Kind.DamageKind, 0.15f); }
                    else Figure.Attack();
                    var bite = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var bit) * power;
                    _rival.TakeHitFrom(this, bite, bit);
                    Landed?.Invoke(this, _rival);
                }
                break;

            case EnemyState.Fighting when _target is not null:
                if (_target.Downed || _target.Safe || _target.Hidden || _target.GlobalPosition.DistanceTo(Home) > Math.Max(IsDragon ? 250 : 60, Roam * 3))
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
                else if (_attackCooldown <= 0 && Stunned <= 0)
                {
                    _attackCooldown = Kind.AttackSeconds / (Stats.Statuses.Contains(AutoStatus.Haste) ? 1.3 : 1) / (Frenzy > 0 ? 1.5 : 1);
                    Figure.Attack();
                    var damage = Stats.Outgoing(AttackKind.Weapon, Kind.Damage, _rng, out var crit);
                    // In a duel, the hero yields rather than falls.
                    if (Sparring && _target.Stats.Health - damage <= _target.Stats.MaxHealth * 0.15) { EndSpar(false); break; }
                    _target.TakeHit(damage, Kind.DamageKind, AttackKind.Weapon, crit, this);
                    Landed?.Invoke(this, null);
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
        if (Rooted > 0 || Stunned > 0) move = Vector3.Zero; // pinned, or seeing stars
        // A sea serpent keeps to deep water: it won't come up the beach after you.
        if (move != Vector3.Zero && Tristinwold.World.Wildlife.Aquatic(Kind.Id) && Tristinwold.World.WorldGen.WaterLevel - City.FloorAt(GlobalPosition + move.Normalized() * 4) < 2.5f) move = Vector3.Zero;
        // And nothing of the land swims out after you: a bear or a wolf waits on the shore while you're in deep water or
        // aboard your ship. (Anything already in the water can still make for land.) A bandit or a deviant waits a
        // moment, then hires a boat or steals one, and rows out after you.
        if (_boat is null && move != Vector3.Zero && !Friendly && Hero is null && !CanFly && !Tristinwold.World.Wildlife.Aquatic(Kind.Id)
            && Tristinwold.World.WorldGen.WaterLevel - City.FloorAt(GlobalPosition + move.Normalized() * 1.5f) is var ahead && ahead > SwimDepth - 0.2f && ahead > Depth)
        {
            if (Boatman && State == EnemyState.Fighting && _target is not null && (_shoreWait += delta) > ShoreWait) TakeBoat();
            else move = Vector3.Zero;
        }
        else if (_boat is null) _shoreWait = 0;
        if (CanFly && Leader is null) move = Fly(move, speed, bulk, delta);
        if (State != EnemyState.Fighting || Airborne) FaceAlong(move, delta);
        if (_boat is not null) Row(delta, move);
        else if (Airborne || _altitude > 0) Hover(delta, move);
        else FallAndSlide(delta, move);
        Figure.Move(_boat is null ? GroundSpeed : 0, (IsOnFloor() || _boat is not null) && !Airborne, Swimming && _boat is null);
    }

    // ---- boats: what bandits and deviants take to the water in ------------------------------------------------------

    Node3D? _boat;
    double _shoreWait, _afloat;
    /// <summary>How long one waits on the shore, held up by the water, before going to find a boat.</summary>
    public const double ShoreWait = 3;

    /// <summary>Hired a boat (true) or stole one, to row out after someone.</summary>
    public static event Action<Enemy, bool>? TookBoat;

    /// <summary>In a boat, rowing.</summary>
    public bool Afloat => _boat is not null;

    /// <summary>Someone with hands and a purse (or no scruples): a person, not a beast, and not a child.</summary>
    bool Boatman => Figure is Figure && !Young && Bond is null;

    /// <summary>A rowing boat, hired off the shore or cut loose from it, and out onto the water after their quarry.</summary>
    void TakeBoat()
    {
        var wood = BuildingMaterials.For(Surface.Timber);
        _boat = new Node3D { Name = "Boat" };
        _boat.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.15f, 0.42f, 2.9f) }, MaterialOverride = wood, Position = new Vector3(0, -0.12f, 0) });
        foreach (var side in new[] { -1f, 1f })
        {
            _boat.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.2f, 3.0f) }, MaterialOverride = wood, Position = new Vector3(side * 0.6f, 0.12f, 0) }); // gunwales
            _boat.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.05f, 0.1f) }, MaterialOverride = wood, Position = new Vector3(side * 0.9f, 0.15f, 0.2f), Rotation = new Vector3(0, 0, side * 0.35f) }); // oars
        }
        _boat.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(1.15f, 0.42f, 0.6f) }, MaterialOverride = wood, Position = new Vector3(0, -0.12f, -1.7f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) }); // the bow
        AddChild(_boat);
        _afloat = 0;
        _shoreWait = 0;
        Roam = Math.Max(Roam, 50); // a boat takes them further from where they started
        TookBoat?.Invoke(this, _rng.Chance(0.5));
    }

    /// <summary>Rowing: on the surface, where they're steering; and run up on the shore, they climb out and leave the boat there.</summary>
    void Row(double delta, Vector3 move)
    {
        _afloat += delta;
        var v = move * 0.85f;
        v.Y = ((float)Tristinwold.World.WorldGen.WaterLevel + 0.15f - GlobalPosition.Y) * 4f;
        Velocity = v;
        MoveAndSlide();
        _boat!.Rotation = new Vector3(Mathf.Sin((float)_afloat * 1.7f) * 0.04f, 0, Mathf.Sin((float)_afloat * 1.3f) * 0.05f);
        if (_afloat > 2.5 && Tristinwold.World.WorldGen.WaterLevel - City.FloorAt(GlobalPosition) < 0.7f) LeaveBoat();
    }

    /// <summary>Out of the boat onto the shore: it's left lying there a while.</summary>
    void LeaveBoat()
    {
        if (_boat is not { } boat) return;
        _boat = null;
        var at = boat.GlobalTransform;
        boat.Reparent(GetParent());
        boat.GlobalTransform = at;
        GetTree().CreateTimer(120).Timeout += () => { if (IsInstanceValid(boat)) boat.QueueFree(); };
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
        // Underground the tunnels wind, and walking straight at the hero means walking into rock: keep up by the way they went.
        if (d > 14 && GlobalPosition.Y > Indoors.Threshold) { CatchUp(leader); return Vector3.Zero; }
        // Each to their own place behind the hero, so the company's spread out enough to see every one of them.
        var toSlot = FollowSlot(leader) - GlobalPosition;
        toSlot.Y = 0;
        var gap = toSlot.Length();
        if (gap < 0.9f || d < 2.2f + Bulk && gap < 3) return Vector3.Zero;
        return toSlot.Normalized() * (gap > 12 ? Math.Max(speed, 9) : gap > 4 ? Math.Max(speed, 5) : Math.Max(1.5f, speed * gap / 4));
    }

    double _slotAge = 9;
    int _slot;
    float _rowGap = 3;

    /// <summary>
    /// Where in the company it walks: rows of three behind the hero (the middle, then left, then right), each row further
    /// back, spaced by the biggest of them, so a golem or a dragon doesn't stand on anyone. Worked out now and then, not
    /// every step.
    /// </summary>
    Vector3 FollowSlot(Player leader)
    {
        if ((_slotAge += GetPhysicsProcessDeltaTime()) > 0.6)
        {
            _slotAge = 0;
            var mates = leader.Companions().Where(c => !c.Carried && c != leader.Mount).OrderBy(c => c.GetInstanceId()).ToList();
            _slot = Math.Max(0, mates.IndexOf(this));
            _rowGap = 2.4f + mates.Select(c => c.Bulk).DefaultIfEmpty(1).Max() * 1.5f;
        }
        var row = _slot / 3;
        var side = (_slot % 3) switch { 0 => 0, 1 => -1, _ => 1 };
        var behind = (leader.GlobalTransform.Basis.Z with { Y = 0 }).Normalized();
        var across = (leader.GlobalTransform.Basis.X with { Y = 0 }).Normalized();
        return leader.GlobalPosition + behind * (2.5f + Bulk + row * _rowGap) + across * (side * _rowGap);
    }

    /// <summary>Close behind the hero, wherever there's room: behind them if that's clear of rock, else at their side.</summary>
    void CatchUp(Player leader)
    {
        var from = leader.GlobalPosition + Vector3.Up * 0.8f;
        var space = GetWorld3D().DirectSpaceState;
        foreach (var dir in new[] { leader.GlobalTransform.Basis.Z, leader.GlobalTransform.Basis.X, -leader.GlobalTransform.Basis.X, -leader.GlobalTransform.Basis.Z })
        {
            var to = from + (dir with { Y = 0 }).Normalized() * (2.2f + Bulk);
            var ray = PhysicsRayQueryParameters3D.Create(from, to);
            ray.Exclude = [leader.GetRid(), GetRid()];
            if (space.IntersectRay(ray).Count > 0) continue;
            GlobalPosition = to with { Y = leader.GlobalPosition.Y + 0.3f };
            ResetPhysicsInterpolation();
            return;
        }
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
            if (to.Length() < length && forward.Dot(to.Normalized()) > 0.6f) { e.TakeHitFrom(this, damage, false); Landed?.Invoke(this, e); }
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
        // A mount that can't fly jumps, as high as its speed and its growth carry it.
        if (!CanFly && climb > 0 && IsOnFloor()) Velocity = Velocity with { Y = (float)Taming.MountJump(Kind.Speed * Stats.SpeedMult, Bond?.Level ?? 1) };
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

    /// <summary>How often it sees a blow coming and isn't there (Soren and the Web).</summary>
    public double Foresees { get; set; }

    /// <summary>A flying ship's hull: put where the ship is, and it counts as in the air, so what's fighting it goes up to it.</summary>
    public void HoldAloft() => Airborne = true;

    /// <summary>Hit by a shell from a long way off: it hurts, but it doesn't tell the thing who fired.</summary>
    public HitResult Shelled(double damage, bool byHero)
    {
        if (State == EnemyState.Dead) return default;
        LastBlow = DamageKind.Fire;
        var result = Stats.TakeHit(damage, DamageKind.Fire, AttackKind.Weapon);
        RefreshPlate();
        if (result.Killed) Die(null, byHero);
        else Figure.Hurt();
        return result;
    }

    public HitResult TakeHit(double damage, DamageKind kind, AttackKind attack, bool crit, Player by)
    {
        if (Hero is not null) { HeroStruck?.Invoke(this, damage, kind, attack, crit); return new HitResult(damage, 0, 0, false, crit, false); }
        if (Foresees > 0 && GD.Randf() < Foresees) { Provoke(by); return new HitResult(0, 0, 0, false, crit, false); }
        LastBlow = kind;
        var result = Stats.TakeHit(damage, kind, attack, crit, by.Stats);
        Struck?.Invoke(result.Dealt, kind, attack, by);
        if (Sparring && (result.Killed || Stats.Health < Stats.MaxHealth * 0.15)) { EndSpar(true); return result; }
        Provoke(by);
        RefreshPlate();
        if (result.Killed) Die(null, byCompany: true);
        else Figure.Hurt();
        return result;
    }

    Tristinwold.Deviant.Rank _rank = Tristinwold.Deviant.Rank.Ringleader;
    double _runningOff;

    /// <summary>Told when a deviant cut down gets up and runs for it (false), and when it's got clear (true).</summary>
    public static event Action<Enemy, bool>? RanOff;
    /// <summary>The self-test fixes the odds of a deviant beating death.</summary>
    public static double? DeathOdds { get; set; }

    /// <summary>How long a deviant running from death takes to get clear. Catch it first, and it falls for good.</summary>
    const double RunOffSeconds = 8;

    /// <summary>A deviant's one chance: instead of falling it gets up, badly hurt, and runs. Named bosses get it; the incomplete immortals have their own ways of coming back.</summary>
    bool BeatsDeath()
    {
        if (_runningOff > 0 || State == EnemyState.Dead || Bond is not null || Sparring || HeroesRival || Friendly || (DeviantId is null && !Named)) return false;
        if (GD.Randf() >= (DeathOdds ?? Tristinwold.Deviant.BeatingDeath.Chance(_rank))) return false;
        _runningOff = RunOffSeconds;
        Stats.Heal(Stats.MaxHealth * 0.1);
        Carrying?.Dismount();
        (Rooted, Hobbled, Stunned, Think) = (0, 0, 0, null);
        (_target, _rival, State) = (Player.Local, null, EnemyState.Fleeing);
        RefreshPlate();
        RanOff?.Invoke(this, false);
        return true;
    }

    void Die(Enemy? killer = null, bool byCompany = false)
    {
        if (BeatsDeath()) return;
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

    /// <summary>A blighted companion fell, and the rot got up instead: it turns on its hero.</summary>
    public void Rot(Player hero)
    {
        if (Bond is null || Leader is null) return;
        (Master, Leader) = (Leader, null);
        Bond.Rotted = true;
        Revive();
        Stats.Name = $"{Bond.Name}, Rotten";
        RefreshPlate();
        Provoke(hero);
    }

    /// <summary>Beaten down by its hero: subdued, and it'll follow them to a druid.</summary>
    public void Subdue()
    {
        if (Bond is null || Master is null) return;
        Leader = Master;
        Stats.Name = $"{Bond.Name} (bound)";
        RefreshPlate();
    }

    /// <summary>A druid drew the rot out.</summary>
    public void Cured()
    {
        if (Bond is null) return;
        (Bond.Blighted, Bond.Rotted) = (false, false);
        Stats.Name = Bond.Name;
        RefreshPlate();
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
