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
            NotificationRules = CreateDefaultNotificationRules(),
            DefaultAudio = CreateDefaultAudio(),
            DefaultOverlay = CreateDefaultOverlay()
        };
    }

    /// <summary>Soft system tone; replace with licensed Bundled/LocalFile adhan when available.</summary>
    public static AudioProfile CreateDefaultAudio()
        => new()
        {
            Source = AudioSource.WindowsDefault,
            Loop = false
        };

    /// <summary>Bottom dock entry for prayer-start overlays; hold a few seconds after audio.</summary>
    public static OverlayProfile CreateDefaultOverlay()
        => new()
        {
            Enabled = true,
            EntryEdge = OverlayEdge.Bottom,
            AnimationDuration = TimeSpan.FromMilliseconds(280),
            PostAudioHold = TimeSpan.FromSeconds(5),
            Opacity = 0.94
        };

    /// <summary>
    /// Prayer-start: toast + overlay + audio.
    /// Pre-alert (15 min): toast + top ribbon overlay (no audio).
    /// </summary>
    public static IReadOnlyList<NotificationRule> CreateDefaultNotificationRules()
    {
        var five = new[]
        {
            PrayerEvent.Fajr,
            PrayerEvent.Dhuhr,
            PrayerEvent.Asr,
            PrayerEvent.Maghrib,
            PrayerEvent.Isha
        };

        return
        [
            new NotificationRule
            {
                Id = Guid.Parse("b1111111-1111-4111-8111-111111111111"),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = five,
                Channels = NotificationChannel.All,
                Enabled = true
            },
            new NotificationRule
            {
                Id = Guid.Parse("b2222222-2222-4222-8222-222222222222"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = five,
                OffsetMinutes = 15,
                Channels = NotificationChannel.WindowsToast | NotificationChannel.Overlay,
                Enabled = true
            }
        ];
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
            NotificationRules = settings.NotificationRules is { Count: > 0 }
                ? UpgradeLegacyDefaultRules(settings.NotificationRules)
                : CreateDefaultNotificationRules(),
            DefaultAudio = settings.DefaultAudio ?? CreateDefaultAudio(),
            DefaultOverlay = settings.DefaultOverlay ?? CreateDefaultOverlay()
        };
    }

    /// <summary>
    /// Phase 3 defaults used toast-only on fixed rule GUIDs. Promote those to Phase 4 channels
    /// without clobbering custom rules the user may have edited.
    /// </summary>
    private static IReadOnlyList<NotificationRule> UpgradeLegacyDefaultRules(
        IReadOnlyList<NotificationRule> rules)
    {
        var startId = Guid.Parse("b1111111-1111-4111-8111-111111111111");
        var beforeId = Guid.Parse("b2222222-2222-4222-8222-222222222222");
        var list = new List<NotificationRule>(rules.Count);

        foreach (var rule in rules)
        {
            if (rule.Id == startId
                && rule.Kind == NotificationEventKind.PrayerStart
                && rule.Channels == NotificationChannel.WindowsToast)
            {
                list.Add(new NotificationRule
                {
                    Id = rule.Id,
                    Kind = rule.Kind,
                    TargetPrayers = rule.TargetPrayers,
                    OffsetMinutes = rule.OffsetMinutes,
                    Channels = NotificationChannel.All,
                    Enabled = rule.Enabled,
                    Audio = rule.Audio,
                    Overlay = rule.Overlay
                });
                continue;
            }

            if (rule.Id == beforeId
                && rule.Kind == NotificationEventKind.BeforePrayer
                && rule.Channels == NotificationChannel.WindowsToast)
            {
                list.Add(new NotificationRule
                {
                    Id = rule.Id,
                    Kind = rule.Kind,
                    TargetPrayers = rule.TargetPrayers,
                    OffsetMinutes = rule.OffsetMinutes,
                    Channels = NotificationChannel.WindowsToast | NotificationChannel.Overlay,
                    Enabled = rule.Enabled,
                    Audio = rule.Audio,
                    Overlay = rule.Overlay
                });
                continue;
            }

            list.Add(rule);
        }

        return list;
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
