using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tristinwold.Shared;

/// <summary>
/// The game's link to the shared world over the HTTP gateway: join once, then sync, posting what
/// the game has to say and collecting what is waiting for it. Plain HTTPS gets through anything a
/// web page does, so this is how phones reach a server through a Cloudflare tunnel. Messages are
/// only dropped from the outbox once the server has them.
/// <para>
/// It syncs twice a second while the game is busy (another hero about, a fight on), and only every
/// few seconds while it isn't: every request through Cloudflare counts against the server's daily
/// allowance, and one idle game syncing twice a second all day would spend it alone. Anything but a
/// position (a word, a trade, a save) goes out at once either way.
/// </para>
/// </summary>
public sealed class HttpGameClient : IGameLink
{
    readonly ConcurrentQueue<Message> _inbox = new();
    readonly ConcurrentQueue<Message> _outbox = new();
    readonly CancellationTokenSource _cts = new();
    readonly Func<Hello> _hello;
    readonly HttpClient _http;
    Task? _loop;
    volatile bool _sending;
    volatile bool _busy = true;
    readonly SemaphoreSlim _wake = new(0);

    public ClientStatus Status { get; private set; } = ClientStatus.Disconnected;
    public string? LastError { get; private set; }
    /// <summary>Between syncs while busy; never closer together than this.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(500);
    /// <summary>Between syncs while nothing's going on (well inside the gateway's session timeout).</summary>
    public TimeSpan IdleInterval { get; init; } = TimeSpan.FromSeconds(5);

    public HttpGameClient(string address, Func<Hello> hello, HttpMessageHandler? handler = null)
    {
        _hello = hello;
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri(address.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Busy (another hero about, a fight on) syncs at <see cref="Interval"/>; idle, at <see cref="IdleInterval"/>.</summary>
    public void Pace(bool busy)
    {
        var was = _busy;
        _busy = busy;
        if (busy && !was) Wake(); // something's started: don't sit out the rest of an idle wait
    }

    void Wake()
    {
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    public void Send(Message message)
    {
        if (Status == ClientStatus.Rejected) return;
        _outbox.Enqueue(message);
        if (message is not Moved) Wake(); // only a position can wait for the next sync
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

                while (_wake.CurrentCount > 0) _wake.Wait(0); // whatever woke us goes out with this sync
                while (_outbox.TryDequeue(out var m)) pending.Add(m);
                // Only the latest position matters; the ones before it would only be played back late.
                for (var i = pending.FindLastIndex(m => m is Moved) - 1; i >= 0; i--)
                    if (pending[i] is Moved) pending.RemoveAt(i);
                _sending = pending.Count > 0;
                var (status, synced) = await PostAsync("v1/sync", JsonSerializer.Serialize(new SyncRequest(session, pending), Wire.Options), ct).ConfigureAwait(false);
                if (status == HttpStatusCode.Unauthorized) { session = null; continue; } // timed out: join again, keep what we had to say
                if (status != HttpStatusCode.OK) throw new HttpRequestException(synced.Error ?? $"sync failed ({(int)status})");
                pending.Clear();
                _sending = false;
                Take(synced);
                await Task.Delay(Interval, ct).ConfigureAwait(false);
                if (!_busy && IdleInterval > Interval) await _wake.WaitAsync(IdleInterval - Interval, ct).ConfigureAwait(false);
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
            if (m is Rejected r) { Status = ClientStatus.Rejected; LastError = Wire.Explain(r.Reason); }
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
        // Give the last messages (a final save, say) a chance to go out with one more sync.
        for (var i = 0; i < 75 && (!_outbox.IsEmpty || _sending) && Status == ClientStatus.Connected; i++) Thread.Sleep(20);
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _cts.Dispose();
        _http.Dispose();
        _wake.Dispose();
    }
}
