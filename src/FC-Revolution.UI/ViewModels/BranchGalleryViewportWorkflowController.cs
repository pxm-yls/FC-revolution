using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryViewportWorkflowController
{
    private const double ZoomStep = 0.15;

    private readonly Func<BranchLayoutOrientation> _getOrientation;
    private readonly Action<BranchLayoutOrientation> _setOrientation;
    private readonly Func<double> _getZoomFactor;
    private readonly Action<double> _setZoomFactor;
    private readonly Func<int> _getSecondsPer100Pixels;
    private readonly Action<int> _setSecondsPer100Pixels;
    private readonly Action _rebuildCanvas;
    private readonly bool _useCenteredTimelineRail;

    public BranchGalleryViewportWorkflowController(
        Func<BranchLayoutOrientation> getOrientation,
        Action<BranchLayoutOrientation> setOrientation,
        Func<double> getZoomFactor,
        Action<double> setZoomFactor,
        Func<int> getSecondsPer100Pixels,
        Action<int> setSecondsPer100Pixels,
        Action rebuildCanvas,
        bool useCenteredTimelineRail)
    {
        ArgumentNullException.ThrowIfNull(getOrientation);
        ArgumentNullException.ThrowIfNull(setOrientation);
        ArgumentNullException.ThrowIfNull(getZoomFactor);
        ArgumentNullException.ThrowIfNull(setZoomFactor);
        ArgumentNullException.ThrowIfNull(getSecondsPer100Pixels);
        ArgumentNullException.ThrowIfNull(setSecondsPer100Pixels);
        ArgumentNullException.ThrowIfNull(rebuildCanvas);

        _getOrientation = getOrientation;
        _setOrientation = setOrientation;
        _getZoomFactor = getZoomFactor;
        _setZoomFactor = setZoomFactor;
        _getSecondsPer100Pixels = getSecondsPer100Pixels;
        _setSecondsPer100Pixels = setSecondsPer100Pixels;
        _rebuildCanvas = rebuildCanvas;
        _useCenteredTimelineRail = useCenteredTimelineRail;
    }

    public void ToggleOrientation()
    {
        _setOrientation(
            _getOrientation() == BranchLayoutOrientation.Horizontal
                ? BranchLayoutOrientation.Vertical
                : BranchLayoutOrientation.Horizontal);
        _rebuildCanvas();
    }

    public void ZoomIn()
    {
        _setZoomFactor(_getZoomFactor() + ZoomStep);
        _rebuildCanvas();
    }

    public void ZoomOut()
    {
        _setZoomFactor(_getZoomFactor() - ZoomStep);
        _rebuildCanvas();
    }

    public void ResetZoom()
    {
        _setZoomFactor(1.0);
        _rebuildCanvas();
    }

    public void AdjustZoom(double delta)
    {
        if (_useCenteredTimelineRail)
        {
            AdjustTimeScale(delta < 0 ? 1 : -1);
            return;
        }

        _setZoomFactor(_getZoomFactor() + delta);
        _rebuildCanvas();
    }

    public void AdjustTimeScale(int direction)
    {
        var scale = _getSecondsPer100Pixels();
        scale = direction > 0
            ? scale < 60 ? scale * 2 : scale * 2
            : scale <= 60 ? Math.Max(15, (int)Math.Ceiling(scale / 2d)) : Math.Max(15, scale / 2);
        _setSecondsPer100Pixels(scale);
    }
}
