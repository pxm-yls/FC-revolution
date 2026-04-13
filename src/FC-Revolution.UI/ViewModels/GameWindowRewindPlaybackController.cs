using System;
using System.Collections.Generic;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRewindPlaybackStartDecision(
    bool ShouldStart,
    bool ShouldShowToastOnly,
    int Seconds,
    long TargetFrame,
    IReadOnlyList<CoreTimelineSnapshot> RewindFrames,
    IReadOnlyList<uint[]> PreviewFrames,
    uint[]? FinalPreviewFrame,
    string? StatusText,
    string? ToastMessage);

internal readonly record struct GameWindowRewindPlaybackCompletionDecision(
    bool ShouldRequestTimelineJump,
    string StatusText,
    string ToastMessage);

internal static class GameWindowRewindPlaybackController
{
    public const int PreviewFrameDelayMilliseconds = 24;

    public static GameWindowRewindPlaybackStartDecision BuildStartDecision(
        bool isRewinding,
        int seconds,
        long currentFrame,
        int snapshotInterval,
        Func<long, CoreTimelineSnapshot?> getNearestSnapshot,
        Func<uint[], uint[]> expandThumbnail)
    {
        if (isRewinding)
        {
            return new GameWindowRewindPlaybackStartDecision(
                ShouldStart: false,
                ShouldShowToastOnly: true,
                Seconds: 0,
                TargetFrame: currentFrame,
                RewindFrames: [],
                PreviewFrames: [],
                FinalPreviewFrame: null,
                StatusText: null,
                ToastMessage: "正在回溯中");
        }

        var normalizedSeconds = Math.Max(1, seconds);
        var targetFrame = Math.Max(0, currentFrame - normalizedSeconds * 60L);
        var rewindFrames = GameWindowRewindSequencePlanner.Build(
            currentFrame,
            targetFrame,
            snapshotInterval,
            getNearestSnapshot);
        if (rewindFrames.Count == 0)
        {
            return new GameWindowRewindPlaybackStartDecision(
                ShouldStart: false,
                ShouldShowToastOnly: false,
                Seconds: normalizedSeconds,
                TargetFrame: targetFrame,
                RewindFrames: rewindFrames,
                PreviewFrames: [],
                FinalPreviewFrame: null,
                StatusText: "无可用回溯快照",
                ToastMessage: "无可用回溯快照");
        }

        var previewFrames = new List<uint[]>(rewindFrames.Count);
        foreach (var snapshot in rewindFrames)
            previewFrames.Add(expandThumbnail(snapshot.Thumbnail));

        uint[]? finalPreviewFrame = null;
        var lastThumbnail = rewindFrames[^1].Thumbnail;
        if (lastThumbnail.Length > 0)
            finalPreviewFrame = expandThumbnail(lastThumbnail);

        return new GameWindowRewindPlaybackStartDecision(
            ShouldStart: true,
            ShouldShowToastOnly: false,
            Seconds: normalizedSeconds,
            TargetFrame: targetFrame,
            RewindFrames: rewindFrames,
            PreviewFrames: previewFrames,
            FinalPreviewFrame: finalPreviewFrame,
            StatusText: null,
            ToastMessage: $"开始回溯 {normalizedSeconds} 秒");
    }

    public static GameWindowRewindPlaybackCompletionDecision BuildCompletionDecision(int seconds, long landed)
    {
        var normalizedSeconds = Math.Max(1, seconds);
        return landed < 0
            ? new GameWindowRewindPlaybackCompletionDecision(
                ShouldRequestTimelineJump: false,
                StatusText: "无可用回溯快照",
                ToastMessage: "回溯失败")
            : new GameWindowRewindPlaybackCompletionDecision(
                ShouldRequestTimelineJump: true,
                StatusText: $"已回溯 {normalizedSeconds} 秒至帧 {landed}",
                ToastMessage: $"已回溯到帧 {landed}");
    }

    public static GameWindowRewindPlaybackCompletionDecision BuildFailureDecision(string exceptionMessage) =>
        new(
            ShouldRequestTimelineJump: false,
            StatusText: $"回溯失败: {exceptionMessage}",
            ToastMessage: $"回溯失败: {exceptionMessage}");
}
