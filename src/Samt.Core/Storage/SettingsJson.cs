using System.Text.Json;
using System.Text.Json.Serialization;
using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Notifications;
using Samt.Core.Time;

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
            DefaultOverlay = CreateDefaultOverlay(),
            AdhanSoundId = BuiltInSoundIds.AdhanAlaqsa,
            PreAlertSoundId = BuiltInSoundIds.Takbir,
            UserSounds = []
        };
    }

    /// <summary>Default prayer-start audio: library adhan (Al-Aqsa sample).</summary>
    public static AudioProfile CreateDefaultAudio()
        => new()
        {
            Source = AudioSource.Library,
            SoundId = BuiltInSoundIds.AdhanAlaqsa,
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
    /// Pre-alert: 15 min general, Fajr exception 30 min (Phase 5 defaults).
    /// </summary>
    public static IReadOnlyList<NotificationRule> CreateDefaultNotificationRules()
        => NotificationRulesComposer.Compose(
            generalBeforeMinutes: 15,
            beforeExceptions: new Dictionary<PrayerEvent, int?>
            {
                [PrayerEvent.Fajr] = 30
            },
            beforeEnabledPrayers: new HashSet<PrayerEvent>(NotificationRulesComposer.FiveDaily),
            startEnabledPrayers: new HashSet<PrayerEvent>(NotificationRulesComposer.FiveDaily),
            beforeChannels: NotificationChannel.WindowsToast | NotificationChannel.Overlay,
            startChannels: NotificationChannel.All);

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
            HijriDayOffset = HijriConverter.ClampDayOffset(settings.HijriDayOffset),
            Locations = locations,
            NotificationRules = settings.NotificationRules is { Count: > 0 }
                ? UpgradeLegacyDefaultRules(settings.NotificationRules)
                : CreateDefaultNotificationRules(),
            DefaultAudio = NormalizeAudio(settings.DefaultAudio),
            DefaultOverlay = settings.DefaultOverlay ?? CreateDefaultOverlay(),
            AdhanSoundId = string.IsNullOrWhiteSpace(settings.AdhanSoundId)
                ? BuiltInSoundIds.AdhanAlaqsa
                : settings.AdhanSoundId.Trim(),
            PreAlertSoundId = string.IsNullOrWhiteSpace(settings.PreAlertSoundId)
                ? BuiltInSoundIds.Takbir
                : settings.PreAlertSoundId.Trim(),
            UserSounds = NormalizeUserSounds(settings.UserSounds)
        };
    }

    private static AudioProfile NormalizeAudio(AudioProfile? audio)
    {
        if (audio is null)
        {
            return CreateDefaultAudio();
        }

        // Promote legacy soft-tone defaults to library adhan.
        if (audio.Source is AudioSource.WindowsDefault or AudioSource.Bundled
            && string.IsNullOrWhiteSpace(audio.SoundId)
            && string.IsNullOrWhiteSpace(audio.FilePath))
        {
            return CreateDefaultAudio();
        }

        return audio;
    }

    private static IReadOnlyList<UserSoundEntry> NormalizeUserSounds(IReadOnlyList<UserSoundEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        return entries
            .Where(e => e is not null
                        && !string.IsNullOrWhiteSpace(e.Id)
                        && !string.IsNullOrWhiteSpace(e.FilePath))
            .Select(e => new UserSoundEntry
            {
                Id = e.Id.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(e.DisplayName) ? e.Id : e.DisplayName.Trim(),
                FilePath = e.FilePath.Trim()
            })
            .ToList();
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
