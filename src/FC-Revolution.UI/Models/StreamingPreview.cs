using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Models;

public sealed class StreamingPreview : IDisposable
{
    private readonly IPreviewSource _source;
    private bool _disposed;

    private StreamingPreview(IPreviewSource source)
    {
        _source = source;
    }

    public int Width => _source.Width;
    public int Height => _source.Height;
    public int IntervalMs => _source.IntervalMs;
    public int FrameCount => _source.FrameCount;
    public bool IsAnimated => _source.IsAnimated;
    public bool IsLegacyPreview => _source.IsLegacyPreview;
    public bool IsMemoryBacked => _source.IsMemoryBacked;
    public bool HasStreamHandle => _source.HasStreamHandle;
    public bool HasPrefetchedFrame => _source.HasPrefetchedFrame;
    public bool SupportsFullFrameCaching => _source.SupportsFullFrameCaching;
    public string DebugInfo => _source.DebugInfo;
    public int CachedBitmapCount => _source.CachedBitmapCount;
    public int CachedFrameCount => _source.CachedFrameCount;
    public long EstimatedBitmapCacheBytes => _source.EstimatedBitmapCacheBytes;
    public long EstimatedMemoryFrameBytes => _source.EstimatedMemoryFrameBytes;
    public WriteableBitmap Bitmap => _source.Bitmap;
    public static int VideoPreloadWindowSeconds
    {
        get => StreamingPreviewSettings.VideoPreloadWindowSeconds;
        set => StreamingPreviewSettings.VideoPreloadWindowSeconds = value;
    }

    public static StreamingPreview Open(string previewPath, string previewMagicV1, string previewMagicV2)
    {
        return new StreamingPreview(
            StreamingPreviewSourceFactory.OpenSource(previewPath, previewMagicV1, previewMagicV2));
    }

    public WriteableBitmap GetFrame(int index)
    {
        ThrowIfDisposed();
        return _source.GetFrame(index);
    }

    public WriteableBitmap AdvanceFrame()
    {
        ThrowIfDisposed();
        return _source.AdvanceFrame();
    }

    public IReadOnlyList<WriteableBitmap> LoadAllBitmaps()
    {
        ThrowIfDisposed();
        return _source.LoadAllBitmaps();
    }

    public void EnableMemoryPlayback()
    {
        ThrowIfDisposed();
        _source.EnableMemoryPlayback();
    }

    public void DisableMemoryPlayback()
    {
        ThrowIfDisposed();
        _source.DisableMemoryPlayback();
    }

    public void ReleaseBitmapCache()
    {
        ThrowIfDisposed();
        _source.ReleaseBitmapCache();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _source.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StreamingPreview));
    }

}
