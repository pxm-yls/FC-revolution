namespace FCRevolution.Storage;

public sealed class FileSystemObjectStorage : IObjectStorage
{
    private readonly IReadOnlyDictionary<ObjectStorageBucket, string> _bucketRoots;

    public FileSystemObjectStorage(IReadOnlyDictionary<ObjectStorageBucket, string> bucketRoots)
    {
        _bucketRoots = bucketRoots;
    }

    public string GetBucketRoot(ObjectStorageBucket bucket)
    {
        if (_bucketRoots.TryGetValue(bucket, out var root))
            return root;

        throw new InvalidOperationException($"未配置对象存储桶: {bucket}");
    }

    public string GetObjectPath(ObjectStorageBucket bucket, string objectKey)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        var bucketRoot = GetBucketRoot(bucket);
        var normalizedKey = objectKey
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        var combinedPath = Path.Combine(bucketRoot, normalizedKey);
        return EnsurePathInsideBucket(bucketRoot, combinedPath);
    }

    public string GetObjectKey(ObjectStorageBucket bucket, string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);

        var bucketRoot = GetBucketRoot(bucket);
        var normalizedPath = EnsurePathInsideBucket(bucketRoot, absolutePath);
        return Path.GetRelativePath(Path.GetFullPath(bucketRoot), normalizedPath);
    }

    public void EnsureBucket(ObjectStorageBucket bucket)
    {
        Directory.CreateDirectory(GetBucketRoot(bucket));
    }

    public void EnsureParentDirectory(ObjectStorageBucket bucket, string objectKey)
    {
        var parent = Path.GetDirectoryName(GetObjectPath(bucket, objectKey));
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
    }

    public IEnumerable<ObjectStorageEntry> Enumerate(
        ObjectStorageBucket bucket,
        string searchPattern,
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var root = GetBucketRoot(bucket);
        if (!Directory.Exists(root))
            yield break;

        foreach (var path in Directory.EnumerateFiles(root, searchPattern, searchOption))
        {
            yield return new ObjectStorageEntry(
                bucket,
                Path.GetRelativePath(root, path),
                path);
        }
    }

    private static string EnsurePathInsideBucket(string bucketRoot, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(bucketRoot));
        var normalizedPath = Path.GetFullPath(path);

        if (string.Equals(normalizedPath, normalizedRoot, GetPathComparison()))
            return normalizedPath;

        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        if (normalizedPath.StartsWith(rootWithSeparator, GetPathComparison()))
            return normalizedPath;

        throw new InvalidOperationException($"对象路径超出存储桶根目录边界: {normalizedPath}");
    }

    private static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
