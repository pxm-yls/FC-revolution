using System;
using System.Collections.Generic;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowManagedCoreCatalogEntry(
    CoreManifest Manifest,
    string? AssemblyPath,
    string? ModuleTypeName,
    string SourceLabel,
    bool IsLoadSupported,
    string? LoadSupportReason,
    bool CanUninstall,
    string? InstallDirectory,
    string? ManifestPath);

internal sealed record MainWindowManagedCoreCatalogState(
    IReadOnlyList<CoreManifest> Manifests,
    IReadOnlyList<MainWindowManagedCoreCatalogEntry> Entries);

internal sealed class MainWindowManagedCoreCatalogController
{
    public MainWindowManagedCoreCatalogState LoadCatalog(
        string? resourceRootPath,
        IReadOnlyList<string>? managedCoreProbePaths)
    {
        var effectiveProbeDirectories = SystemConfigProfile.ResolveEffectiveCoreProbeDirectories(resourceRootPath, managedCoreProbePaths);
        var entries = ManagedCoreRuntime.LoadCatalogEntries(new ManagedCoreRuntimeOptions(
                ResourceRootPath: resourceRootPath,
                ProbeDirectories: effectiveProbeDirectories))
            .Select(MapEntry)
            .OrderBy(entry => entry.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MainWindowManagedCoreCatalogState(
            entries.Select(entry => entry.Manifest).ToList(),
            entries);
    }

    public string BuildSelectedCoreSourceSummary(MainWindowManagedCoreCatalogEntry? entry)
    {
        if (entry == null)
            return "当前未安装或未选择默认核心。";

        var locationText = !string.IsNullOrWhiteSpace(entry.InstallDirectory)
            ? $"安装位置：{entry.InstallDirectory}"
            : string.IsNullOrWhiteSpace(entry.AssemblyPath)
                ? "入口程序集路径不可用"
                : $"入口程序集：{entry.AssemblyPath}";
        var removableText = entry.CanUninstall ? "可卸载" : "不可卸载";
        var loaderText = entry.IsLoadSupported
            ? "当前宿主可装载"
            : entry.LoadSupportReason ?? "当前宿主缺少对应 loader";
        return $"来源：{entry.SourceLabel} · {removableText} · {loaderText} · {locationText}";
    }

    private static MainWindowManagedCoreCatalogEntry MapEntry(ManagedCoreCatalogEntry entry)
    {
        var sourceLabel = entry.SourceKind switch
        {
            ManagedCoreCatalogSourceKind.BundledPackage => "内置核心包",
            ManagedCoreCatalogSourceKind.InstalledPackage => "已安装核心包",
            _ when entry.CanUninstall => "旧版 DLL 安装",
            _ => "探测目录"
        };
        return new MainWindowManagedCoreCatalogEntry(
            entry.Manifest,
            entry.AssemblyPath,
            entry.ModuleTypeName,
            sourceLabel,
            entry.IsLoadSupported,
            entry.LoadSupportReason,
            entry.CanUninstall,
            entry.InstallDirectory,
            entry.ManifestPath);
    }
}
