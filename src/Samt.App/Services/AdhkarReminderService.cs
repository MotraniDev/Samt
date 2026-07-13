using Microsoft.UI.Xaml;
using Samt.Core.Adhkar;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Time;
using Samt_App.Helpers;
using Samt_App.Overlay;

namespace Samt_App.Services;

/// <summary>
/// Schedule-linked Adhkar prompts. After-prayer is queued until the Adhan overlay is dismissed.
/// </summary>
public sealed class AdhkarReminderService : IDisposable
{
    private readonly AppState _state;
    private readonly IClock _clock;
    private readonly IPrayerEngine _engine;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private AdhkarReaderWindow? _reader;
    private AdhkarCollectionKind? _queuedAfterPrayer;
    private DateOnly _lastMorning;
    private DateOnly _lastEvening;
    private DateOnly _lastSleep;
    private readonly HashSet<string> _firedAfterPrayerKeys = new(StringComparer.Ordinal);
    private bool _disposed;

    public AdhkarReminderService(AppState state, IPrayerEngine? engine = null, IClock? clock = null)
    {
        _state = state;
        _engine = engine ?? new PrayerEngine();
        _clock = clock ?? new SystemClock();
        _timer.Tick += (_, _) => OnTick();
    }

    public void Start()
    {
        _timer.Start();
        OnTick();
    }

    public void Stop() => _timer.Stop();

    /// <summary>Called when Adhan overlay fully dismisses so queued After-prayer can open.</summary>
    public void OnAdhanOverlayDismissed()
    {
        if (_queuedAfterPrayer is { } kind)
        {
            _queuedAfterPrayer = null;
            OpenReader(kind);
        }
    }

    /// <summary>Queue After-prayer until overlay dismiss, or open immediately if no overlay.</summary>
    public void NotifyPrayerStartCompleted(PrayerEvent prayer, bool overlayWasShown)
    {
        if (!_state.Settings.AdhkarRemindersEnabled || !_state.Settings.AdhkarAfterPrayerEnabled)
        {
            return;
        }

        if (prayer is not (PrayerEvent.Fajr or PrayerEvent.Dhuhr or PrayerEvent.Asr
            or PrayerEvent.Maghrib or PrayerEvent.Isha or PrayerEvent.Jumuah))
        {
            return;
        }

        var key = $"{DateOnly.FromDateTime(_clock.UtcNow.LocalDateTime):yyyy-MM-dd}:{prayer}";
        if (!_firedAfterPrayerKeys.Add(key))
        {
            return;
        }

        if (overlayWasShown)
        {
            _queuedAfterPrayer = AdhkarCollectionKind.AfterPrayer;
        }
        else
        {
            OpenReader(AdhkarCollectionKind.AfterPrayer);
        }
    }

    public void OpenReader(AdhkarCollectionKind kind)
    {
        try
        {
            var dq = App.MainWindow?.DispatcherQueue
                     ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dq is null)
            {
                return;
            }

            void Show()
            {
                try
                {
                    _reader ??= new AdhkarReaderWindow();
                    _reader.ShowCollection(kind);
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"Adhkar reader open failed: {ex.Message}");
                }
            }

            if (dq.HasThreadAccess)
            {
                Show();
            }
            else
            {
                _ = dq.TryEnqueue(Show);
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Adhkar OpenReader failed: {ex.Message}");
        }
    }

    private void OnTick()
    {
        try
        {
            Evaluate();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Adhkar tick failed: {ex.Message}");
        }
    }

    private void Evaluate()
    {
        var settings = _state.Settings;
        if (!settings.AdhkarRemindersEnabled)
        {
            return;
        }

        var location = settings.GetActiveLocation();
        if (location is null)
        {
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

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, tz);
        var today = DateOnly.FromDateTime(localNow);
        var profile = settings.GetActiveCalculationProfile();
        var schedule = _engine.Calculate(today, location, profile);
        var nowOffset = new DateTimeOffset(localNow, tz.GetUtcOffset(localNow));

        // Morning: after Fajr until Sunrise
        if (settings.AdhkarMorningEnabled && _lastMorning != today)
        {
            var fajr = schedule[PrayerEvent.Fajr];
            var sunrise = schedule[PrayerEvent.Sunrise];
            if (fajr is { } f && sunrise is { } s && nowOffset >= f && nowOffset < s)
            {
                _lastMorning = today;
                OpenReader(AdhkarCollectionKind.Morning);
            }
        }

        // Evening: after Asr until Maghrib
        if (settings.AdhkarEveningEnabled && _lastEvening != today)
        {
            var asr = schedule[PrayerEvent.Asr];
            var maghrib = schedule[PrayerEvent.Maghrib];
            if (asr is { } a && maghrib is { } m && nowOffset >= a && nowOffset < m)
            {
                _lastEvening = today;
                OpenReader(AdhkarCollectionKind.Evening);
            }
        }

        // Sleep: fixed local clock (2-minute fire window)
        if (settings.AdhkarSleepEnabled && _lastSleep != today)
        {
            if (TimeOnly.TryParse(settings.AdhkarSleepTime, out var sleepAt))
            {
                var nowT = TimeOnly.FromDateTime(localNow);
                var sleepEnd = sleepAt.AddMinutes(2);
                if (nowT >= sleepAt && nowT < sleepEnd)
                {
                    _lastSleep = today;
                    OpenReader(AdhkarCollectionKind.Sleep);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }
}
