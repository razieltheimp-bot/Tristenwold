namespace Tristinwold.Gameplay;

public enum Season { Spring, Summer, Fall, Winter }

public enum Weather { Clear, Cloudy, Rain, Storm, Snow, Fog }

/// <summary>
/// The year: four seasons of seven days each, and the weather, turning over three times a day. Both come from the
/// city's day alone, so everyone on a shared world looks up at the same sky.
/// </summary>
public static class Seasons
{
    public const int DaysPerSeason = 7, YearDays = DaysPerSeason * 4, SpellsADay = 3;

    public static Season Of(int day) => (Season)(Math.Max(0, day - 1) / DaysPerSeason % 4);

    public static string Name(Season s) => s.ToString();

    /// <summary>What the sky's doing in a spell of a day (0 to 2): the season sets the odds, and the cold turns rain to snow.</summary>
    public static Weather WeatherOn(int day, int spell, bool cold = false)
    {
        var s = Of(day);
        (Weather Sky, double Chance)[] odds = s switch
        {
            Season.Spring => [(Weather.Clear, 0.35), (Weather.Cloudy, 0.3), (Weather.Rain, 0.25), (Weather.Storm, 0.05), (Weather.Fog, 0.05)],
            Season.Summer => [(Weather.Clear, 0.6), (Weather.Cloudy, 0.2), (Weather.Rain, 0.08), (Weather.Storm, 0.1), (Weather.Fog, 0.02)],
            Season.Fall => [(Weather.Clear, 0.3), (Weather.Cloudy, 0.3), (Weather.Rain, 0.2), (Weather.Storm, 0.05), (Weather.Fog, 0.15)],
            _ => [(Weather.Clear, 0.3), (Weather.Cloudy, 0.3), (Weather.Snow, 0.32), (Weather.Fog, 0.08)],
        };
        // The same day and spell always roll the same, on every machine.
        var roll = (unchecked((uint)(day * 2654435761u) ^ (uint)(spell * 40503u + 12345u)) * 2654435761u >> 8) % 10000 / 10000.0;
        foreach (var (sky, chance) in odds)
        {
            if (roll < chance) return cold && sky is Weather.Rain or Weather.Storm ? Weather.Snow : sky;
            roll -= chance;
        }
        return Weather.Clear;
    }

    /// <summary>How white the land lies: nothing out of winter, deepening through its first days and thinning at the end.</summary>
    public static double SnowCover(int day)
    {
        if (Of(day) != Season.Winter) return 0;
        var into = (Math.Max(0, day - 1) % DaysPerSeason + 1) / (double)DaysPerSeason;
        return Math.Min(1, Math.Min(into * 2.5, (1.15 - into) * 3.5)) * 0.85;
    }

    /// <summary>What the season does to the green of things, as a colour to multiply it by.</summary>
    public static (double R, double G, double B) Tint(Season s) => s switch
    {
        Season.Spring => (0.92, 1.08, 0.88),
        Season.Summer => (1.0, 1.0, 0.96),
        Season.Fall => (1.14, 0.94, 0.72),
        _ => (0.9, 0.92, 0.98),
    };
}
