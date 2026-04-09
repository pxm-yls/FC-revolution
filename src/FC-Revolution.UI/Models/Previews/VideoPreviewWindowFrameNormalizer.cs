using System;
using System.Collections.Generic;
using System.Linq;

namespace FC_Revolution.UI.Models.Previews;

internal static class VideoPreviewWindowFrameNormalizer
{
    public static byte[][] Normalize(
        Dictionary<int, byte[]> decodedFrames,
        int startFrameIndex,
        int endFrameIndex)
    {
        if (decodedFrames.Count == 0)
            return [];

        var normalized = new byte[Math.Max(1, endFrameIndex - startFrameIndex + 1)][];
        var orderedKeys = decodedFrames.Keys.OrderBy(index => index).ToArray();
        var firstKey = orderedKeys[0];
        var lastKnownFrame = decodedFrames[firstKey];

        for (var index = startFrameIndex; index <= endFrameIndex; index++)
        {
            if (decodedFrames.TryGetValue(index, out var frame))
                lastKnownFrame = frame;

            normalized[index - startFrameIndex] = lastKnownFrame;
        }

        return normalized;
    }
}
