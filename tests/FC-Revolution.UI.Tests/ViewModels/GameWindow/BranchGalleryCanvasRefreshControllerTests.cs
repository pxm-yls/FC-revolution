using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryCanvasRefreshControllerTests
{
    [Fact]
    public void BuildRefreshResult_WhenNoProjectedNodes_SelectsFirstPreviewNode()
    {
        var controller = new BranchGalleryCanvasRefreshController(new BranchGalleryCanvasProjectionController());
        var previewNode = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 30,
            TimestampSeconds = 0.5,
            Title = "Preview",
            Bitmap = null
        };

        var result = controller.BuildRefreshResult(
            new BranchGalleryCanvasRefreshRequest(
                Timeline: [],
                Roots: [],
                PreviewNodes: [previewNode],
                RomPath: "/tmp/test.nes",
                Orientation: BranchLayoutOrientation.Horizontal,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 0,
                SelectedNodeId: null,
                SelectedPreviewNodeId: null,
                NodeWidth: 152,
                NodeHeight: 130));

        Assert.Empty(result.Nodes);
        Assert.Null(result.SelectedNodeId);
        Assert.Equal(previewNode.Id, result.SelectedPreviewNodeId);
    }

    [Fact]
    public void BuildRefreshResult_FiltersRootsByRomPath_AndRestoresPreviousBranchSelection()
    {
        var controller = new BranchGalleryCanvasRefreshController(new BranchGalleryCanvasProjectionController());
        var matchingRoot = CreateBranchPoint("Match", "/tmp/match.nes", frame: 120);
        var otherRoot = CreateBranchPoint("Other", "/tmp/other.nes", frame: 240);

        var result = controller.BuildRefreshResult(
            new BranchGalleryCanvasRefreshRequest(
                Timeline: [new CoreTimelineThumbnail(60, new uint[64 * 60])],
                Roots: [matchingRoot, otherRoot],
                PreviewNodes: [],
                RomPath: "/tmp/match.nes",
                Orientation: BranchLayoutOrientation.Horizontal,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 1,
                SelectedNodeId: $"branch:{matchingRoot.Id}",
                SelectedPreviewNodeId: null,
                NodeWidth: 152,
                NodeHeight: 130));

        Assert.Contains(result.Nodes, node => node.Id == $"branch:{matchingRoot.Id}");
        Assert.DoesNotContain(result.Nodes, node => node.Id == $"branch:{otherRoot.Id}");
        Assert.Equal($"branch:{matchingRoot.Id}", result.SelectedNodeId);
    }

    [Fact]
    public void BuildRefreshResult_WhenPreviousSelectionMissing_FallsBackToFirstBranchNode()
    {
        var controller = new BranchGalleryCanvasRefreshController(new BranchGalleryCanvasProjectionController());
        var branchRoot = CreateBranchPoint("Branch", "/tmp/test.nes", frame: 180);

        var result = controller.BuildRefreshResult(
            new BranchGalleryCanvasRefreshRequest(
                Timeline: [new CoreTimelineThumbnail(60, new uint[64 * 60])],
                Roots: [branchRoot],
                PreviewNodes: [],
                RomPath: "/tmp/test.nes",
                Orientation: BranchLayoutOrientation.Horizontal,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 1,
                SelectedNodeId: "branch:missing",
                SelectedPreviewNodeId: null,
                NodeWidth: 152,
                NodeHeight: 130));

        Assert.Equal($"branch:{branchRoot.Id}", result.SelectedNodeId);
    }

    private static CoreBranchPoint CreateBranchPoint(string name, string romPath, long frame) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RomPath = romPath,
            Frame = frame,
            TimestampSeconds = frame / 60d,
            Snapshot = new CoreTimelineSnapshot
            {
                Frame = frame,
                TimestampSeconds = frame / 60d,
                Thumbnail = new uint[64 * 60],
                State = new CoreStateBlob
                {
                    Format = "test/state",
                    Data = []
                }
            },
            CreatedAt = DateTime.UtcNow
        };
}
