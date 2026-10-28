using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>
/// What a deviant has learned about how the hero fights it: every blow it takes (what kind, a spell or a weapon, from
/// close or from far off), every potion it watches you drink to stay up, every time you got away from it, every fight.
/// Saved with the deviant; it never forgets.
/// </summary>
public sealed class Lessons
{
    public Dictionary<DamageKind, double> Damage { get; set; } = [];
    public double Spell { get; set; }
    public double Weapon { get; set; }
    public double Close { get; set; }
    public double Far { get; set; }
    public int Hits { get; set; }
    public int Potions { get; set; }
    public int Escapes { get; set; }
    public int Fights { get; set; }

    public double Total => Spell + Weapon;

    /// <summary>A blow it took: how much, what kind, and how far off the hero stood.</summary>
    public void Struck(double amount, DamageKind kind, AttackKind attack, double metres)
    {
        if (amount <= 0) return;
        Hits++;
        Damage[kind] = Damage.GetValueOrDefault(kind) + amount;
        if (attack == AttackKind.Spell) Spell += amount;
        else Weapon += amount;
        if (metres <= Tactics.CloseRange) Close += amount;
        else Far += amount;
    }
}

/// <summary>
/// How a deviant fights the hero, from what it has learned (see <see cref="Tactics.Read"/>): the damage it has learned
/// to take, a tougher hide, a ward against spells, a lunge across the gap, a quicker step on the hero's trail, and a
/// killing blow saved for when the hero is low. Each comes with what it says about it.
/// </summary>
public sealed record Counter(
    DamageKind? Resists = null,
    double Armor = 1,
    double Ward = 1,
    double Speed = 1,
    bool Lunges = false,
    double Finisher = 1,
    IReadOnlyList<(string Id, string Said)>? Learned = null)
{
    public static readonly Counter None = new();

    public IReadOnlyList<(string Id, string Said)> Lines => Learned ?? [];

    /// <summary>What it has learned that <paramref name="before"/> hadn't.</summary>
    public IEnumerable<(string Id, string Said)> Since(Counter before) => Lines.Where(l => before.Lines.All(b => b.Id != l.Id));
}

/// <summary>
/// The hunt and the lessons. A deviant out in the wilds that sees the hero picks up their trail and comes after them,
/// and keeps coming until they're inside the walls of a town or well clear of it; every escape makes it quicker on
/// the next hunt. Every fight teaches it how the hero fights, and it comes back ready for that.
/// </summary>
public static class Tactics
{
    /// <summary>How close the hero must come for a deviant to see them and take up the hunt (metres).</summary>
    public const double NoticeRange = 85;
    /// <summary>How far ahead the hero must get for it to lose the trail.</summary>
    public const double LoseRange = 360;
    /// <summary>A blow struck from this close is close work; any further off, the hero is keeping their distance.</summary>
    public const double CloseRange = 6;
    /// <summary>Blows it must take before it has learned anything about them.</summary>
    public const int HitsToLearn = 6;

    /// <summary>How it fights the hero now, from everything it has learned.</summary>
    public static Counter Read(Lessons l)
    {
        var learned = new List<(string, string)>();
        DamageKind? resists = null;
        double armor = 1, ward = 1, speed = 1, finisher = 1;
        var lunges = false;
        if (l.Hits >= HitsToLearn && l.Total > 0)
        {
            var (kind, most) = l.Damage.OrderByDescending(d => d.Value).First();
            if (most / l.Total >= 0.5)
            {
                resists = kind;
                learned.Add(($"resist:{kind}", Resisting(kind)));
            }
            if (l.Far / l.Total >= 0.5)
            {
                lunges = true;
                speed *= 1.15;
                learned.Add(("lunge", "You fight from a distance. It's learned to close it, fast."));
            }
            else if (l.Close / l.Total >= 0.6)
            {
                armor = 1.4;
                learned.Add(("armor", "You like it up close. It's grown a harder hide for you."));
            }
            if (l.Spell / l.Total >= 0.5)
            {
                ward = 1.5;
                learned.Add(("ward", "It's learned the shape of your spells, and shrugs them off."));
            }
        }
        if (l.Escapes > 0)
        {
            speed *= 1 + Math.Min(0.3, 0.1 * l.Escapes);
            learned.Add(($"chase:{Math.Min(3, l.Escapes)}", l.Escapes == 1 ? "You ran from it once. It's quicker on your trail now." : $"You've run from it {l.Escapes} times. It doesn't tire any more."));
        }
        if (l.Potions >= 2)
        {
            finisher = 1.3;
            learned.Add(("finisher", "It's watched you drink to stay on your feet. When you're low, it goes for the kill."));
        }
        return new Counter(resists, armor, ward, speed, lunges, finisher, learned);
    }

    static string Resisting(DamageKind kind) => kind switch
    {
        DamageKind.Fire => "Fire again? It's learned to take the heat.",
        DamageKind.Frost => "Your frost doesn't bite it the way it did.",
        DamageKind.Shock => "It's learned to let your lightning run through it.",
        DamageKind.Poison => "It's sweated out your poison so often it hardly feels it.",
        DamageKind.Sun => "Your holy light doesn't burn it the way it did.",
        _ => "It knows your blade now, and turns half of every blow.",
    };

    /// <summary>What it says when it picks up the hero's trail.</summary>
    public static string Taunt(Roamer r, Rng rng)
    {
        var counter = Read(r.Lessons);
        if (counter.Lines.Count > 0 && rng.Chance(0.7)) return rng.Pick(counter.Lines).Said;
        return r.Wins switch
        {
            1 => "You again. I remember you face down in the dirt.",
            2 => "Twice I've put you down. Three's a charm.",
            _ => $"{r.Wins} times now. You never learn. I do.",
        };
    }

    /// <summary>
    /// Whether it keeps after the hero: not once they're well clear of it, and into a town only if it dares (see
    /// <see cref="Dares"/>).
    /// </summary>
    public static bool StillHunting(double metresApart, bool heroInTown, bool dares = false) => (!heroInTown || dares) && metresApart < LoseRange;

    /// <summary>
    /// Whether it thinks it can kill the hero in a town, guards or no: the more often it has beaten them, the lower they
    /// are, and the more it has learned of how they fight, the bolder it is; every guard near them gives it pause.
    /// </summary>
    public static bool Dares(int wins, double heroHealth, int learned, int guardsNear) =>
        Boldness(wins, heroHealth, learned) - 0.3 * guardsNear >= 0.9;

    public static double Boldness(int wins, double heroHealth, int learned) =>
        0.35 * wins + 1.2 * (1 - Math.Clamp(heroHealth, 0, 1)) + 0.1 * learned;
}

/// <summary>What one of the townsfolk does when a deviant comes into their town.</summary>
public enum Defender
{
    /// <summary>Grabs a tool or a blade and goes at it, if it's close; throws whatever's to hand if not.</summary>
    Fights,
    /// <summary>Throws stones, bottles, whatever's to hand.</summary>
    Throws,
    /// <summary>Patches the hero up while they fight.</summary>
    Heals,
    /// <summary>Takes cover: the young, the old, the captive.</summary>
    Cowers,
}

/// <summary>
/// A town defends itself: when a deviant comes in after the hero, the people there don't just stand about. Those who
/// work with their hands or carry steel go at it; most throw whatever's to hand; the priests and healers patch the
/// hero up; the young and the old take cover. The Watch does the rest.
/// </summary>
public static class TownDefence
{
    /// <summary>How close to a deviant the townsfolk join in, and how close the fighters close to strike.</summary>
    public const double JoinRange = 25, Reach = 3.5;
    /// <summary>Seconds between one person's blows, throws or poultices.</summary>
    public const double Every = 2.5;

    public static Defender Role(Npc npc, bool captive = false)
    {
        if (captive || People.IsYouth(npc) || npc.Age >= 70) return Defender.Cowers;
        var job = npc.Occupation.ToLowerInvariant();
        bool Any(params string[] words) => words.Any(job.Contains);
        if (Any("priest", "healer", "midwife", "shrine", "apothecary", "herbalist")) return Defender.Heals;
        if (Any("guard", "watch", "sellsword", "knight", "iron circle", "smith", "miner", "foreman", "woodcutter", "dockhand", "labourer", "hunter",
                "carpenter", "butcher", "quarry", "fishwife", "fisher", "tanner", "thug", "velvet")) return Defender.Fights;
        return Defender.Throws;
    }

    /// <summary>What one blow or throw takes off a deviant (a share of its health): enough that a crowd counts, not enough that it does the work.</summary>
    public static double Blow(Defender role, bool close) => role switch
    {
        Defender.Fights => close ? 0.02 : 0.008,
        Defender.Throws => 0.006,
        _ => 0,
    };

    /// <summary>What a poultice gives back (a share of the hero's health).</summary>
    public const double Poultice = 0.04;

    /// <summary>The townsfolk wear it down, but they don't finish it: that's for the hero, or the Watch.</summary>
    public const double FloorShare = 0.1;
}

/// <summary>
/// Who has the hero on the ropes. It takes two blows in a row from the one foe to count: the first that strikes the
/// hero starts its count, a second strike from it makes two, and a strike from anything else in between wipes the
/// first one's count and starts its own. So a hero who falls back from one foe in a crowded hall and is clipped by
/// another on the way doesn't hand the kill to either; only the one that struck twice running put them down (and
/// only that one takes a name for it, or counts it as a win).
/// </summary>
public sealed class Strikes<T> where T : class
{
    /// <summary>Blows in a row from one foe that it takes to count.</summary>
    public const int Needed = 2;

    /// <summary>The foe whose count is running, and how many in a row it has landed.</summary>
    public T? By { get; private set; }
    public int Count { get; private set; }

    /// <summary>A blow landed: from the same foe, its count goes up; from another, the old count is gone.</summary>
    public void Struck(T by)
    {
        if (ReferenceEquals(by, By)) Count++;
        else (By, Count) = (by, 1);
    }

    /// <summary>Whether this foe has struck the hero twice running, with nothing else between.</summary>
    public bool Counts(T by) => ReferenceEquals(by, By) && Count >= Needed;

    /// <summary>The foe that has, if any.</summary>
    public T? Holder => Count >= Needed ? By : null;

    /// <summary>Back on their feet: everybody starts again.</summary>
    public void Clear() => (By, Count) = (null, 0);
}
