using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The life that isn't there to fight: eagles circling high over the wilds; songbirds about the woods that scatter
/// when you come close; fish under the water near you; and out in the mouth of the harbour, a whale or two rolling at
/// the surface. Only near the hero, and never in the way.
/// </summary>
public partial class Skylife(Main main) : Node3D
{
    sealed class Wing(Body body, Vector3 centre, float radius, float height, float speed, float phase)
    {
        public Body Body => body;
        public Vector3 Centre = centre;
        public float Radius = radius, Height = height, Speed = speed, Phase = phase;
    }

    sealed class Songbird(Body body, Vector3 perch)
    {
        public Body Body => body;
        public Vector3 Perch = perch;
        public Vector3 Away;
        public double Flying = -1;
    }

    readonly List<Wing> _eagles = [];
    readonly List<Songbird> _birds = [];
    readonly List<Wing> _fish = [];
    readonly List<Wing> _whales = [];
    readonly RandomNumberGenerator _rng = new();
    double _tick;
    float _time;

    /// <summary>The harbour mouth, where the whales come in.</summary>
    static readonly Vector3 HarbourMouth = new(385, 0, 0);

    public int Eagles => _eagles.Count;
    public int Birds => _birds.Count;
    public int Fish => _fish.Count;
    public int Whales => _whales.Count;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        var hero = main.PlayerNode.GlobalPosition;
        if ((_tick -= delta) <= 0)
        {
            _tick = 2;
            Populate(hero);
        }
        foreach (var e in _eagles) Circle(e, dt, bank: true);
        foreach (var f in _fish) Circle(f, dt, bank: false);
        foreach (var w in _whales)
        {
            Circle(w, dt, bank: false);
            // It rolls at the surface, and goes down again.
            var roll = Mathf.Sin(_time * 0.25f + w.Phase);
            w.Body.Position = w.Body.Position with { Y = (float)WorldGen.WaterLevel - 2.6f + roll * 1.4f };
        }
        foreach (var b in _birds) Flit(b, hero, dt);
    }

    void Circle(Wing w, float dt, bool bank)
    {
        w.Phase += w.Speed / w.Radius * dt;
        var at = w.Centre + new Vector3(Mathf.Cos(w.Phase), 0, Mathf.Sin(w.Phase)) * w.Radius;
        at.Y = w.Height + (bank ? Mathf.Sin(w.Phase * 2) * 1.5f : 0);
        var ahead = new Vector3(-Mathf.Sin(w.Phase), 0, Mathf.Cos(w.Phase));
        w.Body.Position = at;
        w.Body.Rotation = new Vector3(0, Mathf.Atan2(-ahead.X, -ahead.Z), bank ? -0.35f : 0);
        w.Body.Move(w.Speed, false, false);
    }

    void Flit(Songbird b, Vector3 hero, float dt)
    {
        if (b.Flying < 0 && b.Body.Position.DistanceTo(hero) < 6)
        {
            // Startled: up and away.
            var off = (b.Body.Position - hero) with { Y = 0 };
            b.Away = b.Body.Position + off.Normalized() * 25 + Vector3.Up * 12;
            b.Flying = 0;
        }
        if (b.Flying < 0)
        {
            b.Body.Move(0, true, false);
            return;
        }
        b.Flying += dt;
        var to = b.Away - b.Body.Position;
        if (to.Length() < 0.5f || b.Flying > 6)
        {
            b.Body.Visible = false; // gone into the trees
            return;
        }
        b.Body.Position += to.Normalized() * Math.Min(to.Length(), 9 * dt);
        b.Body.Rotation = new Vector3(0, Mathf.Atan2(-to.X, -to.Z), 0);
        b.Body.Move(6, false, false);
    }

    /// <summary>Who's about, near the hero: added as they come near, taken away as they leave.</summary>
    void Populate(Vector3 hero)
    {
        var world = main.World;
        var inTown = main.RoamersNode.InTown(hero);
        var biome = world.BiomeAt(hero.X, hero.Z);
        var ground = (float)world.Height(hero.X, hero.Z);

        // Eagles over the open wilds, the crags and the pines.
        Drop(_eagles, w => w.Centre.DistanceTo(hero with { Y = 0 }) > 260);
        var eagles = inTown ? 0 : biome is Biome.Crags or Biome.PineForest or Biome.Tundra ? 2 : 1;
        while (_eagles.Count < eagles && Body.For("eagle") is { } eagle)
        {
            var a = _rng.RandfRange(0, Mathf.Tau);
            var centre = new Vector3(hero.X + Mathf.Cos(a) * 70, 0, hero.Z + Mathf.Sin(a) * 70);
            _eagles.Add(new Wing(Add(eagle), centre, _rng.RandfRange(25, 45), ground + _rng.RandfRange(30, 45), _rng.RandfRange(7, 10), a));
        }

        // Songbirds on the ground in the woods, a few at a time.
        Drop(_birds, b => b.Body.Position.DistanceTo(hero) > 90 || (b.Flying >= 0 && !b.Body.Visible));
        if (!inTown && biome is Biome.AutumnForest or Biome.PineForest && _birds.Count < 5)
            for (var i = _birds.Count; i < 5; i++)
            {
                var a = _rng.RandfRange(0, Mathf.Tau);
                var d = _rng.RandfRange(14, 30);
                var at = new Vector3(hero.X + Mathf.Cos(a) * d, 0, hero.Z + Mathf.Sin(a) * d);
                var h = world.Height(at.X, at.Z);
                if (h < WorldGen.WaterLevel + 0.3 || Body.For("songbird") is not { } bird) continue;
                _birds.Add(new Songbird(Add(bird), at with { Y = (float)h }) { });
                _birds[^1].Body.Position = _birds[^1].Perch;
                _birds[^1].Body.Rotation = new Vector3(0, _rng.RandfRange(0, Mathf.Tau), 0);
            }

        // Fish in the water near the hero (not the harbour's: that's the docks' business).
        Drop(_fish, f => f.Centre.DistanceTo(hero with { Y = f.Centre.Y }) > 60);
        if (_fish.Count == 0 && !inTown)
            for (var k = 0; k < 12; k++)
            {
                var a = k * Mathf.Tau / 12;
                var at = new Vector3(hero.X + Mathf.Cos(a) * 18, 0, hero.Z + Mathf.Sin(a) * 18);
                if (world.Height(at.X, at.Z) > WorldGen.WaterLevel - 1.2) continue;
                for (var i = 0; i < 3 && Body.For("fish") is { } fish; i++)
                {
                    var depth = (float)WorldGen.WaterLevel - _rng.RandfRange(0.5f, 0.9f);
                    _fish.Add(new Wing(Add(fish), at with { Y = depth }, _rng.RandfRange(1.5f, 3.5f), depth, _rng.RandfRange(0.8f, 1.6f), _rng.RandfRange(0, Mathf.Tau)));
                }
                break;
            }

        // Whales in the harbour mouth, for anyone on the docks or the shore to see.
        Drop(_whales, w => hero.DistanceTo(HarbourMouth) > 600);
        if (_whales.Count == 0 && hero.DistanceTo(HarbourMouth) < 450)
            for (var i = 0; i < 2 && Body.For("whale") is { } whale; i++)
                _whales.Add(new Wing(Add(whale), HarbourMouth + new Vector3(0, 0, i == 0 ? -30 : 35), 30 + i * 12, (float)WorldGen.WaterLevel - 2.5f, 2.2f, i * 2.5f));
    }

    Body Add(Body b)
    {
        AddChild(b);
        return b;
    }

    static void Drop<T>(List<T> list, Func<T, bool> gone)
    {
        foreach (var x in list.Where(gone).ToList())
        {
            var body = x switch { Wing w => w.Body, Songbird s => s.Body, _ => null };
            body?.QueueFree();
            list.Remove(x);
        }
    }
}
