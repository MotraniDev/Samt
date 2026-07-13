using Samt.Core.Calculation;
using Samt.Core.Domain;

namespace Samt.Core.Tests;

public class PrayerTimeRounderTests
{
    [Theory]
    [InlineData(10, 15, 29, 10, 15)]
    [InlineData(10, 15, 30, 10, 16)]
    [InlineData(10, 15, 59, 10, 16)]
    public void NearestMinute_RoundsAsExpected(int h, int m, int s, int eh, int em)
    {
        var input = new DateTimeOffset(2025, 1, 1, h, m, s, TimeSpan.FromHours(1));
        var rounded = PrayerTimeRounder.Round(input, RoundMode.NearestMinute);
        Assert.Equal(new TimeOnly(eh, em), TimeOnly.FromTimeSpan(rounded.TimeOfDay));
        Assert.Equal(0, rounded.Second);
    }

    [Fact]
    public void FloorMinute_NeverAdvances()
    {
        var input = new DateTimeOffset(2025, 1, 1, 5, 10, 59, TimeSpan.Zero);
        var rounded = PrayerTimeRounder.Round(input, RoundMode.FloorMinute);
        Assert.Equal(new TimeOnly(5, 10), TimeOnly.FromTimeSpan(rounded.TimeOfDay));
    }

    [Fact]
    public void CeilMinute_AdvancesWhenSecondsPresent()
    {
        var input = new DateTimeOffset(2025, 1, 1, 5, 10, 1, TimeSpan.Zero);
        var rounded = PrayerTimeRounder.Round(input, RoundMode.CeilMinute);
        Assert.Equal(new TimeOnly(5, 11), TimeOnly.FromTimeSpan(rounded.TimeOfDay));
    }
}
