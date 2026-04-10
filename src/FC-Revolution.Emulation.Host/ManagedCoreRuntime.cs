using System.Reflection;
using System.Runtime.Loader;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Storage;

namespace FCRevolution.Emulation.Host;

public sealed record ManagedCoreRuntimeOptions(
    string? ResourceRootPath = null,
    IReadOnlyList<string>? ProbeDirectories = null,
    bool EnsureBundledCorePackages = false);

public enum ManagedCoreCatalogSourceKind
{
    ProbeDirectory,
    InstalledPackage,
    BundledPackage
}

public sealed record ManagedCoreCatalogEntry(
    CoreManifest Manifest,
    string? AssemblyPath,
    string? ModuleTypeName,
    ManagedCoreCatalogSourceKind SourceKind,
    bool CanUninstall,
    string? InstallDirectory,
    string? ManifestPath);

public static class ManagedCoreRuntime
{
    public static EmulatorCoreHost CreateHost(
        string? defaultCoreId = null,
        ManagedCoreRuntimeOptions? options = null,
        IEnumerable<IManagedCoreModule>? additionalModules = null)
    {
        var resolvedOptions = ResolveOptions(options);
        EnsureBundledCorePackagesIfRequested(resolvedOptions);

        var resolvedAdditionalModules = new List<IManagedCoreModule>();
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
        IEnumerable<IManagedCoreModule>? additionalModules = null)
    {
        var host = CreateHost(defaultCoreId, options, additionalModules);
        return host.TryCreateSession(request, out session);
    }

    public static IEmulatorCoreSession CreateUnavailableSession(string? requestedCoreId = null) =>
        new UnavailableEmulatorCoreSession(requestedCoreId);

    public static IReadOnlyList<ManagedCoreCatalogEntry> LoadCatalogEntries(ManagedCoreRuntimeOptions? options = null)
    {
        var resolvedOptions = ResolveOptions(options);
        EnsureBundledCorePackagesIfRequested(resolvedOptions);

        var installDirectory = Path.GetFullPath(AppObjectStorage.GetManagedCoreModulesDirectory(
            string.IsNullOrWhiteSpace(resolvedOptions.ResourceRootPath)
                ? AppObjectStorage.GetResourceRoot()
                : resolvedOptions.ResourceRootPath));
        var entries = new Dictionary<string, ManagedCoreCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in EnumerateAssemblyPaths(resolvedOptions.ProbeDirectories))
        {
            foreach (var descriptor in DiscoverModuleDescriptorsFromAssemblyPath(assemblyPath))
            {
                var canUninstall = IsPathUnderDirectory(assemblyPath, installDirectory);
                entries[descriptor.Manifest.CoreId] = new ManagedCoreCatalogEntry(
                    descriptor.Manifest,
                    assemblyPath,
                    descriptor.ModuleTypeName,
                    ManagedCoreCatalogSourceKind.ProbeDirectory,
                    canUninstall,
                    InstallDirectory: null,
                    ManifestPath: null);
            }
        }

        var packageService = new ManagedCorePackageService();
        foreach (var package in packageService.GetInstalledPackages(resolvedOptions.ResourceRootPath))
        {
            entries[package.Manifest.CoreId] = new ManagedCoreCatalogEntry(
                package.Manifest,
                package.EntryAssemblyPath,
                package.FactoryType,
                package.IsBundled ? ManagedCoreCatalogSourceKind.BundledPackage : ManagedCoreCatalogSourceKind.InstalledPackage,
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
            probeDirectories,
            options?.EnsureBundledCorePackages ?? false);
    }

    private static void EnsureBundledCorePackagesIfRequested(ResolvedManagedCoreRuntimeOptions options)
    {
        if (options.EnsureBundledCorePackages)
            BundledManagedCoreBootstrapper.EnsureBundledCorePackages(options.ResourceRootPath);
    }

    private static IReadOnlyList<IManagedCoreModule> LoadPackageModules(string? resourceRootPath)
    {
        var packageSource = new RegistryManagedCoreModuleRegistrationSource(
            "managed-core-runtime-package-registry",
            () => resourceRootPath);
        return packageSource.LoadModules();
    }

    private static IReadOnlyList<IManagedCoreModule> LoadProbeModules(IReadOnlyList<string> probeDirectories)
    {
        if (probeDirectories.Count == 0)
            return [];

        var source = new DirectoryManagedCoreModuleRegistrationSource(
            "managed-core-runtime-probe-directories",
            () => probeDirectories);
        return source.LoadModules();
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

    private sealed record ResolvedManagedCoreRuntimeOptions(
        string ResourceRootPath,
        IReadOnlyList<string> ProbeDirectories,
        bool EnsureBundledCorePackages);

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
