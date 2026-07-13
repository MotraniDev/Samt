using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Time;
using Samt_App.Services;

namespace Samt_App.ViewModels;

public sealed class TodayViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IPrayerEngine _engine;
    private readonly LocalizationService _localization;
    private readonly AppState _appState;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private PrayerSchedule? _schedule;
    private NextPrayerInfo? _next;
    private bool _disposed;

    public TodayViewModel(IPrayerEngine engine, LocalizationService localization, AppState appState)
    {
        _engine = engine;
        _localization = localization;
        _appState = appState;
        _timer.Tick += OnTick;
        _appState.SettingsChanged += OnSettingsChanged;
        Refresh();
        _timer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PrayerTimeRow> Rows { get; } = [];

    public string LocationName => _appState.RequireActiveLocation().DisplayName;

    public string MethodName =>
        CalculationMethods.GetById(_appState.Settings.ActiveCalculationProfileId).DisplayName;

    public string DateText
    {
        get
        {
            var location = _appState.RequireActiveLocation();
            var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            var local = TimeZoneInfo.ConvertTime(_appState.Now, tz);
            return LatinDigits.Date(DateOnly.FromDateTime(local.DateTime));
        }
    }

    public string CoordinatesText
    {
        get
        {
            var loc = _appState.RequireActiveLocation();
            return $"{LatinDigits.Number(loc.Latitude, "0.0000")}, {LatinDigits.Number(loc.Longitude, "0.0000")}";
        }
    }

    public string TimeZoneText => _appState.RequireActiveLocation().TimeZoneId;

    public string NextPrayerName =>
        _next is null ? _localization.Get("DayComplete") : _localization.Get($"Prayer.{_next.Event}");

    public string NextPrayerTime =>
        _next is null ? "—" : LatinDigits.Time(_next.Time);

    public string CountdownText =>
        _next is null ? "—" : LatinDigits.Duration(_next.Remaining);

    public string StatusText
    {
        get
        {
            var asr = _appState.Settings.AsrMadhab == AsrMadhab.Hanafi
                ? _localization.Get("AsrHanafi")
                : _localization.Get("AsrStandard");
            return $"{MethodName} · {asr}";
        }
    }

    public string Disclaimer => _localization.Get("Disclaimer");

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Disclaimer));
        RebuildRows();
    }

    public void Refresh()
    {
        var location = _appState.RequireActiveLocation();
        var profile = _appState.RequireCalculationProfile();
        var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(_appState.Now, tz);
        var date = DateOnly.FromDateTime(localNow.DateTime);

        _schedule = _engine.Calculate(date, location, profile);
        _next = PrayerTimeline.GetNext(_schedule, localNow);

        if (_next is null)
        {
            var tomorrow = _engine.Calculate(date.AddDays(1), location, profile);
            _next = PrayerTimeline.GetNext(tomorrow, localNow);
        }

        RebuildRows();
        OnPropertyChanged(nameof(LocationName));
        OnPropertyChanged(nameof(MethodName));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(CoordinatesText));
        OnPropertyChanged(nameof(TimeZoneText));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTime));
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(StatusText));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _appState.SettingsChanged -= OnSettingsChanged;
    }

    private void RebuildRows()
    {
        Rows.Clear();
        if (_schedule is null)
        {
            return;
        }

        foreach (var key in new[]
                 {
                     PrayerEvent.Fajr,
                     PrayerEvent.Sunrise,
                     PrayerEvent.Dhuhr,
                     PrayerEvent.Asr,
                     PrayerEvent.Maghrib,
                     PrayerEvent.Isha
                 })
        {
            if (!_schedule.Times.TryGetValue(key, out var time))
            {
                continue;
            }

            Rows.Add(new PrayerTimeRow(
                _localization.Get($"Prayer.{key}"),
                LatinDigits.Time(time),
                LatinDigits.Time(time, "HH:mm:ss")));
        }
    }

    private void OnTick(object? sender, object e)
    {
        if (_schedule is null)
        {
            Refresh();
            return;
        }

        var location = _appState.RequireActiveLocation();
        var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(_appState.Now, tz);

        if (DateOnly.FromDateTime(localNow.DateTime) != _schedule.Date)
        {
            Refresh();
            return;
        }

        _next = PrayerTimeline.GetNext(_schedule, localNow);
        if (_next is null)
        {
            var tomorrow = _engine.Calculate(
                _schedule.Date.AddDays(1),
                location,
                _appState.RequireCalculationProfile());
            _next = PrayerTimeline.GetNext(tomorrow, localNow);
        }

        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTime));
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(DateText));
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Refresh();

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
