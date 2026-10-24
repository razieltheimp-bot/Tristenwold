namespace Tristinwold.Nemesis;

/// <summary>
/// SplitMix64. Its whole state is one number that is saved with the world, so a
/// host and its clients that replay the same reports get the same nemeses.
/// </summary>
public sealed class Rng(ulong state)
{
    public ulong State { get; private set; } = state;

    public ulong NextULong()
    {
        var z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public int Next(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(NextULong() % (ulong)maxExclusive);

    /// <summary>Ids come from the dice too, so a seed always produces the same world.</summary>
    public Guid NextGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, NextULong());
        BitConverter.TryWriteBytes(bytes[8..], NextULong());
        return new Guid(bytes);
    }

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    public T PickWeighted<T>(IReadOnlyList<(T Item, double Weight)> items)
    {
        var total = items.Sum(i => i.Weight);
        var roll = NextDouble() * total;
        foreach (var (item, weight) in items)
        {
            if ((roll -= weight) < 0) return item;
        }
        return items[^1].Item;
    }
}
