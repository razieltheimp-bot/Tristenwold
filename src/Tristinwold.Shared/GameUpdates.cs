using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tristinwold.Shared;

/// <summary>One file of the game: where it goes, how big it is, and its fingerprint.</summary>
public sealed record UpdateFile(string Path, long Size, string Sha);

/// <summary>
/// The server's half of updating by itself: the host's own copy of the Windows game, handed out to anyone whose copy
/// differs. Only the game's own files are ever listed, and they're asked for by fingerprint, not by name, so nothing
/// else in the folder (saves, accounts, the server) can be reached this way.
/// </summary>
public sealed class GameFiles(string root)
{
    const string Pack = "Tristinwold.pck", Exe = "Tristinwold.exe", Data = "data_Tristinwold.Game_windows_x86_64";

    readonly object _lock = new();
    IReadOnlyList<UpdateFile>? _files;
    Dictionary<string, string> _bySha = [];
    DateTime _builtFor;

    /// <summary>The game's files, as they stand on disk (worked out again when the game's been replaced). Empty if this server has no game beside it.</summary>
    public IReadOnlyList<UpdateFile> Files
    {
        get
        {
            var pack = Path.Combine(root, Pack);
            if (!File.Exists(pack)) return [];
            lock (_lock)
            {
                var stamp = File.GetLastWriteTimeUtc(pack);
                if (_files is not null && stamp == _builtFor) return _files;
                var paths = new[] { Exe, Pack }.Select(p => Path.Combine(root, p)).Where(File.Exists).ToList();
                if (Directory.Exists(Path.Combine(root, Data))) paths.AddRange(Directory.EnumerateFiles(Path.Combine(root, Data), "*", SearchOption.AllDirectories));
                var files = paths.Select(p => new UpdateFile(Path.GetRelativePath(root, p).Replace('\\', '/'), new FileInfo(p).Length, Sha(p))).ToList();
                _bySha = files.GroupBy(f => f.Sha).ToDictionary(g => g.Key, g => Path.Combine(root, g.First().Path));
                _builtFor = stamp;
                return _files = files;
            }
        }
    }

    /// <summary>Where the file with this fingerprint lives, or null if it isn't one of the game's.</summary>
    public string? PathOf(string sha)
    {
        _ = Files;
        lock (_lock) return _bySha.GetValueOrDefault(sha);
    }

    public static string Sha(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
}

/// <summary>The game's half: what's different from the server's copy, fetched and proved before anything's replaced.</summary>
public static class GameUpdater
{
    public const string Staging = "update-new";

    static HttpClient Client(string address) => new() { BaseAddress = new Uri(address.TrimEnd('/') + "/"), Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Whether this is a server the game can ask: one reached over the web, not this computer's own.</summary>
    public static bool CanAsk(string address) => address.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !address.Contains("127.0.0.1") && !address.Contains("localhost");

    /// <summary>The server's list of game files, or null if it doesn't offer one (an older server, or none reachable).</summary>
    public static async Task<IReadOnlyList<UpdateFile>?> Offered(string address)
    {
        try
        {
            using var http = Client(address);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var reply = await http.GetAsync("update/manifest", timeout.Token).ConfigureAwait(false);
            if (!reply.IsSuccessStatusCode) return null;
            return await reply.Content.ReadFromJsonAsync<List<UpdateFile>>(Wire.Options, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException) { return null; }
    }

    /// <summary>Which of the server's files this copy of the game doesn't have, or has differently.</summary>
    public static List<UpdateFile> Different(IReadOnlyList<UpdateFile> offered, string root) =>
        offered.Where(f =>
        {
            var here = Path.Combine(root, f.Path);
            return !File.Exists(here) || new FileInfo(here).Length != f.Size || GameFiles.Sha(here) != f.Sha;
        }).ToList();

    /// <summary>A file's name from the server is only used inside the game's folder.</summary>
    static string Inside(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"not a game file: {relative}");
        return full;
    }

    /// <summary>Fetches the files into the staging folder and checks each one's fingerprint. Whether all of them came through whole.</summary>
    public static async Task<bool> Fetch(string address, IReadOnlyList<UpdateFile> files, string root, Action<long, long>? progress = null)
    {
        var staging = Path.Combine(root, Staging);
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        var (done, total) = (0L, files.Sum(f => f.Size));
        using var http = Client(address);
        var buffer = new byte[1 << 20];
        foreach (var f in files)
        {
            var to = Inside(staging, f.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            using (var reply = await http.GetAsync($"update/file/{f.Sha}", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (!reply.IsSuccessStatusCode) return false;
                await using var from = await reply.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var file = File.Create(to);
                int n;
                while ((n = await from.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n)).ConfigureAwait(false);
                    progress?.Invoke(done += n, total);
                }
            }
            if (GameFiles.Sha(to) != f.Sha) return false;
        }
        return true;
    }

    /// <summary>The script that finishes the job once the game has closed: moves the new files in, and opens the game again.</summary>
    public static string SwapScript(int gameProcess, string exe, IEnumerable<string> arguments) => string.Join("\r\n",
        "@echo off",
        "cd /d \"%~dp0\"",
        ":wait",
        $"tasklist /FI \"PID eq {gameProcess}\" 2>nul | find \"{gameProcess}\" >nul",
        "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)",
        $"robocopy \"{Staging}\" \".\" /E /MOVE /R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul",
        $"rmdir /s /q \"{Staging}\" 2>nul",
        $"start \"\" \"{exe}\" {string.Join(" ", arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}",
        "(goto) 2>nul & del \"%~f0\"",
        "");
}
