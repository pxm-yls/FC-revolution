using System;
using System.Linq;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryBranchWorkflowController
{
    private readonly ITimeTravelService _timeTravelService;
    private readonly CoreBranchTree _tree;
    private readonly Action<CoreBranchPoint, Guid?> _persistBranch;
    private readonly Action<Guid> _deleteBranch;
    private readonly Action<CoreBranchPoint> _renameBranch;
    private readonly Action<BranchCanvasNode?> _selectNode;
    private readonly Action<string?> _updateSelectedNodeId;
    private readonly Action _refreshAll;
    private readonly Action<string> _updateStatus;

    public BranchGalleryBranchWorkflowController(
        ITimeTravelService timeTravelService,
        CoreBranchTree tree,
        Action<CoreBranchPoint, Guid?>? persistBranch,
        Action<Guid>? deleteBranch,
        Action<CoreBranchPoint>? renameBranch,
        Action<BranchCanvasNode?> selectNode,
        Action<string?> updateSelectedNodeId,
        Action refreshAll,
        Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(timeTravelService);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(updateSelectedNodeId);
        ArgumentNullException.ThrowIfNull(refreshAll);
        ArgumentNullException.ThrowIfNull(updateStatus);

        _timeTravelService = timeTravelService;
        _tree = tree;
        _persistBranch = persistBranch ?? ((_, _) => { });
        _deleteBranch = deleteBranch ?? (_ => { });
        _renameBranch = renameBranch ?? (_ => { });
        _selectNode = selectNode;
        _updateSelectedNodeId = updateSelectedNodeId;
        _refreshAll = refreshAll;
        _updateStatus = updateStatus;
    }

    public void CreateBranch(uint[]? lastFrame, string? romPath, BranchCanvasNode? selectedNode)
    {
        if (lastFrame == null)
        {
            _updateStatus("无法创建分支：当前没有可用画面帧");
            return;
        }

        var branchPoint = _timeTravelService.CreateBranch($"分支 {_tree.AllBranches().Count() + 1}", lastFrame);
        branchPoint.RomPath = romPath ?? string.Empty;
        if (selectedNode?.BranchPoint is { } parentBranch)
            _tree.Fork(parentBranch.Id, branchPoint);
        else
            _tree.AddRoot(branchPoint);

        _persistBranch(branchPoint, selectedNode?.BranchPoint?.Id);
        _updateStatus($"已创建分支「{branchPoint.Name}」帧 {branchPoint.Frame}");
        _updateSelectedNodeId($"branch:{branchPoint.Id}");
        _refreshAll();
    }

    public void DeleteBranch(BranchCanvasNode? selectedNode)
    {
        if (selectedNode?.BranchPoint == null)
            return;

        var branchPoint = selectedNode.BranchPoint;
        var name = branchPoint.Name;
        _deleteBranch(branchPoint.Id);
        _tree.Remove(branchPoint.Id);
        _selectNode(null);
        _updateSelectedNodeId(null);
        _refreshAll();
        _updateStatus($"已删除分支「{name}」");
    }

    public void RenameBranch(BranchCanvasNode? selectedNode)
    {
        if (selectedNode?.BranchPoint == null)
            return;

        var branchPoint = selectedNode.BranchPoint;
        if (_tree.Find(branchPoint.Id) is { } legacyBranchPoint)
            legacyBranchPoint.Name = branchPoint.Name;

        _renameBranch(branchPoint);
        _refreshAll();
        _updateStatus($"已重命名为「{branchPoint.Name}」");
    }
}
