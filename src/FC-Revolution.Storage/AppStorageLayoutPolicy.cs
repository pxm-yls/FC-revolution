namespace FCRevolution.Storage;

internal static class AppStorageLayoutPolicy
{
    private const string SystemConfigFileName = "system.fcr";
    private const string InstanceIdFileName = ".fc-revolution.instance";

    public static IReadOnlyDictionary<ObjectStorageBucket, string> CreateCurrentRoots(string resourceRoot)
    {
        return new Dictionary<ObjectStorageBucket, string>
        {
            [ObjectStorageBucket.Roms] = Path.Combine(resourceRoot, "roms"),
            [ObjectStorageBucket.PreviewVideos] = Path.Combine(resourceRoot, "previews"),
            [ObjectStorageBucket.Configurations] = Path.Combine(resourceRoot, "configs"),
            [ObjectStorageBucket.Saves] = Path.Combine(resourceRoot, "saves"),
            [ObjectStorageBucket.Images] = Path.Combine(resourceRoot, "images"),
            [ObjectStorageBucket.Other] = Path.Combine(resourceRoot, "objects")
        };
    }

    public static string GetSystemConfigPath(string resourceRootPath)
    {
        return GetConfigPath(resourceRootPath, "system", SystemConfigFileName);
    }

    public static string GetSystemConfigDirectory(string resourceRootPath)
    {
        return GetConfigDirectory(resourceRootPath, "system");
    }

    public static string GetInstanceIdPath(string resourceRootPath)
    {
        return GetConfigPath(resourceRootPath, "system", InstanceIdFileName);
    }

    public static string GetRomProfilesDirectory(string resourceRootPath)
    {
        return GetConfigDirectory(resourceRootPath, "rom-profiles");
    }

    public static string GetRomProfilePath(string resourceRootPath, string romPath)
    {
        return GetConfigPath(resourceRootPath, "rom-profiles", $"{AppStorageKeyPolicy.GetStablePathHash(romPath)}.fcr");
    }

    public static string GetTimelineRootDirectory(string resourceRootPath)
    {
        return Path.Combine(Path.Combine(resourceRootPath, "saves"), "timeline");
    }

    public static string GetManagedCoreModulesDirectory(string resourceRootPath)
    {
        return Path.Combine(resourceRootPath, "cores", "managed");
    }

    public static string GetCoresRootDirectory(string resourceRootPath)
    {
        return Path.Combine(resourceRootPath, "cores");
    }

    public static string GetInstalledCoreRootDirectory(string resourceRootPath)
    {
        return Path.Combine(GetCoresRootDirectory(resourceRootPath), "installed");
    }

    public static string GetInstalledCoreVersionDirectory(string resourceRootPath, string coreId, string version)
    {
        return Path.Combine(
            GetInstalledCoreRootDirectory(resourceRootPath),
            AppStorageKeyPolicy.SanitizeFileName(coreId),
            AppStorageKeyPolicy.SanitizeFileName(version));
    }

    public static string GetCoreRegistryDirectory(string resourceRootPath)
    {
        return Path.Combine(GetCoresRootDirectory(resourceRootPath), "registry");
    }

    public static string GetCoreRegistryPath(string resourceRootPath)
    {
        return Path.Combine(GetCoreRegistryDirectory(resourceRootPath), "core-registry.fcr");
    }

    public static string GetCorePackagesDirectory(string resourceRootPath)
    {
        return Path.Combine(GetCoresRootDirectory(resourceRootPath), "packages");
    }

    public static string GetCoreTempDirectory(string resourceRootPath)
    {
        return Path.Combine(GetCoresRootDirectory(resourceRootPath), "temp");
    }

    public static string GetCoreCacheDirectory(string resourceRootPath)
    {
        return Path.Combine(GetCoresRootDirectory(resourceRootPath), "cache");
    }

    public static string BuildPreviewArtifactBasePath(string root, string romPath)
    {
        var safeName = AppStorageKeyPolicy.SanitizeFileName(Path.GetFileNameWithoutExtension(romPath));
        var hash = AppStorageKeyPolicy.GetStablePathHash(romPath)[..12];
        return Path.Combine(root, $"{safeName}-{hash}");
    }

    private static string GetConfigDirectory(string resourceRootPath, string segment)
    {
        return Path.Combine(Path.Combine(resourceRootPath, "configs"), segment);
    }

    private static string GetConfigPath(string resourceRootPath, string segment, string fileName)
    {
        return Path.Combine(GetConfigDirectory(resourceRootPath, segment), fileName);
    }
}
