using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// A Cloudflare quick tunnel to the HTTP gateway: cloudflared gives this computer a public
/// https://….trycloudflare.com address, with no port forwarding and no Cloudflare account.
/// Phones (and anyone else) connect to that address.
/// </summary>
sealed partial class Tunnel(string cloudflared, int httpPort, Action<string> log) : IDisposable
{
    Process? _process;

    [GeneratedRegex(@"https://(?!api\.)[a-z0-9-]+\.trycloudflare\.com")]
    private static partial Regex UrlPattern();

    /// <summary>The tunnel's address in a line of cloudflared's output (not its own API's, which shows up in errors).</summary>
    public static string? FindUrl(string line) => UrlPattern().Match(line) is { Success: true } m ? m.Value : null;

    readonly Queue<string> _recent = new();

    public bool Start(Action<string> onUrl)
    {
        var info = new ProcessStartInfo(cloudflared)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "tunnel", "--no-autoupdate", "--url", $"http://localhost:{httpPort}" }) info.ArgumentList.Add(arg);
        try
        {
            _process = Process.Start(info);
        }
        catch (Win32Exception)
        {
            log("--tunnel needs cloudflared, which isn't installed. Get it with one of:");
            log("  Windows: winget install --id Cloudflare.cloudflared");
            log("  macOS:   brew install cloudflared");
            log("  Linux:   https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/");
            log("Or point at it with --cloudflared C:\\path\\to\\cloudflared.exe");
            return false;
        }
        if (_process is null) return false;
        var announced = 0;
        void Read(string? line)
        {
            if (line is null) return;
            lock (_recent)
            {
                _recent.Enqueue(line);
                if (_recent.Count > 4) _recent.Dequeue();
            }
            if (FindUrl(line) is not { } url || Interlocked.Exchange(ref announced, 1) == 1) return;
            onUrl(url);
        }
        _process.OutputDataReceived += (_, e) => Read(e.Data);
        _process.ErrorDataReceived += (_, e) => Read(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            _process.WaitForExit(); // lets the last of its output arrive
            log("The Cloudflare tunnel closed, so phones can't connect until the server is restarted. cloudflared said:");
            lock (_recent) foreach (var line in _recent) log("  " + line);
        };
        return true;
    }

    public void Dispose()
    {
        try { if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        _process?.Dispose();
    }
}
