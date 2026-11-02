using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Trade beyond the walls (see <see cref="TradeBook"/>). Every place you come to know keeps its own little market,
/// and Tristinwold's own caravans start trading with it. The caravan yard outside the west gate, kept by Garrick Wain,
/// takes on anyone: load the yard's wagon first; then drive it out to a town with Tristinwold's goods and back with
/// the town's; and once you've made three runs, buy a wagon of your own, hitch your own horses to it, and trade on
/// your own account, buying cheap where a thing is made and selling dear where it's wanted. The roads aren't safe for
/// a loaded wagon.
/// </summary>
public partial class Main
{
    TradeBook _trade = new();
    Wagon? _wagon;
    double _caravanTick, _ambushAt = 120, _glutCheck = 5;
    (int Left, bool Holding)? _loading;
    /// <summary>The camp of caravan-robbers the hero's gone to clear, once they're near it: who's there.</summary>
    List<Enemy>? _camp;
    string? _campOf;
    Node3D? _sack;

    public TradeBook TradeLog => _trade;
    public Wagon? WagonNode => _wagon;

    /// <summary>Right up against the city wall, south of the west gate, where the Watch can see it.</summary>
    public static readonly Vector3 YardAt = new(-108, 0, 34);
    static Vector3 YardWagon => YardAt + new Vector3(5, 0, -2);
    static Vector3 YardStack => YardAt + new Vector3(-5, 0, 4);
    const string Master = "tristinwold/caravans";
    public const string Cargo = "res://assets/props/caravan/";

    /// <summary>Tristinwold's old routes are open from the start, and on the map (heard of, until you've been).</summary>
    void BeginTrade()
    {
        foreach (var s in _trade.OpenOldRoutes(_world, _city.Day))
            if (!_explored.Discovered.Contains(s.Id)) _explored.Rumored.Add(s.Id);
        // Every trading place the company has found is on the roads (a save from before the roads were the city's knows them again).
        foreach (var id in _explored.Discovered.ToList())
            if (!_trade.Towns.ContainsKey(id) && _world.Find(id) is { } s && TownMarkets.Trades(s)) _trade.Market(s, _city.Day);
    }

    void AddCaravanYard()
    {
        Townsfolk(new Npc(Master, "Garrick Wain", Sex.Male, 52, "master of the caravan yard", "city:tristinwold", "Tristinwold",
            "Garrick has driven every road out of Tristinwold and lost a wagon on most of them. He keeps the caravan yard outside the west gate, and he'll take on anyone with a strong back, then anyone who can drive, and sell a wagon to anyone who's proved they can bring one home.",
            ["the roads", "money"], "gruff", "Loading, driving or buying? I've work for all three.", YardAt.X - 1.5, YardAt.Z + 1.5));
        // The loading stack, and goods piled all about the yard.
        string[] goods = ["crate_apples.glb", "bag.glb", "barrel_apples.glb"];
        for (var i = 0; i < 6; i++)
            Models.Place(this, Cargo + goods[i % 3], YardStack + new Vector3(i % 3 * 0.9f, 0, i / 3 * 0.9f), 1.1f, i * 0.4f, solid: false);
        foreach (var (x, z, n) in new[] { (9f, -9f, 4), (-13f, 6f, 3), (2f, 13f, 5), (14f, 4f, 3) })
            for (var i = 0; i < n; i++)
                Models.Place(this, Cargo + goods[(i + n) % 3], YardAt + new Vector3(x + i % 2 * 0.9f, 0, z + i / 2 * 0.9f), 1.1f, i * 0.7f, solid: false);
        // Traders' stalls along the wall, and their keepers.
        Models.Place(this, Cargo + "stall_red.glb", YardAt + new Vector3(-4, 0, -9), 2.4f, 0.3f);
        Models.Place(this, Cargo + "stall_green.glb", YardAt + new Vector3(4, 0, -11), 2.4f, -0.2f);
        Models.Place(this, Cargo + "stall_red.glb", YardAt + new Vector3(12, 0, -7), 2.4f, -0.6f);
        Townsfolk(new Npc("tristinwold/yard_spice", "Imra Dask", Sex.Female, 38, "spice trader at the caravan yard", "city:tristinwold", "Tristinwold",
            "Imra buys whatever the wagons bring from the far places and sells it to whoever'll pay. She knows every road's prices by heart.",
            ["money", "the roads"], "sly", "Fresh off the wagons. Don't ask from where.", YardAt.X - 4, YardAt.Z - 7), "traveller");
        Townsfolk(new Npc("tristinwold/yard_harness", "Bodo Hask", Sex.Male, 61, "harness-maker at the caravan yard", "city:tristinwold", "Tristinwold",
            "Bodo has mended every harness on every wagon that's left this yard for forty years. He talks to the horses more than to people.",
            ["horses", "old battles"], "warm", "Mind the mare, she bites.", YardAt.X + 4, YardAt.Z - 9), "worker");
        // Wagons standing in the yard: the one you load, and others waiting for their teams or their drivers.
        foreach (var (at, yaw, horse) in new[] { (YardWagon, 0.4f, (string?)"horse"), (YardAt + new Vector3(-11, 0, -3), 1.2f, null), (YardAt + new Vector3(11, 0, 10), -0.8f, "horse"), (YardAt + new Vector3(-4, 0, 12), 2.2f, null) })
        {
            var parked = new Wagon(this, horse) { Parked = true };
            AddChild(parked);
            parked.GlobalPosition = at with { Y = City.FloorAt(at) };
            parked.Rotation = new Vector3(0, yaw, 0);
        }
        Sign(YardAt + new Vector3(0, 3.4f, -3), "Caravan Yard");
        City.Zones.Add(new Zone("The Caravan Yard", YardAt, 20, 9));
    }

    IEnumerable<(string, Action)> CaravanTalk(Person person)
    {
        var npc = person.Npc;
        if (npc.Id == Master)
        {
            foreach (var o in YardTalk(person)) yield return o;
            yield break;
        }
        // Anyone out in the world can say what their place trades in; with your own wagon there, you can trade.
        if (_world.Find(npc.HomeId) is not { } home || !TownMarkets.Trades(home) || person.GlobalPosition.DistanceTo(_player.GlobalPosition) > 8) yield break;
        if (_trade.Scouting == home.Id)
            yield return ("The caravan yard sent me. Will you trade with Tristinwold?", () =>
            {
                var pay = _trade.Found(home, _city.Day) ?? 0;
                _player.Crowns += pay;
                Sound.Ui("coin");
                Say(person, $"Tristinwold! We've heard of it, and not much else. Send your wagons, and we'll deal with them fair.");
                _hud.Toast($"New trade route: {home.Name}. The yard pays {pay} crowns for the scouting.", 8);
                Save();
            });
        var m = _trade.Market(home, _city.Day);
        yield return ($"What does {home.Name} trade in?", () => Say(person,
            $"We make {m.MakesText}, and sell it cheap. What we're short of is {m.WantsText}: bring us those and we pay well."));
        if (_trade is { OwnWagon: true, WagonOut: true } && _wagon is not null && _wagon.GlobalPosition.DistanceTo(person.GlobalPosition) < 40)
            yield return ("Trade from my wagon.", () => TradeHere(person, m));
    }

    IEnumerable<(string, Action)> YardTalk(Person person)
    {
        var t = _trade;
        if (t.Banned(_city.Day))
        {
            yield return ("About that wagon...", () => Say(person, $"You've a nerve, showing your face in my yard. You took my wagon, my horse and my load. Come back in {t.BannedUntil - _city.Day} days, if the Watch hasn't had you by then, and we'll see."));
            yield break;
        }
        if (_loading is null && t.Run is null)
            yield return (t.Rank <= CaravanRank.Loader ? $"Any loading work? ({TradeBook.LoadWage} crowns a load)" : "I'll load a wagon.", () => StartLoading(person));
        if (t.Rank >= CaravanRank.Driver && t.Run is null && !t.WagonOut && t.Guarding is null)
        {
            yield return ("Give me a wagon to drive.", () => StartRun(person));
            var gluts = t.Gluts(_city).ToList();
            if (gluts.Count > 0) yield return ($"Any gluts to move? ({gluts.Count})", () => OfferGluts(person));
        }
        if (t.Run is null && !t.WagonOut && t.Guarding is null)
            yield return ("Any caravans that need a guard?", () => OfferGuardWork(person));
        if (t.Scouting is { } scouting)
            yield return ($"Where am I scouting again?", () => Say(person, _world.Find(scouting) is { } place ? $"{place.Name}, {Direction(person.GlobalPosition, (place.X, place.Z))}. Talk to anyone there and say the yard sent you." : "Forget that one. Nobody's sure it's there."));
        else if (t.Rank >= CaravanRank.Driver && t.NextFrontier(_world) is { } frontier)
            yield return ("Is there any frontier to scout?", () =>
            {
                t.Scout(frontier);
                _explored.Rumored.Add(frontier.Id);
                Say(person, $"Word is there's a place called {frontier.Name}, {Direction(person.GlobalPosition, (frontier.X, frontier.Z))}, and no road of ours goes there. Find it, talk to someone, and open the road. {TradeBook.ScoutPay}-odd crowns in it for you.");
                Save();
            });
        if (_city.Roads.Troubles.Count > 0)
            yield return ($"Trouble on the roads? ({_city.Roads.Troubles.Count})", () => OfferTroubles(person));
        if (t.Hunting is { } hunting && _city.Roads.Troubles.FirstOrDefault(x => x.Id == hunting) is { } going)
            yield return ("About that camp...", () => Say(person, $"{going.By} took the caravan from {going.TownName}; they're camped off that road, {Direction(person.GlobalPosition, (going.X, going.Z))} of here, {going.Bandits} of them. {going.Bounty} crowns when they're cleared."));
        if (t.Run is { } r)
            yield return ("About my run...", () => Say(person, r.Returning
                ? $"You've {r.ToName}'s {CityEconomy.Info(r.Back).Name} aboard. Bring it home, here, to the yard."
                : $"{r.Load} loads of {CityEconomy.Info(r.Out).Name} for {r.ToName}, {Direction(person.GlobalPosition, (r.X, r.Z))} of here. Mind the road."));
        if (t.MayOwn && !t.OwnWagon)
            yield return ($"Sell me a wagon of my own. ({TradeBook.WagonPrice} crowns)", () => BuyWagon(person));
        if (t.OwnWagon && _bonds.Count(b => b.Hitched) < TradeBook.MaxHorses)
            yield return ($"Sell me a draught horse. ({TradeBook.HorsePrice} crowns)", () => BuyHorse(person));
        if (t.OwnWagon)
        {
            Hitch();
            if (!t.WagonOut) yield return ("Hitch up my wagon.", () => HitchUp(person));
            else
            {
                yield return ($"Load goods for the road. ({t.Loaded}/{t.Capacity} aboard)", () => LoadGoods(person));
                if (t.Loaded > 0) yield return ("Sell what's on my wagon to the Exchange.", () => SellHome(person));
                yield return ("Put my wagon away.", () => PutAway(person));
            }
        }
        if (t.Rank >= CaravanRank.Driver)
            yield return ("Who does Tristinwold trade with?", () => Say(person, t.Towns.Count == 0
                ? "Nobody you've found yet. Find a place out there, and the yard's wagons will be on the road to it by the week's end."
                : "The yard runs to " + string.Join("; ", t.Towns.Values.Where(m => m.DistanceTo(0, 0) < TradeBook.Reach).OrderBy(m => m.DistanceTo(0, 0)).Take(6)
                    .Select(m => $"{m.Name} ({m.MakesText}; short of {m.WantsText})")) + "."));
    }

    // ---- loading ----------------------------------------------------------------------------------------------

    void StartLoading(Person master)
    {
        _loading = (5, false);
        Say(master, $"Five loads from the stack onto the wagon. {TradeBook.LoadWage} crowns a load. Lift with your legs; I'm not paying for your back.");
    }

    void LiftSack()
    {
        _loading = (_loading!.Value.Left, true);
        _player.Burdened = true;
        if (Models.Scene("ph:crate.glb")?.Instantiate() is Node3D crate)
        {
            crate.Scale = Vector3.One * 1.1f;
            crate.Position = new Vector3(0, 1.0f, -0.6f);
            _player.AddChild(crate);
            _sack = crate;
        }
        Sound.At("move/land", _player.GlobalPosition, -6, 0.8f);
    }

    void LoadWagon()
    {
        var left = _loading!.Value.Left - 1;
        _player.Burdened = false;
        _sack?.QueueFree();
        _sack = null;
        _player.Crowns += TradeBook.LoadWage;
        Sound.Ui("coin", -2);
        var promoted = _trade.Loaded1();
        _loading = left > 0 ? (left, false) : null;
        if (promoted) _hud.Toast("Garrick: \"You can lift. Can you drive? Come and ask me for a wagon.\" (You're a driver for the caravan yard now.)", 10);
        else _hud.Toast(left > 0 ? $"+{TradeBook.LoadWage} crowns. {left} to go." : $"+{TradeBook.LoadWage} crowns. That's the wagon loaded.", 3);
        Save();
    }

    // ---- driving for the yard ---------------------------------------------------------------------------------

    void StartRun(Person master)
    {
        if (_trade.Plan(_world, _city.Day, _lootRng) is not { } run) { Say(master, "The yard only runs to places somebody's found, and nobody's found anywhere yet. Go out and find a village; the wagons will follow."); return; }
        _trade.Run = run;
        _explored.Rumored.Add(run.To);
        TakeWagon(null);
        Say(master, $"{run.Load} loads of {CityEconomy.Info(run.Out).Name} for {run.ToName}, {Direction(master.GlobalPosition, (run.X, run.Z))} of here. " +
                    $"They'll load you up with {CityEconomy.Info(run.Back).Name} for the road home. {run.Pay} crowns, half there and half when you're back. Lead the team; don't lose the wagon.");
        Save();
    }

    /// <summary>A driver's wagon has come into its town, or home again.</summary>
    void TendRun()
    {
        if (_trade.Run is not { } r || _wagon is null) return;
        var at = _wagon.GlobalPosition;
        if (r.Glut) { TendGlutRun(r, at); return; }
        if (!r.Returning && _world.Find(r.To) is { } town && town.DistanceTo(at.X, at.Z) < town.Radius + 25)
        {
            var pay = _trade.Arrive(_city.Day);
            _player.Crowns += pay;
            Sound.Ui("coin", -2);
            _hud.Toast($"You unload Tristinwold's {CityEconomy.Info(r.Out).Name} at {r.ToName}, and they load you up with {CityEconomy.Info(r.Back).Name} for the road home. +{pay} crowns. Back to the yard.", 9);
            Save();
        }
        else if (r.Returning && at.DistanceTo(YardAt with { Y = at.Y }) < 14)
        {
            var rest = r.Pay - r.Pay / 2;
            var ready = _trade.Home();
            _player.Crowns += rest;
            _city.Stock[r.Back] = _city.Stock.GetValueOrDefault(r.Back) + r.Load;
            Share(CityAction.Cargo(r.Back, r.Load));
            _city.Renown += 2;
            Sound.Ui("coin", -2);
            StableWagon();
            _hud.Toast(ready
                ? $"+{rest} crowns. Garrick looks the wagon over: \"Three runs and not a wheel lost. You could run your own, you know. I'll sell you one.\""
                : $"+{rest} crowns, and {r.ToName}'s {CityEconomy.Info(r.Back).Name} goes into the city's stores.", 9);
            Save();
        }
    }

    // ---- gluts ---------------------------------------------------------------------------------------------------

    readonly HashSet<string> _gluts = [];

    /// <summary>A place drowning in something, and the work of carting it to where it's wanted.</summary>
    void OfferGluts(Person master)
    {
        var options = _trade.Gluts(_city).Take(6).Select(j => ($"{j.N} {CityEconomy.Info(j.Good).Name} from {j.FromName} to {j.ToName}: {j.Pay} crowns",
            (Action)(() =>
            {
                var run = _trade.Take(j);
                TakeWagon(null);
                if (run.From == TradeBook.CityId) _trade.LoadGlut(_city, _city.Day); // it's here in the city's stores: loaded at the yard
                Say(master, run.Loaded
                    ? $"The Exchange is drowning in {CityEconomy.Info(j.Good).Name}. It's loaded. Take it to {j.ToName}, {Direction(master.GlobalPosition, (j.ToX, j.ToZ))} of here, where they'll pay for it. {j.Pay} crowns when it's there."
                    : $"{j.FromName} is drowning in {CityEconomy.Info(j.Good).Name}; they'll practically give it away. Take the wagon there, {Direction(master.GlobalPosition, (j.FromX, j.FromZ))} of here, load up, and take it to {j.ToName}, where they'll pay for it. {j.Pay} crowns when it's there.");
                Save();
            }))).ToList();
        options.Add(("Not now.", () => Say(master, "Gluts don't last. Somebody else will have it.")));
        _hud.Talk(Who(master.Npc), "Somebody's sold a town more of something than it'll ever use, and now it's worth nothing there and good money elsewhere. That's work for a wagon.", [.. options]);
    }

    void TendGlutRun(CaravanRun r, Vector3 at)
    {
        if (!r.Loaded)
        {
            var near = r.From == TradeBook.CityId ? at.DistanceTo(YardAt with { Y = at.Y }) < 20 : _world.Find(r.From) is { } from && from.DistanceTo(at.X, at.Z) < from.Radius + 25;
            if (!near) return;
            _trade.LoadGlut(_city, _city.Day);
            _hud.Toast($"{r.FromName} loads you up with {r.Load} {CityEconomy.Info(r.Out).Name} for next to nothing. Now to {r.ToName}.", 7);
            Save();
            return;
        }
        var there = r.To == TradeBook.CityId ? at.DistanceTo(YardAt with { Y = at.Y }) < 20 : _world.Find(r.To) is { } to && to.DistanceTo(at.X, at.Z) < to.Radius + 25;
        if (!there) return;
        var pay = _trade.DeliverGlut(_city, _city.Day);
        _player.Crowns += pay;
        _city.Renown += 2;
        Sound.Ui("coin", -2);
        StableWagon();
        _hud.Toast($"{r.ToName} takes the {CityEconomy.Info(r.Out).Name} gladly. +{pay} crowns. (The wagon goes back to the yard with a carter.)", 8);
        Save();
    }

    /// <summary>Word comes to the yard when a glut forms anywhere along the routes.</summary>
    void WatchGluts()
    {
        var now = _trade.Gluts(_city).ToList();
        foreach (var j in now.Where(j => _gluts.Add(j.Key)))
            _hud.Toast($"Word at the caravan yard: {j.FromName} is drowning in {CityEconomy.Info(j.Good).Name}, and the price there has fallen through the floor. Garrick has work carting it to {j.ToName}.", 8);
        _gluts.IntersectWith(now.Select(j => j.Key));
    }

    // ---- guarding the city's caravans --------------------------------------------------------------------------

    Wagon? _guarded;
    double _lastHurt;
    public Wagon? GuardedWagon => _guarded;

    /// <summary>The yard's caravans along its routes, each wanting a guard: pick one, and see it through.</summary>
    void OfferGuardWork(Person master)
    {
        if (!_trade.Seasoned && _trade.ShortRoad(_city.Day) is { } first)
        {
            void Drive() { first.Driver = true; BeginCaravan(master, first); }
            _hud.Talk(Who(master.Npc),
                $"New, are you? Everyone starts somewhere. The city's wagon for {first.ToName} goes out today, and it's a short road: a couple of bandits at most, if they bother. Walk beside it, keep them off the load, and the {first.Pay} crowns are yours. Do that, and I'll trust you with the long roads once you've made a name.",
                ("I'll guard it.", () => BeginCaravan(master, first)),
                ($"I'll drive it. (+{TradeBook.DriverShare:P0} pay)", Drive),
                ("Not today.", () => Say(master, "It'll go without you, then.")));
            return;
        }
        // The short road goes out every day, for anyone; the long roads are for those who've made a name.
        var options = new List<(string, Action)>();
        var shortRoad = _trade.ShortRoad(_city.Day);
        if (shortRoad is not null)
            options.Add(($"The short road to {shortRoad.ToName}: {shortRoad.Pay} crowns ({shortRoad.Danger})", () => StartGuarding(master, shortRoad)));
        var seasoned = _player.Hero.Level >= TradeBook.GuardLevel;
        var escorts = seasoned ? _trade.Escorts(_player.Hero.Level, _city.Day).ToList() : [];
        options.AddRange(escorts.Select(e => ($"To {e.ToName}, {Direction(YardAt, (e.X, e.Z))}: {e.Pay} crowns ({e.Danger})", (Action)(() => StartGuarding(master, e)))));
        options.Add(("Not today.", () => Say(master, "The roads will still be there.")));
        // Known danger pays: a road that's lost wagons, or has a camp on it, or a clan about, pays over the odds, and he says so.
        var known = (shortRoad is { Known: not null } ? [shortRoad] : Enumerable.Empty<Escort>()).Concat(escorts.Where(e => e.Known is not null)).DistinctBy(e => e.To).ToList();
        var warning = known.Count == 0 ? "" : " " + string.Join(" ", known.Select(e => $"The road to {e.ToName} is known trouble ({e.Known}): I'm paying {e.Mult:0.0} times the rate on it, and I'll want the wagon back."));
        _hud.Talk(Who(master.Npc), (seasoned
            ? "The city's caravans go out along every road to every place we know, and the bandits know it. I pay guards by the road: the further, the more, and more again for someone who's made a name."
            : $"The short road's yours any day you want it. The long roads I don't give to anyone without some steel in them: come back at level {TradeBook.GuardLevel}.") + warning, [.. options]);
    }

    /// <summary>The job's picked: as a guard riding along, or holding the reins, for a bit more.</summary>
    void StartGuarding(Person master, Escort e) => _hud.Talk(Who(master.Npc), $"To {e.ToName}, then. Guard or driver? Either way there's a seat on the wagon, and either way you get down and fight if they come.",
        ($"Guard, {e.Pay} crowns", () => BeginCaravan(master, e)),
        ($"Driver, {(int)Math.Round(e.Pay * (1 + TradeBook.DriverShare))} crowns", () => { e.Driver = true; BeginCaravan(master, e); }),
        ("Not today.", () => Say(master, "The roads will still be there.")));

    void BeginCaravan(Person master, Escort e)
    {
        _trade.Guarding = e;
        _guarded?.QueueFree();
        _guarded = new Wagon(this, "horse") { Goal = new Vector3((float)e.X, 0, (float)e.Z), Hold = GuardHold, Path = _world.Highways.WayTo(e.To, e.X, e.Z) };
        AddChild(_guarded);
        _guarded.GlobalPosition = YardWagon with { Y = City.FloorAt(YardWagon) };
        Say(master, e.Driver ? $"She's yours to drive to {e.ToName}. Climb up on the box when you're ready: she'll hold the road by herself, and you set the pace. Bring the wagon through and the {(int)Math.Round(e.Pay * (1 + TradeBook.DriverShare))} crowns are yours." : $"The carter leaves for {e.ToName} when you do. Walk beside her, or climb up on the load for the ride. Stay close: she won't drive on without her guard, and she pulls up when there's trouble. Bring the wagon through and the {e.Pay} crowns are yours.");
        Save();
    }

    /// <summary>The carter pulls up while there's trouble about, or while her guard has fallen behind.</summary>
    bool GuardHold()
    {
        var at = _guarded!.GlobalPosition;
        return _player.Downed || _player.GlobalPosition.DistanceTo(at) > 45 || FoesNear(at, 30).Any();
    }

    IEnumerable<Enemy> FoesNear(Vector3 at, float metres) =>
        GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly && !e.Allied && e.Leader is null
            && e.Kind.Id is "bandit" or "bandit_chief" && e.GlobalPosition.DistanceTo(at) < metres);

    Wagon? _ridden;
    float _riddenYaw;
    public Wagon? RiddenWagon => _ridden;

    /// <summary>Up on the wagon, seeing the road through your own eyes: the yard's caravan (as its guard, or with its reins), or a wagon of your own run.</summary>
    void ClimbAboard(Wagon wagon)
    {
        if (wagon == _guarded && _trade.Guarding is null) return;
        _ridden = wagon;
        _riddenYaw = wagon.Rotation.Y;
        _player.Rotation = new Vector3(0, wagon.Rotation.Y, 0);
        var driver = wagon != _guarded || _trade.Guarding!.Driver;
        _player.SitAt(() => wagon.SeatAt(driver));
        wagon.HoldReins(driver);
        _hud.Toast(driver
            ? $"You've the reins. W to push the horse on, S to pull up, A and D to steer her wherever you like (let go, and she finds her own road again), the mouse to look about. E to jump down{(MayStealWagon ? ", or to keep the wagon" : "")}."
            : "You settle on the load. The mouse looks about. E to jump down.", 9);
    }

    void ClimbDown()
    {
        if (_ridden is not { } wagon) return;
        _ridden = null;
        var at = IsInstanceValid(wagon) ? wagon.GlobalPosition + wagon.GlobalTransform.Basis.X * 2.6f : _player.GlobalPosition;
        if (IsInstanceValid(wagon)) { wagon.Urge = 1; wagon.HoldReins(false); }
        _player.StandUp(at with { Y = City.FloorAt(at) + 0.3f });
    }

    /// <summary>
    /// Whether the one on the box could drive off with the yard's wagon: it's the yard's (a caravan, or a run for Garrick),
    /// they hold the reins, and they've no good name in Tristinwold to lose by it.
    /// </summary>
    bool MayStealWagon => _ridden is { Reins: true } w && (w == _guarded || (w == _wagon && _trade.Run is not null)) && _standing.Repute <= Tristinwold.Gameplay.Repute.Unknown && !_standing.Watch.In;

    /// <summary>E on the box: down to the road; or, for someone with no good name to lose, the yard's wagon kept.</summary>
    void LeaveWagon()
    {
        if (!MayStealWagon) { ClimbDown(); return; }
        _hud.Talk("The reins in your hands", "The yard's wagon, the yard's horse, the yard's load, and nobody out here but you.",
            ("Jump down.", () => { _hud.EndTalk(); ClimbDown(); }),
            ("Keep the wagon, and the load.", () => { _hud.EndTalk(); StealWagon(); }),
            ("Drive on.", _hud.EndTalk));
    }

    /// <summary>
    /// The yard's wagon taken for your own, horse and load and all: a villain's start in trade. The job is over, the load is
    /// yours to sell in any town that'll take it, and Tristinwold hears of it: a price on your head, and the yard's gate
    /// shut to you for a while.
    /// </summary>
    void StealWagon()
    {
        if (_ridden is not { } wagon) return;
        var load = new Dictionary<Good, int>();
        if (wagon == _guarded && _trade.Guarding is not null)
        {
            foreach (var g in _city.Stock.OrderByDescending(s => s.Value).Take(2).Select(s => s.Key)) load[g] = 5; // Tristinwold's goods for the town: what the city has most of
            _guarded = null;
            StableWagon();
            _wagon = wagon;
            wagon.Goal = null;
            wagon.Hold = null;
        }
        else if (_trade.Run is { } r) load[r.Returning ? r.Back : r.Out] = r.Load;
        var took = _trade.Stole(_city.Day, load);
        // The horse in the shafts is yours too: it stays hitched to the wagon (the party menu unhitches it when you want to ride).
        var horse = Adopt("horse", dragon: false, wagon.GlobalPosition + new Vector3(2, 0, 0), level: 6);
        HitchToWagon(horse);
        Hitch();
        var bounty = 40 + 2 * took;
        _standing.Evil(4, "stole a caravan from Tristinwold's yard");
        _standing.Wanted(bounty);
        _city.Renown = Math.Max(0, _city.Renown - 2);
        wagon.Urge = Wagon.RidePace;
        Sound.Ui("coin", -2);
        _hud.Toast($"The wagon's yours, and {took} loads with it; the horse in the shafts is {horse.Name} now, and stays hitched (unhitch {horse.Name} from the party menu, P, to ride). Garrick will hear of it by nightfall: {bounty} crowns on your head in Tristinwold, and no work at the yard for {TradeBook.BanDays} days. Sell the load in any town that'll take it.", 12);
        _hud.Log("Roads: you stole a caravan from the yard.");
        Save();
    }

    /// <summary>Riding: the view turns with the wagon, and whoever holds the reins sets the pace and steers.</summary>
    void TendRide()
    {
        if (_ridden is not { } wagon) return;
        if (!IsInstanceValid(wagon) || (wagon != _guarded && wagon != _wagon) || _player.Downed) { ClimbDown(); return; }
        _player.RotateY(Mathf.AngleDifference(_riddenYaw, wagon.Rotation.Y));
        _riddenYaw = wagon.Rotation.Y;
        var driving = wagon.Reins && !_hud.Talking;
        wagon.Steer = driving ? Input.GetAxis("move_left", "move_right") : 0;
        wagon.Urge = !driving ? (wagon.Goal is null ? 0 : Wagon.RidePace) : Input.IsActionPressed("move_back") ? 0 : Input.IsActionPressed("move_forward") ? Wagon.PushPace : Wagon.RidePace;
    }

    /// <summary>For the self-test.</summary>
    public void ClimbForTest() { if ((_guarded ?? _wagon) is { } w) ClimbAboard(w); }
    public void ClimbDownForTest() => ClimbDown();
    public void StealForTest() => StealWagon();
    public void UnhitchForTest() { if (_bonds.FirstOrDefault(b => b.Hitched) is { } horse) Unhitch(horse); }
    public void HitchForTest() { if (_bonds.FirstOrDefault(b => b.Steed && b.Following) is { } horse) HitchToWagon(horse); }
    public bool MayStealForTest => MayStealWagon;

    /// <summary>The guarded wagon on the road: ambushes as it gets far enough along, the bandits at the wagon, and the end of the road.</summary>
    void TendGuarding(double delta)
    {
        if (_trade.Guarding is not { } e) return;
        if (_guarded is null || !IsInstanceValid(_guarded)) // a loaded game: the caravan sets off again from the yard
        {
            BeginCaravan(Locals.First(p => p.Npc.Id == Master), e);
            return;
        }
        var at = _guarded.GlobalPosition;
        var total = (float)Math.Sqrt((e.X - YardWagon.X) * (e.X - YardWagon.X) + (e.Z - YardWagon.Z) * (e.Z - YardWagon.Z));
        var left = new Vector2(at.X - (float)e.X, at.Z - (float)e.Z).Length();
        var along = 1 - left / Math.Max(1, total);
        if (e.AmbushDue(along))
        {
            e.Sprung++;
            var ahead = -_guarded.GlobalTransform.Basis.Z;
            for (var i = 0; i < e.Bandits; i++)
            {
                var spot = at + ahead * 22 + new Vector3((i - e.Bandits / 2f) * 3, 0, (i % 2) * 4);
                var kind = i == 0 && e.Bandits >= 5 ? "bandit_chief" : "bandit";
                var b = Enemy.Create(Bestiary.Get(kind), spot with { Y = City.FloorAt(spot) + 0.5f }, WorldGen.Danger(spot.X, spot.Z) * (1 + 0.05 * _player.Hero.Level));
                AddEnemy(b);
                b.Provoke(_player);
            }
            _hud.Toast($"Ambush! {e.Bandits} bandits come out of cover at the wagon. {(_ridden is not null ? "Press E to jump down and fight!" : "Keep them off it.")}", 6);
        }
        // Bandits at the wagon break into the load.
        var at_ = FoesNear(at, 9).Count();
        if (at_ > 0)
        {
            e.Wagon = Math.Max(0, e.Wagon - at_ * 2.5 * delta);
            if (_clock - _lastHurt > 4) { _lastHurt = _clock; _hud.Toast($"The bandits are at the wagon! ({e.Wagon:0}% of the load left)", 3); }
        }
        if (e.Wagon <= 0)
        {
            _trade.Overrun();
            _guarded.QueueFree();
            _guarded = null;
            _hud.Toast($"The bandits cut the horse loose and take the load. The caravan to {e.ToName} is lost. Garrick won't be pleased.", 9);
            Save();
            return;
        }
        if (_world.Find(e.To) is { } town && town.DistanceTo(at.X, at.Z) < town.Radius + 12)
        {
            var bonus = (e.Wagon >= 90 ? e.Pay / 4 : 0) + (e.Driver ? (int)Math.Round(e.Pay * TradeBook.DriverShare) : 0);
            var news = _trade.Delivered(_city, _city.Day, _lootRng);
            _player.Crowns += e.Pay + bonus;
            _city.Renown += 3;
            _standing.Good(2, $"brought a caravan safe to {e.ToName}");
            TradedAt(e.To, e.ToName);
            Sound.Ui("coin", -2);
            _hud.Toast($"The caravan reaches {e.ToName} safe{(bonus > 0 ? ", hardly a scratch on it" : "")}. +{e.Pay + bonus} crowns.{(news is null ? "" : " " + news)}", 9);
            if (news is not null) _hud.Log($"Tristinwold: {news}");
            _guarded.Parked = true;
            var done = _guarded;
            GetTree().CreateTimer(30).Timeout += () => { if (IsInstanceValid(done)) done.QueueFree(); };
            _guarded = null;
            Save();
        }
    }

    // ---- a wagon of your own ----------------------------------------------------------------------------------

    void BuyWagon(Person master)
    {
        if (_player.Crowns < TradeBook.WagonPrice) { Say(master, $"{TradeBook.WagonPrice} crowns. Come back when you have them."); return; }
        _player.Crowns -= TradeBook.WagonPrice;
        _trade.BuyWagon();
        Say(master, "She's yours: oak bed, iron tyres, room for a dozen loads a horse. You'll want horses. A grown horse of your own will pull, or I'll sell you a draught horse.");
        Save();
    }

    void BuyHorse(Person master)
    {
        if (_player.Crowns < TradeBook.HorsePrice) { Say(master, $"{TradeBook.HorsePrice} crowns for a good draught horse. Come back when you have them."); return; }
        _player.Crowns -= TradeBook.HorsePrice;
        var horse = Adopt("horse", dragon: false, YardAt + new Vector3(2, 0, 3), level: 6);
        HitchToWagon(horse);
        Say(master, $"{horse.Name}'s yours: steady, strong, and doesn't kick much. {horse.Name}'s in the shafts of your wagon now. A horse can pull or carry you, not both: unhitch {horse.Name} from the party menu (P) to ride.");
        Save();
    }

    /// <summary>The horses pulling your wagon: only those in its shafts, two at most. One you're riding, or that's at your heels, isn't pulling anything.</summary>
    void Hitch()
    {
        _trade.Horses = Math.Min(TradeBook.MaxHorses, _bonds.Count(b => b.Hitched));
        if (_wagon is { } own && _trade.Run is null) own.Team(_trade.Horses > 0); // the yard's own horse pulls a run
    }

    /// <summary>A steed put to the wagon: it leaves your heels and goes with the wagon.</summary>
    void HitchToWagon(Bond horse)
    {
        horse.Hitched = true;
        horse.Following = false;
        if (BodyOf(horse) is { } body) { _companions.Remove(body); body.QueueFree(); }
        Hitch();
    }

    /// <summary>A steed taken off the wagon to ride: at your heels again, and the wagon still yours, standing wherever it was if that was the last horse in the shafts.</summary>
    void Unhitch(Bond horse)
    {
        horse.Hitched = false;
        horse.Following = true;
        BringCompanion(horse);
        Hitch();
    }

    void HitchUp(Person master)
    {
        // Any spare grown horse goes in the shafts; one that's with you is yours to hitch or keep, from the party menu.
        foreach (var spare in _bonds.Where(b => b.Steed && b.Rideable && !b.Following && !b.Hitched).Take(TradeBook.MaxHorses - _bonds.Count(b => b.Hitched)).ToList()) HitchToWagon(spare);
        Hitch();
        if (_trade.Horses == 0)
        {
            var with = _bonds.FirstOrDefault(b => b.Steed && b.Rideable && b.Following);
            Say(master, with is null ? "A wagon doesn't pull itself. You need a grown horse: buy one from me, or raise a foal."
                : $"A wagon doesn't pull itself, and {with.Name} can't carry you and pull her too. Put {with.Name} in the shafts from your party menu (P), or buy another horse from me.");
            return;
        }
        _trade.WagonOut = true;
        TakeWagon(_bonds.Where(b => b.Hitched).Select(b => b.KindId).FirstOrDefault());
        Say(master, $"Hitched: {_trade.Horses} horse{(_trade.Horses == 1 ? "" : "s")}, room for {_trade.Capacity} loads. Buy cheap where they make it, sell dear where they want it.");
        Save();
    }

    void PutAway(Person master)
    {
        if (_trade.Loaded > 0) { Say(master, "Not with goods still aboard. Sell them, or take them somewhere."); return; }
        _trade.WagonOut = false;
        StableWagon();
        Say(master, "I'll keep her dry.");
        Save();
    }

    /// <summary>What a load of a good costs at the Exchange, and what the Exchange pays for one.</summary>
    int ExchangeAsks(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * _city.Factor(g)));
    int ExchangePays(Good g) => Math.Max(1, (int)Math.Round(CityEconomy.Info(g).Worth * _city.Factor(g) * 0.85));

    void LoadGoods(Person master)
    {
        var options = Enum.GetValues<Good>().Where(g => _city.Stock.GetValueOrDefault(g) > 6)
            .Select(g => ($"{CityEconomy.Info(g).Name}: {ExchangeAsks(g)} a load ({_city.Stock[g]:0} in the stores)", (Action)(() =>
            {
                var n = Math.Min(Math.Min(5, _trade.Room), (int)_city.Stock[g] - 5);
                var cost = 0;
                for (var i = 0; i < n && _player.Crowns >= ExchangeAsks(g); i++)
                {
                    var price = ExchangeAsks(g);
                    _player.Crowns -= price;
                    cost += price;
                    _city.Stock[g] -= 1;
                    _trade.Cargo[g] = _trade.Cargo.GetValueOrDefault(g) + 1;
                    _trade.Profit -= price;
                }
                _hud.Toast(cost > 0 ? $"{cost} crowns for {CityEconomy.Info(g).Name} ({_trade.Loaded}/{_trade.Capacity} aboard)." : _trade.Room == 0 ? "The wagon's full." : "You can't afford it.", 4);
                Save();
                LoadGoods(master);
            }))).Take(8).ToList();
        options.Add(("That's enough.", () => Say(master, "Mind the road.")));
        _hud.Talk(Who(master.Npc), $"What'll you take? {_trade.Room} loads of room, {_player.Crowns} crowns in your purse.", [.. options]);
    }

    void SellHome(Person master)
    {
        var crowns = 0;
        foreach (var (g, n) in _trade.Cargo.ToList())
        {
            for (var i = 0; i < n; i++)
            {
                crowns += (int)Math.Round(ExchangePays(g) * PerkTrade);
                _city.Stock[g] = _city.Stock.GetValueOrDefault(g) + 1;
            }
            Share(CityAction.Cargo(g, Math.Min(10, n)));
        }
        _trade.Cargo.Clear();
        _player.Crowns += crowns;
        _trade.Profit += crowns;
        _city.Renown += 1;
        Sound.Ui("coin", -2);
        Say(master, $"The Exchange takes the lot: {crowns} crowns. You've made {_trade.Profit} crowns on the road, all told.");
        Save();
    }

    /// <summary>Trading from your own wagon, in a town out in the world.</summary>
    void TradeHere(Person person, TownMarket m)
    {
        var options = new List<(string, Action)>();
        foreach (var (g, n) in _trade.Cargo.Where(c => c.Value > 0).ToList())
            options.Add(($"Sell {n} {CityEconomy.Info(g).Name} (they pay {m.Pays(g)} a load{(m.Wants.Contains(g) ? ", and they want it" : "")})", () =>
            {
                var crowns = (int)Math.Round(_trade.SellAt(m, g, n) * PerkTrade * RepSellAt(m.Id));
                _trade.Cargo.Remove(g);
                _player.Crowns += crowns;
                _trade.Profit += crowns;
                TradedAt(m.Id, m.Name);
                Sound.Ui("coin", -2);
                _hud.Toast($"+{crowns} crowns for {CityEconomy.Info(g).Name} at {m.Name}.", 4);
                Save();
                TradeHere(person, m);
            }));
        foreach (var g in m.Makes.Concat(Enum.GetValues<Good>().Where(m.Glut)).Distinct().Where(g => m.ForSale(g) > 0 && _trade.Room > 0))
        {
            var n = Math.Min(Math.Min(_trade.Room, m.ForSale(g)), 6);
            options.Add(($"Buy {n} {CityEconomy.Info(g).Name} ({(int)Math.Round(m.Cost(g, n) * RepBuyAt(m.Id))} crowns; Tristinwold pays about {ExchangePays(g)} a load)", () =>
            {
                var cost = (int)Math.Round(m.Cost(g, n) * RepBuyAt(m.Id));
                if (_player.Crowns < cost) { _hud.Toast("You can't afford it.", 3); return; }
                var (_, got) = _trade.BuyAt(m, g, n);
                _player.Crowns -= cost;
                _trade.Profit -= cost;
                _trade.Cargo[g] = _trade.Cargo.GetValueOrDefault(g) + got;
                _hud.Toast($"{cost} crowns for {n} {CityEconomy.Info(g).Name} ({_trade.Loaded}/{_trade.Capacity} aboard).", 4);
                Save();
                TradeHere(person, m);
            }));
        }
        options.Add(("Done.", () => Say(person, "Safe roads.")));
        m.Visits++;
        _hud.Talk(Who(person.Npc), $"{m.Name} makes {m.MakesText} and is short of {m.WantsText}. You've {_trade.Loaded}/{_trade.Capacity} aboard and {_player.Crowns} crowns.", [.. options]);
    }

    // ---- the wagon on the road --------------------------------------------------------------------------------

    void TakeWagon(string? horse)
    {
        StableWagon();
        _wagon = new Wagon(this, horse);
        AddChild(_wagon);
        // At the yard when you take it there; otherwise (a game loaded, a thief's wagon) it's wherever you are: its home is where yours is.
        var hero = _player.GlobalPosition;
        var setOff = hero.DistanceTo(YardAt with { Y = hero.Y }) < 60 ? YardAt + new Vector3(6, 0, 6) : hero + _player.GlobalTransform.Basis.Z * 5;
        _wagon.GlobalPosition = setOff with { Y = City.FloorAt(setOff) };
        Hitch();
    }

    void StableWagon()
    {
        _wagon?.QueueFree();
        _wagon = null;
    }

    /// <summary>Loading, the run under way, the wagon on the road and the bandits who'd like it, and Tristinwold's own caravans.</summary>
    void TendCaravans(double delta)
    {
        if ((_caravanTick -= delta) > 0) return;
        _caravanTick = 0.5;
        if (_wagon is null && (_trade.Run is not null || _trade.WagonOut)) TakeWagon(_trade.WagonOut ? "horse" : null);
        TendRun();
        TendGuarding(0.5);
        TendHunt();
        // What the book did to the roads (places come to know, goods moved), told to the server.
        foreach (var a in _trade.Outbox) Share(a);
        _trade.Outbox.Clear();
        if ((_glutCheck -= 0.5) <= 0) { _glutCheck = 5; WatchGluts(); }
        // A loaded wagon out on the road draws bandits.
        if (_wagon is not null && (_trade.Run is not null || _trade.Loaded > 0) && new Vector2(_wagon.GlobalPosition.X, _wagon.GlobalPosition.Z).Length() > 400 && (_ambushAt -= 0.5) <= 0)
        {
            _ambushAt = 150 + _lootRng.Next(120);
            if (_lootRng.Chance(0.35)) Ambush();
        }
    }

    // ---- the camps of caravan-robbers ------------------------------------------------------------------------

    /// <summary>The caravans lost lately, and the camps of whoever took them: Garrick pays to have them cleared.</summary>
    void OfferTroubles(Person master)
    {
        var options = _city.Roads.Troubles.OrderBy(t => t.Day).Select(t =>
            ($"{(t.Clan ? t.By : "Bandits")} on the road to {t.TownName}, {Direction(master.GlobalPosition, (t.X, t.Z))}: {t.Bandits} of them, {t.Wagons} wagon{(t.Wagons == 1 ? "" : "s")} taken. {t.Bounty} crowns.",
             (Action)(() =>
             {
                 _trade.Hunting = t.Id;
                 Sound.Ui("quest", -5);
                 Say(master, $"They're camped off the road to {t.TownName}, {Direction(master.GlobalPosition, (t.X, t.Z))} of here, a little way to the side of it. {t.Bandits} of them{(t.Clan ? $", {t.By} by the look of what they left" : "")}. Clear them and {t.Bounty} crowns are yours, and the wagons roll again.");
                 Save();
             }))).ToList();
        options.Add(("Not now.", () => Say(master, "They'll not clear themselves.")));
        _hud.Talk(Who(master.Npc), _city.Roads.Troubles.Count == 1
            ? "A caravan's been taken on the road, and the ones who took it are still out there, camped by it and waiting for the next. I'll pay to have them cleared off."
            : $"{_city.Roads.Troubles.Count} caravans taken lately, and the ones who took them are still out there, camped by the roads and waiting for the next. I pay to have them cleared off.", [.. options]);
    }

    /// <summary>The camp the hero's gone for: it's there when they come near, and cleared when the last of them falls.</summary>
    void TendHunt()
    {
        if (_trade.Hunting is not { } id) { ClearCamp(); return; }
        var t = _city.Roads.Troubles.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            // Someone else cleared it, or they moved on.
            _trade.Hunting = null;
            ClearCamp();
            _hud.Toast("Word from the yard: that camp's gone, cleared or moved on. Garrick's bounty with it.", 7);
            return;
        }
        var spot = new Vector3((float)t.X, 0, (float)t.Z);
        var near = new Vector2(_player.GlobalPosition.X - spot.X, _player.GlobalPosition.Z - spot.Z).Length();
        if (_camp is null || _campOf != id)
        {
            if (near > 120) return;
            // Round their fire: bandits, or a clan's raiders, with a chief when there are enough of them.
            _camp = [];
            _campOf = id;
            _terrain.EnsureAround(spot, 1);
            for (var i = 0; i < t.Bandits; i++)
            {
                var a = i * Math.Tau / t.Bandits;
                var at = spot + new Vector3((float)Math.Cos(a) * 4, 0, (float)Math.Sin(a) * 4);
                var kind = i == 0 && t.Bandits >= 5 ? (t.Clan ? "raider_chief" : "bandit_chief") : t.Clan ? "raider" : "bandit";
                var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.5f }, WorldGen.Danger(at.X, at.Z) * (1 + 0.05 * _player.Hero.Level));
                AddEnemy(e);
                _camp.Add(e);
            }
            _hud.Toast($"The camp: {t.Bandits} {(t.Clan ? t.By : "bandits")} round a fire, and the caravan's goods stacked behind them.", 7);
            return;
        }
        if (near > 400) { ClearCamp(); return; } // walked away: they'll be there when you come back
        if (_camp.Any(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) return;
        // Cleared: paid, known for it, and the road's open again (the server hears, so everyone's wagons roll).
        DoCity(CityAction.Cleared(t.Id, _player.Company));
        _trade.Hunting = null;
        _player.Crowns += t.Bounty;
        _city.Renown += 4;
        _standing.Good(2, $"cleared {(t.Clan ? t.By : "the bandits")} off the road to {t.TownName}");
        Sound.Ui("coin", -2);
        _hud.Toast($"The camp's cleared. {t.Bounty} crowns from Garrick, and the road to {t.TownName} is open again.", 9);
        _hud.Log($"Roads: cleared {(t.Clan ? t.By : "the bandits")} off the road to {t.TownName}.");
        _camp = null;
        _campOf = null;
        Save();
    }

    void ClearCamp()
    {
        if (_camp is null) return;
        foreach (var e in _camp) if (IsInstanceValid(e) && e.State != EnemyState.Dead) e.QueueFree();
        _camp = null;
        _campOf = null;
    }

    void Ambush()
    {
        var at = _wagon!.GlobalPosition;
        for (var i = 0; i < 2 + _lootRng.Next(2); i++)
        {
            var a = _lootRng.NextDouble() * Math.Tau;
            var spot = at + new Vector3((float)Math.Cos(a) * 18, 0, (float)Math.Sin(a) * 18);
            var e = Enemy.Create(Bestiary.Get("bandit"), spot with { Y = City.FloorAt(spot) + 0.5f }, WorldGen.Danger(spot.X, spot.Z));
            AddEnemy(e);
            e.Provoke(_player);
        }
        _hud.Toast("Bandits on the road! They want the wagon.", 6);
    }

    IEnumerable<string> CaravanLines(Vector3 pos)
    {
        // A starter's work, on the tracker until it's done: the caravan yard needs a guard.
        if (!_trade.Seasoned && _trade.Guarding is null)
            yield return $"Starter work: the caravan yard by the west gate is hiring guards for the city's caravans. Ask Garrick Wain ({Direction(pos, (YardAt.X, YardAt.Z))})";
        if (_trade.Guarding is { } g && _guarded is not null)
            yield return $"Caravan guard: see the wagon safe to {g.ToName} ({Direction(pos, (g.X, g.Z))}), {g.Wagon:0}% of the load, {g.Pay} crowns";
        if (_ridden is { Reins: true })
            yield return $"On the box: W on, S pull up, A and D steer, E down{(MayStealWagon ? " (or keep the wagon)" : "")}";
        if (_trade.Hunting is { } hunting && _city.Roads.Troubles.FirstOrDefault(x => x.Id == hunting) is { } camp)
            yield return $"Garrick's bounty: clear {(camp.Clan ? camp.By : "the bandits")} camped off the road to {camp.TownName} ({Direction(pos, (camp.X, camp.Z))}), {camp.Bounty} crowns";
        if (_loading is { } l)
            yield return l.Holding
                ? $"Caravan yard: load it onto the wagon ({Direction(pos, (YardWagon.X, YardWagon.Z))}). {l.Left} to go."
                : $"Caravan yard: lift a load from the stack ({Direction(pos, (YardStack.X, YardStack.Z))}). {l.Left} to go.";
        if (_trade.Run is { Glut: true } gr)
            yield return gr.Loaded
                ? $"Glut: take {gr.Load} {CityEconomy.Info(gr.Out).Name} to {gr.ToName} ({Direction(pos, gr.To == TradeBook.CityId ? (YardAt.X, YardAt.Z) : (gr.X, gr.Z))}), {gr.Pay} crowns"
                : $"Glut: load {CityEconomy.Info(gr.Out).Name} at {gr.FromName} ({Direction(pos, (gr.FromX, gr.FromZ))})";
        else if (_trade.Run is { } r)
            yield return r.Returning
                ? $"Caravan: bring {r.ToName}'s {CityEconomy.Info(r.Back).Name} home to the yard ({Direction(pos, (YardAt.X, YardAt.Z))})"
                : $"Caravan: take the wagon to {r.ToName} ({Direction(pos, (r.X, r.Z))}), {r.Pay} crowns";
        else if (_trade.WagonOut)
            yield return $"Your wagon: {_trade.Loaded}/{_trade.Capacity} aboard{(_trade.Loaded > 0 ? $" ({string.Join(", ", _trade.Cargo.Where(c => c.Value > 0).Select(c => $"{c.Value} {CityEconomy.Info(c.Key).Name}"))})" : "")}";
    }

    (string Verb, Action Act)? CaravanAction(Vector3 pos)
    {
        if (_guarded is { } wagon && _trade.Guarding is { } job && _player.Mount is null && pos.DistanceTo(wagon.GlobalPosition with { Y = pos.Y }) < 6) return (job.Driver ? "climb up and take the reins" : "climb up on the wagon", () => ClimbAboard(wagon));
        if (_loading is { Holding: false } && pos.DistanceTo(YardStack with { Y = pos.Y }) < 3.5f) return ("lift a load", LiftSack);
        if (_loading is { Holding: true } && pos.DistanceTo(YardWagon with { Y = pos.Y }) < 4.5f) return ("load the wagon", LoadWagon);
        if (_wagon is { } own && _atSea is null && (_trade.Run is not null || _trade.WagonOut) && _player.Mount is null && pos.DistanceTo(own.GlobalPosition with { Y = pos.Y }) < 6)
        {
            if (own.Teamed) return ("climb up and take the reins", () => ClimbAboard(own));
            return ("look over the wagon", () => _hud.Toast("Nothing in the shafts. Hitch a horse from the party menu (P) to move her.", 5));
        }
        return null;
    }

    /// <summary>For the screenshots: a wagon out, and a grown horse to ride.</summary>
    public void WagonForShot() => TakeWagon("horse");

    public void HorseForShot()
    {
        Adopt("horse", dragon: false, _player.GlobalPosition + new Vector3(2, 0, 0), level: 6);
        ToggleRide();
    }

    /// <summary>For the self-test: the loading done without the walking.</summary>
    public void LoadForTest()
    {
        _loading ??= (5, false);
        LiftSack();
        LoadWagon();
    }
}

/// <summary>
/// A wagon and its horse: a covered cart with goods aboard, the horse in the shafts. On the road it follows the hero
/// (who leads the team, on foot or on horseback); parked at the yard it waits.
/// </summary>
public partial class Wagon(Main main, string? horse) : Node3D
{
    public bool Parked { get; set; }
    /// <summary>Driven by its own carter, to here (a guarded caravan), rather than led by the hero. Stolen, it has none.</summary>
    public Vector3? Goal { get; set; }
    /// <summary>When the carter pulls up.</summary>
    public Func<bool>? Hold { get; set; }
    /// <summary>The road to where she's going, if one's laid: the carter keeps to it.</summary>
    public Highways.Road? Path { get; set; }
    /// <summary>How hard she's driven: 1 at a walk; a little more with someone riding, more again if a driver pushes on, and 0 pulls her up.</summary>
    public float Urge { get; set; } = 1;
    public const float RidePace = 1.7f, PushPace = 3f;
    /// <summary>Someone on the box holds the reins: the horse goes where they steer (-1 left to 1 right), at the pace they set.</summary>
    public bool Reins { get; private set; }
    public float Steer { get; set; }
    /// <summary>How fast she turns her head to the reins (radians a second, at speed), and how long she holds the driver's line after they let go before finding her own road again.</summary>
    const float TurnRate = 0.9f;
    const double HoldsLine = 4;
    double _freeFor = HoldsLine;
    Body? _horse;

    /// <summary>Whether there's a horse in the shafts. With none she stands where she was left, and there's no horse to see.</summary>
    public bool Teamed { get; private set; } = true;

    public void Team(bool hitched)
    {
        Teamed = hitched;
        if (_horse is not null) _horse.Visible = hitched;
    }

    Node3D? _reins;

    /// <summary>What a driver sees of themselves: their hands out in front, and the reins running down to the horse.</summary>
    public void HoldReins(bool holding)
    {
        Reins = holding;
        Steer = 0;
        _reins?.QueueFree();
        _reins = null;
        if (!holding) return;
        AddChild(_reins = new Node3D());
        var skin = new StandardMaterial3D { AlbedoColor = new Color("#b98a68"), Roughness = 0.75f };
        var sleeve = new StandardMaterial3D { AlbedoColor = new Color("#4a3a2c"), Roughness = 0.9f };
        var strap = new StandardMaterial3D { AlbedoColor = new Color("#2a1c12"), Roughness = 0.8f };
        void Limb(Vector3 from, Vector3 to, float radius, Material paint)
        {
            var way = to - from;
            var limb = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = way.Length(), RadialSegments = 10 }, MaterialOverride = paint, Position = (from + to) / 2, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            limb.Basis = new Basis(new Quaternion(Vector3.Up, way.Normalized()));
            _reins!.AddChild(limb);
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var hand = new Vector3(side * 0.2f, 1.72f, -2.72f);
            Limb(new Vector3(side * 0.34f, 1.42f, -2.2f), hand, 0.05f, sleeve);
            _reins.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.062f, Height = 0.12f, RadialSegments = 12, Rings = 8 }, MaterialOverride = skin, Position = hand, Scale = new Vector3(1.1f, 0.9f, 1.25f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            Limb(hand, new Vector3(side * 0.14f, 1.62f, -4.5f), 0.009f, strap);
        }
    }

    /// <summary>Where someone rides: the driver's box up front, or on the load behind (the feet: the eyes are above them).</summary>
    public Vector3 SeatAt(bool driver) => GlobalTransform * (driver ? new Vector3(0, 0.6f, -2.2f) : new Vector3(0, 1.1f, 0.5f));
    float _speed;

    public override void _Ready()
    {
        // Kenney's covered cart (CC0), at a horse's scale, its shafts forward (-Z), and some goods aboard.
        if (Models.Scene(Main.Cargo + "cart.glb")?.Instantiate() is Node3D cart)
        {
            cart.Scale = Vector3.One * 2.6f;
            cart.Rotation = new Vector3(0, CartYaw, 0);
            AddChild(cart);
        }
        foreach (var (file, at) in new[] { ("crate_apples.glb", new Vector3(0.3f, 1.05f, 0.5f)), ("bag.glb", new Vector3(-0.35f, 1.05f, 0.2f)) })
            if (Models.Scene(Main.Cargo + file)?.Instantiate() is Node3D load)
            {
                load.Position = at;
                load.Scale = Vector3.One * 0.75f;
                AddChild(load);
            }
        if (Body.For(horse ?? "horse") is { } h)
        {
            _horse = h;
            AddChild(h);
            h.Position = new Vector3(0, 0, -3.2f);
            h.Visible = Teamed;
        }
    }

    /// <summary>Which way the cart model is turned so its shafts point forward.</summary>
    const float CartYaw = 0;

    public override void _Process(double delta)
    {
        if (Parked || !Teamed) { _horse?.Move(0, true, false); return; }
        if (Reins && Goal is null) { DriveFree(delta); return; }
        if (Goal is { } goal) { Drive(goal, delta); return; }
        var lead = main.PlayerNode.GlobalPosition;
        if (main.AtSeaNode is not null || main.InMine || main.IndoorsNode is not null) { _speed = 0; _horse?.Move(0, true, false); return; } // she waits on the road: a wagon can't follow you aboard, or down a mine, or indoors
        // The horse walks a few metres behind the hero; the wagon trails it.
        var hitch = GlobalPosition + GlobalTransform.Basis.Z * -3.4f;
        var to = new Vector3(lead.X - hitch.X, 0, lead.Z - hitch.Z);
        var gap = to.Length();
        var want = gap > 5 ? Mathf.Min(gap - 5, 12) * 1.4f : 0;
        _speed = Mathf.Lerp(_speed, want, (float)Math.Min(1, delta * 3));
        if (gap > 60) // left far behind (a teleport, a fast horse): it catches up
        {
            var back = lead - to.Normalized() * 8;
            GlobalPosition = back with { Y = City.FloorAt(back) };
            _speed = 0;
        }
        else if (_speed > 0.05f)
        {
            var yaw = Mathf.Atan2(-to.X, -to.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, (float)Math.Min(1, delta * 2.5)), 0);
            var step = -GlobalTransform.Basis.Z * _speed * (float)delta;
            var next = GlobalPosition + step;
            GlobalPosition = next with { Y = Mathf.Lerp(GlobalPosition.Y, City.FloorAt(next), (float)Math.Min(1, delta * 8)) };
        }
        _horse?.Move(_speed, true, false);
    }

    /// <summary>Whether the driver is steering her, or has within the last little while: her head stays where they put it.</summary>
    bool Steered => Reins && (Steer != 0 || _freeFor < HoldsLine);

    /// <summary>Whether the ground a few metres ahead is under water: she won't be driven into it.</summary>
    bool WaterAhead()
    {
        var probe = GlobalPosition - GlobalTransform.Basis.Z * 8;
        return main.World.Height(probe.X, probe.Z) <= WorldGen.WaterLevel + 0.3;
    }

    /// <summary>
    /// The carter drives on toward the goal at a walk, round water, and pulls up when told to. A driver with the reins
    /// steers her off that line wherever they like, and she holds their line a while after they let go, then finds the
    /// road to the goal again herself.
    /// </summary>
    void Drive(Vector3 goal, double delta)
    {
        _freeFor = Steer != 0 ? 0 : _freeFor + delta;
        if (Steered && Steer == 0 && WaterAhead()) _freeFor = HoldsLine; // left pointed at water, she finds her own road again at once rather than standing
        var to = new Vector3(goal.X - GlobalPosition.X, 0, goal.Z - GlobalPosition.Z);
        var want = Hold?.Invoke() == true || (!Steered && to.Length() < 2) || (Steered && WaterAhead()) ? 0 : 3.2f * Urge;
        _speed = Mathf.Lerp(_speed, want, (float)Math.Min(1, delta * 2));
        if (_speed > 0.05f)
        {
            if (Steered) Rotation = new Vector3(0, Rotation.Y - Steer * TurnRate * (float)delta * Math.Min(1, _speed / 1.5f), 0);
            else
            {
                // Along her way by road, a little ahead of where she is, or back to it if she's been driven off it; straight for the goal only where no road goes, or near the end. Never into water: then the nearest way round.
                var aim = to;
                if (Path is { } road && to.Length() > 80)
                {
                    var (off, along, _) = road.Nearest(GlobalPosition.X, GlobalPosition.Z);
                    var (px, pz) = road.At(along + (off < 40 ? 45 : 8));
                    aim = new Vector3((float)px - GlobalPosition.X, 0, (float)pz - GlobalPosition.Z);
                }
                var heading = Mathf.Atan2(-aim.X, -aim.Z);
                foreach (var turn in new[] { 0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f })
                {
                    var dir = new Vector3(-Mathf.Sin(heading + turn), 0, -Mathf.Cos(heading + turn));
                    var probe = GlobalPosition + dir * 8;
                    if (main.World.Height(probe.X, probe.Z) > WorldGen.WaterLevel + 0.3) { heading += turn; break; }
                }
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, heading, (float)Math.Min(1, delta * 2)), 0);
            }
            var next = GlobalPosition - GlobalTransform.Basis.Z * _speed * (float)delta;
            GlobalPosition = next with { Y = Mathf.Lerp(GlobalPosition.Y, City.FloorAt(next), (float)Math.Min(1, delta * 8)) };
        }
        _horse?.Move(_speed, true, false);
    }

    /// <summary>A wagon with no carter and no goal, driven from the box: where the reins say, at the pace the driver sets, and never into the water.</summary>
    void DriveFree(double delta)
    {
        var want = Urge <= 0 || WaterAhead() ? 0 : 3.2f * Urge;
        _speed = Mathf.Lerp(_speed, want, (float)Math.Min(1, delta * 2));
        if (_speed > 0.05f)
        {
            Rotation = new Vector3(0, Rotation.Y - Steer * TurnRate * (float)delta * Math.Min(1, _speed / 1.5f), 0);
            var next = GlobalPosition - GlobalTransform.Basis.Z * _speed * (float)delta;
            GlobalPosition = next with { Y = Mathf.Lerp(GlobalPosition.Y, City.FloorAt(next), (float)Math.Min(1, delta * 8)) };
        }
        _horse?.Move(_speed, true, false);
    }
}
