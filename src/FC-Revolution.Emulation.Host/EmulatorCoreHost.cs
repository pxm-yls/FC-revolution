using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

public sealed record CoreSessionLaunchRequest(string? CoreId = null, CoreSessionCreateOptions? CreateOptions = null);

public sealed class EmulatorCoreHost
{
    private readonly IReadOnlyDictionary<string, IManagedCoreModule> _managedModules;
    private readonly string? _defaultCoreId;

    public EmulatorCoreHost(IEnumerable<IManagedCoreModule> managedModules, string? defaultCoreId = null)
    {
        _managedModules = managedModules.ToDictionary(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase);
        _defaultCoreId = ResolveDefaultCoreId(defaultCoreId);
    }

    public bool HasInstalledCores => _managedModules.Count > 0;

    public string? DefaultCoreId => _defaultCoreId;

    public IReadOnlyList<CoreManifest> GetInstalledCoreManifests() =>
        _managedModules.Values.Select(module => module.Manifest).OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

    public IEmulatorCoreSession CreateSession(CoreSessionLaunchRequest request)
    {
        if (TryCreateSession(request, out var session))
            return session!;

        throw new InvalidOperationException("No emulator core modules are currently available.");
    }

    public bool TryCreateSession(CoreSessionLaunchRequest request, out IEmulatorCoreSession? session)
    {
        var module = ResolveModule(request.CoreId);
        if (module is null)
        {
            session = null;
            return false;
        }

        var preferredCoreId = string.IsNullOrWhiteSpace(request.CoreId)
            ? module.Manifest.CoreId
            : request.CoreId;
        session = module.CreateFactory().CreateSession(
            request.CreateOptions ?? new CoreSessionCreateOptions(preferredCoreId));
        return true;
    }

    private IManagedCoreModule? ResolveModule(string? requestedCoreId)
    {
        if (!string.IsNullOrWhiteSpace(requestedCoreId) &&
            _managedModules.TryGetValue(requestedCoreId, out var resolved))
            return resolved;

        if (!string.IsNullOrWhiteSpace(_defaultCoreId) &&
            _managedModules.TryGetValue(_defaultCoreId, out var defaultModule))
        {
            return defaultModule;
        }

        if (_managedModules.Count == 0)
            return null;

        return _managedModules.Values.First();
    }

    private string? ResolveDefaultCoreId(string? configuredDefaultCoreId)
    {
        if (!string.IsNullOrWhiteSpace(configuredDefaultCoreId) &&
            _managedModules.ContainsKey(configuredDefaultCoreId))
        {
            return configuredDefaultCoreId;
        }

        return _managedModules.Keys.FirstOrDefault();
    }
}

public static class DefaultEmulatorCoreHost
{
    public static EmulatorCoreHost Create() => Create(defaultCoreId: null);

    public static EmulatorCoreHost Create(string? defaultCoreId) =>
        Create(defaultCoreId, options: null, additionalModules: null);

    public static EmulatorCoreHost Create(string? defaultCoreId, ManagedCoreRuntimeOptions? options) =>
        Create(defaultCoreId, options, additionalModules: null);

    public static EmulatorCoreHost Create(string? defaultCoreId, IEnumerable<IManagedCoreModule>? additionalModules)
    {
        return Create(defaultCoreId, options: null, additionalModules);
    }

    public static EmulatorCoreHost Create(
        string? defaultCoreId,
        ManagedCoreRuntimeOptions? options,
        IEnumerable<IManagedCoreModule>? additionalModules)
        => ManagedCoreRuntime.CreateHost(defaultCoreId, options, additionalModules);
}
