using System;
using System.Collections.Generic;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryPreviewNodeWorkflowController
{
    private readonly Func<BranchCanvasNode, BranchPreviewNode?> _createPreviewNode;
    private readonly Action<Guid> _deletePersistedPreviewNode;
    private readonly Action<Guid, string> _renamePersistedPreviewNode;
    private readonly IList<BranchPreviewNode> _previewNodes;
    private readonly Action<BranchPreviewNode?> _selectPreviewNode;
    private readonly Action<string> _updateStatus;
    private readonly Action _rebuildCanvas;

    public BranchGalleryPreviewNodeWorkflowController(
        Func<BranchCanvasNode, BranchPreviewNode?> createPreviewNode,
        Action<Guid> deletePersistedPreviewNode,
        Action<Guid, string> renamePersistedPreviewNode,
        IList<BranchPreviewNode> previewNodes,
        Action<BranchPreviewNode?> selectPreviewNode,
        Action<string> updateStatus,
        Action rebuildCanvas)
    {
        ArgumentNullException.ThrowIfNull(createPreviewNode);
        ArgumentNullException.ThrowIfNull(deletePersistedPreviewNode);
        ArgumentNullException.ThrowIfNull(renamePersistedPreviewNode);
        ArgumentNullException.ThrowIfNull(previewNodes);
        ArgumentNullException.ThrowIfNull(selectPreviewNode);
        ArgumentNullException.ThrowIfNull(updateStatus);
        ArgumentNullException.ThrowIfNull(rebuildCanvas);

        _createPreviewNode = createPreviewNode;
        _deletePersistedPreviewNode = deletePersistedPreviewNode;
        _renamePersistedPreviewNode = renamePersistedPreviewNode;
        _previewNodes = previewNodes;
        _selectPreviewNode = selectPreviewNode;
        _updateStatus = updateStatus;
        _rebuildCanvas = rebuildCanvas;
    }

    public void CreatePreviewNode(BranchCanvasNode? node)
    {
        if (node == null)
            return;

        var previewNode = _createPreviewNode(node);
        if (previewNode == null)
        {
            _updateStatus("无法为该节点生成预览图");
            return;
        }

        RemovePreviewNodeById(previewNode.Id);
        _previewNodes.Add(previewNode);
        _selectPreviewNode(previewNode);
        _updateStatus($"已创建画面节点：帧 {node.Frame}");
        _rebuildCanvas();
    }

    public void DeletePreviewNode(Guid previewNodeId)
    {
        _deletePersistedPreviewNode(previewNodeId);
        var removed = RemovePreviewNodeById(previewNodeId);
        if (removed == 0)
            return;

        _selectPreviewNode(null);
        _updateStatus("已删除画面节点");
        _rebuildCanvas();
    }

    public void RenamePreviewNode(BranchPreviewNode? selectedPreviewNode)
    {
        if (selectedPreviewNode == null)
            return;

        var title = string.IsNullOrWhiteSpace(selectedPreviewNode.Title)
            ? $"画面节点 {selectedPreviewNode.Frame}"
            : selectedPreviewNode.Title.Trim();
        selectedPreviewNode.Title = title;
        _renamePersistedPreviewNode(selectedPreviewNode.Id, title);
        _updateStatus($"已重命名画面节点为「{title}」");
        _rebuildCanvas();
    }

    private int RemovePreviewNodeById(Guid previewNodeId)
    {
        var removed = 0;
        for (var i = _previewNodes.Count - 1; i >= 0; i--)
        {
            if (_previewNodes[i].Id != previewNodeId)
                continue;

            _previewNodes.RemoveAt(i);
            removed++;
        }

        return removed;
    }
}
