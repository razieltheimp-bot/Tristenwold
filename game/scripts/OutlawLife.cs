using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The known outlaws: the Dread Pirate Soren, Lu Wukong, Black Ysolde, and every hero with a price on their head in
/// Tristinwold. Anyone in town can be asked who's out there, and a hero's own name is on that list for as long as
/// their bounty stands.
/// </summary>
public partial class Main
{
    /// <summary>This hero's bounty, put on the shared list (or taken off it) when it changes.</summary>
    void SyncOutlaw()
    {
        var bounty = _standing.Bounty;
        if (_city.Outlaws.GetValueOrDefault(_player.Company) != bounty) DoCity(CityAction.Outlaw(_player.Company, bounty));
    }

    IEnumerable<(string, Action)> OutlawTalk(Person person)
    {
        if (People.IsYouth(person.Npc) || person.Npc.Id.StartsWith("hall:") || person.Npc.Id.StartsWith("store:")) yield break;
        yield return ("Any outlaws I should know about?", () => { SyncOutlaw(); Say(person, KnownOutlaws()); });
    }

    string KnownOutlaws()
    {
        var c = _city;
        var lines = new List<string>
        {
            $"The Dread Pirate Soren, first and worst of them{(c.SorenDeaths > 0 ? $": killed {c.SorenDeaths} time{(c.SorenDeaths == 1 ? "" : "s")}, and back every time" : "")}.",
            $"Lu Wukong, second only to Soren on the water{(c.WukongDeaths > 0 ? $", and no deader for being killed {c.WukongDeaths} time{(c.WukongDeaths == 1 ? "" : "s")}" : "")}.",
            "Black Ysolde of the Widow's Tooth, who takes the company's ships off the Teeth.",
        };
        foreach (var (who, bounty) in c.Outlaws.OrderByDescending(o => o.Value).Take(8))
            lines.Add(who == _player.Company ? $"And you, {bounty} crowns on your head. Don't think I don't know your face." : $"{who}: {bounty} crowns on their head, and the Watch would like a word.");
        return "Known outlaws? " + string.Join(" ", lines);
    }

    /// <summary>For the self-test.</summary>
    public string KnownOutlawsForTest() { SyncOutlaw(); return KnownOutlaws(); }
}
