using System.Net;
using System.Text;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>The Telegram poster, against a pretend Telegram on this machine.</summary>
public class TelegramTests
{
    const string Token = "123456789:AAFabcdefghijklmnopqrstuvwxyz_0123-45";

    [Theory]
    [InlineData(Token)]
    [InlineData("  \"" + Token + "\" ")]
    [InlineData("bot" + Token)]
    [InlineData("https://api.telegram.org/bot" + Token + "/getMe")]
    [InlineData("Use this token to access the HTTP API:\n" + Token + "\nKeep your token secure")]
    public void FindsTheTokenInWhateverWasPasted(string pasted) => Assert.Equal(Token, Telegram.CleanToken(pasted));

    [Theory]
    [InlineData("-5450084003")]
    [InlineData("@MyTristinwoldBot")]
    [InlineData("https://t.me/MyTristinwoldBot")]
    public void SomethingElseIsNotAToken(string pasted) => Assert.Null(Telegram.CleanToken(pasted));

    [Fact]
    public async Task FollowsAGroupThatBecameASupergroup()
    {
        var port = 40000 + Random.Shared.Next(2000);
        using var fake = new HttpListener();
        fake.Prefixes.Add($"http://127.0.0.1:{port}/");
        fake.Start();
        var bodies = new List<string>();
        var serve = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var ctx = await fake.GetContextAsync();
                using (var r = new StreamReader(ctx.Request.InputStream)) bodies.Add(await r.ReadToEndAsync());
                var moved = i == 0;
                ctx.Response.StatusCode = moved ? 400 : 200;
                var reply = moved
                    ? """{"ok":false,"error_code":400,"description":"Bad Request: group chat was upgraded to a supergroup chat","parameters":{"migrate_to_chat_id":-1005450084003}}"""
                    : """{"ok":true}""";
                var bytes = Encoding.UTF8.GetBytes(reply);
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });
        var said = new List<string>();
        var telegram = new Telegram(Token, "-5450084003", said.Add, $"http://127.0.0.1:{port}");

        Assert.True(await telegram.SendAsync("hello"));
        await serve;
        Assert.Contains("\"chat_id\":\"-5450084003\"", bodies[0]);
        Assert.Contains("-1005450084003", bodies[1]);
        Assert.Contains(said, s => s.Contains("-1005450084003"));
    }
}
