using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Storage;
using Samt.Core.Time;
using Samt_App.Helpers;
using Samt_App.Services;
using Windows.Graphics;

namespace Samt_App.Overlay;

public sealed partial class SetupWizardWindow : Window
{
    private readonly WindowsGeolocationService _geo = new();
    private readonly NominatimPlaceSearchService _places = new();
    private readonly IPrayerEngine _engine = new PrayerEngine();
    private readonly IClock _clock = new SystemClock();
    private int _step;
    private bool _suppress;
    private bool _completed;

    public SetupWizardWindow()
    {
        InitializeComponent();
        CustomWindowChrome.StyleCaptionButton(CloseCaptionButton, isClose: true);
        CustomWindowChrome.Apply(
            this,
            TitleBarDrag,
            minimizeButton: null,
            maximizeButton: null,
            closeButton: null,
            new SizeInt32(560, 720),
            allowMaximize: false);

        WindowChromeOpacity.Apply(this, App.State?.Settings.WindowOpacity ?? 1.0);
        Closed += SetupWizardWindow_OnClosed;

        PopulateSeeds();
        PopulateMethods();
        LoadFromSettings();
        ApplyLanguage();
        ShowStep(0);
    }

    public event EventHandler? Completed;

    private void SetupWizardWindow_OnClosed(object sender, WindowEventArgs args)
    {
        // Treat close without Finish/Skip as Skip (smart defaults, mark completed).
        if (!_completed)
        {
            _ = CompleteAsync(applySkipDefaults: true);
        }
    }

    private void ApplyLanguage()
    {
        var loc = App.Localization;
        if (Content is FrameworkElement root)
        {
            root.FlowDirection = loc.FlowDirection;
        }

        TitleText.Text = loc.Get("WizardTitle");
        WelcomeText.Text = loc.Get("WizardWelcome");
        SkipButton.Content = loc.Get("WizardSkip");
        BackButton.Content = loc.Get("WizardBack");
        GpsButtonLabel.Text = loc.Get("WizardUseGps");
        PlaceSearchButton.Content = loc.Get("WizardSearchPlace");
        PlaceSearchBox.PlaceholderText = loc.Get("PlaceSearchHint");
        PlaceSearchAttribution.Text = loc.Get("PlaceSearchAttribution");
        LocationStatusLabel.Text = loc.Get("WizardLocationStatus");
        CalcMethodLabel.Text = loc.Get("WizardCalcMethod");
        MadhabLabel.Text = loc.Get("WizardAsrMadhab");
        MadhabStandardItem.Content = loc.Get("AsrStandard");
        MadhabHanafiItem.Content = loc.Get("AsrHanafi");
        AdhkarMasterToggle.Header = loc.Get("AdhkarRemindersMaster");
        MorningToggle.Header = loc.Get("AdhkarMorningToggle");
        EveningToggle.Header = loc.Get("AdhkarEveningToggle");
        AfterToggle.Header = loc.Get("AdhkarAfterPrayerToggle");
        SleepToggle.Header = loc.Get("AdhkarSleepToggle");
        TourTray.Text = loc.Get("WizardTourTray");
        TourToday.Text = loc.Get("WizardTourToday");
        TourAlerts.Text = loc.Get("WizardTourAlerts");
        TourAdhkar.Text = loc.Get("WizardTourAdhkar");
        UpdateStepChrome();
        RebuildStepDots();
        RefreshLocationStatus();
        RefreshPrayerPreview();
    }

    private void PopulateSeeds()
    {
        _suppress = true;
        try
        {
            SeedCityBox.Items.Clear();
            foreach (var city in KnownLocations.AlgeriaSeeds)
            {
                SeedCityBox.Items.Add(new ComboBoxItem
                {
                    Content = city.DisplayName,
                    Tag = city.Id
                });
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private void PopulateMethods()
    {
        _suppress = true;
        try
        {
            MethodBox.Items.Clear();
            foreach (var m in CalculationMethods.AllPresets)
            {
                MethodBox.Items.Add(new ComboBoxItem
                {
                    Content = m.DisplayName,
                    Tag = m.Id
                });
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private void LoadFromSettings()
    {
        _suppress = true;
        try
        {
            var s = App.State.Settings;
            SelectComboByTag(MethodBox, s.ActiveCalculationProfileId);
            MadhabBox.SelectedIndex = s.AsrMadhab == AsrMadhab.Hanafi ? 1 : 0;
            AdhkarMasterToggle.IsOn = s.AdhkarRemindersEnabled;
            MorningToggle.IsOn = s.AdhkarMorningEnabled;
            EveningToggle.IsOn = s.AdhkarEveningEnabled;
            AfterToggle.IsOn = s.AdhkarAfterPrayerEnabled;
            SleepToggle.IsOn = s.AdhkarSleepEnabled;
            MorningTimeBox.Text = LatinDigits.EnsureLatin(s.AdhkarMorningTime);
            EveningTimeBox.Text = LatinDigits.EnsureLatin(s.AdhkarEveningTime);
            SleepTimeBox.Text = LatinDigits.EnsureLatin(s.AdhkarSleepTime);

            var active = s.GetActiveLocation();
            if (active is not null)
            {
                SelectComboByTag(SeedCityBox, active.Id);
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private static void SelectComboByTag(ComboBox box, object? tag)
    {
        if (tag is null)
        {
            return;
        }

        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (Equals(item.Tag, tag)
                || (item.Tag is string a && tag is string b
                    && string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                || (item.Tag is Guid g1 && tag is Guid g2 && g1 == g2))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 3);
        Step1Panel.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = _step > 0;
        UpdateStepChrome();
        RebuildStepDots();
        if (_step == 1)
        {
            RefreshPrayerPreview();
        }
    }

    private void UpdateStepChrome()
    {
        var loc = App.Localization;
        StepTitle.Text = _step switch
        {
            0 => loc.Get("WizardStep1Title"),
            1 => loc.Get("WizardStep2Title"),
            2 => loc.Get("WizardStep3Title"),
            _ => loc.Get("WizardStep4Title")
        };
        StepHint.Text = _step switch
        {
            0 => loc.Get("WizardStep1Hint"),
            1 => loc.Get("WizardStep2Hint"),
            2 => loc.Get("WizardStep3Hint"),
            _ => loc.Get("WizardStep4Hint")
        };
        NextButton.Content = _step >= 3 ? loc.Get("WizardFinish") : loc.Get("WizardNext");
    }

    private void RebuildStepDots()
    {
        StepDots.Children.Clear();
        for (var i = 0; i < 4; i++)
        {
            var active = i == _step;
            StepDots.Children.Add(new Border
            {
                Width = active ? 22 : 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = active
                    ? (Brush)Application.Current.Resources["SamtGoldBrush"]
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x55, 0xC4, 0xA3, 0x5A))
            });
        }
    }

    private void RefreshLocationStatus()
    {
        var loc = App.State.Settings.GetActiveLocation();
        LocationStatusValue.Text = loc is null
            ? "—"
            : LatinDigits.EnsureLatin(
                $"{loc.DisplayName}  ({loc.Latitude:0.####}, {loc.Longitude:0.####})");
    }

    private void RefreshPrayerPreview()
    {
        try
        {
            var settings = App.State.Settings;
            var location = settings.GetActiveLocation();
            if (location is null)
            {
                PrayerTimesPreview.Text = "—";
                return;
            }

            TimeZoneInfo tz;
            try
            {
                tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            }
            catch
            {
                tz = TimeZoneInfo.Local;
            }

            var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, tz);
            var date = DateOnly.FromDateTime(localNow.DateTime);
            var schedule = _engine.Calculate(date, location, settings.GetActiveCalculationProfile(), tz);
            var loc = App.Localization;
            var lines = new List<string>();
            foreach (var prayer in new[]
                     {
                         PrayerEvent.Fajr, PrayerEvent.Dhuhr, PrayerEvent.Asr,
                         PrayerEvent.Maghrib, PrayerEvent.Isha
                     })
            {
                var t = schedule[prayer];
                if (t is not null)
                {
                    lines.Add($"{loc.GetPrayerName(prayer)}: {LatinDigits.Time(t.Value)}");
                }
            }

            PrayerTimesPreview.Text = string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Wizard prayer preview: {ex.Message}");
            PrayerTimesPreview.Text = "—";
        }
    }

    private async void GpsButton_OnClick(object sender, RoutedEventArgs e)
    {
        LocationBusyText.Text = "…";
        GpsButton.IsEnabled = false;
        try
        {
            var result = await _geo.TryGetPositionAsync();
            if (!result.Success || result.Latitude is null || result.Longitude is null)
            {
                LocationBusyText.Text = result.ErrorMessage
                                        ?? App.Localization.Get("LocationFailed");
                return;
            }

            var profile = WindowsGeolocationService.CreateProfileFromGps(
                result.Latitude.Value,
                result.Longitude.Value,
                App.Localization.Get("GpsLocationName"));

            var list = App.State.Settings.Locations
                .Where(l => l.Source != LocationSource.Gps)
                .ToList();
            list.Insert(0, profile);

            await App.State.UpdateAsync(s => s.With(
                locations: list,
                activeLocationId: profile.Id,
                replaceActiveLocationId: true));

            LocationBusyText.Text = App.Localization.Get("LocationFromGps");
            RefreshLocationStatus();
            RefreshPrayerPreview();
        }
        catch (Exception ex)
        {
            LocationBusyText.Text = ex.Message;
        }
        finally
        {
            GpsButton.IsEnabled = true;
        }
    }

    private async void PlaceSearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        LocationBusyText.Text = "…";
        try
        {
            var results = await _places.SearchAsync(PlaceSearchBox.Text ?? string.Empty);
            PlaceResultsList.Items.Clear();
            foreach (var r in results)
            {
                PlaceResultsList.Items.Add(new ListViewItem
                {
                    Content = r.DisplayName,
                    Tag = r
                });
            }

            LocationBusyText.Text = results.Count == 0
                ? App.Localization.Get("PlaceSearchNoResults")
                : LatinDigits.Number(results.Count);
        }
        catch (Exception ex)
        {
            LocationBusyText.Text = App.Localization.Get("PlaceSearchFailed") + " " + ex.Message;
        }
    }

    private async void PlaceResultsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || PlaceResultsList.SelectedItem is not ListViewItem { Tag: PlaceSearchResult place })
        {
            return;
        }

        var profile = new LocationProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = place.DisplayName,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            TimeZoneId = place.TimeZoneId,
            Source = LocationSource.PlaceSearch,
            CountryCode = place.CountryCode
        };

        var list = App.State.Settings.Locations.ToList();
        list.Insert(0, profile);
        await App.State.UpdateAsync(s => s.With(
            locations: list,
            activeLocationId: profile.Id,
            replaceActiveLocationId: true));

        RefreshLocationStatus();
        RefreshPrayerPreview();
        LocationBusyText.Text = App.Localization.Get("ApplyPlaceResult");
    }

    private async void SeedCityBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || SeedCityBox.SelectedItem is not ComboBoxItem { Tag: Guid id })
        {
            return;
        }

        var city = KnownLocations.AlgeriaSeeds.FirstOrDefault(c => c.Id == id);
        if (city is null)
        {
            return;
        }

        var list = App.State.Settings.Locations.ToList();
        if (list.All(l => l.Id != city.Id))
        {
            list.Insert(0, city);
        }

        await App.State.UpdateAsync(s => s.With(
            locations: list,
            activeLocationId: city.Id,
            replaceActiveLocationId: true));

        RefreshLocationStatus();
        RefreshPrayerPreview();
    }

    private async void MethodBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || MethodBox.SelectedItem is not ComboBoxItem { Tag: string id })
        {
            return;
        }

        await App.State.UpdateAsync(s => s.With(activeCalculationProfileId: id));
        RefreshPrayerPreview();
    }

    private async void MadhabBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || MadhabBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var madhab = string.Equals(tag, "Hanafi", StringComparison.OrdinalIgnoreCase)
            ? AsrMadhab.Hanafi
            : AsrMadhab.Standard;
        await App.State.UpdateAsync(s => s.With(asrMadhab: madhab));
        RefreshPrayerPreview();
    }

    private async void SkipButton_OnClick(object sender, RoutedEventArgs e)
        => await CompleteAsync(applySkipDefaults: true);

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            ShowStep(_step - 1);
        }
    }

    private async void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_step < 3)
        {
            if (_step == 2)
            {
                await PersistAdhkarFromUiAsync();
            }

            ShowStep(_step + 1);
            return;
        }

        await PersistAdhkarFromUiAsync();
        await CompleteAsync(applySkipDefaults: false);
    }

    private async Task PersistAdhkarFromUiAsync()
    {
        var morning = SettingsJson.NormalizeClockTime(MorningTimeBox.Text, "06:00");
        var evening = SettingsJson.NormalizeClockTime(EveningTimeBox.Text, "17:00");
        var sleep = SettingsJson.NormalizeClockTime(SleepTimeBox.Text, "22:00");
        await App.State.UpdateAsync(s => s.With(
            adhkarRemindersEnabled: AdhkarMasterToggle.IsOn,
            adhkarMorningEnabled: MorningToggle.IsOn,
            adhkarEveningEnabled: EveningToggle.IsOn,
            adhkarAfterPrayerEnabled: AfterToggle.IsOn,
            adhkarSleepEnabled: SleepToggle.IsOn,
            adhkarMorningTime: morning,
            adhkarEveningTime: evening,
            adhkarSleepTime: sleep));
    }

    private async Task CompleteAsync(bool applySkipDefaults)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        try
        {
            if (applySkipDefaults)
            {
                // Smart defaults: keep seed location if still default-ish; no GPS; mark wizard done.
                // Adhkar reminders stay off unless user already toggled them in step 3.
                await App.State.UpdateAsync(s => s.With(setupWizardCompleted: true));
            }
            else
            {
                await App.State.UpdateAsync(s => s.With(setupWizardCompleted: true));
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Wizard complete failed: {ex.Message}");
        }

        try
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // ignore
        }

        try
        {
            Close();
        }
        catch
        {
            // ignore
        }
    }

    private void CloseCaptionButton_OnClick(object sender, RoutedEventArgs e)
        => Close();
}
