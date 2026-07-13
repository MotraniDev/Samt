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
    private const int SwShow = 5;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>First-launch layout: size, center on primary work area, then show.</summary>
    public static void ShowCentered(Window window, int width = 1100, int height = 720)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = window.Title;
        AppIconHelper.ApplyToAppWindow(appWindow);
        appWindow.Resize(new SizeInt32(width, height));

        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var x = work.X + Math.Max(0, (work.Width - width) / 2);
        var y = work.Y + Math.Max(0, (work.Height - height) / 2);
        appWindow.Move(new PointInt32(x, y));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.Restore();
        }

        FastShow(window, hwnd, appWindow);
    }

    /// <summary>
    /// Tray restore: show the existing window without re-centering or re-sizing
    /// (those WinUI operations are slow and feel like a hang).
    /// </summary>
    public static void Restore(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = window.Title;
        AppIconHelper.ApplyToAppWindow(appWindow);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }

        FastShow(window, hwnd, appWindow);
    }

    private static void FastShow(Window window, IntPtr hwnd, AppWindow appWindow)
    {
        // Prefer lightweight Win32 restore when the window already exists (hidden to tray).
        if (IsIconic(hwnd) || !IsWindowVisible(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }
        else
        {
            ShowWindow(hwnd, SwShow);
        }

        try
        {
            appWindow.Show();
        }
        catch
        {
            // Some hidden AppWindow states still need Show(); ignore rare failures.
        }

        window.Activate();
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }
}
