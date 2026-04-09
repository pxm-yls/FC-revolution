using System.Collections.Generic;
using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Tests;

public sealed class VideoPreviewWindowFrameNormalizerTests
{
    [Fact]
    public void Normalize_WhenDecodedFramesEmpty_ReturnsEmptyArray()
    {
        var normalized = VideoPreviewWindowFrameNormalizer.Normalize(
            decodedFrames: [],
            startFrameIndex: 3,
            endFrameIndex: 8);

        Assert.Empty(normalized);
    }

    [Fact]
    public void Normalize_FillsMissingFramesBetweenDecodedFrames()
    {
        var frameA = new byte[] { 1 };
        var frameC = new byte[] { 3 };
        var decoded = new Dictionary<int, byte[]>
        {
            [10] = frameA,
            [12] = frameC
        };

        var normalized = VideoPreviewWindowFrameNormalizer.Normalize(decoded, 10, 12);

        Assert.Equal(3, normalized.Length);
        Assert.Same(frameA, normalized[0]);
        Assert.Same(frameA, normalized[1]);
        Assert.Same(frameC, normalized[2]);
    }

    [Fact]
    public void Normalize_WhenFirstFrameMissing_UsesFirstAvailableDecodedFrame()
    {
        var frameB = new byte[] { 2 };
        var frameC = new byte[] { 3 };
        var decoded = new Dictionary<int, byte[]>
        {
            [11] = frameB,
            [12] = frameC
        };

        var normalized = VideoPreviewWindowFrameNormalizer.Normalize(decoded, 10, 12);

        Assert.Equal(3, normalized.Length);
        Assert.Same(frameB, normalized[0]);
        Assert.Same(frameB, normalized[1]);
        Assert.Same(frameC, normalized[2]);
    }

    [Fact]
    public void Normalize_WhenTailFramesMissing_RepeatsLastKnownFrame()
    {
        var frameA = new byte[] { 1 };
        var frameB = new byte[] { 2 };
        var decoded = new Dictionary<int, byte[]>
        {
            [10] = frameA,
            [11] = frameB
        };

        var normalized = VideoPreviewWindowFrameNormalizer.Normalize(decoded, 10, 13);

        Assert.Equal(4, normalized.Length);
        Assert.Same(frameA, normalized[0]);
        Assert.Same(frameB, normalized[1]);
        Assert.Same(frameB, normalized[2]);
        Assert.Same(frameB, normalized[3]);
    }
}
