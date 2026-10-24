namespace Tristinwold.Shared;

/// <summary>Somewhere to send a player's messages: a TCP connection or an HTTP session's mailbox.</summary>
public interface IClientSink
{
    /// <summary>Must not block; called with the host's lock held.</summary>
    void Send(Message message);
}

/// <summary>
/// Runs a <see cref="ServerCore"/> for real connections: one lock around the rules, delivery to
/// every connected player, Vann's voice, and saving. The TCP listener and the HTTP gateway (which
/// SkyrimPlatform plugins use) both go through it.
/// </summary>
public sealed class SharedWorldHost(
    ServerCore core,
    Action save,
    IVannVoice? voice = null,
    Action<string>? log = null,
    bool spoilers = false)
{
    readonly Lock _gate = new();
    readonly Dictionary<string, IClientSink> _sinks = new(StringComparer.OrdinalIgnoreCase);
    readonly Action<string> _log = log ?? (_ => { });

    public ServerCore Core => core;

    /// <returns>The player's name, or null if the server turned them away (already told via the sink).</returns>
    public string? Join(Hello hello, IClientSink sink)
    {
        lock (_gate)
        {
            var outgoing = core.Connect(hello);
            if (outgoing.Count > 0 && outgoing[0].Message is Rejected rejected)
            {
                _log($"Rejected {hello.Player}: {rejected.Reason}");
                sink.Send(rejected);
                return null;
            }
            var player = hello.Player.Trim();
            _sinks[player] = sink;
            _log($"{player} joined.");
            Deliver(outgoing);
            SaveIfDirty();
            return player;
        }
    }

    public void Receive(string player, Message message)
    {
        lock (_gate)
        {
            Deliver(core.Handle(player, message));
            SaveIfDirty();
        }
    }

    /// <summary>Only the sink that joined last counts, so a stale connection can't log a player out.</summary>
    public void Leave(string player, IClientSink sink)
    {
        lock (_gate)
        {
            if (!_sinks.TryGetValue(player, out var current) || current != sink) return;
            _sinks.Remove(player);
            Deliver(core.Disconnect(player));
            SaveIfDirty();
            _log($"{player} left.");
        }
    }

    public void Tick()
    {
        lock (_gate)
        {
            Deliver(core.Tick());
            SaveIfDirty();
        }
    }

    public string Dm(string line)
    {
        lock (_gate)
        {
            if (line.Trim().Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                core.Dirty = true;
                SaveIfDirty();
                return "Saved.";
            }
            var (reply, outgoing) = core.Dm(line);
            Deliver(outgoing);
            SaveIfDirty();
            return reply;
        }
    }

    public void SaveNow()
    {
        lock (_gate)
        {
            core.Dirty = true;
            SaveIfDirty();
        }
    }

    void SaveIfDirty()
    {
        if (!core.Dirty) return;
        core.Dirty = false;
        try { save(); }
        catch (Exception ex) { _log($"Could not save the campaign: {ex.Message}"); }
    }

    // Lock held. Vann's lines go out once his voice has worded them.
    void Deliver(IReadOnlyList<Outgoing> outgoing)
    {
        foreach (var o in outgoing)
        {
            if (voice is not null && o.Scene is not null && o.Message is Notice notice)
                _ = VoiceAsync(o, notice, [.. core.State.VannMemory]);
            else
                Send(o);
        }
    }

    async Task VoiceAsync(Outgoing o, Notice notice, List<string> recent)
    {
        string? line = null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            line = await voice!.SpeakAsync(o.Scene!, recent, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        lock (_gate) Send(o with { Message = notice with { Text = line ?? notice.Text } });
    }

    // Lock held.
    void Send(Outgoing o)
    {
        foreach (var (name, sink) in _sinks)
        {
            var wanted = o.To is null
                ? !name.Equals(o.Except, StringComparison.OrdinalIgnoreCase)
                : name.Equals(o.To, StringComparison.OrdinalIgnoreCase);
            if (wanted) sink.Send(o.Message);
        }
        // The host may be playing too: only public news is printed, unless asked for spoilers.
        if (o.Message is Notice { Kind: not NoticeKind.Error } n && (spoilers || (o.To is null && n.Kind != NoticeKind.Vann)))
            _log($"{(o.To is null ? "all" : o.To)}: {(n.From is null ? "" : n.From + ": ")}{n.Text}");
    }
}
