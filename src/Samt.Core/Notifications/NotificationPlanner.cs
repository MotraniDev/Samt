using Samt.Core.Domain;
using Samt.Core.Formatting;

namespace Samt.Core.Notifications;

public enum PlannedNotificationKind
{
    BeforePrayer = 0,
    PrayerStart = 1
}

/// <summary>One fire opportunity for the notification host (App layer).</summary>
public sealed class PlannedNotification
{
    public required string Id { get; init; }
    public required DateTimeOffset FireAt { get; init; }
    public required PlannedNotificationKind Kind { get; init; }
    public required PrayerEvent Prayer { get; init; }
    public required NotificationChannel Channels { get; init; }
    public int? OffsetMinutes { get; init; }

    /// <summary>Latin-digit local time for toast body builders.</summary>
    public string FireAtDisplayTime => LatinDigits.Time(FireAt);
}

/// <summary>
/// Builds today's notification fire list from a schedule and rules.
/// Pure Core — no UI, no Windows APIs.
/// </summary>
public sealed class NotificationPlanner
{
    public IReadOnlyList<PlannedNotification> Plan(
        PrayerSchedule schedule,
        IEnumerable<NotificationRule> rules,
        DateTimeOffset now,
        bool suppressDhuhrOnFriday = true)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(rules);

        var results = new List<PlannedNotification>();
        var isFriday = schedule.Date.DayOfWeek == DayOfWeek.Friday;

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (rule.Kind is not (NotificationEventKind.BeforePrayer or NotificationEventKind.PrayerStart))
            {
                continue;
            }

            foreach (var prayer in ResolveTargets(rule, schedule, isFriday, suppressDhuhrOnFriday))
            {
                if (!schedule.Times.TryGetValue(prayer, out var prayerTime))
                {
                    continue;
                }

                DateTimeOffset fireAt;
                int? offset = null;

                if (rule.Kind == NotificationEventKind.BeforePrayer)
                {
                    var minutes = rule.OffsetMinutes ?? 0;
                    if (minutes <= 0)
                    {
                        continue;
                    }

                    offset = minutes;
                    fireAt = prayerTime.AddMinutes(-minutes);
                }
                else
                {
                    fireAt = prayerTime;
                }

                if (fireAt <= now)
                {
                    continue;
                }

                results.Add(new PlannedNotification
                {
                    Id = $"{schedule.Date:yyyyMMdd}-{prayer}-{rule.Kind}-{offset ?? 0}",
                    FireAt = fireAt,
                    Kind = rule.Kind == NotificationEventKind.BeforePrayer
                        ? PlannedNotificationKind.BeforePrayer
                        : PlannedNotificationKind.PrayerStart,
                    Prayer = prayer,
                    Channels = rule.Channels,
                    OffsetMinutes = offset
                });
            }
        }

        return results
            .OrderBy(n => n.FireAt)
            .ThenBy(n => n.Prayer)
            .ToList();
    }

    private static IEnumerable<PrayerEvent> ResolveTargets(
        NotificationRule rule,
        PrayerSchedule schedule,
        bool isFriday,
        bool suppressDhuhrOnFriday)
    {
        IEnumerable<PrayerEvent> targets = rule.TargetPrayers.Count > 0
            ? rule.TargetPrayers
            : PrayerSchedule.NotifiablePrayers.Where(p => p != PrayerEvent.Jumuah);

        foreach (var prayer in targets)
        {
            if (isFriday && suppressDhuhrOnFriday && prayer == PrayerEvent.Dhuhr
                && schedule.Times.ContainsKey(PrayerEvent.Jumuah))
            {
                continue;
            }

            if (prayer == PrayerEvent.Jumuah && !isFriday)
            {
                continue;
            }

            yield return prayer;
        }
    }
}
