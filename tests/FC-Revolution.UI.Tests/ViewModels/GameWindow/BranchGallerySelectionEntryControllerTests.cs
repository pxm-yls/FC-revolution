using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGallerySelectionEntryControllerTests
{
    [Fact]
    public void SelectNode_WhenNodeProvided_ClearsPreviewThenSelectsNode()
    {
        var trace = new List<string>();
        BranchCanvasNode? selectedNode = null;
        var controller = new BranchGallerySelectionEntryController(
            node =>
            {
                selectedNode = node;
                trace.Add("node");
            },
            preview =>
            {
                Assert.Null(preview);
                trace.Add("preview");
            },
            _ => null,
            _ => { });
        var node = CreateCanvasNode(frame: 120);

        controller.SelectNode(node);

        Assert.Same(node, selectedNode);
        Assert.Equal(["preview", "node"], trace);
    }

    [Fact]
    public void SelectPreviewNode_ClearsNodeThenSelectsResolvedPreview()
    {
        var previewNode = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 180,
            TimestampSeconds = 3,
            Title = "Boss",
            Bitmap = null
        };
        var trace = new List<string>();
        BranchPreviewNode? selectedPreview = null;
        var controller = new BranchGallerySelectionEntryController(
            node =>
            {
                Assert.Null(node);
                trace.Add("node");
            },
            preview =>
            {
                selectedPreview = preview;
                trace.Add("preview");
            },
            previewNodeId => previewNodeId == previewNode.Id ? previewNode : null,
            _ => { });

        controller.SelectPreviewNode(previewNode.Id);

        Assert.Same(previewNode, selectedPreview);
        Assert.Equal(["node", "preview"], trace);
    }

    [Fact]
    public void SeekToNode_WithMainlineNode_SelectsNodeAndExecutesSeekDecision()
    {
        BranchCanvasNode? selectedNode = null;
        BranchGalleryTimelineNavigationDecision captured = default;
        var controller = new BranchGallerySelectionEntryController(
            node => selectedNode = node,
            _ => { },
            _ => null,
            decision => captured = decision);
        var node = CreateCanvasNode(frame: 210);

        controller.SeekToNode(node);

        Assert.Same(node, selectedNode);
        Assert.Equal(BranchGalleryTimelineNavigationAction.SeekFrame, captured.Action);
        Assert.Equal(210, captured.TargetFrame);
    }

    [Fact]
    public void SeekToNode_WithNullNode_ExecutesDefaultDecisionWithoutSelection()
    {
        var selectCalls = 0;
        BranchGalleryTimelineNavigationDecision captured = new(
            BranchGalleryTimelineNavigationAction.RewindFrames,
            SelectedNode: null,
            BranchPoint: null,
            TargetFrame: -1,
            FrameCount: -1);
        var controller = new BranchGallerySelectionEntryController(
            _ => selectCalls++,
            _ => { },
            _ => null,
            decision => captured = decision);

        controller.SeekToNode(node: null);

        Assert.Equal(0, selectCalls);
        Assert.Equal(BranchGalleryTimelineNavigationAction.None, captured.Action);
    }

    private static BranchCanvasNode CreateCanvasNode(long frame) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = $"Node {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = false,
            IsMainlineNode = true,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = null
        };
}
