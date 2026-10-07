using System.Diagnostics;

namespace Reevun;

// The site, marked as the app in its user agent (it then opens on the
// dashboard and keeps the profile in the bottom-left corner). Pages that
// open inside the app: the site itself and the other Reevun sites, and
// Discord's bot invite. Reevun ID (signing in, account settings) opens in
// the browser, as does anything else.
public static class Site
{
    public static readonly string Url = (Environment.GetEnvironmentVariable("REEVUN_SITE_URL") ?? "https://reevun.app").TrimEnd('/');
    public static readonly string IdUrl = (Environment.GetEnvironmentVariable("REEVUN_ID_URL") ?? "https://id.reevun.app").TrimEnd('/');

    private static bool InAppHost(string host) =>
        host == "reevun.app" || host.EndsWith(".reevun.app") || host == "discord.com" || host == "www.discord.com";

    // The site's "Sign in" (the API's single sign-on for reevun.app): in the
    // app that is the sign-in in the browser instead.
    public static bool IsSignIn(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.AbsolutePath == "/v1/auth/app/sso";

    public static bool Stays(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && InAppHost(uri.Host)
        && uri.Host != new Uri(IdUrl).Host && !IsSignIn(url);

    public static void OpenOutside(string url)
    {
        if (url.StartsWith("https:") || url.StartsWith("mailto:"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
