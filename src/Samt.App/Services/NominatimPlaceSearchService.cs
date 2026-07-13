using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt_App.Services;

/// <summary>
/// Free open place-name search via OpenStreetMap Nominatim.
/// Requires network; never blocks prayer calculation. Manual entry remains the offline fallback.
/// Policy: https://operations.osmfoundation.org/policies/nominatim/
/// </summary>
public sealed class NominatimPlaceSearchService
{
    private static readonly HttpClient Http = CreateClient();
    private static DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Nominatim: max 1 request per second for bulk; we pace conservatively.
            var since = DateTimeOffset.UtcNow - _lastRequestUtc;
            if (since < TimeSpan.FromSeconds(1.1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1.1) - since, ct).ConfigureAwait(false);
            }

            var url =
                "https://nominatim.openstreetmap.org/search?"
                + "format=jsonv2&limit=8&addressdetails=1&q="
                + Uri.EscapeDataString(query.Trim());

            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            _lastRequestUtc = DateTimeOffset.UtcNow;
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var rows = await JsonSerializer.DeserializeAsync<List<NominatimRow>>(stream, JsonOptions, ct)
                .ConfigureAwait(false);
            if (rows is null || rows.Count == 0)
            {
                return [];
            }

            var results = new List<PlaceSearchResult>();
            foreach (var row in rows)
            {
                if (!double.TryParse(row.Lat, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(row.Lon, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lon))
                {
                    continue;
                }

                if (lat is < -90 or > 90 || lon is < -180 or > 180)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(row.DisplayName)
                    ? $"{lat:F4}, {lon:F4}"
                    : row.DisplayName.Trim();

                // Keep display names usable in the list (first two comma segments).
                var shortName = ShortenDisplayName(name);

                var countryCode = NormalizeCountryCode(
                    row.Address?.CountryCode ?? row.CountryCode);

                results.Add(new PlaceSearchResult
                {
                    DisplayName = shortName,
                    FullDisplayName = name,
                    Latitude = lat,
                    Longitude = lon,
                    TimeZoneId = GuessTimeZone(lat, lon),
                    CountryCode = countryCode
                });
            }

            return results;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static LocationProfile ToProfile(PlaceSearchResult place)
        => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = place.DisplayName,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            TimeZoneId = place.TimeZoneId,
            Source = LocationSource.PlaceSearch,
            CountryCode = place.CountryCode
        };

    private static string? NormalizeCountryCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToUpperInvariant();
    }

    private static string ShortenDisplayName(string name)
    {
        var parts = name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
        {
            return name.Length > 80 ? name[..80] + "…" : name;
        }

        var shortName = $"{parts[0]}, {parts[^1]}";
        return shortName.Length > 80 ? shortName[..80] + "…" : shortName;
    }

    private static string GuessTimeZone(double latitude, double longitude)
    {
        // Prefer Algeria zone for Maghreb-ish coords; otherwise local OS zone.
        if (latitude is >= 18.9 and <= 37.5 && longitude is >= -8.7 and <= 12.0)
        {
            return KnownLocations.AlgeriaTimeZoneId;
        }

        // Morocco often uses Africa/Casablanca (UTC+1 with DST quirks) — use system if uncertain.
        if (latitude is >= 27.5 and <= 36.0 && longitude is >= -13.5 and <= -0.9)
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");
                return "Morocco Standard Time";
            }
            catch
            {
                // fall through
            }
        }

        return TimeZoneInfo.Local.Id;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // Nominatim requires a valid identifying User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SAMT-Windows/1.0 (personal prayer app; https://github.com/MotraniDev/Samt)");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar,en,fr,es");
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class NominatimRow
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}

public sealed class PlaceSearchResult
{
    public required string DisplayName { get; init; }
    public string FullDisplayName { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;

    /// <summary>ISO-style country code from Nominatim when available (best-effort).</summary>
    public string? CountryCode { get; init; }

    public string CoordinateText =>
        $"{Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}";
}
