using Tristinwold.Gameplay;

namespace Tristinwold.World;

/// <summary>
/// Each class's own quest line, given by its teacher in the guild: three trials in the way of that class. Clear a
/// lair the way your class would; bring back the relic of your art from where it was lost; then beat your guild's
/// rival in a duel. Master all three and the teacher names you a master of the art, with a title and two skill
/// points. The places are real lairs near Tristinwold, the same for every company.
/// </summary>
public static class ClassQuests
{
    public const int Steps = 3;

    /// <summary>What each class's trials are called and asked for: how to clear the lair, the relic, and the title at the end.</summary>
    static readonly Dictionary<string, (string Style, string Relic, string Mastery, string Lore)> Arts = new()
    {
        ["MartialArtist"] = ("with nothing but your hands and feet", "the Scroll of the Open Hand", "Grandmaster of the Open Hand",
            "The first masters wrote the forms on a scroll that a bandit took for a treasure map."),
        ["Barbarian"] = ("and let them hear you coming", "the Bloodhorn of the Old Clans", "Bloodhorn Chieftain",
            "The last chieftain's horn was lost when the clans broke. Blow it and the dead clans answer."),
        ["Paladin"] = ("and pray over every one of them after", "the Reliquary of Saint Aubrel", "Oathsworn of the Tide",
            "The saint's reliquary was stolen from the Temple of the Tide in a winter nobody likes to remember."),
        ["Ranger"] = ("without letting a single one reach you", "the Greywood Bow", "Warden of the Greywood",
            "The first warden's bow was carried off when she fell, and the Greywood has been angry ever since."),
        ["Thief"] = ("and lift whatever they've stolen while you're at it", "the Lockbreaker's Picks", "Prince of Pockets",
            "The Lockbreaker's picks open any lock ever made. The last man to carry them got careless."),
        ["Assassin"] = ("without them ever seeing your face", "the Black Mercy", "the Black Mercy",
            "The Black Mercy is a vial and a title both. Whoever holds the one may claim the other."),
        ["Wizard"] = ("with fire, frost and storm, all three", "the Tome of the Three Fires", "Magister of the Three Fires",
            "The tome holds every element's true name. A careless student lost it, and the student is still missing."),
        ["Warlock"] = ("and let your patron drink their fear", "the Hollow Candle", "Pactbound",
            "The Hollow Candle burns with a patron's attention. Light it and something old looks your way."),
        ["Sorcerer"] = ("and let the blood in you run wild", "the Dragonblood Pearl", "Dragonblood",
            "The pearl formed in a dragon's heart. Hold it and your own blood remembers what it was."),
        ["Poet"] = ("and compose a verse for every one that falls", "the Laurel of Maren Ashe", "Laureate of Tristinwold",
            "Maren Ashe was the first Laureate. Her laurel was buried with her, and her barrow was robbed the same winter."),
        ["Scribe"] = ("and write down how each of them fell", "the Unwritten Codex", "Keeper of the Codex",
            "The Codex has every word of power in it but one, left blank. A thief took it, hoping to fill the page."),
        ["Musician"] = ("and keep the beat the whole way through", "the Lute of the Long Night", "Master of the Long Night",
            "The lute was played through the longest night the city ever had, and kept the dark out. Then its player went wandering."),
        ["Alchemist"] = ("and burn, freeze or shock every one of them, never a blade", "the Philosopher's Retort", "Grand Alchemist of the Foundry",
            "The Retort turns any reagent into any other. The Foundress's own master lost it to a creditor, and the creditor to a lair."),
        ["EtherAeronaut"] = ("with your golem on and your harpoon out", "the Ether Compass", "Aeronaut of the First Flight",
            "The compass points along the ether's currents, over sea and sky both. It went down with the Foundry's first airship."),
        ["Saboteur"] = ("without a fair fight in it: traps, smoke and a wrench from behind", "the Wrecker's Lantern", "Master Wrecker",
            "The lantern's light shows what's weakest in any machine, wall or hull. The last wrecker to carry it wrecked himself."),
    };

    public static string MasteryTitle(string discipline) => Arts[discipline].Mastery;

    static string QuestId(string discipline, int step) => $"class:{discipline}:{step}";

    public static bool IsClassQuest(string questId, out string discipline, out int step)
    {
        (discipline, step) = ("", 0);
        var parts = questId.Split(':');
        if (parts.Length != 3 || parts[0] != "class" || !int.TryParse(parts[2], out step)) return false;
        discipline = parts[1];
        return true;
    }

    /// <summary>The trial a class's teacher sets at a step (0 to 2), or null when there's none (mastered, or no lair near).</summary>
    public static Quest? For(WorldGen world, Discipline d, int step)
    {
        if (step >= Steps || Guilds.Of(d.Calling).TeacherOf(d.Id) is not { } teacher) return null;
        var guild = Guilds.Of(d.Calling);
        var (style, relic, mastery, lore) = Arts[d.Id];
        var t = teacher.Npc;
        var lairs = world.SitesNear(0, 0, 3000).Where(s => s.Hostile && s.Lair is not null).OrderBy(s => s.DistanceTo(0, 0)).ToList();
        if (lairs.Count < 4) return null;
        // Each class gets its own lairs, near enough to Tristinwold.
        var index = Classes.All.ToList().FindIndex(c => c.Id == d.Id);
        var reward = 60 + step * 50;
        switch (step)
        {
            case 0:
            {
                var lair = lairs[index % Math.Min(6, lairs.Count)];
                var foes = Bestiary.Get(lair.Lair!.KindId).Name.ToLowerInvariant();
                return new Quest(QuestId(d.Id, 0), QuestKind.Slay, $"The {d.Name}'s First Trial",
                    $"Before I teach you more, show me you're worth the teaching. {lair.Name} is full of {foes}s. Clear it, {style}. Then come back and tell me how it felt.",
                    t.Id, t.Name, "city:tristinwold", guild.Hall, t.X, t.Z, lair.Id, lair.Name, lair.X, lair.Z, reward, Count: lair.Lair.Total);
            }
            case 1:
            {
                var lair = lairs[(index + 3) % lairs.Count];
                return new Quest(QuestId(d.Id, 1), QuestKind.Fetch, $"The {d.Name}'s Second Trial: {Cap(relic)}",
                    $"{lore} It's at {lair.Name} now, or so the rumours go. Bring {relic} back to me, and you'll have earned the right to carry it.",
                    t.Id, t.Name, "city:tristinwold", guild.Hall, t.X, t.Z, lair.Id, lair.Name, lair.X, lair.Z, reward, Item: relic);
            }
            default:
            {
                var rival = guild.Rival.Npc;
                return new Quest(QuestId(d.Id, 2), QuestKind.Duel, $"The {d.Name}'s Last Trial",
                    $"One trial left, and it isn't mine. {rival.Name} has been telling the whole guild you'll never be a true {d.Name}. Go to {guild.Hall} and prove {rival.Them} wrong, in a fair duel. Then come back to me, {mastery}.",
                    t.Id, t.Name, "city:tristinwold", guild.Hall, t.X, t.Z, guild.Id, guild.Hall, rival.X, rival.Z, reward,
                    PersonId: rival.Id, PersonName: rival.Name);
            }
        }
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
