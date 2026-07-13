using Samt.Core.Domain;

namespace Samt.Core.Locations;

/// <summary>Offline city seeds. City names are labels + coords — not a geocoder.</summary>
public static class KnownLocations
{
    /// <summary>Windows timezone id for Algeria (UTC+1, no DST).</summary>
    public const string AlgeriaTimeZoneId = "W. Central Africa Standard Time";

    public static LocationProfile Kennadsa { get; } = new()
    {
        Id = Guid.Parse("a1111111-1111-4111-8111-111111111111"),
        DisplayName = "القنادسة / Kennadsa",
        Latitude = 31.5569,
        Longitude = -2.4181,
        TimeZoneId = AlgeriaTimeZoneId,
        Source = LocationSource.CitySeed
    };

    public static LocationProfile Algiers { get; } = new()
    {
        Id = Guid.Parse("a2222222-2222-4222-8222-222222222222"),
        DisplayName = "الجزائر / Algiers",
        Latitude = 36.7538,
        Longitude = 3.0588,
        TimeZoneId = AlgeriaTimeZoneId,
        Source = LocationSource.CitySeed
    };

    public static LocationProfile Oran { get; } = new()
    {
        Id = Guid.Parse("a3333333-3333-4333-8333-333333333333"),
        DisplayName = "وهران / Oran",
        Latitude = 35.6969,
        Longitude = -0.6331,
        TimeZoneId = AlgeriaTimeZoneId,
        Source = LocationSource.CitySeed
    };

    public static LocationProfile Bechar { get; } = new()
    {
        Id = Guid.Parse("a4444444-4444-4444-8444-444444444444"),
        DisplayName = "بشار / Béchar",
        Latitude = 31.6167,
        Longitude = -2.2167,
        TimeZoneId = AlgeriaTimeZoneId,
        Source = LocationSource.CitySeed
    };

    public static IReadOnlyList<LocationProfile> AlgeriaSeeds { get; } =
    [
        Kennadsa,
        Bechar,
        Oran,
        Algiers
    ];

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback used in some Linux CI hosts if Windows ids are unavailable.
            if (string.Equals(timeZoneId, AlgeriaTimeZoneId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(timeZoneId, "Africa/Algiers", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    AlgeriaTimeZoneId,
                    TimeSpan.FromHours(1),
                    "Algeria Standard Time",
                    "Algeria Standard Time");
            }

            throw;
        }
    }
}
