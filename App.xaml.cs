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
        // What made the app close, kept next to its other files to look at.
        UnhandledException += (_, args) =>
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reevun");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "crash.log"), $"{DateTime.UtcNow:O} {args.Exception}\n\n");
        };
    }

    // The first start checks for an update (Updates) before the window
    // opens; starting the app again only brings the window forward.
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instance.Activated += (_, _) => _window?.DispatcherQueue.TryEnqueue(() => _window.BringForward());
        await Updates.OnLaunchAsync(() =>
        {
            _window = new MainWindow();
            _window.Activate();
            Updates.KeepChecking(_window);
        });
    }
}
