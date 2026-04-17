using FCRevolution.Emulation.Abstractions;
using FCRevolution.Storage;

namespace FCRevolution.Emulation.Host;

public sealed record ManagedCoreRuntimeOptions(
    string? ResourceRootPath = null,
    IReadOnlyList<string>? ProbeDirectories = null);

public enum ManagedCoreCatalogSourceKind
{
    ProbeDirectory,
    InstalledPackage,
    BundledPackage
}

public sealed record ManagedCoreCatalogEntry(
    CoreManifest Manifest,
    string? EntryPath,
    string? ActivationType,
    ManagedCoreCatalogSourceKind SourceKind,
    bool IsLoadSupported,
    string? LoadSupportReason,
    bool CanUninstall,
    string? InstallDirectory,
    string? ManifestPath)
{
    public string? AssemblyPath => EntryPath;

    public string? ModuleTypeName => ActivationType;
}

public static class ManagedCoreRuntime
{
    public static EmulatorCoreHost CreateHost(
        string? defaultCoreId = null,
        ManagedCoreRuntimeOptions? options = null,
        IEnumerable<IEmulatorCoreModule>? additionalModules = null)
    {
        var resolvedOptions = ResolveOptions(options);

        var resolvedAdditionalModules = new List<IEmulatorCoreModule>();
        resolvedAdditionalModules.AddRange(LoadPackageModules(resolvedOptions.ResourceRootPath));
        resolvedAdditionalModules.AddRange(LoadProbeModules(resolvedOptions.ProbeDirectories));

        if (additionalModules is not null)
            resolvedAdditionalModules.AddRange(additionalModules);

        return new EmulatorCoreHost(
            DefaultManagedCoreModuleCatalog.CreateModules(resolvedAdditionalModules),
            defaultCoreId);
    }

    public static bool TryCreateSession(
        CoreSessionLaunchRequest request,
        out IEmulatorCoreSession? session,
        string? defaultCoreId = null,
        ManagedCoreRuntimeOptions? options = null,
        IEnumerable<IEmulatorCoreModule>? additionalModules = null)
    {
        var host = CreateHost(defaultCoreId, options, additionalModules);
        return host.TryCreateSession(request, out session);
    }

    public static IEmulatorCoreSession CreateUnavailableSession(string? requestedCoreId = null) =>
        new UnavailableEmulatorCoreSession(requestedCoreId);

    public static IReadOnlyList<ManagedCoreCatalogEntry> LoadCatalogEntries(ManagedCoreRuntimeOptions? options = null)
    {
        var resolvedOptions = ResolveOptions(options);

        var installDirectory = Path.GetFullPath(AppObjectStorage.GetDevelopmentCoreModulesDirectory(
            string.IsNullOrWhiteSpace(resolvedOptions.ResourceRootPath)
                ? AppObjectStorage.GetResourceRoot()
                : resolvedOptions.ResourceRootPath));
        var entries = new Dictionary<string, ManagedCoreCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entryPath in EnumerateProbeEntryPaths(resolvedOptions.ProbeDirectories))
        {
            foreach (var target in InternalCoreLoaderRegistry.BuildProbeTargets(entryPath))
            {
                foreach (var descriptor in InternalCoreLoaderRegistry.DiscoverModules(target))
                {
                    var canUninstall = IsPathUnderDirectory(descriptor.LoadTarget.EntryPath, installDirectory);
                    entries[descriptor.Manifest.CoreId] = new ManagedCoreCatalogEntry(
                        descriptor.Manifest,
                        descriptor.LoadTarget.EntryPath,
                        descriptor.LoadTarget.ModuleTypeName,
                        ManagedCoreCatalogSourceKind.ProbeDirectory,
                        IsLoadSupported: true,
                        LoadSupportReason: null,
                        canUninstall,
                        InstallDirectory: null,
                        ManifestPath: null);
                }
            }
        }

        var packageService = new ManagedCorePackageService();
        foreach (var package in packageService.GetInstalledPackages(resolvedOptions.ResourceRootPath))
        {
            var loadSupport = InternalCoreLoaderRegistry.GetLoadSupport(
                new InternalCoreLoadTarget(
                    package.Manifest.BinaryKind,
                    package.EntryPath,
                    package.ActivationType));
            entries[package.Manifest.CoreId] = new ManagedCoreCatalogEntry(
                package.Manifest,
                package.EntryPath,
                package.ActivationType,
                package.IsBundled ? ManagedCoreCatalogSourceKind.BundledPackage : ManagedCoreCatalogSourceKind.InstalledPackage,
                IsLoadSupported: loadSupport.IsSupported,
                LoadSupportReason: loadSupport.Reason,
                CanUninstall: !package.IsBundled,
                package.InstallDirectory,
                package.ManifestPath);
        }

        return entries.Values
            .OrderBy(entry => entry.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ResolvedManagedCoreRuntimeOptions ResolveOptions(ManagedCoreRuntimeOptions? options)
    {
        var resourceRootPath = AppObjectStorage.NormalizeConfiguredResourceRoot(
            string.IsNullOrWhiteSpace(options?.ResourceRootPath)
                ? AppObjectStorage.GetResourceRoot()
                : options!.ResourceRootPath);
        var probeDirectories = (options?.ProbeDirectories ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(GetPathComparer())
            .ToList();
        return new ResolvedManagedCoreRuntimeOptions(
            resourceRootPath,
            probeDirectories);
    }

    private static IReadOnlyList<IEmulatorCoreModule> LoadPackageModules(string? resourceRootPath)
    {
        var packageSource = new RegistryManagedCoreModuleRegistrationSource(
            "managed-core-runtime-package-registry",
            () => resourceRootPath);
        return packageSource.LoadModules();
    }

    private static IReadOnlyList<IEmulatorCoreModule> LoadProbeModules(IReadOnlyList<string> probeDirectories)
    {
        if (probeDirectories.Count == 0)
            return [];

        var modules = new List<IEmulatorCoreModule>();
        foreach (var entryPath in EnumerateProbeEntryPaths(probeDirectories))
        {
            foreach (var target in InternalCoreLoaderRegistry.BuildProbeTargets(entryPath))
            {
                modules.AddRange(InternalCoreLoaderRegistry.LoadModules(target));
            }
        }

        return modules
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
    }

    private static IEnumerable<string> EnumerateProbeEntryPaths(IReadOnlyList<string> directories)
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
                files = Directory.EnumerateFiles(normalizedDirectory, "*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files.OrderBy(path => path, comparer))
            {
                var normalizedFile = Path.GetFullPath(file);
                if (seenFiles.Add(normalizedFile) &&
                    InternalCoreLoaderRegistry.BuildProbeTargets(normalizedFile).Count > 0)
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

    private sealed record ResolvedManagedCoreRuntimeOptions(
        string ResourceRootPath,
        IReadOnlyList<string> ProbeDirectories);
}
