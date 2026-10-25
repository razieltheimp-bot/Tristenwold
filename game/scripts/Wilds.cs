using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;
using Noise = Tristinwold.World.Noise;

namespace Tristinwold.Game;

/// <summary>
/// Brings the wilds to life around the player: the herds, packs and lone beasts that
/// <see cref="Wildlife"/> places in the land appear as you come near and are put away behind you.
/// A herd you wipe out stays gone for a while (ten minutes) before others move into its range.
/// It also brings out the dragons: each ancient in its lair with its giants, the warbands of the dragon wars
/// being fought this season (see <see cref="Dragons"/>), the Dragonwatch over every pact town and the raids
/// that come against them, and the Order of the Eyrie's dragon knights at their rookery outside Tristinwold.
/// </summary>
public partial class Wilds(Main main, WorldGen world, Terrain terrain) : Node
{
    const double Near = 230, Far = 330, Regrow = 600;

    /// <summary>Dragons are seen from much further off, and their lairs and wars stay put longer.</summary>
    const double DragonNear = 600, DragonFar = 900, AncientRegrow = 3600;

    readonly Dictionary<string, List<Enemy>> _herds = [];
    readonly Dictionary<string, Hawk> _hawks = [];
    readonly Dictionary<string, double> _gone = [];
    string? _territory;
    readonly HashSet<string> _warsSeen = [];

    /// <summary>The real time, which decides which dragon wars are being fought (tests set it).</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    /// <summary>How many wild creatures are about right now (for tests and the curious).</summary>
    public int Count => _herds.Values.Sum(l => l.Count(e => IsInstanceValid(e) && e.State != EnemyState.Dead));

    public IEnumerable<string> Kinds => _herds.Values.SelectMany(l => l).Where(IsInstanceValid).Select(e => e.Kind.Id).Distinct();

    public IEnumerable<Enemy> Creatures => _herds.Values.SelectMany(l => l).Where(e => IsInstanceValid(e));

    public void Update(Vector3 player, double clock)
    {
        foreach (var (id, members) in _herds.ToList())
        {
            if (members.All(e => !IsInstanceValid(e) || e.State == EnemyState.Dead) && !_gone.ContainsKey(id)) _gone[id] = clock;
            var far = members.Any(e => IsInstanceValid(e) && e.IsDragon) ? DragonFar : Far;
            var anyNear = members.Any(e => IsInstanceValid(e) && e.GlobalPosition.DistanceTo(player) < far);
            if (anyNear) continue;
            foreach (var e in members.Where(IsInstanceValid)) e.QueueFree();
            _herds.Remove(id);
            if (_hawks.Remove(id, out var hawk) && IsInstanceValid(hawk)) hawk.QueueFree();
        }

        foreach (var herd in Wildlife.Near(world, player.X, player.Z, Near)) Bring(herd, clock, hawks: true);

        // Ancient dragons in their lairs, and the dragon wars of this season.
        foreach (var ancient in Dragons.Near(world, player.X, player.Z, DragonNear))
            foreach (var herd in Dragons.Lair(ancient)) Bring(herd, clock, regrow: AncientRegrow);
        foreach (var war in Dragons.WarsNear(world, Now(), player.X, player.Z, DragonNear + 300))
        {
            if (_warsSeen.Add(war.Id)) main.HudNode.Toast($"Dragons fill the sky: {war.Story}", 10);
            foreach (var herd in Dragons.Warbands(war))
                if (herd.DistanceTo(player.X, player.Z) < DragonNear) Bring(herd, clock, regrow: AncientRegrow);
        }

        // Dragon knights over the pact towns, the raids that come against them, and the Order's own rookery.
        foreach (var pact in Dragons.PactsNear(world, player.X, player.Z, DragonNear))
            foreach (var herd in Dragons.Watch(pact)) Bring(herd, clock, regrow: AncientRegrow);
        foreach (var raid in Dragons.RaidsNear(world, Now(), player.X, player.Z, DragonNear))
        {
            if (_warsSeen.Add(raid.Id) && Dragons.PactsNear(world, player.X, player.Z, DragonNear).FirstOrDefault(a => a.Id == raid.Foe) is { } town)
                main.HudNode.Toast($"{Bestiary.Get(raid.KindId).Name}s are raiding {town.Ward!.Name}! Its dragon knights fly to meet them.", 10);
            Bring(raid, clock, regrow: AncientRegrow);
        }
        foreach (var herd in Rookery)
            if (herd.DistanceTo(player.X, player.Z) < DragonNear) Bring(herd, clock, regrow: Regrow);

        // Crossing into an ancient's land.
        var lord = Dragons.TerritoryAt(world, player.X, player.Z);
        if (lord?.Id != _territory)
        {
            _territory = lord?.Id;
            if (lord is not null) main.HudNode.Toast($"You enter the lands of {lord.FullName}, {lord.What}.", 8);
        }
    }

    /// <summary>The Order of the Eyrie's rookery on the hill west of Tristinwold: two knights and their dragons, always.</summary>
    static readonly Herd[] Rookery =
    [
        new("rookery_gold", "dragon_gold", DragonSaga.RookeryX - 20, DragonSaga.RookeryZ - 10, 1, 25, DragonSize.Large, "eyrie", Allied: true, Riders: true),
        new("rookery_silver", "dragon_silver", DragonSaga.RookeryX + 15, DragonSaga.RookeryZ - 25, 1, 25, DragonSize.Normal, "eyrie", Allied: true, Riders: true),
    ];

    void Bring(Herd herd, double clock, bool hawks = false, double regrow = Regrow)
    {
        if (_herds.ContainsKey(herd.Id)) return;
        if (_gone.TryGetValue(herd.Id, out var when))
        {
            if (clock - when < regrow) return;
            _gone.Remove(herd.Id);
        }
        var centre = new Vector3((float)herd.X, 0, (float)herd.Z);
        if (!terrain.HasGround(centre)) return; // the land isn't built there yet
        _herds[herd.Id] = Spawn(herd);
        // A hawk wheels over about one stretch of land in three.
        if (hawks && WorldGen.StableHash(herd.Id) % 3 == 0)
        {
            var hawk = Hawk.Create(centre with { Y = City.FloorAt(centre) }, WorldGen.StableHash(herd.Id));
            AddChild(hawk);
            _hawks[herd.Id] = hawk;
        }
    }

    List<Enemy> Spawn(Herd herd)
    {
        var rng = Noise.Dice(world.Seed, 43, WorldGen.StableHash(herd.Id));
        var kind = Bestiary.Get(herd.KindId);
        var danger = WorldGen.Danger(herd.X, herd.Z);
        var list = new List<Enemy>();
        for (var i = 0; i < herd.Count + herd.Young; i++)
        {
            var young = i >= herd.Count;
            var a = rng.NextDouble() * Mathf.Tau;
            var r = rng.NextDouble() * herd.Roam * 0.5;
            var at = new Vector3((float)(herd.X + Math.Cos(a) * r), 0, (float)(herd.Z + Math.Sin(a) * r));
            if (world.Height(at.X, at.Z) < WorldGen.WaterLevel + 0.2) at = new Vector3((float)herd.X, 0, (float)herd.Z);
            var e = Enemy.Create(kind, at with { Y = City.FloorAt(at) + 0.5f }, danger, size: young ? DragonSize.Normal : herd.Size, young: young,
                look: herd.Riders && !young ? $"rider:{kind.Id}" : null);
            e.Roam = (float)herd.Roam;
            (e.Faction, e.Foe, e.Allied) = (herd.Faction, herd.Foe, herd.Allied);
            if (herd.Name is { } name) { e.Stats.Name = name; e.RefreshPlate(); }
            if (herd.Riders && !young) { e.Stats.Name = Dragons.KnightName(WorldGen.StableHash(herd.Id) + i); e.RefreshPlate(); }
            e.RotationDegrees = new Vector3(0, (float)(rng.NextDouble() * 360), 0);
            main.AddEnemy(e);
            list.Add(e);
        }
        return list;
    }
}

/// <summary>A hawk circling high over the land: life overhead. It never comes down to fight.</summary>
public partial class Hawk : Node3D
{
    Vector3 _centre;
    float _radius, _angle, _speed;

    public static Hawk Create(Vector3 ground, long seed)
    {
        var rng = new Tristinwold.Nemesis.Rng((ulong)seed);
        var h = new Hawk
        {
            _centre = ground + new Vector3(0, 22 + (float)rng.NextDouble() * 14, 0),
            _radius = 14 + (float)rng.NextDouble() * 16,
            _angle = (float)(rng.NextDouble() * Mathf.Tau),
            _speed = 7 + (float)rng.NextDouble() * 3,
        };
        var body = Body.For("hawk");
        body.Scale = Vector3.One * 2.2f; // a little larger than life, so it reads from the ground
        h.AddChild(body);
        body.Move(3, false, false);
        return h;
    }

    public override void _Process(double delta)
    {
        _angle += _speed / _radius * (float)delta;
        var at = _centre + new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle * 0.37f) * 0.15f, Mathf.Sin(_angle)) * _radius;
        var ahead = _centre + new Vector3(Mathf.Cos(_angle + 0.1f), 0, Mathf.Sin(_angle + 0.1f)) * _radius;
        Position = at;
        LookAt(ahead with { Y = at.Y }, Vector3.Up);
        Rotation = Rotation with { Z = -0.35f }; // banking into the turn
    }
}
