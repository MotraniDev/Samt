namespace Samt.Core.Domain;

/// <summary>Angle- or interval-based twilight rule for Maghrib / Isha.</summary>
public sealed class TwilightRule
{
    public TwilightKind Kind { get; init; }

    /// <summary>Degrees below horizon when <see cref="Kind"/> is <see cref="TwilightKind.Angle"/>.</summary>
    public double AngleDegrees { get; init; }

    /// <summary>Minutes after Maghrib (Isha) or sunset (Maghrib) when using intervals.</summary>
    public int MinutesAfter { get; init; }

    public static TwilightRule Angle(double degrees) => new()
    {
        Kind = TwilightKind.Angle,
        AngleDegrees = degrees
    };

    public static TwilightRule MinutesAfterReference(int minutes) => new()
    {
        Kind = TwilightKind.MinutesAfter,
        MinutesAfter = minutes
    };

    public static TwilightRule Sunset() => new()
    {
        Kind = TwilightKind.Sunset
    };
}

public enum TwilightKind
{
    /// <summary>Event equals astronomical sunset (Maghrib default).</summary>
    Sunset = 0,

    /// <summary>Sun altitude = -angle.</summary>
    Angle = 1,

    /// <summary>Fixed minutes after Maghrib (Isha) or sunset (Maghrib).</summary>
    MinutesAfter = 2
}
