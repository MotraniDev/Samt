namespace Samt.Core.Domain;

/// <summary>Notification kinds used by rules and the planner.</summary>
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

    /// <summary>Catalog id from the sound library (preferred over FilePath when set).</summary>
    public string? SoundId { get; init; }

    public bool Loop { get; init; }
}

public enum AudioSource
{
    Silent = 0,
    WindowsDefault = 1,
    Bundled = 2,
    LocalFile = 3,
    /// <summary>Resolve via App sound library catalog id (<see cref="AudioProfile.SoundId"/>).</summary>
    Library = 4
}

public sealed class OverlayProfile
{
    public bool Enabled { get; init; } = true;
    public OverlayEdge EntryEdge { get; init; } = OverlayEdge.Top;
    public TimeSpan AnimationDuration { get; init; } = TimeSpan.FromMilliseconds(280);
    public TimeSpan PostAudioHold { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Window opacity 0.30–1.0 (applied via layered HWND alpha).</summary>
    public double Opacity { get; init; } = 0.94;
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

    /// <summary>Full adhan / prayer-start sound library id.</summary>
    public string AdhanSoundId { get; init; } = BuiltInSoundIds.AdhanAlaqsa;

    /// <summary>Pre-alert cue (takbir, hayya, soft tone, or user sound).</summary>
    public string PreAlertSoundId { get; init; } = BuiltInSoundIds.Takbir;

    /// <summary>User-added sounds (copied under LocalAppData\SAMT\sounds).</summary>
    public IReadOnlyList<UserSoundEntry> UserSounds { get; init; } = [];

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
        int? hijriDayOffset = null,
        IReadOnlyList<NotificationRule>? notificationRules = null,
        AudioProfile? defaultAudio = null,
        OverlayProfile? defaultOverlay = null,
        string? adhanSoundId = null,
        string? preAlertSoundId = null,
        IReadOnlyList<UserSoundEntry>? userSounds = null)
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
            NotificationRules = notificationRules ?? NotificationRules,
            DefaultAudio = defaultAudio ?? DefaultAudio,
            DefaultOverlay = defaultOverlay ?? DefaultOverlay,
            AdhanSoundId = adhanSoundId ?? AdhanSoundId,
            PreAlertSoundId = preAlertSoundId ?? PreAlertSoundId,
            UserSounds = userSounds ?? UserSounds
        };
}
