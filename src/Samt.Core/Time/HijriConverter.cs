using System.Globalization;

namespace Samt.Core.Time;

/// <summary>
/// Gregorian ↔ tabular Hijri via BCL <see cref="HijriCalendar"/>.
/// Apply <paramref name="dayOffset"/> to match local moon-sighting habits (see <c>AppSettings.HijriDayOffset</c>).
/// </summary>
public static class HijriConverter
{
    /// <summary>Recommended clamp for UI (±3 days covers common sighting variance).</summary>
    public const int MinDayOffset = -3;
    public const int MaxDayOffset = 3;

    public static int ClampDayOffset(int dayOffset)
        => Math.Clamp(dayOffset, MinDayOffset, MaxDayOffset);

    /// <summary>Convert a civil Gregorian date to Hijri after shifting by <paramref name="dayOffset"/> days.</summary>
    public static HijriDate FromGregorian(DateOnly date, int dayOffset = 0)
    {
        var cal = new HijriCalendar();
        var dt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)
            .AddDays(dayOffset);

        return new HijriDate(
            cal.GetYear(dt),
            cal.GetMonth(dt),
            cal.GetDayOfMonth(dt));
    }

    /// <summary>
    /// Convert a tabular Hijri date to the civil Gregorian date that <see cref="FromGregorian"/>
    /// would map to this Hijri day under the same <paramref name="dayOffset"/>.
    /// </summary>
    public static DateOnly ToGregorian(HijriDate hijri, int dayOffset = 0)
    {
        var cal = new HijriCalendar();
        var dt = cal.ToDateTime(hijri.Year, hijri.Month, hijri.Day, 0, 0, 0, 0);
        // Inverse of FromGregorian: civil = HijriInstant − offset days.
        return DateOnly.FromDateTime(dt.AddDays(-dayOffset));
    }

    /// <summary>Days in the given tabular Hijri month (29 or 30).</summary>
    public static int GetDaysInMonth(int hijriYear, int hijriMonth)
    {
        var cal = new HijriCalendar();
        return cal.GetDaysInMonth(hijriYear, hijriMonth);
    }

    /// <summary>True when the adjusted Hijri month is Ramadan (9).</summary>
    public static bool IsRamadan(DateOnly gregorianDate, int dayOffset = 0)
        => FromGregorian(gregorianDate, dayOffset).IsRamadan;
}
