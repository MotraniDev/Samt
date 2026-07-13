using Samt.Core.Calendar;
using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Storage;

namespace Samt.Core.Tests;

public class UserCalendarReminderPlannerTests
{
    private static readonly TimeZoneInfo AlgeriaTz =
        KnownLocations.ResolveTimeZone(KnownLocations.AlgeriaTimeZoneId);

    [Fact]
    public void Plan_ExpandsRepeatsWithInterval()
    {
        var day = new DateOnly(2025, 6, 15);
        var now = AtLocal(day, 8, 0);
        var settings = SettingsJson.CreateDefault().With(
            userCalendarReminders:
            [
                new UserCalendarReminder
                {
                    Id = Guid.NewGuid(),
                    Title = "Water plants",
                    Note = "Balcony",
                    CivilDate = day,
                    Time = "09:00",
                    RepeatCount = 3,
                    IntervalMinutes = 10,
                    Enabled = true
                }
            ]);

        var plan = UserCalendarReminderPlanner.Plan(now, settings, AlgeriaTz);
        Assert.Equal(3, plan.Count);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(plan[0].FireAt.DateTime));
        Assert.Equal(new TimeOnly(9, 10), TimeOnly.FromDateTime(plan[1].FireAt.DateTime));
        Assert.Equal(new TimeOnly(9, 20), TimeOnly.FromDateTime(plan[2].FireAt.DateTime));
        Assert.All(plan, p => Assert.Equal("Water plants", p.Title));
    }

    [Fact]
    public void Plan_IgnoresOtherDaysAndDisabled()
    {
        var day = new DateOnly(2025, 6, 15);
        var now = AtLocal(day, 8, 0);
        var settings = SettingsJson.CreateDefault().With(
            userCalendarReminders:
            [
                new UserCalendarReminder
                {
                    Id = Guid.NewGuid(),
                    Title = "Tomorrow",
                    CivilDate = day.AddDays(1),
                    Time = "09:00",
                    Enabled = true
                },
                new UserCalendarReminder
                {
                    Id = Guid.NewGuid(),
                    Title = "Off",
                    CivilDate = day,
                    Time = "09:00",
                    Enabled = false
                }
            ]);

        Assert.Empty(UserCalendarReminderPlanner.Plan(now, settings, AlgeriaTz));
    }

    [Fact]
    public void Plan_PastOccurrencesExcluded()
    {
        var day = new DateOnly(2025, 6, 15);
        var now = AtLocal(day, 9, 15);
        var settings = SettingsJson.CreateDefault().With(
            userCalendarReminders:
            [
                new UserCalendarReminder
                {
                    Id = Guid.NewGuid(),
                    Title = "Meds",
                    CivilDate = day,
                    Time = "09:00",
                    RepeatCount = 3,
                    IntervalMinutes = 30,
                    Enabled = true
                }
            ]);

        // 09:00 past; 09:30 and 10:00 future
        var plan = UserCalendarReminderPlanner.Plan(now, settings, AlgeriaTz);
        Assert.Equal(2, plan.Count);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(plan[0].FireAt.DateTime));
    }

    private static DateTimeOffset AtLocal(DateOnly civil, int hour, int minute)
    {
        var local = civil.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, AlgeriaTz.GetUtcOffset(local));
    }
}
