using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Notifications;

namespace Samt.Core.Tests;

public class NotificationPlannerTests
{
    private readonly PrayerEngine _engine = new();
    private readonly NotificationPlanner _planner = new();

    [Fact]
    public void PrayerStart_SchedulesFuturePrayersOnly()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = schedule.Dhuhr.AddMinutes(-30);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = [PrayerEvent.Fajr, PrayerEvent.Dhuhr, PrayerEvent.Asr, PrayerEvent.Maghrib, PrayerEvent.Isha],
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, now);

        Assert.DoesNotContain(plan, p => p.Prayer == PrayerEvent.Fajr);
        Assert.Contains(plan, p => p.Prayer == PrayerEvent.Dhuhr && p.Kind == PlannedNotificationKind.PrayerStart);
        Assert.All(plan, p => Assert.True(p.FireAt > now));
        Assert.All(plan, p => Assert.False(LatinDigits.ContainsIndicDigits(p.FireAtDisplayTime)));
    }

    [Fact]
    public void BeforePrayer_UsesOffsetMinutes()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = schedule.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var nowOffset = new DateTimeOffset(now, schedule.Fajr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Maghrib],
                OffsetMinutes = 15,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, nowOffset);
        var item = Assert.Single(plan);
        Assert.Equal(PlannedNotificationKind.BeforePrayer, item.Kind);
        Assert.Equal(15, item.OffsetMinutes);
        Assert.Equal(schedule.Maghrib.AddMinutes(-15), item.FireAt);
    }

    [Fact]
    public void Friday_SuppressesDhuhrWhenJumuahPresent()
    {
        var date = new DateOnly(2025, 6, 6); // Friday
        Assert.Equal(DayOfWeek.Friday, date.DayOfWeek);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Dhuhr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = [PrayerEvent.Dhuhr, PrayerEvent.Jumuah],
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, now, suppressDhuhrOnFriday: true);
        Assert.DoesNotContain(plan, p => p.Prayer == PrayerEvent.Dhuhr);
        Assert.Contains(plan, p => p.Prayer == PrayerEvent.Jumuah);
    }

    [Fact]
    public void DisabledRules_AreIgnored()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Fajr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = [PrayerEvent.Fajr],
                Channels = NotificationChannel.WindowsToast,
                Enabled = false
            }
        };

        var plan = _planner.Plan(schedule, rules, now);
        Assert.Empty(plan);
    }
}
