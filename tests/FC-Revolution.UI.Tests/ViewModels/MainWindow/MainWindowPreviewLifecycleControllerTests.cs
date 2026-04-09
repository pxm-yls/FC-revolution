using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewLifecycleControllerTests
{
    [Fact]
    public void BuildLoadPreviewDecision_MissingPreviewFile_StopsPlaybackAndShowsHint()
    {
        var controller = new MainWindowPreviewLifecycleController();

        var decision = controller.BuildLoadPreviewDecision(
            hasCurrentRom: true,
            previewFileExists: false,
            isGeneratingPreview: false,
            isCurrentRomLegacyPreview: false);

        Assert.True(decision.ShouldClearCurrentPreviewBitmap);
        Assert.True(decision.ShouldStopPlayback);
        Assert.False(decision.ShouldAttemptLoadCurrentRomPreview);
        Assert.Equal("当前没有预览，可在设置中一键生成", decision.StatusText);
    }

    [Fact]
    public void BuildLoadPreviewDecision_LegacyPreview_UsesLegacyStatusText()
    {
        var controller = new MainWindowPreviewLifecycleController();

        var decision = controller.BuildLoadPreviewDecision(
            hasCurrentRom: true,
            previewFileExists: true,
            isGeneratingPreview: false,
            isCurrentRomLegacyPreview: true);

        Assert.False(decision.ShouldClearCurrentPreviewBitmap);
        Assert.True(decision.ShouldAttemptLoadCurrentRomPreview);
        Assert.Equal("已载入旧版预览封面，重新生成后可启用低内存动画", decision.StatusText);
    }

    [Fact]
    public void PlaybackStartDecision_IsEvaluatedAfterLoadStateChanges()
    {
        var controller = new MainWindowPreviewLifecycleController();

        var preLoadDecision = controller.BuildLoadPreviewDecision(
            hasCurrentRom: true,
            previewFileExists: true,
            isGeneratingPreview: false,
            isCurrentRomLegacyPreview: false);
        var shouldStartBeforeLoad = controller.ShouldStartPlaybackAfterLoad(hasAnyAnimatedPreviewAfterLoad: false);
        var shouldStartAfterLoad = controller.ShouldStartPlaybackAfterLoad(hasAnyAnimatedPreviewAfterLoad: true);

        Assert.True(preLoadDecision.ShouldAttemptLoadCurrentRomPreview);
        Assert.False(shouldStartBeforeLoad);
        Assert.True(shouldStartAfterLoad);
    }

    [Fact]
    public void BuildStartStopRestartDecisions_ReturnExpectedTimerTransitions()
    {
        var controller = new MainWindowPreviewLifecycleController();

        var stop = controller.BuildStopPlaybackDecision(hasAnyAnimatedPreview: false);
        Assert.True(stop.ShouldStopTimer);
        Assert.True(stop.ShouldResetPlaybackCounter);
        Assert.True(stop.ShouldClearDebugText);

        var start = controller.BuildStartPlaybackDecision(isTimerEnabled: false);
        Assert.True(start.ShouldStartTimer);
        Assert.True(start.ShouldRestartWatch);
        Assert.True(start.ShouldResetPlaybackCounter);

        var restart = controller.BuildRestartPlaybackDecision(isTimerEnabled: true);
        Assert.False(restart.ShouldStartTimer);
        Assert.True(restart.ShouldRestartWatch);
        Assert.True(restart.ShouldResetPlaybackCounter);
    }

    [Fact]
    public void BuildLoadedPreviewPlaybackDecision_StaticPreviewDoesNotSyncAnimatedFrame()
    {
        var controller = new MainWindowPreviewLifecycleController();

        var staticDecision = controller.BuildLoadedPreviewPlaybackDecision(
            isPreviewAnimated: false,
            isCurrentRomItem: true,
            shouldRestartPlayback: true);

        Assert.False(staticDecision.ShouldSyncPreviewFrame);
        Assert.True(staticDecision.ShouldUpdateCurrentPreviewBitmap);
        Assert.False(staticDecision.ShouldRestartPlayback);

        var animatedDecision = controller.BuildLoadedPreviewPlaybackDecision(
            isPreviewAnimated: true,
            isCurrentRomItem: false,
            shouldRestartPlayback: true);

        Assert.True(animatedDecision.ShouldSyncPreviewFrame);
        Assert.False(animatedDecision.ShouldUpdateCurrentPreviewBitmap);
        Assert.True(animatedDecision.ShouldRestartPlayback);
    }
}
