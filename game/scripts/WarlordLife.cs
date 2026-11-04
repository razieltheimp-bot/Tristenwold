using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The deviants at the councils' tables (see <see cref="Warlord"/>), in the world. One at the head of a table holds the
/// place: come into it and it's in the street with its band round it, and it'll fight. Strike it down and the town is
/// free, and so is every town it took, and the head of its table is yours. One that came for you is on your tracker.
/// </summary>
public partial class Main
{
    string? _warlordTown;
    Enemy? _warlord;
    readonly List<Enemy> _warlordBand = [];

    /// <summary>The deviants this hero knows, playing alone: every one that beat them and walked off with a name has a score to settle.</summary>
    IEnumerable<Contender> LocalContenders() => _raids.Roamers.Select(r =>
        new Contender($"roamer:{r.Id}", r.Title, r.KindId, 2 + 2 * r.Wins, (Rank)Math.Clamp((r.Wins - 1) / 2, 0, 3), [_player.Company]));

    /// <summary>Who's in the street of a town a deviant holds when the hero comes in: it, and its band.</summary>
    void TendWarlordSeat()
    {
        var p = _player.GlobalPosition;
        var held = _city.WarlordTowns.Where(w => w.Lord.Rules).Select(w => (w.Town, w.Lord, Place: _world.Find(w.Town)))
            .FirstOrDefault(w => w.Place is { } s && s.DistanceTo(p.X, p.Z) < s.Radius + 30);
        if (held.Town is not null && held.Town == _warlordTown) return;
        ClearWarlord();
        if (held.Town is null || _player.Downed || _indoors is not null) return;
        _warlordTown = held.Town;
        RaiseWarlord(held.Lord, held.Place!, p);
    }

    void RaiseWarlord(Warlord lord, Settlement place, Vector3 p)
    {
        var level = Math.Max(lord.Level, _player.Hero.Level) + 1;
        var ahead = (-_player.GlobalBasis.Z with { Y = 0 }).Normalized();
        var at = p + ahead * 16;
        var e = Enemy.Create(Bestiary.Get(lord.Kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(level), look: lord.Kind);
        e.HeroesRival = lord.Id.StartsWith("roamer:");
        (e.Roam, e.Raider) = (16, true);
        AddEnemy(e);
        e.Crown($"{lord.Name}, Lord of {place.Name} (level {level})", Guid.TryParse(lord.Id, out var id) ? id : null);
        _warlord = e;
        var under = lord.Kind switch { "bandit_chief" => "bandit", "pirate_captain" => "pirate", "raider_chief" => "raider", "goblin_king" => "goblin", var k => k };
        for (var i = 0; i < 4; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 1.57f) * 5, 0, Mathf.Sin(i * 1.57f) * 5);
            var b = Enemy.Create(Bestiary.Get(under), spot with { Y = City.FloorAt(spot) + 0.5f }, Northwood.DangerFor(Math.Max(1, level - 3)), look: under);
            b.Stats.Name = Roaming.MinionOf(lord.Name);
            (b.Roam, b.Raider) = (12, true);
            AddEnemy(b);
            b.RefreshPlate();
            _warlordBand.Add(b);
        }
        _hud.Toast(lord.Against == _player.Company
            ? $"{lord.Name} holds {place.Name} now, and is waiting in the street with its band. \"There you are. I took this place to get at you. Shall we?\""
            : $"{lord.Name} holds {place.Name} now: the council's dead, and its band walks the street. \"Another one come to bow? No?\"", 10);
    }

    void ClearWarlord()
    {
        if (_warlord is not null && IsInstanceValid(_warlord) && _warlord.State != EnemyState.Dead) _warlord.QueueFree();
        foreach (var b in _warlordBand.Where(b => IsInstanceValid(b) && b.State != EnemyState.Dead)) b.QueueFree();
        _warlordBand.Clear();
        (_warlord, _warlordTown) = (null, null);
    }

    void OnWarlordSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim != _warlord || _warlordTown is not { } town || _city.CouncilOf(town).Warlord is not { } lord) return;
        _warlord = null;
        foreach (var b in _warlordBand.Where(b => IsInstanceValid(b) && b.State != EnemyState.Dead)) b.Collapse();
        _warlordBand.Clear();
        // A deviant of the hero's own, playing alone, or the shared world's that walks the wilds after them: dead for good, either way.
        if (_raids.Roamers.FirstOrDefault(r => $"roamer:{r.Id}" == lord.Id || r.DeviantId?.ToString() == lord.Id) is { } roamer) _raids.Slain(roamer);
        if (!byCompany) { _hud.Toast($"{lord.Name} has been killed, and not by you. {_world.Find(town)?.Name ?? "The town"} is free of it.", 8); DoCity(CityAction.WarlordFell(town, "")); return; }
        var level = _player.Hero.Level;
        _player.Crowns += 300 + 30 * level;
        GainXp(120 + 20 * level);
        Sound.Ui("quest");
        DoCity(CityAction.WarlordFell(town, _player.Company));
        _hud.Toast(_city.CouncilOf(town).Ruler == _player.Company
            ? $"{lord.Name} is dead. The council it killed its way through is gone to the last, so the head of the table in {_world.Find(town)?.Name ?? "the town"} is yours now."
            : $"{lord.Name} is dead, and {_world.Find(town)?.Name ?? "the town"} is free of it. There's a seat at its table for you.", 10);
        Save();
    }

    /// <summary>On the tracker: a rival who came for this hero, and where.</summary>
    IEnumerable<string> WarlordLines(Vector3 pos)
    {
        if (_city.RivalOf(_player.Company) is not { } rival || _world.Find(rival.Town) is not { } place) yield break;
        yield return rival.Lord.Rules
            ? $"YOUR RIVAL {rival.Lord.Name} rules {place.Name}, and its raiders are for you ({Direction(pos, (place.X, place.Z))})"
            : $"Your rival {rival.Lord.Name} is killing its way to the head of {place.Name}'s council ({Direction(pos, (place.X, place.Z))})";
    }

    // ---- for the self-test -------------------------------------------------------------------------------------------

    public Enemy? WarlordForTest => _warlord;
    public int WarlordBandForTest => _warlordBand.Count(b => IsInstanceValid(b) && b.State != EnemyState.Dead);
    public void TendWarlordSeatForTest() => TendWarlordSeat();
    public IEnumerable<string> WarlordLinesForTest(Vector3 pos) => WarlordLines(pos);
}
