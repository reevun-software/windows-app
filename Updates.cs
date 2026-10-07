using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Velopack;
using Velopack.Sources;

namespace Reevun;

// Updates come from the windows-app GitHub releases (Velopack). Every start
// goes through a small launch window that checks for a new version and,
// when there is one, downloads and installs it and restarts - nothing to
// confirm, like a game launcher. While the app stays open it keeps checking,
// and a version found then installs when the app closes. A Microsoft Store
// install (not installed by Velopack) is updated by the Store.
public static class Updates
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(4);
    // No answer from GitHub by then (offline, blocked): start without updating.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly UpdateManager Manager = new(new GithubSource("https://github.com/reevun-software/windows-app", null, false));

    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    // Whether the app is restarting into a new version (then nothing opens).
    public static async Task<bool> OnLaunchAsync()
    {
        if (!Manager.IsInstalled) return false;
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
        var launch = await Screens.ViewAsync("launch", (method, _) => method == "info" ? Screens.Info() : null);
        window.Content = launch;
        // The whole window moves it.
        Win32.SetCaption(window, (_, _) => new Windows.Graphics.RectInt32(0, 0, window.AppWindow.Size.Width, window.AppWindow.Size.Height));
        window.Activate();
        void Report(JsonObject status) => Screens.Emit(launch, "updateStatus", status);

        try
        {
            Report(new JsonObject { ["phase"] = "checking" });
            var update = await Manager.CheckForUpdatesAsync().WaitAsync(CheckTimeout);
            if (update is null)
            {
                window.Close();
                return false;
            }
            await Manager.DownloadUpdatesAsync(update, percent => launch.DispatcherQueue.TryEnqueue(() =>
                Report(new JsonObject { ["phase"] = "downloading", ["percent"] = percent })));
            Report(new JsonObject { ["phase"] = "installing" });
            await Task.Delay(600);
            Manager.ApplyUpdatesAndRestart(update);
            return true;
        }
        catch (Exception)
        {
            window.Close();
            return false;
        }
    }

    // While the app is open: a new version downloads in the background and
    // installs when the app is closed.
    public static void KeepChecking()
    {
        if (!Manager.IsInstalled) return;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(CheckEvery);
            while (await timer.WaitForNextTickAsync())
            {
                try
                {
                    if (await Manager.CheckForUpdatesAsync() is not { } update) continue;
                    await Manager.DownloadUpdatesAsync(update);
                    Manager.WaitExitThenApplyUpdates(update, silent: true, restart: false);
                    return;
                }
                catch (Exception) { }
            }
        });
    }
}
