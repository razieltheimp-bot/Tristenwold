using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tristinwold.Shared;

public sealed record SyncRequest(string Session, List<Message>? Messages);

public sealed record SyncResponse(string? Session, List<Message> Messages, string? Error = null);

/// <summary>
/// The shared world over plain HTTP, for game clients that can only make GET/POST requests
/// (SkyrimPlatform's HttpClient). A client joins once, then calls sync about once a second:
/// it posts whatever it has to say and gets back whatever is waiting for it.
///
///   POST /v1/join   body: a Hello message     -> SyncResponse (Session set)
///   POST /v1/sync   body: SyncRequest         -> SyncResponse (401 when the session expired)
/// </summary>
public sealed class HttpGateway : IDisposable
{
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(30);

    readonly SharedWorldHost _host;
    readonly HttpListener _listener = new();
    readonly ConcurrentDictionary<string, Mailbox> _sessions = new();
    readonly CancellationTokenSource _cts = new();
    readonly Action<string> _log;

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

    /// <param name="prefix">An HttpListener prefix such as "http://+:7778/".</param>
    public HttpGateway(SharedWorldHost host, string prefix, Action<string>? log = null)
    {
        _host = host;
        _log = log ?? (_ => { });
        _listener.Prefixes.Add(prefix);
    }

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
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception) when (_cts.IsCancellationRequested) { return; }
            catch (HttpListenerException) { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod != "POST") { await Reply(ctx, 405, new SyncResponse(null, [], "POST only")); return; }
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (body.Length > Wire.MaxLineBytes) { await Reply(ctx, 413, new SyncResponse(null, [], "too large")); return; }

            switch (ctx.Request.Url?.AbsolutePath)
            {
                case "/v1/join":
                {
                    if (Wire.Decode(body) is not Hello hello) { await Reply(ctx, 400, new SyncResponse(null, [], "expected hello")); return; }
                    var mailbox = new Mailbox(hello.Player.Trim());
                    var player = _host.Join(hello, mailbox);
                    if (player is null) { await Reply(ctx, 403, new SyncResponse(null, mailbox.Drain())); return; }
                    var session = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                    _sessions[session] = mailbox;
                    await Reply(ctx, 200, new SyncResponse(session, mailbox.Drain()));
                    return;
                }
                case "/v1/sync":
                {
                    var request = JsonSerializer.Deserialize<SyncRequest>(body, Wire.Options);
                    if (request is null || !_sessions.TryGetValue(request.Session, out var mailbox))
                    {
                        await Reply(ctx, 401, new SyncResponse(null, [], "session expired; join again"));
                        return;
                    }
                    mailbox.LastSeen = DateTime.UtcNow;
                    foreach (var m in request.Messages ?? []) _host.Receive(mailbox.Player, m);
                    await Reply(ctx, 200, new SyncResponse(request.Session, mailbox.Drain()));
                    return;
                }
                default:
                    await Reply(ctx, 404, new SyncResponse(null, [], "not found"));
                    return;
            }
        }
        catch (JsonException ex)
        {
            await Reply(ctx, 400, new SyncResponse(null, [], $"bad json: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _log($"HTTP request failed: {ex.Message}");
            try { await Reply(ctx, 500, new SyncResponse(null, [], "server error")); } catch { }
        }
    }

    static async Task Reply(HttpListenerContext ctx, int status, SyncResponse response)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, Wire.Options));
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        ctx.Response.Close();
    }

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
        _listener.Close();
    }
}
