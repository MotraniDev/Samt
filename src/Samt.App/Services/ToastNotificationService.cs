using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>Shows local Windows app notifications for prayer events.</summary>
public sealed class ToastNotificationService
{
    private bool _registered;
    private TrayIconService? _trayFallback;

    public void Initialize(TrayIconService? trayFallback = null)
    {
        _trayFallback = trayFallback;
        try
        {
            // Unpackaged apps may not support full AppNotification registration.
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
            LaunchLog.Write("ToastNotificationService registered");
        }
        catch (Exception ex)
        {
            _registered = false;
            LaunchLog.Write($"Toast register failed (non-fatal, will use tray balloon): {ex.Message}");
        }
    }

    public void Show(PlannedNotification planned, string prayerName, string title)
    {
        var time = LatinDigits.Time(planned.FireAt);
        var body = planned.Kind == PlannedNotificationKind.BeforePrayer
            ? $"{prayerName} — {time} (−{LatinDigits.Number(planned.OffsetMinutes ?? 0)} min)"
            : $"{prayerName} — {time}";
        ShowText(title, body, planned.Id);
    }

    /// <summary>Simple title/body toast (missed-resume summary, diagnostics).</summary>
    public void ShowText(string title, string body, string? tag = null)
    {
        title = LatinDigits.EnsureLatin(title);
        body = LatinDigits.EnsureLatin(body);

        if (_registered)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .BuildNotification();

                notification.Tag = tag ?? ("samt-" + Guid.NewGuid().ToString("N")[..8]);
                notification.Group = "samt-prayer";
                AppNotificationManager.Default.Show(notification);
                LaunchLog.Write($"Toast shown: {notification.Tag}");
                return;
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Toast show failed: {ex.Message}");
            }
        }

        _trayFallback?.ShowBalloon(title, body);
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
            // ignore
        }

        _registered = false;
    }

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Bring main window forward when user clicks a toast.
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.MainWindow is { } window)
            {
                WindowActivation.ShowCentered(window);
            }
        });
    }
}
