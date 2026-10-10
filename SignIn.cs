using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reevun;

// Signing in happens in the system browser, not in the app: the app opens
// Reevun ID's id.reevun.app/app?port=…&challenge=…&state=… and listens there.
// 127.0.0.1. There the person signs in (or already is) and confirms; Reevun
// ID sends a one-time code back here. The app trades it with its private
// PKCE verifier for the session, so the session token never appears in a
// browser address. An unfinished sign-in stops after Timeout.
public sealed class SignIn : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new(Timeout);
    private readonly string _state;
    private readonly string _verifier;

    public string Url { get; }

    // `signedIn` gets the session token, on the caller's thread.
    public SignIn(Action<string> signedIn)
    {
        _state = Base64Url(RandomNumberGenerator.GetBytes(24));
        _verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(_verifier)));
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"{Site.IdUrl}/app?port={port}&challenge={Uri.EscapeDataString(challenge)}&state={Uri.EscapeDataString(_state)}";
        var context = SynchronizationContext.Current;
        _ = ListenAsync(token =>
        {
            if (context is null) signedIn(token);
            else context.Post(_ => signedIn(token), null);
        });
        Site.OpenOutside(Url);
    }

    public void Reopen() => Site.OpenOutside(Url);

    private async Task ListenAsync(Action<string> signedIn)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var stream = client.GetStream();
                var buffer = new byte[8192];
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer, _stop.Token).AsTask().WaitAsync(ReadTimeout, _stop.Token);
                }
                catch (TimeoutException)
                {
                    continue;
                }
                var line = Encoding.UTF8.GetString(buffer, 0, read).Split("\r\n")[0];
                var code = CodeFrom(line, _state);
                var answer = code is null
                    ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : $"HTTP/1.1 302 Found\r\nLocation: {Site.IdUrl}/app/done\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), _stop.Token);
                if (code is null) continue;
                var token = await RedeemAsync(code, _verifier, _stop.Token);
                if (token is null) continue;
                signedIn(token);
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        catch (IOException) { }
        finally
        {
            _listener.Stop();
        }
    }

    // "GET /callback?state=…&code=… HTTP/1.1" with this sign-in's state.
    public static string? CodeFrom(string line, string state)
    {
        var parts = line.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET" || !Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out var uri) || uri.AbsolutePath != "/callback")
            return null;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var code = query["code"];
        return query["state"] == state && !string.IsNullOrEmpty(code) ? code : null;
    }

    private static async Task<string?> RedeemAsync(string code, string verifier, CancellationToken cancellation)
    {
        using var response = await Http.PostAsJsonAsync($"{Site.ApiUrl}/v1/auth/code", new { code, client = "app", verifier }, cancellation);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellation);
        return body.TryGetProperty("token", out var token) && token.GetString() is { Length: > 0 } value ? value : null;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
