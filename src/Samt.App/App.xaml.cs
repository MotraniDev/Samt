using Microsoft.UI.Xaml;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt.Core.Storage;
using Samt_App.Helpers;
using Samt_App.Services;

namespace Samt_App;

public partial class App : Application
{
    private Window? _window;
    private NotificationHost? _notificationHost;
    private ToastNotificationService? _toasts;
    private TrayIconService? _tray;
    private SingleInstanceService? _singleInstance;
    private bool _exitRequested;

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
        LaunchLog.Write("App constructed");
    }

    public static LocalizationService Localization { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static AppState State { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }

    public static bool IsExitRequested { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LaunchLog.Write("OnLaunched begin");
        try
        {
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

            _tray = new TrayIconService();
            _tray.Initialize();
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.ExitRequested += (_, _) => RequestExit();

            _toasts = new ToastNotificationService();
            _toasts.Initialize(_tray);

            _window = new MainWindow();
            MainWindow = _window;
            ApplyThemeFromSettings();
            WireCloseToTray(_window);
            WindowActivation.ShowCentered(_window);

            _notificationHost = new NotificationHost(State, Localization, _toasts, _tray);
            _notificationHost.Start();

            LaunchLog.Write("Window shown; notification host started");
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

        WindowActivation.ShowCentered(MainWindow);
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

    private void RequestExit()
    {
        _exitRequested = true;
        IsExitRequested = true;
        LaunchLog.Write("Exit requested from tray");

        _notificationHost?.Dispose();
        _toasts?.Unregister();
        _tray?.Dispose();
        _singleInstance?.Dispose();

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

        var choice = State.Settings.Theme?.ToLowerInvariant() switch
        {
            "light" => AppThemeChoice.Light,
            "dark" => AppThemeChoice.Dark,
            _ => AppThemeChoice.System
        };
        Theme.Apply(MainWindow, choice);
    }
}
