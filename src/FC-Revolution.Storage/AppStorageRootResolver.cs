namespace FCRevolution.Storage;

internal static class AppStorageRootResolver
{
    private const string PortableModeMarkerFileName = ".fc-revolution-portable";
    private const string PortableResourceDirectoryName = "userdata";
    private const string LegacyInstalledResourceDirectoryName = "FC-Revolution";
    private const string ResourceRootOverrideEnvironmentVariableName = "FC_REVOLUTION_RESOURCE_ROOT";

    public static string GetDefaultResourceRoot(string? baseDirectory = null)
    {
        var environmentOverride = GetEnvironmentResourceRootOverride();
        if (environmentOverride != null)
            return environmentOverride;

        return GetPortableResourceRoot(baseDirectory);
    }

    public static bool HasEnvironmentResourceRootOverride() => GetEnvironmentResourceRootOverride() != null;

    public static string GetPortableResourceRoot(string? baseDirectory = null)
    {
        return Path.Combine(ResolveBaseDirectory(baseDirectory), PortableResourceDirectoryName);
    }

    public static string GetPortableModeMarkerPath(string? baseDirectory = null)
    {
        return Path.Combine(ResolveBaseDirectory(baseDirectory), PortableModeMarkerFileName);
    }

    public static bool IsPortableModeEnabled(string? baseDirectory = null)
    {
        return File.Exists(GetPortableModeMarkerPath(baseDirectory));
    }

    public static string NormalizeConfiguredResourceRoot(string? resourceRootPath, string? baseDirectory = null)
    {
        var defaultRoot = GetDefaultResourceRoot(baseDirectory);
        if (string.IsNullOrWhiteSpace(resourceRootPath))
            return defaultRoot;

        var normalized = Path.GetFullPath(resourceRootPath);
        return ShouldMigrateConfiguredResourceRoot(normalized)
            ? defaultRoot
            : normalized;
    }

    public static string GetLegacyInstalledResourceRoot()
    {
        if (OperatingSystem.IsMacOS())
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userHome))
                return Path.Combine(userHome, "Library", "Application Support", LegacyInstalledResourceDirectoryName);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(localAppData, LegacyInstalledResourceDirectoryName);

        var fallbackHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(fallbackHome))
            return Path.Combine(fallbackHome, ".local", "share", LegacyInstalledResourceDirectoryName);

        return Path.Combine(Path.GetTempPath(), LegacyInstalledResourceDirectoryName);
    }

    private static string ResolveBaseDirectory(string? baseDirectory)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory);
    }

    private static string? GetEnvironmentResourceRootOverride()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ResourceRootOverrideEnvironmentVariableName);
        return string.IsNullOrWhiteSpace(configuredPath)
            ? null
            : Path.GetFullPath(configuredPath);
    }

    private static bool ShouldMigrateConfiguredResourceRoot(string resourceRootPath)
    {
        return IsLegacyInstalledResourceRoot(resourceRootPath) || IsLegacyArtifactsResourceRoot(resourceRootPath);
    }

    private static bool IsLegacyInstalledResourceRoot(string resourceRootPath)
    {
        return PathsEqual(resourceRootPath, GetLegacyInstalledResourceRoot());
    }

    private static bool IsLegacyArtifactsResourceRoot(string resourceRootPath)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resourceRootPath));
        if (!string.Equals(Path.GetFileName(normalized), PortableResourceDirectoryName, GetPathComparison()))
            return false;

        var segments = normalized
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!string.Equals(segments[index], "artifacts", GetPathComparison()))
                continue;

            var nextSegment = segments[index + 1];
            if (string.Equals(nextSegment, "bin", GetPathComparison()) || string.Equals(nextSegment, "publish", GetPathComparison()))
                return true;
        }

        return false;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            GetPathComparison());
    }

    private static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
