using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>System tray icon with open / exit menu (H.NotifyIcon).</summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        try
        {
            // SecondWindow: XAML MenuFlyout is reliable on WinUI unpackaged.
            // PopupMenu often fails to surface ContextFlyout items.
            _icon = new TaskbarIcon
            {
                ToolTipText = "SAMT",
                Icon = AppIconHelper.CreateTrayIcon(),
                ContextMenuMode = ContextMenuMode.SecondWindow,
                NoLeftClickDelay = true
            };

            var menu = new MenuFlyout();

            var openItem = new MenuFlyoutItem
            {
                Text = "فتح / Open",
                Icon = new FontIcon { Glyph = "\uE8A7" }
            };
            openItem.Click += (_, _) =>
            {
                LaunchLog.Write("Tray menu: Open");
                OpenRequested?.Invoke(this, EventArgs.Empty);
            };

            var exitItem = new MenuFlyoutItem
            {
                Text = "خروج / Exit",
                Icon = new FontIcon { Glyph = "\uE7E8" }
            };
            exitItem.Click += (_, _) =>
            {
                LaunchLog.Write("Tray menu: Exit");
                ExitRequested?.Invoke(this, EventArgs.Empty);
            };

            menu.Items.Add(openItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(exitItem);
            _icon.ContextFlyout = menu;

            // Left click → open main window
            _icon.LeftClickCommand = new RelayCommand(() =>
            {
                LaunchLog.Write("Tray left-click: Open");
                OpenRequested?.Invoke(this, EventArgs.Empty);
            });

            // Double-click → open (some shells)
            _icon.DoubleClickCommand = new RelayCommand(() =>
            {
                LaunchLog.Write("Tray double-click: Open");
                OpenRequested?.Invoke(this, EventArgs.Empty);
            });

            _icon.ForceCreate();
            LaunchLog.Write("Tray icon created (SecondWindow menu)");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Tray init failed (non-fatal): {ex}");
            _icon = null;
        }
    }

    public void UpdateMenuLabels(string openLabel, string exitLabel)
    {
        if (_icon?.ContextFlyout is not MenuFlyout menu)
        {
            return;
        }

        try
        {
            foreach (var item in menu.Items.OfType<MenuFlyoutItem>())
            {
                // Match by icon glyph we set at init.
                if (item.Icon is FontIcon { Glyph: "\uE8A7" })
                {
                    item.Text = openLabel;
                }
                else if (item.Icon is FontIcon { Glyph: "\uE7E8" })
                {
                    item.Text = exitLabel;
                }
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Tray menu labels failed: {ex.Message}");
        }
    }

    public void UpdateTooltip(string text)
    {
        if (_icon is null)
        {
            return;
        }

        try
        {
            _icon.ToolTipText = text.Length > 120 ? text[..120] : text;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Tray tooltip failed: {ex.Message}");
        }
    }

    /// <summary>Balloon/tray notification fallback when AppNotification is unavailable (unpackaged).</summary>
    public void ShowBalloon(string title, string message)
    {
        if (_icon is null)
        {
            return;
        }

        try
        {
            _icon.ShowNotification(
                title.Length > 60 ? title[..60] : title,
                message.Length > 200 ? message[..200] : message);
            LaunchLog.Write("Tray balloon shown");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Tray balloon failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _icon?.Dispose();
        }
        catch
        {
            // ignore
        }

        _icon = null;
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
