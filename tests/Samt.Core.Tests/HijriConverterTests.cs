using Samt.Core.Time;

namespace Samt.Core.Tests;

public class HijriConverterTests
{
    [Fact]
    public void FromGregorian_ProducesPositiveComponents()
    {
        var hijri = HijriConverter.FromGregorian(new DateOnly(2025, 1, 15));

        Assert.True(hijri.Year >= 1446);
        Assert.InRange(hijri.Month, 1, 12);
        Assert.InRange(hijri.Day, 1, 30);
    }

    [Fact]
    public void DayOffset_PlusOne_AdvancesHijriDayOrRollsMonth()
    {
        var date = new DateOnly(2025, 3, 1);
        var baseH = HijriConverter.FromGregorian(date, dayOffset: 0);
        var shifted = HijriConverter.FromGregorian(date, dayOffset: 1);

        // Either next day in same month, or day 1 of next month/year.
        var advancedSameMonth = shifted.Year == baseH.Year
                                && shifted.Month == baseH.Month
                                && shifted.Day == baseH.Day + 1;
        var rolled = shifted.Day == 1
                     && (shifted.Month == baseH.Month + 1
                         || (baseH.Month == 12 && shifted.Month == 1 && shifted.Year == baseH.Year + 1));

        Assert.True(advancedSameMonth || rolled, $"base={baseH}, shifted={shifted}");
    }

    [Fact]
    public void DayOffset_MinusOne_DiffersFromBase()
    {
        var date = new DateOnly(2025, 6, 15);
        var baseH = HijriConverter.FromGregorian(date, 0);
        var prev = HijriConverter.FromGregorian(date, -1);

        Assert.NotEqual(baseH, prev);
    }

    [Fact]
    public void ClampDayOffset_LimitsRange()
    {
        Assert.Equal(HijriConverter.MinDayOffset, HijriConverter.ClampDayOffset(-99));
        Assert.Equal(HijriConverter.MaxDayOffset, HijriConverter.ClampDayOffset(99));
        Assert.Equal(0, HijriConverter.ClampDayOffset(0));
    }

    [Fact]
    public void IsRamadan_TrueWhenMonthIsNine()
    {
        // Walk a year and require at least one Ramadan hit (tabular calendar).
        var found = false;
        for (var i = 0; i < 400; i++)
        {
            var g = new DateOnly(2024, 1, 1).AddDays(i);
            if (HijriConverter.IsRamadan(g))
            {
                found = true;
                var h = HijriConverter.FromGregorian(g);
                Assert.Equal(9, h.Month);
                break;
            }
        }

        Assert.True(found);
    }
}
