using Microsoft.UI.Xaml;
using Samt.Core.Formatting;
using Samt.Core.Storage;
using Samt_App.Helpers;
using Samt_App.Services;

namespace Samt_App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        LatinDigits.ApplyProcessDefaults("ar");

        UnhandledException += (_, e) =>
        {
            LaunchLog.Write($"UnhandledException (Handled={e.Handled}): {e.Exception}");
            // Do not swallow launch-critical errors forever without a window.
            // Still mark handled so we can keep process only if a window exists.
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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LaunchLog.Write("OnLaunched begin");
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAMT");
            Directory.CreateDirectory(dataDir);

            try
            {
                var store = new JsonSettingsStore(dataDir);
                State = new AppState(store);
                await State.LoadAsync();
                LaunchLog.Write("Settings loaded");
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Settings load failed, using defaults: {ex.Message}");
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

            _window = new MainWindow();
            MainWindow = _window;
            ApplyThemeFromSettings();
            WindowActivation.ShowCentered(_window);
            LaunchLog.Write("Window shown");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OnLaunched FAILED: {ex}");
            // Last resort: try empty window so user sees something.
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
