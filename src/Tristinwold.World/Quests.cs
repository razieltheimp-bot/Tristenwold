using Tristinwold.Gameplay;
using Tristinwold.Nemesis;

namespace Tristinwold.World;

public enum QuestKind { Slay, Fetch, Deliver, Rescue, Duel }

/// <summary>
/// A job someone in the world asks of a company. Targets are real places nearby: a lair to
/// clear, a tomb to rob, a village to carry a letter to, a camp where someone is held.
/// </summary>
public sealed record Quest(
    string Id,
    QuestKind Kind,
    string Title,
    string Pitch,
    string GiverId,
    string GiverName,
    string GiverHomeId,
    string GiverHomeName,
    double GiverX,
    double GiverZ,
    string TargetId,
    string TargetName,
    double TargetX,
    double TargetZ,
    int Reward,
    int Count = 0,
    string? Item = null,
    string? PersonId = null,
    string? PersonName = null);

public enum QuestStage { Active, Return, Done }

public sealed class QuestEntry
{
    public required Quest Quest { get; init; }
    public QuestStage Stage { get; set; }
    public int Progress { get; set; }

    /// <summary>What to do next, for the journal.</summary>
    public string Objective => (Quest.Kind, Stage) switch
    {
        (_, QuestStage.Done) => "Done.",
        (QuestKind.Slay, QuestStage.Active) => $"Clear {Quest.TargetName}: {Progress}/{Quest.Count}",
        (QuestKind.Fetch, QuestStage.Active) => $"Find {Quest.Item} at {Quest.TargetName}",
        (QuestKind.Deliver, _) => $"Take the {Quest.Item} to {Quest.PersonName} at {Quest.TargetName}",
        (QuestKind.Rescue, QuestStage.Active) => $"Find {Quest.PersonName} at {Quest.TargetName}",
        (QuestKind.Duel, QuestStage.Active) => $"Beat {Quest.PersonName} in a duel at {Quest.TargetName}",
        _ => $"Return to {Quest.GiverName} at {Quest.GiverHomeName}",
    };

    /// <summary>Where the objective is.</summary>
    public (double X, double Z) Goal => Stage == QuestStage.Return ? (Quest.GiverX, Quest.GiverZ) : (Quest.TargetX, Quest.TargetZ);
}

/// <summary>What changed in the journal, and any septims it earned.</summary>
public sealed record QuestUpdate(Quest Quest, QuestStage Stage, string Text, int Reward = 0);

/// <summary>A company's quests. Saved with the company.</summary>
public sealed class QuestLog
{
    public List<QuestEntry> Entries { get; init; } = [];

    public QuestEntry? Get(string questId) => Entries.FirstOrDefault(e => e.Quest.Id == questId);
    public IEnumerable<QuestEntry> Open => Entries.Where(e => e.Stage != QuestStage.Done);

    public QuestEntry Accept(Quest quest)
    {
        if (Get(quest.Id) is { } existing) return existing;
        var entry = new QuestEntry { Quest = quest };
        Entries.Add(entry);
        return entry;
    }

    public bool Abandon(string questId) => Entries.RemoveAll(e => e.Quest.Id == questId && e.Stage != QuestStage.Done) > 0;

    /// <summary>Something died at a site.</summary>
    public IEnumerable<QuestUpdate> Killed(string siteId)
    {
        foreach (var e in Open.Where(e => e.Quest.Kind == QuestKind.Slay && e.Stage == QuestStage.Active && e.Quest.TargetId == siteId).ToList())
        {
            e.Progress++;
            if (e.Progress < e.Quest.Count) continue;
            e.Stage = QuestStage.Return;
            yield return new QuestUpdate(e.Quest, e.Stage, $"{e.Quest.TargetName} is clear. Return to {e.Quest.GiverName}.");
        }
    }

    /// <summary>The company picked up a quest item.</summary>
    public QuestUpdate? PickedUp(string questId)
    {
        if (Get(questId) is not { Stage: QuestStage.Active, Quest.Kind: QuestKind.Fetch } e) return null;
        e.Stage = QuestStage.Return;
        return new QuestUpdate(e.Quest, e.Stage, $"You found {e.Quest.Item}. Bring it to {e.Quest.GiverName}.");
    }

    /// <summary>A duel won against someone: any quest to beat them is done but for the telling.</summary>
    public IEnumerable<QuestUpdate> DuelWon(string personId)
    {
        foreach (var e in Open.Where(e => e.Quest.Kind == QuestKind.Duel && e.Stage == QuestStage.Active && e.Quest.PersonId == personId).ToList())
        {
            e.Stage = QuestStage.Return;
            yield return new QuestUpdate(e.Quest, e.Stage, $"{e.Quest.PersonName} yields. Return to {e.Quest.GiverName}.");
        }
    }

    /// <summary>The company spoke to someone: a recipient, a captive, or a giver waiting on news.</summary>
    public IEnumerable<QuestUpdate> TalkedTo(string npcId)
    {
        foreach (var e in Open.ToList())
        {
            var q = e.Quest;
            if (q.Kind == QuestKind.Deliver && q.PersonId == npcId)
            {
                e.Stage = QuestStage.Done;
                yield return new QuestUpdate(q, e.Stage, $"{q.PersonName} takes the {q.Item} and pays you {q.Reward} septims.", q.Reward);
            }
            else if (q.Kind == QuestKind.Rescue && q.PersonId == npcId && e.Stage == QuestStage.Active)
            {
                e.Stage = QuestStage.Return;
                yield return new QuestUpdate(q, e.Stage, $"\"Thank the gods. Tell {q.GiverName.Split(' ')[0]} I'm coming home.\" {q.PersonName} runs for {q.GiverHomeName}.");
            }
            else if (q.GiverId == npcId && e.Stage == QuestStage.Return)
            {
                e.Stage = QuestStage.Done;
                yield return new QuestUpdate(q, e.Stage, $"{q.GiverName} pays you {q.Reward} septims.", q.Reward);
            }
        }
    }
}

/// <summary>Works out the job, if any, each person in the world has to offer.</summary>
public static class Quests
{
    public static Quest? For(WorldGen world, Npc npc)
    {
        if (People.IsYouth(npc)) return null;
        var rng = Noise.Dice(world.Seed, 4, WorldGen.StableHash(npc.Id));
        if (!rng.Chance(0.55) || world.Find(npc.HomeId) is not { } home) return null;

        var lairs = world.SitesNear(home.X, home.Z, 1600).Where(s => s.Hostile).OrderBy(s => s.DistanceTo(home.X, home.Z)).Take(4).ToList();
        var places = world.SettlementsNear(home.X, home.Z, 2600)
            .Where(s => !s.Hostile && s.Id != home.Id && s.People > 0 && s.DistanceTo(home.X, home.Z) > 150)
            .OrderBy(s => s.DistanceTo(home.X, home.Z)).Take(6).ToList();

        double Likes(params string[] interests) => interests.Any(npc.Interests.Contains) ? 3 : 1;
        var kinds = new List<(QuestKind, double)>();
        if (lairs.Count > 0)
        {
            kinds.Add((QuestKind.Slay, Likes("hunting", "wolves", "blades", "old battles")));
            kinds.Add((QuestKind.Fetch, Likes("alchemy", "old coins", "the dead", "faith", "mushrooms")));
            kinds.Add((QuestKind.Rescue, 0.7));
        }
        if (places.Count > 0) kinds.Add((QuestKind.Deliver, Likes("letters", "gossip", "money")));
        if (kinds.Count == 0) return null;

        var kind = rng.PickWeighted(kinds);
        var id = npc.Id + "#q";
        var first = npc.First;
        if (kind == QuestKind.Deliver)
        {
            var target = rng.Pick(places);
            var recipient = rng.Pick(world.PeopleOf(target));
            var item = rng.Pick(["letter", "sealed parcel", "bundle of herbs", "jar of honey", "carved box", "purse of debts"]);
            var reward = 20 + (int)(target.DistanceTo(home.X, home.Z) / 25);
            var why = rng.Pick([
                "My knees won't take the road any more.", $"{recipient.First} and I haven't spoken in years. This should mend it.",
                "Don't open it. I mean that.", "It's owed, and I'd rather it got there before the Tongs hear of it.",
            ]);
            return new Quest(id, kind, $"A {item} for {target.Name}",
                $"Take this {item} to {recipient.Name} at {target.Name}. {why} {recipient.First} will pay you {reward} septims when it arrives.",
                npc.Id, npc.Name, home.Id, home.Name, npc.X, npc.Z, target.Id, target.Name, recipient.X, recipient.Z, reward,
                Item: item, PersonId: recipient.Id, PersonName: recipient.Name);
        }

        var lair = rng.Pick(lairs);
        var danger = WorldGen.Danger(lair.X, lair.Z);
        var foes = Plural(lair.Lair!.KindId);
        var bounty = (int)(lair.Lair.Total * 15 * danger + lair.DistanceTo(home.X, home.Z) / 30);
        switch (kind)
        {
            case QuestKind.Slay:
                return new Quest(id, kind, $"Clear out {lair.Name}",
                    $"{lair.Name} is crawling with {foes}. They took two sheep last week and a man the week before. Clear them out and I'll pay {bounty} septims.",
                    npc.Id, npc.Name, home.Id, home.Name, npc.X, npc.Z, lair.Id, lair.Name, lair.X, lair.Z, bounty, Count: lair.Lair.Total);
            case QuestKind.Fetch:
            {
                var item = FetchItem(rng, npc, lair);
                return new Quest(id, kind, $"{Cap(item)} from {lair.Name}",
                    $"{Cap(item)} is somewhere in {lair.Name}, and the {foes} there won't give it up. Bring it to me and {bounty} septims are yours.",
                    npc.Id, npc.Name, home.Id, home.Name, npc.X, npc.Z, lair.Id, lair.Name, lair.X, lair.Z, bounty, Item: item);
            }
            default:
            {
                var kin = rng.Pick(People.Kin);
                Sex? kinSex = kin switch { "sister" or "daughter" or "wife" or "mother" => Sex.Female, "brother" or "son" or "husband" or "father" => Sex.Male, _ => null };
                var family = kin is "best friend" or "apprentice" || !npc.Name.Contains(' ') ? Names.Family(rng) : npc.Name.Split(' ')[^1];
                var lost = $"{Names.Given(rng, kinSex ?? (rng.Chance(0.5) ? Sex.Female : Sex.Male))} {family}";
                var reward = bounty + 25;
                return new Quest(id, kind, $"Find {lost.Split(' ')[0]}",
                    $"My {kin}, {lost}, went out past {lair.Name} three days ago and never came back. The {foes} there... please. I'll give you everything I have: {reward} septims.",
                    npc.Id, npc.Name, home.Id, home.Name, npc.X, npc.Z, lair.Id, lair.Name, lair.X, lair.Z, reward,
                    PersonId: id + "/captive", PersonName: lost);
            }
        }
    }

    static string FetchItem(Rng rng, Npc npc, Settlement lair)
    {
        if (npc.Interests.Contains("alchemy")) return "a jar of glowing frost salts";
        if (npc.Interests.Contains("old coins")) return "a silver coin of the Old Kings";
        if (npc.Interests.Contains("the dead")) return "a family burial ring";
        if (npc.Interests.Contains("faith")) return "a stolen relic of the shrine";
        return lair.Kind switch
        {
            SettlementKind.BanditCamp => rng.Pick(["the stolen tithe-chest", "an heirloom sword", "a ledger of Tong payments"]),
            SettlementKind.Barrow => rng.Pick(["an urn of grave-dust", "a bronze death-mask", "a rune-carved horn"]),
            _ => rng.Pick(["a lost hunting horn", "a satchel dropped in flight", "the wedding ring the beast swallowed"]),
        };
    }

    static string Plural(string kindId) => kindId switch
    {
        "bandit" => "bandits", "draugr" => "draugr", "wolf" => "wolves", "ice_wolf" => "ice wolves", "bear" => "bears",
        "frost_troll" => "trolls", "boar" => "boars", "goblin" => "goblins", "skeever" => "skeevers", "mudcrab" => "mudcrabs", "spriggan" => "spriggans",
        _ => Bestiary.Get(kindId).Name.ToLowerInvariant() + "s",
    };

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
