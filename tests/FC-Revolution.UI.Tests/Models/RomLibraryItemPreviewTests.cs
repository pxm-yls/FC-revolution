using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class RomLibraryItemPreviewTests
{
    [Fact]
    public void SetPreviewStream_UpdatesPreviewMetadata_AndBitmapState()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"rom-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(12, 8, 0xFF102030u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(12, 8, 0xFF304050u);
        var item = CreateItem(previewPath);
        StreamingPreview? preview = null;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 12, 8, 120, [frame0, frame1]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);

            item.SetPreviewStream(preview);

            Assert.True(item.HasLoadedPreview);
            Assert.True(item.HasPreviewBitmap);
            Assert.NotNull(item.CurrentPreviewBitmap);
            Assert.True(item.IsPreviewAnimated);
            Assert.Equal(true, item.KnownPreviewIsAnimated);
            Assert.Equal(120, item.KnownPreviewIntervalMs);
            Assert.Equal(2, item.KnownPreviewFrameCount);
            Assert.Equal(2, item.PreviewFrameCount);
            Assert.True(item.SupportsFullFrameCaching);
            Assert.Same(preview.Bitmap, item.CurrentPreviewBitmap);
        }
        finally
        {
            item.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void SmoothPlayback_EnableAdvanceDisable_TogglesState_AndResetsToStreamBitmap()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"rom-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(10, 10, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(10, 10, 0xFFFFFFFFu);
        var frame2 = PreviewRawTestHelper.CreateSolidFrame(10, 10, 0xFF00FF00u);
        var item = CreateItem(previewPath);
        StreamingPreview? preview = null;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 10, 10, 100, [frame0, frame1, frame2]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            item.SetPreviewStream(preview);

            var initialBitmap = item.CurrentPreviewBitmap;
            item.EnableSmoothPlayback();

            Assert.True(item.IsSmoothPlaybackEnabled);
            Assert.Equal(3, item.CachedPreviewBitmapCount);
            Assert.NotNull(item.CurrentPreviewBitmap);
            Assert.NotSame(initialBitmap, item.CurrentPreviewBitmap);

            var smoothFrame0 = item.CurrentPreviewBitmap;
            item.AdvancePreviewFrame();
            Assert.NotSame(smoothFrame0, item.CurrentPreviewBitmap);

            item.DisableSmoothPlayback();
            Assert.False(item.IsSmoothPlaybackEnabled);
            Assert.Equal(0, item.CachedPreviewBitmapCount);
            Assert.Same(preview.Bitmap, item.CurrentPreviewBitmap);
        }
        finally
        {
            item.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void MemoryPlayback_EnableAndDisable_UpdatesMemoryState()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"rom-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF112233u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF445566u);
        var item = CreateItem(previewPath);
        StreamingPreview? preview = null;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 8, 8, 90, [frame0, frame1]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            item.SetPreviewStream(preview);

            Assert.False(item.IsMemoryPreview);
            Assert.Equal(0, item.CachedPreviewFrameCount);

            item.EnableMemoryPlayback();
            Assert.True(item.IsMemoryPreview);
            Assert.Equal(2, item.CachedPreviewFrameCount);

            item.DisableMemoryPlayback();
            Assert.False(item.IsMemoryPreview);
            Assert.Equal(0, item.CachedPreviewFrameCount);
        }
        finally
        {
            item.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void ClearPreviewFrames_ClearsBitmap_AndLoadedState()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"rom-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF778899u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF99AABBu);
        var item = CreateItem(previewPath);
        StreamingPreview? preview = null;

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 8, 8, 75, [frame0, frame1]);
            preview = StreamingPreview.Open(previewPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            item.SetPreviewStream(preview);
            item.EnableSmoothPlayback();
            item.EnableMemoryPlayback();

            item.ClearPreviewFrames();

            Assert.False(item.HasLoadedPreview);
            Assert.False(item.HasPreviewBitmap);
            Assert.True(item.NoPreviewBitmap);
            Assert.Null(item.CurrentPreviewBitmap);
            Assert.False(item.IsPreviewAnimated);
            Assert.False(item.IsMemoryPreview);
            Assert.False(item.IsSmoothPlaybackEnabled);
            Assert.Equal(0, item.PreviewFrameCount);
            Assert.Equal("preview=unloaded", item.PreviewDebugInfo);
        }
        finally
        {
            item.ClearPreviewFrames();
            preview?.Dispose();
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    private static RomLibraryItem CreateItem(string previewPath)
    {
        return new RomLibraryItem(
            name: "demo.nes",
            path: "/tmp/demo.nes",
            previewFilePath: previewPath,
            hasPreview: true,
            fileSizeBytes: 1024,
            importedAtUtc: DateTime.UtcNow);
    }
}
