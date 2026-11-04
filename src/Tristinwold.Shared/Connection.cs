using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace Tristinwold.Shared;

/// <summary>Newline-delimited JSON messages over a stream. Writes are serialised; reads are one at a time.</summary>
public sealed class LineConnection(Stream stream) : IDisposable
{
    readonly StreamReader _reader = new(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 16 * 1024, leaveOpen: true);
    readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>The next message, or null when the other side hung up.</summary>
    public async Task<Message?> ReadAsync(CancellationToken ct)
    {
        var line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (line is null) return null;
        if (line.Length > Wire.MaxLineBytes) throw new InvalidDataException("message too large");
        return Wire.Decode(line);
    }

    public async Task WriteAsync(Message message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(Wire.Encode(message) + "\n");
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
        stream.Dispose();
        _writeLock.Dispose();
    }
}

public enum ClientStatus { Disconnected, Connecting, Connected, Rejected }

/// <summary>A game's link to the shared world, over TCP (<see cref="GameClient"/>) or HTTPS (<see cref="HttpGameClient"/>).</summary>
public interface IGameLink : IDisposable
{
    ClientStatus Status { get; }
    string? LastError { get; }
    void Start();
    void Send(Message message);
    bool TryReceive(out Message message);

    /// <summary>"host:port" for TCP, or an http(s):// address (a Cloudflare tunnel, say) for the HTTP gateway.</summary>
    static IGameLink Open(string address, Func<Hello> hello)
    {
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new HttpGameClient(address, hello);
        var colon = address.LastIndexOf(':');
        return colon > 0 && int.TryParse(address[(colon + 1)..], out var port)
            ? new GameClient(address[..colon], port, hello)
            : new GameClient(address, Wire.DefaultPort, hello);
    }
}

/// <summary>
/// The in-game side of the connection. Runs on background threads and reconnects on its
/// own. The game thread drains <see cref="TryReceive"/> once a frame; nothing here ever
/// touches game memory.
/// </summary>
public sealed class GameClient : IGameLink
{
    readonly ConcurrentQueue<Message> _inbox = new();
    readonly ConcurrentQueue<Message> _outbox = new();
    readonly CancellationTokenSource _cts = new();
    readonly Func<Hello> _hello;
    readonly string _host;
    readonly int _port;
    Task? _loop;

    public ClientStatus Status { get; private set; } = ClientStatus.Disconnected;
    public string? LastError { get; private set; }

    /// <param name="hello">Built fresh for each (re)connect, so it carries the current party.</param>
    public GameClient(string host, int port, Func<Hello> hello)
    {
        _host = host;
        _port = port;
        _hello = hello;
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
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Status = ClientStatus.Connecting;
                using var tcp = new TcpClient { NoDelay = true };
                await tcp.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
                using var conn = new LineConnection(tcp.GetStream());
                await conn.WriteAsync(_hello(), ct).ConfigureAwait(false);

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var writer = PumpOutboxAsync(conn, linked.Token);
                while (await conn.ReadAsync(ct).ConfigureAwait(false) is { } message)
                {
                    if (message is Rejected r)
                    {
                        Status = ClientStatus.Rejected;
                        LastError = Wire.Explain(r.Reason);
                        _inbox.Enqueue(message);
                        return; // a rejection won't fix itself by retrying
                    }
                    if (message is Welcome) { Status = ClientStatus.Connected; delay = TimeSpan.FromSeconds(1); }
                    _inbox.Enqueue(message);
                }
                linked.Cancel();
                await writer.ConfigureAwait(false);
                LastError = "Server closed the connection.";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            Status = ClientStatus.Disconnected;
            try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
        }
        Status = ClientStatus.Disconnected;
    }

    async Task PumpOutboxAsync(LineConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                while (_outbox.TryDequeue(out var message)) await conn.WriteAsync(message, ct).ConfigureAwait(false);
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public void Dispose()
    {
        // Give the last messages (a final save, say) a moment to go out.
        for (var i = 0; i < 75 && !_outbox.IsEmpty && Status == ClientStatus.Connected; i++) Thread.Sleep(20);
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _cts.Dispose();
    }
}
