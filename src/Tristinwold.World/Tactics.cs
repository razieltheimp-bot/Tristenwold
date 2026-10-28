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
    /// Whether it keeps after the hero: not into a town (it waits outside the walls), and not once they're well
    /// clear of it.
    /// </summary>
    public static bool StillHunting(double metresApart, bool heroInTown) => !heroInTown && metresApart < LoseRange;
}
