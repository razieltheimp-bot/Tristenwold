using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A place's council, seen from the street: who sits on it, how a hero wins a seat by the good they've done there, how
/// they talk the others into a raid, and what's theirs to do when they're the only one left at the table.
/// </summary>
public partial class Main
{
    TownCouncil CouncilThere(Person person) => _city.CouncilOf(person.Npc.HomeId);

    /// <summary>What a councillor, or the council's clerk, can be asked.</summary>
    IEnumerable<(string, Action)> CouncilTalk(Person person)
    {
        if (!TownCouncils.IsMember(person.Npc.Id)) yield break;
        var town = person.Npc.HomeId;
        var name = person.Npc.HomeName;
        var council = _city.CouncilOf(town);
        var me = _player.Company;
        yield return ("Who sits on the council here?", () => Say(person, CouncilSays(town, name, council)));
        if (council.Ruler == me)
        {
            yield return ($"Open the town's purse ({council.Purse} crowns).", () => TakePurse(person, town));
            yield return ("Send out raiders.", () => ChooseRaid(person, town, name, sure: true));
            yield break;
        }
        if (!council.Heroes.Contains(me))
            yield return ("I'd like to speak to the council about joining it.", () => AskForSeat(person, town, name));
        else
            yield return ("I want the council to send out raiders.", () => ChooseRaid(person, town, name, sure: false));
        if (person.Npc.Id.EndsWith(TownCouncils.ClerkId) || !council.Members.ContainsKey(person.Npc.Id)) yield break;
        yield return ($"Draw on {person.Npc.First}, to the death.", () => CouncilBlood(person, town, name));
    }

    string CouncilSays(string town, string name, TownCouncil council)
    {
        var seats = council.Living.Concat(council.Heroes).Concat(council.Warlord is { } w ? [w.Name] : []).ToList();
        var who = council.Ruler is { } ruler ? $"{ruler} rules {name} now: the only one of the council left."
            : council.Warlord is { Rules: true } master ? $"{master.Name} rules {name} now. The rest of the council are dead, one after another, and nobody says how.{(master.Against is { } hero ? $" It says it came for {hero}." : "")}"
            : seats.Count == 0 ? $"The council of {name} is an empty table."
            : $"The council of {name}: {string.Join(", ", seats)}.{(council.Warlord is { } schemer ? $" {schemer.Name} is new to the table, and councillors keep dying since it came." : "")}";
        var holds = council.Holds.Count > 0 ? $" It holds {string.Join(", ", council.Holds.Select(h => _city.CouncilOf(h).Name is { Length: > 0 } n ? n : h))}." : "";
        var over = _city.Councils.FirstOrDefault(c => c.Value.Holds.Contains(town)) is { Value: not null } lord ? $" It answers to {lord.Value.Name}." : "";
        var name_ = council.Standing <= CityEconomy.BadName ? " Its neighbours are talking of what to do about it." : council.Standing < 0 ? " Its name is none too good with its neighbours." : "";
        return who + holds + over + name_;
    }

    /// <summary>A hero asks for a seat: they have to be well thought of to be heard, and to have done enough good here to be given one.</summary>
    void AskForSeat(Person person, string town, string name)
    {
        if (_city.CouncilOf(town).Warlord is { } lord) { Say(person, $"Not while {lord.Name} sits at the table. It doesn't share. Ask again when it's gone, if you're brave enough to make it go."); return; }
        var heard = Repute.Town(town);
        if (heard < CityEconomy.SpeakAt) { Say(person, $"With respect, nobody in {name} knows enough about you to hear you out."); return; }
        if (Repute.Own(town) < CityEconomy.SeatAt) { Say(person, $"They've heard good of you. But the council wants to see it done here, in {name}, not told about from the road. Do more good here, and ask again."); return; }
        DoCity(CityAction.CouncilSeat(town, _player.Company));
        Repute.Deed(2, town);
        Sound.Ui("quest");
        Save();
        Say(person, $"It's agreed. There's a seat for you at the table in {name}. Try not to break it.");
    }

    void TakePurse(Person person, string town)
    {
        var purse = _city.CouncilOf(town).Purse;
        if (purse <= 0) { Say(person, "There's nothing in it. Yet."); return; }
        _player.Crowns += purse;
        DoCity(CityAction.CouncilPurse(town, _player.Company));
        Sound.Ui("coin");
        Save();
        Say(person, $"{purse} crowns. I've written it down, as I write everything down.");
    }

    /// <summary>Where to send raiders, and what for. A ruler just gives the word; a seat at the table has to carry the others.</summary>
    void ChooseRaid(Person person, string town, string name, bool sure)
    {
        if (!_city.MayRaid(town)) { Say(person, "They've not long been out. Nobody's going back on the road this week."); return; }
        var options = _city.Neighbours(town).Take(6).Select(id =>
        {
            var there = _city.CouncilOf(id).Name is { Length: > 0 } n ? n : _trade.Towns.GetValueOrDefault(id)?.Name ?? id;
            return ($"Raid {there}", (Action)(() => RaidFor(person, town, name, id, there, sure)));
        }).ToList();
        options.Add(("Not today.", () => Say(person, "As you say.")));
        _hud.Talk($"The raiders of {name}", "Towns remember who raided them, and a bad enough name brings the neighbours down on you together.", [.. options]);
    }

    void RaidFor(Person person, string town, string name, string on, string there, bool sure)
    {
        (string, Action) Aim(string text, RaidAim aim) => (text, () =>
        {
            if (!sure && _lootRng.NextDouble() > Math.Clamp(Repute.Town(town) / 5, 0.15, 0.9))
            {
                Say(person, $"The council heard you out, and won't have it. {there} is left in peace, for now.");
                return;
            }
            DoCity(CityAction.TownRaid(town, on, aim, _player.Company));
            Repute.Deed(-3, on);
            Save();
            Say(person, _city.News.LastOrDefault() ?? "They've gone.");
        });
        _hud.Talk($"Raid {there}", "What for?",
        [
            Aim("Their stores: bring back what we can carry.", RaidAim.Supplies),
            Aim("A tribute: they trade on our terms.", RaidAim.Trade),
            Aim("The place itself: it answers to us now.", RaidAim.Land),
            ("Leave it.", () => Say(person, "As you say.")),
        ]);
    }

    /// <summary>A councillor can be killed like anyone. The town doesn't forget it.</summary>
    void CouncilBlood(Person person, string town, string name) =>
        _hud.Talk($"{person.Npc.Name}, of the council of {name}", $"Kill a councillor of {name}, here, in the open? Win or lose, the whole town will know.",
        [
            ("Do it.", () => StartBloodDuel(person, null)),
            ("No.", () => Say(person, "Was there something else?")),
        ]);

    /// <summary>A blood duel ended: a councillor beaten is dead, and a hero beaten limps away.</summary>
    void CouncilDuelOver(Enemy foe, Person rival, bool won)
    {
        var town = rival.Npc.HomeId;
        Repute.Deed(-6, town);
        Repute.Harmed(rival.Npc.Id, rival.Npc.Name);
        GetTree().CreateTimer(2.5).Timeout += () =>
        {
            if (IsInstanceValid(foe)) foe.QueueFree();
            if (!IsInstanceValid(rival)) return;
            if (won) rival.QueueFree();
            else { rival.Visible = true; rival.CollisionLayer = 1; }
        };
        if (!won)
        {
            _player.Stats.Heal(_player.Stats.MaxHealth * 0.3);
            _hud.Toast($"{rival.Npc.First} lets you live, and tells everyone.", 6);
            return;
        }
        DoCity(CityAction.CouncilFall(town, rival.Npc.Id));
        _hud.Toast(_city.CouncilOf(town).Ruler == _player.Company ? $"You're the last of the council left standing. {rival.Npc.HomeName} is yours." : $"{rival.Npc.Name} is dead.", 8);
    }
}
