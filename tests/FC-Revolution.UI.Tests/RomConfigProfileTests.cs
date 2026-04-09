using System.Text.Json;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class RomConfigProfileTests
{
    [Fact]
    public void LoadValidated_RepairsPreviewVideoObjectKey_ToExistingPreviewAsset()
    {
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Super Mario.nes");
            Directory.CreateDirectory(Path.GetDirectoryName(romPath)!);
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var actualPreviewObjectKey = $"{Path.GetFileNameWithoutExtension(AppObjectStorage.GetRomObjectKey(romPath))}-extra.mp4";
            var actualPreviewPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, actualPreviewObjectKey);
            File.WriteAllBytes(actualPreviewPath, "preview"u8.ToArray());

            var staleProfile = new RomConfigProfile
            {
                Resources = new RomResourceManifest
                {
                    RomObjectKey = AppObjectStorage.GetRomObjectKey(romPath),
                    PreviewVideoObjectKey = AppObjectStorage.GetPreviewVideoObjectKey(romPath)
                }
            };

            var profilePath = RomConfigProfile.GetProfilePath(romPath);
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
            File.WriteAllText(profilePath, JsonSerializer.Serialize(staleProfile, new JsonSerializerOptions { WriteIndented = true }));

            var loaded = RomConfigProfile.LoadValidated(romPath).Profile;

            Assert.Equal(actualPreviewObjectKey, loaded.Resources.PreviewVideoObjectKey);
            Assert.True(loaded.Resources.AdditionalObjects.TryGetValue("preview.video", out var registeredKey));
            Assert.Equal(actualPreviewObjectKey, registeredKey);

            var saved = JsonSerializer.Deserialize<RomConfigProfile>(File.ReadAllText(profilePath));
            Assert.NotNull(saved);
            Assert.Equal(actualPreviewObjectKey, saved!.Resources.PreviewVideoObjectKey);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
        }
    }

    [Fact]
    public void ImportRom_DoesNotReimport_WhenSourceAlreadyInObjectStorage()
    {
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            var existingRomPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Contra.nes");
            Directory.CreateDirectory(Path.GetDirectoryName(existingRomPath)!);
            File.WriteAllBytes(existingRomPath, [0x4E, 0x45, 0x53, 0x1A, 0x01]);

            var service = new RomResourceImportService();
            var imported = service.ImportRom(existingRomPath);

            Assert.Equal(existingRomPath, imported.AbsolutePath);
            Assert.Single(Directory.EnumerateFiles(AppObjectStorage.GetRomsDirectory(), "*.nes", SearchOption.AllDirectories));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
        }
    }

    [Fact]
    public void LoadValidated_MigratesLegacyInputOverrides_ToPlayer1Overrides()
    {
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Double Dragon.nes");
            Directory.CreateDirectory(Path.GetDirectoryName(romPath)!);
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var legacyProfile = new RomConfigProfile
            {
                InputOverrides = new Dictionary<string, string>
                {
                    ["A"] = "Z",
                    ["Start"] = "Enter"
                }
            };

            var profilePath = RomConfigProfile.GetProfilePath(romPath);
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
            File.WriteAllText(profilePath, JsonSerializer.Serialize(legacyProfile, new JsonSerializerOptions { WriteIndented = true }));

            var loaded = RomConfigProfile.LoadValidated(romPath).Profile;

            Assert.True(loaded.PlayerInputOverrides.TryGetValue("Player1", out var player1Overrides));
            Assert.Equal("Z", player1Overrides["A"]);
            Assert.Equal("Enter", player1Overrides["Start"]);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
        }
    }

    private static string CreateTempResourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fc-revolution-rom-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
