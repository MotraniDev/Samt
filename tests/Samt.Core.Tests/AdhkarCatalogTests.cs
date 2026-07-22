using Samt.Core.Adhkar;

namespace Samt.Core.Tests;

public class AdhkarCatalogTests
{
    [Fact]
    public void Catalog_HasLibraryGroupsAndMorningContent()
    {
        Assert.True(AdhkarCatalog.All.Count >= 15);
        var morning = AdhkarCatalog.Get(AdhkarCollectionKind.Morning);
        Assert.True(morning.Items.Count >= 20);
        Assert.True(morning.IsSchedulable);
        Assert.Contains(morning.Items, i => i.ArabicText.Contains("الْحَيُّ", StringComparison.Ordinal)
                                            || i.ArabicText.Contains("الحي", StringComparison.Ordinal)
                                            || i.Reference?.Contains("كرسي", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ReadingSession_TracksCountsAndProgress()
    {
        var session = new AdhkarReadingSession();
        var collection = AdhkarCatalog.Get(AdhkarCollectionKind.Tasbeeh);
        session.Bind(collection);

        var first = collection.Items[0];
        Assert.Equal(0, session.GetCount(first.Id));
        session.Increment(first);
        Assert.Equal(1, session.GetCount(first.Id));
        session.MarkComplete(first);
        Assert.True(session.IsItemComplete(first));
        Assert.True(session.CompletedItemCount >= 1);
        Assert.InRange(session.ItemProgress, 0.01, 1.0);
    }

    [Fact]
    public void QuranicSurahs_AreFlagged_AyatAndHadith_AreNot()
    {
        var morning = AdhkarCatalog.Get(AdhkarCollectionKind.Morning);
        Assert.Contains(morning.Items, i => i.Id == "m02" && i.IsQuranicSurah);
        Assert.Contains(morning.Items, i => i.Id == "m03" && i.IsQuranicSurah);
        Assert.Contains(morning.Items, i => i.Id == "m04" && i.IsQuranicSurah);
        Assert.Contains(morning.Items, i => i.Id == "m01" && i.IsAyatAlKursi && !i.IsQuranicSurah);
        Assert.Contains(morning.Items, i => i.Id == "m05" && !i.IsQuranicSurah && !i.IsAyatAlKursi);

        var hadithBasmala = morning.Items.First(i =>
            i.ArabicText.StartsWith("بِسْمِ اللَّهِ الَّذِي", StringComparison.Ordinal));
        Assert.False(hadithBasmala.IsQuranicSurah);
        Assert.False(hadithBasmala.IsAyatAlKursi);
    }

    [Fact]
    public void BodyArabic_StripsVocalizedBasmala_OnlyForSurahs()
    {
        var ikhlas = AdhkarCatalog.Get(AdhkarCollectionKind.Morning).Items.First(i => i.Id == "m02");
        var body = AdhkarBasmala.BodyArabic(ikhlas);
        Assert.StartsWith("قُلْ هُوَ اللَّهُ", body, StringComparison.Ordinal);
        Assert.DoesNotContain(AdhkarBasmala.VocalizedPrefix, body, StringComparison.Ordinal);
    }

    [Fact]
    public void AyatAlKursi_ShowsIstiadhahThenBasmala_BodyIsAyahOnly()
    {
        var kursi = AdhkarCatalog.Get(AdhkarCollectionKind.Morning).Items.First(i => i.Id == "m01");
        Assert.True(AdhkarBasmala.ShowsIstiadhah(kursi));
        Assert.True(AdhkarBasmala.ShowsBasmala(kursi));

        var body = AdhkarBasmala.BodyArabic(kursi);
        Assert.StartsWith("اللَّهُ لَا إِلَٰهَ", body, StringComparison.Ordinal);
        Assert.DoesNotContain(AdhkarBasmala.IstiadhahVocalizedPrefix, body, StringComparison.Ordinal);

        var evening = AdhkarCatalog.Get(AdhkarCollectionKind.Evening).Items.First(i => i.Id == "e01");
        Assert.True(evening.IsAyatAlKursi);
        Assert.Equal(evening.ArabicText, AdhkarBasmala.BodyArabic(evening));
    }
}
