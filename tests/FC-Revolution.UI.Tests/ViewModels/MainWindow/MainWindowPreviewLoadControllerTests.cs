using System;
using System.IO;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewLoadControllerTests
{
    [Fact]
    public void LoadPreviewForCurrentRom_HandlesSuccessAndFailureWithStatusAndBitmapSync()
    {
        var controller = new MainWindowPreviewLoadController(new MainWindowPreviewLifecycleController());
        var rom = CreateRom("contra", "/tmp/contra.nes");
        var clearCalls = 0;
        var stopCalls = 0;
        var startCalls = 0;
        var syncCalls = 0;
        var tryLoadCalls = 0;
        string? status = null;

        controller.LoadPreviewForCurrentRom(
            rom,
            isGeneratingPreview: false,
            resolvePreviewPlaybackPath: _ => "/tmp/contra.fcpv",
            fileExists: _ => true,
            clearCurrentPreviewBitmap: () => clearCalls++,
            stopPreviewPlayback: () => stopCalls++,
            setPreviewStatusText: text => status = text,
            tryLoadItemPreview: _ => tryLoadCalls++,
            syncCurrentPreviewBitmap: _ => syncCalls++,
            hasAnyAnimatedPreview: () => true,
            startPreviewPlayback: () => startCalls++);

        Assert.Equal(0, clearCalls);
        Assert.Equal(0, stopCalls);
        Assert.Equal(1, tryLoadCalls);
        Assert.Equal(1, syncCalls);
        Assert.Equal(1, startCalls);
        Assert.Equal("自动预览已就绪", status);

        controller.LoadPreviewForCurrentRom(
            rom,
            isGeneratingPreview: false,
            resolvePreviewPlaybackPath: _ => "/tmp/contra.fcpv",
            fileExists: _ => true,
            clearCurrentPreviewBitmap: () => clearCalls++,
            stopPreviewPlayback: () => stopCalls++,
            setPreviewStatusText: text => status = text,
            tryLoadItemPreview: _ => throw new InvalidOperationException("boom"),
            syncCurrentPreviewBitmap: _ => syncCalls++,
            hasAnyAnimatedPreview: () => true,
            startPreviewPlayback: () => startCalls++);

        Assert.Equal(1, clearCalls);
        Assert.Equal("预览文件损坏: boom", status);
    }

    [Fact]
    public void TryLoadItemPreview_CoversMissingExceptionAndSuccess()
    {
        var controller = new MainWindowPreviewLoadController(new MainWindowPreviewLifecycleController());
        var rom = CreateRom("mario", "/tmp/mario.nes");
        string debugText = string.Empty;
        string? status = null;
        var intervalSet = TimeSpan.Zero;
        var applyCalls = 0;
        var metadataPersistCalls = 0;

        StreamingPreview? seededPreview = null;
        var seedPath = Path.Combine(Path.GetTempPath(), $"seed-preview-{Guid.NewGuid():N}.fcpv");
        var successPath = Path.Combine(Path.GetTempPath(), $"success-preview-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? successPreview = null;
        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                seedPath,
                8,
                8,
                90,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF001122u), PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF334455u)]);
            seededPreview = StreamingPreview.Open(seedPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(seededPreview);
            Assert.True(rom.HasLoadedPreview);

            controller.TryLoadItemPreview(
                rom,
                _ => new PreviewStreamLoadResult(seedPath, false, null, null, null),
                text => debugText = text,
                interval => intervalSet = interval,
                _ => true,
                (_, _) => applyCalls++,
                text => status = text,
                (_, _, _, _) => metadataPersistCalls++);

            Assert.False(rom.HasLoadedPreview);
            Assert.Contains("存在: False", debugText, StringComparison.Ordinal);

            controller.TryLoadItemPreview(
                rom,
                _ => new PreviewStreamLoadResult(successPath, true, null, null, null),
                text => debugText = text,
                interval => intervalSet = interval,
                _ => true,
                (_, _) => applyCalls++,
                text => status = text,
                (_, _, _, _) => metadataPersistCalls++);

            Assert.Contains("预览加载异常", debugText, StringComparison.Ordinal);
            Assert.Equal("预览加载失败: Object reference not set to an instance of an object.", status);

            PreviewRawTestHelper.WriteRawPreview(
                successPath,
                8,
                8,
                120,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF8899AAu), PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFFCCDDEEu)]);
            successPreview = StreamingPreview.Open(successPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            var metadata = new PreviewStreamMetadata(true, successPreview.IntervalMs, successPreview.FrameCount);

            controller.TryLoadItemPreview(
                rom,
                _ => new PreviewStreamLoadResult(successPath, true, successPreview, ".fcpv", metadata),
                text => debugText = text,
                interval => intervalSet = interval,
                _ => true,
                (_, _) => applyCalls++,
                text => status = text,
                (_, _, _, _) => metadataPersistCalls++);

            Assert.True(rom.HasLoadedPreview);
            Assert.Equal(TimeSpan.FromMilliseconds(120), intervalSet);
            Assert.Contains("加载成功 IsAnimated=True", debugText, StringComparison.Ordinal);
            Assert.Equal(1, applyCalls);
            Assert.Equal(1, metadataPersistCalls);
        }
        finally
        {
            rom.ClearPreviewFrames();
            seededPreview?.Dispose();
            successPreview?.Dispose();
            if (File.Exists(seedPath))
                File.Delete(seedPath);
            if (File.Exists(successPath))
                File.Delete(successPath);
        }
    }

    [Fact]
    public void StartStopRestartPlayback_AppliesExpectedTransitions()
    {
        var controller = new MainWindowPreviewLoadController(new MainWindowPreviewLifecycleController());
        var resetCalls = 0;
        var restartWatchCalls = 0;
        var stopTimerCalls = 0;
        var clearDebugCalls = 0;
        var startTimerCalls = 0;

        controller.StopPreviewPlayback(
            hasAnyAnimatedPreview: false,
            resetPlaybackCounter: () => resetCalls++,
            restartPlaybackWatch: () => restartWatchCalls++,
            stopTimer: () => stopTimerCalls++,
            clearDebugText: () => clearDebugCalls++,
            startTimer: () => startTimerCalls++);
        Assert.Equal(1, resetCalls);
        Assert.Equal(1, stopTimerCalls);
        Assert.Equal(1, clearDebugCalls);

        controller.StartPreviewPlayback(
            isTimerEnabled: false,
            resetPlaybackCounter: () => resetCalls++,
            restartPlaybackWatch: () => restartWatchCalls++,
            stopTimer: () => stopTimerCalls++,
            clearDebugText: () => clearDebugCalls++,
            startTimer: () => startTimerCalls++);
        Assert.Equal(2, resetCalls);
        Assert.Equal(1, restartWatchCalls);
        Assert.Equal(1, startTimerCalls);

        controller.RestartPreviewPlayback(
            isTimerEnabled: true,
            resetPlaybackCounter: () => resetCalls++,
            restartPlaybackWatch: () => restartWatchCalls++,
            stopTimer: () => stopTimerCalls++,
            clearDebugText: () => clearDebugCalls++,
            startTimer: () => startTimerCalls++);
        Assert.Equal(3, resetCalls);
        Assert.Equal(2, restartWatchCalls);
        Assert.Equal(1, startTimerCalls);
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
