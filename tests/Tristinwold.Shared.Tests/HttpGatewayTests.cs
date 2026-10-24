using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>The HTTP path the SkyrimPlatform add-on uses: join once, then sync every second.</summary>
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
}
