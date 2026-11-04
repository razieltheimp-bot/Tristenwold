using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// An artificer's steam wagon: a house on four iron-tyred wheels, boiler, chimney and all. It needs no horse, but it
/// won't move without something burning in it; its fuel lasts longer the better its maker. Parked, it opens up into a
/// small store anyone can trade at, on the same footing as a hero's home: a crown better than any local shop.
/// </summary>
public partial class Main
{
    string WagonShopKey => $"{_player.Company}{PlayerStore.WagonMark}";

    /// <summary>What the yard can do for an artificer with a wagon of their own.</summary>
    IEnumerable<(string, Action)> SteamTalk(Person person)
    {
        if (person.Npc.Id != Master || _player.Class.Calling != Calling.Artificer || !_trade.OwnWagon || _trade.Steam) yield break;
        yield return ($"Bolt a boiler onto my wagon. ({string.Join(", ", SteamWorks.Needs.Select(n => Items.Count(n.Id, n.N).ToLowerInvariant()))})", () => FitBoiler(person));
    }

    void FitBoiler(Person person)
    {
        var pack = _player.Pack;
        if (SteamWorks.Needs.Any(n => pack.Count(n.Id) < n.N)) { Say(person, "That's a boiler's worth of iron, and you haven't got it. Bring it, and I'll lend you the yard."); return; }
        foreach (var (id, n) in SteamWorks.Needs) pack.Take(id, n);
        (_trade.Steam, _trade.SteamLevel) = (true, _player.Hero.Level);
        foreach (var horse in _bonds.Where(b => b.Hitched).ToList()) Unhitch(horse);
        if (_trade.WagonOut) TakeWagon(null);
        Sound.Ui("quest");
        Say(person, "Well, I've seen everything now. She'll want feeding: anything that burns. Your horses are out of the shafts, and glad of it.");
        Save();
    }

    /// <summary>The steam wagon, seen to on foot: drive her, stoke her, or open up shop.</summary>
    void SteamMenu()
    {
        if (_wagon is not { } own) return;
        var shop = _trade.ShopOpen;
        if (shop) PullWagonShop();
        var options = new List<(string, Action)>();
        if (!shop)
        {
            if (own.Teamed) options.Add(("Take the wheel", () => { _hud.EndTalk(); ClimbAboard(own); }));
            options.Add(($"Stoke the boiler ({_trade.Fuel:0} seconds of steam left)", StokeMenu));
            options.Add(("Open her up as a shop", OpenWagonShop));
        }
        else
        {
            var h = WagonHouse;
            options.Add(("Put goods out", StockMenu));
            options.Add(("Choose what to buy in", BuyInMenu));
            if (h.Till > 0) options.Add(($"Empty the till ({h.Till} crowns)", () => { _player.Crowns += h.Till; h.Till = 0; PushWagonShop(); Sound.Ui("coin"); Save(); SteamMenu(); }));
            options.Add(("Close up, and get her on the road", CloseWagonShop));
        }
        options.Add(("Leave her", _hud.EndTalk));
        _hud.Talk("Your steam wagon", shop ? $"Open for business: {WagonHouse.Chest.Values.Sum()} things out, {WagonHouse.Till} crowns in the till. Anyone can trade here, a crown better than the shops in town, whether you're here or not." : $"Boiler {(_trade.Fuel > 0 ? "hot" : "cold")}: {_trade.Fuel:0} seconds of steam, room for {_trade.Capacity} loads.", [.. options]);
    }

    House WagonHouse => _trade.WagonShop ??= new House { Owned = true, Built = Housing.Stages.Count, IsHome = true, Furniture = ["shop_counter"] };

    void StokeMenu()
    {
        var level = _trade.SteamLevel;
        var fuels = _player.Pack.Contents.Where(c => SteamWorks.Burns(c.Item.Id, level) > 0).ToList();
        var options = fuels.Select(c => ($"{c.Item.Name} ({c.N}): {SteamWorks.Burns(c.Item.Id, level):0} seconds each", (Action)(() =>
        {
            if (_player.Pack.Take(c.Item.Id)) _trade.Fuel = Math.Min(SteamWorks.MostFuel, _trade.Fuel + SteamWorks.Burns(c.Item.Id, level));
            Hitch();
            Sound.Ui("equip", -6);
            Save();
            StokeMenu();
        }))).ToList();
        options.Add(("That's enough", SteamMenu));
        _hud.Talk("The firebox", fuels.Count == 0 ? "You've nothing that'll burn. Coal, logs, anything a fire takes." : $"{_trade.Fuel:0} seconds of steam. She burns anything; better makers get further on it.", [.. options]);
    }

    void OpenWagonShop()
    {
        _trade.ShopOpen = true;
        Hitch();
        PushWagonShop();
        Save();
        SteamMenu();
    }

    void CloseWagonShop()
    {
        if (KeeperAt(Keeping.Wagon) is { } keeper) keeper.Keeps = null; // and whoever was minding it goes home
        PullWagonShop();
        DoCity(CityAction.StoreShut(WagonShopKey));
        _trade.ShopOpen = false;
        Hitch();
        Save();
        SteamMenu();
    }

    void PushWagonShop()
    {
        if (_wagon is not { } own) return;
        var at = own.GlobalPosition;
        DoCity(CityAction.StoreSet(new PlayerStore { Owner = WagonShopKey, Name = $"{_player.Company}'s {(_trade.Steam ? "Steam Wagon" : "Wagon")}", X = at.X, Z = at.Z, Workers = 1, Wagon = true, Yaw = own.Rotation.Y, House = PlayerStore.Copy(WagonHouse) }));
    }

    void PullWagonShop()
    {
        if (_trade.ShopOpen && _city.StoreOf(WagonShopKey) is { } s) WagonHouse.Mirror(PlayerStore.Copy(s.House));
    }

    void StockMenu()
    {
        var h = WagonHouse;
        var options = new List<(string, Action)>();
        foreach (var (item, n) in _player.Pack.Contents.Where(c => !c.Item.Bound && !_player.Pack.Worn.ContainsValue(c.Item.Id)).Take(14).ToList())
            options.Add(($"Put out {Items.Count(item.Id, n)}", () =>
            {
                if (_player.Pack.Take(item.Id, n)) { h.Chest[item.Id] = h.Chest.GetValueOrDefault(item.Id) + n; h.ForSale.Add(item.Id); }
                PushWagonShop();
                Save();
                StockMenu();
            }));
        foreach (var (id, n) in h.Chest.OrderBy(kv => kv.Key).ToList())
            options.Add(($"Take back {Items.Count(id, n)}", () =>
            {
                _player.Pack.Add(id, n);
                h.Chest.Remove(id);
                h.ForSale.Remove(id);
                PushWagonShop();
                Save();
                StockMenu();
            }));
        options.Add(("Back", SteamMenu));
        _hud.Talk("The shelves", h.Chest.Count == 0 ? "Bare." : string.Join(", ", h.Chest.Select(kv => Items.Count(kv.Key, kv.Value))), [.. options]);
    }

    void BuyInMenu()
    {
        var h = WagonHouse;
        var options = _player.Pack.Contents.Where(c => c.Item.Kind is not ItemKind.Tool).Select(c => c.Item.Id).Concat(h.Buying).Distinct().Order().Take(14)
            .Select(id => ($"{(h.Buying.Contains(id) ? "Stop buying" : "Buy in")} {Items.Get(id)?.Name ?? id}", (Action)(() =>
            {
                if (!h.Buying.Remove(id)) h.Buying.Add(id);
                PushWagonShop();
                Save();
                BuyInMenu();
            }))).ToList();
        options.Add(("Back", SteamMenu));
        _hud.Talk("What to buy in", $"You pay a crown over the shops, out of the till ({h.Till} crowns in it).", [.. options]);
    }

    /// <summary>A moving steam wagon eats its fuel. When it's gone, she stops where she is.</summary>
    void TendSteam(double delta)
    {
        if (!_trade.Steam || _wagon is not { Moving: true }) return;
        _trade.Fuel = Math.Max(0, _trade.Fuel - delta);
        if (_trade.Fuel > 0) return;
        Hitch();
        _hud.Toast("The boiler's gone cold, and she rolls to a stop. Feed her something that burns.", 7);
    }

    /// <summary>For the self-test: a steam wagon with a full firebox, out on the road.</summary>
    public void SteamForTest(int fuel)
    {
        _trade.OwnWagon = true;
        _trade.WagonOut = true;
        (_trade.Steam, _trade.SteamLevel, _trade.Fuel) = (true, 10, fuel);
        TakeWagon(null);
    }

    public void OpenWagonShopForTest() => OpenWagonShop();
    public void CloseWagonShopForTest() => CloseWagonShop();
    public string WagonShopKeyForTest => WagonShopKey;
}
