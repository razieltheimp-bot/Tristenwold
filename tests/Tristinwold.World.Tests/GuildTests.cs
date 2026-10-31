using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>The guilds, their people and drama, and each class's trials.</summary>
public class GuildTests
{
    static readonly WorldGen World = WorldGen.Default;

    [Fact]
    public void Six_guilds_each_with_a_leader_a_teacher_for_every_class_members_and_a_rival()
    {
        Assert.Equal(6, Guilds.All.Count);
        foreach (var g in Guilds.All)
        {
            Assert.Equal(Classes.Of(g.Calling).Select(d => d.Id).OrderBy(x => x), g.Teachers.Select(t => t.Teaches!).OrderBy(x => x));
            Assert.NotEmpty(g.Members);
            Assert.Equal(GuildRole.Rival, g.Rival.Role);
            Assert.All(g.Everyone, m => Assert.True(Math.Sqrt(m.Npc.X * m.Npc.X + m.Npc.Z * m.Npc.Z) > 110, $"{m.Name} stands inside the walls"));
            Assert.All(g.Everyone, m => Assert.True(World.Height(m.Npc.X, m.Npc.Z) > WorldGen.WaterLevel, $"{m.Name} stands in the water"));
        }
        var ids = Guilds.All.SelectMany(g => g.Everyone).Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Every_guild_has_its_drama_told_by_its_own_people()
    {
        foreach (var g in Guilds.All)
        {
            var stories = Guilds.Drama.Where(s => s.Guild == g.Calling).ToList();
            Assert.True(stories.Count >= 4);
            Assert.All(stories, s =>
            {
                Assert.Contains(g.Everyone, m => m.Id == s.TellerId);
                Assert.True(s.Choices.Count >= 2);
                Assert.All(s.Choices.Where(c => c.Duel), c => Assert.NotNull(c.Loss));
            });
        }
    }

    [Fact]
    public void Drama_comes_with_time_and_rank_and_choices_change_things()
    {
        var book = new GuildBook();
        Assert.Null(Guilds.NextDrama(book, Calling.Warrior)); // not yet: just joined
        book.PlaySeconds = Guilds.DramaGapSeconds;
        var first = Guilds.NextDrama(book, Calling.Warrior)!;
        Assert.Equal("broken_blade", first.Id);
        Guilds.Choose(book, first, first.Choices[2]);
        Assert.Contains("wise", book.Flags);
        Assert.Equal(15, book.For(Calling.Warrior).Standing);
        Assert.Null(Guilds.NextDrama(book, Calling.Warrior)); // the next waits a while
        book.PlaySeconds += Guilds.DramaGapSeconds;
        Assert.Null(Guilds.NextDrama(book, Calling.Warrior)); // and for you to rise: the bribe comes to members
        book.For(Calling.Warrior).Standing = Guilds.RankStanding[(int)GuildRank.Member];
        var tong = Guilds.NextDrama(book, Calling.Warrior)!;
        Assert.Equal("tong_purse", tong.Id);
        Guilds.Choose(book, tong, tong.Choices[1]);
        book.PlaySeconds += Guilds.DramaGapSeconds;
        book.For(Calling.Warrior).Standing = Guilds.RankStanding[(int)GuildRank.Adept];
        Assert.Equal("first_blade", Guilds.NextDrama(book, Calling.Warrior)!.Id);
        // Losing the duel in a story still moves it on, with less glory.
        var duel = Guilds.NextDrama(book, Calling.Warrior)!;
        var standing = book.For(Calling.Warrior).Standing;
        Guilds.Choose(book, duel, duel.Choices.First(c => c.Duel), wonDuel: false);
        Assert.Contains("first_blade_lost", book.Flags);
        Assert.True(book.For(Calling.Warrior).Standing < standing + duel.Choices[0].Standing);
    }

    [Fact]
    public void Every_class_has_three_trials_ending_in_a_duel_with_the_guild_rival()
    {
        foreach (var d in Classes.All)
        {
            var g = Guilds.Of(d.Calling);
            var teacher = g.TeacherOf(d.Id)!;
            var trials = Enumerable.Range(0, ClassQuests.Steps).Select(i => ClassQuests.For(World, d, i)).ToList();
            Assert.All(trials, q => Assert.Equal(teacher.Id, q!.GiverId));
            Assert.Equal(QuestKind.Slay, trials[0]!.Kind);
            Assert.True(World.Find(trials[0]!.TargetId)!.Hostile);
            Assert.Equal(QuestKind.Fetch, trials[1]!.Kind);
            Assert.Equal(QuestKind.Duel, trials[2]!.Kind);
            Assert.Equal(g.Rival.Id, trials[2]!.PersonId);
            Assert.Null(ClassQuests.For(World, d, ClassQuests.Steps));
            Assert.True(ClassQuests.IsClassQuest(trials[2]!.Id, out var id, out var step) && id == d.Id && step == 2);
        }
    }

    [Fact]
    public void A_duel_won_completes_a_duel_quest_and_guild_life_is_saved()
    {
        var log = new QuestLog();
        var trial = ClassQuests.For(World, Classes.Get("Thief"), 2)!;
        log.Accept(trial);
        Assert.Single(log.DuelWon(trial.PersonId!));
        Assert.Equal(QuestStage.Return, log.Get(trial.Id)!.Stage);
        Assert.Single(log.TalkedTo(trial.GiverId));
        Assert.Equal(QuestStage.Done, log.Get(trial.Id)!.Stage);

        var book = new GuildBook { PlaySeconds = 99, LessonLevel = 4, Titles = { "Prince of Pockets" }, ClassSteps = { ["Thief"] = 3 } };
        book.For(Calling.Rogue).Standing = 80;
        book.Flags.Add("parley");
        var back = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), log, guilds: book).ToJson()).Guilds!;
        Assert.Equal(GuildRank.Adept, back.Rank(Calling.Rogue));
        Assert.Equal(3, back.Step("Thief"));
        Assert.Contains("parley", back.Flags);
        Assert.Equal(4, back.LessonLevel);
    }
}

/// <summary>The Ashen Crypt: a repeatable raid made for low-level heroes.</summary>
public class RaidTests
{
    [Fact]
    public void The_crypt_is_on_dry_open_ground_west_of_the_city()
    {
        var w = WorldGen.Default;
        var r = Raids.AshenCrypt;
        Assert.InRange(r.DistanceTo(0, 0), 200, 600);
        Assert.True(w.Height(r.X, r.Z) > WorldGen.WaterLevel + 0.5);
        Assert.DoesNotContain(w.SettlementsNear(r.X, r.Z, 150), s => s.DistanceTo(r.X, r.Z) < s.Radius + 40);
        Assert.Equal("cinder_warden", Tristinwold.Gameplay.Bestiary.Get("cinder_warden").Id);
    }

    [Fact]
    public void It_grows_with_low_level_heroes_rewards_them_well_and_rekindles()
    {
        var r = Raids.AshenCrypt;
        Assert.True(Raids.Boss(r, 4).Health > Raids.Boss(r, 1).Health);
        Assert.Equal(Raids.Boss(r, r.MaxLevel), Raids.Boss(r, 30)); // never past what it was made for
        Assert.True(Raids.Reward(r, 2, firstTime: true).Xp > Raids.Reward(r, 2, firstTime: false).Xp);
        Assert.True(Raids.Reward(r, 20, false).Xp < Raids.Reward(r, r.MaxLevel, false).Xp);
        Assert.True(Raids.Outgrown(r, 20) && !Raids.Outgrown(r, 3));

        var book = new RaidBook();
        Assert.True(book.Ready(r, 0));
        book.Cleared(r, 100);
        Assert.False(book.Ready(r, 100 + r.CooldownSeconds - 1));
        Assert.True(book.Ready(r, 100 + r.CooldownSeconds));
        Assert.Equal(1, book.ClearsOf(r));
    }
}
