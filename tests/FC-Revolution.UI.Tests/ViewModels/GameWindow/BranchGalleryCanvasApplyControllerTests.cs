using System.Collections.ObjectModel;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryCanvasApplyControllerTests
{
    [Fact]
    public void Apply_ReplacesCollectionsAndRestoresSelections()
    {
        var canvasNodes = new ObservableCollection<BranchCanvasNode> { CreateNode("old:node", frame: 60) };
        var canvasEdges = new ObservableCollection<BranchCanvasEdge>
        {
            new() { StartPoint = new(0, 0), EndPoint = new(10, 10), IsPrimary = false }
        };
        var axisTicks = new ObservableCollection<BranchTimelineTick>
        {
            new() { X = 10, FrameLabel = "帧 10", TimeLabel = "00:00:00" }
        };
        var previewMarkers = new ObservableCollection<BranchPreviewMarker>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Frame = 10,
                X = 1,
                Y = 2,
                Diameter = 6,
                Title = "old",
                Subtitle = "old",
                Bitmap = null
            }
        };
        var previewNode = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 180,
            TimestampSeconds = 3,
            Title = "Boss",
            Bitmap = null
        };
        var previewNodes = new List<BranchPreviewNode> { previewNode };
        var newNodeA = CreateNode("main:120", frame: 120);
        var newNodeB = CreateNode("branch:180", frame: 180);

        var selectedNode = CreateNode("stale:selected", frame: 1);
        var selectedPreviewNode = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 1,
            TimestampSeconds = 0,
            Title = "stale",
            Bitmap = null
        };
        var mainAxisY = 0d;
        var currentMarkerX = 0d;
        var canvasWidth = 0d;
        var canvasHeight = 0d;
        var controller = new BranchGalleryCanvasApplyController(
            canvasNodes,
            canvasEdges,
            axisTicks,
            previewMarkers,
            previewNodes,
            value => mainAxisY = value,
            value => currentMarkerX = value,
            value => canvasWidth = value,
            value => canvasHeight = value,
            node => selectedNode = node,
            preview => selectedPreviewNode = preview);
        var result = new BranchGalleryCanvasRefreshResult(
            Nodes: [newNodeA, newNodeB],
            Edges:
            [
                new BranchCanvasEdge
                {
                    StartPoint = new(20, 20),
                    EndPoint = new(40, 40),
                    IsPrimary = true
                }
            ],
            AxisTicks: [new BranchTimelineTick { X = 180, FrameLabel = "帧 180", TimeLabel = "00:00:03" }],
            PreviewMarkers:
            [
                new BranchPreviewMarker
                {
                    Id = previewNode.Id,
                    Frame = previewNode.Frame,
                    X = 30,
                    Y = 120,
                    Diameter = 8,
                    Title = previewNode.Title,
                    Subtitle = "画面节点",
                    Bitmap = null
                }
            ],
            CanvasWidth: 1920,
            CanvasHeight: 1080,
            CurrentMarkerX: 360,
            MainAxisY: 140,
            SelectedNodeId: newNodeB.Id,
            SelectedPreviewNodeId: previewNode.Id);

        controller.Apply(result);

        Assert.Equal(2, canvasNodes.Count);
        Assert.Equal("main:120", canvasNodes[0].Id);
        Assert.Single(canvasEdges);
        Assert.Single(axisTicks);
        Assert.Single(previewMarkers);
        Assert.Equal(140, mainAxisY);
        Assert.Equal(360, currentMarkerX);
        Assert.Equal(1920, canvasWidth);
        Assert.Equal(1080, canvasHeight);
        Assert.Same(newNodeB, selectedNode);
        Assert.Same(previewNode, selectedPreviewNode);
    }

    [Fact]
    public void Apply_WhenSelectionIdsMissing_ClearsSelections()
    {
        var canvasNodes = new ObservableCollection<BranchCanvasNode>();
        var canvasEdges = new ObservableCollection<BranchCanvasEdge>();
        var axisTicks = new ObservableCollection<BranchTimelineTick>();
        var previewMarkers = new ObservableCollection<BranchPreviewMarker>();
        var previewNodes = new List<BranchPreviewNode>();
        BranchCanvasNode? selectedNode = CreateNode("stale", frame: 1);
        BranchPreviewNode? selectedPreviewNode = new()
        {
            Id = Guid.NewGuid(),
            Frame = 1,
            TimestampSeconds = 0,
            Title = "stale",
            Bitmap = null
        };
        var controller = new BranchGalleryCanvasApplyController(
            canvasNodes,
            canvasEdges,
            axisTicks,
            previewMarkers,
            previewNodes,
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            node => selectedNode = node,
            preview => selectedPreviewNode = preview);

        controller.Apply(
            new BranchGalleryCanvasRefreshResult(
                Nodes: [],
                Edges: [],
                AxisTicks: [],
                PreviewMarkers: [],
                CanvasWidth: 1280,
                CanvasHeight: 720,
                CurrentMarkerX: 120,
                MainAxisY: 120,
                SelectedNodeId: null,
                SelectedPreviewNodeId: null));

        Assert.Null(selectedNode);
        Assert.Null(selectedPreviewNode);
    }

    private static BranchCanvasNode CreateNode(string id, long frame) =>
        new()
        {
            Id = id,
            Title = $"Node {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = id.StartsWith("branch:", StringComparison.Ordinal),
            IsMainlineNode = !id.StartsWith("branch:", StringComparison.Ordinal),
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = null
        };
}
