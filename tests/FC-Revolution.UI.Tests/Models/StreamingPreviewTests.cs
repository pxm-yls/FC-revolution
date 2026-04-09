using Avalonia.Media.Imaging;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Models.Previews;
using System.Runtime.InteropServices;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class StreamingPreviewTests
{
    [Fact]
    public void RawPreview_CanAdvanceToDifferentFrame()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(64, 64, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(64, 64, 0xFFFFFFFFu);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 64, 64, 100, [frame0, frame1]);

            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);
            var first = preview.GetFrame(0);
            var firstBrightness = ReadAverageBrightness(first);
            var second = preview.GetFrame(1);
            var secondBrightness = ReadAverageBrightness(second);

            Assert.True(preview.IsAnimated);
            Assert.True(
                firstBrightness != secondBrightness,
                $"first={firstBrightness} second={secondBrightness} debug={preview.DebugInfo}");
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void Dispose_IsIdempotent_AndOperationsThrowAfterDispose()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}.fcpv");
        var frame = PreviewRawTestHelper.CreateSolidFrame(16, 16, 0xFF112233u);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(previewPath, 16, 16, 100, [frame, frame]);

            var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            preview.Dispose();
            preview.Dispose();

            Assert.Throws<ObjectDisposedException>(() => preview.GetFrame(0));
            Assert.Throws<ObjectDisposedException>(() => preview.AdvanceFrame());
            Assert.Throws<ObjectDisposedException>(() => preview.LoadAllBitmaps());
            Assert.Throws<ObjectDisposedException>(() => preview.EnableMemoryPlayback());
            Assert.Throws<ObjectDisposedException>(() => preview.DisableMemoryPlayback());
            Assert.Throws<ObjectDisposedException>(() => preview.ReleaseBitmapCache());
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void VideoPreloadWindowSeconds_IsClampedToSupportedRange()
    {
        var original = StreamingPreview.VideoPreloadWindowSeconds;
        try
        {
            StreamingPreview.VideoPreloadWindowSeconds = 0;
            Assert.Equal(1, StreamingPreview.VideoPreloadWindowSeconds);

            StreamingPreview.VideoPreloadWindowSeconds = 99;
            Assert.Equal(3, StreamingPreview.VideoPreloadWindowSeconds);
        }
        finally
        {
            StreamingPreview.VideoPreloadWindowSeconds = original;
        }
    }

    [Fact]
    public void RawPreview_CachePolicyCanDisableFullFrameCaching()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(512, 512, 0xFF010203u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(512, 512, 0xFF040506u);
        var original = PreviewCachingPolicy.MaxFullFrameCacheBytes;

        try
        {
            PreviewCachingPolicy.MaxFullFrameCacheBytes = 1024;
            PreviewRawTestHelper.WriteRawPreview(previewPath, 512, 512, 100, [frame0, frame1]);

            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            Assert.False(preview.SupportsFullFrameCaching);
            Assert.Single(preview.LoadAllBitmaps());

            preview.EnableMemoryPlayback();
            Assert.False(preview.IsMemoryBacked);
        }
        finally
        {
            PreviewCachingPolicy.MaxFullFrameCacheBytes = original;
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void Open_VideoExtension_DoesNotFallbackToRawPreviewPayload()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}");
        var rawPath = $"{basePath}.fcpv";
        var videoPath = $"{basePath}.mp4";
        var frame = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF334455u);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(rawPath, 8, 8, 100, [frame, frame]);
            File.Copy(rawPath, videoPath, overwrite: true);

            using var rawPreview = StreamingPreview.Open(
                rawPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);
            Assert.True(rawPreview.FrameCount >= 1);

            Assert.ThrowsAny<Exception>(() =>
            {
                using var _ = StreamingPreview.Open(
                    videoPath,
                    PreviewRawTestHelper.PreviewMagicV1,
                    PreviewRawTestHelper.PreviewMagicV2);
            });
        }
        finally
        {
            if (File.Exists(rawPath))
                File.Delete(rawPath);
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }
    }

    private static int ReadAverageBrightness(WriteableBitmap bitmap)
    {
        using var locked = bitmap.Lock();
        var pixels = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        Marshal.Copy(locked.Address, pixels, 0, pixels.Length);

        long total = 0;
        var pixelCount = bitmap.PixelSize.Width * bitmap.PixelSize.Height;
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 4;
            total += pixels[offset];
            total += pixels[offset + 1];
            total += pixels[offset + 2];
        }

        return (int)(total / Math.Max(1, pixelCount * 3));
    }
}
