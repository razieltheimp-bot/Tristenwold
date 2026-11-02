using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tristinwold.Shared;

public sealed record SyncRequest(string Session, List<Message>? Messages);

public sealed record SyncResponse(string? Session, List<Message> Messages, string? Error = null);

/// <summary>
/// The shared world over plain HTTP, for game clients that can only make GET/POST requests
/// (browsers, the phone game). A client joins once, then calls sync about once a
/// second: it posts whatever it has to say and gets back whatever is waiting for it.
///
///   POST /v1/join   body: a Hello message     -> SyncResponse (Session set)
///   POST /v1/sync   body: SyncRequest         -> SyncResponse (401 when the session expired)
///   POST /v1/account body: an AccountRequest  -> an AccountReply (log in, manage characters)
///   GET  /update/manifest                     -> the game's files, for a copy that wants to update itself
///   GET  /update/file/{fingerprint}           -> one of them
///
/// It speaks HTTP/1.1 itself on a TCP socket rather than through HttpListener, so on Windows the
/// server needs no administrator rights or URL reservation to take requests from other machines.
/// </summary>
public sealed class HttpGateway : IDisposable
{
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a connection may sit idle between requests before it's closed.</summary>
    static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    const int MaxHeaderBytes = 16 * 1024;

    readonly SharedWorldHost _host;
    readonly TcpListener _listener;
    readonly ConcurrentDictionary<string, Mailbox> _sessions = new();
    readonly CancellationTokenSource _cts = new();
    readonly Action<string> _log;

    /// <summary>The host's own copy of the game, if there is one to hand out.</summary>
    public GameFiles? Game { get; init; }

    sealed class Mailbox(string player) : IClientSink
    {
        readonly ConcurrentQueue<Message> _queue = new();
        public string Player { get; } = player;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public void Send(Message message) => _queue.Enqueue(message);
        public List<Message> Drain()
        {
            var list = new List<Message>();
            while (_queue.TryDequeue(out var m)) list.Add(m);
            return list;
        }
    }

    /// <param name="prefix">Where to listen, written like an HttpListener prefix: "http://+:7778/" for every
    /// address on the machine, "http://127.0.0.1:7778/" for this machine only.</param>
    public HttpGateway(SharedWorldHost host, string prefix, Action<string>? log = null)
    {
        _host = host;
        _log = log ?? (_ => { });
        var uri = new Uri(prefix.Replace("://+", "://0.0.0.0").Replace("://*", "://0.0.0.0"));
        var address = uri.Host == "localhost" ? IPAddress.Loopback : IPAddress.TryParse(uri.Host, out var ip) ? ip : IPAddress.Any;
        _listener = new TcpListener(address, uri.Port);
    }

    /// <summary>Starts listening. Throws a <see cref="SocketException"/> if the port is taken.</summary>
    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoop);
        _ = Task.Run(SweepLoop);
    }

    async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { if (_cts.IsCancellationRequested) return; continue; }
            _ = Task.Run(() => Serve(client));
        }
    }

    /// <summary>One connection: requests one after another (keep-alive) until the client closes it or goes quiet.</summary>
    async Task Serve(TcpClient client)
    {
        using var _ = client;
        try
        {
            var stream = client.GetStream();
            while (!_cts.IsCancellationRequested)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                idle.CancelAfter(IdleTimeout);
                var request = await ReadRequest(stream, idle.Token).ConfigureAwait(false);
                if (request is null) return;
                var keepAlive = !request.Value.Close;
                if (request.Value.Method == "GET" && request.Value.Path.StartsWith("/update/"))
                {
                    await ServeUpdate(stream, request.Value.Path, keepAlive).ConfigureAwait(false);
                    if (!keepAlive) return;
                    continue;
                }
                var (status, body) = Handle(request.Value.Method, request.Value.Path, request.Value.Body);
                var head = $"HTTP/1.1 {status} {Reason(status)}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n" +
                           $"Cache-Control: no-store\r\nConnection: {(keepAlive ? "keep-alive" : "close")}\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _cts.Token).ConfigureAwait(false);
                await stream.WriteAsync(body, _cts.Token).ConfigureAwait(false);
                if (!keepAlive) return;
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or InvalidDataException) { }
    }

    /// <summary>A game updating itself: the list of files, or one of them, streamed (the pack is hundreds of megabytes).</summary>
    async Task ServeUpdate(NetworkStream stream, string path, bool keepAlive)
    {
        string Head(int status, long length, string type) => $"HTTP/1.1 {status} {Reason(status)}\r\nContent-Type: {type}\r\nContent-Length: {length}\r\n" +
            $"Cache-Control: no-store\r\nConnection: {(keepAlive ? "keep-alive" : "close")}\r\n\r\n";
        const string Prefix = "/update/file/";
        if (path == "/update/manifest" && Game?.Files is { Count: > 0 } files)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files, Wire.Options));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Head(200, body.Length, "application/json")), _cts.Token).ConfigureAwait(false);
            await stream.WriteAsync(body, _cts.Token).ConfigureAwait(false);
            return;
        }
        if (path.StartsWith(Prefix) && Game?.PathOf(path[Prefix.Length..]) is { } found)
        {
            await using var file = File.OpenRead(found);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Head(200, file.Length, "application/octet-stream")), _cts.Token).ConfigureAwait(false);
            await file.CopyToAsync(stream, 1 << 20, _cts.Token).ConfigureAwait(false);
            return;
        }
        await stream.WriteAsync(Encoding.ASCII.GetBytes(Head(404, 0, "application/json")), _cts.Token).ConfigureAwait(false);
    }

    readonly record struct Request(string Method, string Path, string Body, bool Close);

    /// <summary>Reads one request: its line, its headers, and its body (by length, or chunked). Null when the client hung up.</summary>
    static async Task<Request?> ReadRequest(NetworkStream stream, CancellationToken token)
    {
        var head = new List<byte>(512);
        var one = new byte[1];
        while (head.Count < 4 || head[^4] != '\r' || head[^3] != '\n' || head[^2] != '\r' || head[^1] != '\n')
        {
            if (await stream.ReadAsync(one, token).ConfigureAwait(false) == 0) return null;
            head.Add(one[0]);
            if (head.Count > MaxHeaderBytes) throw new InvalidDataException("headers too large");
        }
        var lines = Encoding.ASCII.GetString([.. head]).Split("\r\n");
        var first = lines[0].Split(' ');
        if (first.Length < 2) throw new InvalidDataException("bad request line");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
            if (line.IndexOf(':') is var colon and > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        if (headers.TryGetValue("Expect", out var expect) && expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
            await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), token).ConfigureAwait(false);

        byte[] body;
        if (headers.TryGetValue("Transfer-Encoding", out var te) && te.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            var all = new MemoryStream();
            while (true)
            {
                var size = Convert.ToInt32((await ReadLine(stream, token).ConfigureAwait(false)).Split(';')[0].Trim(), 16);
                if (size == 0) { await ReadLine(stream, token).ConfigureAwait(false); break; }
                if (all.Length + size > Wire.MaxLineBytes) throw new InvalidDataException("too large");
                all.Write(await ReadExactly(stream, size, token).ConfigureAwait(false));
                await ReadLine(stream, token).ConfigureAwait(false);
            }
            body = all.ToArray();
        }
        else
        {
            var length = headers.TryGetValue("Content-Length", out var cl) && int.TryParse(cl, out var n) ? n : 0;
            if (length > Wire.MaxLineBytes) throw new InvalidDataException("too large");
            body = await ReadExactly(stream, length, token).ConfigureAwait(false);
        }
        var path = first[1].Split('?')[0];
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)) path = absolute.AbsolutePath; // a proxy may send the full URL
        var close = headers.TryGetValue("Connection", out var connection) ? connection.Equals("close", StringComparison.OrdinalIgnoreCase)
            : first.Length > 2 && first[2] == "HTTP/1.0";
        return new Request(first[0], path, Encoding.UTF8.GetString(body), close);
    }

    static async Task<byte[]> ReadExactly(NetworkStream stream, int count, CancellationToken token)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        return buffer;
    }

    static async Task<string> ReadLine(NetworkStream stream, CancellationToken token)
    {
        var line = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(one, token).ConfigureAwait(false) == 0) throw new IOException("closed");
            if (one[0] == '\n') return Encoding.ASCII.GetString([.. line]).TrimEnd('\r');
            line.Add(one[0]);
        }
    }

    static string Reason(int status) => status switch
    {
        200 => "OK", 400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not Found",
        405 => "Method Not Allowed", 413 => "Payload Too Large", _ => "Internal Server Error",
    };

    /// <summary>Answers one request: the status, and the JSON to send back.</summary>
    (int Status, byte[] Body) Handle(string method, string path, string body)
    {
        try
        {
            if (method != "POST") return Reply(405, new SyncResponse(null, [], "POST only"));
            if (body.Length > Wire.MaxLineBytes) return Reply(413, new SyncResponse(null, [], "too large"));
            switch (path)
            {
                case "/v1/join":
                {
                    if (Wire.Decode(body) is not Hello hello) return Reply(400, new SyncResponse(null, [], "expected hello"));
                    var mailbox = new Mailbox(hello.Player.Trim());
                    var player = _host.Join(hello, mailbox);
                    if (player is null) return Reply(403, new SyncResponse(null, mailbox.Drain()));
                    var session = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                    _sessions[session] = mailbox;
                    return Reply(200, new SyncResponse(session, mailbox.Drain()));
                }
                case "/v1/account":
                {
                    if (Wire.Decode(body) is not AccountRequest request) return Reply(400, new SyncResponse(null, [], "expected account_request"));
                    return (200, Encoding.UTF8.GetBytes(Wire.Encode(_host.Account(request))));
                }
                case "/v1/sync":
                {
                    var request = JsonSerializer.Deserialize<SyncRequest>(body, Wire.Options);
                    if (request?.Session is null || !_sessions.TryGetValue(request.Session, out var mailbox))
                        return Reply(401, new SyncResponse(null, [], "session expired; join again"));
                    mailbox.LastSeen = DateTime.UtcNow;
                    foreach (var m in request.Messages ?? []) _host.Receive(mailbox.Player, m);
                    return Reply(200, new SyncResponse(request.Session, mailbox.Drain()));
                }
                default:
                    return Reply(404, new SyncResponse(null, [], "not found"));
            }
        }
        catch (JsonException ex)
        {
            return Reply(400, new SyncResponse(null, [], $"bad json: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _log($"HTTP request failed: {ex.Message}");
            return Reply(500, new SyncResponse(null, [], "server error"));
        }
    }

    static (int, byte[]) Reply(int status, SyncResponse response) =>
        (status, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, Wire.Options)));

    async Task SweepLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            ExpireIdle(DateTime.UtcNow);
        }
    }

    /// <summary>A game that stops syncing (crashed, quit) is logged out after <see cref="SessionTimeout"/>.</summary>
    public void ExpireIdle(DateTime now)
    {
        foreach (var (session, mailbox) in _sessions)
        {
            if (now - mailbox.LastSeen <= SessionTimeout) continue;
            if (_sessions.TryRemove(session, out _)) _host.Leave(mailbox.Player, mailbox);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch (ObjectDisposedException) { }
    }
}
