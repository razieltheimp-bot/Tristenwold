using System.Text.Json;

namespace Tristinwold.Shared;

public sealed record CityContract(string Id, string Title, string PostedBy, int Pay, string? InDanger = null, string? Secret = null);

/// <summary>One of the world's cities: its name, its own places and its own story.</summary>
public sealed record City(
    string Id,
    string Name,
    string Epithet,
    string Story,
    List<string> Places,
    List<CityContract> Contracts,
    double[]? At = null)
{
    /// <summary>Where the city stands, in metres: x east, z south. Tristinwold is the origin.</summary>
    public (double X, double Z) Position => At is [var x, var z] ? (x, z) : (0, 0);
}

/// <summary>A character written by hand: who they are, the place they call home, and their story.</summary>
public sealed record Character(string Name, string Home, string Story = "");

/// <summary>
/// The world of Tristinwold: world/world.json. The game grows its cities from it; the server reads it for
/// place names and city contracts.
/// </summary>
public sealed record Atlas(List<City> Cities, List<Character> Characters, ulong Seed = 0)
{
    public static Atlas Default { get; } = LoadEmbedded();

    public static Atlas Load(string path) => Parse(File.ReadAllText(path));

    public static Atlas Parse(string json) =>
        JsonSerializer.Deserialize<Atlas>(json, Wire.Options) ?? throw new InvalidDataException("Empty world file.");

    static Atlas LoadEmbedded()
    {
        using var stream = typeof(Atlas).Assembly.GetManifestResourceStream("world.json")
            ?? throw new InvalidOperationException("world.json is not embedded in Tristinwold.Shared.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Every named place in every city.</summary>
    public List<string> AllPlaces() => Cities.SelectMany(c => c.Places).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The contracts each city posts on the shared board, tagged with where they are.</summary>
    public IEnumerable<Contract> CityContracts() =>
        Cities.SelectMany(city => city.Contracts.Select(c => new Contract
        {
            Id = c.Id, Title = c.Title, PostedBy = c.PostedBy, Pay = c.Pay, InDanger = c.InDanger, Secret = c.Secret,
            Where = $"{city.Name}, {city.Epithet}",
        }));
}
