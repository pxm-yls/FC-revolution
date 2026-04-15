using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

public sealed record CoreSessionLaunchRequest(string? CoreId = null, CoreSessionCreateOptions? CreateOptions = null);

public sealed class EmulatorCoreHost
{
    private readonly IReadOnlyDictionary<string, IEmulatorCoreModule> _modules;
    private readonly string? _defaultCoreId;

    public EmulatorCoreHost(IEnumerable<IEmulatorCoreModule> modules, string? defaultCoreId = null)
    {
        _modules = modules.ToDictionary(module => module.Manifest.CoreId, StringComparer.OrdinalIgnoreCase);
        _defaultCoreId = ResolveDefaultCoreId(defaultCoreId);
    }

    public bool HasInstalledCores => _modules.Count > 0;

    public string? DefaultCoreId => _defaultCoreId;

    public IReadOnlyList<CoreManifest> GetInstalledCoreManifests() =>
        _modules.Values.Select(module => module.Manifest).OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

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

    private IEmulatorCoreModule? ResolveModule(string? requestedCoreId)
    {
        if (!string.IsNullOrWhiteSpace(requestedCoreId) &&
            _modules.TryGetValue(requestedCoreId, out var resolved))
            return resolved;

        if (!string.IsNullOrWhiteSpace(_defaultCoreId) &&
            _modules.TryGetValue(_defaultCoreId, out var defaultModule))
        {
            return defaultModule;
        }

        if (_modules.Count == 0)
            return null;

        return _modules.Values.First();
    }

    private string? ResolveDefaultCoreId(string? configuredDefaultCoreId)
    {
        if (!string.IsNullOrWhiteSpace(configuredDefaultCoreId) &&
            _modules.ContainsKey(configuredDefaultCoreId))
        {
            return configuredDefaultCoreId;
        }

        return _modules.Keys.FirstOrDefault();
    }
}

public static class DefaultEmulatorCoreHost
{
    public static EmulatorCoreHost Create() => Create(defaultCoreId: null);

    public static EmulatorCoreHost Create(string? defaultCoreId) =>
        Create(defaultCoreId, options: null, additionalModules: null);

    public static EmulatorCoreHost Create(string? defaultCoreId, ManagedCoreRuntimeOptions? options) =>
        Create(defaultCoreId, options, additionalModules: null);

    public static EmulatorCoreHost Create(string? defaultCoreId, IEnumerable<IEmulatorCoreModule>? additionalModules)
    {
        return Create(defaultCoreId, options: null, additionalModules);
    }

    public static EmulatorCoreHost Create(
        string? defaultCoreId,
        ManagedCoreRuntimeOptions? options,
        IEnumerable<IEmulatorCoreModule>? additionalModules)
        => ManagedCoreRuntime.CreateHost(defaultCoreId, options, additionalModules);
}
