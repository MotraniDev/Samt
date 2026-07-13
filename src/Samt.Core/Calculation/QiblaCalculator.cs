namespace Samt.Core.Calculation;

/// <summary>
/// Static qibla bearing and distance from observer coordinates to the Kaaba.
/// True-north degrees (0–360); no magnetic declination or device sensors.
/// </summary>
public static class QiblaCalculator
{
    /// <summary>Kaaba reference (degrees). Common civil approximation used by offline prayer apps.</summary>
    public const double KaabaLatitude = 21.422487;
    public const double KaabaLongitude = 39.826206;

    private const double EarthRadiusKm = 6371.0;
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>Near-zero distance treated as “at the Kaaba” (bearing undefined / 0).</summary>
    public const double AtKaabaThresholdKm = 0.05;

    public static QiblaInfo Calculate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be in [-90, 90].");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be in [-180, 180].");
        }

        var distanceKm = HaversineKm(latitude, longitude, KaabaLatitude, KaabaLongitude);
        if (distanceKm < AtKaabaThresholdKm)
        {
            return new QiblaInfo(BearingDegrees: 0, DistanceKm: distanceKm, IsAtKaaba: true);
        }

        var bearing = BearingDegrees(latitude, longitude, KaabaLatitude, KaabaLongitude);
        return new QiblaInfo(bearing, distanceKm, IsAtKaaba: false);
    }

    private static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = lat1 * DegToRad;
        var φ2 = lat2 * DegToRad;
        var Δλ = (lon2 - lon1) * DegToRad;

        var y = Math.Sin(Δλ) * Math.Cos(φ2);
        var x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
        var θ = Math.Atan2(y, x) * RadToDeg;
        return (θ + 360.0) % 360.0;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = lat1 * DegToRad;
        var φ2 = lat2 * DegToRad;
        var Δφ = (lat2 - lat1) * DegToRad;
        var Δλ = (lon2 - lon1) * DegToRad;

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                + Math.Cos(φ1) * Math.Cos(φ2) * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Map bearing to an 8-point compass key suffix: N, NE, E, SE, S, SW, W, NW
    /// (prepend localization prefix e.g. <c>Compass.</c>).
    /// </summary>
    public static string CompassOctantKey(double bearingDegrees)
    {
        var normalized = bearingDegrees % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        var octant = (int)Math.Floor((normalized + 22.5) / 45.0) % 8;
        return octant switch
        {
            0 => "N",
            1 => "NE",
            2 => "E",
            3 => "SE",
            4 => "S",
            5 => "SW",
            6 => "W",
            _ => "NW"
        };
    }
}

/// <summary>Result of a qibla calculation from observer coordinates.</summary>
public sealed record QiblaInfo(double BearingDegrees, double DistanceKm, bool IsAtKaaba);
