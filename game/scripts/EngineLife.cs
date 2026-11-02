using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Steam in the hero's ship and wagon, and a skyboat of their own (see <see cref="Engines"/>). Only an artificer fits
/// them: the hero, if they are one; an artificer friend on the server, from an order left at the yard with the makings;
/// or, for a ship, Brasswake's engineers, for marks. A boiler drives a ship along faster than the wind, a lift takes
/// her up where no pirate can board her, and both burn whatever a fire takes.
/// </summary>
public partial class Main
{
    Skyboat? _skyboat;
    double _skyTick;
    bool _fuelSaid;

    bool Artificer => Engines.IsArtificer(_player.Class.Id);

    /// <summary>What can be done at the yard (Osric's, for the ship and the skyboat; Garrick's, for the wagon) about engines.</summary>
    IEnumerable<(string, Action)> EngineTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id != Shipwright && id != Master) yield break;
        CollectOrders();
        var works = id == Shipwright
            ? new[] { (Engines.ShipSteam, _sea.OwnShip && !_sea.Steam), (Engines.ShipFlight, _sea.OwnShip && _sea.Steam && !_sea.Flies), (Engines.Skyboat, !_sea.Skyboat) }
            : [(Engines.WagonSteam, _trade.OwnWagon && !_trade.Steam && !Artificer)]; // an artificer bolts their own on with Garrick (see SteamTalk)
        foreach (var (work, wanted) in works.Where(w => w.Item2))
        {
            var (name, _, _) = Engines.Works[work];
            var extra = work == Engines.Skyboat ? $" and {Engines.LifeboatPrice} crowns for the lifeboat" : "";
            if (Artificer) yield return ($"Fit {name} myself. ({Engines.Makings(work)}{extra})", () => FitMyself(person, work));
            else if (_city.Orders.ContainsKey($"{_player.Company}#{work}"))
                yield return ($"Take back my order for {name}.", () => TakeBackOrder(person, work));
            else yield return ($"Leave an order for an artificer: {name}. (the makings: {Engines.Makings(work)}{extra}; and what you'll pay them)", () => PostOrder(person, work));
        }
        if (Artificer)
            foreach (var o in _city.Orders.Values.Where(o => o.FittedBy is null && o.Owner != _player.Company && (id == Shipwright) == (o.Work != Engines.WagonSteam)).Take(6).ToList())
                yield return ($"Do {o.Owner}'s order: {Engines.Works[o.Work].Name}. (they pay {o.Pay} crowns)", () => DoOrder(person, o));
        if (id == Shipwright && _sea.Steam) yield return ($"Stoke {_sea.ShipName}'s boiler ({_sea.Fuel:0} seconds of steam)", () => Firebox(_sea.ShipName, () => _sea.Fuel, f => _sea.Fuel = f, _sea.SteamLevel, () => Say(person, "She'll go.")));
    }

    void FitMyself(Person person, string work)
    {
        var price = work == Engines.Skyboat ? Engines.LifeboatPrice : 0;
        if (!Engines.HasMakings(_player.Pack, work) || _player.Crowns < price) { Say(person, $"You'll want {Engines.Makings(work)}{(price > 0 ? $", and {price} crowns for the boat" : "")}. Bring them and the yard's yours."); return; }
        Engines.TakeMakings(_player.Pack, work);
        _player.Crowns -= price;
        Fitted(work, _player.Hero.Level, null);
        Say(person, "Well. I'd not have believed it if I hadn't held the lamp for you.");
    }

    void PostOrder(Person person, string work)
    {
        var price = work == Engines.Skyboat ? Engines.LifeboatPrice : 0;
        if (!Engines.HasMakings(_player.Pack, work)) { Say(person, $"No artificer works without the makings: {Engines.Makings(work)}. Leave them with me and I'll pass the word."); return; }
        var options = new[] { 0, 50, 150, 400 }.Where(pay => _player.Crowns >= pay + price).Select(pay => ($"Offer {(pay == 0 ? "nothing but thanks" : $"{pay} crowns")}", (Action)(() =>
        {
            Engines.TakeMakings(_player.Pack, work);
            _player.Crowns -= pay + price;
            DoCity(CityAction.OrderPost(_player.Company, work, pay));
            Save();
            Say(person, "It's on the board. Any artificer who comes by can take it on, and you'll find it done when you're back.");
        }))).ToList();
        options.Add(("Not yet.", () => Say(person, "Whenever you're ready.")));
        _hud.Talk("An order for an artificer", $"The makings stay here till it's done. What will you pay whoever does it?{(price > 0 ? $" (and {price} crowns for the lifeboat)" : "")}", [.. options]);
    }

    void TakeBackOrder(Person person, string work)
    {
        if (_city.Orders.GetValueOrDefault($"{_player.Company}#{work}") is not { FittedBy: null } o) { CollectOrders(); return; }
        DoCity(CityAction.OrderDone(o.Key));
        foreach (var (item, n) in Engines.Works[work].Needs) _player.Pack.Add(item, n);
        _player.Crowns += o.Pay + (work == Engines.Skyboat ? Engines.LifeboatPrice : 0);
        Save();
        Say(person, "Your makings and your crowns. Nobody's touched them.");
    }

    /// <summary>Another hero's order, done: the work goes to them, and the pay to whoever did it.</summary>
    void DoOrder(Person person, WorkOrder o)
    {
        DoCity(CityAction.OrderFit(o.Key, _player.Company, _player.Hero.Level));
        _player.Crowns += o.Pay;
        GainXp(30 + 5 * _player.Hero.Level);
        Sound.Ui("anvil");
        Save();
        Say(person, $"That's {o.Owner}'s done. {(o.Pay > 0 ? $"{o.Pay} crowns, as they promised." : "They'll owe you a drink.")}");
    }

    /// <summary>The orders a friend has done for this hero: fitted to what's theirs, and off the board.</summary>
    void CollectOrders()
    {
        foreach (var o in _city.Orders.Values.Where(o => o.Owner == _player.Company && o.FittedBy is not null).ToList())
        {
            DoCity(CityAction.OrderDone(o.Key));
            Fitted(o.Work, o.Level, o.FittedBy);
        }
    }

    /// <summary>Brasswake's engineers will fit a ship that's tied up at the Strangers' Dock, for marks.</summary>
    IEnumerable<(string, Action)> IsleEngineTalk(Person person)
    {
        if (person.Npc.Id != Engineer || !_sea.OwnShip || !Isle.Berthed) yield break;
        foreach (var work in new[] { Engines.ShipSteam, Engines.ShipFlight }.Where(w => w == Engines.ShipSteam ? !_sea.Steam : _sea.Steam && !_sea.Flies))
        {
            var (name, _, marks) = Engines.Works[work];
            yield return ($"Have your engineers fit {_sea.ShipName} with {name}. ({marks} marks; you have {Isle.Marks})", () =>
            {
                if (Isle.Marks < marks) { Say(person, $"{marks} marks. The valves are at the stern."); return; }
                Isle.Marks -= marks;
                Fitted(work, Engines.IsleMaker, "Brasswake's engineers");
                Say(person, "Done before your crew had finished their tea. Don't let the Foundry take her apart to see how.");
            });
        }
    }

    /// <summary>Whatever was fitted, now on the hero's ship, wagon or skyboat, as good as its maker.</summary>
    void Fitted(string work, int level, string? by)
    {
        switch (work)
        {
            case Engines.ShipSteam: (_sea.Steam, _sea.SteamLevel) = (true, level); break;
            case Engines.ShipFlight: (_sea.Flies, _sea.SteamLevel) = (true, Math.Max(level, _sea.SteamLevel)); break;
            case Engines.Skyboat: (_sea.Skyboat, _sea.SkyLevel, _sea.SkyX, _sea.SkyZ) = (true, level, SkyboatBerth.X, SkyboatBerth.Z); break;
            default:
                (_trade.Steam, _trade.SteamLevel) = (true, level);
                foreach (var horse in _bonds.Where(b => b.Hitched).ToList()) Unhitch(horse);
                if (_trade.WagonOut) TakeWagon(null);
                break;
        }
        Sound.Ui("quest");
        _hud.Toast((work switch
        {
            Engines.ShipSteam => $"{_sea.ShipName} has a boiler and paddle-wheels: she'll go faster than the wind while there's fire in her.",
            Engines.ShipFlight => $"{_sea.ShipName} has a lift and a gas-bag: with fire in her she'll go up, faster still, and no pirate can board her.",
            Engines.Skyboat => "A skyboat of your own waits on the end of the north pier. Feed her, and take the helm.",
            _ => "Your wagon has a boiler: no horse in the shafts, and anything that burns to drive her.",
        }) + (by is null ? "" : $" ({by} did the work.)"), 10);
        Save();
    }

    /// <summary>A firebox: anything in the pack that burns goes in, for as long as it lasts with that maker's work.</summary>
    void Firebox(string what, Func<double> fuel, Action<double> set, int level, Action back)
    {
        var burns = _player.Pack.Contents.Where(c => SteamWorks.Burns(c.Item.Id, level) > 0).ToList();
        var options = burns.Select(c => ($"{c.Item.Name} ({c.N}): {SteamWorks.Burns(c.Item.Id, level):0} seconds each", (Action)(() =>
        {
            if (_player.Pack.Take(c.Item.Id)) set(Math.Min(SteamWorks.MostFuel, fuel() + SteamWorks.Burns(c.Item.Id, level)));
            _fuelSaid = false;
            Sound.Ui("equip", -6);
            Save();
            Firebox(what, fuel, set, level, back);
        }))).ToList();
        options.Add(("That's enough", back));
        _hud.Talk($"{what}'s firebox", burns.Count == 0 ? "You've nothing that'll burn. Coal, logs, anything a fire takes." : $"{fuel():0} seconds of steam in her.", [.. options]);
    }

    /// <summary>What the pack can do for a ship with a boiler in her, out at sea, and for a skyboat close by.</summary>
    IEnumerable<(string, Action)> EnginePack()
    {
        if (_sea.Steam && _atSea is not null) yield return ($"Stoke {_sea.ShipName}'s boiler ({_sea.Fuel:0} seconds)", () => Firebox(_sea.ShipName, () => _sea.Fuel, f => _sea.Fuel = f, _sea.SteamLevel, _hud.EndTalk));
        if (_skyboat is not null && _player.GlobalPosition.DistanceTo(_skyboat.GlobalPosition) < 8) yield return ($"Stoke the skyboat ({_sea.SkyFuel:0} seconds)", SkyFirebox);
    }

    void SkyFirebox() => Firebox("The skyboat", () => _sea.SkyFuel, f => _sea.SkyFuel = f, _sea.SkyLevel, _hud.EndTalk);

    /// <summary>How much faster her engine drives her along this moment, burning fuel to do it; and a word when the fire goes out.</summary>
    public double EnginePace(double delta)
    {
        if (!_sea.OwnShip || !_sea.Steam || _sea.Voyage is not { InPort: false } v) return 1;
        if (_sea.Fuel <= 0)
        {
            if (!_fuelSaid) { _fuelSaid = true; _hud.Toast($"{_sea.ShipName}'s fire's out, and she's under sail alone. Stoke her from your pack (I).", 7); }
            return 1;
        }
        var flying = _sea.Flies && v.Out.Count + v.Home.Count == 0;
        _sea.Fuel = Math.Max(0, _sea.Fuel - delta * (flying ? 1.5 : 1));
        return flying ? Engines.FlightPace : Engines.SteamPace;
    }

    // ---- the skyboat ------------------------------------------------------------------------------------

    /// <summary>Where a new skyboat waits: the end of the north pier.</summary>
    static readonly Vector3 SkyboatBerth = new(118, 0, -30);
    public bool AboardSkyboat => _skyboat is { Aboard: true };

    /// <summary>The skyboat drawn when the hero's near where they left her, and gone when they're far.</summary>
    void TendSkyboat(double delta)
    {
        if ((_skyTick -= delta) > 0) return;
        _skyTick = 1;
        TendSite();
        if (!_sea.Skyboat) { _skyboat?.QueueFree(); _skyboat = null; return; }
        if (_skyboat is { Aboard: true }) return;
        var at = new Vector3((float)_sea.SkyX, 0, (float)_sea.SkyZ);
        var near = new Vector2(at.X - _player.GlobalPosition.X, at.Z - _player.GlobalPosition.Z).Length() < 260 && _indoors is null && _cave is null && _atSea is null;
        if (!near) { _skyboat?.QueueFree(); _skyboat = null; return; }
        if (_skyboat is not null || !_terrain.HasGround(at)) return;
        _skyboat = new Skyboat(this);
        AddChild(_skyboat);
        _skyboat.GlobalPosition = at with { Y = Skyboat.Floor(at) };
    }

    (string Verb, Action Act)? SkyAction(Vector3 pos)
    {
        if (_skyboat is not { Aboard: false } boat || pos.DistanceTo(boat.GlobalPosition) > 4.5f) return null;
        return ("see to your skyboat", () => _hud.Talk("Your skyboat", $"A lifeboat under a gas-bag, a boiler amidships and a screw at her stern. {_sea.SkyFuel:0} seconds of steam in her.",
            ("Take the helm", () => { _hud.EndTalk(); BoardSkyboat(); }),
            ("Stoke her", SkyFirebox),
            ("Leave her", _hud.EndTalk)));
    }

    void BoardSkyboat()
    {
        if (_skyboat is not { } boat || _player.Mount is not null) return;
        boat.Aboard = true;
        _player.Rotation = new Vector3(0, boat.Rotation.Y, 0);
        _player.SitAt(() => boat.SeatAt);
        _hud.Toast("At the helm. W opens the throttle and S closes it, A and D bring her round, Space takes her up and C brings her down. E to step off when she's down.", 10);
    }

    void LeaveSkyboat()
    {
        if (_skyboat is not { } boat) return;
        if (!boat.Landed) { _hud.Toast("Bring her down first (C).", 3); return; }
        boat.Aboard = false;
        (_sea.SkyX, _sea.SkyZ) = (boat.GlobalPosition.X, boat.GlobalPosition.Z);
        var side = boat.GlobalPosition + boat.GlobalTransform.Basis.X * 2.6f;
        _player.StandUp(side with { Y = City.FloorAt(side) + 0.3f });
        Save();
    }

    public bool BurnSkyFuel(double seconds)
    {
        if (_sea.SkyFuel <= 0) return false;
        _sea.SkyFuel = Math.Max(0, _sea.SkyFuel - seconds);
        if (_sea.SkyFuel <= 0) _hud.Toast("The skyboat's fire is out. She's coming down, gently. Stoke her from your pack (I).", 7);
        return true;
    }

    /// <summary>For the self-test.</summary>
    public Skyboat? SkyboatNode => _skyboat;
    public void FittedForTest(string work, int level) => Fitted(work, level, null);
    public void BoardSkyboatForTest() => BoardSkyboat();
    public void LeaveSkyboatForTest() => LeaveSkyboat();
    public void TendSkyboatForTest() { _skyTick = 0; TendSkyboat(0); }
    public void CollectOrdersForTest() => CollectOrders();
}

/// <summary>A lifeboat made over by an artificer: a gas-bag over her, a boiler amidships and a screw at her stern. She flies where she's steered, as long as there's fire in her.</summary>
public partial class Skyboat(Main main) : Node3D
{
    public bool Aboard { get; set; }
    float _speed, _climb;
    Node3D? _screw;

    public const float MostSpeed = 24, MostHeight = 140;

    /// <summary>The helm, at her stern: she goes the way the hero looks over the bow (-Z).</summary>
    public Vector3 SeatAt => GlobalPosition + GlobalTransform.Basis * new Vector3(0, 1.5f, 1.5f);

    /// <summary>What she sits on: the land, a pier, or the top of the water.</summary>
    public static float Floor(Vector3 at) => Mathf.Max(City.FloorAt(at), (float)WorldGen.WaterLevel) + 0.35f;

    public bool Landed => GlobalPosition.Y - Floor(GlobalPosition) < 1.2f;

    static StandardMaterial3D Mat(string hex, float metal = 0) => new() { AlbedoColor = new Color(hex), Roughness = 0.75f, Metallic = metal };

    public override void _Ready()
    {
        var wood = Mat("#7a5634");
        void Box(Vector3 at, Vector3 size, Material m) => AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at });
        Box(new Vector3(0, 0.35f, 0), new Vector3(1.9f, 0.5f, 4.6f), wood); // the hull
        foreach (var side in new[] { -1f, 1f }) Box(new Vector3(side * 0.95f, 0.75f, 0), new Vector3(0.12f, 0.5f, 4.6f), wood);
        Box(new Vector3(0, 0.12f, 0), new Vector3(0.25f, 0.25f, 4.9f), Mat("#4a3420")); // the keel
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2 }, MaterialOverride = Mat("#a8814e"), Position = new Vector3(0, 4.2f, 0), Scale = new Vector3(2.1f, 1.4f, 3.6f) }); // the gas-bag
        foreach (var (x, z) in new[] { (-0.9f, -1.8f), (0.9f, -1.8f), (-0.9f, 1.8f), (0.9f, 1.8f) })
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 2.6f }, MaterialOverride = Mat("#3a3020"), Position = new Vector3(x, 2, z) });
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.4f, Height = 0.9f }, MaterialOverride = Mat("#9a6a3a", 0.7f), Position = new Vector3(0, 0.9f, -0.4f) }); // the boiler
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.11f, Height = 1.4f }, MaterialOverride = Mat("#2e2e32", 0.8f), Position = new Vector3(0, 1.8f, -0.4f) });
        _screw = new Node3D { Position = new Vector3(0, 0.55f, 2.55f) };
        foreach (var a in new[] { 0f, Mathf.Pi / 2 })
            _screw.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.1f, 0.12f, 0.04f) }, MaterialOverride = Mat("#b08a40", 0.8f), Rotation = new Vector3(0, 0, a) });
        AddChild(_screw);
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        if (_screw is not null) _screw.Rotation += new Vector3(0, 0, d * _speed * 0.8f);
        if (!Aboard) return;
        var talking = main.HudNode.Talking;
        var forward = !talking && Input.IsActionPressed("move_forward");
        var back = !talking && Input.IsActionPressed("move_back");
        var turn = talking ? 0 : (Input.IsActionPressed("move_left") ? 1 : 0) - (Input.IsActionPressed("move_right") ? 1 : 0);
        var up = !talking && Input.IsActionPressed("jump");
        var down = !talking && Input.IsActionPressed("descend");
        var working = forward || up || _speed > 1;
        var steam = !working || main.BurnSkyFuel(delta * (up ? 1.4 : 1));
        if (!steam) (forward, up) = (false, false);
        _speed = Mathf.Clamp(_speed + (forward ? 8 : back ? -10 : -1.5f) * d, -3, MostSpeed);
        _climb = Mathf.Lerp(_climb, up ? 6 : down ? -6 : steam ? 0 : -1.5f, d * 2);
        Rotation += new Vector3(0, turn * d * 0.9f, 0);
        var at = GlobalPosition - GlobalTransform.Basis.Z * _speed * d + Vector3.Up * _climb * d;
        var floor = Floor(at);
        at.Y = Mathf.Clamp(at.Y, floor, floor + MostHeight);
        if (at.Y <= floor + 0.01f) _speed *= 1 - d * 2; // aground, she drags
        GlobalPosition = at;
    }
}
