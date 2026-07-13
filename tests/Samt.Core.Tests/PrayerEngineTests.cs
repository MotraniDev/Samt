using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt.Core.Tests;

public class PrayerEngineTests
{
    private readonly PrayerEngine _engine = new();

    [Fact]
    public void Kennadsa_Winter_ProducesOrderedTimes()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);

        Assert.Equal(date, schedule.Date);
        Assert.True(schedule.Fajr < schedule.Sunrise);
        Assert.True(schedule.Sunrise < schedule.Dhuhr);
        Assert.True(schedule.Dhuhr < schedule.Asr);
        Assert.True(schedule.Asr < schedule.Maghrib);
        Assert.True(schedule.Maghrib < schedule.Isha);
        Assert.True(schedule.Imsak < schedule.Fajr);
    }

    [Fact]
    public void Kennadsa_Summer_ProducesOrderedTimes()
    {
        var date = new DateOnly(2025, 7, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);

        Assert.True(schedule.Fajr < schedule.Sunrise);
        Assert.True(schedule.Sunrise < schedule.Dhuhr);
        Assert.True(schedule.Dhuhr < schedule.Asr);
        Assert.True(schedule.Asr < schedule.Maghrib);
        Assert.True(schedule.Maghrib < schedule.Isha);
    }

    [Fact]
    public void HanafiAsr_IsLaterThanStandard()
    {
        var date = new DateOnly(2025, 3, 21);
        var standard = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var hanafi = _engine.Calculate(
            date,
            KnownLocations.Kennadsa,
            CalculationMethods.Algeria.WithAsr(AsrMadhab.Hanafi));

        Assert.True(hanafi.Asr > standard.Asr);
    }

    [Fact]
    public void MinuteAdjustments_AreApplied()
    {
        var date = new DateOnly(2025, 5, 1);
        var baseSchedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var adjusted = _engine.Calculate(
            date,
            KnownLocations.Kennadsa,
            CalculationMethods.Algeria.WithAdjustments(new Dictionary<PrayerEvent, int>
            {
                [PrayerEvent.Fajr] = 2,
                [PrayerEvent.Maghrib] = -1
            }));

        Assert.Equal(2, (adjusted.Fajr - baseSchedule.Fajr).TotalMinutes);
        Assert.Equal(-1, (adjusted.Maghrib - baseSchedule.Maghrib).TotalMinutes);
    }

    [Fact]
    public void Imsak_DefaultsToTenMinutesBeforeFajr()
    {
        var schedule = _engine.Calculate(
            new DateOnly(2025, 3, 1),
            KnownLocations.Kennadsa,
            CalculationMethods.Algeria);

        Assert.Equal(10, (schedule.Fajr - schedule.Imsak!.Value).TotalMinutes);
    }

    [Fact]
    public void UmmAlQura_IshaIsNinetyMinutesAfterMaghrib()
    {
        var schedule = _engine.Calculate(
            new DateOnly(2025, 4, 10),
            KnownLocations.Kennadsa,
            CalculationMethods.UmmAlQura);

        var delta = schedule.Isha - schedule.Maghrib;
        Assert.Equal(90, (int)Math.Round(delta.TotalMinutes));
    }

    [Fact]
    public void Friday_IncludesJumuah_MatchingDhuhrByDefault()
    {
        // 2025-06-06 is a Friday
        var date = new DateOnly(2025, 6, 6);
        Assert.Equal(DayOfWeek.Friday, date.DayOfWeek);

        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        Assert.NotNull(schedule.Jumuah);
        Assert.Equal(schedule.Dhuhr, schedule.Jumuah);
    }

    [Fact]
    public void Friday_FixedTime_UsesLocationSetting()
    {
        var date = new DateOnly(2025, 6, 6);
        var location = new LocationProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            Latitude = KnownLocations.Kennadsa.Latitude,
            Longitude = KnownLocations.Kennadsa.Longitude,
            TimeZoneId = KnownLocations.Kennadsa.TimeZoneId,
            FridayTimeMode = FridayTimeMode.FixedTime,
            FixedFridayLocalTime = new TimeOnly(13, 30)
        };

        var schedule = _engine.Calculate(date, location, CalculationMethods.Algeria);
        Assert.Equal(new TimeOnly(13, 30), TimeOnly.FromTimeSpan(schedule.Jumuah!.Value.TimeOfDay));
    }

    [Fact]
    public void HighLatitude_Rules_ProduceFiniteFajrAndIsha()
    {
        // Tromsø-like high latitude
        var arctic = new LocationProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = "Arctic",
            Latitude = 69.6492,
            Longitude = 18.9553,
            TimeZoneId = "W. Europe Standard Time"
        };

        var date = new DateOnly(2025, 6, 21);
        foreach (var rule in new[]
                 {
                     HighLatitudeRule.MiddleOfTheNight,
                     HighLatitudeRule.OneSeventhOfTheNight,
                     HighLatitudeRule.AngleBased
                 })
        {
            var profile = CalculationMethods.MuslimWorldLeague.WithHighLatitude(rule);
            var schedule = _engine.Calculate(date, arctic, profile);
            Assert.True(schedule.Fajr < schedule.Sunrise, rule.ToString());
            Assert.True(schedule.Maghrib < schedule.Isha, rule.ToString());
        }
    }

    [Fact]
    public void DifferentMethods_ChangeFajr()
    {
        var date = new DateOnly(2025, 9, 1);
        var algeria = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var isna = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Isna);

        // ISNA 15° Fajr is later than 18° Fajr
        Assert.True(isna.Fajr > algeria.Fajr);
    }

    [Fact]
    public void NextPrayer_ReturnsUpcomingEvent()
    {
        var date = new DateOnly(2025, 1, 15);
        var schedule = _engine.Calculate(date, KnownLocations.Kennadsa, CalculationMethods.Algeria);
        var afterFajr = schedule.Fajr.AddMinutes(1);
        var next = schedule.GetNextPrayer(afterFajr);
        Assert.Equal(PrayerEvent.Dhuhr, next);
    }

    [Fact]
    public void TimeZoneOffset_IsPlusOneForAlgeria()
    {
        var schedule = _engine.Calculate(
            new DateOnly(2025, 1, 1),
            KnownLocations.Kennadsa,
            CalculationMethods.Algeria);

        Assert.Equal(TimeSpan.FromHours(1), schedule.Dhuhr.Offset);
    }
}
