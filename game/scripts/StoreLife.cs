using System.Text.Json;
using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Heroes' homes in the shared world. Your house is mirrored into the city (see <see cref="PlayerStore"/>), which keeps
/// your people working and your counter open whether you're on or not; other heroes find your shop on the row by
/// Hearthside, and trade there at a crown better than any local shop.
/// </summary>
public partial class Main
{
    string? _storeSeen;
    readonly Dictionary<string, (Node3D Front, Person Keeper)> _storeFronts = [];
    const float StoresDrawnWithin = 260;

    /// <summary>Where the i-th shop stands on the row.</summary>
    static Vector3 StoreLot(int i) => HousePlotAt + new Vector3(-30 - i % 6 * 15, 0, -6 - i / 6 * 16);

    /// <summary>The city's copy and the hero's own: what the hero changed goes up, and what happened while they were away comes down.</summary>
    void SyncStore()
    {
        var (h, me) = (_pursuits.House, _player.Company);
        if (_client?.Status == ClientStatus.Connected && !_sharedCity) return; // the server's copy hasn't arrived, and it may be newer
        if (!h.IsHome || !h.Finished)
        {
            if (_city.Stores.ContainsKey(me)) DoCity(CityAction.StoreShut(me));
            _storeSeen = null;
            return;
        }
        var store = _city.StoreOf(me);
        if (store is not null && _storeSeen is null) h.Mirror(store.House);
        var now = JsonSerializer.Serialize(h);
        var workers = _bonds.Count(Homestead.Free);
        if (store is null || now != _storeSeen && _storeSeen is not null || store.Workers != workers)
        {
            var lot = StoreLot(store is null ? _city.Stores.Count : 0);
            DoCity(CityAction.StoreSet(new PlayerStore
            {
                Owner = me, Name = $"{me}'s Shop", X = store?.X ?? lot.X, Z = store?.Z ?? lot.Z, Workers = workers, House = PlayerStore.Copy(h),
            }));
            _storeSeen = now;
            return;
        }
        h.Mirror(PlayerStore.Copy(store.House));
        _storeSeen = JsonSerializer.Serialize(h);
    }

    /// <summary>Other heroes' open shops near the hero: a shopfront, a sign, and someone minding the counter.</summary>
    void TendStoreFronts()
    {
        var pos = _player.GlobalPosition;
        foreach (var owner in _storeFronts.Keys.ToList())
            if (_city.StoreOf(owner) is not { Open: true } s || new Vector2((float)s.X - pos.X, (float)s.Z - pos.Z).Length() > StoresDrawnWithin * 1.2f)
            {
                _storeFronts[owner].Front.QueueFree();
                _storeFronts.Remove(owner);
            }
        foreach (var s in _city.Stores.Values.Where(s => s.Open && s.Owner != _player.Company && s.Owner != WagonShopKey && s.Owner != ShipShopKey && !s.Owner.StartsWith(_player.Company + PlayerStore.ColonyMark) && !_storeFronts.ContainsKey(s.Owner)))
        {
            var at = new Vector3((float)s.X, 0, (float)s.Z);
            if (new Vector2(at.X - pos.X, at.Z - pos.Z).Length() > StoresDrawnWithin) continue;
            at.Y = City.FloorAt(at);
            var root = new Node3D();
            AddChild(root);
            OwnVessel? moored = null;
            if (s.Stall) // a colony's market stall, in its yard
            {
                root.QueueFree();
                root = StallNode(at, (float)s.Yaw, s.Name);
            }
            else if (s.Ship)
            {
                moored = new OwnVessel(this) { Called = s.Name, Moored = true };
                root.AddChild(moored);
                moored.GlobalPosition = at with { Y = (float)WorldGen.WaterLevel };
                moored.Rotation = new Vector3(0, (float)s.Yaw, 0);
            }
            else if (s.Wagon)
            {
                var wagon = new Wagon(this, null) { Parked = true, Steam = true };
                root.AddChild(wagon);
                wagon.GlobalPosition = at;
                wagon.Rotation = new Vector3(0, (float)s.Yaw, 0);
            }
            else Buildings.Place(root, "store", at, 0, s.Owner.Length, Surface.Shingle);
            if (moored is null && !s.Stall) root.AddChild(new Label3D { Text = s.Name, Position = at + new Vector3(0, 6.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
            var by = s.Stall ? at + new Basis(Vector3.Up, (float)s.Yaw) * new Vector3(1.4f, 0, 1.8f) : at + new Vector3(2.5f, 0, 5);
            var (x, z) = ((double)by.X, (double)by.Z);
            var keeper = new Person
            {
                Npc = new Npc($"store:{s.Owner}", $"{s.Owner}'s shopkeeper", Sex.Other, 30, "shopkeeper", "city:tristinwold", "Tristinwold",
                    $"Minds the counter for {s.Owner}, in or out.", ["trade"], "warm", "A crown better than the shops in town, both ways. What'll it be?", x, z),
                Look = "worker", Position = new Vector3((float)x, City.FloorAt(new Vector3((float)x, 0, (float)z)), (float)z),
            };
            root.AddChild(keeper);
            if (moored is not null) keeper.GlobalPosition = moored.DeckSpot; // aboard her, at the rail
            _storeFronts[s.Owner] = (root, keeper);
        }
    }

    /// <summary>What the keeper of someone's shop can do for you.</summary>
    IEnumerable<(string, Action)> StoreTalk(Person person)
    {
        if (!person.Npc.Id.StartsWith("store:")) yield break;
        var owner = person.Npc.Id["store:".Length..];
        yield return ("Let me see what's for sale.", () => StoreBuying(person, owner));
        yield return ("I've things to sell.", () => StoreSelling(person, owner));
    }

    void StoreBuying(Person person, string owner)
    {
        if (_city.StoreOf(owner) is not { Open: true } s) { Say(person, "We're shut. Try again another day."); return; }
        var options = s.House.ForSale.Where(id => s.House.Chest.GetValueOrDefault(id) > 0 && Items.Get(id) is not null).Order().Select(id =>
        {
            var item = Items.Get(id)!;
            var price = _city.StorePrice(item);
            return ($"{item.Name} ({s.House.Chest[id]}), {price} crowns: {item.Text}", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                DoCity(CityAction.StoreBuy(owner, id, price));
                _player.Crowns -= price;
                _player.Pack.Add(id);
                Sound.Ui("coin");
                Save();
                StoreBuying(person, owner);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "Come again.")));
        _hud.Talk($"{s.Name} ({_player.Crowns} crowns)", options.Count == 1 ? "Shelves are bare just now." : "A crown under what you'd pay in town.", [.. options]);
    }

    void StoreSelling(Person person, string owner)
    {
        if (_city.StoreOf(owner) is not { Open: true } s) { Say(person, "We're shut. Try again another day."); return; }
        var options = _player.Pack.Contents.Where(c => !c.Item.Bound && s.House.Buying.Contains(c.Item.Id)).Select(c =>
        {
            var offer = StorePays(c.Item, s);
            return ($"{c.Item.Name} ({c.N}): {offer} crowns each", (Action)(() =>
            {
                if (s.House.Till < offer) { Sound.Ui("error"); Say(person, "There's not that much in the till just now."); return; }
                if (!_player.Pack.Take(c.Item.Id)) return;
                DoCity(CityAction.StoreSell(owner, c.Item.Id, offer));
                _player.Crowns += offer;
                Sound.Ui("coin", -6);
                Save();
                StoreSelling(person, owner);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "Pleasure.")));
        _hud.Talk($"{s.Name}: selling", $"A crown over what {TownShops(s)} would give you, while the till lasts: {s.House.Till} crowns in it.", [.. options]);
    }

    static Vector3 StoreSpot(PlayerStore s) => new((float)s.X, 0, (float)s.Z);

    /// <summary>The shops a store's prices are set against, in words: its own town's, or the town's in general out in the wilds.</summary>
    string TownShops(PlayerStore s) => TownHere(StoreSpot(s)) is { } town && _world.Find(town) is { } t ? $"{t.Name}'s shops" : "the shops in town";

    /// <summary>What the shops of the town a store stands in would pay this hero for something, with all that makes it more or less (their trade, their name there).</summary>
    int TownPays(Item item, Vector3 at) => Math.Max(1, (int)Math.Round(_city.Offer(item) * PerkSell * (TownHere(at) is { } town ? RepSellAt(town) : 1)));

    /// <summary>
    /// What a hero's shop pays for something: a crown better than the town it stands in would pay this hero, whatever
    /// the town (out in the wilds, a crown better than Tristinwold's market). Never more than it asks for the same
    /// thing, so nobody can buy it back off the shelf and sell it to the counter for a profit.
    /// </summary>
    int StorePays(Item item, PlayerStore s) => Math.Max(1, Math.Min(_city.StorePrice(item), TownPays(item, StoreSpot(s)) + 1));

    // ---- aboard another hero's ship ----

    /// <summary>Another hero's ship, tied up with a shop aboard: up her side to trade at her rail, or ashore again.</summary>
    (string Verb, Action Act)? MooredAction(Vector3 pos)
    {
        foreach (var (front, _) in _storeFronts.Values)
        {
            if (!IsInstanceValid(front) || front.GetChildren().OfType<OwnVessel>().FirstOrDefault() is not { } ship) continue;
            if (ship.OnDeck(pos)) return ("step ashore", () => StepOff(ship));
            if (pos.DistanceTo(ship.GlobalPosition with { Y = pos.Y }) <= 10) return ($"go aboard {ship.Called}", () => StepOn(ship));
        }
        return null;
    }

    /// <summary>Up the side of another hero's ship, onto her deck a few steps from whoever keeps shop at her rail.</summary>
    void StepOn(OwnVessel ship)
    {
        if (_player.Mount is not null) { _hud.Toast("Get down first.", 3); return; }
        _player.GlobalPosition = ship.GlobalTransform * OwnVessel.CrewSpots[1] + Vector3.Up * 0.3f;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        _hud.Toast($"Aboard {ship.Called}. Her shopkeeper's at the rail; E at the rail to step ashore.", 5);
    }

    void StepOff(OwnVessel ship)
    {
        if (ShoreBeside(ship) is not { } step) { _hud.Toast("No pier or shore close enough to step onto.", 4); return; }
        _player.GlobalPosition = step with { Y = City.FloorAt(step) + 0.3f };
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
    }

    // ---- for the self-test ----

    public IEnumerable<Person> StoreKeepersForTest => _storeFronts.Values.Select(f => f.Keeper).Where(IsInstanceValid);
    public Vector3? ShoreBesideForTest(OwnVessel ship) => ShoreBeside(ship);
    public string? MooredVerbForTest => MooredAction(_player.GlobalPosition)?.Verb;
    public void MooredActForTest() => MooredAction(_player.GlobalPosition)?.Act();
    public OwnVessel? MooredShipForTest(string owner) => _storeFronts.TryGetValue(owner, out var f) && IsInstanceValid(f.Front) ? f.Front.GetChildren().OfType<OwnVessel>().FirstOrDefault() : null;
    public int StorePaysForTest(Item item, string owner) => StorePays(item, _city.StoreOf(owner)!);
    public int TownPaysForTest(Item item, Vector3 at) => TownPays(item, at);

    /// <summary>For the self-test.</summary>
    public void TendStoresForTest() { SyncStore(); TendStoreFronts(); }
    public void ForgetStoreSeenForTest() => _storeSeen = null;
    public ReputationBook ReputeForTest => Repute;
}
