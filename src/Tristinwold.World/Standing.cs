using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>A career in the Tristinwold Watch, from the recruit walking a beat to the captain of it.</summary>
public enum WatchRank { None, Recruit, Watchman, Sergeant, Captain }

/// <summary>A place in a barbarian clan: a spear in the line, a raider, the clan's champion, its warchief.</summary>
public enum ClanRank { None, Spearman, Raider, Champion, Warchief }

/// <summary>How the world thinks of a hero: their honour set against their infamy.</summary>
public enum Repute { Villain, Rough, Unknown, Decent, Trusted, Hero }

/// <summary>A beat being walked for the Watch: the posts to visit, in order, and what happened on the way.</summary>
public sealed class Patrol
{
    /// <summary>The beat's posts, by name (see <see cref="WatchBook.Beat"/>), in the order to walk them.</summary>
    public List<string> Posts { get; set; } = [];
    public int Next { get; set; }
    /// <summary>The post where trouble waits (a footpad, a cutpurse), or -1 for a quiet beat.</summary>
    public int TroubleAt { get; set; } = -1;
    public bool TroubleMet { get; set; }
    public int Arrests { get; set; }

    public bool Walked => Next >= Posts.Count;
    public string? NextPost => Walked ? null : Posts[Next];
    /// <summary>Trouble comes at this post, and hasn't yet.</summary>
    public bool TroubleHere => !TroubleMet && TroubleAt == Next;
}

/// <summary>
/// The hero's service in the Tristinwold Watch, saved with the company. A recruit walks beats from post to post;
/// trouble on the way is theirs to collar. Beats walked make a watchman, beats and arrests a sergeant, and enough of
/// both, with the years for it, the captain of the Watch. The Watch doesn't take outlaws, villains or clansmen, and
/// strikes off anyone who gets a price on their head.
/// </summary>
public sealed class WatchBook
{
    public WatchRank Rank { get; set; }
    public int Patrols { get; set; }
    public int Arrests { get; set; }
    public int Earned { get; set; }
    public int Dismissals { get; set; }
    public Patrol? Patrol { get; set; }

    public const int ToWatchman = 3, ToSergeant = 8, SergeantArrests = 4, ToCaptain = 15, CaptainArrests = 10, CaptainLevel = 8;
    /// <summary>A beat's pay: so much for walking it, more by rank, and a reward for every arrest.</summary>
    public const int BeatPay = 12, RankPay = 8, ArrestPay = 12;
    public const double TroubleChance = 0.7;

    /// <summary>The Watch's posts about the city, less the Watch House itself, where every beat begins and ends.</summary>
    public static readonly string[] Beat = ["the east gate", "the west gate", "the Exchange", "the Salt Barrel", "the Warrens", "the docks", "the caravan yard"];
    public const string House = "the Watch House";

    public bool In => Rank != WatchRank.None;

    public bool Join()
    {
        if (In) return false;
        Rank = WatchRank.Recruit;
        return true;
    }

    /// <summary>How many posts a beat takes in: four for a recruit, five from watchman up.</summary>
    public int BeatLength => Rank >= WatchRank.Watchman ? 5 : 4;

    /// <summary>A beat to walk: distinct posts in a random order, with trouble waiting at one of them more often than not.</summary>
    public Patrol? StartPatrol(Rng rng)
    {
        if (!In || Patrol is not null) return null;
        var posts = Beat.ToList();
        var walk = new List<string>();
        for (var i = 0; i < BeatLength && posts.Count > 0; i++) { var p = rng.Pick(posts); posts.Remove(p); walk.Add(p); }
        Patrol = new Patrol { Posts = walk, TroubleAt = rng.Chance(TroubleChance) ? rng.Next(walk.Count) : -1 };
        return Patrol;
    }

    /// <summary>The hero got to a post: true if it was the next on the beat.</summary>
    public bool Reached(string post)
    {
        if (Patrol is null || Patrol.NextPost != post) return false;
        Patrol.Next++;
        return true;
    }

    /// <summary>Trouble on the beat, collared.</summary>
    public void Arrest()
    {
        Arrests++;
        if (Patrol is { } p) { p.Arrests++; p.TroubleMet = true; }
    }

    /// <summary>Back at the Watch House with the beat walked: the pay, and a promotion if one's due.</summary>
    public (int Pay, WatchRank? Promoted) Report(int heroLevel)
    {
        if (Patrol is not { Walked: true } p) return (0, null);
        var pay = BeatPay + RankPay * ((int)Rank - 1) + ArrestPay * p.Arrests;
        Patrols++;
        Earned += pay;
        Patrol = null;
        return (pay, Promote(heroLevel));
    }

    WatchRank? Promote(int level)
    {
        var next = Rank switch
        {
            WatchRank.Recruit when Patrols >= ToWatchman => WatchRank.Watchman,
            WatchRank.Watchman when Patrols >= ToSergeant && Arrests >= SergeantArrests => WatchRank.Sergeant,
            WatchRank.Sergeant when Patrols >= ToCaptain && Arrests >= CaptainArrests && level >= CaptainLevel => WatchRank.Captain,
            _ => Rank,
        };
        if (next == Rank) return null;
        Rank = next;
        return next;
    }

    /// <summary>What it takes to rise next.</summary>
    public string NextStep(int level) => Rank switch
    {
        WatchRank.None => "Join, and walk a beat.",
        WatchRank.Recruit => $"{ToWatchman} beats walked makes a watchman ({Patrols} so far).",
        WatchRank.Watchman => $"{ToSergeant} beats and {SergeantArrests} arrests make a sergeant ({Patrols} beats, {Arrests} arrests so far).",
        WatchRank.Sergeant => $"{ToCaptain} beats, {CaptainArrests} arrests and the years for it (level {CaptainLevel}) make the captain ({Patrols} beats, {Arrests} arrests, level {level}).",
        _ => "There's nothing above the captain but the magistrates, and nobody wants that.",
    };

    /// <summary>Struck off the rolls: a price on the head, or a clan's oath.</summary>
    public void Dismiss()
    {
        if (!In) return;
        Rank = WatchRank.None;
        Patrol = null;
        Dismissals++;
    }

    public static string Title(WatchRank rank) => rank switch
    {
        WatchRank.Recruit => "a recruit of the Watch",
        WatchRank.Watchman => "a watchman of Tristinwold",
        WatchRank.Sergeant => "a sergeant of the Watch",
        WatchRank.Captain => "Captain of the Tristinwold Watch",
        _ => "no one the Watch knows",
    };
}

/// <summary>
/// The hero's place in a barbarian clan, saved with the company. A clan offers a place to whoever beats one of its
/// raiders at its camp; a clansman rides on its raids and takes a share of the plunder, and rises from a spear in the
/// line to raider, champion, and at last, with the years for it, warchief of the clan.
/// </summary>
public sealed class ClanBook
{
    public string? ClanId { get; set; }
    public string? ClanName { get; set; }
    public string? Chief { get; set; }
    public ClanRank Rank { get; set; }
    public int Raids { get; set; }
    public int Plunder { get; set; }

    public const int ToRaider = 2, ToChampion = 5, ToWarchief = 9, WarchiefLevel = 8;

    public bool In => Rank != ClanRank.None;

    public bool Join(Warband w)
    {
        if (In) return false;
        (ClanId, ClanName, Chief, Rank) = (w.Id, w.Name, w.Chief, ClanRank.Spearman);
        return true;
    }

    /// <summary>A clansman's share of a plundered place: more for a bigger clan, and for standing in it.</summary>
    public int Share(Warband w) => 20 + 8 * w.Raiders + 15 * ((int)Rank - 1);

    /// <summary>Rode with the clan on a place, and it fell: the share, and a promotion if one's due.</summary>
    public (int Share, ClanRank? Promoted) Rode(Warband w, int heroLevel)
    {
        if (!In || w.Id != ClanId) return (0, null);
        var share = Share(w);
        Raids++;
        Plunder += share;
        var next = Rank switch
        {
            ClanRank.Spearman when Raids >= ToRaider => ClanRank.Raider,
            ClanRank.Raider when Raids >= ToChampion => ClanRank.Champion,
            ClanRank.Champion when Raids >= ToWarchief && heroLevel >= WarchiefLevel => ClanRank.Warchief,
            _ => Rank,
        };
        if (next == Rank) return (share, null);
        Rank = next;
        return (share, next);
    }

    public string NextStep(int level) => Rank switch
    {
        ClanRank.None => "Beat one of a clan's raiders at its camp, and it may offer you a place.",
        ClanRank.Spearman => $"{ToRaider} raids ridden make a raider ({Raids} so far).",
        ClanRank.Raider => $"{ToChampion} raids make the clan's champion ({Raids} so far).",
        ClanRank.Champion => $"{ToWarchief} raids and the years for it (level {WarchiefLevel}) and the chief gives you the clan ({Raids} raids, level {level}).",
        _ => "The clan is yours.",
    };

    public void Leave() => (ClanId, ClanName, Chief, Rank) = (null, null, null, ClanRank.None);

    public string Title => Rank switch
    {
        ClanRank.Spearman => $"a spear of {ClanName}",
        ClanRank.Raider => $"a raider of {ClanName}",
        ClanRank.Champion => $"champion of {ClanName}",
        ClanRank.Warchief => $"warchief of {ClanName}",
        _ => "",
    };
}

/// <summary>
/// A hero's name in the world, saved with the company: the honour they've earned and the infamy, the deeds people
/// tell of, the price on their head, and their careers in the Watch and in a clan. The world reads it: what the
/// townsfolk say when you walk up, what the shops charge, whether the Watch will have you or hunt you, and who else
/// will.
/// </summary>
public sealed class Standing
{
    public int Honour { get; set; }
    public int Infamy { get; set; }
    /// <summary>Crowns on the hero's head in Tristinwold: the Watch stops them at sight until it's paid.</summary>
    public int Bounty { get; set; }
    /// <summary>The last dozen things people tell of, oldest first: "broke the raid on Larkmere".</summary>
    public List<string> Deeds { get; set; } = [];
    public WatchBook Watch { get; set; } = new();
    public ClanBook Clan { get; set; } = new();

    public const int KeepDeeds = 12;
    /// <summary>What goes on the head of whoever kills a watchman, or rides with a clan on a place.</summary>
    public const int WatchmanBounty = 100, RaidBounty = 40;
    /// <summary>What a smuggler takes to make a bounty go away, on top of the bounty.</summary>
    public const double SmugglersCut = 0.2;

    public int Score => Honour - Infamy;

    public Repute Repute => Score switch
    {
        <= -50 => Repute.Villain,
        <= -15 => Repute.Rough,
        < 15 => Repute.Unknown,
        < 50 => Repute.Decent,
        < 120 => Repute.Trusted,
        _ => Repute.Hero,
    };

    public bool Outlaw => Bounty > 0;

    /// <summary>What the town knows you as.</summary>
    public string Name => Outlaw ? $"an outlaw, with {Bounty} crowns on your head" : Repute switch
    {
        Repute.Villain => "a villain",
        Repute.Rough => "a rough sort",
        Repute.Unknown => "nobody in particular",
        Repute.Decent => "a decent sort",
        Repute.Trusted => "a name people trust",
        _ => "a hero of Tristinwold",
    };

    public string? LastDeed => Deeds.Count > 0 ? Deeds[^1] : null;

    public void Good(int honour, string deed)
    {
        if (honour <= 0) return;
        Honour += honour;
        Tell(deed);
    }

    public void Evil(int infamy, string deed)
    {
        if (infamy <= 0) return;
        Infamy += infamy;
        Tell(deed);
    }

    /// <summary>A crime against Tristinwold: a price on the head, and the Watch strikes its own off the rolls for it.</summary>
    public void Wanted(int crowns)
    {
        if (crowns <= 0) return;
        Bounty += crowns;
        Watch.Dismiss();
    }

    void Tell(string deed)
    {
        Deeds.Add(deed);
        if (Deeds.Count > KeepDeeds) Deeds.RemoveRange(0, Deeds.Count - KeepDeeds);
    }

    /// <summary>Pays off the bounty, if there's the coin for it: what it cost, or null.</summary>
    public int? PayBounty(ref int crowns, bool throughSmuggler = false)
    {
        if (!Outlaw) return null;
        var cost = throughSmuggler ? (int)Math.Ceiling(Bounty * (1 + SmugglersCut)) : Bounty;
        if (crowns < cost) return null;
        crowns -= cost;
        Bounty = 0;
        return cost;
    }

    /// <summary>Why the Watch won't have the hero, or null if it will.</summary>
    public string? WhyNotWatch => Watch.In ? "you're already on the rolls"
        : Outlaw ? "there's a price on your head"
        : Clan.In ? "you ride with a barbarian clan"
        : Repute == Repute.Villain ? "the whole city knows what you are"
        : null;

    public bool MayJoinWatch => WhyNotWatch is null;

    /// <summary>Swore to a clan: no more a watchman, and the city hears of it.</summary>
    public bool JoinClan(Warband w)
    {
        if (!Clan.Join(w)) return false;
        Watch.Dismiss();
        Evil(4, $"swore to {w.Name}");
        return true;
    }

    /// <summary>What the shops charge, by name: an outlaw pays through the nose, a hero gets a little off.</summary>
    public double PriceFactor => Outlaw ? 1.3 : Repute switch
    {
        Repute.Villain => 1.15,
        Repute.Rough => 1.05,
        Repute.Trusted => 0.97,
        Repute.Hero => 0.94,
        _ => 1,
    };

    /// <summary>What the townsfolk add to a greeting, knowing who you are; null for a nobody with no deeds to tell of.</summary>
    public string? Aside(Rng rng)
    {
        if (Outlaw) return rng.Pick<string>(["\"There's a price on your head. I don't want trouble.\"", "\"The Watch is looking for you. Don't make me shout.\"", "\"Outlaw. I've seen the bill at the gate.\""]);
        var deed = LastDeed;
        return Repute switch
        {
            Repute.Hero => rng.Pick<string>([$"\"It's you! The one who {deed}.\"", "\"They sing about you in the Salt Barrel, you know.\"", "\"Tristinwold's own. What can I do for you?\""]),
            Repute.Trusted => rng.Pick<string>([$"\"I heard you {deed}. Good.\"", "\"You're the one people speak well of.\"", "\"Always glad to see a decent face.\""]),
            Repute.Decent => rng.Chance(0.5) ? $"\"Weren't you the one who {deed}?\"" : null,
            Repute.Rough => rng.Chance(0.6) ? rng.Pick<string>([$"\"I heard you {deed}. Keep your hands where I can see them.\"", "\"You've a name, and not a good one.\""]) : null,
            Repute.Villain => rng.Pick<string>([$"\"I know what you did. You {deed}.\"", "\"Stay back. I'll call the Watch.\"", "\"Take what you want and go. Please.\""]),
            _ => null,
        };
    }

    /// <summary>What's said of you when you ask for news, if anything: your last deed, and what people make of it.</summary>
    public string? Gossip(Rng rng)
    {
        if (LastDeed is not { } deed || !rng.Chance(0.4)) return null;
        var what = Repute switch
        {
            Repute.Villain or Repute.Rough => "Nobody's happy about it.",
            Repute.Trusted or Repute.Hero => "The whole city's talking.",
            _ => "That's all anyone's saying.",
        };
        return $"Only that you {deed}. {what}";
    }

    /// <summary>A line for the character sheet: how the world knows you, and your careers in it.</summary>
    public string Summary()
    {
        var parts = new List<string> { $"The town knows you as {Name} (honour {Honour}, infamy {Infamy})." };
        if (Watch.In) parts.Add($"You are {WatchBook.Title(Watch.Rank)} ({Watch.Patrols} beats, {Watch.Arrests} arrests).");
        if (Clan.In) parts.Add($"You are {Clan.Title} ({Clan.Raids} raids ridden).");
        return string.Join(" ", parts);
    }
}
