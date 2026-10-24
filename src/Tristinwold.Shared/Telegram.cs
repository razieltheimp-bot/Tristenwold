using System.Text;
using System.Text.Json;

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

    public static Telegram? FromEnvironment(Action<string> log)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chat = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chat)) return null;
        var api = Environment.GetEnvironmentVariable("TELEGRAM_API") ?? "https://api.telegram.org"; // for testing
        return new Telegram(token.Trim(), chat.Trim(), log, api.TrimEnd('/'));
    }

    /// <summary>Sends without making the caller wait; a failure is logged, never thrown.</summary>
    public void Post(string text) => _ = SendAsync(text);

    public async Task<bool> SendAsync(string text)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { chat_id = chat, text, disable_web_page_preview = true });
            using var res = await _http.PostAsync($"{api}/bot{token}/sendMessage", new StringContent(body, Encoding.UTF8, "application/json"));
            if (res.IsSuccessStatusCode) return true;
            log($"Telegram didn't take the message: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log($"Couldn't reach Telegram: {ex.InnerException?.Message ?? ex.Message}");
        }
        return false;
    }
}
