namespace Tristinwold.Gameplay;

/// <summary>
/// Who can climb walls and cliffs: a rogue of any kind and a martial artist, by their training; the Apekin and the
/// Scalkin, born to it; a druid in a shape that climbs (a bear, a rat, a fox, an ape); an ether-aeronaut inside their
/// golem, which hauls itself up anything; a saboteur with enough clockwork mites out to swarm up ahead and pull them
/// after (their golem alone won't); and anyone at all with climbing gear in their pack.
/// </summary>
public static class Climbing
{
    public const string Gear = "climbing_gear";
    /// <summary>How many mites a saboteur needs out with them to be carried up a wall.</summary>
    public const int MitesToClimb = 3;
    /// <summary>The druid's shapes that can climb.</summary>
    public static readonly IReadOnlySet<string> Shapes = new HashSet<string> { "bear", "giant_rat", "fox", "deathcrest_ape" };
    /// <summary>The peoples who climb by nature.</summary>
    public static readonly IReadOnlySet<string> Peoples = new HashSet<string> { "ape", "lizard" };

    /// <summary>
    /// What lets this hero climb, said plainly, or null if nothing does.
    /// </summary>
    /// <param name="classes">Every class they've learned.</param>
    /// <param name="race">Their people's id.</param>
    /// <param name="shape">The beast shape a druid's in (or a half-form's), if any.</param>
    /// <param name="inGolem">An aeronaut inside their golem.</param>
    /// <param name="mites">How many clockwork mites are out with them.</param>
    /// <param name="gear">Climbing gear in their pack.</param>
    public static string? Why(IEnumerable<string> classes, string race, string? shape, bool inGolem, int mites, bool gear)
    {
        var known = classes.Select(Classes.Get).ToList();
        if (shape is not null) return Shapes.Contains(shape) ? "your beast's claws" : gear ? "your climbing gear" : null; // a wolf or a boar can't, whatever its rider could
        if (inGolem) return "your golem's grip";
        if (known.Any(c => c.Calling == Calling.Rogue)) return "your rogue's training";
        if (known.Any(c => c.Id == "MartialArtist")) return "your training";
        if (Peoples.Contains(race)) return race == "ape" ? "your long arms" : "your claws";
        if (known.Any(c => c.Id == "Saboteur") && mites >= MitesToClimb) return "your mites, swarming up ahead";
        return gear ? "your climbing gear" : null;
    }

    /// <summary>How fast a climber goes up a wall, in metres a second, and the stamina it takes each second they're moving.</summary>
    public const float Speed = 3.2f;
    public const double StaminaPerSecond = 5;
}
