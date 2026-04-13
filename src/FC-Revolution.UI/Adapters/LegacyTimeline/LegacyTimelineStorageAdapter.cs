using System;
using FCRevolution.Core.FC.LegacyAdapters;

namespace FC_Revolution.UI.Adapters.LegacyTimeline;

internal static class LegacyTimelineStorageAdapter
{
    public static string ComputeRomId(string romPath) => LegacyTimelineStorage.ComputeRomId(romPath);

    public static Guid GetStableMainBranchId(string romId) => LegacyTimelineStorage.GetStableMainBranchId(romId);

    public static void EnsureBranchDirectory(string romId, Guid branchId) =>
        LegacyTimelineStorage.EnsureBranchDirectory(romId, branchId);

    public static string GetQuickSavePath(string romId, Guid branchId) =>
        LegacyTimelineStorage.GetQuickSavePath(romId, branchId);

    public static string GetInputLogPath(string romId, Guid branchId) =>
        LegacyTimelineStorage.GetInputLogPath(romId, branchId);

    public static string GetExportPath(string romId, Guid branchId, long startFrame, long endFrame) =>
        LegacyTimelineStorage.GetExportPath(romId, branchId, startFrame, endFrame);

    public static string GetBranchSnapshotPath(string romId, Guid branchId) =>
        LegacyTimelineStorage.GetBranchSnapshotPath(romId, branchId);

    public static string GetRomDirectory(string romId) => LegacyTimelineStorage.GetRomDirectory(romId);

    public static DateTime ReadManifestWriteTimeUtc(string romId) => LegacyTimelineStorage.ReadManifestWriteTimeUtc(romId);
}
