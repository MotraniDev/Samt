using Microsoft.UI.Xaml;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt.Core.Storage;
using Samt_App.Helpers;
using Samt_App.Overlay;
using Samt_App.Services;

namespace Samt_App;

public partial class App : Application
{
    private Window? _window;
    private NotificationHost? _notificationHost;
    private ToastNotificationService? _toasts;
    private TrayIconService? _tray;
    private AdhanAudioService? _audio;
    private OverlayService? _overlay;
    private AdhkarReminderService? _adhkarReminders;
    private SingleInstanceService? _singleInstance;
    private bool _exitRequested;
    private bool _startMinimized;

    public App()
    {
        LatinDigits.ApplyProcessDefaults("ar");

        UnhandledException += (_, e) =>
        {
            LaunchLog.Write($"UnhandledException: {e.Exception}");
            if (_window is not null)
            {
                e.Handled = true;
            }
        };

        InitializeComponent();
        Localization = new LocalizationService();
        Theme = new ThemeService();
        Updates = new UpdateService();
        LaunchLog.Write("App constructed");
    }

    public static LocalizationService Localization { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static UpdateService Updates { get; private set; } = null!;
    public static AppState State { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }
    public static NotificationHost? Notifications { get; private set; }
    public static AdhkarReminderService? AdhkarReminders { get; private set; }

    public static bool IsExitRequested { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LaunchLog.Write("OnLaunched begin");
        try
        {
            var cliArgs = Environment.GetCommandLineArgs();
            _startMinimized = AutoStartService.IsRequestedFromCommandLine(cliArgs);

            _singleInstance = new SingleInstanceService();
            if (!_singleInstance.TryClaimAsPrimary())
            {
                // Another instance is running and was asked to show — exit this process cleanly.
                LaunchLog.Write("Exiting secondary instance");
                Environment.Exit(0);
                return;
            }

            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAMT");
            Directory.CreateDirectory(dataDir);

            try
            {
                State = new AppState(new JsonSettingsStore(dataDir));
                await State.LoadAsync();
                LaunchLog.Write("Settings loaded");
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Settings load failed: {ex.Message}");
                State = new AppState(new JsonSettingsStore(dataDir));
                await State.LoadAsync();
            }

            try
            {
                Localization.Initialize(State.Settings.Language);
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Localization init failed: {ex.Message}");
                Localization.Initialize("ar");
            }

            LatinDigits.ApplyProcessDefaults(Localization.CurrentLanguage);

            // Keep Run key in sync with settings (unpackaged personal install).
            AutoStartService.Apply(State.Settings.AutoStartEnabled);
            State.SettingsChanged += (_, _) =>
            {
                try
                {
                    AutoStartService.Apply(State.Settings.AutoStartEnabled);
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"AutoStart on settings change failed: {ex.Message}");
                }
            };

            _tray = new TrayIconService();
            _tray.Initialize();
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.ExitRequested += (_, _) => RequestExitCore("tray");
            // Localized tray labels when language is already known.
            try
            {
                _tray.UpdateMenuLabels(
                    Localization.Get("TrayOpen"),
                    Localization.Get("TrayExit"));
            }
            catch
            {
                // keys may be missing on first run — defaults already set
            }

            _toasts = new ToastNotificationService();
            _toasts.Initialize(_tray);

            _audio = new AdhanAudioService();
            _overlay = new OverlayService(Localization, _audio);

            _window = new MainWindow();
            MainWindow = _window;
            ApplyThemeFromSettings();
            WireCloseToTray(_window);
            WindowActivation.ShowDockedRight(_window);
            if (_window is MainWindow mainAtLaunch)
            {
                mainAtLaunch.CollapseNavigationPane();
            }

            if (_startMinimized)
            {
                HideMainWindowToTray();
                LaunchLog.Write("Started minimized (--autostart)");
            }

            _notificationHost = new NotificationHost(State, Localization, _toasts, _tray, _overlay, _audio);
            Notifications = _notificationHost;
            _notificationHost.Start();

            _adhkarReminders = new AdhkarReminderService(State);
            AdhkarReminders = _adhkarReminders;
            _overlay.Dismissed += (_, _) => _adhkarReminders.OnAdhanOverlayDismissed();
            _notificationHost.PrayerStartDispatched += (_, prayer) =>
            {
                // Queue only while an overlay session is active; otherwise open After-prayer immediately.
                _adhkarReminders.NotifyPrayerStartCompleted(prayer, overlayWasShown: _overlay.IsSessionActive);
            };
            _adhkarReminders.Start();

            if (State.Settings.AutoCheckUpdates)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                        var result = await Updates.CheckAsync().ConfigureAwait(false);
                        if (result is { Success: true, UpdateAvailable: true })
                        {
                            LaunchLog.Write($"Update available: {result.AvailableVersion}");
                            // User opens Settings → Check for updates to download (no silent download).
                        }
                    }
                    catch (Exception ex)
                    {
                        LaunchLog.Write($"Background update check failed: {ex.Message}");
                    }
                });
            }

            LaunchLog.Write("Window shown; notification host + adhkar reminders started");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OnLaunched FAILED: {ex}");
            try
            {
                _window = new Window { Title = "SAMT — launch error" };
                MainWindow = _window;
                _window.Content = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = "SAMT failed to start.\nSee %LocalAppData%\\SAMT\\launch.log\n\n" + ex.Message,
                    Margin = new Thickness(24),
                    TextWrapping = TextWrapping.Wrap
                };
                WindowActivation.ShowCentered(_window, 640, 360);
            }
            catch (Exception ex2)
            {
                LaunchLog.Write($"Fallback window failed: {ex2}");
            }
        }
    }

    public static void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        // Dock right + vertical center, compact size, nav collapsed (same as first launch).
        WindowActivation.ShowDockedRight(MainWindow);
        if (MainWindow is Samt_App.MainWindow main)
        {
            main.CollapseNavigationPane();
        }
    }

    public static void RefreshTrayMenuLabels()
    {
        if (Current is not App { _tray: { } tray })
        {
            return;
        }

        try
        {
            tray.UpdateMenuLabels(
                Localization.Get("TrayOpen"),
                Localization.Get("TrayExit"));
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"RefreshTrayMenuLabels: {ex.Message}");
        }
    }

    private void HideMainWindowToTray()
    {
        try
        {
            if (_window is null)
            {
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            appWindow.Hide();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"HideMainWindowToTray failed: {ex.Message}");
        }
    }

    /// <summary>Design-lab / debug: show a sample prayer notification.</summary>
    public static void ShowSamplePrayerToast(string title, string body)
    {
        try
        {
            // Prefer tray balloon (works unpackaged); toast service may not be registered.
            var app = Current as App;
            app?._tray?.ShowBalloon(title, body);
            app?._toasts?.Show(
                new PlannedNotification
                {
                    Id = "sample-" + Guid.NewGuid().ToString("N")[..8],
                    FireAt = DateTimeOffset.Now,
                    Kind = PlannedNotificationKind.PrayerStart,
                    Prayer = PrayerEvent.Fajr,
                    Channels = NotificationChannel.WindowsToast
                },
                title,
                title);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ShowSamplePrayerToast failed: {ex.Message}");
        }
    }

    /// <summary>Design-lab: preview production overlay + optional adhan channels with lab tunables.</summary>
    public static void PreviewPrayerChannels(
        bool prayerStart,
        double opacity = 0.94,
        int animationMs = 320,
        string? edgeTag = null,
        string? variantTag = null)
    {
        try
        {
            var app = Current as App;
            var edge = edgeTag?.ToLowerInvariant() switch
            {
                "top" => OverlayEdge.Top,
                "bottom" => OverlayEdge.Bottom,
                "start" or "left" => OverlayEdge.Left,
                "end" or "right" => OverlayEdge.Right,
                _ => prayerStart ? OverlayEdge.Bottom : OverlayEdge.Top
            };

            OverlayVisualStyle? style = variantTag?.ToUpperInvariant() switch
            {
                "A" => OverlayVisualStyle.TopRibbon,
                "B" => OverlayVisualStyle.BottomDock,
                "C" => OverlayVisualStyle.EdgeDock,
                _ => prayerStart ? OverlayVisualStyle.BottomDock : OverlayVisualStyle.TopRibbon
            };

            // Variant C defaults to side entry unless the edge box already chose something else.
            if (string.Equals(variantTag, "C", StringComparison.OrdinalIgnoreCase)
                && (edgeTag is null || edgeTag is "Top"))
            {
                edge = OverlayEdge.Left;
            }

            app?._notificationHost?.PreviewNow(
                prayerStart ? PlannedNotificationKind.PrayerStart : PlannedNotificationKind.BeforePrayer,
                opacity: Math.Clamp(opacity, 0.30, 1.0),
                animationMs: Math.Clamp(animationMs, 80, 1200),
                entryEdge: edge,
                style: style);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"PreviewPrayerChannels failed: {ex.Message}");
        }
    }

    /// <summary>Full quit from tray menu, Exit button, or other UI — not close-to-tray.</summary>
    public static void RequestExit()
    {
        if (Current is App app)
        {
            app.RequestExitCore("ui");
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private void RequestExitCore(string source)
    {
        if (_exitRequested || IsExitRequested)
        {
            return;
        }

        _exitRequested = true;
        IsExitRequested = true;
        LaunchLog.Write($"Exit requested from {source}");

        try
        {
            _notificationHost?.Dispose();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose host: {ex.Message}");
        }

        try
        {
            _overlay?.Dispose();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose overlay: {ex.Message}");
        }

        try
        {
            _audio?.Dispose();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose audio: {ex.Message}");
        }

        try
        {
            _toasts?.Unregister();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose toasts: {ex.Message}");
        }

        try
        {
            _tray?.Dispose();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose tray: {ex.Message}");
        }

        try
        {
            _singleInstance?.Dispose();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Exit dispose single-instance: {ex.Message}");
        }

        try
        {
            _window?.Close();
        }
        catch
        {
            // ignore
        }

        Environment.Exit(0);
    }

    private void WireCloseToTray(Window window)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            appWindow.Closing += (_, e) =>
            {
                if (_exitRequested || IsExitRequested)
                {
                    return;
                }

                e.Cancel = true;
                appWindow.Hide();
                LaunchLog.Write("Window hidden to tray");
            };
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Close-to-tray wire failed: {ex.Message}");
        }
    }

    private static void ApplyThemeFromSettings()
    {
        if (MainWindow is null)
        {
            return;
        }

        Theme.ApplyPackage(MainWindow, State.Settings.Theme);
    }
}
