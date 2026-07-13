namespace Samt.Core.Domain;

/// <summary>Named calculation presets used by SAMT.</summary>
public static class CalculationMethods
{
    public const string AlgeriaId = "algeria";
    public const string MwlId = "mwl";
    public const string IsnaId = "isna";
    public const string EgyptId = "egypt";
    public const string UmmAlQuraId = "umm_al_qura";
    public const string KarachiId = "karachi";
    public const string CustomId = "custom";

    /// <summary>Algerian Ministry style: Fajr 18°, Isha 17°. Not claimed as an official endorsement.</summary>
    public static CalculationProfile Algeria { get; } = new()
    {
        Id = AlgeriaId,
        DisplayName = "Algeria (18°/17°)",
        FajrAngleDegrees = 18,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.Angle(17),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static CalculationProfile MuslimWorldLeague { get; } = new()
    {
        Id = MwlId,
        DisplayName = "Muslim World League",
        FajrAngleDegrees = 18,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.Angle(17),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static CalculationProfile Isna { get; } = new()
    {
        Id = IsnaId,
        DisplayName = "ISNA",
        FajrAngleDegrees = 15,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.Angle(15),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static CalculationProfile Egypt { get; } = new()
    {
        Id = EgyptId,
        DisplayName = "Egyptian General Authority",
        FajrAngleDegrees = 19.5,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.Angle(17.5),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static CalculationProfile UmmAlQura { get; } = new()
    {
        Id = UmmAlQuraId,
        DisplayName = "Umm al-Qura",
        FajrAngleDegrees = 18.5,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.MinutesAfterReference(90),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static CalculationProfile Karachi { get; } = new()
    {
        Id = KarachiId,
        DisplayName = "University of Islamic Sciences, Karachi",
        FajrAngleDegrees = 18,
        Maghrib = TwilightRule.Sunset(),
        Isha = TwilightRule.Angle(18),
        AsrMadhab = AsrMadhab.Standard,
        HighLatitudeRule = HighLatitudeRule.AngleBased
    };

    public static IReadOnlyList<CalculationProfile> AllPresets { get; } =
    [
        Algeria,
        MuslimWorldLeague,
        Isna,
        Egypt,
        UmmAlQura,
        Karachi
    ];

    public static CalculationProfile GetById(string id)
        => AllPresets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
           ?? Algeria;

    public static CalculationProfile WithAsr(this CalculationProfile profile, AsrMadhab madhab)
        => Clone(profile, madhab: madhab);

    public static CalculationProfile WithHighLatitude(this CalculationProfile profile, HighLatitudeRule rule)
        => Clone(profile, highLatitude: rule);

    public static CalculationProfile WithAdjustments(
        this CalculationProfile profile,
        IReadOnlyDictionary<PrayerEvent, int> adjustments)
        => Clone(profile, adjustments: adjustments);

    public static CalculationProfile Custom(
        double fajrAngle,
        TwilightRule maghrib,
        TwilightRule isha,
        AsrMadhab asr = AsrMadhab.Standard,
        HighLatitudeRule highLatitude = HighLatitudeRule.AngleBased,
        IReadOnlyDictionary<PrayerEvent, int>? adjustments = null,
        int imsakOffsetMinutes = 10)
        => new()
        {
            Id = CustomId,
            DisplayName = "Custom",
            FajrAngleDegrees = fajrAngle,
            Maghrib = maghrib,
            Isha = isha,
            AsrMadhab = asr,
            HighLatitudeRule = highLatitude,
            MinuteAdjustments = adjustments ?? new Dictionary<PrayerEvent, int>(),
            ImsakOffsetMinutes = imsakOffsetMinutes
        };

    private static CalculationProfile Clone(
        CalculationProfile source,
        AsrMadhab? madhab = null,
        HighLatitudeRule? highLatitude = null,
        IReadOnlyDictionary<PrayerEvent, int>? adjustments = null)
        => new()
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            FajrAngleDegrees = source.FajrAngleDegrees,
            Maghrib = source.Maghrib,
            Isha = source.Isha,
            AsrMadhab = madhab ?? source.AsrMadhab,
            HighLatitudeRule = highLatitude ?? source.HighLatitudeRule,
            RoundMode = source.RoundMode,
            AltitudeMeters = source.AltitudeMeters,
            ImsakOffsetMinutes = source.ImsakOffsetMinutes,
            MinuteAdjustments = adjustments ?? source.MinuteAdjustments
        };
}
