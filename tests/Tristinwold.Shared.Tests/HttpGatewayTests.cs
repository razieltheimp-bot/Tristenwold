using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>The HTTP path browser clients use: join once, then sync every second.</summary>
public class HttpGatewayTests
{
    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    static async Task<(HttpStatusCode Status, SyncResponse Body)> Post(HttpClient http, string path, string json)
    {
        var res = await http.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
        var body = JsonSerializer.Deserialize<SyncResponse>(await res.Content.ReadAsStringAsync(), Wire.Options)!;
        return (res.StatusCode, body);
    }

    static string Join(string player, string password = "") =>
        Wire.Encode(new Hello(player, password, Wire.ProtocolVersion, [$"{player}-A", $"{player}-B", $"{player}-C"]));

    static string Sync(string session, params Message[] messages) =>
        JsonSerializer.Serialize(new SyncRequest(session, [.. messages]), Wire.Options);

    [Fact]
    public async Task Two_companies_join_talk_and_take_contracts_over_http()
    {
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, RewindChance = 0, KeystoneMeetChance = 0 }, seed: 1);
        var host = new SharedWorldHost(core, () => { });
        var port = FreePort();
        using var gateway = new HttpGateway(host, $"http://127.0.0.1:{port}/");
        gateway.Start();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var (s1, joe) = await Post(http, "/v1/join", Join("Joe"));
        var (_, mira) = await Post(http, "/v1/join", Join("Mira"));
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.NotNull(joe.Session);
        Assert.Contains(joe.Messages, m => m is Welcome);
        Assert.Contains(joe.Messages, m => m is Board);

        await Post(http, "/v1/sync", Sync(joe.Session!, new Say("Meet at the Bannered Mare."), new ClaimContract("brother")));
        var (_, miraSync) = await Post(http, "/v1/sync", Sync(mira.Session!));
        Assert.Contains(miraSync.Messages, m => m is Notice { Kind: NoticeKind.Chat, From: "Joe" } n && n.Text.Contains("Bannered Mare"));
        Assert.Contains(miraSync.Messages, m => m is Board b && b.Contracts.Any(c => c.Id == "brother" && c.ClaimedBy == "Joe"));

        // A phone's hero walking by reaches the PC's game on its next sync.
        await Post(http, "/v1/sync", Sync(mira.Session!, new Moved("Wizard", null, "Mira", 4, 0, 2, 0.3f, 3.8f, null, 0)));
        var (_, joeSync) = await Post(http, "/v1/sync", Sync(joe.Session!));
        Assert.Contains(joeSync.Messages, m => m is Seen { Player: "Mira", Where.Look: "Wizard" });

        var (expired, _) = await Post(http, "/v1/sync", Sync("not-a-session"));
        Assert.Equal(HttpStatusCode.Unauthorized, expired);

        // A game that stops syncing is logged out.
        gateway.ExpireIdle(DateTime.UtcNow + HttpGateway.SessionTimeout + TimeSpan.FromSeconds(1));
        Assert.Empty(core.Online);
        var (afterExpiry, _) = await Post(http, "/v1/sync", Sync(joe.Session!));
        Assert.Equal(HttpStatusCode.Unauthorized, afterExpiry);
    }

    [Fact]
    public async Task A_rejected_join_says_why()
    {
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { Password = "ring" }, seed: 2);
        var port = FreePort();
        using var gateway = new HttpGateway(new SharedWorldHost(core, () => { }), $"http://127.0.0.1:{port}/");
        gateway.Start();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var (status, body) = await Post(http, "/v1/join", Join("Joe", "wrong"));
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Null(body.Session);
        Assert.Contains("password", Assert.IsType<Rejected>(Assert.Single(body.Messages)).Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_phone_over_https_and_a_pc_over_tcp_share_the_world()
    {
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, RewindChance = 0, KeystoneMeetChance = 0 }, seed: 1);
        var host = new SharedWorldHost(core, () => { });
        var httpPort = FreePort();
        using var gateway = new HttpGateway(host, $"http://127.0.0.1:{httpPort}/");
        gateway.Start();
        var tcpPort = FreePort();
        using var server = new TcpListener(IPAddress.Loopback, tcpPort);
        server.Start();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var tcp = await server.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    var conn = new LineConnection(tcp.GetStream());
                    var hello = (Hello)(await conn.ReadAsync(CancellationToken.None))!;
                    var player = host.Join(hello, new Sink(conn));
                    while (player is not null && await conn.ReadAsync(CancellationToken.None) is { } m) host.Receive(player, m);
                });
            }
        });

        Hello Hi(string who, string password = "") => new(who, password, Wire.ProtocolVersion, [who]);
        using var phone = IGameLink.Open($"http://127.0.0.1:{httpPort}", () => Hi("Mira"));
        using var pc = IGameLink.Open($"127.0.0.1:{tcpPort}", () => Hi("Joe"));
        Assert.IsType<HttpGameClient>(phone);
        Assert.IsType<GameClient>(pc);
        phone.Start();
        pc.Start();

        async Task<T> Next<T>(IGameLink link, Func<T, bool>? match = null) where T : Message
        {
            for (var i = 0; i < 200; i++)
            {
                while (link.TryReceive(out var m)) if (m is T t && (match?.Invoke(t) ?? true)) return t;
                await Task.Delay(25);
            }
            throw new TimeoutException($"no {typeof(T).Name}");
        }

        await Next<Welcome>(phone);
        await Next<Welcome>(pc);
        Assert.Equal(ClientStatus.Connected, phone.Status);
        phone.Send(new Say("Typed on a phone."));
        var heard = await Next<Notice>(pc, n => n.Kind == NoticeKind.Chat);
        Assert.Equal("Mira", heard.From);
        pc.Send(new Say("Heard you."));
        Assert.Equal("Joe", (await Next<Notice>(phone, n => n.Kind == NoticeKind.Chat && n.From == "Joe")).From);
    }

    [Fact]
    public async Task A_wrong_password_over_https_is_turned_away()
    {
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, Password = "secret" }, seed: 1);
        var host = new SharedWorldHost(core, () => { });
        var port = FreePort();
        using var gateway = new HttpGateway(host, $"http://127.0.0.1:{port}/");
        gateway.Start();
        using var phone = new HttpGameClient($"http://127.0.0.1:{port}/", () => new Hello("Mira", "guess", Wire.ProtocolVersion, ["Mira"]));
        phone.Start();
        for (var i = 0; i < 200 && phone.Status != ClientStatus.Rejected; i++) await Task.Delay(25);
        Assert.Equal(ClientStatus.Rejected, phone.Status);
        Assert.True(phone.TryReceive(out var m) && m is Rejected);
    }

    sealed class Sink(LineConnection conn) : IClientSink
    {
        public void Send(Message message) => _ = conn.WriteAsync(message, CancellationToken.None);
    }
}
