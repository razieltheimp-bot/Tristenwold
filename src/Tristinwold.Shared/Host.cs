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
    bool spoilers = false,
    AccountStore? accounts = null)
{
    readonly Dictionary<string, string> _accountOf = new(StringComparer.OrdinalIgnoreCase);
    readonly Lock _gate = new();
    readonly Dictionary<string, IClientSink> _sinks = new(StringComparer.OrdinalIgnoreCase);
    readonly Action<string> _log = log ?? (_ => { });

    public ServerCore Core => core;

    /// <summary>Raised (outside the lock) when a company arrives or leaves, for notifications.</summary>
    public event Action<string>? Joined, Left;

    /// <summary>Logging in and managing characters, before joining.</summary>
    public AccountReply Account(AccountRequest request)
    {
        if (accounts is null) return new AccountReply(false, "This server doesn't keep accounts.", null, []);
        if (!core.PasswordOk(request.ServerPassword)) return new AccountReply(false, "Wrong server password.", null, []);
        lock (_gate)
        {
            var reply = accounts.Handle(request, name => core.Known(name) && accounts.OwnerOf(name) is null);
            if (request.Action is "register" or "create" && reply.Ok) _log($"Account {request.Account.Trim()}: {request.Action}{(request.Character is { } c ? $" {c.Name.Trim()}" : "")}.");
            return reply;
        }
    }

    /// <returns>The player's name, or null if the server turned them away (already told via the sink).</returns>
    public string? Join(Hello hello, IClientSink sink)
    {
        lock (_gate)
        {
            // A character that belongs to an account can only be played by that account, logged in.
            var owner = accounts?.OwnerOf(hello.Player);
            var refusal = accounts is null ? null : owner switch
            {
                null when hello.Account is not null => "That character isn't on your account.",
                not null when !string.Equals(owner, hello.Account, StringComparison.OrdinalIgnoreCase) || !accounts!.Holds(owner, hello.Token)
                    => $"{hello.Player.Trim()} belongs to an account. Log in to play as them.",
                _ => null,
            };
            if (refusal is not null && core.PasswordOk(hello.Password))
            {
                _log($"Rejected {hello.Player}: {refusal}");
                sink.Send(new Rejected(refusal));
                return null;
            }
            var outgoing = core.Connect(hello);
            if (outgoing.Count > 0 && outgoing[0].Message is Rejected rejected)
            {
                _log($"Rejected {hello.Player}: {rejected.Reason}");
                sink.Send(rejected);
                return null;
            }
            var player = hello.Player.Trim();
            _sinks[player] = sink;
            if (owner is not null) _accountOf[player] = owner;
            else _accountOf.Remove(player);
            _log($"{player} joined.");
            Deliver(outgoing);
            SaveIfDirty();
        }
        Joined?.Invoke(hello.Player.Trim());
        return hello.Player.Trim();
    }

    public void Receive(string player, Message message)
    {
        if (message is SaveProgress save)
        {
            lock (_gate)
                if (_accountOf.TryGetValue(player, out var account)) accounts?.Store(account, player, save);
            return;
        }
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
        Left?.Invoke(player);
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
