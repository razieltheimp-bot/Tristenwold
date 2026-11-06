using Godot;
using Tristinwold.Gameplay;
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
        if (ready)
            yield return ($"I've brought what {step.Name} takes.", () =>
            {
                foreach (var (id, n) in step.Needs) _player.Pack.Take(id, n);
                _player.Crowns -= step.Crowns;
                DoCity(CityAction.Grew(town, step.Stage));
                Repute.Deed(4, Settling.IsColony(town) ? null : town);
                GainXp(60 + 20 * step.Stage);
                Sound.Ui("quest");
                _hud.EndTalk();
                _hud.Toast($"{name} raises {step.Name}. {step.Gives}", 9);
                if (_world.Find(town) is { } s) _places.Rebuild(s.Id); // up it goes
                else if (_city.Halls.Values.FirstOrDefault(h => h.Kind == HallKind.Colony && h.Town == town) is { } grown && _hallFronts.Remove(grown.Key, out var front)) front.Root.QueueFree(); // a colony is drawn again, grown
                Save();
            });
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
