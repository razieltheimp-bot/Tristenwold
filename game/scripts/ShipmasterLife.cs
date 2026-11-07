using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// One of the hero's apprentices or squires in command of a ship of their fleet (see <see cref="SeaBook.TendFleet"/>).
/// The hero sends them orders by word from anywhere, through the party: sail to a port, sail on past the charts to find
/// new land, or work a trade route round a string of ports. Their master trades only at the port she's tied up in, out
/// of her own purse, and sends home whatever she makes over her stake each time she's in Tristinwold.
/// </summary>
public partial class Main
{
    /// <summary>The ship one of the company has command of, if any.</summary>
    FleetShip? ShipOf(Bond b) => _sea.Fleet.FirstOrDefault(f => f.Master == b.Id);

    void GiveCommand(Person? person, FleetShip f, Bond b)
    {
        if (!f.Crewed) { _hud.Toast($"{f.Name} wants {f.Crew - f.Hands} more hands before she can sail: Captain Hale signs them on.", 6); }
        if (b.Keeps is not null) StopKeeping(b);
        if (!SeaBook.TakeCommand(f, b)) { _hud.Toast($"{b.Name} can't take command of her just now.", 4); return; }
        if (BodyOf(b) is { } body) { _companions.Remove(body); body.QueueFree(); }
        Sound.Ui("quest", -6);
        _hud.Toast($"{b.Name} goes aboard {f.Name} and takes command. Give her a purse to trade with, then send {b.Name} orders: from here, or by word from anywhere through your company (P).", 10);
        Save();
        ShipOrders(person, f);
    }

    /// <summary>The orders the hero can send a ship's master, and how she stands.</summary>
    void ShipOrders(Person? person, FleetShip f)
    {
        TendHarbours(); // the hero's own places on the water are on her charts
        var now = _guilds.PlaySeconds;
        void Back() { if (person is not null) FleetList(person); else ShowParty(); }
        void Send(IReadOnlyList<string> stops, bool loop, string said)
        {
            if (!f.Crewed) { _hud.Toast($"{f.Name} wants {f.Crew - f.Hands} more hands to sail: Captain Hale at the harbour signs them on.", 6); return; }
            if (!_sea.Order(f, stops, loop, now)) { _hud.Toast("She can't sail on those orders.", 4); return; }
            Sound.Ui("quest", -6);
            _hud.Toast(f.Sailing ? $"Word goes out to {f.MasterName}: {said}, once {f.Name} makes port." : $"Word goes to {f.MasterName}: {said}.", 7);
            TendMasters();
            Save();
            ShipOrders(person, f);
        }
        var options = new List<(string, Action)>();
        var here = f.Sailing ? f.Bound : f.Where;
        foreach (var port in _sea.Known.Where(p => p.Id != here).OrderBy(p => Seas.Between(f.Where, p.Id)))
            options.Add(($"Sail to {port.Name}. ({Seas.Between(f.Where, port.Id)} day{(Seas.Between(f.Where, port.Id) == 1 ? "" : "s")}; {(port.Pirates < 0.2 ? "quiet water" : port.Pirates < 0.5 ? "some pirates" : "pirate water")})",
                () => Send([port.Id], false, $"sail to {port.Name} and trade there")));
        if (here != Seas.Tristinwold) options.Add(("Come home to Tristinwold.", () => Send([Seas.Tristinwold], false, "come home")));
        if (_sea.Unfound is { } edge)
            options.Add(($"Sail past the charts and find new land. ({Seas.Between(f.Where, edge.Id)} days or so; dangerous water)", () => Send([Seas.Beyond], false, "sail past the edge of the charts")));
        options.Add((f.OnRoute ? "Set her a different trade route." : "Set up a trade route.", () => RouteMenu(person, f, [])));
        if (f.OnRoute)
            options.Add(("Leave the route at the next port and wait there.", () =>
            {
                (f.Plan, f.Loop, f.Stop) = (f.Plan.Count > 0 ? [f.Plan[f.Stop]] : [], false, 0);
                f.Note("Orders: leave the route at the next port.");
                Save();
                ShipOrders(person, f);
            }));
        if (!f.Away)
        {
            foreach (var stake in new[] { 250, 1000 })
                options.Add(($"Put {stake} crowns in her purse to trade with. (you've {_player.Crowns})", () =>
                {
                    if (_player.Crowns < stake) { _hud.Toast("You haven't the crowns.", 3); return; }
                    _player.Crowns -= stake;
                    SeaBook.StakeHer(f, stake);
                    Sound.Ui("coin", -2);
                    Save();
                    ShipOrders(person, f);
                }));
            options.Add(($"Bring {f.MasterName} ashore. (her purse comes with them, and her hold goes to the Exchange)", () =>
            {
                var b = _bonds.FirstOrDefault(x => x.Id == f.Master);
                var name = f.MasterName;
                var crowns = SeaBook.StandDown(f, b, Dock(Seas.Tristinwold)!);
                _player.Crowns += crowns;
                if (b is not null && (b.Following = MayFollow(b))) BringCompanion(b);
                Sound.Ui("coin", -2);
                _hud.Toast($"{name} comes ashore from {f.Name}{(crowns > 0 ? $" and hands you {crowns} crowns" : "")}.", 7);
                Save();
                Back();
            }));
        }
        options.Add(("Read her log.", () => _hud.Talk($"{f.Name}'s log", f.Log.Count == 0 ? "Nothing in it yet." : string.Join("\n", f.Log), ("Back", () => ShipOrders(person, f)))));
        options.Add(("Back", Back));
        var cargo = f.Cargo.Count == 0 ? "nothing" : string.Join(", ", f.Cargo.Select(c => $"{c.Value} {CityEconomy.Info(c.Key).Name}"));
        var orders = f.Plan.Count == 0 ? "waiting for orders" : f.OnRoute ? $"working a route: {string.Join(", ", f.Plan.Select(Seas.NameOf))}, and round again" : $"bound {string.Join(", then ", f.Plan.Select(Seas.NameOf))}";
        _hud.Talk($"{f.Name}, {f.MasterName} in command",
            $"She's {f.Whereabouts(now)}, {orders}. In her hold: {cargo} ({f.Loaded}/{f.HoldSize}). Her purse: {f.Purse} crowns ({f.Stake} of it yours to start with); {(f.Made >= 0 ? $"{f.Made} made" : $"{-f.Made} lost")} on her trading all told. Hull {f.Hull:0}%, {f.Hands}/{f.Crew} hands. {f.MasterName} only trades at the port she's tied up in, and sends home what she makes whenever she's in Tristinwold.",
            [.. options]);
    }

    /// <summary>A trade route, stop by stop: two to six ports, round and round.</summary>
    void RouteMenu(Person? person, FleetShip f, List<string> stops)
    {
        var options = new List<(string, Action)>();
        if (stops.Count < SeaBook.MostStops)
            foreach (var id in _sea.Known.Select(p => p.Id).Prepend(Seas.Tristinwold).Where(id => stops.Count == 0 || stops[^1] != id))
                options.Add(($"Then {Seas.NameOf(id)}{(stops.Count > 0 ? $" ({Seas.Between(stops[^1], id)} days)" : "")}", () => RouteMenu(person, f, [.. stops, id])));
        if (stops.Distinct().Count() >= 2)
            options.Insert(0, ("That's the route: send it.", () =>
            {
                if (!f.Crewed) { _hud.Toast($"{f.Name} wants {f.Crew - f.Hands} more hands to sail: Captain Hale at the harbour signs them on.", 6); return; }
                if (!_sea.Order(f, stops, true, _guilds.PlaySeconds)) { _hud.Toast("She can't sail that route.", 4); return; }
                Sound.Ui("quest", -6);
                _hud.Toast($"Word goes to {f.MasterName}: a trade route, {string.Join(", ", stops.Select(Seas.NameOf))}, and round again. She buys where it's cheap and sells where it's dear, only ever at the port she's in.", 9);
                TendMasters();
                Save();
                ShipOrders(person, f);
            }));
        if (stops.Count > 0) options.Add(("Start again.", () => RouteMenu(person, f, [])));
        options.Add(("Never mind.", () => ShipOrders(person, f)));
        _hud.Talk($"A route for {f.Name}", stops.Count == 0 ? "Where does the route start? Pick the ports in order; she'll go round them again and again." : $"So far: {string.Join(" → ", stops.Select(Seas.NameOf))} → back to {Seas.NameOf(stops[0])}. Add another, or send it.", [.. options]);
    }

    /// <summary>
    /// A sea serpent or a leviathan doesn't care that you're on a ship: what it bites at, it bites through, and the hull
    /// takes it with you. Her hands and her fittings save what they can, as they do against boarders.
    /// </summary>
    void OnSeaBite(Enemy e, Enemy? at)
    {
        if (at is not null || !AboardShip || !Wildlife.Aquatic(e.Kind.Id)) return;
        var was = _sea.Hull;
        _sea.Damage(e.Kind.Damage * 0.18);
        if (was >= 50 && _sea.Hull < 50) _hud.Toast($"{e.Kind.Name} is tearing at {_sea.ShipName}'s hull: she's at {_sea.Hull:0}%. Get clear of it, or kill it!", 7);
        else if (was >= SeaBook.Seaworthy && _sea.Hull < SeaBook.Seaworthy) _hud.Toast($"{_sea.ShipName} is badly holed ({_sea.Hull:0}%). Get her to Osric's yard before she goes down.", 8);
    }

    readonly Dictionary<string, bool> _coastal = [];

    /// <summary>Deep water within a short walk of a place, where a ship can lie off it.</summary>
    bool Coastal(string key, double x, double z)
    {
        if (_coastal.TryGetValue(key, out var known)) return known;
        var found = false;
        for (var r = 40.0; r <= 320 && !found; r += 20)
            for (var a = 0.0; a < Math.Tau && !found; a += Math.Tau / 16)
                found = _world.Height(x + Math.Cos(a) * r, z + Math.Sin(a) * r) < WorldGen.WaterLevel - 3;
        return _coastal[key] = found;
    }

    /// <summary>
    /// The hero's own places on the water, as harbours for their ships: each colony by the sea, and their hometown if
    /// it's on the coast (a port town on the charts counts as it is). A ship's master hands over her takings at any of
    /// them, as in Tristinwold.
    /// </summary>
    void TendHarbours()
    {
        var pays = new HashSet<string>();
        var mine = new HashSet<string>();
        foreach (var h in _city.ColoniesOf(_player.Company).ToList())
        {
            if (!Coastal(h.Town, h.X, h.Z)) continue;
            var id = $"harbour:{h.Town}";
            Seas.Harbour(id, h.Name, h.X, h.Z, [Good.Fish, Good.Timber, Good.Stone], [Good.Bread, Good.Iron, Good.Ale]);
            pays.Add(id);
            mine.Add(id);
        }
        if (_story.Home is { } home && !Settling.IsColony(home.Id))
        {
            if (Seas.Ports.Concat(Seas.Uncharted).FirstOrDefault(p => p.Name == home.Name) is { } port) pays.Add(port.Id);
            else if (Coastal(home.Id, home.X, home.Z) && _world.Find(home.Id) is { } town && TownMarkets.Trades(town))
            {
                var market = TownMarkets.For(town);
                var id = $"harbour:{home.Id}";
                Seas.Harbour(id, home.Name, home.X, home.Z, [.. market.Makes], [.. market.Wants]);
                pays.Add(id);
                mine.Add(id);
            }
        }
        foreach (var gone in Seas.Harbours.Keys.Where(k => !mine.Contains(k)).ToList()) Seas.Harbours.Remove(gone);
        _sea.PaysAt = pays;
    }

    int _countryLevel = 1;

    /// <summary>Walking out into wilder country than the hero's ready for: a word of warning, each time it gets worse.</summary>
    void TendCountry()
    {
        if (_indoors is not null || _cave is not null || _atSea is not null || _isle is not null) return;
        var at = _player.GlobalPosition;
        var level = (int)Math.Floor(WorldGen.LevelAt(at.X, at.Z));
        var hero = _player.Hero.Level;
        if (level <= _countryLevel) { _countryLevel = Math.Max(level, hero); return; }
        _countryLevel = level;
        if (level >= hero + 3) _hud.Toast($"Wild country: what lives out here is a match for a level {level} hero, and you're level {hero}. Tristinwold's that way.", 7);
        else if (level > hero) _hud.Toast($"The wilds get harder here: level {level} country.", 5);
    }

    /// <summary>Someone after the hero won't be put off by the water: word of it, when they're close enough to see.</summary>
    void OnTookBoat(Enemy e, bool hired)
    {
        if (e.GlobalPosition.DistanceTo(_player.GlobalPosition) > 120) return;
        _hud.Toast(hired ? $"{e.Stats.Name} throws a fisherman a purse for his boat, and rows out after you!" : $"{e.Stats.Name} cuts a boat loose from the shore and rows out after you!", 6);
    }

    /// <summary>For the self-test.</summary>
    public void GiveCommandForTest(FleetShip f, Bond b) => GiveCommand(null, f, b);
    public void TendMastersForTest() => TendMasters();
    public void RemoveBondForTest(Bond b) => _bonds.Remove(b);
    public void StandDownForTest(FleetShip f) => _player.Crowns += SeaBook.StandDown(f, _bonds.FirstOrDefault(x => x.Id == f.Master), Dock(Seas.Tristinwold)!);
}
