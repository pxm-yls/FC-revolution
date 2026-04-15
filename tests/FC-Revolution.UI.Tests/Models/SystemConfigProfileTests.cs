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
    public void ResolveCoreProbePaths_IncludesResourceRootDevelopmentCoreDirectory_AndConfiguredPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-core-probes-{Guid.NewGuid():N}");
        var expectedDefaultPath = AppObjectStorage.GetDevelopmentCoreModulesDirectory(tempRoot);
        var expectedCustomPath = Path.Combine(tempRoot, "external-cores");

        var resolved = SystemConfigProfile.ResolveCoreProbePaths(
            tempRoot,
            [" ", expectedCustomPath, expectedCustomPath]);

        Assert.Equal(
        [
            Path.GetFullPath(expectedDefaultPath),
            Path.GetFullPath(expectedCustomPath)
        ], resolved);
    }

    [Fact]
    public void SystemConfigProfile_SaveLoad_PreservesEmptyDefaultCoreId()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-config-empty-core-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                DefaultCoreId = string.Empty
            });

            var loaded = SystemConfigProfile.Load();

            Assert.Equal(string.Empty, loaded.DefaultCoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SystemConfigProfile_Load_MigratesLegacyAliases_WithoutWritingThemBack()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-config-legacy-{Guid.NewGuid():N}");
        var legacyProbePath = Path.Combine(tempRoot, "legacy-cores");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(SystemConfigProfile.GetProfilePath())!);
            File.WriteAllText(
                SystemConfigProfile.GetProfilePath(),
                $$"""
                {
                  "resourceRootPath": "{{tempRoot}}",
                  "managedCoreProbePaths": ["{{legacyProbePath}}"],
                  "playerInputOverrides": {
                    "p1": {
                      "a": "Z"
                    }
                  },
                  "extraInputBindings": [
                    {
                      "player": 7,
                      "kind": "Turbo",
                      "key": "Q",
                      "buttons": ["a"]
                    }
                  ]
                }
                """);

            var loaded = SystemConfigProfile.Load();

            Assert.Equal([Path.GetFullPath(legacyProbePath)], loaded.CoreProbePaths);
            Assert.Equal("Z", loaded.PortInputOverrides["p1"]["a"]);
            Assert.Equal(7, Assert.Single(loaded.ExtraInputBindings).LegacyPortOrdinal);

            var migratedJson = File.ReadAllText(SystemConfigProfile.GetProfilePath());
            Assert.DoesNotContain("\"managedCoreProbePaths\"", migratedJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"playerInputOverrides\"", migratedJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"player\"", migratedJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
