namespace Tristinwold.Gameplay;

/// <summary>
/// The steam an artificer can put into a hero's ship or wagon: a boiler that drives a ship along faster than the wind,
/// a lift that takes her up into the air, and a lifeboat made over into a little skyboat. Only an artificer fits one:
/// the hero, if they are one; an artificer friend on the same server, if the hero leaves the makings and an order; or,
/// for a ship, the engineers of Brasswake, for their marks. Each burns anything a fire takes, and goes further the
/// better its maker.
/// </summary>
public static class Engines
{
    public const string ShipSteam = "ship_steam", ShipFlight = "ship_flight", WagonSteam = "wagon_steam", Skyboat = "skyboat";

    public static readonly IReadOnlyDictionary<string, (string Name, IReadOnlyList<(string Id, int N)> Needs, int Crowns)> Works = new Dictionary<string, (string, IReadOnlyList<(string, int)>, int)>
    {
        [ShipSteam] = ("a boiler and paddle-wheels for her", [("iron_ingot", 12), ("iron_nails", 24), ("scrap_iron", 10)], 300),
        [ShipFlight] = ("a lift and gas-bag, to take her up into the air", [("steel_ingot", 6), ("leather", 8), ("iron_nails", 20)], 750),
        [WagonSteam] = ("a boiler for a wagon", [.. SteamWorks.Needs], 0),
        [Skyboat] = ("a lifeboat made over into a skyboat", [("steel_ingot", 3), ("leather", 4), ("iron_nails", 10)], 0),
    };

    /// <summary>What Osric asks for the lifeboat itself.</summary>
    public const int LifeboatPrice = 250;
    /// <summary>The level of a Brasswake engineer, as a maker.</summary>
    public const int IsleMaker = 15;
    /// <summary>How much faster a ship goes under steam, and in the air.</summary>
    public const double SteamPace = 1.8, FlightPace = 2.4;

    public static bool IsArtificer(string classId) => Classes.Get(classId).Calling == Calling.Artificer;

    public static bool HasMakings(Pack pack, string work) => Works[work].Needs.All(n => pack.Count(n.Id) >= n.N);

    public static void TakeMakings(Pack pack, string work)
    {
        foreach (var (id, n) in Works[work].Needs) pack.Take(id, n);
    }

    public static string Makings(string work) => string.Join(", ", Works[work].Needs.Select(n => Items.Count(n.Id, n.N).ToLowerInvariant()));
}

/// <summary>A hero's order for an artificer: what they want fitted, the makings left with it, and what they'll pay whoever does it.</summary>
public sealed class WorkOrder
{
    public string Owner { get; set; } = "";
    public string Work { get; set; } = "";
    public int Pay { get; set; }
    public string? FittedBy { get; set; }
    /// <summary>The level of the artificer who fitted it: their work goes further the better they are.</summary>
    public int Level { get; set; }
    public string Key => $"{Owner}#{Work}";
}

public sealed partial class CityEconomy
{
    /// <summary>The orders heroes have left for an artificer, by owner and work.</summary>
    public Dictionary<string, WorkOrder> Orders { get; set; } = [];

    bool OrderAct(CityAction a)
    {
        switch (a.Kind)
        {
            case "order_post" when a.Contract is { Length: > 0 and <= 60 } owner && a.Item is { } work && Engines.Works.ContainsKey(work):
                var o = new WorkOrder { Owner = owner, Work = work, Pay = Math.Clamp(a.Crowns, 0, 5000) };
                if (Orders.ContainsKey(o.Key)) return false;
                Orders[o.Key] = o;
                return true;
            case "order_fit" when a.Contract is { } key && Orders.TryGetValue(key, out var open) && open.FittedBy is null && a.Item is { Length: > 0 and <= 60 } by && by != open.Owner:
                (open.FittedBy, open.Level) = (by, Math.Clamp(a.N, 1, 60));
                News.Add($"{by} has done {open.Owner}'s order: {Engines.Works[open.Work].Name}.");
                return true;
            case "order_done" when a.Contract is { } done: return Orders.Remove(done);
        }
        return false;
    }
}
