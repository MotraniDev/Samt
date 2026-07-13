using Samt.Core.Time;

namespace Samt.Core.Calendar;

/// <summary>
/// Resolves special days for a civil date or Hijri month from offline catalogs.
/// One <see cref="ResolvedSpecialDay"/> per civil date (special-day identity / dedup).
/// </summary>
public static class SpecialDayResolver
{
    /// <summary>
    /// Special day on <paramref name="civilDate"/> after applying <paramref name="dayOffset"/>
    /// for Hijri mapping, using the country package for <paramref name="countryCode"/>
    /// (unknown codes fall back to Algeria via <see cref="CountryCalendarCatalog.GetOrDefault"/>).
    /// </summary>
    public static ResolvedSpecialDay? ForCivilDate(DateOnly civilDate, int dayOffset = 0, string? countryCode = null)
    {
        var offset = HijriConverter.ClampDayOffset(dayOffset);
        var hijri = HijriConverter.FromGregorian(civilDate, offset);
        var matches = MatchDefinitions(civilDate, hijri, countryCode);
        return matches.Count == 0 ? null : BuildResolved(civilDate, hijri, matches);
    }

    /// <summary>
    /// All cells in a tabular Hijri month with dual Gregorian dates and optional special days.
    /// </summary>
    public static IReadOnlyList<HijriMonthCell> ForHijriMonth(
        int hijriYear,
        int hijriMonth,
        int dayOffset = 0,
        string? countryCode = null)
    {
        if (hijriMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(hijriMonth), hijriMonth, "Hijri month must be 1–12.");
        }

        var offset = HijriConverter.ClampDayOffset(dayOffset);
        var dayCount = HijriConverter.GetDaysInMonth(hijriYear, hijriMonth);
        var cells = new List<HijriMonthCell>(dayCount);

        for (var day = 1; day <= dayCount; day++)
        {
            var hijri = new HijriDate(hijriYear, hijriMonth, day);
            var civil = HijriConverter.ToGregorian(hijri, offset);
            // Re-resolve via civil so country Gregorian anchors and Islamic Hijri anchors share one path.
            var special = ForCivilDate(civil, offset, countryCode);
            cells.Add(new HijriMonthCell
            {
                Hijri = hijri,
                CivilDate = civil,
                SpecialDay = special
            });
        }

        return cells;
    }

    private static List<SpecialDayDefinition> MatchDefinitions(
        DateOnly civilDate,
        HijriDate hijri,
        string? countryCode)
    {
        var matches = new List<SpecialDayDefinition>();

        foreach (var def in IslamicObservanceCatalog.All)
        {
            if (def.HijriMonth == hijri.Month && def.HijriDay == hijri.Day)
            {
                matches.Add(def);
            }
        }

        var package = CountryCalendarCatalog.GetOrDefault(countryCode);
        foreach (var def in package.Definitions)
        {
            if (def.GregorianMonth == civilDate.Month && def.GregorianDay == civilDate.Day)
            {
                matches.Add(def);
            }
        }

        return matches;
    }

    private static ResolvedSpecialDay BuildResolved(
        DateOnly civilDate,
        HijriDate hijri,
        List<SpecialDayDefinition> matches)
    {
        // Primary label: Islamic first, then country; stable id order within a source.
        var ordered = matches
            .OrderBy(d => d.Source == SpecialDaySource.Islamic ? 0 : 1)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var sources = SpecialDaySources.None;
        foreach (var def in ordered)
        {
            sources |= def.Source == SpecialDaySource.Islamic
                ? SpecialDaySources.Islamic
                : SpecialDaySources.Country;
        }

        return new ResolvedSpecialDay
        {
            CivilDate = civilDate,
            HijriDate = hijri,
            Definitions = ordered,
            DefinitionIds = ordered.Select(d => d.Id).ToList(),
            DisplayKeys = ordered.Select(d => d.DisplayKey).ToList(),
            Sources = sources
        };
    }
}
