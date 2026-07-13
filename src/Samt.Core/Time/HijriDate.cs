namespace Samt.Core.Time;

/// <summary>Civil Hijri (tabular) date components — display only, not a religious decree.</summary>
public readonly record struct HijriDate(int Year, int Month, int Day)
{
    /// <summary>Islamic month 9 (Ramadan).</summary>
    public bool IsRamadan => Month == 9;

    public void Deconstruct(out int year, out int month, out int day)
    {
        year = Year;
        month = Month;
        day = Day;
    }
}
