using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGallerySelectionEntryController
{
    private readonly Action<BranchCanvasNode?> _selectNode;
    private readonly Action<BranchPreviewNode?> _selectPreviewNode;
    private readonly Func<Guid, BranchPreviewNode?> _findPreviewNode;
    private readonly Action<BranchGalleryTimelineNavigationDecision> _executeTimelineNavigation;

    public BranchGallerySelectionEntryController(
        Action<BranchCanvasNode?> selectNode,
        Action<BranchPreviewNode?> selectPreviewNode,
        Func<Guid, BranchPreviewNode?> findPreviewNode,
        Action<BranchGalleryTimelineNavigationDecision> executeTimelineNavigation)
    {
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(selectPreviewNode);
        ArgumentNullException.ThrowIfNull(findPreviewNode);
        ArgumentNullException.ThrowIfNull(executeTimelineNavigation);

        _selectNode = selectNode;
        _selectPreviewNode = selectPreviewNode;
        _findPreviewNode = findPreviewNode;
        _executeTimelineNavigation = executeTimelineNavigation;
    }

    public void SelectNode(BranchCanvasNode? node)
    {
        if (node == null)
            return;

        _selectPreviewNode(null);
        _selectNode(node);
    }

    public void SelectPreviewNode(Guid previewNodeId)
    {
        _selectNode(null);
        _selectPreviewNode(_findPreviewNode(previewNodeId));
    }

    public void SeekToNode(BranchCanvasNode? node)
    {
        var decision = BranchGalleryTimelineNavigationController.BuildSeekDecision(node);
        if (decision.SelectedNode != null)
            _selectNode(decision.SelectedNode);

        _executeTimelineNavigation(decision);
    }
}
