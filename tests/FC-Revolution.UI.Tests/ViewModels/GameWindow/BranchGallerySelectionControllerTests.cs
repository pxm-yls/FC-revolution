using System;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGallerySelectionControllerTests
{
    [Fact]
    public void BuildNodeSelectionDecision_WithDifferentNode_ReturnsNodeIdAndNotifications()
    {
        var controller = new BranchGallerySelectionController();
        var node = CreateCanvasNode(frame: 120);

        var decision = controller.BuildNodeSelectionDecision(currentNode: null, nextNode: node);

        Assert.NotNull(decision);
        Assert.Equal(node.Id, decision!.SelectedNodeId);
        Assert.Contains(nameof(BranchGalleryViewModel.SelectedTitle), decision.PropertyNamesToNotify);
        Assert.DoesNotContain(nameof(BranchGalleryViewModel.HasPreviewSelection), decision.PropertyNamesToNotify);
    }

    [Fact]
    public void BuildPreviewSelectionDecision_WithPreview_ClearsSelectedNodeIdAndSetsPreviewId()
    {
        var controller = new BranchGallerySelectionController();
        var previewNode = CreatePreviewNode(frame: 180);

        var decision = controller.BuildPreviewSelectionDecision(
            currentPreviewNode: null,
            nextPreviewNode: previewNode,
            currentSelectedNodeId: "branch:old");

        Assert.NotNull(decision);
        Assert.Null(decision!.SelectedNodeId);
        Assert.Equal(previewNode.Id, decision.SelectedPreviewNodeId);
        Assert.Contains(nameof(BranchGalleryViewModel.HasPreviewSelection), decision.PropertyNamesToNotify);
        Assert.Contains(nameof(BranchGalleryViewModel.EditablePreviewTitle), decision.PropertyNamesToNotify);
    }

    [Fact]
    public void BuildSelectedDisplay_WhenPreviewSelected_PrefersPreviewValues()
    {
        var controller = new BranchGallerySelectionController();
        var previewNode = CreatePreviewNode(frame: 180, title: "Boss Shot");

        Assert.Equal("Boss Shot", controller.BuildSelectedTitle(selectedNode: null, previewNode));
        Assert.Equal("画面节点 • 00:03", controller.BuildSelectedSubtitle(selectedNode: null, previewNode));
        Assert.Equal("帧 180 • 画面节点", controller.BuildSelectedMeta(selectedNode: null, previewNode));
        Assert.Equal("Boss Shot", controller.GetEditablePreviewTitle(previewNode));
    }

    [Fact]
    public void BuildSelectedDisplay_WhenNothingSelected_ReturnsPlaceholders()
    {
        var controller = new BranchGallerySelectionController();

        Assert.False(controller.HasSelection(selectedNode: null, selectedPreviewNode: null));
        Assert.False(controller.HasBranchSelection(selectedNode: null));
        Assert.False(controller.HasPreviewSelection(selectedPreviewNode: null));
        Assert.False(controller.CanMarkExportRange(selectedNode: null));
        Assert.Equal("未选择节点", controller.BuildSelectedTitle(selectedNode: null, selectedPreviewNode: null));
        Assert.Equal("点击节点查看详情", controller.BuildSelectedSubtitle(selectedNode: null, selectedPreviewNode: null));
        Assert.Equal("", controller.BuildSelectedMeta(selectedNode: null, selectedPreviewNode: null));
        Assert.Equal("尚未设置导出区间", controller.BuildExportRangeLabel(exportStartNode: null, exportEndNode: null));
    }

    [Fact]
    public void BuildExportRangeLabel_UsesNormalizedFrameOrder()
    {
        var controller = new BranchGallerySelectionController();
        var startNode = CreateCanvasNode(frame: 360);
        var endNode = CreateCanvasNode(frame: 120);

        var label = controller.BuildExportRangeLabel(startNode, endNode);

        Assert.Equal("导出区间: 120 - 360", label);
    }

    private static BranchCanvasNode CreateCanvasNode(long frame) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = $"Node {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = new DateTime(2026, 4, 9, 9, 0, 0, DateTimeKind.Utc),
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
            BranchPoint = CreateBranchPoint(frame)
        };

    private static BranchPreviewNode CreatePreviewNode(long frame, string? title = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Frame = frame,
            TimestampSeconds = frame / 60d,
            Title = title ?? $"Preview {frame}",
            Bitmap = null
        };

    private static CoreBranchPoint CreateBranchPoint(long frame) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"Branch {frame}",
            RomPath = "/tmp/test.nes",
            Frame = frame,
            TimestampSeconds = frame / 60d,
            Snapshot = new CoreTimelineSnapshot
            {
                Frame = frame,
                TimestampSeconds = frame / 60d,
                Thumbnail = [],
                State = new CoreStateBlob
                {
                    Format = "test/state",
                    Data = []
                }
            },
            CreatedAt = new DateTime(2026, 4, 9, 9, 0, 0, DateTimeKind.Utc)
        };
}
