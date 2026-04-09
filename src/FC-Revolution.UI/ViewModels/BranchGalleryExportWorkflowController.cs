using System;
using System.IO;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct BranchGalleryExportMarkerDecision(
    bool ShouldApply,
    BranchCanvasNode? SelectedNode,
    string? ExportStartNodeId,
    string? ExportEndNodeId,
    string? StatusText);

internal readonly record struct BranchGalleryExportRangeRequest(
    bool ShouldExport,
    BranchCanvasNode? StartNode,
    long StartFrame,
    long EndFrame,
    string? StatusText);

internal static class BranchGalleryExportWorkflowController
{
    public static BranchGalleryExportMarkerDecision BuildSetStartDecision(
        BranchCanvasNode? node,
        string? currentEndNodeId)
    {
        return node == null
            ? default
            : new BranchGalleryExportMarkerDecision(
                ShouldApply: true,
                SelectedNode: node,
                ExportStartNodeId: node.Id,
                ExportEndNodeId: currentEndNodeId,
                StatusText: $"已设置导出起点：帧 {node.Frame}");
    }

    public static BranchGalleryExportMarkerDecision BuildSetEndDecision(
        BranchCanvasNode? node,
        string? currentStartNodeId)
    {
        return node == null
            ? default
            : new BranchGalleryExportMarkerDecision(
                ShouldApply: true,
                SelectedNode: node,
                ExportStartNodeId: currentStartNodeId,
                ExportEndNodeId: node.Id,
                StatusText: $"已设置导出终点：帧 {node.Frame}");
    }

    public static string BuildExportRangeLabel(BranchCanvasNode? startNode, BranchCanvasNode? endNode)
    {
        if (startNode == null || endNode == null)
            return "尚未设置导出区间";

        return $"导出区间: {Math.Min(startNode.Frame, endNode.Frame)} - {Math.Max(startNode.Frame, endNode.Frame)}";
    }

    public static BranchGalleryExportRangeRequest BuildExportRequest(BranchCanvasNode? startNode, BranchCanvasNode? endNode)
    {
        if (startNode == null || endNode == null)
        {
            return new BranchGalleryExportRangeRequest(
                ShouldExport: false,
                StartNode: null,
                StartFrame: 0,
                EndFrame: 0,
                StatusText: "请先设置导出起点和终点");
        }

        return new BranchGalleryExportRangeRequest(
            ShouldExport: true,
            StartNode: startNode,
            StartFrame: Math.Min(startNode.Frame, endNode.Frame),
            EndFrame: Math.Max(startNode.Frame, endNode.Frame),
            StatusText: null);
    }

    public static string BuildExportSuccessStatus(string outputPath) =>
        $"已导出 MP4: {Path.GetFileName(outputPath)}";

    public static string BuildExportFailureStatus(string message) =>
        $"导出失败: {message}";
}
