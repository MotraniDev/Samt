namespace Samt.Core.Adhkar;

/// <summary>Named offline remembrance set.</summary>
public enum AdhkarCollectionKind
{
    Morning = 0,
    Evening = 1,
    AfterPrayer = 2,
    Sleep = 3
}

/// <summary>One remembrance item (Arabic primary; translations optional).</summary>
public sealed class AdhkarItem
{
    public required string Id { get; init; }
    public required string ArabicText { get; init; }
    public string? TranslationKey { get; init; }
    public string? Transliteration { get; init; }
    public int? RepeatCount { get; init; }
}

/// <summary>Offline collection of items.</summary>
public sealed class AdhkarCollection
{
    public required AdhkarCollectionKind Kind { get; init; }
    public required string TitleKey { get; init; }
    public required IReadOnlyList<AdhkarItem> Items { get; init; }
}
