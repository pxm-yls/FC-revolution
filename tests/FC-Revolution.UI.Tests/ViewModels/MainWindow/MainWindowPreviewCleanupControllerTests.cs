using System;
using System.IO;
using System.Threading;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewCleanupControllerTests
{
    [Fact]
    public void ClearPreviewFrames_ClearsLoadedPreviewAndCanPreservePreviewAvailability()
    {
        var controller = new MainWindowPreviewCleanupController();
        var rom = CreateRom("contra", "/tmp/contra.nes", hasPreview: true);
        var previewPath = Path.Combine(Path.GetTempPath(), $"cleanup-preview-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? preview = null;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF102030u)]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(preview);

            controller.ClearPreviewFrames([rom], clearPreviewAvailability: false);

            Assert.False(rom.HasLoadedPreview);
            Assert.True(rom.HasPreview);

            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(preview);

            controller.ClearPreviewFrames([rom], clearPreviewAvailability: true);

            Assert.False(rom.HasLoadedPreview);
            Assert.False(rom.HasPreview);
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
    public void ReleasePreviewRuntime_CancelsWarmup_StopsPlayback_ClearsBitmap_AndReleasesLoadedPreviews()
    {
        var controller = new MainWindowPreviewCleanupController();
        var rom = CreateRom("mario", "/tmp/mario.nes", hasPreview: true);
        var previewPath = Path.Combine(Path.GetTempPath(), $"runtime-preview-{Guid.NewGuid():N}.fcpv");
        var warmupCts = new CancellationTokenSource();
        StreamingPreview? preview = null;
        var releaseSmoothPlaybackCalls = 0;
        var stopTimerCalls = 0;
        var clearBitmapCalls = 0;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF405060u)]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(preview);

            controller.ReleasePreviewRuntime(
                warmupCts,
                [rom],
                () => releaseSmoothPlaybackCalls++,
                () => stopTimerCalls++,
                () => clearBitmapCalls++);

            Assert.True(warmupCts.IsCancellationRequested);
            Assert.Equal(1, releaseSmoothPlaybackCalls);
            Assert.Equal(1, stopTimerCalls);
            Assert.Equal(1, clearBitmapCalls);
            Assert.False(rom.HasLoadedPreview);
            Assert.True(rom.HasPreview);
        }
        finally
        {
            rom.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    private static RomLibraryItem CreateRom(string name, string path, bool hasPreview) =>
        new($"{name}.nes", path, "", hasPreview, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
