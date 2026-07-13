using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Time;

namespace Samt.Core.Tests;

public class PrayerTimelineTests
{
    [Fact]
    public void GetNext_ReturnsUpcomingNotifiablePrayer()
    {
        var engine = new PrayerEngine();
        var date = new DateOnly(2025, 1, 15);
        var schedule = engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var afterFajr = schedule.Fajr.AddMinutes(1);

        var next = PrayerTimeline.GetNext(schedule, afterFajr);
        Assert.NotNull(next);
        Assert.Equal(PrayerEvent.Dhuhr, next!.Event);
        Assert.True(next.Remaining > TimeSpan.Zero);
    }

    [Fact]
    public void GetNext_AfterIsha_ReturnsNull()
    {
        var engine = new PrayerEngine();
        var date = new DateOnly(2025, 1, 15);
        var schedule = engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var afterIsha = schedule.Isha.AddMinutes(1);

        var next = PrayerTimeline.GetNext(schedule, afterIsha);
        Assert.Null(next);
    }
}
