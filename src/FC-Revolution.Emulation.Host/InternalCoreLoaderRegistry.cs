using System.Reflection;
using System.Runtime.Loader;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

internal sealed record InternalCoreLoadTarget(
    string BinaryKind,
    string EntryPath,
    string? ModuleTypeName = null);

internal sealed record InternalDiscoveredCoreLoadTarget(
    CoreManifest Manifest,
    InternalCoreLoadTarget LoadTarget);

internal interface IInternalCoreLoader
{
    string BinaryKind { get; }

    IReadOnlyList<IEmulatorCoreModule> LoadModules(InternalCoreLoadTarget target);

    IReadOnlyList<InternalDiscoveredCoreLoadTarget> DiscoverModules(InternalCoreLoadTarget target);
}

internal static class InternalCoreLoaderRegistry
{
    private static readonly IReadOnlyDictionary<string, IInternalCoreLoader> Loaders =
        new Dictionary<string, IInternalCoreLoader>(StringComparer.OrdinalIgnoreCase)
        {
            [CoreBinaryKinds.ManagedDotNet] = new ManagedDotNetInternalCoreLoader()
        };

    public static IReadOnlyList<IEmulatorCoreModule> LoadModules(InternalCoreLoadTarget target)
    {
        if (!TryGetLoader(target.BinaryKind, out var loader))
            return [];

        return loader.LoadModules(target);
    }

    public static IReadOnlyList<InternalDiscoveredCoreLoadTarget> DiscoverModules(InternalCoreLoadTarget target)
    {
        if (!TryGetLoader(target.BinaryKind, out var loader))
            return [];

        return loader.DiscoverModules(target);
    }

    private static bool TryGetLoader(string? binaryKind, out IInternalCoreLoader loader)
    {
        if (!string.IsNullOrWhiteSpace(binaryKind) &&
            Loaders.TryGetValue(binaryKind, out loader!))
        {
            return true;
        }

        loader = null!;
        return false;
    }
}

internal sealed class ManagedDotNetInternalCoreLoader : IInternalCoreLoader
{
    public string BinaryKind => CoreBinaryKinds.ManagedDotNet;

    public IReadOnlyList<IEmulatorCoreModule> LoadModules(InternalCoreLoadTarget target) =>
        ManagedCoreAssemblyModuleLoader.LoadModulesFromAssemblyPath(target.EntryPath, target.ModuleTypeName);

    public IReadOnlyList<InternalDiscoveredCoreLoadTarget> DiscoverModules(InternalCoreLoadTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.EntryPath))
            return [];

        return ManagedDotNetCoreModuleDiscovery.Discover(target.EntryPath)
            .Select(descriptor => new InternalDiscoveredCoreLoadTarget(
                descriptor.Manifest,
                target with { ModuleTypeName = descriptor.ModuleTypeName }))
            .ToList();
    }
}

internal static class ManagedDotNetCoreModuleDiscovery
{
    public static IReadOnlyList<ManagedDotNetCoreModuleDescriptor> Discover(string assemblyPath)
    {
        var loadContext = new ManagedCoreInspectionLoadContext(assemblyPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            return BuildDescriptors(assembly.GetTypes());
        }
        catch (ReflectionTypeLoadException ex)
        {
            return BuildDescriptors(ex.Types.Where(static type => type is not null).Cast<Type>());
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

    private static IReadOnlyList<ManagedDotNetCoreModuleDescriptor> BuildDescriptors(IEnumerable<Type> types) =>
        types
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IEmulatorCoreModule).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => new ManagedDotNetCoreModuleDescriptor(
                ((IEmulatorCoreModule)Activator.CreateInstance(type)!).Manifest,
                type.FullName ?? type.Name))
            .ToList();

    internal sealed record ManagedDotNetCoreModuleDescriptor(
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
