using Samt.Core.Formatting;

namespace Samt.Core.Tests;

public class LatinDigitsTests
{
    [Theory]
    [InlineData("١٢:٣٤", "12:34")]
    [InlineData("۰۹:۰۵", "09:05")]
    [InlineData("18:28", "18:28")]
    [InlineData("فجر ٠٦:٤٤", "فجر 06:44")]
    public void EnsureLatin_ConvertsIndicDigits(string input, string expected)
        => Assert.Equal(expected, LatinDigits.EnsureLatin(input));

    [Fact]
    public void Time_UsesAsciiDigitsOnly()
    {
        var value = new DateTimeOffset(2025, 1, 15, 6, 44, 0, TimeSpan.FromHours(1));
        var text = LatinDigits.Time(value);
        Assert.Equal("06:44", text);
        Assert.DoesNotContain('\u0660', text);
        Assert.DoesNotContain('\u06F0', text);
    }

    [Fact]
    public void Duration_FormatsWithLatinDigits()
    {
        var text = LatinDigits.Duration(new TimeSpan(2, 5, 9));
        Assert.Equal("02:05:09", text);
    }

    [Fact]
    public void Number_FormatsWithLatinDigits()
    {
        Assert.Equal("31.5569", LatinDigits.Number(31.5569, "0.0000"));
    }
}
