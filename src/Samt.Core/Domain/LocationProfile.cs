namespace Samt.Core.Domain;

/// <summary>A named place with coordinates and timezone used for prayer calculation.</summary>
public sealed class LocationProfile
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Degrees north positive.</summary>
    public required double Latitude { get; init; }

    /// <summary>Degrees east positive.</summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Windows or IANA timezone id used for civil prayer times at this location
    /// (may differ from the machine system zone when traveling).
    /// </summary>
    public required string TimeZoneId { get; init; }

    public LocationSource Source { get; init; } = LocationSource.Manual;

    /// <summary>
    /// Optional ISO 3166-1 alpha-2 style country code (e.g. <c>DZ</c>) for calendar country defaulting.
    /// Null/empty means unknown; calendar resolution falls through to product default.
    /// </summary>
    public string? CountryCode { get; init; }

    public FridayTimeMode FridayTimeMode { get; init; } = FridayTimeMode.FollowDhuhr;

    /// <summary>Local clock time for Jumu'ah when <see cref="FridayTimeMode"/> is <see cref="FridayTimeMode.FixedTime"/>.</summary>
    public TimeOnly? FixedFridayLocalTime { get; init; }

    /// <summary>When true, normal Dhuhr prayer-start notifications are suppressed on Fridays.</summary>
    public bool SuppressDhuhrNotificationsOnFriday { get; init; } = true;

    public double? AltitudeMeters { get; init; }
}
