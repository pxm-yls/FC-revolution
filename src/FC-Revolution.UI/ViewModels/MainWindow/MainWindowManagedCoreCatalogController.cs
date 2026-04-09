using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using FCRevolution.Core.Nes.Managed;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowManagedCoreCatalogEntry(
    CoreManifest Manifest,
    string? AssemblyPath,
    string? ModuleTypeName,
    string SourceLabel,
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
        var effectiveProbeDirectories = SystemConfigProfile.ResolveEffectiveManagedCoreProbeDirectories(resourceRootPath, managedCoreProbePaths);
        var installDirectory = Path.GetFullPath(AppObjectStorage.GetManagedCoreModulesDirectory(
            string.IsNullOrWhiteSpace(resourceRootPath)
                ? AppObjectStorage.GetResourceRoot()
                : resourceRootPath!));
        var entries = BuildCatalogEntries(resourceRootPath, installDirectory, effectiveProbeDirectories)
            .OrderBy(entry => entry.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MainWindowManagedCoreCatalogState(
            entries.Select(entry => entry.Manifest).ToList(),
            entries);
    }

    public string BuildSelectedCoreSourceSummary(MainWindowManagedCoreCatalogEntry? entry)
    {
        if (entry == null)
            return "当前默认核心来源未知。";

        var locationText = !string.IsNullOrWhiteSpace(entry.InstallDirectory)
            ? $"安装位置：{entry.InstallDirectory}"
            : string.IsNullOrWhiteSpace(entry.AssemblyPath)
                ? "入口程序集路径不可用"
                : $"入口程序集：{entry.AssemblyPath}";
        var removableText = entry.CanUninstall ? "可卸载" : "不可卸载";
        return $"来源：{entry.SourceLabel} · {removableText} · {locationText}";
    }

    private static IReadOnlyList<MainWindowManagedCoreCatalogEntry> BuildCatalogEntries(
        string? resourceRootPath,
        string installDirectory,
        IReadOnlyList<string> effectiveProbeDirectories)
    {
        var packageService = new ManagedCorePackageService();
        var entries = new Dictionary<string, MainWindowManagedCoreCatalogEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [NesManagedCoreModule.CoreId] = new(
                new NesManagedCoreModule().Manifest,
                AssemblyPath: typeof(NesManagedCoreModule).Assembly.Location,
                ModuleTypeName: typeof(NesManagedCoreModule).FullName,
                SourceLabel: "内置",
                CanUninstall: false,
                InstallDirectory: null,
                ManifestPath: null)
        };

        foreach (var assemblyPath in EnumerateAssemblyPaths(effectiveProbeDirectories))
        {
            foreach (var descriptor in DiscoverModuleDescriptorsFromAssemblyPath(assemblyPath))
            {
                var canUninstall = IsPathUnderDirectory(assemblyPath, installDirectory);
                var sourceLabel = canUninstall ? "旧版 DLL 安装" : "探测目录";
                entries[descriptor.Manifest.CoreId] = new MainWindowManagedCoreCatalogEntry(
                    descriptor.Manifest,
                    assemblyPath,
                    descriptor.ModuleTypeName,
                    sourceLabel,
                    canUninstall,
                    InstallDirectory: null,
                    ManifestPath: null);
            }
        }

        foreach (var package in packageService.GetInstalledPackages(resourceRootPath))
        {
            entries[package.Manifest.CoreId] = new MainWindowManagedCoreCatalogEntry(
                package.Manifest,
                package.EntryAssemblyPath,
                package.FactoryType,
                "已安装核心包",
                CanUninstall: true,
                package.InstallDirectory,
                package.ManifestPath);
        }

        return entries.Values.ToList();
    }

    private static IReadOnlyList<DiscoveredManagedCoreModuleDescriptor> DiscoverModuleDescriptorsFromAssemblyPath(string assemblyPath)
    {
        var loadContext = new ManagedCoreInspectionLoadContext(assemblyPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            return assembly
                .GetTypes()
                .Where(type =>
                    type is { IsAbstract: false, IsInterface: false } &&
                    typeof(IManagedCoreModule).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => new DiscoveredManagedCoreModuleDescriptor(
                    ((IManagedCoreModule)Activator.CreateInstance(type)!).Manifest,
                    type.FullName ?? type.Name))
                .ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(type =>
                    type is { IsAbstract: false, IsInterface: false } &&
                    typeof(IManagedCoreModule).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => new DiscoveredManagedCoreModuleDescriptor(
                    ((IManagedCoreModule)Activator.CreateInstance(type!)!).Manifest,
                    type!.FullName ?? type.Name))
                .ToList();
        }
        catch
        {
            return [];
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static IEnumerable<string> EnumerateAssemblyPaths(IReadOnlyList<string> directories)
    {
        var comparer = GetPathComparer();
        var seenDirectories = new HashSet<string>(comparer);
        var seenFiles = new HashSet<string>(comparer);

        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            string normalizedDirectory;
            try
            {
                normalizedDirectory = Path.GetFullPath(directory);
            }
            catch
            {
                continue;
            }

            if (!seenDirectories.Add(normalizedDirectory) || !Directory.Exists(normalizedDirectory))
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(normalizedDirectory, "*.dll", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files.OrderBy(path => path, comparer))
            {
                var normalizedFile = Path.GetFullPath(file);
                if (seenFiles.Add(normalizedFile))
                    yield return normalizedFile;
            }
        }
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

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record DiscoveredManagedCoreModuleDescriptor(
        CoreManifest Manifest,
        string ModuleTypeName);

    private sealed class ManagedCoreInspectionLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
            {
                if (AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName))
                    return assembly;
            }

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return string.IsNullOrWhiteSpace(resolvedPath)
                ? null
                : LoadFromAssemblyPath(resolvedPath);
        }
    }
}
