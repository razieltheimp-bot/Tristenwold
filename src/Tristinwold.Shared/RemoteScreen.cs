using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Tristinwold.Shared;

/// <summary>
/// Remote play: the game runs on a computer and a phone's web browser shows it and controls it.
/// This is the little web server for that. It serves the page (<see cref="RemotePage"/>), then
/// keeps a WebSocket open to each browser: pictures and sound of the game go out, touches and typing
/// come back. A Cloudflare tunnel in front of it puts it on the internet, so an iPhone can play from
/// anywhere with nothing installed.
///
/// It speaks plain HTTP on a TCP socket rather than through HttpListener, so Windows doesn't ask
/// for administrator rights, and the address the tunnel uses doesn't matter. Every request needs
/// the secret key that's part of the link, so only people given the link can see or play.
/// </summary>
public sealed class RemoteScreen : IDisposable
{
    readonly TcpListener _listener;
    readonly CancellationTokenSource _cts = new();
    readonly ConcurrentDictionary<int, Viewer> _viewers = new();
    readonly Action<string> _log;
    int _nextId;

    /// <summary>The secret in the link (a phone must know it to connect).</summary>
    public string Key { get; }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>How many browsers are connected right now.</summary>
    public int Viewers => _viewers.Count;

    /// <summary>Input from browsers (JSON, one event each), waiting for the game to read it.</summary>
    public ConcurrentQueue<string> Input { get; } = new();

    /// <summary>True when some browser has shown the last picture and is ready for another.</summary>
    public bool Wanted => _viewers.Values.Any(v => !v.Busy);

    public event Action<int>? Connected;

    /// <summary>
    /// One browser. A picture goes out only once the browser has said it drew the last one, so a
    /// slow connection gets fewer pictures instead of a growing delay.
    /// </summary>
    sealed class Viewer(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;
        public readonly SemaphoreSlim Sending = new(1, 1);
        long _sentAt;
        public bool Busy => Interlocked.Read(ref _sentAt) != 0 && Environment.TickCount64 - Interlocked.Read(ref _sentAt) < 3000;
        public void Sent() => Interlocked.Exchange(ref _sentAt, Environment.TickCount64);
        public void Drawn() => Interlocked.Exchange(ref _sentAt, 0);

        /// <summary>Sound on its way to this phone and not yet gone.</summary>
        public int Hearing;

        public async Task Send(byte[] bytes, WebSocketMessageType type, CancellationToken token)
        {
            await Sending.WaitAsync(token).ConfigureAwait(false);
            try { await Socket.SendAsync(bytes, type, true, token).ConfigureAwait(false); }
            catch (Exception) { }
            finally { Sending.Release(); }
        }
    }

    /// <param name="everywhere">Listen on the home network too, not only for the tunnel on this computer.</param>
    public RemoteScreen(int port = 0, string? key = null, Action<string>? log = null, bool everywhere = false)
    {
        _log = log ?? (_ => { });
        Key = key ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        _listener = new TcpListener(everywhere ? IPAddress.Any : IPAddress.Loopback, port);
    }

    /// <summary>The local link, for trying it on this computer.</summary>
    public string LocalUrl => $"http://localhost:{Port}/?k={Key}";

    /// <summary>The link to give a phone, given the tunnel's public address.</summary>
    public string LinkFor(string publicUrl) => $"{publicUrl.TrimEnd('/')}/?k={Key}";

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false); }
            catch (Exception) { return; }
            _ = Task.Run(() => Serve(client));
        }
    }

    async Task Serve(TcpClient client)
    {
        using var _ = client;
        client.NoDelay = true;
        var stream = client.GetStream();
        try
        {
            var (path, query, headers) = await ReadRequest(stream).ConfigureAwait(false);
            if (path is null) return;
            if (query.GetValueOrDefault("k") != Key)
            {
                await Respond(stream, 403, "text/plain", "This link is missing its key. Use the whole link the game gave you."u8.ToArray());
                return;
            }
            if (path == "/ws" && headers.TryGetValue("sec-websocket-key", out var wsKey))
            {
                await Upgrade(stream, wsKey).ConfigureAwait(false);
                return;
            }
            if (path is "/" or "/index.html")
            {
                await Respond(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(RemotePage.Html));
                return;
            }
            await Respond(stream, 404, "text/plain", "Not here."u8.ToArray());
        }
        catch (Exception e) when (e is IOException or SocketException or WebSocketException or OperationCanceledException) { }
    }

    static async Task<(string? Path, Dictionary<string, string> Query, Dictionary<string, string> Headers)> ReadRequest(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < 16384)
        {
            if (await stream.ReadAsync(one).ConfigureAwait(false) == 0) return (null, [], []);
            bytes.Add(one[0]);
            var n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n') break;
        }
        var lines = Encoding.ASCII.GetString([.. bytes]).Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length < 2 || parts[0] != "GET") return (null, [], []);
        var target = parts[1];
        var q = target.IndexOf('?');
        var path = q < 0 ? target : target[..q];
        var query = new Dictionary<string, string>();
        if (q >= 0)
            foreach (var pair in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0) query[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return (path, query, headers);
    }

    static async Task Respond(NetworkStream stream, int status, string type, byte[] body)
    {
        var reason = status switch { 200 => "OK", 403 => "Forbidden", _ => "Not Found" };
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        await stream.WriteAsync(body).ConfigureAwait(false);
    }

    async Task Upgrade(NetworkStream stream, string wsKey)
    {
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(wsKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var head = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.FromSeconds(20) });
        var id = Interlocked.Increment(ref _nextId);
        _viewers[id] = new Viewer(socket);
        _log($"A phone connected to remote play ({_viewers.Count} watching).");
        Connected?.Invoke(_viewers.Count);
        try
        {
            var buffer = new byte[8192];
            var message = new MemoryStream();
            while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var r = await socket.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (message.Length > 65536) break; // no input event is anywhere near this big
                if (!r.EndOfMessage) continue;
                if (r.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    if (text == "drawn") _viewers[id].Drawn();
                    else Input.Enqueue(text);
                }
                message.SetLength(0);
            }
        }
        finally
        {
            _viewers.TryRemove(id, out var _);
            _log($"A phone left remote play ({_viewers.Count} watching).");
        }
    }

    /// <summary>Sends a picture of the game (a JPEG) to every browser that's ready for one.</summary>
    public void Show(byte[] jpeg)
    {
        foreach (var v in _viewers.Values)
        {
            if (v.Busy || v.Socket.State != WebSocketState.Open) continue;
            v.Sent();
            _ = v.Send(jpeg, WebSocketMessageType.Binary, _cts.Token);
        }
    }

    /// <summary>
    /// Sends a moment of the game's sound (a <see cref="RemoteAudio"/> packet) to every browser. A connection too
    /// slow to keep up loses some sound rather than falling behind: late sound is worse than a gap.
    /// </summary>
    public void Hear(byte[] packet)
    {
        foreach (var v in _viewers.Values)
        {
            if (v.Socket.State != WebSocketState.Open || Volatile.Read(ref v.Hearing) > 4) continue;
            Interlocked.Increment(ref v.Hearing);
            _ = v.Send(packet, WebSocketMessageType.Binary, _cts.Token).ContinueWith(_ => Interlocked.Decrement(ref v.Hearing), TaskScheduler.Default);
        }
    }

    /// <summary>Sends a short note to every browser (JSON), e.g. to say the keyboard is wanted.</summary>
    public void Tell(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var v in _viewers.Values)
            if (v.Socket.State == WebSocketState.Open) _ = v.Send(bytes, WebSocketMessageType.Text, _cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        foreach (var v in _viewers.Values) v.Socket.Abort();
    }
}
