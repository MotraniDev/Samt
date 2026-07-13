namespace Samt.Core.Calendar;

/// <summary>
/// Lean universal Islamic observances (tabular Hijri anchors). Not a fatwa source.
/// </summary>
public static class IslamicObservanceCatalog
{
    private static readonly Lazy<IReadOnlyList<SpecialDayDefinition>> LazyAll = new(CreateAll);

    public static IReadOnlyList<SpecialDayDefinition> All => LazyAll.Value;

    public static SpecialDayDefinition? TryGet(string id)
        => All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<SpecialDayDefinition> CreateAll()
        =>
        [
            Islamic("islamic.new_year", month: 1, day: 1),
            Islamic("islamic.ashura", month: 1, day: 10),
            Islamic("islamic.mawlid", month: 3, day: 12, commonlyObserved: true),
            Islamic("islamic.isra_miraj", month: 7, day: 27, commonlyObserved: true),
            Islamic("islamic.mid_shaban", month: 8, day: 15, commonlyObserved: true),
            Islamic("islamic.ramadan_start", month: 9, day: 1),
            Islamic("islamic.laylat_al_qadr_marker", month: 9, day: 27, commonlyObserved: true),
            Islamic("islamic.eid_fitr", month: 10, day: 1),
            Islamic("islamic.arafah", month: 12, day: 9),
            Islamic("islamic.eid_adha", month: 12, day: 10),
            Islamic("islamic.tashreeq_11", month: 12, day: 11),
            Islamic("islamic.tashreeq_12", month: 12, day: 12),
            Islamic("islamic.tashreeq_13", month: 12, day: 13)
        ];

    private static SpecialDayDefinition Islamic(string id, int month, int day, bool commonlyObserved = false)
        => new()
        {
            Id = id,
            Source = SpecialDaySource.Islamic,
            DisplayKey = $"SpecialDay.{id}",
            HijriMonth = month,
            HijriDay = day,
            IsCommonlyObserved = commonlyObserved
        };
}
