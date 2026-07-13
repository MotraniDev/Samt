using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Samt.Core.Calculation;
using Samt.Core.Calendar;
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
    // 1s poll keeps Adhan fire aligned with the pre-alert live countdown (HH:MM:SS).
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly HashSet<string> _fired = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missedReported = new(StringComparer.Ordinal);
    private IReadOnlyList<PlannedNotification> _plan = [];
    private IReadOnlyList<PlannedSpecialDayReminder> _specialPlan = [];
    private IReadOnlyList<PlannedUserCalendarReminder> _userPlan = [];
    private DateOnly _planDate;
    private bool _disposed;
    private DateTimeOffset _lastTickLocal;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

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

    /// <summary>Remaining future planned events (for diagnostics).</summary>
    public int PlannedCount => _plan.Count;

    /// <summary>Raised after a prayer-start notification is dispatched (for Adhkar queue).</summary>
    public event EventHandler<PrayerEvent>? PrayerStartDispatched;

    public void Start()
    {
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _state.SettingsChanged += OnSettingsChanged;
        _timer.Tick += OnTick;
        _overlay.PreAlertCountdownReachedZero += OnPreAlertCountdownReachedZero;
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"PowerMode subscribe failed: {ex.Message}");
        }

        RebuildPlan();
        _lastTickLocal = ResolveLocalNow(out _, out _);
        ReportMissedSinceDayStart();
        _timer.Start();
        LaunchLog.Write($"NotificationHost started with {_plan.Count} planned events");
    }

    private void OnPreAlertCountdownReachedZero(object? sender, EventArgs e)
    {
        // Countdown hit 00:00:00 — fire due Adhan start immediately (do not wait for next poll).
        try
        {
            var now = ResolveLocalNow(out _, out _);
            FireDue(now);
            _lastTickLocal = now;
            LaunchLog.Write("Pre-alert countdown zero → FireDue");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OnPreAlertCountdownReachedZero: {ex.Message}");
        }
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
        _overlay.PreAlertCountdownReachedZero -= OnPreAlertCountdownReachedZero;
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch
        {
            // ignore
        }
        // Overlay/audio lifetime is owned by App.
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        void Run()
        {
            try
            {
                HandleResume();
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Resume handle failed: {ex.Message}");
            }
        }

        try
        {
            if (_dispatcher is not null && _dispatcher.TryEnqueue(Run))
            {
                return;
            }

            Run();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OnPowerModeChanged failed: {ex.Message}");
        }
    }

    private void HandleResume()
    {
        var now = ResolveLocalNow(out _, out _);
        var since = _lastTickLocal == default ? now.Date : _lastTickLocal;
        if (since > now)
        {
            since = now.Date;
        }

        RebuildPlan();
        ReportMissed(since, now);
        _lastTickLocal = now;
        LaunchLog.Write("NotificationHost handled resume");
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
                _missedReported.Clear();
                RebuildPlan();
            }

            // Large gap between ticks ≈ sleep/hibernate without PowerMode (or suspended process).
            if (_lastTickLocal != default && now - _lastTickLocal > TimeSpan.FromMinutes(3))
            {
                ReportMissed(_lastTickLocal, now);
            }

            FireDue(now);
            FireDueSpecialDays(now);
            FireDueUserReminders(now);
            UpdateTrayTooltip(location, now);
            _lastTickLocal = now;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"NotificationHost tick error: {ex.Message}");
        }
    }

    private void ReportMissedSinceDayStart()
    {
        try
        {
            var now = ResolveLocalNow(out _, out _);
            ReportMissed(now.Date, now);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ReportMissedSinceDayStart failed: {ex.Message}");
        }
    }

    private void ReportMissed(DateTimeOffset since, DateTimeOffset now)
    {
        if (!_state.Settings.ShowMissedAlertOnResume)
        {
            return;
        }

        try
        {
            var location = _state.RequireActiveLocation();
            var profile = _state.RequireCalculationProfile();
            var today = DateOnly.FromDateTime(now.DateTime);
            var schedule = _engine.Calculate(today, location, profile);
            var suppress = location.SuppressDhuhrNotificationsOnFriday;
            var missed = _planner.PlanMissed(
                schedule,
                _state.Settings.NotificationRules,
                now,
                since,
                suppressDhuhrOnFriday: suppress);

            // Personal v1: only prayer-start events count as “missed alert” (not pre-alerts).
            var starts = missed
                .Where(m => m.Kind == PlannedNotificationKind.PrayerStart)
                .Where(m => !_missedReported.Contains(m.Id) && !_fired.Contains(m.Id))
                .ToList();

            if (starts.Count > 0)
            {
                foreach (var m in starts)
                {
                    _missedReported.Add(m.Id);
                    _fired.Add(m.Id);
                }

                var title = _localization.Get("MissedAlertTitle");
                string body;
                if (starts.Count == 1)
                {
                    var one = starts[0];
                    body = string.Format(
                        _localization.Get("MissedAlertBodyOne"),
                        _localization.GetPrayerName(one.Prayer),
                        LatinDigits.Time(one.FireAt));
                }
                else
                {
                    var names = string.Join(
                        " · ",
                        starts.Select(s => _localization.GetPrayerName(s.Prayer)));
                    body = string.Format(
                        _localization.Get("MissedAlertBodyMany"),
                        LatinDigits.Number(starts.Count),
                        names);
                }

                _toasts.ShowText(title, body, tag: "missed-" + today.ToString("yyyyMMdd"));
                LaunchLog.Write($"Missed alert reported: {starts.Count} start(s)");
            }

            // Sibling path: special-day resume summary (toast only; reuses same master flag).
            ReportMissedSpecialDays(since, now, today);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ReportMissed failed: {ex.Message}");
        }
    }

    private void ReportMissedSpecialDays(DateTimeOffset since, DateTimeOffset now, DateOnly today)
    {
        try
        {
            var location = _state.RequireActiveLocation();
            var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            var country = CalendarCountryResolver.Resolve(
                _state.Settings.CalendarCountryOverride,
                location.CountryCode);
            var missed = SpecialDayReminderPlanner.PlanMissed(
                now,
                since,
                _state.Settings,
                tz,
                countryCode: country);

            var pending = missed
                .Where(m => !_missedReported.Contains(m.Id) && !_fired.Contains(m.Id))
                .ToList();
            if (pending.Count == 0)
            {
                return;
            }

            foreach (var m in pending)
            {
                _missedReported.Add(m.Id);
                _fired.Add(m.Id);
            }

            var title = _localization.Get("SpecialDayMissedTitle");
            string body;
            if (pending.Count == 1)
            {
                var one = pending[0];
                body = string.Format(
                    _localization.Get("SpecialDayMissedBodyOne"),
                    _localization.Get(one.PrimaryDisplayKey),
                    LatinDigits.Time(one.FireAt));
            }
            else
            {
                var names = string.Join(
                    " · ",
                    pending.Select(p => _localization.Get(p.PrimaryDisplayKey)));
                body = string.Format(
                    _localization.Get("SpecialDayMissedBodyMany"),
                    LatinDigits.Number(pending.Count),
                    names);
            }

            _toasts.ShowText(title, body, tag: "special-missed-" + today.ToString("yyyyMMdd"));
            LaunchLog.Write($"Special-day missed summary: {pending.Count}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ReportMissedSpecialDays failed: {ex.Message}");
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

            var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            var country = CalendarCountryResolver.Resolve(
                _state.Settings.CalendarCountryOverride,
                location.CountryCode);
            _specialPlan = SpecialDayReminderPlanner.Plan(now, _state.Settings, tz, country);
            _userPlan = UserCalendarReminderPlanner.Plan(now, _state.Settings, tz);

            _planDate = today;
            _fired.Clear();
            UpdateTrayTooltip(location, now);
            LaunchLog.Write(
                $"Plan rebuilt: {_plan.Count} prayer + {_specialPlan.Count} special + {_userPlan.Count} user for {today:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"RebuildPlan failed: {ex}");
            _plan = [];
            _specialPlan = [];
            _userPlan = [];
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

    private void FireDueSpecialDays(DateTimeOffset now)
    {
        foreach (var item in _specialPlan)
        {
            if (_fired.Contains(item.Id))
            {
                continue;
            }

            if (item.FireAt > now)
            {
                continue;
            }

            if (now - item.FireAt > TimeSpan.FromMinutes(2))
            {
                _fired.Add(item.Id);
                continue;
            }

            DispatchSpecialDay(item);
            _fired.Add(item.Id);
        }
    }

    private void FireDueUserReminders(DateTimeOffset now)
    {
        foreach (var item in _userPlan)
        {
            if (_fired.Contains(item.Id))
            {
                continue;
            }

            if (item.FireAt > now)
            {
                continue;
            }

            if (now - item.FireAt > TimeSpan.FromMinutes(2))
            {
                _fired.Add(item.Id);
                continue;
            }

            DispatchCalendarReminder(
                item.Id,
                item.Title,
                string.IsNullOrWhiteSpace(item.Note)
                    ? LatinDigits.Time(item.FireAt)
                    : item.Note + " · " + LatinDigits.Time(item.FireAt),
                item.Title,
                item.Note,
                LatinDigits.Time(item.FireAt));
            _fired.Add(item.Id);
        }
    }

    /// <summary>Special-day fire using configured calendar delivery (never prayer adhan overlay).</summary>
    private void DispatchSpecialDay(PlannedSpecialDayReminder item)
    {
        var title = _localization.Get("SpecialDayAlertTitle");
        var label = _localization.Get(item.PrimaryDisplayKey);
        var body = string.Format(
            _localization.Get("SpecialDayAlertBody"),
            label,
            LatinDigits.Time(item.FireAt));
        DispatchCalendarReminder(
            item.Id,
            title,
            body,
            label,
            body,
            LatinDigits.Time(item.FireAt));
        LaunchLog.Write($"Special-day fire: {item.Id}");
    }

    private void DispatchCalendarReminder(
        string tag,
        string toastTitle,
        string toastBody,
        string windowTitle,
        string windowNote,
        string timeLabel)
    {
        var delivery = _state.Settings.CalendarReminderDelivery;
        if (delivery == CalendarReminderDelivery.None)
        {
            delivery = CalendarReminderDelivery.WindowsNotification;
        }

        if (delivery.HasFlag(CalendarReminderDelivery.WindowsNotification))
        {
            _toasts.ShowText(toastTitle, toastBody, tag: tag);
        }

        if (delivery.HasFlag(CalendarReminderDelivery.Sound))
        {
            try
            {
                var cue = SoundLibraryService.ProfileForSoundId(_state.Settings.PreAlertSoundId);
                if (cue.Source != AudioSource.Silent)
                {
                    _audio.Play(cue);
                }
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Calendar reminder sound failed: {ex.Message}");
            }
        }

        if (delivery.HasFlag(CalendarReminderDelivery.SilentWindow))
        {
            try
            {
                void Show()
                {
                    var win = new SilentReminderWindow(windowTitle, windowNote, timeLabel);
                    win.Activate();
                }

                if (_dispatcher is not null && _dispatcher.TryEnqueue(Show))
                {
                    return;
                }

                Show();
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"Silent reminder window failed: {ex.Message}");
            }
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
                ? _localization.Get("NextAdhan")
                : _localization.Get("AdhanTime");
            _toasts.Show(item, prayerName, title);
        }

        // Pre-alert plays the library cue only when Audio channel is enabled (user choice).
        // Prayer-start plays adhan only when the Audio channel is enabled.
        var wantsPreAlertCue = item.Kind == PlannedNotificationKind.BeforePrayer
                               && wantsAudio
                               && !string.Equals(
                                   _state.Settings.PreAlertSoundId,
                                   BuiltInSoundIds.Silent,
                                   StringComparison.OrdinalIgnoreCase);

        var audio = ResolveAudio(item, wantsAudio: wantsAudio || wantsPreAlertCue);

        // Overlay service owns audio when both are requested (stop button stops adhan).
        if (wantsOverlay)
        {
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
            else if (audio.Source != AudioSource.Silent)
            {
                _audio.Play(audio);
            }
        }
        else if (audio.Source != AudioSource.Silent)
        {
            _audio.Play(audio);
        }

        if (item.Kind == PlannedNotificationKind.PrayerStart)
        {
            try
            {
                PrayerStartDispatched?.Invoke(this, item.Prayer);
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"PrayerStartDispatched handler failed: {ex.Message}");
            }
        }
    }

    private AudioProfile ResolveAudio(PlannedNotification item, bool wantsAudio)
    {
        if (!wantsAudio)
        {
            return new AudioProfile { Source = AudioSource.Silent };
        }

        if (item.Kind == PlannedNotificationKind.BeforePrayer)
        {
            // Pre-alert cue (takbir / hayya / soft / user) — not the full adhan.
            return SoundLibraryService.ProfileForSoundId(_state.Settings.PreAlertSoundId);
        }

        if (item.Kind != PlannedNotificationKind.PrayerStart)
        {
            return new AudioProfile { Source = AudioSource.Silent };
        }

        // Prefer explicit library selection; then per-rule audio; then DefaultAudio.
        var adhanId = _state.Settings.AdhanSoundId;
        if (!string.IsNullOrWhiteSpace(adhanId))
        {
            return SoundLibraryService.ProfileForSoundId(adhanId);
        }

        var ruleAudio = _state.Settings.NotificationRules
            .FirstOrDefault(r =>
                r.Enabled
                && r.Kind == NotificationEventKind.PrayerStart
                && r.Audio is not null)?.Audio;

        return ruleAudio
               ?? _state.Settings.DefaultAudio
               ?? SoundLibraryService.ProfileForSoundId(BuiltInSoundIds.AdhanAlaqsa);
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
