using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace FC_Revolution.UI.ViewModels;

internal sealed record BranchGalleryNodeSelectionDecision(
    string? SelectedNodeId,
    IReadOnlyList<string> PropertyNamesToNotify);

internal sealed record BranchGalleryPreviewSelectionDecision(
    string? SelectedNodeId,
    Guid? SelectedPreviewNodeId,
    IReadOnlyList<string> PropertyNamesToNotify);

internal sealed class BranchGallerySelectionController
{
    private static readonly IReadOnlyList<string> NodeSelectionPropertyNames =
    [
        nameof(BranchGalleryViewModel.HasSelection),
        nameof(BranchGalleryViewModel.HasBranchSelection),
        nameof(BranchGalleryViewModel.CanMarkExportRange),
        nameof(BranchGalleryViewModel.SelectedTitle),
        nameof(BranchGalleryViewModel.SelectedSubtitle),
        nameof(BranchGalleryViewModel.SelectedMeta),
        nameof(BranchGalleryViewModel.SelectedBitmap),
        nameof(BranchGalleryViewModel.EditableBranchName)
    ];

    private static readonly IReadOnlyList<string> PreviewSelectionPropertyNames =
    [
        nameof(BranchGalleryViewModel.HasSelection),
        nameof(BranchGalleryViewModel.HasBranchSelection),
        nameof(BranchGalleryViewModel.CanMarkExportRange),
        nameof(BranchGalleryViewModel.HasPreviewSelection),
        nameof(BranchGalleryViewModel.SelectedTitle),
        nameof(BranchGalleryViewModel.SelectedSubtitle),
        nameof(BranchGalleryViewModel.SelectedMeta),
        nameof(BranchGalleryViewModel.SelectedBitmap),
        nameof(BranchGalleryViewModel.EditableBranchName),
        nameof(BranchGalleryViewModel.EditablePreviewTitle)
    ];

    public BranchGalleryNodeSelectionDecision? BuildNodeSelectionDecision(
        BranchCanvasNode? currentNode,
        BranchCanvasNode? nextNode)
    {
        return ReferenceEquals(currentNode, nextNode)
            ? null
            : new BranchGalleryNodeSelectionDecision(nextNode?.Id, NodeSelectionPropertyNames);
    }

    public BranchGalleryPreviewSelectionDecision? BuildPreviewSelectionDecision(
        BranchPreviewNode? currentPreviewNode,
        BranchPreviewNode? nextPreviewNode,
        string? currentSelectedNodeId)
    {
        return ReferenceEquals(currentPreviewNode, nextPreviewNode)
            ? null
            : new BranchGalleryPreviewSelectionDecision(
                nextPreviewNode != null ? null : currentSelectedNodeId,
                nextPreviewNode?.Id,
                PreviewSelectionPropertyNames);
    }

    public bool HasSelection(BranchCanvasNode? selectedNode, BranchPreviewNode? selectedPreviewNode) =>
        selectedNode != null || selectedPreviewNode != null;

    public bool HasBranchSelection(BranchCanvasNode? selectedNode) => selectedNode?.BranchPoint != null;

    public bool HasPreviewSelection(BranchPreviewNode? selectedPreviewNode) => selectedPreviewNode != null;

    public bool CanMarkExportRange(BranchCanvasNode? selectedNode) => selectedNode != null;

    public string BuildSelectedTitle(BranchCanvasNode? selectedNode, BranchPreviewNode? selectedPreviewNode) =>
        selectedPreviewNode?.Title ?? selectedNode?.Title ?? "未选择节点";

    public string BuildSelectedSubtitle(BranchCanvasNode? selectedNode, BranchPreviewNode? selectedPreviewNode)
    {
        return selectedPreviewNode != null
            ? $"画面节点 • {TimeSpan.FromSeconds(selectedPreviewNode.TimestampSeconds):mm\\:ss}"
            : selectedNode?.Subtitle ?? "点击节点查看详情";
    }

    public string BuildSelectedMeta(BranchCanvasNode? selectedNode, BranchPreviewNode? selectedPreviewNode)
    {
        return selectedNode == null
            ? selectedPreviewNode == null ? "" : $"帧 {selectedPreviewNode.Frame} • 画面节点"
            : $"帧 {selectedNode.Frame} • {selectedNode.CreatedAt:yyyy-MM-dd HH:mm:ss}";
    }

    public string BuildExportRangeLabel(BranchCanvasNode? exportStartNode, BranchCanvasNode? exportEndNode) =>
        BranchGalleryExportWorkflowController.BuildExportRangeLabel(exportStartNode, exportEndNode);

    public string GetEditableBranchName(BranchCanvasNode? selectedNode) =>
        selectedNode?.BranchPoint?.Name ?? "";

    public string GetEditablePreviewTitle(BranchPreviewNode? selectedPreviewNode) =>
        selectedPreviewNode?.Title ?? "";

    public WriteableBitmap? GetSelectedBitmap(BranchCanvasNode? selectedNode, BranchPreviewNode? selectedPreviewNode) =>
        selectedPreviewNode?.Bitmap ?? selectedNode?.Bitmap;
}
