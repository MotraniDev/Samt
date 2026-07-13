using Samt.Core.Adhkar;

namespace Samt.Core.Tests;

public class AdhkarAutoAdvanceTests
{
    [Theory]
    [InlineData(true, true, 0, 5, true)]
    [InlineData(true, true, 4, 5, false)]
    [InlineData(false, true, 0, 5, false)]
    [InlineData(true, false, 0, 5, false)]
    [InlineData(true, true, 0, 1, false)]
    [InlineData(true, true, -1, 5, false)]
    public void ShouldAdvance_MatchesRules(bool enabled, bool complete, int index, int count, bool expected)
        => Assert.Equal(expected, AdhkarAutoAdvance.ShouldAdvance(enabled, complete, index, count));
}
