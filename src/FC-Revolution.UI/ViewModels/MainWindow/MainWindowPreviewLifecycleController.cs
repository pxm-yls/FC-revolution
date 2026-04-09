namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewLoadLifecycleDecision(
    bool ShouldClearCurrentPreviewBitmap,
    bool ShouldStopPlayback,
    bool ShouldAttemptLoadCurrentRomPreview,
    string? StatusText);

internal sealed record PreviewPlaybackTransitionDecision(
    bool ShouldResetPlaybackCounter,
    bool ShouldRestartWatch,
    bool ShouldStartTimer,
    bool ShouldStopTimer,
    bool ShouldClearDebugText);

internal sealed record LoadedPreviewPlaybackDecision(
    bool ShouldSyncPreviewFrame,
    bool ShouldUpdateCurrentPreviewBitmap,
    bool ShouldRestartPlayback);

internal sealed class MainWindowPreviewLifecycleController
{
    public PreviewLoadLifecycleDecision BuildLoadPreviewDecision(
        bool hasCurrentRom,
        bool previewFileExists,
        bool isGeneratingPreview,
        bool isCurrentRomLegacyPreview)
    {
        if (!hasCurrentRom)
        {
            return new PreviewLoadLifecycleDecision(
                ShouldClearCurrentPreviewBitmap: true,
                ShouldStopPlayback: true,
                ShouldAttemptLoadCurrentRomPreview: false,
                StatusText: null);
        }

        if (!previewFileExists)
        {
            return new PreviewLoadLifecycleDecision(
                ShouldClearCurrentPreviewBitmap: true,
                ShouldStopPlayback: true,
                ShouldAttemptLoadCurrentRomPreview: false,
                StatusText: isGeneratingPreview ? null : "当前没有预览，可在设置中一键生成");
        }

        return new PreviewLoadLifecycleDecision(
            ShouldClearCurrentPreviewBitmap: false,
            ShouldStopPlayback: false,
            ShouldAttemptLoadCurrentRomPreview: true,
            StatusText: isGeneratingPreview
                ? null
                : isCurrentRomLegacyPreview
                    ? "已载入旧版预览封面，重新生成后可启用低内存动画"
                    : "自动预览已就绪");
    }

    public bool ShouldStartPlaybackAfterLoad(bool hasAnyAnimatedPreviewAfterLoad) => hasAnyAnimatedPreviewAfterLoad;

    public PreviewPlaybackTransitionDecision BuildStopPlaybackDecision(bool hasAnyAnimatedPreview)
    {
        return hasAnyAnimatedPreview
            ? new PreviewPlaybackTransitionDecision(false, false, false, false, false)
            : new PreviewPlaybackTransitionDecision(
                ShouldResetPlaybackCounter: true,
                ShouldRestartWatch: false,
                ShouldStartTimer: false,
                ShouldStopTimer: true,
                ShouldClearDebugText: true);
    }

    public PreviewPlaybackTransitionDecision BuildStartPlaybackDecision(bool isTimerEnabled)
    {
        if (isTimerEnabled)
            return new PreviewPlaybackTransitionDecision(false, false, false, false, false);

        return new PreviewPlaybackTransitionDecision(
            ShouldResetPlaybackCounter: true,
            ShouldRestartWatch: true,
            ShouldStartTimer: true,
            ShouldStopTimer: false,
            ShouldClearDebugText: false);
    }

    public PreviewPlaybackTransitionDecision BuildRestartPlaybackDecision(bool isTimerEnabled) =>
        new(
            ShouldResetPlaybackCounter: true,
            ShouldRestartWatch: true,
            ShouldStartTimer: !isTimerEnabled,
            ShouldStopTimer: false,
            ShouldClearDebugText: false);

    public LoadedPreviewPlaybackDecision BuildLoadedPreviewPlaybackDecision(bool isPreviewAnimated, bool isCurrentRomItem, bool shouldRestartPlayback)
    {
        if (!isPreviewAnimated)
        {
            return new LoadedPreviewPlaybackDecision(
                ShouldSyncPreviewFrame: false,
                ShouldUpdateCurrentPreviewBitmap: isCurrentRomItem,
                ShouldRestartPlayback: false);
        }

        return new LoadedPreviewPlaybackDecision(
            ShouldSyncPreviewFrame: true,
            ShouldUpdateCurrentPreviewBitmap: isCurrentRomItem,
            ShouldRestartPlayback: shouldRestartPlayback);
    }
}
