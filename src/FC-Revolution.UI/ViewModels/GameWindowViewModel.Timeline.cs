using System;
using System.Threading.Tasks;
using FCRevolution.Core.Timeline.Persistence;
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
        GameWindowTimelinePersistenceController.PersistBranchPoint(
            _timelineRepository,
            _timelineManifest,
            _romId,
            branchPoint,
            parentBranchId,
            _romPath);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private void DeleteBranchPoint(Guid branchId)
    {
        GameWindowTimelinePersistenceController.DeleteBranchPoint(
            _timelineRepository,
            _timelineManifest,
            _romId,
            branchId);
        _currentBranchId = _timelineManifest.CurrentBranchId;
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private void RenameBranchPoint(CoreBranchPoint branchPoint)
    {
        GameWindowTimelinePersistenceController.RenameBranchPoint(
            _timelineRepository,
            _timelineManifest,
            branchPoint);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private void ActivateBranch(Guid branchId)
    {
        _currentBranchId = GameWindowTimelinePersistenceController.ActivateBranch(
            _timelineRepository,
            _timelineManifest,
            branchId);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private BranchPreviewNode? PersistPreviewNode(BranchCanvasNode node)
    {
        var persistResult = GameWindowTimelinePersistenceController.TryPersistPreviewNode(
            _timelineRepository,
            _timelineManifest,
            _romId,
            _currentBranchId,
            node,
            _sessionRuntime.GetNearestSnapshot,
            ScreenWidth,
            ScreenHeight);
        if (persistResult is null)
            return null;

        _timelineManifestWriteTimeUtc = persistResult.Value.ManifestWriteTimeUtc;
        return persistResult.Value.PreviewNode;
    }

    private void DeletePersistedPreviewNode(Guid previewNodeId)
    {
        _timelineManifestWriteTimeUtc = GameWindowTimelinePersistenceController.DeletePreviewNode(
            _timelineRepository,
            _timelineManifest,
            _romId,
            previewNodeId);
    }

    private void RenamePersistedPreviewNode(Guid previewNodeId, string title)
    {
        _timelineManifestWriteTimeUtc = GameWindowTimelinePersistenceController.RenamePreviewNode(
            _timelineRepository,
            _timelineManifest,
            _romId,
            previewNodeId,
            title);
    }

    private void MaybeSyncTimelineState()
    {
        var reloadState = GameWindowTimelinePersistenceController.TryReloadTimelineState(
            _timelineRepository,
            _branchTree,
            _timelineManifestWriteTimeUtc,
            _romId,
            DisplayName,
            _romPath,
            ScreenWidth,
            ScreenHeight);
        if (reloadState is null)
            return;

        _timelineManifest = reloadState.Value.Manifest;
        _currentBranchId = reloadState.Value.CurrentBranchId;
        BranchGallery.ReplacePreviewNodes(reloadState.Value.PreviewNodes);
        _timelineManifestWriteTimeUtc = reloadState.Value.ManifestWriteTimeUtc;
    }

    private DateTime GetTimelineManifestWriteTimeUtc()
    {
        return GameWindowTimelinePersistenceController.ReadManifestWriteTimeUtc(_romId);
    }
}
