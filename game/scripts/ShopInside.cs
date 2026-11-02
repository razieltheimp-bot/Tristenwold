using Godot;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>Shops you can walk into: the keeper who stands out front is behind the counter inside too, and trades the same.</summary>
public partial class Main
{
    /// <summary>The keeper of each shop and smithy, by the seed its building was made from: who to put behind the counter.</summary>
    public readonly Dictionary<int, (Npc Npc, string? Look)> ShopDoors = [];
    Person? _keeperInside;

    public void KeeperInside(int seed, Vector3 at)
    {
        if (!ShopDoors.TryGetValue(seed, out var keeper)) return;
        _keeperInside = MeetAt(keeper.Npc with { X = at.X, Z = at.Z }, keeper.Look, at.Y);
        _keeperInside.Rotation = new Vector3(0, 0, 0);
    }

    public void LeftTheShop()
    {
        if (_keeperInside is not null) Dismiss(_keeperInside);
        _keeperInside = null;
    }

    /// <summary>For the self-test.</summary>
    public Person? KeeperInsideForTest => _keeperInside;
}
