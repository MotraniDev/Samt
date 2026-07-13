using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Domain;
using Samt_App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Samt_App.Pages;

public sealed partial class AlertsPage : Page
{
    public AlertsPage()
    {
        ViewModel = new AlertsViewModel(App.State, App.Localization);
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLabels();
        Loaded += (_, _) => ApplyLabels();
        Unloaded += (_, _) => ViewModel.StopPreview();
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
        BeforeAudioCheck.Content = loc.Get("ChannelAudioCue");
        BeforeAudioHint.Text = loc.Get("BeforeAudioHint");
        ExceptionsLabel.Text = loc.Get("AlertsExceptions");
        ExceptionsHint.Text = loc.Get("AlertsExceptionsHint");
        BeforeFajrCheck.Content = loc.GetPrayerName(PrayerEvent.Fajr);
        BeforeDhuhrCheck.Content = loc.GetPrayerName(PrayerEvent.Dhuhr);
        BeforeAsrCheck.Content = loc.GetPrayerName(PrayerEvent.Asr);
        BeforeMaghribCheck.Content = loc.GetPrayerName(PrayerEvent.Maghrib);
        BeforeIshaCheck.Content = loc.GetPrayerName(PrayerEvent.Isha);

        SoundSection.Text = loc.Get("SoundLibrary");
        SoundHint.Text = loc.Get("SoundLibraryHint");
        AdhanSoundLabel.Text = loc.Get("AdhanSound");
        PreAlertSoundLabel.Text = loc.Get("PreAlertSound");
        PreviewAdhanButton.Content = loc.Get("PreviewSound");
        PreviewPreAlertButton.Content = loc.Get("PreviewSound");
        AddSoundButton.Content = loc.Get("AddSound");
        StopSoundButton.Content = loc.Get("StopSound");
        SoundLibraryNote.Text = loc.Get("SoundLibraryNote");

        SaveButton.Content = loc.Get("SaveAlerts");
        FridayHint.Text = loc.Get("AlertsFridayHint");
        Bindings.Update();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.StopPreview();
            await ViewModel.SaveAsync();
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"Alerts save button failed: {ex}");
        }
    }

    private void PreviewAdhanButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.PreviewAdhan();

    private void PreviewPreAlertButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.PreviewPreAlert();

    private void StopSoundButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.StopPreview();

    private async void AddSoundButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wma");
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            await ViewModel.AddUserSoundAsync(file.Path);
            Bindings.Update();
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"Add sound picker failed: {ex}");
            ViewModel.StatusMessage = App.Localization.Get("SoundAddFailed") + " " + ex.Message;
        }
    }
}
