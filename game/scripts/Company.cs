using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The company around the hero: experience and levels from every kill, the companions who follow them (beasts
/// raised from younglings, squires and apprentices from the towns, a warlock's familiar, a dragon), taming,
/// feeding, the guild masters who teach the classes, and the road to Dragon Knight with Ser Aldric.
/// </summary>
public partial class Main
{
    /// <summary>A Ranger (the beastmaster's way) and a Shapeshifter (who runs with the pack) can have as many pets at their heels as they like.</summary>
    bool FreePets => _player.Class.Id is "Ranger" or "Shapeshifter";

    /// <summary>Whether there's room for this one with the hero (see <see cref="PartyRules"/>).</summary>
    bool MayFollow(Bond bond) => PartyRules.MayFollow(_bonds, bond, FreePets, FreeApprentices);

    /// <summary>A Paladin and a Wizard can take on as many apprentices as they like.</summary>
    bool FreeApprentices => PartyRules.FreeApprentices(_player.Class.Id);

    /// <summary>Where a companion not with the hero stays: their house, once they've listed it as home, or the Perch.</summary>
    string WaitsAt => _pursuits.House.IsHome ? "your house" : "the Gallows Perch";

    /// <summary>Companions at home get on with the work there; the chronicle says what's come of it.</summary>
    void HomeWork(double seconds)
    {
        if (!_sharedCity) { _city.TendStores(seconds); _city.TendHalls(seconds); _city.TendKorrin(); _city.TendRaid(); _city.TendWarlords(LocalContenders()); } // on a server its own clock runs them, whoever's online
        SyncStore();
        PullWagonShop();
        TendStoreFronts();
        TendHallFronts();
        TendGrove();
        CollectOrders();
        SyncOutlaw();
        BeginRaidIfDue();
    }

    /// <summary>
    /// Companions standing together have their plates stacked, nearest the camera lowest, so one's name is never
    /// printed over another's.
    /// </summary>
    void StackPlates()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera is null || _companions.Count < 2) return;
        var placed = new List<(Vector3 At, float Lift)>();
        foreach (var c in _companions.Where(c => IsInstanceValid(c) && c.Visible).OrderBy(c => c.GlobalPosition.DistanceSquaredTo(camera.GlobalPosition)))
        {
            c.LiftPlate(0);
            var at = c.PlateAt;
            var lift = 0f;
            // Another plate already drawn about here: go over it, and over whatever went over it.
            while (placed.Any(p => new Vector2(p.At.X - at.X, p.At.Z - at.Z).Length() < 2.4f && Mathf.Abs(p.At.Y + p.Lift - (at.Y + lift)) < 0.75f))
                lift += 0.8f;
            if (lift > 0) c.LiftPlate(lift);
            placed.Add((at, lift));
        }
    }

    /// <summary>Mounts past the number of riders go to the Gallows Perch.</summary>
    void StableSpareMounts()
    {
        foreach (var spare in PartyRules.SpareMounts(_bonds, _player.Mount?.Bond).ToList())
        {
            spare.Following = false;
            if (BodyOf(spare) is { } body) { _companions.Remove(body); body.QueueFree(); }
            _hud.Log($"{spare.Name} goes back to {WaitsAt}: no rider for {spare.Name} just now.");
        }
    }

    readonly List<Bond> _bonds = [];
    readonly List<Enemy> _companions = [];
    DragonSaga _saga = new();
    Pickup? _nest;
    bool _nestRaided;
    readonly HashSet<string> _hungerSaid = [];
    readonly Rng _companyRng = new((ulong)Random.Shared.NextInt64());
    double _regather = 2, _agedAt = 30;

    public IReadOnlyList<Bond> Bonds => _bonds;
    public IEnumerable<Person> Locals => _townsfolk.Where(IsInstanceValid);
    public Pickup? Nest => _nest;

    /// <summary>Rests now (T), and climbs on or off your dragon (V): for the self-test.</summary>
    public void RestNow() => Rest();
    public void RideNow() => ToggleRide();
    public IEnumerable<Enemy> CompanionBodies => _companions.Where(IsInstanceValid);
    public DragonSaga Saga => _saga;

    static readonly string[] PetNames =
    [
        "Bramble", "Ash", "Ember", "Pip", "Thistle", "Rook", "Moss", "Sable", "Juniper", "Flint", "Nettle", "Bracken",
        "Wisp", "Clover", "Tansy", "Briar", "Cinder", "Sorrel", "Hazel", "Burr", "Pebble", "Sleet", "Tamsin", "Gorse",
    ];

    void SetUpCompany()
    {
        Enemy.Slain += OnSlain;
        Enemy.RanOff += OnRanOff;
        Enemy.Felled += OnFelled;
        Enemy.Turned += OnTurned;
        Enemy.SparEnded += OnSparEnded;
        Enemy.Slain += OnDrifterSlain;
        _player.WentDown += OnDrifterWin;
        Enemy.Slain += OnUndyingSlain;
        Enemy.Slain += OnWarlordSlain;
        _player.WentDown += OnUndyingWin;
        Enemy.Slain += OnDeepSlain;
        Enemy.Slain += OnSyndicateSlain;
        _player.WentDown += OnDeepWin;
        _player.Summoned += Summon;
        _player.Merged += Merge;
        _player.Overdriven += Overdrive;
        foreach (var spare in PartyRules.SpareMounts(_bonds)) spare.Following = false;
        foreach (var bond in _bonds.Where(b => b.Following)) BringCompanion(bond);
    }

    void TearDownCompany()
    {
        Enemy.Slain -= OnSlain;
        Enemy.RanOff -= OnRanOff;
        Enemy.Felled -= OnFelled;
        Enemy.Turned -= OnTurned;
        Enemy.SparEnded -= OnSparEnded;
        Enemy.Slain -= OnDrifterSlain;
        _player.WentDown -= OnDrifterWin;
        Enemy.Slain -= OnUndyingSlain;
        Enemy.Slain -= OnWarlordSlain;
        _player.WentDown -= OnUndyingWin;
        Enemy.Slain -= OnDeepSlain;
        Enemy.Slain -= OnSyndicateSlain;
        _player.WentDown -= OnDeepWin;
    }

    // ---- companions ----------------------------------------------------------------------------

    Enemy BringCompanion(Bond bond, Vector3? at = null)
    {
        var spot = at ?? _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 3 + new Vector3((float)GD.RandRange(-2, 2), 0, 0);
        var e = Enemy.Create(Bestiary.Get(bond.KindId), spot with { Y = City.FloorAt(spot) + 0.5f }, bond: bond, look: bond.Machine ? $"golem:{GolemName(_player.Class.Id)}" : null);
        e.Leader = _player;
        AddEnemy(e);
        _companions.Add(e);
        return e;
    }

    Enemy? BodyOf(Bond bond) => CompanionBodies.FirstOrDefault(e => e.Bond == bond);

    /// <summary>Your dragon, if you have one.</summary>
    public Bond? Dragon => _bonds.FirstOrDefault(b => b.Role == BondRole.Dragon);

    /// <summary>A warlock's familiar answers the call for a while.</summary>
    void Summon(double seconds)
    {
        var at = _player.GlobalPosition - _player.GlobalTransform.Basis.Z * 2;
        var spirit = _player.Class.Id == "Spiritcaller";
        var familiar = Enemy.Create(Bestiary.Get("familiar"), at with { Y = City.FloorAt(at) + 0.3f }, look: spirit ? "spirit_beast" : "familiar");
        if (spirit) familiar.Stats.Name = "Spectral Beast";
        familiar.Leader = _player;
        familiar.Expires = seconds;
        familiar.RefreshPlate();
        AddEnemy(familiar);
        Effects.Burst(this, at + Vector3.Up, DamageKind.Shock, 2);
        _hud.Toast(spirit ? "A pale shape pads out of the air and stands beside you." : "A familiar steps out of the dark to fight beside you.");
    }

    /// <summary>A dragon has turned to the Rift: news if it's near, and a warning if it was yours.</summary>
    void OnTurned(Enemy e)
    {
        if (e.Bond is { } bond && _bonds.Contains(bond))
        {
            _hud.Toast($"The Rift has taken {bond.Name}! Mad with it, {bond.Name} turns on you. Beat {bond.Name} down to break its hold.", 12);
            return;
        }
        if (e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 400)
            _hud.Toast($"The Rift takes the {Bestiary.Get(Dragons.KindFor(Dragons.ColourOf(e.Kind.Id)!.Value)).Name.ToLowerInvariant()}: {e.Stats.Name} flies into a frenzy!", 8);
    }

    /// <summary>A companion was knocked down. A dragon too young to fly doesn't get up again.</summary>
    void OnFelled(Enemy e)
    {
        if (e.Bond is not { } bond || !_bonds.Contains(bond)) return;
        if (bond.Rotted && e.Leader is null)
        {
            e.Subdue();
            _hud.Toast($"{bond.Name} goes down, and the rot lets go a little. It'll follow you now, but only a druid can get it out.", 9);
            return;
        }
        if (bond.Blighted && !bond.Rotted && !bond.DiesIfFelled)
        {
            e.Rot(_player);
            _hud.Toast($"{bond.Name} fell with the rot in its blood, and something else gets up. It doesn't know you. Knock it out and get it to a druid!", 10);
            return;
        }
        if (e.Cleanse())
        {
            // Beaten down by its own hero, a turned dragon is freed, whatever its age.
            _hud.Toast($"The Rift's hold on {bond.Name} breaks. {bond.Name} will get up when the fighting is over, yours again.", 10);
            return;
        }
        if (!bond.DiesIfFelled)
        {
            _hud.Toast($"{bond.Name} is down! {bond.Name} will get up when the fighting is over.");
            return;
        }
        _bonds.Remove(bond);
        _companions.Remove(e);
        GetTree().CreateTimer(8).Timeout += () => { if (IsInstanceValid(e)) e.QueueFree(); };
        if (bond.Role == BondRole.Dragon && _saga.Stage == SagaStage.Raising)
        {
            _saga.Lose();
            _hud.Toast($"{bond.Name} is dead. Too young, too small. Ser Aldric will find you another egg, if you have the heart for it.", 12);
        }
        else if (PartyRules.IsApprentice(bond))
        {
            _hud.Toast($"{bond.Name} is dead. They followed you here, and they don't get up.", 12);
            _hud.Log($"{bond.Name}, your {(bond.Role == BondRole.Squire ? "squire" : "apprentice")}, fell in battle.");
            StableSpareMounts();
        }
        else if (bond.Role == BondRole.Mite) _hud.Log($"{bond.Name} is scrap.");
        else _hud.Toast($"{bond.Name} is dead.", 10);
        Save();
    }

    /// <summary>Apprentices grow old with the days, and in the end, like anyone, they die of it.</summary>
    void AgeApprentices()
    {
        foreach (var bond in _bonds.Where(PartyRules.IsApprentice).ToList())
        {
            if (bond.JoinAge <= 0) (bond.JoinAge, bond.JoinDay) = (15, _city.Day); // taken on before they kept count
            var age = bond.JoinAge + Math.Max(0, _city.Day - bond.JoinDay) / (double)Calendar.DaysPerYear;
            if (age < Ageing.Of(bond.Race).Dies) continue;
            if (BodyOf(bond) is { } body) { _companions.Remove(body); body.QueueFree(); }
            _bonds.Remove(bond);
            _hud.Toast($"{bond.Name} dies in their sleep, old, at {age:0}. They were with you a long road.", 12);
            _hud.Log($"{bond.Name} died of old age.");
            StableSpareMounts();
            Save();
        }
    }

    /// <summary>
    /// An artificer's golem: built the day they join the Foundry, as grown as they are, and never fed. It isn't a
    /// pet: it's a machine, it levels with its maker, and it stands down if they leave the calling.
    /// </summary>
    void TendGolem()
    {
        var golems = _bonds.Where(b => b.Role == BondRole.Golem).ToList();
        var id = _player.Class.Id;
        foreach (var mite in _bonds.Where(b => b.Role == BondRole.Mite && id != "Saboteur").ToList()) // a saboteur's work, and nobody else can keep it wound
        {
            if (BodyOf(mite) is { } body) { _companions.Remove(body); body.QueueFree(); }
            _bonds.Remove(mite);
            _hud.Toast($"{mite.Name} runs down: a mite needs a saboteur's hand on the key.", 6);
        }
        var wanted = PartyRules.Constructs(_player.Class.Calling, id, _player.Hero.Level);
        foreach (var spare in golems.Skip(wanted).ToList())
        {
            if (BodyOf(spare) is { } body) { _companions.Remove(body); body.QueueFree(); }
            _bonds.Remove(spare);
            golems.Remove(spare);
            _hud.Toast(wanted > 0 ? $"{spare.Name} stays in the Foundry yard: an aeronaut keeps one hull, and climbs inside it." : $"{spare.Name} winds down and stands in the Foundry yard: a golem follows an artificer.", 8);
        }
        if (wanted == 0) return;
        var level = _player.Hero.Level + (id == "EtherAeronaut" ? 2 : 0); // a tinker's golem is sturdier
        var build = Bond.GolemBuild(id); // and a hull you can climb inside is built bigger
        for (var i = golems.Count; i < wanted; i++)
        {
            var made = new Bond { KindId = "golem", Name = i == 0 ? GolemName(id) : SecondGolemName(id), Role = BondRole.Golem, Level = level, Build = build, Following = true };
            _bonds.Add(made);
            golems.Add(made);
            BringCompanion(made);
            _hud.Toast(i > 0 ? $"{made.Name}, your second construct, stamps out of the Foundry yard after {golems[0].Name}. Nobody but an artificer has one; nobody but you has two."
                : build > 1 ? $"{made.Name}, your golem, stamps out of the Foundry yard after you: a hull of brass with room inside for you, head and shoulders over the Foundry's other work. It fights beside you, and grows as you do."
                : $"{made.Name}, your golem, stamps out of the Foundry yard after you. It fights beside you, and grows as you do.", 10);
        }
        for (var i = 0; i < golems.Count; i++)
        {
            var golem = golems[i];
            var name = i == 0 ? GolemName(id) : SecondGolemName(id);
            var refit = !golem.Renamed && golem.Name != name;
            if ((_worn is not null && BodyOf(golem) == _worn) || (golem.Level == level && golem.Build == build && !refit)) continue; // refitted once you've stepped out, never round you
            if (refit) // another artificer's way: the Foundry refits the machine to its design
            {
                _hud.Toast($"The Foundry refits {golem.Name} to a {_player.Class.Name}'s design: it's {name} now.", 8);
                golem.Name = name;
            }
            (golem.Level, golem.Build) = (level, build);
            if (BodyOf(golem) is { } body && body.State != EnemyState.Dead && golem.Following)
            {
                var at = body.GlobalPosition;
                _companions.Remove(body);
                body.QueueFree();
                BringCompanion(golem, at);
            }
        }
    }

    /// <summary>A Saboteur's mite, forged by hand: the makings and a log go on the fire, and it walks off the anvil to join the rest.</summary>
    void ForgeMite(Action back)
    {
        var pack = _player.Pack;
        var fuel = Recipes.Fuels.FirstOrDefault(f => pack.Count(f) > 0);
        if (fuel is null) { Sound.Ui("error"); _hud.Toast("A forge won't get hot on nothing: you need a log to burn.", 5); return; }
        if (!pack.Has(PartyRules.MiteNeeds)) { Sound.Ui("error"); _hud.Toast("You don't have the makings for a mite.", 4); return; }
        foreach (var (item, n) in PartyRules.MiteNeeds) pack.Take(item, n);
        pack.Take(fuel);
        var mite = new Bond { KindId = "clockwork_mite", Name = PartyRules.MiteName(_trade.MitesMade++), Role = BondRole.Mite, Level = _player.Hero.Level, Following = true };
        _bonds.Add(mite);
        BringCompanion(mite);
        Sound.Ui("anvil");
        _hud.Toast($"You wind {mite.Name} and set it down. It ticks off after the others.", 5);
        Save();
        back();
    }

    /// <summary>A second construct's name, of the same design as the first.</summary>
    static string SecondGolemName(string discipline) => discipline switch
    {
        "Alchemist" => "Retort",
        _ => "Rivet",
    };

    static string GolemName(string discipline) => discipline switch
    {
        "Alchemist" => "Alembic",
        "EtherAeronaut" => "Ballast",
        _ => "Scrap",
    };

    /// <summary>
    /// An ether-aeronaut climbs inside their golem and drives it: for the while, the golem's body is the hero's own
    /// (you walk, swing and cast as the machine, at its size), the hero's body is inside it, and the golem that
    /// followed you is where you are. Every blow lands on the machine and not on you, so it's the machine's own health that
    /// runs down. When the engine winds down you step out, and it stands beside you again; if it's beaten to scrap first,
    /// it throws you clear, unhurt, and lies where it fell until the fighting's done.
    /// </summary>
    void Merge(double seconds)
    {
        if (_bonds.FirstOrDefault(b => b.Role == BondRole.Golem) is not { } golem || BodyOf(golem) is not { } body) return;
        if (_player.Possessing) return; // already inside it: a second press mustn't hide the machine again with no way back out
        if (body.State == EnemyState.Dead) { _hud.Toast($"{golem.Name} is down, and there's nothing to climb into.", 4); return; }
        body.Visible = false;
        body.ProcessMode = ProcessModeEnum.Disabled; // the machine is you now
        var size = (float)Math.Max(1.25, body.Figure.Scale.X * 1.12);
        _worn = body;
        _player.Hull = (damage, kind, attack, crit, from) =>
        {
            body.Stats.TakeHit(damage, kind, attack, crit, from.Stats);
            if (body.Stats.Dead) _player.Unpossess();
        };
        _player.Possess(() => { var shell = Clockwork.For(golem.Name); shell.Scale = Vector3.One * (float)golem.Size; return shell; }, size, seconds, () =>
        {
            _player.Hull = null;
            _worn = null;
            if (!IsInstanceValid(body)) return;
            body.ProcessMode = ProcessModeEnum.Inherit;
            body.Visible = true;
            body.GlobalPosition = _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 1.5f;
            if (body.Stats.Dead)
            {
                body.Collapse();
                _hud.Toast($"{golem.Name}'s plates burst open and throw you clear. You're unhurt, but the machine's done for this fight.", 6);
            }
            else _hud.Toast($"The engine winds down and the plates open: you step out of {golem.Name}, and it stands beside you again.", 5);
        });
        _hud.Toast($"You climb inside {golem.Name} and the plates close round you: you drive the machine now, its weight behind every blow and its brass between you and theirs.", 7);
    }

    Enemy? _worn;

    /// <summary>The golem the hero's inside, for the tracker.</summary>
    public Enemy? WornGolem => _worn;

    /// <summary>The engine opened wide: the golem fights harder, and a ship under the hero surges ahead.</summary>
    void Overdrive(double seconds)
    {
        if (_bonds.FirstOrDefault(b => b.Role == BondRole.Golem) is { } golem && BodyOf(golem) is { } body) body.Empower(seconds, 0.3);
        _atSea?.Surge(seconds);
    }

    /// <summary>Time passes for the company: beasts and dragons get hungry, and starve if never fed.</summary>
    readonly Dictionary<string, double> _studiedWith = [];

    /// <summary>A shapeshifter learns a beast's shape by travelling with one too: every so long at the heels of a following beast counts as a study of it.</summary>
    void StudyCompanions(double delta)
    {
        if (_player.Class.Id != "Shapeshifter") return;
        foreach (var kind in _bonds.Where(b => b.Following && b.Role == BondRole.Pet && ForestBook.Beasts.Contains(b.KindId) && !_forest.Shapes.Contains(b.KindId)).Select(b => b.KindId).Distinct())
        {
            _studiedWith[kind] = _studiedWith.GetValueOrDefault(kind) + delta;
            if (_studiedWith[kind] < StudySeconds) continue;
            _studiedWith[kind] = 0;
            if (_forest.Study(kind) is { } shape) { _hud.Toast($"You've watched your {Bestiary.Get(kind).Name.ToLowerInvariant()} long enough to know it from the inside: {shape} is yours (Beast Form).", 9); Save(); }
            else _hud.Log($"You learn something of the {Bestiary.Get(kind).Name.ToLowerInvariant()} from yours ({_forest.Studied.GetValueOrDefault(kind)}/{ForestBook.StudyKills}).");
        }
    }

    /// <summary>How long at a following beast's side counts as one study of it, against one kill.</summary>
    public const double StudySeconds = 45;

    void TendCompany(double delta)
    {
        TendGolem();
        Regather(delta);
        StudyCompanions(delta);
        if ((_agedAt -= delta) <= 0) { _agedAt = 30; AgeApprentices(); HomeWork(30); }
        if (_player.Form is { } worn) _forest.Worn[worn] = _forest.Worn.GetValueOrDefault(worn) + delta;
        foreach (var bond in _bonds.Where(b => b.Following && b.Eats))
        {
            var body = BodyOf(bond);
            if (body is null || body.State == EnemyState.Dead) continue;
            var wasHungry = bond.Hungry;
            bond.Tick(delta);
            if (bond.Hungry && !wasHungry && _hungerSaid.Add(bond.Id)) _hud.Toast($"{bond.Name} is hungry. Feed {bond.Name} from the party menu (P): beasts you hunt give meat.", 9);
            if (bond.Starving)
            {
                body.Stats.Lose(body.Stats.MaxHealth * 0.006 * delta);
                if (body.Stats.Health <= 1) body.Collapse();
            }
        }
        foreach (var body in CompanionBodies.Where(b => b.Bond is not null)) body.Bond!.Health = body.State == EnemyState.Dead ? 0.4 : body.Stats.Health / body.Stats.MaxHealth;

        // The road to Dragon Knight: the egg keeps warm as you carry it, and hatches.
        if (_saga.Warm(delta, resting: false)) Hatch();
        if (_saga.Stage == SagaStage.SeekEgg) TendNest();
    }

    /// <summary>
    /// Nobody gets left behind. Every so often, a companion that should be following is looked for: one whose body is
    /// gone is brought back to your heels; one hidden and stopped with no hero inside it is woken; one far behind you is
    /// brought up; and one knocked out and left far back gets up and comes on, once the fighting's over.
    /// </summary>
    void Regather(double delta)
    {
        if ((_regather -= delta) > 0) return;
        _regather = 2;
        foreach (var bond in _bonds.Where(b => b.Following).ToList())
        {
            var body = BodyOf(bond);
            if (body is null) { BringCompanion(bond); _hud.Log($"{bond.Name} finds you again."); continue; }
            if (body == _worn || body.Leader != _player || body.Carried || body == _player.Mount) continue;
            if (body.ProcessMode == ProcessModeEnum.Disabled || !body.Visible) { body.ProcessMode = ProcessModeEnum.Inherit; body.Visible = true; }
            var far = body.GlobalPosition.DistanceTo(_player.GlobalPosition);
            var down = body.State == EnemyState.Dead;
            if (down ? far < 60 || _player.InFight : far < 120) continue;
            if (down) body.Revive();
            var at = _player.GlobalPosition + _player.GlobalTransform.Basis.Z * (4 + 2 * body.Figure.Scale.X);
            body.GlobalPosition = at with { Y = City.FloorAt(at) + 0.5f };
            body.ResetPhysicsInterpolation();
            _hud.Log($"{bond.Name} catches you up.");
        }
    }

    public void Feed(Bond bond)
    {
        if (_player.Meat <= 0) { _hud.Toast("You have no meat. Hunt something: deer, boar, anything wild."); return; }
        _player.Meat--;
        bond.Feed();
        if (_player.Pack.Practise(Job.Taming, 3) is { } level) _hud.Toast($"Beast taming rises to {level}!", 4);
        _hungerSaid.Remove(bond.Id);
        BodyOf(bond)?.RefreshPlate();
        _hud.Toast($"{bond.Name} eats, and looks at you for more.");
    }

    // ---- kills and experience --------------------------------------------------------------------

    /// <summary>Somebody's cow, sheep or dog, killed where they could see: it's paid for, and Tristinwold hears of it.</summary>
    void Poached(Enemy victim, string owner)
    {
        if (Poaching(victim)) return;
        var fine = Math.Min(_player.Crowns, Livestock.Fine(victim.Kind.Id));
        _player.Crowns -= fine;
        if (owner == "Tristinwold") _city.Renown = Math.Max(0, _city.Renown - 2);
        _standing.Evil(1, $"killed somebody's {victim.Kind.Name.ToLowerInvariant()}");
        Sound.Ui("error");
        _hud.Toast($"That was somebody's {victim.Kind.Name.ToLowerInvariant()}. The folk of {owner} make you pay for it: {fine} crowns.", 5);
    }


    /// <summary>A deviant cut down gets up and runs: the hero hears it, and if it gets clear the shared world knows it ran.</summary>
    void OnRanOff(Enemy runner, bool clear)
    {
        if (!clear) { _hud.Toast($"{runner.Stats.Name} isn't finished. It staggers up and runs! Catch it before it gets clear.", 6); return; }
        _tracker.RanOff(runner.Id, runner.LastBlow);
        _hud.Toast($"{runner.Stats.Name} got away. It'll be back.", 6);
        _hud.Log($"Deviant: {runner.Stats.Name} was cut down, and got up and ran.");
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim.DeviantId is { } deviant) // whoever struck the blow, a pet, a golem, a spell: the shared world hears it's dead, not that it got away
        {
            _tracker.Engaged(victim.Id, victim.Kind.Id, victim.Kind.Name, victim.Stats.MaxHealth, ToNumerics(victim.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, deviant);
            _tracker.Died(victim.Id, byCompany ? _player.Company : null, _clock);
            Callable.From(Save).CallDeferred();
        }
        if (victim.Bond is not null || !byCompany) return;
        if (victim.SiteId is { } site)
        {
            foreach (var u in _journal.Killed(site).ToList()) Apply(u);
            _places.KilledAt(site);
        }
        if (victim.Leader is not null || victim.Friendly) return;
        StandingSlain(victim);
        if (victim.Owner is { } owner) Poached(victim, owner);
        StorySlain(victim);
        _player.Killed(victim);
        Spoils(victim);
        var xp = Math.Max(3, (int)Math.Round(victim.Stats.MaxHealth / 6 * (victim.DeviantId is null ? 1 : 2)));
        GainXp(xp);
        var meat = victim.Kind.Id switch
        {
            "deer" or "boar" or "frost_troll" or "horse" or "wild_horse" or "cow" => 2,
            "bear" => 3,
            "wolf" or "ice_wolf" or "fox" or "giant_rat" or "mud_crab" or "goblin" => 1,
            _ when victim.IsDragon => 4,
            _ => 0,
        };
        if (victim.Young) meat = Math.Min(meat, 1);
        if (meat > 0) { _player.Meat += meat; _hud.Log($"+{meat} meat ({_player.Meat})"); }
        if (_player.Class.Id == "Thief" && victim.Kind.Height > 1.2)
        {
            var coin = (int)Math.Round((1 + _companyRng.Next(4)) * _player.Hero.Power("light_fingers"));
            _player.Crowns += coin;
            _hud.Log($"+{coin} crowns, lifted from the body");
        }
    }

    /// <summary>Experience for the hero and every companion with them.</summary>
    public void GainXp(int xp)
    {
        var hero = _player.Hero;
        if (hero.Gain((int)Math.Round(xp * _player.Race.Xp * (WellRested ? Housing.RestedXp : 1))) > 0)
        {
            _player.Fit();
            _player.Stats.Heal(_player.Stats.MaxHealth);
            Sound.Ui("levelup");
            foreach (var a in Advances.Between(_player.Class.Id, hero.Level - 1, hero.Level)) _hud.Toast($"New ability: {a.Move.Name}. {a.Move.Text} Open your skills (K) to see it.", 10);
            _hud.Toast($"Level {hero.Level}! You have {hero.Points} skill point{(hero.Points == 1 ? "" : "s")} to spend (K).", 8);
        }
        var beastmaster = _player.Class.Id == "Ranger" ? 1 + _player.Class.Passive.Power * _player.Hero.Power("beastmaster") : 1;
        foreach (var body in CompanionBodies.Where(c => c.Bond is not null && c.Leader == _player && c.GlobalPosition.DistanceTo(_player.GlobalPosition) < 80))
        {
            var bond = body.Bond!;
            var before = bond.Level;
            var training = bond.Role is BondRole.Pet or BondRole.Dragon ? beastmaster * Taming.Training(_player.Pack.Level(Job.Taming)) : 1;
            if (bond.Gain((int)Math.Round(xp * 0.8 * training)) == 0) continue;
            if (bond.Role is BondRole.Pet or BondRole.Dragon && _player.Pack.Practise(Job.Taming, 10) is { } tamer) _hud.Toast($"Beast taming rises to {tamer}!", 4);
            body.Grew();
            var learned = bond.AllSkills.Where(s => s.Level > before && s.Level <= bond.Level).Select(s => s.Name).ToList();
            _hud.Toast($"{bond.Name} is now level {bond.Level}{(learned.Count > 0 ? $" and has learned {string.Join(", ", learned)}" : "")}.", 8);
            if (bond.Role == BondRole.Dragon && bond.Level == 10)
                _hud.Toast(_saga.Stage == SagaStage.Raising
                    ? $"{bond.Name} is big enough to carry you. Take {bond.Name} to {DragonSaga.Knight} at the rookery."
                    : $"{bond.Name} is big enough to carry you (V).", 10);
        }
    }

    // ---- taming and recruiting -----------------------------------------------------------------

    Enemy? YounglingNear(Vector3 pos) =>
        Enemies().Where(e => e.Young && e.State != EnemyState.Dead && Taming.Tameable(e.Kind.Id) && e.GlobalPosition.DistanceTo(pos) < 3.5f + e.Figure.Scale.X)
            .MinBy(e => e.GlobalPosition.DistanceTo(pos));

    /// <summary>
    /// Tries to win a youngling over. Its kin won't allow it while one of them is close by (they come for you); a
    /// little meat helps; a ranger never fails. Won over, it follows you, and grows up at your side.
    /// </summary>
    readonly Dictionary<ulong, double> _spurned = [];

    /// <summary>For the self-test: a companion let go, as if it had never been.</summary>
    public void ForgetForTest(Bond bond)
    {
        if (BodyOf(bond) is { } body) { _companions.Remove(body); body.QueueFree(); }
        _bonds.Remove(bond);
    }

    public bool Tame(Enemy young)
    {
        var kin = Enemies().Where(o => o != young && !o.Young && o.Kind.Id == young.Kind.Id && o.State != EnemyState.Dead
            && o.GlobalPosition.DistanceTo(young.GlobalPosition) < 25).ToList();
        var chance = Taming.Chance(kin.Count > 0, _player.Meat > 0, _player.Class.Id == "Ranger", _player.Hero.Level, _player.Pack.Level(Job.Taming), young.IsDragon);
        if (kin.Count > 0)
        {
            _hud.Toast($"The {young.Stats.Name}'s kin are close, and they won't let you near it.");
            foreach (var k in kin) k.Provoke(_player);
            return false;
        }
        if (young.IsDragon)
        {
            // A wyrmling is clever: it weighs you, and if it finds you wanting it remembers, and screams for its kin.
            if (_spurned.GetValueOrDefault(young.GetInstanceId()) > _clock) { _hud.Toast($"The {young.Stats.Name} remembers you, and turns its back.", 5); return false; }
            if (_player.Hero.Level < Taming.DragonLevel) { _hud.Toast($"The {young.Stats.Name} looks through you as if you weren't there. It won't so much as look at someone so green (level {Taming.DragonLevel}).", 7); return false; }
            if (!_companyRng.Chance(chance))
            {
                _spurned[young.GetInstanceId()] = _clock + Taming.DragonSpurns;
                foreach (var k in Enemies().Where(o => o.IsDragon && !o.Young && o.Kind.Id == young.Kind.Id && o.State != EnemyState.Dead && o.GlobalPosition.DistanceTo(young.GlobalPosition) < 250)) k.Provoke(_player);
                _hud.Toast($"The {young.Stats.Name} looks at you a long moment, and doesn't like what it sees. It screams for its kin. ({chance:P0} it would have taken you)", 8);
                return false;
            }
        }
        else if (!_companyRng.Chance(chance))
        {
            _hud.Toast($"The {young.Stats.Name} bolts.");
            young.Provoke(_player);
            return false;
        }
        if (_player.Meat > 0) _player.Meat--;
        var at = young.GlobalPosition;
        young.QueueFree();
        var bond = Adopt(young.Kind.Id, young.IsDragon, at);
        var up = _player.Pack.Practise(Job.Taming, 40);
        _hud.Toast((bond.Following
            ? young.IsDragon ? $"The {young.Stats.Name} looks at you a long moment, and lowers its head to your hand. It has chosen you. You call it {bond.Name}." : $"The {young.Stats.Name} sniffs your hand and decides you'll do. You call it {bond.Name}."
            : $"The {young.Stats.Name} is yours: you call it {bond.Name}. With no room at your heels, it goes to wait at {WaitsAt}.")
            + (up is { } level ? $" Beast taming rises to {level}!" : ""), 9);
        Save();
        return true;
    }

    /// <summary>A new beast for the company: named, and at your heels if there's room, else waiting at the Gallows Perch.</summary>
    Bond Adopt(string kindId, bool dragon, Vector3 at, int level = 1)
    {
        var bond = new Bond
        {
            KindId = kindId, Role = dragon ? BondRole.Dragon : BondRole.Pet, Level = level,
            Name = dragon ? Dragons.NameFor(_companyRng) : _companyRng.Pick(PetNames.Where(n => _bonds.All(b => b.Name != n)).DefaultIfEmpty("Scrap").ToList()),
        };
        _bonds.Add(bond);
        bond.Following = MayFollow(bond);
        if (bond.Following) BringCompanion(bond, at);
        return bond;
    }

    /// <summary>Buys a young beast from Wren's Menagerie (never a dragon: the menagerie has none).</summary>
    public Bond? BuyPet(PetOffer offer)
    {
        var price = Menagerie.Price(offer, _player.Pack.Level(Job.Taming), _player.Markup);
        if (_player.Crowns < price) { _hud.Toast($"The {offer.Name.ToLowerInvariant()} costs {price} crowns; you have {_player.Crowns}.", 4); return null; }
        _player.Crowns -= price;
        var bond = Adopt(offer.KindId, dragon: false, _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 2);
        var up = _player.Pack.Practise(Job.Taming, 15);
        _hud.Toast((bond.Following
            ? $"Wren hands you the {offer.Name.ToLowerInvariant()}. It already knows its name: {bond.Name}."
            : $"The {offer.Name.ToLowerInvariant()} is yours ({bond.Name}). With no room at your heels, Wren sends it to wait at {WaitsAt}.")
            + (up is { } level ? $" Beast taming rises to {level}!" : ""), 8);
        Save();
        return bond;
    }

    /// <summary>What a youth would be to this hero: a warrior's squire, a mage's apprentice, a rogue's protege, a bard's understudy.</summary>
    (BondRole Role, string Kind, string Word) Follower => _player.Class.Calling switch
    {
        Calling.Warrior => (BondRole.Squire, "squire", "squire"),
        Calling.Mage => (BondRole.Apprentice, "apprentice", "apprentice"),
        Calling.Bard => (BondRole.Apprentice, "understudy", "understudy"),
        Calling.Artificer => (BondRole.Apprentice, "apprentice", "apprentice"),
        _ => (BondRole.Apprentice, "protege", "apprentice"),
    };

    public const int RecruitLevel = 3, RecruitFee = 25;

    /// <summary>A youth leaves home to follow the hero: a fee to the family, and the hero must have made a name.</summary>
    public bool Recruit(Person person)
    {
        var npc = person.Npc;
        var (role, kind, word) = Follower;
        if (_player.Hero.Level < RecruitLevel) { Say(person, $"You? You're nobody yet. Come back when you've made a name (level {RecruitLevel})."); return false; }
        if (!FreeApprentices && _bonds.Count(PartyRules.IsApprentice) >= PartyRules.MaxApprentices) { Say(person, $"You've {PartyRules.MaxApprentices} already. Who'd teach me?"); return false; }
        if (_player.Crowns < RecruitFee) { Say(person, $"My family can't spare me for nothing. {RecruitFee} crowns, for their trouble."); return false; }
        _player.Crowns -= RecruitFee;
        var bond = new Bond { Id = npc.Id, KindId = kind, Role = role, Name = npc.First, Race = npc.Race, JoinAge = npc.Age, JoinDay = _city.Day };
        _bonds.Add(bond);
        bond.Following = MayFollow(bond);
        var at = person.GlobalPosition;
        person.QueueFree();
        if (bond.Following) BringCompanion(bond, at);
        _hud.EndTalk();
        _hud.Toast($"{npc.Name} becomes your {word}. {RecruitFee} crowns go to {npc.Their} family.", 8);
        Save();
        return true;
    }

    // ---- the guild masters -----------------------------------------------------------------------

    void AddGuildMasters()
    {
        Townsfolk(new Npc("eyrie/aldric", DragonSaga.Knight, Sex.Male, 0, "knight-commander of the Order of the Eyrie", "city:tristinwold", "Tristinwold",
            "Ser Aldric has ridden Auriel, a gold dragon older than the city walls, for thirty years. He keeps the Order's rookery on the hill west of Tristinwold, and he is looking for people worth trusting with an egg.",
            ["the stars", "old battles"], "proud", "Mind the dragons. They mind you.", DragonSaga.RookeryX + 8, DragonSaga.RookeryZ + 6));
    }

    /// <summary>The extra things you can say to someone, beyond the usual: join a guild, take on a youth, the Dragon Knight's road.</summary>
    IEnumerable<(string, Action)> MoreTalk(Person person)
    {
        var npc = person.Npc;
        if (npc.Id.StartsWith("drifter:"))
        {
            foreach (var o in DrifterTalk(person)) yield return o;
            yield break;
        }
        if (npc.Id.StartsWith("syndicate:"))
        {
            foreach (var o in SyndicateTalk(person)) yield return o;
            yield break;
        }
        if (npc.Id.StartsWith("deep:"))
        {
            foreach (var o in DeepTalk(person)) yield return o;
            yield break;
        }
        if (npc.Id.StartsWith("undying:"))
        {
            foreach (var o in PrideTalk(person)) yield return o;
            yield break;
        }
        foreach (var o in GuildTalk(person)) yield return o;
        if (People.IsYouth(npc))
        {
            var word = Follower.Word;
            yield return ($"Would you become my {word}? ({RecruitFee} crowns to your family)", () => Recruit(person));
        }
        if (npc.Id == "eyrie/aldric") foreach (var o in Aldric(person)) yield return o;
        foreach (var o in StoryTalk(person)) yield return o;
        foreach (var o in CaravanTalk(person)) yield return o;
        foreach (var o in SeaTalk(person)) yield return o;
        foreach (var o in IsleTalk(person)) yield return o;
        foreach (var o in StandingTalk(person)) yield return o;
        foreach (var o in ArtificerTalk(person)) yield return o;
        foreach (var o in ShadowTalk(person)) yield return o;
        foreach (var o in MineTalk(person)) yield return o;
        foreach (var o in ForestTalk(person)) yield return o;
        foreach (var o in CouncilTalk(person)) yield return o;
        foreach (var o in HireTalk(person)) yield return o;
        foreach (var o in ShaftTalk(person)) yield return o;
        foreach (var o in SteamTalk(person)) yield return o;
        foreach (var o in StoreTalk(person)) yield return o;
        foreach (var o in HallTalk(person)) yield return o;
        foreach (var o in EngineTalk(person)) yield return o;
        foreach (var o in CarpentryTalk(person)) yield return o;
        foreach (var o in SailTalk(person)) yield return o;
        foreach (var o in OutlawTalk(person)) yield return o;
        foreach (var o in IsleEngineTalk(person)) yield return o;
        foreach (var o in ShopTalk(person)) yield return o;
        foreach (var o in ForgeTalk(person)) yield return o;
        foreach (var o in TownTalk(person)) yield return o;
        foreach (var o in PursuitTalk(person)) yield return o;
    }

    void Guild(Person person, Calling calling)
    {
        var npc = person.Npc;
        var options = Classes.Of(calling).Select(d => ($"{d.Name}: {d.Text}", (Action)(() =>
        {
            if (_player.Class.Id == d.Id) { Say(person, "You already walk that path."); return; }
            _player.Join(d.Id);
            _hud.Toast($"You are now a {d.Name}. Your skill points come back to you (K).", 9);
            Save();
            Say(person, calling switch
            {
                Calling.Warrior => "Good. Now hit me. Harder than that.",
                Calling.Rogue => "Welcome to the Knives. You never met me.",
                Calling.Bard => "Welcome to the College. Now: from the top, and this time, louder.",
                Calling.Artificer => "Mind the acid. Your golem's being wound in the yard; it'll follow you out.",
                _ => "Then read. And when you've read everything, read it again.",
            });
        }))).ToList();
        options.Add(("Not yet.", () => Say(person, "The door stays open.")));
        _hud.Talk(Who(npc), $"{Guilds.Of(calling).Name} teaches three paths. Which is yours?", [.. options]);
    }

    // ---- the road to Dragon Knight ----------------------------------------------------------------

    IEnumerable<(string, Action)> Aldric(Person person)
    {
        yield return ("Tell me about the Order.", () => Say(person,
            "The Order of the Eyrie is as old as the pacts. Where a metallic ancient keeps faith with a town, a knight and their dragon keep watch over it: against the chromatic hunters, and now against the things the Rift has made of dragons."));
        var dragon = Dragon;
        switch (_saga.Stage)
        {
            case not SagaStage.Knighted when _saga.ReadyToKnight(dragon):
                var wild = _saga.Stage != SagaStage.Raising;
                yield return ($"I've raised {dragon!.Name}{(wild ? ", from a wyrmling I won over in the wilds" : "")}.", () => _hud.Talk(Who(person.Npc),
                    wild ? $"From the wilds? Nobody wins a wyrmling over. Nobody but you, it seems. Look at {dragon.Name}: grown, and yours. Kneel." : $"So you have. Look at {dragon.Name}. Kneel.",
                    ("(Kneel.)", () =>
                    {
                        _saga.Knighted(dragon);
                        _hud.Toast($"You rise a Dragon Knight of the Eyrie. Stand by {dragon.Name} and press V to ride: Space climbs, C dives.", 14);
                        Save();
                        Say(person, $"Rise, knight. {dragon.Name} will carry you anywhere. Don't make me regret this.");
                    })));
                break;
            case SagaStage.NotStarted:
                yield return ("How do I become a Dragon Knight?", () =>
                {
                    if (DragonSaga.Matriarch(_world) is null) { Say(person, "There's no clutch to be had this year. Ask me again."); return; }
                    _hud.Talk(Who(person.Npc),
                        "Every knight of the Eyrie raised their dragon from the egg, and every one of them earned it. I don't hand out eggs to whoever walks up the hill. " +
                        $"Show me you can stand on your own feet out there (level {DragonSaga.MinLevel}), and show me you'll do the work: my dragons eat, and they eat a great deal. Bring me {DragonSaga.MeatDue} meat you've hunted yourself. " +
                        "Then we'll talk about where an egg might be found.",
                        ("I'll earn it.", () =>
                        {
                            _saga.Begin(_world);
                            _hud.Toast("New quest: the Dragon Knight's road. Prove yourself to Ser Aldric.");
                            Save();
                            Say(person, "Good. The dragons are hungry now.");
                        }),
                        ("Not yet.", () => Say(person, "The dragons will wait. They're good at it.")));
                });
                break;
            case SagaStage.Proving:
                if (_player.Meat > 0 && _saga.MeatGiven < DragonSaga.MeatDue)
                    yield return ($"Here's meat for the dragons. ({_player.Meat} in your pack)", () =>
                    {
                        var took = _saga.Feed(_player.Meat);
                        _player.Meat -= took;
                        Save();
                        Say(person, _saga.MeatGiven >= DragonSaga.MeatDue ? "That'll do them. They know your smell now." : $"{took} more. {DragonSaga.MeatDue - _saga.MeatGiven} to go. They're always hungry.");
                    });
                if (_saga.Proven(_player.Hero.Level))
                    yield return ("I've done what you asked.", () =>
                    {
                        _saga.Bless(_player.Hero.Level);
                        Save();
                        _hud.Talk(Who(person.Npc),
                            $"So you have. {_saga.MatriarchName} keeps a pact with people, and her clutch lies in her lair, somewhere {_saga.Hint} of here. " +
                            "I won't draw you a map and I won't fly you there: a knight who can't find a dragon won't keep one. Rift-corrupted drakes have been at her clutch. Bring back an egg, and keep it warm.",
                            ("I'll find it.", () => Say(person, "Then go.")));
                    });
                else
                    yield return ("What do you still need of me?", () => Say(person,
                        _player.Hero.Level < DragonSaga.MinLevel ? $"You're not ready. Come back at level {DragonSaga.MinLevel}, and with the meat." : $"Meat. {DragonSaga.MeatDue - _saga.MeatGiven} more."));
                break;
            case SagaStage.SeekEgg:
                yield return ("Where is the lair again?", () => Say(person, $"Somewhere {_saga.Hint} of here. {(_saga.FoundNest ? "You've found it, I hear. Then why are you here?" : "Look for her. You'll know her when you see her.")}"));
                if (_saga.Lost > 0) yield return ("I lost my dragon.", () => Say(person, "Then you know what it costs. Try again. The nest is where it was."));
                break;
            case SagaStage.CarryEgg:
                yield return ("About the egg...", () => Say(person, $"Keep it warm and keep it close. Rest when you can: it hatches fastest by a fire. ({_saga.Warmth:P0} warm.)"));
                break;
            case SagaStage.Raising:
                yield return ("How is my dragon?", () => Say(person, dragon is null ? "Where is it? Go and find it." :
                    $"{dragon.Name} is a {dragon.Stage}, level {dragon.Level}. Not yet. Level ten, and strong enough to carry you. And feed it: a hungry dragon is a weak one."));
                break;
            case SagaStage.Knighted:
                yield return ("Any word from the Eyrie?", () => Say(person, RaidNews()));
                break;
        }
    }

    string RaidNews()
    {
        var pos = _player.GlobalPosition;
        if (Dragons.RaidsNear(_world, _wilds.Now(), pos.X, pos.Z, 12000).FirstOrDefault() is { } raid && Dragons.PactsNear(_world, pos.X, pos.Z, 12000).FirstOrDefault(a => a.Id == raid.Foe) is { } pact)
            return $"{pact.Ward!.Name} is under attack: {Bestiary.Get(raid.KindId).Name}s, {Direction(pos, (raid.X, raid.Z))} of here. The Dragonwatch there could use another knight.";
        return "Quiet, for once. The Dragonwatch flies over every pact town; go and see them.";
    }

    /// <summary>The nest at the matriarch's lair: the egg waits there, and the first time you come, corrupted drakes are at it.</summary>
    void TendNest()
    {
        var nest = new Vector3((float)_saga.NestX, 0, (float)_saga.NestZ);
        var d = _player.GlobalPosition.DistanceTo(nest with { Y = _player.GlobalPosition.Y });
        if (!_saga.FoundNest && d < 300)
        {
            _saga.Found();
            _hud.Toast($"There: the lair of {_saga.MatriarchName}, and her nest. You found it yourself.", 8);
            Save();
        }
        if (_nest is null && d < 350 && _terrain.HasGround(nest))
        {
            _nest = new Pickup { QuestId = "saga:egg", Item = "Dragon egg", Egg = Bestiary.Get(Dragons.KindFor(_saga.Colour)).Colour };
            AddChild(_nest);
            _nest.GlobalPosition = nest with { Y = City.FloorAt(nest) };
        }
        else if (_nest is not null && d > 450) { _nest.QueueFree(); _nest = null; }
        if (_nest is not null && !_nestRaided && d < 220)
        {
            _nestRaided = true;
            var kind = Bestiary.Get(Dragons.CorruptedKindFor(_companyRng.Pick(Enum.GetValues<DragonColour>())));
            for (var i = 0; i < 2; i++)
            {
                var at = nest + new Vector3(18 * (i == 0 ? 1 : -1), 0, 12);
                var raider = Enemy.Create(kind, at with { Y = City.FloorAt(at) + 0.5f }, WorldGen.Danger(at.X, at.Z) * 0.6);
                raider.Faction = Dragons.Rift;
                AddEnemy(raider);
            }
            _hud.Toast($"{kind.Name}s are tearing at the nest!", 8);
        }
    }

    void Hatch()
    {
        var dragon = _saga.Hatch(_companyRng);
        _bonds.Add(dragon);
        dragon.Following = true;
        var body = BringCompanion(dragon);
        Effects.Burst(this, body.GlobalPosition + Vector3.Up * 0.5f, DamageKind.Sun, 1.5f);
        _hud.Toast($"The egg cracks. A {_saga.Colour.ToString().ToLowerInvariant()} hatchling the size of a cat blinks up at you and chirps. You name {dragon.Name}. Keep {dragon.Name} fed, and keep {dragon.Name} alive.", 14);
        Save();
    }

    /// <summary>Climbs onto your dragon, or down from it.</summary>
    void ToggleRide()
    {
        if (_player.Mount is not null)
        {
            if (_player.Mount.Flying && _player.GlobalPosition.Y - City.FloorAt(_player.GlobalPosition) > 4) { _hud.Toast("Land first (C to dive)."); return; }
            _player.Dismount();
            return;
        }
        var near = CompanionBodies.Where(e => e.Bond is { Role: BondRole.Dragon } or { Steed: true } && e.Leader == _player && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 4 + e.Figure.Scale.X * 4)
            .OrderByDescending(e => e.Bond!.Rideable).ThenBy(e => e.GlobalPosition.DistanceTo(_player.GlobalPosition)).FirstOrDefault();
        if (near is null) { _hud.Toast("Neither your dragon nor your horse is close enough to climb on."); return; }
        var bond = near.Bond!;
        if (!bond.Rideable) { _hud.Toast(bond.Steed ? $"{bond.Name} is only a foal. Raise {bond.Name} to level 6, and then ride." : $"{bond.Name} is too small to carry you yet (level 10)."); return; }
        if (bond.Role == BondRole.Dragon && _saga.Stage != SagaStage.Knighted) { _hud.Toast($"Only a knight of the Eyrie may ride. Take {bond.Name} to {DragonSaga.Knight} at the rookery."); return; }
        _player.Ride(near);
    }

    // ---- menus -----------------------------------------------------------------------------------

    /// <summary>The party (P): who's with you, how they're doing, feeding them, sending them home.</summary>
    public void ShowParty()
    {
        if (_bonds.Count == 0) { _hud.Talk("Your company", "No one follows you yet. Find a youngling alone in the wilds, or a youth in a town who dreams of adventure.", ("Close", _hud.EndTalk)); return; }
        var lines = _bonds.Select(b =>
        {
            var body = BodyOf(b);
            var kind = Bestiary.Get(b.KindId).Name.ToLowerInvariant();
            var state = !b.Following ? (b.Hitched ? "hitched to your wagon" : _pursuits.House.IsHome ? "at home" : "waiting at the Gallows Perch") : body is null ? "lost" : body.State == EnemyState.Dead ? "down" : $"{body.Stats.Health:0}/{body.Stats.MaxHealth:0} health";
            return $"{b.Name}: {b.Stage} {(b.Role is BondRole.Pet or BondRole.Dragon ? kind : "")}, level {b.Level} ({b.Xp}/{Bond.XpToNext(b.Level)} xp), {state}{(b.Eats ? $", {b.Fed:0}% fed" : "")}. Knows: {(b.Skills.Any() ? string.Join(", ", b.Skills.Select(s => s.Name)) : "nothing yet")}.";
        });
        var options = new List<(string, Action)>();
        foreach (var b in _bonds.Where(b => b.Following && b.Eats)) options.Add(($"Feed {b.Name} (meat: {_player.Meat})", () => { Feed(b); ShowParty(); }));
        foreach (var b in _bonds)
        {
            var bond = b;
            if (!PartyRules.IsApprentice(bond)) options.Add(($"Rename {bond.Name}", () => AskName(bond))); // a person comes with a name of their own
            // A grown horse with a wagon of yours is hitched to it or unhitched to ride, rather than sent home and called.
            if (bond.Steed && bond.Rideable && _trade.OwnWagon)
            {
                if (bond.Hitched) { if (MayFollow(bond)) options.Add(($"Unhitch {bond.Name} to ride", () => { Unhitch(bond); Save(); ShowParty(); })); continue; }
                if (_player.Mount?.Bond != bond && _bonds.Count(b => b.Hitched) < TradeBook.MaxHorses) options.Add(($"Hitch {bond.Name} to the wagon", () => { HitchToWagon(bond); Save(); ShowParty(); })); // what you're riding can't pull as well
                if (!bond.Following && MayFollow(bond)) options.Add(($"Call {bond.Name}", () => { bond.Following = true; BringCompanion(bond); Save(); ShowParty(); }));
                continue;
            }
            if (bond.Following) options.Add(($"Send {bond.Name} home", () => { bond.Following = false; var body = BodyOf(bond); if (body is not null) { _companions.Remove(body); body.QueueFree(); } StableSpareMounts(); Save(); ShowParty(); }));
            else if (MayFollow(bond)) options.Add(($"Call {bond.Name}", () => { bond.Following = true; BringCompanion(bond); Save(); ShowParty(); }));
        }
        options.Add(("Close", _hud.EndTalk));
        _hud.Talk("Your company", string.Join("\n", lines), [.. options]);
    }

    /// <summary>A name for a pet, a mount or a machine, typed in by the hero.</summary>
    void AskName(Bond bond)
    {
        var box = new LineEdit { Text = bond.Name, MaxLength = 24, SelectAllOnFocus = true };
        void Done() { if (Rename(bond, box.Text)) Save(); ShowParty(); }
        box.TextSubmitted += _ => Done();
        _hud.Talk($"Rename {bond.Name}", "What will you call them?", ("That's the name", Done), ("Leave it", ShowParty));
        _hud.AddToTalk(box);
        box.GrabFocus();
    }

    /// <summary>Gives a companion the name the hero chose, on its plate too. People already have theirs.</summary>
    public bool Rename(Bond bond, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || PartyRules.IsApprentice(bond)) return false;
        (bond.Name, bond.Renamed) = (name, true);
        if (BodyOf(bond) is { } body) { body.Stats.Name = name; body.RefreshPlate(); }
        return true;
    }

    /// <summary>The skills (K): the class's moves, their ranks, and points to spend.</summary>
    public void ShowSkills()
    {
        var hero = _player.Hero;
        var c = _player.Class;
        var titles = _guilds.Titles.Count > 0 ? $" {string.Join(", ", _guilds.Titles)}." : "";
        var text = $"Today is {Calendar.Date(_city.Day)}. You are {Ageing.Describe(hero, _player.Race.Id, _player.Race.Name, _city.Day)}.\n" +
            $"{c.Name}, level {hero.Level} ({hero.Xp}/{Hero.XpToNext(hero.Level)} xp), {_player.GuildTitle}.{titles} {hero.Points} skill point{(hero.Points == 1 ? "" : "s")} to spend: click a switch to raise an ability.";
        var more =
            $"Trades: {string.Join(", ", Enum.GetValues<Job>().Select(j => $"{TradeName(j)} {_player.Pack.Level(j)}"))}. The Exchange knows you as {_city.RenownTitle}. Feats: {_pursuits.Feats.Count} ({_pursuits.Points} points).\n" +
            $"Your trade as a {c.Name}, {Perks.Of(c.Id).Name}: {Perks.Of(c.Id).Text}\n{_standing.Summary()}" +
            (hero.Learned.Count > 0 ? $"\nWays you have learned (an immortal keeps them all): {string.Join(", ", hero.Classes.Select(id => Classes.Get(id).Name + (id == c.Id ? " (now)" : "")))}." : "");
        text += $"\nHometown: {_story.Home?.Name ?? "Tristinwold"}. {(_sea.OnACrew ? $"As one of a ship's people you wake on the quay at {WakeDock.Town}." : "The ring brings you back here.")}";
        var options = new List<(string, Action)>();
        options.Add(("My trades and standing", () => _hud.Talk("Trades and standing", more, ("Back", ShowSkills))));
        foreach (var id in hero.Learned.Keys.Where(k => k != c.Id).ToList())
            options.Add(($"Take up the way of the {Classes.Get(id).Name} again", () => { _player.Join(id); _hud.Toast($"You are a {Classes.Get(id).Name} again, with every rank you had in it.", 8); Save(); ShowSkills(); }));
        if (c.Id == "Stormdruid")
        {
            var form = _pursuits.Counters.GetValueOrDefault("elemental_form");
            options.Add(($"Elemental Form: {(form == 0 ? $"your attuned element ({_player.ElementalKind})" : Player.Elementals[form - 1].Name)} (change)", () =>
            {
                _pursuits.Counters["elemental_form"] = (form + 1) % (Player.Elementals.Count + 1);
                Save();
                ShowSkills();
            }));
        }
        options.Add(("Feats", ShowFeats));
        options.Add(("Quick keys", () => ShowQuickBar(ShowSkills)));
        options.Add(("Close", _hud.EndTalk));
        _hud.Talk("Skills", text, [.. options]);
        _hud.TallTalk();
        void Add(Ability a, string name, string? locked = null)
        {
            var raise = locked is null && hero.CanRaise(a.Id) ? (Action)(() => { hero.Raise(a.Id); _player.Fit(); Save(); ShowSkills(); }) : null;
            _hud.AddSkill(name, a.Text, hero.Rank(a.Id), Hero.MaxRank, locked, raise);
        }
        foreach (var a in c.Skills.Where(a => a.Id != "attune")) Add(a, $"{Slot(a)} {a.Name}");
        foreach (var a in Advances.Of(c.Id))
        {
            if (a.Style == MoveStyle.Passive) _hud.AddSkill($"[Always] {a.Move.Name}", a.Move.Text, 0, 0, a.Level <= hero.Level ? null : $"Level {a.Level}", null);
            else Add(a.Move, a.Move.Name, a.Level <= hero.Level ? null : $"Level {a.Level}");
        }

        string Slot(Ability a) => a == c.Primary ? "[Left]" : a == c.Signature ? "[Right]" : a == c.Utility ? "[Q]" : a == c.Limit ? "[R]" : "[Always]";
    }

    /// <summary>Lines for the HUD about each companion following.</summary>
    IEnumerable<string> PartyLines() => CompanionBodies.Where(e => e.Bond is not null).Select(e =>
        $"{e.Bond!.Name}  lv {e.Bond.Level}  {(e.State == EnemyState.Dead ? "down" : new string('|', (int)Math.Ceiling(e.Stats.Health / e.Stats.MaxHealth * 12)))}{(e.Bond.Hungry ? "  hungry" : "")}"
        + (e.Corrupted ? "  TURNED" : e.Infection > 0.05 ? $"  Rift {e.Infection:P0}" : ""));
}
