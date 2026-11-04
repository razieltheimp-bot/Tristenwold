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
        if (_vessel is not { Aboard: false } ship) return null;
        if (ship.OnDeck(pos)) return pos.DistanceTo(ship.WheelAt) < 2.4f ? ("take the wheel", BoardVessel) : ("step ashore", LeaveVessel);
        if (pos.DistanceTo(ship.GlobalPosition with { Y = pos.Y }) > 9) return null;
        return ($"go aboard {_sea.ShipName}", GoAboard);
    }

    /// <summary>Your company aboard while she sails: each at their place on her deck, going where she goes.</summary>
    public void CrewAboard(OwnVessel ship)
    {
        var i = 0;
        foreach (var c in _companions.Where(c => IsInstanceValid(c) && c.State != EnemyState.Dead && c != _player.Mount))
        {
            c.Carried = true;
            c.GlobalPosition = ship.GlobalTransform * OwnVessel.CrewSpots[i++ % OwnVessel.CrewSpots.Length];
            c.Rotation = new Vector3(0, ship.Rotation.Y + (i % 2 == 0 ? 0.6f : -0.6f), 0);
        }
    }

    /// <summary>The wheel let go: they're free about her deck again, on their own feet.</summary>
    void CrewAtEase()
    {
        foreach (var c in _companions.Where(IsInstanceValid)) c.Carried = false;
    }

    /// <summary>Your company over the side with you: onto the deck going aboard, or ashore beside you going ashore.</summary>
    void CrewTo(Vector3 at, OwnVessel? ship)
    {
        var i = 0;
        foreach (var c in _companions.Where(c => IsInstanceValid(c) && c.State != EnemyState.Dead && c != _player.Mount))
        {
            c.Carried = false;
            var spot = ship is not null ? ship.GlobalTransform * OwnVessel.CrewSpots[i % OwnVessel.CrewSpots.Length] + Vector3.Up * 0.3f
                : at + new Vector3(Mathf.Cos(i * 1.3f) * 2.2f, 0, Mathf.Sin(i * 1.3f) * 2.2f) with { Y = 0 };
            if (ship is null) spot.Y = City.FloorAt(spot) + 0.4f;
            c.GlobalPosition = spot;
            c.ResetPhysicsInterpolation();
            i++;
        }
    }

    /// <summary>Up the side and onto her deck, amidships: walk about her, and take the wheel when you want her to go.</summary>
    void GoAboard()
    {
        if (_vessel is not { } ship || _player.Mount is not null) return;
        ship.Halt();
        _player.GlobalPosition = ship.DeckSpot;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        CrewTo(ship.DeckSpot, ship); // and your company with you
        _hud.Toast($"Aboard {_sea.ShipName}. Walk about her deck; E at the wheel, up on the quarterdeck, to take her out, or E at the rail to step ashore.", 7);
    }

    /// <summary>E at the wheel: let go of it and stand up on the quarterdeck. She drifts to a stop and waits.</summary>
    void LeaveWheel()
    {
        if (_vessel is not { } ship) return;
        ship.Aboard = false;
        ship.Halt();
        (_sea.ShipX, _sea.ShipZ) = (ship.GlobalPosition.X, ship.GlobalPosition.Z);
        _player.StandUp(ship.BesideWheel);
        CrewAtEase();
        ShipShopUnderWay(false);
        _hud.Toast($"You let go of the wheel and {_sea.ShipName} drifts to a stop. Walk about her deck; E at the wheel to take her again, or at the rail to step ashore.", 6);
        Save();
    }

    void BoardVessel()
    {
        if (_vessel is not { } ship || _player.Mount is not null) return;
        ship.Aboard = true;
        ShipShopUnderWay(true); // the shop aboard shuts while she sails
        _player.Rotation = new Vector3(0, ship.Rotation.Y, 0);
        _player.SitAt(() => ship.SeatAt);
        _hud.Toast($"At {_sea.ShipName}'s wheel. W and S for the sails, A and D to bring her round{(_sea.Flies ? ", Space to take her up and C to bring her down: with fire in her she flies anywhere" : "")}. Whatever you come close to is found. E to let go of the wheel and walk her deck.", 10);
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
        ship.Halt();
        (_sea.ShipX, _sea.ShipZ) = (ship.GlobalPosition.X, ship.GlobalPosition.Z);
        var ashore = step with { Y = City.FloorAt(step) + 0.3f };
        if (_player.Seat is not null) _player.StandUp(ashore);
        else { _player.GlobalPosition = ashore; _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation(); }
        CrewTo(ashore, null); // they come ashore with you
        ShipShopUnderWay(false);
        Save();
    }

    /// <summary>For the self-test.</summary>
    public OwnVessel? VesselNode => _vessel;
    public void BoardVesselForTest() { TendVessel(); BoardVessel(); }
    public void LeaveVesselForTest() => LeaveVessel();
    public void LeaveWheelForTest() => LeaveWheel();
    public IEnumerable<Enemy> CrewForTest => _companions.Where(IsInstanceValid);
    public Bond AdoptForTest(string kind) => Adopt(kind, dragon: false, _player.GlobalPosition + Vector3.Back * 2, level: 6);
    public void GoAboardForTest() => GoAboard();
    public string? VesselVerbForTest => VesselAction(_player.GlobalPosition)?.Verb;
    public bool UninhabitedIslandForTest(Vector3 at) => UninhabitedIsland(at);
    public string? NoRoomForColonyForTest(Vector3 at) => NoRoomForColony(at);
    public double ColonySpacingForTest => ColonySpacing;
    public void FoundColonyForTest() => FoundHall(Halls.PlanOf(HallKind.Colony));
}

/// <summary>
/// The hero's own ship on the open water: a planked hull with a pointed bow and a bowsprit, bulwarks with a cap rail
/// all round, a hatch and her cargo amidships, a mast with its yards, crow's nest, shrouds and stays, a square sail, and
/// a raised quarterdeck at the stern with stairs up to it, the cabin door and stern windows under it, a lantern, and
/// the ship's wheel on its post. Her deck is solid: the hero can let go of the wheel and walk about her, and take the
/// wheel again. A boiler, if she has one and it's lit, drives her faster.
/// </summary>
public partial class OwnVessel(Main main) : Node3D
{
    public bool Aboard { get; set; }
    /// <summary>Another hero's ship, tied up with a shop aboard: her own name, and nobody at her wheel.</summary>
    public string? Called { get; init; }
    public bool Moored { get; init; }
    float _speed;
    double _aground;
    MeshInstance3D? _sail;
    Node3D? _wheel;
    float _spin;

    public const float MostSpeed = 13;
    /// <summary>Her main deck and her quarterdeck, above the waterline.</summary>
    public const float DeckTop = 1.7f, QuarterTop = 2.7f;

    public Vector3 SeatAt => GlobalPosition + GlobalTransform.Basis * new Vector3(0, 3.1f, 4.6f);
    /// <summary>The wheel, and where the hero stands up from it (just behind, on the quarterdeck).</summary>
    public Vector3 WheelAt => GlobalPosition + GlobalTransform.Basis * new Vector3(0, QuarterTop + 0.9f, 3.75f);
    public Vector3 BesideWheel => GlobalPosition + GlobalTransform.Basis * new Vector3(0.9f, QuarterTop + 0.2f, 4.8f);
    /// <summary>Amidships, where a hero coming aboard over the side stands.</summary>
    public Vector3 DeckSpot => GlobalPosition + GlobalTransform.Basis * new Vector3(1.1f, DeckTop + 0.2f, -1.5f);

    /// <summary>Standing on her: inside her rails, on her deck or her quarterdeck.</summary>
    public bool OnDeck(Vector3 at)
    {
        var l = ToLocal(at);
        return Mathf.Abs(l.X) < 2.1f && l.Z > -8.4f && l.Z < 6.2f && l.Y > DeckTop - 0.6f && l.Y < QuarterTop + 2.5f;
    }

    /// <summary>Way off her: she stops where she is.</summary>
    public void Halt() => (_speed, _climb) = (0, 0);

    /// <summary>Where the hero's company stand on her deck while she sails, clear of the mast, the hatch, the cargo and the wheel.</summary>
    public static readonly Vector3[] CrewSpots =
    [
        new(1.0f, DeckTop + 0.05f, -1.7f), new(-0.6f, DeckTop + 0.05f, -0.2f), new(0.9f, DeckTop + 0.05f, 1.3f), new(-0.8f, DeckTop + 0.05f, -4.6f),
        new(0.7f, DeckTop + 0.05f, -5.9f), new(-0.3f, DeckTop + 0.05f, -7.0f), new(1.2f, QuarterTop + 0.05f, 5.2f), new(-1.2f, QuarterTop + 0.05f, 5.2f),
    ];

    static StandardMaterial3D Mat(string hex, float rough = 0.8f) => new() { AlbedoColor = new Color(hex), Roughness = rough };

    StaticBody3D? _solid;

    public override void _Ready()
    {
        var hull = Mat("#5e3e24");
        var strake = Mat("#3e2816");
        var plank = Mat("#a07a52");
        var seam = Mat("#6a4e32");
        var rail = Mat("#4a3420");
        var cap = Mat("#8a6440");
        var spar = Mat("#5a4028");
        var rope = Mat("#8c7a58", 1);
        var brass = new StandardMaterial3D { AlbedoColor = new Color("#c49a3a"), Metallic = 0.8f, Roughness = 0.35f };
        var iron = Mat("#2e2e32", 0.5f);
        var glow = new StandardMaterial3D { AlbedoColor = new Color("#ffd27a"), EmissionEnabled = true, Emission = new Color("#ffb040"), EmissionEnergyMultiplier = 1.6f };
        // Her solid parts, to stand on and bump into: a body that goes where she goes.
        AddChild(_solid = new StaticBody3D());

        MeshInstance3D Box(Node3D on, Vector3 at, Vector3 size, Material m, float yaw = 0, float pitch = 0)
        {
            var b = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at, Rotation = new Vector3(pitch, yaw, 0) };
            on.AddChild(b);
            return b;
        }
        void Solid(Vector3 at, Vector3 size, float yaw = 0, float pitch = 0) =>
            _solid!.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = at, Rotation = new Vector3(pitch, yaw, 0) });
        void Rod(Node3D on, Vector3 a, Vector3 b, float r, Material m)
        {
            var mid = (a + b) / 2;
            var len = a.DistanceTo(b);
            var rod = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = len, RadialSegments = 6 }, MaterialOverride = m, Position = mid };
            on.AddChild(rod);
            var dir = (b - a).Normalized();
            if (Mathf.Abs(dir.Dot(Vector3.Up)) < 0.999f) rod.Basis = new Basis(new Quaternion(Vector3.Up, dir));
        }
        void Cyl(Node3D on, Vector3 at, float r, float h, Material m, Vector3 rot = default) =>
            on.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = 12 }, MaterialOverride = m, Position = at, Rotation = rot });

        // ---- the hull: planked, a wale of brass-painted trim, a keel, and a pointed bow ----
        Box(this, new Vector3(0, 0.6f, 0), new Vector3(4, 2.2f, 12), hull);
        foreach (var side in new[] { -1f, 1f })
        {
            foreach (var y in new[] { -0.2f, 0.35f, 0.9f }) Box(this, new Vector3(side * 2.01f, y, 0), new Vector3(0.04f, 0.07f, 12), strake);
            Box(this, new Vector3(side * 2.03f, 1.45f, 0), new Vector3(0.06f, 0.16f, 12), brass); // the wale
        }
        Box(this, new Vector3(0, -0.55f, -0.5f), new Vector3(0.3f, 0.3f, 13.5f), strake); // the keel
        var bow = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(4, 2.6f, 2.2f) }, MaterialOverride = hull, Position = new Vector3(0, 0.6f, -7.3f), Rotation = new Vector3(-Mathf.Pi / 2, 0, 0) };
        AddChild(bow);
        Rod(this, new Vector3(0, 1.6f, -8.5f), new Vector3(0, 3.2f, -12f), 0.11f, spar); // the bowsprit
        Box(this, new Vector3(0, 1.1f, -8.45f), new Vector3(0.25f, 0.9f, 0.2f), brass); // a little figurehead plate

        // ---- the deck: light planks with their seams, a hatch amidships ----
        Box(this, new Vector3(0, DeckTop + 0.01f, -0.6f), new Vector3(3.8f, 0.03f, 11.2f), plank);
        for (var i = -3; i <= 3; i++) Box(this, new Vector3(i * 0.52f, DeckTop + 0.03f, -0.6f), new Vector3(0.02f, 0.01f, 11.2f), seam);
        var bowDeck = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(3.6f, 2.3f, 0.03f) }, MaterialOverride = plank, Position = new Vector3(0, DeckTop + 0.01f, -7.3f), Rotation = new Vector3(-Mathf.Pi / 2, 0, 0) };
        AddChild(bowDeck);
        Box(this, new Vector3(0, DeckTop + 0.08f, -3.2f), new Vector3(1.4f, 0.14f, 1.4f), seam); // the hatch
        for (var i = -2; i <= 2; i++) Box(this, new Vector3(i * 0.28f, DeckTop + 0.16f, -3.2f), new Vector3(0.06f, 0.03f, 1.4f), rail);
        Solid(new Vector3(0, DeckTop - 0.25f, -0.6f), new Vector3(3.9f, 0.5f, 11.4f));
        Solid(new Vector3(0, DeckTop - 0.25f, -7.2f), new Vector3(2.2f, 0.5f, 1.8f));

        // ---- the bulwarks: plank sides, posts, and a cap rail all round ----
        foreach (var side in new[] { -1f, 1f })
        {
            Box(this, new Vector3(side * 1.95f, DeckTop + 0.3f, -0.6f), new Vector3(0.12f, 0.6f, 11.2f), rail);
            Box(this, new Vector3(side * 1.95f, DeckTop + 0.63f, -0.6f), new Vector3(0.24f, 0.06f, 11.3f), cap);
            for (var z = -5.8f; z <= 5f; z += 1.4f) Box(this, new Vector3(side * 1.86f, DeckTop + 0.3f, z), new Vector3(0.1f, 0.6f, 0.1f), strake);
            Solid(new Vector3(side * 1.97f, DeckTop + 0.45f, -0.6f), new Vector3(0.2f, 0.9f, 11.4f));
            // Along the bow, to its point.
            var from = new Vector3(side * 1.95f, 0, -6.2f);
            var to = new Vector3(0, 0, -8.5f);
            var along = to - from;
            var yaw = Mathf.Atan2(-along.X, -along.Z);
            var mid = (from + to) / 2;
            Box(this, mid with { Y = DeckTop + 0.3f }, new Vector3(0.12f, 0.6f, along.Length()), rail, yaw);
            Box(this, mid with { Y = DeckTop + 0.63f }, new Vector3(0.24f, 0.06f, along.Length()), cap, yaw);
            Solid(mid with { Y = DeckTop + 0.45f }, new Vector3(0.2f, 0.9f, along.Length()), yaw);
        }

        // ---- the quarterdeck: raised at the stern, stairs up to it, its own rail, the cabin door and windows under it ----
        Box(this, new Vector3(0, 2.2f, 4.6f), new Vector3(3.9f, 1, 2.8f), hull);
        Box(this, new Vector3(0, QuarterTop + 0.01f, 4.6f), new Vector3(3.8f, 0.03f, 2.7f), plank);
        for (var i = -3; i <= 3; i++) Box(this, new Vector3(i * 0.52f, QuarterTop + 0.03f, 4.6f), new Vector3(0.02f, 0.01f, 2.7f), seam);
        Solid(new Vector3(0, 2.2f, 4.6f), new Vector3(3.9f, 1, 2.8f));
        Box(this, new Vector3(0.6f, 2.15f, 3.19f), new Vector3(0.8f, 0.9f, 0.04f), strake); // the cabin door
        Cyl(this, new Vector3(0.9f, 2.15f, 3.16f), 0.04f, 0.04f, brass, new Vector3(Mathf.Pi / 2, 0, 0));
        foreach (var x in new[] { -0.9f, 0f, 0.9f }) Box(this, new Vector3(x, 2.1f, 6.02f), new Vector3(0.5f, 0.4f, 0.04f), glow); // the stern windows
        Box(this, new Vector3(0, 1.4f, 6.03f), new Vector3(3.6f, 0.12f, 0.04f), brass);
        var name = Called ?? main.Sea.ShipName;
        var label = new Label3D { Text = name, Position = new Vector3(0, 1.0f, 6.06f), FontSize = 64, OutlineSize = 6, PixelSize = 0.006f, Modulate = new Color("#e8c060"), Rotation = new Vector3(0, 0, 0) };
        AddChild(label); // her name across her stern
        // Stairs up from the main deck, on the port side: steps to look at, a slope underfoot.
        for (var k = 0; k < 3; k++) Box(this, new Vector3(-1.4f, DeckTop + (k + 1) * 0.165f, 2.2f + k * 0.33f), new Vector3(0.9f, (k + 1) * 0.33f, 0.33f), plank);
        Solid(new Vector3(-1.4f, (DeckTop + QuarterTop) / 2, 2.6f), new Vector3(0.9f, 0.1f, 1.55f), 0, -Mathf.Atan2(QuarterTop - DeckTop, 1.15f));
        // Its rail: across the front (leaving the stairs a gap), down both sides and across the stern.
        void QRail(Vector3 at, Vector3 size)
        {
            Box(this, at with { Y = QuarterTop + 0.45f }, size with { Y = 0.06f }, cap);
            Box(this, at with { Y = QuarterTop + 0.22f }, size with { Y = 0.05f }, rail);
            Solid(at with { Y = QuarterTop + 0.45f }, size with { Y = 0.9f });
            var long_ = size.X > size.Z;
            var n = (int)((long_ ? size.X : size.Z) / 0.45f);
            for (var k = 0; k <= n; k++)
            {
                var t = n == 0 ? 0 : (float)k / n - 0.5f;
                Box(this, (at + (long_ ? new Vector3(t * size.X, 0, 0) : new Vector3(0, 0, t * size.Z))) with { Y = QuarterTop + 0.22f }, new Vector3(0.06f, 0.45f, 0.06f), rail);
            }
        }
        QRail(new Vector3(0.45f, 0, 3.25f), new Vector3(2.9f, 0, 0.1f));
        foreach (var side in new[] { -1f, 1f }) QRail(new Vector3(side * 1.9f, 0, 4.6f), new Vector3(0.1f, 0, 2.8f));
        QRail(new Vector3(0, 0, 5.95f), new Vector3(3.9f, 0, 0.1f));
        // A lantern on its post at the stern.
        Rod(this, new Vector3(0, QuarterTop, 5.95f), new Vector3(0, QuarterTop + 1.6f, 6.3f), 0.05f, iron);
        Box(this, new Vector3(0, QuarterTop + 1.55f, 6.35f), new Vector3(0.3f, 0.4f, 0.3f), glow);
        Box(this, new Vector3(0, QuarterTop + 1.8f, 6.35f), new Vector3(0.36f, 0.08f, 0.36f), iron);

        // ---- the wheel: on its post at the front of the quarterdeck, eight spokes and their handles, a brass hub ----
        Box(this, new Vector3(0, QuarterTop + 0.45f, 3.95f), new Vector3(0.22f, 0.9f, 0.22f), rail); // the post
        Box(this, new Vector3(0, QuarterTop + 0.92f, 3.95f), new Vector3(0.3f, 0.12f, 0.36f), cap);
        Solid(new Vector3(0, QuarterTop + 0.45f, 3.95f), new Vector3(0.3f, 0.9f, 0.3f));
        AddChild(_wheel = new Node3D { Position = new Vector3(0, QuarterTop + 0.95f, 3.72f) });
        Cyl(_wheel, Vector3.Zero, 0.09f, 0.22f, brass, new Vector3(Mathf.Pi / 2, 0, 0)); // the hub
        _wheel.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.46f, OuterRadius = 0.54f, Rings = 28, RingSegments = 8 }, MaterialOverride = cap, Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        _wheel.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.25f, Rings = 18, RingSegments = 6 }, MaterialOverride = cap, Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        for (var i = 0; i < 8; i++)
        {
            var spoke = new Node3D { Rotation = new Vector3(0, 0, i * Mathf.Pi / 4) };
            _wheel.AddChild(spoke);
            Cyl(spoke, new Vector3(0, 0.3f, 0), 0.025f, 0.6f, cap);
            spoke.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.045f, Height = 0.2f, RadialSegments = 8 }, MaterialOverride = rail, Position = new Vector3(0, 0.68f, 0) }); // a handle
        }

        // ---- the mast: yards, a crow's nest, a pennant, the shrouds and stays ----
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.2f, Height = 11 }, MaterialOverride = spar, Position = new Vector3(0, 6.5f, -0.5f) });
        _solid.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.2f, Height = 3 }, Position = new Vector3(0, DeckTop + 1.5f, -0.5f) });
        Cyl(this, new Vector3(0, DeckTop + 0.15f, -0.5f), 0.32f, 0.3f, rail); // its step on the deck
        Rod(this, new Vector3(-3.3f, 9.9f, -0.4f), new Vector3(3.3f, 9.9f, -0.4f), 0.08f, spar); // the yard, the sail hung from it
        Rod(this, new Vector3(-3f, 4.9f, -0.4f), new Vector3(3f, 4.9f, -0.4f), 0.07f, spar); // and the boom under it
        Cyl(this, new Vector3(0, 10.7f, -0.5f), 0.6f, 0.5f, rail); // the crow's nest
        Cyl(this, new Vector3(0, 11f, -0.5f), 0.62f, 0.06f, cap);
        Box(this, new Vector3(0.45f, 12.3f, -0.5f), new Vector3(0.8f, 0.35f, 0.02f), Mat("#a8302a")); // a pennant
        AddChild(_sail = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(6, 5, 0.08f) }, MaterialOverride = Mat("#e8dcc0", 1), Position = new Vector3(0, 7.4f, -0.3f) });
        foreach (var side in new[] { -1f, 1f })
            for (var k = 0; k < 3; k++) Rod(this, new Vector3(0, 10.4f, -0.5f), new Vector3(side * 1.95f, DeckTop + 0.65f, -1.6f + k * 1.1f), 0.02f, rope); // the shrouds
        Rod(this, new Vector3(0, 11.8f, -0.5f), new Vector3(0, 3.2f, -12f), 0.025f, rope); // the forestay, to the bowsprit
        Rod(this, new Vector3(0, 11.8f, -0.5f), new Vector3(0, QuarterTop + 0.5f, 5.9f), 0.025f, rope); // the backstay

        // ---- her cargo: barrels and crates lashed by the rails ----
        var barrel = Mat("#7a5634");
        foreach (var (x, z) in new[] { (1.3f, -4.8f), (1.3f, -4.1f), (-1.3f, -1.8f) })
        {
            Cyl(this, new Vector3(x, DeckTop + 0.42f, z), 0.3f, 0.8f, barrel);
            foreach (var y in new[] { 0.15f, 0.7f }) Cyl(this, new Vector3(x, DeckTop + y, z), 0.315f, 0.05f, iron);
            Solid(new Vector3(x, DeckTop + 0.42f, z), new Vector3(0.6f, 0.8f, 0.6f));
        }
        foreach (var (x, z, s) in new[] { (-1.25f, 0.8f, 0.7f), (-1.25f, 1.45f, 0.55f) })
        {
            Box(this, new Vector3(x, DeckTop + s / 2, z), new Vector3(s, s, s), Mat("#8a6a44"));
            Solid(new Vector3(x, DeckTop + s / 2, z), new Vector3(s, s, s));
        }

        if (!Moored && main.Sea.Steam)
        {
            Cyl(this, new Vector3(1.0f, DeckTop + 1.6f, 1.6f), 0.28f, 3.2f, iron); // the funnel
            Cyl(this, new Vector3(1.0f, DeckTop + 3.2f, 1.6f), 0.34f, 0.12f, brass);
            Solid(new Vector3(1.0f, DeckTop + 1.6f, 1.6f), new Vector3(0.6f, 3.2f, 0.6f));
        }
        AddChild(new Label3D { Text = name, Position = new Vector3(0, 13.4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        if (_sail is not null) _sail.Scale = new Vector3(1, 0.55f + 0.45f * Mathf.Clamp(_speed / MostSpeed, 0, 1), 1); // the sail fills
        if (!Aboard || Moored) return;
        var talking = main.HudNode.Talking;
        var forward = !talking && Input.IsActionPressed("move_forward");
        var back = !talking && Input.IsActionPressed("move_back");
        var turn = talking ? 0 : (Input.IsActionPressed("move_left") ? 1 : 0) - (Input.IsActionPressed("move_right") ? 1 : 0);
        var most = MostSpeed * (float)(forward && main.Sea.Steam && main.Sea.Fuel > 0 ? Engines.SteamPace : 1);
        if (forward && main.Sea.Steam && main.Sea.Fuel > 0) main.Sea.Fuel = Math.Max(0, main.Sea.Fuel - delta);
        _speed = Mathf.Clamp(_speed + (forward ? 5 : back ? -6 : -0.6f) * d, -2, most); // like a wagon's box: W pushes her on, S pulls her up
        Rotation += new Vector3(0, turn * d * 0.7f, 0); // and A and D put her wherever you like
        _spin = turn != 0 ? Mathf.Clamp(_spin + turn * d * 3, -2.5f, 2.5f) : Mathf.MoveToward(_spin, 0, d * 1.5f);
        if (_wheel is not null) _wheel.Rotation = new Vector3(0, 0, _spin); // the wheel goes round under your hands
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
        main.CrewAboard(this); // the company on her deck, not swimming after her
    }

    float _climb;

    /// <summary>What she comes down on: the water, or the land (a ship that flies can set down anywhere).</summary>
    public static float Floor(Vector3 at) => Mathf.Max(City.FloorAt(at), (float)WorldGen.WaterLevel);

    /// <summary>Set down on dry land, where her crew can climb down beside her.</summary>
    public bool OnLand => GlobalPosition.Y - Floor(GlobalPosition) < 1.2f && Floor(GlobalPosition) > WorldGen.WaterLevel + 0.2f;
}
