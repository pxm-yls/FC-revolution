using FCRevolution.Emulation.Abstractions;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace FCRevolution.Emulation.Host;

public interface IManagedCoreModuleRegistrationSource
{
    string SourceId { get; }

    IReadOnlyList<IManagedCoreModule> LoadModules();
}

public sealed class AssemblyManagedCoreModuleRegistrationSource : IManagedCoreModuleRegistrationSource
{
    private readonly Func<IEnumerable<Assembly>> _getAssemblies;

    public AssemblyManagedCoreModuleRegistrationSource(string sourceId, Func<IEnumerable<Assembly>> getAssemblies)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("Source id is required.", nameof(sourceId))
            : sourceId;
        _getAssemblies = getAssemblies ?? throw new ArgumentNullException(nameof(getAssemblies));
    }

    public string SourceId { get; }

    public IReadOnlyList<IManagedCoreModule> LoadModules() =>
        _getAssemblies()
            .SelectMany(DefaultManagedCoreModuleCatalog.DiscoverModulesFromAssembly)
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
}

public sealed class DirectoryManagedCoreModuleRegistrationSource : IManagedCoreModuleRegistrationSource
{
    private readonly Func<IEnumerable<string>> _getDirectories;
    private readonly string _searchPattern;
    private readonly SearchOption _searchOption;

    public DirectoryManagedCoreModuleRegistrationSource(
        string sourceId,
        Func<IEnumerable<string>> getDirectories,
        string searchPattern = "*.dll",
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("Source id is required.", nameof(sourceId))
            : sourceId;
        _getDirectories = getDirectories ?? throw new ArgumentNullException(nameof(getDirectories));
        _searchPattern = string.IsNullOrWhiteSpace(searchPattern)
            ? throw new ArgumentException("Search pattern is required.", nameof(searchPattern))
            : searchPattern;
        _searchOption = searchOption;
    }

    public string SourceId { get; }

    public IReadOnlyList<IManagedCoreModule> LoadModules() =>
        EnumerateAssemblyPaths()
            .SelectMany(ManagedCoreAssemblyModuleLoader.LoadModulesFromAssemblyPath)
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

    private IEnumerable<string> EnumerateAssemblyPaths()
    {
        var directoryComparer = GetPathComparer();
        var seenDirectories = new HashSet<string>(directoryComparer);
        var seenFiles = new HashSet<string>(directoryComparer);

        foreach (var directory in _getDirectories())
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
                files = Directory.EnumerateFiles(normalizedDirectory, _searchPattern, _searchOption);
            }
            catch
            {
                continue;
            }

            foreach (var file in files.OrderBy(path => path, directoryComparer))
            {
                var normalizedFile = Path.GetFullPath(file);
                if (seenFiles.Add(normalizedFile))
                    yield return normalizedFile;
            }
        }
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

public sealed class RegistryManagedCoreModuleRegistrationSource : IManagedCoreModuleRegistrationSource
{
    private readonly Func<string?> _getResourceRootPath;
    private readonly ManagedCorePackageService _packageService = new();

    public RegistryManagedCoreModuleRegistrationSource(string sourceId, Func<string?> getResourceRootPath)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("Source id is required.", nameof(sourceId))
            : sourceId;
        _getResourceRootPath = getResourceRootPath ?? throw new ArgumentNullException(nameof(getResourceRootPath));
    }

    public string SourceId { get; }

    public IReadOnlyList<IManagedCoreModule> LoadModules() =>
        _packageService
            .GetInstalledPackages(_getResourceRootPath())
            .SelectMany(package => ManagedCoreAssemblyModuleLoader.LoadModulesFromAssemblyPath(package.EntryAssemblyPath))
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
}

internal static class ManagedCoreAssemblyModuleLoader
{
    public static IReadOnlyList<IManagedCoreModule> LoadModulesFromAssemblyPath(string assemblyPath)
    {
        var assembly = TryResolveLoadedAssembly(assemblyPath) ?? TryLoadAssembly(assemblyPath);
        return assembly is null
            ? []
            : DefaultManagedCoreModuleCatalog.DiscoverModulesFromAssembly(assembly);
    }

    private static Assembly? TryResolveLoadedAssembly(string assemblyPath)
    {
        var comparer = GetPathComparer();
        var normalizedPath = Path.GetFullPath(assemblyPath);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                if (comparer.Equals(Path.GetFullPath(assembly.Location), normalizedPath))
                    return assembly;
            }
            catch
            {
            }
        }

        return null;
    }

    private static Assembly? TryLoadAssembly(string assemblyPath)
    {
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        }
        catch (FileLoadException)
        {
            return TryResolveLoadedAssembly(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
