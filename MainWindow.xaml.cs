using System.Text.Json.Nodes;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace Reevun;

// The window: the app's title strip on top (with its own minimize /
// maximize / close buttons), the site under it, and the loading screen over
// the site until it has loaded - or saying it can't (offline), or waiting
// for the sign-in in the browser.
public sealed partial class MainWindow : Window
{
    private enum SiteState { Loading, Offline, Browser, Ready }

    // The strip's three buttons, 46 px each, at its right end.
    private const int ButtonsWidth = 3 * 46;
    // The loading screen stays at least this long, so it fades instead of
    // flashing; the fade itself takes Fade.
    private static readonly TimeSpan MinLoading = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(400);
    private const string SessionCookie = "reevun_session";
    private const int SessionDays = 60;

    private readonly WebView2 _site = new() { DefaultBackgroundColor = Microsoft.UI.Colors.White };
    private WebView2? _strip;
    private WebView2? _loading;
    // The loading screen fading out, if one is.
    private WebView2? _hiding;
    private DateTime _shownAt = DateTime.UtcNow;
    private SiteState _state = SiteState.Loading;
    private bool _failed;
    private SignIn? _signIn;

    private OverlappedPresenter Presenter => (OverlappedPresenter)AppWindow.Presenter;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        Presenter.SetBorderAndTitleBar(true, false);
        var scale = Win32.Scale(this);
        Bounds.Restore(AppWindow, scale);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
        Presenter.PreferredMinimumWidth = (int)(960 * scale);
        Presenter.PreferredMinimumHeight = (int)(620 * scale);
        // The strip moves the window, all but its buttons.
        Win32.SetCaption(this, (window, s) => new RectInt32(0, 0, Math.Max(0, window.AppWindow.Size.Width - (int)(ButtonsWidth * s)), (int)(36 * s)));
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange || args.DidSizeChange)
                if (_strip is not null) Screens.Emit(_strip, "maximized", Presenter.State == OverlappedPresenterState.Maximized);
        };
        AppWindow.Closing += (_, _) =>
        {
            Bounds.Save(AppWindow);
            _signIn?.Dispose();
        };
        SiteArea.Children.Add(_site);
        _ = StartAsync();
    }

    public void BringForward()
    {
        if (Presenter.State == OverlappedPresenterState.Minimized) Presenter.Restore();
        Activate();
    }

    private async Task StartAsync()
    {
        _strip = await Screens.ViewAsync("titlebar", StripMessage);
        Strip.Children.Add(_strip);
        await ShowOverlayAsync(SiteState.Loading);

        await _site.EnsureCoreWebView2Async(await WebEnvironment.GetAsync());
        var core = _site.CoreWebView2;
        core.Settings.UserAgent += $" ReevunApp/{Updates.Version} (windows)";
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        Guard(core);
        // A page that failed (offline, the site down) keeps the screen up in
        // its offline state until a load succeeds; a load stopped by the app
        // isn't one.
        core.NavigationStarting += (_, _) => _failed = false;
        core.NavigationCompleted += async (_, args) =>
        {
            if (args.IsSuccess)
            {
                if (!_failed && _state != SiteState.Ready) ShowSite();
                return;
            }
            _failed = true;
            if (args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) await ShowOverlayAsync(SiteState.Offline);
        };
        // The page process gone (out of memory, say): load again.
        core.ProcessFailed += async (_, _) =>
        {
            await ShowOverlayAsync(SiteState.Loading);
            Dashboard();
        };
        Dashboard();
    }

    private void Dashboard() => _site.CoreWebView2?.Navigate($"{Site.Url}/dashboard");

    // Reevun pages stay (Discord's bot invite as a popup window, as on the
    // website); signing in goes to the browser sign-in; Reevun ID's pages
    // and other links open in the browser - links, redirects and new
    // windows alike. What the page embeds (frames) is left alone.
    private void Guard(CoreWebView2 core)
    {
        core.NavigationStarting += (_, args) =>
        {
            if (Site.Stays(args.Uri) || args.Uri.StartsWith("about:")) return;
            args.Cancel = true;
            Leave(args.Uri);
        };
        core.NewWindowRequested += async (_, args) =>
        {
            if (!Site.Stays(args.Uri))
            {
                args.Handled = true;
                Leave(args.Uri);
                return;
            }
            var deferral = args.GetDeferral();
            var popup = new WebView2();
            var window = new Window { Title = "Reevun", Content = popup };
            var scale = Win32.Scale(window);
            window.AppWindow.Resize(new SizeInt32((int)(500 * scale), (int)(760 * scale)));
            window.Activate();
            await popup.EnsureCoreWebView2Async(await WebEnvironment.GetAsync());
            popup.CoreWebView2.Settings.UserAgent = core.Settings.UserAgent;
            Guard(popup.CoreWebView2);
            popup.CoreWebView2.WindowCloseRequested += (_, _) => window.Close();
            args.NewWindow = popup.CoreWebView2;
            args.Handled = true;
            deferral.Complete();
        };
    }

    private void Leave(string url)
    {
        if (Site.IsSignIn(url)) _ = SignInInBrowserAsync();
        else Site.OpenOutside(url);
    }

    private void Report(SiteState next)
    {
        _state = next;
        if (_loading is not null) Screens.Emit(_loading, "siteState", next.ToString().ToLowerInvariant());
    }

    // The screen over the site, in a given state (made again if it's gone or
    // going).
    private async Task ShowOverlayAsync(SiteState next)
    {
        if (_loading is null || _loading == _hiding)
        {
            _loading = null;
            var overlay = await Screens.ViewAsync("loading", LoadingMessage);
            SiteArea.Children.Add(overlay);
            _loading = overlay;
            _shownAt = DateTime.UtcNow;
        }
        Report(next);
    }

    // The site has loaded: the screen fades out (its page does that on
    // "ready"), then goes.
    private async void ShowSite()
    {
        if (_loading is not { } overlay || overlay == _hiding) return;
        _hiding = overlay;
        var wait = MinLoading - (DateTime.UtcNow - _shownAt);
        if (wait > TimeSpan.Zero) await Task.Delay(wait);
        Report(SiteState.Ready);
        await Task.Delay(Fade);
        SiteArea.Children.Remove(overlay);
        overlay.Close();
        if (_loading == overlay) _loading = null;
        if (_hiding == overlay) _hiding = null;
    }

    private JsonNode? LoadingMessage(string method, JsonArray args)
    {
        switch (method)
        {
            case "info": return Screens.Info();
            case "siteState": return _state.ToString().ToLowerInvariant();
            case "retry":
                Report(SiteState.Loading);
                Dashboard();
                break;
            case "reopenSignIn":
                if (_signIn is null) _ = SignInInBrowserAsync();
                else _signIn.Reopen();
                break;
            case "cancelSignIn":
                _signIn?.Dispose();
                _signIn = null;
                ShowSite();
                break;
        }
        return null;
    }

    // The strip's own window buttons (it shows "restore" while maximized).
    private JsonNode? StripMessage(string method, JsonArray args)
    {
        switch (method, args.FirstOrDefault()?.GetValue<string>())
        {
            case ("info", _): return Screens.Info();
            case ("isMaximized", _): return Presenter.State == OverlappedPresenterState.Maximized;
            case ("windowControl", "minimize"): Presenter.Minimize(); break;
            case ("windowControl", "maximize"):
                if (Presenter.State == OverlappedPresenterState.Maximized) Presenter.Restore();
                else Presenter.Maximize();
                break;
            case ("windowControl", "close"): Close(); break;
        }
        return null;
    }

    // While the browser sign-in is open the app waits on its own screen;
    // signed in, the dashboard loads with the new session.
    private async Task SignInInBrowserAsync()
    {
        await ShowOverlayAsync(SiteState.Browser);
        if (_signIn is not null)
        {
            _signIn.Reopen();
            return;
        }
        _signIn = new SignIn(token =>
        {
            _signIn?.Dispose();
            _signIn = null;
            var cookies = _site.CoreWebView2.CookieManager;
            var cookie = cookies.CreateCookie(SessionCookie, token, new Uri(Site.Url).Host, "/");
            cookie.IsSecure = true;
            cookie.IsHttpOnly = true;
            cookie.SameSite = CoreWebView2CookieSameSiteKind.Lax;
            cookie.Expires = DateTimeOffset.UtcNow.AddDays(SessionDays).ToUnixTimeSeconds();
            cookies.AddOrUpdateCookie(cookie);
            Report(SiteState.Loading);
            Dashboard();
            BringForward();
        });
    }
}
