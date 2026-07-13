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
    private const int SwShowNormal = 1;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const int SwHide = 0;

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

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    /// <summary>Resolve AppWindow for a WinUI Window (never throws for a live window).</summary>
    public static AppWindow? TryGetAppWindow(Window window)
    {
        try
        {
            // WinUI 3 / WASDK: first-class property (preferred over reflection).
            return window.AppWindow;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"TryGetAppWindow AppWindow prop: {ex.Message}");
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(id);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"TryGetAppWindow interop: {ex.Message}");
            return null;
        }
    }

    public static IntPtr TryGetHwnd(Window window)
    {
        try
        {
            return WindowNative.GetWindowHandle(window);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Preferred placement: work-area right edge, vertically centered, compact size.
    /// Used on first launch and when restoring from the tray.
    /// </summary>
    public static void ShowDockedRight(
        Window window,
        int width = DefaultWidth,
        int height = DefaultHeight,
        AppWindow? cachedAppWindow = null)
    {
        var appWindow = cachedAppWindow ?? TryGetAppWindow(window);
        if (appWindow is null)
        {
            LaunchLog.Write("ShowDockedRight: AppWindow unavailable");
            try
            {
                window.Activate();
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"ShowDockedRight Activate fallback: {ex.Message}");
            }

            return;
        }

        var hwnd = TryGetHwnd(window);
        LaunchLog.Write(
            $"ShowDockedRight hwnd=0x{(hwnd == IntPtr.Zero ? 0 : hwnd.ToInt64()):X} " +
            $"visible={(hwnd != IntPtr.Zero && IsWindow(hwnd) && IsWindowVisible(hwnd))} " +
            $"iconic={(hwnd != IntPtr.Zero && IsWindow(hwnd) && IsIconic(hwnd))}");

        try
        {
            appWindow.Title = window.Title;
            AppIconHelper.ApplyToAppWindow(appWindow);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ShowDockedRight chrome: {ex.Message}");
        }

        // 1) Unhide first — AppWindow.Show is the correct pair to AppWindow.Hide.
        try
        {
            appWindow.Show();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ShowDockedRight AppWindow.Show: {ex.Message}");
        }

        if (hwnd != IntPtr.Zero && IsWindow(hwnd))
        {
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SwRestore);
            }
            else if (!IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SwShowNormal);
                if (!IsWindowVisible(hwnd))
                {
                    ShowWindow(hwnd, SwShow);
                }
            }
        }

        // 2) Layout after the window is shown again.
        ApplyMinSize(appWindow);
        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);

        try
        {
            appWindow.Resize(new SizeInt32(width, height));

            var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var work = display.WorkArea;
            var x = work.X + Math.Max(0, work.Width - width);
            var y = work.Y + Math.Max(0, (work.Height - height) / 2);
            appWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ShowDockedRight layout: {ex.Message}");
        }

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;

            try
            {
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"ShowDockedRight presenter.Restore: {ex.Message}");
            }
        }

        if (window is MainWindow main)
        {
            try
            {
                main.CollapseNavigationPane();
                main.ApplyShellOpacityFromSettings();
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"ShowDockedRight main chrome: {ex.Message}");
            }
        }

        // 3) Focus
        try
        {
            window.Activate();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ShowDockedRight Activate: {ex.Message}");
        }

        hwnd = TryGetHwnd(window);
        if (hwnd != IntPtr.Zero && IsWindow(hwnd))
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            if (!IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SwShowNormal);
                try
                {
                    appWindow.Show();
                }
                catch
                {
                    // ignore
                }
            }
        }

        LaunchLog.Write(
            $"ShowDockedRight done visible={(hwnd != IntPtr.Zero && IsWindow(hwnd) && IsWindowVisible(hwnd))}");
    }

    /// <summary>First-launch layout (legacy center). Prefer <see cref="ShowDockedRight"/>.</summary>
    public static void ShowCentered(Window window, int width = DefaultWidth, int height = DefaultHeight)
        => ShowDockedRight(window, width, height);

    /// <summary>
    /// Tray restore: re-apply compact docked placement + collapsed nav so the window
    /// always opens in the same predictable position.
    /// </summary>
    public static void Restore(Window window, AppWindow? cachedAppWindow = null)
        => ShowDockedRight(window, cachedAppWindow: cachedAppWindow);

    /// <summary>
    /// Hide without destroying the window (close-to-tray).
    /// Uses AppWindow.Hide (drops taskbar button) with Win32 SW_HIDE fallback.
    /// </summary>
    public static void HideToTray(Window window, AppWindow? cachedAppWindow = null)
    {
        var appWindow = cachedAppWindow ?? TryGetAppWindow(window);
        var hwnd = TryGetHwnd(window);

        try
        {
            appWindow?.Hide();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"HideToTray AppWindow.Hide: {ex.Message}");
        }

        // Ensure HWND is actually hidden even if AppWindow.Hide was a no-op.
        if (hwnd != IntPtr.Zero && IsWindow(hwnd) && IsWindowVisible(hwnd))
        {
            ShowWindow(hwnd, SwHide);
        }

        LaunchLog.Write(
            $"HideToTray done hwnd=0x{(hwnd == IntPtr.Zero ? 0 : hwnd.ToInt64()):X} " +
            $"visible={(hwnd != IntPtr.Zero && IsWindow(hwnd) && IsWindowVisible(hwnd))}");
    }

    private static void ApplyMinSize(AppWindow appWindow)
    {
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }
    }
}
