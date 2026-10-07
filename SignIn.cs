using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Reevun;

// Signing in happens in the system browser, not in the app: the app opens
// Reevun ID's id.reevun.app/app?port=…&state=… and listens on that port of
// 127.0.0.1. There the person signs in (or already is) and confirms; Reevun
// ID sends the browser back here with a session of the app's own, which
// becomes the app's session cookie - the same one the website reads. An
// unfinished sign-in stops listening after Timeout.
public sealed class SignIn : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new(Timeout);
    private readonly string _state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Url { get; }

    // `signedIn` gets the session token, on the caller's thread.
    public SignIn(Action<string> signedIn)
    {
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"{Site.IdUrl}/app?port={port}&state={Uri.EscapeDataString(_state)}";
        var context = SynchronizationContext.Current;
        _ = ListenAsync(token => context?.Post(_ => signedIn(token), null));
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
                stream.ReadTimeout = 5000;
                var buffer = new byte[8192];
                var read = await stream.ReadAsync(buffer, _stop.Token);
                var line = Encoding.UTF8.GetString(buffer, 0, read).Split("\r\n")[0];
                var token = TokenFrom(line, _state);
                var answer = token is null
                    ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : $"HTTP/1.1 302 Found\r\nLocation: {Site.IdUrl}/app/done\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), _stop.Token);
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

    // "GET /callback?state=…&token=… HTTP/1.1" with this sign-in's state.
    public static string? TokenFrom(string line, string state)
    {
        var parts = line.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET" || !Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out var uri) || uri.AbsolutePath != "/callback")
            return null;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var token = query["token"];
        return query["state"] == state && !string.IsNullOrEmpty(token) ? token : null;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
