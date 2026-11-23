using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A town held by something too strong for any one hero (see <see cref="Assault"/>): Ronacse in its streets with the
/// sky falling round him, or a deviant grown past everyone with its band. Come near and it's there, with every wound
/// anyone's given it already on it; whatever you take off it is added to everyone's, so heroes who couldn't beat it
/// alone can beat it between them. Bring it down and the town's free.
/// </summary>
public partial class Main
{
    Enemy? _assailant;
    Assault? _assailantOf;
    /// <summary>Its health when last told to everyone, and how many of its forms are behind it (Ronacse's first).</summary>
    double _assailantTold;
    readonly List<Enemy> _assailantBand = [];
    double _assaultReportIn, _assaultMeteorIn = 3, _assaultCalmUntil;
    /// <summary>How near a town it's there for you, past its edge, and how far before it's gone again.</summary>
    const float AssaultNear = 80, AssaultGone = 180;

    /// <summary>Where it stands in a town, and how far the town reaches.</summary>
    (Vector3 At, float Reach, string Name)? AssaultSpot(Assault a)
    {
        if (a.Town == Undying.Capital) return (KorrinSeat + new Vector3(0, 0, 12), City.OuterRadius, "Tristinwold");
        if (_world.Find(a.Town) is not Settlement s) return null;
        return (new Vector3((float)s.X, 0, (float)s.Z + 12), (float)s.Radius, s.Name);
    }


    void TendAssault()
    {
        var p = _player.GlobalPosition;
        var standing = _assailant is not null && IsInstanceValid(_assailant) && _assailant.State != EnemyState.Dead;
        if (standing)
        {
            var still = _city.AssaultOn(_assailantOf!.Town);
            if (still is null || still.By != _assailantOf.By || AssaultSpot(still) is not { } spot || Flat(p, spot.At) > spot.Reach + AssaultGone)
            {
                TellAssaultWounds();
                ClearAssailant();
                return;
            }
            _assailantOf = still;
            if ((_assaultReportIn -= 1) <= 0) { _assaultReportIn = 5; TellAssaultWounds(); }
            return;
        }
        if (_assailant is not null && !IsInstanceValid(_assailant)) ClearAssailant();
        if (_player.Downed || _guilds.PlaySeconds < _assaultCalmUntil || City.Below is not null || City.Afloat is not null || _atSea is not null) return;
        foreach (var a in _city.Assaults)
        {
            if (AssaultSpot(a) is not { } spot || Flat(p, spot.At) > spot.Reach + AssaultNear || !_terrain.HasGround(spot.At)) continue;
            RaiseAssailant(a, spot.At, spot.Name);
            return;
        }
    }

    void RaiseAssailant(Assault a, Vector3 at, string town)
    {
        _terrain.EnsureAround(at, 1);
        if (a.IsPride)
        {
            if (_prideBody is { } old && IsInstanceValid(old)) old.QueueFree();
            var unbound = a.Hurt >= 1;
            RaisePride(at, unbound, provoke: false);
            _assailant = _prideBody;
            Wound(_assailant!, unbound ? a.Hurt - 1 : a.Hurt);
            _hud.Toast($"Light like noon over {town}, and burning stones coming down out of a clear sky. Ronacse stands in its street in {(unbound ? "white fire, the sun at his back" : "gold")}, and the people are behind barred doors. \"Is this all there is? Then I'll humble the lot of them.\"" + Woundedness(a), 11);
        }
        else
        {
            if (!Guid.TryParse(a.By, out var id) || DeviantOut(id) || DeviantsNow is not { } world || world.Find(id) is not { Alive: true } n) return;
            var mods = world.ModifiersFor(n);
            var e = Enemy.Create(Bestiary.Get(a.Kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(DeviantWorld.Might(n)));
            e.Roam = 14;
            AddEnemy(e);
            e.MakeDeviant(id, mods, null);
            e.Crown(n.FullName, id);
            _tracker.Engaged(e.Id, e.Kind.Id, e.Kind.Name, e.Stats.MaxHealth, ToNumerics(e.GlobalPosition), _area, ToNumerics(_player.GlobalPosition), _clock, id);
            var under = Feuds.Underling(a.Kind);
            for (var i = 0; i < 2 + 2 * a.Rank; i++)
            {
                var spot = at + new Vector3(Mathf.Cos(i * 1.3f) * 6, 0, Mathf.Sin(i * 1.3f) * 6);
                var s = Enemy.Create(Bestiary.Get(under), spot with { Y = City.FloorAt(spot) + 0.5f }, Northwood.DangerFor(Math.Max(1, a.Level)));
                s.Roam = 12;
                AddEnemy(s);
                _assailantBand.Add(s);
            }
            _assailant = e;
            Wound(e, a.Hurt);
            _hud.Toast($"{n.FullName} holds {town}, with its band in the streets, and the market's shut. It's past anything any one hero can face; but every blow anyone lands on it stays landed." + Woundedness(a), 11);
        }
        _assailantOf = a;
        _assailantTold = _assailant!.Stats.Health;
        _assaultReportIn = 5;
        _hud.Log($"{a.Name} holds {town}.");
    }

    static string Woundedness(Assault a) => a.Hurt % 1 is var h && h >= 0.05 ? $" ({h:P0} of its blood already spilled by those who came before you.)" : "";

    /// <summary>The wounds everyone's given it already: off its health, never all of it.</summary>
    static void Wound(Enemy e, double share)
    {
        share = Math.Clamp(share, 0, 0.98);
        if (share > 0) e.Stats.Lose(e.Stats.MaxHealth * share);
        e.RefreshPlate();
    }

    /// <summary>What you've taken off it since it was last told, added to everyone's.</summary>
    void TellAssaultWounds(double? health = null)
    {
        if (_assailant is not { } e || _assailantOf is not { } a || !IsInstanceValid(e)) return;
        var now = health ?? Math.Max(0, e.Stats.Health);
        var share = (_assailantTold - now) / Math.Max(1, e.Stats.MaxHealth);
        _assailantTold = now;
        if (share > 0.001) DoCity(CityAction.AssaultHurt(a.Town, share));
    }

    void ClearAssailant()
    {
        if (_assailant is { } e && IsInstanceValid(e) && e.State != EnemyState.Dead) e.QueueFree();
        if (_assailant == _prideBody) _prideBody = null;
        foreach (var s in _assailantBand.Where(s => IsInstanceValid(s) && s.State != EnemyState.Dead)) s.QueueFree();
        _assailantBand.Clear();
        (_assailant, _assailantOf) = (null, null);
    }

    /// <summary>Ronacse's first form down in a town: its wounds are told, and the second stands up where it fell, wounded as everyone's left it.</summary>
    void AssailantRose(Enemy unbound)
    {
        if (_assailantOf is not { } a) return;
        TellAssaultWounds(0);
        _assailant = unbound;
        Wound(unbound, Math.Max(0, a.Hurt - 1));
        _assailantTold = unbound.Stats.Health;
    }

    /// <summary>The thing holding the town brought down: the town's free.</summary>
    bool AssailantSlain(Enemy victim)
    {
        if (victim != _assailant || _assailantOf is not { } a) return false;
        TellAssaultWounds(0);
        DoCity(CityAction.AssaultBroken(a.Town, _player.Company));
        var level = _player.Hero.Level;
        var pay = (a.IsPride ? 1200 : 400 + 100 * a.Rank) + 40 * level;
        _player.Crowns += pay;
        GainXp(200 + 30 * level);
        Sound.Ui("coin", -2);
        foreach (var s in _assailantBand.Where(s => IsInstanceValid(s) && s.State != EnemyState.Dead)) s.QueueFree();
        _assailantBand.Clear();
        (_assailant, _assailantOf) = (null, null);
        if (!a.IsPride) _hud.Toast($"{a.Name} goes down in the street, and its band breaks and runs. The town's yours, and it knows it: doors open, and the market's crying its wares again by evening. +{pay} crowns.", 10);
        return true;
    }

    /// <summary>The hero went down to it: what they did to it stays done, and it lets them crawl off.</summary>
    void AssailantWon()
    {
        if (_assailant is null) return;
        TellAssaultWounds();
        _assaultCalmUntil = _guilds.PlaySeconds + 120;
        var body = _assailant;
        if (_assailantOf?.IsPride != true) GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(body) && body.State != EnemyState.Dead) body.QueueFree(); };
        foreach (var s in _assailantBand.Where(s => IsInstanceValid(s) && s.State != EnemyState.Dead)) { var b = s; GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(b)) b.QueueFree(); }; }
        _assailantBand.Clear();
        (_assailant, _assailantOf) = (null, null);
    }

    /// <summary>The sky falls on a town Ronacse holds, near whoever's in its streets: not aimed, and not deadly, but it hurts.</summary>
    void TendAssaultMeteors(double delta)
    {
        if (_assailantOf is not { IsPride: true } || _assailant is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead || e.FightingCompany) return;
        if ((_assaultMeteorIn -= delta) > 0) return;
        _assaultMeteorIn = 5 + GD.Randf() * 4;
        var p = _player.GlobalPosition;
        var at = p + new Vector3((GD.Randf() - 0.5f) * 40, 0, (GD.Randf() - 0.5f) * 40);
        at = at with { Y = City.FloorAt(at) };
        CallMeteor(at, new Color("#ff6a20"), new Color("#3a2010"), 0.8f, DamageKind.Fire, () =>
        {
            if (IsInstanceValid(e) && _player.GlobalPosition.DistanceTo(at) < 4f && !_player.Downed)
                _player.TakeHit(_player.Stats.MaxHealth * 0.15, DamageKind.Fire, AttackKind.Spell, false, e);
        });
    }

    // ---- for the self-test -------------------------------------------------------------------------------------------

    public Enemy? AssailantForTest => _assailant;
    public int AssailantBandForTest => _assailantBand.Count(s => IsInstanceValid(s) && s.State != EnemyState.Dead);
    public void TendAssaultForTest() { _assaultCalmUntil = 0; TendAssault(); }
    public void TellAssaultWoundsForTest() => TellAssaultWounds();
    public (Vector3 At, float Reach, string Name)? AssaultSpotForTest(Assault a) => AssaultSpot(a);
}
