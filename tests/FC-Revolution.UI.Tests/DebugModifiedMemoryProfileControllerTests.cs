using FCRevolution.Storage;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class DebugModifiedMemoryProfileControllerTests
{
    [Fact]
    public void Save_PersistsModifiedMemoryEntriesToProfile()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-debug-profile-save-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();
            var romPath = CreateRomFile(tempRoot, "save-test.nes");

            DebugModifiedMemoryProfileController.Save(
                romPath,
                [
                    new ModifiedMemoryEntry { Address = 0x0010, DisplayAddress = "$0010", Value = "ab", IsLocked = true },
                    new ModifiedMemoryEntry { Address = 0x00FF, DisplayAddress = "$00FF", Value = "7f", IsLocked = false }
                ]);

            var profile = RomConfigProfile.LoadValidated(romPath).Profile;

            Assert.Collection(
                profile.ModifiedMemory,
                entry =>
                {
                    Assert.Equal("0010", entry.Address);
                    Assert.Equal("AB", entry.Value);
                    Assert.True(entry.IsLocked);
                },
                entry =>
                {
                    Assert.Equal("00FF", entry.Address);
                    Assert.Equal("7F", entry.Value);
                    Assert.False(entry.IsLocked);
                });
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_ReturnsParsedEntries_AndSkipsInvalidOnes()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-debug-profile-load-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();
            var romPath = CreateRomFile(tempRoot, "load-test.nes");
            RomConfigProfile.Save(
                romPath,
                new RomConfigProfile
                {
                    ModifiedMemory =
                    [
                        new RomConfigMemoryEntry { Address = "0010", Value = "0a", IsLocked = true },
                        new RomConfigMemoryEntry { Address = "oops", Value = "GG", IsLocked = false }
                    ]
                });

            var loaded = DebugModifiedMemoryProfileController.TryLoad(romPath, out var result);

            Assert.True(loaded);
            var entry = Assert.Single(result.Entries);
            Assert.Equal((ushort)0x0010, entry.Address);
            Assert.Equal("$0010", entry.DisplayAddress);
            Assert.Equal("0A", entry.Value);
            Assert.True(entry.IsLocked);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildProfileStatus_AppendsExpectedWarningText()
    {
        var mismatch = DebugModifiedMemoryProfileController.BuildProfileStatus(
            "base",
            new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: true, IsForeignMachineProfile: false, IsFutureVersionProfile: false));
        var foreign = DebugModifiedMemoryProfileController.BuildProfileStatus(
            "base",
            new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: false, IsForeignMachineProfile: true, IsFutureVersionProfile: false));
        var future = DebugModifiedMemoryProfileController.BuildProfileStatus(
            "base",
            new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: false, IsForeignMachineProfile: false, IsFutureVersionProfile: true));

        Assert.Contains("文件类型不是 FC-Revolution 专用配置", mismatch, StringComparison.Ordinal);
        Assert.Contains("来自其他设备", foreign, StringComparison.Ordinal);
        Assert.Contains("版本高于当前程序", future, StringComparison.Ordinal);
    }

    private static string CreateRomFile(string root, string fileName)
    {
        var romPath = Path.Combine(root, "roms", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(romPath)!);
        File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);
        return romPath;
    }
}
