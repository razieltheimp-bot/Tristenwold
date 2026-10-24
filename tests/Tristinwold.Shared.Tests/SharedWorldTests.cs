using System.Net;
using System.Net.Sockets;
using Tristinwold.Nemesis;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

public class SharedWorldTests
{
    static ServerCore NewServer(ServerOptions? options = null) =>
        new(CampaignState.SavedMan(), options ?? new ServerOptions { RewindChance = 0, InterceptChance = 0 }, seed: 1);

    static Hello Hi(string player, string password = "") =>
        new(player, password, Wire.ProtocolVersion, [$"{player}-A", $"{player}-B", $"{player}-C"]);

    static IEnumerable<T> To<T>(IReadOnlyList<Outgoing> outs, string player) where T : Message =>
        outs.Where(o => o.To is null ? !string.Equals(o.Except, player, StringComparison.OrdinalIgnoreCase) : o.To == player)
            .Select(o => o.Message).OfType<T>();

    [Fact]
    public void Joining_gives_each_table_a_secret_keystone_from_its_own_party()
    {
        var server = NewServer();
        var outs = server.Connect(Hi("Joe"));
        Assert.Single(To<Welcome>(outs, "Joe"));
        Assert.Single(To<NemesisSnapshot>(outs, "Joe"));
        var board = Assert.Single(To<Board>(outs, "Joe"));
        Assert.Equal(6, board.Contracts.Count);

        var keystone = server.State.Groups["Joe"].Keystone;
        Assert.Contains(keystone, server.State.Groups["Joe"].Party);
        // Players never see a keystone or a contract secret until the DM says so.
        Assert.DoesNotContain(outs, o => o.Message is KeystoneRevealed);
        Assert.DoesNotContain("lighthouse cellar", Wire.Encode(board));
    }

    [Fact]
    public void Wrong_password_duplicate_name_and_old_versions_are_rejected()
    {
        var server = NewServer(new ServerOptions { Password = "ring" });
        Assert.IsType<Rejected>(server.Connect(Hi("Joe", "nope"))[0].Message);
        Assert.IsType<Welcome>(server.Connect(Hi("Joe", "ring"))[0].Message);
        Assert.IsType<Rejected>(server.Connect(Hi("joe", "ring"))[0].Message);
        Assert.IsType<Rejected>(server.Connect(Hi("Mira", "ring") with { ProtocolVersion = 0 })[0].Message);
    }

    [Fact]
    public void A_claimed_contract_is_gone_for_everyone_and_the_first_rescue_becomes_vann()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        var claim = server.Handle("Joe", new ClaimContract("brother"));
        Assert.Contains(To<Board>(claim, "Mira").Single().Contracts, c => c.Id == "brother" && c.Status == ContractStatus.Claimed);
        Assert.Single(To<Notice>(server.Handle("Mira", new ClaimContract("brother")), "Mira"), n => n.Kind == NoticeKind.Error);

        // The DM runs the story, so players can't close contracts themselves by default.
        Assert.Single(To<Notice>(server.Handle("Joe", new FinishContract("brother")), "Joe"), n => n.Kind == NoticeKind.Error);

        var (outs, note) = server.Dm("done brother") is var r ? (r.Outgoing, r.Reply) : default;
        Assert.Equal(1500, To<Reward>(outs, "Joe").Single().Gil);
        Assert.Empty(To<Reward>(outs, "Mira"));
        Assert.Equal("the fisherman's brother", server.State.Groups["Joe"].SavedMan);
        Assert.Contains("their Vann", note);

        server.Handle("Joe", new ClaimContract("debt"));
        server.Dm("done debt");
        Assert.Equal("the fisherman's brother", server.State.Groups["Joe"].SavedMan); // only the first one counts
    }

    [Fact]
    public void Groups_in_the_same_place_get_near_misses_and_find_each_others_tracks()
    {
        var now = new DateTime(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);
        var server = new ServerCore(CampaignState.SavedMan(), new ServerOptions { RewindChance = 0, InterceptChance = 0 }, seed: 2, clock: () => now);
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        server.Connect(Hi("Dax"));

        server.Handle("Joe", new Presence("The Docks", []));
        var arrive = server.Handle("Mira", new Presence("The Docks", []));
        Assert.Single(To<Notice>(arrive, "Mira"), n => n.Kind == NoticeKind.NearMiss && n.Text.Contains("Joe"));
        Assert.Single(To<Notice>(arrive, "Joe"), n => n.Kind == NoticeKind.NearMiss && n.Text.Contains("Mira"));
        Assert.Empty(To<Notice>(arrive, "Dax").Where(n => n.Kind == NoticeKind.NearMiss));

        server.Handle("Joe", new Presence("The Needle", []));
        now = now.AddMinutes(1);
        var tracks = server.Handle("Dax", new Presence("The Needle", []));
        Assert.Single(To<Notice>(tracks, "Dax"), n => n.Text.Contains("Joe"));
    }

    [Fact]
    public void Dead_drops_reach_the_next_group_unless_vann_intercepts_them()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        server.Handle("Joe", new LeaveDrop("Gallows Perch", "Don't trust the man from the Needle."));
        var drops = To<Drops>(server.Handle("Mira", new Presence("Gallows Perch", [])), "Mira").Single();
        Assert.Equal("Joe", Assert.Single(drops.Notes).From);

        var vann = new ServerCore(CampaignState.SavedMan(), new ServerOptions { InterceptChance = 1, RewindChance = 0 }, seed: 3);
        vann.Connect(Hi("Joe"));
        vann.Connect(Hi("Mira"));
        vann.Handle("Joe", new LeaveDrop("Gallows Perch", "secret"));
        Assert.Empty(To<Drops>(vann.Handle("Mira", new Presence("Gallows Perch", [])), "Mira"));
        Assert.Contains("*", vann.Dm("drops").Reply);
        var released = vann.Dm("release 0").Outgoing;
        Assert.Single(To<Drops>(released, "Mira"));
    }

    [Fact]
    public void Each_table_fights_in_its_own_game_but_shares_one_nemesis_world()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        // Joe's party is wiped in his own game. He can't claim it happened to Mira.
        var report = new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, 7, "Sahagin", 900, Killed: false, Escaped: false)], Players: ["Mira"]);
        var outs = server.Handle("Joe", new ReportBattle(report));

        Assert.Single(To<NemesisEvents>(outs, "Mira"));
        var snapshot = To<NemesisSnapshot>(outs, "Mira").Single();
        var miraWorld = NemesisWorld.FromJson(snapshot.WorldJson);
        var nemesis = Assert.Single(miraWorld.Living);
        Assert.True(nemesis.Players.ContainsKey("Joe"));
        Assert.False(nemesis.Players.ContainsKey("Mira"));

        // And in Mira's game, that nemesis can show up and taunt her about Joe.
        var spawns = NemesisWorld.FromJson(snapshot.WorldJson, new NemesisRules { AppearanceChance = 1, HomeAreaAppearanceChance = 1 })
            .PlanEncounter("The Docks", [new FormationSlot(1, 7, "Sahagin")], ["Mira"]);
        Assert.Contains("Joe", Assert.Single(spawns).Taunt);
    }

    [Fact]
    public void Dm_reveals_a_keystone_to_every_table()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));
        var (reply, outs) = server.Dm("keystone Joe Joe-B");
        Assert.Contains("Joe-B", reply);
        Assert.Equal(new KeystoneRevealed("Joe", "Joe-B"), To<KeystoneRevealed>(outs, "Mira").Single());
    }

    [Fact]
    public void Campaign_state_round_trips_through_its_save_file()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Handle("Joe", new ClaimContract("rats"));
        server.Handle("Joe", new ReportBattle(new BattleReport("Sewers", BattleResult.PartyEscaped,
            [new EnemyReport(0, 3, "Giant Rat", 200, false, false)], ["Joe"])));
        var path = Path.GetTempFileName();
        server.State.Save(path);

        var loaded = CampaignState.Load(path);
        Assert.Equal(ContractStatus.Claimed, loaded.Contracts.Single(c => c.Id == "rats").Status);
        Assert.NotNull(loaded.Groups["Joe"].Keystone);
        var restored = new ServerCore(loaded, seed: 4);
        Assert.Single(restored.World.Living);
    }

    [Fact]
    public async Task Game_clients_talk_to_a_real_server_over_tcp()
    {
        var core = NewServer();
        var gate = new Lock();
        var sessions = new Dictionary<string, LineConnection>();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        void Deliver(IEnumerable<Outgoing> outs)
        {
            foreach (var o in outs)
                foreach (var (name, conn) in sessions.ToList())
                    if (o.To is null ? name != o.Except : name == o.To)
                        _ = conn.WriteAsync(o.Message, cts.Token);
        }

        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var tcp = await listener.AcceptTcpClientAsync(cts.Token);
                _ = Task.Run(async () =>
                {
                    var conn = new LineConnection(tcp.GetStream());
                    var hello = (Hello)(await conn.ReadAsync(cts.Token))!;
                    lock (gate) { sessions[hello.Player] = conn; Deliver(core.Connect(hello)); }
                    while (await conn.ReadAsync(cts.Token) is { } m)
                        lock (gate) Deliver(core.Handle(hello.Player, m));
                });
            }
        });

        using var joe = new GameClient("127.0.0.1", port, () => Hi("Joe"));
        using var mira = new GameClient("127.0.0.1", port, () => Hi("Mira"));
        joe.Start();
        mira.Start();

        async Task<T> Next<T>(GameClient client) where T : Message
        {
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();
                if (client.TryReceive(out var m) && m is T t) return t;
                await Task.Delay(10, cts.Token);
            }
        }

        await Next<Welcome>(joe);
        await Next<Welcome>(mira);
        Assert.Equal(ClientStatus.Connected, joe.Status);

        joe.Send(new Say("Meet at the Gallows Perch."));
        var chat = await Next<Notice>(mira);
        while (chat.Kind != NoticeKind.Chat) chat = await Next<Notice>(mira);
        Assert.Equal("Joe", chat.From);
        Assert.Equal("Meet at the Gallows Perch.", chat.Text);

        mira.Send(new ClaimContract("chapel"));
        Board board;
        do board = await Next<Board>(joe); while (board.Contracts.Single(c => c.Id == "chapel").Status != ContractStatus.Claimed);
        Assert.Equal("Mira", board.Contracts.Single(c => c.Id == "chapel").ClaimedBy);
        cts.Cancel();
        listener.Stop();
    }
}
