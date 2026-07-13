namespace Samt.Core.Domain;

/// <summary>Domain stubs for later phases (scheduling / audio / overlay).</summary>
public enum NotificationEventKind
{
    BeforePrayer = 0,
    PrayerStart = 1,
    Dhikr = 2,
    Friday = 3
}

[Flags]
public enum NotificationChannel
{
    None = 0,
    WindowsToast = 1,
    Overlay = 2,
    Audio = 4,
    All = WindowsToast | Overlay | Audio
}

public sealed class AudioProfile
{
    public AudioSource Source { get; init; } = AudioSource.WindowsDefault;
    public string? FilePath { get; init; }
    public bool Loop { get; init; }
}

public enum AudioSource
{
    Silent = 0,
    WindowsDefault = 1,
    Bundled = 2,
    LocalFile = 3
}

public sealed class OverlayProfile
{
    public bool Enabled { get; init; } = true;
    public OverlayEdge EntryEdge { get; init; } = OverlayEdge.Top;
    public TimeSpan AnimationDuration { get; init; } = TimeSpan.FromMilliseconds(280);
    public TimeSpan PostAudioHold { get; init; } = TimeSpan.FromSeconds(5);
}

public enum OverlayEdge
{
    Top = 0,
    Bottom = 1,
    Left = 2,
    Right = 3
}

public sealed class NotificationRule
{
    public required Guid Id { get; init; }
    public required NotificationEventKind Kind { get; init; }
    public IReadOnlyList<PrayerEvent> TargetPrayers { get; init; } = [];
    public int? OffsetMinutes { get; init; }
    public NotificationChannel Channels { get; init; } = NotificationChannel.All;
    public bool Enabled { get; init; } = true;
    public AudioProfile? Audio { get; init; }
    public OverlayProfile? Overlay { get; init; }
}

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Language { get; init; } = "ar";
    public string Theme { get; init; } = "system";
    public Guid? ActiveLocationId { get; init; }
    public string ActiveCalculationProfileId { get; init; } = CalculationMethods.AlgeriaId;
    public AsrMadhab AsrMadhab { get; init; } = AsrMadhab.Standard;
    public bool AutoStartEnabled { get; init; } = true;
    public bool ShowMissedAlertOnResume { get; init; } = true;
    public int HijriDayOffset { get; init; }
    public IReadOnlyList<LocationProfile> Locations { get; init; } = [];
    public IReadOnlyList<NotificationRule> NotificationRules { get; init; } = [];
    public AudioProfile DefaultAudio { get; init; } = new();
    public OverlayProfile DefaultOverlay { get; init; } = new();

    public LocationProfile? GetActiveLocation()
    {
        if (Locations.Count == 0)
        {
            return null;
        }

        if (ActiveLocationId is { } id)
        {
            var match = Locations.FirstOrDefault(l => l.Id == id);
            if (match is not null)
            {
                return match;
            }
        }

        return Locations[0];
    }

    public CalculationProfile GetActiveCalculationProfile()
        => CalculationMethods.GetById(ActiveCalculationProfileId).WithAsr(AsrMadhab);

    public AppSettings With(
        string? language = null,
        string? theme = null,
        Guid? activeLocationId = null,
        bool replaceActiveLocationId = false,
        string? activeCalculationProfileId = null,
        AsrMadhab? asrMadhab = null,
        IReadOnlyList<LocationProfile>? locations = null,
        bool? autoStartEnabled = null,
        bool? showMissedAlertOnResume = null,
        int? hijriDayOffset = null)
        => new()
        {
            SchemaVersion = CurrentSchemaVersion,
            Language = language ?? Language,
            Theme = theme ?? Theme,
            ActiveLocationId = replaceActiveLocationId ? activeLocationId : activeLocationId ?? ActiveLocationId,
            ActiveCalculationProfileId = activeCalculationProfileId ?? ActiveCalculationProfileId,
            AsrMadhab = asrMadhab ?? AsrMadhab,
            AutoStartEnabled = autoStartEnabled ?? AutoStartEnabled,
            ShowMissedAlertOnResume = showMissedAlertOnResume ?? ShowMissedAlertOnResume,
            HijriDayOffset = hijriDayOffset ?? HijriDayOffset,
            Locations = locations ?? Locations,
            NotificationRules = NotificationRules,
            DefaultAudio = DefaultAudio,
            DefaultOverlay = DefaultOverlay
        };
}
