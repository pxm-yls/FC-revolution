using System;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryTimelineNavigationExecutionController
{
    private readonly ITimeTravelService _timeTravelService;
    private readonly Action<Guid> _activateBranch;
    private readonly Action _notifyTimelineJump;
    private readonly Action _refreshAll;
    private readonly Action<string> _updateStatus;

    public BranchGalleryTimelineNavigationExecutionController(
        ITimeTravelService timeTravelService,
        Action<Guid> activateBranch,
        Action notifyTimelineJump,
        Action refreshAll,
        Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(timeTravelService);
        ArgumentNullException.ThrowIfNull(activateBranch);
        ArgumentNullException.ThrowIfNull(notifyTimelineJump);
        ArgumentNullException.ThrowIfNull(refreshAll);
        ArgumentNullException.ThrowIfNull(updateStatus);

        _timeTravelService = timeTravelService;
        _activateBranch = activateBranch;
        _notifyTimelineJump = notifyTimelineJump;
        _refreshAll = refreshAll;
        _updateStatus = updateStatus;
    }

    public void Execute(BranchGalleryTimelineNavigationDecision decision)
    {
        var result = decision.Action switch
        {
            BranchGalleryTimelineNavigationAction.LoadBranch => ExecuteLoadBranch(decision.BranchPoint),
            BranchGalleryTimelineNavigationAction.SeekFrame => BranchGalleryTimelineNavigationController.BuildSeekFrameResult(
                _timeTravelService.SeekToFrame(decision.TargetFrame)),
            BranchGalleryTimelineNavigationAction.RewindFrames => BranchGalleryTimelineNavigationController.BuildRewindResult(
                _timeTravelService.RewindFrames(decision.FrameCount)),
            _ => default
        };

        Apply(result);
    }

    private BranchGalleryTimelineNavigationResult ExecuteLoadBranch(CoreBranchPoint? branchPoint)
    {
        if (branchPoint == null)
            return default;

        _timeTravelService.RestoreSnapshot(branchPoint.Snapshot);
        _activateBranch(branchPoint.Id);
        return BranchGalleryTimelineNavigationController.BuildLoadBranchResult(branchPoint);
    }

    private void Apply(BranchGalleryTimelineNavigationResult result)
    {
        if (string.IsNullOrEmpty(result.StatusText) &&
            !result.ShouldNotifyTimelineJump &&
            !result.ShouldRefreshAll)
        {
            return;
        }

        if (result.ShouldNotifyTimelineJump)
            _notifyTimelineJump();

        _updateStatus(result.StatusText);

        if (result.ShouldRefreshAll)
            _refreshAll();
    }
}
