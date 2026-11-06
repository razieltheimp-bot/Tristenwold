using System.Net;
using System.Net.Sockets;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

public class SharedWorldTests
{
    // Vann off unless a test is about him, so the dice don't interfere.
    static readonly ServerOptions Quiet = new() { RewindChance = 0, InterceptChance = 0, VannEnabled = false, KeystoneMeetChance = 0 };

    static ServerCore NewServer(ServerOptions? options = null) => new(CampaignState.SavedMan(), options ?? Quiet, seed: 1);

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
        Assert.Single(To<DeviantSnapshot>(outs, "Joe"));
        var board = Assert.Single(To<Board>(outs, "Joe"));
        Assert.Equal(6 + Atlas.Default.Cities.Sum(c => c.Contracts.Count), board.Contracts.Count);

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
        var server = NewServer(new ServerOptions { VannEnabled = false, PlayersFinishContracts = false });
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        var claim = server.Handle("Joe", new ClaimContract("brother"));
        Assert.Contains(To<Board>(claim, "Mira").Single().Contracts, c => c.Id == "brother" && c.Status == ContractStatus.Claimed);
        Assert.Single(To<Notice>(server.Handle("Mira", new ClaimContract("brother")), "Mira"), n => n.Kind == NoticeKind.Error);

        // With a DM running the story, players can't close contracts themselves.
        Assert.Single(To<Notice>(server.Handle("Joe", new FinishContract("brother")), "Joe"), n => n.Kind == NoticeKind.Error);

        var (outs, note) = server.Dm("done brother") is var r ? (r.Outgoing, r.Reply) : default;
        Assert.Equal(1500, To<Reward>(outs, "Joe").Single().Coppers);
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
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 2, clock: () => now);
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
    public void Heroes_see_each_other_move_and_a_newcomer_sees_everyone_already_there()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        var step = new Moved("Knight", "head=1", "Joe", 10, 0.5f, -3, 1.2f, 3.8f, "Attack", 1);
        var outs = server.Handle("Joe", step);
        var seen = Assert.Single(To<Seen>(outs, "Mira"));
        Assert.Equal("Joe", seen.Player);
        Assert.Equal(step, seen.Where);
        Assert.Empty(To<Seen>(outs, "Joe")); // not echoed back

        // Whoever joins later is told where everyone already is, and nothing about themselves.
        var dax = server.Connect(Hi("Dax"));
        Assert.Equal("Joe", Assert.Single(To<Seen>(dax, "Dax")).Player);

        // Gone is gone.
        server.Disconnect("Joe");
        Assert.Empty(To<Seen>(server.Connect(Hi("Pell")), "Pell"));
    }

    [Fact]
    public void A_blow_between_heroes_goes_to_the_one_struck_and_a_fall_pays_the_winner_once_an_hour()
    {
        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var server = new ServerCore(CampaignState.SavedMan(), Quiet, seed: 1, clock: () => now);
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        var hit = Assert.Single(To<HeroStruck>(server.Handle("Joe", new StrikeHero("mira", 42, DamageKind.Fire, AttackKind.Spell, true)), "Mira"));
        Assert.Equal(("Joe", 42.0, DamageKind.Fire, true), (hit.From, hit.Damage, hit.Kind, hit.Crit));
        Assert.Empty(server.Handle("Joe", new StrikeHero("Joe", 42, DamageKind.Fire, AttackKind.Spell, false))); // not yourself
        Assert.Empty(server.Handle("Joe", new StrikeHero("Nobody", 42, DamageKind.Fire, AttackKind.Spell, false)));
        Assert.Empty(server.Handle("Joe", new StrikeHero("Mira", double.NaN, DamageKind.Fire, AttackKind.Spell, false)));

        var outs = server.Handle("Mira", new HeroFell("Joe", [new("healing_potion", 3)], 120, 45, 40));
        var spoils = Assert.Single(To<HeroSpoils>(outs, "Joe"));
        Assert.Equal(("Mira", 120, 45, 40), (spoils.From, spoils.Crowns, spoils.GearCrowns, spoils.Bounty));
        Assert.Equal("healing_potion", Assert.Single(spoils.Items).Id);
        Assert.Contains(outs, o => o.Message is Notice { Kind: NoticeKind.World } n && n.Text.Contains("collects the 40 crowns"));

        // Beat her again within the hour: nothing more. An hour on, it pays again.
        var again = server.Handle("Mira", new HeroFell("Joe", [new("healing_potion", 3)], 120, 45, 0));
        Assert.Empty(To<HeroSpoils>(again, "Joe"));
        Assert.Contains(again, o => o.To == "Joe" && o.Message is Notice n && n.Text.Contains("nothing more to win"));
        Assert.Single(To<HeroSpoils>(server.Handle("Joe", new HeroFell("Mira", [], 5, 0, 0)), "Mira")); // the other way round is its own count
        now = now.AddMinutes(61);
        Assert.Single(To<HeroSpoils>(server.Handle("Mira", new HeroFell("Joe", [], 120, 45, 0)), "Joe"));

        // And it all crosses the wire.
        var back = Assert.IsType<HeroSpoils>(Wire.Decode(Wire.Encode(spoils)));
        Assert.Equal((120, 3), (back.Crowns, Assert.Single(back.Items).N));
        Assert.IsType<HeroStruck>(Wire.Decode(Wire.Encode(hit)));
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

        var vann = new ServerCore(CampaignState.SavedMan(), new ServerOptions { InterceptChance = 1, RewindChance = 0, VannEnabled = false }, seed: 3);
        vann.Connect(Hi("Joe"));
        vann.Connect(Hi("Mira"));
        vann.Handle("Joe", new LeaveDrop("Gallows Perch", "secret"));
        Assert.Empty(To<Drops>(vann.Handle("Mira", new Presence("Gallows Perch", [])), "Mira"));
        Assert.Contains("*", vann.Dm("drops").Reply);
        var released = vann.Dm("release 0").Outgoing;
        Assert.Single(To<Drops>(released, "Mira"));
    }

    [Fact]
    public void Each_table_fights_in_its_own_game_but_shares_one_deviant_world()
    {
        var server = NewServer();
        server.Connect(Hi("Joe"));
        server.Connect(Hi("Mira"));

        // Joe's party is wiped in his own game. He can't claim it happened to Mira.
        var report = new BattleReport("The Docks", BattleResult.Defeat,
            [new EnemyReport(0, "Draugr", "Draugr Wight", 900, Killed: false, Escaped: false)], Players: ["Mira"]);
        var outs = server.Handle("Joe", new ReportBattle(report));

        Assert.Single(To<DeviantEvents>(outs, "Mira"));
        var snapshot = To<DeviantSnapshot>(outs, "Mira").Single();
        var miraWorld = DeviantWorld.FromJson(snapshot.WorldJson);
        var deviant = Assert.Single(miraWorld.Living);
        Assert.True(deviant.Players.ContainsKey("Joe"));
        Assert.False(deviant.Players.ContainsKey("Mira"));

        // And in Mira's game, that deviant can show up and taunt her about Joe.
        var spawns = DeviantWorld.FromJson(snapshot.WorldJson, new DeviantRules { AppearanceChance = 1, HomeAreaAppearanceChance = 1 })
            .PlanEncounter("The Docks", [new FormationSlot(1, "Draugr", "Draugr Wight")], ["Mira"]);
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
            [new EnemyReport(0, "Giant Rat", "Giant Rat", 200, false, false)], ["Joe"])));
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

        // One city for both: cargo Joe lands is in Mira's market on the server's next tick.
        double fish;
        lock (gate) fish = core.City.Stock[Gameplay.Good.Fish];
        joe.Send(new CityTrade(Gameplay.CityAction.Cargo(Gameplay.Good.Fish, 10)));
        CitySnapshot? city = null;
        while (city is null || city.City.Stock[Gameplay.Good.Fish] < fish + 9.9)
        {
            await Task.Delay(20, cts.Token);
            lock (gate) Deliver(core.Tick());
            while (mira.TryReceive(out var m)) if (m is CitySnapshot c) city = c;
        }
        Assert.Equal(fish + 10, city.City.Stock[Gameplay.Good.Fish], 3);
        cts.Cancel();
        listener.Stop();
    }
}
