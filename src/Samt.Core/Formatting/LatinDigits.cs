using System.Globalization;
using System.Text;

namespace Samt.Core.Formatting;

/// <summary>
/// Always format times and numbers with Western (Latin) digits 0–9,
/// even when the UI language is Arabic.
/// </summary>
public static class LatinDigits
{
    /// <summary>Culture used for numeric/time formatting (Latin digits, 24h-friendly).</summary>
    public static CultureInfo FormatCulture { get; } = CreateFormatCulture();

    /// <summary>
    /// Arabic UI culture whose <see cref="NumberFormatInfo.NativeDigits"/> are Latin 0–9,
    /// so system controls do not render ٠–٩.
    /// </summary>
    public static CultureInfo ArabicUiWithLatinDigits { get; } = CreateArabicWithLatinDigits();

    public static string Time(DateTimeOffset value, string format = "HH:mm")
        => value.ToString(format, FormatCulture);

    public static string Time(TimeOnly value, string format = "HH:mm")
        => value.ToString(format, FormatCulture);

    public static string Time(DateTime value, string format = "HH:mm")
        => value.ToString(format, FormatCulture);

    public static string Number(double value, string format = "0.####")
        => value.ToString(format, FormatCulture);

    public static string Number(int value)
        => value.ToString(FormatCulture);

    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalHours >= 1)
        {
            return string.Format(
                FormatCulture,
                "{0:00}:{1:00}:{2:00}",
                (int)value.TotalHours,
                value.Minutes,
                value.Seconds);
        }

        return string.Format(
            FormatCulture,
            "{0:00}:{1:00}",
            value.Minutes,
            value.Seconds);
    }

    public static string Date(DateOnly value, string format = "yyyy-MM-dd")
        => value.ToString(format, FormatCulture);

    public static string Date(DateTimeOffset value, string format = "yyyy-MM-dd")
        => value.ToString(format, FormatCulture);

    /// <summary>Replace Arabic-Indic and Eastern Arabic digits with Latin digits if any slipped through.</summary>
    public static string EnsureLatin(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                // Arabic-Indic ٠-٩
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                // Eastern Arabic-Indic (Persian) ۰-۹
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                _ => ch
            });
        }

        return sb.ToString();
    }

    public static void ApplyProcessDefaults(string uiLanguage)
    {
        var isArabic = uiLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var culture = isArabic ? ArabicUiWithLatinDigits : CultureInfo.GetCultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static CultureInfo CreateFormatCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NativeDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return CultureInfo.ReadOnly(culture);
    }

    private static CultureInfo CreateArabicWithLatinDigits()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ar").Clone();
        culture.NumberFormat.NativeDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        culture.NumberFormat.DigitSubstitution = DigitShapes.None;
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        // Keep short time as 24h-style for consistency in diagnostics
        culture.DateTimeFormat.ShortTimePattern = "HH:mm";
        culture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
        return CultureInfo.ReadOnly(culture);
    }
}
