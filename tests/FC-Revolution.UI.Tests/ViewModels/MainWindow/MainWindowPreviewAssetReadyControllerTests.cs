using System;
using System.IO;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewAssetReadyControllerTests
{
    [Fact]
    public void ApplyPreviewAssetReady_UpdatesRomState_ClearsExistingFrames_AndInvokesReload()
    {
        var controller = new MainWindowPreviewAssetReadyController();
        var rom = CreateRom("contra", "/tmp/contra.nes");
        var seedPath = Path.Combine(Path.GetTempPath(), $"seed-preview-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? seededPreview = null;
        var tryLoadCalls = 0;

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

            var readyPath = "/tmp/contra-ready.mp4";
            controller.ApplyPreviewAssetReady(
                rom,
                readyPath,
                isCurrentRom: false,
                syncCurrentPreviewFrame: false,
                tryLoadItemPreview: item =>
                {
                    tryLoadCalls++;
                    Assert.Same(rom, item);
                    Assert.Equal(readyPath, item.PreviewFilePath);
                    Assert.True(item.HasPreview);
                    Assert.False(item.HasLoadedPreview);
                },
                refreshCurrentRomState: () => throw new InvalidOperationException("non-current ROM should not refresh current state"),
                syncCurrentPreviewBitmap: () => throw new InvalidOperationException("non-current ROM should not sync current bitmap"));

            Assert.Equal(1, tryLoadCalls);
            Assert.Equal(readyPath, rom.PreviewFilePath);
            Assert.True(rom.HasPreview);
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
    public void ApplyPreviewAssetReady_CurrentRom_RefreshesState_AndSyncsBitmapWhenRequested()
    {
        var controller = new MainWindowPreviewAssetReadyController();
        var rom = CreateRom("mario", "/tmp/mario.nes");
        var previewPath = Path.Combine(Path.GetTempPath(), $"ready-preview-{Guid.NewGuid():N}.fcpv");
        StreamingPreview? loadedPreview = null;
        var tryLoadCalls = 0;
        var refreshCalls = 0;
        var syncCalls = 0;

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

            controller.ApplyPreviewAssetReady(
                rom,
                previewPath,
                isCurrentRom: true,
                syncCurrentPreviewFrame: true,
                tryLoadItemPreview: item =>
                {
                    tryLoadCalls++;
                    loadedPreview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
                    item.SetPreviewStream(loadedPreview);
                },
                refreshCurrentRomState: () => refreshCalls++,
                syncCurrentPreviewBitmap: () =>
                {
                    syncCalls++;
                    Assert.NotNull(rom.CurrentPreviewBitmap);
                });

            Assert.Equal(1, tryLoadCalls);
            Assert.Equal(1, refreshCalls);
            Assert.Equal(1, syncCalls);
            Assert.Equal(previewPath, rom.PreviewFilePath);
            Assert.True(rom.HasPreview);
            Assert.True(rom.HasLoadedPreview);
            Assert.NotNull(rom.CurrentPreviewBitmap);
        }
        finally
        {
            rom.ClearPreviewFrames();
            loadedPreview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void ApplyPreviewAssetReady_NonCurrentRom_SkipsCurrentRomRefresh()
    {
        var controller = new MainWindowPreviewAssetReadyController();
        var rom = CreateRom("zelda", "/tmp/zelda.nes");
        var refreshCalls = 0;
        var syncCalls = 0;

        controller.ApplyPreviewAssetReady(
            rom,
            "/tmp/zelda-ready.mp4",
            isCurrentRom: false,
            syncCurrentPreviewFrame: true,
            tryLoadItemPreview: _ => { },
            refreshCurrentRomState: () => refreshCalls++,
            syncCurrentPreviewBitmap: () => syncCalls++);

        Assert.Equal(0, refreshCalls);
        Assert.Equal(0, syncCalls);
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: false, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
