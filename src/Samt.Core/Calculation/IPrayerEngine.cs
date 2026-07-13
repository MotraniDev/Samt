using Samt.Core.Domain;

namespace Samt.Core.Calculation;

public interface IPrayerEngine
{
    PrayerSchedule Calculate(
        DateOnly date,
        LocationProfile location,
        CalculationProfile profile,
        TimeZoneInfo? timeZone = null);
}
