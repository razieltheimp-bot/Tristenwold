// The shared-world server. Anyone can run it, even someone who is also playing: Vann runs
// the story, and the console doesn't spoil his schemes. Every company's game connects to it.
//
//   dotnet run --project tools/Tristinwold.Server -- [--port 7777] [--http-port 7778]
//       [--password secret] [--campaign tristinwold.json] [--dm] [--no-vann]
//       [--vann-minutes 6] [--voice claude|off] [--spoilers] [--tunnel [--cloudflared path]]
//
// --port       TCP (JSON lines) for tools and bots
// --http-port  HTTP for phones, tunnels and the Skyrim add-on (SkyrimPlatform can only make HTTP requests)
// --dm         a human DM closes contracts from this window (players can't close their own)
// --no-vann    Vann doesn't scheme on his own
// --voice      claude: Claude voices Vann (needs ANTHROPIC_API_KEY); off: written lines only.
//              Defaults to claude when an API key is set.
// --spoilers   print private messages and Vann's lines here (for a DM who isn't playing)
// --tunnel     open a Cloudflare quick tunnel to the HTTP port and print its https:// address,
//              so phones and friends anywhere can join without port forwarding (needs cloudflared)
//
// DM commands still work in this window (help for the list), but nothing needs them.
using System.Net;
using System.Net.Sockets;
using Anthropic;
using Tristinwold.Shared;

var port = Wire.DefaultPort;
var httpPort = Wire.DefaultPort + 1;
var password = "";
var campaignPath = "tristinwold.json";
var humanDm = false;
var vann = true;
var vannMinutes = 6.0;
var spoilers = false;
string? voiceChoice = null;
var tunnel = false;
var cloudflared = "cloudflared";
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--http-port": httpPort = int.Parse(args[++i]); break;
        case "--password": password = args[++i]; break;
        case "--campaign": campaignPath = args[++i]; break;
        case "--dm": humanDm = true; break;
        case "--no-vann": vann = false; break;
        case "--vann-minutes": vannMinutes = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--voice": voiceChoice = args[++i]; break;
        case "--spoilers": spoilers = true; break;
        case "--tunnel": tunnel = true; break;
        case "--cloudflared": cloudflared = args[++i]; tunnel = true; break;
        default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 1;
    }
}

void Log(string text) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");

var state = File.Exists(campaignPath) ? CampaignState.Load(campaignPath) : CampaignState.SavedMan();
var core = new ServerCore(state, new ServerOptions
{
    Password = password,
    PlayersFinishContracts = !humanDm,
    VannEnabled = vann,
    VannMinutesBetweenSchemes = vannMinutes,
});

var hasKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
          || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN"));
IVannVoice? voice = (voiceChoice ?? (hasKey ? "claude" : "off")) switch
{
    "claude" => new ClaudeVannVoice(new AnthropicClient(), Log),
    "off" => null,
    var other => throw new ArgumentException($"--voice must be claude or off, not {other}"),
};

var host = new SharedWorldHost(core, () => state.Save(campaignPath), voice, Log, spoilers);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

// ---- TCP (tools, bots) ---------------------------------------------------------------------------

async Task ServeTcpAsync(TcpClient tcp)
{
    using var tcpScope = tcp;
    using var conn = new LineConnection(tcp.GetStream());
    var sink = new TcpSink(conn, shutdown.Token);
    string? player = null;
    try
    {
        if (await conn.ReadAsync(shutdown.Token) is not Hello hello) return;
        player = host.Join(hello, sink);
        if (player is null) return;
        while (await conn.ReadAsync(shutdown.Token) is { } message) host.Receive(player, message);
    }
    catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Text.Json.JsonException)
    {
        if (ex is System.Text.Json.JsonException) Log($"Bad message from {player ?? tcp.Client.RemoteEndPoint?.ToString()}: {ex.Message}");
    }
    finally
    {
        if (player is not null) host.Leave(player, sink);
    }
}

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

// ---- HTTP (the Skyrim add-on) --------------------------------------------------------------------

using var gateway = new HttpGateway(host, $"http://+:{httpPort}/", Log);
try
{
    gateway.Start();
}
catch (HttpListenerException ex)
{
    Log($"Could not listen for HTTP on port {httpPort} ({ex.Message}). On Windows, run once as administrator or: " +
        $"netsh http add urlacl url=http://+:{httpPort}/ user=Everyone");
    return 1;
}

Log($"Tristinwold server: campaign \"{state.Campaign}\". PC games connect on TCP {port}; phones, tunnels and the Skyrim add-on on HTTP {httpPort}.");
Log($"Saving to {Path.GetFullPath(campaignPath)}.");
Log(tunnel
    ? "Friends at home connect to this computer's address; everyone else uses the tunnel's address below."
    : "Friends connect to this computer's address. From anywhere else (phones too), restart with --tunnel, or use a VPN like Tailscale.");
Log(vann
    ? $"Vann is playing{(voice is null ? " (written lines; set ANTHROPIC_API_KEY for Claude's voice)" : ", voiced by Claude")}. {(humanDm ? "The DM closes contracts from this window." : "Players close their own contracts.")}"
    : "Vann is not scheming on his own (--no-vann).");
Log("DM commands are optional; type 'help' to see them.");

using var cloudflare = tunnel ? new Tunnel(cloudflared, httpPort, Log) : null;
if (cloudflare is not null && cloudflare.Start(url =>
    {
        Log($"Cloudflare tunnel open. On a phone (or anywhere), enter this as the server: {url}");
        if (password.Length == 0) Log("Anyone with that address can join. Start the server with --password to keep it to friends.");
    }))
    Log("Opening a Cloudflare tunnel...");

// Vann's own clock: his schemes come when they come.
_ = Task.Run(async () =>
{
    while (!shutdown.IsCancellationRequested)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), shutdown.Token); } catch (OperationCanceledException) { break; }
        host.Tick();
    }
});

_ = Task.Run(() =>
{
    while (!shutdown.IsCancellationRequested && Console.In.ReadLine() is { } line)
    {
        if (line.Trim().Length == 0) continue;
        var reply = host.Dm(line);
        if (reply.Length > 0) Console.WriteLine(reply);
    }
});

try
{
    while (!shutdown.IsCancellationRequested)
    {
        var tcp = await listener.AcceptTcpClientAsync(shutdown.Token);
        tcp.NoDelay = true;
        _ = Task.Run(() => ServeTcpAsync(tcp));
    }
}
catch (OperationCanceledException) { }

host.SaveNow();
Log("Server stopped.");
return 0;

sealed class TcpSink(LineConnection conn, CancellationToken ct) : IClientSink
{
    public void Send(Message message) => _ = conn.WriteAsync(message, ct).ContinueWith(_ => { }, TaskScheduler.Default);
}
