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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref uint value, int size);

    private const int DwmBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;

    // No thin border line around the window (Windows 11 draws one): the
    // window keeps its shadow, resizing and snapping, as the site's edges
    // meet the screen directly.
    public static void NoBorderLine(Window window)
    {
        var color = DwmColorNone;
        DwmSetWindowAttribute(WinRT.Interop.WindowNative.GetWindowHandle(window), DwmBorderColor, ref color, sizeof(uint));
    }

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
