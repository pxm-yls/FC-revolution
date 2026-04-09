using System.Linq;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryCanvasProjectionControllerTests
{
    [Fact]
    public void BuildProjection_WithSelectedBranchNode_ProjectsNodesEdgesAndSelectionBorder()
    {
        var controller = new BranchGalleryCanvasProjectionController();
        var root = CreateLegacyBranchPoint("Boss Branch", frame: 180);

        var result = controller.BuildProjection(
            new BranchGalleryCanvasProjectionRequest(
                Timeline: CreateTimeline(60, 120),
                Roots: [root],
                PreviewNodes: [],
                Orientation: BranchLayoutOrientation.Horizontal,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 2.0,
                SelectedNodeId: $"branch:{root.Id}",
                NodeWidth: 152,
                NodeHeight: 130));

        var branchNode = Assert.Single(result.Nodes, node => node.Id == $"branch:{root.Id}");
        Assert.Equal(3, branchNode.BorderThicknessValue);
        Assert.Equal(root.Id, branchNode.BranchPoint?.Id);
        Assert.NotEmpty(result.Edges);
        Assert.True(result.CanvasWidth >= 1400);
        Assert.True(result.CanvasHeight >= 320);
    }

    [Fact]
    public void BuildProjection_VerticalOrientation_SwapsNodeAxes()
    {
        var controller = new BranchGalleryCanvasProjectionController();
        var horizontal = controller.BuildProjection(
            new BranchGalleryCanvasProjectionRequest(
                Timeline: CreateTimeline(60, 120),
                Roots: [],
                PreviewNodes: [],
                Orientation: BranchLayoutOrientation.Horizontal,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 2.0,
                SelectedNodeId: null,
                NodeWidth: 152,
                NodeHeight: 130));
        var vertical = controller.BuildProjection(
            new BranchGalleryCanvasProjectionRequest(
                Timeline: CreateTimeline(60, 120),
                Roots: [],
                PreviewNodes: [],
                Orientation: BranchLayoutOrientation.Vertical,
                ZoomFactor: 1.0,
                UseCenteredTimelineRail: false,
                SecondsPer100Pixels: 30,
                AxisIntervalSeconds: 30,
                CurrentTimestampSeconds: 2.0,
                SelectedNodeId: null,
                NodeWidth: 152,
                NodeHeight: 130));

        var horizontalNode = Assert.Single(horizontal.Nodes, node => node.Id == "main:60");
        var verticalNode = Assert.Single(vertical.Nodes, node => node.Id == "main:60");

        Assert.Equal(horizontalNode.Y, verticalNode.X);
        Assert.Equal(horizontalNode.X, verticalNode.Y);

        var horizontalEdge = Assert.Single(horizontal.Edges);
        var verticalEdge = Assert.Single(vertical.Edges);
        Assert.Equal(horizontalEdge.StartPoint.Y, verticalEdge.StartPoint.X);
        Assert.Equal(horizontalEdge.StartPoint.X, verticalEdge.StartPoint.Y);
    }

    [Fact]
    public void CreatePreviewNodeFromSnapshot_UsesNearestSnapshotWhenBranchPointMissing()
    {
        var controller = new BranchGalleryCanvasProjectionController();
        var snapshot = new CoreTimelineSnapshot
        {
            Frame = 240,
            TimestampSeconds = 4.0,
            Thumbnail = Enumerable.Repeat(0xFF336699u, 64 * 60).ToArray(),
            State = new CoreStateBlob
            {
                Format = "test/state",
                Data = []
            }
        };
        var timeTravelService = new FakeTimeTravelService(snapshot);
        var node = CreateCanvasNode(frame: 240, branchPoint: null);

        var previewNode = controller.CreatePreviewNodeFromSnapshot(node, timeTravelService, "/tmp/test-rom.nes");

        Assert.NotNull(previewNode);
        Assert.Equal(240, previewNode.Frame);
        Assert.Equal(4.0, previewNode.TimestampSeconds);
        Assert.Equal(node.Title, previewNode.Title);
        Assert.NotNull(previewNode.Bitmap);
    }

    private static IReadOnlyList<CoreTimelineThumbnail> CreateTimeline(params long[] frames) =>
        frames.Select(frame => new CoreTimelineThumbnail(frame, Enumerable.Repeat((uint)frame, 64 * 60).ToArray())).ToList();

    private static BranchPoint CreateLegacyBranchPoint(string name, long frame) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RomPath = "/tmp/test-rom.nes",
            Frame = frame,
            Timestamp = frame / 60.0,
            Snapshot = new FrameSnapshot
            {
                Frame = frame,
                Timestamp = frame / 60.0,
                Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray()
            },
            CreatedAt = DateTime.UtcNow
        };

    private static BranchCanvasNode CreateCanvasNode(long frame, CoreBranchPoint? branchPoint) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = branchPoint?.Name ?? $"主线帧 {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = branchPoint != null,
            IsMainlineNode = branchPoint == null,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = branchPoint
        };

    private sealed class FakeTimeTravelService(CoreTimelineSnapshot snapshot) : ITimeTravelService
    {
        public long CurrentFrame => snapshot.Frame;
        public double CurrentTimestampSeconds => snapshot.TimestampSeconds;
        public int SnapshotInterval { get; set; } = 5;
        public int HotCacheCount => 0;
        public int WarmCacheCount => 0;
        public long NewestFrame => snapshot.Frame;
        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, snapshot.Frame, SnapshotInterval);
        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];
        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) => throw new NotSupportedException();
        public void RestoreSnapshot(CoreTimelineSnapshot snapshotToRestore) => throw new NotSupportedException();
        public long SeekToFrame(long frame) => throw new NotSupportedException();
        public long RewindFrames(int frames) => throw new NotSupportedException();
        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => frame == snapshot.Frame ? snapshot : null;
        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => null;
        public void PauseRecording() => throw new NotSupportedException();
        public void ResumeRecording() => throw new NotSupportedException();
    }
}
