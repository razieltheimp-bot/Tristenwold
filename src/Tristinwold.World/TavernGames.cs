using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>A playing card: rank 1 (ace) to 13 (king), and one of the four suits.</summary>
public sealed record Card(int Rank, int Suit)
{
    static readonly string[] Suits = ["coins", "cups", "swords", "staves"];
    static readonly string[] Ranks = ["", "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Knight", "Queen", "King"];

    public string Name => $"{Ranks[Rank]} of {Suits[Suit]}";
    public int Points => Rank == 1 ? 11 : Math.Min(Rank, 10);
}

public enum TableResult { Playing, Won, Natural, Push, Lost }

/// <summary>Twenty-One, as they play it in every tavern: get closer to 21 than the house without going over.</summary>
public sealed class TwentyOne
{
    readonly List<Card> _deck;

    public int Stake { get; }
    public List<Card> Hand { get; } = [];
    public List<Card> House { get; } = [];
    public TableResult Result { get; private set; }

    public TwentyOne(int stake, Rng rng)
    {
        Stake = stake;
        _deck = [.. Enumerable.Range(0, 4).SelectMany(s => Enumerable.Range(1, 13).Select(r => new Card(r, s))).OrderBy(_ => rng.Next(int.MaxValue))];
        (Hand, House) = ([Draw(), Draw()], [Draw(), Draw()]);
        if (Value(Hand) == 21) Finish();
    }

    Card Draw()
    {
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }

    /// <summary>A hand's points: aces count 11, or 1 if 11 would break it.</summary>
    public static int Value(IEnumerable<Card> hand)
    {
        var cards = hand.ToList();
        var total = cards.Sum(c => c.Points);
        for (var aces = cards.Count(c => c.Rank == 1); total > 21 && aces > 0; aces--) total -= 10;
        return total;
    }

    public void Hit()
    {
        if (Result != TableResult.Playing) return;
        Hand.Add(Draw());
        if (Value(Hand) > 21) Result = TableResult.Lost;
        else if (Value(Hand) == 21) Stand();
    }

    public void Stand()
    {
        if (Result == TableResult.Playing) Finish();
    }

    void Finish()
    {
        while (Value(House) < 17) House.Add(Draw());
        var (mine, theirs) = (Value(Hand), Value(House));
        var natural = mine == 21 && Hand.Count == 2;
        Result = natural && !(theirs == 21 && House.Count == 2) ? TableResult.Natural
            : theirs > 21 || mine > theirs ? TableResult.Won
            : mine == theirs ? TableResult.Push : TableResult.Lost;
    }

    /// <summary>What comes back to the table at the end: the stake and winnings, the stake alone on a tie, or nothing.</summary>
    public int Returned => Result switch
    {
        TableResult.Natural => Stake + Stake * 3 / 2,
        TableResult.Won => Stake * 2,
        TableResult.Push => Stake,
        _ => 0,
    };
}

/// <summary>Doubles: throw two dice and hope they match. One throw in six does; the house keeps a little edge.</summary>
public static class Doubles
{
    /// <summary>What a matching pair pays, on top of the stake coming back.</summary>
    public const int Odds = 4;

    public static (int A, int B) Throw(Rng rng) => (1 + rng.Next(6), 1 + rng.Next(6));
    public static int Returned(int stake, (int A, int B) dice) => dice.A == dice.B ? stake * (1 + Odds) : 0;
}

/// <summary>What a night in a tavern is worth to a Bard, and what the tables will take.</summary>
public static class Tavern
{
    public static readonly IReadOnlyList<int> Stakes = [5, 25, 100];
    public static readonly IReadOnlyList<string> Bar = ["ale", "cider", "mead", "spiced_wine", "bread", "meat_pie", "hearty_stew"];
    /// <summary>Seconds of play before the same crowd will pay for another set.</summary>
    public const double SetEvery = 180;

    /// <summary>The tips for a set: the better the player and the better they're liked, the more the room gives.</summary>
    public static int Tips(int level, double view, Rng rng) => Math.Max(1, (int)Math.Round((6 + level * 2 + rng.Next(8)) * ReputationBook.SellFactor(view * 3)));
}
