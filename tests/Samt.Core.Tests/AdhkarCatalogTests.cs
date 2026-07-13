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
}
