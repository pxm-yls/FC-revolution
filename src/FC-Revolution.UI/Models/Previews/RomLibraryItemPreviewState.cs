using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace FC_Revolution.UI.Models;

internal sealed class RomLibraryItemPreviewState
{
    private StreamingPreview? _preview;
    private IReadOnlyList<WriteableBitmap>? _smoothFrames;
    private int _smoothFrameIndex;
    private WriteableBitmap? _currentBitmap;

    public WriteableBitmap? CurrentBitmap => _currentBitmap;

    public bool? KnownPreviewIsAnimated => _preview?.IsAnimated;

    public int KnownPreviewIntervalMs => _preview?.IntervalMs ?? 0;

    public int KnownPreviewFrameCount => _preview?.FrameCount ?? 0;

    public int FrameCount => _preview?.FrameCount ?? (_currentBitmap != null ? 1 : 0);

    public bool HasLoadedPreview => _preview != null;

    public bool IsAnimated => _preview?.IsAnimated == true;

    public bool IsLegacyPreview => _preview?.IsLegacyPreview == true;

    public bool IsMemoryBacked => _preview?.IsMemoryBacked == true;

    public bool IsSmoothPlaybackEnabled => _smoothFrames is { Count: > 1 };

    public bool HasStreamHandle => _preview?.HasStreamHandle == true;

    public bool HasPrefetchedFrame => _preview?.HasPrefetchedFrame == true;

    public int CachedBitmapCount => _preview?.CachedBitmapCount ?? 0;

    public int CachedFrameCount => _preview?.CachedFrameCount ?? 0;

    public long EstimatedBitmapCacheBytes => _preview?.EstimatedBitmapCacheBytes ?? 0;

    public long EstimatedMemoryFrameBytes => _preview?.EstimatedMemoryFrameBytes ?? 0;

    public int IntervalMs => _preview?.IntervalMs ?? 0;

    public bool SupportsFullFrameCaching => _preview?.SupportsFullFrameCaching == true;

    public string DebugInfo => _preview?.DebugInfo ?? "preview=unloaded";

    public void SetPreviewStream(StreamingPreview preview)
    {
        _preview?.Dispose();
        _preview = preview;
        _smoothFrames = null;
        _smoothFrameIndex = 0;
        _currentBitmap = preview.Bitmap;
    }

    public void ClearPreviewFrames()
    {
        _preview?.Dispose();
        _preview = null;
        _smoothFrames = null;
        _smoothFrameIndex = 0;
        _currentBitmap = null;
    }

    public bool AdvancePreviewFrame()
    {
        if (_preview == null)
            return false;

        if (_smoothFrames is { Count: > 1 })
        {
            _smoothFrameIndex = (_smoothFrameIndex + 1) % _smoothFrames.Count;
            _currentBitmap = _smoothFrames[_smoothFrameIndex];
            return true;
        }

        _currentBitmap = _preview.AdvanceFrame();
        return true;
    }

    public bool SyncPreviewFrame(long elapsedMilliseconds)
    {
        if (_preview == null || !_preview.IsAnimated || _preview.IntervalMs <= 0 || _preview.FrameCount <= 0)
            return false;

        var frameIndex = (int)((elapsedMilliseconds / _preview.IntervalMs) % _preview.FrameCount);
        if (_smoothFrames is { Count: > 1 })
        {
            if (frameIndex >= _smoothFrames.Count)
                frameIndex %= _smoothFrames.Count;

            _smoothFrameIndex = frameIndex;
            _currentBitmap = _smoothFrames[_smoothFrameIndex];
            return true;
        }

        _currentBitmap = _preview.GetFrame(frameIndex);
        return true;
    }

    public bool EnableMemoryPlayback()
    {
        if (_preview == null || !_preview.SupportsFullFrameCaching)
            return false;

        _preview.EnableMemoryPlayback();
        return true;
    }

    public bool DisableMemoryPlayback()
    {
        if (_preview == null)
            return false;

        _preview.DisableMemoryPlayback();
        return true;
    }

    public bool EnableSmoothPlayback()
    {
        if (_preview == null || !_preview.SupportsFullFrameCaching || _smoothFrames is { Count: > 1 })
            return false;

        _smoothFrames = _preview.LoadAllBitmaps();
        _smoothFrameIndex = 0;
        if (_smoothFrames.Count > 0)
            _currentBitmap = _smoothFrames[0];

        return true;
    }

    public bool DisableSmoothPlayback()
    {
        if (_smoothFrames == null)
            return false;

        _smoothFrames = null;
        _smoothFrameIndex = 0;
        _preview?.ReleaseBitmapCache();
        if (_preview != null)
            _currentBitmap = _preview.Bitmap;

        return true;
    }
}
