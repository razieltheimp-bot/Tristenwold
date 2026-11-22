using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Shared;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Refuges and colonies growing into towns (see <see cref="Settling"/>): any of a refuge's people, or a colony's reeve,
/// will say what the place needs next, and take it when a hero brings it; the place is built up a step at a time, a
/// tavern, a market, a council chamber, walls. And every town's council meets in its chamber: the councillors inside,
/// the clerk at the door.
/// </summary>
public partial class Main
{
    /// <summary>The council chambers by the seed on their door: the town each one belongs to.</summary>
    public readonly Dictionary<int, string> CouncilChambers = [];
    readonly List<Person> _councilFolk = [];

    /// <summary>What a refuge's people can be asked about building the place up.</summary>
    IEnumerable<(string, Action)> GrowthTalk(Person person)
    {
        if (_world.Find(person.Npc.HomeId) is not { Kind: SettlementKind.AshbornCamp, Hostile: false } s || TownCouncils.IsMember(person.Npc.Id)) yield break;
        foreach (var o in GrowthOptions(person, s.Id, s.Name)) yield return o;
    }

    /// <summary>The next step for a place, and handing over what it takes.</summary>
    IEnumerable<(string, Action)> GrowthOptions(Person person, string town, string name)
    {
        var stage = _city.GrownOf(town);
        if (Settling.Next(stage) is not { } step)
        {
            yield return ($"How is {name} doing?", () => Say(person, $"{name} has everything a town needs now: a tavern, a market, a council, walls. Thanks to you, mostly."));
            yield break;
        }
        string Needs() => string.Join(", ", step.Needs.Select(n => $"{Items.Count(n.Id, n.N)} ({_player.Pack.Count(n.Id)})")) + $", and {step.Crowns} crowns";
        yield return ($"What does {name} need?", () => Say(person,
            $"{(stage == 0 ? "We've tents and a fire, and not much else. " : "")}Next we want {step.Name}. {step.Gives} It takes {Needs()}. Bring it, and we'll build."));
        var ready = step.Needs.All(n => _player.Pack.Count(n.Id) >= n.N) && _player.Crowns >= step.Crowns;
        // On a server the place is everyone's, so it's built there or not at all: with the server out of reach, nothing is
        // taken, rather than built here and then lost when the server's copy comes back.
        if (ready && _client is not null && !(_sharedCity && _client.Status == ClientStatus.Connected))
            yield return ($"I've brought what {step.Name} takes.", () => Say(person,
                $"Keep hold of it a while. We can't get word to the rest of the world just now (the server: {_client.Status}), and {step.Name} has to go up there too, or it won't stand. Once you're back in touch, bring it, and we'll build."));
        else if (ready)
            yield return ($"I've brought what {step.Name} takes.", () =>
            {
                foreach (var (id, n) in step.Needs) _player.Pack.Take(id, n);
                _player.Crowns -= step.Crowns;
                DoCity(CityAction.Grew(town, step.Stage));
                if (_client is not null) _awaitingBuilds.Add((town, name, step, _clock + BuildWait));
                Repute.Deed(4, Settling.IsColony(town) ? null : town);
                GainXp(60 + 20 * step.Stage);
                Sound.Ui("quest");
                _hud.EndTalk();
                _hud.Toast($"{name} raises {step.Name}. {step.Gives}", 9);
                Redraw(town);
                Save();
            });
    }

    /// <summary>A place drawn again as it now stands.</summary>
    void Redraw(string town)
    {
        if (_world.Find(town) is { } s) _places.Rebuild(s.Id); // up it goes
        else if (_city.Halls.Values.FirstOrDefault(h => h.Kind == HallKind.Colony && h.Town == town) is { } grown && _hallFronts.Remove(grown.Key, out var front)) front.Root.QueueFree(); // a colony is drawn again, grown
    }

    /// <summary>Buildings handed over on a server, waiting for the server's city to show them up: and when to give up waiting.</summary>
    readonly List<(string Town, string Name, GrowthStep Step, double GiveUpAt)> _awaitingBuilds = [];
    const double BuildWait = 20;

    /// <summary>
    /// With the server's city in: a building it shows standing is done with; one it still doesn't, well after it was
    /// handed over, never reached it, and what it took goes back to the hero.
    /// </summary>
    void CheckBuilds()
    {
        for (var i = _awaitingBuilds.Count - 1; i >= 0; i--)
        {
            var (town, name, step, giveUpAt) = _awaitingBuilds[i];
            if (_city.GrownOf(town) >= step.Stage) { _awaitingBuilds.RemoveAt(i); continue; }
            if (_clock < giveUpAt) continue; // the server may not have had it yet
            _awaitingBuilds.RemoveAt(i);
            foreach (var (id, n) in step.Needs) _player.Pack.Add(id, n);
            _player.Crowns += step.Crowns;
            var said = $"Word never reached the rest of the world, and {step.Name} didn't stand at {name}. The builders hand back what you brought: {string.Join(", ", step.Needs.Select(n => Items.Count(n.Id, n.N)))}, and {step.Crowns} crowns.";
            _hud.Toast(said, 12);
            _hud.Log(said);
            Redraw(town);
            Save();
        }
    }

    /// <summary>For the self-test: as if the server had long since had its chance.</summary>
    public void OverdueBuildsForTest()
    {
        for (var i = 0; i < _awaitingBuilds.Count; i++) _awaitingBuilds[i] = _awaitingBuilds[i] with { GiveUpAt = double.MinValue };
    }

    // ---- the council chamber ----------------------------------------------------------------------------------------------

    /// <summary>The council inside its chamber, at the table: those still living, as the town's council has them.</summary>
    public void PeopleTheCouncil(Indoors room, int seed)
    {
        _councilFolk.Clear();
        if (!CouncilChambers.TryGetValue(seed, out var town) || _world.Find(town) is not { } s) return;
        var council = _city.CouncilOf(s.Id);
        var years = Calendar.Years(_city.Day);
        var stage = Indoors.StageAt;
        var i = 0;
        foreach (var m in TownCouncils.Members(s).Where(m => !council.Fallen.Contains(m.Id)))
        {
            var side = i % 2 == 0 ? -1 : 1;
            var at = stage + new Vector3(side * 2.1f, 0, -3.2f + i / 2 * 2.4f);
            var npc = m with { Age = (int)(m.Age + years), X = at.X, Z = at.Z };
            var p = MeetAt(npc, "priest", stage.Y);
            p.Face(stage + new Vector3(0, 0, -3.2f + i / 2 * 2.4f));
            _councilFolk.Add(p);
            i++;
        }
    }

    public void LeftTheCouncil()
    {
        foreach (var p in _councilFolk.Where(IsInstanceValid)) Dismiss(p);
        _councilFolk.Clear();
    }

    /// <summary>For the self-test.</summary>
    public IReadOnlyList<Person> CouncilFolkForTest => _councilFolk;
}
