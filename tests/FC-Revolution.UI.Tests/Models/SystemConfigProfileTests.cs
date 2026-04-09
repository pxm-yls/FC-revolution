using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class SystemConfigProfileTests
{
    [Fact]
    public void SystemConfigProfile_Load_DisablesLanArcadeByDefaultWhenConfigMissing()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-config-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);

            var profile = SystemConfigProfile.Load();

            Assert.False(profile.IsLanArcadeEnabled);
            Assert.True(profile.IsLanArcadeWebPadEnabled);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveManagedCoreProbePaths_IncludesResourceRootManagedCoreDirectory_AndConfiguredPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-core-probes-{Guid.NewGuid():N}");
        var expectedDefaultPath = AppObjectStorage.GetManagedCoreModulesDirectory(tempRoot);
        var expectedCustomPath = Path.Combine(tempRoot, "external-cores");

        var resolved = SystemConfigProfile.ResolveManagedCoreProbePaths(
            tempRoot,
            [" ", expectedCustomPath, expectedCustomPath]);

        Assert.Equal(
        [
            Path.GetFullPath(expectedDefaultPath),
            Path.GetFullPath(expectedCustomPath)
        ], resolved);
    }
}
