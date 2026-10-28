using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// Stateless hashing and value noise. Any point of the world, asked at any time on any
/// machine, gives the same answer, so nothing between the cities has to be stored.
/// </summary>
public static class Noise
{
    public static ulong Hash(ulong seed, long a, long b = 0, long c = 0)
    {
        var h = Mix(seed ^ 0x9E3779B97F4A7C15UL);
        h = Mix(h + (ulong)a * 0xBF58476D1CE4E5B9UL);
        h = Mix(h + (ulong)b * 0x94D049BB133111EBUL);
        return Mix(h + (ulong)c * 0xD6E8FEB86659FD93UL);
    }

    static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A hash as a number in [0, 1).</summary>
    public static double Unit(ulong h) => (h >> 11) * (1.0 / (1UL << 53));

    /// <summary>Dice for one thing in the world: a cell, a person, a quest.</summary>
    public static Rng Dice(ulong seed, long a, long b = 0, long c = 0) => new(Hash(seed, a, b, c));

    /// <summary>Smooth value noise in [-1, 1], one bump per unit.</summary>
    public static double Value(ulong seed, double x, double z)
    {
        var x0 = Math.Floor(x);
        var z0 = Math.Floor(z);
        var (ix, iz) = ((long)x0, (long)z0);
        var u = Fade(x - x0);
        var v = Fade(z - z0);
        var top = Lerp(Corner(seed, ix, iz), Corner(seed, ix + 1, iz), u);
        var bottom = Lerp(Corner(seed, ix, iz + 1), Corner(seed, ix + 1, iz + 1), u);
        return Lerp(top, bottom, v);
    }

    /// <summary>Layers of value noise, each finer and fainter than the last.</summary>
    public static double Fbm(ulong seed, double x, double z, int octaves)
    {
        double sum = 0, amp = 1, norm = 0, freq = 1;
        for (var i = 0; i < octaves; i++)
        {
            sum += amp * Value(seed + (ulong)i * 1013, x * freq + i * 17.3, z * freq - i * 9.1);
            norm += amp;
            amp *= 0.5;
            freq *= 2.03;
        }
        return sum / norm;
    }

    static double Corner(ulong seed, long x, long z) => Unit(Hash(seed, x, z)) * 2 - 1;

    static double Fade(double t) => t * t * (3 - 2 * t);

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
