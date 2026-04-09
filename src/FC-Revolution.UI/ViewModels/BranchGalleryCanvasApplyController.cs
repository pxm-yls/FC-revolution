using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryCanvasApplyController
{
    private readonly ObservableCollection<BranchCanvasNode> _canvasNodes;
    private readonly ObservableCollection<BranchCanvasEdge> _canvasEdges;
    private readonly ObservableCollection<BranchTimelineTick> _axisTicks;
    private readonly ObservableCollection<BranchPreviewMarker> _previewMarkers;
    private readonly IReadOnlyList<BranchPreviewNode> _previewNodes;
    private readonly Action<double> _setMainAxisY;
    private readonly Action<double> _setCurrentMarkerX;
    private readonly Action<double> _setCanvasWidth;
    private readonly Action<double> _setCanvasHeight;
    private readonly Action<BranchCanvasNode?> _selectNode;
    private readonly Action<BranchPreviewNode?> _selectPreviewNode;

    public BranchGalleryCanvasApplyController(
        ObservableCollection<BranchCanvasNode> canvasNodes,
        ObservableCollection<BranchCanvasEdge> canvasEdges,
        ObservableCollection<BranchTimelineTick> axisTicks,
        ObservableCollection<BranchPreviewMarker> previewMarkers,
        IReadOnlyList<BranchPreviewNode> previewNodes,
        Action<double> setMainAxisY,
        Action<double> setCurrentMarkerX,
        Action<double> setCanvasWidth,
        Action<double> setCanvasHeight,
        Action<BranchCanvasNode?> selectNode,
        Action<BranchPreviewNode?> selectPreviewNode)
    {
        ArgumentNullException.ThrowIfNull(canvasNodes);
        ArgumentNullException.ThrowIfNull(canvasEdges);
        ArgumentNullException.ThrowIfNull(axisTicks);
        ArgumentNullException.ThrowIfNull(previewMarkers);
        ArgumentNullException.ThrowIfNull(previewNodes);
        ArgumentNullException.ThrowIfNull(setMainAxisY);
        ArgumentNullException.ThrowIfNull(setCurrentMarkerX);
        ArgumentNullException.ThrowIfNull(setCanvasWidth);
        ArgumentNullException.ThrowIfNull(setCanvasHeight);
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(selectPreviewNode);

        _canvasNodes = canvasNodes;
        _canvasEdges = canvasEdges;
        _axisTicks = axisTicks;
        _previewMarkers = previewMarkers;
        _previewNodes = previewNodes;
        _setMainAxisY = setMainAxisY;
        _setCurrentMarkerX = setCurrentMarkerX;
        _setCanvasWidth = setCanvasWidth;
        _setCanvasHeight = setCanvasHeight;
        _selectNode = selectNode;
        _selectPreviewNode = selectPreviewNode;
    }

    public void Apply(BranchGalleryCanvasRefreshResult refreshResult)
    {
        ReplaceCollection(_canvasNodes, refreshResult.Nodes);
        ReplaceCollection(_canvasEdges, refreshResult.Edges);
        ReplaceCollection(_axisTicks, refreshResult.AxisTicks);
        ReplaceCollection(_previewMarkers, refreshResult.PreviewMarkers);
        _setMainAxisY(refreshResult.MainAxisY);
        _setCurrentMarkerX(refreshResult.CurrentMarkerX);
        _setCanvasWidth(refreshResult.CanvasWidth);
        _setCanvasHeight(refreshResult.CanvasHeight);
        _selectNode(refreshResult.SelectedNodeId == null
            ? null
            : _canvasNodes.FirstOrDefault(node => node.Id == refreshResult.SelectedNodeId));
        _selectPreviewNode(refreshResult.SelectedPreviewNodeId == null
            ? null
            : _previewNodes.FirstOrDefault(node => node.Id == refreshResult.SelectedPreviewNodeId.Value));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }
}
