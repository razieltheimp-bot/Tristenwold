using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>Where a hero stands on Brasswake: never been, let aboard, trusted with the work, or one of its own.</summary>
public enum IsleRank { Stranger, Guest, Hand, Citizen }

/// <summary>Where a hero stands in the air crews: not signed on, then up from the rail to their own deck.</summary>
public enum AirRank { None, Hand, Gunner, Helmsman, Skipper }

/// <summary>What comes at the island out of the sky: a word for it, what's said when it's seen, what's in it (what, how many, how big), the pay for seeing it off, and who's trusted to go up against it.</summary>
public sealed record SkyRaid(string Id, string Hail, IReadOnlyList<(string Kind, int N, DragonSize Size)> Foes, int Pay, AirRank From, bool Riders = false);

/// <summary>
/// Brasswake: one great city on one small island, and the island is a ship. The artificers who left the mainland two
/// hundred years ago with everything they'd invented bolted a hull to a rock and went to sea under steam, and they've
/// never come back. It never stops: it keeps to the open water, on a long slow course of its own, so how far out it is
/// depends on the day. Everyone knows of it, and any ship that comes alongside may tie up at the Strangers' Dock, which is
/// always out, and trade. Dragons and those who ride them get the guns. What it makes for itself, it keeps: nothing
/// that's made for a hero on the island leaves it, though a few of its designs have found their way to Tristinwold.
/// </summary>
public static class Brasswake
{
    public const string Name = "Brasswake", Port = "isle:brasswake";
    /// <summary>How much of the open sea it wanders, how far a league runs out there, and the sailing it can be.</summary>
    const double Wide = 5200, Deep = 3800, LeagueIs = 2200;
    public const int MostLeagues = 3;

    /// <summary>The island's market, for a trade route: what a rock at sea can't make, against what its foundries turn out.</summary>
    public static readonly SeaPort Market = new("port:brasswake", Name, 2, 0.15, [Good.Iron, Good.Precious], [Good.Timber, Good.Ale, Good.Herbs],
        "a mountain with a hull on it and a dock always out, somewhere different every day");

    public static (double X, double Z) Where(double playSeconds) =>
        (900 + Wide * Math.Sin(playSeconds / 5100 + 0.7), -2600 + Deep * Math.Sin(playSeconds / 3300 + 2.1) + 900 * Math.Sin(playSeconds / 870));

    /// <summary>The way it's headed at a moment, as a compass heading in radians.</summary>
    public static double Heading(double playSeconds)
    {
        var (a, b) = (Where(playSeconds), Where(playSeconds + 30));
        return Math.Atan2(b.X - a.X, b.Z - a.Z);
    }

    /// <summary>How many days' sailing it is from Tristinwold at a moment.</summary>
    public static int Leagues(double playSeconds)
    {
        var (x, z) = Where(playSeconds);
        return Math.Clamp((int)Math.Ceiling(Math.Sqrt(x * x + z * z) / LeagueIs), 1, MostLeagues);
    }

    /// <summary>How long between things coming at her out of the sky, in seconds of play.</summary>
    public const double RaidEvery = 480, FirstRaid = 150;

    /// <summary>What comes for her: a town that won't be found is still a lot of warm metal, and dragons can smell that.</summary>
    public static readonly IReadOnlyList<SkyRaid> Raids =
    [
        new("stormwings", "Horns on every tier. There's a squall coming up against the wind, and it has wings in it: stormwings, a whole flock", [("stormwing", 9, DragonSize.Normal)], 8, AirRank.Hand),
        new("young", "Horns on every tier. Three shapes coming in low out of the sun: young red dragons, after the smell of the furnaces", [("dragon_red", 3, DragonSize.Normal)], 14, AirRank.Hand),
        new("flight", "Horns on every tier. A flight of blue dragons has found the steam, with a big one at its head", [("dragon_blue", 3, DragonSize.Normal), ("dragon_blue", 1, DragonSize.Large)], 22, AirRank.Gunner),
        new("riders", "Horns on every tier, and every gun on the island swings round at once. Riders of the Eyrie, on gold and silver, come to see what the artificers won't show them. Nobody who rides a dragon comes aboard Brasswake", [("dragon_gold", 1, DragonSize.Normal), ("dragon_silver", 3, DragonSize.Normal)], 26, AirRank.Gunner, Riders: true),
        new("giant", "Every horn on the island, and then the big one at the Gearworks, that nobody's heard in years. A black dragon the size of a street is coming over the bank", [("dragon_black", 1, DragonSize.Giant), ("dragon_black", 2, DragonSize.Normal)], 34, AirRank.Helmsman),
        new("rift", "The horns stop. Something's turned the steam violet. A dragon the Rift has taken is coming straight for the boilers, and its flight with it", [("dragon_white_corrupt", 1, DragonSize.Large), ("dragon_green_corrupt", 2, DragonSize.Normal)], 40, AirRank.Skipper),
    ];

    /// <summary>What's coming: nothing worse than the crew's been trusted with, and more often the worst of that.</summary>
    public static SkyRaid RaidFor(AirRank rank, Rng rng)
    {
        var fit = Raids.Where(r => r.From <= (rank == AirRank.None ? AirRank.Hand : rank)).ToList();
        return rng.Chance(0.5) ? fit[^1] : rng.Pick(fit);
    }

    /// <summary>What they make, and what it costs in their own marks. None of it leaves the island.</summary>
    public static readonly IReadOnlyList<(string Id, int Marks)> Wares =
    [
        ("steamweave_coat", 18),
        ("spring_buckler", 24),
        ("aether_gauntlet", 40),
        ("orrery_ring", 60),
    ];

    public static bool Made(string id) => Wares.Any(w => w.Id == id);
}

/// <summary>What a hero knows of Brasswake and what they are there, kept with their sea book.</summary>
public sealed class IsleBook
{
    public const int ToHand = 30, ToCitizen = 120, VentPay = 2, ShiftPay = 5, Licence = 5, Valves = 6;
    public const int PassagePrice = 60;
    public const int ToGunner = 2, ToHelmsman = 5, HelmsmanKills = 4, ToSkipper = 10, SkipperKills = 12, KillPay = 3;

    /// <summary>Their place in the air crews, how often they've gone up and come back, and what they've brought down.</summary>
    public AirRank Air { get; set; }
    public int Sorties { get; set; }
    public int AirKills { get; set; }
    /// <summary>When the sky next has something in it: seconds of play.</summary>
    public double NextRaidAt { get; set; }

    public IsleRank Rank { get; set; }
    public int Visits { get; set; }
    /// <summary>On the island right now.</summary>
    public bool Aboard { get; set; }
    /// <summary>The ship the hero came on is tied up at the Strangers' Dock.</summary>
    public bool Berthed { get; set; }
    /// <summary>The island's own money: crowns are no good here, and marks are no good anywhere else.</summary>
    public int Marks { get; set; }
    /// <summary>All the marks ever earned: it's the work that makes a citizen, not the spending.</summary>
    public int Earned { get; set; }
    public int Shifts { get; set; }
    /// <summary>May take a sky-skiff up alone.</summary>
    public bool Pilot { get; set; }
    /// <summary>What they've made for the hero, left at the gangway when the hero goes: it doesn't leave the island.</summary>
    public List<string> Locker { get; set; } = [];

    /// <summary>Let aboard. The first time makes a guest of a stranger.</summary>
    public void Board()
    {
        if (Rank == IsleRank.Stranger) Rank = IsleRank.Guest;
        Aboard = true;
        Visits++;
    }

    /// <summary>A valve eased, and a shift's bonus when the last of them is: the pay, and the rank it's made them, if any.</summary>
    public (int Pay, IsleRank? Made) Vent(bool lastOfShift)
    {
        var pay = VentPay + (lastOfShift ? ShiftPay : 0);
        if (lastOfShift) Shifts++;
        return (pay, Earn(pay));
    }

    /// <summary>Signed on with the air crews, at the rail. Anyone let aboard can ask; it's only dangerous.</summary>
    public bool Enlist()
    {
        if (Rank == IsleRank.Stranger || Air != AirRank.None) return false;
        Air = AirRank.Hand;
        return true;
    }

    /// <summary>Something brought down by the hero's own gun: a bounty in marks, and it counts for the crew.</summary>
    public int Downed()
    {
        AirKills++;
        return KillPay;
    }

    /// <summary>
    /// Back on the dock. If the sky's clear it pays, better for a skipper, and counts toward the next rank; if the ship
    /// was lost or the hero came off her early, there's a little for the trouble and it doesn't count.
    /// </summary>
    public (int Pay, AirRank? Air, IsleRank? Isle) Sortie(SkyRaid raid, bool won)
    {
        var pay = won ? (int)Math.Round(raid.Pay * (Air == AirRank.Skipper ? 1.5 : 1)) : raid.Pay / 4;
        var isle = Earn(pay);
        if (!won) return (pay, null, isle);
        Sorties++;
        AirRank? next = Air switch
        {
            AirRank.Hand when Sorties >= ToGunner => AirRank.Gunner,
            AirRank.Gunner when Sorties >= ToHelmsman && AirKills >= HelmsmanKills => AirRank.Helmsman,
            AirRank.Helmsman when Sorties >= ToSkipper && AirKills >= SkipperKills => AirRank.Skipper,
            _ => null,
        };
        if (next is { } r) Air = r;
        return (pay, next, isle);
    }

    /// <summary>How hard the hero's shots land: they get better at it.</summary>
    public double GunPower => Air switch { AirRank.Gunner => 1.3, AirRank.Helmsman => 1.5, AirRank.Skipper => 1.8, _ => 1 };

    /// <summary>A helmsman takes the wheel on patrol, and doesn't just man a gun.</summary>
    public bool TakesTheWheel => Air >= AirRank.Helmsman;

    public string NextAirStep => Air switch
    {
        AirRank.None => "Sign on with the Skymaster.",
        AirRank.Hand => $"{ToGunner} patrols home and you're a gunner ({Sorties} so far).",
        AirRank.Gunner => $"Helmsman wants {ToHelmsman} patrols ({Sorties}) and {HelmsmanKills} brought down ({AirKills}).",
        AirRank.Helmsman => $"A deck of your own wants {ToSkipper} patrols ({Sorties}) and {SkipperKills} brought down ({AirKills}).",
        _ => "You're a skipper. The sky's yours to keep clear.",
    };

    public static string Title(AirRank r) => r switch
    {
        AirRank.Hand => "hand of the air", AirRank.Gunner => "sky gunner", AirRank.Helmsman => "sky helmsman", AirRank.Skipper => "sky skipper", _ => "",
    };

    /// <summary>Marks earned, by any honest work: the rank it's made them, if any.</summary>
    IsleRank? Earn(int pay)
    {
        Marks += pay;
        Earned += pay;
        IsleRank? next = Rank switch
        {
            IsleRank.Guest when Earned >= ToHand => IsleRank.Hand,
            IsleRank.Hand when Earned >= ToCitizen => IsleRank.Citizen,
            _ => null,
        };
        if (next is { } r) Rank = r;
        return next;
    }

    /// <summary>A citizen can light the brass beacon and have a skiff sent, wherever the island's got to.</summary>
    public bool MaySummon => Rank == IsleRank.Citizen;

    public bool LearnToFly()
    {
        if (Pilot || Marks < Licence) return false;
        Marks -= Licence;
        return Pilot = true;
    }

    /// <summary>Bought with marks: it goes straight into the hero's hands, for as long as they're aboard.</summary>
    public bool Buy(string id)
    {
        var (found, price) = Brasswake.Wares.FirstOrDefault(w => w.Id == id);
        if (found is null || Marks < price) return false;
        Marks -= price;
        return true;
    }

    public static string Title(IsleRank r) => r switch
    {
        IsleRank.Guest => "guest", IsleRank.Hand => "hand of the engines", IsleRank.Citizen => "citizen of Brasswake", _ => "stranger",
    };

    public string NextStep => Rank switch
    {
        IsleRank.Guest => $"{ToHand} marks earned at the valves and you're a hand of the engines ({Earned} so far).",
        IsleRank.Hand => $"{ToCitizen} marks earned and the Archwright will have you as a citizen ({Earned} so far).",
        _ => "There's nothing higher. There's only keeping her moving.",
    };
}
