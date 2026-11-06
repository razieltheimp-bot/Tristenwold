using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Steam in the hero's ship and wagon, and a skyboat of their own (see <see cref="Engines"/>). Only an artificer fits
/// them: the hero, if they are one; an artificer friend on the server, from an order left at the yard with the makings;
/// or, for a ship, Brasswake's engineers, for crowns. A boiler drives a ship along faster than the wind, a lift takes
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
            var extra = work == Engines.Skyboat ? $" and {Engines.LifeboatPrice} crowns for the lifeboat" : work == Engines.ShipFlight ? $" and {_sea.LiftYardPrice} crowns for the gas and rigging, for a {SeaBook.Hulls[_sea.Size].Name}" : "";
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

    /// <summary>What the yard asks on top of the makings: the lifeboat for a skyboat; the gas and rigging for a ship's lift, by her size.</summary>
    int YardPrice(string work) => work == Engines.Skyboat ? Engines.LifeboatPrice : work == Engines.ShipFlight ? _sea.LiftYardPrice : 0;

    void FitMyself(Person person, string work)
    {
        var price = YardPrice(work);
        if (!Engines.HasMakings(_player.Pack, work) || _player.Crowns < price) { Say(person, $"You'll want {Engines.Makings(work)}{(price > 0 ? $", and {price} crowns {(work == Engines.Skyboat ? "for the boat" : "for the gas and rigging")}" : "")}. Bring them and the yard's yours."); return; }
        Engines.TakeMakings(_player.Pack, work);
        _player.Crowns -= price;
        Fitted(work, _player.Hero.Level, null);
        Say(person, "Well. I'd not have believed it if I hadn't held the lamp for you.");
    }

    void PostOrder(Person person, string work)
    {
        var price = YardPrice(work);
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
        _hud.Talk("An order for an artificer", $"The makings stay here till it's done. What will you pay whoever does it?{(price > 0 ? $" (and {price} crowns to the yard)" : "")}", [.. options]);
    }

    void TakeBackOrder(Person person, string work)
    {
        if (_city.Orders.GetValueOrDefault($"{_player.Company}#{work}") is not { FittedBy: null } o) { CollectOrders(); return; }
        DoCity(CityAction.OrderDone(o.Key));
        foreach (var (item, n) in Engines.Works[work].Needs) _player.Pack.Add(item, n);
        _player.Crowns += o.Pay + YardPrice(work);
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

    /// <summary>Brasswake's engineers will fit a ship that's tied up at the Strangers' Dock, for crowns.</summary>
    IEnumerable<(string, Action)> IsleEngineTalk(Person person)
    {
        if (person.Npc.Id != Engineer || !_sea.OwnShip || !Isle.Berthed) yield break;
        foreach (var work in new[] { Engines.ShipSteam, Engines.ShipFlight }.Where(w => w == Engines.ShipSteam ? !_sea.Steam : _sea.Steam && !_sea.Flies))
        {
            var (name, _, listed) = Engines.Works[work];
            var price = work == Engines.ShipFlight ? _sea.LiftIslePrice : listed; // a bigger hull takes more lifting
            yield return ($"Have your engineers fit {_sea.ShipName} with {name}. ({price} crowns; you have {_player.Crowns})", () =>
            {
                if (_player.Crowns < price) { Say(person, $"{price} crowns. There's work at the valves, at the stern, if you're short."); return; }
                _player.Crowns -= price;
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
        TendVessel();
        TendFleet(1); // the fleet in the harbour, and its runs
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

/// <summary>A lifeboat made over by an artificer: a planked, double-ended boat slung under a netted, striped gas-bag with fins at its tail,
/// a brass boiler amidships, a screw at her stern and two more on outriggers. She flies where she's steered, as long as there's fire in her.</summary>
public partial class Skyboat(Main main) : Node3D
{
    public bool Aboard { get; set; }
    float _speed, _climb;
    Node3D? _screw;
    readonly List<Node3D> _lifters = []; // the screws on her outriggers, that spin as she climbs or comes down

    public const float MostSpeed = 24, MostHeight = 140;

    /// <summary>The helm, at her stern: she goes the way the hero looks over the bow (-Z).</summary>
    public Vector3 SeatAt => GlobalPosition + GlobalTransform.Basis * new Vector3(0, 1.7f, 1.55f);

    /// <summary>What she sits on: the land, a pier, or the top of the water.</summary>
    public static float Floor(Vector3 at) => Mathf.Max(City.FloorAt(at), (float)WorldGen.WaterLevel) + 0.35f;

    public bool Landed => GlobalPosition.Y - Floor(GlobalPosition) < 1.2f;

    static StandardMaterial3D Mat(string hex, float metal = 0) => new() { AlbedoColor = new Color(hex), Roughness = 0.75f, Metallic = metal };

    // ---- her lines: a double-ended lifeboat. u runs from her bow (-1) to her stern (+1); a runs round her from the port gunwale (-1) through the keel (0) to starboard (+1) ----
    const float HalfLength = 2.7f, Beam = 2.1f;
    static float HalfBeam(float u) => Beam / 2 * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow(Mathf.Abs(u), 2.4f)), 0.55f);
    static float GunwaleY(float u) => 1.05f + 0.32f * u * u; // her sheer, rising to bow and stern
    static float KeelY(float u) => 0.02f + 0.55f * Mathf.Pow(Mathf.Abs(u), 3.5f); // the stem and sternpost curving up
    static Vector3 Hull(float u, float a)
    {
        var th = a * Mathf.Pi / 2;
        var (keel, top) = (KeelY(u), GunwaleY(u));
        return new Vector3(HalfBeam(u) * Mathf.Sin(th), keel + (top - keel) * Mathf.Pow(1 - Mathf.Cos(th), 0.75f), u * HalfLength);
    }

    // ---- the gas-bag: a cigar, blunt at the nose and drawn out to the tail, striped in its gores ----
    const float BagY = 4.6f, BagR = 1.45f, BagHalf = 3.9f;
    static float BagRadius(float v) => v >= 1 || v <= -1 ? 0 : BagR * Mathf.Pow(1 - v * v, v < 0 ? 0.5f : 0.7f) * (1 - 0.18f * Mathf.Max(0, v));
    static Vector3 Bag(float v, float phi, float lift = 0)
    {
        var r = BagRadius(v) + lift;
        return new Vector3(r * Mathf.Cos(phi), BagY + r * Mathf.Sin(phi), v * BagHalf);
    }

    /// <summary>A mesh made a triangle at a time, each face turned the way its normals face and coloured as it goes.</summary>
    sealed class Shell
    {
        readonly SurfaceTool _st = new();
        public Shell() => _st.Begin(Godot.Mesh.PrimitiveType.Triangles);

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color col)
        {
            if ((c - a).Cross(b - a).Dot(na + nb + nc) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
            _st.SetColor(col); _st.SetNormal(na); _st.AddVertex(a);
            _st.SetColor(col); _st.SetNormal(nb); _st.AddVertex(b);
            _st.SetColor(col); _st.SetNormal(nc); _st.AddVertex(c);
        }

        /// <summary>A sheet over a grid of points and their normals; colour(i, j) for the square from (i, j).</summary>
        public void Sheet(Vector3[,] p, Vector3[,] n, Func<int, int, Color> colour)
        {
            for (var i = 0; i < p.GetLength(0) - 1; i++)
                for (var j = 0; j < p.GetLength(1) - 1; j++)
                {
                    var c = colour(i, j);
                    Tri(p[i, j], p[i + 1, j], p[i, j + 1], n[i, j], n[i + 1, j], n[i, j + 1], c);
                    Tri(p[i + 1, j], p[i + 1, j + 1], p[i, j + 1], n[i + 1, j], n[i + 1, j + 1], n[i, j + 1], c);
                }
        }

        /// <summary>A rope, rail or pipe along a line of points.</summary>
        public void Tube(IReadOnlyList<Vector3> path, float r, Color col, int sides = 6)
        {
            var rings = new Vector3[path.Count, sides + 1];
            var norms = new Vector3[path.Count, sides + 1];
            var side = Vector3.Zero;
            for (var i = 0; i < path.Count; i++)
            {
                var t = (path[Math.Min(i + 1, path.Count - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
                side = i == 0 ? t.Cross(Mathf.Abs(t.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized() : (side - t * side.Dot(t)).Normalized(); // carried along, so it doesn't twist
                var up = side.Cross(t);
                for (var k = 0; k <= sides; k++)
                {
                    var a = Mathf.Tau * k / sides;
                    norms[i, k] = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    rings[i, k] = path[i] + norms[i, k] * r;
                }
            }
            Sheet(rings, norms, (_, _) => col);
        }

        public void Rod(Vector3 a, Vector3 b, float r, Color col, int sides = 6) => Tube([a, b], r, col, sides);
        public ArrayMesh Mesh() => _st.Commit();
    }

    public override void _Ready()
    {
        var wood = new Shell();
        var metal = new Shell();
        var cloth = new Shell();
        var (brass, iron, rope, darkWood) = (new Color("#c09848"), new Color("#2e2e32"), new Color("#4e4230"), new Color("#4a3420"));
        void Box(Vector3 at, Vector3 size, Material m, Vector3 rot = default) => AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at, Rotation = rot });
        var brassMat = Mat("#b08a40", 0.8f);
        var ironMat = Mat("#2e2e32", 0.8f);

        // ---- the hull: planked strakes, a painted sheer strake, tarred about the keel; planked inside too ----
        const int along = 30, round = 18, mid = round / 2;
        var outer = new Vector3[along + 1, round + 1];
        var outN = new Vector3[along + 1, round + 1];
        for (var i = 0; i <= along; i++)
            for (var j = 0; j <= round; j++) outer[i, j] = Hull(-1 + 2f * i / along, -1 + 2f * j / round);
        for (var i = 0; i <= along; i++)
            for (var j = 0; j <= round; j++)
            {
                var du = outer[Math.Min(i + 1, along), j] - outer[Math.Max(i - 1, 0), j];
                var da = outer[i, Math.Min(j + 1, round)] - outer[i, Math.Max(j - 1, 0)];
                var n = da.Cross(du);
                var p = outer[i, j];
                if (n.Dot(new Vector3(p.X, p.Y - 1.2f, p.Z * 0.3f)) < 0) n = -n;
                outN[i, j] = n.LengthSquared() > 1e-10f ? n.Normalized() : Vector3.Down;
            }
        for (var j = 0; j <= round; j++) (outN[0, j], outN[along, j]) = (outN[1, j], outN[along - 1, j]);
        var inner = new Vector3[along + 1, round + 1];
        var inN = new Vector3[along + 1, round + 1];
        for (var i = 0; i <= along; i++)
            for (var j = 0; j <= round; j++) (inner[i, j], inN[i, j]) = (outer[i, j] - outN[i, j] * 0.06f, -outN[i, j]);
        Color Strake(int j)
        {
            var band = (int)Mathf.Abs(j + 0.5f - mid); // 0 at the keel, mid - 1 at the gunwale
            if (band >= mid - 1) return new Color("#2f5f66"); // the sheer strake, painted
            if (band <= 2) return new Color("#3a2a1c"); // tarred about the keel
            return band % 2 == 0 ? new Color("#8a6038") : new Color("#77502e");
        }
        wood.Sheet(outer, outN, (_, j) => Strake(j));
        wood.Sheet(inner, inN, (_, j) => j % 2 == 0 ? new Color("#6e4c2c") : new Color("#664628"));
        Vector3[] Line(Func<int, Vector3> at) => [.. Enumerable.Range(0, along + 1).Select(at)];
        foreach (var j in new[] { 0, round })
        {
            wood.Tube(Line(i => outer[i, j] + Vector3.Up * 0.03f), 0.055f, new Color("#5a3c22")); // the gunwale rail
            var wale = j == 0 ? 2 : round - 2;
            metal.Tube(Line(i => outer[i, wale] + outN[i, wale] * 0.03f), 0.035f, brass); // a brass rubbing strake
        }
        wood.Tube(Line(i => outer[i, mid] + Vector3.Down * 0.02f), 0.07f, darkWood); // the keel
        foreach (var i in new[] { 0, along }) // the stem and the sternpost, standing a little proud
        {
            var post = Enumerable.Range(0, mid + 1).Select(j => outer[i, j]).Reverse().ToList();
            post.Add(outer[i, 0] + Vector3.Up * 0.28f + Vector3.Back * (i == 0 ? -0.08f : 0.08f));
            wood.Tube(post, 0.065f, darkWood);
        }
        for (var i = 4; i < along; i += 4) // her ribs, inside
            wood.Tube([.. Enumerable.Range(0, round + 1).Select(j => inner[i, j] + inN[i, j] * 0.03f)], 0.035f, new Color("#5a3c22"));
        var boards = Mat("#8a6a44");
        foreach (var x in new[] { -0.5f, -0.25f, 0f, 0.25f, 0.5f }) Box(new Vector3(x, 0.57f, 0), new Vector3(0.2f, 0.06f, 3.4f), boards); // the bottom boards
        Box(new Vector3(0, 0.85f, -1.5f), new Vector3(1.6f, 0.07f, 0.32f), Mat("#7a5634")); // a thwart forward
        Box(new Vector3(0, 0.85f, 2.1f), new Vector3(1.1f, 0.07f, 0.32f), Mat("#7a5634")); // and the stern bench
        foreach (var side in new[] { -1f, 1f }) // sandbags for ballast, hung over her sides
            foreach (var u in new[] { -0.45f, 0.05f, 0.5f })
            {
                var top = Hull(u, side) + Vector3.Up * 0.03f;
                var bag = Hull(u, side * 0.84f) + new Vector3(side * 0.16f, 0, 0);
                wood.Rod(top, bag + Vector3.Up * 0.1f, 0.012f, rope, 4);
                AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 10, Rings = 6 }, MaterialOverride = Mat("#9a8458"), Position = bag, Scale = new Vector3(0.8f, 1.15f, 0.8f) });
            }
        var lamp = Hull(-1, -1) + new Vector3(0, 0.4f, -0.05f); // a lamp on her stem
        Box(lamp, new Vector3(0.16f, 0.2f, 0.16f), new StandardMaterial3D { AlbedoColor = new Color("#ffd890"), EmissionEnabled = true, Emission = new Color("#ffb040"), EmissionEnergyMultiplier = 2 });
        Box(lamp + Vector3.Up * 0.13f, new Vector3(0.2f, 0.05f, 0.2f), brassMat);

        // ---- the gas-bag, its netting, the ropes down to her gunwales, and fins at its tail ----
        const int bagAlong = 30, bagRound = 24;
        var skin = new Vector3[bagAlong + 1, bagRound + 1];
        var skinN = new Vector3[bagAlong + 1, bagRound + 1];
        for (var i = 0; i <= bagAlong; i++)
            for (var k = 0; k <= bagRound; k++) skin[i, k] = Bag(-1 + 2f * i / bagAlong, Mathf.Tau * k / bagRound);
        for (var i = 0; i <= bagAlong; i++)
            for (var k = 0; k <= bagRound; k++)
            {
                var dv = skin[Math.Min(i + 1, bagAlong), k] - skin[Math.Max(i - 1, 0), k];
                var dp = skin[i, (k + 1) % bagRound] - skin[i, (k + bagRound - 1) % bagRound];
                var n = dp.Cross(dv);
                var p = skin[i, k];
                if (n.Dot(new Vector3(p.X, p.Y - BagY, p.Z * 0.4f)) < 0) n = -n;
                skinN[i, k] = n.LengthSquared() > 1e-10f ? n.Normalized() : new Vector3(0, 0, Mathf.Sign(p.Z));
            }
        for (var k = 0; k <= bagRound; k++) (skinN[0, k], skinN[bagAlong, k]) = (new Vector3(0, 0, -1), new Vector3(0, 0, 1));
        cloth.Sheet(skin, skinN, (_, k) => k / 2 % 2 == 0 ? new Color("#dccca6") : new Color("#8e3a2c"));
        const float starboardBelly = -Mathf.Pi / 2 + 0.8f, portBelly = -Mathf.Pi / 2 - 0.8f + Mathf.Tau;
        var bands = new[] { -0.7f, -0.45f, -0.2f, 0.05f, 0.3f, 0.55f };
        foreach (var v in bands) // the netting's hoops, over the top from belly to belly
            wood.Tube([.. Enumerable.Range(0, 17).Select(s => Bag(v, starboardBelly + (portBelly - starboardBelly) * s / 16, 0.025f))], 0.022f, rope, 4);
        foreach (var phi in new[] { Mathf.Pi / 2, Mathf.Pi / 2 - 0.6f, Mathf.Pi / 2 + 0.6f, Mathf.Pi / 2 - 1.25f, Mathf.Pi / 2 + 1.25f }) // and its long lines
            wood.Tube([.. Enumerable.Range(0, 15).Select(s => Bag(-0.85f + 1.7f * s / 14, phi, 0.025f))], 0.02f, rope, 4);
        foreach (var phi in new[] { starboardBelly, portBelly }) // the load bands the boat hangs from
            metal.Tube([.. Enumerable.Range(0, 15).Select(s => Bag(-0.82f + 1.64f * s / 14, phi, 0.03f))], 0.035f, brass);
        wood.Tube([.. Enumerable.Range(0, 15).Select(s => Bag(-0.8f + 1.6f * s / 14, -Mathf.Pi / 2, 0.07f))], 0.06f, darkWood); // a spar along its belly
        foreach (var side in new[] { -1f, 1f })
        {
            var phi = side > 0 ? starboardBelly : portBelly;
            var hangs = bands.Select(v => Bag(v, phi, 0.03f)).ToArray();
            var holds = bands.Select((_, k) => Hull(-0.72f + 1.44f * k / (bands.Length - 1), side) + Vector3.Up * 0.05f).ToArray();
            for (var k = 0; k < bands.Length; k++)
            {
                wood.Rod(hangs[k], holds[k], 0.016f, rope, 4);
                if (k + 1 < bands.Length) { wood.Rod(hangs[k], holds[k + 1], 0.012f, rope, 4); wood.Rod(hangs[k + 1], holds[k], 0.012f, rope, 4); }
            }
        }
        foreach (var phi in new[] { Mathf.Pi / 2, -Mathf.Pi / 2, 0, Mathf.Pi }) // the fins
        {
            var (out_, across) = (new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0), new Vector3(-Mathf.Sin(phi), Mathf.Cos(phi), 0));
            Vector3 F(float radial, float z) => new Vector3(0, BagY, 0) + out_ * radial + Vector3.Back * z;
            var pts = new[] { F(BagRadius(2.4f / BagHalf) - 0.06f, 2.4f), F(BagRadius(3.75f / BagHalf) - 0.04f, 3.75f), F(1.6f, 4.05f), F(1.4f, 3.25f) };
            foreach (var s in new[] { -1f, 1f })
            {
                var (n, d) = (across * s, across * s * 0.03f);
                cloth.Tri(pts[0] + d, pts[1] + d, pts[2] + d, n, n, n, new Color("#8e3a2c"));
                cloth.Tri(pts[0] + d, pts[2] + d, pts[3] + d, n, n, n, new Color("#8e3a2c"));
            }
            metal.Tube([pts[0], pts[3], pts[2], pts[1]], 0.035f, brass);
        }
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f }, MaterialOverride = brassMat, Position = new Vector3(0, BagY, -BagHalf + 0.05f) }); // its nose cap
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f }, MaterialOverride = brassMat, Position = new Vector3(0, BagY, BagHalf) });

        // ---- the boiler amidships, its funnel, and the shaft aft to her screw; a wheel before the helm ----
        var boiler = new Vector3(0, 1.05f, -0.3f);
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.38f, Height = 0.9f }, MaterialOverride = brassMat, Position = boiler });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.38f, Height = 0.76f }, MaterialOverride = brassMat, Position = boiler + Vector3.Up * 0.45f, Scale = new Vector3(1, 0.55f, 1) });
        foreach (var y in new[] { -0.32f, 0f, 0.32f })
            AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.37f, OuterRadius = 0.42f }, MaterialOverride = ironMat, Position = boiler + Vector3.Up * y });
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 1.2f }, MaterialOverride = ironMat, Position = boiler + Vector3.Up * 1.1f });
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.1f, Height = 0.16f }, MaterialOverride = ironMat, Position = boiler + Vector3.Up * 1.76f });
        Box(boiler + new Vector3(0, -0.2f, 0.385f), new Vector3(0.24f, 0.18f, 0.02f), new StandardMaterial3D { AlbedoColor = new Color("#ff9040"), EmissionEnabled = true, Emission = new Color("#ff6010"), EmissionEnergyMultiplier = 2.5f }); // the firebox's glow
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.03f }, MaterialOverride = Mat("#efe6cc"), Position = boiler + new Vector3(0.2f, 0.2f, 0.34f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) }); // a gauge
        metal.Tube([boiler + new Vector3(0, -0.36f, 0.3f), new Vector3(0, 0.68f, 1.1f), new Vector3(0, 0.95f, 2.45f), new Vector3(0, 0.95f, 2.98f)], 0.045f, iron); // the shaft
        metal.Tube([boiler + new Vector3(0.38f, 0.25f, 0), boiler + new Vector3(0.55f, 0.3f, 0.2f), boiler + new Vector3(0.6f, 0.15f, 0.9f), new Vector3(0.62f, 1.1f, 0.6f)], 0.03f, brass); // a steam pipe out to the starboard screw
        metal.Tube([boiler + new Vector3(-0.38f, 0.25f, 0), boiler + new Vector3(-0.55f, 0.3f, 0.2f), boiler + new Vector3(-0.6f, 0.15f, 0.9f), new Vector3(-0.62f, 1.1f, 0.6f)], 0.03f, brass);
        var wheel = new Vector3(0, 1.35f, 0.85f);
        wood.Rod(new Vector3(0, 0.6f, 0.85f), wheel, 0.06f, darkWood);
        AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.26f, OuterRadius = 0.31f }, MaterialOverride = Mat("#6a4628"), Position = wheel, Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        for (var k = 0; k < 6; k++)
        {
            var dir = new Vector3(Mathf.Cos(k * Mathf.Pi / 3), Mathf.Sin(k * Mathf.Pi / 3), 0);
            wood.Rod(wheel + dir * 0.05f, wheel + dir * 0.4f, 0.018f, new Color("#6a4628"), 4);
        }
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f }, MaterialOverride = brassMat, Position = wheel });

        // ---- her screws: a big one at the stern, two on outriggers for lifting and turning ----
        Node3D Screw(Vector3 at, float reach, int blades)
        {
            var s = new Node3D { Position = at };
            s.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f }, MaterialOverride = brassMat });
            for (var k = 0; k < blades; k++)
            {
                var arm = new Node3D { Rotation = new Vector3(0, 0, Mathf.Tau * k / blades) };
                arm.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(reach * 0.34f, reach, 0.03f) }, MaterialOverride = brassMat, Position = new Vector3(0, reach / 2 + 0.05f, 0), Rotation = new Vector3(0, 0.55f, 0) });
                s.AddChild(arm);
            }
            AddChild(s);
            return s;
        }
        _screw = Screw(new Vector3(0, 0.95f, 3.0f), 0.5f, 4);
        foreach (var side in new[] { -1f, 1f })
        {
            var pod = new Vector3(side * 1.75f, 1.25f, 0.6f);
            metal.Tube([Hull(0.22f, side) + Vector3.Up * 0.05f, new Vector3(side * 1.4f, 1.2f, 0.6f), pod], 0.045f, brass); // the outrigger
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.13f, Height = 0.55f }, MaterialOverride = ironMat, Position = pod, Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
            _lifters.Add(Screw(pod + Vector3.Back * 0.32f, 0.36f, 3));
        }

        var woodMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f };
        var metalMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.4f, Metallic = 0.75f };
        var clothMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.95f };
        AddChild(new MeshInstance3D { Mesh = wood.Mesh(), MaterialOverride = woodMat });
        AddChild(new MeshInstance3D { Mesh = metal.Mesh(), MaterialOverride = metalMat });
        AddChild(new MeshInstance3D { Mesh = cloth.Mesh(), MaterialOverride = clothMat });
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        if (_screw is not null) _screw.Rotation += new Vector3(0, 0, d * _speed * 0.8f);
        foreach (var s in _lifters) s.Rotation += new Vector3(0, 0, d * (Mathf.Abs(_climb) * 2.5f + Mathf.Abs(_speed) * 0.4f));
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
