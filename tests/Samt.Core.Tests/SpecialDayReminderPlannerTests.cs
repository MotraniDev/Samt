using Samt.Core.Calendar;
using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Storage;
using Samt.Core.Time;

namespace Samt.Core.Tests;

public class SpecialDayReminderPlannerTests
{
    private static readonly TimeZoneInfo AlgeriaTz =
        KnownLocations.ResolveTimeZone(KnownLocations.AlgeriaTimeZoneId);

    [Fact]
    public void Plan_MasterOff_ReturnsEmptyEvenOnSpecialDay()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 8, minute: 0);
        var settings = EnabledIslamic().With(specialDayRemindersEnabled: false);

        var plan = SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ");
        Assert.Empty(plan);
    }

    [Fact]
    public void Plan_IslamicEid_At0900_WhenMasterAndIslamicSetOn()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 8, minute: 0);
        var settings = EnabledIslamic();

        var plan = SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ");
        var item = Assert.Single(plan);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(item.FireAt.DateTime));
        Assert.Equal(eidCivil, item.CivilDate);
        Assert.Contains("islamic.eid_fitr", item.DefinitionIds);
        Assert.Equal($"special:{eidCivil:yyyy-MM-dd}:islamic.eid_fitr", item.Id);
        Assert.True(item.FireAt > now);
    }

    [Fact]
    public void Plan_MutedDefinition_SuppressesFire()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 8, minute: 0);
        var settings = EnabledIslamic().With(specialDayMutedIds: ["islamic.eid_fitr"]);

        Assert.Empty(SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ"));
    }

    [Fact]
    public void Plan_CountryOnlyDay_RequiresCountrySet()
    {
        // Pick an Algeria civil holiday that does not coincide with an Islamic day under tabular Hijri.
        var civil = FindCountryOnlyAlgeriaDay();
        var now = AtLocal(civil, hour: 8, minute: 0);

        var islamicOnly = EnabledIslamic();
        Assert.Empty(SpecialDayReminderPlanner.Plan(now, islamicOnly, AlgeriaTz, "DZ"));

        var withCountry = EnabledIslamic().With(
            specialDayCountrySetEnabled: true,
            specialDayIslamicSetEnabled: false);
        var plan = SpecialDayReminderPlanner.Plan(now, withCountry, AlgeriaTz, "DZ");
        var item = Assert.Single(plan);
        Assert.All(item.DefinitionIds, id => Assert.StartsWith("dz.", id));
        Assert.Equal(SpecialDaySources.Country, item.Sources);
    }

    private static DateOnly FindCountryOnlyAlgeriaDay()
    {
        var anchors = new (int Month, int Day)[] { (1, 1), (1, 12), (5, 1), (7, 5), (11, 1) };
        for (var year = 2024; year <= 2032; year++)
        {
            foreach (var (month, day) in anchors)
            {
                var civil = new DateOnly(year, month, day);
                var resolved = SpecialDayResolver.ForCivilDate(civil, dayOffset: 0, countryCode: "DZ");
                if (resolved?.Sources == SpecialDaySources.Country)
                {
                    return civil;
                }
            }
        }

        throw new InvalidOperationException("No country-only Algeria holiday found in scan window.");
    }

    [Fact]
    public void Plan_PastFireTime_ExcludedFromPlan()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 10, minute: 0);
        var settings = EnabledIslamic();

        Assert.Empty(SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ"));
    }

    [Fact]
    public void PlanMissed_After0900_ReturnsPastDue()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 10, minute: 30);
        var since = AtLocal(eidCivil, hour: 0, minute: 0);
        var settings = EnabledIslamic();

        var missed = SpecialDayReminderPlanner.PlanMissed(now, since, settings, AlgeriaTz, countryCode: "DZ");
        var item = Assert.Single(missed);
        Assert.Equal(eidCivil, item.CivilDate);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(item.FireAt.DateTime));
    }

    [Fact]
    public void PlanMissed_WithinGrace_NotYetMissed()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        // Fire at 09:00; now 09:01 — still inside 2 min grace upper bound (now - grace = 08:59)
        var now = AtLocal(eidCivil, hour: 9, minute: 1);
        var since = AtLocal(eidCivil, hour: 0, minute: 0);
        var settings = EnabledIslamic();

        Assert.Empty(SpecialDayReminderPlanner.PlanMissed(now, since, settings, AlgeriaTz, countryCode: "DZ"));
    }

    [Fact]
    public void Plan_HijriOffset_ShiftsCivilFireDay()
    {
        // Tabular 1 Shawwal under offset 0
        var offset0 = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        // Same Hijri label under offset +1 maps to a different civil day
        var offset1 = CivilForHijri(year: 1446, month: 10, day: 1, offset: 1);
        Assert.NotEqual(offset0, offset1);

        var now = AtLocal(offset1, hour: 8, minute: 0);
        var settings = EnabledIslamic().With(hijriDayOffset: 1);
        var plan = SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ");
        Assert.Single(plan);
        Assert.Equal(offset1, plan[0].CivilDate);

        // On offset1's civil day, offset 0 settings should not treat it as Eid
        var settingsOffset0 = EnabledIslamic().With(hijriDayOffset: 0);
        // May still fire if another special lands that day — only assert Eid id when present under offset 1
        Assert.Contains("islamic.eid_fitr", plan[0].DefinitionIds);
        var otherPlan = SpecialDayReminderPlanner.Plan(now, settingsOffset0, AlgeriaTz, "DZ");
        Assert.DoesNotContain(otherPlan, p => p.DefinitionIds.Contains("islamic.eid_fitr"));
    }

    [Fact]
    public void Plan_CustomReminderTime_UsesSettingsClock()
    {
        var eidCivil = CivilForHijri(year: 1446, month: 10, day: 1, offset: 0);
        var now = AtLocal(eidCivil, hour: 7, minute: 0);
        var settings = EnabledIslamic().With(specialDayReminderTime: "07:30");

        var plan = SpecialDayReminderPlanner.Plan(now, settings, AlgeriaTz, "DZ");
        var item = Assert.Single(plan);
        Assert.Equal(new TimeOnly(7, 30), TimeOnly.FromDateTime(item.FireAt.DateTime));
    }

    private static AppSettings EnabledIslamic()
        => SettingsJson.CreateDefault().With(
            specialDayRemindersEnabled: true,
            specialDayIslamicSetEnabled: true,
            specialDayCountrySetEnabled: false,
            specialDayReminderTime: "09:00");

    private static DateOnly CivilForHijri(int year, int month, int day, int offset)
        => HijriConverter.ToGregorian(new HijriDate(year, month, day), offset);

    private static DateTimeOffset AtLocal(DateOnly civil, int hour, int minute)
    {
        var local = civil.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, AlgeriaTz.GetUtcOffset(local));
    }
}
