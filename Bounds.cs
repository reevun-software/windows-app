using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Reevun;

// The window reopens where the person left it (and maximized if it was), as
// long as that spot is still on a connected display.
public static class Bounds
{
    private record Saved(int X, int Y, int Width, int Height, bool Maximized);

    private static string File => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reevun", "window.json");

    // `scale`: the display's scale (1.5 at 150%), for the first size.
    public static void Restore(AppWindow window, double scale)
    {
        Saved? saved = null;
        try { saved = JsonSerializer.Deserialize<Saved>(System.IO.File.ReadAllText(File)); } catch { }
        var area = DisplayArea.GetFromPoint(new PointInt32(saved?.X ?? 0, saved?.Y ?? 0), DisplayAreaFallback.None);
        if (saved is not null && area is not null)
            window.MoveAndResize(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height));
        else
        {
            var primary = DisplayArea.Primary.WorkArea;
            var (width, height) = (Math.Min((int)(1360 * scale), primary.Width), Math.Min((int)(860 * scale), primary.Height));
            window.MoveAndResize(new RectInt32(primary.X + (primary.Width - width) / 2, primary.Y + (primary.Height - height) / 2, width, height));
        }
        if (saved?.Maximized == true && window.Presenter is OverlappedPresenter presenter) presenter.Maximize();
    }

    public static void Save(AppWindow window)
    {
        if (window.Presenter is not OverlappedPresenter presenter || presenter.State == OverlappedPresenterState.Minimized) return;
        var maximized = presenter.State == OverlappedPresenterState.Maximized;
        Saved? previous = null;
        try { previous = JsonSerializer.Deserialize<Saved>(System.IO.File.ReadAllText(File)); } catch { }
        var (position, size) = (window.Position, window.Size);
        var saved = maximized && previous is not null
            ? previous with { Maximized = true }
            : new Saved(position.X, position.Y, size.Width, size.Height, maximized);
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, JsonSerializer.Serialize(saved));
    }
}
