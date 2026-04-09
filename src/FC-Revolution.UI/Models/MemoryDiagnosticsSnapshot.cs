using System;
using System.Collections.Generic;

namespace FC_Revolution.UI.Models;

public sealed class MemoryDiagnosticsSnapshot
{
    public required DateTime TimestampLocal { get; init; }

    public required string LayoutName { get; init; }

    public required string? CurrentRomName { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long PrivateBytes { get; init; }

    public required long VirtualBytes { get; init; }

    public required long ManagedHeapBytes { get; init; }

    public required long TotalCommittedBytes { get; init; }

    public required long HeapSizeBytes { get; init; }

    public required long FragmentedBytes { get; init; }

    public required int Gen0Collections { get; init; }

    public required int Gen1Collections { get; init; }

    public required int Gen2Collections { get; init; }

    public required int TotalRomCount { get; init; }

    public required int VisiblePreviewTargetCount { get; init; }

    public required int LoadedPreviewCount { get; init; }

    public required int AnimatedPreviewCount { get; init; }

    public required int SmoothPlaybackCount { get; init; }

    public required int MemoryBackedPreviewCount { get; init; }

    public required int StreamHandleCount { get; init; }

    public required int PrefetchedFrameCount { get; init; }

    public required int TotalCachedBitmapCount { get; init; }

    public required int TotalCachedFrameCount { get; init; }

    public required long EstimatedBitmapCacheBytes { get; init; }

    public required long EstimatedFrameCacheBytes { get; init; }

    public required IReadOnlyList<PreviewMemoryUsageItem> PreviewItems { get; init; }
}
