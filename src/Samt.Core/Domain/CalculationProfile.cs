namespace Samt.Core.Domain;

/// <summary>Method parameters used by the local prayer engine.</summary>
public sealed class CalculationProfile
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required double FajrAngleDegrees { get; init; }

    public required TwilightRule Maghrib { get; init; }

    public required TwilightRule Isha { get; init; }

    public AsrMadhab AsrMadhab { get; init; } = AsrMadhab.Standard;

    public HighLatitudeRule HighLatitudeRule { get; init; } = HighLatitudeRule.AngleBased;

    public RoundMode RoundMode { get; init; } = RoundMode.NearestMinute;

    /// <summary>Optional altitude override when location has none.</summary>
    public double? AltitudeMeters { get; init; }

    /// <summary>Default imsak offset before Fajr (minutes). Personal default: 10.</summary>
    public int ImsakOffsetMinutes { get; init; } = 10;

    /// <summary>Signed minute adjustments applied after rounding, keyed by event.</summary>
    public IReadOnlyDictionary<PrayerEvent, int> MinuteAdjustments { get; init; }
        = new Dictionary<PrayerEvent, int>();

    public int GetAdjustment(PrayerEvent prayerEvent)
        => MinuteAdjustments.TryGetValue(prayerEvent, out var minutes) ? minutes : 0;
}
