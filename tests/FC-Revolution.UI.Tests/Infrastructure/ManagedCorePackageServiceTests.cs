using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;

namespace FC_Revolution.UI.Tests;

public sealed class ManagedCorePackageServiceTests
{
    [Fact]
    public void InstallPackage_AcceptsGenericEntryPointForNativePackage()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-core-package-native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var packagePath = BuildPackage(
                tempRoot,
                new ManagedCorePackageManifestDocument
                {
                    Payload = new ManagedCorePackageManifestPayload
                    {
                        CoreId = "fc.native.test",
                        DisplayName = "Native Test Core",
                        Version = "1.0.0",
                        SystemIds = ["test"],
                        BinaryKind = CoreBinaryKinds.NativeCabi,
                        EntryPoint = new ManagedCorePackageEntryPoint
                        {
                            EntryPath = "native/core.bin"
                        },
                        Files = [CreateFileSpec("native/core.bin", "native-test-core"u8.ToArray()).Entry]
                    }
                },
                [CreateFileSpec("native/core.bin", "native-test-core"u8.ToArray())]);

            var service = new ManagedCorePackageService();
            var result = service.InstallPackage(packagePath, tempRoot);
            var installed = Assert.Single(
                service.GetInstalledPackages(tempRoot),
                package => string.Equals(package.Manifest.CoreId, "fc.native.test", StringComparison.OrdinalIgnoreCase));
            var catalogEntry = Assert.Single(
                ManagedCoreRuntime.LoadCatalogEntries(new ManagedCoreRuntimeOptions(ResourceRootPath: tempRoot)),
                entry => string.Equals(entry.Manifest.CoreId, "fc.native.test", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(CoreBinaryKinds.NativeCabi, result.Package.Manifest.BinaryKind);
            Assert.Equal(result.Package.EntryPath, installed.EntryPath);
            Assert.Null(result.Package.ActivationType);
            Assert.Equal(result.Package.EntryPath, catalogEntry.EntryPath);
            Assert.Equal(CoreBinaryKinds.NativeCabi, catalogEntry.Manifest.BinaryKind);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void InstallPackage_ReadsLegacyManagedEntryPointFields()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-core-package-legacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var packagePath = BuildPackage(
                tempRoot,
                new ManagedCorePackageManifestDocument
                {
                    Payload = new ManagedCorePackageManifestPayload
                    {
                        CoreId = "fc.legacy.managed",
                        DisplayName = "Legacy Managed Test Core",
                        Version = "1.0.0",
                        SystemIds = ["test"],
                        BinaryKind = CoreBinaryKinds.ManagedDotNet,
                        EntryPoint = new ManagedCorePackageEntryPoint
                        {
                            AssemblyPath = "managed/legacy.dll",
                            FactoryType = "Legacy.Managed.Factory"
                        },
                        Files = [CreateFileSpec("managed/legacy.dll", "legacy-managed-core"u8.ToArray()).Entry]
                    }
                },
                [CreateFileSpec("managed/legacy.dll", "legacy-managed-core"u8.ToArray())]);

            var service = new ManagedCorePackageService();
            var result = service.InstallPackage(packagePath, tempRoot);
            var catalogEntry = Assert.Single(
                ManagedCoreRuntime.LoadCatalogEntries(new ManagedCoreRuntimeOptions(ResourceRootPath: tempRoot)),
                entry => string.Equals(entry.Manifest.CoreId, "fc.legacy.managed", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("Legacy.Managed.Factory", result.Package.ActivationType);
            Assert.Equal("Legacy.Managed.Factory", result.Package.FactoryType);
            Assert.EndsWith(Path.Combine("managed", "legacy.dll"), result.Package.EntryPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(result.Package.EntryPath, catalogEntry.EntryPath);
            Assert.Equal("Legacy.Managed.Factory", catalogEntry.ActivationType);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string BuildPackage(
        string rootDirectory,
        ManagedCorePackageManifestDocument manifestDocument,
        IReadOnlyList<PackageFileSpec> fileSpecs)
    {
        var packageDirectory = Path.Combine(rootDirectory, $"package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageDirectory);

        foreach (var fileSpec in fileSpecs)
        {
            var targetPath = Path.Combine(packageDirectory, fileSpec.Entry.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllBytes(targetPath, fileSpec.Content);
        }

        File.WriteAllText(
            Path.Combine(packageDirectory, "core-manifest.fcr"),
            JsonSerializer.Serialize(manifestDocument, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }));

        var packagePath = Path.Combine(rootDirectory, $"{manifestDocument.Payload.CoreId}.fcrcore.zip");
        if (File.Exists(packagePath))
            File.Delete(packagePath);
        ZipFile.CreateFromDirectory(packageDirectory, packagePath);
        Directory.Delete(packageDirectory, recursive: true);
        return packagePath;
    }

    private static PackageFileSpec CreateFileSpec(string relativePath, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new PackageFileSpec(
            new ManagedCorePackageFileEntry
            {
                Path = relativePath,
                Sha256 = hash
            },
            content);
    }

    private sealed record PackageFileSpec(ManagedCorePackageFileEntry Entry, byte[] Content);
}
