using System.Security.Cryptography;
using System.Text;

namespace FCRevolution.Storage;

public static class TimelineStoragePaths
{
    public static string GetStorageRoot() => AppObjectStorage.GetTimelineRootDirectory();

    public static string ComputeRomId(string romPath)
    {
        using var stream = File.OpenRead(romPath);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(stream, hash);
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }

    public static Guid GetStableMainBranchId(string romId) => CreateStableGuid($"branch:main:{romId}");

    public static string GetRomDirectory(string romId) => Path.Combine(GetStorageRoot(), romId);

    public static string GetManifestPath(string romId) => Path.Combine(GetRomDirectory(romId), "manifest.json");

    public static DateTime ReadManifestWriteTimeUtc(string romId)
    {
        var manifestPath = GetManifestPath(romId);
        return File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
    }

    public static string GetBranchDirectory(string romId, Guid branchId) =>
        Path.Combine(GetRomDirectory(romId), "branches", branchId.ToString("N"));

    public static string GetQuickSavePath(string romId, Guid branchId) =>
        Path.Combine(GetBranchDirectory(romId, branchId), "quicksave.fcsnap");

    public static string GetInputLogPath(string romId, Guid branchId) =>
        Path.Combine(GetBranchDirectory(romId, branchId), "inputlog.bin");

    public static string GetSnapshotsDirectory(string romId, Guid branchId) =>
        Path.Combine(GetBranchDirectory(romId, branchId), "snapshots");

    public static string GetBranchSnapshotPath(string romId, Guid branchId) =>
        Path.Combine(GetSnapshotsDirectory(romId, branchId), $"{branchId:N}.fcsnap");

    public static string GetPreviewNodesDirectory(string romId) =>
        Path.Combine(GetRomDirectory(romId), "preview-nodes");

    public static string GetPreviewNodeSnapshotPath(string romId, Guid previewNodeId) =>
        Path.Combine(GetPreviewNodesDirectory(romId), $"{previewNodeId:N}.fcsnap");

    public static string GetExportsDirectory(string romId, Guid branchId) =>
        Path.Combine(GetBranchDirectory(romId, branchId), "exports");

    public static string GetExportPath(string romId, Guid branchId, long startFrame, long endFrame) =>
        Path.Combine(GetExportsDirectory(romId, branchId), $"export-{startFrame}-{endFrame}.mp4");

    public static void EnsureBranchDirectory(string romId, Guid branchId)
    {
        Directory.CreateDirectory(GetBranchDirectory(romId, branchId));
        Directory.CreateDirectory(GetSnapshotsDirectory(romId, branchId));
        Directory.CreateDirectory(GetExportsDirectory(romId, branchId));
    }

    public static void EnsurePreviewNodesDirectory(string romId)
    {
        Directory.CreateDirectory(GetPreviewNodesDirectory(romId));
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
