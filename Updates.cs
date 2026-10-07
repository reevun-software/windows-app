using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Reevun;

// Updates come from the windows-app GitHub releases. Every start of an
// installed copy goes through a small launch window that asks for the newest
// release and, when it's newer, downloads its Reevun_Setup.exe, checks it
// against the release's checksum, installs it silently and starts the new
// version - nothing to confirm, like a game launcher. While the app stays
// open it keeps checking, and a version found then installs when the app
// closes. A copy the installer didn't put there (a Microsoft Store install,
// updated by the Store, or a build run as is) skips all this.
public static class Updates
{
    private const string Release = "https://api.github.com/repos/reevun-software/windows-app/releases/latest";
    private const string Installer = "Reevun_Setup.exe";
    private const string Silently = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(4);
    // No answer from GitHub by then (offline, blocked): start without updating.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly HttpClient Http = new() { DefaultRequestHeaders = { { "User-Agent", "Reevun" } } };

    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    // Installed by Reevun_Setup.exe, which leaves its uninstaller beside the app.
    private static bool Installed => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    private record Asset(string Name, [property: JsonPropertyName("browser_download_url")] string Url, string? Digest);
    private record LatestRelease([property: JsonPropertyName("tag_name")] string Tag, Asset[] Assets);

    // `open` opens the app's window: right away when there's nothing to
    // check or no newer version; not at all when the new version is being
    // installed (it starts by itself).
    public static async Task OnLaunchAsync(Action open)
    {
        if (!Installed)
        {
            open();
            return;
        }
        var window = LaunchWindow();
        var launch = await Screens.ViewAsync("launch", (method, _) => method == "info" ? Screens.Info() : null);
        window.Content = launch;
        // The whole window moves it.
        Win32.SetCaption(window, (_, _) => new Windows.Graphics.RectInt32(0, 0, window.AppWindow.Size.Width, window.AppWindow.Size.Height));
        window.Activate();
        void Report(JsonObject status) => Screens.Emit(launch, "updateStatus", status);

        try
        {
            Report(new JsonObject { ["phase"] = "checking" });
            if (await NewerAsync().WaitAsync(CheckTimeout) is { Url: { } url, Checksum: { } checksum })
            {
                var file = await DownloadAsync(url, checksum, percent =>
                    launch.DispatcherQueue.TryEnqueue(() => Report(new JsonObject { ["phase"] = "downloading", ["percent"] = percent })));
                Report(new JsonObject { ["phase"] = "installing" });
                await Task.Delay(600);
                // The installer closes this copy, puts the new one in place
                // and starts it (/relaunch).
                Process.Start(file, $"{Silently} /relaunch=1");
                Application.Current.Exit();
                return;
            }
        }
        catch (Exception) { }
        // The app's window first: the last window closing ends the app.
        open();
        window.Close();
    }

    // While the app is open: a new version downloads in the background and
    // installs when `main` (the app's window) closes.
    public static void KeepChecking(Window main)
    {
        if (!Installed) return;
        string? ready = null;
        main.AppWindow.Closing += (_, _) =>
        {
            if (ready is not null) Process.Start(ready, Silently);
        };
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(CheckEvery);
            while (ready is null && await timer.WaitForNextTickAsync())
            {
                try
                {
                    if (await NewerAsync() is { Url: { } url, Checksum: { } checksum })
                        ready = await DownloadAsync(url, checksum, _ => { });
                }
                catch (Exception) { }
            }
        });
    }

    private static Window LaunchWindow()
    {
        var window = new Window { Title = "Reevun", ExtendsContentIntoTitleBar = true };
        var scale = Win32.Scale(window);
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(320 * scale), (int)(360 * scale)));
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }
        var area = DisplayArea.Primary.WorkArea;
        window.AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - window.AppWindow.Size.Width) / 2, area.Y + (area.Height - window.AppWindow.Size.Height) / 2));
        return window;
    }

    // The newest release's installer and its SHA-256, when it's newer than
    // this copy.
    private static async Task<(string? Url, string? Checksum)?> NewerAsync()
    {
        var release = await Http.GetFromJsonAsync<LatestRelease>(Release);
        if (release is null || !System.Version.TryParse(release.Tag.TrimStart('v'), out var newest)
            || !System.Version.TryParse(Version, out var current) || newest <= current) return null;
        var asset = release.Assets.FirstOrDefault(a => a.Name == Installer);
        return (asset?.Url, asset?.Digest?.StartsWith("sha256:") == true ? asset.Digest[7..] : null);
    }

    private static async Task<string> DownloadAsync(string url, string checksum, Action<int> progress)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        var file = Path.Combine(Path.GetTempPath(), Installer);
        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var target = File.Create(file))
        {
            var buffer = new byte[1 << 16];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0) progress((int)(done * 100 / total));
            }
        }
        await using (var written = File.OpenRead(file))
        {
            if (!Convert.ToHexString(await SHA256.HashDataAsync(written)).Equals(checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("checksum");
        }
        return file;
    }
}
