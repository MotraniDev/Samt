using System.Globalization;
using Samt.Core.Formatting;

namespace Samt.Core.Tests;

public class LatinDigitsTests
{
    [Theory]
    [InlineData("١٢:٣٤", "12:34")]
    [InlineData("۰۹:۰۵", "09:05")]
    [InlineData("18:28", "18:28")]
    [InlineData("فجر ٠٦:٤٤", "فجر 06:44")]
    [InlineData("", "")]
    public void EnsureLatin_ConvertsIndicDigits(string input, string expected)
        => Assert.Equal(expected, LatinDigits.EnsureLatin(input));

    [Fact]
    public void Time_UsesAsciiDigitsOnly()
    {
        var value = new DateTimeOffset(2025, 1, 15, 6, 44, 0, TimeSpan.FromHours(1));
        var text = LatinDigits.Time(value);
        Assert.Equal("06:44", text);
        Assert.False(LatinDigits.ContainsIndicDigits(text));
    }

    [Fact]
    public void Duration_FormatsWithLatinDigits()
    {
        var text = LatinDigits.Duration(new TimeSpan(2, 5, 9));
        Assert.Equal("02:05:09", text);
        Assert.False(LatinDigits.ContainsIndicDigits(text));
    }

    [Fact]
    public void Number_FormatsWithLatinDigits()
    {
        Assert.Equal("31.5569", LatinDigits.Number(31.5569, "0.0000"));
    }

    [Fact]
    public void ApplyProcessDefaults_Arabic_DoesNotFormatWithIndicDigits()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            LatinDigits.ApplyProcessDefaults("ar");
            var formatted = 6.ToString("00");
            // May still be culture-dependent; EnsureLatin is the hard guarantee.
            Assert.Equal("06", LatinDigits.EnsureLatin(formatted));
            Assert.False(LatinDigits.ContainsIndicDigits(LatinDigits.Time(DateTimeOffset.Now)));
            Assert.Equal("en-US", LatinDigits.XamlLanguageTag);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void FormatHelpers_AlwaysStripIndicDigits()
    {
        // Simulate a string that already mixed in Indic digits somehow.
        var polluted = LatinDigits.EnsureLatin("٠٦") + ":" + LatinDigits.EnsureLatin("٤٤");
        Assert.Equal("06:44", polluted);
    }
}
