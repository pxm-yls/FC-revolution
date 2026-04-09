using System;
using FC_Revolution.UI.ViewModels;
using Xunit;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryExportWorkflowControllerTests
{
    [Fact]
    public void BuildSetStartDecision_WithNode_PopulatesStartStatus()
    {
        var node = CreateCanvasNode(frame: 120);

        var decision = BranchGalleryExportWorkflowController.BuildSetStartDecision(
            node,
            currentEndNodeId: "end-node");

        Assert.True(decision.ShouldApply);
        Assert.Same(node, decision.SelectedNode);
        Assert.Equal(node.Id, decision.ExportStartNodeId);
        Assert.Equal("end-node", decision.ExportEndNodeId);
        Assert.Equal($"已设置导出起点：帧 {node.Frame}", decision.StatusText);
    }

    [Fact]
    public void BuildSetStartDecision_WithNullNode_ReturnsNoOp()
    {
        var decision = BranchGalleryExportWorkflowController.BuildSetStartDecision(null, currentEndNodeId: null);

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.SelectedNode);
        Assert.Null(decision.StatusText);
    }

    [Fact]
    public void BuildSetEndDecision_WithNode_PopulatesEndStatus()
    {
        var node = CreateCanvasNode(frame: 200);

        var decision = BranchGalleryExportWorkflowController.BuildSetEndDecision(
            node,
            currentStartNodeId: "start-node");

        Assert.True(decision.ShouldApply);
        Assert.Same(node, decision.SelectedNode);
        Assert.Equal("start-node", decision.ExportStartNodeId);
        Assert.Equal(node.Id, decision.ExportEndNodeId);
        Assert.Equal($"已设置导出终点：帧 {node.Frame}", decision.StatusText);
    }

    [Fact]
    public void BuildSetEndDecision_WithNullNode_ReturnsNoOp()
    {
        var decision = BranchGalleryExportWorkflowController.BuildSetEndDecision(null, currentStartNodeId: null);

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.SelectedNode);
        Assert.Null(decision.StatusText);
    }

    [Fact]
    public void BuildExportRangeLabel_WhenNodesMissing_ReturnsPlaceholder()
    {
        var label = BranchGalleryExportWorkflowController.BuildExportRangeLabel(null, null);

        Assert.Equal("尚未设置导出区间", label);
    }

    [Fact]
    public void BuildExportRangeLabel_NormalizesFrameOrder()
    {
        var start = CreateCanvasNode(frame: 300);
        var end = CreateCanvasNode(frame: 100);

        var label = BranchGalleryExportWorkflowController.BuildExportRangeLabel(start, end);

        Assert.Equal("导出区间: 100 - 300", label);
    }

    [Fact]
    public void BuildExportRequest_WhenRangeMissing_ReturnsMissingStatus()
    {
        var request = BranchGalleryExportWorkflowController.BuildExportRequest(null, null);

        Assert.False(request.ShouldExport);
        Assert.Equal("请先设置导出起点和终点", request.StatusText);
    }

    [Fact]
    public void BuildExportRequest_NormalizesRangeFrames()
    {
        var start = CreateCanvasNode(frame: 250);
        var end = CreateCanvasNode(frame: 150);

        var request = BranchGalleryExportWorkflowController.BuildExportRequest(start, end);

        Assert.True(request.ShouldExport);
        Assert.Equal(150, request.StartFrame);
        Assert.Equal(250, request.EndFrame);
        Assert.Null(request.StatusText);
    }

    [Fact]
    public void BuildExportSuccessStatus_IncludesFileName()
    {
        var status = BranchGalleryExportWorkflowController.BuildExportSuccessStatus("/tmp/export.mp4");

        Assert.Equal("已导出 MP4: export.mp4", status);
    }

    [Fact]
    public void BuildExportFailureStatus_IncludesMessage()
    {
        var status = BranchGalleryExportWorkflowController.BuildExportFailureStatus("boom");

        Assert.Equal("导出失败: boom", status);
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
