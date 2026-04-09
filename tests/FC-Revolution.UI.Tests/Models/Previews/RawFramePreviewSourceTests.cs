using FC_Revolution.UI.Models.Previews;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class RawFramePreviewSourceTests
{
    [Fact]
    public void LoadAllBitmaps_AndReleaseBitmapCache_UpdateCachedBitmapCount()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"raw-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFFFFFFFFu);
        var frame2 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF00FF00u);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [frame0, frame1, frame2]);

            using var source = RawFramePreviewSource.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            Assert.Equal(0, source.CachedBitmapCount);

            var all = source.LoadAllBitmaps();
            Assert.Equal(3, all.Count);
            Assert.Equal(3, source.CachedBitmapCount);

            source.ReleaseBitmapCache();
            Assert.Equal(0, source.CachedBitmapCount);
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void MemoryPlaybackToggle_UpdatesMemoryCache_AndClearsPrefetchState()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"raw-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFFFFFFFFu);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [frame0, frame1]);

            using var source = RawFramePreviewSource.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            source.AdvanceFrame();
            SpinWait.SpinUntil(() => source.HasPrefetchedFrame, millisecondsTimeout: 2000);
            Assert.True(source.HasPrefetchedFrame);

            source.EnableMemoryPlayback();
            Assert.True(source.IsMemoryBacked);
            Assert.Equal(2, source.CachedFrameCount);
            Assert.False(source.HasPrefetchedFrame);

            source.AdvanceFrame();
            Assert.False(source.HasPrefetchedFrame);

            source.DisableMemoryPlayback();
            Assert.False(source.IsMemoryBacked);
            Assert.Equal(0, source.CachedFrameCount);
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void EnableMemoryPlayback_WithPendingPrefetch_DoesNotAllowLatePrefetchPublish()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"raw-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFFFFFFFFu);
        var frame2 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFF00FF00u);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                256,
                256,
                100,
                [frame0, frame1, frame2]);

            using var source = RawFramePreviewSource.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            source.AdvanceFrame();
            Assert.True(SpinWait.SpinUntil(
                () => GetPrivateFieldValue<Task>(source, "_prefetchTask") != null ||
                      GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts") != null ||
                      source.HasPrefetchedFrame,
                millisecondsTimeout: 2000));

            source.EnableMemoryPlayback();

            Assert.True(source.IsMemoryBacked);
            Assert.False(source.HasPrefetchedFrame);
            Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts"));

            Thread.Sleep(200);
            Assert.False(source.HasPrefetchedFrame);
            Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts"));
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void Dispose_WithPendingPrefetch_ClearsPrefetchLifecycleState()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), $"raw-preview-{Guid.NewGuid():N}.fcpv");
        var frame0 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFF000000u);
        var frame1 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFFFFFFFFu);
        var frame2 = PreviewRawTestHelper.CreateSolidFrame(256, 256, 0xFF00FF00u);

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                256,
                256,
                100,
                [frame0, frame1, frame2]);

            var source = RawFramePreviewSource.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            source.AdvanceFrame();
            Assert.True(SpinWait.SpinUntil(
                () => GetPrivateFieldValue<Task>(source, "_prefetchTask") != null ||
                      GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts") != null ||
                      source.HasPrefetchedFrame,
                millisecondsTimeout: 2000));

            source.Dispose();

            Assert.False(source.HasPrefetchedFrame);
            Assert.Null(GetPrivateFieldValue<Task>(source, "_prefetchTask"));
            Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts"));

            Thread.Sleep(200);
            Assert.False(source.HasPrefetchedFrame);
            Assert.Null(GetPrivateFieldValue<Task>(source, "_prefetchTask"));
            Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(source, "_prefetchCts"));
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    private static T? GetPrivateFieldValue<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(instance) as T;
    }
}
