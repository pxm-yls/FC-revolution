using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal enum BranchGalleryTimelineNavigationAction
{
    None,
    LoadBranch,
    SeekFrame,
    RewindFrames,
}

internal readonly record struct BranchGalleryTimelineNavigationDecision(
    BranchGalleryTimelineNavigationAction Action,
    BranchCanvasNode? SelectedNode,
    CoreBranchPoint? BranchPoint,
    long TargetFrame,
    int FrameCount);

internal readonly record struct BranchGalleryTimelineNavigationResult(
    bool ShouldNotifyTimelineJump,
    bool ShouldRefreshAll,
    string StatusText);

internal static class BranchGalleryTimelineNavigationController
{
    public static BranchGalleryTimelineNavigationDecision BuildLoadBranchDecision(BranchCanvasNode? selectedNode)
    {
        return selectedNode?.BranchPoint is { } branchPoint
            ? new BranchGalleryTimelineNavigationDecision(
                Action: BranchGalleryTimelineNavigationAction.LoadBranch,
                SelectedNode: selectedNode,
                BranchPoint: branchPoint,
                TargetFrame: 0,
                FrameCount: 0)
            : default;
    }

    public static BranchGalleryTimelineNavigationDecision BuildSeekDecision(BranchCanvasNode? node)
    {
        if (node == null)
            return default;

        return node.BranchPoint != null
            ? BuildLoadBranchDecision(node)
            : new BranchGalleryTimelineNavigationDecision(
                Action: BranchGalleryTimelineNavigationAction.SeekFrame,
                SelectedNode: node,
                BranchPoint: null,
                TargetFrame: node.Frame,
                FrameCount: 0);
    }

    public static BranchGalleryTimelineNavigationDecision BuildRewindDecision(string framesText)
    {
        return int.TryParse(framesText, out var frames)
            ? new BranchGalleryTimelineNavigationDecision(
                Action: BranchGalleryTimelineNavigationAction.RewindFrames,
                SelectedNode: null,
                BranchPoint: null,
                TargetFrame: 0,
                FrameCount: frames)
            : default;
    }

    public static BranchGalleryTimelineNavigationResult BuildLoadBranchResult(CoreBranchPoint branchPoint) =>
        new(
            ShouldNotifyTimelineJump: true,
            ShouldRefreshAll: false,
            StatusText: $"已载入节点「{branchPoint.Name}」帧 {branchPoint.Frame}");

    public static BranchGalleryTimelineNavigationResult BuildSeekFrameResult(long landed) =>
        new(
            ShouldNotifyTimelineJump: landed >= 0,
            ShouldRefreshAll: false,
            StatusText: landed < 0 ? "未找到可载入的主线快照" : $"已载入主线帧 {landed}");

    public static BranchGalleryTimelineNavigationResult BuildRewindResult(long landed) =>
        new(
            ShouldNotifyTimelineJump: landed >= 0,
            ShouldRefreshAll: true,
            StatusText: landed < 0 ? "无可用快照" : $"已回退至帧 {landed}");
}
