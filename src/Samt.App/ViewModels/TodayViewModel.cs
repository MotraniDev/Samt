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
    private PrayerEvent? _markedNext;
    private bool _isRamadan;
    private bool _disposed;
    private bool _timerStarted;

    public TodayViewModel(IPrayerEngine engine, LocalizationService localization, AppState appState)
    {
        _engine = engine;
        _localization = localization;
        _appState = appState;
        _timer.Tick += OnTick;
        _appState.SettingsChanged += OnSettingsChanged;
        Refresh();
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

    public string HijriDateText { get; private set; } = "—";

    public bool IsRamadan
    {
        get => _isRamadan;
        private set
        {
            if (_isRamadan == value)
            {
                return;
            }

            _isRamadan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RamadanBadgeVisibility));
        }
    }

    public Visibility RamadanBadgeVisibility =>
        IsRamadan ? Visibility.Visible : Visibility.Collapsed;

    public string QiblaBearingText { get; private set; } = "—";

    public string QiblaDistanceText { get; private set; } = "—";

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
        _next is null ? _localization.Get("DayComplete") : FormatPrayerName(_next.Event);

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

    /// <summary>Start the 1s countdown timer after the page is in the visual tree.</summary>
    public void StartTimer()
    {
        if (_timerStarted || _disposed)
        {
            return;
        }

        _timerStarted = true;
        _timer.Start();
    }

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Disclaimer));
        RefreshHijriAndQibla();
        RebuildRows(force: true);
    }

    public void Refresh()
    {
        var location = _appState.RequireActiveLocation();
        var profile = _appState.RequireCalculationProfile();
        var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(_appState.Now, tz);
        var date = DateOnly.FromDateTime(localNow.DateTime);

        _schedule = _engine.Calculate(date, location, profile);
        RefreshHijriAndQibla(date, location);
        UpdateNext(localNow, location, profile, forceRows: true);

        OnPropertyChanged(nameof(LocationName));
        OnPropertyChanged(nameof(MethodName));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(CoordinatesText));
        OnPropertyChanged(nameof(TimeZoneText));
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

    private void RefreshHijriAndQibla()
    {
        var location = _appState.RequireActiveLocation();
        var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(_appState.Now, tz);
        var date = DateOnly.FromDateTime(local.DateTime);
        RefreshHijriAndQibla(date, location);
    }

    private void RefreshHijriAndQibla(DateOnly date, LocationProfile location)
    {
        var offset = HijriConverter.ClampDayOffset(_appState.Settings.HijriDayOffset);
        var hijri = HijriConverter.FromGregorian(date, offset);
        var monthName = _localization.Get($"Hijri.Month.{hijri.Month}");
        HijriDateText = LatinDigits.Hijri(hijri.Day, monthName, hijri.Year);
        IsRamadan = hijri.IsRamadan;
        OnPropertyChanged(nameof(HijriDateText));

        var qibla = QiblaCalculator.Calculate(location.Latitude, location.Longitude);
        if (qibla.IsAtKaaba)
        {
            QiblaBearingText = _localization.Get("QiblaAtKaaba");
            QiblaDistanceText = LatinDigits.Number(0, "0") + " " + _localization.Get("QiblaKm");
        }
        else
        {
            var compass = _localization.Get("Compass." + QiblaCalculator.CompassOctantKey(qibla.BearingDegrees));
            var degrees = LatinDigits.Number(qibla.BearingDegrees, "0");
            QiblaBearingText = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                _localization.Get("QiblaBearingFormat"),
                degrees,
                compass);
            QiblaDistanceText = LatinDigits.Number(qibla.DistanceKm, "0") + " " + _localization.Get("QiblaKm");
        }

        OnPropertyChanged(nameof(QiblaBearingText));
        OnPropertyChanged(nameof(QiblaDistanceText));
    }

    private void UpdateNext(
        DateTimeOffset localNow,
        LocationProfile location,
        CalculationProfile profile,
        bool forceRows)
    {
        if (_schedule is null)
        {
            return;
        }

        var previous = _next?.Event;
        _next = PrayerTimeline.GetNext(_schedule, localNow);
        if (_next is null)
        {
            var tomorrow = _engine.Calculate(_schedule.Date.AddDays(1), location, profile);
            _next = PrayerTimeline.GetNext(tomorrow, localNow);
        }

        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTime));
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(DateText));

        var current = _next?.Event;
        if (forceRows || previous != current || _markedNext != current)
        {
            RebuildRows(force: true);
        }
    }

    /// <summary>
    /// Set a manual clock time for a prayer; stores a persistent minute offset from the
    /// unadjusted calculation so notifications and the timeline stay consistent.
    /// </summary>
    public async Task SetManualTimeAsync(PrayerEvent prayer, TimeSpan localTimeOfDay)
    {
        if (_schedule is null)
        {
            return;
        }

        var location = _appState.RequireActiveLocation();
        var baseProfile = CalculationMethods
            .GetById(_appState.Settings.ActiveCalculationProfileId)
            .WithAsr(_appState.Settings.AsrMadhab);
        var baseSchedule = _engine.Calculate(_schedule.Date, location, baseProfile);
        if (!baseSchedule.Times.TryGetValue(prayer, out var baseTime))
        {
            return;
        }

        var desired = new DateTimeOffset(
            _schedule.Date.ToDateTime(TimeOnly.FromTimeSpan(localTimeOfDay)),
            baseTime.Offset);

        // Prefer same civil day; if user set a time far past midnight edge, still clamp minutes.
        var deltaMinutes = (int)Math.Round((desired - baseTime).TotalMinutes);
        deltaMinutes = Math.Clamp(deltaMinutes, -180, 180);

        var map = _appState.Settings.MinuteAdjustments.ToDictionary(
            kv => kv.Key,
            kv => kv.Value,
            StringComparer.OrdinalIgnoreCase);

        if (deltaMinutes == 0)
        {
            map.Remove(prayer.ToString());
        }
        else
        {
            map[prayer.ToString()] = deltaMinutes;
        }

        await _appState.UpdateAsync(s => s.With(minuteAdjustments: map));
    }

    public async Task ClearManualTimeAsync(PrayerEvent prayer)
    {
        if (!_appState.Settings.HasManualAdjustment(prayer))
        {
            return;
        }

        var map = _appState.Settings.MinuteAdjustments.ToDictionary(
            kv => kv.Key,
            kv => kv.Value,
            StringComparer.OrdinalIgnoreCase);
        map.Remove(prayer.ToString());
        await _appState.UpdateAsync(s => s.With(minuteAdjustments: map));
    }

    private void RebuildRows(bool force)
    {
        if (_schedule is null)
        {
            Rows.Clear();
            _markedNext = null;
            return;
        }

        // Avoid full collection rebuild every second (layout thrash / possible stack pressure).
        var nextKey = _next is not null
                      && _schedule.Times.TryGetValue(_next.Event, out var nt)
                      && nt == _next.Time
            ? _next.Event
            : (PrayerEvent?)null;

        if (!force && nextKey == _markedNext && Rows.Count > 0)
        {
            return;
        }

        _markedNext = nextKey;
        Rows.Clear();

        IReadOnlyList<PrayerEvent> order = IsRamadan
            ?
            [
                PrayerEvent.Imsak,
                PrayerEvent.Fajr,
                PrayerEvent.Sunrise,
                PrayerEvent.Dhuhr,
                PrayerEvent.Asr,
                PrayerEvent.Maghrib,
                PrayerEvent.Isha,
                PrayerEvent.Midnight
            ]
            :
            [
                PrayerEvent.Fajr,
                PrayerEvent.Sunrise,
                PrayerEvent.Dhuhr,
                PrayerEvent.Asr,
                PrayerEvent.Maghrib,
                PrayerEvent.Isha
            ];

        foreach (var key in order)
        {
            if (!_schedule.Times.TryGetValue(key, out var time))
            {
                continue;
            }

            Rows.Add(new PrayerTimeRow(
                FormatPrayerName(key),
                LatinDigits.Time(time),
                LatinDigits.Time(time, "HH:mm:ss"),
                IsNext: nextKey == key,
                Event: key,
                IsManuallyAdjusted: _appState.Settings.HasManualAdjustment(key)));
        }
    }

    private string FormatPrayerName(PrayerEvent prayer)
    {
        if (IsRamadan && prayer == PrayerEvent.Maghrib)
        {
            return _localization.Get("Prayer.MaghribIftar");
        }

        return _localization.GetPrayerName(prayer);
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            if (_schedule is null)
            {
                Refresh();
                return;
            }

            var location = _appState.RequireActiveLocation();
            var profile = _appState.RequireCalculationProfile();
            var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(_appState.Now, tz);

            if (DateOnly.FromDateTime(localNow.DateTime) != _schedule.Date)
            {
                Refresh();
                return;
            }

            // Countdown only — do not rebuild the list every second.
            UpdateNext(localNow, location, profile, forceRows: false);
        }
        catch
        {
            // Never let timer exceptions tear down the process.
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Refresh();

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
