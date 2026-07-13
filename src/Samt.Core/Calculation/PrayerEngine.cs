using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt.Core.Calculation;

/// <summary>
/// Local offline prayer-time engine.
/// Does not call network APIs. Results are astronomical estimates; users should verify against their mosque.
/// </summary>
public sealed class PrayerEngine : IPrayerEngine
{
    public PrayerSchedule Calculate(
        DateOnly date,
        LocationProfile location,
        CalculationProfile profile,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(profile);

        var tz = timeZone ?? KnownLocations.ResolveTimeZone(location.TimeZoneId);
        var noonUnspecified = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(noonUnspecified);
        var timezoneHours = offset.TotalHours;

        // Julian day adjusted for longitude (PrayTimes convention).
        var jd = AstronomicalMath.JulianDay(date.Year, date.Month, date.Day)
                 - location.Longitude / (15.0 * 24.0);

        var (equationOfTime, declination) = AstronomicalMath.SunPosition(jd);

        // Transit (Dhuhr) in hours from local midnight.
        var dhuhrHours = AstronomicalMath.FixHour(
            12.0 + timezoneHours - location.Longitude / 15.0 - equationOfTime);

        var altitude = location.AltitudeMeters ?? profile.AltitudeMeters ?? 0;
        var riseSetAltitude = -(AstronomicalMath.SunriseSunsetAngle
                                + 0.0347 * Math.Sqrt(Math.Max(altitude, 0)));

        var sunriseHours = TimeFromTransit(dhuhrHours, location.Latitude, declination, riseSetAltitude, before: true);
        var sunsetHours = TimeFromTransit(dhuhrHours, location.Latitude, declination, riseSetAltitude, before: false);

        var fajrHours = TimeFromTransit(
            dhuhrHours,
            location.Latitude,
            declination,
            -Math.Abs(profile.FajrAngleDegrees),
            before: true);

        var ishaHours = ComputeIshaHours(dhuhrHours, sunsetHours, location.Latitude, declination, profile);
        var maghribHours = ComputeMaghribHours(dhuhrHours, sunsetHours, location.Latitude, declination, profile);

        var asrFactor = profile.AsrMadhab == AsrMadhab.Hanafi ? 2.0 : 1.0;
        var asrAltitude = AstronomicalMath.AsrAltitudeDegrees(location.Latitude, declination, asrFactor);
        var asrHours = TimeFromTransit(dhuhrHours, location.Latitude, declination, asrAltitude, before: false);

        ApplyHighLatitudeAdjustments(
            profile,
            ref fajrHours,
            ref ishaHours,
            sunriseHours,
            sunsetHours);

        fajrHours ??= sunriseHours is null ? dhuhrHours - 2 : sunriseHours.Value - 1.5;
        ishaHours ??= sunsetHours is null ? dhuhrHours + 2 : sunsetHours.Value + 1.5;
        sunriseHours ??= fajrHours + 1.0;
        sunsetHours ??= maghribHours ?? ishaHours - 1.0;
        maghribHours ??= sunsetHours;
        asrHours ??= (dhuhrHours + sunsetHours.Value) / 2.0;

        var midnightHours = AstronomicalMath.FixHour(
            (sunsetHours.Value + (fajrHours.Value + 24.0)) / 2.0);

        var raw = new Dictionary<PrayerEvent, DateTimeOffset>
        {
            [PrayerEvent.Fajr] = ToDateTimeOffset(date, fajrHours.Value, offset),
            [PrayerEvent.Sunrise] = ToDateTimeOffset(date, sunriseHours.Value, offset),
            [PrayerEvent.Dhuhr] = ToDateTimeOffset(date, dhuhrHours, offset),
            [PrayerEvent.Asr] = ToDateTimeOffset(date, asrHours.Value, offset),
            [PrayerEvent.Maghrib] = ToDateTimeOffset(date, maghribHours.Value, offset),
            [PrayerEvent.Isha] = ToDateTimeOffset(date, ishaHours.Value, offset),
            [PrayerEvent.Midnight] = ToDateTimeOffset(date, midnightHours, offset),
            [PrayerEvent.Imsak] = ToDateTimeOffset(date, fajrHours.Value, offset)
                .AddMinutes(-profile.ImsakOffsetMinutes)
        };

        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            raw[PrayerEvent.Jumuah] = location.FridayTimeMode == FridayTimeMode.FixedTime
                                      && location.FixedFridayLocalTime is { } fixedTime
                ? new DateTimeOffset(date.ToDateTime(fixedTime), offset)
                : raw[PrayerEvent.Dhuhr];
        }

        var final = new Dictionary<PrayerEvent, DateTimeOffset>(raw.Count);
        foreach (var (key, value) in raw)
        {
            var rounded = PrayerTimeRounder.Round(value, profile.RoundMode);
            final[key] = PrayerTimeRounder.ApplyAdjustment(rounded, profile.GetAdjustment(key));
        }

        return new PrayerSchedule
        {
            Date = date,
            LocationId = location.Id,
            CalculationProfileId = profile.Id,
            TimeZoneId = location.TimeZoneId,
            RawTimes = raw,
            Times = final
        };
    }

    private static double? ComputeMaghribHours(
        double dhuhrHours,
        double? sunsetHours,
        double latitude,
        double declination,
        CalculationProfile profile)
    {
        return profile.Maghrib.Kind switch
        {
            TwilightKind.Sunset => sunsetHours,
            TwilightKind.MinutesAfter => sunsetHours is null
                ? null
                : sunsetHours.Value + profile.Maghrib.MinutesAfter / 60.0,
            TwilightKind.Angle => TimeFromTransit(
                dhuhrHours,
                latitude,
                declination,
                -Math.Abs(profile.Maghrib.AngleDegrees),
                before: false),
            _ => sunsetHours
        };
    }

    private static double? ComputeIshaHours(
        double dhuhrHours,
        double? sunsetHours,
        double latitude,
        double declination,
        CalculationProfile profile)
    {
        return profile.Isha.Kind switch
        {
            TwilightKind.Angle => TimeFromTransit(
                dhuhrHours,
                latitude,
                declination,
                -Math.Abs(profile.Isha.AngleDegrees),
                before: false),
            TwilightKind.MinutesAfter => sunsetHours is null
                ? null
                : sunsetHours.Value + profile.Isha.MinutesAfter / 60.0,
            TwilightKind.Sunset => sunsetHours,
            _ => null
        };
    }

    private static double? TimeFromTransit(
        double transitHours,
        double latitude,
        double declination,
        double altitudeDegrees,
        bool before)
    {
        var hourAngle = AstronomicalMath.HourAngleHours(latitude, declination, altitudeDegrees);
        if (hourAngle is null)
        {
            return null;
        }

        return before
            ? AstronomicalMath.FixHour(transitHours - hourAngle.Value)
            : AstronomicalMath.FixHour(transitHours + hourAngle.Value);
    }

    private static void ApplyHighLatitudeAdjustments(
        CalculationProfile profile,
        ref double? fajrHours,
        ref double? ishaHours,
        double? sunriseHours,
        double? sunsetHours)
    {
        if (profile.HighLatitudeRule == HighLatitudeRule.None)
        {
            return;
        }

        if (sunriseHours is null || sunsetHours is null)
        {
            return;
        }

        var night = AstronomicalMath.FixHour(sunriseHours.Value + 24 - sunsetHours.Value);
        if (night <= 0)
        {
            return;
        }

        var portion = profile.HighLatitudeRule switch
        {
            HighLatitudeRule.MiddleOfTheNight => night / 2.0,
            HighLatitudeRule.OneSeventhOfTheNight => night / 7.0,
            HighLatitudeRule.AngleBased => night * (profile.FajrAngleDegrees / 60.0),
            _ => night / 2.0
        };

        var safeFajr = AstronomicalMath.FixHour(sunriseHours.Value - portion);
        var safeIsha = AstronomicalMath.FixHour(sunsetHours.Value + portion);

        if (fajrHours is null || !IsWithinNight(fajrHours.Value, sunsetHours.Value, sunriseHours.Value))
        {
            fajrHours = safeFajr;
        }

        if (ishaHours is null || !IsWithinNight(ishaHours.Value, sunsetHours.Value, sunriseHours.Value))
        {
            ishaHours = safeIsha;
        }
    }

    /// <summary>True if time lies between sunset and next sunrise (night segment).</summary>
    private static bool IsWithinNight(double time, double sunset, double sunrise)
    {
        // Normalize night as [sunset, sunrise+24)
        var t = time;
        if (t < sunset)
        {
            t += 24;
        }

        var end = sunrise;
        if (end <= sunset)
        {
            end += 24;
        }

        return t >= sunset && t <= end;
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date, double hoursFromMidnight, TimeSpan offset)
    {
        hoursFromMidnight = AstronomicalMath.FixHour(hoursFromMidnight);
        var totalSeconds = (int)Math.Floor(hoursFromMidnight * 3600.0);
        var h = totalSeconds / 3600;
        var m = totalSeconds % 3600 / 60;
        var s = totalSeconds % 60;
        var local = date.ToDateTime(new TimeOnly(h, m, s));
        return new DateTimeOffset(local, offset);
    }
}
