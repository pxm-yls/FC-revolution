using FCRevolution.Rendering.Diagnostics;

namespace FC_Revolution.Rendering.Tests;

public sealed class PixelDiffTests
{
    [Fact]
    public void Compare_ReturnsMismatchRatio()
    {
        uint[] reference = [0xFF000000u, 0xFF112233u, 0xFF445566u, 0xFF778899u];
        uint[] candidate = [0xFF000000u, 0xFF112233u, 0xFF445500u, 0xFF778800u];

        float diff = PixelDiff.Compare(reference, candidate);

        Assert.Equal(0.5f, diff);
    }

    [Fact]
    public void Compare_HonorsChannelTolerance()
    {
        uint[] reference = [0xFF112233u];
        uint[] candidate = [0xFF112236u];

        Assert.Equal(1f, PixelDiff.Compare(reference, candidate, channelTolerance: 2));
        Assert.Equal(0f, PixelDiff.Compare(reference, candidate, channelTolerance: 3));
    }

    [Fact]
    public void BuildHeatmap_HighlightsDifferentPixelsInRed()
    {
        uint[] reference = [0xFF000000u, 0xFF101010u];
        uint[] candidate = [0xFF000000u, 0xFF401010u];

        uint[] heatmap = PixelDiff.BuildHeatmap(reference, candidate);

        Assert.Equal(0xFF000000u, heatmap[0]);
        Assert.Equal(0xFF300000u, heatmap[1]);
    }
}
