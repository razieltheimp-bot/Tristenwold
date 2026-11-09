namespace Tristinwold.Gameplay;

/// <summary>
/// The Summoner's road with Braz Algún, which any hero of any class can walk. She marks their ring and sends them, one at
/// a time, after the great ones of the road (Quvirn, Blaze, Shicene, and last of all Draxon, Bubbles' son, the King of
/// Beasts), each bound to their ring only in its own fight. The first one bound, shown to Braz, makes them a Summoner.
/// </summary>
public enum SummonerStep
{
    /// <summary>Not on the road.</summary>
    None,
    /// <summary>Braz has marked their ring and sent them after the first of the great ones.</summary>
    Marked,
    /// <summary>Named a Summoner: they can call what they've bound (G).</summary>
    Summoner,
}
