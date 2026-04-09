using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewAssetControllerTests
{
    private static readonly string[] SupportedVideoExtensions = [".mp4", ".mov", ".m4v", ".webm"];

    [Fact]
    public void ResolvePreviewPath_PrefersVideoOverLegacyRaw()
    {
        var controller = new MainWindowPreviewAssetController(SupportedVideoExtensions, ".fcpv");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var artifactBasePath = Path.Combine(tempRoot, "contra-preview");
            var mp4Path = $"{artifactBasePath}.mp4";
            var rawPath = $"{artifactBasePath}.fcpv";
            File.WriteAllBytes(rawPath, [0x01]);
            File.WriteAllBytes(mp4Path, [0x02]);

            var resolved = controller.ResolvePreviewPath(
                "contra.nes",
                null,
                [artifactBasePath],
                File.Exists,
                _ => mp4Path);

            Assert.Equal(mp4Path, resolved);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolvePreviewPlaybackPath_UsesVideoAndDeletesSiblingLegacyRaw()
    {
        var controller = new MainWindowPreviewAssetController(SupportedVideoExtensions, ".fcpv");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var artifactBasePath = Path.Combine(tempRoot, "contra-preview");
            var mp4Path = $"{artifactBasePath}.mp4";
            var rawPath = $"{artifactBasePath}.fcpv";
            File.WriteAllBytes(mp4Path, [0x02]);
            File.WriteAllBytes(rawPath, [0x01]);

            var resolved = controller.ResolvePreviewPlaybackPath(
                "contra.nes",
                mp4Path,
                [artifactBasePath],
                File.Exists,
                _ => mp4Path,
                (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(mp4Path, resolved);
            Assert.False(File.Exists(rawPath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolvePreviewPlaybackPath_DoesNotFallbackToLegacyRaw_WhenOnlyLegacyExists()
    {
        var controller = new MainWindowPreviewAssetController(SupportedVideoExtensions, ".fcpv");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-preview-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var artifactBasePath = Path.Combine(tempRoot, "contra-preview");
            var mp4Path = $"{artifactBasePath}.mp4";
            var rawPath = $"{artifactBasePath}.fcpv";
            File.WriteAllBytes(rawPath, [0x01]);

            var resolved = controller.ResolvePreviewPlaybackPath(
                "contra.nes",
                rawPath,
                [artifactBasePath],
                File.Exists,
                _ => mp4Path,
                (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(mp4Path, resolved);
            Assert.True(File.Exists(rawPath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
