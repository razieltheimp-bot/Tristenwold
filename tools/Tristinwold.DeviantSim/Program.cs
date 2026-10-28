// Plays a fake campaign against the deviant rules and prints what happens, so the
// balance can be tuned without launching the game.
//
//   dotnet run --project tools/Tristinwold.DeviantSim -- [battles] [seed] [namepack.json]
using Tristinwold.Deviant;

var battles = args.Length > 0 ? int.Parse(args[0]) : 60;
var seed = args.Length > 1 ? ulong.Parse(args[1]) : 42UL;
var names = args.Length > 2 ? NamePack.Load(args[2]) : NamePack.Default;

string[] players = ["Joe", "Mira", "Dax"];
string[] areas = ["Outer Ring", "The Docks", "The Needle", "Drowned Chapel", "The Exchange"];
(string Id, string Name, int Hp)[] species =
[
    ("Giant Rat", "Plague Rat", 60), ("Bandit", "Dock Thug", 150), ("Draugr", "Draugr Wight", 300),
    ("Spriggan", "Spriggan", 450), ("Troll", "Frost Troll", 800),
];

var world = new DeviantWorld(seed, names: names);
var dice = new Rng(seed ^ 0xDEADBEEF);

for (var b = 1; b <= battles; b++)
{
    var area = dice.Pick(areas);
    var formation = Enumerable.Range(0, 1 + dice.Next(3))
        .Select(slot => { var s = dice.Pick(species); return new FormationSlot(slot, s.Id, s.Name); })
        .ToList();
    var party = players.Where(_ => dice.Chance(0.7)).DefaultIfEmpty(players[0]).ToList();
    var spawns = world.PlanEncounter(area, formation, party).ToDictionary(s => s.Slot);

    foreach (var s in spawns.Values)
        Console.WriteLine($"  >> {s.Modifiers.DisplayName} appears! {(s.Taunt is null ? "" : $"\"{s.Taunt}\"")}");

    var result = dice.NextDouble() switch { < 0.62 => BattleResult.Victory, < 0.8 => BattleResult.Defeat, < 0.93 => BattleResult.PartyEscaped, _ => BattleResult.EnemiesEscaped };
    var enemies = formation.Select(f => new EnemyReport(
        f.Slot, f.Kind, f.Species, species.First(s => s.Id == f.Kind).Hp,
        Killed: result == BattleResult.Victory,
        Escaped: result == BattleResult.EnemiesEscaped,
        DeviantId: spawns.TryGetValue(f.Slot, out var sp) ? sp.DeviantId : null,
        KillingBlow: (DamageKind)dice.Next(6),
        KilledBy: dice.Pick(party))).ToList();

    Console.WriteLine($"Battle {b} in {area}: {string.Join(", ", party)} vs {string.Join(", ", formation.Select(f => f.Species))} -> {result}");
    foreach (var e in world.RecordBattle(new BattleReport(area, result, enemies, party, 10 + b / 2)))
        Console.WriteLine($"  * {e.Text}");
    if (b % 4 == 0)
        foreach (var e in world.AdvanceDay()) Console.WriteLine($"  ~ Day {world.Day}: {e.Text}");
}

Console.WriteLine();
Console.WriteLine("=== THE RANKS ===");
if (world.Tyrant is { } o) Console.WriteLine($"TYRANT     {Describe(o)}");
foreach (var w in world.Scourges)
{
    Console.WriteLine($"SCOURGE    {Describe(w)}");
    foreach (var c in world.RingleadersUnder(w.Id)) Console.WriteLine($"   ringleader {Describe(c)}");
}
foreach (var c in world.UnassignedRingleaders) Console.WriteLine($"RINGLEADER {Describe(c)}");
foreach (var g in world.Strays) Console.WriteLine($"stray      {Describe(g)}");
Console.WriteLine($"Fallen: {world.All.Count(n => !n.Alive)}");

string Describe(Deviant n) =>
    $"{n.FullName} ({n.Species}, Lv {n.Level}, power {n.Power}) " +
    $"[{string.Join("; ", n.Traits.Select(t => t.Label.Split(':')[0]))}]" +
    (n.Scars.Count > 0 ? $" scars:{n.Scars.Count}" : "");
