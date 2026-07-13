using Samt.Core.Domain;

namespace Samt.Core.Calculation;

public static class PrayerTimeRounder
{
    public static DateTimeOffset Round(DateTimeOffset value, RoundMode mode)
    {
        var local = value;
        return mode switch
        {
            RoundMode.FloorMinute => FloorToMinute(local),
            RoundMode.CeilMinute => CeilToMinute(local),
            _ => RoundNearestMinute(local)
        };
    }

    public static DateTimeOffset ApplyAdjustment(DateTimeOffset value, int minutes)
        => value.AddMinutes(minutes);

    private static DateTimeOffset RoundNearestMinute(DateTimeOffset value)
    {
        var truncated = FloorToMinute(value);
        return value.Second >= 30 ? truncated.AddMinutes(1) : truncated;
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value)
        => new(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            0,
            value.Offset);

    private static DateTimeOffset CeilToMinute(DateTimeOffset value)
    {
        var floor = FloorToMinute(value);
        return value == floor ? floor : floor.AddMinutes(1);
    }
}
