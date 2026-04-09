using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRewindPlaybackControllerTests
{
    [Fact]
    public void BuildStartDecision_WhenAlreadyRewinding_ReturnsToastOnlyNoOp()
    {
        var decision = GameWindowRewindPlaybackController.BuildStartDecision(
            isRewinding: true,
            seconds: 5,
            currentFrame: 300,
            snapshotInterval: 5,
            getNearestSnapshot: _ => throw new InvalidOperationException("should not be called"),
            expandThumbnail: _ => throw new InvalidOperationException("should not be called"));

        Assert.False(decision.ShouldStart);
        Assert.True(decision.ShouldShowToastOnly);
        Assert.Null(decision.StatusText);
        Assert.Equal("正在回溯中", decision.ToastMessage);
        Assert.Empty(decision.RewindFrames);
        Assert.Empty(decision.PreviewFrames);
        Assert.Null(decision.FinalPreviewFrame);
        Assert.Equal(300, decision.TargetFrame);
    }

    [Fact]
    public void BuildStartDecision_WhenNoSnapshotsExist_ReturnsUnavailableStatus()
    {
        var decision = GameWindowRewindPlaybackController.BuildStartDecision(
            isRewinding: false,
            seconds: 5,
            currentFrame: 300,
            snapshotInterval: 5,
            getNearestSnapshot: _ => null,
            expandThumbnail: _ => throw new InvalidOperationException("should not be called"));

        Assert.False(decision.ShouldStart);
        Assert.False(decision.ShouldShowToastOnly);
        Assert.Equal("无可用回溯快照", decision.StatusText);
        Assert.Equal("无可用回溯快照", decision.ToastMessage);
        Assert.Empty(decision.RewindFrames);
        Assert.Empty(decision.PreviewFrames);
        Assert.Null(decision.FinalPreviewFrame);
        Assert.Equal(0, decision.TargetFrame);
    }

    [Fact]
    public void BuildStartDecision_WhenSnapshotsExist_ComputesTargetFrameSequenceAndPreviewFrames()
    {
        var snapshots = new Dictionary<long, CoreTimelineSnapshot>
        {
            [300] = CreateSnapshot(300),
            [60] = CreateSnapshot(60),
            [0] = CreateSnapshot(0)
        };

        var decision = GameWindowRewindPlaybackController.BuildStartDecision(
            isRewinding: false,
            seconds: 5,
            currentFrame: 300,
            snapshotInterval: 5,
            getNearestSnapshot: frame => snapshots.TryGetValue(frame, out var snapshot) ? snapshot : null,
            expandThumbnail: thumbnail => [thumbnail.Length == 0 ? 0u : thumbnail[0] + 1000u]);

        Assert.True(decision.ShouldStart);
        Assert.False(decision.ShouldShowToastOnly);
        Assert.Null(decision.StatusText);
        Assert.Equal("开始回溯 5 秒", decision.ToastMessage);
        Assert.Equal(0, decision.TargetFrame);
        Assert.Equal([300L, 60L, 0L], decision.RewindFrames.Select(snapshot => snapshot.Frame));
        Assert.Equal(3, decision.PreviewFrames.Count);
        Assert.Equal(1300u, decision.PreviewFrames[0][0]);
        Assert.Equal(1060u, decision.PreviewFrames[1][0]);
        Assert.Equal(1000u, decision.FinalPreviewFrame![0]);
    }

    [Fact]
    public void BuildStartDecision_NormalizesNonPositiveSeconds()
    {
        var decision = GameWindowRewindPlaybackController.BuildStartDecision(
            isRewinding: false,
            seconds: 0,
            currentFrame: 120,
            snapshotInterval: 60,
            getNearestSnapshot: frame => frame == 60 ? CreateSnapshot(60) : null,
            expandThumbnail: thumbnail => [thumbnail.Length == 0 ? 0u : thumbnail[0]]);

        Assert.True(decision.ShouldStart);
        Assert.Equal(1, decision.Seconds);
        Assert.Equal(60, decision.TargetFrame);
        Assert.Equal("开始回溯 1 秒", decision.ToastMessage);
    }

    [Fact]
    public void BuildCompletionDecision_WhenSeekSucceeds_RequestsTimelineJump()
    {
        var decision = GameWindowRewindPlaybackController.BuildCompletionDecision(seconds: 5, landed: 42);

        Assert.True(decision.ShouldRequestTimelineJump);
        Assert.Equal("已回溯 5 秒至帧 42", decision.StatusText);
        Assert.Equal("已回溯到帧 42", decision.ToastMessage);
    }

    [Fact]
    public void BuildCompletionDecision_WhenSeekFails_ReturnsFailureToast()
    {
        var decision = GameWindowRewindPlaybackController.BuildCompletionDecision(seconds: 5, landed: -1);

        Assert.False(decision.ShouldRequestTimelineJump);
        Assert.Equal("无可用回溯快照", decision.StatusText);
        Assert.Equal("回溯失败", decision.ToastMessage);
    }

    [Fact]
    public void BuildCompletionDecision_NormalizesNonPositiveSeconds()
    {
        var decision = GameWindowRewindPlaybackController.BuildCompletionDecision(seconds: 0, landed: 12);

        Assert.True(decision.ShouldRequestTimelineJump);
        Assert.Equal("已回溯 1 秒至帧 12", decision.StatusText);
    }

    [Fact]
    public void BuildFailureDecision_ProjectsExceptionMessage()
    {
        var decision = GameWindowRewindPlaybackController.BuildFailureDecision("disk full");

        Assert.False(decision.ShouldRequestTimelineJump);
        Assert.Equal("回溯失败: disk full", decision.StatusText);
        Assert.Equal("回溯失败: disk full", decision.ToastMessage);
    }

    private static CoreTimelineSnapshot CreateSnapshot(long frame) =>
        new()
        {
            Frame = frame,
            TimestampSeconds = frame / 60.0,
            Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray(),
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };
}
