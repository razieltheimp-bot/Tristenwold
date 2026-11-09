using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>What a summoned beast does when it comes, and again while it stays.</summary>
public enum SummonMove
{
    /// <summary>Orrogh's stamp: the ground split under every foe about, foes thrown and dazed.</summary>
    Quake,
    /// <summary>Lumira's light: the company healed and quickened, Regen on them, and a sting of light on every foe.</summary>
    Dawn,
    /// <summary>Aegrath's guard: Protect, Shell and Reflect on the company, and a stamp that throws foes back.</summary>
    Bulwark,
    /// <summary>Quvirn's storm: lightning down on every foe about, chained from one to the next.</summary>
    Storm,
    /// <summary>Blaze's fire: a whirlwind of flame turning round it, flinging foes out of it burning.</summary>
    Firestorm,
    /// <summary>Shicene's cold: a storm of ice falling from her wings over everything, foes frozen where they stand.</summary>
    Blizzard,
    /// <summary>Draxon's breath: a river of green Rift-fire; whatever it corrupts and leaves standing obeys the summoner for a while (no boss).</summary>
    Dominion,
}

/// <summary>
/// A great beast a summoner can call, beaten in its own lair and bound to their ring. <paramref name="Might"/> is each of
/// its blows before the hero's level and spell power; <paramref name="Rise"/> is how far above the ground it hangs (a
/// flier).
/// </summary>
public sealed record Summon(string Id, string Name, string Title, DamageKind Kind, string Look, float Scale, SummonMove Move, double Might, string Text, float Rise = 0)
{
    public string FullName => $"{Name}, {Title}";
}

/// <summary>
/// One step of the road: a single creature in all the world, in a lair of its own (<paramref name="Lair"/>), that Braz
/// sends a summoner after at its step, and that binds only in that fight. <paramref name="Kind"/> is the creature it's
/// fought as; <paramref name="Health"/> is its own (it doesn't grow with the country); <paramref name="Send"/> is what
/// Braz says when she sends them; <paramref name="Recommended"/> is the level she'd want them at before they face it.
/// </summary>
public sealed record Trial(int Step, string Summon, string Kind, string Lair, double[] Distances, int Turn, double Health, string Send, int Recommended)
{
    public string Id => Summon;
}

/// <summary>
/// Summoning (see <see cref="SummonerStep"/>). Braz Algún walks the hero down the road: she marks their ring, and sends
/// them after the great ones of the road one at a time, in order (<see cref="Trials"/>). Each must be beaten in its own
/// lair, with the hero there, and then it's bound to their ring; the first makes them a Summoner, once Braz has seen it.
/// What's bound is theirs to call with G: for a while it stands over the fight and strikes, and then it's gone back into
/// the ring, and needs a long breath before it comes again. No dragon is ever bound but one: Draxon, Bubbles' son, at the end of the road.
/// </summary>
public static class Summons
{
    /// <summary>Braz won't mark a ring before this (the Dragon Knight's road opens at the same level).</summary>
    public const int MinLevel = 5;
    /// <summary>A call takes this share of the hero's mana, and the ring needs this long before the next.</summary>
    public const double ManaShare = 0.5, Cooldown = 120;
    /// <summary>How long a beast stays, and how often it strikes while it does (once as it comes, then every <see cref="Pulse"/>).</summary>
    public const double Stays = 12, Pulse = 4;
    /// <summary>How long a foe Draxon's fire corrupts obeys the summoner.</summary>
    public const double Obeys = 20;
    /// <summary>A called beast stands with this share of the strength it had in its lair: it fights, and it can be killed.</summary>
    public const double Sturdy = 0.4;

    /// <summary>Where Braz keeps her stones, west of Tristinwold's walls, below the Eyrie's hill.</summary>
    public const double StonesX = -212, StonesZ = -62;

    public const string Orrogh = "orrogh", Lumira = "lumira", Aegrath = "aegrath", Quvirn = "quvirn", Blaze = "blaze", Shicene = "shicene", Draxon = "draxon", DraxonKind = "dragon_black_corrupt";

    public static readonly IReadOnlyList<Summon> All =
    [
        // Steps 1, 2 and 4 are stand-ins of my own until the user names theirs.
        new(Orrogh, "Orrogh", "the Stone-Heart", DamageKind.Physical, Orrogh, 1.1f, SummonMove.Quake, 55,
            "An earth giant shouldering up out of the ground, moss on its back and a heart of glowing stone, splitting the earth under your foes."),
        new(Lumira, "Lumira", "the Dawnmoth", DamageKind.Sun, Lumira, 1.1f, SummonMove.Dawn, 40,
            "A great moth of living light, its wings shedding a dawn that mends your company and stings whatever fights it.", Rise: 2.5f),
        new(Quvirn, "Quvirn", "the Storm-Beast", DamageKind.Shock, Quvirn, 1.1f, SummonMove.Storm, 85,
            "A lightning elemental like a great clawed beast, dark-furred and spined, two snarling heads, the storm crawling over it, chaining lightning from foe to foe."),
        new(Aegrath, "Aegrath", "the Brass Colossus", DamageKind.Physical, Aegrath, 1.1f, SummonMove.Bulwark, 70,
            "A clockwork colossus out of Brasswake's oldest days, its core still turning: it plants itself over your company, and blows turn back off them."),
        new(Blaze, "Blaze", "the Living Pyre", DamageKind.Fire, Blaze, 1.15f, SummonMove.Firestorm, 100,
            "A fire elemental the size of a keep, black-plated and crested, wound about in a whirlwind of its own flame."),
        new(Shicene, "Shicene", "the Winter Wing", DamageKind.Frost, Shicene, 1.15f, SummonMove.Blizzard, 105,
            "A giant of living ice on wings like frost on a window, hanging in the air over the fight and freezing all below her.", Rise: 3),
        new(Draxon, "Draxon", "King of Beasts", DamageKind.Poison, Draxon, 1.6f, SummonMove.Dominion, 130,
            "Bubbles' son: a corrupted dragon far bigger than his mother, solid black and lit green from inside, his mind his own. The King of Beasts: whatever his green fire corrupts and leaves standing obeys him, and fights for you."),
    ];

    /// <summary>The great ones of the road, in the order Braz sends a summoner after them, by their step.</summary>
    public static readonly IReadOnlyList<Trial> Trials =
    [
        new(1, Orrogh, Orrogh, "the Broken Barrow", [1100.0, 1300, 1500, 1700], 1, 1600,
            "Start small. There's a barrow out there the ground's been heaving up for a hundred years, and the thing doing the heaving has a name: Orrogh. A heart of stone, and slow. Beat it, and it's yours to call. Your first.", 10),
        new(2, Lumira, Lumira, "the Glimmerfen", [1200.0, 1400, 1600, 1800], 15, 1900,
            "Out past the fields there's a fen that glows at night. Lumira hangs over it, a moth the size of a barn, made of light. She mends whatever fights for her, so hit her hard and don't stop. Beat her, and she'll mend you instead.", 12),
        new(3, Quvirn, Quvirn, "the Stormscar", [1500.0, 1700, 1900, 2200], 5, 2800,
            "Out on the high ground there's a scar where lightning comes down and never leaves: the Stormscar. Quvirn lives in it, a storm that grew claws and two heads to bite with. Beat it, and it's yours to call.", 15),
        new(4, Aegrath, Aegrath, "the Sunken Foundry", [1600.0, 1800, 2100, 2400], 21, 3000,
            "Before Brasswake put to sea, its makers built a guardian too big to take aboard, and left it. It's still out there in its sunken foundry, still turning, still guarding nothing. Aegrath. Blows bounce off it. Beat it, and it'll guard you.", 18),
        new(5, Blaze, Blaze, "the Cinder Maw", [1800.0, 2000, 2300, 2600], 11, 3400,
            "There's a hole in the world out there where the ground burns and doesn't stop: the Cinder Maw. Something lives in the fire. It calls itself nothing; the Ashborn call it Blaze. Beat it, and it's yours.", 22),
        new(6, Shicene, Shicene, "the Rime Spire", [2100.0, 2400, 2700, 3000], 19, 3200,
            "Far out, a spire of ice stands where no ice should, and snow falls round it from a clear sky. Shicene keeps it. She flies, and she'll freeze you where you stand. Beat her, and she's yours.", 26),
        new(7, Draxon, DraxonKind, "the Black Roost", [2500.0, 2800, 3200, 3700], 3, 6000,
            "Bubbles had a son. Draxon. The Rift took him the way it took her, but it never got his mind: he thinks, and he talks, and he's bigger than she ever was. The beasts call him their king, and whatever he corrupts obeys him. He won't be called by anyone he hasn't lost to. No summoner calls a dragon. Beat Draxon in his Black Roost, and you'll be the one who does.", 30),
    ];

    public static Summon? Get(string? id) => All.FirstOrDefault(s => s.Id == id);
    public static Trial? TrialOf(string? id) => Trials.FirstOrDefault(t => t.Id == id);

    /// <summary>One of a summoned beast's blows, at the hero's level (a limit break's growth).</summary>
    public static double Blow(Summon s, int level) => s.Might * (1 + 0.05 * (Math.Max(1, level) - 1));

    // ---- the road ----

    public static bool MayBegin(Hero h) => h.Summoner == SummonerStep.None && h.Level >= MinLevel;

    /// <summary>Braz marks their ring.</summary>
    public static bool Begin(Hero h)
    {
        if (!MayBegin(h)) return false;
        h.Summoner = SummonerStep.Marked;
        return true;
    }

    /// <summary>The next great one Braz would send them after: the first of the road they haven't bound.</summary>
    public static Trial? NextTrial(Hero h) => Trials.FirstOrDefault(t => !h.Bound.Contains(t.Id));

    /// <summary>Ready for Braz to send them after the next great one: on the road, not already sent, and (past the first) named a Summoner.</summary>
    public static Trial? ReadyFor(Hero h) =>
        h.Seeking is null && (h.Summoner == SummonerStep.Summoner || h.Summoner == SummonerStep.Marked && h.Bound.Count == 0) ? NextTrial(h) : null;

    /// <summary>Braz sends them after the next great one of the road.</summary>
    public static Trial? Send(Hero h)
    {
        if (ReadyFor(h) is not { } t) return null;
        h.Seeking = t.Id;
        return t;
    }

    /// <summary>
    /// A great one of the road beaten by the hero's company, with the hero there to see it: bound to their ring, if it's
    /// the one Braz sent them after (any other creature of its kind binds nothing: no dragon but Draxon, and only in
    /// his own fight). Returns what was bound, or null.
    /// </summary>
    public static Summon? Bind(Hero h, string greatOne)
    {
        if (h.Summoner == SummonerStep.None || h.Seeking != greatOne || Get(greatOne) is not { } s || h.Bound.Contains(s.Id)) return null;
        h.Seeking = null;
        h.Bound.Add(s.Id);
        h.Calls ??= s.Id;
        return s;
    }

    /// <summary>
    /// A called beast killed in the fight: torn out of the ring. It's gone, and the summoner must walk that step of the road
    /// again (Braz sends them after it once more, and it must be beaten again); whatever they call now is another they hold.
    /// </summary>
    public static bool Lose(Hero h, string id)
    {
        if (!h.Bound.Remove(id)) return false;
        if (h.Calls == id) h.Calls = h.Bound.FirstOrDefault();
        return true;
    }

    /// <summary>Back to Braz with the first of them bound: she names them a Summoner, and they can call it.</summary>
    public static bool Name(Hero h)
    {
        if (h.Summoner != SummonerStep.Marked || h.Bound.Count == 0) return false;
        h.Summoner = SummonerStep.Summoner;
        return true;
    }

    /// <summary>Which step of the road they're on: the step of the great one they're after or will be sent after next (0 before the road; one past the last when it's walked).</summary>
    public static int Step(Hero h) =>
        h.Summoner == SummonerStep.None ? 0 : TrialOf(h.Seeking)?.Step ?? NextTrial(h)?.Step ?? Trials[^1].Step + 1;

    /// <summary>Whether they can call anything now, and what.</summary>
    public static Summon? Callable(Hero h) => h.Summoner == SummonerStep.Summoner && h.Calls is { } id && h.Bound.Contains(id) ? Get(id) : null;

    /// <summary>The beast G calls from now on: one they've bound.</summary>
    public static bool Choose(Hero h, string id)
    {
        if (!h.Bound.Contains(id) || Get(id) is null) return false;
        h.Calls = id;
        return true;
    }

    /// <summary>
    /// Where a great one of the road keeps its lair: the same for every hero on the seed, out in the wilds (a kilometre and
    /// a half and more from Tristinwold: hard country), on dry open ground clear of every town, the great forest, and the
    /// lairs of the great ones before it.
    /// </summary>
    public static (double X, double Z) LairOf(WorldGen world, Trial t)
    {
        var before = Trials.TakeWhile(o => o != t).Select(o => LairOf(world, o)).ToList();
        foreach (var far in t.Distances)
            for (var i = 0; i < 24; i++)
            {
                var a = Math.PI * 2 * ((i * 7 + t.Turn) % 24) / 24;
                var (x, z) = (Math.Cos(a) * far, Math.Sin(a) * far);
                if (world.Height(x, z) < WorldGen.WaterLevel + 4 || Northwood.Inside(x, z) || world.SettlementsNear(x, z, 400).Any()) continue;
                if (Enumerable.Range(0, 8).Any(k => world.Height(x + Math.Cos(k * Math.PI / 4) * 30, z + Math.Sin(k * Math.PI / 4) * 30) < WorldGen.WaterLevel + 2)) continue;
                if (before.Any(o => Math.Sqrt((x - o.X) * (x - o.X) + (z - o.Z) * (z - o.Z)) < 900)) continue;
                return (x, z);
            }
        return (t.Distances[0], 0);
    }
}
