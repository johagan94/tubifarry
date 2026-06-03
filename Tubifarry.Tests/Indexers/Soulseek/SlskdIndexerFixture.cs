using System.Runtime.CompilerServices;
using Tubifarry.Indexers.Soulseek;
using Xunit;

namespace Tubifarry.Tests.Indexers.Soulseek;

public class SlskdIndexerFixture
{
    [Fact]
    public void RateLimit_is_three_seconds()
    {
        SlskdIndexer indexer = (SlskdIndexer)RuntimeHelpers.GetUninitializedObject(typeof(SlskdIndexer));

        Assert.Equal(TimeSpan.FromSeconds(3), indexer.RateLimit);
    }
}
