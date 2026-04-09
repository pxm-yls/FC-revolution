namespace FC_Revolution.UI.Models.Previews;

internal enum VideoPreviewFrameWindowSource
{
    Miss,
    Active,
    Prefetched
}

internal readonly record struct VideoPreviewWindowRange(
    int StartIndex,
    int EndIndex,
    int Count)
{
    public bool Contains(int frameIndex) => Count > 0 && frameIndex >= StartIndex && frameIndex <= EndIndex;
}

internal readonly record struct VideoPreviewFrameLookupDecision(
    VideoPreviewFrameWindowSource Source,
    bool PromotePrefetchedToActive);

internal readonly record struct VideoPreviewPreloadWindowDecision(
    bool ShouldSchedule,
    int StartIndex,
    int FrameCount);

internal readonly record struct VideoPreviewActiveWindowAssignmentDecision(
    bool EvictCurrentActiveWindow,
    bool EvictPrefetchedWindow);

internal readonly record struct VideoPreviewPrefetchedWindowReplacementDecision(
    bool EvictExistingPrefetchedWindow);

internal sealed class VideoPreviewWindowEvictionPolicy
{
    public int ResolvePrefetchFrameWindowCount(int intervalMs, int frameBytes, int activeWindowFrameCount) =>
        VideoPreviewWindowPolicy.GetPrefetchFrameWindowCount(intervalMs, frameBytes, activeWindowFrameCount);

    public VideoPreviewPreloadWindowDecision BuildPreloadWindowDecision(
        int totalFrameCount,
        bool isDisposed,
        int prefetchFrameCount,
        VideoPreviewWindowRange? activeWindow,
        VideoPreviewWindowRange? prefetchedWindow,
        bool preloadRunning)
    {
        if (totalFrameCount <= 1 || isDisposed || prefetchFrameCount <= 0)
            return new(false, StartIndex: -1, FrameCount: 0);

        var nextStart = (activeWindow?.EndIndex ?? -1) + 1;
        if (nextStart >= totalFrameCount)
            nextStart = 0;

        if (nextStart == activeWindow?.StartIndex ||
            nextStart == prefetchedWindow?.StartIndex ||
            preloadRunning)
        {
            return new(false, nextStart, prefetchFrameCount);
        }

        return new(true, nextStart, prefetchFrameCount);
    }

    public VideoPreviewFrameLookupDecision BuildFrameLookupDecision(
        int frameIndex,
        VideoPreviewWindowRange? activeWindow,
        VideoPreviewWindowRange? prefetchedWindow)
    {
        if (activeWindow?.Contains(frameIndex) == true)
            return new(VideoPreviewFrameWindowSource.Active, PromotePrefetchedToActive: false);

        if (prefetchedWindow?.Contains(frameIndex) == true)
            return new(VideoPreviewFrameWindowSource.Prefetched, PromotePrefetchedToActive: true);

        return new(VideoPreviewFrameWindowSource.Miss, PromotePrefetchedToActive: false);
    }

    public VideoPreviewActiveWindowAssignmentDecision BuildActiveWindowAssignmentDecision() =>
        new(EvictCurrentActiveWindow: true, EvictPrefetchedWindow: true);

    public VideoPreviewPrefetchedWindowReplacementDecision BuildPrefetchedWindowReplacementDecision() =>
        new(EvictExistingPrefetchedWindow: true);
}
