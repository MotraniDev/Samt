namespace Samt.Core.Calendar;

/// <summary>
/// Resolves the effective calendar country code (hybrid: override → location → product default).
/// Does not load packages; pair with <see cref="CountryCalendarCatalog.GetOrDefault"/>.
/// </summary>
public static class CalendarCountryResolver
{
    /// <summary>Product default when location country is empty and no override (v1: Algeria).</summary>
    public const string DefaultCountryCode = CountryCalendarCatalog.AlgeriaCountryCode;

    /// <summary>
    /// <paramref name="countryOverride"/> wins when non-empty;
    /// else <paramref name="locationCountryCode"/>;
    /// else <see cref="DefaultCountryCode"/>.
    /// </summary>
    public static string Resolve(string? countryOverride, string? locationCountryCode)
    {
        if (!string.IsNullOrWhiteSpace(countryOverride))
        {
            return Normalize(countryOverride);
        }

        if (!string.IsNullOrWhiteSpace(locationCountryCode))
        {
            return Normalize(locationCountryCode);
        }

        return DefaultCountryCode;
    }

    private static string Normalize(string code)
        => code.Trim().ToUpperInvariant();
}
