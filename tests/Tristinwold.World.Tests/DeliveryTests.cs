using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class DeliveryTests
{
    static Quest Letter(string town, bool council) => new("q1", QuestKind.Deliver, $"A letter for {town}", "Take it.", "giver", "Ada Giver", "city:tristinwold", "Tristinwold",
        0, 0, town, "Somewhere", 100, 100, 30, Item: "letter", PersonId: council ? $"{town}/council" : $"{town}/someone", PersonName: council ? "the council of Somewhere" : "Bel Someone");

    static Npc Someone(string job, string home) => new("x", "Corin Hale", Sex.Male, 30, job, home, "Somewhere", "", [], "plain", "Hello.", 0, 0);

    [Fact]
    public void A_towns_guard_takes_a_delivery_for_their_town_and_the_job_is_done_and_paid()
    {
        foreach (var council in new[] { true, false })
        {
            var log = new QuestLog();
            log.Accept(Letter("town:somewhere", council));
            Assert.Single(log.ForTheGuard("town:somewhere"));
            Assert.Empty(log.ForTheGuard("town:elsewhere")); // only a guard of the town it's for
            var done = log.HandToGuard("q1", "Corin");
            Assert.NotNull(done);
            Assert.Equal(QuestStage.Done, done.Stage);
            Assert.Equal(30, done.Reward);
            Assert.Empty(log.ForTheGuard("town:somewhere"));
            Assert.Null(log.HandToGuard("q1", "Corin")); // not twice
        }
    }

    [Fact]
    public void A_guard_is_anyone_who_keeps_the_town_but_not_a_guardian_of_the_wood()
    {
        Assert.True(QuestLog.KeepsTheTown(Someone("town guard", "t")));
        Assert.True(QuestLog.KeepsTheTown(Someone("watchman of the east gate", "t")));
        Assert.True(QuestLog.KeepsTheTown(Someone("warden of the gangway", "t")));
        Assert.False(QuestLog.KeepsTheTown(Someone("speaker for the guardians", "t")));
        Assert.False(QuestLog.KeepsTheTown(Someone("baker", "t")));
    }
}
