using StorageTimelineStoragePaths = FCRevolution.Storage.TimelineStoragePaths;

namespace FCRevolution.Core.Timeline.Persistence;

public static class TimelineStoragePaths
{
    public static string GetStorageRoot() => StorageTimelineStoragePaths.GetStorageRoot();

    public static string ComputeRomId(string romPath) => StorageTimelineStoragePaths.ComputeRomId(romPath);

    public static Guid GetStableMainBranchId(string romId) => StorageTimelineStoragePaths.GetStableMainBranchId(romId);

    public static string GetRomDirectory(string romId) => StorageTimelineStoragePaths.GetRomDirectory(romId);

    public static string GetManifestPath(string romId) => StorageTimelineStoragePaths.GetManifestPath(romId);

    public static DateTime ReadManifestWriteTimeUtc(string romId) => StorageTimelineStoragePaths.ReadManifestWriteTimeUtc(romId);

    public static string GetBranchDirectory(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetBranchDirectory(romId, branchId);

    public static string GetQuickSavePath(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetQuickSavePath(romId, branchId);

    public static string GetInputLogPath(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetInputLogPath(romId, branchId);

    public static string GetSnapshotsDirectory(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetSnapshotsDirectory(romId, branchId);

    public static string GetBranchSnapshotPath(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetBranchSnapshotPath(romId, branchId);

    public static string GetPreviewNodesDirectory(string romId) =>
        StorageTimelineStoragePaths.GetPreviewNodesDirectory(romId);

    public static string GetPreviewNodeSnapshotPath(string romId, Guid previewNodeId) =>
        StorageTimelineStoragePaths.GetPreviewNodeSnapshotPath(romId, previewNodeId);

    public static string GetExportsDirectory(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.GetExportsDirectory(romId, branchId);

    public static string GetExportPath(string romId, Guid branchId, long startFrame, long endFrame) =>
        StorageTimelineStoragePaths.GetExportPath(romId, branchId, startFrame, endFrame);

    public static void EnsureBranchDirectory(string romId, Guid branchId) =>
        StorageTimelineStoragePaths.EnsureBranchDirectory(romId, branchId);

    public static void EnsurePreviewNodesDirectory(string romId) =>
        StorageTimelineStoragePaths.EnsurePreviewNodesDirectory(romId);
}
