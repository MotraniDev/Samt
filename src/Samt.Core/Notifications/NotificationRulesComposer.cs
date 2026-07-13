using Samt.Core.Domain;

namespace Samt.Core.Notifications;

/// <summary>
/// Builds the persisted <see cref="NotificationRule"/> list from a simple UI model.
/// General before-offset + optional per-prayer exceptions; start uses the five daily prayers.
/// </summary>
public static class NotificationRulesComposer
{
    public static readonly Guid PrayerStartRuleId = Guid.Parse("b1111111-1111-4111-8111-111111111111");
    public static readonly Guid BeforeGeneralRuleId = Guid.Parse("b2222222-2222-4222-8222-222222222222");

    /// <summary>Stable ids for per-prayer before exceptions (Fajr…Isha).</summary>
    public static Guid BeforeExceptionId(PrayerEvent prayer)
        => prayer switch
        {
            PrayerEvent.Fajr => Guid.Parse("c1000001-0001-4001-8001-000000000001"),
            PrayerEvent.Dhuhr => Guid.Parse("c1000002-0002-4002-8002-000000000002"),
            PrayerEvent.Asr => Guid.Parse("c1000003-0003-4003-8003-000000000003"),
            PrayerEvent.Maghrib => Guid.Parse("c1000004-0004-4004-8004-000000000004"),
            PrayerEvent.Isha => Guid.Parse("c1000005-0005-4005-8005-000000000005"),
            _ => Guid.Parse("c1000000-0000-4000-8000-000000000000")
        };

    public static IReadOnlyList<PrayerEvent> FiveDaily { get; } =
    [
        PrayerEvent.Fajr,
        PrayerEvent.Dhuhr,
        PrayerEvent.Asr,
        PrayerEvent.Maghrib,
        PrayerEvent.Isha
    ];

    /// <param name="generalBeforeMinutes">Default pre-alert minutes (all five unless overridden).</param>
    /// <param name="beforeExceptions">
    /// Per-prayer override minutes. Missing key = use general.
    /// Value null with key present = disabled exception (cancels that prayer's pre-alert).
    /// </param>
    /// <param name="beforeEnabledPrayers">Which prayers get a pre-alert at all (after exceptions).</param>
    /// <param name="startEnabledPrayers">Which prayers fire at start.</param>
    public static IReadOnlyList<NotificationRule> Compose(
        int generalBeforeMinutes,
        IReadOnlyDictionary<PrayerEvent, int?> beforeExceptions,
        IReadOnlySet<PrayerEvent> beforeEnabledPrayers,
        IReadOnlySet<PrayerEvent> startEnabledPrayers,
        NotificationChannel beforeChannels,
        NotificationChannel startChannels,
        bool beforeAlertsEnabled = true,
        bool startAlertsEnabled = true)
    {
        if (generalBeforeMinutes < 0)
        {
            generalBeforeMinutes = 0;
        }

        var list = new List<NotificationRule>();

        var startTargets = FiveDaily.Where(startEnabledPrayers.Contains).ToArray();
        list.Add(new NotificationRule
        {
            Id = PrayerStartRuleId,
            Kind = NotificationEventKind.PrayerStart,
            TargetPrayers = startTargets,
            Channels = startChannels,
            Enabled = startAlertsEnabled && startTargets.Length > 0
        });

        var beforeTargets = FiveDaily.Where(beforeEnabledPrayers.Contains).ToArray();
        list.Add(new NotificationRule
        {
            Id = BeforeGeneralRuleId,
            Kind = NotificationEventKind.BeforePrayer,
            TargetPrayers = beforeTargets,
            OffsetMinutes = generalBeforeMinutes,
            Channels = beforeChannels,
            Enabled = beforeAlertsEnabled && beforeTargets.Length > 0 && generalBeforeMinutes > 0
        });

        foreach (var prayer in FiveDaily)
        {
            if (!beforeExceptions.TryGetValue(prayer, out var overrideMinutes))
            {
                continue;
            }

            // Present in dictionary: exception rule (enabled with minutes, or disabled cancel).
            list.Add(new NotificationRule
            {
                Id = BeforeExceptionId(prayer),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [prayer],
                OffsetMinutes = overrideMinutes ?? 0,
                Channels = beforeChannels,
                Enabled = overrideMinutes is > 0
            });
        }

        return list;
    }

    /// <summary>Parse a saved rule list back into UI-friendly fields.</summary>
    public static NotificationRulesUiModel Parse(IReadOnlyList<NotificationRule> rules)
    {
        var start = rules.FirstOrDefault(r => r.Id == PrayerStartRuleId)
                    ?? rules.FirstOrDefault(r => r.Kind == NotificationEventKind.PrayerStart);
        var general = rules.FirstOrDefault(r => r.Id == BeforeGeneralRuleId)
                      ?? rules.FirstOrDefault(r =>
                          r.Kind == NotificationEventKind.BeforePrayer
                          && r.TargetPrayers.Count != 1);

        var model = new NotificationRulesUiModel
        {
            GeneralBeforeMinutes = general?.OffsetMinutes is > 0 ? general.OffsetMinutes.Value : 15,
            BeforeAlertsEnabled = general?.Enabled ?? true,
            StartAlertsEnabled = start?.Enabled ?? true,
            BeforeChannels = general?.Channels
                             ?? (NotificationChannel.WindowsToast | NotificationChannel.Overlay),
            StartChannels = start?.Channels ?? NotificationChannel.All,
            BeforeEnabledPrayers = new HashSet<PrayerEvent>(
                general is { TargetPrayers.Count: > 0 }
                    ? general.TargetPrayers
                    : FiveDaily),
            StartEnabledPrayers = new HashSet<PrayerEvent>(
                start is { TargetPrayers.Count: > 0 }
                    ? start.TargetPrayers
                    : FiveDaily),
            BeforeExceptions = new Dictionary<PrayerEvent, int?>()
        };

        foreach (var prayer in FiveDaily)
        {
            var ex = rules.FirstOrDefault(r =>
                r.Id == BeforeExceptionId(prayer)
                || (r.Kind == NotificationEventKind.BeforePrayer
                    && r.TargetPrayers.Count == 1
                    && r.TargetPrayers[0] == prayer
                    && r.Id != BeforeGeneralRuleId));

            if (ex is null)
            {
                continue;
            }

            if (!ex.Enabled)
            {
                model.BeforeExceptions[prayer] = null;
                model.BeforeEnabledPrayers.Remove(prayer);
            }
            else if (ex.OffsetMinutes is > 0 and var m && m != model.GeneralBeforeMinutes)
            {
                model.BeforeExceptions[prayer] = m;
            }
        }

        return model;
    }
}

/// <summary>UI-facing snapshot of notification rule settings.</summary>
public sealed class NotificationRulesUiModel
{
    public int GeneralBeforeMinutes { get; set; } = 15;
    public bool BeforeAlertsEnabled { get; set; } = true;
    public bool StartAlertsEnabled { get; set; } = true;
    public NotificationChannel BeforeChannels { get; set; } =
        NotificationChannel.WindowsToast | NotificationChannel.Overlay;
    public NotificationChannel StartChannels { get; set; } = NotificationChannel.All;
    public HashSet<PrayerEvent> BeforeEnabledPrayers { get; set; } = new(NotificationRulesComposer.FiveDaily);
    public HashSet<PrayerEvent> StartEnabledPrayers { get; set; } = new(NotificationRulesComposer.FiveDaily);
    public Dictionary<PrayerEvent, int?> BeforeExceptions { get; set; } = new();
}
