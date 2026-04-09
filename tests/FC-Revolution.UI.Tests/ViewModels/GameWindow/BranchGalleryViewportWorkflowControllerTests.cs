using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryViewportWorkflowControllerTests
{
    [Fact]
    public void ToggleOrientation_SwitchesOrientationAndRebuilds()
    {
        var orientation = BranchLayoutOrientation.Horizontal;
        var rebuildCalls = 0;
        var controller = CreateController(
            useCenteredTimelineRail: false,
            getOrientation: () => orientation,
            setOrientation: value => orientation = value,
            getZoomFactor: () => 1.0,
            setZoomFactor: _ => { },
            getSecondsPer100Pixels: () => 30,
            setSecondsPer100Pixels: _ => { },
            rebuildCanvas: () => rebuildCalls++);

        controller.ToggleOrientation();

        Assert.Equal(BranchLayoutOrientation.Vertical, orientation);
        Assert.Equal(1, rebuildCalls);
    }

    [Fact]
    public void ZoomCommands_UpdateZoomAndRebuildEachTime()
    {
        var zoomFactor = 1.0;
        var rebuildCalls = 0;
        var controller = CreateController(
            useCenteredTimelineRail: false,
            getOrientation: () => BranchLayoutOrientation.Horizontal,
            setOrientation: _ => { },
            getZoomFactor: () => zoomFactor,
            setZoomFactor: value => zoomFactor = value,
            getSecondsPer100Pixels: () => 30,
            setSecondsPer100Pixels: _ => { },
            rebuildCanvas: () => rebuildCalls++);

        controller.ZoomIn();
        controller.ZoomOut();
        controller.ResetZoom();

        Assert.Equal(1.0, zoomFactor);
        Assert.Equal(3, rebuildCalls);
    }

    [Fact]
    public void AdjustZoom_WhenCenteredRailEnabled_AdjustsTimeScaleOnly()
    {
        var zoomFactor = 1.0;
        var secondsPer100Pixels = 30;
        var rebuildCalls = 0;
        var controller = CreateController(
            useCenteredTimelineRail: true,
            getOrientation: () => BranchLayoutOrientation.Horizontal,
            setOrientation: _ => { },
            getZoomFactor: () => zoomFactor,
            setZoomFactor: value => zoomFactor = value,
            getSecondsPer100Pixels: () => secondsPer100Pixels,
            setSecondsPer100Pixels: value => secondsPer100Pixels = value,
            rebuildCanvas: () => rebuildCalls++);

        controller.AdjustZoom(delta: -0.2);

        Assert.Equal(1.0, zoomFactor);
        Assert.Equal(60, secondsPer100Pixels);
        Assert.Equal(0, rebuildCalls);
    }

    [Fact]
    public void AdjustTimeScale_HalvesAndClampsAtMinimum()
    {
        var secondsPer100Pixels = 15;
        var controller = CreateController(
            useCenteredTimelineRail: false,
            getOrientation: () => BranchLayoutOrientation.Horizontal,
            setOrientation: _ => { },
            getZoomFactor: () => 1.0,
            setZoomFactor: _ => { },
            getSecondsPer100Pixels: () => secondsPer100Pixels,
            setSecondsPer100Pixels: value => secondsPer100Pixels = value,
            rebuildCanvas: () => { });

        controller.AdjustTimeScale(direction: -1);

        Assert.Equal(15, secondsPer100Pixels);
    }

    private static BranchGalleryViewportWorkflowController CreateController(
        bool useCenteredTimelineRail,
        Func<BranchLayoutOrientation> getOrientation,
        Action<BranchLayoutOrientation> setOrientation,
        Func<double> getZoomFactor,
        Action<double> setZoomFactor,
        Func<int> getSecondsPer100Pixels,
        Action<int> setSecondsPer100Pixels,
        Action rebuildCanvas)
        => new(
            getOrientation,
            setOrientation,
            getZoomFactor,
            setZoomFactor,
            getSecondsPer100Pixels,
            setSecondsPer100Pixels,
            rebuildCanvas,
            useCenteredTimelineRail);
}
