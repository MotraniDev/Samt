using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt_App.Services;

namespace Samt_App.ViewModels;

public sealed class LocationsViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly LocalizationService _localization;
    private readonly WindowsGeolocationService _geo = new();
    private LocationProfile? _selected;
    private string _name = string.Empty;
    private string _latitude = string.Empty;
    private string _longitude = string.Empty;
    private string _timeZoneId = KnownLocations.AlgeriaTimeZoneId;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public LocationsViewModel(AppState appState, LocalizationService localization)
    {
        _appState = appState;
        _localization = localization;
        ReloadList();
        _appState.SettingsChanged += (_, _) => ReloadList();
        PrefillFromActive();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LocationProfile> Locations { get; } = [];

    public IReadOnlyList<string> TimeZoneIds { get; } = BuildTimeZoneList();

    public LocationProfile? SelectedLocation
    {
        get => _selected;
        set
        {
            _selected = value;
            OnPropertyChanged();
            if (value is not null)
            {
                Name = value.DisplayName;
                Latitude = LatinDigits.Number(value.Latitude, "0.######");
                Longitude = LatinDigits.Number(value.Longitude, "0.######");
                TimeZoneId = value.TimeZoneId;
            }
        }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Latitude
    {
        get => _latitude;
        set { _latitude = LatinDigits.EnsureLatin(value); OnPropertyChanged(); }
    }

    public string Longitude
    {
        get => _longitude;
        set { _longitude = LatinDigits.EnsureLatin(value); OnPropertyChanged(); }
    }

    public string TimeZoneId
    {
        get => _timeZoneId;
        set { _timeZoneId = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = LatinDigits.EnsureLatin(value); OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public async Task UseAsActiveAsync()
    {
        if (_selected is null)
        {
            StatusMessage = _localization.Get("SelectLocationFirst");
            return;
        }

        await _appState.UpdateAsync(s => s.With(activeLocationId: _selected.Id));
        StatusMessage = _localization.Get("LocationActivated");
    }

    public async Task SaveManualAsync()
    {
        if (!TryParseCoordinates(out var lat, out var lon, out var error))
        {
            StatusMessage = error;
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = _localization.Get("NameRequired");
            return;
        }

        if (!IsValidTimeZone(TimeZoneId))
        {
            StatusMessage = _localization.Get("InvalidTimeZone");
            return;
        }

        var list = _appState.Settings.Locations.ToList();
        LocationProfile profile;

        if (_selected is not null && list.Any(l => l.Id == _selected.Id))
        {
            profile = new LocationProfile
            {
                Id = _selected.Id,
                DisplayName = Name.Trim(),
                Latitude = lat,
                Longitude = lon,
                TimeZoneId = TimeZoneId,
                Source = LocationSource.Manual,
                FridayTimeMode = _selected.FridayTimeMode,
                FixedFridayLocalTime = _selected.FixedFridayLocalTime,
                SuppressDhuhrNotificationsOnFriday = _selected.SuppressDhuhrNotificationsOnFriday,
                AltitudeMeters = _selected.AltitudeMeters
            };
            var index = list.FindIndex(l => l.Id == profile.Id);
            list[index] = profile;
        }
        else
        {
            profile = new LocationProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = Name.Trim(),
                Latitude = lat,
                Longitude = lon,
                TimeZoneId = TimeZoneId,
                Source = LocationSource.Manual
            };
            list.Add(profile);
        }

        await _appState.UpdateAsync(s => s.With(
            locations: list,
            activeLocationId: profile.Id));

        SelectedLocation = profile;
        StatusMessage = _localization.Get("LocationSaved");
    }

    public async Task DeleteSelectedAsync()
    {
        if (_selected is null)
        {
            return;
        }

        var list = _appState.Settings.Locations.Where(l => l.Id != _selected.Id).ToList();
        if (list.Count == 0)
        {
            StatusMessage = _localization.Get("CannotDeleteLastLocation");
            return;
        }

        var newActive = list[0].Id;
        await _appState.UpdateAsync(s => s.With(
            locations: list,
            activeLocationId: newActive,
            replaceActiveLocationId: true));

        SelectedLocation = list[0];
        StatusMessage = _localization.Get("LocationDeleted");
    }

    public void NewManual()
    {
        SelectedLocation = null;
        Name = string.Empty;
        Latitude = string.Empty;
        Longitude = string.Empty;
        TimeZoneId = KnownLocations.AlgeriaTimeZoneId;
        StatusMessage = _localization.Get("EnterCoordinatesHint");
    }

    public async Task DetectGpsAsync()
    {
        IsBusy = true;
        StatusMessage = _localization.Get("RequestingLocation");
        try
        {
            var result = await _geo.TryGetPositionAsync();
            if (!result.Success || result.Latitude is null || result.Longitude is null)
            {
                StatusMessage = result.Permission == LocationPermissionState.Denied
                    ? _localization.Get("LocationDenied")
                    : (_localization.Get("LocationFailed") + (result.ErrorMessage is null ? "" : $" ({result.ErrorMessage})"));
                return;
            }

            var profile = WindowsGeolocationService.CreateProfileFromGps(
                result.Latitude.Value,
                result.Longitude.Value,
                _localization.Get("GpsLocationName"));

            var list = _appState.Settings.Locations.ToList();
            // Replace previous GPS entry if any
            list = list.Where(l => l.Source != LocationSource.Gps).ToList();
            list.Insert(0, profile);

            await _appState.UpdateAsync(s => s.With(
                locations: list,
                activeLocationId: profile.Id));

            SelectedLocation = profile;
            StatusMessage = _localization.Get("LocationFromGps");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReloadList()
    {
        Locations.Clear();
        foreach (var location in _appState.Settings.Locations)
        {
            Locations.Add(location);
        }

        var activeId = _appState.Settings.ActiveLocationId;
        SelectedLocation = Locations.FirstOrDefault(l => l.Id == activeId) ?? Locations.FirstOrDefault();
        OnPropertyChanged(nameof(Locations));
    }

    private void PrefillFromActive()
    {
        var active = _appState.TryGetActiveLocation();
        if (active is not null)
        {
            SelectedLocation = active;
        }
    }

    private bool TryParseCoordinates(out double lat, out double lon, out string error)
    {
        lat = 0;
        lon = 0;
        error = string.Empty;

        var latText = LatinDigits.EnsureLatin(Latitude).Trim();
        var lonText = LatinDigits.EnsureLatin(Longitude).Trim();

        if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            || lat is < -90 or > 90)
        {
            error = _localization.Get("InvalidLatitude");
            return false;
        }

        if (!double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
            || lon is < -180 or > 180)
        {
            error = _localization.Get("InvalidLongitude");
            return false;
        }

        return true;
    }

    private static bool IsValidTimeZone(string id)
    {
        try
        {
            _ = KnownLocations.ResolveTimeZone(id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> BuildTimeZoneList()
    {
        var preferred = new[]
        {
            KnownLocations.AlgeriaTimeZoneId,
            "Morocco Standard Time",
            "UTC",
            "GMT Standard Time",
            "W. Europe Standard Time",
            "Central Europe Standard Time",
            "Egypt Standard Time",
            "Arabic Standard Time",
            "Arab Standard Time",
            "Arabian Standard Time",
            "Turkey Standard Time",
            "Pakistan Standard Time",
            "India Standard Time",
            "Singapore Standard Time",
            "China Standard Time",
            "Tokyo Standard Time",
            "AUS Eastern Standard Time",
            "Eastern Standard Time",
            "Central Standard Time",
            "Pacific Standard Time"
        };

        var all = TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = preferred.Where(all.Contains).ToList();
        foreach (var id in all.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!list.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(id);
            }
        }

        return list;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
