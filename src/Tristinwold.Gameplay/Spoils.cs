namespace Tristinwold.Gameplay;

/// <summary>
/// What a hero wins for putting another player's hero down. Nothing is taken from the loser: the winner gets a copy of
/// everything the loser carries, except that weapons and armour (any gear, worn or packed) come as their worth in
/// crowns, never as the pieces themselves. A purse as full as the loser's comes too. Something bound to the one who
/// earned it (a quest's keepsake) isn't copied; bound gear still counts at its worth.
/// </summary>
public static class Spoils
{
    public sealed record Haul(IReadOnlyList<(string Id, int N)> Items, int Crowns, int GearCrowns)
    {
        public int Worth => Crowns + GearCrowns + Items.Sum(i => (Gameplay.Items.Get(i.Id)?.Value ?? 0) * i.N);
    }

    public static Haul Of(Pack pack, int purse)
    {
        var items = new List<(string, int)>();
        long gear = 0;
        foreach (var (id, n) in pack.Items)
        {
            if (n <= 0 || Items.Get(id) is not { } item) continue;
            if (item.Kind == ItemKind.Gear) gear += (long)item.Value * n;
            else if (!item.Bound) items.Add((id, n));
        }
        foreach (var id in pack.Worn.Values)
            if (Items.Get(id) is { } worn) gear += worn.Value;
        return new Haul(items, Math.Max(0, purse), (int)Math.Min(int.MaxValue, gear));
    }

    /// <summary>The winner's share, as told: "120 crowns, 45 crowns for their gear, 3 Healing Draughts".</summary>
    public static string Tell(IEnumerable<(string Id, int N)> items, int crowns, int gearCrowns, int bounty)
    {
        var parts = new List<string>();
        if (crowns > 0) parts.Add($"{crowns} crowns from their purse");
        if (gearCrowns > 0) parts.Add($"{gearCrowns} crowns for what their gear is worth");
        var things = items.Select(i => (Item: Items.Get(i.Id), i.N)).Where(i => i.Item is not null).ToList();
        foreach (var (item, n) in things.Take(6)) parts.Add(n == 1 ? item!.Name : $"{n} {item!.Name}");
        if (things.Count > 6) parts.Add($"{things.Count - 6} more kinds of things");
        if (bounty > 0) parts.Add($"the {bounty} crowns on their head");
        return parts.Count == 0 ? "nothing at all: they carried nothing" : string.Join(", ", parts);
    }
}
