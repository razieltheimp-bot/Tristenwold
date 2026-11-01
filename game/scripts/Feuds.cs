using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Deviants and their soldiers. Every deviant has its own kind at its back, more the higher it stands; and out in the
/// wilds, two who've a grudge may be settling it, band against band, when the hero comes over the rise.
/// </summary>
public partial class Feuds(Main main) : Node
{
    const float Ahead = 70, Apart = 9, Gone = 260;
    /// <summary>The chance, every half-minute out in the wilds, of walking into one; and how long before the next.</summary>
    const double Chance = 0.08, Between = 240;
    static readonly string[] Bandfolk = ["bandit", "raider", "goblin"];

    /// <summary>How long between one of a deviant's soldiers turning up and the next: a band gathers, it doesn't appear.</summary>
    const double Gathers = 10;
    readonly List<Enemy> _fighters = [];
    readonly Dictionary<Enemy, List<Enemy>> _bands = [];
    double _tick = 30, _nextAt;

    public IReadOnlyList<Enemy> Fighters => _fighters;

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 30;
        var p = main.PlayerNode;
        foreach (var (leader, band) in _bands.ToList())
        {
            band.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead);
            if (!IsInstanceValid(leader) || leader.State == EnemyState.Dead)
            {
                foreach (var e in band.Where(e => !e.FightingCompany)) e.QueueFree(); // with nobody to follow, they drift off
                band.RemoveAll(e => !e.FightingCompany);
                if (band.Count == 0) _bands.Remove(leader);
            }
        }
        _fighters.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead);
        if (_fighters.Count > 0)
        {
            if (_fighters.All(e => e.GlobalPosition.DistanceTo(p.GlobalPosition) > Gone))
            {
                foreach (var e in _fighters) e.QueueFree();
                _fighters.Clear();
            }
            return;
        }
        if (main.GuildBook.PlaySeconds < _nextAt || p.Downed || p.Seat is not null || p.InFight || p.Hero.Level < 3 || City.Below is not null || City.Afloat is not null) return;
        if (main.InTownForTurlin(p.GlobalPosition) || GD.Randf() >= Chance) return;
        Begin();
    }

    static bool Known(string kind)
    {
        try { return Bestiary.Get(kind) is not null; }
        catch (Exception) { return false; }
    }

    /// <summary>Two rivals from the shared world, if it has any; failing that, two names out of the wilds' own bandfolk.</summary>
    public bool Begin()
    {
        var p = main.PlayerNode;
        var spot = p.GlobalPosition + (-p.GlobalBasis.Z with { Y = 0 }).Normalized() * Ahead;
        if (main.World.Height(spot.X, spot.Z) < WorldGen.WaterLevel + 1 || main.InTownForTurlin(spot)) return false;
        main.TerrainNode.EnsureAround(spot, 1);
        _nextAt = main.GuildBook.PlaySeconds + Between;
        var side = (-p.GlobalBasis.X with { Y = 0 }).Normalized();
        var shared = main.DeviantsNow?.Feuds.FirstOrDefault(f => Known(f.A.BaseForm ?? f.A.Kind) && Known(f.B.BaseForm ?? f.B.Kind));
        string a, b;
        if (shared is { A: { } left, B: { } right })
        {
            var world = main.DeviantsNow!;
            (a, b) = (left.FullName, right.FullName);
            Band(left.BaseForm ?? left.Kind, a, left.Rank, spot + side * Apart, "feud_a", "feud_b", left.Id, world.ModifiersFor(left));
            Band(right.BaseForm ?? right.Kind, b, right.Rank, spot - side * Apart, "feud_b", "feud_a", right.Id, world.ModifiersFor(right));
        }
        else
        {
            var names = NamePack.Default;
            string Name() => $"{names.FirstNames[GD.RandRange(0, names.FirstNames.Count - 1)]} {names.Epithets[GD.RandRange(0, names.Epithets.Count - 1)]}";
            var kind = Bandfolk[GD.RandRange(0, Bandfolk.Length - 1)];
            (a, b) = (Name(), Name());
            Band(kind, a, Rank.Stray, spot + side * Apart, "feud_a", "feud_b");
            Band(kind, b, GD.Randf() < 0.35f ? Rank.Ringleader : Rank.Stray, spot - side * Apart, "feud_b", "feud_a");
        }
        main.HudNode.Toast($"Steel and shouting ahead: {a} and {b} are settling a feud, with their soldiers at their backs. They haven't seen you yet.", 8);
        main.HudNode.Log($"You came on a feud: {a} against {b}.");
        return true;
    }

    void Band(string kind, string name, Rank rank, Vector3 at, string side, string foe, Guid? id = null, BattleModifiers? mods = null)
    {
        var danger = WorldGen.Danger(at.X, at.Z);
        Enemy Fighter(Vector3 where)
        {
            var e = Enemy.Create(Bestiary.Get(kind), where with { Y = City.FloorAt(where) + 0.5f }, danger);
            (e.Faction, e.Foe, e.Roam) = (side, foe, 16);
            main.AddEnemy(e);
            _fighters.Add(e);
            return e;
        }
        var leader = Fighter(at);
        if (id is { } known && mods is not null) leader.MakeDeviant(known, mods, null);
        leader.Crown(name, id);
        for (var i = 0; i < Bands.Soldiers(rank); i++)
        {
            var a = i * Mathf.Tau / Bands.Soldiers(rank);
            Fighter(at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.5f);
        }
    }

    /// <summary>Who serves a kind of leader: a lair boss's own thralls, a chief's own people, or else a lesser one of its kind.</summary>
    public static string Underling(string kind) => Raids.All.FirstOrDefault(r => r.BossKind == kind)?.ThrallKind ?? kind switch
    {
        "dock_thug" => "lantern_boy",
        "bandit_chief" => "bandit",
        "raider_chief" => "raider",
        "pirate_captain" => "pirate",
        "draugr_overlord" => "draugr",
        "goblin_king" => "goblin",
        _ => kind,
    };

    /// <summary>The soldiers a leader has round it now (for the self-test).</summary>
    public int BandOf(Enemy leader) => _bands.TryGetValue(leader, out var band) ? band.Count(IsInstanceValid) : 0;
    public bool HasBand(Enemy leader) => _bands.ContainsKey(leader);

    /// <summary>
    /// A deviant's soldiers. They come to it one at a time, and a leader only ever has the one band, however often it's
    /// seen. A dragon has none: nothing follows a dragon. Nor does anything gather inside a town.
    /// </summary>
    public void Soldiers(Enemy leader, Rank rank, string? kind = null, double wait = Gathers)
    {
        var count = Bands.Soldiers(rank);
        if (count == 0 || leader.IsDragon || _bands.ContainsKey(leader) || main.RoamersNode.InTown(leader.GlobalPosition)) return;
        var band = new List<Enemy>();
        _bands[leader] = band;
        var who = kind ?? Underling(leader.Kind.Id);
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            GetTree().CreateTimer(wait * (i + 1)).Timeout += () =>
            {
                if (!IsInstanceValid(leader) || leader.State == EnemyState.Dead || !_bands.ContainsKey(leader)) return;
                var at = leader.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 6;
                var e = Enemy.Create(Bestiary.Get(who), at with { Y = City.FloorAt(at) + 0.5f }, WorldGen.Danger(at.X, at.Z));
                if (who == leader.Kind.Id) // a lesser one of its kind, not another of the same
                {
                    e.Stats.Grow(0.5);
                    e.Stats.DamageMult *= 0.6;
                }
                e.Roam = 12;
                main.AddEnemy(e);
                band.Add(e);
                if (leader.FightingCompany) e.Provoke(main.PlayerNode);
            };
        }
    }

    /// <summary>A leader's gone from the world for now: its band goes with it.</summary>
    public void Disband(Enemy leader)
    {
        if (!_bands.Remove(leader, out var band)) return;
        foreach (var e in band.Where(IsInstanceValid)) e.QueueFree();
    }
}
