using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Guilds as neighbours and as work for hire: each keeps a compound outside the walls, three to five buildings round a
/// fenced yard with an open gate, and anyone with the crowns can hire one for a job, whatever their own calling.
/// </summary>
public partial class Main
{
    const float CompoundFence = 27, CompoundBuildings = 19.5f, FencePanel = 3.6f;

    /// <summary>A guild's compound: its buildings round the yard, front doors in, and a fence with an open gate towards the city.</summary>
    void Compound(Guild g, Vector3 centre)
    {
        var rng = new Tristinwold.Deviant.Rng((ulong)WorldGen.StableHash(g.Id) ^ 0xC0AF);
        var kinds = g.Calling switch
        {
            Calling.Warrior => new[] { "longhall", "smithy", "house", "barn", "tower" },
            Calling.Rogue => ["barn", "house", "cottage", "barn", "house"],
            Calling.Mage => ["tower", "church", "house", "tower", "cottage"],
            Calling.Bard => ["longhall", "tavern", "house", "cottage", "house"],
            _ => ["smithy", "barn", "longhall", "smithy", "house"],
        };
        var count = 3 + rng.Next(3);
        var gate = Mathf.Atan2(-centre.Z, -centre.X);
        var roof = rng.Chance(0.5) ? Surface.Slate : Surface.Shingle;
        for (var i = 0; i < count; i++)
        {
            // Its slot round the yard, or a little either side of it if that's flatter; on a hillside too steep for any of them it isn't built.
            var best = (Rise: float.MaxValue, At: Vector3.Zero, Yaw: 0f);
            foreach (var nudge in new[] { 0f, 0.12f, -0.12f, 0.24f, -0.24f })
            {
                var a = gate + (i + 1f) / (count + 1) * Mathf.Tau + nudge;
                var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * CompoundBuildings;
                var yaw = Mathf.Atan2(centre.X - at.X, centre.Z - at.Z);
                var rise = Buildings.Rise(kinds[i], i + 3, at, yaw);
                if (rise < best.Rise - 0.3f) best = (rise, at, yaw);
            }
            if (best.Rise > 3.5f) continue;
            Buildings.Place(this, kinds[i], best.At with { Y = City.FloorAt(best.At) }, best.Yaw, i + 3, roof);
        }
        var panels = (int)(Mathf.Tau * CompoundFence / FencePanel);
        for (var i = 0; i < panels; i++)
        {
            var a = gate + (i + 0.5f) / panels * Mathf.Tau;
            if (Math.Abs(Mathf.Wrap(a - gate, -Mathf.Pi, Mathf.Pi)) < 0.22f) continue; // the gate, standing open
            var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * CompoundFence;
            Models.Place(this, "medieval/neutral/fence_wood_straight.gltf", at with { Y = City.FloorAt(at) }, 1.8f, -a + Mathf.Pi / 2);
        }
    }

    /// <summary>The places near enough for a guild's people to reach: the ones the wagons go to.</summary>
    IEnumerable<TownMarket> JobTowns => _trade.Towns.Values.OrderBy(m => m.DistanceTo(0, 0)).Take(6);

    /// <summary>Anyone can talk business with a guild's leader.</summary>
    IEnumerable<(string, Action)> HireTalk(Person person)
    {
        if (Guilds.Member(person.Npc.Id) is not { Role: GuildRole.Leader } || Guilds.OfMember(person.Npc.Id) is not { } g) yield break;
        yield return ($"I'd like to hire {g.Name} for a job.", () => Hire(person, g));
    }

    void Hire(Person person, Guild g)
    {
        var options = new List<(string, Action)>();
        switch (g.Calling)
        {
            case Calling.Warrior:
                foreach (var t in _city.Roads.Troubles.Take(5))
                    options.Add(($"Clear {(t.Clan ? t.By : "the bandits")} off the road to {t.TownName} ({t.Bounty * 2} crowns)", () => TakeJob(person, t.Bounty * 2, () => DoCity(CityAction.Cleared(t.Id, g.Name)), $"It's done. The road to {t.TownName} is open.")));
                break;
            case Calling.Rogue:
                Towns(options, person, 150, "Rob the stores at", town => { DoCity(CityAction.Plundered(town.Id)); _player.Crowns += 200; }, town => $"We were in and out of {town.Name} by dawn. Your cut's in your purse: 200 crowns.");
                foreach (var (town, id, name) in Sitting().Take(6))
                    options.Add(($"A quiet killing: {name}, of the council of {town} (600 crowns)", () => TakeJob(person, 600, () => Kill(town, id, name), $"You'll hear about it.")));
                break;
            case Calling.Mage:
                Towns(options, person, 100, "Steady the market at", town => DoCity(CityAction.Steadied(town.Id)), town => $"{town.Name}'s prices will settle.");
                break;
            case Calling.Bard:
                Towns(options, person, 80, "Sing your name in", town => Repute.Deed(3, town.Id), town => $"They'll be humming it in {town.Name} by the end of the week.");
                Towns(options, person, 200, "Blacken the name of", town => DoCity(CityAction.TownName(town.Id, -1.5)), town => $"A verse or two about {town.Name}, and its neighbours won't look at it the same way.");
                break;
            case Calling.Artificer:
                foreach (var town in JobTowns.Where(t => _city.FabricOf(t.Id).Derelict > 0))
                    options.Add(($"Mend the empty homes in {town.Name} (120 crowns)", () => TakeJob(person, 120, () => DoCity(CityAction.Mended(town.Id)), $"We'll have a home in {town.Name} sound again by nightfall.")));
                break;
            default:
                Towns(options, person, 120, "Bless the fields at", town => DoCity(CityAction.TownStock(town.Id, Good.Bread, 30)), town => $"{town.Name} will have a good harvest.");
                break;
        }
        if (options.Count == 0) { Say(person, "Nothing we could do for you just now. The wagons don't go anywhere that needs it."); return; }
        options.Add(("Not today.", () => Say(person, "You know where we are.")));
        _hud.Talk($"Hiring {g.Name} ({_player.Crowns} crowns)", "Crowns first. It's done when it's done, and we don't ask why.", [.. options]);
    }

    void Towns(List<(string, Action)> options, Person person, int price, string what, Action<TownMarket> does, Func<TownMarket, string> said)
    {
        foreach (var town in JobTowns)
            options.Add(($"{what} {town.Name} ({price} crowns)", () => TakeJob(person, price, () => does(town), said(town))));
    }

    void TakeJob(Person person, int price, Action does, string said)
    {
        if (_player.Crowns < price) { Sound.Ui("error"); Say(person, "Come back with the crowns."); return; }
        _player.Crowns -= price;
        does();
        Sound.Ui("coin");
        Save();
        Say(person, said);
    }

    /// <summary>Every living member of a council the wagons reach.</summary>
    IEnumerable<(string Town, string Id, string Name)> Sitting() =>
        _city.Councils.Where(c => _trade.Towns.ContainsKey(c.Key) || c.Key == ReputationBook.Capital)
            .SelectMany(c => c.Value.Members.Where(m => !c.Value.Fallen.Contains(m.Key)).Select(m => (c.Key, m.Key, m.Value)))
            .Select(x => (x.Item1, x.Item2, x.Item3));

    /// <summary>The Knives' knife: most nights it goes in clean, and now and then it comes back to whoever paid.</summary>
    void Kill(string town, string id, string name)
    {
        if (_lootRng.NextDouble() < 0.7) { DoCity(CityAction.CouncilFall(town, id)); return; }
        Repute.Deed(-5, town);
        _hud.Toast($"The knife missed {name}, and your name came up in the questioning.", 8);
    }
}
