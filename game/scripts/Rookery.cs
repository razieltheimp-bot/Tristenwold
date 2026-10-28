using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The Order of the Eyrie's riders come and go at the rookery on the hill west of Tristinwold, every day: one comes
/// in over the wilds, wheels once over the city, and lands at the rookery; rests a while; and goes again. Nobody tells
/// a new hero what that is. They can see it from the walls, and go and find out, or not.
/// </summary>
public partial class RiderFlights(Main main) : Node3D
{
    enum Leg { Coming, Landed, Going }

    static readonly string[] Colours = ["gold", "silver", "bronze", "copper", "brass"];
    static Vector3 Pad => new((float)DragonSaga.RookeryX + 28, 0, (float)DragonSaga.RookeryZ + 18);

    /// <summary>How long a rider rests at the rookery.</summary>
    public const double RestSeconds = 150;

    Body? _rider;
    Leg _leg;
    Vector3 _from, _away;
    double _rest, _next = 90;
    int _day = -1;

    /// <summary>How many riders have come down at the rookery (for the self-test), and whether one is there now.</summary>
    public int Landings { get; private set; }
    public bool Landed => _rider is not null && _leg == Leg.Landed;
    public Body? Rider => _rider;

    public override void _Process(double delta)
    {
        var day = main.Economy.Day;
        if (_rider is null && (day != _day && _day >= 0 || (_next -= delta) <= 0))
        {
            _day = day;
            _next = double.MaxValue; // after the first, one a day
            Arrive();
        }
        if (_day < 0) _day = day;
        if (_rider is null) return;
        var dt = (float)delta;
        var pad = Pad with { Y = City.FloorAt(Pad) };
        switch (_leg)
        {
            case Leg.Coming:
            {
                var to = pad - _rider.Position;
                var flat = new Vector3(to.X, 0, to.Z);
                // Long and high over the wilds, then down in a long glide.
                var height = Mathf.Clamp(flat.Length() * 0.12f, 0, 60);
                var target = pad + Vector3.Up * height;
                var step = (target - _rider.Position).Normalized() * Mathf.Min(24 * dt, (target - _rider.Position).Length());
                _rider.Position += step;
                Face(flat);
                _rider.Move(16, false, false);
                if (flat.Length() < 2 && _rider.Position.Y - pad.Y < 0.5f)
                {
                    _rider.Position = pad;
                    _leg = Leg.Landed;
                    _rest = RestSeconds;
                    Landings++;
                    if (Near(400)) main.HudNode.Toast("A dragon rider of the Order lands at the rookery on the hill.", 6);
                }
                break;
            }
            case Leg.Landed:
                _rider.Move(0, true, false);
                if ((_rest -= delta) <= 0) _leg = Leg.Going;
                break;
            case Leg.Going:
            {
                var to = _away - _rider.Position;
                var climb = Mathf.Min(_rider.Position.Y + 8 * dt, pad.Y + 70);
                _rider.Position = (_rider.Position + new Vector3(to.X, 0, to.Z).Normalized() * 22 * dt) with { Y = climb };
                Face(to);
                _rider.Move(16, false, false);
                if (new Vector2(to.X, to.Z).Length() < 20) { _rider.QueueFree(); _rider = null; }
                break;
            }
        }
    }

    void Arrive()
    {
        var colour = Colours[(int)(GD.Randi() % Colours.Length)];
        if (Body.For($"rider:dragon_{colour}") is not { } body) return;
        var a = GD.Randf() * Mathf.Tau;
        _from = Pad + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 700 + Vector3.Up * 80;
        _away = Pad + new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a)) * 800;
        _rider = body;
        AddChild(body);
        body.Position = _from;
        _leg = Leg.Coming;
        // Seen from the walls, or from anywhere near.
        if (Near(400) || TownWatch.InTristinwold(main.PlayerNode.GlobalPosition))
            main.HudNode.Toast($"A {colour} dragon with a rider on its back is coming in low over the wilds, toward the hill west of the walls.", 7);
    }

    bool Near(float metres) => main.PlayerNode.GlobalPosition.DistanceTo(Pad with { Y = main.PlayerNode.GlobalPosition.Y }) < metres;

    void Face(Vector3 along)
    {
        if (along.LengthSquared() < 0.01f) return;
        _rider!.Rotation = new Vector3(0, Mathf.Atan2(-along.X, -along.Z), 0);
    }

    /// <summary>For the self-test: a rider brought in at once, nearly down.</summary>
    public void LandForTest()
    {
        if (_rider is null) Arrive();
        if (_rider is not null) _rider.Position = Pad with { Y = City.FloorAt(Pad) + 3 } + new Vector3(4, 0, 4);
    }
}
