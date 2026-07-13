using System.Globalization;
using System.Text;

namespace Samt.Core.Formatting;

/// <summary>
/// App rule: always use Western (Latin) digits 0–9 for times and numbers,
/// even when the UI language is Arabic (RTL).
/// </summary>
/// <remarks>
/// WinUI/XAML may still substitute Arabic-Indic glyphs (٠–٩) when a control's
/// <c>Language</c> is Arabic. Numeric controls must set Language to
/// <see cref="XamlLanguageTag"/> in addition to using these formatters.
/// </remarks>
public static class LatinDigits
{
    /// <summary>BCP-47 tag for XAML controls that display or edit numbers/times.</summary>
    public const string XamlLanguageTag = "en-US";

    /// <summary>Culture used for numeric/time formatting (Latin digits only).</summary>
    public static CultureInfo FormatCulture { get; } = CreateFormatCulture();

    /// <summary>
    /// Arabic UI culture whose native digits are forced to Latin 0–9
    /// (Maghreb-style preference; used for process CurrentCulture).
    /// </summary>
    public static CultureInfo ArabicUiWithLatinDigits { get; } = CreateArabicWithLatinDigits();

    public static string Time(DateTimeOffset value, string format = "HH:mm")
        => EnsureLatin(value.ToString(format, FormatCulture));

    public static string Time(TimeOnly value, string format = "HH:mm")
        => EnsureLatin(value.ToString(format, FormatCulture));

    public static string Time(DateTime value, string format = "HH:mm")
        => EnsureLatin(value.ToString(format, FormatCulture));

    public static string Number(double value, string format = "0.####")
        => EnsureLatin(value.ToString(format, FormatCulture));

    public static string Number(int value)
        => EnsureLatin(value.ToString(FormatCulture));

    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        string text;
        if (value.TotalHours >= 1)
        {
            text = string.Format(
                FormatCulture,
                "{0:00}:{1:00}:{2:00}",
                (int)value.TotalHours,
                value.Minutes,
                value.Seconds);
        }
        else
        {
            text = string.Format(
                FormatCulture,
                "{0:00}:{1:00}",
                value.Minutes,
                value.Seconds);
        }

        return EnsureLatin(text);
    }

    public static string Date(DateOnly value, string format = "yyyy-MM-dd")
        => EnsureLatin(value.ToString(format, FormatCulture));

    public static string Date(DateTimeOffset value, string format = "yyyy-MM-dd")
        => EnsureLatin(value.ToString(format, FormatCulture));

    /// <summary>Hijri components as <c>d monthName yyyy</c> with Latin digits (month name already localized).</summary>
    public static string Hijri(int day, string monthName, int year)
        => EnsureLatin(string.Format(FormatCulture, "{0} {1} {2}", day, monthName, year));

    /// <summary>
    /// Replace Arabic-Indic (٠–٩) and Eastern Arabic-Indic (۰–۹) digits with Latin 0–9.
    /// Safe to call on any UI string that may contain numbers.
    /// </summary>
    public static string EnsureLatin(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var needsRewrite = false;
        foreach (var ch in text)
        {
            if (IsIndicDigit(ch))
            {
                needsRewrite = true;
                break;
            }
        }

        if (!needsRewrite)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                _ => ch
            });
        }

        return sb.ToString();
    }

    public static bool ContainsIndicDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (IsIndicDigit(ch))
            {
                return true;
            }
        }

        return false;
    }

    public static void ApplyProcessDefaults(string uiLanguage)
    {
        var isArabic = uiLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        // Even for Arabic UI language, keep process culture on Latin-digit Arabic
        // so any accidental ToString() without an explicit culture stays Latin.
        var culture = isArabic ? ArabicUiWithLatinDigits : CultureInfo.GetCultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static bool IsIndicDigit(char ch)
        => ch is (>= '\u0660' and <= '\u0669') or (>= '\u06F0' and <= '\u06F9');

    private static CultureInfo CreateFormatCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NativeDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        culture.NumberFormat.DigitSubstitution = DigitShapes.None;
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        culture.DateTimeFormat.ShortTimePattern = "HH:mm";
        culture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        return CultureInfo.ReadOnly(culture);
    }

    private static CultureInfo CreateArabicWithLatinDigits()
    {
        // Start from ar-DZ (Maghreb) which prefers Latin digits in CLDR, then force explicitly.
        CultureInfo culture;
        try
        {
            culture = (CultureInfo)CultureInfo.GetCultureInfo("ar-DZ").Clone();
        }
        catch (CultureNotFoundException)
        {
            culture = (CultureInfo)CultureInfo.GetCultureInfo("ar").Clone();
        }

        culture.NumberFormat.NativeDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        culture.NumberFormat.DigitSubstitution = DigitShapes.None;
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        culture.DateTimeFormat.ShortTimePattern = "HH:mm";
        culture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        return CultureInfo.ReadOnly(culture);
    }
}
