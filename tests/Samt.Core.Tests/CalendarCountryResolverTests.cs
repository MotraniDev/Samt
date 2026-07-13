using Samt.Core.Calendar;

namespace Samt.Core.Tests;

public class CalendarCountryResolverTests
{
    [Fact]
    public void Resolve_OverrideWinsOverLocation()
    {
        var code = CalendarCountryResolver.Resolve(countryOverride: "dz", locationCountryCode: "FR");
        Assert.Equal("DZ", code);
    }

    [Fact]
    public void Resolve_UsesLocationWhenNoOverride()
    {
        var code = CalendarCountryResolver.Resolve(countryOverride: null, locationCountryCode: "fr");
        Assert.Equal("FR", code);
    }

    [Fact]
    public void Resolve_EmptyInputs_DefaultAlgeria()
    {
        Assert.Equal(
            CalendarCountryResolver.DefaultCountryCode,
            CalendarCountryResolver.Resolve(null, null));
        Assert.Equal(
            CalendarCountryResolver.DefaultCountryCode,
            CalendarCountryResolver.Resolve("  ", "   "));
    }

    [Fact]
    public void Resolve_WhitespaceOverride_FallsThroughToLocation()
    {
        var code = CalendarCountryResolver.Resolve(countryOverride: "\t", locationCountryCode: "MA");
        Assert.Equal("MA", code);
    }
}
