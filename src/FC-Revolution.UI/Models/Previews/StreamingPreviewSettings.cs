using System;

namespace FC_Revolution.UI.Models.Previews;

internal static class StreamingPreviewSettings
{
    private static volatile int _videoPreloadWindowSeconds = 3;

    public static int VideoPreloadWindowSeconds
    {
        get => _videoPreloadWindowSeconds;
        set => _videoPreloadWindowSeconds = Math.Clamp(value, 1, 3);
    }
}
