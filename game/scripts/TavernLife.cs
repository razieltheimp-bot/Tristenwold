using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>A night in a tavern: the bar, Twenty-One, doubles, and for a Bard, a set by the fire with a hat out.</summary>
public partial class Main
{
    double _nextSet;
    TwentyOne? _hand;

    public TwentyOne? TavernHand => _hand;

    /// <summary>For the self-test.</summary>
    public Indoors EnterTavernForTest()
    {
        GoInside("tavern", 3, _player.GlobalPosition);
        return _indoors!;
    }

    public void LeaveTavernForTest() => GoOutside();
    public void ShowBarForTest() => ShowBar();

    public void AbandonCaravanForTest()
    {
        _trade.Guarding = null;
        _guarded?.QueueFree();
        _guarded = null;
    }

    public (string Verb, Action Act)? ActionForTest() => Nearest();

    public void StartCaravanForTest(bool driver)
    {
        var e = _trade.ShortRoad(_city.Day)!;
        e.Driver = driver;
        BeginCaravan(Locals.First(p => p.Npc.Id == Master), e);
        _hud.EndTalk();
    }

    (string Verb, Action Act)? TavernAction(Vector3 pos)
    {
        if (_indoors is not { IsTavern: true } room) return null;
        if (pos.DistanceTo(room.Bar) < 3) return ("order at the bar", ShowBar);
        if (pos.DistanceTo(room.CardTable) < 2.4f) return ("sit down to Twenty-One", () => PickStake("Twenty-One", "Closer to 21 than the house, without going over. Pays even money, three to two on a natural 21.", DealCards));
        if (pos.DistanceTo(room.DiceTable) < 2.4f) return ("throw for doubles", () => PickStake("Doubles", $"Two dice. A matching pair pays {Doubles.Odds} to 1.", ThrowDice));
        if (_player.Class.Calling == Calling.Bard && pos.DistanceTo(room.Fireside) < 3.2f) return ("play a set by the fire", PlaySet);
        return null;
    }

    void ShowBar()
    {
        var view = Repute.Town(TownHere(_player.GlobalPosition) ?? ReputationBook.Capital);
        var options = Tavern.Bar.Select(id =>
        {
            var item = Items.Get(id)!;
            var price = Shops.Charge(item.Value, ShopMarkup * ReputationBook.BuyFactor(view));
            return ($"{item.Name}, {price} crowns: {item.Text}", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                _player.Crowns -= price;
                _player.Pack.Add(id);
                Sound.Ui("coin");
                _hud.Toast($"{item.Name}. ({_player.Crowns} crowns left)", 3);
                Save();
                ShowBar();
            }));
        }).ToList();
        if (TavernHome() is { } home) options.Add(home);
        options.Add(("That's all.", _hud.EndTalk));
        _hud.Talk("The bar", "\"What'll it be?\"", [.. options]);
    }

    void PickStake(string game, string rules, Action<int> play)
    {
        var options = Tavern.Stakes.Where(s => _player.Crowns >= s).Select(s => ($"Play for {s} crowns", (Action)(() => play(s)))).Append(("Leave the table.", (Action)_hud.EndTalk)).ToArray();
        _hud.Talk(game, $"{rules} You've {_player.Crowns} crowns.{(options.Length == 1 ? " Not enough to sit down." : "")}", options);
    }

    void DealCards(int stake)
    {
        _player.Crowns -= stake;
        _hand = new TwentyOne(stake, _lootRng);
        Sound.Ui("pickup");
        ShowHand();
    }

    static string Named(IEnumerable<Card> cards) => string.Join(", ", cards.Select(c => c.Name));

    void ShowHand()
    {
        var h = _hand!;
        var mine = $"You: {Named(h.Hand)} ({TwentyOne.Value(h.Hand)}).";
        if (h.Result == TableResult.Playing)
        {
            _hud.Talk("Twenty-One", $"{mine}\nThe house shows the {h.House[0].Name}.",
                ("Another card.", () => { h.Hit(); Sound.Ui("pickup"); ShowHand(); }),
                ("I'll stand.", () => { h.Stand(); ShowHand(); }));
            return;
        }
        _player.Crowns += h.Returned;
        var told = h.Result switch
        {
            TableResult.Natural => $"A natural 21! +{h.Returned - h.Stake} crowns.",
            TableResult.Won => $"Yours. +{h.Stake} crowns.",
            TableResult.Push => "A tie. Your stake comes back.",
            _ => TwentyOne.Value(h.Hand) > 21 ? $"Bust. -{h.Stake} crowns." : $"The house has it. -{h.Stake} crowns.",
        };
        Sound.Ui(h.Returned > h.Stake ? "coin" : "error");
        Save();
        var stake = h.Stake;
        var again = new List<(string, Action)> { ("Leave the table.", _hud.EndTalk) };
        if (_player.Crowns >= stake) again.Insert(0, ($"Again, for {stake} crowns.", () => DealCards(stake)));
        _hud.Talk("Twenty-One", $"{mine}\nThe house: {Named(h.House)} ({TwentyOne.Value(h.House)}).\n{told} You've {_player.Crowns} crowns.", [.. again]);
    }

    void ThrowDice(int stake)
    {
        var dice = Doubles.Throw(_lootRng);
        var back = Doubles.Returned(stake, dice);
        _player.Crowns += back - stake;
        Sound.Ui(back > 0 ? "coin" : "error");
        Save();
        var again = new List<(string, Action)> { ("Leave the table.", _hud.EndTalk) };
        if (_player.Crowns >= stake) again.Insert(0, ($"Again, for {stake} crowns.", () => ThrowDice(stake)));
        _hud.Talk("Doubles", $"The dice stop on {dice.A} and {dice.B}. {(back > 0 ? $"Doubles! +{back - stake} crowns." : $"-{stake} crowns.")} You've {_player.Crowns} crowns.", [.. again]);
    }

    /// <summary>A Bard's set: the room goes quiet, then the hat goes round. The same crowd won't pay again for a while.</summary>
    void PlaySet()
    {
        var wait = _nextSet - _guilds.PlaySeconds;
        if (wait > 0) { _hud.Toast($"This crowd's heard you already. Give them {Math.Ceiling(wait / 60)} minutes.", 4); return; }
        _nextSet = _guilds.PlaySeconds + Tavern.SetEvery;
        Hands()?.Perform("Cheer", 2.2);
        var tips = Tavern.Tips(_player.Hero.Level, Repute.Town(TownHere(_player.GlobalPosition) ?? ReputationBook.Capital), _lootRng);
        _player.Crowns += tips;
        WellDone(1);
        Sound.Ui("coin");
        _hud.Toast($"You play the room quiet, then loud, and the hat goes round: +{tips} crowns.", 6);
        Save();
    }
}
