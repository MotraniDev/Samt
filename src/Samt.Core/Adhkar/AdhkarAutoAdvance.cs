namespace Samt.Core.Adhkar;

/// <summary>Pure navigation helper for the Adhkar reader auto-advance setting.</summary>
public static class AdhkarAutoAdvance
{
    /// <summary>
    /// Returns true when the reader should move from <paramref name="index"/> to the next item.
    /// </summary>
    public static bool ShouldAdvance(bool enabled, bool itemComplete, int index, int itemCount)
        => enabled
           && itemComplete
           && itemCount > 0
           && index >= 0
           && index < itemCount - 1;
}
