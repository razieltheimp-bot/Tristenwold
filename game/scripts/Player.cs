using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.Game;

/// <summary>
/// The player: third-person, fighting the way their class fights (see <see cref="Classes"/>). Left button: the
/// class's main attack. Right button: its signature move. Q: its third move (a mage's is switching element).
/// R: its limit break, once the gauge is full. V: mount or dismount your dragon; in the saddle, Space climbs,
/// C dives and the attack button is your dragon's breath.
/// </summary>
public partial class Player : Fighter
{
    public static Player? Local { get; private set; }

    public string Company { get; set; } = "Adventurer";
    public bool Downed { get; private set; }
    public DamageKind Element { get; set; } = DamageKind.Fire;
    public int Septims { get; set; }

    /// <summary>Meat in the packs, for feeding beasts and dragons (and tempting a youngling).</summary>
    public int Meat { get; set; }

    /// <summary>Their look, chosen when the character was made (a class, or an older character's look).</summary>
    public string Outfit { get; init; } = "Paladin";

    /// <summary>How the hero looks, picked on the character page (see <see cref="Gameplay.Avatar"/>); null for the class's own look.</summary>
    public string? Avatar { get; init; }

    Gameplay.Avatar? _look;
    bool _looked;

    /// <summary>Their look as picked on the character page, or null for their class's own.</summary>
    public Gameplay.Avatar? Appearance
    {
        get
        {
            if (!_looked) (_look, _looked) = (Gameplay.Avatar.Parse(Avatar, Class.Id), true);
            return _look;
        }
    }

    /// <summary>Their people, and any dragon in their blood.</summary>
    public Race Race => Appearance?.People ?? Races.Get("human");
    public DragonBlood? Blood => Appearance?.Blood;

    /// <summary>What shops ask of them, as a share of the usual price.</summary>
    public double Markup => Races.Prices(Race);

    Body MakeFigure() => Tristinwold.Game.Figure.Create(Class.Look, Gameplay.Avatar.Parse(Avatar, Class.Id));

    /// <summary>Their rank in their guild ("Adept of the Iron Circle").</summary>
    public string GuildTitle { get; set; } = "";

    /// <summary>Their class, level and skill ranks.</summary>
    public Hero Hero { get; set; } = new();
    public Discipline Class => Classes.Get(Hero.Discipline);

    public event Action<string>? Said;
    public event Action? WentDown;
    public event Action<Enemy, HitResult, DamageKind>? Struck;

    /// <summary>A warlock calls a familiar: the game brings it.</summary>
    public event Action<double>? Summoned;

    const float WalkSpeed = 5, SprintSpeed = 9, JumpSpeed = 5;

    readonly Rng _rng = new((ulong)Random.Shared.NextInt64());
    readonly Dictionary<string, double> _ready = [];
    Node3D _pivot = new();
    SpringArm3D _arm = new();
    double _actingUntil, _downTimer, _rage, _poison;
    bool _smoke;
    int _combo, _notes;
    double _stride, _airborne, _wingbeat;
    Enemy? _noted;

    public Camera3D Camera { get; private set; } = null!;

    /// <summary>The foe this hero struck last: companions join in on it.</summary>
    public Enemy? LastStruck { get; private set; }

    /// <summary>The dragon being ridden.</summary>
    public Enemy? Mount { get; private set; }

    /// <summary>Where the rider steers: a direction, climbing or diving, and whether to hurry.</summary>
    public (Vector3 Steer, int Climb, bool Fast) Reins { get; private set; }

    double Now => Time.GetTicksMsec() / 1000.0;

    public override void _Ready()
    {
        Local = this;
        if (Hero.Discipline is null or "") Hero.Discipline = Classes.For(Outfit).Id;
        Stats = new Combatant(Company, 1);
        var size = Appearance?.Size ?? 1;
        Dress(Class.Look, 1.8f * size, make: Avatar is null ? null : MakeFigure);
        Fit();
        Stats.Heal(1e9);
        Stats.Restore(1e9, 1e9);
        Plate.Text = "";
        _pivot = new Node3D { Position = new Vector3(0, 1.6f * size, 0) };
        AddChild(_pivot);
        _arm = new SpringArm3D { SpringLength = 5 * Math.Max(1, size), Position = new Vector3(0.6f, 0, 0) };
        _pivot.AddChild(_arm);
        _arm.AddChild(Camera = new Camera3D { Current = true, Far = 1500 });
        if (DisplayServer.GetName() != "headless") Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>Sets health, magicka, stamina and the rest from the class, its passive, and the hero's level.</summary>
    public void Fit()
    {
        var c = Class;
        var (h, m, s) = Hero.Growth;
        var passive = Hero.Power(c.Passive.Id);
        var race = Race;
        var health = (c.Health + h) * (c.Id == "Barbarian" ? 1.2 * passive : 1) * race.Health;
        var magicka = (c.Magicka + m) * (c.Id == "Wizard" ? 1 + 0.25 * passive : 1) * race.Magicka;
        Stats.SetPools(health, magicka, (c.Stamina + s) * race.Stamina);
        Stats.Armor = c.Armor + (Hero.Level - 1) * 3;
        Stats.SpellPower = (1 + (Hero.Level - 1) * 0.04 + (c.Id == "Wizard" ? c.Passive.Power * passive : 0)) * (c.Id == "Poet" ? 1 + c.Passive.Power * passive : 1);
        Stats.DamageMult = 1 + (Hero.Level - 1) * 0.04;
        Stats.CritChance = c.Id == "Assassin" ? 25 + 5 * (Hero.Rank("backstab") - 1) : c.Calling == Calling.Rogue ? 10 : 5;
        Stats.SpeedMult = c.Id == "MartialArtist" ? 1.15 : 1;
        Stats.Name = Company;
        Races.Apply(Stats, race, Blood, Appearance?.PureDragon == true); // their people's gifts and costs, and a dragonkind's blood
        // What they wear, and what they've drunk.
        Stats.Armor += Pack.Armor + (_wardLeft > 0 ? _ward : 0);
        var might = (1 + Pack.Damage) * (_mightLeft > 0 ? 1 + _might : 1);
        Stats.DamageMult *= might;
        Stats.SpellPower *= might;
    }

    /// <summary>What the hero carries and wears (the game hands it over on loading).</summary>
    public Pack Pack { get; set; } = new();

    double _might, _mightLeft, _ward, _wardLeft;

    /// <summary>Puts on what's worn: the class's own look, then the gear over it, and the numbers to match.</summary>
    public void Dress()
    {
        Restyle(Class.Look, Avatar is null ? null : MakeFigure);
        if (Figure is Figure f) Wardrobe.Wear(f, Pack);
        Fit();
    }

    /// <summary>Eats or drinks something: health and the rest back, and a while of might or a ward.</summary>
    public void Consume(Effect e)
    {
        Stats.Heal(Stats.MaxHealth * e.Health);
        Stats.Restore(Stats.MaxMagicka * e.Magicka, Stats.MaxStamina * e.Stamina);
        if (e.Might > 0) (_might, _mightLeft) = (e.Might, e.Seconds);
        if (e.Ward > 0) (_ward, _wardLeft) = (e.Ward, e.Seconds);
        if (e.Might > 0 || e.Ward > 0) Fit();
    }

    /// <summary>A potion's might or ward still working, and for how long.</summary>
    public string? Buffs => _mightLeft > 0 || _wardLeft > 0
        ? string.Join(", ", new[] { _mightLeft > 0 ? $"might {_mightLeft:0}s" : null, _wardLeft > 0 ? $"ward {_wardLeft:0}s" : null }.Where(x => x is not null))
        : null;

    /// <summary>Takes up a saved hero: their class (and its look), level and ranks.</summary>
    public void Become(Hero hero)
    {
        var look = Class.Look;
        Hero = hero;
        Dress();
        Stats.Heal(1e9);
        Stats.Restore(1e9, 1e9);
    }

    /// <summary>Joins another class: a new look, new moves, the same hero.</summary>
    public void Join(string discipline)
    {
        Hero.Join(discipline);
        Dress();
        Stats.Heal(1e9);
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
        if (Downed)
        {
            _downTimer -= delta;
            if (_downTimer <= 0) Rise();
            FallAndSlide(delta, Vector3.Zero);
            return;
        }
        Stats.Tick(delta, InFight);
        _rage -= delta;
        if (_mightLeft > 0 && (_mightLeft -= delta) <= 0) Fit();
        if (_wardLeft > 0 && (_wardLeft -= delta) <= 0) Fit();
        _poison -= delta;
        // Passives that work over time: a barbarian's hide closes wounds; a sorcerer's well refills fast.
        if (Class.Id == "Barbarian" && InFight) Stats.Heal(Stats.MaxHealth * Class.Passive.Power * Hero.Power("thick_hide") * delta);
        if (Class.Id == "Sorcerer") Stats.Restore(Stats.MaxMagicka * 0.02 * delta);
        if (Class.Id == "Musician") Stats.Restore(Stats.MaxMagicka * Class.Passive.Power * Hero.Power("perfect_pitch") * delta, Stats.MaxStamina * Class.Passive.Power * Hero.Power("perfect_pitch") * delta);
        if (Frozen)
        {
            if (Mount is null) FallAndSlide(delta, Vector3.Zero);
            Figure.Move(0, IsOnFloor(), Swimming);
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var sprint = Input.IsActionPressed("sprint") && (Stats.Stamina > 1 || Class.Id == "Thief");
        var dir = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        if (Mount is not null)
        {
            Ride(dir, sprint);
        }
        else
        {
            var speed = (sprint ? SprintSpeed : WalkSpeed) * (float)Stats.SpeedMult;
            if (sprint && input != Vector2.Zero && Class.Id != "Thief") Stats.Spend(AttackKind.Weapon, 15 * delta);
            if (Input.IsActionJustPressed("jump") && (IsOnFloor() || Swimming))
            {
                Velocity = Velocity with { Y = JumpSpeed };
                if (!Swimming) { Figure.Perform("Jump_Start", 0.22, 1.5f); Sound.At("move/jump", GlobalPosition, -6); } // a crouch and a spring
            }
            FallAndSlide(delta, dir * speed);
            Steps(delta);

            // The body turns to run the way you're going; it faces forward to strike or cast.
            var acting = Now < _actingUntil;
            var target = acting ? 0 : input == Vector2.Zero ? Figure.Rotation.Y : Mathf.Atan2(-input.X, -input.Y); // -Z is forward
            var before = Figure.Rotation.Y;
            var yaw = Mathf.LerpAngle(before, target, (float)Math.Min(1, delta * 12));
            // Leaning into the turn and forward into a sprint, more the faster you go, and easing back upright.
            var pace = Mathf.Clamp(GroundSpeed / SprintSpeed, 0, 1);
            var turning = Mathf.AngleDifference(before, yaw) / (float)Math.Max(delta, 1e-4);
            var lean = IsOnFloor() ? Mathf.Clamp(turning * 0.035f * pace, -0.22f, 0.22f) : 0;
            var stoop = IsOnFloor() && sprint ? -0.12f * pace : 0;
            var ease = (float)Math.Min(1, delta * 8);
            Figure.Rotation = new Vector3(Mathf.Lerp(Figure.Rotation.X, stoop, ease), yaw, Mathf.Lerp(Figure.Rotation.Z, lean, ease));
            Figure.Move(GroundSpeed, IsOnFloor(), Swimming);
        }
        // The view widens a little at a sprint, and settles back when you slow.
        var widen = GroundSpeed > WalkSpeed * 1.15f ? 82f : 75f;
        Camera.Fov = Mathf.Lerp(Camera.Fov, widen, (float)Math.Min(1, delta * 3));

        if (Input.IsActionJustPressed("attack")) Swing();
        if (Input.IsActionJustPressed("spell")) Cast();
        if (Input.IsActionJustPressed("cycle_element")) Use(Class.Utility);
        if (Input.IsActionJustPressed("limit")) LimitBreak();
    }

    /// <summary>Set by the game while a fight is on, for regeneration.</summary>
    public bool InFight { get; set; }

    /// <summary>Set while talking or reading the map: no walking off mid-sentence.</summary>
    public bool Frozen { get; set; }

    // ---- the moves -----------------------------------------------------------------------------

    /// <summary>The main attack (left button). In the saddle, the dragon strikes instead.</summary>
    public bool Swing() => Mount is not null ? DragonStrike() : Use(Class.Primary);

    /// <summary>The signature move (right button).</summary>
    public bool Cast() => Use(Class.Signature);

    /// <summary>The limit break (R), once the gauge is full.</summary>
    public bool LimitBreak() => Use(Class.Limit);

    /// <summary>A limit break went off (the HUD shows its name across the screen).</summary>
    public event Action<Ability, Color>? LimitUsed;

    /// <summary>The colour of each class's limit break.</summary>
    public Color LimitColour => Class.Id switch
    {
        "MartialArtist" => new Color("#ffd27a"),
        "Barbarian" => new Color("#ff4a32"),
        "Paladin" => new Color("#fff0a0"),
        "Ranger" => new Color("#9be07a"),
        "Thief" => new Color("#ffcc33"),
        "Assassin" => new Color("#b07aff"),
        "Wizard" => new Color("#ff8a2a"),
        "Warlock" => new Color("#9a5aff"),
        "Sorcerer" => new Color("#ff5a1a"),
        "Poet" => new Color("#ffd86a"),
        "Scribe" => new Color("#7ad8ff"),
        "Musician" => new Color("#ff9ad0"),
        _ => Effects.Of(Class.Limit.Kind),
    };

    /// <summary>The moment a limit break goes off: light, a shockwave, the world slowing for a heartbeat, the camera shaking.</summary>
    void LimitMoment(Ability a)
    {
        var c = LimitColour;
        Effects.Limit(GetParent(), GlobalPosition, c);
        LimitUsed?.Invoke(a, c);
        Engine.TimeScale = 0.35;
        GetTree().CreateTimer(0.45, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => Engine.TimeScale = 1;
        var shake = 0.0;
        void Shake()
        {
            shake += 1.0 / 60;
            if (shake > 0.6 || !IsInstanceValid(Camera)) { if (IsInstanceValid(Camera)) (Camera.HOffset, Camera.VOffset) = (0, 0); return; }
            var k = (float)(0.25 * (1 - shake / 0.6));
            (Camera.HOffset, Camera.VOffset) = ((float)GD.RandRange(-k, k), (float)GD.RandRange(-k, k));
            GetTree().CreateTimer(1.0 / 60, ignoreTimeScale: true).Timeout += Shake;
        }
        Shake();
    }

    /// <summary>How long until a move can be used again (0: ready).</summary>
    public double Cooldown(string id) => Math.Max(0, _ready.GetValueOrDefault(id) - Now);

    /// <summary>Uses one of the class's moves, if it's ready and there's the stamina or magicka for it.</summary>
    public bool Use(Ability a)
    {
        if (Downed) return false;
        var limit = a == Class.Limit;
        if (limit ? !Stats.LimitReady : Cooldown(a.Id) > 0) return false;
        var cost = a.Cost * (Class.Id == "Sorcerer" && a.Pays == AttackKind.Spell ? 0.85 : 1);
        if (!limit && !Stats.Spend(a.Pays, cost)) return false;
        if (limit) { Stats.UseLimit(); LimitMoment(a); }
        _ready[a.Id] = Now + a.Cooldown;
        _actingUntil = Now + Math.Min(0.6, Math.Max(0.3, a.Cooldown));
        Figure.Rotation = new Vector3(0, 0, 0);
        var power = Hero.Power(a.Id);
        var dmg = a.Power * power;
        var near = InFront(a.Range > 0 ? a.Range : 2.4, a.Range > 6 ? 30 : 70).FirstOrDefault();
        if (a.Pays == AttackKind.Weapon && a.Range < 6 && a.Power > 1 && Class.Id != "Ranger") // a blade, an axe, a fist through the air
            Sound.At(Class.Id == "MartialArtist" ? "melee/punch" : "melee/swing", GlobalPosition + Vector3.Up, -6);
        if (Class.Calling == Calling.Bard) Play(a);
        switch (a.Id)
        {
            // ---- the martial artist ----
            case "flurry":
                var kick = ++_combo % 3 == 0;
                Figure.Perform(kick ? "Unarmed_Melee_Attack_Kick" : _combo % 2 == 0 ? "Unarmed_Melee_Attack_Punch_A" : "Unarmed_Melee_Attack_Punch_B", 0.35, 1.8f);
                if (near is not null) Strike(near, kick ? dmg * 2 : dmg, a.Kind, AttackKind.Weapon);
                break;
            case "flying_kick":
                if (near is null) { Refund(a); return false; }
                Lunge(near, 0.25);
                Figure.Perform("Unarmed_Melee_Attack_Kick", 0.5, 1.4f);
                Strike(near, dmg, a.Kind, AttackKind.Weapon);
                break;
            case "inner_focus":
                Figure.Perform("Spellcast_Raise", 0.8);
                Effects.Rise(this, DamageKind.Sun);
                For(a.Seconds * power, AutoStatus.Regen, AutoStatus.Haste);
                break;
            case "hundred_fists":
                if (near is null) break;
                for (var i = 0; i < 8; i++)
                {
                    var blow = i;
                    GetTree().CreateTimer(blow * 0.09).Timeout += () =>
                    {
                        if (!IsInstanceValid(near) || near.State == EnemyState.Dead) return;
                        Figure.Perform(blow % 2 == 0 ? "Unarmed_Melee_Attack_Punch_A" : "Unarmed_Melee_Attack_Punch_B", 0.1, 3);
                        Strike(near, dmg, a.Kind, AttackKind.Weapon);
                    };
                }
                Said?.Invoke("Hundred Fists!");
                break;

            // ---- the barbarian ----
            case "cleave":
                Figure.Perform("2H_Melee_Attack_Slice", 0.8, 1.2f);
                foreach (var e in InFront(a.Range, 75).Take(5).ToList()) Strike(e, dmg, a.Kind, AttackKind.Weapon);
                break;
            case "leap_slam":
                if (near is null) { Refund(a); return false; }
                Lunge(near, 0.45, arc: 3);
                Figure.Perform("2H_Melee_Attack_Chop", 0.8, 1.2f);
                GetTree().CreateTimer(0.45).Timeout += () =>
                {
                    Effects.Burst(GetParent(), GlobalPosition, DamageKind.Physical, 3);
                    foreach (var e in Around(4).ToList()) Strike(e, dmg, a.Kind, AttackKind.Weapon);
                };
                break;
            case "rage":
                Figure.Perform("Cheer", 0.8);
                Effects.Rise(this, DamageKind.Fire);
                _rage = a.Seconds;
                Said?.Invoke("RAGE!");
                break;
            case "whirlwind":
                Figure.Perform("2H_Melee_Attack_Spin", 1.1, 1);
                Said?.Invoke("LIMIT BREAK: Whirlwind!");
                foreach (var e in Around(a.Range).ToList()) Strike(e, dmg, a.Kind, AttackKind.Weapon);
                break;

            // ---- the paladin ----
            case "smite":
                Figure.Attack();
                if (near is not null) { Strike(near, dmg, a.Kind, AttackKind.Weapon); Effects.Burst(GetParent(), near.GlobalPosition + Vector3.Up, DamageKind.Sun, 0.8f); }
                break;
            case "holy_light":
                Figure.Perform("Spellcast_Raise", 0.8);
                Heal(a.Power * power);
                foreach (var c in Companions()) { c.Stats.Heal(c.Stats.MaxHealth * a.Power * power); c.Soothe(0.5 * power); c.RefreshPlate(); Effects.Rise(c); }
                Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, DamageKind.Sun, 4);
                foreach (var e in Around(a.Range).ToList()) Strike(e, 25 * power, DamageKind.Sun, AttackKind.Spell);
                break;
            case "shield_wall":
                Figure.Perform("Block", 0.9, 1);
                For(a.Seconds * power, AutoStatus.Protect, AutoStatus.Shell);
                Said?.Invoke("Shield Wall: Protect and Shell.");
                break;
            case "judgement":
                Figure.Perform("Spellcast_Long", 1.2, 1.2f);
                Said?.Invoke("LIMIT BREAK: Divine Judgement!");
                Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, DamageKind.Sun, (float)a.Range);
                foreach (var e in Around(a.Range).ToList()) Strike(e, dmg, a.Kind, AttackKind.Spell);
                Heal(0.5);
                foreach (var c in Companions()) { c.Revive(); c.Stats.Heal(1e9); c.Soothe(1); c.RefreshPlate(); }
                break;

            // ---- the ranger ----
            case "shoot":
                Figure.Perform("2H_Ranged_Shoot", 0.6, 1.5f);
                if (near is not null) { Shot(near, DamageKind.Physical); Strike(near, dmg, a.Kind, AttackKind.Weapon); }
                break;
            case "volley":
                Figure.Perform("2H_Ranged_Shoot", 0.6, 1.2f);
                foreach (var e in InFront(a.Range, 45).Take(4).ToList()) { Shot(e, DamageKind.Physical); Strike(e, dmg, a.Kind, AttackKind.Weapon); }
                break;
            case "call_wild":
                Figure.Perform("Cheer", 0.8);
                foreach (var c in Companions()) c.Empower(a.Seconds * power, a.Power);
                Said?.Invoke(Companions().Any() ? "Your beasts answer the call." : "The call goes out, but nothing of yours is near to answer.");
                break;
            case "rain_of_bolts":
                if (near is null) break;
                Figure.Perform("2H_Ranged_Shoot", 0.8, 1);
                Said?.Invoke("LIMIT BREAK: Rain of Bolts!");
                for (var i = 0; i < 10; i++)
                {
                    var spot = near.GlobalPosition + new Vector3((float)GD.RandRange(-5, 5), 0, (float)GD.RandRange(-5, 5));
                    Effects.Bolt(GetParent(), spot + new Vector3(0, 25, 0), spot, DamageKind.Physical, 0.12f);
                }
                foreach (var e in Around(6, near.GlobalPosition).ToList()) Strike(e, dmg, a.Kind, AttackKind.Weapon);
                break;

            // ---- the thief ----
            case "stab":
                Figure.Perform(++_combo % 2 == 0 ? "Dualwield_Melee_Attack_Stab" : "Dualwield_Melee_Attack_Slice", 0.35, 1.8f);
                if (near is not null) Strike(near, dmg, a.Kind, AttackKind.Weapon);
                break;
            case "pilfer":
                Figure.Perform("Dualwield_Melee_Attack_Chop", 0.5, 1.5f);
                if (near is not null) { Strike(near, dmg, a.Kind, AttackKind.Weapon); Steal(near, power); }
                break;
            case "smoke_bomb":
                Figure.Perform("Throw", 0.5, 1.5f);
                Effects.Smoke(GetParent(), GlobalPosition + Vector3.Up, (float)a.Range * 0.6f);
                foreach (var e in Around(a.Range).ToList()) e.LoseTrack(a.Seconds * power);
                _smoke = true;
                Said?.Invoke("You vanish into the smoke.");
                break;
            case "grand_heist":
                Figure.Perform("2H_Melee_Attack_Spin", 1, 1.2f);
                Said?.Invoke("LIMIT BREAK: Grand Heist!");
                foreach (var e in Around(a.Range).ToList()) { Strike(e, dmg, a.Kind, AttackKind.Weapon); Steal(e, power * 2); }
                break;

            // ---- the assassin ----
            case "assassinate":
                Figure.Perform("1H_Melee_Attack_Stab", 0.5, 1.5f);
                if (near is not null) Strike(near, dmg, a.Kind, AttackKind.Weapon);
                break;
            case "shadowstep":
                if (near is null) { Refund(a); return false; }
                Effects.Smoke(GetParent(), GlobalPosition + Vector3.Up, 1.5f);
                var behind = near.GlobalPosition + near.GlobalTransform.Basis.Z * 1.4f;
                GlobalPosition = behind with { Y = Math.Max(behind.Y, City.FloorAt(behind)) };
                Face(near.GlobalPosition);
                Figure.Perform("1H_Melee_Attack_Stab", 0.5, 1.5f);
                Strike(near, dmg, a.Kind, AttackKind.Weapon, sureCrit: true);
                break;
            case "poison_blades":
                Figure.Perform("Use_Item", 0.6, 1.5f);
                Effects.Rise(this, DamageKind.Poison, 1.2f);
                _poison = a.Seconds * power;
                break;
            case "death_mark":
                if (near is null) break;
                Figure.Perform("Dualwield_Melee_Attack_Stab", 0.8, 1);
                Said?.Invoke("LIMIT BREAK: Death Mark.");
                Strike(near, dmg, a.Kind, AttackKind.Weapon, sureCrit: true);
                break;

            // ---- the wizard ----
            case "bolt":
                Figure.Cast();
                if (near is not null) Spell(near, dmg, Element, 0.25f);
                break;
            case "blast":
                if (near is null) { Refund(a); return false; }
                Figure.Perform("Spellcast_Shoot", 0.7, 1.2f);
                Effects.Bolt(GetParent(), Hand, near.GlobalPosition + Vector3.Up, Element, 0.4f);
                Effects.Burst(GetParent(), near.GlobalPosition + Vector3.Up, Element, 5);
                foreach (var e in Around(5, near.GlobalPosition).ToList()) Strike(e, dmg, Element, AttackKind.Spell);
                break;
            case "attune":
                Element = Element switch { DamageKind.Fire => DamageKind.Frost, DamageKind.Frost => DamageKind.Shock, _ => DamageKind.Fire };
                Said?.Invoke($"Spell: {Element}");
                break;
            case "meteor":
                if (near is null) break;
                Figure.Perform("Spellcast_Raise", 1, 1);
                Said?.Invoke("LIMIT BREAK: Meteor!");
                Effects.Bolt(GetParent(), near.GlobalPosition + new Vector3(8, 40, 8), near.GlobalPosition, DamageKind.Fire, 1.4f);
                Effects.Burst(GetParent(), near.GlobalPosition + Vector3.Up, DamageKind.Fire, 7);
                foreach (var e in Around(7, near.GlobalPosition).ToList()) Strike(e, dmg, a.Kind, AttackKind.Spell);
                break;

            // ---- the warlock or witch ----
            case "eldritch_blast":
                Figure.Cast();
                if (near is not null) Spell(near, dmg, a.Kind, 0.3f, look: DamageKind.Shock);
                break;
            case "drain_life":
                if (near is null) { Refund(a); return false; }
                Figure.Perform("Spellcast_Long", 0.8, 1.4f);
                Effects.Bolt(GetParent(), near.GlobalPosition + Vector3.Up, Hand, DamageKind.Poison, 0.3f);
                Heal(Strike(near, dmg, a.Kind, AttackKind.Spell) / Stats.MaxHealth);
                break;
            case "familiar":
                Figure.Perform("Spellcast_Raise", 0.8);
                Summoned?.Invoke(a.Seconds * power);
                break;
            case "soul_harvest":
                Figure.Perform("Spellcast_Long", 1.2, 1);
                Said?.Invoke("LIMIT BREAK: Soul Harvest!");
                var drunk = 0.0;
                foreach (var e in Around(a.Range).ToList())
                {
                    Effects.Bolt(GetParent(), e.GlobalPosition + Vector3.Up, Hand, DamageKind.Poison, 0.25f);
                    drunk += Strike(e, dmg, a.Kind, AttackKind.Spell);
                }
                Heal(drunk * 0.5 / Stats.MaxHealth);
                break;

            // ---- the sorcerer ----
            case "missiles":
                Figure.Perform("Spellcast_Shoot", 0.3, 2.2f);
                if (near is not null) Spell(near, dmg, Element, 0.14f);
                break;
            case "chain_lightning":
                if (near is null) { Refund(a); return false; }
                Figure.Perform("Spellcast_Shoot", 0.6, 1.4f);
                var from = Hand;
                var struck = new HashSet<Enemy>();
                var link = near;
                for (var jump = 0; jump < 5 && link is not null; jump++)
                {
                    Effects.Bolt(GetParent(), from, link.GlobalPosition + Vector3.Up, DamageKind.Shock, 0.18f);
                    Strike(link, dmg * Math.Pow(0.85, jump), a.Kind, AttackKind.Spell);
                    struck.Add(link);
                    from = link.GlobalPosition + Vector3.Up;
                    var here = link.GlobalPosition;
                    link = Foes().Where(e => !struck.Contains(e) && e.GlobalPosition.DistanceTo(here) < 9).MinBy(e => e.GlobalPosition.DistanceTo(here));
                }
                break;
            case "dragonfire":
                Figure.Perform("Spellcast_Long", 1, 1.2f);
                Said?.Invoke("LIMIT BREAK: Dragonfire!");
                Effects.Cone(GetParent(), Hand, -GlobalTransform.Basis.Z, DamageKind.Fire, (float)a.Range);
                foreach (var e in InFront(a.Range, 40).ToList()) Strike(e, dmg, a.Kind, AttackKind.Spell);
                break;

            // ---- the poet ----
            case "barbed_verse":
                Figure.Perform("Spellcast_Shoot", 0.5, 1.6f);
                if (near is not null) Spell(near, dmg, a.Kind, 0.2f);
                break;
            case "rousing_ode":
                Figure.Perform("Cheer", 0.9);
                Effects.Song(GetParent(), GlobalPosition, Effects.Of(DamageKind.Sun), 6, 10);
                Heal(a.Power * power);
                For(a.Seconds * power, AutoStatus.Haste);
                foreach (var c in Companions()) c.Empower(a.Seconds * power, a.Power * power);
                Said?.Invoke("Rousing Ode: the company takes heart.");
                break;
            case "mocking_couplet":
                Figure.Perform("Spellcast_Raise", 0.7, 1.4f);
                Effects.Song(GetParent(), GlobalPosition, LimitColour, (float)a.Range, 6);
                foreach (var e in Around(a.Range).ToList()) e.LoseTrack(a.Seconds * power);
                Said?.Invoke("Your couplet leaves them speechless.");
                break;
            case "epic_of_ages":
                Figure.Perform("Spellcast_Long", 1.2, 1.1f);
                Said?.Invoke("LIMIT BREAK: Epic of Ages!");
                Effects.Song(GetParent(), GlobalPosition, LimitColour, (float)a.Range, 16);
                Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, DamageKind.Sun, (float)a.Range);
                foreach (var e in Around(a.Range).ToList()) Strike(e, dmg, a.Kind, AttackKind.Spell);
                Heal(0.4);
                foreach (var c in Companions()) { c.Revive(); c.Stats.Heal(1e9); c.RefreshPlate(); }
                break;

            // ---- the scribe ----
            case "inkbolt":
                Figure.Perform("Spellcast_Shoot", 0.45, 1.8f);
                if (near is not null) Spell(near, dmg, a.Kind, 0.16f);
                break;
            case "glyph":
            {
                if (near is null) { Refund(a); return false; }
                Figure.Perform("Spellcast_Shoot", 0.7, 1.2f);
                var spot = near.GlobalPosition;
                Effects.Glyph(GetParent(), spot, Effects.Of(a.Kind), 4, 0.8, a.Kind);
                GetTree().CreateTimer(0.8).Timeout += () =>
                {
                    if (!IsInstanceValid(this)) return;
                    foreach (var e in Around(4.5, spot).ToList()) Strike(e, dmg, a.Kind, AttackKind.Spell);
                };
                break;
            }
            case "scroll":
                Figure.Perform("Use_Item", 0.8, 1.2f);
                Effects.Rise(this, DamageKind.Shock, 2);
                For(a.Seconds * power, AutoStatus.Protect, AutoStatus.Shell, AutoStatus.Regen);
                Said?.Invoke("Scroll of Protection: Protect, Shell and Regen.");
                break;
            case "unmaking":
            {
                Figure.Perform("Spellcast_Long", 1.2, 1.1f);
                Said?.Invoke("LIMIT BREAK: Words of Unmaking!");
                var spot = GlobalPosition;
                Effects.Glyph(GetParent(), spot, LimitColour, (float)a.Range, 0.5, a.Kind);
                GetTree().CreateTimer(0.5).Timeout += () =>
                {
                    if (!IsInstanceValid(this)) return;
                    foreach (var e in Around(a.Range, spot).ToList()) { Strike(e, dmg, a.Kind, AttackKind.Spell); e.LoseTrack(3); }
                };
                break;
            }

            // ---- the musician ----
            case "discord":
            {
                var strong = ++_combo % 4 == 0;
                Figure.Perform(strong ? "Spellcast_Raise" : "Spellcast_Shoot", 0.4, 2);
                if (near is not null) Spell(near, strong ? dmg * 2 : dmg, a.Kind, strong ? 0.26f : 0.16f, look: DamageKind.Sun);
                if (strong) Effects.Song(GetParent(), GlobalPosition, LimitColour, 2, 3);
                break;
            }
            case "thunder_chord":
                Figure.Perform("Spellcast_Shoot", 0.6, 1.3f);
                Effects.Wave(GetParent(), Hand, -GlobalTransform.Basis.Z, LimitColour, (float)a.Range);
                foreach (var e in InFront(a.Range, 45).ToList()) { Strike(e, dmg, a.Kind, AttackKind.Spell); Push(e, 3.5f); }
                break;
            case "battle_hymn":
                Figure.Perform("Cheer", 0.9);
                Effects.Song(GetParent(), GlobalPosition, LimitColour, 6, 12);
                For(a.Seconds * power, AutoStatus.Haste, AutoStatus.Regen);
                foreach (var c in Companions()) c.Empower(a.Seconds * power, 0.2 * power);
                Said?.Invoke("Battle Hymn: the company fights to your song.");
                break;
            case "crescendo":
                Figure.Perform("Spellcast_Long", 1.4, 1);
                Said?.Invoke("LIMIT BREAK: Crescendo!");
                for (var beat = 0; beat < 3; beat++)
                {
                    var b = beat;
                    GetTree().CreateTimer(b * 0.45).Timeout += () =>
                    {
                        if (!IsInstanceValid(this)) return;
                        Effects.Song(GetParent(), GlobalPosition, LimitColour, (float)a.Range * (0.5f + b * 0.25f), 6 + b * 4);
                        foreach (var e in Around(a.Range).ToList()) Strike(e, dmg * (1 + b * 0.25), a.Kind, AttackKind.Spell);
                    };
                }
                break;
        }
        // A sorcerer's magic sometimes surges and the spell goes off twice.
        if (Class.Id == "Sorcerer" && a.Pays == AttackKind.Spell && a.Power > 1 && !limit && _rng.Chance(Class.Passive.Power * Hero.Power("wild_surge")))
        {
            Said?.Invoke("Wild surge!");
            _ready[a.Id] = 0;
            Stats.Restore(cost);
            GetTree().CreateTimer(0.25).Timeout += () => { if (IsInstanceValid(this)) Use(a); };
        }
        return true;
    }

    /// <summary>What a bard's moves sound like: a strummed lute, a harp, a drum, a horn, a chant, a quill, a turned page.</summary>
    void Play(Ability a)
    {
        var at = GlobalPosition + Vector3.Up;
        switch (a.Id)
        {
            case "barbed_verse": Sound.At("bard/chant", at, -10, 1.4f); break;
            case "rousing_ode": Sound.At("bard/harp", at, -2); Sound.At("bard/chant", at, -6); break;
            case "mocking_couplet": Sound.At("bard/lute", at, -4, 1.2f); break;
            case "epic_of_ages": Sound.At("bard/horn", at, 0, range: 60); Sound.At("bard/harp", at, -2); break;
            case "inkbolt": Sound.At("bard/quill", at, -6); break;
            case "glyph": Sound.At("bard/quill", at, -2, 0.8f); break;
            case "scroll": Sound.At("bard/page", at, 0); break;
            case "unmaking": Sound.At("bard/page", at, 0, 0.7f); Sound.At("bard/chant", at, -2, 0.8f); break;
            case "discord": Sound.At("bard/lute", at, -6, 0.9f + 0.1f * (_combo % 4)); break;
            case "thunder_chord": Sound.At("bard/drum", at, 0, 0.8f); Sound.At("bard/lute", at, -2, 0.7f); break;
            case "battle_hymn": Sound.At("bard/drum", at, -2); Sound.At("bard/lute", at, -2); break;
            case "crescendo": Sound.At("bard/horn", at, 0, range: 60); Sound.At("bard/drum", at, 0, 0.9f); break;
        }
    }

    void Refund(Ability a)
    {
        _ready[a.Id] = 0;
        Stats.Restore(a.Pays == AttackKind.Spell ? a.Cost : 0, a.Pays == AttackKind.Weapon ? a.Cost : 0);
    }

    /// <summary>Statuses for a while (Regen and Haste, Protect and Shell), then gone.</summary>
    void For(double seconds, params AutoStatus[] statuses)
    {
        foreach (var s in statuses) Stats.Statuses.Add(s);
        GetTree().CreateTimer(seconds).Timeout += () => { foreach (var s in statuses) Stats.Statuses.Remove(s); };
    }

    void Heal(double share)
    {
        if (share <= 0) return;
        Stats.Heal(Stats.MaxHealth * share);
        Effects.Rise(this, DamageKind.Sun);
    }

    Vector3 Hand => GlobalPosition + Vector3.Up * 1.3f - GlobalTransform.Basis.Z * 0.6f;

    void Shot(Enemy e, DamageKind kind) => Effects.Bolt(GetParent(), Hand, e.GlobalPosition + Vector3.Up, kind, 0.07f);

    void Spell(Enemy e, double damage, DamageKind kind, float size, DamageKind? look = null)
    {
        Effects.Bolt(GetParent(), Hand, e.GlobalPosition + Vector3.Up, look ?? kind, size);
        Strike(e, damage, kind, AttackKind.Spell);
    }

    /// <summary>Leaps or dashes to just short of a foe.</summary>
    void Lunge(Enemy e, double seconds, float arc = 0.5f)
    {
        var to = e.GlobalPosition - GlobalPosition;
        to.Y = 0;
        var end = e.GlobalPosition - to.Normalized() * 1.3f;
        end.Y = Math.Max(end.Y, City.FloorAt(end));
        Face(e.GlobalPosition);
        var tween = CreateTween();
        tween.TweenProperty(this, "global_position", (GlobalPosition + end) / 2 + Vector3.Up * arc, seconds / 2);
        tween.TweenProperty(this, "global_position", end, seconds / 2);
    }

    /// <summary>Throws a foe back from the hero (a musician's wall of sound).</summary>
    void Push(Enemy e, float distance)
    {
        if (e.Kind.Height > 3 || e.State == EnemyState.Dead) return; // dragons and the great bosses stand their ground
        var away = e.GlobalPosition - GlobalPosition;
        away.Y = 0;
        if (away.LengthSquared() < 0.01f) return;
        var end = e.GlobalPosition + away.Normalized() * distance;
        end.Y = Math.Max(e.GlobalPosition.Y, City.FloorAt(end));
        e.CreateTween().TweenProperty(e, "global_position", end, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    void Steal(Enemy e, double power)
    {
        if (e.Friendly) return;
        var take = (int)Math.Round((1 + _rng.Next(5)) * power);
        Septims += take;
        Said?.Invoke($"You lift {take} septims from {e.Stats.Name}.");
    }

    /// <summary>
    /// Lands a blow, with everything the class brings to it: a rogue's sure critical from the smoke, an assassin's
    /// backstab, a barbarian's rage, a paladin's light against the dead and the corrupted, poisoned blades.
    /// Returns the harm done.
    /// </summary>
    double Strike(Enemy target, double baseDamage, DamageKind kind, AttackKind attack, bool sureCrit = false)
    {
        if (target.State == EnemyState.Dead) return 0;
        var damage = Stats.Outgoing(attack, baseDamage, _rng, out var crit);
        if ((sureCrit || _smoke) && !crit) { damage *= 2; crit = true; }
        _smoke = false;
        if (Class.Id == "Assassin" && !target.FightingCompany) damage *= 1 + Class.Passive.Power * Hero.Power("backstab");
        if (_rage > 0) damage *= 1 + 0.6 * Hero.Power("rage");
        if (Class.Id == "Scribe") // every weakness noted: each blow on the same foe hits harder
        {
            if (_noted != target) (_noted, _notes) = (target, 0);
            damage *= 1 + Math.Min(_notes++, 5) * Class.Passive.Power * Hero.Power("marginalia");
        }
        if (kind == DamageKind.Sun && Class.Id == "Paladin" && (target.Undead || target.Side == Tristinwold.World.Side.Corrupted))
            damage *= 1.3 + Class.Passive.Power * Hero.Power("devotion");
        var result = Hit(target, damage, kind, attack, crit);
        if (_poison > 0 && attack == AttackKind.Weapon && target.State != EnemyState.Dead)
            Hit(target, Class.Utility.Power * Hero.Power("poison_blades"), DamageKind.Poison, AttackKind.Spell, false);
        return result.Dealt;
    }

    HitResult Hit(Enemy target, double damage, DamageKind kind, AttackKind attack, bool crit)
    {
        LastStruck = target;
        var result = target.TakeHit(damage, kind, attack, crit, this);
        if (attack == AttackKind.Weapon && GetParent() is { } world) // where the blow lands
            Effects.Impact(world, target.GlobalPosition + Vector3.Up * (float)Math.Max(0.8, target.Kind.Height * 0.55), target.GlobalPosition - GlobalPosition, kind, crit);
        Struck?.Invoke(target, result, kind);
        if (result.Healed > 0) Said?.Invoke($"{target.Stats.Name} drinks the {kind.ToString().ToLowerInvariant()}!");
        if (result.Reflected > 0) Said?.Invoke($"{target.Stats.Name} turns the blow back on you.");
        return result;
    }

    /// <summary>A blow from a foe: a martial artist may slip it; rage leaves you open; a paladin's devotion turns some.</summary>
    public void TakeHit(double damage, DamageKind kind, AttackKind attack, bool crit, Enemy from)
    {
        if (Downed) return;
        if (Class.Id == "MartialArtist" && _rng.Chance(Class.Passive.Power + 0.03 * (Hero.Rank("evasion") - 1)))
        {
            Figure.Perform("Dodge_Left", 0.4, 1.6f);
            return;
        }
        if (_rage > 0) damage *= 1.2;
        if (Class.Id == "Paladin") damage *= 1 - Class.Passive.Power * Hero.Power("devotion");
        Stats.TakeHit(damage, kind, attack, crit, from.Stats);
        if (_rng.Chance(0.35) || crit) Sound.At("creature/hurt_hero", GlobalPosition + Vector3.Up, -4, Appearance?.Size > 1.15 ? 0.8f : Appearance?.Size < 0.85 ? 1.25f : 1);
        if (attack == AttackKind.Weapon && GetParent() is { } world) Effects.Impact(world, GlobalPosition + Vector3.Up * 1.1f, GlobalPosition - from.GlobalPosition, DamageKind.Physical, crit);
        if (!Stats.Dead && Mount is null) Figure.Hurt();
        if (Stats.Dead) GoDown();
    }

    /// <summary>What this hero's moves do when a foe falls to the company: a warlock drinks its soul, a raging barbarian heals.</summary>
    public void Killed(Enemy e)
    {
        if (Class.Id == "Warlock")
        {
            var pact = Class.Passive.Power * Hero.Power("dark_pact");
            Stats.Heal(Stats.MaxHealth * pact);
            Stats.Restore(Stats.MaxMagicka * pact);
        }
        if (_rage > 0) Stats.Heal(Stats.MaxHealth * 0.1);
        if (Class.Id == "Poet") // the muse rewards a fall well sung
        {
            var muse = 0.5 * Class.Passive.Power * Hero.Power("muse");
            Stats.Restore(Stats.MaxMagicka * muse, Stats.MaxStamina * muse);
        }
    }

    void GoDown()
    {
        Dismount();
        Downed = true;
        _downTimer = 4;
        Figure.Die();
        Sound.At("creature/die_hero", GlobalPosition + Vector3.Up, 0);
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

    // ---- riding ----------------------------------------------------------------------------------

    /// <summary>Climbs into the saddle.</summary>
    public void Ride(Enemy dragon)
    {
        Mount = dragon;
        dragon.Bear(this);
        (CollisionLayer, CollisionMask) = (0, 0);
        Velocity = Vector3.Zero;
        Figure.Seat(true);
        Said?.Invoke($"You climb onto {dragon.Stats.Name}'s back. Space climbs, C dives, V gets down.");
    }

    /// <summary>Gets down, beside the dragon.</summary>
    public void Dismount()
    {
        if (Mount is not { } dragon) return;
        Mount = null;
        if (IsInstanceValid(dragon)) dragon.Bear(null);
        (CollisionLayer, CollisionMask) = (1, 1);
        Figure.Seat(false);
        var side = IsInstanceValid(dragon) ? dragon.GlobalPosition + dragon.GlobalTransform.Basis.X * 3 : GlobalPosition;
        GlobalPosition = side with { Y = City.FloorAt(side) + 0.3f };
        Figure.Rotation = Vector3.Zero;
    }

    /// <summary>Footsteps on what's underfoot (stone in the city, boards on the docks, grass in the wilds, snow up high), and the thump of landing.</summary>
    void Steps(double delta)
    {
        if (!IsOnFloor() || Swimming)
        {
            if (!Swimming) _airborne += delta;
            return;
        }
        if (_airborne > 0.45) Sound.At("move/land", GlobalPosition, -4);
        _airborne = 0;
        _stride += GroundSpeed * delta;
        var length = GroundSpeed > WalkSpeed * 1.15f ? 2.3f : 1.6f; // longer strides at a run
        if (_stride < length) return;
        _stride = 0;
        var area = City.AreaAt(GlobalPosition);
        var ground = area is "The Docks" or "The Harbor" ? "wood"
            : area is not ("The Crown" or "The Exchange" or "Tristinwold") && Tristinwold.World.WorldGen.Default.BiomeAt(GlobalPosition.X, GlobalPosition.Z) is Tristinwold.World.Biome.Snowfield or Tristinwold.World.Biome.Tundra ? "snow"
            : area is "The Crown" or "The Exchange" or "Tristinwold" ? "stone" : "grass";
        Sound.At("foot/" + ground, GlobalPosition, GroundSpeed > WalkSpeed * 1.15f ? -8 : -12, range: 18);
    }

    void Ride(Vector3 dir, bool fast)
    {
        var climb = Input.IsActionPressed("jump") ? 1 : Input.IsActionPressed("descend") ? -1 : 0;
        Reins = (dir, climb, fast);
        if (!IsInstanceValid(Mount)) { Dismount(); return; }
        // Sit in the saddle, facing the way the dragon faces; the camera is yours to turn.
        GlobalPosition = Mount!.SaddleGlobal;
        Velocity = Vector3.Zero;
        if (Mount.Airborne && (_wingbeat -= GetPhysicsProcessDeltaTime()) <= 0) // the great wings beating
        {
            _wingbeat = fast ? 0.7 : 1.1;
            Sound.At("move/wings", GlobalPosition, -4, 0.9f);
        }
        Figure.GlobalRotation = new Vector3(0, Mount.GlobalRotation.Y, 0);
    }

    /// <summary>In the saddle, the attack button is the dragon's: breath if it knows how, else its claws on the nearest foe.</summary>
    bool DragonStrike()
    {
        if (Mount is not { Bond: { } bond } dragon || Cooldown("mount") > 0) return false;
        _ready["mount"] = Now + 2.5;
        var damage = dragon.Kind.Damage * dragon.Stats.DamageMult * 1.5;
        if (bond.Knows("breath")) { dragon.Breathe(damage * 1.4, 14 * (float)Math.Max(1, bond.Size)); return true; }
        var near = Foes().Where(e => e.GlobalPosition.DistanceTo(dragon.GlobalPosition) < 6 * bond.Size).MinBy(e => e.GlobalPosition.DistanceTo(dragon.GlobalPosition));
        dragon.Figure.Attack();
        near?.TakeHitFrom(dragon, damage, false);
        return true;
    }

    // ---- who's about -----------------------------------------------------------------------------

    /// <summary>Foes: everything that fights, less the company's own and the alliance.</summary>
    IEnumerable<Enemy> Foes() =>
        GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly);

    /// <summary>The company's companions that are with this hero and on their feet.</summary>
    public IEnumerable<Enemy> Companions() =>
        GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e.Leader == this && e.State != EnemyState.Dead);

    /// <summary>Foes attacking this hero right now, nearest first.</summary>
    public IEnumerable<Enemy> Attackers() =>
        Foes().Where(e => e.FightingCompany && e.GlobalPosition.DistanceTo(GlobalPosition) < 20).OrderBy(e => e.GlobalPosition.DistanceTo(GlobalPosition));

    IEnumerable<Enemy> Around(double radius, Vector3? at = null)
    {
        var centre = at ?? GlobalPosition;
        return Foes().Where(e => e.GlobalPosition.DistanceTo(centre) <= radius);
    }

    IEnumerable<Enemy> InFront(double range, float coneDegrees)
    {
        var forward = -GlobalTransform.Basis.Z;
        return Foes()
            .Where(e => e.GlobalPosition.DistanceTo(GlobalPosition) <= range)
            .Where(e =>
            {
                var to = e.GlobalPosition - GlobalPosition;
                to.Y = 0;
                return to.Length() < 0.5f + e.Figure.Scale.X * 0.6f || Mathf.RadToDeg(forward.AngleTo(to.Normalized())) <= coneDegrees;
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
