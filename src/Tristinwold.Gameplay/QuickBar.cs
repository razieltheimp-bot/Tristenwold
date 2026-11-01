namespace Tristinwold.Gameplay;

/// <summary>
/// The ten quick keys. 1 to 5 hold moves: the class's own to begin with, and the moves of any other class the hero
/// has learned. 6 to 0 hold what's used up: potions, food and the like. Saved with the hero, and swapped about as
/// they learn more.
/// </summary>
public sealed class QuickBar
{
    public const int MoveSlots = 5, Size = 10;

    /// <summary>What's on each key: a move's id on the first five, an item's on the rest.</summary>
    public string?[] Slots { get; set; } = new string?[Size];

    /// <summary>The class the bar was last laid out for.</summary>
    public string Way { get; set; } = "";

    /// <summary>What the hero took off the bar: it isn't put back for them.</summary>
    public HashSet<string> Left { get; set; } = [];

    /// <summary>The key a slot answers to: the tenth is 0, as it is on the keyboard.</summary>
    public static string Key(int slot) => slot == Size - 1 ? "0" : $"{slot + 1}";

    public static bool ForMoves(int slot) => slot < MoveSlots;

    static Ability[] Own(Discipline d) => [d.Primary, d.Signature, d.Utility, d.Limit];

    /// <summary>
    /// Every move that can go on the bar: the class's own, then those of the classes learned before. A limit break
    /// belongs to the class it was learned in: it runs on that class's gauge, so another's can't be carried over.
    /// </summary>
    public static IReadOnlyList<Ability> Moves(Hero hero) =>
        Own(Classes.Get(hero.Discipline))
            .Concat(Advances.Known(hero.Discipline, hero.Level).Where(a => a.Style != MoveStyle.Passive).Select(a => a.Move))
            .Concat(hero.Classes.Skip(1).Select(Classes.Get).SelectMany(c => new[] { c.Primary, c.Signature, c.Utility }))
            .DistinctBy(a => a.Id).ToList();

    /// <summary>
    /// Keeps the bar true to the hero: lays it out the first time, hands a new class's moves the keys the old class's
    /// held, drops what can't be used, and gives a free key to anything not yet on the bar.
    /// </summary>
    public void Arrange(Hero hero)
    {
        if (Slots.Length != Size) Slots = [.. Slots.Take(Size).Concat(new string?[Size]).Take(Size)];
        var now = Own(Classes.Get(hero.Discipline));
        if (Way.Length == 0) for (var i = 0; i < now.Length; i++) Slots[i] = now[i].Id;
        else if (Way != hero.Discipline)
        {
            var was = Own(Classes.Get(Way));
            for (var i = 0; i < was.Length; i++)
                if (Array.IndexOf(Slots, was[i].Id) is >= 0 and var at) Slots[at] = now[i].Id;
        }
        Way = hero.Discipline;
        var moves = Moves(hero);
        for (var slot = 0; slot < MoveSlots; slot++)
            if (moves.All(a => a.Id != Slots[slot])) Slots[slot] = null;
        foreach (var a in moves)
            if (!Left.Contains(a.Id) && !Slots.Contains(a.Id) && Array.IndexOf(Slots, null, 0, MoveSlots) is >= 0 and var free) Slots[free] = a.Id;
    }

    /// <summary>Gives each potion a free key, unless the hero has taken it off before.</summary>
    public void Stock(IEnumerable<string> potions)
    {
        foreach (var id in potions)
            if (!Left.Contains(id) && !Slots.Contains(id) && Array.IndexOf(Slots, null, MoveSlots) is >= 0 and var free) Slots[free] = id;
    }

    /// <summary>Puts something on a key (or clears it). What's already on the bar swaps places with what was there.</summary>
    public void Set(int slot, string? id)
    {
        var was = Slots[slot];
        var other = id is null ? -1 : Array.IndexOf(Slots, id);
        if (other == slot) return;
        Slots[slot] = id;
        if (other >= 0) Slots[other] = was;
        else if (was is not null) Left.Add(was);
        if (id is not null) Left.Remove(id);
    }
}
