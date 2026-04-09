using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Tests;

public sealed class VideoPreviewWindowPolicyTests
{
    [Fact]
    public void GetFrameWindowCount_UsesConfiguredPreloadSeconds()
    {
        var originalSeconds = StreamingPreviewSettings.VideoPreloadWindowSeconds;
        var originalBytes = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        try
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = 2;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 64L * 1024 * 1024;

            Assert.Equal(20, VideoPreviewWindowPolicy.GetFrameWindowCount(intervalMs: 100, frameBytes: 1024));
            Assert.Equal(2, VideoPreviewWindowPolicy.GetFrameWindowCount(intervalMs: 1000, frameBytes: 1024));
            Assert.Equal(1, VideoPreviewWindowPolicy.GetFrameWindowCount(intervalMs: 5000, frameBytes: 1024));
        }
        finally
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = originalSeconds;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = originalBytes;
        }
    }

    [Fact]
    public void GetFrameWindowCount_IsCappedByWindowByteBudget()
    {
        var originalSeconds = StreamingPreviewSettings.VideoPreloadWindowSeconds;
        var originalBytes = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        try
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = 3;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 1024;

            Assert.Equal(1, VideoPreviewWindowPolicy.GetFrameWindowCount(intervalMs: 100, frameBytes: 2 * 1024 * 1024));
        }
        finally
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = originalSeconds;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = originalBytes;
        }
    }

    [Fact]
    public void GetPrefetchFrameWindowCount_UsesRemainingBudgetAfterActiveWindow()
    {
        var originalSeconds = StreamingPreviewSettings.VideoPreloadWindowSeconds;
        var originalBytes = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        try
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = 3;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 3L * 1024 * 1024;

            Assert.Equal(
                1,
                VideoPreviewWindowPolicy.GetPrefetchFrameWindowCount(
                    intervalMs: 100,
                    frameBytes: 1024 * 1024,
                    activeWindowFrameCount: 2));
            Assert.Equal(
                0,
                VideoPreviewWindowPolicy.GetPrefetchFrameWindowCount(
                    intervalMs: 100,
                    frameBytes: 1024 * 1024,
                    activeWindowFrameCount: 3));
        }
        finally
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = originalSeconds;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = originalBytes;
        }
    }

    [Fact]
    public void EvictionPolicy_BuildPreloadWindowDecision_RespectsBudgetAndSchedulingGuards()
    {
        var originalSeconds = StreamingPreviewSettings.VideoPreloadWindowSeconds;
        var originalBytes = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        try
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = 2;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 3L * 1024 * 1024;
            var policy = new VideoPreviewWindowEvictionPolicy();
            var active = new VideoPreviewWindowRange(StartIndex: 0, EndIndex: 1, Count: 2);
            var prefetched = new VideoPreviewWindowRange(StartIndex: 2, EndIndex: 2, Count: 1);

            var blockedByPrefetched = policy.BuildPreloadWindowDecision(
                totalFrameCount: 20,
                isDisposed: false,
                prefetchFrameCount: policy.ResolvePrefetchFrameWindowCount(100, 1024 * 1024, active.Count),
                activeWindow: active,
                prefetchedWindow: prefetched,
                preloadRunning: false);
            Assert.False(blockedByPrefetched.ShouldSchedule);
            Assert.Equal(2, blockedByPrefetched.StartIndex);

            var allowed = policy.BuildPreloadWindowDecision(
                totalFrameCount: 20,
                isDisposed: false,
                prefetchFrameCount: policy.ResolvePrefetchFrameWindowCount(100, 1024 * 1024, active.Count),
                activeWindow: active,
                prefetchedWindow: null,
                preloadRunning: false);
            Assert.True(allowed.ShouldSchedule);
            Assert.Equal(2, allowed.StartIndex);
            Assert.Equal(1, allowed.FrameCount);
        }
        finally
        {
            StreamingPreviewSettings.VideoPreloadWindowSeconds = originalSeconds;
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = originalBytes;
        }
    }

    [Fact]
    public void EvictionPolicy_BuildFrameLookupDecision_PromotesPrefetchedHits()
    {
        var policy = new VideoPreviewWindowEvictionPolicy();
        var active = new VideoPreviewWindowRange(StartIndex: 0, EndIndex: 9, Count: 10);
        var prefetched = new VideoPreviewWindowRange(StartIndex: 10, EndIndex: 19, Count: 10);

        var activeHit = policy.BuildFrameLookupDecision(5, active, prefetched);
        Assert.Equal(VideoPreviewFrameWindowSource.Active, activeHit.Source);
        Assert.False(activeHit.PromotePrefetchedToActive);

        var prefetchedHit = policy.BuildFrameLookupDecision(12, active, prefetched);
        Assert.Equal(VideoPreviewFrameWindowSource.Prefetched, prefetchedHit.Source);
        Assert.True(prefetchedHit.PromotePrefetchedToActive);

        var miss = policy.BuildFrameLookupDecision(40, active, prefetched);
        Assert.Equal(VideoPreviewFrameWindowSource.Miss, miss.Source);
    }

    [Fact]
    public void EvictionPolicy_AssignmentAndReplacementDecisions_EvictExistingWindows()
    {
        var policy = new VideoPreviewWindowEvictionPolicy();

        var activeAssignment = policy.BuildActiveWindowAssignmentDecision();
        Assert.True(activeAssignment.EvictCurrentActiveWindow);
        Assert.True(activeAssignment.EvictPrefetchedWindow);

        var prefetchedReplacement = policy.BuildPrefetchedWindowReplacementDecision();
        Assert.True(prefetchedReplacement.EvictExistingPrefetchedWindow);
    }
}
