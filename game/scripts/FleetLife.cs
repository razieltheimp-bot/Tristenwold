using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The hero's fleet: ships of their own besides the one they sail, bought at Osric's yard and tied up in a line off the
/// end of the piers, each with hands the harbour master signs on for her. The hands are ordinary sailors, seen about
/// her deck; they don't count against the hero's apprentices or hirelings and they're no use in a fight. A ship with a
/// full crew can be sent on a trading run to a port and back while the hero's about their business, and comes home
/// with crowns in her (fewer, and holed, if pirates found her). Any of them can be made the ship the hero sails.
/// The ship the hero sails can have hands too: they patch and pump while the hero fights off boarders.
/// </summary>
public partial class Main
{
    readonly Dictionary<FleetShip, OwnVessel> _fleetDrawn = [];
    double _fleetTick;

    /// <summary>Where the n-th ship of the fleet ties up: in a line off the ends of the piers, either side of the berth by the middle pier.</summary>
    public static Vector3 FleetBerth(int i) => new(150, 0, i switch { 0 => 21, 1 => -11, 2 => 37, 3 => -27, _ => 53 });

    /// <summary>The fleet drawn when the hero's near the harbour, its hands on deck; and every ship home from her run counted in.</summary>
    void TendFleet(double delta)
    {
        if ((_fleetTick -= delta) <= 0)
        {
            _fleetTick = 5;
            foreach (var f in _sea.Fleet.Where(f => f.Away && _guilds.PlaySeconds >= f.RunBack).ToList()) FleetHome(f);
        }
        var pos = _player.GlobalPosition;
        var near = new Vector2(150 - pos.X, 5 - pos.Z).Length() < 320 && _indoors is null && _cave is null && _atSea is null;
        foreach (var (f, v) in _fleetDrawn.ToList())
            if (!near || !_sea.Fleet.Contains(f) || f.Away || !IsInstanceValid(v) || v.Called != f.Name || v.HandsAboard != Math.Min(f.Hands, OwnVessel.HandSpots.Length))
            {
                if (IsInstanceValid(v)) v.QueueFree();
                _fleetDrawn.Remove(f);
            }
        if (!near) return;
        foreach (var f in _sea.Fleet.Where(f => !f.Away && !_fleetDrawn.ContainsKey(f)))
        {
            var ship = new OwnVessel(this) { Called = f.Name, Moored = true };
            AddChild(ship);
            ship.GlobalPosition = FleetBerth(f.Berth) with { Y = (float)WorldGen.WaterLevel };
            ship.Hands(f.Hands);
            _fleetDrawn[f] = ship;
        }
    }

    void FleetHome(FleetShip f)
    {
        var port = Seas.Ports.FirstOrDefault(p => p.Id == f.RunTo)?.Name ?? "the run";
        var pirates = f.RunPirates;
        var made = SeaBook.HomeFromRun(f);
        _player.Crowns += made;
        _sea.Profit += made;
        Sound.Ui("coin", -4);
        _hud.Toast(pirates
            ? $"{f.Name} is home from {port}, holed: pirates found her and took half of it. Her crew hand over {made} crowns. She's at {f.Hull:0}%."
            : $"{f.Name} is home from {port}, and her crew hand over {made} crowns.", 9);
        Save();
    }

    (string Verb, Action Act)? FleetAction(Vector3 pos)
    {
        foreach (var (f, v) in _fleetDrawn)
            if (IsInstanceValid(v) && pos.DistanceTo(v.GlobalPosition with { Y = pos.Y }) < 9)
                return ($"see to {f.Name}", () => FleetShipMenu(null, f));
        return null;
    }

    /// <summary>The harbour master's word on the fleet, and the hands for the ship the hero sails.</summary>
    IEnumerable<(string, Action)> FleetTalk(Person person)
    {
        if (!_sea.OwnShip) yield break;
        var crew = SeaBook.HandsFor(_sea.Size);
        if (_sea.Hands < crew)
            yield return ($"Sign on hands for {_sea.ShipName}. ({_sea.Hands}/{crew}; {SeaBook.HandWage} crowns a hand: they patch and pump while you fight)", () => HireHands(person, null));
        if (_sea.Fleet.Count > 0)
            yield return ($"My fleet. ({_sea.Fleet.Count} ship{(_sea.Fleet.Count == 1 ? "" : "s")}{(_sea.Fleet.Any(f => f.Away) ? $", {_sea.Fleet.Count(f => f.Away)} away trading" : "")})", () => FleetList(person));
    }

    void FleetList(Person? person)
    {
        var options = _sea.Fleet.Select(f => ($"{f.Name}, a {SeaBook.Hulls[f.Size].Name}: {(f.Away ? $"away to {Seas.Ports.FirstOrDefault(p => p.Id == f.RunTo)?.Name}, home in {FleetDue(f)}" : $"{f.Hands}/{f.Crew} hands, hull {f.Hull:0}%")}", (Action)(() => FleetShipMenu(person, f)))).ToList();
        options.Add(("Done.", _hud.EndTalk));
        _hud.Talk("Your fleet", $"Your own ship, {_sea.ShipName}, and {_sea.Fleet.Count} more of your own. A ship with a full crew can be sent trading while you're about your business.", [.. options]);
    }

    string FleetDue(FleetShip f)
    {
        var left = Math.Max(0, f.RunBack - _guilds.PlaySeconds);
        return left < 90 ? "a minute or so" : $"{Math.Ceiling(left / 60)} minutes";
    }

    void FleetShipMenu(Person? person, FleetShip f)
    {
        var hull = SeaBook.Hulls[f.Size];
        if (f.Away) { _hud.Talk(f.Name, $"Away to {Seas.Ports.FirstOrDefault(p => p.Id == f.RunTo)?.Name} with {f.Hands} hands. Home in {FleetDue(f)}.", ("Back", () => FleetList(person))); return; }
        var options = new List<(string, Action)>();
        if (f.Hands < f.Crew) options.Add(($"Sign on hands. ({f.Hands}/{f.Crew}; {SeaBook.HandWage} crowns a hand)", () => HireHands(person, f)));
        if (f.Crewed && f.Hull >= SeaBook.Seaworthy)
            foreach (var port in Seas.Ports.Where(p => p.Leagues <= 3).OrderBy(p => p.Leagues).Take(6))
                options.Add(($"Send her trading to {port.Name}. ({port.Leagues} league{(port.Leagues == 1 ? "" : "s")}, {(port.Pirates < 0.2 ? "quiet water" : port.Pirates < 0.35 ? "some pirates" : "pirate water")})", () =>
                {
                    SeaBook.SendOnRun(f, port, _guilds.PlaySeconds, _lootRng);
                    Sound.Ui("quest", -6);
                    _hud.EndTalk();
                    _hud.Toast($"{f.Name} casts off for {port.Name} with {f.Hands} hands. She'll be home in {FleetDue(f)}, with whatever she makes.", 8);
                    Save();
                }));
        if (f.Hull < 100)
        {
            var cost = (int)Math.Ceiling((100 - f.Hull) / SeaBook.PlankMends) * 12;
            options.Add(($"Have Osric's men mend her. ({cost} crowns; she's at {f.Hull:0}%)", () =>
            {
                if (_player.Crowns < cost) { _hud.Toast($"{cost} crowns for the mending.", 4); return; }
                _player.Crowns -= cost;
                f.Hull = 100;
                Sound.Ui("coin", -4);
                Save();
                FleetShipMenu(person, f);
            }));
        }
        options.Add(($"Make her the ship I sail. ({_sea.ShipName} ties up here with her crew)", () => MakeFlagship(person, f)));
        options.Add(("Back", () => FleetList(person)));
        _hud.Talk(f.Name, $"A {hull.Name} of your fleet, with a hold for {hull.Hold + (f.Upgrades.Contains("deep_hold") ? 20 : 0)} loads. {f.Hands} of the {f.Crew} hands she wants are aboard; the hull's at {f.Hull:0}%.", [.. options]);
    }

    /// <summary>Hands signed on, as many as she wants and the hero can pay for. Ordinary sailors: not the hero's company.</summary>
    void HireHands(Person? person, FleetShip? f)
    {
        var (have, want) = f is null ? (_sea.Hands, SeaBook.HandsFor(_sea.Size)) : (f.Hands, f.Crew);
        var n = Math.Min(want - have, _player.Crowns / SeaBook.HandWage);
        if (n <= 0) { _hud.Toast($"{SeaBook.HandWage} crowns a hand, to sign them on.", 4); return; }
        _player.Crowns -= n * SeaBook.HandWage;
        if (f is null) _sea.Hands += n; else f.Hands += n;
        Sound.Ui("coin", -2);
        if (f is null) { _vessel?.Hands(_sea.Hands); }
        _hud.Toast($"{n} hand{(n == 1 ? "" : "s")} signed on for {f?.Name ?? _sea.ShipName} ({n * SeaBook.HandWage} crowns). {(have + n >= want ? "She's fully crewed." : $"{want - have - n} more and she's fully crewed.")}", 7);
        Save();
        if (f is not null) FleetShipMenu(person, f); else _hud.EndTalk();
    }

    void MakeFlagship(Person? person, FleetShip f)
    {
        if (AboardShip || _sea.Voyage is not null) { _hud.Toast("Not while you're sailing her.", 4); return; }
        if (_sea.ShipShopOpen) { _hud.Toast($"Bring your shopkeeper off {_sea.ShipName} first.", 5); return; }
        var was = _sea.ShipName;
        _sea.Flagship(f);
        var old = f; // they swap places as well as roles: she lies where the old one did, and the old one takes her berth in the line
        _vessel?.QueueFree();
        _vessel = null;
        if (_fleetDrawn.Remove(old, out var drawn) && IsInstanceValid(drawn)) drawn.QueueFree();
        TendVessel();
        _hud.EndTalk();
        Sound.Ui("quest", -6);
        _hud.Toast($"You'll sail {_sea.ShipName} now. {was} ties up in the fleet with her crew.", 7);
        Save();
    }

    /// <summary>Osric's hulls for the fleet: the ship the hero sails stays theirs, and this one ties up beside her.</summary>
    IEnumerable<(string, Action)> FleetYard(Person master)
    {
        if (!_sea.OwnShip || _sea.Fleet.Count >= SeaBook.MostFleet) yield break;
        foreach (var size in Enum.GetValues<ShipSize>())
        {
            var hull = SeaBook.Hulls[size];
            yield return ($"Build me a {hull.Name} for my fleet. ({hull.Price} crowns; {hull.Hold} loads; wants {SeaBook.HandsFor(size)} hands)", () =>
            {
                var crowns = _player.Crowns;
                if (_sea.BuyForFleet(size, _sea.FreshName(_lootRng), ref crowns) is not { } ship) { Say(master, $"{hull.Price} crowns for a {hull.Name}. I don't build on credit."); return; }
                _player.Crowns = crowns;
                Sound.Ui("coin", -2);
                Say(master, $"She's yours: {ship.Name}, a {hull.Name}, tied up off the piers with the rest of your fleet. She wants {ship.Crew} hands to sail without you: Captain Hale signs them on.");
                Save();
            });
        }
    }

    /// <summary>For the self-test.</summary>
    public void TendFleetForTest() { _fleetTick = 0; TendFleet(0); }
    public OwnVessel? FleetVesselForTest(FleetShip f) => _fleetDrawn.GetValueOrDefault(f);
    public FleetShip? BuyForFleetForTest(ShipSize size) { var c = _player.Crowns + 99999; var s = _sea.BuyForFleet(size, _sea.FreshName(_lootRng), ref c); return s; }
    public double PlaySecondsForTest => _guilds.PlaySeconds;
    public void HireHandsForTest(FleetShip? f) => HireHands(null, f);
    public void MakeFlagshipForTest(FleetShip f) => MakeFlagship(null, f);
}
