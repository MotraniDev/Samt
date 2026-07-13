using Windows.Devices.Geolocation;
using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt_App.Services;

public enum LocationPermissionState
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    Unspecified = 3
}

public sealed class GeolocationResult
{
    public required bool Success { get; init; }
    public LocationPermissionState Permission { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Optional Windows location. Manual coordinates always remain available (privacy-first).
/// </summary>
public sealed class WindowsGeolocationService
{
    public async Task<LocationPermissionState> RequestPermissionAsync()
    {
        var access = await Geolocator.RequestAccessAsync();
        return access switch
        {
            GeolocationAccessStatus.Allowed => LocationPermissionState.Allowed,
            GeolocationAccessStatus.Denied => LocationPermissionState.Denied,
            GeolocationAccessStatus.Unspecified => LocationPermissionState.Unspecified,
            _ => LocationPermissionState.Unknown
        };
    }

    public async Task<GeolocationResult> TryGetPositionAsync()
    {
        var permission = await RequestPermissionAsync();
        if (permission != LocationPermissionState.Allowed)
        {
            return new GeolocationResult
            {
                Success = false,
                Permission = permission,
                ErrorMessage = "Location permission was not granted."
            };
        }

        try
        {
            var locator = new Geolocator
            {
                DesiredAccuracyInMeters = 500
            };
            var position = await locator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(5),
                timeout: TimeSpan.FromSeconds(15));

            var coord = position.Coordinate.Point.Position;
            return new GeolocationResult
            {
                Success = true,
                Permission = LocationPermissionState.Allowed,
                Latitude = coord.Latitude,
                Longitude = coord.Longitude
            };
        }
        catch (Exception ex)
        {
            return new GeolocationResult
            {
                Success = false,
                Permission = permission,
                ErrorMessage = ex.Message
            };
        }
    }

    public static LocationProfile CreateProfileFromGps(double latitude, double longitude, string? displayName = null)
        => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName ?? "GPS",
            Latitude = latitude,
            Longitude = longitude,
            // Prefer Algeria zone when coordinates fall roughly in DZ; otherwise system zone.
            TimeZoneId = LooksLikeAlgeria(latitude, longitude)
                ? KnownLocations.AlgeriaTimeZoneId
                : TimeZoneInfo.Local.Id,
            Source = LocationSource.Gps
        };

    private static bool LooksLikeAlgeria(double latitude, double longitude)
        => latitude is >= 18.9 and <= 37.2 && longitude is >= -8.7 and <= 12.0;
}
