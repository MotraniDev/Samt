using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Time;
using Samt_App.Services;

namespace Samt_App.ViewModels;

public sealed class DiagnosticsViewModel : INotifyPropertyChanged
{
    private readonly IPrayerEngine _engine;
    private readonly LocalizationService _localization;
    private readonly AppState _appState;
    private LocationProfile _location;
    private CalculationProfile _method;
    private AsrMadhab _asrMadhab;
    private int _hijriDayOffset;
    private bool _autoStartEnabled;
    private bool _showMissedAlertOnResume;
    private string _processStatusText = string.Empty;
    private DateTimeOffset _selectedDate = DateTimeOffset.Now;
    private bool _suppressPersist;

    public DiagnosticsViewModel(IPrayerEngine engine, LocalizationService localization, AppState appState)
    {
        _engine = engine;
        _localization = localization;
        _appState = appState;
        _location = appState.RequireActiveLocation();
        _method = CalculationMethods.GetById(appState.Settings.ActiveCalculationProfileId);
        _asrMadhab = appState.Settings.AsrMadhab;
        _hijriDayOffset = HijriConverter.ClampDayOffset(appState.Settings.HijriDayOffset);
        _autoStartEnabled = appState.Settings.AutoStartEnabled;
        _showMissedAlertOnResume = appState.Settings.ShowMissedAlertOnResume;
        Locations = appState.Settings.Locations.ToList();
        Methods = CalculationMethods.AllPresets.ToList();
        Recalculate();
        RefreshProcessStatus();
        _appState.SettingsChanged += (_, _) =>
        {
            if (!_suppressPersist)
            {
                ReloadFromSettings();
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LocationProfile> Locations { get; private set; }
    public IReadOnlyList<CalculationProfile> Methods { get; }

    public ObservableCollection<PrayerTimeRow> Rows { get; } = [];

    public LocationProfile SelectedLocation
    {
        get => _location;
        set
        {
            if (value is null || _location.Id == value.Id)
            {
                return;
            }

            _location = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LatitudeText));
            OnPropertyChanged(nameof(LongitudeText));
            OnPropertyChanged(nameof(TimeZoneText));
            Recalculate();
            PersistQuietly(s => s.With(activeLocationId: value.Id, replaceActiveLocationId: true));
        }
    }

    public CalculationProfile SelectedMethod
    {
        get => _method;
        set
        {
            if (value is null || string.Equals(_method.Id, value.Id, StringComparison.Ordinal))
            {
                return;
            }

            _method = value;
            OnPropertyChanged();
            Recalculate();
            PersistQuietly(s => s.With(activeCalculationProfileId: value.Id));
        }
    }

    public AsrMadhab SelectedAsrMadhab
    {
        get => _asrMadhab;
        set
        {
            if (_asrMadhab == value)
            {
                return;
            }

            _asrMadhab = value;
            OnPropertyChanged();
            Recalculate();
            PersistQuietly(s => s.With(asrMadhab: value));
        }
    }

    public DateTimeOffset SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate.Date == value.Date)
            {
                return;
            }

            _selectedDate = value;
            OnPropertyChanged();
            Recalculate();
        }
    }

    public int HijriDayOffset
    {
        get => _hijriDayOffset;
        set
        {
            var clamped = HijriConverter.ClampDayOffset(value);
            if (_hijriDayOffset == clamped)
            {
                return;
            }

            _hijriDayOffset = clamped;
            OnPropertyChanged();
            PersistQuietly(s => s.With(hijriDayOffset: clamped));
        }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (_autoStartEnabled == value)
            {
                return;
            }

            _autoStartEnabled = value;
            OnPropertyChanged();
            PersistQuietly(s => s.With(autoStartEnabled: value));
            try
            {
                AutoStartService.Apply(value);
            }
            catch (Exception ex)
            {
                Helpers.LaunchLog.Write($"Diagnostics AutoStart apply failed: {ex.Message}");
            }
        }
    }

    public bool ShowMissedAlertOnResume
    {
        get => _showMissedAlertOnResume;
        set
        {
            if (_showMissedAlertOnResume == value)
            {
                return;
            }

            _showMissedAlertOnResume = value;
            OnPropertyChanged();
            PersistQuietly(s => s.With(showMissedAlertOnResume: value));
        }
    }

    public string ProcessStatusText
    {
        get => _processStatusText;
        private set
        {
            if (_processStatusText == value)
            {
                return;
            }

            _processStatusText = value;
            OnPropertyChanged();
        }
    }

    public string LatitudeText => LatinDigits.Number(_location.Latitude, "0.0000");
    public string LongitudeText => LatinDigits.Number(_location.Longitude, "0.0000");
    public string TimeZoneText => _location.TimeZoneId;
    public string PhaseBanner => _localization.Get("PhaseBanner");
    public string Disclaimer => _localization.Get("Disclaimer");

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(PhaseBanner));
        OnPropertyChanged(nameof(Disclaimer));
        Recalculate();
        RefreshProcessStatus();
    }

    public void RefreshProcessStatus()
    {
        try
        {
            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            proc.Refresh();
            var mb = proc.WorkingSet64 / (1024.0 * 1024.0);
            var planned = App.Notifications?.PlannedCount ?? 0;
            var run = AutoStartService.IsRegistered()
                ? _localization.Get("AutoStartRegisteredYes")
                : _localization.Get("AutoStartRegisteredNo");
            ProcessStatusText = string.Format(
                _localization.Get("ProcessStatusFormat"),
                LatinDigits.Number(mb, "0.0"),
                LatinDigits.Number(planned),
                run);
        }
        catch (Exception ex)
        {
            ProcessStatusText = ex.Message;
        }
    }

    public void Recalculate()
    {
        var profile = _method.WithAsr(_asrMadhab);
        var date = DateOnly.FromDateTime(_selectedDate.DateTime);
        var schedule = _engine.Calculate(date, _location, profile);

        Rows.Clear();
        foreach (var key in PrayerSchedule.CoreDisplayOrder)
        {
            if (!schedule.Times.TryGetValue(key, out var time))
            {
                continue;
            }

            var raw = schedule.RawTimes.TryGetValue(key, out var rawTime)
                ? LatinDigits.Time(rawTime, "HH:mm:ss")
                : "—";

            Rows.Add(new PrayerTimeRow(
                _localization.GetPrayerName(key),
                LatinDigits.Time(time),
                raw));
        }

        if (schedule.Jumuah is { } jumuah)
        {
            Rows.Add(new PrayerTimeRow(
                _localization.GetPrayerName(PrayerEvent.Jumuah),
                LatinDigits.Time(jumuah),
                LatinDigits.Time(jumuah, "HH:mm:ss")));
        }
    }

    private void ReloadFromSettings()
    {
        Locations = _appState.Settings.Locations.ToList();
        OnPropertyChanged(nameof(Locations));

        var active = _appState.RequireActiveLocation();
        _location = active;
        _method = CalculationMethods.GetById(_appState.Settings.ActiveCalculationProfileId);
        _asrMadhab = _appState.Settings.AsrMadhab;
        _hijriDayOffset = HijriConverter.ClampDayOffset(_appState.Settings.HijriDayOffset);
        _autoStartEnabled = _appState.Settings.AutoStartEnabled;
        _showMissedAlertOnResume = _appState.Settings.ShowMissedAlertOnResume;
        OnPropertyChanged(nameof(SelectedLocation));
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(SelectedAsrMadhab));
        OnPropertyChanged(nameof(HijriDayOffset));
        OnPropertyChanged(nameof(AutoStartEnabled));
        OnPropertyChanged(nameof(ShowMissedAlertOnResume));
        OnPropertyChanged(nameof(LatitudeText));
        OnPropertyChanged(nameof(LongitudeText));
        OnPropertyChanged(nameof(TimeZoneText));
        Recalculate();
        RefreshProcessStatus();
    }

    private void PersistQuietly(Func<AppSettings, AppSettings> mutate)
    {
        _suppressPersist = true;
        _ = PersistQuietlyAsync(mutate);
    }

    private async Task PersistQuietlyAsync(Func<AppSettings, AppSettings> mutate)
    {
        try
        {
            await _appState.UpdateAsync(mutate).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"Diagnostics persist failed: {ex.Message}");
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record PrayerTimeRow(
    string Name,
    string DisplayTime,
    string RawTime,
    bool IsNext = false)
{
    public Microsoft.UI.Xaml.Visibility NextMarkerVisibility =>
        IsNext ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}

