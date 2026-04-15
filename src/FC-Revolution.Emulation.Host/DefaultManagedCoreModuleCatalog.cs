using FCRevolution.Emulation.Abstractions;
using System.Reflection;

namespace FCRevolution.Emulation.Host;

public static class DefaultManagedCoreModuleCatalog
{
    private static readonly Lock AdditionalModuleGate = new();
    private static readonly Dictionary<string, IEmulatorCoreModule> AdditionalModules = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IEmulatorCoreModuleRegistrationSource> AdditionalModuleSources = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterAdditionalModule(IEmulatorCoreModule module)
    {
        lock (AdditionalModuleGate)
            AdditionalModules[module.Manifest.CoreId] = module;
    }

    public static void RegisterAdditionalModules(IEnumerable<IEmulatorCoreModule> modules)
    {
        lock (AdditionalModuleGate)
        {
            foreach (var module in modules)
                AdditionalModules[module.Manifest.CoreId] = module;
        }
    }

    public static IReadOnlyList<IEmulatorCoreModule> DiscoverModulesFromAssembly(Assembly assembly)
    {
        var types = GetLoadableTypes(assembly);
        return types
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IEmulatorCoreModule).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IEmulatorCoreModule)Activator.CreateInstance(type)!)
            .ToList();
    }

    public static IReadOnlyList<IEmulatorCoreModule> RegisterAdditionalModulesFromAssembly(Assembly assembly)
    {
        var modules = DiscoverModulesFromAssembly(assembly);
        RegisterAdditionalModules(modules);
        return modules;
    }

    public static IReadOnlyList<IEmulatorCoreModule> RegisterAdditionalModulesFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var modules = assemblies
            .SelectMany(DiscoverModulesFromAssembly)
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
        RegisterAdditionalModules(modules);
        return modules;
    }

    public static void RegisterAdditionalModuleSource(IEmulatorCoreModuleRegistrationSource source)
    {
        lock (AdditionalModuleGate)
            AdditionalModuleSources[source.SourceId] = source;
    }

    public static bool UnregisterAdditionalModuleSource(string sourceId)
    {
        lock (AdditionalModuleGate)
            return AdditionalModuleSources.Remove(sourceId);
    }

    public static bool UnregisterAdditionalModule(string coreId)
    {
        lock (AdditionalModuleGate)
            return AdditionalModules.Remove(coreId);
    }

    public static IReadOnlyList<IEmulatorCoreModule> CreateModules(IEnumerable<IEmulatorCoreModule>? additionalModules = null)
    {
        var modules = new Dictionary<string, IEmulatorCoreModule>(StringComparer.OrdinalIgnoreCase);

        lock (AdditionalModuleGate)
        {
            foreach (var source in AdditionalModuleSources.Values)
            {
                foreach (var module in source.LoadModules())
                    modules[module.Manifest.CoreId] = module;
            }

            foreach (var module in AdditionalModules.Values)
                modules[module.Manifest.CoreId] = module;
        }

        if (additionalModules != null)
        {
            foreach (var module in additionalModules)
                modules[module.Manifest.CoreId] = module;
        }

        return modules.Values.ToList();
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Cast<Type>().ToArray();
        }
    }
}
