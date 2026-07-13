using Samt.Core.Calculation;
using Samt.Core.Locations;

namespace Samt.Core.Tests;

public class QiblaCalculatorTests
{
    [Fact]
    public void AtKaaba_IsFlaggedAndNearZeroDistance()
    {
        var info = QiblaCalculator.Calculate(
            QiblaCalculator.KaabaLatitude,
            QiblaCalculator.KaabaLongitude);

        Assert.True(info.IsAtKaaba);
        Assert.True(info.DistanceKm < QiblaCalculator.AtKaabaThresholdKm);
    }

    [Fact]
    public void Kennadsa_BearingIsEastwardTowardMecca()
    {
        var loc = KnownLocations.Kennadsa;
        var info = QiblaCalculator.Calculate(loc.Latitude, loc.Longitude);

        Assert.False(info.IsAtKaaba);
        // Kennadsa is west of Mecca → qibla roughly east (NE–SE band).
        Assert.InRange(info.BearingDegrees, 45, 135);
        Assert.InRange(info.DistanceKm, 3000, 5500);
    }

    [Fact]
    public void Bearing_IsNormalizedTo0_360()
    {
        var info = QiblaCalculator.Calculate(36.75, 3.05); // Algiers area
        Assert.InRange(info.BearingDegrees, 0, 360);
        Assert.False(info.IsAtKaaba);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void InvalidCoordinates_Throw(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QiblaCalculator.Calculate(lat, lon));
    }
}
