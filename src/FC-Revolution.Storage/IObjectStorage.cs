namespace FCRevolution.Storage;

public interface IObjectStorage
{
    string GetBucketRoot(ObjectStorageBucket bucket);

    string GetObjectPath(ObjectStorageBucket bucket, string objectKey);

    string GetObjectKey(ObjectStorageBucket bucket, string absolutePath);

    void EnsureBucket(ObjectStorageBucket bucket);

    void EnsureParentDirectory(ObjectStorageBucket bucket, string objectKey);

    IEnumerable<ObjectStorageEntry> Enumerate(
        ObjectStorageBucket bucket,
        string searchPattern,
        SearchOption searchOption = SearchOption.TopDirectoryOnly);
}
