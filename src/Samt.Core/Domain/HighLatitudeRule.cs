namespace Samt.Core.Domain;

/// <summary>How to resolve Fajr/Isha when the sun never reaches the method angles.</summary>
public enum HighLatitudeRule
{
    /// <summary>No adjustment; angles may be unavailable at extreme latitudes.</summary>
    None = 0,

    /// <summary>Middle of the night method.</summary>
    MiddleOfTheNight = 1,

    /// <summary>One-seventh of the night method.</summary>
    OneSeventhOfTheNight = 2,

    /// <summary>Angle-based portion of the night (recommended by PrayTimes).</summary>
    AngleBased = 3
}
