// The shared-world server. Anyone can run it, even someone who is also playing: Vann runs
// the story, and the console doesn't spoil his schemes. Every friend's game connects to it.
//
//   dotnet run --project tools/Tristinwold.Server -- [--port 7777] [--password secret]
//       [--campaign tristinwold.json] [--dm] [--no-vann] [--vann-minutes 6]
//       [--voice claude|off] [--spoilers]
//
// --dm        a human DM closes contracts from this window (players can't close their own)
// --no-vann   Vann doesn't scheme on his own
// --voice     claude: Claude voices Vann (needs ANTHROPIC_API_KEY); off: written lines only.
//             Defaults to claude when an API key is set.
// --spoilers  print private messages and Vann's lines here (for a DM who isn't playing)
//
// DM commands still work in this window (help for the list), but nothing needs them.
using System.Net;
using System.Net.Sockets;
using Anthropic;
using Tristinwold.Shared;

var port = Wire.DefaultPort;
var password = "";
var campaignPath = "tristinwold.json";
var humanDm = false;
var vann = true;
var vannMinutes = 6.0;
var spoilers = false;
string? voiceChoice = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--password": password = args[++i]; break;
        case "--campaign": campaignPath = args[++i]; break;
        case "--dm": humanDm = true; break;
        case "--no-vann": vann = false; break;
        case "--vann-minutes": vannMinutes = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--voice": voiceChoice = args[++i]; break;
        case "--spoilers": spoilers = true; break;
        default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 1;
    }
}

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
    "claude" => new ClaudeVannVoice(new AnthropicClient(), text => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}")),
    "off" => null,
    var other => throw new ArgumentException($"--voice must be claude or off, not {other}"),
};
var gate = new Lock();
var sessions = new Dictionary<string, LineConnection>(StringComparer.OrdinalIgnoreCase);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

void Log(string text) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");

void SaveIfDirty()
{
    if (!core.Dirty) return;
    core.Dirty = false;
    try { state.Save(campaignPath); }
    catch (Exception ex) { Log($"Could not save {campaignPath}: {ex.Message}"); }
}

// Call with the gate held.
void Send(Outgoing o)
{
    IEnumerable<KeyValuePair<string, LineConnection>> targets = o.To is null
        ? sessions.Where(s => !s.Key.Equals(o.Except, StringComparison.OrdinalIgnoreCase)).ToList()
        : sessions.Where(s => s.Key.Equals(o.To, StringComparison.OrdinalIgnoreCase)).ToList();
    foreach (var (_, conn) in targets)
        _ = conn.WriteAsync(o.Message, shutdown.Token).ContinueWith(_ => { }, TaskScheduler.Default);
    // The host may be playing too: only public news is printed, unless asked for spoilers.
    if (o.Message is Notice { Kind: not NoticeKind.Error } n && (spoilers || (o.To is null && n.Kind != NoticeKind.Vann)))
        Log($"{(o.To is null ? "all" : o.To)}: {(n.From is null ? "" : n.From + ": ")}{n.Text}");
}

// Call with the gate held. Vann's lines go out once his voice has worded them.
void Deliver(IReadOnlyList<Outgoing> outgoing)
{
    foreach (var o in outgoing)
    {
        if (voice is not null && o.Scene is not null && o.Message is Notice notice)
            _ = VoiceAsync(o, notice, [.. state.VannMemory]);
        else
            Send(o);
    }
}

async Task VoiceAsync(Outgoing o, Notice notice, List<string> recent)
{
    string? line = null;
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        line = await voice!.SpeakAsync(o.Scene!, recent, timeout.Token);
    }
    catch (Exception ex) when (ex is not OutOfMemoryException) { }
    lock (gate) Send(o with { Message = notice with { Text = line ?? notice.Text } });
}

async Task ServeAsync(TcpClient tcp)
{
    var endpoint = tcp.Client.RemoteEndPoint;
    using var tcpScope = tcp;
    using var conn = new LineConnection(tcp.GetStream());
    string? player = null;
    try
    {
        if (await conn.ReadAsync(shutdown.Token) is not Hello hello)
            return;
        lock (gate)
        {
            var outgoing = core.Connect(hello);
            if (outgoing.Count > 0 && outgoing[0].Message is Rejected rejected)
            {
                Log($"Rejected {hello.Player} from {endpoint}: {rejected.Reason}");
                _ = conn.WriteAsync(rejected, shutdown.Token);
                return;
            }
            player = hello.Player.Trim();
            sessions[player] = conn;
            Log($"{player} connected from {endpoint}.");
            Deliver(outgoing);
            SaveIfDirty();
        }

        while (await conn.ReadAsync(shutdown.Token) is { } message)
        {
            lock (gate)
            {
                Deliver(core.Handle(player, message));
                SaveIfDirty();
            }
        }
    }
    catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Text.Json.JsonException)
    {
        if (ex is System.Text.Json.JsonException) Log($"Bad message from {player ?? endpoint?.ToString()}: {ex.Message}");
    }
    finally
    {
        if (player is not null)
        {
            lock (gate)
            {
                if (sessions.TryGetValue(player, out var current) && current == conn) sessions.Remove(player);
                Deliver(core.Disconnect(player));
                SaveIfDirty();
            }
            Log($"{player} disconnected.");
        }
    }
}

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Log($"Tristinwold server: campaign \"{state.Campaign}\", port {port}, saving to {Path.GetFullPath(campaignPath)}.");
Log("Friends connect to this computer's address. Over the internet, forward this TCP port or use a VPN like Tailscale.");
Log(vann
    ? $"Vann is playing{(voice is null ? " (written lines; set ANTHROPIC_API_KEY for Claude's voice)" : ", voiced by Claude")}. {(humanDm ? "The DM closes contracts from this window." : "Players close their own contracts.")}"
    : "Vann is not scheming on his own (--no-vann).");
Log("DM commands are optional; type 'help' to see them.");

// Vann's own clock: his schemes come when they come.
_ = Task.Run(async () =>
{
    while (!shutdown.IsCancellationRequested)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), shutdown.Token); } catch (OperationCanceledException) { break; }
        lock (gate)
        {
            Deliver(core.Tick());
            SaveIfDirty();
        }
    }
});

_ = Task.Run(async () =>
{
    while (!shutdown.IsCancellationRequested && Console.In.ReadLine() is { } line)
    {
        if (line.Trim().Length == 0) continue;
        lock (gate)
        {
            if (line.Trim().Equals("save", StringComparison.OrdinalIgnoreCase)) { core.Dirty = true; SaveIfDirty(); Console.WriteLine("Saved."); continue; }
            var (reply, outgoing) = core.Dm(line);
            if (reply.Length > 0) Console.WriteLine(reply);
            Deliver(outgoing);
            SaveIfDirty();
        }
    }
    await Task.CompletedTask;
});

try
{
    while (!shutdown.IsCancellationRequested)
    {
        var tcp = await listener.AcceptTcpClientAsync(shutdown.Token);
        tcp.NoDelay = true;
        _ = Task.Run(() => ServeAsync(tcp));
    }
}
catch (OperationCanceledException) { }

lock (gate) { core.Dirty = true; SaveIfDirty(); }
Log("Server stopped.");
return 0;
