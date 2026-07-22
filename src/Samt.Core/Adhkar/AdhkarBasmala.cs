namespace Samt.Core.Adhkar;

/// <summary>Quranic chrome (istiʿādhah / basmala) for the Adhkar reader.</summary>
public static class AdhkarBasmala
{
    /// <summary>Unvocalized basmala chrome line.</summary>
    public const string ChromeText = "بسم الله الرحمن الرحيم";

    /// <summary>Unvocalized istiʿādhah chrome (Ayat al-Kursi only).</summary>
    public const string IstiadhahChromeText = "أعوذ بالله من الشيطان الرجيم";

    /// <summary>Vocalized basmala prefix as stored in catalog surah Arabic.</summary>
    public const string VocalizedPrefix = "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ";

    /// <summary>Vocalized istiʿādhah as stored before morning Ayat al-Kursi.</summary>
    public const string IstiadhahVocalizedPrefix = "أَعُوذُ بِاللهِ مِنَ الشَّيْطَانِ الرَّجِيمِ";

    public static bool ShowsBasmala(AdhkarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsQuranicSurah || item.IsAyatAlKursi;
    }

    public static bool ShowsIstiadhah(AdhkarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsAyatAlKursi;
    }

    /// <summary>
    /// Arabic body for the reader: strips chrome prefixes so they are not duplicated.
    /// </summary>
    public static string BodyArabic(AdhkarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var text = item.ArabicText;

        if (item.IsAyatAlKursi)
            return StripLeadingPrefix(text, IstiadhahVocalizedPrefix);

        if (item.IsQuranicSurah)
            return StripLeadingPrefix(text, VocalizedPrefix);

        return text;
    }

    private static string StripLeadingPrefix(string text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return text;

        var rest = text[prefix.Length..].TrimStart();
        if (rest.StartsWith('.'))
            rest = rest[1..].TrimStart();

        return rest.Length > 0 ? rest : text;
    }
}
