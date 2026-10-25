using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tristinwold.Shared;

/// <summary>
/// Posts server news to a Telegram chat through a bot: the tunnel's address when it opens (so
/// phones can find the server), and who comes and goes. Configured from the environment, so the
/// bot's token never sits in a command line or a file in the repository:
///   TELEGRAM_BOT_TOKEN   the token @BotFather gave the bot
///   TELEGRAM_CHAT_ID     the chat to post in (message the bot once first, so it may write to you)
/// </summary>
public sealed class Telegram(string token, string chat, Action<string> log, string api = "https://api.telegram.org")
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    string _chat = chat;

    public static Telegram? FromEnvironment(Action<string> log)
    {
        var raw = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chat = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(chat)) return null;
        var token = CleanToken(raw);
        if (token is null)
        {
            log("TELEGRAM_BOT_TOKEN doesn't look like a bot token (it should look like 123456789:AAF...). " +
                "Copy it again from @BotFather; nothing will be posted to Telegram until then.");
            return null;
        }
        var api = Environment.GetEnvironmentVariable("TELEGRAM_API") ?? "https://api.telegram.org"; // for testing
        return new Telegram(token, chat.Trim(), log, api.TrimEnd('/'));
    }

    /// <summary>
    /// The token inside whatever was pasted: @BotFather's whole message, a "bot" prefix, quotes or
    /// an api.telegram.org link all still hold it. Null when there's no token there at all.
    /// </summary>
    public static string? CleanToken(string pasted)
    {
        var m = Regex.Match(pasted, @"(\d{6,}:[A-Za-z0-9_-]{30,})");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Sends without making the caller wait; a failure is logged, never thrown.</summary>
    public void Post(string text) => _ = SendAsync(text);

    public async Task<bool> SendAsync(string text)
    {
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var body = JsonSerializer.Serialize(new { chat_id = _chat, text, disable_web_page_preview = true });
                using var res = await _http.PostAsync($"{api}/bot{token}/sendMessage", new StringContent(body, Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode) return true;
                var reply = await res.Content.ReadAsStringAsync();
                // A group that became a supergroup has a new id; Telegram says which.
                if (attempt == 0 && MovedTo(reply) is { } moved)
                {
                    log($"Telegram moved chat {_chat} to {moved}; posting there (set TELEGRAM_CHAT_ID={moved} to skip this).");
                    _chat = moved;
                    continue;
                }
                log((int)res.StatusCode switch
                {
                    401 or 404 => "Telegram doesn't recognise the bot token. Copy it again from @BotFather " +
                                  "(run \"Change Telegram token.bat\" in the game's folder).",
                    400 when reply.Contains("chat not found") => $"Telegram can't find chat {_chat}. Add the bot to the group, then try again.",
                    403 => $"The bot isn't allowed to post in chat {_chat}. Add it to the group (or unblock it) and let it send messages.",
                    var code => $"Telegram didn't take the message: {code} {reply}",
                });
                return false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log($"Couldn't reach Telegram: {ex.InnerException?.Message ?? ex.Message}");
        }
        return false;
    }

    static string? MovedTo(string reply)
    {
        try
        {
            using var doc = JsonDocument.Parse(reply);
            return doc.RootElement.TryGetProperty("parameters", out var p) && p.TryGetProperty("migrate_to_chat_id", out var id)
                ? id.GetRawText() : null;
        }
        catch (JsonException) { return null; }
    }
}
