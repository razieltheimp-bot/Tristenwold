using System.Numerics;
using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// Decides what counts as one fight in a real-time game and how it ended, then reports it in
/// the shared world's terms. The game feeds it facts each frame; it never touches the engine.
/// </summary>
public sealed class EncounterTracker(
    Action<string, IReadOnlyList<FormationSlot>> onStart,
    Action<BattleReport> onEnd,
    string company,
    double quietSeconds = 8,
    double gatherSeconds = 1.5)
{
    sealed class Tracked
    {
        public required int Slot;
        public required long Id;
        public required string Kind;
        public required string Species;
        public required double MaxHealth;
        public required Vector3 Start;
        public Vector3 Last;
        public Guid? DeviantId;
        public bool Killed;
        public string? KilledBy;
        public DamageKind? Blow;
        public double LastFight;
    }

    readonly Dictionary<long, Tracked> _byId = [];
    readonly Dictionary<int, Tracked> _bySlot = [];
    readonly List<long> _unannounced = [];
    int _nextSlot;
    bool _active, _downed;
    Vector3 _playerStart, _playerLast;
    double _announceAt;
    string _area = "";

    public bool InFight => _active;

    public long? ActorForSlot(int slot) => _bySlot.TryGetValue(slot, out var t) ? t.Id : null;

    public bool IsDeviant(long id) => _byId.TryGetValue(id, out var t) && t.DeviantId is not null;

    /// <summary>An enemy is fighting the company. Pass the deviant id when it came hunting.</summary>
    public void Engaged(long id, string kind, string species, double maxHealth, Vector3 pos, string area, Vector3 player, double now, Guid? deviantId = null)
    {
        if (!_active)
        {
            _active = true;
            _downed = false;
            _playerStart = player;
            _area = area;
            _announceAt = now + gatherSeconds;
        }
        _playerLast = player;
        if (_byId.TryGetValue(id, out var known))
        {
            known.LastFight = now;
            known.Last = pos;
            return;
        }
        var t = new Tracked { Slot = _nextSlot++, Id = id, Kind = kind, Species = species, MaxHealth = maxHealth, Start = pos, Last = pos, DeviantId = deviantId, LastFight = now };
        _byId[id] = t;
        _bySlot[t.Slot] = t;
        if (deviantId is null) _unannounced.Add(id);
    }

    public void MarkDeviant(int slot, Guid deviantId)
    {
        if (_bySlot.TryGetValue(slot, out var t)) t.DeviantId = deviantId;
    }

    public void Hit(long id, DamageKind kind, string? byCompany, double now)
    {
        if (!_byId.TryGetValue(id, out var t)) return;
        t.Blow = kind;
        if (byCompany is not null) t.KilledBy = byCompany;
        t.LastFight = now;
    }

    public void Died(long id, string? killer, double now)
    {
        if (!_byId.TryGetValue(id, out var t)) return;
        t.Killed = true;
        if (killer is not null) t.KilledBy = killer;
        t.LastFight = now;
    }

    public void Downed()
    {
        if (_active) _downed = true;
    }

    public void Tick(double now, Vector3 player, Func<long, bool> stillFighting, Func<long, Vector3?> posOf)
    {
        if (!_active) return;
        _playerLast = player;

        if (_unannounced.Count > 0 && now >= _announceAt)
        {
            var slots = _unannounced.Select(id => _byId[id]).Select(t => new FormationSlot(t.Slot, t.Kind, t.Species)).ToList();
            _unannounced.Clear();
            onStart(_area, slots);
        }

        var anyFighting = false;
        foreach (var t in _byId.Values.Where(t => !t.Killed))
        {
            if (posOf(t.Id) is { } p) t.Last = p;
            if (stillFighting(t.Id)) { t.LastFight = now; anyFighting = true; }
            else if (now - t.LastFight < quietSeconds) anyFighting = true;
        }
        if (!anyFighting || _downed) Finish();
    }

    void Finish()
    {
        var all = _byId.Values.OrderBy(t => t.Slot).ToList();
        var alive = all.Where(t => !t.Killed).ToList();
        BattleResult result;
        if (_downed) result = BattleResult.Defeat;
        else if (alive.Count == 0) result = BattleResult.Victory;
        else
        {
            // Somebody broke off with enemies alive: whoever moved further is the one who ran.
            var playerMoved = Vector3.Distance(_playerStart, _playerLast);
            var enemyMoved = alive.Average(t => Vector3.Distance(t.Start, t.Last));
            result = playerMoved > enemyMoved + 5 ? BattleResult.PartyEscaped : BattleResult.EnemiesEscaped;
        }

        onEnd(new BattleReport(_area, result, all.Select(t => new EnemyReport(
            t.Slot, t.Kind, t.Species, (int)Math.Round(t.MaxHealth),
            Killed: t.Killed,
            Escaped: !t.Killed && result == BattleResult.EnemiesEscaped,
            DeviantId: t.DeviantId,
            KillingBlow: t.Killed ? t.Blow ?? DamageKind.Physical : null,
            KilledBy: t.Killed ? t.KilledBy : null,
            BaseForm: t.Kind)).ToList(), [company], PartyLevel: 5));

        _byId.Clear();
        _bySlot.Clear();
        _unannounced.Clear();
        _nextSlot = 0;
        _active = false;
        _downed = false;
    }
}
