using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// One of the company set to keep shop: an apprentice or squire sent to the hero's home (the house's counter), to their
/// wagon (parked and opened up), aboard their ship (wherever she's tied up), or to a market stall in their colony's yard. They stand at it and serve whoever
/// comes, slowly, a customer every minute and a half or so (<see cref="Homestead"/>): selling what the hero put out and
/// buying in what the hero asked for, out of the till. Other heroes can trade there too. Talk to them to put goods out,
/// choose what to buy in, take the takings, or bring them back. A ship's shop shuts while she sails, and opens again
/// wherever she ties up.
/// </summary>
public partial class Main
{
    string ShipShopKey => $"{_player.Company}{PlayerStore.ShipMark}";
    string ColonyShopKey(string place) => $"{_player.Company}{PlayerStore.ColonyMark}{place[Keeping.Colony.Length..]}";
    House ColonyHouse(string place) => _pursuits.ColonyShops.TryGetValue(place, out var h) ? h
        : _pursuits.ColonyShops[place] = new House { Owned = true, Built = Housing.Stages.Count, IsHome = true, Furniture = ["shop_counter"] };
    Hall? ColonyAt(string place) => _city.ColoniesOf(_player.Company).FirstOrDefault(h => Keeping.ColonyPlace(h) == place);
    readonly Dictionary<string, Node3D> _colonyStalls = [];
    House ShipHouse => _sea.ShipShop ??= new House { Owned = true, Built = Housing.Stages.Count, IsHome = true, Furniture = ["shop_counter"] };
    readonly Dictionary<string, Person> _keepers = [];

    Bond? KeeperAt(string place) => _bonds.FirstOrDefault(b => b.Keeps == place);

    /// <summary>Where a keeper can be set up now, and what each is called.</summary>
    IEnumerable<(string Place, string Called)> ShopPlaces()
    {
        if (_pursuits.House is { IsHome: true, Finished: true }) yield return (Keeping.Home, ShopCalled(Keeping.Home));
        if (_trade.OwnWagon) yield return (Keeping.Wagon, ShopCalled(Keeping.Wagon));
        if (_sea.OwnShip) yield return (Keeping.Ship, ShopCalled(Keeping.Ship));
        foreach (var colony in _city.ColoniesOf(_player.Company).ToList()) yield return (Keeping.ColonyPlace(colony), ShopCalled(Keeping.ColonyPlace(colony)));
    }

    string ShopCalled(string place) => place switch { Keeping.Home => "at home", Keeping.Wagon => "at your wagon", var c when Keeping.IsColony(c) => $"at {ColonyAt(c)?.Name ?? "your colony"}'s market stall", _ => $"aboard {_sea.ShipName}" };

    /// <summary>The party's keeping-shop options for one of the company.</summary>
    IEnumerable<(string, Action)> KeepingOptions(Bond bond)
    {
        if (!Keeping.CanKeep(bond)) yield break;
        if (bond.Keeps is { } keeps)
        {
            yield return ($"Bring {bond.Name} back from the shop {ShopCalled(keeps)}", () => { StopKeeping(bond); ShowParty(); });
            yield break;
        }
        foreach (var (place, called) in ShopPlaces().Where(p => KeeperAt(p.Place) is null).ToList())
            yield return ($"Set {bond.Name} to keep shop {called}", () => { StartKeeping(bond, place); ShowParty(); });
        if (!ShopPlaces().Any())
            yield return ($"Set {bond.Name} to keep shop (needs a house that's home, a wagon, a ship or a colony)", () => _hud.Toast("Nowhere to keep shop yet: finish a house and list it as home, get a wagon or a ship of your own, or found a colony.", 6));
    }

    void StartKeeping(Bond bond, string place)
    {
        bond.Keeps = place;
        bond.Following = false;
        if (BodyOf(bond) is { } body) { _companions.Remove(body); body.QueueFree(); }
        switch (place)
        {
            case Keeping.Home:
                _pursuits.House.Furniture.Add("shop_counter"); // they knock a counter together, if there isn't one
                SyncStore();
                break;
            case Keeping.Wagon:
                _trade.ShopOpen = true;
                Hitch();
                PushWagonShop();
                break;
            case Keeping.Ship:
                _sea.ShipShopOpen = true;
                PushShipShop();
                break;
            case var c when Keeping.IsColony(c):
                _pursuits.ColonyShopsOpen.Add(c);
                PushColonyShop(c);
                break;
        }
        TendKeepers();
        Sound.Ui("quest", -6);
        _hud.Toast($"{bond.Name} goes to keep shop {ShopCalled(place)}. Give {bond.Name} something to sell and tell them what to buy in; they'll trade a little at a time, whether you're there or not.", 9);
        Save();
    }

    void StopKeeping(Bond bond)
    {
        var place = bond.Keeps;
        bond.Keeps = null;
        CloseShopAt(place);
        if (_keepers.Remove(bond.Id, out var p) && IsInstanceValid(p)) Dismiss(p);
        bond.Following = MayFollow(bond);
        if (bond.Following) BringCompanion(bond);
        TendKeepers(); // a colony's stall packed away with them
        Save();
    }

    /// <summary>Nobody minding it any more: a wagon or ship's shop shuts (what's on the shelves stays for next time). A house's counter is the house's.</summary>
    void CloseShopAt(string? place)
    {
        if (place == Keeping.Wagon && _trade.ShopOpen)
        {
            PullWagonShop();
            DoCity(CityAction.StoreShut(WagonShopKey));
            _trade.ShopOpen = false;
            Hitch();
        }
        if (place == Keeping.Ship && _sea.ShipShopOpen)
        {
            PullShipShop();
            DoCity(CityAction.StoreShut(ShipShopKey));
            _sea.ShipShopOpen = false;
        }
        if (place is { } colony && Keeping.IsColony(colony) && _pursuits.ColonyShopsOpen.Contains(colony))
        {
            PullColonyShop(colony);
            DoCity(CityAction.StoreShut(ColonyShopKey(colony)));
            _pursuits.ColonyShopsOpen.Remove(colony);
        }
    }

    /// <summary>The colony's market stall, in its yard: the world keeps it, and its customers come, whether the hero's there or not.</summary>
    void PushColonyShop(string place)
    {
        if (!_pursuits.ColonyShopsOpen.Contains(place) || ColonyAt(place) is not { } h) return;
        var (at, yaw, _) = ColonyStall(h);
        DoCity(CityAction.StoreSet(new PlayerStore { Owner = ColonyShopKey(place), Name = $"{h.Name} Market Stall", X = at.X, Z = at.Z, Yaw = yaw, Workers = 1, Stall = true, House = PlayerStore.Copy(ColonyHouse(place)) }));
    }

    void PullColonyShop(string place)
    {
        if (_pursuits.ColonyShopsOpen.Contains(place) && _city.StoreOf(ColonyShopKey(place)) is { } s) ColonyHouse(place).Mirror(PlayerStore.Copy(s.House));
    }

    /// <summary>Where a colony's market stall stands in its yard, the far side from where its people work, which way it faces (into the yard), and where its keeper stands.</summary>
    static (Vector3 At, float Yaw, Vector3 Keeper) ColonyStall(Hall h)
    {
        var centre = new Vector3((float)h.X, 0, (float)h.Z);
        var gate = Mathf.Atan2(-centre.Z, -centre.X); // as the hall's laid out (see HallCompound)
        var a = gate - 0.75f;
        var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5f; // in the yard, clear of the buildings round it (which keep 6.5 m off the middle)
        var yaw = Mathf.Atan2(centre.X - at.X, centre.Z - at.Z);
        var keeper = at + new Basis(Vector3.Up, yaw) * new Vector3(1.4f, 0, 1.8f);
        return (at with { Y = City.FloorAt(at) }, yaw, keeper with { Y = City.FloorAt(keeper) });
    }

    /// <summary>A market stall drawn: the stall, its goods, and its name over it.</summary>
    Node3D StallNode(Vector3 at, float yaw, string name)
    {
        var root = new Node3D();
        AddChild(root);
        Models.Place(root, Cargo + "stall_green.glb", at, 2.4f, yaw);
        var b = new Basis(Vector3.Up, yaw);
        foreach (var (x, z, kind) in new[] { (-1.6f, 0.9f, "crate"), (-1.9f, -0.2f, "barrel"), (1.7f, -0.6f, "sack") })
        {
            var spot = at + b * new Vector3(x, 0, z);
            Models.Place(root, Cargo + kind switch { "crate" => "crate_apples.glb", "barrel" => "barrel_apples.glb", _ => "bag.glb" }, spot with { Y = City.FloorAt(spot) }, 1.1f, yaw + x, solid: false);
        }
        root.AddChild(new Label3D { Text = name, Position = at + Vector3.Up * 3.8f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color("#ffd27a") });
        return root;
    }

    /// <summary>The ship's shop, where she lies now; not while she's under way.</summary>
    void PushShipShop()
    {
        if (!_sea.ShipShopOpen || AboardShip) return;
        var (x, z, yaw) = _vessel is { } v ? (v.GlobalPosition.X, v.GlobalPosition.Z, v.Rotation.Y) : ((float)_sea.ShipX, (float)_sea.ShipZ, 0f);
        DoCity(CityAction.StoreSet(new PlayerStore { Owner = ShipShopKey, Name = $"{_sea.ShipName}, {_player.Company}'s ship", X = x, Z = z, Yaw = yaw, Workers = 1, Ship = true, House = PlayerStore.Copy(ShipHouse) }));
    }

    void PullShipShop()
    {
        if (_sea.ShipShopOpen && _city.StoreOf(ShipShopKey) is { } s) ShipHouse.Mirror(PlayerStore.Copy(s.House));
    }

    /// <summary>
    /// The hero's shops away from home (the wagon's, the ship's, a colony's stall) put back in the world if it's lost them
    /// or never heard of them: set up offline or in a world of the hero's own, or on a server restarted since. Each goes
    /// back where it stands, as the hero last saw it, so other heroes find it again. The house's counter sees to itself
    /// (<see cref="SyncStore"/>).
    /// </summary>
    void RestoreShops()
    {
        if (_client?.Status == ClientStatus.Connected && !_sharedCity) return; // the server's copy hasn't arrived yet
        if (_trade is { ShopOpen: true, OwnWagon: true } && _city.StoreOf(WagonShopKey) is null) PushWagonShop();
        if (_sea is { ShipShopOpen: true, OwnShip: true } && _city.StoreOf(ShipShopKey) is null) PushShipShop(); // not while she's under way
        foreach (var place in _pursuits.ColonyShopsOpen.ToList())
            if (_city.StoreOf(ColonyShopKey(place)) is null) PushColonyShop(place);
    }

    /// <summary>Under way: the ship's shop shuts and its keeper goes below; tied up again, it opens where she lies.</summary>
    void ShipShopUnderWay(bool sailing)
    {
        if (!_sea.ShipShopOpen) return;
        if (sailing) { PullShipShop(); DoCity(CityAction.StoreShut(ShipShopKey)); }
        else PushShipShop();
        TendKeepers();
    }

    /// <summary>A keeper's shop, as the world has it now.</summary>
    House ShopOf(string place)
    {
        switch (place)
        {
            case Keeping.Home: SyncStore(); return _pursuits.House;
            case Keeping.Wagon: PullWagonShop(); return WagonHouse;
            case var c when Keeping.IsColony(c): PullColonyShop(c); return ColonyHouse(c);
            default: PullShipShop(); return ShipHouse;
        }
    }

    void PushShop(string place)
    {
        switch (place)
        {
            case Keeping.Home: SyncStore(); break;
            case Keeping.Wagon: PushWagonShop(); break;
            case var c when Keeping.IsColony(c): PushColonyShop(c); break;
            default: PushShipShop(); break;
        }
    }

    /// <summary>Where a keeper stands: by the house's door, beside the wagon, on the ship's deck, at the colony's stall. Null when there's nowhere to stand (the ship under way).</summary>
    Vector3? KeeperSpot(string place)
    {
        switch (place)
        {
            case Keeping.Home:
                var door = HousePlotAt + new Vector3(2.5f, 0, 8);
                return door with { Y = City.FloorAt(door) };
            case Keeping.Wagon:
                if (_wagon is not { } w) return null;
                var by = w.GlobalPosition + w.GlobalTransform.Basis * new Vector3(2.4f, 0, 0.5f);
                return by with { Y = City.FloorAt(by) };
            case var c when Keeping.IsColony(c):
                return ColonyAt(c) is { } h ? ColonyStall(h).Keeper : null;
            default:
                return _vessel is { Aboard: false } v ? v.GlobalPosition + v.GlobalTransform.Basis * new Vector3(-1.1f, OwnVessel.DeckTop, -2.3f) : null;
        }
    }

    /// <summary>Keepers drawn where they keep shop when the hero's near, and sent home if their wagon, ship or house is gone.</summary>
    void TendKeepers()
    {
        foreach (var b in _bonds.Where(b => b.Keeps is not null).ToList())
            if (b.Keeps == Keeping.Wagon && !_trade.OwnWagon || b.Keeps == Keeping.Ship && !_sea.OwnShip || b.Keeps == Keeping.Home && !_pursuits.House.IsHome || Keeping.IsColony(b.Keeps) && ColonyAt(b.Keeps!) is null)
            {
                CloseShopAt(b.Keeps);
                b.Keeps = null;
            }
        if (_sea.ShipShopOpen && KeeperAt(Keeping.Ship) is null) CloseShopAt(Keeping.Ship);
        foreach (var open in _pursuits.ColonyShopsOpen.Where(c => KeeperAt(c) is null).ToList()) CloseShopAt(open);
        var pos = _player.GlobalPosition;
        foreach (var id in _keepers.Keys.ToList())
        {
            var p = _keepers[id];
            var spot = _bonds.FirstOrDefault(b => b.Id == id) is { Keeps: { } k } ? KeeperSpot(k) : null;
            if (!IsInstanceValid(p) || spot is not { } at || at.DistanceTo(pos) > 180)
            {
                if (IsInstanceValid(p)) Dismiss(p);
                _keepers.Remove(id);
            }
            else if (p.GlobalPosition.DistanceTo(at) > 1) p.GlobalPosition = at;
        }
        foreach (var b in _bonds.Where(b => b.Keeps is not null && !_keepers.ContainsKey(b.Id)).ToList())
        {
            if (KeeperSpot(b.Keeps!) is not { } at || at.DistanceTo(pos) > 160) continue;
            var sex = WorldGen.StableHash(b.Id) % 2 == 0 ? Sex.Female : Sex.Male;
            var npc = new Npc($"{Keeping.NpcMark}{b.Id}", b.Name, sex, (int)Math.Max(13, b.JoinAge), $"keeping your shop {ShopCalled(b.Keeps!)}", "city:tristinwold", "Tristinwold",
                "One of your company, minding the counter for you.", ["trade"], "warm", "Trade's slow, but it's trade. What can I do for you?", at.X, at.Z, Race: b.Race ?? "human");
            _keepers[b.Id] = MeetAt(npc, "townsfolk", at.Y);
        }
        // Each colony's stall, drawn while its keeper is.
        foreach (var place in _colonyStalls.Keys.ToList())
            if (KeeperAt(place) is not { } ck || !_keepers.ContainsKey(ck.Id) || ColonyAt(place) is null)
            {
                if (IsInstanceValid(_colonyStalls[place])) _colonyStalls[place].QueueFree();
                _colonyStalls.Remove(place);
            }
        foreach (var b in _bonds.Where(b => Keeping.IsColony(b.Keeps) && _keepers.ContainsKey(b.Id) && !_colonyStalls.ContainsKey(b.Keeps!)).ToList())
            if (ColonyAt(b.Keeps!) is { } col)
            {
                var (at, yaw, _) = ColonyStall(col);
                _colonyStalls[b.Keeps!] = StallNode(at, yaw, $"{col.Name} Market Stall");
            }
    }

    /// <summary>What a keeper can do for the hero.</summary>
    IEnumerable<(string, Action)> KeeperTalk(Person person)
    {
        if (!person.Npc.Id.StartsWith(Keeping.NpcMark) || _bonds.FirstOrDefault(b => Keeping.NpcMark + b.Id == person.Npc.Id) is not { Keeps: { } place } bond) yield break;
        var h = ShopOf(place);
        yield return ("How's trade?", () =>
        {
            var on = h.ForSale.Where(h.Chest.ContainsKey).Sum(id => h.Chest[id]);
            Say(person, on + h.Buying.Count == 0
                ? "Nothing on the shelves and nothing to buy in. Give me something to sell, or tell me what to buy, and I'll get going."
                : $"{on} thing{(on == 1 ? "" : "s")} on the shelves, {h.Till} crowns in the till.{(h.Buying.Count > 0 ? $" Buying in {string.Join(", ", h.Buying.Select(id => Items.Get(id)?.Name ?? id))}, while the till holds out." : "")} One customer at a time; it adds up.");
        });
        yield return ("Here's something to sell.", () => KeeperShelves(person, place));
        yield return ("Buy these in for me.", () => KeeperBuying(person, place));
        if (h.Till > 0)
            yield return ($"I'll take the takings. ({h.Till} crowns)", () =>
            {
                var shop = ShopOf(place);
                _player.Crowns += shop.Till;
                var took = shop.Till;
                shop.Till = 0;
                PushShop(place);
                Sound.Ui("coin");
                Save();
                Say(person, $"{took} crowns. Count it if you like.");
            });
        yield return ("Come back with me.", () =>
        {
            _hud.EndTalk();
            StopKeeping(bond);
            _hud.Toast($"{bond.Name} shuts up shop and comes back to you.", 5);
        });
    }

    void KeeperShelves(Person person, string place)
    {
        var h = ShopOf(place);
        var options = new List<(string, Action)>();
        foreach (var (item, n) in _player.Pack.Contents.Where(c => !c.Item.Bound && c.Item.Kind is not ItemKind.Tool && !_player.Pack.Worn.ContainsValue(c.Item.Id)).Take(12).ToList())
            options.Add(($"Put out {Items.Count(item.Id, n)} ({Homestead.SellsFor(item.Id)} crowns each)", () =>
            {
                var shop = ShopOf(place);
                if (_player.Pack.Take(item.Id, n)) { shop.Chest[item.Id] = shop.Chest.GetValueOrDefault(item.Id) + n; shop.ForSale.Add(item.Id); }
                PushShop(place);
                Save();
                KeeperShelves(person, place);
            }));
        foreach (var (id, n) in h.Chest.OrderBy(kv => kv.Key).ToList())
            options.Add(($"Take back {Items.Count(id, n)}", () =>
            {
                var shop = ShopOf(place);
                var have = shop.Chest.GetValueOrDefault(id);
                if (have > 0) _player.Pack.Add(id, have);
                shop.Chest.Remove(id);
                shop.ForSale.Remove(id);
                PushShop(place);
                Save();
                KeeperShelves(person, place);
            }));
        options.Add(("That's all", () => Talk(person)));
        _hud.Talk($"{person.Npc.Name}'s shelves", h.Chest.Count == 0 ? "Bare. Hand over anything you want sold." : string.Join(", ", h.Chest.Select(kv => Items.Count(kv.Key, kv.Value))), [.. options]);
    }

    void KeeperBuying(Person person, string place)
    {
        var h = ShopOf(place);
        var options = _player.Pack.Contents.Where(c => c.Item.Kind is not ItemKind.Tool && !c.Item.Bound).Select(c => c.Item.Id).Concat(h.Buying).Distinct().Order().Take(12)
            .Select(id => ($"{(h.Buying.Contains(id) ? "Stop buying" : "Buy in")} {Items.Get(id)?.Name ?? id} (pays {Homestead.BuysFor(id)} crowns)", (Action)(() =>
            {
                var shop = ShopOf(place);
                if (!shop.Buying.Remove(id)) shop.Buying.Add(id);
                PushShop(place);
                Save();
                KeeperBuying(person, place);
            }))).ToList();
        options.Add(("That's all", () => Talk(person)));
        _hud.Talk("What to buy in", $"Paid out of the till ({h.Till} crowns in it), a crown over the shops in town. What's bought goes on the shelves.", [.. options]);
    }

    // ---- for the self-test ----
    public void AddBondForTest(Bond bond) => _bonds.Add(bond);
    public Bond? BondForTest(string id) => _bonds.FirstOrDefault(b => b.Id == id);
    public IEnumerable<string> KeepingOptionsForTest(Bond bond) => KeepingOptions(bond).Select(o => o.Item1);
    public void StartKeepingForTest(Bond bond, string place) => StartKeeping(bond, place);
    public void StopKeepingForTest(Bond bond) => StopKeeping(bond);
    public void PutOutForTest(string place, string id, int n)
    {
        var shop = ShopOf(place);
        if (_player.Pack.Take(id, n)) { shop.Chest[id] = shop.Chest.GetValueOrDefault(id) + n; shop.ForSale.Add(id); }
        PushShop(place);
    }
    public Person? KeeperForTest(Bond bond) { TendKeepers(); return _keepers.GetValueOrDefault(bond.Id); }
    public string ColonyShopKeyForTest => ColonyShopKey(Keeping.Colony);
    public Vector3? ColonyStallForTest => ColonyAt(Keeping.Colony) is { } h ? ColonyStall(h).At : null;
    public bool ColonyStallDrawnForTest => _colonyStalls.TryGetValue(Keeping.Colony, out var s) && IsInstanceValid(s);
    public string ShipShopKeyForTest => ShipShopKey;
    public void RestoreShopsForTest() => RestoreShops();
}
