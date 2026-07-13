using System.Drawing;
using Microsoft.UI.Windowing;

namespace Samt_App.Helpers;

/// <summary>Shared SAMT icon for window/taskbar and system tray.</summary>
internal static class AppIconHelper
{
    private static string? _cachedPath;
    private static Icon? _cachedTrayIcon;

    public static string? ResolveIconPath()
    {
        if (_cachedPath is not null && File.Exists(_cachedPath))
        {
            return _cachedPath;
        }

        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "Assets", "AppIcon.ico"),
            Path.Combine(baseDir, "AppIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "..", "Assets", "AppIcon.ico")
        ];

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full))
                {
                    _cachedPath = full;
                    return full;
                }
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    public static void ApplyToAppWindow(AppWindow appWindow)
    {
        try
        {
            var path = ResolveIconPath();
            if (path is null)
            {
                return;
            }

            appWindow.SetIcon(path);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"AppIcon SetIcon failed: {ex.Message}");
        }
    }

    /// <summary>Tray uses the same .ico as the taskbar when available.</summary>
    public static Icon CreateTrayIcon()
    {
        try
        {
            var path = ResolveIconPath();
            if (path is not null)
            {
                // Clone so TaskbarIcon dispose does not free a shared handle incorrectly.
                _cachedTrayIcon ??= new Icon(path);
                return (Icon)_cachedTrayIcon.Clone();
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"AppIcon tray load failed: {ex.Message}");
        }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(0x0B, 0x1F, 0x33));
            using var brush = new SolidBrush(Color.FromArgb(0xC4, 0xA3, 0x5A));
            g.FillEllipse(brush, 2, 2, 12, 12);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
