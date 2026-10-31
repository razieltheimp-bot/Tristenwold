using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>What people think of the hero (see <see cref="ReputationBook"/>): what's done gets told where it's done, and the meter shows how it stands here.</summary>
public partial class Main
{
    ReputationBook Repute => _standing.Reputation;
    double _lastRepute;

    public ReputationBook ReputationLog => _standing.Reputation;

    /// <summary>How someone here regards the hero: what they think, and what their town does.</summary>
    public double RegardOf(Person person) => Repute.Regard(TownHere(person.GlobalPosition), person.Npc.Id);

    /// <summary>What someone asks and pays the hero, as a share of the usual, and the same for a town's market.</summary>
    public double RepBuy(Person person) => ReputationBook.BuyFactor(RegardOf(person));
    public double RepSell(Person person) => ReputationBook.SellFactor(RegardOf(person));
    double RepBuyAt(string place) => ReputationBook.BuyFactor(Repute.Town(place));
    double RepSellAt(string place) => ReputationBook.SellFactor(Repute.Town(place));

    /// <summary>Whether someone will still serve the hero. If not, they say so, and it stops there.</summary>
    bool Serves(Person person)
    {
        if (!ReputationBook.ShutsDoor(RegardOf(person))) return true;
        Sound.Ui("error");
        Say(person, "Your coin's no good here. Not after what I've heard. Get out, before I send for someone.");
        return false;
    }

    /// <summary>The town whose ground this is, if any; the wilds have none.</summary>
    public string? TownHere(Vector3 at)
    {
        if (City.Afloat is not null) return null;
        if (TownWatch.InTristinwold(at)) return ReputationBook.Capital;
        return _world.SettlementAt(at.X, at.Z) is { Hostile: false, Id: var id } ? id : null;
    }

    /// <summary>Whatever goes in the hero's deeds, good or bad, goes round the town they did it in.</summary>
    void OnDeed(int size, string deed)
    {
        Repute.Deed(size, TownHere(_player.GlobalPosition));
        ShowRepute();
    }

    /// <summary>Good honest work done in a town, that doesn't make a deed of its own.</summary>
    void WellDone(int size) => OnDeed(size, "");

    /// <summary>A trip's trade with a town: the country's talk reaches it, and it may think differently of the hero for it.</summary>
    void TradedAt(string place, string name)
    {
        var moved = Repute.Trip(place);
        if (Math.Abs(moved) >= 0.2) _hud.Toast(moved < 0 ? $"The talk on the road has reached {name}, and it doesn't flatter you. They think less of you here." : $"The talk on the road has reached {name}, and it does you credit. They think better of you here.", 7);
        ShowRepute();
    }

    void TendRepute()
    {
        if (_clock - _lastRepute < 0.5) return;
        _lastRepute = _clock;
        var marked = _player.Pack.WornIn(Slot.Weapon)?.Id == Legend.Arachnicalibur;
        if (marked != Repute.Marked) _hud.Toast(marked ? "The strands of the Web pull tight round your hand. However good you've been, nobody who sees that blade thinks so now." : "The black blade's put away, and the Web lets go of you. People remember who you were.", 8);
        Repute.Marked = marked;
        ShowRepute();
        TendStone();
    }

    void ShowRepute()
    {
        var town = TownHere(_player.GlobalPosition);
        _hud.ShowReputeMeter(town is null ? Repute.CountryView : Repute.Town(town), town is null ? "the country" : _world.Find(town)?.Name ?? "here");
        _hud.ShowRepute(RepSheet());
    }

    /// <summary>The whole of it, for the Reputation tab.</summary>
    string RepSheet()
    {
        var lines = new List<string> { $"THE COUNTRY  {ReputationBook.Number(Repute.CountryView)}: {ReputationBook.Word(Repute.CountryView)}", "", "TOWNS" };
        var towns = Repute.Towns.Keys.Concat(Repute.Links.Keys).Append(ReputationBook.Capital).Distinct().OrderByDescending(Repute.Town);
        lines.AddRange(towns.Select(t => $"   {_world.Find(t)?.Name ?? t}  {ReputationBook.Number(Repute.Town(t))}: {ReputationBook.Word(Repute.Town(t))}{(Repute.Link(t) >= 0.75 ? " (well connected)" : Repute.Link(t) <= 0 ? " (out of touch)" : "")}"));
        if (Repute.People.Count > 0)
        {
            lines.AddRange(["", "PEOPLE"]);
            lines.AddRange(Repute.People.OrderByDescending(p => p.Value).Select(p => $"   {Repute.Names.GetValueOrDefault(p.Key, p.Key)}  {ReputationBook.Number(p.Value)}"));
        }
        return string.Join("\n", lines);
    }
}
