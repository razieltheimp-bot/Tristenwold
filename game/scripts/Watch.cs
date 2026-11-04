using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Tristinwold keeps the peace. The Watch stands at its posts (the Watch House, both gates, the Exchange, the Salt
/// Barrel, the Warrens, the docks) and walks its beat; when a deviant comes into town after the hero, or something
/// out of the wilds chases them through the streets, the Watch comes running. And the townsfolk don't just stand
/// there, in Tristinwold or any village: whoever works with their hands or carries steel goes at it, most throw
/// whatever's to hand, the priests and healers patch the hero up, and the young and the old take cover (see
/// <see cref="TownDefence"/>). A fallen watchman's post is filled again in a couple of minutes.
/// </summary>
public partial class TownWatch(Main main) : Node
{
    public static IEnumerable<Vector3> PostSpots => Posts.Select(p => p.At);

    /// <summary>Where a post is, by its name ("the east gate"), for a beat to walk (see <see cref="WatchBook.Beat"/>).</summary>
    public static Vector3? PostAt(string post) => Posts.FirstOrDefault(p => p.Post == post) is { Post: not null } x ? x.At : null;

    static readonly (Vector3 At, string Post)[] Posts =
    [
        (new(64, 0, 48), "the Watch House"), (new(92, 0, 5), "the east gate"), (new(-92, 0, 2), "the west gate"),
        (new(25, 0, -18), "the Exchange"), (new(40, 0, 64), "the Salt Barrel"), (new(-20, 0, -72), "the Warrens"), (new(104, 0, 8), "the docks"), (Main.YardAt + new Vector3(2, 0, 4), "the caravan yard"),
    ];

    static readonly string[] Surnames = ["Bracken", "Holt", "Tamsin", "Corrow", "Edda", "Marl", "Pike", "Wrenn", "Ashby"];

    /// <summary>How long a fallen watchman's post stands empty.</summary>
    public const double RefillSeconds = 120;

    readonly Enemy?[] _guards = new Enemy?[Posts.Length];
    readonly double[] _refill = new double[Posts.Length];
    readonly Dictionary<string, double> _ready = [];
    readonly Tristinwold.Deviant.Rng _rng = new((ulong)GD.Randi() << 32 | GD.Randi());
    double _tick, _clock;
    bool _alarm, _hostile;

    /// <summary>The Watch is after the hero (a price on their head): every watchman turns on them at sight, until it's paid.</summary>
    public bool Hostile
    {
        get => _hostile;
        set
        {
            _hostile = value;
            foreach (var g in Guards)
            {
                if (!value) g.LoseTrack(1);
                g.Allied = !value;
                g.RefreshPlate();
            }
        }
    }

    /// <summary>The townsfolk's blows, throws and poultices so far (for the self-test).</summary>
    public int Helped { get; private set; }

    public IEnumerable<Enemy> Guards => _guards.OfType<Enemy>().Where(g => IsInstanceValid(g) && g.State != EnemyState.Dead);

    /// <summary>Inside Tristinwold's walls (or on its docks): the Watch's streets.</summary>
    public static bool InTristinwold(Vector3 at) => new Vector2(at.X, at.Z).Length() < City.OuterRadius + 40;

    /// <summary>The watchmen within <paramref name="metres"/> of a place.</summary>
    public int GuardsNear(Vector3 at, float metres) => Guards.Count(g => g.GlobalPosition.DistanceTo(at) < metres);

    public override void _Ready()
    {
        for (var i = 0; i < Posts.Length; i++) Post(i);
    }

    void Post(int i)
    {
        var at = Posts[i].At with { Y = City.FloorAt(Posts[i].At) + 0.4f };
        var g = Enemy.Create(Bestiary.Get("watchman"), at, look: main.GuardLook(CityEconomy.Capital)); // armed as Tristinwold's forges allow
        main.ArmGuard(g, CityEconomy.Capital);
        g.Allied = !_hostile;
        g.Watch = true;
        g.Roam = 7;
        g.Stats.Name = $"Watchman {Surnames[(i + (int)(GD.Randi() % 3)) % Surnames.Length]}";
        main.AddEnemy(g);
        g.RefreshPlate();
        _guards[i] = g;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if ((_tick -= delta) > 0) return;
        _tick = 0.5;
        // Fallen watchmen are replaced.
        for (var i = 0; i < Posts.Length; i++)
        {
            var g = _guards[i];
            if (g is not null && IsInstanceValid(g) && g.State != EnemyState.Dead) continue;
            if (_refill[i] <= 0) _refill[i] = _clock + RefillSeconds;
            else if (_clock >= _refill[i]) { _refill[i] = 0; Post(i); }
        }

        var p = main.PlayerNode;
        var roamers = main.RoamersNode;
        var threats = roamers.Near.Select(n => n.Body).Where(b => IsInstanceValid(b) && b.State != EnemyState.Dead && roamers.InTown(b.GlobalPosition)).ToList();
        if (threats.Count == 0) { _alarm = false; return; }
        if (!_alarm)
        {
            _alarm = true;
            var here = threats[0].GlobalPosition;
            main.HudNode.Toast(InTristinwold(here)
                ? $"{threats[0].Stats.Name} is in the town! The townsfolk turn on it, and someone runs for the Watch."
                : $"{threats[0].Stats.Name} is in the village! The people turn out to drive it off.", 7);
        }
        // The Watch comes running, from anywhere in town.
        foreach (var threat in threats.Where(t => InTristinwold(t.GlobalPosition)))
            foreach (var g in Guards.Where(g => g.Rival is null && g.GlobalPosition.DistanceTo(threat.GlobalPosition) < 160))
                g.Distract(threat);
        // The townsfolk join in.
        foreach (var person in main.Locals.Concat(main.PlacesNode.People).Where(IsInstanceValid))
        {
            var threat = threats.MinBy(t => t.GlobalPosition.DistanceTo(person.GlobalPosition))!;
            var apart = person.GlobalPosition.DistanceTo(threat.GlobalPosition);
            if (apart > TownDefence.JoinRange || _ready.GetValueOrDefault(person.Npc.Id) > _clock) continue;
            _ready[person.Npc.Id] = _clock + TownDefence.Every * (0.8 + 0.4 * _rng.NextDouble());
            Defend(person, TownDefence.Role(person.Npc, person.Captive), threat, apart);
        }
    }

    void Defend(Person person, Defender role, Enemy threat, float apart)
    {
        var p = main.PlayerNode;
        switch (role)
        {
            case Defender.Fights or Defender.Throws:
            {
                person.Face(threat.GlobalPosition);
                var close = role == Defender.Fights && apart <= TownDefence.Reach;
                if (close) person.Figure.Attack();
                else
                {
                    person.Figure.Cast();
                    Effects.Bolt(GetParent(), person.GlobalPosition + Vector3.Up * 1.5f, threat.GlobalPosition + Vector3.Up * 1.2f, DamageKind.Physical, 0.06f);
                }
                threat.Pelted(TownDefence.Blow(role, close));
                Helped++;
                if (_rng.Chance(0.15)) main.HudNode.Log($"{person.Npc.Name} {(close ? "lays into" : "pelts")} {threat.Stats.Name}{(close ? "" : " with whatever's to hand")}.");
                break;
            }
            case Defender.Heals when !p.Downed && p.GlobalPosition.DistanceTo(person.GlobalPosition) < 20 && p.Stats.Health < p.Stats.MaxHealth * 0.9:
                person.Face(p.GlobalPosition);
                person.Figure.Cast();
                p.Stats.Heal(p.Stats.MaxHealth * TownDefence.Poultice);
                Effects.Rise(p, DamageKind.Sun, 0.8f);
                Helped++;
                if (_rng.Chance(0.3)) main.HudNode.Log($"{person.Npc.Name} patches you up.");
                break;
            case Defender.Cowers:
                person.Face(threat.GlobalPosition);
                person.Figure.Perform("Block", 1.6, 1);
                break;
        }
    }
}
