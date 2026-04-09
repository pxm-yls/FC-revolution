using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryTimelineNavigationControllerTests
{
    [Fact]
    public void BuildLoadBranchDecision_WithBranchNode_ReturnsLoadBranchAction()
    {
        var branchPoint = CreateBranchPoint("Boss Branch", frame: 180);
        var node = CreateNode(frame: 180, branchPoint);

        var decision = BranchGalleryTimelineNavigationController.BuildLoadBranchDecision(node);

        Assert.Equal(BranchGalleryTimelineNavigationAction.LoadBranch, decision.Action);
        Assert.Same(node, decision.SelectedNode);
        Assert.Same(branchPoint, decision.BranchPoint);
    }

    [Fact]
    public void BuildLoadBranchDecision_WithoutBranchNode_ReturnsNoOp()
    {
        var decision = BranchGalleryTimelineNavigationController.BuildLoadBranchDecision(
            CreateNode(frame: 42, branchPoint: null));

        Assert.Equal(BranchGalleryTimelineNavigationAction.None, decision.Action);
        Assert.Null(decision.BranchPoint);
    }

    [Fact]
    public void BuildLoadBranchResult_ProjectsStatusAndJump()
    {
        var branchPoint = CreateBranchPoint("Boss Branch", frame: 180);

        var result = BranchGalleryTimelineNavigationController.BuildLoadBranchResult(branchPoint);

        Assert.True(result.ShouldNotifyTimelineJump);
        Assert.False(result.ShouldRefreshAll);
        Assert.Equal("已载入节点「Boss Branch」帧 180", result.StatusText);
    }

    [Fact]
    public void BuildSeekDecision_WithMainlineNode_ReturnsSeekFrameAction()
    {
        var node = CreateNode(frame: 240, branchPoint: null);

        var decision = BranchGalleryTimelineNavigationController.BuildSeekDecision(node);

        Assert.Equal(BranchGalleryTimelineNavigationAction.SeekFrame, decision.Action);
        Assert.Same(node, decision.SelectedNode);
        Assert.Equal(240, decision.TargetFrame);
    }

    [Fact]
    public void BuildSeekDecision_WithBranchNode_ReusesLoadBranchAction()
    {
        var branchPoint = CreateBranchPoint("Branch A", frame: 120);
        var node = CreateNode(frame: 120, branchPoint);

        var decision = BranchGalleryTimelineNavigationController.BuildSeekDecision(node);

        Assert.Equal(BranchGalleryTimelineNavigationAction.LoadBranch, decision.Action);
        Assert.Same(branchPoint, decision.BranchPoint);
    }

    [Fact]
    public void BuildSeekFrameResult_WhenSeekFails_DoesNotRequestJump()
    {
        var result = BranchGalleryTimelineNavigationController.BuildSeekFrameResult(-1);

        Assert.False(result.ShouldNotifyTimelineJump);
        Assert.False(result.ShouldRefreshAll);
        Assert.Equal("未找到可载入的主线快照", result.StatusText);
    }

    [Fact]
    public void BuildRewindDecision_WithInvalidFrames_ReturnsNoOp()
    {
        var decision = BranchGalleryTimelineNavigationController.BuildRewindDecision("invalid");

        Assert.Equal(BranchGalleryTimelineNavigationAction.None, decision.Action);
    }

    [Fact]
    public void BuildRewindResult_WhenRewindSucceeds_RequestsRefreshAndJump()
    {
        var result = BranchGalleryTimelineNavigationController.BuildRewindResult(88);

        Assert.True(result.ShouldNotifyTimelineJump);
        Assert.True(result.ShouldRefreshAll);
        Assert.Equal("已回退至帧 88", result.StatusText);
    }

    [Fact]
    public void BuildRewindResult_WhenRewindFails_RefreshesWithoutJump()
    {
        var result = BranchGalleryTimelineNavigationController.BuildRewindResult(-1);

        Assert.False(result.ShouldNotifyTimelineJump);
        Assert.True(result.ShouldRefreshAll);
        Assert.Equal("无可用快照", result.StatusText);
    }

    private static BranchCanvasNode CreateNode(long frame, CoreBranchPoint? branchPoint) =>
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

    private static CoreBranchPoint CreateBranchPoint(string name, long frame) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RomPath = "/tmp/test-rom.nes",
            Frame = frame,
            TimestampSeconds = frame / 60.0,
            Snapshot = new CoreTimelineSnapshot
            {
                Frame = frame,
                TimestampSeconds = frame / 60.0,
                Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray(),
                State = new CoreStateBlob
                {
                    Format = "test/snapshot",
                    Data = []
                }
            },
            CreatedAt = DateTime.UtcNow
        };
}
