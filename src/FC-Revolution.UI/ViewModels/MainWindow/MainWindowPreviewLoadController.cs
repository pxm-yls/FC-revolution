using System;
using System.IO;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewLoadController
{
    private readonly MainWindowPreviewLifecycleController _previewLifecycleController;

    public MainWindowPreviewLoadController(MainWindowPreviewLifecycleController previewLifecycleController)
    {
        _previewLifecycleController = previewLifecycleController;
    }

    public void LoadPreviewForCurrentRom(
        RomLibraryItem? currentRom,
        bool isGeneratingPreview,
        Func<string, string> resolvePreviewPlaybackPath,
        Func<string, bool> fileExists,
        Action clearCurrentPreviewBitmap,
        Action stopPreviewPlayback,
        Action<string> setPreviewStatusText,
        Action<RomLibraryItem> tryLoadItemPreview,
        Action<RomLibraryItem> syncCurrentPreviewBitmap,
        Func<bool> hasAnyAnimatedPreview,
        Action startPreviewPlayback)
    {
        var hasCurrentRom = currentRom != null;
        var previewFileExists = false;
        if (currentRom != null)
        {
            currentRom.UpdatePreviewFilePath(resolvePreviewPlaybackPath(currentRom.Path));
            previewFileExists = fileExists(currentRom.PreviewFilePath);
        }

        var loadDecision = _previewLifecycleController.BuildLoadPreviewDecision(
            hasCurrentRom,
            previewFileExists,
            isGeneratingPreview,
            currentRom?.IsLegacyPreview ?? false);
        if (loadDecision.ShouldClearCurrentPreviewBitmap)
            clearCurrentPreviewBitmap();
        if (loadDecision.ShouldStopPlayback)
            stopPreviewPlayback();
        if (loadDecision.StatusText != null)
            setPreviewStatusText(loadDecision.StatusText);
        if (!loadDecision.ShouldAttemptLoadCurrentRomPreview || currentRom == null)
            return;

        try
        {
            if (!currentRom.HasLoadedPreview)
                tryLoadItemPreview(currentRom);

            syncCurrentPreviewBitmap(currentRom);
            if (_previewLifecycleController.ShouldStartPlaybackAfterLoad(hasAnyAnimatedPreview()))
                startPreviewPlayback();

            if (loadDecision.StatusText != null)
                setPreviewStatusText(loadDecision.StatusText);
        }
        catch (Exception ex)
        {
            clearCurrentPreviewBitmap();
            setPreviewStatusText($"预览文件损坏: {ex.Message}");
        }
    }

    public void TryLoadItemPreview(
        RomLibraryItem item,
        Func<RomLibraryItem, PreviewStreamLoadResult> loadPreviewStream,
        Action<string> setPreviewDebugText,
        Action<TimeSpan> setPreviewTimerInterval,
        Func<RomLibraryItem, bool> isCurrentRomItem,
        Action<RomLibraryItem, bool> applyLoadedPreviewPlaybackState,
        Action<string> setStatusText,
        Action<string, bool, int, int> queueSavePreviewMetadata)
    {
        var loadResult = loadPreviewStream(item);
        item.UpdatePreviewFilePath(loadResult.PlaybackPath);
        setPreviewDebugText($"[DEBUG] TryLoad: {item.DisplayName}\n路径: {item.PreviewFilePath}\n存在: {loadResult.FileExists}");

        if (!loadResult.FileExists)
        {
            item.ClearPreviewFrames();
            return;
        }

        try
        {
            var preview = loadResult.Preview!;
            var metadata = loadResult.Metadata!;
            setPreviewDebugText(
                $"[DEBUG] TryLoad: {item.DisplayName}\n路径: {item.PreviewFilePath}\n存在: {loadResult.FileExists}\n格式: {loadResult.FormatLabel ?? Path.GetExtension(item.PreviewFilePath)} 帧数={metadata.FrameCount} 间隔={metadata.IntervalMs}ms");
            item.SetPreviewStream(preview);
            if (preview.IntervalMs > 0)
                setPreviewTimerInterval(TimeSpan.FromMilliseconds(preview.IntervalMs));

            setPreviewDebugText(
                $"[DEBUG] TryLoad: {item.DisplayName}\n路径: {item.PreviewFilePath}\n存在: {loadResult.FileExists}\n格式: {loadResult.FormatLabel ?? Path.GetExtension(item.PreviewFilePath)} 帧数={metadata.FrameCount} 间隔={metadata.IntervalMs}ms\n加载成功 IsAnimated={preview.IsAnimated}");
            applyLoadedPreviewPlaybackState(item, isCurrentRomItem(item));
            queueSavePreviewMetadata(item.Path, metadata.IsAnimated, metadata.IntervalMs, metadata.FrameCount);
        }
        catch (Exception ex)
        {
            setPreviewDebugText($"[DEBUG] 预览加载异常: {item.DisplayName}\n路径: {item.PreviewFilePath}\n错误: {ex.Message}\n{ex.GetType().Name}");
            setStatusText($"预览加载失败: {ex.Message}");
            item.ClearPreviewFrames();
        }
    }

    public void StopPreviewPlayback(
        bool hasAnyAnimatedPreview,
        Action resetPlaybackCounter,
        Action restartPlaybackWatch,
        Action stopTimer,
        Action clearDebugText,
        Action startTimer)
    {
        var transition = _previewLifecycleController.BuildStopPlaybackDecision(hasAnyAnimatedPreview);
        ApplyPlaybackTransition(transition, resetPlaybackCounter, restartPlaybackWatch, stopTimer, clearDebugText, startTimer);
    }

    public void StartPreviewPlayback(
        bool isTimerEnabled,
        Action resetPlaybackCounter,
        Action restartPlaybackWatch,
        Action stopTimer,
        Action clearDebugText,
        Action startTimer)
    {
        var transition = _previewLifecycleController.BuildStartPlaybackDecision(isTimerEnabled);
        ApplyPlaybackTransition(transition, resetPlaybackCounter, restartPlaybackWatch, stopTimer, clearDebugText, startTimer);
    }

    public void RestartPreviewPlayback(
        bool isTimerEnabled,
        Action resetPlaybackCounter,
        Action restartPlaybackWatch,
        Action stopTimer,
        Action clearDebugText,
        Action startTimer)
    {
        var transition = _previewLifecycleController.BuildRestartPlaybackDecision(isTimerEnabled);
        ApplyPlaybackTransition(transition, resetPlaybackCounter, restartPlaybackWatch, stopTimer, clearDebugText, startTimer);
    }

    public void ApplyLoadedPreviewPlaybackState(
        RomLibraryItem item,
        bool isCurrentRomItem,
        bool restartPlayback,
        bool isPreviewTimerEnabled,
        long previewPlaybackElapsedMilliseconds,
        Action<RomLibraryItem> syncCurrentPreviewBitmap,
        Action restartPreviewPlayback)
    {
        var decision = _previewLifecycleController.BuildLoadedPreviewPlaybackDecision(
            item.IsPreviewAnimated,
            isCurrentRomItem,
            restartPlayback);
        if (!decision.ShouldSyncPreviewFrame)
        {
            if (decision.ShouldUpdateCurrentPreviewBitmap)
                syncCurrentPreviewBitmap(item);
            return;
        }

        var elapsedMilliseconds = isPreviewTimerEnabled ? previewPlaybackElapsedMilliseconds : 0;
        item.SyncPreviewFrame(elapsedMilliseconds);
        if (decision.ShouldUpdateCurrentPreviewBitmap)
            syncCurrentPreviewBitmap(item);

        if (decision.ShouldRestartPlayback)
            restartPreviewPlayback();
    }

    private static void ApplyPlaybackTransition(
        PreviewPlaybackTransitionDecision transition,
        Action resetPlaybackCounter,
        Action restartPlaybackWatch,
        Action stopTimer,
        Action clearDebugText,
        Action startTimer)
    {
        if (transition.ShouldResetPlaybackCounter)
            resetPlaybackCounter();
        if (transition.ShouldRestartWatch)
            restartPlaybackWatch();
        if (transition.ShouldStopTimer)
            stopTimer();
        if (transition.ShouldClearDebugText)
            clearDebugText();
        if (transition.ShouldStartTimer)
            startTimer();
    }
}
