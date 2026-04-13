using FCRevolution.Core.Checker;
using FCRevolution.Core.Sample.Managed;
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
}
