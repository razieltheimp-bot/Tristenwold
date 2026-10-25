namespace Tristinwold.World;

/// <summary>
/// A raid: a fixed place where something terrible waits, which a company can fight through again and again. The
/// dead rise first; then the boss. Beat it and the place falls quiet for a while, then rekindles.
/// </summary>
/// <param name="MaxLevel">Made for heroes up to this level: the boss grows to match them that far and no further, and
/// those far past it find little worth taking.</param>
public sealed record Raid(string Id, string Name, string BossName, string BossTitle, string Story, string Taunt, double X, double Z,
    int MaxLevel, double CooldownSeconds, int Thralls)
{
    public string Boss => $"{BossName}, {BossTitle}";
    public double DistanceTo(double x, double z) => Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
}

public static class Raids
{
    /// <summary>
    /// The Ashen Crypt, west of Tristinwold past the Iron Circle's yard. A warden of the Old Kings was burned alive
    /// there defending the royal dead; he never stopped defending them. His body is charred black and grown with
    /// spikes, his hands are claws, and where his head should be a skull burns.
    /// </summary>
    public static readonly Raid AshenCrypt = new("raid:ashen_crypt", "the Ashen Crypt", "Varrow", "the Cinder Warden",
        "Before Tristinwold had walls, a warden of the Old Kings was burned alive defending their crypt from looters. He never stopped defending it. Every so often the ash stirs, his thralls crawl out of the ground, and the Cinder Warden walks again.",
        "None... shall... take... from the KINGS.",
        -300, -80, MaxLevel: 6, CooldownSeconds: 20 * 60, Thralls: 4);

    public static IReadOnlyList<Raid> All { get; } = [AshenCrypt];

    public static Raid? Get(string id) => All.FirstOrDefault(r => r.Id == id);

    /// <summary>How strong the boss stands against a hero: their level, but never past what the raid was made for.</summary>
    public static int Strength(Raid r, int heroLevel) => Math.Clamp(heroLevel, 1, r.MaxLevel);

    /// <summary>The boss's health and the harm it does, for a hero of a level.</summary>
    public static (double Health, double Damage) Boss(Raid r, int heroLevel)
    {
        var s = Strength(r, heroLevel);
        return (320 + 70 * s, 12 + 2.5 * s);
    }

    /// <summary>A thrall's health and harm.</summary>
    public static (double Health, double Damage) Thrall(Raid r, int heroLevel)
    {
        var s = Strength(r, heroLevel);
        return (35 + 8 * s, 5 + s);
    }

    /// <summary>
    /// What the hoard holds: experience and septims, double the first time. A hero far past the raid's level finds
    /// it hardly worth the walk.
    /// </summary>
    public static (int Xp, int Septims) Reward(Raid r, int heroLevel, bool firstTime)
    {
        var s = Strength(r, heroLevel);
        var (xp, septims) = (150 + 45 * s, 40 + 12 * s);
        if (heroLevel > r.MaxLevel + 2) (xp, septims) = (xp / 4, septims / 2);
        return firstTime ? (xp * 2, septims * 2) : (xp, septims);
    }

    /// <summary>Too strong for it: the raid is for low-level heroes.</summary>
    public static bool Outgrown(Raid r, int heroLevel) => heroLevel > r.MaxLevel + 2;
}

/// <summary>When each raid can be fought again, and how often each has been beaten. Saved with the company.</summary>
public sealed class RaidBook
{
    public Dictionary<string, double> ReadyAt { get; set; } = [];
    public Dictionary<string, int> Clears { get; set; } = [];

    public bool Ready(Raid r, double playSeconds) => playSeconds >= ReadyAt.GetValueOrDefault(r.Id);

    public double SecondsLeft(Raid r, double playSeconds) => Math.Max(0, ReadyAt.GetValueOrDefault(r.Id) - playSeconds);

    public int ClearsOf(Raid r) => Clears.GetValueOrDefault(r.Id);

    /// <summary>Beaten: the place falls quiet, and rekindles after a while.</summary>
    public void Cleared(Raid r, double playSeconds)
    {
        Clears[r.Id] = ClearsOf(r) + 1;
        ReadyAt[r.Id] = playSeconds + r.CooldownSeconds;
    }
}
