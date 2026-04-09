using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewWarmupItemControllerTests
{
    [Fact]
    public async Task WarmItemAsync_MissingPreview_ClearsFrames()
    {
        var controller = new MainWindowPreviewWarmupItemController(
            new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2));
        var rom = CreateRom("contra", "/tmp/contra.nes");
        var seedPath = Path.Combine(Path.GetTempPath(), $"warmup-missing-seed-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? seededPreview = null;
        var clearCalls = 0;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                seedPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF112233u)]);
            seededPreview = StreamingPreview.Open(seedPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            rom.SetPreviewStream(seededPreview);

            await controller.WarmItemAsync(
                rom,
                CancellationToken.None,
                _ => new PreviewStreamLoadResult("/tmp/missing.fcpv", false, null, null, null),
                action =>
                {
                    action();
                    return Task.CompletedTask;
                },
                _ => false,
                _ => { },
                (_, _) => { },
                item =>
                {
                    clearCalls++;
                    item.ClearPreviewFrames();
                },
                (_, _, _, _, _) => { });

            Assert.Equal(1, clearCalls);
            Assert.False(rom.HasLoadedPreview);
        }
        finally
        {
            rom.ClearPreviewFrames();
            seededPreview?.Dispose();
            if (File.Exists(seedPath))
                File.Delete(seedPath);
        }
    }

    [Fact]
    public async Task WarmItemAsync_Success_AppliesPreview_UpdatesInterval_AndSkipsRedundantMetadataPersist()
    {
        var controller = new MainWindowPreviewWarmupItemController(
            new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2));
        var rom = CreateRom("mario", "/tmp/mario.nes");
        var previewPath = Path.Combine(Path.GetTempPath(), $"warmup-success-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? preview = null;
        var interval = TimeSpan.Zero;
        var applyCalls = 0;
        var persistCalls = 0;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                120,
                [
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF445566u),
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF778899u)
                ]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            var metadata = new PreviewStreamMetadata(preview.IsAnimated, preview.IntervalMs, preview.FrameCount);

            await controller.WarmItemAsync(
                rom,
                CancellationToken.None,
                _ => new PreviewStreamLoadResult(previewPath, true, preview, ".fcpv", metadata),
                action =>
                {
                    action();
                    return Task.CompletedTask;
                },
                _ => true,
                value => interval = value,
                (item, restartPlayback) =>
                {
                    applyCalls++;
                    Assert.True(restartPlayback);
                    Assert.Same(rom, item);
                },
                item => item.ClearPreviewFrames(),
                (_, _, _, _, _) => persistCalls++);

            Assert.True(rom.HasLoadedPreview);
            Assert.Equal(TimeSpan.FromMilliseconds(120), interval);
            Assert.Equal(1, applyCalls);
            Assert.Equal(0, persistCalls);
            Assert.Equal(previewPath, rom.PreviewFilePath);
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
