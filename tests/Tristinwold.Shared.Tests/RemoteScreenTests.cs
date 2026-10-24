using System.Net;
using System.Net.WebSockets;
using System.Text;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>Remote play's web server: the page, the key in the link, pictures out and touches back.</summary>
public class RemoteScreenTests
{
    [Fact]
    public async Task The_page_needs_the_key_from_the_link()
    {
        using var screen = new RemoteScreen(key: "secret");
        screen.Start();
        using var http = new HttpClient();

        var page = await http.GetAsync($"http://localhost:{screen.Port}/?k=secret");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Tristinwold", await page.Content.ReadAsStringAsync());

        var stranger = await http.GetAsync($"http://localhost:{screen.Port}/?k=guess");
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal("https://x.trycloudflare.com/?k=secret", screen.LinkFor("https://x.trycloudflare.com/"));
    }

    [Fact]
    public async Task A_phone_sees_the_game_and_its_touches_come_back()
    {
        using var screen = new RemoteScreen(key: "secret");
        screen.Start();
        using var phone = new ClientWebSocket();
        await phone.ConnectAsync(new Uri($"ws://localhost:{screen.Port}/ws?k=secret"), default);
        await Until(() => screen.Viewers == 1);

        // A picture goes out; the next waits until the phone says it drew the first.
        Assert.True(screen.Wanted);
        screen.Show([0xFF, 0xD8, 1, 2, 3]);
        Assert.False(screen.Wanted);
        var buffer = new byte[64];
        var got = await phone.ReceiveAsync(buffer, default);
        Assert.Equal(WebSocketMessageType.Binary, got.MessageType);
        Assert.Equal(5, got.Count);
        await phone.SendAsync(Encoding.UTF8.GetBytes("drawn"), WebSocketMessageType.Text, true, default);
        await Until(() => screen.Wanted);

        // A touch arrives for the game to read.
        await phone.SendAsync(Encoding.UTF8.GetBytes("""{"t":"d","i":1,"x":0.5,"y":0.5}"""), WebSocketMessageType.Text, true, default);
        await Until(() => !screen.Input.IsEmpty);
        Assert.True(screen.Input.TryDequeue(out var touch));
        Assert.Contains("\"t\":\"d\"", touch);
    }

    [Fact]
    public async Task No_key_no_pictures()
    {
        using var screen = new RemoteScreen(key: "secret");
        screen.Start();
        using var phone = new ClientWebSocket();
        await Assert.ThrowsAnyAsync<WebSocketException>(() => phone.ConnectAsync(new Uri($"ws://localhost:{screen.Port}/ws?k=nope"), default));
        Assert.Equal(0, screen.Viewers);
    }

    static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }
}
