using System;
using System.IO;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewTickControllerTests
{
    [Fact]
    public void OnPreviewTick_SyncsAnimatedTargets_AndMarksCurrentBitmapSync()
    {
        var controller = new MainWindowPreviewTickController();
        var rom = CreateRom("contra", "/tmp/contra.nes");
        var previewPath = Path.Combine(Path.GetTempPath(), $"tick-preview-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? preview = null;
        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF102030u),
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF405060u)
                ]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(preview);

            var result = controller.OnPreviewTick(
                elapsedMilliseconds: 120,
                currentTickCounter: 0,
                showDebugStatus: false,
                isPreviewTimerEnabled: true,
                currentRom: rom,
                previewAnimationTargets: [rom],
                shouldShowLiveGameOnDisc: false);

            Assert.Equal(1, result.NextTickCounter);
            Assert.True(result.ShouldSyncCurrentPreviewBitmap);
            Assert.NotNull(rom.CurrentPreviewBitmap);
        }
        finally
        {
            rom.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void OnPreviewTick_BuildsDebugTextForEnabledAndDisabledDebugMode()
    {
        var controller = new MainWindowPreviewTickController();
        var loadedRom = CreateRom("mario", "/tmp/mario.nes");
        var previewPath = Path.Combine(Path.GetTempPath(), $"tick-debug-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? preview = null;
        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                90,
                [
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF001122u),
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF334455u)
                ]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            loadedRom.SetPreviewStream(preview);

            var debugOn = controller.OnPreviewTick(
                elapsedMilliseconds: 95,
                currentTickCounter: 4,
                showDebugStatus: true,
                isPreviewTimerEnabled: true,
                currentRom: loadedRom,
                previewAnimationTargets: [loadedRom],
                shouldShowLiveGameOnDisc: true);
            Assert.NotNull(debugOn.DebugText);
            Assert.Contains("预览调试 tick=5", debugOn.DebugText, StringComparison.Ordinal);
            Assert.Contains("target=", debugOn.DebugText, StringComparison.Ordinal);

            var debugOff = controller.OnPreviewTick(
                elapsedMilliseconds: 120,
                currentTickCounter: 5,
                showDebugStatus: false,
                isPreviewTimerEnabled: true,
                currentRom: loadedRom,
                previewAnimationTargets: [loadedRom],
                shouldShowLiveGameOnDisc: true);
            Assert.Null(debugOff.DebugText);
        }
        finally
        {
            loadedRom.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void OnPreviewTick_DiscRefreshConditionDependsOnBitmapAndLiveDiscMode()
    {
        var controller = new MainWindowPreviewTickController();
        var rom = CreateRom("zelda", "/tmp/zelda.nes");
        var previewPath = Path.Combine(Path.GetTempPath(), $"tick-disc-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? preview = null;
        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF778899u)]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(preview);

            var refreshDisc = controller.OnPreviewTick(
                elapsedMilliseconds: 0,
                currentTickCounter: 0,
                showDebugStatus: false,
                isPreviewTimerEnabled: true,
                currentRom: rom,
                previewAnimationTargets: [rom],
                shouldShowLiveGameOnDisc: false);
            Assert.True(refreshDisc.ShouldRefreshDiscBitmap);

            var noRefresh = controller.OnPreviewTick(
                elapsedMilliseconds: 0,
                currentTickCounter: 1,
                showDebugStatus: false,
                isPreviewTimerEnabled: true,
                currentRom: rom,
                previewAnimationTargets: [rom],
                shouldShowLiveGameOnDisc: true);
            Assert.False(noRefresh.ShouldRefreshDiscBitmap);
        }
        finally
        {
            rom.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
