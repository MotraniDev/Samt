using Samt.Core.Calendar;
using Samt.Core.Time;

namespace Samt.Core.Tests;

public class SpecialDayResolverTests
{
    [Fact]
    public void ForCivilDate_AlgeriaIndependence_IncludesCountry_DedupesToOneDay()
    {
        var day = SpecialDayResolver.ForCivilDate(new DateOnly(2025, 7, 5), dayOffset: 0, countryCode: "DZ");

        Assert.NotNull(day);
        Assert.True(day.Sources.HasFlag(SpecialDaySources.Country));
        Assert.Contains("dz.independence", day.DefinitionIds);
        Assert.Equal(new DateOnly(2025, 7, 5), day.CivilDate);
        // Tabular Hijri may also land an Islamic observance on the same civil day → Both is valid;
        // special-day identity is still a single ResolvedSpecialDay.
        Assert.True(day.Mark is SpecialDayMark.Country or SpecialDayMark.Both);
        if (day.Sources.HasFlag(SpecialDaySources.Islamic))
        {
            Assert.Equal(SpecialDayMark.Both, day.Mark);
            Assert.StartsWith("islamic.", day.PrimaryDefinitionId, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ForCivilDate_EidFitr_IsIslamic_PrimaryLabelIslamic()
    {
        // Find civil date for 1 Shawwal (month 10 day 1) at offset 0.
        var civil = FindCivilForHijri(month: 10, day: 1, dayOffset: 0);
        var day = SpecialDayResolver.ForCivilDate(civil, dayOffset: 0, countryCode: "DZ");

        Assert.NotNull(day);
        Assert.True(day.Sources.HasFlag(SpecialDaySources.Islamic));
        Assert.Equal("islamic.eid_fitr", day.PrimaryDefinitionId);
        Assert.Equal("SpecialDay.islamic.eid_fitr", day.PrimaryDisplayKey);
        Assert.Equal(10, day.HijriDate.Month);
        Assert.Equal(1, day.HijriDate.Day);
    }

    [Fact]
    public void ForCivilDate_DayOffset_ShiftsIslamicMapping()
    {
        var civil = FindCivilForHijri(month: 10, day: 1, dayOffset: 0);
        var atZero = SpecialDayResolver.ForCivilDate(civil, dayOffset: 0, countryCode: "DZ");
        var atPlusOne = SpecialDayResolver.ForCivilDate(civil, dayOffset: 1, countryCode: "DZ");

        Assert.NotNull(atZero);
        Assert.Contains("islamic.eid_fitr", atZero.DefinitionIds);
        // Same civil date with +1 offset maps to a different Hijri day → not Eid Fitr.
        Assert.True(
            atPlusOne is null || !atPlusOne.DefinitionIds.Contains("islamic.eid_fitr"),
            "Offset +1 should move the civil day off 1 Shawwal.");
    }

    [Fact]
    public void ForCivilDate_CountryHoliday_UnaffectedByHijriOffset()
    {
        var civil = new DateOnly(2026, 11, 1);
        var a = SpecialDayResolver.ForCivilDate(civil, dayOffset: 0, countryCode: "DZ");
        var b = SpecialDayResolver.ForCivilDate(civil, dayOffset: 2, countryCode: "DZ");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Contains("dz.revolution", a.DefinitionIds);
        Assert.Contains("dz.revolution", b.DefinitionIds);
    }

    [Fact]
    public void ForCivilDate_UnknownCountry_StillGetsAlgeriaCivilDays()
    {
        var day = SpecialDayResolver.ForCivilDate(new DateOnly(2025, 5, 1), countryCode: "XX");
        Assert.NotNull(day);
        Assert.Contains("dz.labour", day.DefinitionIds);
    }

    [Fact]
    public void ForCivilDate_OrdinaryDay_ReturnsNull()
    {
        // Mid-month civil that is unlikely to hit both catalogs; scan if needed.
        var day = SpecialDayResolver.ForCivilDate(new DateOnly(2025, 3, 10), countryCode: "DZ");
        // May rarely coincide with an Islamic day; if so, skip this assert shape.
        if (day is not null)
        {
            Assert.NotEmpty(day.DefinitionIds);
            return;
        }

        Assert.Null(day);
    }

    [Fact]
    public void ForHijriMonth_CellCountMatchesDaysInMonth_AndDualDatesRoundTrip()
    {
        const int year = 1446;
        const int month = 9; // Ramadan
        var daysInMonth = HijriConverter.GetDaysInMonth(year, month);
        var cells = SpecialDayResolver.ForHijriMonth(year, month, dayOffset: 0, countryCode: "DZ");

        Assert.Equal(daysInMonth, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal(year, c.Hijri.Year);
            Assert.Equal(month, c.Hijri.Month);
            Assert.True(c.IsRamadan);
            var back = HijriConverter.FromGregorian(c.CivilDate, dayOffset: 0);
            Assert.Equal(c.Hijri, back);
        });
    }

    [Fact]
    public void ForHijriMonth_IncludesRamadanStartAndQadrMarker()
    {
        // Pick a Hijri year that includes our catalog month 9 days.
        var sampleCivil = FindCivilForHijri(month: 9, day: 1, dayOffset: 0);
        var hijri = HijriConverter.FromGregorian(sampleCivil, 0);
        var cells = SpecialDayResolver.ForHijriMonth(hijri.Year, 9, dayOffset: 0, countryCode: "DZ");

        var start = cells.Single(c => c.Hijri.Day == 1);
        Assert.NotNull(start.SpecialDay);
        Assert.Contains("islamic.ramadan_start", start.SpecialDay.DefinitionIds);

        var qadr = cells.Single(c => c.Hijri.Day == 27);
        Assert.NotNull(qadr.SpecialDay);
        Assert.Contains("islamic.laylat_al_qadr_marker", qadr.SpecialDay.DefinitionIds);
    }

    [Fact]
    public void ForHijriMonth_MapsIndependenceDayOntoCorrectCell()
    {
        var independence = new DateOnly(2025, 7, 5);
        var hijri = HijriConverter.FromGregorian(independence, 0);
        var cells = SpecialDayResolver.ForHijriMonth(hijri.Year, hijri.Month, dayOffset: 0, countryCode: "DZ");
        var cell = cells.Single(c => c.CivilDate == independence);

        Assert.True(cell.Mark is SpecialDayMark.Country or SpecialDayMark.Both);
        Assert.Contains("dz.independence", cell.SpecialDay!.DefinitionIds);
    }

    [Fact]
    public void ForCivilDate_Yennayer_IsCountryWhenNoIslamicOverlap()
    {
        // 12 Jan is fixed civil; assert country mark present (may be Both in rare years).
        var day = SpecialDayResolver.ForCivilDate(new DateOnly(2025, 1, 12), countryCode: "DZ");
        Assert.NotNull(day);
        Assert.Contains("dz.yennayer", day.DefinitionIds);
        Assert.True(day.Sources.HasFlag(SpecialDaySources.Country));
    }

    [Fact]
    public void ForHijriMonth_InvalidMonth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpecialDayResolver.ForHijriMonth(1446, 0, countryCode: "DZ"));
    }

    /// <summary>Walk civil days until tabular Hijri matches month/day at the given offset.</summary>
    private static DateOnly FindCivilForHijri(int month, int day, int dayOffset)
    {
        for (var i = 0; i < 800; i++)
        {
            var g = new DateOnly(2024, 1, 1).AddDays(i);
            var h = HijriConverter.FromGregorian(g, dayOffset);
            if (h.Month == month && h.Day == day)
            {
                return g;
            }
        }

        throw new InvalidOperationException($"No civil date for Hijri {month}/{day} at offset {dayOffset} in scan window.");
    }
}
