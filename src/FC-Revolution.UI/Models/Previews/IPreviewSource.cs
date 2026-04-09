using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace FC_Revolution.UI.Models.Previews;

internal interface IPreviewSource : IDisposable
{
    int Width { get; }
    int Height { get; }
    int IntervalMs { get; }
    int FrameCount { get; }
    bool IsAnimated { get; }
    bool IsLegacyPreview { get; }
    bool IsMemoryBacked { get; }
    bool HasStreamHandle { get; }
    bool HasPrefetchedFrame { get; }
    bool SupportsFullFrameCaching { get; }
    string DebugInfo { get; }
    int CachedBitmapCount { get; }
    int CachedFrameCount { get; }
    long EstimatedBitmapCacheBytes { get; }
    long EstimatedMemoryFrameBytes { get; }
    WriteableBitmap Bitmap { get; }
    WriteableBitmap GetFrame(int index);
    WriteableBitmap AdvanceFrame();
    IReadOnlyList<WriteableBitmap> LoadAllBitmaps();
    void EnableMemoryPlayback();
    void DisableMemoryPlayback();
    void ReleaseBitmapCache();
}
