using FCRevolution.Storage;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowViewModelPreviewTests
{
    [Fact]
    public void ResolvePreviewPlaybackPath_PrefersVideoPreview_AndCleansLegacyRawPreview()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-root-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Contra.nes");
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var previewBasePath = AppObjectStorage.GetPreviewArtifactBasePath(romPath);
            var mp4Path = $"{previewBasePath}.mp4";
            var rawPath = $"{previewBasePath}.fcpv";
            File.WriteAllBytes(mp4Path, [0x00]);
            PreviewRawTestHelper.WriteRawPreview(
                rawPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u)]);

            File.SetLastWriteTimeUtc(mp4Path, DateTime.UtcNow.AddMinutes(-1));
            File.SetLastWriteTimeUtc(rawPath, DateTime.UtcNow);

            using var host = new MainWindowViewModelTestHost();
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var playbackPath = host.InvokeResolvePreviewPlaybackPath(romPath);

            Assert.Equal(mp4Path, playbackPath);
            Assert.False(File.Exists(rawPath));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolvePreviewPlaybackPath_DoesNotFallbackToLegacyRawPreviewWhenVideoIsMissing()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-root-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Contra.nes");
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var previewBasePath = AppObjectStorage.GetPreviewArtifactBasePath(romPath);
            var mp4Path = $"{previewBasePath}.mp4";
            var rawPath = $"{previewBasePath}.fcpv";
            PreviewRawTestHelper.WriteRawPreview(
                rawPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u)]);

            using var host = new MainWindowViewModelTestHost();
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var playbackPath = host.InvokeResolvePreviewPlaybackPath(romPath);

            Assert.Equal(mp4Path, playbackPath);
            Assert.True(File.Exists(rawPath));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolvePreviewPlaybackPath_KeepsNewerVideoPreviewWhenRawIsStale()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-root-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Contra.nes");
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var previewBasePath = AppObjectStorage.GetPreviewArtifactBasePath(romPath);
            var mp4Path = $"{previewBasePath}.mp4";
            var rawPath = $"{previewBasePath}.fcpv";
            File.WriteAllBytes(mp4Path, [0x00]);
            PreviewRawTestHelper.WriteRawPreview(
                rawPath,
                8,
                8,
                100,
                [PreviewRawTestHelper.CreateSolidFrame(8, 8, 0xFF000000u)]);

            File.SetLastWriteTimeUtc(rawPath, DateTime.UtcNow.AddMinutes(-1));
            File.SetLastWriteTimeUtc(mp4Path, DateTime.UtcNow);

            using var host = new MainWindowViewModelTestHost();
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var playbackPath = host.InvokeResolvePreviewPlaybackPath(romPath);

            Assert.Equal(mp4Path, playbackPath);
            Assert.False(File.Exists(rawPath));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
