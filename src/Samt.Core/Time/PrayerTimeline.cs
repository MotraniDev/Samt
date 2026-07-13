using Samt.Core.Domain;
using Samt.Core.Formatting;

namespace Samt.Core.Time;

public sealed record NextPrayerInfo(
    PrayerEvent Event,
    DateTimeOffset Time,
    TimeSpan Remaining);

public static class PrayerTimeline
{
    /// <summary>
    /// Next notifiable prayer for the given schedule relative to <paramref name="now"/>.
    /// If the day is finished, returns null (caller may load tomorrow).
    /// </summary>
    public static NextPrayerInfo? GetNext(PrayerSchedule schedule, DateTimeOffset now)
    {
        NextPrayerInfo? best = null;

        foreach (var key in PrayerSchedule.NotifiablePrayers)
        {
            if (key == PrayerEvent.Jumuah && schedule.Date.DayOfWeek != DayOfWeek.Friday)
            {
                continue;
            }

            if (!schedule.Times.TryGetValue(key, out var time))
            {
                continue;
            }

            // On Friday with Jumu'ah, skip Dhuhr as "next" if Jumu'ah is present (same or custom).
            if (key == PrayerEvent.Dhuhr
                && schedule.Date.DayOfWeek == DayOfWeek.Friday
                && schedule.Times.ContainsKey(PrayerEvent.Jumuah))
            {
                continue;
            }

            if (time <= now)
            {
                continue;
            }

            var remaining = time - now;
            if (best is null || time < best.Time)
            {
                best = new NextPrayerInfo(key, time, remaining);
            }
        }

        return best;
    }

    public static string FormatCountdown(TimeSpan remaining)
        => LatinDigits.Duration(remaining);

    /// <summary>Always HH:MM:SS for alert overlays (pre-adhan live timer).</summary>
    public static string FormatCountdownHms(TimeSpan remaining)
        => LatinDigits.DurationHms(remaining);
}
