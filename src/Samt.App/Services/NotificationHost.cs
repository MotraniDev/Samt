using Microsoft.UI.Xaml;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Notifications;
using Samt.Core.Time;
using Samt_App.Helpers;
using Samt_App.Overlay;

namespace Samt_App.Services;

/// <summary>
/// Rebuilds today's notification plan and fires due toast / overlay / audio channels.
/// Reschedules on settings change, day rollover, and a 15s poll.
/// </summary>
public sealed class NotificationHost : IDisposable
{
    private readonly AppState _state;
    private readonly LocalizationService _localization;
    private readonly ToastNotificationService _toasts;
    private readonly TrayIconService _tray;
    private readonly OverlayService _overlay;
    private readonly AdhanAudioService _audio;
    private readonly IPrayerEngine _engine = new PrayerEngine();
    private readonly NotificationPlanner _planner = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly HashSet<string> _fired = new(StringComparer.Ordinal);
    private IReadOnlyList<PlannedNotification> _plan = [];
    private DateOnly _planDate;
    private bool _disposed;

    public NotificationHost(
        AppState state,
        LocalizationService localization,
        ToastNotificationService toasts,
        TrayIconService tray,
        OverlayService overlay,
        AdhanAudioService audio)
    {
        _state = state;
        _localization = localization;
        _toasts = toasts;
        _tray = tray;
        _overlay = overlay;
        _audio = audio;
    }

    public void Start()
    {
        _state.SettingsChanged += OnSettingsChanged;
        _timer.Tick += OnTick;
        RebuildPlan();
        _timer.Start();
        LaunchLog.Write($"NotificationHost started with {_plan.Count} planned events");
    }

    /// <summary>Design-lab / diagnostics: fire channels for a synthetic event now.</summary>
    public void PreviewNow(
        PlannedNotificationKind kind,
        PrayerEvent prayer = PrayerEvent.Fajr,
        double? opacity = null,
        int? animationMs = null,
        OverlayEdge? entryEdge = null,
        OverlayVisualStyle? style = null)
    {
        var channels = kind == PlannedNotificationKind.PrayerStart
            ? NotificationChannel.All
            : NotificationChannel.WindowsToast | NotificationChannel.Overlay;

        var planned = new PlannedNotification
        {
            Id = "preview-" + Guid.NewGuid().ToString("N")[..8],
            FireAt = DateTimeOffset.Now,
            Kind = kind,
            Prayer = prayer,
            Channels = channels,
            OffsetMinutes = kind == PlannedNotificationKind.BeforePrayer ? 15 : null
        };

        // Design lab always wants motion — do not honor OS reduce-motion here.
        Dispatch(planned, opacity, animationMs, entryEdge, style, forceMotion: true);
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
        _state.SettingsChanged -= OnSettingsChanged;
        // Overlay/audio lifetime is owned by App.
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Defer off the save call stack to avoid UI reentrancy crashes.
        try
        {
            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() is { } dq)
            {
                dq.TryEnqueue(() =>
                {
                    try { RebuildPlan(); }
                    catch (Exception ex) { LaunchLog.Write($"Deferred RebuildPlan failed: {ex.Message}"); }
                });
            }
            else
            {
                RebuildPlan();
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OnSettingsChanged failed: {ex.Message}");
        }
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            var now = ResolveLocalNow(out var location, out _);
            var today = DateOnly.FromDateTime(now.DateTime);
            if (today != _planDate)
            {
                RebuildPlan();
            }

            FireDue(now);
            UpdateTrayTooltip(location, now);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"NotificationHost tick error: {ex.Message}");
        }
    }

    public void RebuildPlan()
    {
        try
        {
            var now = ResolveLocalNow(out var location, out var profile);
            var today = DateOnly.FromDateTime(now.DateTime);
            var schedule = _engine.Calculate(today, location, profile);
            var rules = _state.Settings.NotificationRules;
            var suppress = location.SuppressDhuhrNotificationsOnFriday;

            _plan = _planner.Plan(schedule, rules, now, suppress);
            _planDate = today;
            _fired.Clear();
            UpdateTrayTooltip(location, now);
            LaunchLog.Write($"Plan rebuilt: {_plan.Count} remaining for {today:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"RebuildPlan failed: {ex}");
            _plan = [];
        }
    }

    private void FireDue(DateTimeOffset now)
    {
        foreach (var item in _plan)
        {
            if (_fired.Contains(item.Id))
            {
                continue;
            }

            // Fire when due or up to one poll interval late (covers sleep briefly).
            if (item.FireAt > now)
            {
                continue;
            }

            if (now - item.FireAt > TimeSpan.FromMinutes(2))
            {
                // Too old — mark skipped.
                _fired.Add(item.Id);
                continue;
            }

            Dispatch(item);
            _fired.Add(item.Id);
        }
    }

    private void Dispatch(
        PlannedNotification item,
        double? opacityOverride = null,
        int? animationMsOverride = null,
        OverlayEdge? edgeOverride = null,
        OverlayVisualStyle? styleOverride = null,
        bool forceMotion = false)
    {
        var prayerName = _localization.GetPrayerName(item.Prayer);
        var channels = item.Channels;
        var wantsToast = channels.HasFlag(NotificationChannel.WindowsToast)
                         || channels == NotificationChannel.All;
        var wantsOverlay = channels.HasFlag(NotificationChannel.Overlay)
                           || channels == NotificationChannel.All;
        var wantsAudio = channels.HasFlag(NotificationChannel.Audio)
                         || channels == NotificationChannel.All;

        if (wantsToast)
        {
            var title = item.Kind == PlannedNotificationKind.BeforePrayer
                ? _localization.Get("NextPrayer")
                : prayerName;
            _toasts.Show(item, prayerName, title);
        }

        // Overlay service owns audio when both are requested (stop button stops adhan).
        if (wantsOverlay)
        {
            var audio = ResolveAudio(item, wantsAudio);
            var overlay = ResolveOverlay(item);
            if (overlay.Enabled)
            {
                _overlay.Show(
                    item,
                    prayerName,
                    audio,
                    overlay,
                    styleOverride: styleOverride,
                    edgeOverride: edgeOverride,
                    opacityOverride: opacityOverride,
                    animationMsOverride: animationMsOverride,
                    forceMotion: forceMotion);
            }
            else if (wantsAudio && item.Kind == PlannedNotificationKind.PrayerStart)
            {
                _audio.Play(audio);
            }
        }
        else if (wantsAudio && item.Kind == PlannedNotificationKind.PrayerStart)
        {
            _audio.Play(ResolveAudio(item, wantsAudio: true));
        }
    }

    private AudioProfile ResolveAudio(PlannedNotification item, bool wantsAudio)
    {
        if (!wantsAudio || item.Kind != PlannedNotificationKind.PrayerStart)
        {
            return new AudioProfile { Source = AudioSource.Silent };
        }

        // Prefer per-rule audio when present; else app default.
        var ruleAudio = _state.Settings.NotificationRules
            .FirstOrDefault(r =>
                r.Enabled
                && r.Kind == NotificationEventKind.PrayerStart
                && r.Audio is not null)?.Audio;

        return ruleAudio ?? _state.Settings.DefaultAudio ?? new AudioProfile();
    }

    private OverlayProfile ResolveOverlay(PlannedNotification item)
    {
        var ruleOverlay = _state.Settings.NotificationRules
            .FirstOrDefault(r =>
                r.Enabled
                && ((item.Kind == PlannedNotificationKind.PrayerStart && r.Kind == NotificationEventKind.PrayerStart)
                    || (item.Kind == PlannedNotificationKind.BeforePrayer && r.Kind == NotificationEventKind.BeforePrayer))
                && r.Overlay is not null)?.Overlay;

        return ruleOverlay ?? _state.Settings.DefaultOverlay ?? new OverlayProfile();
    }

    private void UpdateTrayTooltip(LocationProfile location, DateTimeOffset now)
    {
        try
        {
            var profile = _state.RequireCalculationProfile();
            var schedule = _engine.Calculate(DateOnly.FromDateTime(now.DateTime), location, profile);
            var next = PrayerTimeline.GetNext(schedule, now);
            if (next is null)
            {
                var tomorrow = _engine.Calculate(DateOnly.FromDateTime(now.DateTime).AddDays(1), location, profile);
                next = PrayerTimeline.GetNext(tomorrow, now);
            }

            if (next is null)
            {
                _tray.UpdateTooltip("SAMT");
                return;
            }

            var name = _localization.GetPrayerName(next.Event);
            var time = LatinDigits.Time(next.Time);
            var left = LatinDigits.Duration(next.Remaining);
            _tray.UpdateTooltip($"SAMT — {name} {time} ({left})");
        }
        catch
        {
            _tray.UpdateTooltip("SAMT");
        }
    }

    private DateTimeOffset ResolveLocalNow(out LocationProfile location, out CalculationProfile profile)
    {
        location = _state.RequireActiveLocation();
        profile = _state.RequireCalculationProfile();
        var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
        return TimeZoneInfo.ConvertTime(_state.Now, tz);
    }
}
