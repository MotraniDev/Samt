using Samt.Core.Time;

namespace Samt.Core.Calendar;

/// <summary>Origin of a special-day definition in the offline catalogs.</summary>
public enum SpecialDaySource
{
    Islamic = 0,
    Country = 1
}

/// <summary>Combined sources present on one civil date after resolution.</summary>
[Flags]
public enum SpecialDaySources
{
    None = 0,
    Islamic = 1,
    Country = 2,
    Both = Islamic | Country
}

/// <summary>
/// Grid / toast mark derived from <see cref="SpecialDaySources"/>.
/// Highlight ignores reminder enablement.
/// </summary>
public enum SpecialDayMark
{
    None = 0,
    Islamic = 1,
    Country = 2,
    Both = 3
}

/// <summary>
/// One offline catalog entry (Islamic Hijri anchor or country Gregorian month/day).
/// Id is stable for mute lists (e.g. <c>islamic.eid_fitr</c>).
/// </summary>
public sealed class SpecialDayDefinition
{
    public required string Id { get; init; }

    public required SpecialDaySource Source { get; init; }

    /// <summary>Localization key, typically <c>SpecialDay.{Id}</c>.</summary>
    public required string DisplayKey { get; init; }

    /// <summary>When true, UI may show a “commonly observed” hint (not a fatwa).</summary>
    public bool IsCommonlyObserved { get; init; }

    /// <summary>Islamic: Hijri month 1–12.</summary>
    public int? HijriMonth { get; init; }

    /// <summary>Islamic: Hijri day of month.</summary>
    public int? HijriDay { get; init; }

    /// <summary>Country: Gregorian month 1–12.</summary>
    public int? GregorianMonth { get; init; }

    /// <summary>Country: Gregorian day of month.</summary>
    public int? GregorianDay { get; init; }
}

/// <summary>One civil date after collapsing all matching definitions (special-day identity).</summary>
public sealed class ResolvedSpecialDay
{
    public required DateOnly CivilDate { get; init; }

    public required HijriDate HijriDate { get; init; }

    public required IReadOnlyList<string> DefinitionIds { get; init; }

    public required IReadOnlyList<string> DisplayKeys { get; init; }

    public required SpecialDaySources Sources { get; init; }

    public required IReadOnlyList<SpecialDayDefinition> Definitions { get; init; }

    public string PrimaryDefinitionId => DefinitionIds[0];

    public string PrimaryDisplayKey => DisplayKeys[0];

    public SpecialDayMark Mark => Sources switch
    {
        SpecialDaySources.Both => SpecialDayMark.Both,
        SpecialDaySources.Islamic => SpecialDayMark.Islamic,
        SpecialDaySources.Country => SpecialDayMark.Country,
        _ => SpecialDayMark.None
    };
}

/// <summary>One cell in a Hijri-month grid.</summary>
public sealed class HijriMonthCell
{
    public required HijriDate Hijri { get; init; }

    public required DateOnly CivilDate { get; init; }

    public ResolvedSpecialDay? SpecialDay { get; init; }

    public bool IsRamadan => Hijri.IsRamadan;

    public SpecialDayMark Mark => SpecialDay?.Mark ?? SpecialDayMark.None;
}

/// <summary>Offline country holiday package (civil Gregorian anchors).</summary>
public sealed class CountryCalendarPackage
{
    public required string CountryCode { get; init; }

    public required IReadOnlyList<SpecialDayDefinition> Definitions { get; init; }
}
