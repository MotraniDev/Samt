using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Notifications;
using Samt.Core.Calculation;

namespace Samt.Core.Tests;

public class NotificationRulesComposerTests
{
    private readonly PrayerEngine _engine = new();
    private readonly NotificationPlanner _planner = new();

    [Fact]
    public void Compose_Fajr30_Others15_PlansCorrectOffsets()
    {
        var rules = NotificationRulesComposer.Compose(
            generalBeforeMinutes: 15,
            beforeExceptions: new Dictionary<PrayerEvent, int?>
            {
                [PrayerEvent.Fajr] = 30
            },
            beforeEnabledPrayers: new HashSet<PrayerEvent>(NotificationRulesComposer.FiveDaily),
            startEnabledPrayers: new HashSet<PrayerEvent>(NotificationRulesComposer.FiveDaily),
            beforeChannels: NotificationChannel.WindowsToast,
            startChannels: NotificationChannel.All);

        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var now = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), schedule.Fajr.Offset);
        var plan = _planner.Plan(schedule, rules, now);

        Assert.Equal(30, Assert.Single(plan, p => p.Prayer == PrayerEvent.Fajr && p.Kind == PlannedNotificationKind.BeforePrayer).OffsetMinutes);
        Assert.Equal(15, Assert.Single(plan, p => p.Prayer == PrayerEvent.Maghrib && p.Kind == PlannedNotificationKind.BeforePrayer).OffsetMinutes);
    }

    [Fact]
    public void Parse_RoundTrips_ComposerOutput()
    {
        var rules = NotificationRulesComposer.Compose(
            generalBeforeMinutes: 20,
            beforeExceptions: new Dictionary<PrayerEvent, int?>
            {
                [PrayerEvent.Fajr] = 45,
                [PrayerEvent.Isha] = null
            },
            beforeEnabledPrayers: new HashSet<PrayerEvent>(NotificationRulesComposer.FiveDaily),
            startEnabledPrayers: new HashSet<PrayerEvent> { PrayerEvent.Fajr, PrayerEvent.Maghrib },
            beforeChannels: NotificationChannel.WindowsToast | NotificationChannel.Overlay,
            startChannels: NotificationChannel.All,
            beforeAlertsEnabled: true,
            startAlertsEnabled: true);

        var model = NotificationRulesComposer.Parse(rules);
        Assert.Equal(20, model.GeneralBeforeMinutes);
        Assert.Equal(45, model.BeforeExceptions[PrayerEvent.Fajr]);
        Assert.Null(model.BeforeExceptions[PrayerEvent.Isha]);
        Assert.DoesNotContain(PrayerEvent.Isha, model.BeforeEnabledPrayers);
        Assert.True(model.StartEnabledPrayers.SetEquals([PrayerEvent.Fajr, PrayerEvent.Maghrib]));
    }
}
