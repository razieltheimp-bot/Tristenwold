// The shared-world server. One person (usually the DM) runs it; every friend's game connects.
//
//   dotnet run --project tools/Tristinwold.Server -- [--port 7777] [--password secret]
//                                                    [--campaign tristinwold.json] [--players-finish]
//
// Type DM commands into this window (help for the list).
using System.Net;
using System.Net.Sockets;
using Tristinwold.Shared;

var port = Wire.DefaultPort;
var password = "";
var campaignPath = "tristinwold.json";
var playersFinish = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--password": password = args[++i]; break;
        case "--campaign": campaignPath = args[++i]; break;
        case "--players-finish": playersFinish = true; break;
        default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 1;
    }
}

var state = File.Exists(campaignPath) ? CampaignState.Load(campaignPath) : CampaignState.SavedMan();
var core = new ServerCore(state, new ServerOptions { Password = password, PlayersFinishContracts = playersFinish });
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

void Deliver(IReadOnlyList<Outgoing> outgoing)
{
    foreach (var o in outgoing)
    {
        IEnumerable<KeyValuePair<string, LineConnection>> targets = o.To is null
            ? sessions.Where(s => !s.Key.Equals(o.Except, StringComparison.OrdinalIgnoreCase)).ToList()
            : sessions.Where(s => s.Key.Equals(o.To, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var (_, conn) in targets)
            _ = conn.WriteAsync(o.Message, shutdown.Token).ContinueWith(_ => { }, TaskScheduler.Default);
        if (o.Message is Notice { Kind: not NoticeKind.Error } n) Log($"{(o.To is null ? "all" : o.To)}: {n.Text}");
    }
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
Log("Type 'help' for DM commands.");

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
