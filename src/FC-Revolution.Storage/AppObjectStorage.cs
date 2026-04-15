namespace FCRevolution.Storage;

public static class AppObjectStorage
{
    private static readonly object SyncRoot = new();
    private static string _resourceRootPath = AppStorageRootResolver.GetDefaultResourceRoot();

    public static IObjectStorage Default => new FileSystemObjectStorage(
        AppStorageLayoutPolicy.CreateCurrentRoots(GetResourceRoot()));

    public static string GetDefaultResourceRoot(string? baseDirectory = null) =>
        AppStorageRootResolver.GetDefaultResourceRoot(baseDirectory);

    public static bool HasEnvironmentResourceRootOverride() =>
        AppStorageRootResolver.HasEnvironmentResourceRootOverride();

    public static string GetPortableResourceRoot(string? baseDirectory = null) =>
        AppStorageRootResolver.GetPortableResourceRoot(baseDirectory);

    public static string GetPortableModeMarkerPath(string? baseDirectory = null) =>
        AppStorageRootResolver.GetPortableModeMarkerPath(baseDirectory);

    public static bool IsPortableModeEnabled(string? baseDirectory = null) =>
        AppStorageRootResolver.IsPortableModeEnabled(baseDirectory);

    public static string NormalizeConfiguredResourceRoot(string? resourceRootPath, string? baseDirectory = null) =>
        AppStorageRootResolver.NormalizeConfiguredResourceRoot(resourceRootPath, baseDirectory);

    public static string GetResourceRoot()
    {
        lock (SyncRoot)
            return _resourceRootPath;
    }

    public static void ConfigureResourceRoot(string? resourceRootPath)
    {
        var normalized = NormalizeConfiguredResourceRoot(resourceRootPath);

        lock (SyncRoot)
            _resourceRootPath = normalized;
    }

    public static string GetBucketRoot(ObjectStorageBucket bucket) => Default.GetBucketRoot(bucket);

    public static string GetObjectKey(ObjectStorageBucket bucket, string absolutePath) =>
        Default.GetObjectKey(bucket, absolutePath);

    public static string GetRomsDirectory() => GetBucketRoot(ObjectStorageBucket.Roms);

    public static string GetPreviewVideosDirectory() => GetBucketRoot(ObjectStorageBucket.PreviewVideos);

    public static string GetLegacyPreviewVideosDirectory() =>
        Path.Combine(GetRomsDirectory(), ".previews");

    public static string GetConfigurationsDirectory() => GetBucketRoot(ObjectStorageBucket.Configurations);

    public static string GetSavesDirectory() => GetBucketRoot(ObjectStorageBucket.Saves);

    public static string GetImagesDirectory() => GetBucketRoot(ObjectStorageBucket.Images);

    public static string GetOtherResourcesDirectory() => GetBucketRoot(ObjectStorageBucket.Other);

    public static string GetSystemConfigPath() => GetSystemConfigPath(GetResourceRoot());

    public static string GetSystemConfigPath(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetSystemConfigPath(resourceRootPath);

    public static string GetBootstrapSystemConfigPath(string? baseDirectory = null) =>
        GetSystemConfigPath(GetDefaultResourceRoot(baseDirectory));

    public static string GetPortableSystemConfigPath(string? baseDirectory = null) =>
        GetSystemConfigPath(GetPortableResourceRoot(baseDirectory));

    public static string GetLegacyInstalledSystemConfigPath() =>
        GetSystemConfigPath(AppStorageRootResolver.GetLegacyInstalledResourceRoot());

    public static string GetLegacySystemConfigPath() =>
        Path.Combine(AppContext.BaseDirectory, "system.fcr");

    public static string GetInstanceIdPath() =>
        AppStorageLayoutPolicy.GetInstanceIdPath(GetResourceRoot());

    public static string GetRomProfilesDirectory() =>
        AppStorageLayoutPolicy.GetRomProfilesDirectory(GetResourceRoot());

    public static string GetRomProfilePath(string romPath) =>
        AppStorageLayoutPolicy.GetRomProfilePath(GetResourceRoot(), romPath);

    public static string GetLegacyRomProfilePath(string romPath) => Path.ChangeExtension(romPath, ".fcr");

    public static string GetTimelineRootDirectory() =>
        AppStorageLayoutPolicy.GetTimelineRootDirectory(GetResourceRoot());

    public static string GetManagedCoreModulesDirectory() =>
        GetManagedCoreModulesDirectory(GetResourceRoot());

    public static string GetManagedCoreModulesDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetManagedCoreModulesDirectory(resourceRootPath);

    public static string GetDevelopmentCoreModulesDirectory() =>
        GetDevelopmentCoreModulesDirectory(GetResourceRoot());

    public static string GetDevelopmentCoreModulesDirectory(string resourceRootPath) =>
        GetManagedCoreModulesDirectory(resourceRootPath);

    public static string GetCoresRootDirectory() =>
        GetCoresRootDirectory(GetResourceRoot());

    public static string GetCoresRootDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCoresRootDirectory(resourceRootPath);

    public static string GetInstalledCoreRootDirectory() =>
        GetInstalledCoreRootDirectory(GetResourceRoot());

    public static string GetInstalledCoreRootDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetInstalledCoreRootDirectory(resourceRootPath);

    public static string GetInstalledCoreVersionDirectory(string coreId, string version) =>
        GetInstalledCoreVersionDirectory(GetResourceRoot(), coreId, version);

    public static string GetInstalledCoreVersionDirectory(string resourceRootPath, string coreId, string version) =>
        AppStorageLayoutPolicy.GetInstalledCoreVersionDirectory(resourceRootPath, coreId, version);

    public static string GetCoreRegistryDirectory() =>
        GetCoreRegistryDirectory(GetResourceRoot());

    public static string GetCoreRegistryDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCoreRegistryDirectory(resourceRootPath);

    public static string GetCoreRegistryPath() =>
        GetCoreRegistryPath(GetResourceRoot());

    public static string GetCoreRegistryPath(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCoreRegistryPath(resourceRootPath);

    public static string GetCorePackagesDirectory() =>
        GetCorePackagesDirectory(GetResourceRoot());

    public static string GetCorePackagesDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCorePackagesDirectory(resourceRootPath);

    public static string GetCoreTempDirectory() =>
        GetCoreTempDirectory(GetResourceRoot());

    public static string GetCoreTempDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCoreTempDirectory(resourceRootPath);

    public static string GetCoreCacheDirectory() =>
        GetCoreCacheDirectory(GetResourceRoot());

    public static string GetCoreCacheDirectory(string resourceRootPath) =>
        AppStorageLayoutPolicy.GetCoreCacheDirectory(resourceRootPath);

    public static string GetRomObjectKey(string romPath) => AppStorageKeyPolicy.GetRomObjectKey(romPath);

    public static string GetPreviewVideoObjectKey(string romPath) =>
        AppStorageKeyPolicy.GetPreviewVideoObjectKey(romPath);

    public static string GetPreviewVideoObjectKey(string romPath, string sourcePath) =>
        AppStorageKeyPolicy.GetPreviewVideoObjectKey(romPath, sourcePath);

    public static string GetRomConfigObjectKey(string romPath) =>
        AppStorageKeyPolicy.GetRomConfigObjectKey(romPath);

    public static string GetRomImageObjectPrefix(string romPath) =>
        AppStorageKeyPolicy.GetRomImageObjectPrefix(romPath);

    public static string GetRomImageObjectKey(string romPath, string imageRole, string sourcePath) =>
        AppStorageKeyPolicy.GetRomImageObjectKey(romPath, imageRole, sourcePath);

    public static string GetSaveNamespace(string romPath) =>
        AppStorageKeyPolicy.GetSaveNamespace(romPath);

    public static string GetPreviewArtifactBasePath(string romPath) =>
        AppStorageLayoutPolicy.BuildPreviewArtifactBasePath(GetPreviewVideosDirectory(), romPath);

    public static string GetLegacyPreviewArtifactBasePath(string romPath) =>
        AppStorageLayoutPolicy.BuildPreviewArtifactBasePath(GetLegacyPreviewVideosDirectory(), romPath);

    public static void EnsureDefaults()
    {
        foreach (ObjectStorageBucket bucket in Enum.GetValues<ObjectStorageBucket>())
            Default.EnsureBucket(bucket);

        Directory.CreateDirectory(AppStorageLayoutPolicy.GetSystemConfigDirectory(GetResourceRoot()));
        Directory.CreateDirectory(GetRomProfilesDirectory());
        Directory.CreateDirectory(GetTimelineRootDirectory());
        Directory.CreateDirectory(GetInstalledCoreRootDirectory());
        Directory.CreateDirectory(GetCoreRegistryDirectory());
        Directory.CreateDirectory(GetCorePackagesDirectory());
        Directory.CreateDirectory(GetCoreTempDirectory());
        Directory.CreateDirectory(GetCoreCacheDirectory());
        Directory.CreateDirectory(GetDevelopmentCoreModulesDirectory());
    }

    public static string SanitizeFileName(string value) => AppStorageKeyPolicy.SanitizeFileName(value);
}
