namespace FC_Revolution.UI.Models;

public sealed class PreviewMemoryUsageItem
{
    public required string RomName { get; init; }

    public required string RomPath { get; init; }

    public bool IsCurrent { get; init; }

    public bool HasPreviewFile { get; init; }

    public bool HasLoadedPreview { get; init; }

    public bool IsAnimated { get; init; }

    public bool IsSmoothPlaybackEnabled { get; init; }

    public bool IsMemoryBacked { get; init; }

    public bool HasStreamHandle { get; init; }

    public bool HasPrefetchedFrame { get; init; }

    public int PreviewFrameCount { get; init; }

    public int CachedBitmapCount { get; init; }

    public int CachedFrameCount { get; init; }

    public long EstimatedBitmapCacheBytes { get; init; }

    public long EstimatedFrameCacheBytes { get; init; }

    public long EstimatedTotalBytes => EstimatedBitmapCacheBytes + EstimatedFrameCacheBytes;

    public string StatusLabel =>
        !HasPreviewFile ? "无预览文件" :
        !HasLoadedPreview ? "未载入" :
        IsSmoothPlaybackEnabled ? "完整帧播放" :
        IsAnimated ? "流式动画" :
        "静态封面";
}
