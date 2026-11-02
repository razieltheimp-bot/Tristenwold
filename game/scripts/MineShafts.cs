using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The little mines out in the country: anyone can walk down one, and more often than not there's something wrong down
/// there: the dead have got in, or someone hasn't come up. The foreman keeps trading at the head of the shaft regardless.
/// What's wrong changes with the month; clearing it counts for that month.
/// </summary>
public partial class Main
{
    public enum ShaftTrouble { None, Undead, Lost }

    const int ShaftMonth = 30;
    public const string LostMiner = "shaft:lost:";

    /// <summary>The mines by the seed on their door: a room only knows the seed it was built from.</summary>
    public readonly Dictionary<int, Settlement> Shafts = [];
    readonly List<Enemy> _shaftFoes = [];
    Settlement? _shaftHere;

    public IReadOnlyList<Enemy> ShaftFoes => _shaftFoes;

    string ShaftKey(Settlement s) => $"shaft:{s.Id}:{_city.Day / ShaftMonth}";

    /// <summary>What's wrong down this mine this month.</summary>
    public ShaftTrouble TroubleAt(Settlement s)
    {
        if (_pursuits.Counters.ContainsKey(ShaftKey(s))) return ShaftTrouble.None;
        var roll = new Rng((ulong)WorldGen.StableHash(ShaftKey(s)) ^ 0x5AF7).NextDouble();
        return roll < 0.45 ? ShaftTrouble.Undead : roll < 0.8 ? ShaftTrouble.Lost : ShaftTrouble.None;
    }

    /// <summary>What's waiting at the far end of a mine, set out as the hero walks in.</summary>
    public void PeopleTheShaft(Indoors room, int seed, Vector3 back)
    {
        if (!Shafts.TryGetValue(seed, out var s)) return;
        _shaftHere = s;
        var rng = new Rng((ulong)WorldGen.StableHash(ShaftKey(s)) ^ 0x1057);
        switch (TroubleAt(s))
        {
            case ShaftTrouble.Undead:
                for (var i = 0; i < 3 + rng.Next(2); i++)
                {
                    var e = Enemy.Create(Bestiary.Get("draugr"), back + new Vector3(i * 2.2f - 3, 0.3f, rng.Next(3)), WorldGen.Danger(s.X, s.Z));
                    AddEnemy(e);
                    _shaftFoes.Add(e);
                }
                break;
            case ShaftTrouble.Lost:
                var sex = rng.Chance(0.5) ? Sex.Male : Sex.Female;
                room.AddChild(new Person
                {
                    Npc = new Npc($"{LostMiner}{s.Id}", Names.Person(rng, sex), sex, 24 + rng.Next(30), "miner", s.Id, s.Name, "Went down a shift ago and didn't come up.",
                        ["the mine"], "nervous", "Thank the gods. My lamp went, and then the prop came down behind me.", back.X, back.Z),
                    Look = "worker", Position = back,
                });
                break;
        }
    }

    /// <summary>The way out: anything still down there stays down there.</summary>
    public void LeftTheShaft()
    {
        foreach (var e in _shaftFoes.Where(IsInstanceValid)) e.QueueFree();
        _shaftFoes.Clear();
        _shaftHere = null;
    }

    void TendShaft()
    {
        if (_shaftFoes.Count == 0 || _shaftHere is not { } s || _shaftFoes.Any(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) return;
        _shaftFoes.Clear();
        ShaftCleared(s, $"The last of the dead in {s.Name} falls.");
    }

    void ShaftCleared(Settlement s, string said)
    {
        _pursuits.Counters[ShaftKey(s)] = 1;
        var pay = 60 + 10 * _player.Hero.Level;
        _player.Crowns += pay;
        GainXp(40 + 6 * _player.Hero.Level);
        Repute.Deed(3, s.Id);
        Sound.Ui("quest");
        _hud.Toast($"{said} The miners send {pay} crowns after you.", 8);
        Save();
    }

    /// <summary>The foreman at the head of the shaft, and whoever's lost at the bottom of it.</summary>
    IEnumerable<(string, Action)> ShaftTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id.StartsWith(LostMiner))
        {
            if (_world.Find(person.Npc.HomeId) is { } mine)
                yield return ("Come on, I'll see you out.", () =>
                {
                    _hud.EndTalk();
                    ShaftCleared(mine, $"{person.Npc.Name} follows your lamp up to daylight.");
                    person.QueueFree();
                });
            yield break;
        }
        if (Shops.Of(id)?.Id.EndsWith("/mine") != true || _world.Find(person.Npc.HomeId) is not { } s) yield break;
        yield return ("Any trouble below?", () => Say(person, TroubleAt(s) switch
        {
            ShaftTrouble.Undead => "We broke into something old, and now the dead are in the low drift. Nobody'll go down. Clear them out and there's pay in it.",
            ShaftTrouble.Lost => "One of ours went down a shift ago and hasn't come up. I can't spare anyone. Go and find them, would you? There's pay in it.",
            _ => "Quiet as a mine ever is. Have a look if you like: mind your head.",
        }));
    }
}
