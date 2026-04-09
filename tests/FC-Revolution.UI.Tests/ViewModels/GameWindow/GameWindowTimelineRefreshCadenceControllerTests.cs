using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowTimelineRefreshCadenceControllerTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BuildUiTickDecision_SyncsManifestOnlyWhenNoFramePresented(
        bool framePresented,
        bool expectedShouldSyncManifest)
    {
        var decision = GameWindowTimelineRefreshCadenceController.BuildUiTickDecision(framePresented);

        Assert.Equal(expectedShouldSyncManifest, decision.ShouldSyncManifest);
    }

    [Fact]
    public void BuildPresentedFrameDecision_IncrementsTickAndSchedulesSyncAndRefreshAtCadenceBoundary()
    {
        var decision = GameWindowTimelineRefreshCadenceController.BuildPresentedFrameDecision(currentBranchGalleryRefreshTick: 11);

        Assert.Equal(12, decision.NextBranchGalleryRefreshTick);
        Assert.True(decision.ShouldSyncManifest);
        Assert.True(decision.ShouldRefreshGallery);
    }

    [Fact]
    public void BuildPresentedFrameDecision_IncrementsTickWithoutSchedulingBeforeCadenceBoundary()
    {
        var decision = GameWindowTimelineRefreshCadenceController.BuildPresentedFrameDecision(currentBranchGalleryRefreshTick: 10);

        Assert.Equal(11, decision.NextBranchGalleryRefreshTick);
        Assert.False(decision.ShouldSyncManifest);
        Assert.False(decision.ShouldRefreshGallery);
    }

    [Fact]
    public void BuildPresentedFrameDecision_ClampsRefreshIntervalToAvoidDivisionByZero()
    {
        var decision = GameWindowTimelineRefreshCadenceController.BuildPresentedFrameDecision(
            currentBranchGalleryRefreshTick: 3,
            refreshInterval: 0);

        Assert.Equal(4, decision.NextBranchGalleryRefreshTick);
        Assert.True(decision.ShouldSyncManifest);
        Assert.True(decision.ShouldRefreshGallery);
    }
}
