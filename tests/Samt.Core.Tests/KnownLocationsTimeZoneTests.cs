using Samt.Core.Locations;

namespace Samt.Core.Tests;

public class KnownLocationsTimeZoneTests
{
    [Fact]
    public void ToIanaTimeZoneId_maps_algeria_windows_id()
    {
        var iana = KnownLocations.ToIanaTimeZoneId(KnownLocations.AlgeriaTimeZoneId);
        Assert.Contains('/', iana);
        Assert.StartsWith("Africa/", iana);
    }

    [Fact]
    public void ToIanaTimeZoneId_passes_through_iana()
    {
        Assert.Equal("Africa/Algiers", KnownLocations.ToIanaTimeZoneId("Africa/Algiers"));
    }

    [Fact]
    public void ToIanaTimeZoneId_prefers_algeria_region_when_provided()
    {
        var iana = KnownLocations.ToIanaTimeZoneId(KnownLocations.AlgeriaTimeZoneId, region: "DZ");
        Assert.Equal("Africa/Algiers", iana);
    }
}
