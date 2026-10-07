using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Velopack;

namespace Reevun;

// Reevun for Windows: reevun.app in a window of its own (WinUI 3 and
// WebView2), with the app's own screens - title strip, loading and offline
// screens, update window - from reevun-software/app-core, the same in every
// Reevun app.
public static class Program
{
    [STAThread]
    private static void Main()
    {
        // Velopack's install, update and uninstall steps run (and exit) here.
        VelopackApp.Build().Run();
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // One app at a time: starting it again brings the open window forward.
        var main = AppInstance.FindOrRegisterForKey("main");
        if (!main.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait();
            return;
        }

        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(main);
        });
    }
}
