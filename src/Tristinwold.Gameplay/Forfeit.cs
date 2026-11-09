using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>
/// A hero struck down by an enemy wakes at the Gallows Perch with half their crowns and half of every stack of goods
/// they'd gathered (wood, ore, coal, fish, pelts, herbs: whatever's material or herb), and the one that killed them
/// carries the other half off as loot. Gear, potions, food, tools, trophies and anything bound stay with them.
/// </summary>
public static class Forfeit
{
    public static bool Forfeits(Item item) => item.Kind is ItemKind.Material or ItemKind.Herb && !item.Bound;

    /// <summary>Takes half the hero's goods out of the pack and works out half their crowns (the caller takes those): what the killer carries off.</summary>
    public static Hoard Halve(Pack pack, int crowns)
    {
        var hoard = new Hoard { Crowns = Math.Max(0, crowns) / 2 };
        foreach (var (item, n) in pack.Contents.Where(c => Forfeits(c.Item) && !pack.Worn.ContainsValue(c.Item.Id)).ToList())
        {
            var half = n / 2;
            if (half > 0 && pack.Take(item.Id, half)) hoard.Items[item.Id] = half;
        }
        return hoard;
    }

    /// <summary>A hoard taken back: into the pack, whatever of it is still a thing (unknown ids are dropped), and the crowns.</summary>
    public static int Recover(Pack pack, Hoard hoard)
    {
        foreach (var (id, n) in hoard.Items.Where(kv => kv.Value > 0 && Items.Get(kv.Key) is not null)) pack.Add(id, n);
        return Math.Max(0, hoard.Crowns);
    }
}
