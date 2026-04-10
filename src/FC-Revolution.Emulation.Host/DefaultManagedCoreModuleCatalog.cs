using FCRevolution.Emulation.Abstractions;
using System.Reflection;

namespace FCRevolution.Emulation.Host;

public static class DefaultManagedCoreModuleCatalog
{
    private static readonly Lock AdditionalModuleGate = new();
    private static readonly Dictionary<string, IManagedCoreModule> AdditionalModules = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IManagedCoreModuleRegistrationSource> AdditionalModuleSources = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterAdditionalModule(IManagedCoreModule module)
    {
        lock (AdditionalModuleGate)
            AdditionalModules[module.Manifest.CoreId] = module;
    }

    public static void RegisterAdditionalModules(IEnumerable<IManagedCoreModule> modules)
    {
        lock (AdditionalModuleGate)
        {
            foreach (var module in modules)
                AdditionalModules[module.Manifest.CoreId] = module;
        }
    }

    public static IReadOnlyList<IManagedCoreModule> DiscoverModulesFromAssembly(Assembly assembly)
    {
        var types = GetLoadableTypes(assembly);
        return types
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IManagedCoreModule).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IManagedCoreModule)Activator.CreateInstance(type)!)
            .ToList();
    }

    public static IReadOnlyList<IManagedCoreModule> RegisterAdditionalModulesFromAssembly(Assembly assembly)
    {
        var modules = DiscoverModulesFromAssembly(assembly);
        RegisterAdditionalModules(modules);
        return modules;
    }

    public static IReadOnlyList<IManagedCoreModule> RegisterAdditionalModulesFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var modules = assemblies
            .SelectMany(DiscoverModulesFromAssembly)
            .GroupBy(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
        RegisterAdditionalModules(modules);
        return modules;
    }

    public static void RegisterAdditionalModuleSource(IManagedCoreModuleRegistrationSource source)
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

    public static IReadOnlyList<IManagedCoreModule> CreateModules(IEnumerable<IManagedCoreModule>? additionalModules = null)
    {
        var modules = new Dictionary<string, IManagedCoreModule>(StringComparer.OrdinalIgnoreCase);

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
