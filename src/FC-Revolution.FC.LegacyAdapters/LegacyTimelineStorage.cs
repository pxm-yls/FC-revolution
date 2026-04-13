using FCRevolution.Core.Timeline.Persistence;

namespace FCRevolution.FC.LegacyAdapters;

public static class LegacyTimelineStorage
{
    public static string ComputeRomId(string romPath) => TimelineStoragePaths.ComputeRomId(romPath);

    public static Guid GetStableMainBranchId(string romId) => TimelineStoragePaths.GetStableMainBranchId(romId);

    public static void EnsureBranchDirectory(string romId, Guid branchId) =>
        TimelineStoragePaths.EnsureBranchDirectory(romId, branchId);

    public static string GetQuickSavePath(string romId, Guid branchId) =>
        TimelineStoragePaths.GetQuickSavePath(romId, branchId);

    public static string GetInputLogPath(string romId, Guid branchId) =>
        TimelineStoragePaths.GetInputLogPath(romId, branchId);

    public static string GetExportPath(string romId, Guid branchId, long startFrame, long endFrame) =>
        TimelineStoragePaths.GetExportPath(romId, branchId, startFrame, endFrame);

    public static string GetBranchSnapshotPath(string romId, Guid branchId) =>
        TimelineStoragePaths.GetBranchSnapshotPath(romId, branchId);

    public static string GetRomDirectory(string romId) => TimelineStoragePaths.GetRomDirectory(romId);

    public static DateTime ReadManifestWriteTimeUtc(string romId)
    {
        var manifestPath = TimelineStoragePaths.GetManifestPath(romId);
        return File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
    }
}
