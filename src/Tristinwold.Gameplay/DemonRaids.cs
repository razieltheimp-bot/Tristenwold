namespace Tristinwold.Gameplay;

/// <summary>
/// A raid up out of the Dark Depths: demons from the Rift come up a network's tunnel and fall on the dwarf city in the
/// Deep Trenches first. If nobody stops them there, the next day they burst out of a mine onto the surface, and two days
/// after that they ravage the nearest town. Three times in ten, when they break the surface, the gates of heaven open
/// and heaven's host comes down to end them, and doesn't care what else it ends: the town burns either way, worse.
/// </summary>
public sealed class DemonRaid
{
    public string Id { get; set; } = "";
    /// <summary>The network they came up, and its dwarf city.</summary>
    public string Trench { get; set; } = "";
    public string City { get; set; } = "";
    /// <summary>The mine they'll break out of, where its mouth is, and the town nearest it.</summary>
    public string Mine { get; set; } = "";
    public string MineName { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
    public string Town { get; set; } = "";
    public string TownName { get; set; } = "";
    public int Started { get; set; }
    public bool Surfaced { get; set; }
    public int SurfacedOn { get; set; }

    /// <summary>Whether heaven answers this one, when it breaks the surface: three in ten, the same for everyone.</summary>
    public bool Heaven => DemonRaids.HeavenAnswers(Id);
}

public static class DemonRaids
{
    /// <summary>Days between raids; a day in the Trenches before they rise; two days on the surface before they ravage the town.</summary>
    public const int Every = 4, RiseAfter = 1, RavageAfter = 2, HeavenPercent = 30;

    public static bool HeavenAnswers(string id)
    {
        var h = 17u;
        foreach (var c in id) h = h * 31 + c;
        return h % 100 < HeavenPercent;
    }

    /// <summary>The demon lord over the Seven, by the Rift: he starts stronger than any of them.</summary>
    public const int DuragonLeast = 25;
}

public sealed partial class CityEconomy
{
    public DemonRaid? Demons { get; set; }
    public int NextRaid { get; set; } = 3;
    public int DuragonDeaths { get; set; }
    public int DuragonWins { get; set; }
    public int DuragonBack { get; set; }

    public bool DuragonDown => DuragonBack > Day;
    public int DuragonLevel => DemonRaids.DuragonLeast + 2 * DuragonWins + DuragonDeaths;

    bool RaidBegan(DemonRaid? r)
    {
        if (r is null || Demons is not null || Day < NextRaid || r.Id.Length is 0 or > 60 || r.Trench.Length == 0) return false;
        (r.Started, r.Surfaced) = (Day, false);
        Demons = r;
        News.Add($"Demons out of the Dark Depths are at the gates of {r.City}, the dwarf city in the Deep Trenches under {r.MineName}.");
        return true;
    }

    /// <summary>A raid stopped: cleared by heroes, or (not cleared) the town it reached is burned and its trade wrecked.</summary>
    bool RaidEnded(string? id, string? by, bool cleared)
    {
        if (Demons is not { } r || r.Id != id) return false;
        Demons = null;
        NextRaid = Day + DemonRaids.Every;
        if (cleared)
        {
            News.Add(r.Surfaced
                ? $"{by ?? "Heroes"} drove the demons back down {r.MineName}. {r.TownName} is safe."
                : $"{by ?? "Heroes"} broke the demon raid in {r.City}. The dwarves are singing in the deep roads.");
            return true;
        }
        Ravage(r, r.Heaven);
        return true;
    }

    void Ravage(DemonRaid r, bool heaven)
    {
        if (r.Town.Length > 0)
        {
            FabricOf(r.Town).Burnt += heaven ? 4 : 2;
            Apply(CityAction.Plundered(r.Town));
        }
        News.Add(heaven
            ? $"The gates of heaven opened over {r.MineName}, and heaven's host came down and ended the demons, and {r.TownName} with them: houses burned, the roads broken, and nobody left standing who was in the way."
            : $"The demons out of {r.MineName} have ravaged {r.TownName}: homes burned and its trade wrecked.");
    }

    /// <summary>The raid's clock, run by whoever keeps the city: up out of the Trenches after a day; then heaven, or the town.</summary>
    public void TendRaid()
    {
        if (Demons is not { } r) return;
        if (!r.Surfaced)
        {
            if (Day < r.Started + DemonRaids.RiseAfter) return;
            (r.Surfaced, r.SurfacedOn) = (true, Day);
            News.Add(r.Heaven
                ? $"Demons have burst out of {r.MineName}, and the sky over it has split open: light is coming down. Get clear of {r.TownName}."
                : $"Demons have burst out of {r.MineName} and are making for {r.TownName}.");
            return;
        }
        if (r.Heaven && Day >= r.SurfacedOn + 1 || Day >= r.SurfacedOn + DemonRaids.RavageAfter)
        {
            Demons = null;
            NextRaid = Day + DemonRaids.Every;
            Ravage(r, r.Heaven);
        }
    }

    bool DuragonFell(string? by)
    {
        if (DuragonDown) return false;
        DuragonDeaths++;
        DuragonBack = Day + 2;
        News.Add($"{by ?? "Someone"} has struck down Duragon at the Rift. The demon lord will rise again. They always do, down there.");
        return true;
    }
}
