namespace Tristinwold.Gameplay;

public sealed partial class CityEconomy
{
    /// <summary>The heroes with a price on their head in Tristinwold, and how much: the known outlaws, alongside Soren and Lu Wukong.</summary>
    public Dictionary<string, int> Outlaws { get; set; } = [];

    /// <summary>A hero's bounty, as the town has it now: on the list while there's a price on them, off it once it's paid.</summary>
    bool Outlawed(string who, int bounty)
    {
        if (who.Length is 0 or > 60) return false;
        if (bounty <= 0) return Outlaws.Remove(who);
        var named = !Outlaws.ContainsKey(who);
        Outlaws[who] = Math.Min(bounty, 100_000);
        if (named) News.Add($"There's a price on {who}'s head now: {bounty} crowns. The Watch has their name.");
        return true;
    }
}
