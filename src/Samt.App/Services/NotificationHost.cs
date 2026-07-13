using Microsoft.UI.Xaml;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Notifications;
using Samt.Core.Time;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Rebuilds today's notification plan and fires due toasts.
/// Reschedules on settings change, day rollover, and a 15s poll.
/// </summary>
public sealed class NotificationHost : IDisposable
{
    private readonly AppState _state;
    private readonly LocalizationService _localization;
    private readonly ToastNotificationService _toasts;
    private readonly TrayIconService _tray;
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
        TrayIconService tray)
    {
        _state = state;
        _localization = localization;
        _toasts = toasts;
        _tray = tray;
    }

    public void Start()
    {
        _state.SettingsChanged += OnSettingsChanged;
        _timer.Tick += OnTick;
        RebuildPlan();
        _timer.Start();
        LaunchLog.Write($"NotificationHost started with {_plan.Count} planned events");
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
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => RebuildPlan();

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

            if (item.Channels.HasFlag(NotificationChannel.WindowsToast)
                || item.Channels == NotificationChannel.All)
            {
                var prayerName = _localization.GetPrayerName(item.Prayer);
                var title = item.Kind == PlannedNotificationKind.BeforePrayer
                    ? _localization.Get("NextPrayer")
                    : prayerName;
                _toasts.Show(item, prayerName, title);
            }

            _fired.Add(item.Id);
        }
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
