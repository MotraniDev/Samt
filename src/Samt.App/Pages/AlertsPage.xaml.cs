using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Domain;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class AlertsPage : Page
{
    public AlertsPage()
    {
        ViewModel = new AlertsViewModel(App.State, App.Localization);
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLabels();
        Loaded += (_, _) => ApplyLabels();
    }

    public AlertsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.LoadFromSettings();
        ApplyLabels();
        Bindings.Update();
    }

    private void ApplyLabels()
    {
        var loc = App.Localization;
        FlowDirection = loc.FlowDirection;
        TitleText.Text = loc.Get("NavAlerts");
        ViewModel.RefreshLabels();

        StartSection.Text = loc.Get("AlertsStartSection");
        StartEnabledCheck.Content = loc.Get("AlertsStartEnabled");
        StartChannelsLabel.Text = loc.Get("AlertsChannels");
        StartToastCheck.Content = loc.Get("ChannelToast");
        StartOverlayCheck.Content = loc.Get("ChannelOverlay");
        StartAudioCheck.Content = loc.Get("ChannelAudio");
        StartPrayersLabel.Text = loc.Get("AlertsStartPrayers");
        StartFajrCheck.Content = loc.GetPrayerName(PrayerEvent.Fajr);
        StartDhuhrCheck.Content = loc.GetPrayerName(PrayerEvent.Dhuhr);
        StartAsrCheck.Content = loc.GetPrayerName(PrayerEvent.Asr);
        StartMaghribCheck.Content = loc.GetPrayerName(PrayerEvent.Maghrib);
        StartIshaCheck.Content = loc.GetPrayerName(PrayerEvent.Isha);

        BeforeSection.Text = loc.Get("AlertsBeforeSection");
        BeforeEnabledCheck.Content = loc.Get("AlertsBeforeEnabled");
        GeneralOffsetLabel.Text = loc.Get("AlertsGeneralOffset");
        BeforeChannelsLabel.Text = loc.Get("AlertsChannels");
        BeforeToastCheck.Content = loc.Get("ChannelToast");
        BeforeOverlayCheck.Content = loc.Get("ChannelOverlay");
        ExceptionsLabel.Text = loc.Get("AlertsExceptions");
        ExceptionsHint.Text = loc.Get("AlertsExceptionsHint");
        BeforeFajrCheck.Content = loc.GetPrayerName(PrayerEvent.Fajr);
        BeforeDhuhrCheck.Content = loc.GetPrayerName(PrayerEvent.Dhuhr);
        BeforeAsrCheck.Content = loc.GetPrayerName(PrayerEvent.Asr);
        BeforeMaghribCheck.Content = loc.GetPrayerName(PrayerEvent.Maghrib);
        BeforeIshaCheck.Content = loc.GetPrayerName(PrayerEvent.Isha);

        SaveButton.Content = loc.Get("SaveAlerts");
        FridayHint.Text = loc.Get("AlertsFridayHint");
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveAsync();
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"Alerts save button failed: {ex}");
        }
    }
}
