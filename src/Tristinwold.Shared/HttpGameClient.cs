using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tristinwold.Shared;

/// <summary>
/// The game's link to the shared world over the HTTP gateway: join once, then sync twice a
/// second, posting what the game has to say and collecting what is waiting for it. Plain
/// HTTPS gets through anything a web page does, so this is how phones reach a server through a
/// Cloudflare tunnel. Messages are only dropped from the outbox once the server has them.
/// </summary>
public sealed class HttpGameClient : IGameLink
{
    readonly ConcurrentQueue<Message> _inbox = new();
    readonly ConcurrentQueue<Message> _outbox = new();
    readonly CancellationTokenSource _cts = new();
    readonly Func<Hello> _hello;
    readonly HttpClient _http;
    Task? _loop;

    public ClientStatus Status { get; private set; } = ClientStatus.Disconnected;
    public string? LastError { get; private set; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(500);

    public HttpGameClient(string address, Func<Hello> hello)
    {
        _hello = hello;
        _http = new HttpClient { BaseAddress = new Uri(address.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) };
    }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    public void Send(Message message)
    {
        if (Status != ClientStatus.Rejected) _outbox.Enqueue(message);
    }

    public bool TryReceive(out Message message) => _inbox.TryDequeue(out message!);

    async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        string? session = null;
        var pending = new List<Message>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (session is null)
                {
                    Status = ClientStatus.Connecting;
                    var (code, joined) = await PostAsync("v1/join", Wire.Encode(_hello()), ct).ConfigureAwait(false);
                    Take(joined);
                    if (code == HttpStatusCode.Forbidden || joined.Messages.Any(m => m is Rejected))
                    {
                        Status = ClientStatus.Rejected;
                        LastError ??= "The server turned us away.";
                        return;
                    }
                    if (code != HttpStatusCode.OK || joined.Session is null) throw new HttpRequestException(joined.Error ?? $"join failed ({(int)code})");
                    session = joined.Session;
                    delay = TimeSpan.FromSeconds(1);
                }

                while (_outbox.TryDequeue(out var m)) pending.Add(m);
                var (status, synced) = await PostAsync("v1/sync", JsonSerializer.Serialize(new SyncRequest(session, pending), Wire.Options), ct).ConfigureAwait(false);
                if (status == HttpStatusCode.Unauthorized) { session = null; continue; } // timed out: join again, keep what we had to say
                if (status != HttpStatusCode.OK) throw new HttpRequestException(synced.Error ?? $"sync failed ({(int)status})");
                pending.Clear();
                Take(synced);
                await Task.Delay(Interval, ct).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                LastError = ex.Message;
            }
            Status = ClientStatus.Disconnected;
            session = null;
            try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
        }
        Status = ClientStatus.Disconnected;
    }

    void Take(SyncResponse response)
    {
        foreach (var m in response.Messages)
        {
            if (m is Rejected r) { Status = ClientStatus.Rejected; LastError = r.Reason; }
            if (m is Welcome) Status = ClientStatus.Connected;
            _inbox.Enqueue(m);
        }
    }

    async Task<(HttpStatusCode, SyncResponse)> PostAsync(string path, string json, CancellationToken ct)
    {
        using var res = await _http.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        SyncResponse parsed;
        try { parsed = JsonSerializer.Deserialize<SyncResponse>(body, Wire.Options) ?? new SyncResponse(null, []); }
        catch (JsonException) { parsed = new SyncResponse(null, [], $"{(int)res.StatusCode} {res.ReasonPhrase}"); } // a tunnel's error page, say
        return (res.StatusCode, parsed);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _cts.Dispose();
        _http.Dispose();
    }
}
