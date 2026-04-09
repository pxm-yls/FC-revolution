using System;
using FCRevolution.Emulation.Host;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowManagedCoreExportResult(
    string PackagePath,
    string CoreId,
    string Version);

internal sealed class MainWindowManagedCoreExportController
{
    private readonly ManagedCorePackageService _packageService = new();

    public MainWindowManagedCoreExportResult Export(MainWindowManagedCoreCatalogEntry entry, string destinationPackagePath)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrWhiteSpace(entry.InstallDirectory) &&
            !string.IsNullOrWhiteSpace(entry.ManifestPath))
        {
            var result = _packageService.ExportInstalledPackage(entry.InstallDirectory, destinationPackagePath);
            return new MainWindowManagedCoreExportResult(
                result.PackagePath,
                result.ManifestDocument.Payload.CoreId,
                result.ManifestDocument.Payload.Version);
        }

        if (string.IsNullOrWhiteSpace(entry.AssemblyPath) || string.IsNullOrWhiteSpace(entry.ModuleTypeName))
            throw new InvalidOperationException("当前核心缺少可导出的入口程序集信息。");

        var looseResult = _packageService.ExportLooseManagedModule(
            entry.Manifest,
            entry.AssemblyPath,
            entry.ModuleTypeName,
            destinationPackagePath);
        return new MainWindowManagedCoreExportResult(
            looseResult.PackagePath,
            looseResult.ManifestDocument.Payload.CoreId,
            looseResult.ManifestDocument.Payload.Version);
    }
}
