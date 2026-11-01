using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>The place a hero has made their home: the ring brings them back there, and not to the Perch.</summary>
public sealed record Hometown(string Id, string Name, double X, double Z);

/// <summary>Where the hero is in the main story, The Rings of Tristinwold.</summary>
public enum Chapter { NotStarted, Waking, Archive, Proving, Breach, Dragonfire, Warden, Done }

/// <summary>
/// The main story: what the hero is. Every hero in the world, every player's, is an Immortal, and none of them was born
/// one. When the Rift tore open and the Hollow Host came through it, the only cities left standing were the ones the
/// Magi had ringed: a Wake sunk under each, a well of Keystone-light the demons can't abide. Then the Magi found what
/// the Host fears most, and set about making it. In the undercroft of the Needle they took the people nobody would
/// miss (foundlings out of the Warrens, the condemned off the gallows, Ashborn orphans from outside the walls) and
/// remade them in the Ringing, the greatest working the Magi ever wrought: nine nights of spellwork in a circle of
/// Keystone-light, the blood sung through with it, the old life burned out by the spell, a ring of black iron sunk into
/// the bone of one finger and bound to the Wake beneath the city. Of every hundred given to the Needle, a handful
/// woke. Those who did remember nothing before it. They're quicker and harder than anyone born, cold to pain, and
/// bound: when one dies, the ring drags what's left of them back down its tether to the Wake, and the Wake makes them
/// again, at the Gallows Perch, where Mother Corva waits with a blanket and a bottle.
/// <para>
/// That's why they're the line the cities hold against the Host: a soldier dies once. And it's why an Immortal can
/// bring down a dragon the Rift has taken, which no army can: they can come back, and try again, and again, and each
/// time know a little more of how it fights. The townsfolk pay them and need them and don't love them: to their
/// faces, Immortals; behind their backs, gallows-born, cuckoos, the Needle's get.
/// </para>
/// <para>
/// The road: wake, and hear from Mother Corva what you are; learn from Sister Ilse in the Magi Archive what was done
/// to you; prove the ring holds (fall, and come back); hold the line at a breach the Host has opened beyond the walls;
/// bring down a dragon the Rift has taken; and come back to the Perch a warden of the Rings.
/// </para>
/// </summary>
public sealed class ImmortalSaga
{
    public const string Corva = "tristinwold/corva", Ilse = "tristinwold/ilse";

    /// <summary>How many of the Host it takes to close the breach.</summary>
    public const int BreachToClose = 8;

    /// <summary>The title the road ends in.</summary>
    public const string Title = "Warden of the Rings";

    public Chapter Chapter { get; set; }
    /// <summary>How many times the ring has brought the hero back. Mother Corva keeps count; she keeps a ledger.</summary>
    public int Returns { get; set; }
    /// <summary>Of the Host, slain at the breach (or anywhere, once the breach is open).</summary>
    public int HostSlain { get; set; }
    /// <summary>Where the ring brings them back; none means Tristinwold and the Gallows Perch.</summary>
    public Hometown? Home { get; set; }

    /// <summary>Somewhere a hero can make a home: a place with people in it, and not one that would rather they were dead.</summary>
    public static bool Homely(Settlement s) => !s.Hostile && s.Kind is SettlementKind.City or SettlementKind.Town or SettlementKind.Hamlet or SettlementKind.Inn;

    /// <summary>The ring is tied to a new home, if it's somewhere that can be one. True if that changed anything.</summary>
    public bool Settle(Settlement s)
    {
        if (!Homely(s) || Home?.Id == s.Id) return false;
        Home = new Hometown(s.Id, s.Name, s.X, s.Z);
        return true;
    }

    /// <summary>The nearest of some docks to the hero's home: a sailor wakes on the quay.</summary>
    public int NearestDock(IReadOnlyList<(double X, double Z)> docks, (double X, double Z) tristinwold)
    {
        var (x, z) = Home is { } h ? (h.X, h.Z) : tristinwold;
        var (best, nearest) = (0, double.MaxValue);
        for (var i = 0; i < docks.Count; i++)
        {
            var d = Math.Sqrt(Math.Pow(docks[i].X - x, 2) + Math.Pow(docks[i].Z - z, 2));
            if (d < nearest) (best, nearest) = (i, d);
        }
        return best;
    }

    /// <summary>What every new hero hears as they first open their eyes.</summary>
    public const string Opening =
        "You wake on straw that smells of ale, under a blanket that isn't yours, with no memory of anything before this. " +
        "There's a ring of black iron on your finger. It doesn't come off. It isn't meant to. An old woman behind a bar is watching you. " +
        "\"There you are,\" says Mother Corva. \"Come here, when you can stand. We should talk about what you are.\"";

    /// <summary>A new hero: the story starts with them waking.</summary>
    public bool Begin()
    {
        if (Chapter != Chapter.NotStarted) return false;
        Chapter = Chapter.Waking;
        return true;
    }

    /// <summary>The hero talked to someone in the story: it moves on if that's who they needed. Returns what was said.</summary>
    public string? Talked(string npcId)
    {
        switch (Chapter)
        {
            case Chapter.Waking when npcId == Corva:
                Chapter = Chapter.Archive;
                return
                    "\"You weren't born, love. Not like that. You were made, under the Needle, by the Magi, and you came up out of the Wake " +
                    "beneath this floor, same as all of them do. That ring on your finger is tied to it. Die out there, and it brings you back " +
                    "here. Every time. That's what you are: an Immortal. The cities stand because of people like you.\" She pours you a drink. " +
                    "\"Don't ask me how it's done. Ask the Magi. Sister Ilse keeps their Archive, and their records. Yours will be in there.\"";
            case Chapter.Archive when npcId == Ilse:
                Chapter = Chapter.Proving;
                return
                    "Sister Ilse finds the ledger without having to look. \"The Ringing, autumn intake. A hundred given to the Needle: foundlings, " +
                    "two from the gallows, Ashborn children from outside the walls. Nine nights in the circle, the Keystone-light sung into the blood, the old life burned out, the ring " +
                    "sunk into the bone and tied to the Wake.\" She turns the page round so you can see the column of crosses, and the few names " +
                    "without them. \"Nine woke. You're one. Whoever you were before, that person didn't survive it.\" She closes the book. " +
                    "\"The Magi will tell you the ring holds. Don't take their word. Go out past the walls, and find out.\"";
            case Chapter.Warden when npcId == Corva:
                Chapter = Chapter.Done;
                return
                    $"Mother Corva writes something in her ledger, and turns it so you can read it: your name, and after it, {Title}. " +
                    "\"There are other cities with a Wake under them, and other Perches, and Immortals waking in all of them. Most never get this far. " +
                    "You'll be needed, out there. The Host isn't done.\" She pours two drinks this time. \"And the Magi aren't telling us everything. " +
                    "They never have. One of them walked out of the Needle with the whole of the Ringing in his head, years back, and they've been " +
                    "looking for him since. Keep your eyes open.\"";
        }
        return null;
    }

    /// <summary>The ring brought the hero back. Returns what Mother Corva says as they wake.</summary>
    public string Returned(Rng rng)
    {
        Returns++;
        if (Chapter == Chapter.Proving)
        {
            Chapter = Chapter.Breach;
            return "You come up out of the dark gasping, at the Gallows Perch, whole. Mother Corva is already there with the blanket. " +
                   "\"Now you know. It holds.\" She helps you up. \"Good timing, too: the Watch says the Host has torn a breach open past the walls. " +
                   "That's what you're for. Go and shut it.\"";
        }
        return rng.Pick(WakeLines).Replace("{n}", Ordinal(Returns));
    }

    static readonly string[] WakeLines =
    [
        "You wake at the Gallows Perch. \"That's the {n} time,\" says Mother Corva, writing it down. \"Drink this.\"",
        "You come back gasping, on the straw by the bar. Mother Corva doesn't look up. \"Welcome back. The {n} time. Try not to make a habit of it.\"",
        "The Wake lets you go, and you're on the Perch's floor. \"Again,\" says Mother Corva. \"That's the {n}. You'll remember what killed you. Use it.\"",
        "You wake at the Gallows Perch with the taste of iron in your mouth. Somebody at the bar makes the sign against the Needle. Mother Corva throws them out.",
        "You wake. The ring is warm. Mother Corva puts a cup in your hand. \"The {n} time. Something stays behind, every time. Don't let it be anything you need.\"",
    ];

    /// <summary>Something died at the hero's hand: one of the Host counts toward closing the breach; a dragon the Rift took ends that chapter.</summary>
    /// <returns>True when that moved the story on.</returns>
    public bool Slain(string kindId)
    {
        if (Chapter == Chapter.Breach && IsHost(kindId) && ++HostSlain >= BreachToClose)
        {
            Chapter = Chapter.Dragonfire;
            return true;
        }
        if (Chapter == Chapter.Dragonfire && Dragons.IsCorrupted(kindId))
        {
            Chapter = Chapter.Warden;
            return true;
        }
        return false;
    }

    /// <summary>The Hollow Host: demons of the Rift, imps and worse, and whatever it has twisted.</summary>
    public static bool IsHost(string kindId) => kindId is "imp" or "dire_imp" || kindId.EndsWith("_corrupt");

    /// <summary>What the story says to do next, for the quest tracker.</summary>
    public string? Objective => Chapter switch
    {
        Chapter.Waking => "Speak to Mother Corva at the Gallows Perch",
        Chapter.Archive => "Ask Sister Ilse at the Magi Archive what was done to you",
        Chapter.Proving => "Prove the ring holds: fall in battle, and come back",
        Chapter.Breach => $"Hold the line: close the Host's breach beyond the walls ({HostSlain}/{BreachToClose} of the Host slain)",
        Chapter.Dragonfire => "Bring down a dragon the Rift has taken. Only an Immortal can keep trying",
        Chapter.Warden => "Return to Mother Corva at the Gallows Perch",
        _ => null,
    };

    /// <summary>What's said when a chapter ends in the field.</summary>
    public string? Moved => Chapter switch
    {
        Chapter.Dragonfire =>
            "The breach gutters and closes, with a sound like a held breath let go. The Host's stragglers scatter. " +
            "Word comes from the Watch: a dragon the Rift has taken is hunting the wilds. Armies have died against such things. " +
            "An army can only die once.",
        Chapter.Warden =>
            "The dragon comes down, and the violet light goes out of it. No soldier could have done this, and you know it. " +
            "Go home. Mother Corva will want to write it in her ledger.",
        _ => null,
    };

    /// <summary>Where the breach the Host tears open is: out in the wilds south of the walls, on dry ground, clear of every
    /// village and lair. The same in every world grown from the same seed.</summary>
    public static (double X, double Z) BreachSite(WorldGen world)
    {
        for (var i = 0; i < 64; i++)
        {
            var a = 1.9 + i * 0.37;
            var d = 520 + 12 * i;
            var (x, z) = (Math.Cos(a) * d, Math.Sin(a) * d);
            if (world.Height(x, z) < WorldGen.WaterLevel + 2) continue;
            if (world.SettlementsNear(x, z, 300).Any(s => s.DistanceTo(x, z) < s.Radius + 70)) continue;
            if (Raids.All.Any(r => r.DistanceTo(x, z) < 160)) continue;
            return (Math.Round(x), Math.Round(z));
        }
        return (-200, 560);
    }

    /// <summary>Where to go next, if somewhere in particular.</summary>
    public (double X, double Z)? Goal(WorldGen world, (double X, double Z) corva, (double X, double Z) ilse, double x, double z) => Chapter switch
    {
        Chapter.Waking or Chapter.Warden => corva,
        Chapter.Archive => ilse,
        Chapter.Breach => BreachSite(world),
        Chapter.Dragonfire => Dragons.Near(world, x, z, 30000).Where(a => a.Corrupted).MinBy(a => a.DistanceTo(x, z)) is { } a ? (a.X, a.Z) : null,
        _ => null,
    };

    static string Ordinal(int n) => n switch
    {
        1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth", 10 => "tenth",
        _ => n % 10 == 1 && n % 100 != 11 ? $"{n}st" : n % 10 == 2 && n % 100 != 12 ? $"{n}nd" : n % 10 == 3 && n % 100 != 13 ? $"{n}rd" : $"{n}th",
    };
}
