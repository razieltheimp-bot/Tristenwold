using System.Text.RegularExpressions;

namespace Tristinwold.Deviant;

/// <summary>
/// Brings old saves up to date as they're read: the coin, the third pool and a few creatures and items were renamed
/// in version 0.10, and the enemies who remember you (deviants, once called by another name), their ranks and their
/// grudges and pacts in 0.11. Field names, enum values and item ids are swapped wherever they stand quoted in the
/// JSON (and inside JSON kept as a string in other JSON), so a character keeps their coin and everything they carried.
/// </summary>
public static class SaveUpgrade
{
    static readonly (string Old, string New)[] Renamed =
    [
        ("septims", "crowns"), ("Septims", "Crowns"), ("workSeptims", "workCrowns"),
        ("Magicka", "Mana"), ("magicka", "mana"), ("MaxMagicka", "MaxMana"), ("magicka_tonic", "mana_tonic"),
        ("skeever", "giant_rat"), ("crystal_skeever", "crystal_rat"), ("skeever_tail", "rat_tail"), ("mudcrab", "mud_crab"),
        ("nemesisId", "deviantId"), ("NemesisId", "DeviantId"), ("nemesisWorldJson", "deviantWorldJson"), ("NemesisWorldJson", "DeviantWorldJson"),
        ("nemeses", "deviants"), ("Nemeses", "Deviants"), ("maxNemesesPerBattle", "maxDeviantsPerBattle"),
        ("bloodBrother", "pactSworn"),
    ];

    /// <summary>Ranks and memories as a deviant world saves them: one name, or several ("KilledPlayer, CheatedDeath").</summary>
    static readonly Dictionary<string, string> RenamedValues = new()
    {
        ["Grunt"] = "Stray", ["Captain"] = "Ringleader", ["Warchief"] = "Scourge", ["Overlord"] = "Tyrant",
        ["CheatedDeath"] = "Returned", ["Avenger"] = "Vengeful",
    };

    static readonly Regex Names = new(@"""([A-Za-z]+(?:, [A-Za-z]+)*)""", RegexOptions.Compiled);

    /// <summary>
    /// A saved deviant world brought up to date: everything <see cref="Upgrade"/> does, and the old ranks and memories
    /// renamed wherever a whole value is made of them. Only for deviant worlds, where no name or story is ever just
    /// "Captain".
    /// </summary>
    public static string UpgradeDeviants(string json) =>
        Names.Replace(Upgrade(json), m =>
        {
            var words = m.Groups[1].Value.Split(", ");
            return words.Any(RenamedValues.ContainsKey)
                ? $"\"{string.Join(", ", words.Select(w => RenamedValues.GetValueOrDefault(w, w)))}\""
                : m.Value;
        });

    public static string Upgrade(string json)
    {
        foreach (var (old, @new) in Renamed)
        {
            if (!json.Contains(old, StringComparison.Ordinal)) continue;
            json = json.Replace($"\"{old}\"", $"\"{@new}\"").Replace($"\\\"{old}\\\"", $"\\\"{@new}\\\"");
        }
        return json;
    }
}
