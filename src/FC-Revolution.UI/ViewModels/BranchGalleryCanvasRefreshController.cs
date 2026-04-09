using System;
using System.Collections.Generic;
using System.Linq;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct BranchGalleryCanvasRefreshRequest(
    IReadOnlyList<CoreTimelineThumbnail> Timeline,
    IReadOnlyList<BranchPoint> Roots,
    IReadOnlyList<BranchPreviewNode> PreviewNodes,
    string? RomPath,
    BranchLayoutOrientation Orientation,
    double ZoomFactor,
    bool UseCenteredTimelineRail,
    int SecondsPer100Pixels,
    int AxisIntervalSeconds,
    double CurrentTimestampSeconds,
    string? SelectedNodeId,
    Guid? SelectedPreviewNodeId,
    double NodeWidth,
    double NodeHeight);

internal readonly record struct BranchGalleryCanvasRefreshResult(
    IReadOnlyList<BranchCanvasNode> Nodes,
    IReadOnlyList<BranchCanvasEdge> Edges,
    IReadOnlyList<BranchTimelineTick> AxisTicks,
    IReadOnlyList<BranchPreviewMarker> PreviewMarkers,
    double CanvasWidth,
    double CanvasHeight,
    double CurrentMarkerX,
    double MainAxisY,
    string? SelectedNodeId,
    Guid? SelectedPreviewNodeId);

internal sealed class BranchGalleryCanvasRefreshController
{
    private readonly BranchGalleryCanvasProjectionController _projectionController;

    public BranchGalleryCanvasRefreshController(BranchGalleryCanvasProjectionController projectionController)
    {
        ArgumentNullException.ThrowIfNull(projectionController);
        _projectionController = projectionController;
    }

    public BranchGalleryCanvasRefreshResult BuildRefreshResult(BranchGalleryCanvasRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Timeline);
        ArgumentNullException.ThrowIfNull(request.Roots);
        ArgumentNullException.ThrowIfNull(request.PreviewNodes);

        var timeline = request.Timeline
            .Take(90)
            .OrderBy(entry => entry.Frame)
            .ToList();
        var roots = request.Roots
            .Where(root => string.IsNullOrWhiteSpace(request.RomPath) || string.Equals(root.RomPath, request.RomPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(root => root.Frame)
            .ToList();
        var projection = _projectionController.BuildProjection(
            new BranchGalleryCanvasProjectionRequest(
                timeline,
                roots,
                request.PreviewNodes,
                request.Orientation,
                request.ZoomFactor,
                request.UseCenteredTimelineRail,
                request.SecondsPer100Pixels,
                request.AxisIntervalSeconds,
                request.CurrentTimestampSeconds,
                request.SelectedNodeId,
                request.NodeWidth,
                request.NodeHeight));

        var selectedNodeId = projection.Nodes.Count == 0
            ? null
            : request.SelectedNodeId == null
                ? projection.Nodes.FirstOrDefault(node => node.IsBranchNode)?.Id ?? projection.Nodes.FirstOrDefault()?.Id
                : projection.Nodes.FirstOrDefault(node => node.Id == request.SelectedNodeId)?.Id
                  ?? projection.Nodes.FirstOrDefault(node => node.IsBranchNode)?.Id
                  ?? projection.Nodes.FirstOrDefault()?.Id;
        var selectedPreviewNodeId = projection.Nodes.Count == 0
            ? request.SelectedPreviewNodeId == null
                ? request.PreviewNodes.FirstOrDefault()?.Id
                : request.PreviewNodes.FirstOrDefault(node => node.Id == request.SelectedPreviewNodeId.Value)?.Id
            : request.SelectedPreviewNodeId == null
                ? null
                : request.PreviewNodes.FirstOrDefault(node => node.Id == request.SelectedPreviewNodeId.Value)?.Id;

        return new BranchGalleryCanvasRefreshResult(
            projection.Nodes,
            projection.Edges,
            projection.AxisTicks,
            projection.PreviewMarkers,
            projection.CanvasWidth,
            projection.CanvasHeight,
            projection.CurrentMarkerX,
            projection.MainAxisY,
            selectedNodeId,
            selectedPreviewNodeId);
    }
}
