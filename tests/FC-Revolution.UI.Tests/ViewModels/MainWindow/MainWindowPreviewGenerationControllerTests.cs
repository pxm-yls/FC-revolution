using System.IO.Compression;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewGenerationControllerTests
{
    private static MainWindowPreviewGenerationController CreateController() =>
        new(
            previewSourceWidth: 256,
            previewSourceHeight: 240,
            previewDurationSeconds: 60,
            previewPlaybackFps: 30,
            previewSourceFps: 60,
            previewAnimationFrameCount: 1800,
            previewCaptureStride: 2,
            previewFrameIntervalMs: 33,
            previewBuildTimeout: TimeSpan.FromSeconds(10),
            previewMagicV1: PreviewRawTestHelper.PreviewMagicV1,
            previewMagicV2: PreviewRawTestHelper.PreviewMagicV2,
            legacyPreviewExtension: ".fcpv");

    [Fact]
    public void GetPreviewOutputSize_ClampsByScaleAndSourceBounds()
    {
        var controller = CreateController();

        var min = controller.GetPreviewOutputSize(0.01);
        var half = controller.GetPreviewOutputSize(0.5);
        var max = controller.GetPreviewOutputSize(9.9);

        Assert.Equal((3, 2), min);
        Assert.Equal((128, 120), half);
        Assert.Equal((256, 240), max);
    }

    [Fact]
    public void WritePreviewFrame_DownscalesUsingNearestSampling()
    {
        var controller = CreateController();
        var source = new uint[256 * 240];
        source[0] = 0x11223344;
        source[1] = 0x55667788;
        source[256] = 0x99AABBCC;
        source[257] = 0xDDEEFF11;
        var destination = new byte[sizeof(uint)];
        var resizeBuffer = new uint[1];

        controller.WritePreviewFrame(source, 1, 1, resizeBuffer, destination);

        var pixel = BitConverter.ToUInt32(destination, 0);
        Assert.Equal(0x11223344u, pixel);
    }

    [Fact]
    public void UpgradeLegacyPreview_UsesLoadedFrames_AndCleansSourceAndSiblingLegacy()
    {
        var controller = CreateController();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"preview-generation-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var legacyPath = Path.Combine(tempRoot, "legacy.fcpv");
        var targetPath = Path.Combine(tempRoot, "migrated.mp4");
        var siblingLegacyPath = Path.Combine(tempRoot, "migrated.fcpv");
        PreviewFileData? captured = null;
        string? capturedTarget = null;

        try
        {
            WriteLegacyPreview(
                legacyPath,
                width: 4,
                height: 4,
                intervalMs: 100,
                frames:
                [
                    PreviewRawTestHelper.CreateSolidFrame(4, 4, 0xFF000000u),
                    PreviewRawTestHelper.CreateSolidFrame(4, 4, 0xFFFFFFFFu)
                ]);
            File.WriteAllBytes(siblingLegacyPath, [0x01]);

            controller.UpgradeLegacyPreview(
                legacyPath,
                targetPath,
                persistPreview: (path, preview) =>
                {
                    capturedTarget = path;
                    captured = preview;
                    File.WriteAllBytes(path, [0x02]);
                });

            Assert.False(File.Exists(legacyPath));
            Assert.False(File.Exists(siblingLegacyPath));
            Assert.True(File.Exists(targetPath));
            Assert.Equal(targetPath, capturedTarget);
            Assert.NotNull(captured);
            Assert.Equal(4, captured!.Width);
            Assert.Equal(4, captured.Height);
            Assert.Equal(2, captured.Frames.Count);
            Assert.Equal(0xFF000000u, captured.Frames[0][0]);
            Assert.Equal(0xFFFFFFFFu, captured.Frames[1][0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SavePreviewAsRawFrames_WritesV2FileThatStreamingPreviewCanOpen()
    {
        var controller = CreateController();
        var tempPath = Path.Combine(Path.GetTempPath(), $"preview-raw-v2-{Guid.NewGuid():N}.fcpv");

        try
        {
            var preview = new PreviewFileData(
                Width: 8,
                Height: 8,
                IntervalMs: 100,
                Frames:
                [
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u),
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFFFFFFFFu)
                ]);

            controller.SavePreviewAsRawFrames(tempPath, preview);

            using var opened = StreamingPreview.Open(tempPath, PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
            Assert.True(opened.IsAnimated);
            Assert.Equal(2, opened.FrameCount);
            Assert.Equal(8, opened.Width);
            Assert.Equal(8, opened.Height);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void WriteLegacyPreview(string path, int width, int height, int intervalMs, IReadOnlyList<uint[]> frames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        using var writer = new BinaryWriter(gzip);
        writer.Write(PreviewRawTestHelper.PreviewMagicV1);
        writer.Write(width);
        writer.Write(height);
        writer.Write(intervalMs);
        writer.Write(frames.Count);
        foreach (var frame in frames)
        {
            foreach (var pixel in frame)
                writer.Write(pixel);
        }
    }
}
