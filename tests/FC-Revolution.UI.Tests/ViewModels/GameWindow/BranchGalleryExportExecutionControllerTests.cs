using System;
using System.Threading.Tasks;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryExportExecutionControllerTests
{
    [Fact]
    public void ApplyMarkerDecision_UpdatesSelectionMarkersAndStatus()
    {
        var node = CreateNode(frame: 120);
        BranchCanvasNode? selectedNode = null;
        string? exportStartNodeId = null;
        string? exportEndNodeId = null;
        var rangeChangedCount = 0;
        string? status = null;
        var controller = new BranchGalleryExportExecutionController(
            _ => null,
            exportRange: null,
            nodeValue => selectedNode = nodeValue,
            (startNodeId, endNodeId) =>
            {
                exportStartNodeId = startNodeId;
                exportEndNodeId = endNodeId;
            },
            () => rangeChangedCount++,
            text => status = text);

        controller.ApplyMarkerDecision(
            BranchGalleryExportWorkflowController.BuildSetStartDecision(node, currentEndNodeId: "end-node"));

        Assert.Same(node, selectedNode);
        Assert.Equal(node.Id, exportStartNodeId);
        Assert.Equal("end-node", exportEndNodeId);
        Assert.Equal(1, rangeChangedCount);
        Assert.Equal("已设置导出起点：帧 120", status);
    }

    [Fact]
    public async Task ExecuteExportAsync_WithoutConfiguredRange_UpdatesMissingRangeStatus()
    {
        var exportCallCount = 0;
        string? status = null;
        var controller = new BranchGalleryExportExecutionController(
            _ => null,
            (_, _, _) =>
            {
                exportCallCount++;
                return Task.FromResult("/tmp/unused.mp4");
            },
            _ => { },
            (_, _) => { },
            () => { },
            text => status = text);

        await controller.ExecuteExportAsync("start-node", "end-node");

        Assert.Equal(0, exportCallCount);
        Assert.Equal("请先设置导出起点和终点", status);
    }

    [Fact]
    public async Task ExecuteExportAsync_NormalizesFramesAndUpdatesSuccessStatus()
    {
        var startNode = CreateNode(frame: 240);
        var endNode = CreateNode(frame: 120);
        ExportInvocation? invocation = null;
        string? status = null;
        var controller = new BranchGalleryExportExecutionController(
            nodeId => nodeId switch
            {
                "start" => startNode,
                "end" => endNode,
                _ => null
            },
            (node, startFrame, endFrame) =>
            {
                invocation = new ExportInvocation(node, startFrame, endFrame);
                return Task.FromResult("/tmp/export-120-240.mp4");
            },
            _ => { },
            (_, _) => { },
            () => { },
            text => status = text);

        await controller.ExecuteExportAsync("start", "end");

        Assert.NotNull(invocation);
        Assert.Same(startNode, invocation.Value.StartNode);
        Assert.Equal(120, invocation.Value.StartFrame);
        Assert.Equal(240, invocation.Value.EndFrame);
        Assert.Equal("已导出 MP4: export-120-240.mp4", status);
    }

    [Fact]
    public async Task ExecuteExportAsync_WhenExporterThrows_UpdatesFailureStatus()
    {
        var node = CreateNode(frame: 180);
        string? status = null;
        var controller = new BranchGalleryExportExecutionController(
            _ => node,
            (_, _, _) => throw new InvalidOperationException("boom"),
            _ => { },
            (_, _) => { },
            () => { },
            text => status = text);

        await controller.ExecuteExportAsync("start", "end");

        Assert.Equal("导出失败: boom", status);
    }

    private static BranchCanvasNode CreateNode(long frame) =>
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

    private readonly record struct ExportInvocation(BranchCanvasNode StartNode, long StartFrame, long EndFrame);
}
