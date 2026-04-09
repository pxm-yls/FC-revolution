using System.IO.Compression;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewStreamControllerTests
{
    [Fact]
    public void LoadPreviewStream_ReturnsMissing_WhenPlaybackFileDoesNotExist()
    {
        var controller = new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-preview-{Guid.NewGuid():N}.fcpv");

        var result = controller.LoadPreviewStream(
            "/tmp/contra.nes",
            _ => missingPath,
            _ => missingPath,
            (_, _) => { });

        Assert.Equal(missingPath, result.PlaybackPath);
        Assert.False(result.FileExists);
        Assert.Null(result.Preview);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public void LoadPreviewStream_OpensNonLegacyPreview_AndReturnsMetadata()
    {
        var controller = new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
        var previewPath = Path.Combine(Path.GetTempPath(), $"preview-stream-{Guid.NewGuid():N}.fcpv");

        try
        {
            PreviewRawTestHelper.WriteRawPreview(
                previewPath,
                8,
                8,
                100,
                [
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u),
                    PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFFFFFFFFu)
                ]);

            var result = controller.LoadPreviewStream(
                "/tmp/contra.nes",
                _ => previewPath,
                _ => previewPath,
                (_, _) => throw new InvalidOperationException("should not upgrade"));

            Assert.True(result.FileExists);
            Assert.NotNull(result.Preview);
            Assert.NotNull(result.Metadata);
            Assert.Equal(previewPath, result.PlaybackPath);
            Assert.Equal(".fcpv", result.FormatLabel);
            Assert.True(result.Metadata!.IsAnimated);
            Assert.Equal(100, result.Metadata.IntervalMs);
            Assert.Equal(2, result.Metadata.FrameCount);
            result.Preview!.Dispose();
        }
        finally
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
    }

    [Fact]
    public void LoadPreviewStream_UpgradesLegacyPreview_AndReopensMigratedPath()
    {
        var controller = new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
        var legacyPath = Path.Combine(Path.GetTempPath(), $"preview-legacy-{Guid.NewGuid():N}.fcpv");
        var migratedPath = Path.Combine(Path.GetTempPath(), $"preview-migrated-{Guid.NewGuid():N}.fcpv");
        var upgradeCalls = 0;
        string? upgradedFrom = null;
        string? upgradedTo = null;

        try
        {
            WriteLegacyPreview(legacyPath, 8, 8, 100, [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF112233u)]);
            var result = controller.LoadPreviewStream(
                "/tmp/contra.nes",
                _ => legacyPath,
                _ => migratedPath,
                (from, to) =>
                {
                    upgradeCalls++;
                    upgradedFrom = from;
                    upgradedTo = to;
                    PreviewRawTestHelper.WriteRawPreview(
                        to,
                        8,
                        8,
                        100,
                        [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF445566u)],
                        PreviewRawTestHelper.PreviewMagicV2);
                });

            Assert.True(result.FileExists);
            Assert.Equal(1, upgradeCalls);
            Assert.Equal(legacyPath, upgradedFrom);
            Assert.Equal(migratedPath, upgradedTo);
            Assert.Equal(migratedPath, result.PlaybackPath);
            Assert.Equal(".fcpv", result.FormatLabel);
            Assert.NotNull(result.Metadata);
            Assert.Equal(1, result.Metadata!.FrameCount);
            result.Preview!.Dispose();
        }
        finally
        {
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
            if (File.Exists(migratedPath))
                File.Delete(migratedPath);
        }
    }

    [Fact]
    public void ShouldPersistMetadata_OnlyWhenKnownValuesDiffer()
    {
        var controller = new MainWindowPreviewStreamController(PreviewRawTestHelper.PreviewMagicV1, PreviewRawTestHelper.PreviewMagicV2);
        var metadata = new PreviewStreamMetadata(IsAnimated: true, IntervalMs: 100, FrameCount: 2);

        Assert.True(controller.ShouldPersistMetadata(null, 100, 2, metadata));
        Assert.False(controller.ShouldPersistMetadata(true, 100, 2, metadata));
        Assert.True(controller.ShouldPersistMetadata(false, 100, 2, metadata));
        Assert.True(controller.ShouldPersistMetadata(true, 200, 2, metadata));
        Assert.True(controller.ShouldPersistMetadata(true, 100, 5, metadata));
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
