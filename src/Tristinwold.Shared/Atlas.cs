using System.Text.Json;

namespace Tristinwold.Shared;

public sealed record CityContract(string Id, string Title, string PostedBy, int Pay, string? InDanger = null, string? Secret = null);

/// <summary>One of Skyrim's cities, reworked: a new name, its own places and its own story.</summary>
public sealed record City(
    string Id,
    string Name,
    string BuiltFrom,
    string Epithet,
    string Story,
    Dictionary<string, string> Places,
    List<CityContract> Contracts);

/// <summary>A character taken from one city and set down in another, perhaps under a new name.</summary>
public sealed record NpcMove(string Npc, string To, string? Rename = null, string? Why = null);

/// <summary>
/// The world of Tristinwold built from Skyrim: world/world.json. The server reads it for place
/// names and city contracts; the plugin builder reads it to rename places and move characters.
/// </summary>
public sealed record Atlas(List<City> Cities, List<NpcMove> Moves)
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

    /// <summary>Every Skyrim place name -> its name in this world, across all cities.</summary>
    public Dictionary<string, string> AllPlaces()
    {
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var city in Cities)
            foreach (var (skyrim, name) in city.Places) all[skyrim] = name;
        return all;
    }

    /// <summary>The contracts each city posts on the shared board, tagged with where they are.</summary>
    public IEnumerable<Contract> CityContracts() =>
        Cities.SelectMany(city => city.Contracts.Select(c => new Contract
        {
            Id = c.Id, Title = c.Title, PostedBy = c.PostedBy, Pay = c.Pay, InDanger = c.InDanger, Secret = c.Secret,
            Where = $"{city.Name}, {city.Epithet}",
        }));
}
