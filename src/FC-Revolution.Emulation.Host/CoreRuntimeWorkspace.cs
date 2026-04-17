using FCRevolution.Storage;

namespace FCRevolution.Emulation.Host;

public sealed record CoreRuntimeWorkspaceOptions(
    string? ResourceRootPath = null,
    IReadOnlyList<string>? ProbeDirectories = null,
    string? PackagePath = null);

public sealed class CoreRuntimeWorkspace : IDisposable
{
    private readonly string? _cleanupDirectory;

    private CoreRuntimeWorkspace(
        string resourceRootPath,
        IReadOnlyList<string> probeDirectories,
        string? cleanupDirectory)
    {
        ResourceRootPath = resourceRootPath;
        ProbeDirectories = probeDirectories;
        RuntimeOptions = new ManagedCoreRuntimeOptions(
            ResourceRootPath: resourceRootPath,
            ProbeDirectories: probeDirectories);
        _cleanupDirectory = cleanupDirectory;
    }

    public string ResourceRootPath { get; }

    public IReadOnlyList<string> ProbeDirectories { get; }

    public ManagedCoreRuntimeOptions RuntimeOptions { get; }

    public static CoreRuntimeWorkspace Create(CoreRuntimeWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var probeDirectories = (options.ProbeDirectories ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(GetPathComparer())
            .ToList();

        if (!string.IsNullOrWhiteSpace(options.PackagePath))
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-core-workspace-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            new ManagedCorePackageService().InstallPackage(options.PackagePath, tempRoot);
            return new CoreRuntimeWorkspace(tempRoot, probeDirectories, cleanupDirectory: tempRoot);
        }

        var resourceRootPath = AppObjectStorage.NormalizeConfiguredResourceRoot(
            string.IsNullOrWhiteSpace(options.ResourceRootPath)
                ? AppObjectStorage.GetResourceRoot()
                : options.ResourceRootPath);
        return new CoreRuntimeWorkspace(resourceRootPath, probeDirectories, cleanupDirectory: null);
    }

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(_cleanupDirectory) || !Directory.Exists(_cleanupDirectory))
            return;

        try
        {
            Directory.Delete(_cleanupDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for isolated runtime workspaces.
        }
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
