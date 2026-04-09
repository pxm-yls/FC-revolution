using System;
using System.Threading.Tasks;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel
{
    private async Task PlayRewindAsync(int seconds)
    {
        var timelinePosition = _sessionRuntime.CaptureTimelinePosition();
        var startDecision = GameWindowRewindPlaybackController.BuildStartDecision(
            _isRewinding,
            seconds,
            timelinePosition.CurrentFrame,
            _sessionRuntime.SnapshotInterval,
            _sessionRuntime.GetNearestSnapshot,
            ExpandThumbnail);
        if (!startDecision.ShouldStart)
        {
            if (startDecision.ShouldShowToastOnly)
                ShowToast(startDecision.ToastMessage!);
            else if (!string.IsNullOrWhiteSpace(startDecision.StatusText))
                UpdateStatus(startDecision.StatusText, startDecision.ToastMessage);
            return;
        }

        _isRewinding = true;
        ShowToast(startDecision.ToastMessage!);
        try
        {
            _sessionRuntime.PauseRecording();

            foreach (var previewFrame in startDecision.PreviewFrames)
            {
                _framePresenter.SetPendingPreviewFrame(previewFrame);
                await Task.Delay(GameWindowRewindPlaybackController.PreviewFrameDelayMilliseconds);
            }

            var landed = _sessionRuntime.SeekToFrame(startDecision.TargetFrame);
            if (startDecision.FinalPreviewFrame != null)
                _framePresenter.SetPendingPreviewFrame(startDecision.FinalPreviewFrame);

            var completionDecision = GameWindowRewindPlaybackController.BuildCompletionDecision(startDecision.Seconds, landed);
            if (completionDecision.ShouldRequestTimelineJump)
                RequestTemporalHistoryReset(MacMetalTemporalResetReason.TimelineJump);

            UpdateStatus(completionDecision.StatusText, completionDecision.ToastMessage);
        }
        catch (Exception ex)
        {
            var failureDecision = GameWindowRewindPlaybackController.BuildFailureDecision(ex.Message);
            UpdateStatus(failureDecision.StatusText, failureDecision.ToastMessage);
        }
        finally
        {
            _sessionRuntime.ResumeRecording();

            _isRewinding = false;
            RefreshBranchGallery(force: true);
        }
    }

    private void ToggleBranchGallery()
    {
        IsBranchGalleryVisible = !IsBranchGalleryVisible;
        if (IsBranchGalleryVisible)
        {
            RefreshBranchGallery(force: true);
            ShowToast("时间线已显示在底部");
        }
        else
        {
            ShowToast("时间线已隐藏");
        }
    }

    private void RefreshBranchGallery(bool force = false)
    {
        var frameBuffer = _framePresenter.LastPresentedFrame;
        if (frameBuffer != null)
            BranchGallery.SetLastFrame(frameBuffer);

        var timelinePosition = _sessionRuntime.CaptureTimelinePosition();

        var viewState = GameWindowTimelineStateController.BuildRefreshViewState(
            force,
            IsBranchGalleryVisible,
            timelinePosition.CurrentFrame,
            timelinePosition.NewestFrame,
            timelinePosition.TimestampSeconds);
        if (viewState.ShouldRefreshAll)
            BranchGallery.RefreshAll();

        BranchGallery.SetCurrentPosition(viewState.Cursor.CurrentFrame, viewState.Cursor.TimestampSeconds);
        TimelinePositionText = viewState.TimelinePositionText;
        TimelineHintText = viewState.TimelineHintText;
    }

    private static uint[] ExpandThumbnail(uint[] thumbnail)
    {
        if (thumbnail.Length == 0)
            return new uint[ScreenWidth * ScreenHeight];

        const int thumbWidth = 64;
        const int thumbHeight = 60;
        var frame = new uint[ScreenWidth * ScreenHeight];
        for (var y = 0; y < ScreenHeight; y++)
        {
            var sourceY = Math.Min(thumbHeight - 1, y / 4);
            for (var x = 0; x < ScreenWidth; x++)
            {
                var sourceX = Math.Min(thumbWidth - 1, x / 4);
                frame[y * ScreenWidth + x] = thumbnail[sourceY * thumbWidth + sourceX];
            }
        }

        return frame;
    }

    private void PersistBranchPoint(CoreBranchPoint branchPoint, Guid? parentBranchId)
    {
        _legacyTimeline.PersistBranchPoint(branchPoint, parentBranchId);
    }

    private void DeleteBranchPoint(Guid branchId)
    {
        _legacyTimeline.DeleteBranchPoint(branchId);
    }

    private void RenameBranchPoint(CoreBranchPoint branchPoint)
    {
        _legacyTimeline.RenameBranchPoint(branchPoint);
    }

    private void ActivateBranch(Guid branchId)
    {
        _legacyTimeline.ActivateBranch(branchId);
    }

    private BranchPreviewNode? PersistPreviewNode(BranchCanvasNode node)
    {
        return _legacyTimeline.PersistPreviewNode(
            node,
            _sessionRuntime.GetNearestSnapshot,
            ScreenWidth,
            ScreenHeight);
    }

    private void DeletePersistedPreviewNode(Guid previewNodeId)
    {
        _legacyTimeline.DeletePreviewNode(previewNodeId);
    }

    private void RenamePersistedPreviewNode(Guid previewNodeId, string title)
    {
        _legacyTimeline.RenamePreviewNode(previewNodeId, title);
    }

    private void MaybeSyncTimelineState()
    {
        var reloadState = _legacyTimeline.TryReload(
            ScreenWidth,
            ScreenHeight);
        if (reloadState is null)
            return;

        BranchGallery.ReplacePreviewNodes(reloadState.Value.PreviewNodes);
    }
}
