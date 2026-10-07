using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Reevun;

// The bits of window handling WinUI leaves to the system.
public static class Win32
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    // The display's scale for the window (1.5 at 150%).
    public static double Scale(Window window) => GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96.0;

    // The part of the window that moves it, as a title bar does (double-click
    // maximizes, Windows' snap layouts): `caption` gives it, in physical
    // pixels, again on every resize.
    public static void SetCaption(Window window, Func<Window, double, RectInt32> caption)
    {
        var source = InputNonClientPointerSource.GetForWindowId(window.AppWindow.Id);
        void Update() => source.SetRegionRects(NonClientRegionKind.Caption, [caption(window, Scale(window))]);
        Update();
        window.SizeChanged += (_, _) => Update();
    }
}
