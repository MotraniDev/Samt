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

    [Fact]
    public void BeforePrayer_SpecificException_OverridesGeneralOffset()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Fajr.Offset);

        var five = new[]
        {
            PrayerEvent.Fajr, PrayerEvent.Dhuhr, PrayerEvent.Asr, PrayerEvent.Maghrib, PrayerEvent.Isha
        };

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = five,
                OffsetMinutes = 15,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            },
            new NotificationRule
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Fajr],
                OffsetMinutes = 30,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, now);
        var fajr = Assert.Single(plan, p => p.Prayer == PrayerEvent.Fajr);
        Assert.Equal(30, fajr.OffsetMinutes);
        Assert.Equal(schedule.Fajr.AddMinutes(-30), fajr.FireAt);

        var maghrib = Assert.Single(plan, p => p.Prayer == PrayerEvent.Maghrib);
        Assert.Equal(15, maghrib.OffsetMinutes);
        Assert.Equal(schedule.Maghrib.AddMinutes(-15), maghrib.FireAt);

        Assert.Equal(5, plan.Count(p => p.Kind == PlannedNotificationKind.BeforePrayer));
    }

    [Fact]
    public void BeforePrayer_DisabledSpecific_CancelsPrayer_DoesNotFallBack()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Fajr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Fajr, PrayerEvent.Maghrib],
                OffsetMinutes = 15,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            },
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Fajr],
                OffsetMinutes = 30,
                Channels = NotificationChannel.WindowsToast,
                Enabled = false
            }
        };

        var plan = _planner.Plan(schedule, rules, now);
        Assert.DoesNotContain(plan, p => p.Prayer == PrayerEvent.Fajr);
        Assert.Contains(plan, p => p.Prayer == PrayerEvent.Maghrib);
    }

    [Fact]
    public void Friday_RemapsDhuhrTargetToJumuah_WhenSuppressing()
    {
        var date = new DateOnly(2025, 6, 6);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Dhuhr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.NewGuid(),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = [PrayerEvent.Dhuhr],
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, now, suppressDhuhrOnFriday: true);
        Assert.DoesNotContain(plan, p => p.Prayer == PrayerEvent.Dhuhr);
        Assert.Contains(plan, p => p.Prayer == PrayerEvent.Jumuah && p.Kind == PlannedNotificationKind.PrayerStart);
        Assert.Single(plan);
    }

    [Fact]
    public void ConflictingSameSpecificity_DedupesToSingleFire()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Fajr.Offset);

        var rules = new[]
        {
            new NotificationRule
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Asr],
                OffsetMinutes = 10,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            },
            new NotificationRule
            {
                Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Asr],
                OffsetMinutes = 20,
                Channels = NotificationChannel.WindowsToast | NotificationChannel.Overlay,
                Enabled = true
            }
        };

        var plan = _planner.Plan(schedule, rules, now);
        var asr = Assert.Single(plan, p => p.Prayer == PrayerEvent.Asr);
        // Stable pick: lower rule Id wins when specificity ties.
        Assert.Equal(10, asr.OffsetMinutes);
        Assert.Equal(NotificationChannel.WindowsToast, asr.Channels);
    }
}
