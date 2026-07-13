namespace Samt.Core.Domain;

/// <summary>Computed prayer times for a single civil date at a location.</summary>
public sealed class PrayerSchedule
{
    public required DateOnly Date { get; init; }

    public required Guid LocationId { get; init; }

    public required string CalculationProfileId { get; init; }

    public required string TimeZoneId { get; init; }

    /// <summary>Event → local DateTimeOffset (rounded + adjusted).</summary>
    public required IReadOnlyDictionary<PrayerEvent, DateTimeOffset> Times { get; init; }

    /// <summary>Raw second-precision times before rounding/adjustments (for diagnostics).</summary>
    public required IReadOnlyDictionary<PrayerEvent, DateTimeOffset> RawTimes { get; init; }

    public DateTimeOffset? this[PrayerEvent prayerEvent]
        => Times.TryGetValue(prayerEvent, out var value) ? value : null;

    public DateTimeOffset Fajr => Times[PrayerEvent.Fajr];
    public DateTimeOffset Sunrise => Times[PrayerEvent.Sunrise];
    public DateTimeOffset Dhuhr => Times[PrayerEvent.Dhuhr];
    public DateTimeOffset Asr => Times[PrayerEvent.Asr];
    public DateTimeOffset Maghrib => Times[PrayerEvent.Maghrib];
    public DateTimeOffset Isha => Times[PrayerEvent.Isha];
    public DateTimeOffset? Imsak => this[PrayerEvent.Imsak];
    public DateTimeOffset? Midnight => this[PrayerEvent.Midnight];
    public DateTimeOffset? Jumuah => this[PrayerEvent.Jumuah];

    public PrayerEvent? GetNextPrayer(DateTimeOffset now)
    {
        PrayerEvent? next = null;
        DateTimeOffset? nextTime = null;

        foreach (var key in NotifiablePrayers)
        {
            if (!Times.TryGetValue(key, out var time))
            {
                continue;
            }

            if (time > now && (nextTime is null || time < nextTime))
            {
                next = key;
                nextTime = time;
            }
        }

        return next;
    }

    public static IReadOnlyList<PrayerEvent> CoreDisplayOrder { get; } =
    [
        PrayerEvent.Imsak,
        PrayerEvent.Fajr,
        PrayerEvent.Sunrise,
        PrayerEvent.Dhuhr,
        PrayerEvent.Asr,
        PrayerEvent.Maghrib,
        PrayerEvent.Isha,
        PrayerEvent.Midnight
    ];

    public static IReadOnlyList<PrayerEvent> NotifiablePrayers { get; } =
    [
        PrayerEvent.Fajr,
        PrayerEvent.Dhuhr,
        PrayerEvent.Asr,
        PrayerEvent.Maghrib,
        PrayerEvent.Isha,
        PrayerEvent.Jumuah
    ];
}
