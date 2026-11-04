using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Something worth taking in every building you can walk into (see <see cref="Indoors.Stash"/>): a house's chest, a
/// tavern's takings, a smith's strongbox, a shop's till, a warehouse's crates, a church's alms box. None of it is the
/// hero's, so taking it is theft: done unseen it's a haul and practice at thieving, caught at it it's the haul lost, a
/// fine, a worse name in the town, and in Tristinwold a price on the hero's head. Someone in the room makes being caught
/// far likelier. A stash fills again in a few days.
/// </summary>
public partial class Main
{
    const int StashRefills = 3, StashFine = 15, StashBounty = 20, StashPractice = 6;

    /// <summary>What a stash holds, by the building: crowns from and to, and the kinds of thing in it.</summary>
    static (int From, int To, string[] Things) StashOf(string kind) => kind switch
    {
        "tavern" or "longhall" => (15, 45, ["ale", "mead", "cider", "spiced_wine"]),
        "smithy" => (6, 18, ["iron_ingot", "steel_ingot", "coal", "leather"]),
        "store" => (12, 35, ["travel_rations", "tinderbox", "healing_draught", "bread"]),
        "warehouse" => (0, 10, ["wool", "leather", "iron_ingot", "ale", "coal"]),
        "barn" => (0, 6, ["wool", "leather", "potato", "cabbage", "apple"]),
        "church" => (8, 30, ["silver_ring", "healing_draught", "silver_ring", "gold_ring"]),
        "keep" or "tower" => (25, 70, ["gold_ring", "sapphire", "silver_ingot", "gold_ingot"]),
        "storehouse" => (0, 5, ["coal", "pine_log"]),
        _ => (4, 20, ["bread", "apple", "ale", "honey_cake", "wool", "silver_ring"]),
    };

    public static string StashName(string kind) => kind switch
    {
        "tavern" or "longhall" => "cash box behind the bar",
        "smithy" or "keep" or "tower" => "strongbox",
        "store" => "till",
        "warehouse" => "crates",
        "barn" => "feed bins",
        "church" => "alms box",
        "storehouse" => "old crates",
        _ => "chest",
    };

    string StashKey => $"looted:{_indoors!.Kind}:{_indoors.Seed}";

    /// <summary>For the self-test: whether the next theft is caught, whatever the dice say.</summary>
    public bool? StashCaughtForTest { get; set; }

    (string Verb, Action Act)? StashAction(Vector3 pos)
    {
        if (_indoors?.Stash is not { } at || pos.DistanceTo(at with { Y = pos.Y }) > 1.8f) return null;
        var name = StashName(_indoors.Kind);
        if (_pursuits.Counters.TryGetValue(StashKey, out var day) && _city.Day - day < StashRefills)
            return ($"look in the {name}", () => _hud.Toast($"The {name}'s empty. Somebody's been at it already. You, as it happens.", 4));
        return ($"search the {name} (it isn't yours)", Burgle);
    }

    /// <summary>Whether a thief's seen at it: likelier with somebody in the room, less likely the better a thief they are.</summary>
    bool CaughtStealing()
    {
        if (StashCaughtForTest is { } forced) return forced;
        var watched = _keeperInside is not null || _indoors?.Kind is "tavern" or "longhall";
        var chance = (watched ? 0.5 : 0.12) * (1 - Math.Min(0.6, 0.03 * _player.Pack.Level(Job.Thieving)));
        if (_player.Class.Id == "Thief") chance *= 0.4;
        else if (_player.Class.Calling == Calling.Rogue) chance *= 0.6;
        return _lootRng.Chance(Math.Clamp(chance, 0.03, 0.9));
    }

    public (string Verb, Action Act)? StashActionForTest(Vector3 at) => StashAction(at);

    void Burgle()
    {
        if (_indoors is not { } room) return;
        var town = TownHere(_player.GlobalPosition);
        var place = town is null ? "" : $" in {(_world.Find(town)?.Name ?? (town == ReputationBook.Capital ? "Tristinwold" : town))}";
        var (from, to, things) = StashOf(room.Kind);
        var name = StashName(room.Kind);
        _pursuits.Counters[StashKey] = _city.Day;
        if (CaughtStealing())
        {
            var fine = Math.Min(_player.Crowns, StashFine);
            _player.Crowns -= fine;
            _standing.Evil(2, $"were caught going through the {name} of a {Indoors.Called(room.Kind)}{place}");
            if (town is not null) Repute.Deed(-3, town);
            if (town == ReputationBook.Capital) _standing.Wanted(StashBounty);
            _player.Pack.Practise(Job.Thieving, StashPractice / 3);
            if (_keeperInside is { } keeper) { Repute.Robbed(keeper.Npc.Id, keeper.Npc.Name); keeper.TalkTo(_player.GlobalPosition); }
            Sound.Ui("error");
            _hud.Toast($"\"Thief!\" You're seen with your hands in the {name}. You put it all back, pay {fine} crowns for your trouble{(town == ReputationBook.Capital ? $", and the Watch puts {StashBounty} crowns on your head" : "")}, and they'll remember your face{place}.", 7);
        }
        else
        {
            var crowns = from + _lootRng.Next(to - from + 1);
            var took = new List<(string Id, int N)>();
            for (var i = 0; i < (room.Kind == "warehouse" ? 2 : 1); i++)
                if (_lootRng.Chance(0.75)) took.Add((_lootRng.Pick(things), 1));
            _player.Crowns += crowns;
            foreach (var (id, n) in took) _player.Pack.Add(id, n);
            var rose = _player.Pack.Practise(Job.Thieving, StashPractice);
            Sound.Ui("coin", -6);
            var haul = string.Join(" and ", new[] { crowns > 0 ? $"{crowns} crowns" : null, took.Count > 0 ? Loot.Describe(took).ToLowerInvariant() : null }.OfType<string>());
            _hud.Toast(haul.Length > 0 ? $"Nobody sees. From the {name}: {haul}.{(rose is { } l ? $" Thieving rises to {l}." : "")}" : $"Nobody sees, and there's nothing in the {name} worth the risk.", 5);
        }
        Save();
    }
}
