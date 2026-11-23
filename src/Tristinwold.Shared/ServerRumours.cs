using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.Shared;

/// <summary>
/// The deviants' doings, as people tell them: a hero left for dead on the road, a band's new chief, a grudge settled.
/// Kept where the fight was (the hero's game says where, and how they look), for the folk nearby to talk of. The hero
/// is described, never named.
/// </summary>
public sealed partial class ServerCore
{
    IEnumerable<Rumour> DeviantRumours(IReadOnlyList<WorldEvent> events, BattleReport report)
    {
        var who = report.Who is { Length: > 0 } w ? w : "a traveller";
        var hero = report.Players.FirstOrDefault();
        static string A(string word) => word.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(word[0])) ? "an" : "a";
        foreach (var ev in events)
        {
            if (World.Find(ev.DeviantId) is not { } n) continue;
            var other = ev.OtherId is { } id ? World.Find(id)?.FullName : null;
            var (name, kind) = (n.FullName, n.Species.ToLowerInvariant());
            var (did, theirs) = ev.Kind switch
            {
                WorldEventKind.Triumph when report.Result == BattleResult.Defeat
                    => ($"{who} was cut down by {name}, {A(kind)} {kind}{(report.Dropped is { Empty: false } ? ", and it carried off half of what they had" : "")}", true),
                WorldEventKind.Triumph => ($"{who} ran from {name}, {A(kind)} {kind}, and it laughed to see them go", true),
                WorldEventKind.Slain => ($"{who} killed {name}, {A(kind)} {kind}", true),
                WorldEventKind.Returned => ($"{who} cut down {name}, {A(kind)} {kind}, but it got up and ran. It'll be back, scarred and angrier", true),
                WorldEventKind.Escaped => ($"{name}, {A(kind)} {kind}, got away from {who}", true),
                WorldEventKind.Born when report.Result == BattleResult.Defeat
                    => ($"{who} was cut down by {A(kind)} {kind}{(report.Dropped is { Empty: false } ? ", and it carried off half of what they had" : "")}. It calls itself {name} now, and leads a band of its own", true),
                WorldEventKind.Born => ($"{A(kind)} {kind} calling itself {name} has risen to lead a band of its own", false),
                WorldEventKind.Promoted => ($"{name}, {A(kind)} {kind}, has risen to {n.Rank.ToString().ToLowerInvariant()}. Its band's bolder for it", false),
                WorldEventKind.Succession when other is not null => ($"{name} has taken {other}'s place at the head of its band", false),
                WorldEventKind.GrudgeWon when other is not null => ($"{name} has killed its rival, {other}", false),
                WorldEventKind.GrudgeLost when other is not null => ($"{name} was beaten and shamed by {other}", false),
                WorldEventKind.Vengeful when other is not null => ($"{name} has sworn to avenge {other}, and it's out looking", false),
                WorldEventKind.Pact when other is not null => ($"{name} and {other} have sworn a pact", false),
                _ => ((string?)null, false),
            };
            if (did is null) continue;
            yield return new Rumour
            {
                Key = $"deviant|{n.Id}|{ev.Kind}|{City.Day}",
                By = theirs ? hero : null,
                Did = did,
                X = report.X, Z = report.Z,
                Area = report.AreaId.Length is > 0 and <= 60 ? report.AreaId : null,
                Bad = ev.Kind is WorldEventKind.Triumph or WorldEventKind.Born or WorldEventKind.Promoted or WorldEventKind.Vengeful,
            };
        }
    }
}
