using System.Text.Json;
using System.Text.Json.Serialization;
using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt.Core.Storage;

public static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static AppSettings CreateDefault()
    {
        var kennadsa = KnownLocations.Kennadsa;
        return new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Language = "ar",
            Theme = "system",
            ActiveLocationId = kennadsa.Id,
            ActiveCalculationProfileId = CalculationMethods.AlgeriaId,
            AsrMadhab = AsrMadhab.Standard,
            AutoStartEnabled = true,
            ShowMissedAlertOnResume = true,
            Locations =
            [
                kennadsa,
                KnownLocations.Bechar,
                KnownLocations.Oran,
                KnownLocations.Algiers
            ],
            NotificationRules = [],
            DefaultAudio = new AudioProfile(),
            DefaultOverlay = new OverlayProfile()
        };
    }

    public static string Serialize(AppSettings settings)
        => JsonSerializer.Serialize(settings, Options);

    public static AppSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, Options)
                       ?? CreateDefault();

        return Normalize(settings);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        var locations = settings.Locations is { Count: > 0 }
            ? settings.Locations.ToList()
            : CreateDefault().Locations.ToList();

        var activeId = settings.ActiveLocationId;
        if (activeId is null || locations.All(l => l.Id != activeId))
        {
            activeId = locations[0].Id;
        }

        var methodId = string.IsNullOrWhiteSpace(settings.ActiveCalculationProfileId)
            ? CalculationMethods.AlgeriaId
            : settings.ActiveCalculationProfileId;

        return new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Language = string.IsNullOrWhiteSpace(settings.Language) ? "ar" : settings.Language,
            Theme = string.IsNullOrWhiteSpace(settings.Theme) ? "system" : settings.Theme,
            ActiveLocationId = activeId,
            ActiveCalculationProfileId = methodId,
            AsrMadhab = settings.AsrMadhab,
            AutoStartEnabled = settings.AutoStartEnabled,
            ShowMissedAlertOnResume = settings.ShowMissedAlertOnResume,
            HijriDayOffset = settings.HijriDayOffset,
            Locations = locations,
            NotificationRules = settings.NotificationRules ?? [],
            DefaultAudio = settings.DefaultAudio ?? new AudioProfile(),
            DefaultOverlay = settings.DefaultOverlay ?? new OverlayProfile()
        };
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
        return options;
    }
}

file sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm:ss";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return TimeOnly.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, System.Globalization.CultureInfo.InvariantCulture));
}
