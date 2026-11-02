using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Life at sea and in the shipyard (see <see cref="SeaBook"/>). The harbour master at the root of the north pier signs
/// on anyone for the Brine Company's ships: a voyage is played out aboard, at sea, with deck work on every leg, a call
/// at the port, and pirates out of the Teeth who come alongside and board. Voyages home and boardings beaten off bring
/// promotion, bosun, first mate, and at last captain, with a private venture in the company's hold or a whole ship of
/// your own. The shipyard on the shore south of the piers takes on anyone to fit planks to holed hulls; a master
/// shipwright can build their own ship. And a hero whose ship the pirates take may be offered a place among them.
/// </summary>
public partial class Main
{
    SeaBook _sea = new();
    AtSea? _atSea;
    (int Left, bool Holding, bool Building)? _planking;
    Node3D? _plank;
    double _seaTick, _crewTick = 20;

    public SeaBook Sea => _sea;
    public AtSea? AtSeaNode => _atSea;

    /// <summary>The shipyard, on the shore south of the piers: the slipway, its hull, the timber, and the master.</summary>
    public static readonly Vector3 ShipyardAt = new(84, 0, 84);
    /// <summary>Where Osric stands to be found: where the boardwalk meets the east gate's pier, first thing anyone comes to on the docks.</summary>
    public static readonly Vector3 ShipwrightPost = new(City.BoardwalkX - 1, 0, 4);
    static readonly Vector3 SlipHull = new(100, 0, 84), TimberStack = new(80, 0, 78);
    static readonly Vector3[] PlankSpots = [new(92, 0, 80.6f), new(96, 0, 80.6f), new(100, 0, 80.6f), new(104, 0, 80.6f)];
    /// <summary>The harbour master's post at the root of the north pier, and the smugglers' corner by the south pier.</summary>
    public static readonly Vector3 HarbourPost = new(97, 0, -36), Smugglers = new(97, 0, 38);
    const string HarbourMaster = "tristinwold/harbourmaster", Shipwright = "tristinwold/shipwright", Smuggler = "tristinwold/smuggler";

    void AddShipyard()
    {
        Townsfolk(new Npc(HarbourMaster, "Captain Marit Hale", Sex.Female, 57, "harbour master of the Brine Company", "city:tristinwold", "Tristinwold",
            "Marit commanded the company's ships for twenty years and lost one to the Teeth. Now she signs on the crews, sends them out, and promotes the ones who come home.",
            ["the sea", "money"], "brisk", "Signing on, or shipping out?", HarbourPost.X, HarbourPost.Z), "guard");
        Townsfolk(new Npc(Shipwright, "Osric Tarr", Sex.Male, 63, "master shipwright of the Tristinwold yard", "city:tristinwold", "Tristinwold",
            "Osric has built ships on this slipway since he could lift an adze. Everything the pirates hole, he mends; everything the company sails, he built or rebuilt.",
            ["the sea", "craft"], "gruff", "Mind the adze. What do you want?", ShipwrightPost.X, ShipwrightPost.Z), "worker");
        Townsfolk(new Npc(Smuggler, "Nell Corran", Sex.Female, 34, "boatwoman of the Drowned Hand", "city:tristinwold", "Tristinwold",
            "Nell rows things out to ships at night and rows other things back. Everyone on the docks knows what she does; nobody says it to the Watch.",
            ["money", "the sea"], "sly", "Nice night for a row. Or isn't it.", Smugglers.X, Smugglers.Z), "traveller");

        // The hull on the slipway, half down the shore into the water, and the yard about it.
        if (Models.Scene("ph:ship.glb")?.Instantiate() is Node3D hull)
        {
            var slip = new Node3D { Position = SlipHull with { Y = -0.4f }, Rotation = new Vector3(0, 0, -0.05f) };
            AddChild(slip);
            slip.AddChild(hull);
        }
        var timber = new StandardMaterial3D { AlbedoColor = new Color("#8a6440"), Roughness = 0.85f };
        for (var i = 0; i < 9; i++)
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(4.2f, 0.18f, 0.32f) }, MaterialOverride = timber,
                Position = TimberStack + new Vector3(0, 0.1f + i / 3 * 0.18f, i % 3 * 0.36f - 0.36f),
            });
        foreach (var spot in PlankSpots) // where the planks go: marked in chalk on the hull
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.06f, 0.06f) }, Position = spot + new Vector3(0, 1.6f, 0.4f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#f0ead0"), EmissionEnabled = true, Emission = new Color("#f0ead0"), EmissionEnergyMultiplier = 0.4f },
            });
        Models.Place(this, Cargo + "barrel_apples.glb", ShipyardAt + new Vector3(-3, 0, 3), 1.1f, 0.3f, solid: false);
        Sign(ShipyardAt + new Vector3(0, 3.4f, -6), "The Shipyard");
        Sign(HarbourPost + new Vector3(0, 3.2f, -1), "Harbour Master");
        Sign(ShipwrightPost + new Vector3(0, 3.2f, 0), "Osric Tarr, Shipwright");
        City.Zones.Add(new Zone("The Shipyard", ShipyardAt + new Vector3(6, 0, 0), 18, 9));
    }

    IEnumerable<(string, Action)> SeaTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id is HarbourMaster or Smuggler) yield return ("Who's the worst on the water?", () => Say(person,
            "Black Ysolde's the one you'll meet. The Teeth are hers, and she's bad enough. But she answers to the Dread Pirate Soren of the Red Reckoning, and he's the worst name there is: a sword like a broken gate, and a fist of iron. There's small fry too, crews of a dozen in a leaky hull, on one run this week and another the next. And there's Lu Wukong of the Drunken Monkey, the Apekin king's own brother. He's never touched a company ship. He robs pirates, singing, and when Soren lands he comes and stands in the road. The company says a pirate's a pirate. I've stopped arguing with people who've seen him do it."));
        if (id is HarbourMaster or Smuggler) yield return ("Who's working the water just now?", () => Say(person, WaterNews()));
        if (id == HarbourMaster) foreach (var o in HarbourTalk(person)) yield return o;
        else if (id == Shipwright) foreach (var o in YardWork(person)) yield return o;
        else if (id == Smuggler) foreach (var o in SmugglerTalk(person)) yield return o;
        else if (_atSea?.Agent == person && _sea.Voyage is { InPort: true } v && Seas.Get(v.Port) is { } port)
        {
            if (_sea.Capacity > 0) yield return ("Trade from the hold.", () => TradeAtPort(person, _sea.Market(port, _city.Day)));
            if (v.Explore) yield return ("What is this place?", () => Say(person, $"Stranger, you're the first ship from your side of the water to find {port.Name}. Tell them we'll trade."));
            yield return ($"What news in {port.Name}?", () => Say(person, $"{char.ToUpper(port.About[0])}{port.About[1..]}. That's {port.Name} for you."));
            yield return ("Cast off for home.", () =>
            {
                _sea.CastOff();
                _atSea?.LeavePort();
                _hud.EndTalk();
                _hud.Toast($"Lines in, sails out: {_sea.Ship} stands out of {port.Name} for Tristinwold.", 6);
                Save();
            });
        }
    }

    /// <summary>The small crews on the runs the hero knows, as the quays have it.</summary>
    string WaterNews()
    {
        var t = _guilds.PlaySeconds;
        var out_ = _sea.Ports.Select(p => (Port: p, Crew: _sea.Hunters(p, t))).Where(x => x.Crew is not null).Select(x => $"{x.Crew!.Name} on the {x.Port.Name} run, in {x.Crew.Ship} under {x.Crew.Captain}").ToList();
        return out_.Count == 0 ? "Nothing small about, that I've heard. That just leaves the Teeth." : $"This week it's {string.Join("; ", out_)}. They'll be somewhere else by the next tide.";
    }

    IEnumerable<(string, Action)> HarbourTalk(Person person)
    {
        var s = _sea;
        if (s.Outlaw)
        {
            yield return ($"I want the company's pardon. ({SeaBook.PardonPrice} crowns)", () => BuyPardon(person));
            yield break;
        }
        if (s.Rank == SeaRank.Landsman)
        {
            yield return ("Sign me on.", () =>
            {
                s.SignOn();
                Say(person, $"Deckhand, then. You haul what the bosun says, you sleep where he says, and you're paid when we're home: {s.PayFor(Seas.Ports[0])} crowns for the Druitt run, more for the long ones. Come and see me when you want a ship.");
                Save();
            });
            yield break;
        }
        if (s.Voyage is null)
        {
            yield return ("Where are the ships bound?", () => OfferVoyages(person));
            if (s.NextChart is { } edge)
                yield return ($"I want to chart new waters. ({edge.Leagues} days out, past the charts)", () => OfferChart(person, edge));
            else if (s.Rank >= SeaRank.Bosun && s.Charted.Count > 0)
                yield return ("Is there anything left to chart?", () => Say(person, "You've filled every map I own. The company's never had a better chart-maker."));
            if (s.Capacity > 0)
            {
                yield return ($"Load the hold from the Exchange. ({s.Loaded}/{s.Capacity} aboard)", () => LoadHold(person));
                if (s.Loaded > 0) yield return ("Sell the hold to the Exchange.", () => SellHold(person));
            }
            if (s.MayBuy) yield return ($"Sell me a ship of my own. ({SeaBook.ShipPrice} crowns)", () => BuyShip(person));
        }
        if (s.NextStep(_player.Hero.Level) is { } next)
            yield return ("How do I get on?", () => Say(person, $"You're {Article(SeaBook.Title(s.Rank))}, with {s.Voyages} voyage{(s.Voyages == 1 ? "" : "s")} home and {s.Repelled} boarding{(s.Repelled == 1 ? "" : "s")} beaten off. {next}"));
    }

    static string Article(string word) => ("aeiou".Contains(word[0]) ? "an " : "a ") + word;

    void OfferVoyages(Person person)
    {
        var s = _sea;
        if (s.OwnShip && s.Hull < SeaBook.Seaworthy) { Say(person, $"{s.ShipName} won't swim with her hull like that. Get her to Osric's yard."); return; }
        var options = s.Ports.Select(p => ($"{p.Name}: {p.Leagues} day{(p.Leagues == 1 ? "" : "s")} out, {(p.Pirates < 0.3 ? "quiet water" : p.Pirates < 0.6 ? "pirates about" : "the pirates' own water")}{(s.Hunters(p, _guilds.PlaySeconds) is { } crew ? $", {crew.Name} on that run" : "")}{(s.PayFor(p) > 0 ? $", {s.PayFor(p)} crowns" : "")}",
            (Action)(() => Embark(person, p)))).ToList();
        options.Add(("Not today.", () => Say(person, "The sea'll wait. It always does.")));
        _hud.Talk(Who(person.Npc), s.Rank == SeaRank.Captain
            ? $"Where will you take {s.Ship}, Captain?"
            : $"There's a berth for {Article(SeaBook.Title(s.Rank))} on {SeaBook.CompanyShip} to any of these.", [.. options]);
    }

    /// <summary>Marit's offer of a voyage past the charts: a better wage, worse water, and a new port for the company if you find it.</summary>
    void OfferChart(Person person, SeaPort port)
    {
        if (_sea.OwnShip && _sea.Hull < SeaBook.Seaworthy) { Say(person, $"{_sea.ShipName} won't swim with her hull like that. Get her to Osric's yard."); return; }
        var pay = (int)Math.Round(_sea.PayFor(port) * SeaBook.ExplorePay);
        _hud.Talk(Who(person.Npc), $"A fisherman swears there's land {port.Leagues} days past the last chart I've got. Find it, and it's a port for the company from then on. {(pay > 0 ? $"{pay} crowns, and I won't pretend the water's safe." : "It's your own ship, so it's your own risk.")}",
            ("Cast off for the edge of the chart.", () => Embark(person, port, explore: true)), ("Not today.", () => Say(person, "The edge of the map'll keep.")));
    }

    void Embark(Person? person, SeaPort port, bool explore = false)
    {
        if (_sea.Sail(port, _lootRng, explore, _sea.Hunters(port, _guilds.PlaySeconds)) is not { } v) { if (person is not null) Say(person, "Not with that ship, you're not."); return; }
        _hud.EndTalk();
        _hud.Toast(explore ? $"Aboard {_sea.Ship}, past the last line on the chart: {v.Leagues} days of open water." : $"Aboard {_sea.Ship}, bound for {port.Name}: {v.Leagues} day{(v.Leagues == 1 ? "" : "s")} out, {v.Danger}.", 7);
        GoToSea(v);
        Save();
    }

    void GoToSea(Voyage v)
    {
        _atSea?.QueueFree();
        _atSea = new AtSea(this, v);
        AddChild(_atSea);
        _player.GlobalPosition = AtSea.Aboard;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
    }

    /// <summary>Off the ship and back on the shore (at the harbour master's, or wherever the story puts the hero).</summary>
    void Ashore(Vector3 at)
    {
        _atSea?.QueueFree();
        _atSea = null;
        City.Afloat = null;
        _terrain.EnsureAround(at, 1);
        _player.GlobalPosition = at with { Y = City.FloorAt(at) + 0.5f };
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
    }

    /// <summary>The voyage: deck work, pirates, the port, and home; a voyage from a loaded game puts to sea again.</summary>
    void TendSea(double delta)
    {
        if ((_seaTick -= delta) > 0) return;
        _seaTick = 0.25;
        if (_sea.Voyage is { } v && _atSea is null && !_player.Downed) GoToSea(v);
        if (_sea.Voyage is null && _atSea is not null) Ashore(HarbourPost + new Vector3(2, 0, 2));
        _atSea?.Tend(0.25);
        TendCrews(0.25);
    }

    /// <summary>A small crew on a run the hero trades: word comes to the quay, and that port's market pays for it.</summary>
    void TendCrews(double delta)
    {
        if ((_crewTick -= delta) > 0) return;
        _crewTick = 20;
        var t = _guilds.PlaySeconds;
        foreach (var m in _sea.Markets.Values)
        {
            if (Seas.Get(m.Id) is not { } port || _sea.Voyage?.Port == m.Id || _sea.Hunters(port, t) is not { } crew || !_sea.Hear(crew, crew.Where(t).Round) || !_lootRng.Chance(0.3)) continue;
            var loads = m.Plundered(_city.Day);
            _hud.Log($"Word on the quay: {crew.Name} took {crew.Ship} into {port.Name} and carried off {loads} loads.");
        }
    }

    /// <summary>The ring brought the hero back to the Gallows Perch: whatever was happening at sea went on without them.</summary>
    void SeaWoke()
    {
        if (_sea.Voyage is null) return;
        _sea.Voyage = null;
        _atSea?.QueueFree();
        _atSea = null;
        City.Afloat = null;
        _hud.Toast($"{(_sea.Outlaw ? _sea.Sworn.Ship : _sea.Ship)} comes in behind you and ties up at the quay at {WakeDock.Town}, none the worse. No wage for a voyage you didn't finish.", 8);
    }

    // ---- what happens at sea ------------------------------------------------------------------------------------

    /// <summary>Home: the wage and any promotion; or for a pirate, the Teeth, the plunder and the crew's say.</summary>
    public void HomePort()
    {
        if (_sea.Voyage is not { } v) return;
        if (v.Raid)
        {
            var crew = _sea.Sworn;
            var (share, made) = _sea.Plunder(_player.Hero.Level);
            _player.Crowns += share;
            Ashore(Smugglers + new Vector3(1, 0, -2));
            _hud.Toast(share > 0 ? $"Back at {crew.Haven} with the prize. Your share: {share} crowns. Nell rows you ashore before dawn." : $"Back at {crew.Haven} with nothing to show for it. Nell rows you ashore.", 8);
            if (made is { } p) _hud.Toast($"The crew vote you {SeaBook.Title(p)} of {crew.Ship}.", 8);
        }
        else
        {
            var (pay, made) = _sea.Home(_player.Hero.Level);
            _player.Crowns += pay;
            _city.Renown += 1;
            Ashore(HarbourPost + new Vector3(2, 0, 2));
            _hud.Toast($"Home: {_sea.Ship} ties up in Tristinwold. {(pay > 0 ? $"+{pay} crowns." : "")}", 7);
            if (made is { } r) _hud.Toast(r == SeaRank.Captain
                ? "Marit Hale gives you command: you're a captain of the Brine Company. Sail where you like, and trade on your own account."
                : $"Marit Hale makes you {SeaBook.Title(r)}.", 9);
        }
        Sound.Ui("coin", -2);
        Save();
    }

    /// <summary>The pirates have the ship. Their captain looks the hero over: join, or over the side.</summary>
    public void ShipTaken()
    {
        if (_sea.Voyage is not { } v) return;
        var ship = v.Raid ? _sea.Sworn.Ship : _sea.Ship;
        if (v.Marines)
        {
            _sea.Taken();
            _hud.Toast($"The company's marines take {ship}. They hang the crew at the Gallows Perch, you with them; and the ring brings you back.", 10);
            Ashore(City.PlayerStart);
            Save();
            return;
        }
        var flag = v.Raid && !v.Homeward ? null : Seas.Flag(v.Flag);
        var taker = flag is { HuntsPirates: true } ? PirateCrew.DrunkenMonkey : flag is null ? PirateCrew.Get(v.Crew) ?? PirateCrew.WidowsTooth : PirateCrew.WidowsTooth;
        var title = _sea.Outlaw ? SeaBook.Title(_sea.Pirate) : _sea.Rank == SeaRank.Landsman ? "landsman" : SeaBook.Title(_sea.Rank);
        _sea.Taken();
        _atSea?.ClearDeck();
        _hud.Talk(flag is { } f ? $"{char.ToUpperInvariant(f.Captain[0])}{f.Captain[1..]}, of {f.Ship}" : $"{taker.Captain}, captain of {taker.Ship}",
            flag?.Offer ?? $"\"You fought well for {Article(title)}, and you're still on your feet, which is more than most. {char.ToUpperInvariant(ship[0])}{ship[1..]}'s ours now, and her cargo. There's a place in {taker.Name} for someone who can fight. Or there's the sea.\"",
            ("Join the pirates.", () =>
            {
                _sea.JoinCrew(taker);
                if (taker.Good) _standing.Good(5, "signed aboard the Drunken Monkey");
                else _standing.Evil(5, $"turned pirate aboard {taker.Ship}");
                Ashore(Smugglers + new Vector3(1, 0, -2));
                _hud.Toast($"You're a swab of {taker.Ship} now. When you want to go out on the account, find Nell Corran by the south pier.", 10);
                Save();
            }),
            ("Over the side, then.", () =>
            {
                Ashore(HarbourPost + new Vector3(2, 0, 6));
                _hud.Toast($"You swim. A fishing boat picks you up and brings you home. {ship} is lost.", 9);
                Save();
            }));
        Save();
    }

    public void Repelled()
    {
        var crew = PirateCrew.Get(_sea.Voyage?.Crew);
        var t = _guilds.PlaySeconds;
        if (_sea.Voyage is { Raid: true, Homeward: false } v)
        {
            var sworn = _sea.Sworn;
            _sea.TakePrize();
            if (sworn.Good)
            {
                if (crew is not null) _sea.Break(crew, t);
                _standing.Good(3, $"took {v.PortName} off the pirates");
                _hud.Toast($"She strikes her colours: {v.PortName} is a prize, and that's one less crew on the water. Hands to the plunder, then home to {sworn.Haven}.", 8);
            }
            else
            {
                _standing.Evil(4, $"took {v.PortName} as a prize for {sworn.Ship}");
                _hud.Toast($"She strikes her colours: {v.PortName.Replace("a ", "the ").Replace("an ", "the ")} is a prize. Hands to the plunder, then home to {sworn.Haven}.", 8);
            }
        }
        else
        {
            if (Seas.Flag(_sea.Voyage?.Flag) is { } f && _atSea?.ReachedFlag == true)
            {
                var first = !_sea.Humbled.Contains(f.Id);
                var price = _sea.Humble(f);
                _player.Crowns += price;
                if (first) Take([(f.Loot, 1)]);
                _hud.Toast($"{(_atSea.FlagSlain ? "" : f.Escape + " ")}{(first ? $"The price on {f.Captain}: {price} crowns, and {Items.Get(f.Loot)!.Name} left behind on your deck." : $"+{price} crowns.")}", 12);
            }
            _sea.Repel();
            var bounty = _sea.Rank >= SeaRank.FirstMate ? 15 + _sea.Voyage!.Leagues * 10 : 0;
            if (crew is not null)
            {
                _sea.Break(crew, t);
                bounty += crew.Bounty;
                _standing.Good(2, $"beat off {crew.Name}");
            }
            _player.Crowns += bounty;
            _hud.Toast($"{(crew is not null ? $"Beaten off! {char.ToUpperInvariant(crew.Name[0])}{crew.Name[1..]} cut their grapples and run, and they'll keep their heads down for a while." : "Beaten off! The pirates cut their grapples and run.")}{(bounty > 0 ? $" Their purses: +{bounty} crowns." : "")}", 7);
        }
        Save();
    }

    // ---- the hold -----------------------------------------------------------------------------------------------

    void LoadHold(Person master)
    {
        var options = Enum.GetValues<Good>().Where(g => _city.Stock.GetValueOrDefault(g) > 6)
            .Select(g => ($"{CityEconomy.Info(g).Name}: {ExchangeAsks(g)} a load ({_city.Stock[g]:0} in the stores)", (Action)(() =>
            {
                var n = Math.Min(Math.Min(5, _sea.Room), (int)_city.Stock[g] - 5);
                var cost = 0;
                for (var i = 0; i < n && _player.Crowns >= ExchangeAsks(g); i++)
                {
                    var price = ExchangeAsks(g);
                    _player.Crowns -= price;
                    cost += price;
                    _city.Stock[g] -= 1;
                    _sea.Hold[g] = _sea.Hold.GetValueOrDefault(g) + 1;
                    _sea.Profit -= price;
                }
                _hud.Toast(cost > 0 ? $"{cost} crowns for {CityEconomy.Info(g).Name} ({_sea.Loaded}/{_sea.Capacity} in the hold)." : _sea.Room == 0 ? "The hold's full." : "You can't afford it.", 4);
                Save();
                LoadHold(master);
            }))).Take(8).ToList();
        options.Add(("That's enough.", () => Say(master, "Fair winds.")));
        _hud.Talk(Who(master.Npc), $"What goes in the hold? {_sea.Room} loads of room, {_player.Crowns} crowns in your purse.", [.. options]);
    }

    void SellHold(Person master)
    {
        var crowns = 0;
        foreach (var (g, n) in _sea.Hold.ToList())
            for (var i = 0; i < n; i++)
            {
                crowns += (int)Math.Round(ExchangePays(g) * PerkTrade);
                _city.Stock[g] = _city.Stock.GetValueOrDefault(g) + 1;
            }
        _sea.Hold.Clear();
        _player.Crowns += crowns;
        _sea.Profit += crowns;
        Sound.Ui("coin", -2);
        Say(master, $"The Exchange takes the lot: {crowns} crowns. You're {(_sea.Profit >= 0 ? $"{_sea.Profit} up" : $"{-_sea.Profit} down")} on your trading, all told.");
        Save();
    }

    void TradeAtPort(Person person, TownMarket m)
    {
        var options = new List<(string, Action)>();
        foreach (var (g, n) in _sea.Hold.Where(c => c.Value > 0).ToList())
            options.Add(($"Sell {n} {CityEconomy.Info(g).Name} (they pay {m.Pays(g)} a load{(m.Wants.Contains(g) ? ", and they want it" : "")})", () =>
            {
                var crowns = (int)Math.Round(m.Sell(g, n) * PerkTrade * RepSellAt(m.Id));
                _sea.Hold.Remove(g);
                _player.Crowns += crowns;
                _sea.Profit += crowns;
                TradedAt(m.Id, m.Name);
                Sound.Ui("coin", -2);
                Save();
                TradeAtPort(person, m);
            }));
        foreach (var g in m.Makes.Where(g => m.ForSale(g) > 0 && _sea.Room > 0))
        {
            var n = Math.Min(Math.Min(_sea.Room, m.ForSale(g)), 8);
            options.Add(($"Buy {n} {CityEconomy.Info(g).Name} ({(int)Math.Round(m.Cost(g, n) * RepBuyAt(m.Id))} crowns; Tristinwold pays about {ExchangePays(g)} a load)", () =>
            {
                var cost = (int)Math.Round(m.Cost(g, n) * RepBuyAt(m.Id));
                if (_player.Crowns < cost) { _hud.Toast("You can't afford it.", 3); return; }
                m.Buy(g, n);
                _player.Crowns -= cost;
                _sea.Profit -= cost;
                _sea.Hold[g] = _sea.Hold.GetValueOrDefault(g) + n;
                Save();
                TradeAtPort(person, m);
            }));
        }
        options.Add(("Done.", () => Say(person, "Fair winds home.")));
        m.Visits++;
        _hud.Talk(Who(person.Npc), $"{m.Name} sells {m.MakesText} cheap and pays well for {m.WantsText}. You've {_sea.Loaded}/{_sea.Capacity} in the hold and {_player.Crowns} crowns.", [.. options]);
    }

    void BuyShip(Person master)
    {
        if (_player.Crowns < SeaBook.ShipPrice) { Say(master, $"{SeaBook.ShipPrice} crowns, Captain. Ships aren't cheap; Osric could build you one for less, if you'd the skill."); return; }
        _player.Crowns -= SeaBook.ShipPrice;
        var name = SeaBook.NameShip(_lootRng);
        _sea.Launch(name);
        Say(master, $"She's yours: {name}, a sound pinnace with a hold for fifty loads. No company wage on your own ship; every crown she makes is yours.");
        Save();
    }

    void BuyHull(Person master, ShipSize size)
    {
        var hull = SeaBook.Hulls[size];
        var (crowns, had) = (_player.Crowns, _sea.OwnShip);
        if (_sea.BuyHull(size, SeaBook.NameShip(_lootRng), ref crowns) is not { } paid) { Say(master, $"{_sea.HullCost(size)} crowns for a {hull.Name}. I don't build on credit."); return; }
        _player.Crowns = crowns;
        Sound.Ui("coin", -2);
        Say(master, had ? $"Done. {_sea.ShipName} goes out as a {hull.Name} now, {paid} crowns on top of the old hull, with a hold for {_sea.Capacity} loads." : $"She's yours: {_sea.ShipName}, a {hull.Name} with a hold for {_sea.Capacity} loads. {paid} crowns, and every one of them worth it.");
        Save();
    }

    void FitUpgrade(Person master, string id)
    {
        var kind = SeaBook.UpgradeKinds[id];
        var crowns = _player.Crowns;
        if (_sea.Upgrade(id, ref crowns) is null) { Say(master, $"{kind.Price} crowns for {kind.Name}. Come back when you have them."); return; }
        _player.Crowns = crowns;
        Sound.Ui("anvil");
        Say(master, $"{kind.Name} on {_sea.ShipName}: {kind.Text}.");
        Save();
    }

    void BuyPardon(Person master)
    {
        if (_player.Crowns < SeaBook.PardonPrice) { Say(master, $"I know what you are. The company's pardon is {SeaBook.PardonPrice} crowns, and not a copper less."); return; }
        _player.Crowns -= SeaBook.PardonPrice;
        _sea.Pardon();
        Say(master, "Your name's off the pirates' list and back on the company's books, at the bottom. Deckhand. Don't make me sorry.");
        Save();
    }

    // ---- the pirates' side --------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> SmugglerTalk(Person person)
    {
        if (!_sea.Outlaw)
        {
            yield return ("Who are you rowing out to?", () => Say(person, "Nobody. Fish. Mind your own business, or the Teeth will mind it for you."));
            yield break;
        }
        var crew = _sea.Sworn;
        if (_sea.Voyage is null)
            yield return ($"Row me out to {crew.Ship}. ({SeaBook.Title(_sea.Pirate)})", () =>
            {
                if (_sea.Prowl(_lootRng, _guilds.PlaySeconds) is not { } v) return;
                _hud.EndTalk();
                _hud.Toast($"Out on the account aboard {crew.Ship}, hunting {v.PortName}.", 7);
                GoToSea(v);
                Save();
            });
        yield return ("How do I rise in the crew?", () => Say(person, _sea.Pirate switch
        {
            PirateRank.Swab => $"Take prizes. {SeaBook.ToCutthroat} and they'll call you cutthroat ({_sea.Prizes} so far).",
            PirateRank.Cutthroat => $"{SeaBook.ToQuartermaster} prizes and the crew trust you with the shares ({_sea.Prizes} so far).",
            PirateRank.Quartermaster => $"{SeaBook.ToPirateCaptain} prizes and the years for it (level {SeaBook.CaptainLevel}) and {crew.Captain} gives you a deck of your own to answer for ({_sea.Prizes} so far).",
            _ => "You're the captain. Rise any higher and you'll be a legend, or a corpse.",
        }));
    }

    // ---- the shipyard -------------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> YardWork(Person master)
    {
        var s = _sea;
        if (_planking is null)
        {
            yield return ($"Any work in the yard? ({s.PlankPay} crowns a plank)", () => StartPlanking(master, building: false));
            if (s.KeelLaid) yield return ($"Work on my ship. ({s.Keel}/{SeaBook.KeelJobs} done)", () => StartPlanking(master, building: true));
        }
        // The yard's hulls, for anyone with the crowns, and what he'll fit to one.
        foreach (var size in Enum.GetValues<ShipSize>().Where(z => !s.OwnShip || z != s.Size))
        {
            var hull = SeaBook.Hulls[size];
            yield return ($"{(s.OwnShip ? "Trade her in for" : "Sell me")} a {hull.Name}. ({s.HullCost(size)} crowns; {hull.Hold} loads)", () => BuyHull(master, size));
        }
        if (s.OwnShip)
            foreach (var (id, kind) in SeaBook.UpgradeKinds.Where(k => !s.HasUpgrade(k.Key)))
                yield return ($"Fit {kind.Name} to {s.ShipName}. ({kind.Price} crowns: {kind.Text})", () => FitUpgrade(master, id));
        if (s.MayLayKeel) yield return ($"Lay down a keel for a ship of my own. ({SeaBook.KeelPrice} crowns of timber)", () => LayKeel(master));
        if (s.OwnShip && s.Hull < 100)
        {
            var cost = (int)Math.Ceiling((100 - s.Hull) / SeaBook.PlankMends) * 12;
            yield return ($"Mend {s.ShipName}. ({cost} crowns; she's at {s.Hull:0}%)", () =>
            {
                if (_player.Crowns < cost) { Say(master, $"{cost} crowns, or mend her yourself."); return; }
                _player.Crowns -= cost;
                s.Hull = 100;
                Say(master, $"{s.ShipName}'s as sound as the day she was launched.");
                Save();
            });
        }
        if (s.Wright != WrightRank.None)
            yield return ("How am I doing?", () => Say(master, s.Wright switch
            {
                WrightRank.Apprentice => $"You're an apprentice with {s.Repairs} hull{(s.Repairs == 1 ? "" : "s")} behind you. {SeaBook.JourneymanAt} and I'll call you journeyman.",
                WrightRank.Journeyman => $"Journeyman, {s.Repairs} hulls. {SeaBook.MasterAt} and you're a master, and can build your own.",
                _ => "You're a master shipwright. Build something that floats, and make me proud.",
            }));
    }

    void StartPlanking(Person master, bool building)
    {
        var n = building ? 6 : SeaBook.PlanksPerJob;
        _planking = (n, false, building);
        Say(master, building
            ? $"Six strakes on your own hull today. Timber's on the stack."
            : $"{n} planks off the stack, onto the hull where they're marked in chalk. {_sea.PlankPay} crowns a plank. Pirates make work for honest folk.");
    }

    void LayKeel(Person master)
    {
        if (_player.Crowns < SeaBook.KeelPrice) { Say(master, $"{SeaBook.KeelPrice} crowns of oak and pine. Come back with them."); return; }
        _player.Crowns -= SeaBook.KeelPrice;
        _sea.LayKeel();
        Say(master, $"The keel's laid. {SeaBook.KeelJobs} days' planking and she'll swim. Ask me for work on your ship.");
        Save();
    }

    void TakePlank()
    {
        if (_planking is not { Holding: false } p) return;
        _planking = p with { Holding = true };
        _plank = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.12f, 2.4f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#8a6440") }, Position = new Vector3(0.4f, 1.3f, -0.3f) };
        _player.AddChild(_plank);
    }

    void FitPlank()
    {
        if (_planking is not { Holding: true } p) return;
        _plank?.QueueFree();
        _plank = null;
        _sea.FitPlank(ownShip: p.Building);
        if (!p.Building)
        {
            _player.Crowns += _sea.PlankPay;
            Sound.Ui("coin", -6);
        }
        _planking = p.Left > 1 ? (p.Left - 1, false, p.Building) : null;
        if (_planking is not null) return;
        if (p.Building)
        {
            if (_sea.Build(SeaBook.NameShip(_lootRng)))
                _hud.Toast($"She slides down the slipway and floats: {_sea.ShipName}, your own ship, built with your own hands. She's waiting at the harbour master's.", 10);
            else _hud.Toast($"A good day's planking on your ship ({_sea.Keel}/{SeaBook.KeelJobs}).", 6);
        }
        else
        {
            var made = _sea.JobDone();
            _player.Crowns += 5;
            _hud.Toast($"The hull's tight again (+5 crowns for the job).{(made is { } w ? $" Osric makes you {SeaBook.Title(w)}." : "")}", 7);
        }
        Save();
    }

    (string Verb, Action Act)? SeaAction(Vector3 pos)
    {
        if (_atSea?.Action(pos) is { } aboard) return aboard;
        if (_planking is { Holding: false } && pos.DistanceTo(TimberStack with { Y = pos.Y }) < 3.5f) return ("take a plank", TakePlank);
        if (_planking is { Holding: true } && PlankSpots.Any(s => pos.DistanceTo(s with { Y = pos.Y }) < 3.5f)) return ("fit the plank", FitPlank);
        return null;
    }

    IEnumerable<string> SeaLines(Vector3 pos)
    {
        if (_atSea?.Line() is { } at) yield return at;
        if (_planking is { } p)
            yield return p.Holding
                ? $"Shipyard: fit the plank to the hull ({Direction(pos, (PlankSpots[0].X, PlankSpots[0].Z))}). {p.Left} to go."
                : $"Shipyard: take a plank from the stack ({Direction(pos, (TimberStack.X, TimberStack.Z))}). {p.Left} to go.";
    }

    /// <summary>For the self-test: a voyage begun without the talking, and the yard's work without the walking.</summary>
    public void SailForTest(string port) => Embark(null, Seas.Get(port)!);
    /// <summary>For the self-test: the voyage under way counts as one of exploration.</summary>
    public void ExploreForTest() { if (_sea.Voyage is { } v) v.Explore = true; }
    public void ProwlForTest() { if (_sea.Prowl(_lootRng) is { } v) GoToSea(v); }
    public void PlankForTest(bool building = false)
    {
        _planking ??= (building ? 6 : SeaBook.PlanksPerJob, false, building);
        TakePlank();
        FitPlank();
    }
}

/// <summary>
/// Out at sea: the ship (Poly Haven's scanned pinnace, its deck walkable), the open water to the horizon, the crew's
/// work on deck, the pirates who come alongside and board, and the port at the end of the run. It's played out far
/// above the land, where nothing but sea and sky can be seen.
/// </summary>
public partial class AtSea(Main main, Voyage voyage) : Node3D
{
    public static readonly Vector3 Stage = new(260, 1200, -40);
    /// <summary>The main deck's height, and where the hero comes aboard.</summary>
    public static float DeckY => Stage.Y + 3.85f; // the pinnace's waist deck, with her hull set 1.6 m into the water
    public static Vector3 Aboard => new(Stage.X, DeckY + 0.8f, Stage.Z + 2);
    const float Beam = 3.6f, Length = 15;

    readonly List<Enemy> _boarders = [];
    readonly List<Job> _jobs = [];
    Node3D? _ship, _raider, _port;
    double _clock, _nextJob = 6, _nag = 20, _saved;
    bool _closing;

    /// <summary>Work wanting doing about the deck: done with E where it's marked, or by standing a watch there.</summary>
    sealed class Job(string verb, Node3D marker, double stand, double posted)
    {
        public string Verb => verb;
        public Node3D Marker => marker;
        public double Stand { get; set; } = stand;
        public bool Watch => stand > 0;
        public double Posted => posted;
    }

    /// <summary>The posts about the deck (her bow is +Z) and the work at each, for a hand, an officer, and a pirate.</summary>
    static readonly (string Hand, string Officer, string Pirate, Vector3 At, double Stand)[] Posts =
    [
        ("haul the line", "haul with the hands", "haul the line", new(-2.3f, 0, -6), 0),
        ("haul the line", "haul with the hands", "haul the line", new(2.3f, 0, 3), 0),
        ("scrub the deck", "inspect the deck", "swab the deck", new(0.6f, 0, -1), 0),
        ("man the pump", "sound the well", "man the pump", new(-0.8f, 0, 6), 0),
        ("trim the sail", "trim the sail", "make sail", new(1.4f, 0, -4), 0),
        ("mend the rigging", "check the rigging", "splice the rigging", new(-2.2f, 0, 9), 0),
        ("keep lookout at the bow", "keep lookout at the bow", "watch for prey at the bow", new(0, 0, 11), 20),
        ("take the wheel", "take the helm", "take the helm", new(0, 0, -8), 30),
    ];

    public Person? Agent { get; private set; }
    public IReadOnlyList<Enemy> Boarders => _boarders;
    double _surge;

    /// <summary>An ether-aeronaut's overdrive: she surges ahead at four times her pace for a while (a pursuer's lost).</summary>
    public void Surge(double seconds)
    {
        _surge = seconds;
        main.HudNode.Toast("The engine screams and she leaps ahead, the sea white at her bow.", 5);
    }
    public int Pending => _jobs.Count;
    /// <summary>How far along this leg (0 to 1, kept with the voyage); the self-test moves it on.</summary>
    public double Along { get => voyage.Along; set => voyage.Along = value; }

    public override void _Ready()
    {
        City.Afloat = p => new Vector2(p.X - Stage.X, p.Z - Stage.Z).Length() < 400 && p.Y > Stage.Y - 200 ? DeckY : null;
        // The sea to the horizon, and a floor under it so the land far below never shows through.
        var reach = 6000f;
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(reach, reach), SubdivideWidth = 200, SubdivideDepth = 200 }, Position = Stage,
            MaterialOverride = main.TerrainNode.Fancy ? Graphics.Water(main.TerrainNode.MirrorSky) : new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.25f, 0.33f), Roughness = 0.1f, Metallic = 0.3f } });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(reach, reach) }, Position = Stage with { Y = Stage.Y - 6 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#0b2230"), Roughness = 1, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        });
        _ship = Ship("ph:pinnace.glb", Stage with { Y = Stage.Y - 1.6f }, 0, solid: true);
        // The crew about the deck.
        foreach (var (x, z, yaw) in new[] { (-2.4f, -6f, 0.5f), (2.2f, 6f, 2.6f), (0f, 9.5f, 3.1f) })
            if (Body.For("sailor") is { } sailor)
            {
                AddChild(sailor);
                sailor.GlobalPosition = new Vector3(Stage.X + x, DeckY, Stage.Z + z);
                sailor.Rotation = new Vector3(0, yaw, 0);
            }
        // Work left undone when the game was saved is still waiting.
        for (var i = 0; i < Math.Min(5, voyage.ChoresDue - voyage.Chores); i++) Post(count: false);
        if (voyage.InPort) MakePort();
    }

    /// <summary>A ship on the water, rolling a little; the hero's own walkable.</summary>
    Node3D Ship(string file, Vector3 at, float yaw, bool solid)
    {
        var hold = new Node3D { Position = at, Rotation = new Vector3(0, yaw, 0) };
        AddChild(hold);
        if (Models.Scene(file)?.Instantiate() is Node3D model)
        {
            hold.AddChild(model);
            if (solid)
            {
                // Walk her real deck, her cabin and her hold, and fetch up against her real sides: her own shape, made solid.
                // Her small gear (the boat on its chocks, casks, coils of rope) isn't: nobody gets wedged on a lifeboat.
                var body = new StaticBody3D();
                hold.AddChild(body);
                foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => !m.Name.ToString().Contains("rigging") && !m.Name.ToString().Contains("sails") && !m.Name.ToString().Contains("details")))
                    body.AddChild(new CollisionShape3D { Shape = m.Mesh.CreateTrimeshShape(), Transform = hold.GlobalTransform.AffineInverse() * m.GlobalTransform });
            }
            var t = model.CreateTween().SetLoops();
            t.TweenProperty(model, "rotation", new Vector3(0.006f, 0, 0.012f), 4.5).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(model, "rotation", new Vector3(-0.006f, 0, -0.012f), 4.5).SetTrans(Tween.TransitionType.Sine);
        }
        else if (solid)
        {
            // No scanned ships on this device: a plain hull and deck that do the same job.
            var body = new StaticBody3D();
            hold.AddChild(body);
            var wood = new StandardMaterial3D { AlbedoColor = new Color("#6a4a30"), Roughness = 0.9f };
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(Beam * 2, 3, Length * 2) }, MaterialOverride = wood, Position = new Vector3(0, 1.55f, 0) });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(Beam * 2, 3, Length * 2) }, Position = new Vector3(0, 1.55f, 0) });
            body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.3f, Height = 16 }, MaterialOverride = wood, Position = new Vector3(0, 11, 0) });
        }
        return hold;
    }

    /// <summary>New work at a free post: there's always more on a ship.</summary>
    void Post(bool count = true)
    {
        var free = Posts.Where(p => _jobs.All(j => j.Marker.Position.DistanceTo(Spot(p.At)) > 0.5f)).ToList();
        if (free.Count == 0) return;
        var post = free[main.LootRng.Next(free.Count)];
        var s = main.Sea;
        var verb = s.Outlaw ? post.Pirate : s.Rank >= SeaRank.FirstMate ? post.Officer : post.Hand;
        var glow = new StandardMaterial3D { AlbedoColor = new Color("#d8c08a"), EmissionEnabled = true, Emission = new Color("#ffd27a"), EmissionEnergyMultiplier = 0.6f };
        var marker = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = post.Stand > 0 ? 0.9f : 0.25f, OuterRadius = post.Stand > 0 ? 1.05f : 0.5f }, Position = Spot(post.At), MaterialOverride = glow,
        };
        AddChild(marker);
        _jobs.Add(new Job(verb, marker, post.Stand, _clock));
        if (count) voyage.ChoresDue++;
    }

    static Vector3 Spot(Vector3 at) => new(Stage.X + at.X, DeckY + 0.15f, Stage.Z + at.Z);

    void Complete(Job job)
    {
        voyage.Chores++;
        _jobs.Remove(job);
        job.Marker.QueueFree();
        main.HudNode.Toast(_jobs.Count == 0 ? $"The deck's in order ({voyage.Chores}/{voyage.ChoresDue} this voyage). The bosun nods." : $"Done ({voyage.Chores}/{voyage.ChoresDue} this voyage).", 3);
    }

    // ---- at the wheel, on a voyage of exploration ------------------------------------------------------------

    Node3D? _sea, _goal;
    MeshInstance3D? _wheel;
    readonly Dictionary<(int, int), Node3D> _patches = [];
    readonly HashSet<int> _charted = [];
    bool _helm;
    double _heading, _rudder, _sail = 1, _knock;
    Vector2 _at;

    public bool AtHelm => _helm;
    public double Heading { get => _heading; set => _heading = value; }
    public Vector2 SeaPosition => _at;

    /// <summary>The mouth of Tristinwold's cove, where every voyage puts to sea, and how far a league of it is on the chart.</summary>
    public static readonly Vector2 HarbourMouth = new(980, -350);
    const float LeagueOnChart = 900;

    /// <summary>
    /// Where the ship is on the map: along the line from the harbour mouth to the port (a home port's own quay; a far
    /// port out on its bearing, a league at a time), as far as the leg has come, and back again on the way home; plus
    /// wherever the helm has taken her off that line.
    /// </summary>
    public Vector2 ChartPosition
    {
        get
        {
            var port = Seas.Get(voyage.Port);
            var end = voyage.Port switch
            {
                "port:druitt" => new Vector2(-900 + 60, -262 + 40),
                "port:greywater" => new Vector2(640 + 60, -600 + 30),
                _ => HarbourMouth + new Vector2((float)Math.Cos(Helm.Bearing(voyage.Port)), (float)Math.Sin(Helm.Bearing(voyage.Port))) * LeagueOnChart * Math.Max(1, port?.Leagues ?? voyage.Leagues),
            };
            var t = voyage.InPort ? 1 : voyage.Homeward ? 1 - Along : Along;
            return HarbourMouth.Lerp(end, (float)t) + _at;
        }
    }

    /// <summary>The ship's own plan for the map while you're aboard: her hull from above (bow up), the helm and the mast, and where you stand on her deck.</summary>
    public StructureChart DeckChart(Player p)
    {
        Vector2[] hull = [new(0, 17), new(2.6f, 14.5f), new(3.8f, 10), new(4.3f, 0), new(4f, -11), new(3f, -16.5f), new(1.2f, -18), new(-1.2f, -18), new(-3f, -16.5f), new(-4f, -11), new(-4.3f, 0), new(-3.8f, 10), new(-2.6f, 14.5f)];
        var facing = -p.GlobalTransform.Basis.Z;
        return new StructureChart(voyage.InPort ? $"Alongside at {voyage.PortName}" : voyage.Homeward ? "Homeward" : $"Bound for {voyage.PortName}", [], [], hull,
            [(new Vector2(0, -8), "helm"), (new Vector2(0, 0), "mainmast"), (new Vector2(0, 13), "bow")],
            new Vector2(p.GlobalPosition.X - Stage.X, p.GlobalPosition.Z - Stage.Z), new Vector2(facing.X, facing.Z), 0.22f);
    }

    /// <summary>Which way she's heading on the map.</summary>
    public Vector2 ChartHeading
    {
        get
        {
            var port = Seas.Get(voyage.Port);
            var end = voyage.Port switch
            {
                "port:druitt" => new Vector2(-840, -222),
                "port:greywater" => new Vector2(700, -570),
                _ => HarbourMouth + new Vector2((float)Math.Cos(Helm.Bearing(voyage.Port)), (float)Math.Sin(Helm.Bearing(voyage.Port))) * LeagueOnChart * Math.Max(1, port?.Leagues ?? voyage.Leagues),
            };
            var way = (end - HarbourMouth).Normalized();
            return voyage.Homeward ? -way : way;
        }
    }
    double Bearing => Helm.Bearing(voyage.Port);
    static Vector3 WheelAt => new(Stage.X, DeckY, Stage.Z - 8);

    bool CanSteer => voyage.Explore && !voyage.InPort && _raider is null;

    /// <summary>E at the wheel: take her, or give her back.</summary>
    public (string Verb, Action Act)? HelmAction() => _helm ? ("give the wheel back to the bosun", LeaveHelm) : null;

    public void TakeHelm()
    {
        if (!CanSteer || _helm) return;
        _helm = true;
        BuildWheel();
        main.PlayerNode.Face(main.PlayerNode.GlobalPosition + Vector3.Back);
        main.PlayerNode.SitAt(() => new Vector3(Stage.X, DeckY + 0.15f, Stage.Z - 9.3f));
        main.HudNode.Toast("You take the wheel. A and D steer, W for full sail, S to shorten sail near rocks. Follow the compass in the tracker. E to give her back.", 9);
    }

    public void LeaveHelm()
    {
        if (!_helm) return;
        _helm = false;
        main.PlayerNode.StandUp(WheelAt + Vector3.Back * 1.5f + Vector3.Up * 0.4f);
    }

    void BuildWheel()
    {
        if (_wheel is not null) return;
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#5a3a22"), Roughness = 0.7f };
        _wheel = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.6f, Rings = 24, RingSegments = 10 }, MaterialOverride = wood, Position = new Vector3(Stage.X, DeckY + 1.25f, Stage.Z - 8.3f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) };
        for (var i = 0; i < 4; i++)
            _wheel.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 1.1f, RadialSegments = 6 }, MaterialOverride = wood, Rotation = new Vector3(Mathf.Pi / 2, i * Mathf.Pi / 4, 0) });
        AddChild(_wheel);
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.25f, 1.2f, 0.25f) }, MaterialOverride = wood, Position = new Vector3(Stage.X, DeckY + 0.6f, Stage.Z - 8.1f) });
        // The sea round her, in its own frame: it turns and slides past while she stays put on the stage.
        AddChild(_sea = new Node3D());
        var land = new StandardMaterial3D { AlbedoColor = new Color("#7d8a4a"), Roughness = 1 };
        _goal = new Node3D();
        _sea.AddChild(_goal);
        _goal.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 240, Height = 90, RadialSegments = 32, Rings = 12 }, MaterialOverride = land });
        _goal.AddChild(new Label3D { Text = voyage.PortName, Position = Vector3.Up * 90, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 96, OutlineSize = 16, PixelSize = 0.3f, Modulate = new Color("#ffd27a"), NoDepthTest = true });
    }

    /// <summary>A patch of the sea, made as she comes to it: its islets and reefs, and foam on the water to show she's moving.</summary>
    Node3D MakePatch(int px, int pz)
    {
        var patch = new Node3D();
        var sand = new StandardMaterial3D { AlbedoColor = new Color("#c8b47a"), Roughness = 1 };
        var green = new StandardMaterial3D { AlbedoColor = new Color("#4a6a34"), Roughness = 1 };
        var rock = new StandardMaterial3D { AlbedoColor = new Color("#3a3a3e"), Roughness = 0.9f };
        foreach (var m in Helm.In(px, pz, voyage.Port))
        {
            var (at, r) = (new Vector3((float)m.X, 0, (float)m.Z), (float)m.Radius);
            if (m.Reef)
                for (var i = 0; i < 5; i++)
                    patch.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r * 0.42f, Height = r * 0.5f, RadialSegments = 8, Rings = 5 }, MaterialOverride = rock, Position = at + new Vector3(Mathf.Cos(i * 2.4f) * r * 0.6f, 0.5f, Mathf.Sin(i * 2.4f) * r * 0.6f) });
            else
            {
                patch.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 0.3f, RadialSegments = 20, Rings = 8 }, MaterialOverride = sand, Position = at });
                patch.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r * 0.6f, Height = r * 0.5f, RadialSegments = 16, Rings = 8 }, MaterialOverride = green, Position = at + Vector3.Up * r * 0.1f });
            }
        }
        var foam = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = 1, Height = 0.1f, RadialSegments = 6, Rings = 2 }, InstanceCount = 40 };
        var rng = new Tristinwold.Deviant.Rng((ulong)WorldGen.StableHash($"{px}/{pz}"));
        for (var i = 0; i < foam.InstanceCount; i++)
        {
            var spot = new Vector3((px + (float)rng.NextDouble()) * (float)Helm.Patch, 0.1f, (pz + (float)rng.NextDouble()) * (float)Helm.Patch);
            foam.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(2 + (float)rng.NextDouble() * 4, 1, 1 + (float)rng.NextDouble() * 2)), spot));
        }
        patch.AddChild(new MultiMeshInstance3D { Multimesh = foam, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        return patch;
    }

    /// <summary>A moment at the wheel: she answers the rudder, sails on, and what's out there comes up to meet her.</summary>
    double Steer(double delta)
    {
        var talking = main.HudNode.Talking;
        var turn = talking ? 0 : Input.GetAxis("move_right", "move_left");
        _rudder = Mathf.Lerp(_rudder, turn, Math.Min(1, delta * 2.5));
        _heading = Mathf.Wrap(_heading + _rudder * Helm.TurnRate * delta, -Math.PI, Math.PI);
        if (!talking && Input.IsActionPressed("move_back")) _sail = Math.Max(Helm.ReefSail, _sail - delta * 0.6);
        else if (!talking && Input.IsActionPressed("move_forward")) _sail = Math.Min(1, _sail + delta * 0.6);
        var dir = new Vector2((float)Math.Sin(_heading), (float)Math.Cos(_heading));
        _at += dir * (float)(Helm.FullSpeed * _sail * delta);
        if (_wheel is not null) _wheel.Rotation = new Vector3(Mathf.Pi / 2, (float)_rudder * 2.5f, 0);
        if (_ship is not null) _ship.Rotation = new Vector3(0, 0, -(float)_rudder * 0.05f);

        var (cx, cz) = ((int)Math.Floor(_at.X / Helm.Patch), (int)Math.Floor(_at.Y / Helm.Patch));
        foreach (var key in _patches.Keys.Where(k => Math.Abs(k.Item1 - cx) > 3 || Math.Abs(k.Item2 - cz) > 3).ToList())
        {
            _patches[key].QueueFree();
            _patches.Remove(key);
        }
        for (var px = cx - 3; px <= cx + 3; px++)
            for (var pz = cz - 3; pz <= cz + 3; pz++)
                if (!_patches.ContainsKey((px, pz)))
                {
                    var patch = MakePatch(px, pz);
                    _sea!.AddChild(patch);
                    _patches[(px, pz)] = patch;
                }
        _knock -= delta;
        for (var px = cx - 1; px <= cx + 1; px++)
            for (var pz = cz - 1; pz <= cz + 1; pz++)
                foreach (var m in Helm.In(px, pz, voyage.Port))
                {
                    var d = new Vector2((float)m.X - _at.X, (float)m.Z - _at.Y).Length();
                    if (m.Reef && d < m.Radius + Helm.Clearance && _knock <= 0)
                    {
                        _knock = 5;
                        _at -= dir * 24;
                        main.Sea.Damage(main.Sea.Hull > Helm.LowestHull + Helm.ReefDamage ? Helm.ReefDamage : 0);
                        main.HudNode.Toast("She grinds on the reef! The whole ship shudders. Shorten sail and steer clear.", 5);
                    }
                    else if (!m.Reef && d < Helm.Charting + m.Radius && _charted.Add(m.Id))
                    {
                        voyage.Pay += Helm.ChartPay;
                        main.HudNode.Toast($"An islet nobody's charted! You mark it down: +{Helm.ChartPay} crowns on your pay.", 5);
                    }
                }
        // The sea's frame, turned and slid so her place in it sits where she does on the stage.
        var turned = Basis.Identity.Rotated(Vector3.Up, -(float)_heading);
        _sea!.Transform = new Transform3D(turned, Stage + Vector3.Up * 0.2f - turned * new Vector3(_at.X, 0, _at.Y));
        var left = (float)((1 - Along) * Helm.FullSpeed * voyage.LegSeconds);
        var way = new Vector2((float)Math.Sin(Bearing), (float)Math.Cos(Bearing));
        var goal = _at + way * Math.Min(left + 300, 1300);
        _goal!.Position = new Vector3(goal.X, 0, goal.Y);
        return Helm.Pace(_heading, Bearing, _sail);
    }

    public (string Verb, Action Act)? Action(Vector3 pos)
    {
        if (CanSteer && !_helm && pos.DistanceTo(WheelAt) < 2.6f) return ("take the helm", TakeHelm);
        var job = _jobs.FirstOrDefault(j => !j.Watch && j.Marker.GlobalPosition.DistanceTo(pos) < 2.5f);
        return job is null || voyage.InPort ? null : (job.Verb, () => Complete(job));
    }

    /// <summary>For the self-test: every job on deck done.</summary>
    public void WorkForTest()
    {
        foreach (var j in _jobs.ToList()) Complete(j);
    }

    public void Tend(double delta)
    {
        _clock += delta;
        var p = main.PlayerNode;
        // Over the side: the crew haul you back.
        if (p.GlobalPosition.Y < DeckY - 8 || new Vector2(p.GlobalPosition.X - Stage.X, p.GlobalPosition.Z - Stage.Z).Length() > 60) // below her keel, or well clear of her: not merely below decks
        {
            p.GlobalPosition = Aboard;
            p.Velocity = Vector3.Zero;
            p.ResetPhysicsInterpolation();
            main.HudNode.Toast("Man overboard! The crew throw you a line and haul you back aboard.", 5);
        }
        _boarders.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead || e.GlobalPosition.Y < DeckY - 6);
        if (_raider is not null)
        {
            WatchLu();
            if (_boarders.Count == 0 && !_closing)
            {
                SailAway(_raider);
                _raider = null;
                main.Repelled();
            }
            else
            {
                // Boarders on deck hack at the rigging and hole her. If they win with the hero still on their feet, there's an offer.
                main.Sea.Damage(_boarders.Count * 0.35 * delta);
                if (main.Sea.Hull <= 0 && !p.Downed) { main.ShipTaken(); return; }
            }
            return; // nobody makes way while they're fighting
        }
        if (voyage.InPort) return;
        // The work never stops: more comes every half-minute or so, and a watch is kept by standing it.
        if (!_helm && (_nextJob -= delta) <= 0)
        {
            _nextJob = 30 + main.LootRng.NextDouble() * 30;
            if (_jobs.Count < 5) Post();
        }
        foreach (var watch in _jobs.Where(j => j.Watch).ToList())
            if (new Vector2(p.GlobalPosition.X - watch.Marker.GlobalPosition.X, p.GlobalPosition.Z - watch.Marker.GlobalPosition.Z).Length() < 2.2f && Math.Abs(p.GlobalPosition.Y - DeckY) < 2.5f
                && (watch.Stand -= delta) <= 0)
                Complete(watch);
        // Work left undone slows her: slack lines, a foul bottom, water in the well.
        var behind = _jobs.Count(j => _clock - j.Posted > 45);
        var pace = (_helm ? Steer(delta) : behind >= 3 ? 0.4 : behind >= 2 ? 0.7 : 1) * ((_surge -= delta) > 0 ? 4 : 1);
        if (!_helm && behind >= 2 && (_nag -= delta) <= 0)
        {
            _nag = 40;
            main.HudNode.Toast(main.Sea.Outlaw ? "The quartermaster: \"She's dragging like a barge! Work, or walk the plank!\"" : "The bosun: \"Slack lines and water in the well, and she's losing way! Get to it!\"", 5);
        }
        Along = Math.Clamp(Along + delta * pace / voyage.LegSeconds, 0, 1);
        if ((_saved += delta) > 30) { _saved = 0; main.Save(); }
        if (voyage.AttackDue(Along)) { LeaveHelm(); Attack(); }
        else if (Along >= 1)
        {
            LeaveHelm();
            if (voyage.Homeward) main.HomePort();
            else if (voyage.Port == Brasswake.Port) main.ReachIsle();
            else if (voyage.Raid) { } // she got away: nothing to do but wait for the ship to turn
            else
            {
                main.Sea.Arrive();
                MakePort();
                main.HudNode.Toast($"Land ho: {voyage.PortName}. {(main.Sea.Capacity > 0 ? "Its harbour master comes aboard to trade." : "Its harbour master comes aboard; talk to them when you're ready to sail home.")}", 8);
                main.Save();
            }
        }
        if (voyage.Raid && !voyage.Homeward && Along >= 1)
        {
            voyage.Homeward = true; // the prize slipped away in the night
            ResetLeg();
        }
    }

    void ResetLeg()
    {
        Along = 0;
        voyage.Sprung = 0;
    }

    /// <summary>This boarding is a named captain's, come in person.</summary>
    public bool ReachedFlag { get; private set; }
    /// <summary>The named captain was brought down, and didn't get away.</summary>
    public bool FlagSlain { get; set; }
    bool _vaulting;

    /// <summary>Lu Wukong on a pirate's deck, badly hurt: he goes back over the rail to his own ship, and you've a few seconds to finish him.</summary>
    void WatchLu()
    {
        if (_vaulting || _raider is null || _boarders.FirstOrDefault(e => e.Kind.Id == "lu_wukong") is not { } lu || lu.Stats.Health > lu.Stats.MaxHealth * Legend.WukongFleeAt) return;
        _vaulting = true;
        lu.Carried = true;
        main.HudNode.Toast($"{Legend.WukongOf(main.Economy.WukongDeaths).Retreat} Bring him down now, or he's away!", 7);
        var away = lu.CreateTween();
        away.TweenProperty(lu, "global_position", _raider.GlobalPosition + Vector3.Up * 6, Legend.WukongFlightSeconds);
        away.TweenCallback(Callable.From(() =>
        {
            _boarders.Remove(lu);
            lu.QueueFree();
        }));
    }

    /// <summary>A sail on the horizon, then she's alongside and they're over the rail.</summary>
    void Attack()
    {
        voyage.Sprung++;
        var side = main.LootRng.Chance(0.5) ? 1 : -1;
        var to = new Vector3(Stage.X + side * 12.5f, Stage.Y - 1.6f, Stage.Z);
        _raider = Ship(voyage.Raid && !voyage.Homeward ? "ph:ship_large.glb" : "ph:ship.glb", to + new Vector3(side * 160, 0, -120), Mathf.Pi / 2, solid: false);
        _closing = true;
        var tween = _raider.CreateTween();
        tween.TweenProperty(_raider, "position", to, 9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => Board(side)));
        var named = voyage.Raid && !voyage.Homeward ? null : Seas.Flag(voyage.Flag);
        ReachedFlag = named is not null;
        var crew = PirateCrew.Get(voyage.Crew);
        var foe = named?.Hail ?? (voyage.Raid
            ? (voyage.Homeward ? (crew is null ? "A company patrol! Marines over the rail" : $"Pirates! Friends of {crew.Name} have come looking for you") : $"There she is: {voyage.PortName}. Grapples! Her crew's coming to meet you")
            : crew is null ? "Pirates! A ship out of the Teeth is closing" : $"Pirates! {char.ToUpperInvariant(crew.Ship[0])}{crew.Ship[1..]} is closing, {crew.Name} under {crew.Captain}");
        main.HudNode.Toast($"{foe}. All hands!", 7);
    }

    void Board(int side)
    {
        _closing = false;
        var kind = voyage.Marines ? "marine" : voyage.Flag == "wukong" ? "monkey_hand" : "pirate";
        var crew = PirateCrew.Get(voyage.Crew);
        var danger = WorldGen.Danger(voyage.Leagues * 900, 0) * (1 + 0.05 * main.PlayerNode.Hero.Level);
        for (var i = 0; i < voyage.Pirates; i++)
        {
            var leader = i == 0 && !voyage.Marines && kind == "pirate" && (crew is not null || voyage.Pirates >= 5);
            var spot = new Vector3(Stage.X + side * 2.6f, DeckY + 0.8f, Stage.Z - 8 + i * 16f / Math.Max(1, voyage.Pirates - 1));
            var named = i == 0 && ReachedFlag ? Seas.Flag(voyage.Flag) : null;
            var lu = named?.Id == "wukong";
            var e = Enemy.Create(Bestiary.Get(named?.Kind ?? (leader ? "pirate_captain" : kind)), spot, danger * (lu ? Legend.WukongStrength(main.Economy.WukongDeaths) : 1), look: named?.Id == "soren" ? Legends.Look(main.Economy) : lu ? Legends.LuLook(main.Economy) : null);
            if (leader && named is null) e.Stats.Name = crew?.Captain ?? "Black Ysolde's bosun";
            if (named?.Id == "soren") Legends.Dress(e, main.Economy);
            if (lu) e.Scale = Vector3.One * Legend.WukongOf(main.Economy.WukongDeaths).Size;
            e.Roam = 6;
            main.AddEnemy(e);
            e.Provoke(main.PlayerNode);
            _boarders.Add(e);
        }
    }

    void SailAway(Node3D ship)
    {
        var t = ship.CreateTween();
        t.TweenProperty(ship, "position", ship.Position + new Vector3(Math.Sign(ship.Position.X - Stage.X) * 200, 0, 160), 14);
        t.TweenCallback(Callable.From(ship.QueueFree));
    }

    /// <summary>The port: a quay alongside, its houses and stalls, and its harbour master come aboard.</summary>
    void MakePort()
    {
        if (_port is not null || Seas.Get(voyage.Port) is not { } port) return;
        _port = new Node3D { Position = new Vector3(Stage.X + 14, Stage.Y, Stage.Z) };
        AddChild(_port);
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#8a8478"), Roughness = 0.95f };
        _port.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(16, 2.4f, 70) }, MaterialOverride = stone, Position = new Vector3(4, 0.2f, 0) });
        for (var i = 0; i < 6; i++)
            Buildings.Place(_port, i % 3 == 2 ? "smithy" : "house", new Vector3(8 + i % 2 * 9, 1.4f, -24 + i * 9), -Mathf.Pi / 2, port.Id.GetHashCode() + i);
        _port.AddChild(new Label3D { Text = port.Name, Position = new Vector3(0, 9, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, OutlineSize = 12, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
        Agent = main.MeetAt(new Npc($"{port.Id}/harbour", $"the harbour master of {port.Name}", Sex.Other, 50, $"harbour master of {port.Name}", port.Id, port.Name,
            $"Keeps the quays of {port.Name} and takes the dues of every ship that ties up.", ["the sea"], "brisk", "Dues paid? Then welcome.",
            Stage.X + 2, Stage.Z + 4), "guard", DeckY);
    }

    public void LeavePort()
    {
        if (Agent is not null) main.Dismiss(Agent);
        Agent = null;
        var port = _port;
        _port = null;
        if (port is not null) GetTree().CreateTimer(6).Timeout += () => { if (IsInstanceValid(port)) port.QueueFree(); };
        ResetLeg();
    }

    /// <summary>The ship's lost: the boarders go back with their prize.</summary>
    public void ClearDeck()
    {
        foreach (var e in _boarders.Where(IsInstanceValid)) e.QueueFree();
        _boarders.Clear();
    }

    public string? Line()
    {
        var s = main.Sea;
        var where = voyage.Raid ? $"hunting {voyage.PortName}" : voyage.Homeward ? "homeward bound" : voyage.InPort ? $"in port at {voyage.PortName}" : $"bound for {voyage.PortName}";
        var hull = s.Hull < 100 ? $", hull {s.Hull:0}%" : "";
        if (_helm) return $"At the helm of {(s.Outlaw ? s.Sworn.Ship : s.Ship)}: {Helm.Steer(_heading, Bearing)}. {voyage.PortName} {Along:P0} of the way, sail {_sail:P0}, hull {s.Hull:0}%";
        if (_raider is not null) return $"At sea aboard {(s.Outlaw ? s.Sworn.Ship : s.Ship)}: boarders on deck! {_boarders.Count} left{hull}";
        var left = (int)Math.Ceiling((1 - Along) * voyage.LegSeconds / 60);
        var next = _jobs.OrderBy(j => j.Posted).FirstOrDefault();
        var work = next is null || voyage.InPort ? "" : $". On deck: {next.Verb}{(next.Watch ? $" (stand there {Math.Ceiling(next.Stand)}s)" : "")}{(_jobs.Count > 1 ? $", and {_jobs.Count - 1} more waiting" : "")}";
        var slow = _jobs.Count(j => _clock - j.Posted > 45) >= 2 ? ", losing way" : "";
        return $"At sea aboard {(s.Outlaw ? s.Sworn.Ship : s.Ship)}, {where}{(voyage.InPort ? "" : $" ({Along:P0}, about {left} min to go{slow})")}{hull}{work}";
    }

    public override void _ExitTree()
    {
        foreach (var e in _boarders.Where(IsInstanceValid)) e.QueueFree();
        if (Agent is not null) main.Dismiss(Agent);
        if (main.IsleNode is null) City.Afloat = null;
    }
}
