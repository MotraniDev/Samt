using System.Globalization;
using Samt.Core.Domain;

namespace Samt.Core.Calendar;

/// <summary>One fire of a user calendar reminder for the App host.</summary>
public sealed class PlannedUserCalendarReminder
{
    public required string Id { get; init; }

    public required Guid ReminderId { get; init; }

    public required DateTimeOffset FireAt { get; init; }

    public required string Title { get; init; }

    public required string Note { get; init; }

    public required int OccurrenceIndex { get; init; }

    public required int OccurrenceCount { get; init; }
}

/// <summary>
/// Expands user calendar reminders into fire times for the civil day of <c>now</c>.
/// Pure Core — no UI.
/// </summary>
public static class UserCalendarReminderPlanner
{
    public static IReadOnlyList<PlannedUserCalendarReminder> Plan(
        DateTimeOffset now,
        AppSettings settings,
        TimeZoneInfo locationTimeZone)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(locationTimeZone);

        return Build(now, settings, locationTimeZone)
            .Where(r => r.FireAt > now)
            .OrderBy(r => r.FireAt)
            .ToList();
    }

    public static IReadOnlyList<PlannedUserCalendarReminder> PlanMissed(
        DateTimeOffset now,
        DateTimeOffset since,
        AppSettings settings,
        TimeZoneInfo locationTimeZone,
        TimeSpan? lateGrace = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(locationTimeZone);

        var grace = lateGrace ?? TimeSpan.FromMinutes(2);
        var upper = now - grace;
        if (upper <= since)
        {
            return [];
        }

        return Build(now, settings, locationTimeZone)
            .Where(r => r.FireAt > since && r.FireAt <= upper)
            .OrderBy(r => r.FireAt)
            .ToList();
    }

    private static IReadOnlyList<PlannedUserCalendarReminder> Build(
        DateTimeOffset now,
        AppSettings settings,
        TimeZoneInfo locationTimeZone)
    {
        var reminders = settings.UserCalendarReminders;
        if (reminders is null || reminders.Count == 0)
        {
            return [];
        }

        var localNow = TimeZoneInfo.ConvertTime(now, locationTimeZone);
        var civilToday = DateOnly.FromDateTime(localNow.DateTime);
        var results = new List<PlannedUserCalendarReminder>();

        foreach (var reminder in reminders)
        {
            if (reminder is null || !reminder.Enabled)
            {
                continue;
            }

            if (reminder.CivilDate != civilToday)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(reminder.Title))
            {
                continue;
            }

            if (!TryParseTime(reminder.Time, out var clock))
            {
                clock = new TimeOnly(9, 0);
            }

            var count = Math.Clamp(reminder.RepeatCount, 1, 20);
            var interval = Math.Clamp(reminder.IntervalMinutes, 0, 1440);
            if (count > 1 && interval <= 0)
            {
                interval = 5;
            }

            var title = reminder.Title.Trim();
            var note = reminder.Note?.Trim() ?? "";

            for (var i = 0; i < count; i++)
            {
                var minutes = i == 0 ? 0 : interval * i;
                var local = civilToday.ToDateTime(clock, DateTimeKind.Unspecified).AddMinutes(minutes);
                var fireAt = new DateTimeOffset(local, locationTimeZone.GetUtcOffset(local));
                results.Add(new PlannedUserCalendarReminder
                {
                    Id = $"user:{reminder.Id:N}:{i}",
                    ReminderId = reminder.Id,
                    FireAt = fireAt,
                    Title = title,
                    Note = note,
                    OccurrenceIndex = i + 1,
                    OccurrenceCount = count
                });
            }
        }

        return results;
    }

    private static bool TryParseTime(string? value, out TimeOnly clock)
    {
        clock = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, out clock)
               || TimeOnly.TryParse(trimmed, out clock);
    }
}
