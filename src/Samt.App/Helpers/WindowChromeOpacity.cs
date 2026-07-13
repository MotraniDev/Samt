using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Samt_App.Helpers;

/// <summary>
/// Applies layered HWND alpha to shell windows (main, Adhkar reader, setup wizard).
/// Adhan overlay keeps its own opacity path via <see cref="Overlay.OverlayWindow"/>.
/// </summary>
internal static class WindowChromeOpacity
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x2;

    public static double Clamp(double opacity)
        => Math.Clamp(opacity, 0.30, 1.0);

    public static void Apply(Window window, double opacity)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            Apply(hwnd, opacity);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"WindowChromeOpacity.Apply window: {ex.Message}");
        }
    }

    public static void Apply(IntPtr hwnd, double opacity)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var alpha = (byte)Math.Clamp((int)Math.Round(Clamp(opacity) * 255), 1, 255);
            var ex = GetWindowLong(hwnd, GwlExStyle);
            if ((ex & WsExLayered) == 0)
            {
                SetWindowLong(hwnd, GwlExStyle, ex | WsExLayered);
            }

            SetLayeredWindowAttributes(hwnd, 0, alpha, LwaAlpha);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"WindowChromeOpacity.Apply hwnd: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex).ToInt32()
            : GetWindowLong32(hWnd, nIndex);

    private static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong)).ToInt32()
            : SetWindowLong32(hWnd, nIndex, dwNewLong);
}
