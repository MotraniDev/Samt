using Samt.Core.Calendar;

namespace Samt.Core.Tests;

public class SpecialDayCatalogTests
{
    [Fact]
    public void IslamicCatalog_ContainsLeanSetWithStableIds()
    {
        var ids = IslamicObservanceCatalog.All.Select(d => d.Id).ToList();

        Assert.Equal(13, ids.Count);
        Assert.Contains("islamic.new_year", ids);
        Assert.Contains("islamic.eid_fitr", ids);
        Assert.Contains("islamic.eid_adha", ids);
        Assert.Contains("islamic.tashreeq_13", ids);
        Assert.Contains("islamic.laylat_al_qadr_marker", ids);
        Assert.All(IslamicObservanceCatalog.All, d =>
        {
            Assert.Equal(SpecialDaySource.Islamic, d.Source);
            Assert.True(d.HijriMonth is >= 1 and <= 12);
            Assert.True(d.HijriDay is >= 1 and <= 30);
            Assert.StartsWith("SpecialDay.", d.DisplayKey, StringComparison.Ordinal);
            Assert.Null(d.GregorianMonth);
        });
    }

    [Fact]
    public void IslamicCatalog_MarksContestedDaysAsCommonlyObserved()
    {
        Assert.True(IslamicObservanceCatalog.TryGet("islamic.mawlid")!.IsCommonlyObserved);
        Assert.True(IslamicObservanceCatalog.TryGet("islamic.mid_shaban")!.IsCommonlyObserved);
        Assert.False(IslamicObservanceCatalog.TryGet("islamic.eid_fitr")!.IsCommonlyObserved);
    }

    [Fact]
    public void AlgeriaPackage_HasFiveCivilDays_NoIslamicDupes()
    {
        var package = CountryCalendarCatalog.TryGet("DZ");
        Assert.NotNull(package);
        Assert.Equal(5, package.Definitions.Count);
        Assert.All(package.Definitions, d =>
        {
            Assert.Equal(SpecialDaySource.Country, d.Source);
            Assert.True(d.GregorianMonth is >= 1 and <= 12);
            Assert.True(d.GregorianDay is >= 1 and <= 31);
            Assert.Null(d.HijriMonth);
            Assert.DoesNotContain("islamic.", d.Id, StringComparison.OrdinalIgnoreCase);
        });

        var md = package.Definitions
            .Select(d => (d.GregorianMonth!.Value, d.GregorianDay!.Value))
            .ToHashSet();
        Assert.Contains((1, 1), md);
        Assert.Contains((1, 12), md);
        Assert.Contains((5, 1), md);
        Assert.Contains((7, 5), md);
        Assert.Contains((11, 1), md);
    }

    [Fact]
    public void CountryCatalog_UnknownCode_GetOrDefault_ReturnsAlgeria()
    {
        Assert.Null(CountryCalendarCatalog.TryGet("FR"));
        var fallback = CountryCalendarCatalog.GetOrDefault("FR");
        Assert.Equal(CountryCalendarCatalog.AlgeriaCountryCode, fallback.CountryCode);
        Assert.Equal(5, fallback.Definitions.Count);
    }

    [Fact]
    public void CountryCatalog_IsCaseInsensitive()
    {
        var a = CountryCalendarCatalog.TryGet("dz");
        var b = CountryCalendarCatalog.TryGet("DZ");
        Assert.NotNull(a);
        Assert.Same(a, b);
    }
}
