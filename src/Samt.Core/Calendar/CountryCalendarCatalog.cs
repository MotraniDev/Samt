namespace Samt.Core.Calendar;

/// <summary>
/// Offline calendar country packages. v1 ships Algeria (<c>DZ</c>) civil holidays only.
/// Islamic public holidays are not duplicated here.
/// </summary>
public static class CountryCalendarCatalog
{
    public const string AlgeriaCountryCode = "DZ";

    private static readonly Lazy<IReadOnlyDictionary<string, CountryCalendarPackage>> LazyByCode =
        new(CreatePackages);

    public static IReadOnlyCollection<CountryCalendarPackage> All
        => LazyByCode.Value.Values.ToList();

    /// <summary>Returns the package for <paramref name="countryCode"/>, or null if unknown.</summary>
    public static CountryCalendarPackage? TryGet(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        return LazyByCode.Value.TryGetValue(Normalize(countryCode), out var package)
            ? package
            : null;
    }

    /// <summary>
    /// Package for the code, or Algeria when the code has no package (v1 product default).
    /// </summary>
    public static CountryCalendarPackage GetOrDefault(string? countryCode)
        => TryGet(countryCode) ?? LazyByCode.Value[AlgeriaCountryCode];

    private static string Normalize(string code)
        => code.Trim().ToUpperInvariant();

    private static IReadOnlyDictionary<string, CountryCalendarPackage> CreatePackages()
    {
        var algeria = new CountryCalendarPackage
        {
            CountryCode = AlgeriaCountryCode,
            Definitions =
            [
                Country("dz.new_year", month: 1, day: 1),
                Country("dz.yennayer", month: 1, day: 12),
                Country("dz.labour", month: 5, day: 1),
                Country("dz.independence", month: 7, day: 5),
                Country("dz.revolution", month: 11, day: 1)
            ]
        };

        return new Dictionary<string, CountryCalendarPackage>(StringComparer.OrdinalIgnoreCase)
        {
            [AlgeriaCountryCode] = algeria
        };
    }

    private static SpecialDayDefinition Country(string id, int month, int day)
        => new()
        {
            Id = id,
            Source = SpecialDaySource.Country,
            DisplayKey = $"SpecialDay.{id}",
            GregorianMonth = month,
            GregorianDay = day
        };
}
