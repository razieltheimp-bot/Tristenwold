using System.Net.Sockets;
using System.Text;

namespace Tristinwold.Shared;

/// <summary>Asks a server an account question (log in, list or make characters) before joining.</summary>
public static class AccountClient
{
    /// <param name="address">The same address the game joins with: host:port for TCP, or http(s):// for the gateway.</param>
    public static async Task<AccountReply> AskAsync(string address, AccountRequest request, CancellationToken ct = default)
    {
        try
        {
            if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient { BaseAddress = new Uri(address.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
                using var res = await http.PostAsync("v1/account", new StringContent(Wire.Encode(request), Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return Failed($"The server answered {(int)res.StatusCode} {res.ReasonPhrase}.");
                return Wire.Decode(body) as AccountReply ?? Failed("The server sent something unexpected.");
            }
            var colon = address.LastIndexOf(':');
            var (host, port) = colon > 0 && int.TryParse(address[(colon + 1)..], out var p) ? (address[..colon], p) : (address, Wire.DefaultPort);
            using var tcp = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await tcp.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            using var conn = new LineConnection(tcp.GetStream());
            await conn.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            return await conn.ReadAsync(timeout.Token).ConfigureAwait(false) as AccountReply ?? Failed("The server didn't answer. Is it up to date?");
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or OperationCanceledException or System.Text.Json.JsonException)
        {
            return Failed($"Couldn't reach the server: {ex.Message}");
        }
    }

    static AccountReply Failed(string why) => new(false, why, null, []);
}
