using FCRevolution.Storage;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class AppObjectStorageTests
{
    [Fact]
    public void FileSystemObjectStorage_GetObjectPath_RejectsBucketEscapeTraversal()
    {
        var bucketRoot = CreateTempBaseDirectory();
        try
        {
            var storage = CreateStorage(bucketRoot);
            var escapedKey = Path.Combine("roms", "..", "..", "outside.bin");

            Assert.Throws<InvalidOperationException>(() =>
                storage.GetObjectPath(ObjectStorageBucket.Roms, escapedKey));
        }
        finally
        {
            SafeDeleteDirectory(bucketRoot);
        }
    }

    [Fact]
    public void FileSystemObjectStorage_GetObjectKey_RejectsAbsolutePathOutsideBucket()
    {
        var bucketRoot = CreateTempBaseDirectory();
        var outsideRoot = CreateTempBaseDirectory();
        try
        {
            var storage = CreateStorage(bucketRoot);
            var outsideFile = Path.Combine(outsideRoot, "outside.bin");

            Assert.Throws<InvalidOperationException>(() =>
                storage.GetObjectKey(ObjectStorageBucket.Roms, outsideFile));
        }
        finally
        {
            SafeDeleteDirectory(bucketRoot);
            SafeDeleteDirectory(outsideRoot);
        }
    }

    [Fact]
    public void FileSystemObjectStorage_ObjectPathAndKey_RoundTripWithinBucket()
    {
        var bucketRoot = CreateTempBaseDirectory();
        try
        {
            var storage = CreateStorage(bucketRoot);
            const string objectKey = "covers/super-mario.png";

            var objectPath = storage.GetObjectPath(ObjectStorageBucket.Roms, objectKey);
            var roundTripKey = storage.GetObjectKey(ObjectStorageBucket.Roms, objectPath);

            Assert.Equal(
                objectKey.Replace('/', Path.DirectorySeparatorChar),
                roundTripKey);
        }
        finally
        {
            SafeDeleteDirectory(bucketRoot);
        }
    }

    [Fact]
    public void GetDefaultResourceRoot_UsesEnvironmentOverrideWhenPresent()
    {
        var original = Environment.GetEnvironmentVariable("FC_REVOLUTION_RESOURCE_ROOT");
        var overrideRoot = CreateTempBaseDirectory();

        try
        {
            Environment.SetEnvironmentVariable("FC_REVOLUTION_RESOURCE_ROOT", overrideRoot);

            Assert.Equal(Path.GetFullPath(overrideRoot), AppObjectStorage.GetDefaultResourceRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FC_REVOLUTION_RESOURCE_ROOT", original);
            SafeDeleteDirectory(overrideRoot);
        }
    }

    [Fact]
    public void GetDefaultResourceRoot_UsesApplicationBaseDirectoryByDefault()
    {
        var baseDirectory = CreateTempBaseDirectory();

        try
        {
            Assert.Equal(
                Path.Combine(Path.GetFullPath(baseDirectory), "userdata"),
                AppObjectStorage.GetDefaultResourceRoot(baseDirectory));
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void GetDefaultResourceRoot_RemainsApplicationRelativeWhenPortableMarkerExists()
    {
        var baseDirectory = CreateTempBaseDirectory();

        try
        {
            Directory.CreateDirectory(baseDirectory);
            File.WriteAllText(AppObjectStorage.GetPortableModeMarkerPath(baseDirectory), "");

            Assert.Equal(
                AppObjectStorage.GetPortableResourceRoot(baseDirectory),
                AppObjectStorage.GetDefaultResourceRoot(baseDirectory));
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void NormalizeConfiguredResourceRoot_MigratesLegacyArtifactsRootWhenPortableModeDisabled()
    {
        var baseDirectory = CreateTempBaseDirectory();

        try
        {
            var legacyArtifactsRoot = Path.Combine(
                baseDirectory,
                "artifacts",
                "bin",
                "FC-Revolution.UI",
                "Debug",
                "net10.0",
                "userdata");

            Assert.Equal(
                AppObjectStorage.GetPortableResourceRoot(baseDirectory),
                AppObjectStorage.NormalizeConfiguredResourceRoot(legacyArtifactsRoot, baseDirectory));
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void NormalizeConfiguredResourceRoot_MigratesLegacyInstalledRootToApplicationDirectory()
    {
        var baseDirectory = CreateTempBaseDirectory();

        try
        {
            Assert.Equal(
                AppObjectStorage.GetPortableResourceRoot(baseDirectory),
                AppObjectStorage.NormalizeConfiguredResourceRoot(GetExpectedLegacyInstalledResourceRoot(), baseDirectory));
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void NormalizeConfiguredResourceRoot_PreservesExplicitCustomRoot()
    {
        var baseDirectory = CreateTempBaseDirectory();

        try
        {
            var customRoot = Path.Combine(baseDirectory, "custom-root");

            Assert.Equal(
                Path.GetFullPath(customRoot),
                AppObjectStorage.NormalizeConfiguredResourceRoot(customRoot, baseDirectory));
        }
        finally
        {
            SafeDeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void AppObjectStorage_GeneratedObjectKeys_RoundTripThroughStoragePaths()
    {
        var resourceRoot = CreateTempBaseDirectory();
        var previousRoot = AppObjectStorage.GetResourceRoot();
        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            string romPath = Path.Combine(resourceRoot, "imports", "Contra.nes");
            string coverSourcePath = Path.Combine(resourceRoot, "sources", "cover.final.PNG");
            string previewSourcePath = Path.Combine(resourceRoot, "sources", "preview.MOV");

            string romObjectKey = AppObjectStorage.GetRomObjectKey(romPath);
            string previewObjectKey = AppObjectStorage.GetPreviewVideoObjectKey(romPath, previewSourcePath);
            string configObjectKey = AppObjectStorage.GetRomConfigObjectKey(romPath);
            string imageObjectKey = AppObjectStorage.GetRomImageObjectKey(romPath, "cover-art", coverSourcePath);

            AssertRoundTrip(ObjectStorageBucket.Roms, romObjectKey);
            AssertRoundTrip(ObjectStorageBucket.PreviewVideos, previewObjectKey);
            AssertRoundTrip(ObjectStorageBucket.Configurations, configObjectKey);
            AssertRoundTrip(ObjectStorageBucket.Images, imageObjectKey);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
        }
    }

    [Fact]
    public void AppObjectStorage_DefaultStorage_ResolvesPathsWithinEachBucketRoot()
    {
        var resourceRoot = CreateTempBaseDirectory();
        var previousRoot = AppObjectStorage.GetResourceRoot();
        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            AssertPathContainedInBucket(ObjectStorageBucket.Roms, "nested/rom.nes");
            AssertPathContainedInBucket(ObjectStorageBucket.PreviewVideos, "preview/movie.mp4");
            AssertPathContainedInBucket(ObjectStorageBucket.Configurations, "rom-profiles/a.fcr");
            AssertPathContainedInBucket(ObjectStorageBucket.Saves, "timeline/session/frame.bin");
            AssertPathContainedInBucket(ObjectStorageBucket.Images, "roms/a/cover.png");
            AssertPathContainedInBucket(ObjectStorageBucket.Other, "misc/data.bin");
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
        }
    }

    [Fact]
    public void AppObjectStorage_GetObjectKey_RejectsPathOutsideConfiguredBucket()
    {
        var resourceRoot = CreateTempBaseDirectory();
        var previousRoot = AppObjectStorage.GetResourceRoot();
        var outsideRoot = CreateTempBaseDirectory();
        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            AppObjectStorage.EnsureDefaults();

            string outsidePath = Path.Combine(outsideRoot, "not-in-bucket.bin");
            Assert.Throws<InvalidOperationException>(() =>
                AppObjectStorage.GetObjectKey(ObjectStorageBucket.PreviewVideos, outsidePath));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
            SafeDeleteDirectory(outsideRoot);
        }
    }

    private static string GetExpectedLegacyInstalledResourceRoot()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "FC-Revolution");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(localAppData, "FC-Revolution");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "FC-Revolution");
    }

    private static string CreateTempBaseDirectory()
    {
        return Path.Combine(Path.GetTempPath(), $"fc-appobjectstorage-{Guid.NewGuid():N}");
    }

    private static FileSystemObjectStorage CreateStorage(string bucketRoot)
    {
        return new FileSystemObjectStorage(new Dictionary<ObjectStorageBucket, string>
        {
            [ObjectStorageBucket.Roms] = bucketRoot,
            [ObjectStorageBucket.PreviewVideos] = bucketRoot,
            [ObjectStorageBucket.Configurations] = bucketRoot,
            [ObjectStorageBucket.Saves] = bucketRoot,
            [ObjectStorageBucket.Images] = bucketRoot,
            [ObjectStorageBucket.Other] = bucketRoot
        });
    }

    private static void AssertRoundTrip(ObjectStorageBucket bucket, string objectKey)
    {
        string objectPath = AppObjectStorage.Default.GetObjectPath(bucket, objectKey);
        string roundTripKey = AppObjectStorage.GetObjectKey(bucket, objectPath);

        Assert.Equal(
            objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar),
            roundTripKey);
    }

    private static void AssertPathContainedInBucket(ObjectStorageBucket bucket, string objectKey)
    {
        string bucketRoot = Path.TrimEndingDirectorySeparator(AppObjectStorage.GetBucketRoot(bucket));
        string objectPath = AppObjectStorage.Default.GetObjectPath(bucket, objectKey);
        string normalizedPath = Path.GetFullPath(objectPath);

        Assert.StartsWith(
            bucketRoot + Path.DirectorySeparatorChar,
            normalizedPath + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
