using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Tests;

public sealed class PreviewCachingPolicyTests
{
    [Fact]
    public void SupportsFullFrameCaching_UsesConfiguredByteCap()
    {
        var original = PreviewCachingPolicy.MaxFullFrameCacheBytes;
        try
        {
            PreviewCachingPolicy.MaxFullFrameCacheBytes = 1024;

            Assert.False(PreviewCachingPolicy.SupportsFullFrameCaching(width: 512, height: 512, frameCount: 2));
            Assert.True(PreviewCachingPolicy.SupportsFullFrameCaching(width: 16, height: 16, frameCount: 2));
        }
        finally
        {
            PreviewCachingPolicy.MaxFullFrameCacheBytes = original;
        }
    }
}
