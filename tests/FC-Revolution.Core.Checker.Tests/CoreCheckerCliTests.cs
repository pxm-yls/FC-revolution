using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FCRevolution.Core.Checker;
using FCRevolution.Core.Sample.Managed;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;

namespace FCRevolution.Core.Checker.Tests;

public sealed class CoreCheckerCliTests
{
    [Fact]
    public async Task RunAsync_List_ReportsDiscoveredProbeDirectoryCores()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var probeDirectory = Path.GetDirectoryName(typeof(SampleManagedCoreModule).Assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(probeDirectory));

        var exitCode = await CoreCheckerCli.RunAsync(
            ["list", "--probe-dir", probeDirectory!],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains(SampleManagedCoreModule.CoreId, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("probe-directory", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task RunAsync_CheckPackage_CreatesSessionAndLoadsMedia()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-checker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var packagePath = BuildSamplePackage(tempDirectory);
            var romPath = Path.Combine(tempDirectory, "sample.rom");
            await File.WriteAllBytesAsync(romPath, "sample-core-checker"u8.ToArray());

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await CoreCheckerCli.RunAsync(
                ["check", "--package", packagePath, "--core-id", SampleManagedCoreModule.CoreId, "--rom", romPath, "--frames", "2"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.Contains("Smoke check passed.", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"Selected core: {SampleManagedCoreModule.CoreId}", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Media load: ok", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_Check_WhenRequestedCoreIsMissing_ReturnsValidationFailure()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var probeDirectory = Path.GetDirectoryName(typeof(SampleManagedCoreModule).Assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(probeDirectory));

        var exitCode = await CoreCheckerCli.RunAsync(
            ["check", "--probe-dir", probeDirectory!, "--core-id", "missing.core"],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("missing.core", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CheckPackage_WhenBinaryKindLoaderIsMissing_ReturnsValidationFailure()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-checker-native-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var packagePath = BuildStandalonePackage(
                tempDirectory,
                new ManagedCorePackageManifestDocument
                {
                    Payload = new ManagedCorePackageManifestPayload
                    {
                        CoreId = "fc.native.checker",
                        DisplayName = "Native Checker Core",
                        Version = "1.0.0",
                        SystemIds = ["test"],
                        BinaryKind = CoreBinaryKinds.NativeCabi,
                        EntryPoint = new ManagedCorePackageEntryPoint
                        {
                            EntryPath = "native/core.bin"
                        },
                        Files = [CreateFileSpec("native/core.bin", "native-checker-core"u8.ToArray()).Entry]
                    }
                },
                [CreateFileSpec("native/core.bin", "native-checker-core"u8.ToArray())]);

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await CoreCheckerCli.RunAsync(
                ["check", "--package", packagePath, "--core-id", "fc.native.checker"],
                stdout,
                stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("binaryKind='native-cabi'", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Smoke check passed.", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string BuildSamplePackage(string tempDirectory)
    {
        var packageService = new ManagedCorePackageService();
        var module = new SampleManagedCoreModule();
        var packagePath = Path.Combine(
            tempDirectory,
            packageService.BuildSuggestedPackageFileName(module.Manifest));
        return packageService.ExportLooseManagedModule(
            module.Manifest,
            typeof(SampleManagedCoreModule).Assembly.Location,
            typeof(SampleManagedCoreModule).FullName ?? nameof(SampleManagedCoreModule),
            packagePath).PackagePath;
    }

    private static string BuildStandalonePackage(
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
