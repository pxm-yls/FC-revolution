using System.IO.Compression;
using FCRevolution.Emulation.Host;
using System.Reflection;
using FCRevolution.Core.Nes.Managed;
using FCRevolution.Core.Sample.Managed;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowManagedCoreWorkflowTests
{
    [Fact]
    public void MainWindowViewModel_InstallManagedCoreFromPath_RefreshesCatalogAndSourceSummary()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-install-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var packagePath = BuildSampleManagedCorePackage(tempRoot);

            Assert.DoesNotContain(vm.InstalledCoreManifests, manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);

            InvokeInstallManagedCoreFromPath(vm, packagePath);

            var importedManifest = Assert.Single(
                vm.InstalledCoreManifests,
                manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);
            vm.SelectedDefaultCoreManifest = importedManifest;

            var expectedInstalledDirectory = AppObjectStorage.GetInstalledCoreVersionDirectory(
                tempRoot,
                importedManifest.CoreId,
                importedManifest.Version);
            Assert.True(Directory.Exists(expectedInstalledDirectory));
            Assert.True(File.Exists(Path.Combine(expectedInstalledDirectory, "core-manifest.fcr")));
            Assert.True(vm.CanUninstallSelectedManagedCore);
            Assert.Contains("已安装核心包", vm.SelectedDefaultCoreSourceSummary, StringComparison.Ordinal);
            Assert.Contains(Path.GetFullPath(expectedInstalledDirectory), vm.SelectedDefaultCoreAssemblyPathDisplay, StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void UninstallSelectedManagedCoreCommand_RemovesInstalledCore_AndPersistsFallbackDefaultCore()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-uninstall-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var fallbackCoreId = vm.DefaultCoreId;
            var packagePath = BuildSampleManagedCorePackage(tempRoot);

            InvokeInstallManagedCoreFromPath(vm, packagePath);

            var importedManifest = Assert.Single(
                vm.InstalledCoreManifests,
                manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);
            vm.SelectedDefaultCoreManifest = importedManifest;

            var expectedInstalledDirectory = AppObjectStorage.GetInstalledCoreVersionDirectory(
                tempRoot,
                importedManifest.CoreId,
                importedManifest.Version);

            vm.UninstallSelectedManagedCoreCommand.Execute(null);

            Assert.DoesNotContain(vm.InstalledCoreManifests, manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);
            Assert.Equal(fallbackCoreId, vm.DefaultCoreId);
            Assert.False(vm.CanUninstallSelectedManagedCore);
            Assert.False(Directory.Exists(expectedInstalledDirectory));
            Assert.Equal(fallbackCoreId, SystemConfigProfile.Load().DefaultCoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowManagedCoreInstallController_Install_ExtractsPackageAndWritesRegistry()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-install-controller-{Guid.NewGuid():N}");

        try
        {
            var controller = new MainWindowManagedCoreInstallController();
            var sourcePath = BuildSampleManagedCorePackage(tempRoot);

            var result = controller.Install(sourcePath, tempRoot);

            Assert.Equal(SampleManagedCoreModule.CoreId, result.Manifest.CoreId);
            Assert.True(Directory.Exists(result.InstallDirectory));
            Assert.True(File.Exists(result.EntryAssemblyPath));
            Assert.True(File.Exists(AppObjectStorage.GetCoreRegistryPath(tempRoot)));
            Assert.False(result.ReplacedExistingCore);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowManagedCoreCatalogController_LoadCatalog_MarksInstalledPackageAsUninstallable()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-catalog-{Guid.NewGuid():N}");

        try
        {
            var installController = new MainWindowManagedCoreInstallController();
            var packagePath = BuildSampleManagedCorePackage(tempRoot);
            var installResult = installController.Install(packagePath, tempRoot);
            var controller = new MainWindowManagedCoreCatalogController();

            var state = controller.LoadCatalog(tempRoot, []);

            var importedEntry = Assert.Single(
                state.Entries,
                entry => entry.Manifest.CoreId == SampleManagedCoreModule.CoreId);
            Assert.Equal(Path.GetFullPath(installResult.EntryAssemblyPath), importedEntry.AssemblyPath);
            Assert.Equal("已安装核心包", importedEntry.SourceLabel);
            Assert.True(importedEntry.CanUninstall);
            Assert.Equal(Path.GetFullPath(installResult.InstallDirectory), importedEntry.InstallDirectory);
            Assert.Contains("安装位置", controller.BuildSelectedCoreSourceSummary(importedEntry), StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowManagedCoreCatalogController_LoadCatalog_ExposesBundledPackageAsReadOnly()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-bundled-catalog-{Guid.NewGuid():N}");

        try
        {
            BundledManagedCoreBootstrapper.EnsureBundledCorePackages(tempRoot);
            var controller = new MainWindowManagedCoreCatalogController();

            var state = controller.LoadCatalog(tempRoot, []);

            var bundledEntry = Assert.Single(
                state.Entries,
                entry => entry.Manifest.CoreId == NesManagedCoreModule.CoreId);
            Assert.Equal("内置核心包", bundledEntry.SourceLabel);
            Assert.False(bundledEntry.CanUninstall);
            Assert.False(string.IsNullOrWhiteSpace(bundledEntry.InstallDirectory));
            Assert.Contains("不可卸载", controller.BuildSelectedCoreSourceSummary(bundledEntry), StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowManagedCoreExportController_Export_CreatesReusableCorePackage()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-export-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var exportController = new MainWindowManagedCoreExportController();
            var installController = new MainWindowManagedCoreInstallController();
            var packagePath = Path.Combine(tempRoot, "sample-export.fcrcore.zip");
            var entry = new MainWindowManagedCoreCatalogEntry(
                new SampleManagedCoreModule().Manifest,
                GetSampleManagedCoreAssemblyPath(),
                typeof(SampleManagedCoreModule).FullName,
                "探测目录",
                CanUninstall: false,
                InstallDirectory: null,
                ManifestPath: null);

            var exportResult = exportController.Export(entry, packagePath);
            var installResult = installController.Install(exportResult.PackagePath, tempRoot);

            Assert.True(File.Exists(exportResult.PackagePath));
            using var archive = ZipFile.OpenRead(exportResult.PackagePath);
            Assert.Contains(archive.Entries, item => string.Equals(item.FullName, "core-manifest.fcr", StringComparison.Ordinal));
            Assert.Contains(archive.Entries, item => item.FullName.EndsWith("Sample.Managed.dll", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(SampleManagedCoreModule.CoreId, installResult.Manifest.CoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowManagedCoreExportController_ExportBuiltInNesCore_CreatesInstallablePackage()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-built-in-export-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var exportController = new MainWindowManagedCoreExportController();
            var installController = new MainWindowManagedCoreInstallController();
            var packagePath = Path.Combine(tempRoot, "built-in-nes.fcrcore.zip");
            var entry = new MainWindowManagedCoreCatalogEntry(
                new NesManagedCoreModule().Manifest,
                typeof(NesManagedCoreModule).Assembly.Location,
                typeof(NesManagedCoreModule).FullName,
                "内置",
                CanUninstall: false,
                InstallDirectory: null,
                ManifestPath: null);

            var exportResult = exportController.Export(entry, packagePath);
            var installResult = installController.Install(exportResult.PackagePath, tempRoot);

            Assert.True(File.Exists(exportResult.PackagePath));
            Assert.Equal(NesManagedCoreModule.CoreId, installResult.Manifest.CoreId);
            Assert.True(Directory.Exists(installResult.InstallDirectory));
            Assert.True(File.Exists(installResult.EntryAssemblyPath));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string BuildSampleManagedCorePackage(string rootDirectory)
    {
        var packageRoot = Path.Combine(rootDirectory, "packages");
        Directory.CreateDirectory(packageRoot);

        var exportController = new MainWindowManagedCoreExportController();
        var packagePath = Path.Combine(packageRoot, $"sample-{Guid.NewGuid():N}.fcrcore.zip");
        var entry = new MainWindowManagedCoreCatalogEntry(
            new SampleManagedCoreModule().Manifest,
            GetSampleManagedCoreAssemblyPath(),
            typeof(SampleManagedCoreModule).FullName,
            "探测目录",
            CanUninstall: false,
            InstallDirectory: null,
            ManifestPath: null);

        exportController.Export(entry, packagePath);
        return packagePath;
    }

    private static string GetSampleManagedCoreAssemblyPath()
    {
        var assemblyPath = typeof(SampleManagedCoreModule).Assembly.Location;
        Assert.False(string.IsNullOrWhiteSpace(assemblyPath));
        return assemblyPath;
    }

    private static void InvokeInstallManagedCoreFromPath(MainWindowViewModel viewModel, string sourcePath)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "InstallManagedCoreFromPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [sourcePath]);
    }
}
