namespace Tristinwold.Gameplay;

/// <summary>
/// Korrin's empire, the Iron Syndicate: every thug and grunt in the country answers to him the day he's out. His aim is
/// the whole map, every city locked down and every trade route his. While he holds Tristinwold, his influence over the
/// region sets what anything costs anywhere it's still sold (two and a half times, to start; five times, at the
/// worst), and it grows with every town his thugs take. Heroes push it back: burn a supply depot, run food past the
/// blockade, break an ambush on a caravan. Each costs them something: he sends assassins after whoever burned his
/// depot, and hangs a councillor for every load of food that gets in. A depot's captain can be bought instead, and his
/// ground is safe while he stays bought, until Korrin finds out, and then there's no ground and no captain.
/// </summary>
public static class Syndicate
{
    public const int StartInfluence = 40, PerTown = 10, PerDepot = 5, PerSmuggle = 8, PerRescue = 6;

    /// <summary>What a captain costs to buy.</summary>
    public const int Bribe = 400;

    /// <summary>Whether Korrin finds out a bought captain, this time he takes a town: one in three.</summary>
    public static bool FindsOut(string captain, int day)
    {
        var h = 23u;
        foreach (var c in captain + day) h = h * 37 + c;
        return h % 3 == 0;
    }
}

public sealed partial class CityEconomy
{
    public int KorrinInfluence { get; set; }
    public List<string> DepotsBurned { get; set; } = [];
    public List<string> CaptainsBought { get; set; } = [];
    /// <summary>The day the merchants who were starving have food again, and open their best stock, until.</summary>
    public int FedUntil { get; set; }

    /// <summary>What his blockade does to every price still asked: from two and a half times to five.</summary>
    public double KorrinMarkup => KorrinHolds ? 1 + 4 * Math.Clamp(KorrinInfluence, 0, 100) / 100.0 : 1;

    void Sway(int by) => KorrinInfluence = Math.Clamp(KorrinInfluence + by, 0, 100);

    bool DepotBurned(string? depot, string? by)
    {
        if (!KorrinHolds || depot is not { Length: > 0 and <= 40 } || DepotsBurned.Contains(depot)) return false;
        DepotsBurned.Add(depot);
        Sway(-Syndicate.PerDepot);
        News.Add($"{by ?? "Someone"} has burned one of Korrin's supply depots. Prices are easing, a little. Korrin has noticed.");
        return true;
    }

    bool CaptainBought(string? captain)
    {
        if (!KorrinHolds || captain is not { Length: > 0 and <= 40 } || CaptainsBought.Contains(captain) || DepotsBurned.Contains(captain)) return false;
        CaptainsBought.Add(captain);
        return true;
    }

    /// <summary>Food run past the blockade: the hungry merchants open up, the Watch takes heart, and Korrin hangs someone for it.</summary>
    bool Smuggled(string? by)
    {
        if (!KorrinHolds) return false;
        Sway(-Syndicate.PerSmuggle);
        FedUntil = Day + 2;
        News.Add($"{by ?? "Someone"} ran food past Korrin's blockade into Tristinwold. The merchants who were starving have opened their best stock, and the Watch are standing straighter.");
        var council = CouncilOf(Undying.Capital);
        if (council.Members.FirstOrDefault(m => !council.Fallen.Contains(m.Key)) is { Key: { } id, Value: var member })
        {
            Apply(CityAction.CouncilFall(Undying.Capital, id));
            News.Add($"In answer, Korrin hanged {member} of the city council in the market square, and left them there.");
        }
        else News.Add("In answer, Korrin hanged a Watch sergeant in the market square, and left him there.");
        return true;
    }

    bool Rescued(string? by)
    {
        if (!KorrinHolds) return false;
        Sway(-Syndicate.PerRescue);
        News.Add($"{by ?? "Someone"} broke the Iron Syndicate's ambush on a caravan and brought it into the yard. Something's getting through.");
        return true;
    }

    /// <summary>His thugs take a town: his grip tightens; and any captain he's caught selling him out is ash, camp and all.</summary>
    void Tightened()
    {
        Sway(Syndicate.PerTown);
        foreach (var captain in CaptainsBought.Where(c => Syndicate.FindsOut(c, Day)).ToList())
        {
            CaptainsBought.Remove(captain);
            if (!DepotsBurned.Contains(captain)) DepotsBurned.Add(captain);
            News.Add("Korrin found out one of his captains had been bought. There's nothing left of that camp but ash, and nothing left of the captain at all.");
        }
    }
}
