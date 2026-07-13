namespace Samt.Core.Adhkar;

/// <summary>
/// Named offline remembrance collection. Structure mirrors common Hisn / azkar.me sections.
/// Scheduled reminder kinds: Morning, Evening, AfterPrayer, Sleep.
/// </summary>
public enum AdhkarCollectionKind
{
    Morning = 0,
    Evening = 1,
    AfterPrayer = 2,
    Sleep = 3,
    WakeUp = 4,
    Tasbeeh = 5,
    Istighfar = 6,
    Salawat = 7,
    GeneralDuas = 8,
    Home = 9,
    Adhan = 10,
    Mosque = 11,
    Travel = 12,
    Food = 13,
    Clothing = 14,
    Sickness = 15,
    Distress = 16,
    Misc = 17,
    Prayer = 18,
    QuranDuas = 19,
    SunnahDuas = 20
}

/// <summary>Browse group for the Adhkar library (مكتبة الأذكار).</summary>
public enum AdhkarLibraryGroup
{
    Daily = 0,
    PrayerRelated = 1,
    LifeSituations = 2,
    PraiseAndDuas = 3
}

/// <summary>One remembrance item (Arabic primary).</summary>
public sealed class AdhkarItem
{
    public required string Id { get; init; }
    public required string ArabicText { get; init; }

    /// <summary>Optional localization key for translation line.</summary>
    public string? TranslationKey { get; init; }

    /// <summary>Optional benefit / note (Arabic or key).</summary>
    public string? BenefitText { get; init; }

    /// <summary>Target repetitions for this item (1 = once).</summary>
    public int RepeatCount { get; init; } = 1;

    /// <summary>Short reference (e.g. آية الكرسي — البقرة 255).</summary>
    public string? Reference { get; init; }
}

/// <summary>Offline collection of items with library metadata.</summary>
public sealed class AdhkarCollection
{
    public required AdhkarCollectionKind Kind { get; init; }
    public required string TitleKey { get; init; }
    public required IReadOnlyList<AdhkarItem> Items { get; init; }

    /// <summary>Library grouping for browsing.</summary>
    public AdhkarLibraryGroup Group { get; init; } = AdhkarLibraryGroup.Daily;

    /// <summary>Whether scheduled reminders may open this collection.</summary>
    public bool IsSchedulable { get; init; }

    /// <summary>Emoji / glyph hint for library cards.</summary>
    public string IconHint { get; init; } = "📿";

    /// <summary>Attribution key (e.g. source line for UI).</summary>
    public string SourceKey { get; init; } = "Adhkar.Source.AzkarMe";
}
