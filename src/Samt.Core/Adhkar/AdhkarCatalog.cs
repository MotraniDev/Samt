namespace Samt.Core.Adhkar;

/// <summary>
/// Curated offline Adhkar subset (well-known short texts). Not a fatwa source or complete Hisn.
/// </summary>
public static class AdhkarCatalog
{
    private static readonly Lazy<IReadOnlyList<AdhkarCollection>> LazyAll = new(CreateAll);

    public static IReadOnlyList<AdhkarCollection> All => LazyAll.Value;

    public static AdhkarCollection Get(AdhkarCollectionKind kind)
        => All.First(c => c.Kind == kind);

    private static IReadOnlyList<AdhkarCollection> CreateAll()
        =>
        [
            Build(AdhkarCollectionKind.Morning, "Adhkar.Morning", CreateMorning()),
            Build(AdhkarCollectionKind.Evening, "Adhkar.Evening", CreateEvening()),
            Build(AdhkarCollectionKind.AfterPrayer, "Adhkar.AfterPrayer", CreateAfterPrayer()),
            Build(AdhkarCollectionKind.Sleep, "Adhkar.Sleep", CreateSleep())
        ];

    private static AdhkarCollection Build(
        AdhkarCollectionKind kind,
        string titleKey,
        IReadOnlyList<AdhkarItem> items)
        => new()
        {
            Kind = kind,
            TitleKey = titleKey,
            Items = items
        };

    private static IReadOnlyList<AdhkarItem> CreateMorning()
        =>
        [
            Item("m1", "أَصْبَحْنَا وَأَصْبَحَ الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللَّهُ وَحْدَهُ لَا شَرِيكَ لَهُ", "Adhkar.Morning.1.Translation"),
            Item("m2", "بِسْمِ اللَّهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الْأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ الْعَلِيمُ", "Adhkar.Morning.2.Translation", repeat: 3),
            Item("m3", "رَضِيتُ بِاللَّهِ رَبًّا، وَبِالْإِسْلَامِ دِينًا، وَبِمُحَمَّدٍ ﷺ نَبِيًّا", "Adhkar.Morning.3.Translation", repeat: 3),
            Item("m4", "اللَّهُمَّ بِكَ أَصْبَحْنَا، وَبِكَ أَمْسَيْنَا، وَبِكَ نَحْيَا، وَبِكَ نَمُوتُ، وَإِلَيْكَ النُّشُورُ", "Adhkar.Morning.4.Translation"),
            Item("m5", "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ", "Adhkar.Morning.5.Translation", repeat: 100)
        ];

    private static IReadOnlyList<AdhkarItem> CreateEvening()
        =>
        [
            Item("e1", "أَمْسَيْنَا وَأَمْسَى الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللَّهُ وَحْدَهُ لَا شَرِيكَ لَهُ", "Adhkar.Evening.1.Translation"),
            Item("e2", "أَعُوذُ بِكَلِمَاتِ اللَّهِ التَّامَّاتِ مِنْ شَرِّ مَا خَلَقَ", "Adhkar.Evening.2.Translation", repeat: 3),
            Item("e3", "بِسْمِ اللَّهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الْأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ الْعَلِيمُ", "Adhkar.Evening.3.Translation", repeat: 3),
            Item("e4", "اللَّهُمَّ بِكَ أَمْسَيْنَا، وَبِكَ أَصْبَحْنَا، وَبِكَ نَحْيَا، وَبِكَ نَمُوتُ، وَإِلَيْكَ الْمَصِيرُ", "Adhkar.Evening.4.Translation"),
            Item("e5", "أَعُوذُ بِكَلِمَاتِ اللَّهِ التَّامَّاتِ مِنْ غَضَبِهِ وَعِقَابِهِ، وَشَرِّ عِبَادِهِ، وَمِنْ هَمَزَاتِ الشَّيَاطِينِ وَأَنْ يَحْضُرُونِ", "Adhkar.Evening.5.Translation")
        ];

    private static IReadOnlyList<AdhkarItem> CreateAfterPrayer()
        =>
        [
            Item("a1", "أَسْتَغْفِرُ اللَّهَ", "Adhkar.AfterPrayer.1.Translation", repeat: 3),
            Item("a2", "اللَّهُمَّ أَنْتَ السَّلَامُ وَمِنْكَ السَّلَامُ، تَبَارَكْتَ يَا ذَا الْجَلَالِ وَالْإِكْرَامِ", "Adhkar.AfterPrayer.2.Translation"),
            Item("a3", "سُبْحَانَ اللَّهِ", "Adhkar.AfterPrayer.3a.Translation", repeat: 33),
            Item("a4", "الْحَمْدُ لِلَّهِ", "Adhkar.AfterPrayer.3b.Translation", repeat: 33),
            Item("a5", "اللَّهُ أَكْبَرُ", "Adhkar.AfterPrayer.3c.Translation", repeat: 33),
            Item("a6", "لَا إِلَهَ إِلَّا اللَّهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ الْمُلْكُ وَلَهُ الْحَمْدُ، وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ", "Adhkar.AfterPrayer.4.Translation")
        ];

    private static IReadOnlyList<AdhkarItem> CreateSleep()
        =>
        [
            Item("s1", "بِاسْمِكَ اللَّهُمَّ أَمُوتُ وَأَحْيَا", "Adhkar.Sleep.1.Translation"),
            Item("s2", "اللَّهُمَّ قِنِي عَذَابَكَ يَوْمَ تَبْعَثُ عِبَادَكَ", "Adhkar.Sleep.2.Translation"),
            Item("s3", "بِاسْمِكَ رَبِّي وَضَعْتُ جَنْبِي، وَبِكَ أَرْفَعُهُ، إِنْ أَمْسَكْتَ نَفْسِي فَارْحَمْهَا، وَإِنْ أَرْسَلْتَهَا فَاحْفَظْهَا بِمَا تَحْفَظُ بِهِ عِبَادَكَ الصَّالِحِينَ", "Adhkar.Sleep.3.Translation"),
            Item("s4", "آمَنَ الرَّسُولُ بِمَا أُنْزِلَ إِلَيْهِ مِنْ رَبِّهِ وَالْمُؤْمِنُونَ…", "Adhkar.Sleep.4.Translation"),
            Item("s5", "اللَّهُمَّ أَسْلَمْتُ نَفْسِي إِلَيْكَ، وَفَوَّضْتُ أَمْرِي إِلَيْكَ، وَوَجَّهْتُ وَجْهِي إِلَيْكَ، وَأَلْجَأْتُ ظَهْرِي إِلَيْكَ", "Adhkar.Sleep.5.Translation")
        ];

    private static AdhkarItem Item(string id, string arabic, string translationKey, int? repeat = null)
        => new()
        {
            Id = id,
            ArabicText = arabic,
            TranslationKey = translationKey,
            RepeatCount = repeat
        };
}
