using System;
using System.IO;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowManagedCoreInstallResult(
    CoreManifest Manifest,
    string InstallDirectory,
    string EntryAssemblyPath,
    bool ReplacedExistingCore);

internal sealed class MainWindowManagedCoreInstallController
{
    private readonly ManagedCorePackageService _packageService = new();

    public MainWindowManagedCoreInstallResult Install(string sourcePath, string resourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedSourcePath))
            throw new FileNotFoundException("Core package was not found.", normalizedSourcePath);

        var installResult = _packageService.InstallPackage(normalizedSourcePath, resourceRootPath);
        return new MainWindowManagedCoreInstallResult(
            installResult.Package.Manifest,
            installResult.Package.InstallDirectory,
            installResult.Package.EntryAssemblyPath,
            installResult.ReplacedExistingCore);
    }

    public bool Uninstall(MainWindowManagedCoreCatalogEntry entry, string resourceRootPath)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrWhiteSpace(entry.InstallDirectory))
            return _packageService.UninstallInstalledPackage(resourceRootPath, entry.Manifest.CoreId);

        if (string.IsNullOrWhiteSpace(entry.AssemblyPath) || !entry.CanUninstall)
            return false;

        var legacyInstallDirectory = Path.GetFullPath(AppObjectStorage.GetManagedCoreModulesDirectory(resourceRootPath));
        var normalizedAssemblyPath = Path.GetFullPath(entry.AssemblyPath);
        if (!IsPathUnderDirectory(normalizedAssemblyPath, legacyInstallDirectory) || !File.Exists(normalizedAssemblyPath))
            return false;

        File.Delete(normalizedAssemblyPath);
        return true;
    }

    private static bool IsPathUnderDirectory(string path, string directory)
    {
        try
        {
            var relativePath = Path.GetRelativePath(
                Path.GetFullPath(directory),
                Path.GetFullPath(path));
            return !relativePath.StartsWith("..", StringComparison.Ordinal) &&
                   !Path.IsPathRooted(relativePath);
        }
        catch
        {
            return false;
        }
    }
}
