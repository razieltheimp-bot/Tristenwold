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

    readonly List<Enemy> _fighters = [];
    double _tick = 30, _nextAt;

    public IReadOnlyList<Enemy> Fighters => _fighters;

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 30;
        var p = main.PlayerNode;
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

    /// <summary>A deviant's soldiers, round it where it stands. A dragon has none: nothing follows a dragon.</summary>
    public void Soldiers(Enemy leader, Rank rank, string? kind = null)
    {
        if (leader.IsDragon) return;
        var count = Bands.Soldiers(rank);
        for (var i = 0; i < count; i++)
        {
            var a = i * Mathf.Tau / count;
            var at = leader.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.2f;
            var e = Enemy.Create(Bestiary.Get(kind ?? leader.Kind.Id), at with { Y = City.FloorAt(at) + 0.5f }, WorldGen.Danger(at.X, at.Z));
            e.Roam = 12;
            main.AddEnemy(e);
            if (leader.FightingCompany) e.Provoke(main.PlayerNode);
        }
    }
}
