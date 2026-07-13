using Microsoft.UI.Xaml;
using Samt.Core.Storage;
using Samt_App.Services;

namespace Samt_App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Localization = new LocalizationService();
        Theme = new ThemeService();
    }

    public static LocalizationService Localization { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static AppState State { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SAMT");
        var store = new JsonSettingsStore(dataDir);
        State = new AppState(store);
        await State.LoadAsync();

        Localization.Initialize(State.Settings.Language);

        MainWindow = new MainWindow();
        ApplyThemeFromSettings();
        MainWindow.Activate();
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
