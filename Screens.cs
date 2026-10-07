using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Reevun;

// The app's own screens (reevun-software/app-core, one self-contained page
// built into the app as Screens\index.html), served to the app's own web
// views at https://screens.reevun.local/ and talking to the app through
// chrome.webview.postMessage; answers and events go back through
// window.reevunNative.receive. The site's web view has neither.
public static class Screens
{
    private const string Host = "screens.reevun.local";

    // `page`: "launch", "titlebar" or "loading". Each message goes to
    // `handle`, whose answer (if any) goes back.
    public static async Task<WebView2> ViewAsync(string page, Func<string, JsonArray, JsonNode?> handle)
    {
        var view = new WebView2 { DefaultBackgroundColor = Microsoft.UI.Colors.Transparent };
        await view.EnsureCoreWebView2Async(await WebEnvironment.GetAsync());
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "Screens"), CoreWebView2HostResourceAccessKind.Deny);
        // The screens never go anywhere else.
        core.NavigationStarting += (_, args) => args.Cancel = new Uri(args.Uri).Host != Host;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += (_, args) =>
        {
            if (new Uri(args.Source).Host != Host) return;
            var message = JsonNode.Parse(args.TryGetWebMessageAsString()) as JsonObject;
            var method = message?["method"]?.GetValue<string>();
            if (message is null || method is null) return;
            var result = handle(method, message["args"] as JsonArray ?? []);
            if (message["id"] is { } id) Send(view, new JsonObject { ["id"] = id.DeepClone(), ["result"] = result });
        };
        core.Navigate($"https://{Host}/index.html#{page}");
        return view;
    }

    // An event for a screen: "siteState", "maximized" or "updateStatus".
    public static void Emit(WebView2 view, string @event, JsonNode? data) =>
        Send(view, new JsonObject { ["event"] = @event, ["data"] = data });

    private static void Send(WebView2 view, JsonObject message) =>
        _ = view.CoreWebView2?.ExecuteScriptAsync($"window.reevunNative && window.reevunNative.receive({message.ToJsonString()})");

    // What every screen asks first.
    public static JsonObject Info() => new()
    {
        ["platform"] = "windows",
        ["version"] = Updates.Version,
        ["locale"] = CultureInfo.CurrentUICulture.Name,
        ["titleBar"] = new JsonObject { ["insetLeft"] = 0, ["windowButtons"] = true },
    };
}

// One WebView2 environment (and profile folder) for every web view.
public static class WebEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    public static Task<CoreWebView2Environment> GetAsync() => _environment ??= CoreWebView2Environment.CreateWithOptionsAsync(
        null, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reevun", "WebView2"), new CoreWebView2EnvironmentOptions()).AsTask();
}
