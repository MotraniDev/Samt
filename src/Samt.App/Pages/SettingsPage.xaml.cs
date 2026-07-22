using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Storage;
using Samt.Core.Time;
using Samt_App.Helpers;
using Samt_App.Services;
using Windows.System;

namespace Samt_App.Pages;

public sealed partial class SettingsPage : Page
{
    /// <summary>
    /// True until the first <see cref="SyncControlsFromSettings"/> finishes.
    /// Prevents Slider/Toggle default values from writing opacity/settings on page construct.
    /// </summary>
    private bool _suppress = true;
    private const string GitHubUrl = "https://github.com/MotraniDev/Samt";

    public SettingsPage()
    {
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLanguageUi();
        if (App.GoogleCalendar is not null)
        {
            App.GoogleCalendar.StatusChanged += (_, _) =>
            {
                DispatcherQueue.TryEnqueue(RefreshGoogleLinkStatusUi);
            };
        }

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
        AppearanceSectionHeader.Text = loc.Get("SettingsAppearanceSection");
        AppearanceSectionHint.Text = loc.Get("SettingsAppearanceHint");
        CalendarSectionHeader.Text = loc.Get("SettingsCalendarSection");
        CalendarDisclaimerText.Text = loc.Get("CalendarDisclaimer");
        HijriOffsetLabel.Text = loc.Get("HijriDayOffset");
        HijriOffsetHint.Text = loc.Get("HijriDayOffsetHint");
        CalendarCountryLabel.Text = loc.Get("SettingsCalendarCountry");
        CalendarCountryDefaultItem.Content = loc.Get("SettingsCalendarCountryDefault");
        CalendarCountryDzItem.Content = loc.Get("SettingsCalendarCountryAlgeria");
        SpecialDayMasterToggle.Header = loc.Get("SpecialDayRemindersMaster");
        SpecialDayMasterToggle.OffContent = loc.Get("ToggleOff");
        SpecialDayMasterToggle.OnContent = loc.Get("ToggleOn");
        SpecialDayIslamicToggle.Header = loc.Get("SpecialDayIslamicSet");
        SpecialDayIslamicToggle.OffContent = loc.Get("ToggleOff");
        SpecialDayIslamicToggle.OnContent = loc.Get("ToggleOn");
        SpecialDayCountryToggle.Header = loc.Get("SpecialDayCountrySet");
        SpecialDayCountryToggle.OffContent = loc.Get("ToggleOff");
        SpecialDayCountryToggle.OnContent = loc.Get("ToggleOn");
        SpecialDayTimeLabel.Text = loc.Get("SpecialDayReminderTime");
        CalendarDeliveryLabel.Text = loc.Get("CalendarReminderDelivery");
        DeliveryToastCheck.Content = loc.Get("CalendarDeliveryToast");
        DeliverySoundCheck.Content = loc.Get("CalendarDeliverySound");
        DeliverySilentWindowCheck.Content = loc.Get("CalendarDeliverySilentWindow");
        CalendarDeliveryHint.Text = loc.Get("CalendarDeliveryHint");
        GoogleLinkHeader.Text = loc.Get("GoogleCalendarLinkHeader");
        GoogleLinkHint.Text = loc.Get("GoogleCalendarLinkHint");
        GoogleConnectButton.Content = loc.Get("GoogleCalendarConnect");
        GoogleSyncNowButton.Content = loc.Get("GoogleCalendarSyncNow");
        GoogleDisconnectButton.Content = loc.Get("GoogleCalendarDisconnect");
        GoogleDisconnectDeleteButton.Content = loc.Get("GoogleCalendarDisconnectDelete");
        RefreshGoogleLinkStatusUi();
        AdhkarSectionHeader.Text = loc.Get("SettingsAdhkarSection");
        AdhkarSectionHint.Text = loc.Get("AdhkarSettingsHint");
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
        AdhkarAutoAdvanceToggle.Header = loc.Get("AdhkarAutoAdvance");
        AdhkarAutoAdvanceToggle.OffContent = loc.Get("ToggleOff");
        AdhkarAutoAdvanceToggle.OnContent = loc.Get("ToggleOn");
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
        AdhkarMorningTimeLabel.Text = loc.Get("AdhkarDefaultTime");
        AdhkarEveningTimeLabel.Text = loc.Get("AdhkarDefaultTime");
        AdhkarSleepTimeLabel.Text = loc.Get("AdhkarDefaultTime");
        AdhkarAfterDelayLabel.Text = loc.Get("AdhkarAfterPrayerDelay");
        AdhkarAfterPrayerHint.Text = loc.Get("AdhkarAfterPrayerHint");
        AdhkarAfterDelayUnit.Text = loc.Get("AdhkarMinutesUnit");
        CheckUpdatesButton.Content = loc.Get("CheckForUpdates");
        RefreshUpdateVersionText();
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
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(WindowOpacitySlider, loc.Get("SettingsWindowOpacity"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AdhkarAutoAdvanceToggle, loc.Get("AdhkarAutoAdvance"));
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
            AdhkarAutoAdvanceToggle.IsOn = settings.AdhkarAutoAdvanceEnabled;
            AdhkarMorningToggle.IsOn = settings.AdhkarMorningEnabled;
            AdhkarEveningToggle.IsOn = settings.AdhkarEveningEnabled;
            AdhkarAfterToggle.IsOn = settings.AdhkarAfterPrayerEnabled;
            AdhkarSleepToggle.IsOn = settings.AdhkarSleepEnabled;
            AdhkarMorningTimeBox.Text = LatinDigits.EnsureLatin(settings.AdhkarMorningTime);
            AdhkarEveningTimeBox.Text = LatinDigits.EnsureLatin(settings.AdhkarEveningTime);
            AdhkarSleepTimeBox.Text = LatinDigits.EnsureLatin(settings.AdhkarSleepTime);
            AdhkarAfterDelayBox.Text = LatinDigits.Number(settings.AdhkarAfterPrayerDelayMinutes);
            var opacityPct = (int)Math.Round(WindowChromeOpacity.Clamp(settings.WindowOpacity) * 100);
            WindowOpacitySlider.Value = opacityPct;
            WindowOpacityValueText.Text = LatinDigits.Number(opacityPct) + "%";
            HijriOffsetBox.Value = HijriConverter.ClampDayOffset(settings.HijriDayOffset);
            SyncCalendarCountryBox(settings.CalendarCountryOverride);
            SpecialDayMasterToggle.IsOn = settings.SpecialDayRemindersEnabled;
            SpecialDayIslamicToggle.IsOn = settings.SpecialDayIslamicSetEnabled;
            SpecialDayCountryToggle.IsOn = settings.SpecialDayCountrySetEnabled;
            SpecialDayTimeBox.Text = LatinDigits.EnsureLatin(settings.SpecialDayReminderTime);
            var delivery = settings.CalendarReminderDelivery;
            DeliveryToastCheck.IsChecked = delivery.HasFlag(CalendarReminderDelivery.WindowsNotification);
            DeliverySoundCheck.IsChecked = delivery.HasFlag(CalendarReminderDelivery.Sound);
            DeliverySilentWindowCheck.IsChecked = delivery.HasFlag(CalendarReminderDelivery.SilentWindow);
            RefreshUpdateVersionText();
            if (string.IsNullOrWhiteSpace(UpdateStatusText.Text))
            {
                UpdateStatusText.Text = App.Localization.Get("UpdateStatusIdle");
                UpdateStatusText.Opacity = 0.65;
                UpdateStatusText.ClearValue(TextBlock.ForegroundProperty);
            }

            RefreshGoogleLinkStatusUi();
        }
        finally
        {
            _suppress = false;
        }
    }

    private void RefreshUpdateVersionText()
    {
        var current = UpdateService.GetCurrentVersion();
        UpdateVersionText.Text = LatinDigits.EnsureLatin(
            string.Format(
                CultureInfo.InvariantCulture,
                App.Localization.Get("UpdateCurrentVersionFormat"),
                $"{current.Major}.{current.Minor}.{Math.Max(current.Build, 0)}"));
    }

    private void SetUpdateBusy(bool busy, string? phase = null)
    {
        UpdateBusyPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateBusyRing.IsActive = busy;
        CheckUpdatesButton.IsEnabled = !busy;
        AutoUpdateToggle.IsEnabled = !busy;
        if (!busy)
        {
            UpdateBusyText.Text = "";
            return;
        }

        UpdateBusyText.Text = phase switch
        {
            "download" => App.Localization.Get("UpdateDownloading"),
            _ => App.Localization.Get("UpdateChecking")
        };
    }

    private void ApplyUpdateStatus(string message, bool success)
    {
        UpdateStatusText.Text = LatinDigits.EnsureLatin(message);
        UpdateStatusText.Opacity = 0.95;
        if (success)
        {
            UpdateStatusText.ClearValue(TextBlock.ForegroundProperty);
        }
        else if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var brushObj)
                 && brushObj is Microsoft.UI.Xaml.Media.Brush brush)
        {
            UpdateStatusText.Foreground = brush;
        }
    }

    private void RefreshGoogleLinkStatusUi()
    {
        var loc = App.Localization;
        var link = App.State.Settings.GoogleCalendarLink;
        var linked = link.IsLinked;
        var busy = App.GoogleCalendar?.IsBusy == true;
        var phase = App.GoogleCalendar?.BusyPhase ?? "";

        GoogleBusyPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        GoogleBusyRing.IsActive = busy;
        if (busy)
        {
            GoogleBusyText.Text = phase switch
            {
                "connect" => loc.Get("GoogleCalendarBusyConnect"),
                "disconnect" => loc.Get("GoogleCalendarBusyDisconnect"),
                _ => loc.Get("GoogleCalendarBusySync")
            };
        }
        else
        {
            GoogleBusyText.Text = "";
        }

        GoogleConnectButton.IsEnabled = !busy && !linked && App.GoogleCalendar is not null;
        GoogleSyncNowButton.IsEnabled = !busy && linked && App.GoogleCalendar is not null;
        GoogleDisconnectButton.IsEnabled = !busy && linked && App.GoogleCalendar is not null;
        GoogleDisconnectDeleteButton.IsEnabled = !busy && linked && App.GoogleCalendar is not null;

        if (!linked)
        {
            var configured = GoogleOAuthClientConfig.TryLoad(out _, out _, out _);
            if (!configured)
            {
                GoogleLinkStatusText.Text = loc.Get("GoogleCalendarStatusNeedClient");
                return;
            }

            var fail = link.LastSyncMessage?.Trim() ?? "";
            if (string.Equals(fail, "insufficient-scopes", StringComparison.OrdinalIgnoreCase))
            {
                GoogleLinkStatusText.Text = loc.Get("GoogleCalendarStatusInsufficientScopes");
                return;
            }

            if (fail.StartsWith("connect-failed:", StringComparison.OrdinalIgnoreCase))
            {
                var failDetail = fail["connect-failed:".Length..].Trim();
                GoogleLinkStatusText.Text = string.IsNullOrWhiteSpace(failDetail)
                    ? loc.Get("GoogleCalendarStatusConnectFailed")
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        loc.Get("GoogleCalendarStatusConnectFailedDetail"),
                        LatinDigits.EnsureLatin(failDetail));
                return;
            }

            GoogleLinkStatusText.Text = busy
                ? loc.Get("GoogleCalendarBusyConnect")
                : loc.Get("GoogleCalendarStatusNotLinked");
            return;
        }

        var email = string.IsNullOrWhiteSpace(link.AccountEmail)
            ? loc.Get("GoogleCalendarAccountUnknown")
            : link.AccountEmail;
        var when = link.LastSyncUtc is { } t
            ? LatinDigits.EnsureLatin(t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            : "—";
        var outcome = busy
            ? loc.Get("GoogleCalendarLastInProgress")
            : link.LastSyncSucceeded
                ? loc.Get("GoogleCalendarLastOk")
                : loc.Get("GoogleCalendarLastFail");
        var detail = string.IsNullOrWhiteSpace(link.LastSyncMessage)
            ? ""
            : " · " + LatinDigits.EnsureLatin(link.LastSyncMessage);
        if (!busy && link.LastSkippedCount > 0)
        {
            detail += " · " + string.Format(
                CultureInfo.InvariantCulture,
                loc.Get("GoogleCalendarSkippedFormat"),
                LatinDigits.Number(link.LastSkippedCount));
        }

        GoogleLinkStatusText.Text = string.Format(
            CultureInfo.InvariantCulture,
            loc.Get("GoogleCalendarStatusLinkedFormat"),
            email,
            when,
            outcome) + (busy ? "" : detail);
    }

    private async void GoogleConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.GoogleCalendar is null)
        {
            return;
        }

        GoogleConnectButton.IsEnabled = false;
        try
        {
            await App.GoogleCalendar.ConnectAsync();
        }
        finally
        {
            RefreshGoogleLinkStatusUi();
        }
    }

    private async void GoogleSyncNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.GoogleCalendar is null)
        {
            return;
        }

        GoogleSyncNowButton.IsEnabled = false;
        try
        {
            await App.GoogleCalendar.SyncNowAsync();
        }
        finally
        {
            RefreshGoogleLinkStatusUi();
        }
    }

    private async void GoogleDisconnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.GoogleCalendar is null)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = App.Localization.Get("GoogleCalendarDisconnect"),
            Content = App.Localization.Get("GoogleCalendarDisconnectConfirm"),
            PrimaryButtonText = App.Localization.Get("GoogleCalendarDisconnect"),
            CloseButtonText = App.Localization.Get("CalendarClose"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await App.GoogleCalendar.DisconnectAsync(deleteCloudCalendar: false);
        RefreshGoogleLinkStatusUi();
    }

    private async void GoogleDisconnectDeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.GoogleCalendar is null)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = App.Localization.Get("GoogleCalendarDisconnectDelete"),
            Content = App.Localization.Get("GoogleCalendarDisconnectDeleteConfirm"),
            PrimaryButtonText = App.Localization.Get("GoogleCalendarDisconnectDelete"),
            CloseButtonText = App.Localization.Get("CalendarClose"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await App.GoogleCalendar.DisconnectAsync(deleteCloudCalendar: true);
        RefreshGoogleLinkStatusUi();
    }

    private void SyncCalendarCountryBox(string? countryOverride)
    {
        var tag = string.IsNullOrWhiteSpace(countryOverride)
            ? "default"
            : countryOverride.Trim().ToUpperInvariant();

        foreach (var item in CalendarCountryBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                CalendarCountryBox.SelectedItem = item;
                return;
            }
        }

        CalendarCountryBox.SelectedItem = CalendarCountryDefaultItem;
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

    private async void HijriOffsetBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress || !IsLoaded || double.IsNaN(args.NewValue))
        {
            return;
        }

        var offset = HijriConverter.ClampDayOffset((int)Math.Round(args.NewValue));
        if (offset == App.State.Settings.HijriDayOffset)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(hijriDayOffset: offset));
    }

    private async void CalendarCountryBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || CalendarCountryBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        if (string.Equals(tag, "default", StringComparison.OrdinalIgnoreCase))
        {
            await App.State.UpdateAsync(s => s.With(
                calendarCountryOverride: null,
                replaceCalendarCountryOverride: true));
            return;
        }

        await App.State.UpdateAsync(s => s.With(calendarCountryOverride: tag));
    }

    private async void SpecialDayMasterToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(specialDayRemindersEnabled: SpecialDayMasterToggle.IsOn));
    }

    private async void SpecialDaySetToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(
            specialDayIslamicSetEnabled: SpecialDayIslamicToggle.IsOn,
            specialDayCountrySetEnabled: SpecialDayCountryToggle.IsOn));
    }

    private async void SpecialDayTimeBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await SaveSpecialDayTimeAsync();
    }

    private async void SpecialDayTimeBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _suppress || !IsLoaded)
        {
            return;
        }

        e.Handled = true;
        await SaveSpecialDayTimeAsync();
    }

    private async Task SaveSpecialDayTimeAsync()
    {
        var time = SettingsJson.NormalizeClockTime(SpecialDayTimeBox.Text, "09:00");
        _suppress = true;
        try
        {
            SpecialDayTimeBox.Text = LatinDigits.EnsureLatin(time);
        }
        finally
        {
            _suppress = false;
        }

        await App.State.UpdateAsync(s => s.With(specialDayReminderTime: time));
    }

    private async void CalendarDelivery_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        var delivery = CalendarReminderDelivery.None;
        if (DeliveryToastCheck.IsChecked == true)
        {
            delivery |= CalendarReminderDelivery.WindowsNotification;
        }

        if (DeliverySoundCheck.IsChecked == true)
        {
            delivery |= CalendarReminderDelivery.Sound;
        }

        if (DeliverySilentWindowCheck.IsChecked == true)
        {
            delivery |= CalendarReminderDelivery.SilentWindow;
        }

        if (delivery == CalendarReminderDelivery.None)
        {
            // Keep at least one channel so reminders are not silently dropped.
            delivery = CalendarReminderDelivery.WindowsNotification;
            _suppress = true;
            try
            {
                DeliveryToastCheck.IsChecked = true;
            }
            finally
            {
                _suppress = false;
            }
        }

        await App.State.UpdateAsync(s => s.With(calendarReminderDelivery: delivery));
    }

    private async void AdhkarMasterToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(adhkarRemindersEnabled: AdhkarMasterToggle.IsOn));
    }

    private async void AdhkarAutoAdvanceToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(adhkarAutoAdvanceEnabled: AdhkarAutoAdvanceToggle.IsOn));
    }

    private async void WindowOpacitySlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Never touch shell opacity until the page is live and controls are synced.
        // (Slider default/min fires ValueChanged during InitializeComponent otherwise.)
        if (_suppress || !IsLoaded)
        {
            return;
        }

        var pct = (int)Math.Round(Math.Clamp(e.NewValue, 30, 100));
        if (WindowOpacityValueText is not null)
        {
            WindowOpacityValueText.Text = LatinDigits.Number(pct) + "%";
        }

        var opacity = WindowChromeOpacity.Clamp(pct / 100.0);

        // Live preview on open shell windows.
        if (App.MainWindow is not null)
        {
            WindowChromeOpacity.Apply(App.MainWindow, opacity);
        }

        App.AdhkarReminders?.ApplyReaderOpacity(opacity);

        await App.State.UpdateAsync(s => s.With(windowOpacity: opacity));
    }

    private async void AdhkarCollectionToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await SaveAdhkarSettingsAsync();
    }

    private async void AdhkarTimeBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        await SaveAdhkarSettingsAsync();
    }

    private async void AdhkarTimeBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _suppress || !IsLoaded)
        {
            return;
        }

        e.Handled = true;
        await SaveAdhkarSettingsAsync();
    }

    private async Task SaveAdhkarSettingsAsync()
    {
        var morning = SettingsJson.NormalizeClockTime(AdhkarMorningTimeBox.Text, "06:00");
        var evening = SettingsJson.NormalizeClockTime(AdhkarEveningTimeBox.Text, "17:00");
        var sleep = SettingsJson.NormalizeClockTime(AdhkarSleepTimeBox.Text, "22:00");
        var delay = ParseDelayMinutes(AdhkarAfterDelayBox.Text);

        _suppress = true;
        try
        {
            AdhkarMorningTimeBox.Text = LatinDigits.EnsureLatin(morning);
            AdhkarEveningTimeBox.Text = LatinDigits.EnsureLatin(evening);
            AdhkarSleepTimeBox.Text = LatinDigits.EnsureLatin(sleep);
            AdhkarAfterDelayBox.Text = LatinDigits.Number(delay);
        }
        finally
        {
            _suppress = false;
        }

        await App.State.UpdateAsync(s => s.With(
            adhkarMorningEnabled: AdhkarMorningToggle.IsOn,
            adhkarEveningEnabled: AdhkarEveningToggle.IsOn,
            adhkarAfterPrayerEnabled: AdhkarAfterToggle.IsOn,
            adhkarSleepEnabled: AdhkarSleepToggle.IsOn,
            adhkarMorningTime: morning,
            adhkarEveningTime: evening,
            adhkarSleepTime: sleep,
            adhkarAfterPrayerDelayMinutes: delay));
    }

    private static int ParseDelayMinutes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var raw = LatinDigits.EnsureLatin(text.Trim());
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return Math.Clamp(n, 0, 180);
        }

        return 0;
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var loc = App.Localization;
        SetUpdateBusy(true, "check");
        ApplyUpdateStatus(loc.Get("UpdateChecking"), success: true);
        try
        {
            var result = await App.Updates.CheckAsync(force: true);
            ApplyUpdateStatus(result.UserMessage, result.Success);
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

            SetUpdateBusy(true, "download");
            ApplyUpdateStatus(loc.Get("UpdateDownloading"), success: true);
            var install = await App.Updates.DownloadAndLaunchAsync(result.Manifest);
            if (install.Success)
            {
                ApplyUpdateStatus(loc.Get("UpdateInstallStarted"), success: true);
            }
            else
            {
                ApplyUpdateStatus(
                    install.Error ?? loc.Get("UpdateCheckFailed"),
                    success: false);
            }
        }
        catch (Exception ex)
        {
            ApplyUpdateStatus(
                string.Format(
                    CultureInfo.InvariantCulture,
                    loc.Get("UpdateCheckFailedDetail"),
                    LatinDigits.EnsureLatin(ex.Message)),
                success: false);
        }
        finally
        {
            SetUpdateBusy(false);
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
