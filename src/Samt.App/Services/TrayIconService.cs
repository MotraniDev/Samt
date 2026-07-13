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
            _icon = new TaskbarIcon
            {
                ToolTipText = "SAMT",
                // Generated icon from app resources when possible.
                Icon = AppIconHelper.CreateTrayIcon(),
                ContextMenuMode = ContextMenuMode.PopupMenu
            };

            // Win32 popup menu items via ContextFlyout alternative — use MenuFlyout through ContextMenuMode.
            var menu = new MenuFlyout();
            var openItem = new MenuFlyoutItem { Text = "Open / فتح" };
            openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
            var exitItem = new MenuFlyoutItem { Text = "Exit / خروج" };
            exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(openItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(exitItem);
            _icon.ContextFlyout = menu;

            _icon.LeftClickCommand = new RelayCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty));

            _icon.ForceCreate();
            LaunchLog.Write("Tray icon created");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Tray init failed (non-fatal): {ex}");
            _icon = null;
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
