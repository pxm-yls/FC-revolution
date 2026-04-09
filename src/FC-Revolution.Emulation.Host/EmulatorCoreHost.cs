using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

public sealed record CoreSessionLaunchRequest(string? CoreId = null, CoreSessionCreateOptions? CreateOptions = null);

public sealed class EmulatorCoreHost
{
    private readonly IReadOnlyDictionary<string, IManagedCoreModule> _managedModules;
    private readonly string _defaultCoreId;

    public EmulatorCoreHost(IEnumerable<IManagedCoreModule> managedModules, string? defaultCoreId = null)
    {
        _managedModules = managedModules.ToDictionary(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase);
        if (_managedModules.Count == 0)
            throw new ArgumentException("At least one managed core module is required.", nameof(managedModules));

        _defaultCoreId = ResolveDefaultCoreId(defaultCoreId);
    }

    public IReadOnlyList<CoreManifest> GetInstalledCoreManifests() =>
        _managedModules.Values.Select(module => module.Manifest).OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

    public IEmulatorCoreSession CreateSession(CoreSessionLaunchRequest request)
    {
        var coreId = request.CoreId;
        var module = ResolveModule(coreId);
        return module.CreateFactory().CreateSession(request.CreateOptions ?? new CoreSessionCreateOptions(coreId));
    }

    private IManagedCoreModule ResolveModule(string? requestedCoreId)
    {
        if (!string.IsNullOrWhiteSpace(requestedCoreId) &&
            _managedModules.TryGetValue(requestedCoreId, out var resolved))
            return resolved;

        if (_managedModules.TryGetValue(_defaultCoreId, out var defaultModule))
            return defaultModule;

        return _managedModules.Values.First();
    }

    private string ResolveDefaultCoreId(string? configuredDefaultCoreId)
    {
        if (!string.IsNullOrWhiteSpace(configuredDefaultCoreId) &&
            _managedModules.ContainsKey(configuredDefaultCoreId))
        {
            return configuredDefaultCoreId;
        }

        if (_managedModules.ContainsKey(DefaultEmulatorCoreHost.DefaultCoreId))
            return DefaultEmulatorCoreHost.DefaultCoreId;

        return _managedModules.Keys.First();
    }
}

public static class DefaultEmulatorCoreHost
{
    public const string DefaultCoreId = DefaultManagedCoreModuleCatalog.DefaultCoreId;

    public static EmulatorCoreHost Create() => Create(DefaultCoreId);

    public static EmulatorCoreHost Create(string? defaultCoreId) =>
        Create(defaultCoreId, additionalModules: null);

    public static EmulatorCoreHost Create(string? defaultCoreId, IEnumerable<IManagedCoreModule>? additionalModules) =>
        new(DefaultManagedCoreModuleCatalog.CreateModules(additionalModules), defaultCoreId);
}
