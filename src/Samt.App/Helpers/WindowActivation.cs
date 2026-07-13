using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Samt_App.Helpers;

/// <summary>Forces a WinUI window onto a visible primary monitor (unpackaged-friendly).</summary>
internal static class WindowActivation
{
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    public static void ShowCentered(Window window, int width = 1100, int height = 720)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = window.Title;
        appWindow.Resize(new SizeInt32(width, height));

        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var x = work.X + Math.Max(0, (work.Width - width) / 2);
        var y = work.Y + Math.Max(0, (work.Height - height) / 2);
        appWindow.Move(new PointInt32(x, y));

        // Ensure overlapped presenter is not minimized / compact overlay.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.Restore();
        }

        appWindow.Show();
        window.Activate();

        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }

        SetForegroundWindow(hwnd);
    }
}
