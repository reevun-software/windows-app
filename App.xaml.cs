using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Reevun;

public partial class App : Application
{
    private readonly AppInstance _instance;
    private MainWindow? _window;

    public App(AppInstance instance)
    {
        _instance = instance;
        InitializeComponent();
    }

    // The first start checks for an update (Updates) before the window
    // opens; starting the app again only brings the window forward.
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instance.Activated += (_, _) => _window?.DispatcherQueue.TryEnqueue(() => _window.BringForward());
        if (await Updates.OnLaunchAsync()) return;
        _window = new MainWindow();
        _window.Activate();
        Updates.KeepChecking();
    }
}
