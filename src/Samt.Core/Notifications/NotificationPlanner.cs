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
/// <remarks>
/// Priority: more specific <see cref="NotificationRule.TargetPrayers"/> wins
/// (smaller non-empty count). Empty targets = least specific (all notifiable).
/// A disabled specific rule cancels that prayer (no fall-back to general).
/// On Friday with suppress-Dhuhr, Dhuhr targets remap to Jumu‘ah.
/// </remarks>
public sealed class NotificationPlanner
{
    /// <summary>Future fire opportunities after <paramref name="now"/>.</summary>
    public IReadOnlyList<PlannedNotification> Plan(
        PrayerSchedule schedule,
        IEnumerable<NotificationRule> rules,
        DateTimeOffset now,
        bool suppressDhuhrOnFriday = true)
        => Build(schedule, rules, suppressDhuhrOnFriday)
            .Where(n => n.FireAt > now)
            .OrderBy(n => n.FireAt)
            .ThenBy(n => n.Prayer)
            .ToList();

    /// <summary>
    /// Events that should already have fired in <c>(since, now − grace]</c>.
    /// Used for “missed while asleep / late start” summaries (no late adhan).
    /// </summary>
    public IReadOnlyList<PlannedNotification> PlanMissed(
        PrayerSchedule schedule,
        IEnumerable<NotificationRule> rules,
        DateTimeOffset now,
        DateTimeOffset since,
        TimeSpan? lateGrace = null,
        bool suppressDhuhrOnFriday = true)
    {
        var grace = lateGrace ?? TimeSpan.FromMinutes(2);
        var upper = now - grace;
        if (upper <= since)
        {
            return [];
        }

        return Build(schedule, rules, suppressDhuhrOnFriday)
            .Where(n => n.FireAt > since && n.FireAt <= upper)
            .OrderBy(n => n.FireAt)
            .ThenBy(n => n.Prayer)
            .ToList();
    }

    private static IReadOnlyList<PlannedNotification> Build(
        PrayerSchedule schedule,
        IEnumerable<NotificationRule> rules,
        bool suppressDhuhrOnFriday)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(rules);

        var ruleList = rules.ToList();
        var isFriday = schedule.Date.DayOfWeek == DayOfWeek.Friday;
        var results = new List<PlannedNotification>();

        foreach (var kind in new[] { NotificationEventKind.BeforePrayer, NotificationEventKind.PrayerStart })
        {
            foreach (var logicalPrayer in CandidatePrayers(ruleList, kind))
            {
                var effectivePrayer = RemapFridayPrayer(
                    logicalPrayer,
                    schedule,
                    isFriday,
                    suppressDhuhrOnFriday);

                if (effectivePrayer is null)
                {
                    continue;
                }

                if (!schedule.Times.TryGetValue(effectivePrayer.Value, out var prayerTime))
                {
                    continue;
                }

                var winner = SelectWinningRule(ruleList, kind, logicalPrayer);
                if (winner is null || !winner.Enabled)
                {
                    continue;
                }

                DateTimeOffset fireAt;
                int? offset = null;

                if (kind == NotificationEventKind.BeforePrayer)
                {
                    var minutes = winner.OffsetMinutes ?? 0;
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

                results.Add(new PlannedNotification
                {
                    Id = $"{schedule.Date:yyyyMMdd}-{effectivePrayer}-{kind}-{offset ?? 0}",
                    FireAt = fireAt,
                    Kind = kind == NotificationEventKind.BeforePrayer
                        ? PlannedNotificationKind.BeforePrayer
                        : PlannedNotificationKind.PrayerStart,
                    Prayer = effectivePrayer.Value,
                    Channels = winner.Channels,
                    OffsetMinutes = offset
                });
            }
        }

        // Deduplicate: Friday remap or overlapping rules can produce same (kind, prayer).
        return results
            .GroupBy(n => (n.Kind, n.Prayer))
            .Select(g => g.OrderBy(n => n.FireAt).First())
            .ToList();
    }

    private static IEnumerable<PrayerEvent> CandidatePrayers(
        IReadOnlyList<NotificationRule> rules,
        NotificationEventKind kind)
    {
        var set = new HashSet<PrayerEvent>();
        foreach (var rule in rules.Where(r => r.Kind == kind))
        {
            foreach (var p in ExpandTargets(rule))
            {
                set.Add(p);
            }
        }

        return set.OrderBy(p => p);
    }

    private static NotificationRule? SelectWinningRule(
        IReadOnlyList<NotificationRule> rules,
        NotificationEventKind kind,
        PrayerEvent logicalPrayer)
    {
        var candidates = rules
            .Where(r => r.Kind == kind)
            .Where(r => ExpandTargets(r).Contains(logicalPrayer))
            .Select(r => (Rule: r, Score: SpecificityScore(r)))
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Rule.Id)
            .ToList();

        return candidates.Count == 0 ? null : candidates[0].Rule;
    }

    /// <summary>Lower score = more specific. Empty targets = least specific.</summary>
    internal static int SpecificityScore(NotificationRule rule)
        => rule.TargetPrayers.Count == 0 ? int.MaxValue : rule.TargetPrayers.Count;

    private static IEnumerable<PrayerEvent> ExpandTargets(NotificationRule rule)
        => rule.TargetPrayers.Count > 0
            ? rule.TargetPrayers
            : PrayerSchedule.NotifiablePrayers.Where(p => p != PrayerEvent.Jumuah);

    private static PrayerEvent? RemapFridayPrayer(
        PrayerEvent prayer,
        PrayerSchedule schedule,
        bool isFriday,
        bool suppressDhuhrOnFriday)
    {
        if (prayer == PrayerEvent.Jumuah)
        {
            if (!isFriday || !schedule.Times.ContainsKey(PrayerEvent.Jumuah))
            {
                return null;
            }

            return PrayerEvent.Jumuah;
        }

        if (prayer == PrayerEvent.Dhuhr
            && isFriday
            && suppressDhuhrOnFriday
            && schedule.Times.ContainsKey(PrayerEvent.Jumuah))
        {
            return PrayerEvent.Jumuah;
        }

        return prayer;
    }
}
