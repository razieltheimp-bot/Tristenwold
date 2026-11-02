using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The open sea under the hero's own hand: a ship of their own taken out from the harbour, steered anywhere on the
/// water, to the islands and the far coasts. Every place they come close to is found, as it is on land, and joins their
/// trade. Stepped off at a shore or a pier, she waits there; the harbour master's crew will always bring her home.
/// </summary>
public partial class Main
{
    OwnVessel? _vessel;
    public static readonly Vector3 ShipBerth = new(148, 0, 5);
    public bool AboardShip => _vessel is { Aboard: true };

    IEnumerable<(string, Action)> SailTalk(Person person)
    {
        if (person.Npc.Id != HarbourMaster || !_sea.OwnShip || _sea.Voyage is not null || AboardShip) yield break;
        yield return ($"I'll take {_sea.ShipName} out myself, on the open sea.", () =>
        {
            _hud.EndTalk();
            if (_sea.Hull < SeaBook.Seaworthy) { Say(person, $"Not in that state. Get {_sea.ShipName} mended first."); return; }
            (_sea.ShipX, _sea.ShipZ) = (ShipBerth.X, ShipBerth.Z);
            _vessel?.QueueFree();
            _vessel = null;
            TendVessel();
            BoardVessel();
        });
        if (_sea.ShipX != ShipBerth.X || _sea.ShipZ != ShipBerth.Z)
            yield return ($"Have the crew bring {_sea.ShipName} home.", () => { (_sea.ShipX, _sea.ShipZ) = (ShipBerth.X, ShipBerth.Z); _vessel?.QueueFree(); _vessel = null; Save(); Say(person, "She'll be alongside the middle pier by morning."); });
    }

    /// <summary>The hero's ship drawn where they left her, when they're near.</summary>
    void TendVessel()
    {
        if (!_sea.OwnShip) { _vessel?.QueueFree(); _vessel = null; return; }
        if (_vessel is { Aboard: true }) return;
        var at = new Vector3((float)_sea.ShipX, 0, (float)_sea.ShipZ);
        var near = new Vector2(at.X - _player.GlobalPosition.X, at.Z - _player.GlobalPosition.Z).Length() < 300 && _indoors is null && _cave is null && _atSea is null;
        if (!near || at == ShipBerth && _sea.Voyage is not null) { _vessel?.QueueFree(); _vessel = null; return; }
        if (_vessel is not null) return;
        _vessel = new OwnVessel(this);
        AddChild(_vessel);
        _vessel.GlobalPosition = at with { Y = (float)WorldGen.WaterLevel };
    }

    (string Verb, Action Act)? VesselAction(Vector3 pos)
    {
        if (_vessel is not { Aboard: false } ship || pos.DistanceTo(ship.GlobalPosition with { Y = pos.Y }) > 8) return null;
        return ($"take the helm of {_sea.ShipName}", BoardVessel);
    }

    void BoardVessel()
    {
        if (_vessel is not { } ship || _player.Mount is not null) return;
        ship.Aboard = true;
        _player.Rotation = new Vector3(0, ship.Rotation.Y, 0);
        _player.SitAt(() => ship.SeatAt);
        _hud.Toast($"At {_sea.ShipName}'s wheel. W and S for the sails, A and D to bring her round{(_sea.Flies ? ", Space to take her up and C to bring her down: with fire in her she flies anywhere" : "")}. Whatever you come close to is found. E to step ashore at a shore or a pier.", 10);
    }

    /// <summary>Ashore, if there's a shore or a pier to step onto close by; she waits where she is.</summary>
    void LeaveVessel()
    {
        if (_vessel is not { } ship) return;
        Vector3? dry = null;
        for (var r = 4f; r <= 12 && dry is null; r += 2)
            for (var a = 0; a < 16 && dry is null; a++)
            {
                var probe = ship.GlobalPosition + new Vector3(Mathf.Cos(a * Mathf.Tau / 16), 0, Mathf.Sin(a * Mathf.Tau / 16)) * r;
                if (City.OnPier(probe) || _world.Height(probe.X, probe.Z) > WorldGen.WaterLevel + 0.3) dry = probe;
            }
        if (ship.OnLand) dry = ship.GlobalPosition + ship.GlobalTransform.Basis.X * 4;
        if (dry is not { } step) { _hud.Toast(ship.GlobalPosition.Y > OwnVessel.Floor(ship.GlobalPosition) + 1 ? "Bring her down first (C)." : "No shore or pier close enough to step onto. Bring her in alongside.", 4); return; }
        ship.Aboard = false;
        (_sea.ShipX, _sea.ShipZ) = (ship.GlobalPosition.X, ship.GlobalPosition.Z);
        _player.StandUp(step with { Y = City.FloorAt(step) + 0.3f });
        Save();
    }

    /// <summary>For the self-test.</summary>
    public OwnVessel? VesselNode => _vessel;
    public void BoardVesselForTest() { TendVessel(); BoardVessel(); }
    public void LeaveVesselForTest() => LeaveVessel();
    public bool UninhabitedIslandForTest(Vector3 at) => UninhabitedIsland(at);
    public string? NoRoomForColonyForTest(Vector3 at) => NoRoomForColony(at);
    public double ColonySpacingForTest => ColonySpacing;
    public void FoundColonyForTest() => FoundHall(Halls.PlanOf(HallKind.Colony));
}

/// <summary>The hero's own ship on the open water: a hull, a mast and a square sail, steered from the wheel at her stern. A boiler, if she has one and it's lit, drives her faster.</summary>
public partial class OwnVessel(Main main) : Node3D
{
    public bool Aboard { get; set; }
    float _speed;
    double _aground;
    MeshInstance3D? _sail;

    public const float MostSpeed = 13;

    public Vector3 SeatAt => GlobalPosition + GlobalTransform.Basis * new Vector3(0, 3.1f, 4.6f);

    static StandardMaterial3D Mat(string hex) => new() { AlbedoColor = new Color(hex), Roughness = 0.8f };

    public override void _Ready()
    {
        var wood = Mat("#6a4a2c");
        void Box(Vector3 at, Vector3 size, Material m) => AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at });
        Box(new Vector3(0, 0.6f, 0), new Vector3(4, 2.2f, 12), wood); // the hull
        Box(new Vector3(0, 0.2f, -6.6f), new Vector3(2.4f, 1.6f, 1.6f), wood); // the bow
        Box(new Vector3(0, 2.2f, 4.6f), new Vector3(3.6f, 1, 2.6f), Mat("#5a3a22")); // the stern deck, and the wheel on it
        foreach (var side in new[] { -1f, 1f }) Box(new Vector3(side * 1.95f, 2, 0), new Vector3(0.15f, 0.6f, 12), Mat("#4a3420"));
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 11 }, MaterialOverride = Mat("#5a4028"), Position = new Vector3(0, 6.5f, -0.5f) });
        AddChild(_sail = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(6, 5, 0.08f) }, MaterialOverride = Mat("#e8dcc0"), Position = new Vector3(0, 7.4f, -0.3f) });
        if (main.Sea.Steam) AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.3f, Height = 3 }, MaterialOverride = Mat("#2e2e32"), Position = new Vector3(0, 3.2f, 2) });
        AddChild(new Label3D { Text = main.Sea.ShipName, Position = new Vector3(0, 13, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        if (_sail is not null) _sail.Scale = new Vector3(1, 0.55f + 0.45f * Mathf.Clamp(_speed / MostSpeed, 0, 1), 1); // the sail fills
        if (!Aboard) return;
        var talking = main.HudNode.Talking;
        var forward = !talking && Input.IsActionPressed("move_forward");
        var back = !talking && Input.IsActionPressed("move_back");
        var turn = talking ? 0 : (Input.IsActionPressed("move_left") ? 1 : 0) - (Input.IsActionPressed("move_right") ? 1 : 0);
        var most = MostSpeed * (float)(forward && main.Sea.Steam && main.Sea.Fuel > 0 ? Engines.SteamPace : 1);
        if (forward && main.Sea.Steam && main.Sea.Fuel > 0) main.Sea.Fuel = Math.Max(0, main.Sea.Fuel - delta);
        _speed = Mathf.Clamp(_speed + (forward ? 5 : back ? -6 : -0.6f) * d, -2, most); // like a wagon's box: W pushes her on, S pulls her up
        Rotation += new Vector3(0, turn * d * 0.7f, 0); // and A and D put her wherever you like
        // A ship with a lift goes up with fire in her, and flies anywhere, over land or sea, till the fire's out.
        var lift = main.Sea.Flies && main.Sea.Fuel > 0;
        var up = !talking && Input.IsActionPressed("jump") && lift;
        var down = !talking && Input.IsActionPressed("descend");
        var at = GlobalPosition - GlobalTransform.Basis.Z * _speed * d;
        var floor = Floor(at);
        var aloft = GlobalPosition.Y > Floor(GlobalPosition) + 1;
        if (aloft || up) main.Sea.Fuel = Math.Max(0, main.Sea.Fuel - delta * 1.5);
        _climb = Mathf.Lerp(_climb, up ? 7 : down ? -7 : aloft && !lift ? -2.5f : 0, d * 2);
        var ahead = at - GlobalTransform.Basis.Z * Mathf.Sign(_speed) * 6.5f;
        var blocked = aloft || up
            ? Floor(ahead) > GlobalPosition.Y - 1.5f // a hillside in her way
            : main.World.Height(ahead.X, ahead.Z) > WorldGen.WaterLevel - 1.2 || City.OnPier(ahead); // shoal water, or a pier
        if (blocked)
        {
            _speed = 0;
            at = GlobalPosition;
            if ((_aground -= delta) <= 0) { _aground = 6; main.HudNode.Toast(aloft ? "High ground ahead. Take her up, or bring her about." : main.Sea.Flies ? "Shoal water ahead. Bring her about, or take her up (Space)." : "Shoal water ahead. Bring her about.", 3); }
        }
        var y = Mathf.Clamp(GlobalPosition.Y + _climb * d, floor, floor + Skyboat.MostHeight);
        var afloat = y <= floor + 0.05f && main.World.Height(at.X, at.Z) <= WorldGen.WaterLevel;
        GlobalPosition = at with { Y = afloat ? (float)WorldGen.WaterLevel + Mathf.Sin((float)Time.GetTicksMsec() / 900f) * 0.12f : y };
    }

    float _climb;

    /// <summary>What she comes down on: the water, or the land (a ship that flies can set down anywhere).</summary>
    public static float Floor(Vector3 at) => Mathf.Max(City.FloorAt(at), (float)WorldGen.WaterLevel);

    /// <summary>Set down on dry land, where her crew can climb down beside her.</summary>
    public bool OnLand => GlobalPosition.Y - Floor(GlobalPosition) < 1.2f && Floor(GlobalPosition) > WorldGen.WaterLevel + 0.2f;
}
