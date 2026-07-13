using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Samt_App.Services;

namespace Samt_App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _suppress;
    private const string GitHubUrl = "https://github.com/MotraniDev/Samt";

    public SettingsPage()
    {
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLanguageUi();
        Loaded += (_, _) =>
        {
            ApplyLanguageUi();
            SyncControlsFromSettings();
            LoadPublisherLogo();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyLanguageUi();
        SyncControlsFromSettings();
        LoadPublisherLogo();
    }

    private void ApplyLanguageUi()
    {
        var loc = App.Localization;
        FlowDirection = loc.FlowDirection;
        TitleText.Text = loc.Get("NavSettings");
        LanguageSectionHeader.Text = loc.Get("SettingsLanguageSection");
        ThemeSectionHeader.Text = loc.Get("SettingsThemeSection");
        AppOptionsHeader.Text = loc.Get("AppOptions");
        UpdatesSectionHeader.Text = loc.Get("SettingsUpdatesSection");
        AdhkarSectionHeader.Text = loc.Get("SettingsAdhkarSection");
        AboutSectionHeader.Text = loc.Get("SettingsAboutSection");
        CommunitySectionHeader.Text = loc.Get("SettingsCommunitySection");
        AutoStartToggle.Header = loc.Get("AutoStartEnabled");
        AutoStartToggle.OffContent = loc.Get("ToggleOff");
        AutoStartToggle.OnContent = loc.Get("ToggleOn");
        MissedResumeToggle.Header = loc.Get("ShowMissedAlertOnResume");
        MissedResumeToggle.OffContent = loc.Get("ToggleOff");
        MissedResumeToggle.OnContent = loc.Get("ToggleOn");
        AutoUpdateToggle.Header = loc.Get("AutoCheckUpdates");
        AutoUpdateToggle.OffContent = loc.Get("ToggleOff");
        AutoUpdateToggle.OnContent = loc.Get("ToggleOn");
        AdhkarMasterToggle.Header = loc.Get("AdhkarRemindersMaster");
        AdhkarMasterToggle.OffContent = loc.Get("ToggleOff");
        AdhkarMasterToggle.OnContent = loc.Get("ToggleOn");
        AdhkarMorningToggle.Header = loc.Get("AdhkarMorningToggle");
        AdhkarMorningToggle.OffContent = loc.Get("ToggleOff");
        AdhkarMorningToggle.OnContent = loc.Get("ToggleOn");
        AdhkarEveningToggle.Header = loc.Get("AdhkarEveningToggle");
        AdhkarEveningToggle.OffContent = loc.Get("ToggleOff");
        AdhkarEveningToggle.OnContent = loc.Get("ToggleOn");
        AdhkarAfterToggle.Header = loc.Get("AdhkarAfterPrayerToggle");
        AdhkarAfterToggle.OffContent = loc.Get("ToggleOff");
        AdhkarAfterToggle.OnContent = loc.Get("ToggleOn");
        AdhkarSleepToggle.Header = loc.Get("AdhkarSleepToggle");
        AdhkarSleepToggle.OffContent = loc.Get("ToggleOff");
        AdhkarSleepToggle.OnContent = loc.Get("ToggleOn");
        CheckUpdatesButton.Content = loc.Get("CheckForUpdates");
        GitHubButtonLabel.Text = loc.Get("OpenGitHub");
        PublisherText.Text = $"{loc.Get("PublisherLabel")}: MotraniSoft";
        VersionText.Text = $"{loc.Get("VersionLabel")}: {GetAppVersion()}";

        ThemeSystemItem.Content = loc.Get("ThemeSystem");
        ThemeLightItem.Content = loc.Get("ThemeLight");
        ThemeDarkItem.Content = loc.Get("ThemeDark");
        ThemeRamadanItem.Content = loc.Get("ThemeRamadan");
        ThemeAlgeriaItem.Content = loc.Get("ThemeAlgeria");
        ThemeMoroccoItem.Content = loc.Get("ThemeMorocco");

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LanguageBox, loc.Get("Language"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ThemeBox, loc.Get("Theme"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CheckUpdatesButton, loc.Get("CheckForUpdates"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(GitHubButton, loc.Get("OpenGitHub"));
    }

    private void SyncControlsFromSettings()
    {
        _suppress = true;
        try
        {
            var settings = App.State.Settings;
            SyncLanguageBox(settings.Language);
            SyncThemeBox(settings.Theme);
            AutoStartToggle.IsOn = settings.AutoStartEnabled;
            MissedResumeToggle.IsOn = settings.ShowMissedAlertOnResume;
            AutoUpdateToggle.IsOn = settings.AutoCheckUpdates;
            AdhkarMasterToggle.IsOn = settings.AdhkarRemindersEnabled;
            AdhkarMorningToggle.IsOn = settings.AdhkarMorningEnabled;
            AdhkarEveningToggle.IsOn = settings.AdhkarEveningEnabled;
            AdhkarAfterToggle.IsOn = settings.AdhkarAfterPrayerEnabled;
            AdhkarSleepToggle.IsOn = settings.AdhkarSleepEnabled;
            UpdateStatusText.Text = string.Empty;
        }
        finally
        {
            _suppress = false;
        }
    }

    private void SyncLanguageBox(string language)
    {
        var tag = LocalizationService.Normalize(language);
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                return;
            }
        }
    }

    private void SyncThemeBox(string? theme)
    {
        var tag = ThemeService.NormalizePackageId(theme);
        foreach (var item in ThemeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                ThemeBox.SelectedItem = item;
                return;
            }
        }
    }

    private async void LanguageBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var language = LocalizationService.Normalize(tag);
        if (string.Equals(App.State.Settings.Language, language, StringComparison.OrdinalIgnoreCase)
            && string.Equals(App.Localization.CurrentLanguage, language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        App.Localization.SetLanguage(language);
        await App.State.UpdateAsync(s => s.With(language: language));
        ApplyLanguageUi();
    }

    private async void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var packageId = ThemeService.NormalizePackageId(tag);
        if (string.Equals(App.State.Settings.Theme, packageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(App.Theme.CurrentPackageId, packageId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (App.MainWindow is not null)
        {
            App.Theme.ApplyPackage(App.MainWindow, packageId);
        }

        await App.State.UpdateAsync(s => s.With(theme: packageId));
        LoadPublisherLogo();
    }

    private async void AutoStartToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        var enabled = AutoStartToggle.IsOn;
        await App.State.UpdateAsync(s => s.With(autoStartEnabled: enabled));
        try
        {
            AutoStartService.Apply(enabled);
        }
        catch
        {
            // non-fatal
        }
    }

    private async void MissedResumeToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(showMissedAlertOnResume: MissedResumeToggle.IsOn));
    }

    private async void AutoUpdateToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(autoCheckUpdates: AutoUpdateToggle.IsOn));
    }

    private async void AdhkarMasterToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(adhkarRemindersEnabled: AdhkarMasterToggle.IsOn));
    }

    private async void AdhkarCollectionToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(
            adhkarMorningEnabled: AdhkarMorningToggle.IsOn,
            adhkarEveningEnabled: AdhkarEveningToggle.IsOn,
            adhkarAfterPrayerEnabled: AdhkarAfterToggle.IsOn,
            adhkarSleepEnabled: AdhkarSleepToggle.IsOn));
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var loc = App.Localization;
        UpdateStatusText.Text = "…";
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            var result = await App.Updates.CheckAsync(force: true);
            UpdateStatusText.Text = result.UserMessage;
            if (!result.Success || !result.UpdateAvailable || result.Manifest is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = loc.Get("SettingsUpdatesSection"),
                Content = result.UserMessage + Environment.NewLine + loc.Get("UpdateDownloadPrompt"),
                PrimaryButtonText = loc.Get("ToggleOn"),
                CloseButtonText = loc.Get("ToggleOff"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            var choice = await dialog.ShowAsync();
            if (choice != ContentDialogResult.Primary)
            {
                return;
            }

            UpdateStatusText.Text = loc.Get("UpdateDownloading");
            var install = await App.Updates.DownloadAndLaunchAsync(result.Manifest);
            UpdateStatusText.Text = install.Success
                ? loc.Get("UpdateDownloading")
                : (install.Error ?? loc.Get("UpdateCheckFailed"));
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = ex.Message;
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch
        {
            // non-fatal
        }
    }

    private void LoadPublisherLogo()
    {
        try
        {
            var useDarkLogo = App.Theme.IsEffectivelyDark;
            var path = useDarkLogo
                ? "ms-appx:///Assets/Publisher/motrani-logo-dark.png"
                : "ms-appx:///Assets/Publisher/motrani-logo-light.jpg";
            PublisherLogo.Source = new BitmapImage(new Uri(path));
        }
        catch
        {
            PublisherLogo.Source = null;
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "—" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            return "—";
        }
    }
}
