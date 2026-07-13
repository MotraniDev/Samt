namespace Samt.Core.Calculation;

/// <summary>
/// Solar position helpers based on the widely used PrayTimes approximations
/// (sufficient for civil prayer schedules; offline, no external services).
/// </summary>
internal static class AstronomicalMath
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public static double DegreesToRadians(double degrees) => degrees * DegToRad;

    public static double RadiansToDegrees(double radians) => radians * RadToDeg;

    public static double FixAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0)
        {
            angle += 360.0;
        }

        return angle;
    }

    public static double FixHour(double hour)
    {
        hour %= 24.0;
        if (hour < 0)
        {
            hour += 24.0;
        }

        return hour;
    }

    /// <summary>Julian day at 0h UT for the given civil date.</summary>
    public static double JulianDay(int year, int month, int day)
    {
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        var a = Math.Floor(year / 100.0);
        var b = 2 - a + Math.Floor(a / 4.0);
        return Math.Floor(365.25 * (year + 4716))
               + Math.Floor(30.6001 * (month + 1))
               + day
               + b
               - 1524.5;
    }

    /// <summary>
    /// Equation of time (hours) and solar declination (degrees) for Julian day.
    /// </summary>
    public static (double EquationOfTimeHours, double DeclinationDegrees) SunPosition(double julianDay)
    {
        var d = julianDay - 2451545.0;
        var g = FixAngle(357.529 + 0.98560028 * d);
        var q = FixAngle(280.459 + 0.98564736 * d);
        var l = FixAngle(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g));

        var e = 23.439 - 0.00000036 * d;

        var ra = ArcTan2(Cos(e) * Sin(l), Cos(l)) / 15.0;
        var equation = q / 15.0 - FixHour(ra);
        var declination = ArcSin(Sin(e) * Sin(l));
        return (equation, declination);
    }

    /// <summary>
    /// Hours from solar transit for a given solar altitude (degrees; negative = below horizon).
    /// Returns null when the sun never reaches that altitude.
    /// cos H = (sin α − sin φ sin δ) / (cos φ cos δ)
    /// </summary>
    public static double? HourAngleHours(double latitude, double declination, double altitudeDegrees)
    {
        var lat = DegreesToRadians(latitude);
        var dec = DegreesToRadians(declination);
        var alt = DegreesToRadians(altitudeDegrees);

        var numerator = Math.Sin(alt) - Math.Sin(lat) * Math.Sin(dec);
        var denominator = Math.Cos(lat) * Math.Cos(dec);
        if (Math.Abs(denominator) < 1e-12)
        {
            return null;
        }

        var cosH = numerator / denominator;
        if (cosH < -1.0 || cosH > 1.0)
        {
            return null;
        }

        return RadiansToDegrees(Math.Acos(cosH)) / 15.0;
    }

    /// <summary>
    /// Positive solar altitude (degrees) when shadow length = factor × object length.
    /// </summary>
    public static double AsrAltitudeDegrees(double latitude, double declination, double shadowFactor)
    {
        var absolute = Math.Abs(latitude - declination);
        return RadiansToDegrees(Math.Atan(1.0 / (shadowFactor + Math.Tan(DegreesToRadians(absolute)))));
    }

    /// <summary>Approximate sunrise/sunset refraction + solar radius correction (~0.833°).</summary>
    public const double SunriseSunsetAngle = 0.833;

    public static double Sin(double degrees) => Math.Sin(DegreesToRadians(degrees));

    public static double Cos(double degrees) => Math.Cos(DegreesToRadians(degrees));

    public static double ArcSin(double value) => RadiansToDegrees(Math.Asin(value));

    public static double ArcTan2(double y, double x) => RadiansToDegrees(Math.Atan2(y, x));
}
