using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The company around the hero: experience and levels from every kill, the companions who follow them (beasts
/// raised from younglings, squires and apprentices from the towns, a warlock's familiar, a dragon), taming,
/// feeding, the guild masters who teach the classes, and the road to Dragon Knight with Ser Aldric.
/// </summary>
public partial class Main
{
    /// <summary>How many companions can follow at once; the rest wait at the Gallows Perch.</summary>
    public const int MaxFollowing = 4;

    readonly List<Bond> _bonds = [];
    readonly List<Enemy> _companions = [];
    DragonSaga _saga = new();
    Pickup? _nest;
    bool _nestRaided;
    readonly HashSet<string> _hungerSaid = [];
    readonly Rng _companyRng = new((ulong)Random.Shared.NextInt64());

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
        Enemy.Felled += OnFelled;
        Enemy.Turned += OnTurned;
        Enemy.SparEnded += OnSparEnded;
        _player.Summoned += Summon;
        foreach (var bond in _bonds.Where(b => b.Following)) BringCompanion(bond);
    }

    void TearDownCompany()
    {
        Enemy.Slain -= OnSlain;
        Enemy.Felled -= OnFelled;
        Enemy.Turned -= OnTurned;
        Enemy.SparEnded -= OnSparEnded;
    }

    // ---- companions ----------------------------------------------------------------------------

    Enemy BringCompanion(Bond bond, Vector3? at = null)
    {
        var spot = at ?? _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 3 + new Vector3((float)GD.RandRange(-2, 2), 0, 0);
        var e = Enemy.Create(Bestiary.Get(bond.KindId), spot with { Y = City.FloorAt(spot) + 0.5f }, bond: bond);
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
        var familiar = Enemy.Create(Bestiary.Get("familiar"), at with { Y = City.FloorAt(at) + 0.3f }, look: "familiar");
        familiar.Leader = _player;
        familiar.Expires = seconds;
        familiar.RefreshPlate();
        AddEnemy(familiar);
        Effects.Burst(this, at + Vector3.Up, DamageKind.Shock, 2);
        _hud.Toast("A familiar steps out of the dark to fight beside you.");
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
        else _hud.Toast($"{bond.Name} is dead.", 10);
        Save();
    }

    /// <summary>Time passes for the company: beasts and dragons get hungry, and starve if never fed.</summary>
    void TendCompany(double delta)
    {
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

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim.Bond is not null || !byCompany) return;
        if (victim.SiteId is { } site)
        {
            foreach (var u in _journal.Killed(site).ToList()) Apply(u);
            _places.KilledAt(site);
        }
        if (victim.Leader is not null || victim.Friendly) return;
        _player.Killed(victim);
        Spoils(victim);
        var xp = Math.Max(3, (int)Math.Round(victim.Stats.MaxHealth / 6 * (victim.NemesisId is null ? 1 : 2)));
        GainXp(xp);
        var meat = victim.Kind.Id switch
        {
            "deer" or "boar" or "frost_troll" => 2,
            "bear" => 3,
            "wolf" or "ice_wolf" or "fox" or "skeever" or "mudcrab" or "goblin" => 1,
            _ when victim.IsDragon => 4,
            _ => 0,
        };
        if (victim.Young) meat = Math.Min(meat, 1);
        if (meat > 0) { _player.Meat += meat; _hud.Log($"+{meat} meat ({_player.Meat})"); }
        if (_player.Class.Id == "Thief" && victim.Kind.Height > 1.2)
        {
            var coin = (int)Math.Round((1 + _companyRng.Next(4)) * _player.Hero.Power("light_fingers"));
            _player.Septims += coin;
            _hud.Log($"+{coin} septims, lifted from the body");
        }
    }

    /// <summary>Experience for the hero and every companion with them.</summary>
    public void GainXp(int xp)
    {
        var hero = _player.Hero;
        if (hero.Gain((int)Math.Round(xp * _player.Race.Xp)) > 0)
        {
            _player.Fit();
            _player.Stats.Heal(_player.Stats.MaxHealth);
            Sound.Ui("levelup");
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
    public bool Tame(Enemy young)
    {
        var kin = Enemies().Where(o => o != young && !o.Young && o.Kind.Id == young.Kind.Id && o.State != EnemyState.Dead
            && o.GlobalPosition.DistanceTo(young.GlobalPosition) < 25).ToList();
        var chance = Taming.Chance(kin.Count > 0, _player.Meat > 0, _player.Class.Id == "Ranger", _player.Hero.Level, _player.Pack.Level(Job.Taming));
        if (kin.Count > 0)
        {
            _hud.Toast($"The {young.Stats.Name}'s kin are close, and they won't let you near it.");
            foreach (var k in kin) k.Provoke(_player);
            return false;
        }
        if (!_companyRng.Chance(chance))
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
            ? $"The {young.Stats.Name} sniffs your hand and decides you'll do. You call it {bond.Name}."
            : $"The {young.Stats.Name} is yours: you call it {bond.Name}. With {MaxFollowing} already at your heels, it goes to wait at the Gallows Perch.")
            + (up is { } level ? $" Beast taming rises to {level}!" : ""), 9);
        Save();
        return true;
    }

    /// <summary>A new beast for the company: named, and at your heels if there's room, else waiting at the Gallows Perch.</summary>
    Bond Adopt(string kindId, bool dragon, Vector3 at)
    {
        var bond = new Bond
        {
            KindId = kindId, Role = dragon ? BondRole.Dragon : BondRole.Pet,
            Name = dragon ? Dragons.NameFor(_companyRng) : _companyRng.Pick(PetNames.Where(n => _bonds.All(b => b.Name != n)).DefaultIfEmpty("Scrap").ToList()),
        };
        _bonds.Add(bond);
        bond.Following = _bonds.Count(b => b.Following) <= MaxFollowing;
        if (bond.Following) BringCompanion(bond, at);
        return bond;
    }

    /// <summary>Buys a young beast from Wren's Menagerie (never a dragon: the menagerie has none).</summary>
    public Bond? BuyPet(PetOffer offer)
    {
        var price = Menagerie.Price(offer, _player.Pack.Level(Job.Taming), _player.Markup);
        if (_player.Septims < price) { _hud.Toast($"The {offer.Name.ToLowerInvariant()} costs {price} septims; you have {_player.Septims}.", 4); return null; }
        _player.Septims -= price;
        var bond = Adopt(offer.KindId, dragon: false, _player.GlobalPosition + _player.GlobalTransform.Basis.Z * 2);
        var up = _player.Pack.Practise(Job.Taming, 15);
        _hud.Toast((bond.Following
            ? $"Wren hands you the {offer.Name.ToLowerInvariant()}. It already knows its name: {bond.Name}."
            : $"The {offer.Name.ToLowerInvariant()} is yours ({bond.Name}). With {MaxFollowing} at your heels, Wren sends it to wait at the Gallows Perch.")
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
        _ => (BondRole.Apprentice, "protege", "apprentice"),
    };

    public const int RecruitLevel = 3, RecruitFee = 25;

    /// <summary>A youth leaves home to follow the hero: a fee to the family, and the hero must have made a name.</summary>
    public bool Recruit(Person person)
    {
        var npc = person.Npc;
        var (role, kind, word) = Follower;
        if (_player.Hero.Level < RecruitLevel) { Say(person, $"You? You're nobody yet. Come back when you've made a name (level {RecruitLevel})."); return false; }
        if (_player.Septims < RecruitFee) { Say(person, $"My family can't spare me for nothing. {RecruitFee} septims, for their trouble."); return false; }
        _player.Septims -= RecruitFee;
        var bond = new Bond { Id = npc.Id, KindId = kind, Role = role, Name = npc.First };
        _bonds.Add(bond);
        bond.Following = _bonds.Count(b => b.Following) <= MaxFollowing;
        var at = person.GlobalPosition;
        person.QueueFree();
        if (bond.Following) BringCompanion(bond, at);
        _hud.EndTalk();
        _hud.Toast($"{npc.Name} becomes your {word}. {RecruitFee} septims go to {npc.Their} family.", 8);
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
        foreach (var o in GuildTalk(person)) yield return o;
        if (People.IsYouth(npc))
        {
            var word = Follower.Word;
            yield return ($"Would you become my {word}? ({RecruitFee} septims to your family)", () => Recruit(person));
        }
        if (npc.Id == "eyrie/aldric") foreach (var o in Aldric(person)) yield return o;
        foreach (var o in ShopTalk(person)) yield return o;
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
            case SagaStage.NotStarted:
                yield return ("How do I become a Dragon Knight?", () =>
                {
                    var m = DragonSaga.Matriarch(_world);
                    if (m is null) { Say(person, "There's no clutch to be had this year. Ask me again."); return; }
                    var here = new Vector3((float)DragonSaga.RookeryX, 0, (float)DragonSaga.RookeryZ);
                    _hud.Talk(Who(person.Npc),
                        $"Every knight of the Eyrie raised their dragon from the egg. {m.FullName}, {m.What}, keeps a pact with {m.Ward!.Name}, and her clutch lies in her lair, {Direction(here, (m.X, m.Z))} of here. Rift-corrupted drakes have been preying on it. Bring back an egg and keep it warm. What hatches will be small, and hungry, and it will need you: feed it, keep it alive, and fight beside it until it's grown enough to carry you. Then bring it to me, and kneel.",
                        ("I'll do it.", () =>
                        {
                            _saga.Begin(_world);
                            _hud.Toast("New quest: the Dragon Knight's road.");
                            Save();
                            Say(person, "Then go. And come back with an egg, not an excuse.");
                        }),
                        ("Not yet.", () => Say(person, "The dragons will wait. They're good at it.")));
                });
                break;
            case SagaStage.SeekEgg:
                yield return ("Can your knights fly me to the nest?", () =>
                {
                    _hud.EndTalk();
                    FlyTo(_saga.NestX + 60, _saga.NestZ + 40, $"A knight of the Eyrie flies you over the wilds to the lair of {_saga.MatriarchName}, and sets you down at the edge of the nest.");
                });
                if (_saga.Lost > 0) yield return ("I lost my dragon.", () => Say(person, "Then you know what it costs. Try again. The nest is where it was."));
                break;
            case SagaStage.CarryEgg:
                yield return ("About the egg...", () => Say(person, $"Keep it warm and keep it close. Rest when you can: it hatches fastest by a fire. ({_saga.Warmth:P0} warm.)"));
                break;
            case SagaStage.Raising when _saga.ReadyToKnight(dragon):
                yield return ($"I've raised {dragon!.Name}.", () => _hud.Talk(Who(person.Npc), $"So you have. Look at {dragon.Name}. Kneel.",
                    ("(Kneel.)", () =>
                    {
                        _saga.Knighted(dragon);
                        _hud.Toast($"You rise a Dragon Knight of the Eyrie. Stand by {dragon.Name} and press V to ride: Space climbs, C dives.", 14);
                        Save();
                        Say(person, $"Rise, knight. {dragon.Name} will carry you anywhere. Don't make me regret this.");
                    })));
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

    void FlyTo(double x, double z, string story)
    {
        var at = new Vector3((float)x, 0, (float)z);
        _terrain.EnsureAround(at, 1);
        _player.GlobalPosition = at with { Y = City.FloorAt(at) + 1 };
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        _hud.Toast(story, 10);
    }

    /// <summary>The nest at the matriarch's lair: the egg waits there, and the first time you come, corrupted drakes are at it.</summary>
    void TendNest()
    {
        var nest = new Vector3((float)_saga.NestX, 0, (float)_saga.NestZ);
        var d = _player.GlobalPosition.DistanceTo(nest with { Y = _player.GlobalPosition.Y });
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
        var near = CompanionBodies.Where(e => e.Bond is { Role: BondRole.Dragon } && e.Leader == _player && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(_player.GlobalPosition) < 4 + e.Figure.Scale.X * 4)
            .MinBy(e => e.GlobalPosition.DistanceTo(_player.GlobalPosition));
        if (near is null) { _hud.Toast("Your dragon isn't close enough to climb on."); return; }
        if (!near.Bond!.Rideable) { _hud.Toast($"{near.Bond.Name} is too small to carry you yet (level 10)."); return; }
        if (_saga.Stage != SagaStage.Knighted) { _hud.Toast($"Only a knight of the Eyrie may ride. Take {near.Bond.Name} to {DragonSaga.Knight} at the rookery."); return; }
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
            var state = !b.Following ? "waiting at the Gallows Perch" : body is null ? "lost" : body.State == EnemyState.Dead ? "down" : $"{body.Stats.Health:0}/{body.Stats.MaxHealth:0} health";
            return $"{b.Name}: {b.Stage} {(b.Role is BondRole.Pet or BondRole.Dragon ? kind : "")}, level {b.Level} ({b.Xp}/{Bond.XpToNext(b.Level)} xp), {state}{(b.Eats ? $", {b.Fed:0}% fed" : "")}. Knows: {(b.Skills.Any() ? string.Join(", ", b.Skills.Select(s => s.Name)) : "nothing yet")}.";
        });
        var options = new List<(string, Action)>();
        foreach (var b in _bonds.Where(b => b.Following && b.Eats)) options.Add(($"Feed {b.Name} (meat: {_player.Meat})", () => { Feed(b); ShowParty(); }));
        foreach (var b in _bonds)
        {
            var bond = b;
            if (bond.Following) options.Add(($"Send {bond.Name} home", () => { bond.Following = false; var body = BodyOf(bond); if (body is not null) { _companions.Remove(body); body.QueueFree(); } Save(); ShowParty(); }));
            else if (_bonds.Count(x => x.Following) < MaxFollowing) options.Add(($"Call {bond.Name}", () => { bond.Following = true; BringCompanion(bond); Save(); ShowParty(); }));
        }
        options.Add(("Close", _hud.EndTalk));
        _hud.Talk("Your company", string.Join("\n", lines), [.. options]);
    }

    /// <summary>The skills (K): the class's moves, their ranks, and points to spend.</summary>
    public void ShowSkills()
    {
        var hero = _player.Hero;
        var c = _player.Class;
        var titles = _guilds.Titles.Count > 0 ? $" {string.Join(", ", _guilds.Titles)}." : "";
        var text = $"{c.Name}, level {hero.Level} ({hero.Xp}/{Hero.XpToNext(hero.Level)} xp), {_player.GuildTitle}.{titles} {hero.Points} skill point{(hero.Points == 1 ? "" : "s")} to spend.\n" +
            string.Join("\n", c.Skills.Where(a => a.Id != "attune").Select(a => $"{Slot(a)} {a.Name} (rank {hero.Rank(a.Id)}/{Hero.MaxRank}): {a.Text}"));
        var options = c.Skills.Where(a => a.Id != "attune" && hero.CanRaise(a.Id))
            .Select(a => ($"Raise {a.Name} to rank {hero.Rank(a.Id) + 1}", (Action)(() => { hero.Raise(a.Id); _player.Fit(); Save(); ShowSkills(); }))).ToList();
        options.Add(("Close", _hud.EndTalk));
        _hud.Talk("Skills", text, [.. options]);

        string Slot(Ability a) => a == c.Primary ? "[Left]" : a == c.Signature ? "[Right]" : a == c.Utility ? "[Q]" : a == c.Limit ? "[R]" : "[Always]";
    }

    /// <summary>Lines for the HUD about each companion following.</summary>
    IEnumerable<string> PartyLines() => CompanionBodies.Where(e => e.Bond is not null).Select(e =>
        $"{e.Bond!.Name}  lv {e.Bond.Level}  {(e.State == EnemyState.Dead ? "down" : new string('|', (int)Math.Ceiling(e.Stats.Health / e.Stats.MaxHealth * 12)))}{(e.Bond.Hungry ? "  hungry" : "")}"
        + (e.Corrupted ? "  TURNED" : e.Infection > 0.05 ? $"  Rift {e.Infection:P0}" : ""));
}
