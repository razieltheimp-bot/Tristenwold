using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Heroes fighting heroes. Any player may fight any other, anywhere but a tavern, for any reason. B draws on every
/// other player about (or puts it away); a hero struck by another can always strike back at them, for a while. Each
/// blow goes to the struck player's own game, which takes it as it takes any blow, armour, dodges and all.
///
/// A hero put down by another loses nothing. The winner gets a copy of everything the loser carried, except that
/// weapons and armour come as what they're worth in crowns; a purse as full as the loser's; and any bounty on the
/// loser's head, which is then paid and gone (see <see cref="Gameplay.Spoils"/>). That's once an hour for each
/// winner from each loser, so friends can't farm each other.
///
/// A player who's gone is left asleep where they were (see <see cref="Visitors.Sleepers"/>), and can be fought the same
/// way: the server counts the blows, and whoever cuts them down wins a share of their purse, as a copy. This game
/// keeps the server told where this hero would lie (<see cref="SleepSpot"/>), and hears, on coming back, who went for
/// them while they slept (<see cref="WhileAway"/>). They wake where they lay all the same.
/// </summary>
public partial class Main
{
    /// <summary>Drawn on every other player about (B).</summary>
    bool _drawn;

    /// <summary>Players this hero is fighting, and until when: struck, or striking, keeps it going.</summary>
    readonly Dictionary<string, double> _heroFeuds = new(StringComparer.OrdinalIgnoreCase);
    const double FeudSeconds = 120;

    /// <summary>When the last blow from another player landed: a fall long after it isn't theirs.</summary>
    double _heroBlowAt = double.MinValue;
    const double FallCounts = 30;

    /// <summary>When each player last won spoils from this hero: once an hour, no more (the server keeps count too).</summary>
    readonly Dictionary<string, double> _paidTo = new(StringComparer.OrdinalIgnoreCase);
    public const double SpoilsEvery = 3600;

    /// <summary>The last thing sent about a fight between heroes (for the self-test, which has no server).</summary>
    public Message? LastHeroMessage { get; private set; }

    bool FightingHero(string player) => _drawn || _heroFeuds.GetValueOrDefault(player) > _clock;

    void SetUpHeroFights()
    {
        _player.Rivals = () => _visitors.StandIns.Where(e => FightingHero(e.Hero!));
        _visitors.Fighting = FightingHero;
        Enemy.HeroStruck += StrikeHero;
        _player.WentDown += FellToHero;
    }

    void TearDownHeroFights() => Enemy.HeroStruck -= StrikeHero;

    void Tell(Message m)
    {
        LastHeroMessage = m;
        _client?.Send(m);
    }

    /// <summary>B: draw on every other player about, or put it away. Anyone already fighting this hero still can be.</summary>
    void DrawOnHeroes()
    {
        _drawn = !_drawn;
        _hud.Toast(_drawn
            ? "You're ready to fight other players: your blows reach any of them now (their names go red). B again to stop. Nobody fights in a tavern."
            : _heroFeuds.Values.Any(t => t > _clock)
                ? "You stop picking fights. Anyone already fighting you, you can still hit back."
                : "You stop fighting other players.", 6);
    }

    /// <summary>This hero's blow landed on another player's stand-in: off it goes to their game.</summary>
    void StrikeHero(Enemy standIn, double damage, DamageKind kind, AttackKind attack, bool crit)
    {
        if (standIn.Hero is not { } who || !IsInstanceValid(standIn)) return;
        _heroFeuds[who] = _clock + FeudSeconds;
        _visitors.Flinch(who);
        Tell(new StrikeHero(who, damage, kind, attack, crit));
    }

    /// <summary>Another player's blow, landing on this hero: taken like any other, unless this is a tavern.</summary>
    void StruckByHero(HeroStruck h)
    {
        if (_visitors.StandIn(h.From) is not { } from) return; // nowhere near: it can't have reached
        var fresh = _heroFeuds.GetValueOrDefault(h.From) <= _clock;
        _heroFeuds[h.From] = _clock + FeudSeconds;
        if (_player.Downed) return;
        if (_player.Safe)
        {
            if (fresh) _hud.Toast($"{from.Stats.Name} goes for you, but this is a tavern: the room stops it.", 5);
            return;
        }
        if (fresh) _hud.Toast($"{from.Stats.Name} attacks you! Your blows reach them now: fight back, or run.", 6);
        _heroBlowAt = _clock;
        _player.TakeHit(h.Damage, h.Kind, h.Attack, h.Crit, from);
    }

    /// <summary>Put down by another player: they're told what they've won. This hero keeps it all, bar a bounty.</summary>
    void FellToHero()
    {
        if (_player.LastHitBy is not { } by || !IsInstanceValid(by) || by.Hero is not { } killer || _clock - _heroBlowAt > FallCounts) return;
        _heroFeuds.Remove(killer);
        if (_paidTo.TryGetValue(killer, out var paid) && _clock - paid < SpoilsEvery)
        {
            Tell(new HeroFell(killer, [], 0, 0, 0)); // they've had their spoils from you this hour: nothing more, and the bounty stands
            _hud.Toast($"{by.Stats.Name} has beaten you again, but they won nothing by it: they've had their spoils from you this hour. You keep everything you had.", 8);
            return;
        }
        _paidTo[killer] = _clock;
        var haul = Gameplay.Spoils.Of(_player.Pack, _player.Crowns);
        var bounty = _standing.Bounty;
        if (bounty > 0)
        {
            _standing.Bounty = 0; // collected: the Watch has what it wanted
            SyncOutlaw();
        }
        Tell(new HeroFell(killer, [.. haul.Items.Select(i => new SpoilItem(i.Id, i.N))], haul.Crowns, haul.GearCrowns, bounty));
        _hud.Toast($"{by.Stats.Name} has beaten you. You keep everything you had{(bounty > 0 ? $"; they collect the {bounty} crowns on your head, and the Watch stops looking for you" : "")}.", 9);
    }

    /// <summary>This hero put another player down: what's theirs for it.</summary>
    void TakeSpoils(HeroSpoils s)
    {
        foreach (var i in s.Items) _player.Pack.Add(i.Id, i.N);
        _player.Crowns += s.Crowns + s.GearCrowns + s.Bounty;
        _heroFeuds.Remove(s.From);
        var items = s.Items.Select(i => (i.Id, i.N)).ToList();
        _hud.Toast($"You've beaten {_visitors.NameOf(s.From) ?? s.From}. Yours for it: {Gameplay.Spoils.Tell(items, s.Crowns, s.GearCrowns, s.Bounty)}.", 10);
    }

    // ---- asleep ----

    SleepSpot? _spotSent;
    double _spotAt;

    /// <summary>Keeps the server told where this hero would lie if the player left now, and what's on them, whenever that changes.</summary>
    void TendSleepSpot()
    {
        if (_client?.Status != ClientStatus.Connected) { _spotSent = null; return; } // a new connection starts afresh
        if (_clock - _spotAt < 2) return;
        _spotAt = _clock;
        if (SpotNow() is not { } spot) return;
        if (_spotSent is { } last && last.Crowns == spot.Crowns && last.Safe == spot.Safe && Math.Abs(last.MaxHealth - spot.MaxHealth) < 1
            && new Vector3(last.X - spot.X, last.Y - spot.Y, last.Z - spot.Z).Length() < 0.5f) return;
        _spotSent = spot;
        _client.Send(spot);
    }

    /// <summary>Where this hero would lie if the player left now (where the save has them), and what's on them.</summary>
    SleepSpot? SpotNow() => Here() is { } here ? new SleepSpot(here.X, here.Y, here.Z, here.Facing, _player.Crowns, _player.Stats.MaxHealth, _player.Safe) : null;

    /// <summary>Back in the game: who went for this hero while they slept, and who cut them down. Nothing was lost by it.</summary>
    void HeardWhileAway(WhileAway w)
    {
        static string And(IReadOnlyList<string> names) =>
            names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        var attackers = w.Attackers.Count > 0 ? w.Attackers : w.Killer is { } k ? [k] : [];
        var text = w.Killer is { } killer
            ? $"While you were away, {And(attackers)} went for you where you slept, and {killer} cut you down" +
              (w.Taken > 0 ? $", winning {w.Taken} crowns by it (a copy: your purse is as full as you left it)." : ".") +
              " You wake where you lay, none the worse."
            : $"While you were away, {And(attackers)} went for you where you slept, but you held on. You wake where you lay.";
        _hud.Toast(text, 14);
    }

    // ---- for the self-test ----

    public void DrawOnHeroesForTest() => DrawOnHeroes();
    public SleepSpot? SleepSpotForTest => SpotNow();
    public bool DrawnForTest => _drawn;
    public bool FightingHeroForTest(string player) => FightingHero(player);
}
