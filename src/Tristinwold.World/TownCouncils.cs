using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>The people who sit on a place's council, the same for everyone: usually three, now and then five, and a clerk who keeps its books.</summary>
public static class TownCouncils
{
    public const string ClerkId = "/council_clerk";

    /// <summary>Whether a place is big enough to have one: a village, a town or a city.</summary>
    public static bool Keeps(Settlement s) => !s.Hostile && (s.Kind is SettlementKind.City or SettlementKind.Town or SettlementKind.Hamlet || s.Kind == SettlementKind.AshbornCamp && Grown?.Invoke(s.Id) >= GrownChamber);

    /// <summary>How far a refuge has grown (the game's city book fills this in), and the step at which it has a council chamber and a council.</summary>
    public static Func<string, int>? Grown { get; set; }
    public const int GrownChamber = 3, GrownTavern = 1;

    public static bool IsMember(string id) => id.Contains("/council");

    /// <summary>Where the council meets, for the i-th person there: round the seat of power, out from the middle.</summary>
    static (double X, double Z) Seat(Settlement s, double angle)
    {
        var reach = s.Id == "city:tristinwold" ? 23 : s.Id == "city:hollowheart" ? s.Radius * 0.7 : Math.Max(s.Radius * 0.2, 19);
        return (s.X + Math.Cos(angle) * reach, s.Z + Math.Sin(angle) * reach);
    }

    public static IReadOnlyList<Npc> Members(Settlement s)
    {
        var rng = new Rng((ulong)WorldGen.StableHash(s.Id) ^ 0xC041);
        var seats = rng.Chance(0.15) ? 5 : 3;
        var people = new List<Npc>();
        for (var i = 0; i < seats; i++)
        {
            var sex = rng.PickWeighted<Sex>([(Sex.Female, 0.47), (Sex.Male, 0.47), (Sex.Other, 0.06)]);
            var name = Names.Person(rng, sex);
            var (x, z) = Seat(s, 0.5 + i * 0.5);
            people.Add(new Npc($"{s.Id}/council{i}", name, sex, 38 + rng.Next(30), $"councillor of {s.Name}", s.Id, s.Name,
                $"{name} sits on the council of {s.Name}, and has the tired look of someone who's read every letter.", ["the town", "trade"], "measured",
                "The council's business is everyone's business. What is it?", x, z));
        }
        return people;
    }

    /// <summary>The clerk: there whoever's left at the table.</summary>
    public static Npc Clerk(Settlement s)
    {
        var rng = new Rng((ulong)WorldGen.StableHash(s.Id) ^ 0xC1E4);
        var sex = rng.Chance(0.5) ? Sex.Female : Sex.Male;
        var name = Names.Person(rng, sex);
        var (x, z) = Seat(s, 0.5 + 6 * 0.5);
        return new Npc($"{s.Id}{ClerkId}", name, sex, 45, $"clerk to the council of {s.Name}", s.Id, s.Name,
            $"{name} has kept the council's books in {s.Name} through more members than anyone remembers.", ["the town", "ledgers"], "dry",
            "Councils come and go. The ledger stays.", x, z);
    }
}
