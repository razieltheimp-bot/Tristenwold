using Tristinwold.Nemesis;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>Vann playing the shared world with no DM.</summary>
public class VannTests
{
    static Hello Hi(string player) => new(player, "", Wire.ProtocolVersion, [$"{player}-A", $"{player}-B", $"{player}-C"]);

    static IEnumerable<T> To<T>(IEnumerable<Outgoing> outs, string player) where T : Message =>
        outs.Where(o => o.To is null ? !string.Equals(o.Except, player, StringComparison.OrdinalIgnoreCase) : o.To == player)
            .Select(o => o.Message).OfType<T>();

    static ServerCore Server(ServerOptions options, Func<DateTime>? clock = null)
    {
        var s = new ServerCore(CampaignState.SavedMan(), options, seed: 5, clock: clock);
        s.Connect(Hi("Joe"));
        s.Connect(Hi("Mira"));
        return s;
    }

    [Fact]
    public void Without_a_dm_players_close_their_own_contracts_and_the_saved_man_attaches_himself()
    {
        var server = Server(new ServerOptions { RivalJobChance = 0, ContractsBeforeReveal = 2 });
        server.Handle("Joe", new ClaimContract("brother"));
        var done = server.Handle("Joe", new FinishContract("brother"));

        Assert.Single(To<Reward>(done, "Joe"));
        var thanks = To<Notice>(done, "Joe").Single(n => n.Kind == NoticeKind.Vann);
        Assert.Equal("The fisherman's brother", thanks.From); // a friend, as far as Joe knows
        Assert.Contains(done, o => o.Scene is { Trick: "grateful", Revealed: false });
    }

    [Fact]
    public void After_enough_jobs_the_saved_man_reveals_himself_and_the_other_tables_hear_of_it()
    {
        var server = Server(new ServerOptions { RivalJobChance = 0, ContractsBeforeReveal = 2 });
        server.Handle("Joe", new ClaimContract("brother"));
        server.Handle("Joe", new FinishContract("brother"));
        server.Handle("Joe", new ClaimContract("rats"));
        var reveal = server.Handle("Joe", new FinishContract("rats"));

        Assert.True(server.State.Groups["Joe"].VannRevealed);
        Assert.Contains(To<Notice>(reveal, "Joe"), n => n.From == "Vann" && n.Text.Contains("You always save me"));
        Assert.Contains(To<Notice>(reveal, "Mira"), n => n.Kind == NoticeKind.Vann && n.Text.Contains("Joe"));
        Assert.DoesNotContain(To<Notice>(reveal, "Mira"), n => n.Text.Contains("fisherman"));
    }

    [Fact]
    public void Claiming_a_job_can_get_another_company_hired_against_you()
    {
        var server = Server(new ServerOptions { RivalJobChance = 1 });
        var outs = server.Handle("Joe", new ClaimContract("debt"));
        var rival = To<Board>(outs, "Mira").Last().Contracts.Single(c => c.Id.StartsWith("rival-debt"));
        Assert.Equal("A well-dressed stranger", rival.PostedBy);
        Assert.Contains(To<Notice>(outs, "Mira"), n => n.Text.Contains("well-dressed stranger"));
        Assert.DoesNotContain(To<Notice>(outs, "Joe"), n => n.Text.Contains("well-dressed stranger"));

        // Vann doesn't pile rivals onto his own rival jobs.
        var again = server.Handle("Mira", new ClaimContract(rival.Id));
        Assert.Single(To<Board>(again, "Joe"));
    }

    [Fact]
    public void Some_dead_drops_arrive_rewritten_and_only_the_dm_sees_the_original()
    {
        var server = Server(new ServerOptions { ForgeChance = 1, InterceptChance = 0 });
        server.Handle("Mira", new Presence("The Needle", []));
        var outs = server.Handle("Joe", new LeaveDrop("The Needle", "Brother is in the cellar."));
        var note = To<Drops>(outs, "Mira").Single().Notes.Single();
        Assert.DoesNotContain("cellar", note.Text);
        Assert.Contains("- Joe", note.Text); // signed with Joe's name, which is the point
        Assert.Contains("really: Brother is in the cellar.", server.Dm("drops").Reply);
    }

    [Fact]
    public void Two_keystones_in_one_place_stop_time_and_are_revealed()
    {
        var server = Server(new ServerOptions { RewindChance = 0, KeystoneMeetChance = 1 });
        server.Handle("Joe", new Presence("The Exchange", []));
        var outs = server.Handle("Mira", new Presence("The Exchange", []));

        Assert.Equal(2, To<KeystoneRevealed>(outs, "Joe").Count());
        Assert.Contains(To<Notice>(outs, "Mira"), n => n.Text.Contains("time stops"));
        Assert.Contains(outs, o => o.Scene is { Trick: "keystones_met" });

        // It only happens once per pair.
        server.Handle("Joe", new Presence("The Docks", []));
        Assert.Empty(To<KeystoneRevealed>(server.Handle("Joe", new Presence("The Exchange", [])), "Joe"));
    }

    [Fact]
    public void Vann_schemes_on_his_own_schedule()
    {
        var now = new DateTime(2026, 10, 24, 21, 0, 0, DateTimeKind.Utc);
        var server = Server(new ServerOptions { VannMinutesBetweenSchemes = 5 }, () => now);
        server.Handle("Joe", new ClaimContract("brother"));
        server.Handle("Joe", new FinishContract("brother")); // Joe now travels with his saved man
        server.Handle("Joe", new ReportBattle(new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, 7, "Sahagin", 900, false, false)], ["Joe"])));

        Assert.Empty(server.Tick()); // first tick only schedules
        var schemes = new List<Outgoing>();
        for (var minute = 0; minute < 240; minute++)
        {
            now = now.AddMinutes(1);
            schemes.AddRange(server.Tick());
        }

        var tricks = schemes.Where(o => o.Scene is not null).Select(o => o.Scene!.Trick).Distinct().ToList();
        Assert.True(schemes.Count >= 20, $"only {schemes.Count} messages in four hours");
        Assert.Contains("steer", tricks);
        Assert.Contains("slander", tricks);
        Assert.Contains("set_nemesis", tricks);
        Assert.Contains("omen", tricks);
        // Setting a nemesis on Joe makes it stronger in the shared world.
        Assert.True(server.World.Living.Single().Power > 40);
        Assert.NotEmpty(server.State.VannMemory);
    }

    [Fact]
    public void Nobody_online_means_no_schemes()
    {
        var now = DateTime.UtcNow;
        var server = new ServerCore(CampaignState.SavedMan(), new ServerOptions(), seed: 6, clock: () => now);
        server.Tick();
        now = now.AddHours(5);
        Assert.Empty(server.Tick());
    }
}
