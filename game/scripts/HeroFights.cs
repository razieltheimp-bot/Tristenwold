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
/// loser's head, which is then paid and gone (see <see cref="Gameplay.Spoils"/>).
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
        var haul = Gameplay.Spoils.Of(_player.Pack, _player.Crowns);
        var bounty = _standing.Bounty;
        if (bounty > 0)
        {
            _standing.Bounty = 0; // collected: the Watch has what it wanted
            SyncOutlaw();
        }
        _heroFeuds.Remove(killer);
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

    // ---- for the self-test ----

    public void DrawOnHeroesForTest() => DrawOnHeroes();
    public bool DrawnForTest => _drawn;
    public bool FightingHeroForTest(string player) => FightingHero(player);
}
