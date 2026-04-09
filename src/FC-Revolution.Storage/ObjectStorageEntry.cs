namespace FCRevolution.Storage;

public sealed record ObjectStorageEntry(
    ObjectStorageBucket Bucket,
    string ObjectKey,
    string AbsolutePath);
