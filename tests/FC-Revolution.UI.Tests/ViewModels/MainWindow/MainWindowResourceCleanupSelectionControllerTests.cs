using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowResourceCleanupSelectionControllerTests
{
    [Fact]
    public void Build_WhenAnyTargetSelected_PreservesFlagsAndReportsSelection()
    {
        var state = MainWindowResourceCleanupSelectionController.Build(
            cleanupPreviewAnimationsSelected: false,
            cleanupThumbnailsSelected: true,
            cleanupTimelineSavesSelected: false,
            cleanupExportVideosSelected: false);

        Assert.False(state.CleanupPreviewAnimationsSelected);
        Assert.True(state.CleanupThumbnailsSelected);
        Assert.False(state.CleanupTimelineSavesSelected);
        Assert.False(state.CleanupExportVideosSelected);
        Assert.True(state.HasSelectedCleanupTargets);
    }

    [Fact]
    public void Build_WhenNoTargetSelected_ReportsNoSelection()
    {
        var state = MainWindowResourceCleanupSelectionController.Build(
            cleanupPreviewAnimationsSelected: false,
            cleanupThumbnailsSelected: false,
            cleanupTimelineSavesSelected: false,
            cleanupExportVideosSelected: false);

        Assert.False(state.HasSelectedCleanupTargets);
        Assert.False(state.ToSelection().HasAnySelection);
    }

    [Fact]
    public void SelectAll_SelectsEveryCleanupTarget()
    {
        var state = MainWindowResourceCleanupSelectionController.SelectAll();

        Assert.True(state.CleanupPreviewAnimationsSelected);
        Assert.True(state.CleanupThumbnailsSelected);
        Assert.True(state.CleanupTimelineSavesSelected);
        Assert.True(state.CleanupExportVideosSelected);
        Assert.True(state.HasSelectedCleanupTargets);
    }

    [Fact]
    public void ClearAll_ClearsEveryCleanupTarget()
    {
        var state = MainWindowResourceCleanupSelectionController.ClearAll();

        Assert.False(state.CleanupPreviewAnimationsSelected);
        Assert.False(state.CleanupThumbnailsSelected);
        Assert.False(state.CleanupTimelineSavesSelected);
        Assert.False(state.CleanupExportVideosSelected);
        Assert.False(state.HasSelectedCleanupTargets);
    }

    [Fact]
    public void ToSelection_MapsCleanupFlags()
    {
        var state = MainWindowResourceCleanupSelectionController.Build(
            cleanupPreviewAnimationsSelected: true,
            cleanupThumbnailsSelected: false,
            cleanupTimelineSavesSelected: true,
            cleanupExportVideosSelected: false);

        var selection = state.ToSelection();

        Assert.True(selection.CleanupPreviewAnimations);
        Assert.False(selection.CleanupThumbnails);
        Assert.True(selection.CleanupTimelineSaves);
        Assert.False(selection.CleanupExportVideos);
        Assert.True(selection.HasAnySelection);
    }
}
