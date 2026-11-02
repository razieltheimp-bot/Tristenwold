using System.Net;
using System.Net.Sockets;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

public class GameUpdateTests : IDisposable
{
    readonly string _host = Directory.CreateTempSubdirectory("update-host").FullName, _player = Directory.CreateTempSubdirectory("update-player").FullName;

    public void Dispose()
    {
        foreach (var dir in new[] { _host, _player })
            try { Directory.Delete(dir, true); }
            catch (IOException) { } // the swap's window may still be letting go of it
    }

    static void Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    void HostGame()
    {
        Put(_host, "Tristinwold.exe", "exe");
        Put(_host, "Tristinwold.pck", "new pack");
        Put(_host, "data_Tristinwold.Game_windows_x86_64/Tristinwold.Game.dll", "new code");
        Put(_host, "server/tristinwold.json", "the world");
        Put(_host, "server/tristinwold.accounts.json", "secrets");
    }

    [Fact]
    public void Only_the_games_own_files_are_listed()
    {
        HostGame();
        var files = new GameFiles(_host).Files;
        Assert.Equal(3, files.Count);
        Assert.DoesNotContain(files, f => f.Path.Contains("server") || f.Path.Contains("json"));
    }

    [Fact]
    public void A_server_with_no_game_beside_it_offers_nothing()
    {
        Assert.Empty(new GameFiles(_host).Files);
    }

    [Fact]
    public void Nothing_but_a_game_file_can_be_asked_for()
    {
        HostGame();
        var game = new GameFiles(_host);
        Assert.Null(game.PathOf(GameFiles.Sha(Path.Combine(_host, "server", "tristinwold.accounts.json"))));
        Assert.Null(game.PathOf("../server/tristinwold.json"));
        Assert.NotNull(game.PathOf(game.Files[0].Sha));
    }

    [Fact]
    public void A_player_only_fetches_what_differs()
    {
        HostGame();
        Put(_player, "Tristinwold.exe", "exe");
        Put(_player, "Tristinwold.pck", "old pack");
        var different = GameUpdater.Different(new GameFiles(_host).Files, _player);
        Assert.Equal(["Tristinwold.pck", "data_Tristinwold.Game_windows_x86_64/Tristinwold.Game.dll"], different.Select(f => f.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_new_files_come_down_from_a_real_server_whole()
    {
        HostGame();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var world = new SharedWorldHost(new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false }), () => { });
        using var gateway = new HttpGateway(world, $"http://127.0.0.1:{port}/") { Game = new GameFiles(_host) };
        gateway.Start();
        var address = $"http://127.0.0.1:{port}";

        var offered = await GameUpdater.Offered(address);
        Assert.NotNull(offered);
        Put(_player, "Tristinwold.pck", "old pack");
        var different = GameUpdater.Different(offered!, _player);
        Assert.True(await GameUpdater.Fetch(address, different, _player));
        Assert.Equal("new pack", File.ReadAllText(Path.Combine(_player, GameUpdater.Staging, "Tristinwold.pck")));
        Assert.Equal("old pack", File.ReadAllText(Path.Combine(_player, "Tristinwold.pck"))); // nothing's touched until the game has closed

        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"{address}/update/file/nonsense")).StatusCode);
    }

    [Fact]
    public async Task A_server_without_updates_is_no_trouble()
    {
        Assert.Null(await GameUpdater.Offered("http://127.0.0.1:1"));
        Assert.False(GameUpdater.CanAsk("127.0.0.1:7777"));
        Assert.True(GameUpdater.CanAsk("https://example.trycloudflare.com"));
    }

    [Fact]
    public void The_swap_really_moves_the_new_files_in_over_the_old_on_windows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Put(_player, "Tristinwold.pck", "old pack");
        Put(_player, "data/keep.txt", "untouched");
        Put(_player, "server/tristinwold.json", "the world");
        Put(_player, $"{GameUpdater.Staging}/Tristinwold.pck", "new pack");
        Put(_player, $"{GameUpdater.Staging}/data/code.dll", "new code");
        var script = Path.Combine(_player, "apply-update.bat");
        File.WriteAllText(script, GameUpdater.SwapScript(999999, "whoami.exe", []));
        using var run = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false })!;
        Assert.True(run.WaitForExit(30000));
        for (var i = 0; i < 50 && File.Exists(script); i++) Thread.Sleep(100);
        Assert.Equal("new pack", File.ReadAllText(Path.Combine(_player, "Tristinwold.pck")));
        Assert.Equal("new code", File.ReadAllText(Path.Combine(_player, "data", "code.dll")));
        Assert.Equal("untouched", File.ReadAllText(Path.Combine(_player, "data", "keep.txt")));
        Assert.Equal("the world", File.ReadAllText(Path.Combine(_player, "server", "tristinwold.json")));
        Assert.False(Directory.Exists(Path.Combine(_player, GameUpdater.Staging)));
        Assert.False(File.Exists(script));
    }

    [Fact]
    public void The_swap_waits_for_the_game_then_reopens_it()
    {
        var script = GameUpdater.SwapScript(4321, "Tristinwold.exe", ["--", "--remote"]);
        Assert.Contains("PID eq 4321", script);
        Assert.Contains("robocopy", script);
        Assert.Contains("start \"\" \"Tristinwold.exe\" -- --remote", script);
    }
}
