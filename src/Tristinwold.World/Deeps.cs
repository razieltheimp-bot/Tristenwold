namespace Tristinwold.World;

/// <summary>A network of the Deep Trenches: the mines above it, the dwarf city in it, and the one way down from it.</summary>
public sealed record Trench(string Id, int I, int J, string City, string Thane, IReadOnlyList<Settlement> Mines, int Seed)
{
    /// <summary>Under Tristinwold: the trench the Greyhollow mine opens into, and the Rift's own.</summary>
    public bool Home => I == 0 && J == 0;
}

/// <summary>
/// What lies under the mines. The Deep Trenches: below every few mines, a network of old dwarf roads and the city they
/// lead to, where the dwarves came up from long ago and where their kin still live; the mines above a network all open
/// into it, so it's a way from one to another. Under all of them, the Dark Depths: one great dark under the whole
/// world, where a rift to the hells has opened under Tristinwold and demons pour through. Every network has one tunnel
/// down to it, so the Depths join every mine in the world to every other, and to the Rift. Nothing down there is built
/// until someone goes down: a network is only the mines it reaches, worked out from where they are.
/// </summary>
public static class Deeps
{
    /// <summary>How wide a network's reach is: four or five mines' worth of country.</summary>
    public const double Cell = 4500;

    public static (int I, int J) CellOf(double x, double z) => ((int)Math.Floor((x + Cell / 2) / Cell), (int)Math.Floor((z + Cell / 2) / Cell));

    public static string IdOf(int i, int j) => $"trench:{i}:{j}";

    static readonly string[] Fore = ["Khaz", "Dun", "Kar", "Bar", "Grim", "Thor", "Mor", "Azan", "Durn", "Vel", "Brun", "Hal"];
    static readonly string[] Aft = ["dum", "garde", "hold", "mar", "drak", "zul", "heim", "rok", "gath", "deep", "forge", "vault"];
    static readonly string[] Thanes = ["Thane Borin Ironvein", "Thane Hilda Coalbrow", "Thane Durgan Stonefist", "Thane Ysolt Deepdelver", "Thane Odvar Ashbeard", "Thane Brenna Goldseam", "Thane Kili Anvilborn", "Thane Magda Lampwright"];

    /// <summary>The network under this cell: its mines (Greyhollow's under Tristinwold is the home cell's), and its city.</summary>
    public static Trench Get(WorldGen world, int i, int j)
    {
        var (cx, cz) = (i * Cell, j * Cell);
        var mines = world.SettlementsNear(cx, cz, Cell * 0.75).Where(s => s.Kind == SettlementKind.Mine && CellOf(s.X, s.Z) == (i, j)).OrderBy(s => s.Id).ToList();
        var seed = (int)Math.Abs(WorldGen.StableHash(IdOf(i, j)) % 1_000_000);
        var city = i == 0 && j == 0 ? "Khazad Greyhollow" : $"{Fore[seed % Fore.Length]}{Aft[seed / 7 % Aft.Length]}";
        return new Trench(IdOf(i, j), i, j, city, Thanes[seed / 13 % Thanes.Length], mines, seed);
    }

    public static Trench Under(WorldGen world, double x, double z) => CellOf(x, z) is var (i, j) ? Get(world, i, j) : Get(world, 0, 0);

    public static Trench? Parse(WorldGen world, string id) =>
        id.Split(':') is ["trench", var a, var b] && int.TryParse(a, out var i) && int.TryParse(b, out var j) ? Get(world, i, j) : null;

    /// <summary>The networks near a place that have mines over them (the home network always does: Greyhollow), nearest first.</summary>
    public static IEnumerable<Trench> Around(WorldGen world, double x, double z, int reach = 2)
    {
        var (ci, cj) = CellOf(x, z);
        var found = new List<Trench>();
        for (var i = ci - reach; i <= ci + reach; i++)
            for (var j = cj - reach; j <= cj + reach; j++)
                if (Get(world, i, j) is var t && (t.Mines.Count > 0 || t.Home)) found.Add(t);
        return found.OrderBy(t => Math.Pow(t.I * Cell - x, 2) + Math.Pow(t.J * Cell - z, 2));
    }
}
