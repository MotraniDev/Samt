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

    /// <summary>Default compact shell: small enough for Today + rail, not smaller than content needs.</summary>
    public const int DefaultWidth = 440;
    public const int DefaultHeight = 680;
    public const int MinWidth = 380;
    public const int MinHeight = 520;

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

    /// <summary>
    /// Preferred placement: work-area right edge, vertically centered, compact size.
    /// Used on first launch and when restoring from the tray.
    /// </summary>
    public static void ShowDockedRight(
        Window window,
        int width = DefaultWidth,
        int height = DefaultHeight)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = window.Title;
        AppIconHelper.ApplyToAppWindow(appWindow);

        ApplyMinSize(appWindow);
        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);
        appWindow.Resize(new SizeInt32(width, height));

        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        // Far right of the work area; vertically centered.
        var x = work.X + Math.Max(0, work.Width - width);
        var y = work.Y + Math.Max(0, (work.Height - height) / 2);
        appWindow.Move(new PointInt32(x, y));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
            presenter.Restore();
        }

        if (window is MainWindow main)
        {
            main.CollapseNavigationPane();
        }

        FastShow(window, hwnd, appWindow);
    }

    /// <summary>First-launch layout (legacy center). Prefer <see cref="ShowDockedRight"/>.</summary>
    public static void ShowCentered(Window window, int width = DefaultWidth, int height = DefaultHeight)
        => ShowDockedRight(window, width, height);

    /// <summary>
    /// Tray restore: re-apply compact docked placement + collapsed nav so the window
    /// always opens in the same predictable position.
    /// </summary>
    public static void Restore(Window window)
        => ShowDockedRight(window);

    private static void ApplyMinSize(AppWindow appWindow)
    {
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }
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
