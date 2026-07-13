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
/// Schedule-linked Adhkar prompts. After-prayer can wait for Adhan overlay dismiss
/// and/or a user-defined delay after the Adhan.
/// </summary>
public sealed class AdhkarReminderService : IDisposable
{
    private readonly AppState _state;
    private readonly IClock _clock;
    private readonly IPrayerEngine _engine;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private AdhkarReaderWindow? _reader;
    private AdhkarCollectionKind? _queuedAfterPrayer;
    private DateTimeOffset? _scheduledAfterPrayerUtc;
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

    /// <summary>Called when Adhan overlay fully dismisses so queued After-prayer can open (delay 0).</summary>
    public void OnAdhanOverlayDismissed()
    {
        if (_queuedAfterPrayer is { } kind)
        {
            _queuedAfterPrayer = null;
            OpenReader(kind);
        }
    }

    /// <summary>
    /// After each prayer Adhan: open After-prayer immediately, after overlay dismiss,
    /// or after a configured delay from this moment.
    /// </summary>
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

        var delayMinutes = Math.Clamp(_state.Settings.AdhkarAfterPrayerDelayMinutes, 0, 180);
        if (delayMinutes > 0)
        {
            // User-defined period after Adhan — open when the timer elapses.
            _queuedAfterPrayer = null;
            _scheduledAfterPrayerUtc = _clock.UtcNow.AddMinutes(delayMinutes);
            return;
        }

        // Delay 0: after Adhan, wait for overlay if it was shown.
        _scheduledAfterPrayerUtc = null;
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
                    // WinUI windows cannot be re-shown after Close(); always recreate.
                    if (_reader is null)
                    {
                        _reader = new AdhkarReaderWindow();
                        _reader.Closed += Reader_OnClosed;
                    }

                    _reader.ShowCollection(kind);
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"Adhkar reader open failed: {ex.Message}");
                    _reader = null;
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

    private void Reader_OnClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _reader))
        {
            _reader = null;
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

        // Delayed after-prayer (minutes after Adhan).
        if (_scheduledAfterPrayerUtc is { } scheduled
            && settings.AdhkarAfterPrayerEnabled
            && _clock.UtcNow >= scheduled)
        {
            _scheduledAfterPrayerUtc = null;
            OpenReader(AdhkarCollectionKind.AfterPrayer);
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
        var nowT = TimeOnly.FromDateTime(localNow);

        // Morning / Evening / Sleep: user-configured local clock times (2-minute fire window).
        TryFireClock(settings.AdhkarMorningEnabled, settings.AdhkarMorningTime, ref _lastMorning, today, nowT,
            AdhkarCollectionKind.Morning);
        TryFireClock(settings.AdhkarEveningEnabled, settings.AdhkarEveningTime, ref _lastEvening, today, nowT,
            AdhkarCollectionKind.Evening);
        TryFireClock(settings.AdhkarSleepEnabled, settings.AdhkarSleepTime, ref _lastSleep, today, nowT,
            AdhkarCollectionKind.Sleep);
    }

    private void TryFireClock(
        bool enabled,
        string? timeText,
        ref DateOnly lastFired,
        DateOnly today,
        TimeOnly nowT,
        AdhkarCollectionKind kind)
    {
        if (!enabled || lastFired == today)
        {
            return;
        }

        if (!TimeOnly.TryParse(timeText, out var at))
        {
            return;
        }

        var end = at.AddMinutes(2);
        var inWindow = end > at
            ? nowT >= at && nowT < end
            : nowT >= at || nowT < end; // crosses midnight (rare)

        if (!inWindow)
        {
            return;
        }

        lastFired = today;
        OpenReader(kind);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        try
        {
            if (_reader is not null)
            {
                _reader.Closed -= Reader_OnClosed;
            }
        }
        catch
        {
            // ignore
        }
    }
}
