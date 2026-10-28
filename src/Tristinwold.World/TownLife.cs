using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>An initiate of one of the four guilds, about the town on the errand every initiate is set.</summary>
public sealed record Initiate(string Name, Calling Calling, string Look, string Errand)
{
    public string Guild => Guilds.Of(Calling).Name;
    /// <summary>"Wren Ashby, initiate of the Velvet Knives".</summary>
    public string Title => $"{Name}, initiate of {Guild}";
}

/// <summary>
/// Tristinwold isn't still. The guilds send their initiates out into the town on the errand every one of them is set
/// before they're let in properly: the Velvet Knives' young thieves must lift purses in the streets without the Watch
/// laying a hand on them (and the hero's purse is as good as anyone's); the Iron Circle's go bouts with one another in
/// front of the town; the Magi's apprentices work their cantrips at the wells; the College's busk for their supper.
/// </summary>
public static class Initiates
{
    public static string ErrandOf(Calling c) => c switch
    {
        Calling.Rogue => "lift three purses in the streets without the Watch laying a hand on them",
        Calling.Warrior => "go three bouts with another initiate where the whole town can see",
        Calling.Mage => "work the seven cantrips, one at each of the town's wells",
        _ => "earn ten crowns busking before the lamps are lit",
    };

    public static Initiate Make(Calling c, Rng rng)
    {
        var sex = rng.Chance(0.5) ? Sex.Male : Sex.Female;
        var look = rng.Pick(Classes.Of(c).Select(d => d.Look).ToList());
        return new Initiate(Names.Person(rng, sex), c, look, ErrandOf(c));
    }

    /// <summary>
    /// The chance a young thief gets the hero's purse without being caught at it: less the better a thief the hero is
    /// themselves, and much less for a rogue, who knows the trick.
    /// </summary>
    public static double LiftChance(int thieving, bool rogue) => Math.Clamp(0.55 - 0.05 * (thieving - 1) - (rogue ? 0.2 : 0), 0.1, 0.55);

    /// <summary>What's in the purse they lift: a handful of crowns, never more than the hero has.</summary>
    public static int Purse(int crowns, Rng rng) => Math.Min(crowns, 5 + rng.Next(16));

    /// <summary>How often one of the townsfolk feels it and shouts "Thief!".</summary>
    public const double Noticed = 0.3;

    /// <summary>What the Watch pays for a thief or a cutthroat brought down in the streets.</summary>
    public static int Reward(string kind) => kind == "velvet_assassin" ? 25 : kind == "young_thief" ? 5 : 10;
}

/// <summary>
/// Crime in the streets, for the Watch to deal with: a cutthroat sent by the Velvet Knives for a hero who's made a
/// name, now and then, and muggers on the docks and in the Warrens.
/// </summary>
public static class Crime
{
    /// <summary>How often the dice are rolled while the hero is in town (seconds of play).</summary>
    public const double CheckSeconds = 600;

    /// <summary>The chance, each roll, that a cutthroat comes for the hero: more the more of a name they've made.</summary>
    public static double AssassinChance(int heroLevel) => Math.Min(0.35, 0.05 + 0.02 * heroLevel);

    /// <summary>The chance, each roll, that muggers try the hero: the docks and the Warrens are rough, the rest less so.</summary>
    public static double MuggingChance(string area) => area is "The Docks" or "The Warrens" ? 0.4 : 0.05;
}
