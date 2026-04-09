using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class FFmpegVideoPreviewSourceTests
{
    [Fact]
    public void VideoPreview_LoadAllBitmapsAndMemoryToggle_FollowStreamingSemantics()
    {
        var previewPath = CreateVideoPreviewFile(frameCount: 3, width: 16, height: 16, fps: 10);

        try
        {
            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            Assert.True(preview.IsAnimated);
            Assert.False(preview.SupportsFullFrameCaching);

            var all = preview.LoadAllBitmaps();
            Assert.Single(all);

            preview.EnableMemoryPlayback();
            preview.DisableMemoryPlayback();

            Assert.False(preview.IsMemoryBacked);
            Assert.Equal(0, preview.CachedFrameCount);
            Assert.False(preview.HasPrefetchedFrame);
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void VideoPreview_GetFrame_SchedulesPrefetch_AndAdvanceFrameReturnsBitmap()
    {
        var previewPath = CreateVideoPreviewFile(frameCount: 80, width: 16, height: 16, fps: 12);

        try
        {
            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            var frame0 = preview.GetFrame(0);
            Assert.NotNull(frame0);

            var preloadScheduled = SpinWait.SpinUntil(
                () => !preview.DebugInfo.Contains("preloadStart=-1", StringComparison.Ordinal),
                millisecondsTimeout: 3000);
            Assert.True(preloadScheduled);

            var advanced = preview.AdvanceFrame();
            Assert.NotNull(advanced);
            Assert.True(preview.CachedFrameCount > 0);
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void VideoPreview_WindowPolicyCapsCachedFrameCount()
    {
        var original = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        var previewPath = CreateVideoPreviewFile(frameCount: 20, width: 512, height: 512, fps: 12);

        try
        {
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 2048;
            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            _ = preview.GetFrame(0);

            var preloadScheduled = SpinWait.SpinUntil(
                () => preview.CachedFrameCount > 0,
                millisecondsTimeout: 3000);
            Assert.True(preloadScheduled);
            Assert.InRange(preview.CachedFrameCount, 1, 2);
        }
        finally
        {
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = original;
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void VideoPreview_Dispose_IsIdempotent_AndOperationsThrow()
    {
        var previewPath = CreateVideoPreviewFile(frameCount: 2, width: 12, height: 12, fps: 8);

        try
        {
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
    public void VideoPreview_Dispose_AfterPreloadScheduled_RemainsIdempotent()
    {
        var previewPath = CreateVideoPreviewFile(frameCount: 80, width: 16, height: 16, fps: 12);

        try
        {
            var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            _ = preview.GetFrame(0);
            var preloadScheduled = SpinWait.SpinUntil(
                () => !preview.DebugInfo.Contains("preloadStart=-1", StringComparison.Ordinal),
                millisecondsTimeout: 3000);
            Assert.True(preloadScheduled);

            preview.Dispose();
            preview.Dispose();

            Assert.Throws<ObjectDisposedException>(() => preview.GetFrame(0));
            Assert.Throws<ObjectDisposedException>(() => preview.AdvanceFrame());
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void VideoPreview_Dispose_WhileDecodeGateHeld_ReturnsPromptly_AndCleansDecoderAfterRelease()
    {
        var previewPath = CreateVideoPreviewFile(frameCount: 40, width: 32, height: 32, fps: 12);

        try
        {
            var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            var source = GetPrivateFieldValue(preview, "_source")
                ?? throw new InvalidOperationException("Unable to read StreamingPreview._source via reflection.");
            Assert.IsType<FFmpegVideoPreviewSource>(source);

            var decodeGate = (SemaphoreSlim?)GetPrivateFieldValue(source, "_decodeGate")
                ?? throw new InvalidOperationException("Unable to read FFmpegVideoPreviewSource._decodeGate via reflection.");
            var streamIndexBeforeDispose = Convert.ToInt32(
                GetPrivateFieldValue(source, "_streamIndex")
                ?? throw new InvalidOperationException("Unable to read FFmpegVideoPreviewSource._streamIndex via reflection."));
            Assert.True(streamIndexBeforeDispose >= 0);

            decodeGate.Wait();
            var disposeElapsed = Stopwatch.StartNew();
            preview.Dispose();
            disposeElapsed.Stop();

            Assert.True(disposeElapsed.Elapsed < TimeSpan.FromMilliseconds(500));
            Assert.True(Convert.ToInt32(GetPrivateFieldValue(source, "_streamIndex") ?? -1) >= 0);

            decodeGate.Release();

            var cleanupCompleted = SpinWait.SpinUntil(
                () => Convert.ToInt32(GetPrivateFieldValue(source, "_streamIndex") ?? 0) == -1,
                millisecondsTimeout: 3000);
            Assert.True(cleanupCompleted);
            Assert.Throws<ObjectDisposedException>(() => decodeGate.Wait(0));
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public async Task VideoPreview_ForegroundMiss_DoesNotHoldSyncRoot_WhileWaitingForDecodeLock()
    {
        var originalWindowBytes = VideoPreviewWindowPolicy.MaxWindowCacheBytes;
        var previewPath = CreateVideoPreviewFile(frameCount: 80, width: 128, height: 128, fps: 12);

        try
        {
            // Keep the window small so requesting a far frame is guaranteed to be a cache miss.
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = 256 * 1024;

            using var preview = StreamingPreview.Open(
                previewPath,
                PreviewRawTestHelper.PreviewMagicV1,
                PreviewRawTestHelper.PreviewMagicV2);

            var source = GetPrivateFieldValue(preview, "_source")
                ?? throw new InvalidOperationException("Unable to read StreamingPreview._source via reflection.");
            Assert.IsType<FFmpegVideoPreviewSource>(source);

            var sourceType = source.GetType();
            var syncRoot = GetPrivateFieldValue(source, "_syncRoot")
                ?? throw new InvalidOperationException("Unable to read FFmpegVideoPreviewSource._syncRoot via reflection.");
            var decodeGate = (SemaphoreSlim?)GetPrivateFieldValue(source, "_decodeGate")
                ?? throw new InvalidOperationException("Unable to read FFmpegVideoPreviewSource._decodeGate via reflection.");
            var decodeLimiter = (SemaphoreSlim?)GetPrivateStaticFieldValue(sourceType, "DecodeLimiter")
                ?? throw new InvalidOperationException("Unable to read FFmpegVideoPreviewSource.DecodeLimiter via reflection.");

            var requestedIndex = Math.Max(0, preview.FrameCount - 1);

            // Hold decode locks to force the foreground cache-miss path to wait on the decode gate.
            decodeLimiter.Wait();
            decodeGate.Wait();

            var foregroundTask = Task.Run(() => preview.GetFrame(requestedIndex));

            var requestedSet = SpinWait.SpinUntil(
                () => (int)(GetPrivateFieldValue(source, "_lastRequestedFrameIndex") ?? -1) == requestedIndex,
                millisecondsTimeout: 1000);
            Assert.True(requestedSet);

            // If the foreground miss path holds _syncRoot while waiting for decode locks, this will time out.
            var syncRootAvailable = Monitor.TryEnter(syncRoot, millisecondsTimeout: 250);
            if (syncRootAvailable)
                Monitor.Exit(syncRoot);

            decodeGate.Release();
            decodeLimiter.Release();

            await foregroundTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(syncRootAvailable);
        }
        finally
        {
            VideoPreviewWindowPolicy.MaxWindowCacheBytes = originalWindowBytes;
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    private static string CreateVideoPreviewFile(int frameCount, int width, int height, int fps)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("FFmpegVideoPreviewSource tests only support macOS and Windows runtimes.");
        }

        var ffmpegPath = ResolveBundledFfmpegPath();
        var previewPath = Path.Combine(Path.GetTempPath(), $"ffmpeg-preview-{Guid.NewGuid():N}.mp4");
        var frameBytes = BuildRgbaFrames(frameCount, width, height);
        var args = $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i pipe:0 -an -c:v mpeg4 -q:v 5 \"{previewPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start bundled ffmpeg.");
        process.StandardInput.BaseStream.Write(frameBytes, 0, frameBytes.Length);
        process.StandardInput.Close();

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || !File.Exists(previewPath) || new FileInfo(previewPath).Length == 0)
            throw new InvalidOperationException($"Failed to create preview video for tests. exit={process.ExitCode}, error={stderr}");

        return previewPath;
    }

    private static byte[] BuildRgbaFrames(int frameCount, int width, int height)
    {
        var framePixels = checked(width * height);
        var frameSize = checked(framePixels * sizeof(uint));
        var bytes = new byte[checked(frameSize * frameCount)];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var color = frame switch
            {
                0 => 0xFF0000FFu,
                1 => 0xFF00FF00u,
                _ => 0xFFFF0000u
            };

            var pixels = new uint[framePixels];
            Array.Fill(pixels, color);
            Buffer.BlockCopy(pixels, 0, bytes, frame * frameSize, frameSize);
        }

        return bytes;
    }

    private static string ResolveBundledFfmpegPath()
    {
        var toolRelativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine("src", "FC-Revolution.ffmpeg", "tools", "win-x64", "ffmpeg.exe")
            : Path.Combine("src", "FC-Revolution.ffmpeg", "tools", "osx-x64", "ffmpeg");

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, toolRelativePath);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Unable to locate bundled ffmpeg tool via relative path '{toolRelativePath}'.");
    }

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(instance);
    }

    private static object? GetPrivateStaticFieldValue(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        return field?.GetValue(null);
    }
}
