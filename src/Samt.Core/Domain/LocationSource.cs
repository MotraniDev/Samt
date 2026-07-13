namespace Samt.Core.Domain;

public enum LocationSource
{
    Manual = 0,
    Gps = 1,
    CitySeed = 2,
    /// <summary>Filled from free open place-name search (map place lookup).</summary>
    PlaceSearch = 3
}
