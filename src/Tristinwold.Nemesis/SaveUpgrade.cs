namespace Tristinwold.Nemesis;

/// <summary>
/// Brings saves from before version 0.10 up to date as they're read: the coin, the third pool and a few creatures
/// and items were renamed then. Field names, enum values and item ids are swapped wherever they stand quoted in the
/// JSON (and inside JSON kept as a string in other JSON), so a character keeps their coin and everything they carried.
/// </summary>
public static class SaveUpgrade
{
    static readonly (string Old, string New)[] Renamed =
    [
        ("septims", "crowns"), ("Septims", "Crowns"), ("workSeptims", "workCrowns"),
        ("Magicka", "Mana"), ("magicka", "mana"), ("MaxMagicka", "MaxMana"), ("magicka_tonic", "mana_tonic"),
        ("skeever", "giant_rat"), ("crystal_skeever", "crystal_rat"), ("skeever_tail", "rat_tail"), ("mudcrab", "mud_crab"),
    ];

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
