using System;

namespace FC_Revolution.UI.Models.Previews;

internal static class VideoPreviewWindowPolicy
{
    private const long MinWindowCacheBytes = 1L * 1024 * 1024;
    private const long MaxWindowCacheBytesLimit = 128L * 1024 * 1024;
    private static long _maxWindowCacheBytes = 24L * 1024 * 1024;

    public static long MaxWindowCacheBytes
    {
        get => _maxWindowCacheBytes;
        set => _maxWindowCacheBytes = Math.Clamp(value, MinWindowCacheBytes, MaxWindowCacheBytesLimit);
    }

    public static int GetFrameWindowCount(int intervalMs, int frameBytes)
    {
        var normalizedIntervalMs = Math.Max(1, intervalMs);
        var preloadMilliseconds = TimeSpan.FromSeconds(StreamingPreviewSettings.VideoPreloadWindowSeconds).TotalMilliseconds;
        var desiredFrames = Math.Max(1, (int)Math.Ceiling(preloadMilliseconds / normalizedIntervalMs));
        var normalizedFrameBytes = Math.Max(1, frameBytes);
        var budgetFrames = Math.Max(1, (int)(MaxWindowCacheBytes / normalizedFrameBytes));
        return Math.Min(desiredFrames, budgetFrames);
    }

    public static int GetPrefetchFrameWindowCount(int intervalMs, int frameBytes, int activeWindowFrameCount)
    {
        var normalizedFrameBytes = Math.Max(1, frameBytes);
        var budgetFrames = Math.Max(1, (int)(MaxWindowCacheBytes / normalizedFrameBytes));
        var activeFrames = Math.Max(0, activeWindowFrameCount);
        var remainingFrames = Math.Max(0, budgetFrames - activeFrames);
        if (remainingFrames == 0)
            return 0;

        var normalizedIntervalMs = Math.Max(1, intervalMs);
        var preloadMilliseconds = TimeSpan.FromSeconds(StreamingPreviewSettings.VideoPreloadWindowSeconds).TotalMilliseconds;
        var desiredFrames = Math.Max(1, (int)Math.Ceiling(preloadMilliseconds / normalizedIntervalMs));
        return Math.Min(desiredFrames, remainingFrames);
    }
}
