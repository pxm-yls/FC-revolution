using System;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowTimelineUiTickCadenceDecision(
    bool ShouldSyncManifest);

internal readonly record struct GameWindowTimelinePresentedFrameCadenceDecision(
    int NextBranchGalleryRefreshTick,
    bool ShouldSyncManifest,
    bool ShouldRefreshGallery);

internal static class GameWindowTimelineRefreshCadenceController
{
    private const int DefaultGalleryRefreshInterval = 12;

    public static GameWindowTimelineUiTickCadenceDecision BuildUiTickDecision(bool framePresented) =>
        new(ShouldSyncManifest: !framePresented);

    public static GameWindowTimelinePresentedFrameCadenceDecision BuildPresentedFrameDecision(
        int currentBranchGalleryRefreshTick,
        int refreshInterval = DefaultGalleryRefreshInterval)
    {
        var normalizedInterval = Math.Max(1, refreshInterval);
        var nextTick = unchecked(currentBranchGalleryRefreshTick + 1);
        var shouldRefresh = nextTick % normalizedInterval == 0;
        return new GameWindowTimelinePresentedFrameCadenceDecision(
            NextBranchGalleryRefreshTick: nextTick,
            ShouldSyncManifest: shouldRefresh,
            ShouldRefreshGallery: shouldRefresh);
    }
}
