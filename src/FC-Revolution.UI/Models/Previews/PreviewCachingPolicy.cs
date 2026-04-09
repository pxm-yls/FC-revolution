using System;

namespace FC_Revolution.UI.Models.Previews;

internal static class PreviewCachingPolicy
{
    private const long MinFullFrameCacheBytes = 1L * 1024 * 1024;
    private const long MaxFullFrameCacheBytesLimit = 256L * 1024 * 1024;
    private static long _maxFullFrameCacheBytes = 24L * 1024 * 1024;

    public static long MaxFullFrameCacheBytes
    {
        get => _maxFullFrameCacheBytes;
        set => _maxFullFrameCacheBytes = Math.Clamp(value, MinFullFrameCacheBytes, MaxFullFrameCacheBytesLimit);
    }

    public static bool SupportsFullFrameCaching(int width, int height, int frameCount)
        => EstimateFullFrameCacheBytes(width, height, frameCount) <= MaxFullFrameCacheBytes;

    public static long EstimateFullFrameCacheBytes(int width, int height, int frameCount)
    {
        if (width <= 0 || height <= 0 || frameCount <= 0)
            return long.MaxValue;

        try
        {
            return checked((long)width * height * 4 * frameCount);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}
