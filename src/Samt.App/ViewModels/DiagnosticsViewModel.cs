using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
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
    private DateTimeOffset _selectedDate = DateTimeOffset.Now;

    public DiagnosticsViewModel(IPrayerEngine engine, LocalizationService localization, AppState appState)
    {
        _engine = engine;
        _localization = localization;
        _appState = appState;
        _location = appState.RequireActiveLocation();
        _method = CalculationMethods.GetById(appState.Settings.ActiveCalculationProfileId);
        _asrMadhab = appState.Settings.AsrMadhab;
        Locations = appState.Settings.Locations.ToList();
        Methods = CalculationMethods.AllPresets.ToList();
        Recalculate();
        _appState.SettingsChanged += (_, _) => ReloadFromSettings();
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
            _ = _appState.UpdateAsync(s => s.With(activeLocationId: value.Id));
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
            _ = _appState.UpdateAsync(s => s.With(activeCalculationProfileId: value.Id));
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
            _ = _appState.UpdateAsync(s => s.With(asrMadhab: value));
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
                _localization.Get($"Prayer.{key}"),
                LatinDigits.Time(time),
                raw));
        }

        if (schedule.Jumuah is { } jumuah)
        {
            Rows.Add(new PrayerTimeRow(
                _localization.Get("Prayer.Jumuah"),
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
        OnPropertyChanged(nameof(SelectedLocation));
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(SelectedAsrMadhab));
        OnPropertyChanged(nameof(LatitudeText));
        OnPropertyChanged(nameof(LongitudeText));
        OnPropertyChanged(nameof(TimeZoneText));
        Recalculate();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record PrayerTimeRow(string Name, string DisplayTime, string RawTime);
