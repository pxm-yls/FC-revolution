using System;
using System.Diagnostics;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    public MemoryDiagnosticsSnapshot CaptureMemoryDiagnosticsSnapshot()
    {
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var gcInfo = GC.GetGCMemoryInfo();
        var previewItems = _romLibrary
            .Select(rom => new PreviewMemoryUsageItem
            {
                RomName = rom.DisplayName,
                RomPath = rom.Path,
                IsCurrent = ReferenceEquals(rom, CurrentRom),
                HasPreviewFile = rom.HasPreview,
                HasLoadedPreview = rom.HasLoadedPreview,
                IsAnimated = rom.IsPreviewAnimated,
                IsSmoothPlaybackEnabled = rom.IsSmoothPlaybackEnabled,
                IsMemoryBacked = rom.IsMemoryPreview,
                HasStreamHandle = rom.HasPreviewStreamHandle,
                HasPrefetchedFrame = rom.HasPrefetchedPreviewFrame,
                PreviewFrameCount = rom.PreviewFrameCount,
                CachedBitmapCount = rom.CachedPreviewBitmapCount,
                CachedFrameCount = rom.CachedPreviewFrameCount,
                EstimatedBitmapCacheBytes = rom.EstimatedPreviewBitmapCacheBytes,
                EstimatedFrameCacheBytes = rom.EstimatedPreviewFrameCacheBytes
            })
            .OrderByDescending(item => item.IsCurrent)
            .ThenByDescending(item => item.EstimatedTotalBytes)
            .ThenBy(item => item.RomName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new MemoryDiagnosticsSnapshot
        {
            TimestampLocal = DateTime.Now,
            LayoutName = CurrentLayoutName,
            CurrentRomName = CurrentRom?.DisplayName,
            WorkingSetBytes = process.WorkingSet64,
            PrivateBytes = process.PrivateMemorySize64,
            VirtualBytes = process.VirtualMemorySize64,
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            TotalCommittedBytes = gcInfo.TotalCommittedBytes,
            HeapSizeBytes = gcInfo.HeapSizeBytes,
            FragmentedBytes = gcInfo.FragmentedBytes,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalRomCount = _romLibrary.Count,
            VisiblePreviewTargetCount = GetPreviewAnimationTargets().Count,
            LoadedPreviewCount = previewItems.Count(item => item.HasLoadedPreview),
            AnimatedPreviewCount = previewItems.Count(item => item.IsAnimated),
            SmoothPlaybackCount = previewItems.Count(item => item.IsSmoothPlaybackEnabled),
            MemoryBackedPreviewCount = previewItems.Count(item => item.IsMemoryBacked),
            StreamHandleCount = previewItems.Count(item => item.HasStreamHandle),
            PrefetchedFrameCount = previewItems.Count(item => item.HasPrefetchedFrame),
            TotalCachedBitmapCount = previewItems.Sum(item => item.CachedBitmapCount),
            TotalCachedFrameCount = previewItems.Sum(item => item.CachedFrameCount),
            EstimatedBitmapCacheBytes = previewItems.Sum(item => item.EstimatedBitmapCacheBytes),
            EstimatedFrameCacheBytes = previewItems.Sum(item => item.EstimatedFrameCacheBytes),
            PreviewItems = previewItems
        };
    }
}
