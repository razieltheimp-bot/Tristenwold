namespace Tristinwold.Gameplay;

/// <summary>What a carpenter's job is: a burned home raised again, an empty one put right, or a new house out past the wall.</summary>
public enum CarpentryWork { Raise, Mend, Grow }

/// <summary>
/// The carpenters' trade, open to any hero. Their master at the yard by Hollin's woodlot hands out the week's work
/// wherever it's wanted: the burned first, then the empty homes of towns that are doing well again, then new houses
/// past the walls of the ones that are thriving. A hero brings the planks and puts the house up, and the town has it,
/// for everyone.
/// </summary>
public static class Carpentry
{
    public const int Planks = 6, Stages = 3, JourneymanAt = 4, MasterAt = 10;

    public static string Rank(int done) => done >= MasterAt ? "master carpenter" : done >= JourneymanAt ? "journeyman carpenter" : "carpenter's apprentice";

    public static int Pay(int done, CarpentryWork work) => (done >= MasterAt ? 110 : done >= JourneymanAt ? 70 : 40) + (work == CarpentryWork.Grow ? 30 : 0);

    public static string Say(CarpentryWork work, string town) => work switch
    {
        CarpentryWork.Raise => $"Raise a burned home in {town}",
        CarpentryWork.Mend => $"Put an empty home right in {town}",
        _ => $"Build a new house out past {town}'s wall",
    };

    /// <summary>The work a town wants, if any: the burned first, then the rotten if trade's come back, then a new house if it's thriving.</summary>
    public static CarpentryWork? Wanted(CityEconomy city, string town)
    {
        var f = city.FabricOf(town);
        if (f.Burnt > 0) return CarpentryWork.Raise;
        if (f.Derelict > 0 && city.Prosperity(town) > CityEconomy.Fails) return CarpentryWork.Mend;
        if (!city.DragonFree && city.Prosperity(town) >= CityEconomy.Thrives && f.Grown < CityEconomy.GrowthCap) return CarpentryWork.Grow;
        return null;
    }
}

public sealed partial class CityEconomy
{
    /// <summary>A hero's carpentry finished: what it was, done to the town.</summary>
    bool Carpentered(string town, CarpentryWork work, string? by)
    {
        var f = FabricOf(town);
        switch (work)
        {
            case CarpentryWork.Raise when f.Burnt > 0: f.Burnt--; break;
            case CarpentryWork.Mend when f.Derelict > 0: f.Derelict--; break;
            case CarpentryWork.Grow when f.Grown < GrowthCap: f.Grown++; break;
            default: return false;
        }
        News.Add($"{(by is { Length: > 0 and <= 60 } ? by : "A carpenter")} has {(work switch { CarpentryWork.Raise => "raised a burned home again", CarpentryWork.Mend => "put an empty home right", _ => "built a new house" })} in {TownName(town)}.");
        return true;
    }
}
